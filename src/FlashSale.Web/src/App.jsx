import { useCallback, useEffect, useState } from "react";
import { api } from "./lib/api";
import { count } from "./lib/format";
import { useDebounced, useMedia, useRoute } from "./lib/hooks";
import { PAGE_SIZE, useProducts } from "./lib/useProducts";
import { FeaturedCompare } from "./components/FeaturedCompare";
import { FilterFields, FilterSheet, INITIAL_FILTERS, activeFilterCount } from "./components/Filters";
import { FilterIcon, InfoIcon } from "./components/Icons";
import { AppBar, TabBar, TopNav } from "./components/Nav";
import { NoticeDialog, Toast } from "./components/Overlays";
import { ProductDialog } from "./components/ProductDialog";
import { ProductRow } from "./components/ProductRow";
import { SearchBar } from "./components/SearchBar";
import { AccountView, AlertsView, WatchView } from "./components/Views";

function SkeletonRows() {
  return Array.from({ length: PAGE_SIZE }, (_, i) => (
    <div className="row-item row-skeleton" key={i} aria-hidden="true">
      <div className="skeleton skeleton-line" /><div className="skeleton skeleton-line short" />
    </div>
  ));
}

/** Picks the product whose evidence is clearest to open the app with. */
function pickFeatured(cards) {
  const candidates = cards.filter(c => c.insight?.evidence === "provisional" && c.insight.realDiscountPercent != null);
  return candidates.sort((a, b) => b.insight.realDiscountPercent - a.insight.realDiscountPercent)[0] ?? null;
}

