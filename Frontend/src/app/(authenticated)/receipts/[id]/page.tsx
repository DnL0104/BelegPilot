import type { Metadata } from "next";
import { ReceiptDetailClient } from "./receipt-detail-client";

export async function generateMetadata(): Promise<Metadata> {
  // Static fallback — vendor name not available server-side without auth.
  return { title: "Belegdetails | BelegPilot" };
}

export default function ReceiptDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  return <ReceiptDetailClient params={params} />;
}
