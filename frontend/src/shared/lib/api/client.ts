const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7054/api";

type ApiErrorPayload = {
  detail?: string;
  title?: string;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  status: number;
  fieldErrors?: Record<string, string[]>;

  constructor(status: number, message: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

type ApiClientOptions = RequestInit & {
  accessToken?: string | null;
};

async function buildApiError(response: Response): Promise<ApiError> {
  let payload: ApiErrorPayload | null = null;

  try {
    payload = (await response.json()) as ApiErrorPayload;
  } catch {
    payload = null;
  }

  return new ApiError(
    response.status,
    payload?.detail ?? payload?.title ?? "The request could not be completed.",
    payload?.errors,
  );
}

function buildHeaders(init?: ApiClientOptions) {
  const headers = new Headers(init?.headers ?? {});

  if (!headers.has("Content-Type") && init?.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (init?.accessToken) {
    headers.set("Authorization", `Bearer ${init.accessToken}`);
  }

  return headers;
}

function resolveFileName(response: Response, fallback: string) {
  const contentDisposition = response.headers.get("Content-Disposition");
  if (!contentDisposition) {
    return fallback;
  }

  const match = /filename\*?=(?:UTF-8''|\")?([^\";]+)/i.exec(contentDisposition);
  return match?.[1] ? decodeURIComponent(match[1].replace(/\"/g, "").trim()) : fallback;
}

export async function apiClient<T>(path: string, init?: ApiClientOptions): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: "include",
    headers: buildHeaders(init),
  });

  if (!response.ok) {
    throw await buildApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function downloadFile(path: string, fallbackFileName: string, init?: ApiClientOptions): Promise<string> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: "include",
    headers: buildHeaders(init),
  });

  if (!response.ok) {
    throw await buildApiError(response);
  }

  const blob = await response.blob();
  const fileName = resolveFileName(response, fallbackFileName);
  const objectUrl = window.URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = objectUrl;
  anchor.download = fileName;
  anchor.style.display = "none";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.URL.revokeObjectURL(objectUrl);
  return fileName;
}
