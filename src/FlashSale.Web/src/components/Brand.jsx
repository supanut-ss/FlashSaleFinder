export function BrandMark({ size = 32 }) {
  return (
    <svg className="brand-mark" width={size} height={size} viewBox="0 0 64 64" aria-hidden="true" focusable="false">
      <rect width="64" height="64" rx="15" fill="currentColor" />
      <rect x="13" y="19" width="38" height="9" rx="4.500" className="mark-list" />
      <rect x="13" y="36" width="25" height="9" rx="4.500" className="mark-real" />
    </svg>
  );
}

export function Brand() {
  return (
    <a className="brand" href="#/" aria-label="ProDiff หน้าแรก">
      <BrandMark />
      <span>ProDiff</span>
    </a>
  );
}
