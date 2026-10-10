import { useCallback, useEffect, useState } from "react";
import { api, isAbort } from "./api";

export const PAGE_SIZE = 6;

/** Loads one catalog page plus per-product price insight; stale requests are aborted. */
export function useProducts(filters, page) {
  const [cards, setCards] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE), sort: filters.sort });
    if (filters.query) params.set("q", filters.query);
    if (filters.marketplace) params.set("marketplace", filters.marketplace);
    if (filters.category) params.set("category", filters.category);
    if (filters.minPrice) params.set("minPrice", filters.minPrice);
    if (filters.maxPrice) params.set("maxPrice", filters.maxPrice);

    setLoading(true);
    setError(null);
    (async () => {
      try {
        const result = await api(`/api/products?${params}`, { signal });
        const withInsights = await Promise.all(result.items.map(async product => {
          // The catalog stays useful when price evidence is temporarily unavailable.
          const insight = await api(`/api/products/${encodeURIComponent(product.id)}/insights`, { signal }).catch(e => {
            if (isAbort(e)) throw e;
            return null;
          });
          return { product, insight };
        }));
        setCards(withInsights);
        setTotal(result.total);
      } catch (e) {
        if (isAbort(e)) return;
        setCards([]);
        setTotal(0);
        setError(e instanceof Error ? e.message : "ไม่ทราบสาเหตุ");
      } finally {
        if (!signal.aborted) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [filters, page, reloadKey]);

  const reload = useCallback(() => setReloadKey(key => key + 1), []);
  return { cards, total, loading, error, reload };
}
