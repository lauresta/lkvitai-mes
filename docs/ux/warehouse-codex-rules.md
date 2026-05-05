# Warehouse — Codex implementation rules (LKvitai.MES)

Reference design: `Warehouse Shell (redesign).html` (and bundled `warehouse-shell-redesign.html`).
Continuation of approved Portal / Sales Orders visual language. Same tokens, same chip grammar, same table density. Don't invent a new style — lift the structure below.

This document covers the **Warehouse module shell + Available Stock page** (the first screen handed off). Subsequent warehouse pages (Inbound, Outbound, Operations, Finance, Reports) plug into the same shell and reuse the same toolbar / table / panel grammar.

---

## 1. Design tokens

Reuse the full token block from `sales-orders-codex-rules.md` §1. No new colors. Specifically:

- **Teal** (`--accent-*`) — primary, links, active nav, focus, primary buttons.
- **Sand** (`--sand-*`) — only for warn/inprogress chips and group-rows. **Not** for nav, headers, panels.
- **Functional families** (`--ok-* / --warn-* / --info-* / --slate-* / --danger-* / --done-*`) — single grammar for all chips.
- `--font-ui` (Inter) for chrome, `--font-mono` (JetBrains Mono) for SKU/codes/quantities/locations/timestamps.
- All quantities and money use `font-variant-numeric: tabular-nums`.

**Hard rule about yellow:** the previous Bootstrap shell used yellow active-nav. Drop it. Active = teal. Yellow does not exist in this system.

---

## 2. Page structure (full-bleed shell)

```
<test-strip>          full-bleed, only when env != production
<topbar>              full-bleed, dark, brand + module-pill + global search + env + user
<shell-body>          row: sidebar + main, fills viewport height under topbar
  <sidebar>           240px expanded / 52px collapsed, persists in localStorage
    <sidebar__head>   module name + collapse toggle
    <sidebar__nav>    operational sections, divider, secondary sections
    <sidebar__footer> last-sync timestamp
  <main>
    <main__inner>     max-width var(--content-max), centered, fluid padding
      <page-head>     breadcrumb + eyebrow + h1 + sub  ·  page-actions on the right
      <kpis>          4-column strip (same grammar as Orders amounts)
      <dash-grid>     panel(s) — list / detail / sidecards as needed
```

**Width rules:**
- `test-strip`, `topbar`, `shell-body` are **full-bleed** (100% of viewport).
- `sidebar` is fixed width (240px / 52px).
- `main__inner` is content-capped: `max-width: var(--content-max, 1600px)`, padding `clamp(20px, 2.4vw, 36px)`. On 4K the content breathes; on 1280 it stays dense.
- Never let the table or KPIs go full bleed against a 4K monitor — operators get lost.

---

## 3. Sidebar — IA, states, collapse

**Information architecture:**

```
Dashboard                     (single link, no section)
─── Operational ───
Stock          ← section, expandable
  Available stock         ← default landing
  Low stock        (47)
  Search by image
  Stock dashboard
  Location balance
  Adjustments
  Reservations
Inbound        ← section
  Receivings, Returns from customer, Cross-dock, Quality hold, …
Outbound       ← section
  Picking, Packing, Shipments, Returns to supplier, …
Operations     ← section
  Transfers, Counts / cycle counts, Re-bin, Print labels, …
Finance        ← section
  Stock value, Write-offs, Cost adjustments, …
─── divider ───
Reports        ← section
Analytics      ← section
Admin          ← section (warehouse-admin only — locations, ABC, settings)
```

- **Operational** (Stock / Inbound / Outbound / Operations / Finance) sit above a 1px `--n-150` divider.
- **Secondary** (Reports / Analytics / Admin) sit below.
- The system-wide **Admin** module (users, roles, integrations, audit) is **its own module shell** reached from the topbar module switcher — not nested in Warehouse sidebar.

**Section behaviour:**
- One section open at a time (`role="tablist"`-ish, accordion).
- Section auto-expands when its child is the current route.
- Section header: 12.5px, weight 600, `--n-700`. Active section header gets a teal left-edge tick when its body is open and contains the active link (`has-active` class).
- Items: 13px, `--n-700`, hover `--n-50`. Active item: `--accent-50` background + `--accent-700` text + `inset 2px 0 0 var(--accent-500)` left border.
- Right-side count chip (`nav-link__count`) is mono 11px, `--n-100` background, `--n-600` text. Use `is-warn` modifier (sand bg, warn-fg text) when the count is something the operator must act on (e.g. Low stock 47).

