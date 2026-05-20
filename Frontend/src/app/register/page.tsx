"use client";

import { useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/providers/auth-provider";

export default function RegisterPage() {
  const { register } = useAuth();
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (password !== confirmPassword) {
      toast.error("Passwörter stimmen nicht überein.");
      return;
    }

    setLoading(true);
    try {
      await register(email, displayName, password);
      toast.success("Konto erstellt! Willkommen bei BelegPilot.");
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data
          ?.error ?? "Registrierung fehlgeschlagen.";
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen">
      {/* Left side - branding */}
      <div className="hidden w-1/2 items-center justify-center bg-gradient-to-br from-navy-950 to-navy-800 lg:flex">
        <div className="px-16 text-white">
          <div className="mb-6 flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-600 text-lg font-bold">
              B
</div>
            <span className="text-2xl font-bold tracking-tight">BelegPilot</span>
          </div>
          <h1 className="mb-3 text-3xl font-bold tracking-tight">
            Steuererklärung leicht gemacht.
          </h1>
          <p className="text-lg text-white/60">
            Registriere dich kostenlos und starte mit 10 Credits für die
            automatische KI-Klassifizierung deiner Belege.
          </p>
        </div>
      </div>

      {/* Right side - form */}
      <div className="flex w-full items-center justify-center p-8 lg:w-1/2">
        <div className="w-full max-w-sm">
          <div className="mb-8 text-center lg:text-left">
            <div className="mb-4 flex items-center justify-center gap-2 lg:hidden">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-600 text-sm font-bold text-white">
                B
  </div>
              <span className="text-lg font-bold">BelegPilot</span>
            </div>
            <h2 className="text-2xl font-bold tracking-tight">Konto erstellen</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Registriere dich, um mit der Belegverwaltung zu starten
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <label htmlFor="displayName" className="text-sm font-medium">
                Name
              </label>
              <Input
                id="displayName"
                type="text"
                placeholder="Max Mustermann"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                required
                autoFocus
                className="h-11"
              />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="email" className="text-sm font-medium">
                E-Mail
              </label>
              <Input
                id="email"
                type="email"
                placeholder="name@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                className="h-11"
              />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="password" className="text-sm font-medium">
                Passwort
              </label>
              <Input
                id="password"
                type="password"
                placeholder="Mind. 8 Zeichen"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                minLength={8}
                className="h-11"
              />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="confirmPassword" className="text-sm font-medium">
                Passwort bestätigen
              </label>
              <Input
                id="confirmPassword"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                minLength={8}
                className="h-11"
              />
            </div>

            <Button type="submit" className="h-11 w-full" disabled={loading}>
              {loading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Registrieren
            </Button>

            <p className="text-center text-sm text-muted-foreground">
              Bereits ein Konto?{" "}
              <Link href="/login" className="font-medium text-primary hover:underline">
                Anmelden
              </Link>
            </p>

            <p className="mt-4 text-center text-[11px] text-muted-foreground/70">
              Mit der Registrierung stimmst du unserer{" "}
              <Link href="/datenschutz" className="hover:underline">
                Datenschutzerklärung
              </Link>{" "}
              zu.
            </p>
          </form>

          <p className="mt-6 text-center text-[11px] text-muted-foreground/60">
            <Link href="/impressum" className="hover:underline">Impressum</Link>
            {" · "}
            <Link href="/datenschutz" className="hover:underline">Datenschutz</Link>
          </p>
        </div>
      </div>
    </div>
  );
}
