import { useEffect, useRef } from "react";

/** Native <dialog> wrapper: focus trap, Esc and backdrop handling come from the browser. */
export function Modal({ open, onClose, labelledBy, className = "", children }) {
  const ref = useRef(null);

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  return (
    <dialog
      ref={ref}
      className={`modal ${className}`}
      aria-labelledby={labelledBy}
      onClose={onClose}
      onClick={event => { if (event.target === event.currentTarget) onClose(); }}
    >
      {open && children}
    </dialog>
  );
}
