# Phase 4: Classification Trustworthiness - Pattern Map

**Mapped:** 2026-05-22
**Files analyzed:** 22 (5 new, 17 modified)
**Analogs found:** 22 / 22

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `Backend/.../Commands/SaveClassificationRuleHandler.cs` | command handler | request-response | `ConfirmClassificationHandler.cs` | exact |
| `Backend/.../Commands/AcknowledgeSumMismatchHandler.cs` | command handler | request-response | `DeleteReceiptFileHandler.cs` | exact |
| `Backend/.../Services/RuleBasedClassifier.cs` | service | request-response | `AiOnlyClassificationService.cs` | role-match |
| `Backend/.../Services/HybridClassificationService.cs` | service | request-response | `AiOnlyClassificationService.cs` | exact |
| `Backend/.../Configurations/ClassificationRuleConfiguration.cs` | EF config | CRUD | `ItemClassificationConfiguration.cs` + self | exact |
| `Backend/.../Configurations/ReceiptConfiguration.cs` | EF config | CRUD | `ReceiptConfiguration.cs` (self) | exact |
| `Backend/.../DependencyInjection.cs` | config | — | `DependencyInjection.cs` (self) | exact |
| `Backend/.../Endpoints/ReceiptItemEndpoints.cs` | endpoint | request-response | `ClassificationEndpoints.cs` | exact |
| `Backend/.../Endpoints/ReceiptEndpoints.cs` | endpoint | request-response | `ReceiptEndpoints.cs` (self) | exact |
| `Backend/.../Jobs/ClassifyBatchJob.cs` | job | batch | `ClassifyBatchJob.cs` (self) | exact |
| `Backend/.../Domain/Enums/Category.cs` | enum | — | `Category.cs` (self) | exact |
| `Backend/.../Domain/Entities/ClassificationRule.cs` | entity | — | `ClassificationRule.cs` (self) | exact |
| `Backend/.../Domain/Entities/Receipt.cs` | entity | — | `Receipt.cs` (self) | exact |
| `Frontend/src/types/api.ts` | type definitions | — | `api.ts` (self) | exact |
| `Frontend/src/lib/format.ts` | utility | — | `format.ts` (self) | exact |
| `Frontend/src/lib/api-client.ts` | API client | request-response | `api-client.ts` (self) | exact |
| `Frontend/src/hooks/use-receipt-items.ts` | hook | request-response | `use-receipt-items.ts` (self) | exact |
| `Frontend/src/hooks/use-receipts.ts` | hook | request-response | `use-receipts.ts` (self) | exact |
| `Frontend/src/components/receipts/save-rule-dialog.tsx` | component | request-response | `classify-dialog.tsx` | exact |
| `Frontend/src/components/receipts/classify-dialog.tsx` | component | request-response | `classify-dialog.tsx` (self) | exact |
| `Frontend/src/app/(authenticated)/receipts/[id]/page.tsx` | page | request-response | `receipts/[id]/page.tsx` (self) | exact |
| `Frontend/src/app/(authenticated)/settings/page.tsx` | page | request-response | `settings/page.tsx` (self) | exact |

---

## Pattern Assignments

### `Backend/.../Commands/SaveClassificationRuleHandler.cs` (command handler, request-response)

**Analog:** `Backend/src/TaxReader.Application/Commands/ConfirmClassificationHandler.cs`

**Imports pattern** (lines 1-9):
```csharp
using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;
```

**Primary constructor DI pattern** (line 11):
```csharp
public class SaveClassificationRuleHandler(IAppDbContext dbContext, ICurrentUser currentUser)
```

