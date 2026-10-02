// Browser code calls same-origin /api (Nginx in prod, Next rewrite in dev).
// Server components run inside the container network and call the API directly via API_INTERNAL_URL.
const baseUrl = typeof window === "undefined" ? (process.env.API_INTERNAL_URL ?? "http://localhost:5080") : "";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
    cache: "no-store",
  });

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new ApiError(res.status, body?.detail ?? body?.title ?? res.statusText);
  }
  return res.json() as Promise<T>;
}
