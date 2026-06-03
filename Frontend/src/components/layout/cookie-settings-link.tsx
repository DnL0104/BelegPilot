"use client";

export function CookieSettingsLink() {
  // TODO(06-02): wire reopenSettings() from ConsentProvider once it lands in 06-02
  return (
    <button
      type="button"
      className="text-xs text-muted-foreground hover:text-foreground hover:underline"
      onClick={() => {}}
    >
      Cookie-Einstellungen
    </button>
  );
}
