const TOKEN_KEY = "flash-hunt-demo-token";

export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

// Session storage can throw in private windows or with blocked site data.
const tokenStore = {
  get() {
    try { return sessionStorage.getItem(TOKEN_KEY); } catch { return null; }
  },
  set(token) {
    try { sessionStorage.setItem(TOKEN_KEY, token); } catch { /* token stays in memory only */ }
  },
  clear() {
    try { sessionStorage.removeItem(TOKEN_KEY); } catch { /* nothing to clear */ }
  },
};

let currentToken = tokenStore.get();

export function setToken(token) {
  currentToken = token;
  if (token) tokenStore.set(token); else tokenStore.clear();
}

export const getToken = () => currentToken;

export async function api(path, options = {}) {
  const headers = new Headers(options.headers);
  if (currentToken) headers.set("Authorization", `Bearer ${currentToken}`);
  if (options.body) headers.set("Content-Type", "application/json");
  const response = await fetch(path, { ...options, headers });
  if (!response.ok) {
    let detail = `เชื่อมต่อไม่สำเร็จ (${response.status})`;
    try {
      const problem = await response.json();
      detail = problem.detail || problem.title || detail;
    } catch { /* keep the status fallback */ }
    throw new ApiError(detail, response.status);
  }
  if (response.status === 204) return null;
  return response.json();
}

export const isAbort = error => error instanceof DOMException && error.name === "AbortError";
