using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaxReader.EvalTests;

/// <summary>
/// Loads the golden dataset from eval/golden-dataset/ relative to the repo root.
/// Walks up from the test assembly location to find the repo root (the directory
/// containing the "eval" folder).
/// </summary>
public static class GoldenDatasetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Finds the repo root by walking up from the current assembly directory
    /// until a directory containing an "eval" sub-folder is found.
    /// </summary>
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "eval")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repo root (a directory containing 'eval/') " +
            $"by walking up from '{AppContext.BaseDirectory}'. " +
            "Ensure the golden dataset exists at eval/golden-dataset/.");
    }

    /// <summary>
    /// Loads and parses labels.json, optionally reading the receipt text for each entry.
    /// </summary>
    public static IReadOnlyList<GoldenDatasetEntry> Load(string repoRoot)
    {
        var labelsPath = Path.Combine(repoRoot, "eval", "golden-dataset", "labels.json");

        if (!File.Exists(labelsPath))
            throw new FileNotFoundException(
                $"Golden dataset manifest not found at '{labelsPath}'. " +
                "Populate eval/golden-dataset/labels.json before running the eval harness.",
                labelsPath);

        var json = File.ReadAllText(labelsPath);
        var manifests = JsonSerializer.Deserialize<LabelManifestEntry[]>(json, JsonOptions)
            ?? throw new InvalidOperationException("labels.json parsed to null.");

        var entries = new List<GoldenDatasetEntry>(manifests.Length);
        var receiptsDir = Path.Combine(repoRoot, "eval", "golden-dataset", "receipts");

        foreach (var manifest in manifests)
        {
            string? receiptText = null;
            if (!string.IsNullOrWhiteSpace(manifest.ReceiptFile))
            {
                var receiptPath = Path.Combine(repoRoot, "eval", "golden-dataset", manifest.ReceiptFile);
                if (File.Exists(receiptPath))
                    receiptText = File.ReadAllText(receiptPath);
            }

            entries.Add(new GoldenDatasetEntry(
                ReceiptFile: manifest.ReceiptFile,
                Vendor: manifest.Vendor,
                ReceiptText: receiptText,
                Items: manifest.Items ?? []));
        }

        return entries;
    }
}

/// <summary>A single entry from labels.json.</summary>
public record GoldenDatasetEntry(
    string? ReceiptFile,
    string? Vendor,
    string? ReceiptText,
    EvalItem[] Items);

/// <summary>A single labeled item within a receipt entry.</summary>
public record EvalItem(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("expectedCategory")] string ExpectedCategory);

// Internal deserialization types that mirror the JSON structure.
file record LabelManifestEntry(
    [property: JsonPropertyName("receiptFile")] string? ReceiptFile,
    [property: JsonPropertyName("vendor")] string? Vendor,
    [property: JsonPropertyName("items")] EvalItem[]? Items);