**Core pattern — ownership guard + entity insert** (lines 11-43):
```csharp
public async Task<Result<ClassificationRuleDto>> HandleAsync(
    SaveClassificationRuleCommand command,
    CancellationToken cancellationToken = default)
{
    // Verify the item belongs to this user (per-user scoping via ICurrentUser)
    var item = await dbContext.ReceiptItems
        .Include(i => i.Receipt)
            .ThenInclude(r => r.ReceiptFile)
        .FirstOrDefaultAsync(i => i.Id == command.ReceiptItemId
            && i.Receipt.ReceiptFile.UserId == currentUser.UserId, cancellationToken);

    if (item is null)
        return Result<ClassificationRuleDto>.Failure(
            $"Artikel mit id '{command.ReceiptItemId}' nicht gefunden.");

    // D-12: 409 Conflict if identical user rule already exists
    var duplicate = await dbContext.ClassificationRules
        .AnyAsync(r => r.UserId == currentUser.UserId
            && r.DescriptionPattern == command.DescriptionPattern
            && r.VendorPattern == command.VendorPattern
            && r.SourceFilePattern == command.SourceFilePattern, cancellationToken);

    if (duplicate)
        return Result<ClassificationRuleDto>.Failure("Eine identische Regel existiert bereits.");

    var rule = new ClassificationRule
    {
        Id = Guid.NewGuid(),
        UserId = currentUser.UserId,
        VendorPattern = command.VendorPattern,
        DescriptionPattern = command.DescriptionPattern,
        SourceFilePattern = command.SourceFilePattern,
        Category = command.Category,
        Priority = 10,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    dbContext.ClassificationRules.Add(rule);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Result<ClassificationRuleDto>.Success(rule.ToDto());
}
```

**Endpoint translates Result to 201 / 409** — see endpoint section below.

---

### `Backend/.../Commands/AcknowledgeSumMismatchHandler.cs` (command handler, request-response)

**Analog:** `Backend/src/TaxReader.Application/Commands/DeleteReceiptFileHandler.cs`

**Core pattern — ownership guard + field update** (lines 1-27):
```csharp
using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

public class AcknowledgeSumMismatchHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        AcknowledgeSumMismatchCommand command,
        CancellationToken cancellationToken = default)
    {
        // Scope: receipt belongs to current user (via ReceiptFile.UserId)
        var receipt = await dbContext.Receipts
            .Include(r => r.ReceiptFile)
            .FirstOrDefaultAsync(r => r.Id == command.ReceiptId
                && r.ReceiptFile.UserId == currentUser.UserId, cancellationToken);

        if (receipt is null)
            return Result<bool>.Failure($"Beleg mit id '{command.ReceiptId}' nicht gefunden.");

        receipt.HasSumMismatch = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
```

**Note:** Endpoint returns 204 No Content on success (same as delete endpoints).

---

### `Backend/.../Services/RuleBasedClassifier.cs` (service, request-response)

**Analog:** `Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs`

**Imports + class signature pattern**:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using System.Text.RegularExpressions;

namespace TaxReader.Infrastructure.Services;

public class RuleBasedClassifier(
    IAppDbContext dbContext,
    ILogger<RuleBasedClassifier> logger)
```

**Core evaluation pattern per D-06** (user rules first, then system rules, ALL non-null fields must match):
```csharp
// D-06: user rules > system rules. Query both in priority-DESC order.
// Rule fires when ALL non-null fields match.
// VendorPattern: case-insensitive substring against receipt.Vendor
// DescriptionPattern: regex with RegexOptions.IgnoreCase against item.Description
// SourceFilePattern: regex with RegexOptions.IgnoreCase against receiptFile.OriginalFileName

public async Task<ItemClassification?> ClassifyItemAsync(
    ReceiptItem item,
    string vendor,
    string sourceFileName,
    Guid userId,
    CancellationToken cancellationToken = default)
{
    // User rules first
    var userRules = await dbContext.ClassificationRules
        .Where(r => r.UserId == userId && r.IsActive)
        .OrderByDescending(r => r.Priority)
        .ToListAsync(cancellationToken);

    var matched = userRules.FirstOrDefault(r => Matches(r, item.Description, vendor, sourceFileName));
    if (matched is not null)
        return BuildClassification(item, matched);

    // System rules fallback (UserId == null)
    var systemRules = await dbContext.ClassificationRules
        .Where(r => r.UserId == null && r.IsActive)
        .OrderByDescending(r => r.Priority)
        .ToListAsync(cancellationToken);

    matched = systemRules.FirstOrDefault(r => Matches(r, item.Description, vendor, sourceFileName));
    return matched is null ? null : BuildClassification(item, matched);
}
```

**Matching helper — mirrors D-05 spec**:
```csharp
private static bool Matches(ClassificationRule rule, string description, string vendor, string fileName)
{
    if (rule.VendorPattern is not null
        && !vendor.Contains(rule.VendorPattern, StringComparison.OrdinalIgnoreCase))
        return false;
    if (rule.DescriptionPattern is not null
        && !Regex.IsMatch(description, rule.DescriptionPattern, RegexOptions.IgnoreCase))
        return false;
    if (rule.SourceFilePattern is not null
        && !Regex.IsMatch(fileName, rule.SourceFilePattern, RegexOptions.IgnoreCase))
        return false;
    return true;
}

