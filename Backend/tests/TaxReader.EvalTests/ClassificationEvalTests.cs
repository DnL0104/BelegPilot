using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Services;

namespace TaxReader.EvalTests;

/// <summary>
/// Offline classification accuracy evaluation harness (CLS-07).
///
/// ALL tests carry [Trait("Category", "Eval")] — they are NEVER run in CI.
/// The project is also excluded from Backend/TaxReader.sln (Pitfall 6 guard).
///
/// Run manually with Anthropic__ApiKey set:
///   dotnet test Backend/tests/TaxReader.EvalTests --filter "Category=Eval"
///
/// The harness writes a per-category accuracy report to eval/baseline/yyyy-MM-dd.md
/// regardless of pass/fail so coverage gaps are always visible.
/// </summary>
[Trait("Category", "Eval")]
public class ClassificationEvalTests
{
    private const double AccuracyGate = 0.80;
    private const int MinSamplesForGate = 5; // D-13: fewer than 5 = "not evaluated"

    private readonly ClaudeAiClassifier _classifier;
    private readonly string _repoRoot;
    private readonly bool _isConfigured;

    public ClassificationEvalTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("Anthropic__ApiKey")
            ?? Environment.GetEnvironmentVariable("ANTHROPIC__APIKEY");

        var model = Environment.GetEnvironmentVariable("Anthropic__Model")
            ?? Environment.GetEnvironmentVariable("ANTHROPIC__MODEL")
            ?? "claude-haiku-4-5";

