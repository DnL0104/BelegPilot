"use client";

import { Header } from "@/components/layout/header";
import { UploadForm } from "@/components/upload/upload-form";

export function UploadClient() {
  return (
    <>
      <Header title="Belege hochladen" />
      <div className="flex-1 p-6 overflow-auto">
        <UploadForm />
      </div>
    </>
  );
}
