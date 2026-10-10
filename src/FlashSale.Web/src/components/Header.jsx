import { useSession } from "../lib/session";
import { BellIcon, BoltIcon, HeartIcon } from "./Icons";

export function Header({ onOpenPersonal }) {
  const { signedIn, watchlist, alerts, openLoginNotice, signOut } = useSession();
  return (
    <header className="topbar">
      <a className="brand" href="/" aria-label="Flash Hunt หน้าแรก">
        <span className="brand-mark"><BoltIcon /></span>
        <span>flash<b>hunt</b></span>
      </a>
      <nav className="top-actions" aria-label="เมนูหลัก">
        <button className="nav-button" type="button" onClick={onOpenPersonal}>
          <HeartIcon /> <span className="nav-label">รายการติดตาม</span>
          <span className="count">{signedIn ? watchlist.length : 0}</span>
        </button>
        <button className="nav-button" type="button" onClick={onOpenPersonal}>
          <BellIcon /> <span className="nav-label">แจ้งเตือน</span>
          {signedIn && alerts.length > 0 && <span className="count count-alert">{alerts.length}</span>}
        </button>
        <button className="login-button" type="button" onClick={signedIn ? signOut : openLoginNotice}>
          {signedIn ? "ออกจากเดโม" : "เข้าโหมดเดโม"}
        </button>
      </nav>
    </header>
  );
}
