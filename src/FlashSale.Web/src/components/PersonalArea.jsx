import { dateTime, money } from "../lib/format";
import { useSession } from "../lib/session";

export function PersonalArea({ sectionRef }) {
  const { signedIn, watchlist, alerts, removeWatch } = useSession();
  if (!signedIn) return null;

  return (
    <section ref={sectionRef} className="personal-area" aria-label="ข้อมูลส่วนตัว">
      <div className="personal-section">
        <div className="section-heading">
          <div><p className="eyebrow eyebrow-dark">เก็บไว้ดูอีกครั้ง</p><h2>รายการติดตาม</h2></div>
          <span className="pill">{watchlist.length} รายการ</span>
        </div>
        <div className="personal-content">
          {watchlist.length === 0 && <p className="empty-note">ยังไม่มีสินค้าที่ติดตาม เลือกดูประวัติราคาแล้วเพิ่มได้เลย</p>}
          {watchlist.map(item => (
            <div className="row" key={item.id}>
              <div className="row-main">
                <div className="row-title">{item.product.title}</div>
                <div className="row-detail">
                  ราคาล่าสุด {money(item.product.currentPrice)} · {item.targetPrice == null ? "ติดตามอย่างเดียว" : `เป้าหมาย ${money(item.targetPrice)}`}
                </div>
              </div>
              <button className="remove-button" type="button" onClick={() => removeWatch(item.id)} aria-label={`เลิกติดตาม ${item.product.title}`}>เลิกติดตาม</button>
            </div>
          ))}
        </div>
      </div>

      <div className="personal-section">
        <div className="section-heading">
          <div><p className="eyebrow eyebrow-dark">ราคาถึงเป้า</p><h2>การแจ้งเตือน</h2></div>
          <span className="pill">{alerts.length} รายการ</span>
        </div>
        <div className="personal-content">
          {alerts.length === 0 && <p className="empty-note">ยังไม่มีการแจ้งเตือน เมื่อราคาผ่านเป้าที่ตั้งไว้จะแสดงที่นี่</p>}
          {alerts.map((item, i) => (
            <article className="row row-alert" key={item.id ?? i}>
              <div className="row-main">
                <div className="row-title">ถึงเป้าแล้ว · {item.title}</div>
                <div className="row-detail">
                  {money(item.price)} · เป้าหมาย {money(item.targetPrice)} · {dateTime(item.createdAt)}{item.isSample ? " · ตัวอย่าง" : ""}
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
