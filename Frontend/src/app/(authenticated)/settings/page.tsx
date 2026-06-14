"use client";

import { useState, useEffect } from "react";
import { toast } from "sonner";
import { Loader2, Save, Sparkles, Info, TriangleAlert, Download } from "lucide-react";
import { Header } from "@/components/layout/header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useSettings, useUpdateSettings } from "@/hooks/use-settings";
import { deleteAccount, downloadExportBundle } from "@/lib/api-client";
import { useAuth } from "@/providers/auth-provider";
import { useDataExport } from "@/hooks/use-data-export";

const THRESHOLD_STEPS = [
  { value: null, label: "Aus", description: "Alle Vorschläge manuell bestätigen" },
  { value: 0.5, label: "50 %", description: "Niedrige Schwelle — viel wird automatisch bestätigt" },
  { value: 0.6, label: "60 %" },
  { value: 0.7, label: "70 %" },
  { value: 0.75, label: "75 %", description: "Empfohlen — guter Kompromiss" },
  { value: 0.8, label: "80 %" },
  { value: 0.85, label: "85 %" },
  { value: 0.9, label: "90 %", description: "Hohe Schwelle — nur sehr sichere Ergebnisse" },
  { value: 0.95, label: "95 %" },
] as const;

export default function SettingsPage() {
  const { logout } = useAuth();
  const { data: settings, isLoading } = useSettings();
  const updateMutation = useUpdateSettings();

  const [threshold, setThreshold] = useState<number | null>(null);
  const [hasChanges, setHasChanges] = useState(false);

  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [password, setPassword] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  // Data export state (DSGVO Art. 20 / LEG-07)
  const {
    requestExport,
    isRequesting,
    exportToken,
    status: exportStatus,
  } = useDataExport();
  const [isDownloading, setIsDownloading] = useState(false);

  const handleDownloadExport = async () => {
    if (!exportToken) return;
    setIsDownloading(true);
    try {
      const blob = await downloadExportBundle(exportToken);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "taxreader-export.zip";
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch {
      toast.error("Export fehlgeschlagen. Bitte erneut versuchen.");
    } finally {
      setIsDownloading(false);
    }
  };

  useEffect(() => {
    if (settings) {
      setThreshold(settings.autoConfirmThreshold);
      setHasChanges(false);
    }
  }, [settings]);

  const handleThresholdChange = (value: number | null) => {
    setThreshold(value);
    setHasChanges(value !== settings?.autoConfirmThreshold);
  };

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({ autoConfirmThreshold: threshold });
      setHasChanges(false);
      toast.success("Einstellungen gespeichert");
    } catch {
      toast.error("Speichern fehlgeschlagen");
    }
  };

  const handleDeleteAccount = async () => {
    if (password.length === 0) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      await deleteAccount(password);
      logout();  // clears tokens + navigates to /login
    } catch (err) {
      const status = (err as { response?: { status?: number } }).response?.status;
      if (status === 401) {
        // D-11: surface wrong-password inline; do NOT close the dialog.
        setDeleteError("Ungültiges Passwort.");
      } else {
        toast.error("Konto konnte nicht gelöscht werden. Bitte erneut versuchen.");
      }
      setIsDeleting(false);
    }
  };

  const currentStep = THRESHOLD_STEPS.find((s) => s.value === threshold);

  return (
    <>
      <Header title="Einstellungen" />
      <div className="flex-1 p-6 overflow-auto">
        <div className="mx-auto w-full max-w-2xl space-y-6">
          {/* Auto-Confirm */}
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <div className="flex items-start gap-3 mb-5">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-400">
                <Sparkles className="h-5 w-5" />
              </div>
              <div>
                <h2 className="text-lg font-semibold">Automatische Bestätigung</h2>
                <p className="mt-1 text-sm text-muted-foreground">
                  Wenn die KI eine Klassifizierung mit einer Konfidenz über dem Schwellenwert vorschlägt,
                  wird sie automatisch bestätigt — ohne manuellen Klick.
                </p>
              </div>
            </div>

            {isLoading ? (
              <div className="flex items-center justify-center py-8">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            ) : (
              <div className="space-y-4">
                <div className="grid grid-cols-3 sm:grid-cols-5 gap-2">
                  {THRESHOLD_STEPS.map((step) => (
                    <button
                      key={step.label}
                      onClick={() => handleThresholdChange(step.value)}
                      className={`rounded-lg border px-3 py-2.5 text-sm font-medium transition-colors ${
                        threshold === step.value
                          ? step.value === null
                            ? "border-muted-foreground bg-muted text-foreground"
                            : "border-primary bg-primary/10 text-primary"
                          : "border-border bg-card text-muted-foreground hover:border-primary/50 hover:text-foreground"
                      }`}
                    >
                      {step.label}
                    </button>
                  ))}
                </div>

                {currentStep && "description" in currentStep && currentStep.description && (
                  <div className="flex items-start gap-2 rounded-lg bg-muted/50 px-3 py-2.5">
                    <Info className="h-4 w-4 shrink-0 text-muted-foreground mt-0.5" />
                    <p className="text-sm text-muted-foreground">
                      {currentStep.description}
                    </p>
                  </div>
                )}

                {threshold !== null && (
                  <div className="rounded-lg border border-border p-4 space-y-2">
                    <p className="text-sm font-medium">Beispiel</p>
                    <div className="space-y-1.5 text-sm text-muted-foreground">
                      <div className="flex items-center justify-between">
                        <span>„Druckerpatrone“ — KI: 96 %</span>
                        <span className="font-medium text-emerald-600 dark:text-emerald-400">
                          ✓ Auto-bestätigt
                        </span>
                      </div>
                      <div className="flex items-center justify-between">
                        <span>„USB-Adapter 3-in-1“ — KI: {Math.round(threshold * 100 - 10)} %</span>
                        <span className="font-medium text-amber-600 dark:text-amber-400">
                          → Manuell prüfen
                        </span>
                      </div>
                    </div>
                  </div>
                )}

                <div className="flex items-center justify-between pt-2">
                  <p className="text-xs text-muted-foreground">
                    Aktuell: {settings?.autoConfirmThreshold
                      ? `≥ ${Math.round(settings.autoConfirmThreshold * 100)} % wird automatisch bestätigt`
                      : "Deaktiviert — alle Vorschläge manuell"}
                  </p>
                  <Button
                    onClick={handleSave}
                    disabled={!hasChanges || updateMutation.isPending}
                    size="sm"
                  >
                    {updateMutation.isPending ? (
                      <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                    ) : (
                      <Save className="mr-1.5 h-3.5 w-3.5" />
                    )}
                    Speichern
                  </Button>
                </div>
              </div>
            )}
          </div>
          {/* Data Export — DSGVO Art. 20 */}
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <div className="flex items-start gap-3 mb-5">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-blue-100 text-blue-700 dark:bg-blue-950/50 dark:text-blue-400">
                <Download className="h-5 w-5" />
              </div>
              <div>
                <h2 className="text-lg font-semibold">Meine Daten exportieren</h2>
                <p className="mt-1 text-sm text-muted-foreground">
                  Erstellt einen vollständigen Export Ihrer Daten (Belege, Klassifizierungen, Token-Transaktionen) als ZIP-Archiv gemäß DSGVO Art. 20. Der Link ist 24 Stunden gültig.
                </p>
              </div>
            </div>

            <div className="space-y-3">
              {/* Idle or error state: show trigger button */}
              {(exportStatus === null || exportStatus === "Expired") && (
                <>
                  {exportStatus === "Expired" && (
                    <p className="text-sm text-amber-600 dark:text-amber-400">
                      Der Export-Link ist abgelaufen. Bitte fordern Sie einen neuen Export an.
                    </p>
                  )}
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => requestExport()}
                    disabled={isRequesting}
                  >
                    {isRequesting ? (
                      <>
                        <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                        Wird erstellt…
                      </>
                    ) : (
                      <>
                        <Download className="mr-1.5 h-3.5 w-3.5" />
                        Daten exportieren
                      </>
                    )}
                  </Button>
                </>
              )}

              {/* Generating state */}
              {exportStatus === "Generating" && (
                <div className="flex items-center gap-2">
                  <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                  <p className="text-sm text-muted-foreground">
                    Ihr Export wird erstellt. Dies kann einige Sekunden dauern.
                  </p>
                </div>
              )}

              {/* Ready state */}
              {exportStatus === "Ready" && (
                <div className="flex items-center gap-3">
                  <span className="inline-flex items-center rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-400">
                    Export bereit
                  </span>
                  <Button
                    size="sm"
                    onClick={handleDownloadExport}
                    disabled={isDownloading}
                  >
                    {isDownloading ? (
                      <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                    ) : (
                      <Download className="mr-1.5 h-3.5 w-3.5" />
                    )}
                    Herunterladen
                  </Button>
                </div>
              )}
            </div>
          </div>

          {/* Danger Zone */}
          <div className="rounded-xl border border-destructive/40 bg-card p-6 shadow-sm">
            <div className="flex items-start gap-3 mb-5">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-rose-100 text-rose-700 dark:bg-rose-950/50 dark:text-rose-400">
                <TriangleAlert className="h-5 w-5" />
              </div>
              <div>
                <h2 className="text-lg font-semibold">Konto löschen</h2>
                <p className="mt-1 text-sm text-muted-foreground">
                  Löscht dein Konto und alle damit verbundenen Daten unwiderruflich —
                  Belege, Artikel, Klassifizierungen und Token-Guthaben.
                </p>
              </div>
            </div>
            <Button
              variant="destructive"
              size="sm"
              onClick={() => {
                setPassword("");
                setDeleteError(null);
                setDeleteDialogOpen(true);
              }}
            >
              Konto unwiderruflich löschen
            </Button>
          </div>
        </div>
      </div>

      <Dialog
        open={deleteDialogOpen}
        onOpenChange={(open) => {
          if (isDeleting) return;
          setDeleteDialogOpen(open);
          if (!open) {
            setPassword("");
            setDeleteError(null);
          }
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Konto wirklich löschen?</DialogTitle>
            <DialogDescription>
              Diese Aktion kann nicht rückgängig gemacht werden. Alle Belege,
              Artikel, Klassifizierungen und dein Token-Guthaben werden
              dauerhaft gelöscht.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3 py-2">
            <p className="text-sm text-muted-foreground">
              Geben Sie zur Bestätigung Ihr Passwort ein.
            </p>
            <Input
              type="password"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value);
                setDeleteError(null);
              }}
              placeholder="Passwort"
              disabled={isDeleting}
              autoComplete="current-password"
              onKeyDown={(e) => {
                if (e.key === "Enter" && password.length >= 1) {
                  handleDeleteAccount();
                }
              }}
            />
            {deleteError && (
              <p className="text-sm text-rose-600 dark:text-rose-400">{deleteError}</p>
            )}
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setDeleteDialogOpen(false)}
              disabled={isDeleting}
            >
              Abbrechen
            </Button>
            <Button
              variant="destructive"
              onClick={handleDeleteAccount}
              disabled={password.length === 0 || isDeleting}
            >
              {isDeleting && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
              Konto löschen
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
