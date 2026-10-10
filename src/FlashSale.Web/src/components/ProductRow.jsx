import { categoryName, dateTime, marketName, money, priceDifference, windowAverage } from "../lib/format";
import { ArrowDownIcon, ArrowUpIcon, ChevronIcon, MinusIcon } from "./Icons";
import { PriceBar } from "./PriceBar";

const VERDICT_ICON = { below: ArrowDownIcon, above: ArrowUpIcon, neutral: MinusIcon };

function Verdict({ insight }) {
  if (insight?.evidence === "provisional") {
    const { text, kind } = priceDifference(insight.realDiscountPercent);
    const Icon = VERDICT_ICON[kind];
    return <span className={`verdict verdict-${kind}`}><Icon />{text}</span>;
  }
  return (
    <span className="verdict verdict-neutral">
      <MinusIcon />{insight?.evidence === "stale" ? "ราคาเก่าเกิน 24 ชม." : "ประวัติยังไม่พอ"}
    </span>
  );
}

/** One ledger line per product; the whole row opens the price evidence (stretched-button pattern). */
export function ProductRow({ product, insight, onOpen }) {
  return (
    <article className="row-item">
      <div className="row-info">
        <p className="row-market">{marketName(product.marketplace)}{product.isSample && <span className="sample-tag">ตัวอย่าง</span>}</p>
        <h3 className="row-name">
          <button className="row-hit" type="button" onClick={() => onOpen(product.id)}>{product.title}</button>
        </h3>
        <p className="row-sub">{categoryName(product.category)}, {product.variantLabel}</p>
      </div>
      <div className="row-price">
        <strong className="price">{money(product.currentPrice)}</strong>
        {product.originalPrice != null && <del className="price-list">{money(product.originalPrice)}</del>}
      </div>
      <PriceBar current={product.currentPrice} average={windowAverage(insight, 30)} list={product.originalPrice} />
      <div className="row-verdict">
        <Verdict insight={insight} />
        <span className="row-time">{product.capturedAt ? dateTime(product.capturedAt) : "ยังไม่มีประวัติราคา"}</span>
      </div>
      <ChevronIcon className="row-chevron" />
    </article>
  );
}
