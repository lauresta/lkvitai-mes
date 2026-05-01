// Tile status enum (see .module-card.is-* classes in styles.css):
//   - active     → green, fully navigable
//   - scaffolded → amber/grey, navigable (WIP — module vertical works end-to-end but has no business logic yet)
//   - planned    → grey, click shows a toast with the target quarter
const modules = [
  { key: "warehouse", title: "Warehouse", category: "Operations", desc: "Inventory locations, stock movements, reservations, handling units, 3D warehouse layout.", status: "active", url: "/warehouse/" },
  { key: "sales", title: "Sales", category: "Commercial", desc: "Customer orders.", status: "scaffolded", url: "/sales/" },
  { key: "frontline", title: "Frontline", category: "Field", desc: "Field availability lookup.", status: "scaffolded", url: "/frontline/" },
  { key: "scanning", title: "Scanning", category: "Mobile", desc: "Mobile barcode scan.", status: "scaffolded", url: "/scan/" },
  { key: "orders", title: "Orders", category: "Commercial", desc: "Order lifecycle, product composition, workflow planning.", status: "planned", quarter: "Q3 2026" },
  { key: "shopfloor", title: "Shopfloor", category: "Operations", desc: "Workstation tasks, WIP routing, operator kiosk execution.", status: "planned", quarter: "Q3 2026" },
  { key: "quality", title: "Quality", category: "Operations", desc: "Inspections, defect tracking, rework and returns.", status: "planned", quarter: "Q4 2026" },
  { key: "delivery", title: "Delivery", category: "Logistics", desc: "Route planning, driver tasks, proof of delivery, tracking.", status: "planned", quarter: "Q4 2026" },
  { key: "installation", title: "Installation", category: "Logistics", desc: "Installer visits, acceptance acts, customer sign-off.", status: "planned", quarter: "Q1 2027" },
  { key: "reporting", title: "Reporting", category: "Intelligence", desc: "Dashboards, KPIs, production and warehouse analytics.", status: "planned", quarter: "Q1 2027" },
  { key: "finance", title: "Finance", category: "Intelligence", desc: "Accounting exports, payments, posting events.", status: "planned", quarter: "Q2 2027" },
  { key: "audit", title: "Audit", category: "Compliance", desc: "Immutable event log, traceability, compliance reports.", status: "planned", quarter: "Q2 2027" },
];

const stages = [
  { key: "reg", label: "Registered", this: 248, last: 221 },
  { key: "acc", label: "Accepted", this: 140, last: 132 },
  { key: "mfg", label: "Manufacturing", this: 480, last: 455 },
  { key: "ship", label: "Shipped", this: 56, last: 48 },
  { key: "done", label: "Completed", this: 150, last: 142 },
];

const daily = {
  this: [5, 6, 7, 6, 8, 2, 1, 6, 7, 8, 7, 9, 3, 2, 7, 9, 8, 7, 6, 3, 2, 8, 9, 7, 8, 6, 2, 1, 8, 9],
  last: [4, 5, 6, 6, 7, 1, 1, 5, 6, 7, 6, 8, 2, 2, 6, 8, 7, 6, 5, 2, 1, 7, 8, 6, 7, 5, 1, 1, 7, 8],
};

const branches = [
  { name: "Vilnius", value: 94 },
  { name: "Kaunas", value: 89 },
  { name: "Klaipeda", value: 91 },
  { name: "Siauliai", value: 84 },
];

const news = [
  { tag: "SHIPPED", tagColor: "oklch(42% 0.14 155)", tagBg: "oklch(94% 0.04 155)", title: "Warehouse 3D layout viewer", excerpt: "Rack-level zoom, stock density heatmap, keyboard navigation.", date: "Apr 22" },
  { tag: "SHIPPED", tagColor: "oklch(42% 0.14 155)", tagBg: "oklch(94% 0.04 155)", title: "Stock movement audit trail", excerpt: "Every transfer now includes operator, reason code and timestamp.", date: "Apr 18" },
  { tag: "IN PROG", tagColor: "var(--accent-700)", tagBg: "var(--accent-50)", title: "Orders module - alpha", excerpt: "Order lifecycle + product composition. Internal QA, Q3 rollout.", date: "ongoing" },
  { tag: "PLANNED", tagColor: "oklch(50% 0.015 240)", tagBg: "var(--n-100)", title: "Shopfloor operator kiosk", excerpt: "Workstation task list, WIP routing. Design review next week.", date: "Q3 2026" },
];

