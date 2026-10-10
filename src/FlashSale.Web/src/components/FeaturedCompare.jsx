import { useEffect, useState } from "react";
import { api, isAbort } from "../lib/api";
import { marketName, money, priceDifference } from "../lib/format";
import { PriceChart } from "./PriceChart";

/** The product's own evidence as the opening view: list price vs. what it really sells for, over time. */
export function FeaturedCompare({ product, insight, onOpen }) {
  const [prices, setPrices] = useState(null);

  useEffect(() => {
    const controller = new AbortController();
    api(`/api/products/${encodeURIComponent(product.id)}/prices?pageSize=100`, { signal: controller.signal })
      .then(result => setPrices(result.items))
      .catch(e => { if (!isAbort(e)) setPrices([]); });
    return () => controller.abort();
  }, [product.id]);

  const { text, kind } = priceDifference(insight.realDiscountPercent);
  return (
    <section className="compare" aria-labelledby="compare-title">
      <p className="compare-market">{marketName(product.marketplace)}{product.isSample && <span className="sample-tag">ตัวอย่าง</span>}</p>
      <h2 id="compare-title" className="compare-title">{product.title}</h2>
      <div className="compare-prices">
        {product.originalPrice != null && <span className="price-list">ราคาป้าย <del>{money(product.originalPrice)}</del></span>}
        <mark className="price-now">{money(product.currentPrice)}</mark>
      </div>
      <p className={`compare-verdict note-${kind}`}>{text} เทียบกับ 30 วันที่ผ่านมา</p>
      {prices && <PriceChart prices={prices} draw label={`ประวัติราคา ${product.title}`} />}
      <button className="btn btn-ghost compare-open" type="button" onClick={() => onOpen(product.id)}>ดูหลักฐานทั้งหมด</button>
    </section>
  );
}
