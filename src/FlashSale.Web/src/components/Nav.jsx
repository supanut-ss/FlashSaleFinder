import { useSession } from "../lib/session";
import { Brand } from "./Brand";
import { BellIcon, BookmarkIcon, SearchIcon, UserIcon } from "./Icons";

function useDestinations() {
  const { signedIn, watchlist, alerts } = useSession();
  return [
    { route: "explore", label: "ค้นหา", Icon: SearchIcon, badge: 0 },
    { route: "watch", label: "รายการติดตาม", Icon: BookmarkIcon, badge: signedIn ? watchlist.length : 0 },
    { route: "alerts", label: "แจ้งเตือน", Icon: BellIcon, badge: signedIn ? alerts.length : 0, urgent: true },
    { route: "account", label: "บัญชี", Icon: UserIcon, badge: 0 },
  ];
}

const hrefFor = route => (route === "explore" ? "#/" : `#/${route}`);
const badgeLabel = (label, badge) => (badge ? `${label} ${badge} รายการ` : label);

/** Wide screens: top bar with the same destinations as the compact tab bar. */
export function TopNav({ route }) {
  const { signedIn, openLoginNotice, signOut } = useSession();
  const items = useDestinations().filter(item => item.route !== "account");
  return (
    <header className="topnav">
      <div className="topnav-inner">
        <Brand />
        <nav aria-label="เมนูหลัก">
          {items.map(({ route: target, label, badge }) => (
            <a key={target} href={hrefFor(target)} className="topnav-link" aria-current={route === target ? "page" : undefined} aria-label={badgeLabel(label, badge)}>
              {label}{badge > 0 && <span className="count" aria-hidden="true">{badge}</span>}
            </a>
          ))}
        </nav>
        <button className="btn btn-ink" type="button" onClick={signedIn ? signOut : openLoginNotice}>
          {signedIn ? "ออกจากเดโม" : "เข้าโหมดเดโม"}
        </button>
      </div>
    </header>
  );
}

/** Compact screens: slim app bar that always shows the sample-data status. */
export function AppBar() {
  return (
    <div className="appbar">
      <Brand />
      <span className="sample-tag sample-tag-lg">ข้อมูลตัวอย่าง</span>
    </div>
  );
}

export function TabBar({ route }) {
  const items = useDestinations();
  return (
    <nav className="tabbar" aria-label="เมนูหลัก">
      {items.map(({ route: target, label, Icon, badge }) => (
        <a key={target} href={hrefFor(target)} className="tab" aria-current={route === target ? "page" : undefined} aria-label={badgeLabel(label, badge)}>
          <span className="tab-icon"><Icon />{badge > 0 && <span className="count" aria-hidden="true">{badge}</span>}</span>
          <span className="tab-label" aria-hidden="true">{target === "watch" ? "ติดตาม" : label}</span>
        </a>
      ))}
    </nav>
  );
}
