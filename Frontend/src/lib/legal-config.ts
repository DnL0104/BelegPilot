// Single source of truth for operator legal identity.
// Used by: Impressum, Datenschutz, AGB, Widerruf pages + Stripe invoice footer.
// Operator must fill these placeholders once ladungsfähige Geschäftsadresse + legal
// name are obtained. Lawyer must confirm the address is §5 DDG / §14 UStG-compliant (D-12).
export const LEGAL_CONFIG = {
  name:        "[Name]",
  tradingName: "TaxReader",
  address:     "[Anschrift]",
  city:        "[PLZ Ort]",
  email:       "[kontakt@taxreader.de]",
} as const;

// Per-page reviewed flags. Set to true ONLY after the lawyer has signed off that page.
// DraftWarning reads this flag and hides the Entwurf banner when true (D-11).
// Staggered unlock: flip each page independently as lawyer sign-off arrives.
export const LEGAL_REVIEWED = {
  impressum:   false,
  datenschutz: false,
  agb:         false,
  widerruf:    false,
} as const satisfies Record<string, boolean>;