export default function App() {
  const compact = useMedia("(max-width: 1023px)");
  const [route, navigate] = useRoute();
  const [filters, setFilters] = useState(INITIAL_FILTERS);
  const [page, setPage] = useState(1);
  const [filterOpen, setFilterOpen] = useState(false);
  const [openId, setOpenId] = useState(null);
  const [status, setStatus] = useState("เชื่อมต่อข้อมูล");
  const [featured, setFeatured] = useState(null);

  const debounced = useDebounced(filters, 350);
  const { cards, total, loading, error, reload } = useProducts(debounced, page);
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const filtersActive = activeFilterCount(filters);

  useEffect(() => {
    api("/api/status")
      .then(s => setStatus(s.isDemo ? "ข้อมูลตัวอย่างเท่านั้น" : "ไม่มี Marketplace สด"))
      .catch(() => setStatus("ตรวจสถานะข้อมูลไม่ได้"));
  }, []);

  // The featured comparison comes from the first unfiltered load and then stays put while the list changes.
  useEffect(() => {
    if (featured || !cards.length || debounced.query || activeFilterCount(debounced)) return;
    setFeatured(pickFeatured(cards));
  }, [cards, debounced, featured]);

  // The detail sheet owns a history entry so the phone's back gesture closes it instead of leaving the app.
  useEffect(() => {
    const onPop = () => setOpenId(null);
    window.addEventListener("popstate", onPop);
    return () => window.removeEventListener("popstate", onPop);
  }, []);
  const openProduct = useCallback(id => { window.history.pushState({ sheet: true }, ""); setOpenId(id); }, []);
  const closeProduct = useCallback(() => {
    if (window.history.state?.sheet) window.history.back();
    else setOpenId(null);
  }, []);

  const changeFilters = useCallback(patch => { setFilters(f => ({ ...f, ...patch })); setPage(1); }, []);
  const goToPage = next => { setPage(next); window.scrollTo({ top: 0, behavior: "smooth" }); };

  const searchBar = (
    <SearchBar query={filters.query} onSearch={query => changeFilters({ query })}>
      {compact && (
        <button className="icon-button filter-button" type="button" onClick={() => setFilterOpen(true)}
          aria-label={filtersActive ? `ตัวกรอง ใช้อยู่ ${filtersActive} รายการ` : "ตัวกรอง"}>
          <FilterIcon />{filtersActive > 0 && <span className="count" aria-hidden="true">{filtersActive}</span>}
        </button>
      )}
    </SearchBar>
  );

  const explore = (
    <>
      {compact ? <h1 className="sr-only">ProDiff ค้นหาสินค้า</h1> : (
        <section className="hero">
          <div className="hero-copy">
            <h1>เช็กว่าลดจริงไหม ก่อนกดซื้อ</h1>
            <p>เทียบราคาปัจจุบันกับประวัติราคาที่บันทึกไว้ แล้วดูหลักฐานของส่วนต่างราคา</p>
            {searchBar}
          </div>
          {featured && <FeaturedCompare product={featured.product} insight={featured.insight} onOpen={openProduct} />}
        </section>
      )}
      {compact && featured && <FeaturedCompare product={featured.product} insight={featured.insight} onOpen={openProduct} />}

      {!compact && <div className="toolbar"><FilterFields values={filters} onChange={changeFilters} /></div>}

      <div className="results-heading">
        <div>
          <h2>สินค้า</h2>
          <p className="muted">{error ? "ยังโหลดข้อมูลไม่ได้" : loading && !cards.length ? "กำลังโหลดข้อมูล…" : `${count(total)} รายการ, หน้า ${page} จาก ${pages}`}</p>
        </div>
        <span className="pill">{status}</span>
      </div>

      {error && (
        <div className="status-message" role="alert">
          <p>โหลดสินค้าไม่สำเร็จ: {error}</p>
          <button className="btn btn-ghost" type="button" onClick={reload}>ลองอีกครั้ง</button>
        </div>
      )}

      <section className={`rows${loading && cards.length ? " is-refreshing" : ""}`} aria-label="ผลการค้นหา" aria-busy={loading}>
        {loading && !cards.length && <SkeletonRows />}
        {cards.map(card => <ProductRow key={card.product.id} {...card} onOpen={openProduct} />)}
        {!loading && !error && cards.length === 0 && (
          <div className="empty-state">
            <p><strong>ยังไม่พบสินค้าที่ตรงกัน</strong>ลองเปลี่ยนคำค้นหาหรือตัวกรอง แล้วค้นหาอีกครั้ง</p>
            {(filters.query || filtersActive > 0) && (
              <button className="btn btn-ghost" type="button" onClick={() => changeFilters(INITIAL_FILTERS)}>ล้างการค้นหาและตัวกรอง</button>
            )}
          </div>
        )}
      </section>

      {pages > 1 && (
        <nav className="pagination" aria-label="เปลี่ยนหน้าผลการค้นหา">
          <button className="btn btn-ghost" type="button" disabled={page <= 1} onClick={() => goToPage(page - 1)}>หน้าก่อน</button>
          <span>หน้า {page} จาก {pages}</span>
          <button className="btn btn-ghost" type="button" disabled={page >= pages} onClick={() => goToPage(page + 1)}>หน้าถัดไป</button>
        </nav>
      )}
    </>
  );

  const screens = {
    explore,
    watch: <WatchView onOpen={openProduct} />,
    alerts: <AlertsView />,
    account: <AccountView />,
  };

  return (
    <div className={`app${compact ? " app-compact" : ""}`}>
      <a className="skip-link" href="#main" onClick={e => { e.preventDefault(); document.getElementById("main")?.focus(); }}>ข้ามไปเนื้อหาหลัก</a>
      {compact ? (
        <div className="compact-top">
          <AppBar />
          {route === "explore" && searchBar}
        </div>
      ) : (
        <>
          <div className="sample-banner" role="note">
            <InfoIcon />
            <p><strong>โหมดข้อมูลตัวอย่าง</strong> ราคาทั้งหมดเป็นข้อมูลจำลอง ไม่ใช่ราคาปัจจุบันจาก Marketplace</p>
          </div>
          <TopNav route={route} />
        </>
      )}

      <main className="page" id="main" tabIndex={-1}>{screens[route]}</main>

      {!compact && (
        <footer className="footer">
          <span>ProDiff</span>
          <p>ข้อมูลนี้เป็นตัวอย่างเพื่อสาธิตระบบ ไม่มีการเชื่อมต่อหรือยืนยันราคา Marketplace จริง</p>
        </footer>
      )}
      {compact && <TabBar route={route} />}

      <ProductDialog productId={openId} onClose={closeProduct} onCatalogChanged={reload} />
      <FilterSheet open={filterOpen} filters={filters} onApply={draft => changeFilters(draft)} onClose={() => setFilterOpen(false)} />
      <NoticeDialog />
      <Toast />
    </div>
  );
}
