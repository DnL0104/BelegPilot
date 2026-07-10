using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

public class ClaudeVisionExtractor(
    HttpClient httpClient,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeVisionExtractor> logger) : IReceiptVisionExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AnthropicOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<VisionExtractionResult?> ExtractAsync(
        Stream content,
        string mediaType,
        bool isPdf,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Anthropic API key is not configured.");

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var base64Data = Convert.ToBase64String(ms.ToArray());

        var sourceMediaType = isPdf ? "application/pdf" : mediaType;
        var contentBlocks = new List<object>
        {
            new ClaudeContentSourceBlock(
                isPdf ? "document" : "image",
                new ClaudeContentSource("base64", sourceMediaType, base64Data)),
            new ClaudeTextBlock("text", ExtractionPrompt)
        };

        var request = new ClaudeVisionRequest(
            Model: _options.Model,
            MaxTokens: 1024,
            Messages: [new ClaudeVisionMessage("user", contentBlocks)]);

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

        return ParseExtractionResult(text);
    }

    private const string ExtractionPrompt =
        "Extrahiere aus diesem deutschen Kassenbon-Foto alle Artikel mit Preis sowie den " +
        "Gesamtbetrag. Antworte NUR mit JSON: {\"vendor\": ..., \"items\": " +
        "[{\"description\": ..., \"price\": ...}], \"total\": ...}";

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
    /// Tolerant-but-defensive parse of the vision model's response: strips markdown fences,
    /// isolates the outer JSON object, and never trusts the model's shape blindly — items with
    /// an empty description or a negative price are dropped rather than persisted (T-05-05).
    /// Returns <c>null</c> (with a logged warning) on any malformed/non-JSON response instead
    /// of throwing.
    /// </summary>
    private VisionExtractionResult? ParseExtractionResult(string text)
    {
        var cleaned = StripMarkdownFences(text.Trim());

        // Isolate the JSON object — be tolerant of leading/trailing prose.
        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            logger.LogWarning("Vision extraction response did not contain a JSON object. Raw: {Raw}", text);
            return null;
        }

        var json = cleaned[start..(end + 1)];

        VisionResponsePayload? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<VisionResponsePayload>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Vision extraction response was not valid JSON. Raw: {Raw}", text);
            return null;
        }

        if (parsed is null)
            return null;

        var items = (parsed.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Description) && i.Price >= 0)
            .Select(i => new VisionExtractedItem(i.Description!, i.Price))
            .ToArray();

        return new VisionExtractionResult(parsed.Vendor, items, parsed.Total);
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

    private record ClaudeVisionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("messages")] IReadOnlyList<ClaudeVisionMessage> Messages);

    private record ClaudeVisionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] IReadOnlyList<object> Content);

    private record ClaudeContentSource(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("media_type")] string MediaType,
        [property: JsonPropertyName("data")] string Data);

    private record ClaudeContentSourceBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("source")] ClaudeContentSource Source);

    private record ClaudeTextBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private record ClaudeResponse(
        [property: JsonPropertyName("content")] IReadOnlyList<ClaudeContentBlock>? Content);

    private record ClaudeContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);

    private record VisionResponsePayload(
        [property: JsonPropertyName("vendor")] string? Vendor,
        [property: JsonPropertyName("items")] VisionItemPayload[]? Items,
        [property: JsonPropertyName("total")] decimal? Total);

    private record VisionItemPayload(
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("price")] decimal Price);
}