**Collapse:**
- Toggle in `sidebar__head`. Collapses to **52px icon-only** rail. Section headers become a glyph; tooltips on hover.
- State is persisted in `localStorage` (`lkvitai.warehouse.sidebar.collapsed`).
- **Do not** auto-expand on hover — operators kept misclicking. Click to expand.

**Mobile (<980px):**
- Sidebar hides off-canvas. Topbar gets a hamburger that slides it in over a scrim.
- On desktop (≥980px) the sidebar is **always visible** — no hamburger.

---

## 4. Topbar

Same grammar as Portal / Orders, plus a **module pill** between brand and search:

```
[brand]  [▣ Warehouse]  │  [search SKU/location/document … ⌘K]  ……  [ENV TEST · VER 1.4.2]  [user]
```

- Module pill (`.module-pill`) is `--accent-50` bg, `--accent-700` text, 11.5px, mono-cased label, with a 10×10 icon. Clicking it opens a popover module switcher (Sales / Warehouse / Production / Admin) — **not** a dropdown of warehouse pages.
- Global search hits SKU, location code, document number (KVT-…/RCV-…/PCK-…/SHP-…), and supplier/customer codes. `⌘K` opens, `Esc` closes, `↑↓` navigates suggestions, `Enter` opens.
- Env badge and user block — identical to Orders.

---

## 5. Available Stock page — content

**Page head:**
- Breadcrumb: `Warehouse › Stock › Available stock`. Last segment in `--n-700`, no link.
- Eyebrow mono: `Warehouse · Stock`.
- `<h1>` = "Available stock".
- Sub: short factual line — what "available" means here (`On-hand minus hard-locked quantities · auto-refresh 5s · 1 284 SKUs across 892 locations`). No marketing copy.
- Page actions on the right: `Export CSV` (outline) + `+ New adjustment` (primary).

**KPI strip (`.kpis`):**
- 4 equal columns, border-left tick in the metric's color, mono value, small delta line.
- Same component as Orders amounts grid (`.amount-card`) — just relabelled `.kpi` here. Don't fork the styling.
- Default 4 KPIs for Available Stock: `Total SKUs`, `Low stock` (warn variant), `Hard-locked`, `Locations used`. All values mono. Delta line uses `up` / `down` modifiers — `up` = `--ok-fg`, `down` = `--danger-fg`. **Direction is semantic, not numeric** — for "Low stock", an *increase* is bad → `down` color.

**Toolbar:**
- `Search SKU, description…` with `/` kbd hint.
- Selects: `Status` (All/OK/Low/Out/Locked), `Location` (warehouse/zone), `Group` (Fabric/Hardware/Accessories/…).
- `Has reservations` checkbox toggle.
- Top pagination on the right.

**Table columns:**
`select | SKU | Description | Location | On hand | Reserved | Available | Status | row-action`

- SKU — mono link, `--accent-700`, with `↗`.
- Description — single line, ellipsis. **No second line.** Use a Flags column if attributes (lot-tracked, serial, hazardous) need to surface.
- Location — mono, format `A-02-03` (zone-aisle-bin). If multi-location, show primary + `(+3)` chip linking to Location balance.
- On hand / Reserved / Available — mono, tabular, right-aligned (`is-num`). `Available` is bold.
- Status chip — see §6.
- Row action — `↗ Open` opens detail; right-click for context menu.

---

## 6. Status chips for stock

Reuse the `.chip` base + dot grammar. Five canonical states:

| Status | Class | Family | Trigger |
|---|---|---|---|
| OK | `chip--ok` | ok (green) | available ≥ reorder point |
| Low | `chip--low` | warn (sand) | 0 < available < reorder point |
| Out | `chip--out` | danger | available ≤ 0 |
| Locked | `chip--locked` | slate | hard-locked, on-hand > 0, available 0 |
| Reserved | `chip--reserved` | info (teal-dim) | majority of on-hand is reserved against open orders |

If a SKU is in two states (e.g. low **and** mostly reserved), pick the more actionable one for the chip and let the operator see the raw numbers in the columns.

---

## 7. Density, sticky head, selection

Identical to Orders:
- Compact only. Row height ~30px, td padding `6px 12px`.
- `position: sticky; top: 0` on `<thead>` inside the table's scroll container. Header bg `--n-50`, border-bottom `--n-200`.
- Selected row (when its detail drawer/panel is open): `--accent-50` background + `inset 3px 0 0 var(--accent-500)` left.
- Hover: `--n-25`.

