const API = "";
const PAGE_SIZE = 6;
const TOKEN_KEY = "flash-hunt-demo-token";
const state = { page: 1, total: 0, query: "", marketplace: "", category: "", minPrice: "", maxPrice: "", sort: "newest", token: sessionStorage.getItem(TOKEN_KEY), detailProduct: null, productDialog: null, noticeDialog: null };
const $ = (selector, root = document) => root.querySelector(selector);
const money = value => value == null ? "—" : new Intl.NumberFormat("th-TH", { style: "currency", currency: "THB", maximumFractionDigits: 2 }).format(value);
const dateTime = value => value ? new Intl.DateTimeFormat("th-TH", { dateStyle: "medium", timeStyle: "short", timeZone: "Asia/Bangkok" }).format(new Date(value)) : "ไม่มีข้อมูลเวลา";
const marketName = market => ({ Lazada: "Lazada", Shopee: "Shopee", TikTok: "TikTok Shop" })[market] || market || "ไม่ระบุแพลตฟอร์ม";

async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (state.token) headers.set("Authorization", `Bearer ${state.token}`);
  if (options.body) headers.set("Content-Type", "application/json");
  const response = await fetch(`${API}${path}`, { ...options, headers });
  if (!response.ok) {
    let detail = `เชื่อมต่อไม่สำเร็จ (${response.status})`;
    try { const problem = await response.json(); detail = problem.detail || problem.title || detail; } catch { /* Keep the clear status fallback. */ }
    const error = new Error(detail);
    error.status = response.status;
    throw error;
  }
  if (response.status === 204) return null;
  return response.json();
}

function setMessage(message, kind = "info") {
  const box = $("#status-message");
  box.textContent = message;
  box.classList.toggle("error", kind === "error");
  box.hidden = false;
}

function clearMessage() { $("#status-message").hidden = true; }

function resetSession() {
  state.token = null;
  sessionStorage.removeItem(TOKEN_KEY);
  updateSessionUI();
}

function updateSessionUI() {
  $("#login-button").textContent = state.token ? "ออกจากเดโม" : "เข้าโหมดเดโม";
  $("#personal-area").hidden = !state.token;
  $("#watch-count").textContent = state.watchCount || 0;
  if (!state.token) {
    $("#watchlist-content").replaceChildren(emptyNote("เข้าสู่โหมดเดโมเพื่อติดตามสินค้า"));
    $("#alerts-content").replaceChildren(emptyNote("รายการแจ้งเตือนจะแสดงที่นี่"));
    $("#watch-total").textContent = "0 รายการ";
    $("#alert-total").textContent = "0 รายการ";
  }
}

function emptyNote(text) {
  const node = document.createElement("p");
  node.className = "empty-note";
  node.textContent = text;
  return node;
}

function formatDifference(percent) {
  if (percent == null) return { text: "ข้อมูลราคาไม่พอ", kind: "neutral" };
  if (percent > 0) return { text: `ต่ำกว่าค่าเฉลี่ย ${percent.toFixed(1)}%`, kind: "" };
  if (percent < 0) return { text: `สูงกว่าค่าเฉลี่ย ${Math.abs(percent).toFixed(1)}%`, kind: "above" };
  return { text: "เท่ากับค่าเฉลี่ย 30 วัน", kind: "neutral" };
}