private static ItemClassification BuildClassification(ReceiptItem item, ClassificationRule rule)
{
    var patternDesc = rule.VendorPattern ?? rule.DescriptionPattern ?? rule.SourceFilePattern ?? "?";
    return new ItemClassification
    {
        Id = Guid.NewGuid(),
        ReceiptItemId = item.Id,
        Category = rule.Category,
        Method = ClassificationMethod.Rule,
        Status = ClassificationStatus.Confirmed,   // rules are deterministic — always Confirmed
        Reason = $"Regel angewendet: {patternDesc} → {rule.Category}",
        ClassifiedAt = DateTime.UtcNow
    };
}
```

---

### `Backend/.../Services/HybridClassificationService.cs` (service, request-response)

**Analog:** `Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs`

**Signature + constructor** (mirrors AiOnlyClassificationService lines 21-28):
```csharp
public class HybridClassificationService(
    RuleBasedClassifier ruleBasedClassifier,
    AiOnlyClassificationService aiClassifier,
    ILogger<HybridClassificationService> logger) : IClassificationService
```

**Core pattern — rules first, AI for remainder in one batch per D-07**:
```csharp
public async Task<IReadOnlyList<ItemClassification>> ClassifyItemsAsync(
    IEnumerable<ReceiptItem> items,
    Guid userId,
    CancellationToken cancellationToken = default)
{
    var itemList = items as IReadOnlyList<ReceiptItem> ?? items.ToList();
    if (itemList.Count == 0) return [];

    var results = new List<ItemClassification>(itemList.Count);
    var aiItems = new List<ReceiptItem>();

    foreach (var item in itemList)
    {
        // item.Receipt navigation must be loaded by caller (ClassifyBatchJob already does this)
        var vendor = item.Receipt?.Vendor ?? string.Empty;
        var fileName = item.Receipt?.ReceiptFile?.OriginalFileName ?? string.Empty;
        var ruleMatch = await ruleBasedClassifier.ClassifyItemAsync(
            item, vendor, fileName, userId, cancellationToken);

        if (ruleMatch is not null)
            results.Add(ruleMatch);
        else
            aiItems.Add(item);
    }

    logger.LogInformation(
        "Hybrid classification: {RuleCount} rule-matched, {AiCount} AI-bound",
        results.Count, aiItems.Count);

    // Single AI batch call for all unmatched items — preserves Phase 3 D-01 batching
    if (aiItems.Count > 0)
    {
        var aiResults = await aiClassifier.ClassifyItemsAsync(aiItems, userId, cancellationToken);
        results.AddRange(aiResults);
    }

    return results;
}
```

---

### `Backend/.../Configurations/ClassificationRuleConfiguration.cs` (EF config, CRUD)

**Analog:** `ClassificationRuleConfiguration.cs` (self) + `ItemClassificationConfiguration.cs` (FK pattern)

**New fields to add in Phase 4 migration and config update**:
```csharp
// Add after existing builder.Property(e => e.Pattern) line — rename Pattern → DescriptionPattern
builder.Property(e => e.DescriptionPattern).HasMaxLength(500);
builder.Property(e => e.VendorPattern).HasMaxLength(500);
builder.Property(e => e.SourceFilePattern).HasMaxLength(500);

// UserId: nullable FK to users (D-04)
builder.Property(e => e.UserId);  // nullable Guid?

builder.HasOne<User>()
    .WithMany()
    .HasForeignKey(e => e.UserId)
    .OnDelete(DeleteBehavior.Cascade);  // deleting a user removes their rules

