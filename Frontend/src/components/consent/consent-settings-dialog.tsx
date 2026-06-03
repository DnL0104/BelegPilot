"use client";

import { useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Separator } from "@/components/ui/separator";
import { useConsent } from "@/providers/consent-provider";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ConsentSettingsDialog({ open, onOpenChange }: Props) {
  const { consent, updateConsent, closeSettings } = useConsent();

  // Local state initialized from current consent so the user sees their previous choice.
  // For a fresh visitor, consent.fehleranalyse is false (not pre-ticked — TTDSG T-06-21).
  const [fehleranalyse, setFehleranalyse] = useState(consent.fehleranalyse);

  // Reset local state when dialog opens so it reflects the current stored consent.
  const handleOpenChange = (next: boolean) => {
    if (next) {
      setFehleranalyse(consent.fehleranalyse);
    }
    onOpenChange(next);
  };

  const save = () => {
    updateConsent({ fehleranalyse, decided: true });
    closeSettings();
    onOpenChange(false);
  };

  const cancel = () => {
    onOpenChange(false);
    closeSettings();
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Cookie-Einstellungen</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* Notwendig — always on, cannot be toggled */}
          <div className="flex items-start gap-3">
            <Checkbox
              id="notwendig-checkbox"
              checked
              disabled
              aria-disabled="true"
              className="mt-0.5 opacity-50 cursor-not-allowed"
            />
            <div>
              <label
                htmlFor="notwendig-checkbox"
                className="text-sm font-semibold cursor-not-allowed opacity-50"
              >
                Notwendig
              </label>
              <p className="text-sm text-muted-foreground">
                Authentifizierung, Sicherheit und Sitzungsverwaltung. Immer aktiv.
              </p>
            </div>
          </div>

          <Separator />

          {/* Fehleranalyse — opt-in, defaults unchecked for fresh visitors */}
          <div className="flex items-start gap-3">
            <Checkbox
              id="fehleranalyse-checkbox"
              checked={fehleranalyse}
              onCheckedChange={(checked) => setFehleranalyse(checked === true)}
              className="mt-0.5"
            />
            <div>
              <label
                htmlFor="fehleranalyse-checkbox"
                className="text-sm font-semibold cursor-pointer"
              >
                Fehleranalyse
              </label>
              <p className="text-sm text-muted-foreground">
                Anonyme Fehlerberichte über Sentry (EU-Server) zur Qualitätsverbesserung.
                Kein Tracking.
              </p>
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={cancel}>
            Abbrechen
          </Button>
          <Button onClick={save}>Einstellungen speichern</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
