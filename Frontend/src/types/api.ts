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
  failedCount: number;     // items with ClassificationStatus.Failed — separate from unknownCount (Pitfall 2)
  hasSumMismatch: boolean;
}

// --- Batch upload response (202 Accepted — D-03) ---

/** One entry per uploaded file in the 202 Accepted response. */
export interface UploadAcceptedFile {
  receiptFileId: string;
  jobId: string;
  fileName: string;
}

/** A file skipped during upload because its content already exists for this user. */
export interface DuplicateFileInfo {
  fileName: string;
  reason: string;
}

/** POST /receipt-files → 202 Accepted response body. */
export interface UploadAcceptedResponse {
  files: UploadAcceptedFile[];
  duplicates: DuplicateFileInfo[];
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
  confidenceTier?: "HIGH" | "MEDIUM" | "LOW" | null;  // D-01: derived from Confidence; null for manual/rule items
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
  receiptId?: string;
}

// "Processed" is the FileStatus string returned by GET /receipt-files for a completed file.
// It does not match any ProcessingStatus value, so we cast it here to keep the union open
// and avoid a silent non-terminal state when ReceiptFile.status is used as ProcessingStatus.
export const TERMINAL_STATUSES: readonly string[] = [
  "Completed",  // ProcessingStatus terminal
  "Processed",  // FileStatus terminal (ReceiptFile.Status on success)
  "Failed",     // terminal in both enums
  "Cancelled",  // ProcessingStatus terminal
];

export function isTerminal(status: ProcessingStatus | string): boolean {
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
