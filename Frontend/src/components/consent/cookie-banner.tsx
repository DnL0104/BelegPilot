"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { useConsent } from "@/providers/consent-provider";
import { ConsentSettingsDialog } from "./consent-settings-dialog";

export function CookieBanner() {
  const { consent, acceptAll, acceptNecessary, settingsPanelOpen, closeSettings } = useConsent();
  const [showSettings, setShowSettings] = useState(false);

  // Banner is hidden once the user has made a choice.
  if (consent.decided) return null;

  const isDialogOpen = showSettings || settingsPanelOpen;

  return (
    <div
      role="region"
      aria-label="Cookie-Einstellungen"
      className="fixed bottom-0 left-0 right-0 z-50 border-t bg-card shadow-lg"
    >
      <div className="max-w-2xl mx-auto px-6 py-4 space-y-3">
        <p className="text-sm font-semibold">Wir verwenden Cookies</p>
        <p className="text-sm text-muted-foreground">
          Diese Website verwendet notwendige Cookies für Authentifizierung und Sicherheit.
          Mit Ihrer Zustimmung aktivieren wir außerdem Fehlerberichte (Sentry), um die Qualität
          zu verbessern.
        </p>
        <div className="flex flex-wrap items-center gap-2">
          <Button onClick={acceptAll}>Alle akzeptieren</Button>
          <Button variant="outline" onClick={acceptNecessary}>
            Nur notwendige
          </Button>
          <Button
            variant="ghost"
            onClick={() => setShowSettings(true)}
          >
            Einstellungen
          </Button>
        </div>
      </div>

      <ConsentSettingsDialog
        open={isDialogOpen}
        onOpenChange={(open) => {
          setShowSettings(open);
          if (!open) closeSettings();
        }}
      />
    </div>
  );
}
