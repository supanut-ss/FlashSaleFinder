const base = {
  viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: 1.8,
  strokeLinecap: "round", strokeLinejoin: "round", "aria-hidden": true, focusable: "false",
};

const icon = children => function Icon(props) {
  return <svg {...base} {...props}>{children}</svg>;
};

export const SearchIcon = icon(<><circle cx="10.8" cy="10.8" r="6.3" /><path d="m16 16 4.5 4.5" /></>);
export const FilterIcon = icon(<path d="M4 7h10M18 7h2M4 17h2M10 17h10M14 4v6M6 14v6" />);
export const CloseIcon = icon(<path d="M6 6l12 12M18 6 6 18" />);
export const BellIcon = icon(<path d="M6 16v-5a6 6 0 1 1 12 0v5l1.5 2h-15L6 16ZM10 21h4" />);
export const BookmarkIcon = icon(<path d="M6 4h12v17l-6-4-6 4V4Z" />);
export const UserIcon = icon(<><circle cx="12" cy="8.5" r="3.6" /><path d="M5 20c.8-3.6 3.6-5.4 7-5.4s6.200 1.800 7 5.400" /></>);
export const InfoIcon = icon(<><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 8h.01" /></>);
export const ExternalIcon = icon(<path d="M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5" />);
export const ArrowDownIcon = icon(<path d="M12 5v14M6 13l6 6 6-6" />);
export const ArrowUpIcon = icon(<path d="M12 19V5M6 11l6-6 6 6" />);
export const MinusIcon = icon(<path d="M6 12h12" />);
export const ChevronIcon = icon(<path d="m9 6 6 6-6 6" />);
