import { money } from "../lib/format";

/**
 * Current price against the 30-day average and the list price, on one shared scale.
 * Purely a visual aid: the verdict beside it carries the same meaning in text.
 */
export function PriceBar({ current, average, list }) {
  if (current == null || average == null) return <div className="price-bar price-bar-empty" aria-hidden="true" />;
  const scale = Math.max(current, average, list ?? 0) * 1.04;
  const pct = value => `${Math.min(100, (value / scale) * 100)}%`;
  return (
    <div className="price-bar" role="img"
      aria-label={`ราคาปัจจุบัน ${money(current)} ค่าเฉลี่ย 30 วัน ${money(average)}${list != null ? ` ราคาป้าย ${money(list)}` : ""}`}>
      <span className="price-bar-fill" style={{ width: pct(current) }} />
      <span className="price-bar-tick" style={{ left: pct(average) }} title="ค่าเฉลี่ย 30 วัน" />
      {list != null && <span className="price-bar-tick price-bar-list" style={{ left: pct(list) }} title="ราคาป้าย" />}
    </div>
  );
}
