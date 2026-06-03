"use client";

import { useConsent } from "@/providers/consent-provider";

export function CookieSettingsLink() {
  const { reopenSettings } = useConsent();

  return (
    <button
      type="button"
      className="text-xs text-muted-foreground hover:text-foreground hover:underline"
      onClick={reopenSettings}
    >
      Cookie-Einstellungen
    </button>
  );
}
