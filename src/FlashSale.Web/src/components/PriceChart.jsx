import { useId, useMemo, useState } from "react";
import { dateTime, money, shortDate } from "../lib/format";

const W = 640;
const H = 220;
const PAD = { left: 56, right: 16, top: 16, bottom: 28 };

export function PriceChart({ prices }) {
  const gradientId = useId();
  const [hover, setHover] = useState(null);

  const points = useMemo(() => {
    const sorted = [...prices].sort((a, b) => new Date(a.capturedAt) - new Date(b.capturedAt));
    if (!sorted.length) return { sorted, min: 0, max: 0, coords: [] };
    const values = sorted.map(p => Number(p.price));
    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = max - min || 1;
    const innerW = W - PAD.left - PAD.right;
    const innerH = H - PAD.top - PAD.bottom;
    const coords = values.map((value, i) => ({
      x: sorted.length === 1 ? PAD.left + innerW / 2 : PAD.left + (i * innerW) / (sorted.length - 1),
      y: PAD.top + innerH - ((value - min) / span) * innerH,
    }));
    return { sorted, min, max, coords };
  }, [prices]);

  const { sorted, min, max, coords } = points;
  if (!sorted.length) return <p className="empty-note">ไม่มีข้อมูลราคาในช่วงนี้</p>;

  const line = coords.map(c => `${c.x},${c.y}`).join(" ");
  const baseline = H - PAD.bottom;
  const area = `${coords[0].x},${baseline} ${line} ${coords.at(-1).x},${baseline}`;
  const active = hover == null ? null : { ...coords[hover], point: sorted[hover] };

  const onMove = event => {
    const box = event.currentTarget.getBoundingClientRect();
    const x = ((event.clientX - box.left) / box.width) * W;
    let nearest = 0;
    coords.forEach((c, i) => { if (Math.abs(c.x - x) < Math.abs(coords[nearest].x - x)) nearest = i; });
    setHover(nearest);
  };

  return (
    <figure className="chart">
      <svg viewBox={`0 0 ${W} ${H}`} role="img" onPointerMove={onMove} onPointerLeave={() => setHover(null)}
        aria-label={`กราฟแสดงราคาตั้งแต่ ${dateTime(sorted[0].capturedAt)} ถึง ${dateTime(sorted.at(-1).capturedAt)} ต่ำสุด ${money(min)} สูงสุด ${money(max)}`}>
        <defs>
          <linearGradient id={gradientId} x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" stopColor="var(--chart-line)" stopOpacity=".28" />
            <stop offset="100%" stopColor="var(--chart-line)" stopOpacity="0" />
          </linearGradient>
        </defs>
        {[0, 0.5, 1].map(t => {
          const y = PAD.top + (H - PAD.top - PAD.bottom) * t;
          return <line key={t} x1={PAD.left} x2={W - PAD.right} y1={y} y2={y} className="chart-grid" />;
        })}
        <text x={PAD.left - 8} y={PAD.top + 4} className="chart-axis" textAnchor="end">{money(max)}</text>
        <text x={PAD.left - 8} y={baseline + 4} className="chart-axis" textAnchor="end">{money(min)}</text>
        <text x={PAD.left} y={H - 8} className="chart-axis">{shortDate(sorted[0].capturedAt)}</text>
        <text x={W - PAD.right} y={H - 8} className="chart-axis" textAnchor="end">{shortDate(sorted.at(-1).capturedAt)}</text>
        <polygon points={area} fill={`url(#${gradientId})`} />
        <polyline points={line} className="chart-line" />
        {[0, coords.length - 1].map(i => <circle key={i} cx={coords[i].x} cy={coords[i].y} r="4.5" className="chart-dot" />)}
        {active && (
          <g>
            <line x1={active.x} x2={active.x} y1={PAD.top} y2={baseline} className="chart-cursor" />
            <circle cx={active.x} cy={active.y} r="5.5" className="chart-dot chart-dot-active" />
          </g>
        )}
      </svg>
      {active && (
        <div className="chart-tip" style={{ left: `${(active.x / W) * 100}%`, top: `${(active.y / H) * 100}%` }}>
          <strong>{money(active.point.price)}</strong>
          <span>{dateTime(active.point.capturedAt)}</span>
        </div>
      )}
    </figure>
  );
}
