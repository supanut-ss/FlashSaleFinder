import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from "react";

/** Tracks a CSS media query, e.g. useMedia("(max-width: 1023px)"). */
export function useMedia(query) {
  const subscribe = useCallback(callback => {
    const list = window.matchMedia(query);
    list.addEventListener("change", callback);
    return () => list.removeEventListener("change", callback);
  }, [query]);
  return useSyncExternalStore(subscribe, () => window.matchMedia(query).matches, () => false);
}

export const ROUTES = ["explore", "watch", "alerts", "account"];

const routeFromHash = () => {
  const name = window.location.hash.replace(/^#\/?/, "");
  return ROUTES.includes(name) ? name : "explore";
};

/** Hash routing keeps every screen deep-linkable and the back button predictable without a router library. */
export function useRoute() {
  const [route, setRoute] = useState(routeFromHash);
  useEffect(() => {
    const onChange = () => setRoute(routeFromHash());
    window.addEventListener("hashchange", onChange);
    return () => window.removeEventListener("hashchange", onChange);
  }, []);
  const navigate = useCallback(next => {
    window.location.hash = next === "explore" ? "/" : `/${next}`;
    window.scrollTo({ top: 0 });
  }, []);
  return [route, navigate];
}

/** Measures an element's width so SVG charts can render at true pixel size (readable text on phones). */
export function useWidth() {
  const ref = useRef(null);
  const [width, setWidth] = useState(0);
  useEffect(() => {
    const element = ref.current;
    if (!element) return;
    // Measure up front: observers can stay silent until the first resize (e.g. in a hidden tab).
    setWidth(Math.round(element.getBoundingClientRect().width));
    const observer = new ResizeObserver(([entry]) => setWidth(Math.round(entry.contentRect.width)));
    observer.observe(element);
    return () => observer.disconnect();
  }, []);
  return [ref, width];
}

/** Delays rapid changes (typing a price) so each keystroke does not hit the API. */
export function useDebounced(value, delay) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);
  return debounced;
}
