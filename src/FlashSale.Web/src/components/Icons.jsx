const base = {
  viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: 1.8,
  strokeLinecap: "round", strokeLinejoin: "round", "aria-hidden": true,
};

const icon = (children, overrides) => function Icon(props) {
  return <svg {...base} {...overrides} {...props}>{children}</svg>;
};

export const BoltIcon = icon(<path d="M13.4 2 5 13.4h5.6L9.6 22 19 9.8h-5.7L13.4 2Z" />, { fill: "currentColor", stroke: "none" });
export const SearchIcon = icon(<><circle cx="10.8" cy="10.8" r="6.3" /><path d="m16 16 4.5 4.5" /></>);
export const TrendIcon = icon(<path d="M4 17 9 12l3 3 7-8M15 7h4v4" />);
export const ArrowIcon = icon(<path d="M5 12h14M13 6l6 6-6 6" />);
export const CloseIcon = icon(<path d="M6 6l12 12M18 6 6 18" />);
export const BellIcon = icon(<path d="M6 16v-5a6 6 0 1 1 12 0v5l1.5 2h-15L6 16ZM10 21h4" />);
export const HeartIcon = icon(<path d="M12 20s-7.5-4.4-7.5-10A4.3 4.3 0 0 1 12 7.4 4.3 4.3 0 0 1 19.5 10c0 5.6-7.5 10-7.5 10Z" />);
export const InfoIcon = icon(<><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 8h.01" /></>);
export const ExternalIcon = icon(<path d="M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5" />);
export const ChipIcon = icon(<><rect x="6" y="6" width="12" height="12" rx="2" /><path d="M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4" /></>);
export const HomeIcon = icon(<path d="M4 11 12 4l8 7M6 10v10h12V10M10 20v-5h4v5" />);
export const SparkleIcon = icon(<path d="M12 3c.6 4.2 2.8 6.4 7 7-4.2.6-6.4 2.8-7 7-.6-4.2-2.8-6.4-7-7 4.2-.6 6.4-2.8 7-7ZM19 16v4M17 18h4" />);
export const BagIcon = icon(<path d="M5 8h14l-1 12H6L5 8ZM9 8V7a3 3 0 0 1 6 0v1" />);