// Replace existing index: (IsActive, Priority) → (UserId, IsActive, Priority) per D-04
builder.HasIndex(e => new { e.UserId, e.IsActive, e.Priority });
```

**Seed data update pattern** — all existing `HasData` rows gain `UserId = null` (system rules) and
`DescriptionPattern` = old `Pattern` value; `VendorPattern = null`, `SourceFilePattern = null`.
Old category enum names in seed rows are remapped to new German identifiers per D-02 mapping table.

---

### `Backend/.../Configurations/ReceiptConfiguration.cs` (EF config, CRUD)

**Analog:** `ReceiptConfiguration.cs` (self)

**Single new property to map** (add after existing `builder.Property(e => e.RawExtractedText)` block):
```csharp
builder.Property(e => e.HasSumMismatch)
    .HasDefaultValue(false)
    .IsRequired();
```

No new index or FK needed.

---

### `Backend/.../DependencyInjection.cs` (config)

**Analog:** `DependencyInjection.cs` (self, lines 124-128)

**Replace** the existing `IClassificationService` registration (line 128):
```csharp
// Before (line 128):
services.AddScoped<IClassificationService, AiOnlyClassificationService>();

// After (Phase 4):
// AiOnlyClassificationService stays registered so HybridClassificationService can inject it.
services.AddScoped<AiOnlyClassificationService>();
services.AddScoped<RuleBasedClassifier>();
services.AddScoped<IClassificationService, HybridClassificationService>();
```

---

### `Backend/.../Endpoints/ReceiptItemEndpoints.cs` (NEW endpoint, request-response)

**Analog:** `Backend/src/TaxReader.Api/Endpoints/ClassificationEndpoints.cs`

The existing `ClassificationEndpoints.cs` owns the `/receipt-items` route group. The new
`POST /{id}/save-rule` endpoint **extends that same file** (there is no separate
`ReceiptItemEndpoints.cs` — the CONTEXT.md's reference means the classification endpoint file).

**New endpoint pattern** (mirrors confirm endpoint at lines 14-32):
```csharp
classification.MapPost("/{id:guid}/save-rule", async (
    Guid id,
    SaveRuleRequest request,
    SaveClassificationRuleHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<Category>(request.Category, true, out var category))
        return Results.BadRequest(new { error = $"Ungültige Kategorie: {request.Category}" });

    // D-12: at least one pattern field must be non-empty
    if (string.IsNullOrWhiteSpace(request.VendorPattern)
        && string.IsNullOrWhiteSpace(request.DescriptionPattern)
        && string.IsNullOrWhiteSpace(request.SourceFilePattern))
        return Results.BadRequest(new { error = "Mindestens ein Musterfeld muss angegeben werden." });

    var command = new SaveClassificationRuleCommand(
        id, request.VendorPattern, request.DescriptionPattern,
        request.SourceFilePattern, category);
    var result = await handler.HandleAsync(command, cancellationToken);

    return result.IsSuccess
        ? Results.Created($"/api/v1/classification-rules/{result.Value!.Id}", result.Value)
        : result.Error!.Contains("identische Regel")
            ? Results.Conflict(new { error = result.Error })
            : Results.NotFound(new { error = result.Error });
})
.WithName("SaveClassificationRule")
.WithSummary("Save a user classification rule derived from a manual override");

// Record at bottom of file (same pattern as ConfirmClassificationRequest line 64):
public record SaveRuleRequest(
    string? VendorPattern,
    string? DescriptionPattern,
    string? SourceFilePattern,
    string Category);
