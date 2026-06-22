# TaxReader.EvalTests — Classification Accuracy Evaluation Harness

CLS-07 offline eval harness. Scores `ClaudeAiClassifier` against a curated golden
dataset of anonymized German tax receipts and gates per-category accuracy at ≥ 80%.

---

## Purpose

This project provides reproducible, evidence-backed proof that the AI classifier
meets the launch-gate accuracy requirement (CLS-07). It is the operator's
responsibility to run it before each release and commit the resulting baseline report.

---

## CI Exclusion

**This project is intentionally NOT listed in `Backend/TaxReader.sln`.**

`dotnet test Backend` (the CI command) discovers only projects in the solution file and
therefore never picks up this project. This is the primary CI-exclusion guarantee (Pitfall 6).

The secondary guard is the `[Trait("Category", "Eval")]` attribute on all test methods.
If the project were ever added to the solution, CI commands could be extended with
`--filter "Category!=Eval"` to exclude eval tests without breaking the solution structure.

---

## Prerequisites

| Requirement | Details |
|-------------|---------|
| .NET 10 SDK | `dotnet --version` should show 10.x |
| Anthropic API key | Set `Anthropic__ApiKey` (or `ANTHROPIC__APIKEY`) environment variable |
| Golden dataset | `eval/golden-dataset/labels.json` + receipt `.txt` files must exist |

---

## Running the Eval Harness

From the repo root:

```bash
# Set the Anthropic API key (Windows PowerShell):
$env:Anthropic__ApiKey = "sk-ant-..."

# Run all eval tests:
dotnet test Backend/tests/TaxReader.EvalTests --filter "Category=Eval"

# Run with verbose output:
dotnet test Backend/tests/TaxReader.EvalTests --filter "Category=Eval" --logger "console;verbosity=detailed"
```

The harness writes a per-category accuracy report to `eval/baseline/<yyyy-MM-dd>.md`.
Commit this file after each run so the baseline is tracked in version control (D-12).

---

## What the Harness Does

1. Loads `eval/golden-dataset/labels.json` (ground-truth manifest).
2. Calls `ClaudeAiClassifier.ClassifyBatchAsync` with item descriptions (real API call).
3. Computes per-category accuracy: `correct / total` for each category.
4. Applies the D-13 gate: categories with fewer than 5 samples are marked "not evaluated"
   and excluded from the ≥ 80% pass/fail assertion.
5. Writes a markdown report to `eval/baseline/<yyyy-MM-dd>.md`.
6. Asserts that all covered categories (≥ 5 samples) achieve ≥ 80% accuracy.

If the API key is missing, all tests are skipped with a clear message — no false failures.

---

## Golden Dataset — Anonymization Policy (D-10, GDPR/GoBD)

**Only anonymized receipt text may be committed to this repository.**

Before adding a receipt to `eval/golden-dataset/receipts/`:
- Remove all personal data: full name, address, email, phone, customer ID, order ID.
- Remove or mask all monetary amounts (use `ANONYM EUR`).
- Replace the vendor name with `ANONYM` if the vendor name itself constitutes personal data.
- Replace dates with `ANONYM` or a generic month/year (no full dates).
- Keep only the article descriptions and line-item labels — these are the classification inputs.

The `labels.json` manifest maps receipt files to items and their expected categories.
Items use the canonical `Category` enum names (e.g. `WerbungskostenArbeitsmittel`).

---

## Golden Dataset — Curating More Samples

The seed dataset ships with a handful of anonymized samples to prove the harness works.
For the CLS-07 launch gate (≥ 50 receipts, ≥ 5 samples per covered category), the
operator must curate additional anonymized receipts. This is a manual data task.

**Target distribution (D-13):** Aim for ≥ 5 samples per category you want in scope.
Categories below 5 are reported as "not evaluated" and excluded from the gate.

---

## Report Format

The harness writes `eval/baseline/<yyyy-MM-dd>.md` with:

- Per-category table: samples | correct | accuracy | gate status
- Overall: covered categories pass/fail count
- Pass/fail verdict (PASS if all covered categories ≥ 80%, FAIL otherwise)

Commit the report after each run as evidence for the launch gate.

---

## Environment Variables

| Variable | Description |
|----------|-------------|
| `Anthropic__ApiKey` | Anthropic API key (required; `sk-ant-...`) |
| `Anthropic__Model` | Model override (default: `claude-haiku-4-5`) |
