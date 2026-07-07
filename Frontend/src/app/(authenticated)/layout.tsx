"use client";

import { Loader2 } from "lucide-react";
import { SidebarProvider, SidebarInset } from "@/components/ui/sidebar";
import { AppSidebar } from "@/components/layout/app-sidebar";
import { Footer } from "@/components/layout/footer";
import { CookieBanner } from "@/components/consent/cookie-banner";
import { useAuth } from "@/providers/auth-provider";

export default function AuthenticatedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <Loader2 className="h-8 w-8 animate-spin text-primary" />
          <span className="text-sm text-muted-foreground">Laden...</span>
        </div>
      </div>
    );
  }

  if (!user) return null;

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset className="overflow-y-auto" id="main-content">
        {children}
        <Footer />
      </SidebarInset>
      <CookieBanner />
    </SidebarProvider>
  );
}
