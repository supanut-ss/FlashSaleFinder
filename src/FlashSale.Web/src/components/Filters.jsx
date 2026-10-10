import { useState } from "react";
import { Modal } from "./Modal";
import { CloseIcon } from "./Icons";

export const INITIAL_FILTERS = { query: "", marketplace: "", category: "", minPrice: "", maxPrice: "", sort: "newest" };

/** Number of narrowing filters in effect (search text and sort order do not count). */
export const activeFilterCount = f => [f.marketplace, f.category, f.minPrice, f.maxPrice].filter(Boolean).length;

export function FilterFields({ values, onChange }) {
  return (
    <>
      <label className="field"><span>แพลตฟอร์ม</span>
        <select value={values.marketplace} onChange={e => onChange({ marketplace: e.target.value })}>
          <option value="">ทุกแพลตฟอร์ม</option><option value="Lazada">Lazada</option><option value="Shopee">Shopee</option><option value="TikTok">TikTok Shop</option>
        </select>
      </label>
      <label className="field"><span>หมวดหมู่</span>
        <select value={values.category} onChange={e => onChange({ category: e.target.value })}>
          <option value="">ทุกหมวดหมู่</option><option value="Electronics">อิเล็กทรอนิกส์</option><option value="Home">บ้านและครัว</option><option value="Beauty">ความงาม</option>
        </select>
      </label>
      <label className="field"><span>ราคาต่ำสุด</span>
        <span className="field-input"><span aria-hidden="true">฿</span>
          <input type="number" min={0} step={100} inputMode="numeric" placeholder="ไม่จำกัด" value={values.minPrice} onChange={e => onChange({ minPrice: e.target.value })} />
        </span>
      </label>
      <label className="field"><span>ราคาสูงสุด</span>
        <span className="field-input"><span aria-hidden="true">฿</span>
          <input type="number" min={0} step={100} inputMode="numeric" placeholder="ไม่จำกัด" value={values.maxPrice} onChange={e => onChange({ maxPrice: e.target.value })} />
        </span>
      </label>
      <label className="field"><span>เรียงตาม</span>
        <select value={values.sort} onChange={e => onChange({ sort: e.target.value })}>
          <option value="newest">ข้อมูลล่าสุด</option><option value="priceAsc">ราคาต่ำไปสูง</option><option value="priceDesc">ราคาสูงไปต่ำ</option>
        </select>
      </label>
    </>
  );
}

function SheetBody({ filters, onApply, onClose }) {
  const [draft, setDraft] = useState(filters);
  const patch = change => setDraft(d => ({ ...d, ...change }));
  return (
    <>
      <div className="modal-top">
        <h2 id="filter-title" className="modal-title">ตัวกรอง</h2>
        <button className="icon-button" type="button" onClick={onClose} aria-label="ปิดตัวกรอง"><CloseIcon /></button>
      </div>
      <div className="modal-body sheet-fields"><FilterFields values={draft} onChange={patch} /></div>
      <div className="sheet-actions">
        <button className="btn btn-ghost" type="button" onClick={() => setDraft({ ...INITIAL_FILTERS, query: draft.query })}>ล้างตัวกรอง</button>
        <button className="btn btn-primary" type="button" onClick={() => onApply(draft)}>แสดงผลลัพธ์</button>
      </div>
    </>
  );
}

/** Compact-screen filters: a bottom sheet applied as one change instead of refetching per field. */
export function FilterSheet({ open, filters, onApply, onClose }) {
  return (
    <Modal open={open} onClose={onClose} labelledBy="filter-title" className="sheet">
      <SheetBody filters={filters} onApply={draft => { onApply(draft); onClose(); }} onClose={onClose} />
    </Modal>
  );
}
