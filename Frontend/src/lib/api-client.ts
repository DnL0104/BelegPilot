import axios from "axios";
import type {
  AuthResponse,
  ReceiptFile,
  Receipt,
  ReceiptItem,
  ItemClassification,
  ClassificationRule,
  CategoryTotal,
  AnnualSummary,
  TokenBalance,
  TokenTransaction,
  PendingSuggestion,
  UploadAcceptedResponse,
  ReceiptFileStatus,
  CheckoutSession,
  Invoice,
  PortalSession,
  CreateCheckoutSessionRequest,
} from "@/types/api";

const api = axios.create({
  baseURL: "/api/v1",
});

// --- Auth token management ---

let accessToken: string | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken() {
  return accessToken;
}

// Attach bearer token to every request
api.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

// On 401, try to refresh; if refresh fails, redirect to login
let refreshPromise: Promise<string | null> | null = null;

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      // If a refresh is already in progress, wait for it
      if (!refreshPromise) {
        refreshPromise = tryRefreshToken().finally(() => {
          refreshPromise = null;
        });
      }

      const newToken = await refreshPromise;
      if (newToken) {
        originalRequest.headers.Authorization = `Bearer ${newToken}`;
        return api(originalRequest);
      }

      // Refresh failed — clear state and redirect to login
      clearAuthStorage();
      if (typeof window !== "undefined") {
        window.location.href = "/login";
      }
    }

    return Promise.reject(error);
  }
);

async function tryRefreshToken(): Promise<string | null> {
  const storedRefresh = typeof window !== "undefined"
    ? localStorage.getItem("refreshToken")
    : null;

  if (!storedRefresh) return null;

  try {
    const { data } = await axios.post<AuthResponse>("/api/v1/auth/refresh", {
      refreshToken: storedRefresh,
    });
    accessToken = data.accessToken;
    localStorage.setItem("refreshToken", data.refreshToken);
    localStorage.setItem("user", JSON.stringify(data.user));
    return data.accessToken;
  } catch {
    return null;
  }
}

function clearAuthStorage() {
  accessToken = null;
  if (typeof window !== "undefined") {
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("user");
  }
}

// --- Auth API ---

export async function register(email: string, displayName: string, password: string): Promise<AuthResponse> {
  const { data } = await axios.post<AuthResponse>("/api/v1/auth/register", {
    email,
    displayName,
    password,
  });
  return data;
}

export async function login(email: string, password: string): Promise<AuthResponse> {
  const { data } = await axios.post<AuthResponse>("/api/v1/auth/login", {
    email,
    password,
  });
  return data;
}

export function logout() {
  clearAuthStorage();
}

export async function deleteAccount(password: string): Promise<void> {
  // Use raw axios (not the shared `api` instance) so a 401 from wrong-password
  // surfaces inline instead of triggering the shared refresh-interceptor's
  // logout-and-redirect flow. Body for DELETE goes via the `data` config slot
  // (axios.delete's second arg is a CONFIG object, not a body).
  await axios.delete("/api/v1/auth/account", {
    headers: { Authorization: `Bearer ${getAccessToken()}` },
    data: { password },
  });
  clearAuthStorage();
}

export async function uploadReceiptFiles(
  files: File[]
): Promise<UploadAcceptedResponse> {
  const formData = new FormData();
  files.forEach((file) => formData.append("files", file));

  // Backend returns 202 Accepted with per-file { receiptFileId, jobId, fileName }
  // entries. Status polling is done via getReceiptFileStatus (D-13).
  const { data } = await api.post<UploadAcceptedResponse>(
    "/receipt-files",
    formData
  );
  return data;
}

export async function getReceiptFileStatus(
  id: string
): Promise<ReceiptFileStatus> {
  const { data } = await api.get<ReceiptFileStatus>(
    `/receipt-files/${id}/status`
  );
  return data;
}

export async function cancelReceiptFile(id: string): Promise<void> {
  // 204 No Content on success; 409 / 404 propagate as AxiosError for the
  // hook to switch on.
  await api.post(`/receipt-files/${id}/cancel`);
}

export async function retryReceiptFile(id: string): Promise<void> {
  // 204 No Content on success; 409 (not in a retryable state) / 404 propagate
  // as AxiosError for the hook to switch on.
  await api.post(`/receipt-files/${id}/retry`);
}

export async function getReceiptFiles(): Promise<ReceiptFile[]> {
  const { data } = await api.get<ReceiptFile[]>("/receipt-files");
  return data;
}

export async function deleteReceiptFile(id: string): Promise<void> {
  await api.delete(`/receipt-files/${id}`);
}

export async function bulkDeleteReceiptFiles(
  ids: string[]
): Promise<{ deleted: number }> {
  const { data } = await api.post<{ deleted: number }>(
    "/receipt-files/bulk-delete",
    { ids }
  );
  return data;
}

export async function bulkRetryReceiptFiles(
  ids: string[]
): Promise<{ retried: number }> {
  const { data } = await api.post<{ retried: number }>(
    "/receipt-files/bulk-retry",
    { ids }
  );
  return data;
}

export async function getReceipts(year?: number): Promise<Receipt[]> {
  const params = year ? { year } : undefined;
  const { data } = await api.get<Receipt[]>("/receipts", { params });
  return data;
}

export async function getReceiptById(id: string): Promise<Receipt> {
  const { data } = await api.get<Receipt>(`/receipts/${id}`);
  return data;
}

