"use client";

import { Sparkles, BookOpen, Hand, Check, HelpCircle, AlertCircle } from "lucide-react";
import { categoryLabel } from "@/lib/format";
import type { ItemClassification } from "@/types/api";

interface ClassificationBadgeProps {
  classification: ItemClassification | null;
}

const categoryStyles: Record<string, string> = {
  WerbungskostenArbeitsmittel: "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-400",
  WerbungskostenFachliteratur: "bg-purple-50 text-purple-700 dark:bg-purple-500/10 dark:text-purple-400",
  WerbungskostenBueromaterial: "bg-blue-50 text-blue-700 dark:bg-blue-500/10 dark:text-blue-400",
  WerbungskostenReisekosten: "bg-orange-50 text-orange-700 dark:bg-orange-500/10 dark:text-orange-400",
  WerbungskostenFortbildung: "bg-indigo-50 text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-400",
  WerbungskostenTelekommunikation: "bg-cyan-50 text-cyan-700 dark:bg-cyan-500/10 dark:text-cyan-400",
  SonderausgabenSpenden: "bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-400",
  SonderausgabenVorsorgeaufwendungen: "bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-400",
  AussergewoehnlicheBelastungenKrankheit: "bg-red-50 text-red-700 dark:bg-red-500/10 dark:text-red-400",
  HaushaltsnaheDienstleistung: "bg-lime-50 text-lime-700 dark:bg-lime-500/10 dark:text-lime-400",
  Handwerkerleistung: "bg-yellow-50 text-yellow-700 dark:bg-yellow-500/10 dark:text-yellow-400",
  Privat: "bg-slate-50 text-slate-700 dark:bg-slate-500/10 dark:text-slate-400",
  Unbekannt: "bg-muted text-muted-foreground",
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

  const isSuggested = classification.status === "Suggested";
  const isUnknown = classification.category === "Unbekannt";
  const isFailed = classification.status === "Failed";   // CLS-01 / CLS-02

  // Failed state — distinct from "Nicht erkannt" (Unbekannt). Technical AI failure.
  if (isFailed) {
    return (
      <span
        className="inline-flex items-center gap-1 rounded-md px-2.5 py-0.5 text-[12px] font-medium bg-red-50 text-red-700 dark:bg-red-500/10 dark:text-red-400"
      >
        <AlertCircle className="h-3 w-3" />
        Fehler
      </span>
    );
  }

  const style = categoryStyles[classification.category] ?? categoryStyles.Unbekannt;

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
      {classification.confidenceTier && (
        <span className="text-[10px] text-muted-foreground">
          {classification.confidenceTier}
        </span>
      )}
    </div>
  );
}
