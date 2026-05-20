"use client";

import { Header } from "@/components/layout/header";
import { UploadForm } from "@/components/upload/upload-form";

export default function UploadPage() {
  return (
    <>
      <Header title="Upload" />
      <div className="flex-1 p-6 overflow-auto">
        <UploadForm />
      </div>
    </>
  );
}
