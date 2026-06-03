import Link from "next/link";
import { CookieSettingsLink } from "@/components/layout/cookie-settings-link";

export function Footer() {
  return (
    <footer className="border-t bg-card px-6 py-4">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-muted-foreground">
        <Link href="/impressum" className="hover:text-foreground hover:underline">
          Impressum
        </Link>
        <Link href="/datenschutz" className="hover:text-foreground hover:underline">
          Datenschutzerklärung
        </Link>
        <Link href="/agb" className="hover:text-foreground hover:underline">
          AGB
        </Link>
        <Link href="/widerruf" className="hover:text-foreground hover:underline">
          Widerrufsbelehrung
        </Link>
        <CookieSettingsLink />
      </div>
    </footer>
  );
}
