"use client";

import { createContext, useCallback, useContext, useEffect, useState } from "react";
import * as Sentry from "@sentry/nextjs";
import { scrubEvent } from "@/lib/sentry-scrubber";

interface ConsentState {
  notwendig: true;
  fehleranalyse: boolean;
  decided: boolean;
}

interface ConsentContextValue {
  consent: ConsentState;
  acceptAll: () => void;
  acceptNecessary: () => void;
  updateConsent: (state: Partial<ConsentState>) => void;
  reopenSettings: () => void;
  settingsPanelOpen: boolean;
  closeSettings: () => void;
}

const CONSENT_KEY = "taxreader-consent";

const DEFAULT_CONSENT: ConsentState = {
  notwendig: true,
  fehleranalyse: false,
  decided: false,
};

const ConsentContext = createContext<ConsentContextValue | null>(null);

function sentryConfig() {
  return {
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? "production",
    sendDefaultPii: false,
    tracesSampleRate: 0,
    replaysOnErrorSampleRate: 0,
    replaysSessionSampleRate: 0,
    beforeSend: (e: Parameters<typeof scrubEvent>[0]) => scrubEvent(e),
  };
}

// Guard against double-init (instrumentation-client.ts may have already called Sentry.init
// if the user had consent from a prior session — Pitfall 3 in 06-RESEARCH.md).
function grantSentry() {
  if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true" && !Sentry.isInitialized()) {
    Sentry.init(sentryConfig());
  }
}

// Pitfall 7: Sentry.close() is async (returns Promise<boolean>). Fire-and-forget is
// intentional here — the UI should not block. No page reload (D-07).
function revokeSentry() {
  if (Sentry.isInitialized()) {
    void Sentry.close(2000);
  }
}

function persist(state: ConsentState) {
  localStorage.setItem(CONSENT_KEY, JSON.stringify(state));
}

export function ConsentProvider({ children }: { children: React.ReactNode }) {
  const [consent, setConsent] = useState<ConsentState>(DEFAULT_CONSENT);
  const [settingsPanelOpen, setSettingsPanelOpen] = useState(false);

  // On mount, read localStorage synchronously (no isLoading needed).
  useEffect(() => {
    try {
      const raw = localStorage.getItem(CONSENT_KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as Partial<ConsentState>;
        setConsent({
          notwendig: true,
          fehleranalyse: parsed.fehleranalyse ?? false,
          decided: parsed.decided ?? false,
        });
      }
    } catch {
      // Malformed localStorage entry — reset to default (banner will show again).
      localStorage.removeItem(CONSENT_KEY);
    }
  }, []);

  const acceptAll = useCallback(() => {
    const next: ConsentState = { notwendig: true, fehleranalyse: true, decided: true };
    setConsent(next);
    persist(next);
    grantSentry();
  }, []);

  const acceptNecessary = useCallback(() => {
    const next: ConsentState = { notwendig: true, fehleranalyse: false, decided: true };
    setConsent(next);
    persist(next);
    revokeSentry();
  }, []);

  const updateConsent = useCallback((partial: Partial<ConsentState>) => {
    setConsent((prev) => {
      const next: ConsentState = {
        notwendig: true,
        fehleranalyse: partial.fehleranalyse ?? prev.fehleranalyse,
        decided: partial.decided ?? prev.decided,
      };
      persist(next);
      if (next.fehleranalyse) {
        grantSentry();
      } else {
        revokeSentry();
      }
      return next;
    });
  }, []);

  const reopenSettings = useCallback(() => {
    setSettingsPanelOpen(true);
  }, []);

  const closeSettings = useCallback(() => {
    setSettingsPanelOpen(false);
  }, []);

  return (
    <ConsentContext
      value={{
        consent,
        acceptAll,
        acceptNecessary,
        updateConsent,
        reopenSettings,
        settingsPanelOpen,
        closeSettings,
      }}
    >
      {children}
    </ConsentContext>
  );
}

export function useConsent() {
  const context = useContext(ConsentContext);
  if (!context) throw new Error("useConsent must be used within ConsentProvider");
  return context;
}
