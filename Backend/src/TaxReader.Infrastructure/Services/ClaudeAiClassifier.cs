using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

public class ClaudeAiClassifier(
    HttpClient httpClient,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeAiClassifier> logger) : IAiClassifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AnthropicOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<AiClassificationResult>> ClassifyBatchAsync(
        IReadOnlyList<string> itemDescriptions,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Anthropic API key is not configured.");

        if (itemDescriptions.Count == 0)
            return [];

        var prompt = BuildBatchPrompt(itemDescriptions);

        // Output budget: ~80 tokens per JSON entry is generous (category + reason + confidence).
        // Floor of 256 keeps single-item calls within the model's natural envelope.
        var maxTokens = Math.Max(256, itemDescriptions.Count * 100);

        var request = new ClaudeRequest(
            Model: _options.Model,
            MaxTokens: maxTokens,
            System: SystemPrompt,
            Messages: [new ClaudeMessage("user", prompt)]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Add("x-api-key", _options.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Anthropic API returned {Status}: {Body}", response.StatusCode, body);
            throw new HttpRequestException(ExtractFriendlyError(response.StatusCode, body));
        }

        var payload = await response.Content.ReadFromJsonAsync<ClaudeResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty response from Anthropic API.");

        var text = string.Concat(
            payload.Content?
                .Where(b => b.Type == "text" && b.Text is not null)
                .Select(b => b.Text) ?? []);

        return ParseBatchResult(text, itemDescriptions.Count);
    }

    private const string SystemPrompt = """
        Du bist ein Assistent, der Ausgaben für die Steuererklärung klassifiziert.
        Der Nutzer kann Arbeitnehmer, Freiberufler oder Selbstständiger sein.
        Du musst jeden Artikel in GENAU eine der folgenden Kategorien einordnen:

        - ConsumablesAndOfficeSupplies: Bürobedarf und Verbrauchsmaterial (Tinte, Papier, Stifte, Ordner, Druckerpapier, Briefumschläge, Toner)
        - SpecialistLiterature: Fachbücher, Fachliteratur, Fachzeitschriften, E-Books mit beruflichem Bezug
        - TeachingMaterials: Lehr- und Arbeitsmaterialien (Arbeitsblätter, Laminierfolien, Flipcharts, Präsentationsmaterial, Schulungsmaterial)
        - DigitalToolsAndSoftware: Software, Lizenzen, Apps, Cloud-Dienste, Domains, Hosting, USB-Sticks, digitale Werkzeuge
        - OfficeEquipment: Büroausstattung und Arbeitsmittel (Drucker, Monitor, Tastatur, Maus, Headset, Webcam, Schreibtisch, Bürostuhl, Laminator)
        - TravelAndCommuting: Fahrtkosten, Dienstreisen, Pendlerpauschale (Tickets, Benzin, Hotels, Parkgebühren, Maut)
        - ProfessionalDevelopment: Fortbildung, Seminare, Kurse, Workshops, Konferenzen, Zertifizierungen, Fachmessen
        - Unknown: Nur wenn wirklich keine berufliche Kategorie passt (z.B. rein private Artikel wie Lebensmittel, Kleidung)

        Antworte AUSSCHLIESSLICH mit gültigem JSON-Array (ohne Markdown Code-Blöcke). Pro Artikel ein Objekt
        in der gleichen Reihenfolge wie die Eingabe:
        [
          {"category": "CategoryName", "reason": "Kurze Begründung in einem Satz", "confidence": 0.95},
          ...
        ]

        confidence ist ein Wert zwischen 0.0 und 1.0.
        """;

    private static string BuildBatchPrompt(IReadOnlyList<string> descriptions)
    {
        var sb = new StringBuilder(256 + descriptions.Count * 64);
        sb.Append("Klassifiziere folgende ");
        sb.Append(descriptions.Count);
        sb.Append(" Artikel. Antworte mit einem JSON-Array, ein Objekt pro Artikel, in der gleichen Reihenfolge:\n\n");
        for (var i = 0; i < descriptions.Count; i++)
        {
            sb.Append(i + 1);
            sb.Append(". \"");
            sb.Append(descriptions[i]);
            sb.Append("\"\n");
        }
        return sb.ToString();
    }

    private static string ExtractFriendlyError(System.Net.HttpStatusCode status, string body)
    {
        // Anthropic returns: {"type":"error","error":{"type":"...","message":"..."}}
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg) &&
                msg.GetString() is { Length: > 0 } message)
            {
                return message;
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return $"Anthropic API {status}";
    }

    /// <summary>
    /// Always returns exactly <paramref name="expectedCount"/> entries — items the
    /// model didn't return (truncation, malformed JSON, parsing error) are filled
    /// with <see cref="Category.Unknown"/> so callers can handle the partial result
    /// uniformly (e.g. refund tokens for missing entries).
    /// </summary>
    private IReadOnlyList<AiClassificationResult> ParseBatchResult(string text, int expectedCount)
    {
        var fallback = Enumerable.Repeat(
            new AiClassificationResult(Category.Unknown, "AI-Antwort konnte nicht geparst werden.", 0),
            expectedCount).ToArray();

        var cleaned = StripMarkdownFences(text.Trim());

        // Isolate the JSON array — be tolerant of leading/trailing prose.
        var start = cleaned.IndexOf('[');
        var end = cleaned.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            logger.LogWarning("AI batch response did not contain a JSON array. Raw: {Raw}", text);
            return fallback;
        }

        var json = cleaned[start..(end + 1)];

        AiResponsePayload[]? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AiResponsePayload[]>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "AI batch response was not valid JSON. Raw: {Raw}", text);
            return fallback;
        }

        if (parsed is null || parsed.Length == 0)
            return fallback;

        var results = new AiClassificationResult[expectedCount];
        for (var i = 0; i < expectedCount; i++)
        {
            if (i >= parsed.Length || parsed[i] is null)
            {
                results[i] = new AiClassificationResult(
                    Category.Unknown,
                    "AI hat dieses Item nicht klassifiziert.",
                    0);
                continue;
            }

            var p = parsed[i];
            var category = Enum.TryParse<Category>(p.Category, ignoreCase: true, out var c)
                ? c
                : Category.Unknown;
            results[i] = new AiClassificationResult(category, p.Reason ?? "", p.Confidence);
        }

        if (parsed.Length != expectedCount)
        {
            logger.LogWarning(
                "AI batch returned {Returned} entries for {Expected} items — missing entries filled with Unknown.",
                parsed.Length, expectedCount);
        }

        return results;
    }

    private static string StripMarkdownFences(string s)
    {
        if (!s.StartsWith("```")) return s;
        var firstNewline = s.IndexOf('\n');
        if (firstNewline >= 0) s = s[(firstNewline + 1)..];
        var lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence >= 0) s = s[..lastFence];
        return s.Trim();
    }

    private record ClaudeRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] IReadOnlyList<ClaudeMessage> Messages);

    private record ClaudeMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record ClaudeResponse(
        [property: JsonPropertyName("content")] IReadOnlyList<ClaudeContentBlock>? Content);

    private record ClaudeContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);

    private record AiResponsePayload(
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("confidence")] double Confidence);
}
