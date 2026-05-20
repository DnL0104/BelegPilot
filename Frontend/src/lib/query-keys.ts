export const queryKeys = {
  receiptFiles: {
    all: ["receipt-files"] as const,
  },
  receipts: {
    all: ["receipts"] as const,
    list: (year?: number) => ["receipts", { year }] as const,
    detail: (id: string) => ["receipts", id] as const,
    items: (id: string) => ["receipts", id, "items"] as const,
  },
  reports: {
    categoryTotals: (year: number) =>
      ["reports", "category-totals", year] as const,
    annualSummary: (year: number) =>
      ["reports", "annual-summary", year] as const,
  },
  tokens: {
    balance: ["tokens", "balance"] as const,
    transactions: (take: number) => ["tokens", "transactions", take] as const,
  },
  classifications: {
    pendingSuggestions: ["classifications", "pending-suggestions"] as const,
  },
  settings: {
    all: ["settings"] as const,
  },
};
