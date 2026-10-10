import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "./lib/api";
import { count } from "./lib/format";
import { useSession } from "./lib/session";
import { PAGE_SIZE, useProducts } from "./lib/useProducts";
import { Header } from "./components/Header";
import { Hero } from "./components/Hero";
import { InfoIcon } from "./components/Icons";
import { NoticeDialog, Toast } from "./components/Overlays";
import { PersonalArea } from "./components/PersonalArea";
import { ProductCard } from "./components/ProductCard";
import { ProductDialog } from "./components/ProductDialog";
import { SearchPanel } from "./components/SearchPanel";

const INITIAL_FILTERS = { query: "", marketplace: "", category: "", minPrice: "", maxPrice: "", sort: "newest" };

/** Delays rapid changes (typing a price) so each keystroke does not hit the API. */
function useDebounced(value, delay) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);
  return debounced;
}

function SkeletonGrid() {
  return Array.from({ length: PAGE_SIZE }, (_, i) => (
    <div className="product-card skeleton-card" key={i} aria-hidden="true">
      <div className="skeleton skeleton-art" />
      <div className="card-body"><div className="skeleton skeleton-line" /><div className="skeleton skeleton-line short" /><div className="skeleton skeleton-price" /></div>
    </div>
  ));
}

export default function App() {
  const { signedIn, ensureLogin } = useSession();
  const [filters, setFilters] = useState(INITIAL_FILTERS);
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState(null);
  const [status, setStatus] = useState("เชื่อมต่อข้อมูล");
  const personalRef = useRef(null);

  const debounced = useDebounced(filters, 350);
  const { cards, total, loading, error, reload } = useProducts(debounced, page);
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  useEffect(() => {
    api("/api/status")
      .then(s => setStatus(s.isDemo ? "ข้อมูลตัวอย่างเท่านั้น" : "ไม่มี Marketplace สด"))
      .catch(() => setStatus("ตรวจสถานะข้อมูลไม่ได้"));
  }, []);

  const changeFilters = useCallback(patch => { setFilters(f => ({ ...f, ...patch })); setPage(1); }, []);
  const goToPage = next => { setPage(next); window.scrollTo({ top: 0, behavior: "smooth" }); };

  const openPersonal = async () => {
    if (!(await ensureLogin())) return;
    // The section mounts only once signed in, so wait a frame before scrolling.
    requestAnimationFrame(() => personalRef.current?.scrollIntoView({ behavior: "smooth", block: "start" }));
  };

  return (
    <>
      <div className="sample-banner" role="note">
        <InfoIcon />
        <p><strong>โหมดข้อมูลตัวอย่าง</strong> ราคาทั้งหมดเป็นข้อมูลจำลอง ไม่ใช่ราคาปัจจุบันจาก Marketplace</p>
      </div>

      <Header onOpenPersonal={openPersonal} />

      <main className="page-shell">
        <Hero />
        <SearchPanel filters={filters} onChange={changeFilters} />

        <div className="results-heading">
          <div>
            <h2>สินค้าน่าดู</h2>
            <p className="muted">{error ? "ยังโหลดข้อมูลไม่ได้" : loading && !cards.length ? "กำลังโหลดข้อมูล…" : `${count(total)} รายการ · ${page} หน้า`}</p>
          </div>
          <span className="pill">{status}</span>
        </div>

        {error && (
          <div className="status-message" role="alert">
            <p>โหลดสินค้าไม่สำเร็จ: {error}</p>
            <button className="btn btn-ghost" type="button" onClick={reload}>ลองอีกครั้ง</button>
          </div>
        )}

        <section className={`product-grid${loading && cards.length ? " is-refreshing" : ""}`} aria-label="ผลการค้นหา" aria-busy={loading}>
          {loading && !cards.length && <SkeletonGrid />}
          {cards.map(card => <ProductCard key={card.product.id} {...card} onOpen={setOpenId} />)}
          {!loading && !error && cards.length === 0 && (
            <div className="empty-state">
              <p><strong>ยังไม่พบสินค้าที่ตรงกัน</strong>ลองเปลี่ยนคำค้นหาหรือตัวกรอง แล้วค้นหาอีกครั้ง</p>
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

        {signedIn && <PersonalArea sectionRef={personalRef} />}
      </main>

      <footer className="footer">
        <span>flash<b>hunt</b></span>
        <p>ข้อมูลนี้เป็นตัวอย่างเพื่อสาธิตระบบ ไม่มีการเชื่อมต่อหรือยืนยันราคา Marketplace จริง</p>
      </footer>

      <ProductDialog productId={openId} onClose={() => setOpenId(null)} onCatalogChanged={reload} />
      <NoticeDialog />
      <Toast />
    </>
  );
}
