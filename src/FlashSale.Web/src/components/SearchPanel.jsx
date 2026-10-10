import { useState } from "react";
import { SearchIcon } from "./Icons";

export function SearchPanel({ filters, onChange }) {
  const [draft, setDraft] = useState(filters.query);
  const submit = event => { event.preventDefault(); onChange({ query: draft.trim() }); };

  return (
    <section className="search-panel" aria-label="ค้นหาและกรองสินค้า">
      <form className="search-form" role="search" onSubmit={submit}>
        <label className="search-box">
          <SearchIcon className="search-icon" />
          <span className="sr-only">ค้นหาสินค้า</span>
          <input type="search" maxLength={200} placeholder="ค้นหาสินค้า เช่น หูฟังไร้สาย" autoComplete="off"
            value={draft} onChange={e => setDraft(e.target.value)} />
          <kbd>Enter</kbd>
        </label>
        <button className="btn btn-primary" type="submit">ค้นหา</button>
      </form>
      <div className="filter-row">
        <label className="filter-field"><span>แพลตฟอร์ม</span>
          <select value={filters.marketplace} onChange={e => onChange({ marketplace: e.target.value })}>
            <option value="">ทุกแพลตฟอร์ม</option><option value="Lazada">Lazada</option><option value="Shopee">Shopee</option><option value="TikTok">TikTok Shop</option>
          </select>
        </label>
        <label className="filter-field"><span>หมวดหมู่</span>
          <select value={filters.category} onChange={e => onChange({ category: e.target.value })}>
            <option value="">ทุกหมวดหมู่</option><option value="Electronics">อิเล็กทรอนิกส์</option><option value="Home">บ้านและครัว</option><option value="Beauty">ความงาม</option>
          </select>
        </label>
        <label className="filter-field"><span>ราคาต่ำสุด</span>
          <span className="price-input"><span aria-hidden="true">฿</span>
            <input type="number" min={0} step={100} inputMode="numeric" placeholder="ไม่จำกัด" value={filters.minPrice} onChange={e => onChange({ minPrice: e.target.value })} />
          </span>
        </label>
        <label className="filter-field"><span>ราคาสูงสุด</span>
          <span className="price-input"><span aria-hidden="true">฿</span>
            <input type="number" min={0} step={100} inputMode="numeric" placeholder="ไม่จำกัด" value={filters.maxPrice} onChange={e => onChange({ maxPrice: e.target.value })} />
          </span>
        </label>
        <label className="filter-field sort-field"><span>เรียงตาม</span>
          <select value={filters.sort} onChange={e => onChange({ sort: e.target.value })}>
            <option value="newest">ข้อมูลล่าสุด</option><option value="priceAsc">ราคาต่ำไปสูง</option><option value="priceDesc">ราคาสูงไปต่ำ</option>
          </select>
        </label>
      </div>
    </section>
  );
}