export async function getReceiptItems(
  receiptId: string
): Promise<ReceiptItem[]> {
  const { data } = await api.get<ReceiptItem[]>(
    `/receipts/${receiptId}/items`
  );
  return data;
}

export async function reclassifyReceipt(
  receiptId: string
): Promise<ReceiptItem[]> {
  const { data } = await api.post<ReceiptItem[]>(
    `/receipts/${receiptId}/reclassify`
  );
  return data;
}

export async function retryFailedClassifications(
  receiptId: string
): Promise<ReceiptItem[]> {
  const { data } = await api.post<ReceiptItem[]>(
    `/receipts/${receiptId}/retry-failed`
  );
  return data;
}

export async function batchConfirmClassifications(
  itemIds: string[]
): Promise<{ confirmed: number }> {
  const { data } = await api.post<{ confirmed: number }>(
    "/receipt-items/batch-confirm",
    { itemIds }
  );
  return data;
}

export async function getPendingSuggestions(): Promise<PendingSuggestion[]> {
  const { data } = await api.get<PendingSuggestion[]>(
    "/receipt-items/pending-suggestions"
  );
  return data;
}

export async function confirmClassification(
  itemId: string,
  category: string
): Promise<ItemClassification> {
  const { data } = await api.post<ItemClassification>(
    `/receipt-items/${itemId}/confirm`,
    { category }
  );
  return data;
}

export async function getCategoryTotals(
  year: number
): Promise<CategoryTotal[]> {
  const { data } = await api.get<CategoryTotal[]>("/reports/category-totals", {
    params: { year },
  });
  return data;
}

export async function getAnnualSummary(
  year: number
): Promise<AnnualSummary> {
  const { data } = await api.get<AnnualSummary>("/reports/annual-summary", {
    params: { year },
  });
  return data;
}

/**
 * Downloads the annual report as a Blob (CSV or PDF), carrying the JWT
 * Authorization header via the shared `api` instance. Using `api` (not a bare
 * fetch()) ensures a token that expired since the last full page load is
 * transparently refreshed-and-retried by the response interceptor, instead of
 * failing with a stale/missing Authorization header.
 */
export async function exportReport(
  format: "csv" | "pdf",
  year: number
): Promise<Blob> {
  const { data } = await api.get<Blob>(`/reports/export/${format}`, {
    params: { year },
    responseType: "blob",
  });
  return data;
}

// --- Settings ---

export interface UserSettings {
  autoConfirmThreshold: number | null;
}

export async function getUserSettings(): Promise<UserSettings> {
  const { data } = await api.get<UserSettings>("/settings");
  return data;
}

export async function updateUserSettings(
  settings: UserSettings
): Promise<UserSettings> {
  const { data } = await api.put<UserSettings>("/settings", settings);
  return data;
}

// --- Tokens ---

export async function getTokenBalance(): Promise<TokenBalance> {
  const { data } = await api.get<TokenBalance>("/tokens/balance");
  return data;
}

export async function getTokenTransactions(
  take = 20
): Promise<TokenTransaction[]> {
  const { data } = await api.get<TokenTransaction[]>("/tokens/transactions", {
    params: { take },
  });
  return data;
}

export async function purchaseTokens(amount: number): Promise<TokenBalance> {
  const { data } = await api.post<TokenBalance>("/tokens/purchase", { amount });
  return data;
}

// --- Classification rules ---

export interface SaveRulePayload {
  vendorPattern?: string | null;
  descriptionPattern?: string | null;
  sourceFilePattern?: string | null;
  category: string;
}

export async function saveClassificationRule(
  itemId: string,
  payload: SaveRulePayload
): Promise<ClassificationRule> {
  const { data } = await api.post<ClassificationRule>(
    `/receipt-items/${itemId}/save-rule`,
    payload
  );
  return data;
}

export async function acknowledgeSumMismatch(receiptId: string): Promise<void> {
  await api.post(`/receipts/${receiptId}/acknowledge-sum`);
}

// --- Payments API ---

export async function createCheckoutSession(
  req: CreateCheckoutSessionRequest
): Promise<CheckoutSession> {
  const { data } = await api.post<CheckoutSession>("/payments/checkout", req);
  return data;
}

export async function createPortalSession(): Promise<PortalSession> {
  const { data } = await api.post<PortalSession>("/payments/portal");
  return data;
}

export async function getInvoices(): Promise<Invoice[]> {
  const { data } = await api.get<Invoice[]>("/payments/invoices");
  return data;
}

// --- Data Export (DSGVO Art. 20 / LEG-07) ---

export type ExportStatus = "Generating" | "Ready" | "Expired";

export async function requestDataExport(): Promise<{ exportToken: string }> {
  const { data } = await api.post<{ exportToken: string }>("/export/request");
  return data;
}

export async function getExportStatus(
  exportToken: string
): Promise<{ status: ExportStatus }> {
  const { data } = await api.get<{ status: ExportStatus }>("/export/status", {
    params: { token: exportToken },
  });
  return data;
}

/**
 * Downloads the export bundle as a Blob, carrying the JWT Authorization header.
 * Using the api instance (not a bare <a href>) ensures the token is attached (T-06-40).
 */
export async function downloadExportBundle(exportToken: string): Promise<Blob> {
  const { data } = await api.get<Blob>("/export/download", {
    params: { token: exportToken },
    responseType: "blob",
  });
  return data;
}
