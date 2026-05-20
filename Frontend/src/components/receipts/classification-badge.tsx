"use client";

import { Sparkles, BookOpen, Hand, Check, HelpCircle } from "lucide-react";
import { categoryLabel } from "@/lib/format";
import type { ItemClassification } from "@/types/api";

interface ClassificationBadgeProps {
  classification: ItemClassification | null;
}

const categoryStyles: Record<string, string> = {
  ConsumablesAndOfficeSupplies: "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-400",
  SpecialistLiterature: "bg-purple-50 text-purple-700 dark:bg-purple-500/10 dark:text-purple-400",
  TeachingMaterials: "bg-blue-50 text-blue-700 dark:bg-blue-500/10 dark:text-blue-400",
  DigitalToolsAndSoftware: "bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-400",
  OfficeEquipment: "bg-cyan-50 text-cyan-700 dark:bg-cyan-500/10 dark:text-cyan-400",
  TravelAndCommuting: "bg-orange-50 text-orange-700 dark:bg-orange-500/10 dark:text-orange-400",
  ProfessionalDevelopment: "bg-indigo-50 text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-400",
  Unknown: "bg-muted text-muted-foreground",
};

export function ClassificationBadge({
  classification,
}: ClassificationBadgeProps) {
  if (!classification) {
    return (
      <span className="inline-flex items-center rounded-md bg-muted px-2.5 py-0.5 text-[12px] font-medium text-muted-foreground">
        Nicht klassifiziert
      </span>
    );
  }

  const style = categoryStyles[classification.category] ?? categoryStyles.Unknown;
  const isSuggested = classification.status === "Suggested";
  const isUnknown = classification.category === "Unknown";

  const methodIcon =
    classification.method === "AI" ? (
      <Sparkles className="h-3 w-3" />
    ) : classification.method === "Manual" ? (
      <Hand className="h-3 w-3" />
    ) : (
      <BookOpen className="h-3 w-3" />
    );

  const statusIcon = isUnknown ? (
    <HelpCircle className="h-3 w-3 opacity-60" />
  ) : isSuggested ? null : (
    <Check className="h-3 w-3 opacity-60" />
  );

  return (
    <div className="flex items-center gap-1.5">
      <span
        className={`inline-flex items-center gap-1 rounded-md px-2.5 py-0.5 text-[12px] font-medium ${style} ${
          isSuggested && !isUnknown ? "ring-1 ring-inset ring-current/20" : ""
        }`}
        title={
          isUnknown
            ? classification.reason
            : isSuggested
              ? `Vorschlag — ${classification.reason}`
              : `Bestätigt — ${classification.reason}`
        }
      >
        {categoryLabel(classification.category)}
        {methodIcon}
        {statusIcon}
      </span>
      {isSuggested && !isUnknown && (
        <span className="text-[10px] text-muted-foreground">Vorschlag</span>
      )}
    </div>
  );
}
