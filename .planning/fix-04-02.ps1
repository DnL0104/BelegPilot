$file = 'D:\Programming\Repos\TaxReader\.planning\phases\04-classification-trustworthiness\04-02-PLAN.md'
$content = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

$old = '3. Update `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs`:'

$newStep = '3. Verify and fix the ThenInclude chain in `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs`. `HybridClassificationService` accesses `item.Receipt?.Vendor` and `item.Receipt?.ReceiptFile?.OriginalFileName` for every item passed to it. If ClassifyBatchJob''s initial DB query does not load the Receipt and ReceiptFile navigations, both fields silently resolve to null/empty and all vendor- and file-pattern rules are skipped.

Read ClassifyBatchJob.cs and confirm the query includes at minimum:
```csharp
.Include(r => r.ReceiptFile)
    .ThenInclude(rf => rf.Receipt)
        .ThenInclude(r => r!.Items)
```
If the ThenInclude chain is missing or incomplete (e.g., Items are included but ReceiptFile/Receipt are not), add the missing ThenInclude calls now. Do not change the job''s processing logic -- only fix the query includes.

4. Update `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs`:'

if (-not $content.Contains($old)) {
    Write-Host "Pattern NOT FOUND"
    exit 1
}
$content = $content.Replace($old, $newStep)
Write-Host "Step 3a inserted"

$content = $content.Replace(
    '4. Create `Backend/tests/TaxReader.UnitTests/Services/RuleBasedClassifierTests.cs`.',
    '5. Create `Backend/tests/TaxReader.UnitTests/Services/RuleBasedClassifierTests.cs`.'
)
$content = $content.Replace(
    '5. Run: `dotnet test Backend --filter',
    '6. Run: `dotnet test Backend --filter'
)

[System.IO.File]::WriteAllText($file, $content, [System.Text.Encoding]::UTF8)
Write-Host "File written successfully"
