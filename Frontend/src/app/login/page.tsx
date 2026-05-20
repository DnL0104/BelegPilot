"use client";

import { useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/providers/auth-provider";

export default function LoginPage() {
  const { login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await login(email, password);
    } catch {
      toast.error("Anmeldung fehlgeschlagen. Bitte E-Mail und Passwort prüfen.");
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
            Deine Belege. Automatisch klassifiziert.
          </h1>
          <p className="text-lg text-white/60">
            Lade PDF-Belege hoch, lasse sie automatisch auslesen und
            kategorisieren — perfekt für die Steuererklärung.
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
            <h2 className="text-2xl font-bold tracking-tight">Anmelden</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Melde dich an, um deine Belege zu verwalten
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
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
                autoFocus
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
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                minLength={8}
                className="h-11"
              />
            </div>

            <Button type="submit" className="h-11 w-full" disabled={loading}>
              {loading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Anmelden
            </Button>

            <p className="text-center text-sm text-muted-foreground">
              Noch kein Konto?{" "}
              <Link href="/register" className="font-medium text-primary hover:underline">
                Registrieren
              </Link>
            </p>
          </form>

          <p className="mt-8 text-center text-[11px] text-muted-foreground/60">
            <Link href="/impressum" className="hover:underline">Impressum</Link>
            {" · "}
            <Link href="/datenschutz" className="hover:underline">Datenschutz</Link>
          </p>
        </div>
      </div>
    </div>
  );
}
