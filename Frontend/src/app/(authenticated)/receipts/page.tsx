import type { Metadata } from "next";
import { ReceiptsClient } from "./receipts-client";

export const metadata: Metadata = {
  title: "Meine Belege | BelegPilot",
};

export default function ReceiptsPage() {
  return <ReceiptsClient />;
}
