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

export async function apiClient<T>(path: string, init?: ApiClientOptions): Promise<T> {
  const headers = new Headers(init?.headers ?? {});

  if (!headers.has("Content-Type") && init?.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (init?.accessToken) {
    headers.set("Authorization", `Bearer ${init.accessToken}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: "include",
    headers,
  });

  if (!response.ok) {
    let payload: ApiErrorPayload | null = null;

    try {
      payload = (await response.json()) as ApiErrorPayload;
    } catch {
      payload = null;
    }

    throw new ApiError(
      response.status,
      payload?.detail ?? payload?.title ?? "The request could not be completed.",
      payload?.errors,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