async function loadProducts() {
  clearMessage();
  const grid = $("#product-grid");
  grid.setAttribute("aria-busy", "true");
  grid.replaceChildren();
  const placeholder = document.createElement("div");
  placeholder.className = "empty-state";
  placeholder.textContent = "กำลังค้นหาสินค้า…";
  grid.append(placeholder);
  const params = new URLSearchParams({ page: String(state.page), pageSize: String(PAGE_SIZE), sort: state.sort });
  if (state.query) params.set("q", state.query);
  if (state.marketplace) params.set("marketplace", state.marketplace);
  if (state.category) params.set("category", state.category);
  if (state.minPrice) params.set("minPrice", state.minPrice);
  if (state.maxPrice) params.set("maxPrice", state.maxPrice);
  try {
    const result = await api(`/api/products?${params}`);
    state.total = result.total;
    const cards = await Promise.all(result.items.map(async product => {
      let insight = null;
      try { insight = await api(`/api/products/${encodeURIComponent(product.id)}/insights`); }
      catch { /* Catalog remains useful when price evidence is temporarily unavailable. */ }
      return makeCard(product, insight);
    }));
    grid.replaceChildren(...(cards.length ? cards : [makeEmptySearch()]));
    $("#results-caption").textContent = `${new Intl.NumberFormat("th-TH").format(result.total)} รายการ · ${state.page} หน้า`;
    updatePagination(result.total);
  } catch (error) {
    grid.replaceChildren();
    setMessage(`โหลดสินค้าไม่สำเร็จ: ${error.message}`, "error");
    $("#results-caption").textContent = "ยังโหลดข้อมูลไม่ได้";
    updatePagination(0);
  } finally {
    grid.removeAttribute("aria-busy");
  }
}

function makeEmptySearch() {
  const empty = document.createElement("div");
  empty.className = "empty-state";
  const copy = document.createElement("p");
  const title = document.createElement("strong");
  title.textContent = "ยังไม่พบสินค้าที่ตรงกัน";
  copy.append(title, document.createTextNode("ลองเปลี่ยนคำค้นหาหรือตัวกรอง แล้วค้นหาอีกครั้ง"));
  empty.append(copy);
  return empty;
}

function makeCard(product, insight) {
  const card = $("#product-card-template").content.firstElementChild.cloneNode(true);
  $(".market-label", card).textContent = marketName(product.marketplace);
  $(".sample-tag", card).hidden = !product.isSample;
  $(".card-title", card).textContent = product.title;
  $(".variant-label", card).textContent = `${product.category} · ${product.variantLabel}`;
  $(".current-price", card).textContent = money(product.currentPrice);
  const originalPrice = $(".original-price", card);
  originalPrice.textContent = product.originalPrice == null ? "" : `ราคาป้าย ${money(product.originalPrice)}`;
  originalPrice.hidden = product.originalPrice == null;
  $(".captured-label", card).textContent = product.capturedAt ? `บันทึก ${dateTime(product.capturedAt)}` : "ยังไม่มีประวัติราคา";
  const badge = $(".card-savings", card);
  if (insight && insight.evidence === "provisional") {
    const difference = formatDifference(insight.realDiscountPercent);
    badge.textContent = difference.text;
    if (difference.kind) badge.classList.add(difference.kind);
  } else {
    badge.textContent = insight?.evidence === "stale" ? "ราคาเก่าเกิน 24 ชม." : "ประวัติยังไม่พอ";
    badge.classList.add("neutral");
  }
  $(".detail-button", card).addEventListener("click", () => openProduct(product.id));
  return card;
}

function updatePagination(total) {
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const nav = $("#pagination");
  nav.hidden = pages <= 1;
  $("#previous-page").disabled = state.page <= 1;
  $("#next-page").disabled = state.page >= pages;
  $("#page-caption").textContent = `หน้า ${state.page} จาก ${pages}`;
}

async function openProduct(productId) {
  const dialog = state.productDialog;
  const content = $("#detail-content");
  const heading = document.createElement("h2");
  heading.id = "detail-title";
  heading.textContent = "กำลังโหลดรายละเอียดสินค้า";
  const loading = emptyNote("กำลังโหลดประวัติราคา…");
  content.replaceChildren(heading, loading);
  if (!dialog.open) dialog.showModal();
  try {
    const [product, prices, insight] = await Promise.all([
      api(`/api/products/${encodeURIComponent(productId)}`),
      api(`/api/products/${encodeURIComponent(productId)}/prices?pageSize=100`),
      api(`/api/products/${encodeURIComponent(productId)}/insights`)
    ]);
    state.detailProduct = product;
    renderDetail(product, prices.items, insight);
  } catch (error) {
    content.replaceChildren(emptyNote(`เปิดรายละเอียดไม่ได้: ${error.message}`));
  }
}

