/** Official ProDiff logo, split into mark and wordmark so each can be sized for the header. */
export function Brand() {
  return (
    <a className="brand" href="#/" aria-label="ProDiff หน้าแรก">
      <img className="brand-mark" src="/logo-mark.webp" width="41" height="40" alt="" />
      <img className="brand-wordmark" src="/logo-wordmark.webp" width="95" height="24" alt="" />
    </a>
  );
}
