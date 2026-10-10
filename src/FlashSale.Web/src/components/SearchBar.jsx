import { useState } from "react";
import { SearchIcon } from "./Icons";

export function SearchBar({ query, onSearch, children }) {
  const [draft, setDraft] = useState(query);
  const submit = event => { event.preventDefault(); onSearch(draft.trim()); };
  return (
    <form className="searchbar" role="search" onSubmit={submit}>
      <label className="searchbox">
        <SearchIcon />
        <span className="sr-only">ค้นหาสินค้า</span>
        <input type="search" enterKeyHint="search" maxLength={200} placeholder="ค้นหาสินค้า เช่น หูฟังไร้สาย" autoComplete="off"
          value={draft} onChange={e => setDraft(e.target.value)} />
      </label>
      <button className="btn btn-primary searchbar-submit" type="submit">ค้นหา</button>
      {children}
    </form>
  );
}
