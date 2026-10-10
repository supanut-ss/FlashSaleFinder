import { useEffect, useState } from "react";
import { api } from "../lib/api";
import { categoryName, dateTime, evidenceText, marketName, money, priceDifference } from "../lib/format";
import { useSession } from "../lib/session";
import { CloseIcon, ExternalIcon } from "./Icons";
import { Modal } from "./Modal";
import { PriceChart } from "./PriceChart";

function WatchForm({ product, onSimulated }) {
  const { ensureLogin, refreshPersonal, notify, handleError } = useSession();
  const [target, setTarget] = useState("");

  const watch = async () => {
    if (!(await ensureLogin())) return;
    const request = { productId: product.id };
    if (target.trim() !== "") {
      const value = Number(target);
      if (!Number.isFinite(value) || value < 0) { notify("กรุณาใส่ราคาเป้าหมายเป็นจำนวนเงินบาทตั้งแต่ 0 ขึ้นไป", "error"); return; }
      request.targetPrice = value;
    }
    try {
      await api("/api/watchlist", { method: "POST", body: JSON.stringify(request) });
      await refreshPersonal();
      notify(request.targetPrice == null ? "เพิ่มสินค้าในรายการติดตามแล้ว" : `ตั้งแจ้งเตือนเมื่อราคาต่ำกว่า ${money(request.targetPrice)} แล้ว`);
    } catch (error) { handleError(error); }
  };

  const simulate = async () => {
    if (!(await ensureLogin())) return;
    try {
      const result = await api("/api/demo/tick", { method: "POST", body: JSON.stringify({ productId: product.id }) });
      await refreshPersonal();
      onSimulated();
      notify(`จำลองราคาลด 20% เป็น ${money(result.price)} แล้ว`);
    } catch (error) {
      if (error.status === 409) notify("รอสักครู่ก่อนจำลองราคาของสินค้านี้อีกครั้ง");
      else notify(`จำลองราคาไม่สำเร็จ: ${error.message}`, "error");
    }
  };

  return (
    <div className="detail-actions">
      <label className="target-field">
        <span>แจ้งเตือนเมื่อราคาต่ำกว่า (เว้นว่างเพื่อติดตามอย่างเดียว)</span>
        <input type="number" min="0" step="0.01" inputMode="decimal" placeholder="เช่น 900" value={target} onChange={e => setTarget(e.target.value)} />
      </label>
      <button className="btn btn-primary" type="button" onClick={watch}>ติดตามสินค้า</button>
      {product.isSample && <button className="btn btn-ghost" type="button" onClick={simulate}>จำลองราคาลด 20%</button>}
      {product.affiliateUrl && (
        <a className="btn btn-accent" href={product.affiliateUrl} target="_blank" rel="noopener noreferrer sponsored">
          ไปยังร้านค้า <ExternalIcon />
        </a>
      )}
    </div>
  );
}

function Detail({ productId, version, onSimulated }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    const id = encodeURIComponent(productId);
    Promise.all([
      api(`/api/products/${id}`, { signal }),
      api(`/api/products/${id}/prices?pageSize=100`, { signal }),
      api(`/api/products/${id}/insights`, { signal }),
    ]).then(([product, prices, insight]) => { setData({ product, prices: prices.items, insight }); setError(null); })
      .catch(e => { if (e.name !== "AbortError") setError(e.message); });
    return () => controller.abort();
  }, [productId, version]);

  if (error) return <p className="empty-note">เปิดรายละเอียดไม่ได้: {error}</p>;
  if (!data) return (
    <div aria-busy="true">
      <h2 id="detail-title" className="sr-only">กำลังโหลดรายละเอียดสินค้า</h2>
      <div className="skeleton skeleton-line" /><div className="skeleton skeleton-block" /><div className="skeleton skeleton-block" />
    </div>
  );

  const { product, prices, insight } = data;
  const provisional = insight.evidence === "provisional";
  const difference = priceDifference(insight.realDiscountPercent);
  return (
    <>
      <p className="eyebrow">{marketName(product.marketplace)} · {product.isSample ? "ข้อมูลตัวอย่าง" : "ข้อมูลสินค้า"}</p>
      <h2 id="detail-title" className="detail-title">{product.title}</h2>
      <p className="detail-variant">{categoryName(product.category)} · {product.variantLabel}</p>

      <section className="price-evidence" aria-label="ราคาปัจจุบันและสรุปประวัติราคา">
        <div className="detail-current">
          <div>
            <strong>{money(insight.currentPrice)}</strong>
            {product.originalPrice != null && <del className="detail-original">ราคาป้าย {money(product.originalPrice)}</del>}
            <span className="detail-caption">{insight.capturedAt ? `บันทึกล่าสุด ${dateTime(insight.capturedAt)}` : "ยังไม่มีการบันทึกราคา"}</span>
          </div>
          <span className="sample-tag">{product.isSample ? "ราคาจำลอง" : insight.isFresh ? "ข้อมูลไม่เกิน 24 ชม." : "ข้อมูลเก่ากว่า 24 ชม."}</span>
        </div>
        <p className={`evidence-note ${provisional ? `note-${difference.kind}` : "note-neutral"}`}>
          {provisional
            ? `${difference.text} · เทียบกับค่าเฉลี่ยของ ${insight.priorDays} วันที่มีข้อมูลในช่วง 30 วัน ไม่ใช่ Deal Score`
            : evidenceText(insight.evidence, insight.priorDays)}
        </p>
        <div className="window-grid">
          {insight.windows.map(w => (
            <div className="window-item" key={w.days}>
              <span className="window-days">{w.days} วัน · มีข้อมูล {w.observedDays} วัน</span>
              <strong>เฉลี่ย {money(w.averagePrice)}</strong>
              <span className="window-low">ต่ำสุด {money(w.lowestPrice)}</span>
            </div>
          ))}
        </div>
      </section>

      <h3 className="section-label">กราฟประวัติราคา</h3>
      <PriceChart prices={prices} />

      <details className="history-details">
        <summary>ราคาที่บันทึก ({prices.length} รายการล่าสุด)</summary>
        {prices.length === 0 ? <p className="empty-note">ยังไม่มีข้อมูลประวัติราคา</p> : (
          <div className="history-table-wrap">
            <table className="history-table">
              <thead><tr><th scope="col">วันที่บันทึก</th><th scope="col">ราคา</th><th scope="col">สถานะ</th></tr></thead>
              <tbody>
                {prices.map((p, i) => (
                  <tr key={i}><td>{dateTime(p.capturedAt)}</td><td>{money(p.price)}</td><td>{p.inStock ? "มีสินค้า" : "หมด"}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </details>

      <WatchForm product={product} onSimulated={onSimulated} />
    </>
  );
}

export function ProductDialog({ productId, onClose, onCatalogChanged }) {
  const [version, setVersion] = useState(0);
  // Keep the last id mounted while the dialog closes so content does not flash empty.
  return (
    <Modal open={productId != null} onClose={onClose} labelledBy="detail-title" className="modal-wide">
      <div className="modal-top">
        <span className="detail-kicker">รายละเอียดราคา</span>
        <button className="icon-button" type="button" onClick={onClose} aria-label="ปิดรายละเอียด"><CloseIcon /></button>
      </div>
      <div className="modal-body">
        {productId != null && (
          <Detail productId={productId} version={version} onSimulated={() => { setVersion(v => v + 1); onCatalogChanged(); }} />
        )}
      </div>
    </Modal>
  );
}
