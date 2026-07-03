import type { Metadata } from "next";
import { ReportsClient } from "./reports-client";

export const metadata: Metadata = {
  title: "Jahresbericht | BelegPilot",
};

export default function ReportsPage() {
  return <ReportsClient />;
}
