import { describe, expect, it } from "vitest";
import { categoryName, count, evidenceText, marketName, money, priceDifference, windowAverage } from "./format";

describe("money", () => {
  it("formats Thai baht with satang", () => expect(money(2590)).toBe("฿2,590.00"));
  it("shows a dash when there is no price", () => {
    expect(money(null)).toBe("—");
    expect(money(undefined)).toBe("—");
  });
  it("keeps zero as a real price", () => expect(money(0)).toBe("฿0.00"));
});

describe("priceDifference", () => {
  it("is below average for a positive discount", () => {
    expect(priceDifference(13.24)).toEqual({ text: "ต่ำกว่าค่าเฉลี่ย 13.2%", kind: "below" });
  });
  it("is above average for a negative discount, without the sign", () => {
    expect(priceDifference(-4.56)).toEqual({ text: "สูงกว่าค่าเฉลี่ย 4.6%", kind: "above" });
  });
  it("is neutral when equal or unknown", () => {
    expect(priceDifference(0).kind).toBe("neutral");
    expect(priceDifference(null)).toEqual({ text: "ข้อมูลราคาไม่พอ", kind: "neutral" });
  });
});

describe("evidenceText", () => {
  it("explains each way evidence can be missing", () => {
    expect(evidenceText("noHistory", 0)).toContain("ยังไม่มีข้อมูลราคา");
    expect(evidenceText("stale", 3)).toContain("เก่ากว่า 24 ชั่วโมง");
    expect(evidenceText("outOfStock", 3)).toContain("สินค้าหมด");
  });
  it("reports how many days of history exist when it is too short", () => {
    expect(evidenceText("insufficient", 4)).toContain("4 วัน");
  });
});

describe("names", () => {
  it("maps known marketplaces and categories", () => {
    expect(marketName("TikTok")).toBe("TikTok Shop");
    expect(categoryName("Home")).toBe("บ้านและครัว");
  });
  it("falls back to the raw value or a placeholder", () => {
    expect(marketName("Other")).toBe("Other");
    expect(marketName(null)).toBe("ไม่ระบุแพลตฟอร์ม");
    expect(categoryName("Toys")).toBe("Toys");
  });
  it("formats counts with Thai grouping", () => expect(count(12345)).toBe("12,345"));
});

describe("windowAverage", () => {
  const insight = { windows: [{ days: 7, averagePrice: 100 }, { days: 30, averagePrice: 90.5 }] };
  it("finds the requested window", () => expect(windowAverage(insight, 30)).toBe(90.5));
  it("returns null when the window or insight is missing", () => {
    expect(windowAverage(insight, 90)).toBeNull();
    expect(windowAverage(null, 30)).toBeNull();
  });
});
