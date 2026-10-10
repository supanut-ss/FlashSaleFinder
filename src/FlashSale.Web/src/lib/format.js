const moneyFormat = new Intl.NumberFormat("th-TH", { style: "currency", currency: "THB", maximumFractionDigits: 2 });
const dateFormat = new Intl.DateTimeFormat("th-TH", { dateStyle: "medium", timeStyle: "short", timeZone: "Asia/Bangkok" });
const shortDateFormat = new Intl.DateTimeFormat("th-TH", { day: "numeric", month: "short", timeZone: "Asia/Bangkok" });
const countFormat = new Intl.NumberFormat("th-TH");

export const money = value => (value == null ? "—" : moneyFormat.format(value));
export const dateTime = value => (value ? dateFormat.format(new Date(value)) : "ไม่มีข้อมูลเวลา");
export const shortDate = value => shortDateFormat.format(new Date(value));
export const count = value => countFormat.format(value);

const MARKET_NAMES = { Lazada: "Lazada", Shopee: "Shopee", TikTok: "TikTok Shop" };
export const marketName = market => MARKET_NAMES[market] || market || "ไม่ระบุแพลตฟอร์ม";

const CATEGORY_NAMES = { Electronics: "อิเล็กทรอนิกส์", Home: "บ้านและครัว", Beauty: "ความงาม" };
export const categoryName = category => CATEGORY_NAMES[category] ?? category;

/** kind: "below" | "above" | "neutral" */
export function priceDifference(percent) {
  if (percent == null) return { text: "ข้อมูลราคาไม่พอ", kind: "neutral" };
  if (percent > 0) return { text: `ต่ำกว่าค่าเฉลี่ย ${percent.toFixed(1)}%`, kind: "below" };
  if (percent < 0) return { text: `สูงกว่าค่าเฉลี่ย ${Math.abs(percent).toFixed(1)}%`, kind: "above" };
  return { text: "เท่ากับค่าเฉลี่ย 30 วัน", kind: "neutral" };
}

export function evidenceText(evidence, days) {
  if (evidence === "noHistory") return "ยังไม่มีข้อมูลราคาที่บันทึกไว้ จึงยังเปรียบเทียบราคาไม่ได้";
  if (evidence === "stale") return "ราคาล่าสุดเก่ากว่า 24 ชั่วโมง ส่วนต่างอาจไม่สะท้อนราคาปัจจุบัน";
  if (evidence === "outOfStock") return "สินค้าหมดในข้อมูลล่าสุด จึงยังไม่แสดงส่วนต่างราคา";
  return `มีข้อมูลย้อนหลัง ${days} วัน ซึ่งยังไม่เพียงพอสำหรับสรุปส่วนต่างราคา`;
}

/** Average price over the given window (e.g. 30 days), or null when the insight lacks it. */
export const windowAverage = (insight, days) => insight?.windows?.find(w => w.days === days)?.averagePrice ?? null;
