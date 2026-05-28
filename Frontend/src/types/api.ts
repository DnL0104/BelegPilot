export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: AuthUser;
}

export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
}

export interface ReceiptFile {
  id: string;
  originalFileName: string;
  fileSize: number;
  sourceHint: string | null;
  yearHint: number | null;
  uploadedBy: string | null;
  uploadedAt: string;
  status: string;
}

export interface Receipt {
  id: string;
  receiptFileId: string;
  vendor: string;
  purchaseDate: string;
  subTotal: number;
  taxAmount: number;
  totalAmount: number;
  currency: string;
  parsedAt: string;
  itemCount: number;
  suggestedCount: number;
  unknownCount: number;
  hasSumMismatch: boolean;
}

// --- Batch upload response (202 Accepted — D-03) ---

/** One entry per uploaded file in the 202 Accepted response. */
export interface UploadAcceptedFile {
  receiptFileId: string;
  jobId: string;
  fileName: string;
}

/** POST /receipt-files → 202 Accepted response body. */
export interface UploadAcceptedResponse {
  files: UploadAcceptedFile[];
}

export interface ReceiptItem {
  id: string;
  receiptId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  lineNumber: number;
  latestClassification: ItemClassification | null;
}

export interface ItemClassification {
  id: string;
  category: string;
  method: string;
  status: string;
  reason: string;
  classifiedAt: string;
}

export interface ClassificationRule {
  id: string;
  userId: string | null;
  vendorPattern: string | null;
  sourceFilePattern: string | null;
  descriptionPattern: string | null;
  category: Category;
  priority: number;
  isActive: boolean;
  createdAt: string;
}

export interface CategoryTotal {
  category: string;
  totalAmount: number;
  itemCount: number;
}

export interface AnnualSummary {
  year: number;
  totalReceipts: number;
  totalAmount: number;
  categoryBreakdown: CategoryTotal[];
  unclassifiedItemCount: number;
}

export interface TokenBalance {
  balance: number;
  updatedAt: string;
}

export interface TokenTransaction {
  id: string;
  type: string;
  amount: number;
  balanceAfter: number;
  description: string;
  relatedItemId: string | null;
  createdAt: string;
}

export interface PendingSuggestion {
  itemId: string;
  receiptId: string;
  description: string;
  totalPrice: number;
  vendor: string;
  purchaseDate: string;
  category: string;
  reason: string;
  confidence: number | null;
  classifiedAt: string;
}

export type Category =
  | "Unbekannt"
  | "WerbungskostenArbeitsmittel"
  | "WerbungskostenFachliteratur"
  | "WerbungskostenBueromaterial"
  | "WerbungskostenReisekosten"
  | "WerbungskostenFortbildung"
  | "WerbungskostenTelekommunikation"
  | "SonderausgabenSpenden"
  | "SonderausgabenVorsorgeaufwendungen"
  | "AussergewoehnlicheBelastungenKrankheit"
  | "HaushaltsnaheDienstleistung"
  | "Handwerkerleistung"
  | "Privat";

/** Mirrors backend ProcessingStatus enum (D-06). PascalCase to match string-serialised enum. */
export type ProcessingStatus =
  | "Pending"
  | "Queued"
  | "Extracting"
  | "Parsing"
  | "Classifying"
  | "Completed"
  | "Failed"
  | "Cancelled";

/** Stable error code enum (D-13/D-21) for client-side switching. */
export type ReceiptFileErrorCode =
  | "NoTextExtracted"
  | "ParserMissing"
  | "AiUnavailable"
  | "InsufficientTokens"
  | "Cancelled"
  | "Unknown";

/** GET /receipt-files/{id}/status response shape (D-13). */
export interface ReceiptFileStatus {
  status: ProcessingStatus;
  updatedAt: string;
  errorCode?: ReceiptFileErrorCode;
  errorMessage?: string;
}

export const TERMINAL_STATUSES: readonly ProcessingStatus[] = [
  "Completed",
  "Failed",
  "Cancelled",
];

export function isTerminal(status: ProcessingStatus): boolean {
  return TERMINAL_STATUSES.includes(status);
}

// --- Payments ---

export interface CheckoutSession {
  checkoutUrl: string;
  isDemoMode: boolean;
}

export interface Invoice {
  id: string;
  number: string | null;
  amountPaid: number;
  currency: string;
  created: string;
  invoicePdfUrl: string | null;
  hostedInvoiceUrl: string | null;
}

export interface PortalSession {
  url: string;
}

export interface CreateCheckoutSessionRequest {
  credits: number;
  waiverAccepted: boolean;
  agbAccepted: boolean;
}