```

**Handler registration** in `Program.cs` (same block as lines 93-111):
```csharp
builder.Services.AddScoped<SaveClassificationRuleHandler>();
builder.Services.AddScoped<AcknowledgeSumMismatchHandler>();
```

---

### `Backend/.../Endpoints/ReceiptEndpoints.cs` (modified, request-response)

**Analog:** `ReceiptEndpoints.cs` (self, lines 56-70 `reclassify` endpoint pattern)

**New `POST /{id}/acknowledge-sum` endpoint** (mirrors reclassify pattern):
```csharp
receipts.MapPost("/{id:guid}/acknowledge-sum", async (
    Guid id,
    [FromServices] AcknowledgeSumMismatchHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new AcknowledgeSumMismatchCommand(id), cancellationToken);

    return result.IsSuccess
        ? Results.NoContent()
        : Results.NotFound(new { error = result.Error });
})
.WithName("AcknowledgeSumMismatch")
.WithSummary("Dismiss the sum-mismatch warning on a receipt");
```

---

### `Backend/.../Jobs/ClassifyBatchJob.cs` (modified, batch)

**Analog:** `ClassifyBatchJob.cs` (self)

**Sum validation insertion point** — add immediately before the final `foreach` that sets `Completed`
(after the `classifications` save, inside the outer `try` after `SaveChangesAsync`):

```csharp
// D-16: sum validation after all items classified
foreach (var run in runs.Where(r => r.ReceiptFile.Receipt is not null))
{
    var receipt = run.ReceiptFile.Receipt!;
    var itemsSum = receipt.Items.Sum(i => i.TotalPrice);
    receipt.HasSumMismatch = Math.Abs(itemsSum - receipt.TotalAmount) > 0.50m;
}
// falls through into the existing SaveChangesAsync call that follows
```

**Hybrid dispatch** — `classificationService.ClassifyItemsAsync(allItems, userId, ...)` at line 78
requires no change in signature; `HybridClassificationService` is transparently substituted by DI.

---

### `Backend/.../Domain/Enums/Category.cs` (modified)

**Analog:** `Category.cs` (self, lines 1-13)

**Replace all 8 values with 13 German identifiers** (D-03). Preserve `= 0` for `Unbekannt`
(replaces `Unknown = 0`):
```csharp
namespace TaxReader.Domain.Enums;

