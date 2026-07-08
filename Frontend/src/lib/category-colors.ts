// Grouped by hue family (Werbungskosten = blue, Sonderausgaben = amber) so related
// categories read as one family at a glance; each bar always carries a text label,
// so color is a secondary cue, not the sole identifier. Avoids red (reserved for
// --destructive), green (reserved for success states), and teal (reserved for
// --primary) to prevent semantic collisions.
export const CATEGORY_COLORS: Record<string, string> = {
  WerbungskostenArbeitsmittel: "bg-blue-400",
  WerbungskostenFachliteratur: "bg-blue-500",
  WerbungskostenBueromaterial: "bg-blue-600",
  WerbungskostenReisekosten: "bg-blue-700",
  WerbungskostenFortbildung: "bg-sky-500",
  WerbungskostenTelekommunikation: "bg-sky-600",
  SonderausgabenSpenden: "bg-amber-600",
  SonderausgabenVorsorgeaufwendungen: "bg-amber-800",
  AussergewoehnlicheBelastungenKrankheit: "bg-violet-500",
  HaushaltsnaheDienstleistung: "bg-rose-500",
  Handwerkerleistung: "bg-indigo-500",
  Privat: "bg-slate-400",
  Unbekannt: "bg-muted-foreground",
};
