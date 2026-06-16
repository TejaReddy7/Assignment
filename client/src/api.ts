// Minimal typed API client. Token is stored in localStorage and attached to requests.
// Requests go to /api/... and Vite proxies them to the backend (see vite.config.ts).

// API base URL: uses environment variable in production, or /api proxy in development
const API_BASE_URL = import.meta.env.VITE_API_URL || '/api';

const TOKEN_KEY = 'ipl_access_token';
const REFRESH_KEY = 'ipl_refresh_token';

type SessionExpiredHandler = () => void;
const sessionExpiredHandlers = new Set<SessionExpiredHandler>();

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  getRefresh: () => localStorage.getItem(REFRESH_KEY),
  set: (access: string, refresh: string) => {
    localStorage.setItem(TOKEN_KEY, access);
    localStorage.setItem(REFRESH_KEY, refresh);
  },
  clear: () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
  },
  onSessionExpired(handler: SessionExpiredHandler) {
    sessionExpiredHandlers.add(handler);
    return () => {
      sessionExpiredHandlers.delete(handler);
    };
  },
};

export class ApiError extends Error {
  constructor(public status: number, message: string, public errorCode?: string) {
    super(message);
  }
}

// Single-flight refresh: concurrent 401s share one /auth/refresh call.
let refreshInFlight: Promise<boolean> | null = null;

async function tryRefresh(): Promise<boolean> {
  const refresh = tokenStore.getRefresh();
  if (!refresh) return false;

  refreshInFlight ??= (async () => {
    try {
      const res = await fetch('/api/v1/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: refresh }),
      });
      if (!res.ok) return false;
      const body = (await res.json()) as { accessToken: string; refreshToken: string };
      tokenStore.set(body.accessToken, body.refreshToken);
      return true;
    } catch {
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

async function request<T>(path: string, options: RequestInit = {}, retried = false): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('Content-Type', 'application/json');
  const token = tokenStore.get();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const res = await fetch(`${API_BASE_URL}/v1${path}`, { ...options, headers });

  // Transparently recover from an expired access token: refresh once, retry once.
  // Skip for /auth/* so /auth/refresh and /auth/login don't recurse.
  if (res.status === 401 && !retried && !path.startsWith('/auth/')) {
    if (await tryRefresh()) return request<T>(path, options, true);
    tokenStore.clear();
    sessionExpiredHandlers.forEach((h) => h());
    throw new ApiError(401, 'Session expired', 'auth.session_expired');
  }

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const body = text ? JSON.parse(text) : undefined;

  if (!res.ok) {
    const message = body?.detail || body?.title || `Request failed (${res.status})`;
    throw new ApiError(res.status, message, body?.errorCode);
  }
  return body as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown, extraHeaders?: Record<string, string>) =>
    request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined, headers: extraHeaders }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body ? JSON.stringify(body) : undefined }),
  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PATCH', body: body ? JSON.stringify(body) : undefined }),
  del: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};
