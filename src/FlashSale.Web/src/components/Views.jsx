import { useEffect, useState } from "react";
import { api } from "../lib/api";
import { dateTime, money } from "../lib/format";
import { useSession } from "../lib/session";
import { ChevronIcon } from "./Icons";

function ScreenTitle({ title, count }) {
  return (
    <div className="screen-title">
      <h1>{title}</h1>
      {count != null && <span className="pill">{count} รายการ</span>}
    </div>
  );
}

function SignedOut({ title, hint }) {
  const { openLoginNotice } = useSession();
  return (
    <>
      <ScreenTitle title={title} />
      <div className="empty-state">
        <p><strong>เข้าโหมดเดโมเพื่อใช้งานส่วนนี้</strong>{hint}</p>
        <button className="btn btn-primary" type="button" onClick={openLoginNotice}>เข้าโหมดเดโม</button>
      </div>
    </>
  );
}

export function WatchView({ onOpen }) {
  const { signedIn, watchlist, removeWatch } = useSession();
  if (!signedIn) return <SignedOut title="รายการติดตาม" hint="บันทึกสินค้าที่สนใจไว้ดูราคาอีกครั้ง" />;
  return (
    <>
      <ScreenTitle title="รายการติดตาม" count={watchlist.length} />
      {watchlist.length === 0 && (
        <div className="empty-state"><p><strong>ยังไม่มีสินค้าที่ติดตาม</strong>เปิดดูหลักฐานราคาของสินค้า แล้วกดติดตามได้เลย</p></div>
      )}
      <ul className="list">
        {watchlist.map(item => (
          <li className="list-item" key={item.id}>
            <button className="list-main" type="button" onClick={() => onOpen(item.product.id)}>
              <span className="list-title">{item.product.title}</span>
              <span className="list-detail">
                ราคาล่าสุด <span className="price-inline">{money(item.product.currentPrice)}</span>
                {item.targetPrice == null ? " ติดตามอย่างเดียว" : <>, เป้าหมาย <span className="price-inline">{money(item.targetPrice)}</span></>}
              </span>
            </button>
            <button className="btn btn-quiet" type="button" onClick={() => removeWatch(item.id)} aria-label={`เลิกติดตาม ${item.product.title}`}>เลิกติดตาม</button>
          </li>
        ))}
      </ul>
    </>
  );
}

export function AlertsView() {
  const { signedIn, alerts } = useSession();
  if (!signedIn) return <SignedOut title="แจ้งเตือน" hint="เมื่อราคาลดถึงเป้าที่ตั้งไว้ จะแสดงที่นี่" />;
  return (
    <>
      <ScreenTitle title="แจ้งเตือน" count={alerts.length} />
      {alerts.length === 0 && (
        <div className="empty-state"><p><strong>ยังไม่มีการแจ้งเตือน</strong>ตั้งราคาเป้าหมายตอนติดตามสินค้า เมื่อราคาลดถึงเป้าจะแสดงที่นี่</p></div>
      )}
      <ul className="list">
        {alerts.map((item, i) => (
          <li className="list-item list-alert" key={item.id ?? i}>
            <div className="list-main">
              <span className="list-title">ถึงเป้าแล้ว {item.title}</span>
              <span className="list-detail">
                <span className="price-inline">{money(item.price)}</span> เป้าหมาย <span className="price-inline">{money(item.targetPrice)}</span>
                {" "}เมื่อ {dateTime(item.createdAt)}{item.isSample ? " (ตัวอย่าง)" : ""}
              </span>
            </div>
          </li>
        ))}
      </ul>
    </>
  );
}

const STORAGE_HINT = {
  InMemory: "อยู่ในหน่วยความจำ หายเมื่อปิดเซิร์ฟเวอร์",
  MariaDB: "เก็บถาวรในฐานข้อมูล",
  Memory: "แคชในหน่วยความจำ",
  Redis: "แคชด้วย Redis",
};

/** Shows which database and cache the API is actually using, so a mis-set environment is easy to spot. */
function SystemStatus() {
  const [status, setStatus] = useState(null);
  useEffect(() => {
    const controller = new AbortController();
    api("/api/status", { signal: controller.signal }).then(setStatus).catch(() => setStatus(false));
    return () => controller.abort();
  }, []);

  return (
    <section className="panel" aria-labelledby="system-title">
      <h2 id="system-title">สถานะระบบ</h2>
      {status === null && <p className="panel-text">กำลังตรวจสอบ…</p>}
      {status === false && <p className="panel-text">ตรวจสถานะระบบไม่ได้</p>}
      {status && (
        <dl className="status-list">
          <div><dt>ฐานข้อมูล</dt><dd><strong className="price-inline">{status.databaseProvider}</strong> {STORAGE_HINT[status.databaseProvider]}</dd></div>
          <div><dt>แคช</dt><dd><strong className="price-inline">{status.cacheProvider}</strong> {STORAGE_HINT[status.cacheProvider]}</dd></div>
          <div><dt>ข้อมูล</dt><dd>{status.isDemo ? "ข้อมูลตัวอย่างเท่านั้น" : "ไม่มี Marketplace สด"}</dd></div>
        </dl>
      )}
    </section>
  );
}

export function AccountView() {
  const { signedIn, openLoginNotice, signOut } = useSession();
  return (
    <>
      <ScreenTitle title="บัญชี" />
      <section className="panel">
        <h2>โหมดเดโม</h2>
        <p className="panel-text">
          {signedIn ? "คุณกำลังใช้บัญชีเดโมที่ใช้ร่วมกันบนเครื่องนี้ รายการติดตามและแจ้งเตือนจะถูกเก็บไว้ในบัญชีนี้" : "เข้าโหมดเดโมเพื่อติดตามสินค้าและตั้งแจ้งเตือนราคา บัญชีนี้เป็นบัญชีตัวอย่าง ไม่ใช่บัญชีผู้ใช้จริง"}
        </p>
        <button className={`btn ${signedIn ? "btn-ghost" : "btn-primary"}`} type="button" onClick={signedIn ? signOut : openLoginNotice}>
          {signedIn ? "ออกจากเดโม" : "เข้าโหมดเดโม"}
        </button>
      </section>
      <section className="panel">
        <h2>เกี่ยวกับข้อมูล</h2>
        <p className="panel-text">ราคาทั้งหมดเป็นข้อมูลจำลองเพื่อสาธิตระบบ ไม่มีการเชื่อมต่อหรือยืนยันราคากับ Marketplace จริง ส่วนต่างราคาคำนวณจากวันที่มีข้อมูลบันทึกไว้เท่านั้น และยังไม่ใช่ Deal Score</p>
      </section>
      <SystemStatus />
      <a className="panel panel-link" href="#/"><span>กลับไปค้นหาสินค้า</span><ChevronIcon /></a>
    </>
  );
}