const icons = {
  warehouse: '<path d="M4 7l8-4 8 4v14H4z"/><path d="M9 21V11h6v10"/><path d="M4 10h16"/>',
  sales: '<path d="M6 4h13l-1.2 9H7.4L6 4z"/><path d="M6 4 5 2H2"/><circle cx="9" cy="19" r="1.6"/><circle cx="17" cy="19" r="1.6"/>',
  frontline: '<path d="M12 3l9 4-9 4-9-4 9-4z"/><path d="M3 11l9 4 9-4"/><path d="M3 15l9 4 9-4"/>',
  scanning: '<path d="M4 6V4h4M20 6V4h-4M4 18v2h4M20 18v2h-4"/><path d="M7 8v8M11 8v8M15 8v8M19 8v8"/>',
  orders: '<path d="M6 7h15M6 12h15M6 17h15"/><path d="M3.5 7h.01M3.5 12h.01M3.5 17h.01"/>',
  shopfloor: '<path d="M3 21h18"/><path d="M7 21V8l5-3 5 3v13"/><path d="M9.5 11h5M9.5 14h5"/>',
  quality: '<path d="M9 12l2 2 4-5"/><path d="M12 22c5.5-2 8-6 8-12V6l-8-3-8 3v4c0 6 2.5 10 8 12z"/>',
  delivery: '<path d="M3 16V7h11v9"/><path d="M14 10h4l3 3v3h-7"/><circle cx="7" cy="18" r="2"/><circle cx="17" cy="18" r="2"/>',
  installation: '<path d="M14 7l3 3M7 14l3 3M13 8l-7 7"/><path d="M16 5l3 3-2 2-3-3zM5 16l3 3-2 2-3-3z"/>',
  reporting: '<path d="M4 19V5M4 19h16M8 16v-5M12 16V8M16 16v-3"/>',
  finance: '<path d="M14 6H9a3 3 0 0 0 0 6h4a3 3 0 0 1 0 6H8"/><path d="M12 4v16"/>',
  audit: '<path d="M9 5h6M9 9h6M9 13h6"/><path d="M7 3h10a2 2 0 0 1 2 2v16H5V5a2 2 0 0 1 2-2z"/>',
  lock: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/>',
  wip: '<path d="M12 2v3M4.2 4.2l2.1 2.1M2 12h3M19.8 4.2l-2.1 2.1M22 12h-3"/><path d="M6 14h12l-1 6H7l-1-6z"/>',
};

let activePeriod = "this";
let toastTimer = null;

const host = window.location.hostname.toLowerCase();
const productionHost = "mes.lauresta.com";

function isPrivateIpHost(hostname) {
  const parts = hostname.split(".").map((part) => Number.parseInt(part, 10));
  if (parts.length !== 4 || parts.some((part) => !Number.isInteger(part) || part < 0 || part > 255)) return false;

  return (
    parts[0] === 127 ||
    parts[0] === 10 ||
    (parts[0] === 172 && parts[1] >= 16 && parts[1] <= 31) ||
    (parts[0] === 192 && parts[1] === 168)
  );
}

function getEnvironment(hostname) {
  if (hostname === productionHost) return { name: "PROD", channel: "prod", type: "prod" };
  if (hostname === "localhost" || hostname === "::1" || isPrivateIpHost(hostname)) {
    return { name: "DEV", channel: "dev", type: "dev" };
  }
  return { name: "TEST", channel: "test", type: "test" };
}

const environment = getEnvironment(host);

const moduleGrid = document.querySelector("#module-grid");
const searchInput = document.querySelector("#module-search");
const emptyState = document.querySelector("#empty-state");
const emptyQuery = document.querySelector("#empty-query");
const stageGrid = document.querySelector("#stage-grid");
const dailyBars = document.querySelector("#daily-bars");
const dailyTotal = document.querySelector("#daily-total");
const branchGrid = document.querySelector("#branch-grid");
const newsGrid = document.querySelector("#news-grid");
const toast = document.querySelector("#toast");
const testStrip = document.querySelector("#test-strip");
const portalBannerTitle = document.querySelector("#portal-banner-title");
const portalBannerNote = document.querySelector("#portal-banner-note");
const portalHost = document.querySelector("#portal-host");
const portalEnv = document.querySelector("#portal-env");
const portalStatusEnv = document.querySelector("#portal-status-env");
const portalChannelElement = document.querySelector("#portal-channel");
const portalBuildChannel = document.querySelector("#portal-build-channel");

function renderEnvironment() {
  const isProduction = environment.type === "prod";
  const isDevelopment = environment.type === "dev";

  testStrip.hidden = isProduction;
  testStrip.classList.toggle("test-strip--dev", isDevelopment);
  portalBannerTitle.textContent = isDevelopment ? "DEV ENVIRONMENT" : "TEST ENVIRONMENT";
  portalBannerNote.textContent = isDevelopment
    ? "Local or private-network environment. Data may be reset without notice."
    : "Data in this environment is not production data and may be reset without notice.";
  portalHost.textContent = window.location.host;
  portalEnv.textContent = environment.name;
  portalStatusEnv.textContent = environment.name;
  portalStatusEnv.classList.toggle("status-row__test", environment.type === "test");
  portalStatusEnv.classList.toggle("status-row__dev", isDevelopment);
  portalChannelElement.textContent = environment.channel;
  portalBuildChannel.textContent = environment.channel;
}

function svgIcon(key) {
  return `<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[key] || ""}</svg>`;
}