function renderDetail(product, prices, insight) {
  const root = $("#detail-content");
  root.replaceChildren();
  const titleRow = document.createElement("div");
  titleRow.className = "detail-title-row";
  const names = document.createElement("div");
  const kicker = document.createElement("p");
  kicker.className = "eyebrow";
  kicker.textContent = `${marketName(product.marketplace)} · ${product.isSample ? "ข้อมูลตัวอย่าง" : "ข้อมูลสินค้า"}`;
  const title = document.createElement("h2");
  title.id = "detail-title";
  title.textContent = product.title;
  const variant = document.createElement("p");
  variant.className = "detail-variant";
  variant.textContent = `${product.category} · ${product.variantLabel}`;
  names.append(kicker, title, variant);
  titleRow.append(names);
  root.append(titleRow);

  const evidence = document.createElement("section");
  evidence.className = "price-evidence";
  evidence.setAttribute("aria-label", "ราคาปัจจุบันและสรุปประวัติราคา");
  const current = document.createElement("div");
  current.className = "detail-current";
  const priceBlock = document.createElement("div");
  const price = document.createElement("strong");
  price.textContent = money(insight.currentPrice);
  const caption = document.createElement("span");
  caption.textContent = insight.capturedAt ? `บันทึกล่าสุด ${dateTime(insight.capturedAt)}` : "ยังไม่มีการบันทึกราคา";
  const original = document.createElement("span");
  original.className = "detail-original";
  original.textContent = product.originalPrice == null ? "" : `ราคาป้าย ${money(product.originalPrice)}`;
  original.hidden = product.originalPrice == null;
  priceBlock.append(price, original, caption);
  const freshness = document.createElement("span");
  freshness.className = "sample-tag";
  freshness.textContent = product.isSample ? "ราคาจำลอง" : insight.isFresh ? "ข้อมูลไม่เกิน 24 ชม." : "ข้อมูลเก่ากว่า 24 ชม.";
  current.append(priceBlock, freshness);
  const note = document.createElement("p");
  note.className = `evidence-note ${insight.evidence === "provisional" ? "saving" : "neutral"}`;
  note.textContent = insight.evidence === "provisional"
    ? `${formatDifference(insight.realDiscountPercent).text} · เทียบกับค่าเฉลี่ยของ ${insight.priorDays} วันที่มีข้อมูลในช่วง 30 วัน ไม่ใช่ Deal Score`
    : evidenceText(insight.evidence, insight.priorDays);
  evidence.append(current, note, makeWindows(insight.windows));
  root.append(evidence);

  const historyTitle = document.createElement("h3");
  historyTitle.className = "section-label";
  historyTitle.textContent = "กราฟประวัติราคา";
  root.append(historyTitle, makeChart(prices));
  const tableTitle = document.createElement("h3");
  tableTitle.className = "section-label";
  tableTitle.textContent = `ราคาที่บันทึก (${prices.length} รายการล่าสุด)`;
  root.append(tableTitle, makePriceTable(prices));

  const actions = document.createElement("div");
  actions.className = "detail-actions";
  const targetLabel = document.createElement("label");
  targetLabel.className = "target-field";
  targetLabel.textContent = "แจ้งเตือนเมื่อราคาต่ำกว่า (เว้นว่างเพื่อติดตามอย่างเดียว)";
  const target = document.createElement("input");
  target.type = "number";
  target.min = "0";
  target.step = "0.01";
  target.inputMode = "decimal";
  target.placeholder = "เช่น 900";
  targetLabel.append(target);
  const watch = document.createElement("button");
  watch.className = "search-button";
  watch.type = "button";
  watch.textContent = "ติดตามสินค้า";
  watch.addEventListener("click", () => saveWatch(product, target.value));
  actions.append(targetLabel, watch);
  if (product.isSample) {
    const tick = document.createElement("button");
    tick.className = "quiet-button";
    tick.type = "button";
    tick.textContent = "จำลองราคาลด 20%";
    tick.addEventListener("click", () => simulatePrice(product));
    actions.append(tick);
  }
  if (product.affiliateUrl) {
    const link = document.createElement("a");
    link.className = "external-link";
    link.href = product.affiliateUrl;
    link.target = "_blank";
    link.rel = "noopener noreferrer sponsored";
    link.textContent = "ไปยังร้านค้า";
    actions.append(link);
  }
  root.append(actions);
}

