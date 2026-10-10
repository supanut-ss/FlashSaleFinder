import { categoryName, dateTime, marketName, money, priceDifference } from "../lib/format";
import { ArrowIcon, BagIcon, ChipIcon, HomeIcon, SparkleIcon } from "./Icons";

const CATEGORY_ART = {
  Electronics: { Icon: ChipIcon, tone: "tone-blue" },
  Home: { Icon: HomeIcon, tone: "tone-amber" },
  Beauty: { Icon: SparkleIcon, tone: "tone-rose" },
};

function Badge({ insight }) {
  if (insight?.evidence === "provisional") {
    const { text, kind } = priceDifference(insight.realDiscountPercent);
    return <span className={`badge badge-${kind}`}>{text}</span>;
  }
  return <span className="badge badge-neutral">{insight?.evidence === "stale" ? "ราคาเก่าเกิน 24 ชม." : "ประวัติยังไม่พอ"}</span>;
}

export function ProductCard({ product, insight, onOpen }) {
  const { Icon, tone } = CATEGORY_ART[product.category] ?? { Icon: BagIcon, tone: "tone-blue" };
  return (
    <article className="product-card">
      <div className={`product-art ${tone}`} aria-hidden="true">
        <span className="art-glyph"><Icon /></span>
        <span className="art-orbit" />
      </div>
      <div className="card-body">
        <div className="card-meta">
          <span className="market-label">{marketName(product.marketplace)}</span>
          {product.isSample && <span className="sample-tag">ตัวอย่าง</span>}
        </div>
        <h3 className="card-title">{product.title}</h3>
        <p className="variant-label">{categoryName(product.category)} · {product.variantLabel}</p>
        <div className="card-price-row">
          <div>
            <strong className="current-price">{money(product.currentPrice)}</strong>
            {product.originalPrice != null && <del className="original-price">ราคาป้าย {money(product.originalPrice)}</del>}
          </div>
          <Badge insight={insight} />
        </div>
        <div className="card-bottom">
          <span className="captured-label">{product.capturedAt ? `บันทึก ${dateTime(product.capturedAt)}` : "ยังไม่มีประวัติราคา"}</span>
          <button className="link-button" type="button" onClick={() => onOpen(product.id)}>
            ดูประวัติราคา <ArrowIcon />
          </button>
        </div>
      </div>
    </article>
  );
}
