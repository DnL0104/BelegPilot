import type { Metadata } from "next";
import { UploadClient } from "./upload-client";

export const metadata: Metadata = {
  title: "Belege hochladen | BelegPilot",
};

export default function UploadPage() {
  return <UploadClient />;
}