function evidenceText(evidence, days) {
  if (evidence === "noHistory") return "ยังไม่มีข้อมูลราคาที่บันทึกไว้ จึงยังเปรียบเทียบราคาไม่ได้";
  if (evidence === "stale") return "ราคาล่าสุดเก่ากว่า 24 ชั่วโมง ส่วนต่างอาจไม่สะท้อนราคาปัจจุบัน";
  if (evidence === "outOfStock") return "สินค้าหมดในข้อมูลล่าสุด จึงยังไม่แสดงส่วนต่างราคา";
  return `มีข้อมูลย้อนหลัง ${days} วัน ซึ่งยังไม่เพียงพอสำหรับสรุปส่วนต่างราคา`;
}

function makeWindows(windows) {
  const grid = document.createElement("div");
  grid.className = "window-grid";
  for (const window of windows) {
    const item = document.createElement("div");
    item.className = "window-item";
    const period = document.createElement("span");
    period.className = "window-days";
    period.textContent = `${window.days} วัน · มีข้อมูล ${window.observedDays} วัน`;
    const average = document.createElement("strong");
    average.className = "window-average";
    average.textContent = `เฉลี่ย ${money(window.averagePrice)}`;
    const low = document.createElement("span");
    low.className = "window-low";
    low.textContent = `ต่ำสุด ${money(window.lowestPrice)}`;
    item.append(period, average, low);
    grid.append(item);
  }
  return grid;
}

