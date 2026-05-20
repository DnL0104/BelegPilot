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
}

// --- Batch upload response ---

export type FailureKind = "Duplicate" | "ProcessingError";

export interface SuccessfulUpload {
  fileName: string;
  receipt: Receipt;
}

export interface FailedUpload {
  fileName: string;
  reason: string;
  kind: FailureKind;
}

/**
 * Per-file outcome of a batch upload. The backend never aborts a batch on a
 * single file's failure — callers render `successful` and `failed` side-by-side.
 */
export interface UploadReceiptFilesResponse {
  successful: SuccessfulUpload[];
  failed: FailedUpload[];
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
  | "Unknown"
  | "ConsumablesAndOfficeSupplies"
  | "SpecialistLiterature"
  | "TeachingMaterials"
  | "DigitalToolsAndSoftware"
  | "OfficeEquipment"
  | "TravelAndCommuting"
  | "ProfessionalDevelopment";
