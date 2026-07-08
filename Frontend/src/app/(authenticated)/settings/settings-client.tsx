"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Loader2, Download, User, Palette, ShieldCheck, TriangleAlert } from "lucide-react";
import { Header } from "@/components/layout/header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { ThemeToggle } from "@/components/layout/theme-toggle";
import { CookieSettingsLink } from "@/components/layout/cookie-settings-link";
import { useAuth } from "@/providers/auth-provider";
import { useDataExport } from "@/hooks/use-data-export";
import { useDeleteAccount, useDownloadExportBundle } from "@/hooks/use-account";

export function SettingsClient() {
  const { user, logout } = useAuth();

  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [password, setPassword] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const deleteAccountMutation = useDeleteAccount();

  // Data export state (DSGVO Art. 20 / LEG-07)
  const {
    requestExport,
    isRequesting,
    exportToken,
    status: exportStatus,
  } = useDataExport();
  const downloadExportMutation = useDownloadExportBundle();

  const handleDownloadExport = async () => {
    if (!exportToken) return;
    try {
      const blob = await downloadExportMutation.mutateAsync(exportToken);
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
    }
  };

  const handleDeleteAccount = async () => {
    if (password.length === 0) return;
    setDeleteError(null);
    try {
      await deleteAccountMutation.mutateAsync(password);
      logout(); // clears tokens + navigates to /login
    } catch (err) {
      const status = (err as { response?: { status?: number } }).response?.status;
      if (status === 401) {
        // D-11: surface wrong-password inline; do NOT close the dialog.
        setDeleteError("Ungültiges Passwort.");
      } else {
        toast.error("Ihr Konto konnte nicht gelöscht werden. Bitte versuchen Sie es erneut.");
      }
    }
  };

  return (
    <>
      <Header title="Einstellungen" />
      <div className="flex-1 p-6 overflow-auto">
        <div className="mx-auto w-full max-w-2xl space-y-6">
          {/* Konto */}
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <div className="flex items-start gap-3 mb-5">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                <User className="h-5 w-5" />
              </div>
              <div>
                <h2 className="text-lg font-semibold">Konto</h2>
                <p className="mt-1 text-sm text-muted-foreground">
                  Ihre Kontodaten.
                </p>
              </div>
            </div>
            <dl className="space-y-3 text-sm">
              <div className="flex items-center justify-between border-b border-border pb-3">
                <dt className="text-muted-foreground">E-Mail</dt>
                <dd className="font-medium">{user?.email}</dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-muted-foreground">Name</dt>
                <dd className="font-medium">{user?.displayName}</dd>
              </div>
            </dl>
          </div>

          {/* Erscheinungsbild */}
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-start gap-3">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <Palette className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold">Erscheinungsbild</h2>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Wählen Sie zwischen hellem und dunklem Erscheinungsbild.
                  </p>
                </div>
              </div>
              <ThemeToggle />
            </div>
          </div>

          {/* Datenschutz */}
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <div className="flex items-start gap-3 mb-5">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                <ShieldCheck className="h-5 w-5" />
              </div>
              <div>
                <h2 className="text-lg font-semibold">Datenschutz</h2>
                <p className="mt-1 text-sm text-muted-foreground">
                  Verwalten Sie Ihre Cookie-Einstellungen und exportieren Sie Ihre Daten.
                </p>
              </div>
            </div>

            <div className="space-y-5">
              <div className="flex items-center justify-between border-b border-border pb-5">
                <p className="text-sm font-medium">Cookie-Einstellungen anpassen</p>
                <CookieSettingsLink />
              </div>

              <div>
                <p className="text-sm font-medium mb-1">Meine Daten exportieren</p>
                <p className="mb-3 text-sm text-muted-foreground">
                  Erstellt einen vollständigen Export Ihrer Daten (Belege, Klassifizierungen,
                  Token-Transaktionen) als ZIP-Archiv gemäß DSGVO Art. 20. Der Link ist 24 Stunden
                  gültig.
                </p>

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
                      <span className="inline-flex items-center rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-medium text-primary">
                        Export bereit
                      </span>
                      <Button
                        size="sm"
                        onClick={handleDownloadExport}
                        disabled={downloadExportMutation.isPending}
                      >
                        {downloadExportMutation.isPending ? (
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
                  Löscht Ihr Konto und alle damit verbundenen Daten unwiderruflich — Belege,
                  Artikel, Klassifizierungen und Token-Guthaben.
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

      <AlertDialog
        open={deleteDialogOpen}
        onOpenChange={(open) => {
          if (deleteAccountMutation.isPending) return;
          setDeleteDialogOpen(open);
          if (!open) {
            setPassword("");
            setDeleteError(null);
          }
        }}
      >
        <AlertDialogContent className="sm:max-w-md">
          <AlertDialogHeader>
            <AlertDialogTitle>Konto löschen</AlertDialogTitle>
            <AlertDialogDescription>
              Ihr Konto und alle persönlichen Daten werden gemäß DSGVO dauerhaft gelöscht. Diese
              Aktion kann nicht rückgängig gemacht werden.
            </AlertDialogDescription>
          </AlertDialogHeader>

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
              disabled={deleteAccountMutation.isPending}
              autoComplete="current-password"
              aria-label="Passwort zur Bestätigung der Kontolöschung"
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

          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteAccountMutation.isPending}>
              Abbrechen
            </AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={handleDeleteAccount}
              disabled={password.length === 0 || deleteAccountMutation.isPending}
            >
              {deleteAccountMutation.isPending && (
                <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
              )}
              Konto unwiderruflich löschen
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