function makeChart(prices) {
  const wrap = document.createElement("div");
  wrap.className = "chart-wrap";
  const sorted = [...prices].sort((a, b) => new Date(a.capturedAt) - new Date(b.capturedAt));
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.classList.add("history-chart");
  svg.setAttribute("viewBox", "0 0 640 160");
  svg.setAttribute("role", "img");
  svg.setAttribute("aria-label", sorted.length ? `กราฟแสดงราคาตั้งแต่ ${dateTime(sorted[0].capturedAt)} ถึง ${dateTime(sorted.at(-1).capturedAt)}` : "ยังไม่มีข้อมูลสำหรับกราฟ");
  const desc = document.createElementNS(svg.namespaceURI, "desc");
  desc.textContent = sorted.length ? `ราคาต่ำสุด ${money(Math.min(...sorted.map(x => x.price)))} ราคาสูงสุด ${money(Math.max(...sorted.map(x => x.price)))}` : "ไม่มีการบันทึกราคา";
  svg.append(desc);
  for (const y of [24, 72, 120]) {
    const line = document.createElementNS(svg.namespaceURI, "line");
    line.setAttribute("x1", "0"); line.setAttribute("x2", "640"); line.setAttribute("y1", String(y)); line.setAttribute("y2", String(y)); line.setAttribute("class", "chart-grid");
    svg.append(line);
  }
  if (sorted.length) {
    const defs = document.createElementNS(svg.namespaceURI, "defs");
    const gradient = document.createElementNS(svg.namespaceURI, "linearGradient");
    gradient.id = "chart-fill";
    gradient.setAttribute("x1", "0"); gradient.setAttribute("x2", "0"); gradient.setAttribute("y1", "0"); gradient.setAttribute("y2", "1");
    const top = document.createElementNS(svg.namespaceURI, "stop");
    top.setAttribute("offset", "0%"); top.setAttribute("stop-color", "#55a9c8"); top.setAttribute("stop-opacity", ".24");
    const bottom = document.createElementNS(svg.namespaceURI, "stop");
    bottom.setAttribute("offset", "100%"); bottom.setAttribute("stop-color", "#55a9c8"); bottom.setAttribute("stop-opacity", "0");
    gradient.append(top, bottom); defs.append(gradient); svg.append(defs);
    const values = sorted.map(x => Number(x.price));
    const min = Math.min(...values); const max = Math.max(...values); const span = max - min || 1;
    const points = values.map((value, index) => `${sorted.length === 1 ? 320 : 14 + index * (612 / (sorted.length - 1))},${136 - ((value - min) / span) * 112}`).join(" ");
    const polygon = document.createElementNS(svg.namespaceURI, "polygon");
    polygon.setAttribute("points", `14,136 ${points} 626,136`); polygon.setAttribute("class", "chart-area");
    const polyline = document.createElementNS(svg.namespaceURI, "polyline");
    polyline.setAttribute("points", points); polyline.setAttribute("class", "chart-line");
    svg.append(polygon, polyline);
    for (const index of [0, sorted.length - 1]) {
      const x = sorted.length === 1 ? 320 : 14 + index * (612 / (sorted.length - 1));
      const y = 136 - ((values[index] - min) / span) * 112;
      const dot = document.createElementNS(svg.namespaceURI, "circle");
      dot.setAttribute("cx", String(x)); dot.setAttribute("cy", String(y)); dot.setAttribute("r", "4"); dot.setAttribute("class", "chart-dot");
      svg.append(dot);
    }
  }
  const legend = document.createElement("div");
  legend.className = "history-legend";
  legend.textContent = sorted.length ? `${dateTime(sorted[0].capturedAt)} · ต่ำสุด ${money(Math.min(...sorted.map(x => x.price)))} · สูงสุด ${money(Math.max(...sorted.map(x => x.price)))} · ${dateTime(sorted.at(-1).capturedAt)}` : "ไม่มีข้อมูลราคาในช่วงนี้";
  wrap.append(svg, legend);
  return wrap;
}

function makePriceTable(prices) {
  const wrap = document.createElement("div");
  wrap.className = "history-table-wrap";
  if (!prices.length) { wrap.append(emptyNote("ยังไม่มีข้อมูลประวัติราคา")); return wrap; }
  const table = document.createElement("table");
  table.className = "history-table";
  const head = document.createElement("thead");
  const header = document.createElement("tr");
  for (const title of ["วันที่บันทึก", "ราคา", "สถานะ"]) { const th = document.createElement("th"); th.scope = "col"; th.textContent = title; header.append(th); }
  head.append(header);
  const body = document.createElement("tbody");
  for (const item of prices) {
    const row = document.createElement("tr");
    for (const value of [dateTime(item.capturedAt), money(item.price), item.inStock ? "มีสินค้า" : "หมด"] ) { const cell = document.createElement("td"); cell.textContent = value; row.append(cell); }
    body.append(row);
  }
  table.append(head, body); wrap.append(table);
  return wrap;
}

async function ensureLogin() {
  if (state.token) return true;
  state.noticeDialog.showModal();
  return new Promise(resolve => { state.resolveLogin = resolve; });
}

