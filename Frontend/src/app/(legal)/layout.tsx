import Link from "next/link";
import { ThemeToggle } from "@/components/layout/theme-toggle";

export default function LegalLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="min-h-screen bg-background">
      {/* Same height/border/padding as the app Header */}
      <header className="flex h-14 shrink-0 items-center justify-between border-b bg-card px-6">
        <Link href="/" className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-600 text-sm font-bold text-white">
            B
</div>
          <span className="text-lg font-bold tracking-tight">BelegPilot</span>
        </Link>
        <ThemeToggle />
      </header>
      <main>{children}</main>
    </div>
  );
}
