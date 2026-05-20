export function formatCurrency(amount: number): string {
  return new Intl.NumberFormat("de-DE", {
    style: "currency",
    currency: "EUR",
  }).format(amount);
}

export function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString("de-DE", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  });
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function categoryLabel(category: string): string {
  const labels: Record<string, string> = {
    ConsumablesAndOfficeSupplies: "Bürobedarf & Verbrauchsmaterial",
    SpecialistLiterature: "Fachliteratur",
    TeachingMaterials: "Unterrichtsmaterial",
    DigitalToolsAndSoftware: "Digitale Tools & Software",
    OfficeEquipment: "Büroausstattung",
    TravelAndCommuting: "Fahrtkosten & Reisen",
    ProfessionalDevelopment: "Fortbildung",
    Unknown: "Nicht zugeordnet",
  };
  return labels[category] ?? category;
}

export function statusLabel(status: string): string {
  const labels: Record<string, string> = {
    Uploaded: "Hochgeladen",
    Processing: "Wird verarbeitet",
    Processed: "Verarbeitet",
    Failed: "Fehlgeschlagen",
  };
  return labels[status] ?? status;
}
