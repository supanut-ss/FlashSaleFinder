import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { api, ApiError, getToken, setToken } from "./api";

const Ctx = createContext(null);

export function useSession() {
  const value = useContext(Ctx);
  if (!value) throw new Error("useSession must be used inside SessionProvider");
  return value;
}

export function SessionProvider({ children }) {
  const [signedIn, setSignedIn] = useState(() => getToken() != null);
  const [watchlist, setWatchlist] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [toast, setToast] = useState(null);
  const [noticeOpen, setNoticeOpen] = useState(false);
  const pendingLogin = useRef(null);
  const refreshSeq = useRef(0);
  const toastTimer = useRef(undefined);

  const dismissToast = useCallback(() => setToast(null), []);
  const notify = useCallback((text, tone = "info") => {
    window.clearTimeout(toastTimer.current);
    setToast({ id: Date.now(), text, tone });
    // Errors stay until dismissed so they cannot be missed.
    if (tone === "info") toastTimer.current = window.setTimeout(() => setToast(null), 6000);
  }, []);

  const resolveLogin = ok => {
    pendingLogin.current?.(ok);
    pendingLogin.current = null;
  };

  const clearSession = useCallback(() => {
    setToken(null);
    setSignedIn(false);
    setWatchlist([]);
    setAlerts([]);
  }, []);

  const handleError = useCallback(error => {
    if (error instanceof ApiError && error.status === 401) {
      clearSession();
      notify("เซสชันเดโมหมดอายุแล้ว เข้าสู่ระบบใหม่เพื่อใช้รายการติดตาม", "error");
    } else {
      notify(error instanceof Error ? error.message : "เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ", "error");
    }
  }, [clearSession, notify]);

  const refreshPersonal = useCallback(async () => {
    if (!getToken()) return;
    // Overlapping refreshes can finish out of order; only the newest request may update the lists.
    const seq = ++refreshSeq.current;
    try {
      const [watch, alertList] = await Promise.all([api("/api/watchlist"), api("/api/alerts")]);
      if (seq !== refreshSeq.current) return;
      setWatchlist(watch.items);
      setAlerts(alertList.items);
    } catch (error) { handleError(error); }
  }, [handleError]);

  // Validate a restored token once; an expired one is dropped silently.
  useEffect(() => {
    if (!getToken()) return;
    api("/api/me").then(refreshPersonal, clearSession);
  }, [refreshPersonal, clearSession]);

  const confirmLogin = useCallback(async () => {
    try {
      const session = await api("/api/demo/session", { method: "POST", body: "{}" });
      setToken(session.accessToken);
      setSignedIn(true);
      // Detach the waiting action first: closing the notice fires cancelLogin, which would otherwise cancel it.
      const waiting = pendingLogin.current;
      pendingLogin.current = null;
      setNoticeOpen(false);
      await refreshPersonal();
      waiting?.(true);
    } catch (error) {
      setNoticeOpen(false);
      notify(`เข้าสู่โหมดเดโมไม่สำเร็จ: ${error.message}`, "error");
      resolveLogin(false);
    }
  }, [notify, refreshPersonal]);

  const cancelLogin = useCallback(() => {
    setNoticeOpen(false);
    resolveLogin(false);
  }, []);

  const ensureLogin = useCallback(() => {
    if (getToken()) return Promise.resolve(true);
    setNoticeOpen(true);
    return new Promise(resolve => { pendingLogin.current = resolve; });
  }, []);

  const removeWatch = useCallback(async id => {
    try {
      await api(`/api/watchlist/${encodeURIComponent(id)}`, { method: "DELETE" });
      await refreshPersonal();
    } catch (error) { handleError(error); }
  }, [handleError, refreshPersonal]);

  const value = useMemo(() => ({
    signedIn, watchlist, alerts, toast, notify, dismissToast, ensureLogin, noticeOpen, confirmLogin, cancelLogin,
    openLoginNotice: () => setNoticeOpen(true), signOut: clearSession, refreshPersonal, handleError, removeWatch,
  }), [signedIn, watchlist, alerts, toast, notify, dismissToast, ensureLogin, noticeOpen, confirmLogin, cancelLogin, clearSession, refreshPersonal, handleError, removeWatch]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}
