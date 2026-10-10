import { useId, useMemo, useState } from "react";
import { useWidth } from "../lib/hooks";
import { dateTime, money, shortDate } from "../lib/format";

const PAD = { left: 64, right: 14, top: 14, bottom: 30 };

/**
 * Price history line. Renders at true pixel width so axis text stays readable on phones.
 * Pointer, touch and arrow keys all move the same readout; `draw` plays the line-in once.
 */
export function PriceChart({ prices, draw = false, label }) {
  const [wrapRef, width] = useWidth();
  const gradientId = useId();
  const [hover, setHover] = useState(null);

  const sorted = useMemo(
    () => [...prices].sort((a, b) => new Date(a.capturedAt) - new Date(b.capturedAt)),
    [prices],
  );
  const height = width && width < 480 ? 200 : 250;

  const geometry = useMemo(() => {
    if (!sorted.length || !width) return null;
    const values = sorted.map(p => Number(p.price));
    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = max - min || 1;
    const innerW = width - PAD.left - PAD.right;
    const innerH = height - PAD.top - PAD.bottom;
    const coords = values.map((value, i) => ({
      x: sorted.length === 1 ? PAD.left + innerW / 2 : PAD.left + (i * innerW) / (sorted.length - 1),
      y: PAD.top + innerH - ((value - min) / span) * innerH,
    }));
    return { min, max, coords, baseline: height - PAD.bottom };
  }, [sorted, width, height]);

  if (!sorted.length) return <p className="empty-note">ไม่มีข้อมูลราคาในช่วงนี้</p>;

  const move = delta => setHover(i => Math.min(sorted.length - 1, Math.max(0, (i ?? sorted.length - 1) + delta)));
  const onKeyDown = event => {
    const actions = { ArrowLeft: () => move(-1), ArrowRight: () => move(1), Home: () => setHover(0), End: () => setHover(sorted.length - 1), Escape: () => setHover(null) };
    const action = actions[event.key];
    if (action) { event.preventDefault(); action(); }
  };
  const onPointer = event => {
    if (!geometry) return;
    const box = event.currentTarget.getBoundingClientRect();
    const x = event.clientX - box.left;
    let nearest = 0;
    geometry.coords.forEach((c, i) => { if (Math.abs(c.x - x) < Math.abs(geometry.coords[nearest].x - x)) nearest = i; });
    setHover(nearest);
  };

  const active = geometry && hover != null ? { ...geometry.coords[hover], point: sorted[hover] } : null;
  const summary = `${label ?? "กราฟราคา"} ตั้งแต่ ${dateTime(sorted[0].capturedAt)} ถึง ${dateTime(sorted.at(-1).capturedAt)} ต่ำสุด ${money(Math.min(...sorted.map(p => p.price)))} สูงสุด ${money(Math.max(...sorted.map(p => p.price)))}`;

  return (
    <figure className="chart" ref={wrapRef}>
      {geometry && (
        <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} role="group" aria-roledescription="กราฟเส้น" aria-label={summary}
          tabIndex={0} onKeyDown={onKeyDown} onPointerDown={onPointer} onPointerMove={onPointer} onPointerLeave={() => setHover(null)} onBlur={() => setHover(null)}>
          <defs>
            <linearGradient id={gradientId} x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" stopColor="var(--ink)" stopOpacity=".14" />
              <stop offset="100%" stopColor="var(--ink)" stopOpacity="0" />
            </linearGradient>
          </defs>
          {[0, 0.5, 1].map(t => {
            const y = PAD.top + (geometry.baseline - PAD.top) * t;
            return <line key={t} x1={PAD.left} x2={width - PAD.right} y1={y} y2={y} className="chart-grid" />;
          })}
          <text x={PAD.left - 10} y={PAD.top + 4} className="chart-axis" textAnchor="end">{money(geometry.max)}</text>
          <text x={PAD.left - 10} y={geometry.baseline + 4} className="chart-axis" textAnchor="end">{money(geometry.min)}</text>
          <text x={PAD.left} y={height - 8} className="chart-axis">{shortDate(sorted[0].capturedAt)}</text>
          <text x={width - PAD.right} y={height - 8} className="chart-axis" textAnchor="end">{shortDate(sorted.at(-1).capturedAt)}</text>
          <polygon points={`${geometry.coords[0].x},${geometry.baseline} ${geometry.coords.map(c => `${c.x},${c.y}`).join(" ")} ${geometry.coords.at(-1).x},${geometry.baseline}`} fill={`url(#${gradientId})`} />
          <polyline points={geometry.coords.map(c => `${c.x},${c.y}`).join(" ")} pathLength="1" className={`chart-line${draw ? " chart-line-draw" : ""}`} />
          <circle cx={geometry.coords.at(-1).x} cy={geometry.coords.at(-1).y} r="6" className="chart-now" />
          {active && (
            <g>
              <line x1={active.x} x2={active.x} y1={PAD.top} y2={geometry.baseline} className="chart-cursor" />
              <circle cx={active.x} cy={active.y} r="5" className="chart-dot" />
            </g>
          )}
        </svg>
      )}
      <p className="sr-only" aria-live="polite">{active ? `${money(active.point.price)} เมื่อ ${dateTime(active.point.capturedAt)}` : ""}</p>
      {active && (
        <div className="chart-tip" style={{ left: Math.min(Math.max(active.x, 70), width - 70), top: active.y }}>
          <strong>{money(active.point.price)}</strong>
          <span>{dateTime(active.point.capturedAt)}</span>
        </div>
      )}
    </figure>
  );
}