function showToast(message) {
  clearTimeout(toastTimer);
  toast.textContent = message;
  toast.hidden = false;
  toastTimer = setTimeout(() => {
    toast.hidden = true;
  }, 2600);
}

function renderModules(items) {
  moduleGrid.innerHTML = "";
  items.forEach((mod) => {
    // "active" and "scaffolded" are both navigable → <a>. Only "planned"
    // is a disabled <button> that opens a toast on click.
    const isNavigable = mod.status === "active" || mod.status === "scaffolded";
    const element = document.createElement(isNavigable ? "a" : "button");
    const statusClass =
      mod.status === "active" ? " is-active" :
      mod.status === "scaffolded" ? " is-scaffolded" : "";
    element.className = `module-card${statusClass}`;

    if (isNavigable) {
      element.href = mod.url;
    } else {
      element.type = "button";
      element.addEventListener("click", () => showToast(`${mod.title} - planned for ${mod.quarter}. Not available yet.`));
    }

    let footer;
    if (mod.status === "active") {
      footer = `<span class="module-card__status is-available">Available</span><span class="module-card__action">Open ${mod.title} &rarr;</span>`;
    } else if (mod.status === "scaffolded") {
      footer = `<span class="module-card__status is-scaffolded">Scaffolded</span><span class="module-card__action">Open ${mod.title} &rarr;</span>`;
    } else {
      footer = `<span class="module-card__status mono">Planned &middot; ${mod.quarter}</span><span class="module-card__lock" aria-hidden="true">${svgIcon("lock")}</span>`;
    }

    element.innerHTML = `
      <div class="module-card__top">
        <div class="module-card__icon">${svgIcon(mod.key)}</div>
        <span class="module-card__category mono">${mod.category}</span>
      </div>
      <h3>${mod.title}</h3>
      <p>${mod.desc}</p>
      <div class="module-card__footer">
        ${footer}
      </div>
    `;
    moduleGrid.append(element);
  });

  emptyState.hidden = items.length > 0;
  moduleGrid.hidden = items.length === 0;
}

function filterModules() {
  const query = searchInput.value.trim().toLowerCase();
  const filtered = query
    ? modules.filter((mod) => `${mod.title} ${mod.category} ${mod.desc}`.toLowerCase().includes(query))
    : modules;

  emptyQuery.textContent = `"${searchInput.value}"`;
  renderModules(filtered);
}

function renderOperations() {
  stageGrid.innerHTML = "";
  stages.forEach((stage, index) => {
    const isLast = index === stages.length - 1;
    const stageEl = document.createElement("div");
    stageEl.className = `stage${isLast ? " is-final" : ""}`;
    stageEl.innerHTML = `<span>${stage.label}</span><strong class="mono">${stage[activePeriod]}</strong>`;
    stageGrid.append(stageEl);
  });

  dailyBars.innerHTML = "";
  const values = daily[activePeriod];
  const max = Math.max(...values);
  values.forEach((value, index) => {
    const bar = document.createElement("i");
    bar.style.setProperty("--bar-height", `${Math.max(4, Math.round((value / max) * 26))}px`);
    if (index % 7 === 5 || index % 7 === 6) bar.className = "is-weekend";
    dailyBars.append(bar);
  });
  dailyTotal.textContent = activePeriod === "this" ? "150" : "142";
}

function renderBranches() {
  branchGrid.innerHTML = "";
  branches.forEach((branch) => {
    const item = document.createElement("div");
    item.className = `branch${branch.value < 90 ? " is-warning" : ""}`;
    item.innerHTML = `<span>${branch.name}</span><strong class="mono">${branch.value}%</strong>`;
    branchGrid.append(item);
  });
}

function renderNews() {
  newsGrid.innerHTML = "";
  news.forEach((item) => {
    const article = document.createElement("article");
    article.className = "news-item";
    article.innerHTML = `
      <div class="news-item__meta">
        <span class="news-tag mono" style="color:${item.tagColor};background:${item.tagBg}">${item.tag}</span>
        <span class="news-date mono">${item.date}</span>
      </div>
      <h3>${item.title}</h3>
      <p>${item.excerpt}</p>
    `;
    newsGrid.append(article);
  });
}

document.querySelectorAll("[data-planned]").forEach((button) => {
  button.addEventListener("click", () => {
    showToast(`${button.dataset.planned} - planned for ${button.dataset.quarter}. Not available yet.`);
  });
});

document.querySelectorAll("[data-period]").forEach((button) => {
  button.addEventListener("click", () => {
    activePeriod = button.dataset.period;
    document.querySelectorAll("[data-period]").forEach((item) => item.classList.toggle("is-active", item === button));
    renderOperations();
  });
});

searchInput.addEventListener("input", filterModules);

document.querySelector("#available-count").textContent = modules.filter((mod) => mod.status === "active").length;
document.querySelector("#planned-count").textContent = modules.filter((mod) => mod.status === "planned").length;

renderEnvironment();
renderModules(modules);
renderOperations();
renderBranches();
renderNews();
