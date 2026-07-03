import type { Metadata } from "next";
import { BillingClient } from "./billing-client";

export const metadata: Metadata = {
  title: "Credits & Abrechnung | BelegPilot",
};

export default function BillingPage() {
  return <BillingClient />;
}