        var options = Options.Create(new AnthropicOptions
        {
            ApiKey = apiKey,
            Model = model
        });

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com/")
        };

        _classifier = new ClaudeAiClassifier(
            httpClient,
            options,
            NullLogger<ClaudeAiClassifier>.Instance);

        _isConfigured = _classifier.IsConfigured;
        _repoRoot = GoldenDatasetLoader.FindRepoRoot();
    }

    /// <summary>
    /// Loads the golden dataset, calls the classifier for each receipt's items,
    /// computes per-category accuracy, applies the D-13 gate (covered = ≥ 5 samples),
    /// writes a markdown baseline report to eval/baseline/, and asserts all covered
    /// categories achieve ≥ 80% accuracy.
    /// </summary>
    [Fact]
    public async Task PerCategoryAccuracy_MeetsGate_ForAllCoveredCategories()
    {
        if (!_isConfigured)
        {
            // Skip gracefully when no API key is available.
            // Do NOT throw — a missing key is an environment issue, not a code bug.
            OutputSkipMessage();
            return;
        }

        var dataset = GoldenDatasetLoader.Load(_repoRoot);
        dataset.Should().NotBeEmpty("the golden dataset must have at least one entry to evaluate");

        // Collect all item descriptions and their expected categories in order.
        var flatItems = dataset
            .SelectMany(e => e.Items)
            .ToList();

        flatItems.Should().NotBeEmpty("labels.json must contain at least one item");

        // Call the classifier for all items (the EvalTests project calls ClaudeAiClassifier
        // directly — not via the full DI stack or HTTP). We send all items as a single batch;
        // for very large datasets this should be chunked, but for the seed dataset it is fine.
        var descriptions = flatItems
            .Select(i => i.Description)
            .ToList();

        var results = await _classifier.ClassifyBatchAsync(descriptions, CancellationToken.None);

        results.Should().HaveCount(flatItems.Count,
            "ClassifyBatchAsync always-expectedCount invariant must hold");

        // Compute per-category accuracy.
        var categoryStats = ComputePerCategoryStats(flatItems, results);

        // Write the baseline report (regardless of pass/fail per D-12).
        var reportPath = WriteBaselineReport(categoryStats);

        // Apply the D-13 gate: only categories with ≥ MinSamplesForGate samples.
        var coveredCategories = categoryStats
            .Where(s => s.Total >= MinSamplesForGate)
            .ToList();

        var failedCategories = coveredCategories
            .Where(s => s.Accuracy < AccuracyGate)
            .ToList();

        failedCategories.Should().BeEmpty(
            $"all covered categories (≥{MinSamplesForGate} samples) must achieve ≥{AccuracyGate:P0} accuracy. " +
            $"Failing categories: {string.Join(", ", failedCategories.Select(f => $"{f.Category}({f.Accuracy:P0})"))}. " +
            $"See report: {reportPath}");
    }

    // --- Private helpers ---

    private static List<CategoryStat> ComputePerCategoryStats(
        List<EvalItem> items,
        IReadOnlyList<AiClassificationResult> results)
    {
        // Build a dictionary: expectedCategory → (total, correct).
        var stats = new Dictionary<string, (int Total, int Correct)>(StringComparer.Ordinal);

        for (var i = 0; i < items.Count; i++)
        {
            var expected = items[i].ExpectedCategory;
            var actual = results[i].Category.ToString();

            if (!stats.TryGetValue(expected, out var s))
                s = (0, 0);

            var correct = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            stats[expected] = (s.Total + 1, s.Correct + correct);
        }

        return stats
            .Select(kvp => new CategoryStat(
                Category: kvp.Key,
                Total: kvp.Value.Total,
                Correct: kvp.Value.Correct,
                Accuracy: kvp.Value.Total == 0 ? 0.0 : (double)kvp.Value.Correct / kvp.Value.Total,
                IsCovered: kvp.Value.Total >= MinSamplesForGate))
            .OrderBy(s => s.Category)
            .ToList();
    }

    private string WriteBaselineReport(List<CategoryStat> stats)
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var baselineDir = Path.Combine(_repoRoot, "eval", "baseline");
        Directory.CreateDirectory(baselineDir);
        var reportPath = Path.Combine(baselineDir, $"{date}.md");

        var coveredCount = stats.Count(s => s.IsCovered);
        var passedCount = stats.Count(s => s.IsCovered && s.Accuracy >= AccuracyGate);
        var overallPass = coveredCount == 0 || passedCount == coveredCount;

        var sb = new StringBuilder();
        sb.AppendLine($"# Classification Accuracy Baseline — {date}");
        sb.AppendLine();
        sb.AppendLine($"**Gate:** ≥ {AccuracyGate:P0} accuracy per category with ≥ {MinSamplesForGate} samples (D-13)");
        sb.AppendLine($"**Verdict:** {(overallPass ? "PASS" : "FAIL")}");
        sb.AppendLine($"**Covered categories:** {coveredCount} | **Passed:** {passedCount} | **Failed:** {coveredCount - passedCount}");
        sb.AppendLine($"**Total items evaluated:** {stats.Sum(s => s.Total)}");
        sb.AppendLine();
        sb.AppendLine("## Per-Category Results");
        sb.AppendLine();
        sb.AppendLine("| Category | Samples | Correct | Accuracy | Gate Status |");
        sb.AppendLine("|----------|---------|---------|----------|-------------|");

        foreach (var s in stats)
        {
            string gateStatus;
            if (!s.IsCovered)
                gateStatus = $"not evaluated (< {MinSamplesForGate} samples)";
            else if (s.Accuracy >= AccuracyGate)
                gateStatus = "PASS";
            else
                gateStatus = "FAIL";

            sb.AppendLine(
                $"| {s.Category} | {s.Total} | {s.Correct} | {s.Accuracy:P0} | {gateStatus} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Notes");
        sb.AppendLine();
        sb.AppendLine("- Categories with < 5 samples are excluded from the accuracy gate (D-13).");
        sb.AppendLine("- Add more anonymized receipts to `eval/golden-dataset/` to cover thin categories.");
        sb.AppendLine("- All receipt text in the golden dataset is PII-redacted per D-10 / GDPR.");
        sb.AppendLine($"- Model: `{Environment.GetEnvironmentVariable("Anthropic__Model") ?? "claude-haiku-4-5"}`");

        File.WriteAllText(reportPath, sb.ToString());
        return reportPath;
    }

    private static void OutputSkipMessage()
    {
        // xUnit does not have a built-in skip mechanism for [Fact] without external packages.
        // We write to stdout and return without asserting — the test passes vacuously.
        // Operators will see the message in verbose test output.
        Console.WriteLine(
            "[EVAL SKIPPED] Anthropic__ApiKey environment variable is not set. " +
            "Set it and re-run to execute the classification accuracy evaluation. " +
            "Example: $env:Anthropic__ApiKey = 'sk-ant-...'");
    }

    private record CategoryStat(
        string Category,
        int Total,
        int Correct,
        double Accuracy,
        bool IsCovered);
}
