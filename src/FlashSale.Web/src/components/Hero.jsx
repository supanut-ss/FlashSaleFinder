import { TrendIcon } from "./Icons";

export function Hero() {
  return (
    <section className="hero" aria-labelledby="hero-title">
      <div className="hero-copy">
        <p className="eyebrow"><span className="live-dot" aria-hidden="true" /> หลักฐานราคาก่อนตัดสินใจ</p>
        <h1 id="hero-title">เจอราคาดี<br /><span>พร้อมรู้ที่มา</span></h1>
        <p className="hero-subtitle">ค้นหาสินค้าจากข้อมูลราคา แล้วเทียบกับประวัติที่บันทึกไว้ ไม่ต้องเดาว่าลดจริงหรือไม่</p>
      </div>
      <div className="hero-aside" aria-label="ข้อมูลจากระบบ">
        <span className="hero-aside-icon"><TrendIcon /></span>
        <p>ส่วนต่างราคาอ้างอิงจาก<br /><strong>วันที่มีข้อมูลจริง</strong></p>
      </div>
    </section>
  );
}