async function signIn() {
  try {
    const session = await api("/api/demo/session", { method: "POST", body: "{}" });
    state.token = session.accessToken;
    sessionStorage.setItem(TOKEN_KEY, state.token);
    state.noticeDialog.close();
    updateSessionUI();
    await loadPersonalData();
    state.resolveLogin?.(true); state.resolveLogin = null;
    return true;
  } catch (error) {
    setMessage(`เข้าสู่โหมดเดโมไม่สำเร็จ: ${error.message}`, "error");
    state.noticeDialog.close();
    state.resolveLogin?.(false); state.resolveLogin = null;
    return false;
  }
}

async function saveWatch(product, rawTarget) {
  if (!(await ensureLogin())) return;
  const request = { productId: product.id };
  if (rawTarget.trim() !== "") {
    const target = Number(rawTarget);
    if (!Number.isFinite(target) || target < 0) { setMessage("กรุณาใส่ราคาเป้าหมายเป็นจำนวนเงินบาทตั้งแต่ 0 ขึ้นไป", "error"); return; }
    request.targetPrice = target;
  }
  try {
    await api("/api/watchlist", { method: "POST", body: JSON.stringify(request) });
    await loadPersonalData();
    setMessage(request.targetPrice == null ? "เพิ่มสินค้าในรายการติดตามแล้ว" : `ตั้งแจ้งเตือนเมื่อราคาต่ำกว่า ${money(request.targetPrice)} แล้ว`);
  } catch (error) { handleApiError(error); }
}

async function simulatePrice(product) {
  if (!(await ensureLogin())) return;
  try {
    const result = await api("/api/demo/tick", { method: "POST", body: JSON.stringify({ productId: product.id }) });
    await Promise.all([loadProducts(), loadPersonalData()]);
    await openProduct(product.id);
    setMessage(`จำลองราคาลด 20% เป็น ${money(result.price)} แล้ว`);
  } catch (error) {
    setMessage(error.status === 409 ? "รอสักครู่ก่อนจำลองราคาของสินค้านี้อีกครั้ง" : `จำลองราคาไม่สำเร็จ: ${error.message}`, error.status === 409 ? "info" : "error");
  }
}

function handleApiError(error) {
  if (error.status === 401) { resetSession(); setMessage("เซสชันเดโมหมดอายุแล้ว เข้าสู่ระบบใหม่เพื่อใช้รายการติดตาม", "error"); }
  else setMessage(error.message, "error");
}

async function loadPersonalData() {
  if (!state.token) { updateSessionUI(); return; }
  try {
    const [watchlist, alerts] = await Promise.all([api("/api/watchlist"), api("/api/alerts")]);
    state.watchCount = watchlist.items.length;
    updateSessionUI();
    renderWatchlist(watchlist.items);
    renderAlerts(alerts.items);
  } catch (error) { handleApiError(error); }
}

function renderWatchlist(items) {
  const root = $("#watchlist-content");
  $("#watch-total").textContent = `${items.length} รายการ`;
  $("#watch-count").textContent = items.length;
  if (!items.length) { root.replaceChildren(emptyNote("ยังไม่มีสินค้าที่ติดตาม เลือกดูประวัติราคาแล้วเพิ่มได้เลย")); return; }
  root.replaceChildren(...items.map(item => {
    const row = document.createElement("div"); row.className = "watch-row";
    const main = document.createElement("div"); main.className = "watch-main";
    const name = document.createElement("div"); name.className = "watch-name"; name.textContent = item.product.title;
    const info = document.createElement("div"); info.className = "watch-detail"; info.textContent = item.targetPrice == null ? `ราคาล่าสุด ${money(item.product.currentPrice)} · ติดตามอย่างเดียว` : `ราคาล่าสุด ${money(item.product.currentPrice)} · เป้าหมาย ${money(item.targetPrice)}`;
    main.append(name, info);
    const remove = document.createElement("button"); remove.className = "remove-button"; remove.type = "button"; remove.textContent = "เลิกติดตาม";
    remove.setAttribute("aria-label", `เลิกติดตาม ${item.product.title}`);
    remove.addEventListener("click", async () => {
      try { await api(`/api/watchlist/${encodeURIComponent(item.id)}`, { method: "DELETE" }); await loadPersonalData(); }
      catch (error) { handleApiError(error); }
    });
    row.append(main, remove); return row;
  }));
}

