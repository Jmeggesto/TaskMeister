// Relative path — Vite's dev server proxies /api → the backend (see vite.config.ts).
// In production, deploy frontend and backend behind a shared reverse proxy
// so /api routes to the backend naturally, or set this to an absolute URL via
// an environment variable: import.meta.env.VITE_API_BASE ?? "/api"
const BASE = "/api";

interface RequestOptions {
  token?: string;
  body?: unknown;
  method?: string;
  headers?: Record<string, string>;
}

/**
 * Core fetch wrapper. All API modules go through here.
 *
 * Returns null for 204 No Content; otherwise parses and returns JSON as T.
 * Throws an Error with a message extracted from the response body on non-2xx.
 */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { token, body, method = "GET", headers: extraHeaders = {} } = options;

  const headers: Record<string, string> = {
    ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...extraHeaders,
  };

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });

  if (!res.ok) {
    const json = await res.json().catch(() => ({})) as Record<string, string>;
    throw new Error(json["error"] ?? json["detail"] ?? json["title"] ?? "Request failed");
  }

  // Callers expecting no body should type T as void.
  // null as T is the standard escape hatch for 204 No Content.
  if (res.status === 204) return null as T;
  return res.json() as Promise<T>;
}
