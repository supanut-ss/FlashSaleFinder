/** Magnifier + discount tag + sparkle. Colours come from the --logo-* tokens so the lens suits both themes. */
export function BrandMark({ size = 40 }) {
  const purple = { stroke: "var(--logo-purple)" };
  return (
    <svg className="brand-mark" width={size} height={size} viewBox="0 0 100 100" aria-hidden="true" focusable="false">
      <circle cx="46" cy="45" r="32" strokeWidth="8" fill="none" style={{ ...purple, fill: "var(--surface)" }} />
      <line x1="69" y1="68" x2="88" y2="87" strokeWidth="10" strokeLinecap="round" style={purple} />
      <g transform="translate(44 50) rotate(-35) scale(.72)">
        <path d="M0 -12 C-4 -27 12 -28 9 -16" fill="none" strokeWidth="3.2" strokeLinecap="round" style={purple} />
        <polygon points="-9,-20 9,-20 17,-10 17,20 -17,20 -17,-10" strokeWidth="4" strokeLinejoin="round" style={{ fill: "var(--logo-coral)", stroke: "var(--logo-coral)" }} />
        <circle cx="0" cy="-12" r="3.2" style={{ fill: "var(--surface)" }} />
        <g transform="translate(0 6) rotate(35)"><line x1="-4" y1="8" x2="4" y2="-8" strokeWidth="3" strokeLinecap="round" style={{ stroke: "var(--surface)" }} /><circle cx="-5.500" cy="-5.500" r="3.400" style={{ fill: "var(--surface)" }} /><circle cx="5.500" cy="5.500" r="3.400" style={{ fill: "var(--surface)" }} /></g>
      </g>
      <path transform="translate(-3 3)" d="M63 24.500 Q64.600 30.400 70.500 32 Q64.600 33.600 63 39.500 Q61.400 33.600 55.500 32 Q61.400 30.400 63 24.500Z" strokeWidth="2" strokeLinejoin="round" style={{ fill: "var(--logo-mint)", stroke: "var(--logo-mint)" }} />
    </svg>
  );
}

export function Brand() {
  return (
    <a className="brand" href="#/" aria-label="ProDiff หน้าแรก">
      <BrandMark />
      <span aria-hidden="true"><span className="brand-pro">Pro</span><span className="brand-diff">Diff</span></span>
    </a>
  );
}
