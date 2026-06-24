"use client";

type Props = { reviewed: boolean };

export function DraftWarning({ reviewed }: Props) {
  if (reviewed) return null;
  return (
    <div className="rounded border border-amber-400 bg-amber-50 dark:bg-amber-500/10 px-4 py-2 text-sm text-amber-800 dark:text-amber-200">
      ⚠ Entwurf – anwaltliche Prüfung ausstehend
    </div>
  );
}