---

## 8. MudBlazor mapping

Same overrides as Orders (§6 in `sales-orders-codex-rules.md`). Specifically for Warehouse:

- **Topbar** — custom CSS, not `MudAppBar`'s defaults. Module pill is custom.
- **Sidebar** — **don't** use `MudNavMenu` directly; its expansion animation and active-state styling fight the design. Render a custom `<nav class="sidebar">` with plain `<a>` + `<button>` and bind active state from the route. If MudNavMenu is mandated, override `.mud-nav-link.mud-nav-link-active` to match the spec exactly (teal-50 bg, teal-700 fg, inset 2px teal border) and disable Mud's default ripple.
- **MudDataGrid** — same skin as Orders. ServerData for the stock list; auto-refresh every 5s using a `Timer` that re-queries server-side, not a client-side cache. Show a tiny "Updated 09:42:18" mono stamp in `panel__head` next to the count.
- **MudDatePicker** / **MudDateRangePicker** — keep them, skin the trigger to match `.field` (label inside, neutral border, teal focus ring).
- **Drop Bootstrap entirely.** No `container`, no `row/col`, no `btn-warning`. Layout is CSS Grid + Flex against the tokens.

---

## 9. Search & filtering (server-side)

- Single search box queries SKU, description, location code, **and** alternate codes (supplier SKU, EAN). Debounce 200ms.
- Status / Location / Group / HasReservations are AND'd with search.
- Filter chips render under the toolbar when active so the operator can dismiss individually (`Status: Low ×`, `Location: A ×`). Don't hide active filters inside collapsed selects.
- Empty state inside the table: neutral icon + "No SKUs match these filters" + "Clear filters" link button. Same grammar as Orders.

---

## 10. Auto-refresh, real-time, deltas

Available Stock is the operator's live picture:

- **5-second auto-refresh** of the visible page (server poll, not WebSocket — keeps it simple and cache-friendly).
- A row whose `Available` changed since last poll briefly flashes the cell background (`--ok-bg` if up, `--warn-bg` if down) for ~1.2s, then fades. **No persistent highlight.**
- KPI deltas (`↑ 12 vs last week`) are computed server-side at page load; they don't tick during the session. The auto-refresh only updates the table.
- A pause toggle (`⏸ Pause refresh`) lives in `panel__head` for when an operator is mid-edit.

---

## 11. Mobile (≤760px)

- Sidebar off-canvas (see §3).
- KPI strip: 4 → 2 columns.
- Toolbar wraps; selects become full-width.
- Table replaced by `mobile-cards`:
  - SKU mono (top-left), Status chip (top-right).
  - Description on second line.
  - Meta row: `Location · On hand · Reserved · **Available**` — Available bolded.
  - Tap → opens detail.
- Page actions wrap; primary stays last (`+ New adjustment`).

---

## 12. Things missing from the legacy Bootstrap shell that must exist

1. Persistent sidebar collapse (icon-only rail).
2. Module switcher in topbar (popover, not dropdown of pages).
3. Sticky table head.
4. Active-route auto-expand of the matching sidebar section.
5. Server-side combined search + filter (incl. alternate SKU codes).
6. Filter chips strip under the toolbar.
7. 5-second auto-refresh with cell-flash deltas + pause control.
8. Empty / loading / error states inside the table area.
9. Keyboard: `/` focus search, `↑↓` move row selection, `Enter` open, `Esc` close detail, `[` / `]` collapse/expand sidebar.
10. Right-click row → copy SKU / open in new tab / open in Adjustments.
11. ARIA: sidebar `<nav aria-label="Warehouse navigation">`, sections as accordions with `aria-expanded`, table with `<caption class="sr-only">`.

---

## 13. Don't

- Don't bring back yellow/orange active states. Active = teal.
- Don't use Bootstrap classes anywhere in the warehouse module.
- Don't put Admin links in the warehouse sidebar — Admin is a separate module.
- Don't auto-expand sidebar sections on hover. Click only.
- Don't render quantities or location codes in proportional fonts.
- Don't push KPI cards full-bleed on wide screens — content stays capped at `--content-max`.
- Don't use MudBlazor's default `Color.Primary` on chips/buttons. Always go through token classes.
- Don't add a second line to the Description cell. Use a Flags column if needed.
- Don't ship a density toggle. Compact only.
- Don't make the auto-refresh flash persistent — 1.2s and out.