function renderAlerts(items) {
  const root = $("#alerts-content");
  $("#alert-total").textContent = `${items.length} รายการ`;
  if (!items.length) { root.replaceChildren(emptyNote("ยังไม่มีการแจ้งเตือน เมื่อราคาผ่านเป้าที่ตั้งไว้จะแสดงที่นี่")); return; }
  root.replaceChildren(...items.map(item => {
    const row = document.createElement("article"); row.className = "alert-row";
    const main = document.createElement("div"); main.className = "alert-main";
    const name = document.createElement("div"); name.className = "alert-name"; name.textContent = `ถึงเป้าแล้ว · ${item.title}`;
    const detail = document.createElement("div"); detail.className = "alert-detail"; detail.textContent = `${money(item.price)} · เป้าหมาย ${money(item.targetPrice)} · ${dateTime(item.createdAt)}${item.isSample ? " · ตัวอย่าง" : ""}`;
    main.append(name, detail); row.append(main); return row;
  }));
}

async function start() {
  state.productDialog = $("#product-dialog");
  state.noticeDialog = $("#notice-dialog");
  $("#search-form").addEventListener("submit", event => { event.preventDefault(); state.query = $("#query").value.trim(); state.page = 1; loadProducts(); });
  for (const [id, property] of [["marketplace", "marketplace"], ["category", "category"], ["sort", "sort"]]) {
    $(`#${id}`).addEventListener("change", event => { state[property] = event.target.value; state.page = 1; loadProducts(); });
  }
  $("#max-price").addEventListener("change", event => { state.maxPrice = event.target.value; state.page = 1; loadProducts(); });
  $("#min-price").addEventListener("change", event => { state.minPrice = event.target.value; state.page = 1; loadProducts(); });
  $("#previous-page").addEventListener("click", () => { state.page--; loadProducts(); window.scrollTo({ top: 0, behavior: "smooth" }); });
  $("#next-page").addEventListener("click", () => { state.page++; loadProducts(); window.scrollTo({ top: 0, behavior: "smooth" }); });
  $("#close-detail").addEventListener("click", () => state.productDialog.close());
  state.productDialog.addEventListener("click", event => { if (event.target === state.productDialog) state.productDialog.close(); });
  document.querySelectorAll("[data-close-notice]").forEach(button => button.addEventListener("click", () => { state.noticeDialog.close(); state.resolveLogin?.(false); state.resolveLogin = null; }));
  state.noticeDialog.addEventListener("cancel", () => { state.resolveLogin?.(false); state.resolveLogin = null; });
  $("#confirm-login").addEventListener("click", signIn);
  $("#login-button").addEventListener("click", async () => {
    if (state.token) { resetSession(); }
    else { state.noticeDialog.showModal(); state.resolveLogin = () => {}; }
  });
  $("#watchlist-open").addEventListener("click", async () => { if (!(await ensureLogin())) return; $("#personal-area").scrollIntoView({ behavior: "smooth", block: "start" }); });
  $("#alerts-open").addEventListener("click", async () => { if (!(await ensureLogin())) return; $("#personal-area").scrollIntoView({ behavior: "smooth", block: "start" }); $("#alerts-content").focus?.(); });
  updateSessionUI();
  try {
    const status = await api("/api/status");
    $("#marketplace-status").textContent = status.isDemo ? "ข้อมูลตัวอย่างเท่านั้น" : "ไม่มี Marketplace สด";
    if (state.token) {
      try { await api("/api/me"); }
      catch { resetSession(); }
    }
  } catch { $("#marketplace-status").textContent = "ตรวจสถานะข้อมูลไม่ได้"; }
  await loadProducts();
  await loadPersonalData();
}

start();
