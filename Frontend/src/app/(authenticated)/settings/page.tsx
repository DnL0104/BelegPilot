import type { Metadata } from "next";
import { SettingsClient } from "./settings-client";

export const metadata: Metadata = {
  title: "Einstellungen | BelegPilot",
};

export default function SettingsPage() {
  return <SettingsClient />;
}