public enum Category
{
    Unbekannt = 0,
    WerbungskostenArbeitsmittel = 1,
    WerbungskostenFachliteratur = 2,
    WerbungskostenBueromaterial = 3,
    WerbungskostenReisekosten = 4,
    WerbungskostenFortbildung = 5,
    WerbungskostenTelekommunikation = 6,
    SonderausgabenSpenden = 7,
    SonderausgabenVorsorgeaufwendungen = 8,
    AussergewoehnlicheBelastungenKrankheit = 9,
    HaushaltsnaheDienstleistung = 10,
    Handwerkerleistung = 11,
    Privat = 12
}
```

All files that reference old enum member names (`Category.Unknown`, `Category.ConsumablesAndOfficeSupplies`, etc.)
must be updated: `AiOnlyClassificationService.cs` line 91 (`Category.Unknown` → `Category.Unbekannt`),
`classify-dialog.tsx` categories array, `format.ts` `categoryLabel()`, `api.ts` `Category` union type.

---

### `Backend/.../Domain/Entities/ClassificationRule.cs` (modified)

**Analog:** `ClassificationRule.cs` (self)

**Replace `Pattern` with three-field schema** (D-05) and add `UserId` (D-04):
```csharp
public class ClassificationRule
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }              // null = system rule; non-null = user-private rule
    public string? VendorPattern { get; set; }     // substring match, case-insensitive
    public string? SourceFilePattern { get; set; } // regex match, case-insensitive
    public string? DescriptionPattern { get; set; }// regex match, case-insensitive (was Pattern)
    public Category Category { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

---

### `Backend/.../Domain/Entities/Receipt.cs` (modified)

**Analog:** `Receipt.cs` (self)

**Add one property** after `ParsedAt` (D-15):
```csharp
public bool HasSumMismatch { get; set; } = false;
```

---

### `Frontend/src/types/api.ts` (modified)

**Analog:** `api.ts` (self, lines 115-123 `Category` type)

**Replace `Category` union** (lines 115-123):
```typescript
export type Category =
  | "Unbekannt"
  | "WerbungskostenArbeitsmittel"
  | "WerbungskostenFachliteratur"
  | "WerbungskostenBueromaterial"
  | "WerbungskostenReisekosten"
  | "WerbungskostenFortbildung"
  | "WerbungskostenTelekommunikation"
  | "SonderausgabenSpenden"
  | "SonderausgabenVorsorgeaufwendungen"
  | "AussergewoehnlicheBelastungenKrankheit"
  | "HaushaltsnaheDienstleistung"
  | "Handwerkerleistung"
  | "Privat";
```

**Add `hasReceiptSumMismatch` to `Receipt` interface** (after `unknownCount: number`):
```typescript
hasSumMismatch: boolean;
```

**Add `ClassificationRule` interface** (new type, same style as `ItemClassification` lines 64-71):
```typescript
export interface ClassificationRule {
  id: string;
  userId: string | null;
  vendorPattern: string | null;
  sourceFilePattern: string | null;
  descriptionPattern: string | null;
  category: Category;
  priority: number;
  isActive: boolean;
  createdAt: string;
}
```

---

### `Frontend/src/lib/format.ts` (modified)

**Analog:** `format.ts` (self, lines 22-34 `categoryLabel()`)

**Replace the 8-entry map with 13-entry German map** (D-03):
```typescript
export function categoryLabel(category: string): string {
  const labels: Record<string, string> = {
    Unbekannt: "Nicht zugeordnet",
    WerbungskostenArbeitsmittel: "Werbungskosten – Arbeitsmittel",
    WerbungskostenFachliteratur: "Werbungskosten – Fachliteratur",
    WerbungskostenBueromaterial: "Werbungskosten – Büromaterial",
    WerbungskostenReisekosten: "Werbungskosten – Reisekosten",
    WerbungskostenFortbildung: "Werbungskosten – Fortbildung",
    WerbungskostenTelekommunikation: "Werbungskosten – Telekommunikation",
    SonderausgabenSpenden: "Sonderausgaben – Spenden",
    SonderausgabenVorsorgeaufwendungen: "Sonderausgaben – Vorsorgeaufwendungen",
    AussergewoehnlicheBelastungenKrankheit: "Außergewöhnliche Belastungen – Krankheit",
    HaushaltsnaheDienstleistung: "Haushaltsnahe Dienstleistung",
    Handwerkerleistung: "Handwerkerleistung",
    Privat: "Privat",
  };
  return labels[category] ?? category;
}
```

---

### `Frontend/src/lib/api-client.ts` (modified)

**Analog:** `api-client.ts` (self, lines 234-243 `confirmClassification` pattern)

**New `saveClassificationRule` function** (append after existing classification functions):
```typescript
export interface SaveRulePayload {
  vendorPattern?: string | null;
  descriptionPattern?: string | null;
  sourceFilePattern?: string | null;
  category: string;
}

export async function saveClassificationRule(
  itemId: string,
  payload: SaveRulePayload
): Promise<ClassificationRule> {
  const { data } = await api.post<ClassificationRule>(
    `/receipt-items/${itemId}/save-rule`,
    payload
  );
  return data;
}
```

**New `acknowledgeSumMismatch` function** (append after receipt functions):
```typescript
export async function acknowledgeSumMismatch(receiptId: string): Promise<void> {
  await api.post(`/receipts/${receiptId}/acknowledge-sum`);
}
```

Both imports must be added to the `import type { ... } from "@/types/api"` block at the top of the file.

---

### `Frontend/src/hooks/use-receipt-items.ts` (modified)

**Analog:** `use-receipt-items.ts` (self, lines 28-44 `useConfirmClassification` mutation pattern)

**New `useSaveClassificationRule` hook** (append to file):
```typescript
export function useSaveClassificationRule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      itemId,
      payload,
    }: {
      itemId: string;
      payload: SaveRulePayload;
    }) => saveClassificationRule(itemId, payload),
    onSuccess: () => {
      // Rules don't change existing classifications, no cache invalidation needed
      // except to signal the UI the rule was saved
    },
  });
}
```

Import `saveClassificationRule` and `SaveRulePayload` from `@/lib/api-client`.

---

### `Frontend/src/hooks/use-receipts.ts` (modified)

**Analog:** `use-receipts.ts` (self) + `use-receipt-items.ts` lines 28-44 mutation pattern

**New `useAcknowledgeSumMismatch` hook** (append to file):
```typescript
export function useAcknowledgeSumMismatch() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (receiptId: string) => acknowledgeSumMismatch(receiptId),
    onSuccess: (_data, receiptId) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.detail(receiptId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
    },
  });
}
```

Add imports: `useMutation`, `useQueryClient` from `@tanstack/react-query`; `acknowledgeSumMismatch` from `@/lib/api-client`.

---

### `Frontend/src/components/receipts/save-rule-dialog.tsx` (NEW component)

**Analog:** `classify-dialog.tsx` (full file)

**File structure pattern** (mirrors classify-dialog.tsx lines 1-201):
```typescript
"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Loader2, BookmarkPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useSaveClassificationRule } from "@/hooks/use-receipt-items";
import type { ReceiptItem, Category } from "@/types/api";
import { categoryLabel } from "@/lib/format";

interface SaveRuleDialogProps {
  item: ReceiptItem | null;
  category: Category | string;   // the newly-selected category from classify-dialog
  vendor: string;                // receipt vendor name — pre-populates VendorPattern
  open: boolean;
  onOpenChange: (open: boolean) => void;
}
```

**State and mutation pattern** (mirrors classify-dialog.tsx lines 47-76):
```typescript
export function SaveRuleDialog({ item, category, vendor, open, onOpenChange }: SaveRuleDialogProps) {
  const [vendorPattern, setVendorPattern] = useState(vendor);
  const [descPattern, setDescPattern] = useState(item?.description ?? "");
  const [includeVendor, setIncludeVendor] = useState(true);
  const [includeDesc, setIncludeDesc] = useState(true);
  const mutation = useSaveClassificationRule();

  const handleSave = async () => {
    if (!item) return;
    try {
      await mutation.mutateAsync({
        itemId: item.id,
        payload: {
          vendorPattern: includeVendor ? vendorPattern : null,
          descriptionPattern: includeDesc ? descPattern : null,
          category,
        },
      });
      toast.success("Regel gespeichert");
      onOpenChange(false);
    } catch {
      toast.error("Regel konnte nicht gespeichert werden");
    }
  };
```

**Dialog body structure** — two labelled `Input` fields with `Checkbox` toggles to include/exclude,
then `DialogFooter` with Abbrechen / Speichern buttons (mirrors settings/page.tsx dialog pattern lines 213-277).

---

### `Frontend/src/components/receipts/classify-dialog.tsx` (modified)

**Analog:** `classify-dialog.tsx` (self)

**Changes** (D-09, D-10):
1. Add `vendor: string` to `ClassifyDialogProps` (passed from parent receipt detail page which has receipt data).
2. Add `const [saveRuleOpen, setSaveRuleOpen] = useState(false)` state.
3. Render `<SaveRuleDialog>` nested inside (not blocking the classify dialog).
4. Add "Diese Regel speichern" button in `DialogFooter` — visible only when `category !== suggestedCategory && category !== ""`:

```typescript
// In DialogFooter, before Abbrechen button:
{category && category !== suggestedCategory && (
  <Button
    variant="outline"
    onClick={() => setSaveRuleOpen(true)}
    disabled={mutation.isPending}
  >
    <BookmarkPlus className="mr-1.5 h-3.5 w-3.5" />
    Diese Regel speichern
  </Button>
)}
```

The `categories` array (lines 32-40) is updated to use the 13 new German enum identifiers.

---

### `Frontend/src/app/(authenticated)/receipts/[id]/page.tsx` (modified)

**Analog:** `receipts/[id]/page.tsx` (self)

**Sum-mismatch Alert pattern** (D-17) — mirrors existing Alert usage at lines 156-161 but dismissable:
```typescript
// Import additions:
import { AlertTriangle } from "lucide-react";
import { useAcknowledgeSumMismatch } from "@/hooks/use-receipts";

// In component body:
const acknowledgeMutation = useAcknowledgeSumMismatch();

// JSX — render after the receipt summary card and before the items table:
{receipt.hasSumMismatch && (
  <Alert className="border-amber-200 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/30">
    <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400" />
    <AlertTitle className="text-amber-800 dark:text-amber-300">
      Summe stimmt nicht überein
    </AlertTitle>
    <AlertDescription className="flex items-center justify-between gap-4">
      <span>
        Die Summe der Artikel weicht von der Belegsumme ab. Bitte prüfen.
      </span>
      <Button
        size="sm"
        variant="outline"
        onClick={() => acknowledgeMutation.mutate(id)}
        disabled={acknowledgeMutation.isPending}
      >
        {acknowledgeMutation.isPending && (
          <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
        )}
        Als geprüft markieren
      </Button>
    </AlertDescription>
  </Alert>
)}
```

**Inline reasoning display** (D-13) — items already show `item.latestClassification.reason` inside
`ReceiptItemsTable`. The receipt detail page itself needs no change for reasoning; it delegates to
the table component. Only the table component needs to ensure reason is visible without expand.

---

### `Frontend/src/app/(authenticated)/settings/page.tsx` (modified)

**Analog:** `settings/page.tsx` (self)

The settings page already has the `autoConfirmThreshold` control fully implemented (D-14 is already done).
No changes needed unless a UI description update is required to note that the threshold now also applies
to rule-matched items. Rule-matched items are always `Confirmed` regardless of threshold (D-07 states rules
are deterministic), so the existing copy ("KI-Klassifizierung mit einer Konfidenz über dem Schwellenwert")
remains accurate with no changes needed.

---

## Shared Patterns

### Per-user data scoping
**Source:** `Backend/src/TaxReader.Application/Commands/DeleteReceiptFileHandler.cs` lines 15-16
**Apply to:** `SaveClassificationRuleHandler`, `AcknowledgeSumMismatchHandler`, `RuleBasedClassifier`
```csharp
// All queries filter on ICurrentUser.UserId (handlers) or explicit userId param (services called from jobs)
.FirstOrDefaultAsync(f => f.Id == command.Id && f.UserId == currentUser.UserId, cancellationToken)
```

### Result<T> error handling
**Source:** `Backend/src/TaxReader.Domain/Common/Result.cs` (used in all handler analogs)
**Apply to:** `SaveClassificationRuleHandler`, `AcknowledgeSumMismatchHandler`
```csharp
return Result<T>.Failure("German error message here.");  // failure path
return Result<T>.Success(value);                          // success path
```
Error messages are German (e.g., `"Mindestens ein Musterfeld muss angegeben werden."`).

### Endpoint Result translation
**Source:** `ClassificationEndpoints.cs` lines 14-32, `ReceiptEndpoints.cs` lines 28-38
**Apply to:** new `save-rule` and `acknowledge-sum` endpoints
```csharp
return result.IsSuccess
    ? Results.Created(...)   // or Results.Ok / Results.NoContent
    : Results.NotFound(new { error = result.Error });
// For 409: Results.Conflict(new { error = result.Error })
```

### Mutation hook with cache invalidation
**Source:** `Frontend/src/hooks/use-receipt-items.ts` lines 28-44
**Apply to:** `useSaveClassificationRule`, `useAcknowledgeSumMismatch`
```typescript
return useMutation({
  mutationFn: (args) => apiFunction(args),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
  },
});
```

### shadcn Dialog with form fields
**Source:** `Frontend/src/app/(authenticated)/settings/page.tsx` lines 213-277
**Apply to:** `save-rule-dialog.tsx`
Pattern: controlled `useState` for each field, `disabled={isPending}` on inputs, `Loader2` spinner
on submit button, `toast.success` / `toast.error` in try/catch.

### EF IEntityTypeConfiguration with FK
**Source:** `Backend/src/TaxReader.Infrastructure/Data/Configurations/ItemClassificationConfiguration.cs` lines 25-29
**Apply to:** `ClassificationRuleConfiguration` (UserId FK to User)
```csharp
builder.HasOne<User>()
    .WithMany()
    .HasForeignKey(e => e.UserId)
    .OnDelete(DeleteBehavior.Cascade);
```

### Structured logging
**Source:** `AiOnlyClassificationService.cs` lines 40-41, 75-76
**Apply to:** `RuleBasedClassifier`, `HybridClassificationService`
```csharp
logger.LogInformation("Rule matched {Count} items for user {UserId}", count, userId);
// Named placeholders only — never string interpolation in message templates
```

---

## No Analog Found

All files in this phase have direct analogs in the codebase. No files require falling back to
RESEARCH.md patterns.

---

## Metadata

**Analog search scope:** `Backend/src/`, `Frontend/src/`
**Files scanned:** 30
**Pattern extraction date:** 2026-05-22
