"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Upload,
  Receipt,
  BarChart3,
  Settings,
  LogOut,
  FileText,
  ShieldCheck,
  CreditCard,
} from "lucide-react";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar";
import { useAuth } from "@/providers/auth-provider";
import { TokenBalanceBadge } from "@/components/tokens/token-balance-badge";

const navItems = [
  { title: "Übersicht", href: "/", icon: LayoutDashboard },
  { title: "Belege hochladen", href: "/upload", icon: Upload },
  { title: "Meine Belege", href: "/receipts", icon: Receipt },
  { title: "Berichte", href: "/reports", icon: BarChart3 },
  { title: "Credits & Abrechnung", href: "/billing", icon: CreditCard },
  { title: "Einstellungen", href: "/settings", icon: Settings },
];

const legalItems = [
  { title: "Impressum", href: "/impressum", icon: FileText },
  { title: "Datenschutz", href: "/datenschutz", icon: ShieldCheck },
];

function UserAvatar({ name }: { name: string }) {
  const initials = name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  return (
    <div className="flex size-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary to-primary/80 text-[13px] font-semibold text-white">
      {initials}
    </div>
  );
}

export function AppSidebar() {
  const pathname = usePathname();
  const { user, logout } = useAuth();

  return (
    <Sidebar>
      <SidebarHeader className="border-b border-sidebar-border px-5 py-5">
        <Link href="/" className="flex items-center gap-2.5">
          <div className="flex size-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-white">
            B
          </div>
          <span className="text-lg font-bold tracking-tight text-sidebar-primary-foreground">
            BelegPilot
          </span>
        </Link>
      </SidebarHeader>

      <SidebarContent>
        {/* Main navigation */}
        <SidebarGroup>
          <SidebarGroupLabel className="text-[11px] uppercase tracking-wider text-sidebar-foreground/50">
            Navigation
          </SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {navItems.map((item) => (
                <SidebarMenuItem key={item.href}>
                  <SidebarMenuButton
                    render={<Link href={item.href} />}
                    isActive={
                      item.href === "/"
                        ? pathname === "/"
                        : pathname.startsWith(item.href)
                    }
                  >
                    <item.icon className="h-[18px] w-[18px]" />
                    <span className="text-[14px] font-medium">{item.title}</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>

        {/* Legal pages */}
        <SidebarGroup className="mt-auto">
          <SidebarGroupLabel className="text-[11px] uppercase tracking-wider text-sidebar-foreground/50">
            Rechtliches
          </SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {legalItems.map((item) => (
                <SidebarMenuItem key={item.href}>
                  <SidebarMenuButton
                    render={<Link href={item.href} />}
                    isActive={pathname === item.href}
                  >
                    <item.icon className="h-[18px] w-[18px]" />
                    <span className="text-[14px] font-medium">{item.title}</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>

      {/* Persistent token balance above the footer */}
      <div className="px-4 pb-3">
        <TokenBalanceBadge />
      </div>

      <SidebarFooter className="border-t border-sidebar-border p-4">
        {user && (
          <div className="flex items-center gap-2.5">
            <UserAvatar name={user.displayName} />
            <div className="min-w-0 flex-1">
              <p className="truncate text-[13px] font-semibold text-sidebar-primary-foreground">
                {user.displayName}
              </p>
              <p className="truncate text-[11px] text-sidebar-foreground">
                {user.email}
              </p>
            </div>
            <button
              type="button"
              onClick={logout}
              className="rounded-md p-1.5 text-sidebar-foreground transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2"
              aria-label="Abmelden"
            >
              <LogOut className="h-4 w-4" aria-hidden="true" />
            </button>
          </div>
        )}
        <p className="mt-3 text-[10px] leading-tight text-sidebar-foreground/35">
          Keine Steuerberatung. Klassifizierungen sind Vorschläge.
        </p>
      </SidebarFooter>
    </Sidebar>
  );
}
