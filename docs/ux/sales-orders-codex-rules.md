# Sales Orders — Codex implementation rules (LKvitai.MES)

> **Extends: `lkvitai-shell-rules.md` — read it first. This file only adds Sales-Orders-specific rules.**

Reference design: `docs/ux/orders-static-preview.html` (and `docs/ux/reference/sales-orders-redesign.html` as raw designer export).

---

## 1. Page structure

```
<test-strip>            (only when env != production)
<topbar>                (dark, brand + module pill + global search + env badge + user)
<shell-body>
  <sidebar>             (per lkvitai-shell-rules §5)
  <main>
    <main__inner>
      <page-head>       (eyebrow + h1 + sub) + (page-actions: Export, +New order)
      <panel.orders-list>
        <panel__head>   (title + meta)
        <toolbar>       (search, status, store, date, has-debt, paging-top)
        <table.lk-table> (sticky head, dense)
        <table-foot>    (showing X of N + paging-bottom)
      <panel.details>
        <breadcrumb>
        <details__title> (id, customer, sub-meta, status chip, actions)
        <section> Items  (with group-rows + acc-rows)
        <section> Amounts (6-card grid)
        <section> Employees
```

---

## 2. Orders table — columns, density, behaviour

**Density:** single fixed Compact mode. No toggle. Row height ~30px, `td` padding `6px 12px`.

**Columns (in order):**

`select | Number | Date | Price | Debt | Customer | Flags | Status | Store | Address`

**Customer column = single line.** No second-line meta. Ellipsis on overflow.

**Flags column** — 24px wide, immediately after Customer. Each flag is a single 14×14px mono glyph. Render only when set:

| Flag | Class | Meaning | Color |
|---|---|---|---|
| `€` | `cust__flag--debt` | Has debt | danger |
| `★` | `cust__flag--vip` | VIP / important | warn (sand) |
| `!` | `cust__flag--note` | Has internal note | info (teal) |

Extensible: add `cust__flag--blocked`, `cust__flag--prepaid`, `cust__flag--branch`, etc. as needed. Always a single glyph, never text.

**Sticky table head** per `lkvitai-shell-rules §7`. Header bg `--n-50`. **Never sand on `<th>`.**

**Selected row** (details shown below): `--accent-50` bg + `inset 3px 0 0 var(--accent-500)` + class `is-selected`.

**Row hover:** `--n-25` background.

**Order link** (`KVT-…`): mono, `--accent-700`. Subtle `↗` suffix. **No icon in the link** — keep it text + `↗`.

**Debt color tiers** on the Debt column:

- `0,00 €` → `--n-400` (muted)
- positive, on-time → `--n-900` bold
- overdue → `--danger-fg` bold (class `is-debt-overdue`)

**Toolbar:** search (`/` keyboard hint), Status / Store / Date selects, "Has debt" checkbox toggle, top pagination on the right. See `lkvitai-shell-rules §8`.

---

## 3. Sales status mapping

| Status | Class | Family |
|---|---|---|
| Įvestas | `chip--entered` | slate (neutral grey) |
| Patvirtintas | `chip--approved` | info (teal) |
| Gaminamas | `chip--inprogress` | warn (sand) |
| Pagamintas | `chip--made` | ok (green) |
| Filialui | `chip--shipped` | info dimmed |
| Atiduotas | `chip--delivered` | done (muted green) |
| Sustabdytas | `chip--paused` | danger |
| Atšauktas | `chip--cancelled` | neutral, line-through |

If a new status appears, map it to one of the six functional families from `lkvitai-shell-rules §6`. Never bright/random hues.

---

## 4. Order details panel

- **Breadcrumb** in panel head: teal links, mono order id at the end.
- **`details__title`:**
  - mono id (small, `--n-500`)
  - `<h2>` customer name
  - flexed sub-meta row: Date · Address · Store · Status chip
  - operator + timestamp italic muted (e.g. `Entered by Jonas K. · 2026-04-15 09:12`)
- **Title actions** (right side): `Print`, `Email customer`, primary `Open in PoS`.
- **Items table:**
  - `group-row` per product: `--sand-25` bg, `--sand-700` text — visually separates parent product from accessories.
  - `acc-row` per accessory: `--n-25` bg, italic muted, `↳` prefix on item name.
- **Amounts grid:** 6 equal cards (responsive 6 → 3 → 2). Three accented:
  - `amount-card--total` (After discount) — teal border-left tick.
  - `amount-card--paid` — ok green text.
  - `amount-card--debt` — sand-pink bg + danger text.
- **Employees table:** `lk-table` grammar, `emp-avatar` initials circle + `duty` colored dot prefix (sales=teal, prod=sand, install=ok green).

---

## 5. Search scope

Single search box hits **server-side** across: number, customer name, customer code, address, **product text** (the legacy "hidden searchable product" field — unique to Sales Orders). Debounce ~200ms. Status / Store / Date / HasDebt are AND'd with search.

---

## 6. Things missing from the original static page that must exist

1. Server-side search including the hidden product text field.
2. Selected-row state synced with details panel (`SelectedOrderId` server-side parameter).
3. Debt color tiering (zero muted / pending bold / overdue danger).
4. Customer Flags column (extensible, `€ ★ !`).
5. Operator + timestamp on details title.
6. Bulk select (checkbox column) for future bulk actions (export, mark, etc.).

Full keyboard, loading/error states, and filter strip are covered by `lkvitai-shell-rules §8, §11, §12`.

---

## 7. MudBlazor mapping (Sales-specific)

- **`MudDataGrid`** for orders list. Class `lk-grid`. `ServerData="@…"`. Selected row via `SelectedOrderId` — `is-selected` class added server-side, not via Mud's built-in row selection highlight.
- **Order details** — plain Razor components inside `MudPaper class="panel"`: `<OrderDetailsHeader/>`, `<OrderItemsTable/>`, `<OrderAmountsGrid/>`, `<OrderEmployeesTable/>`. **Don't use `MudDataGrid`** here — display-only, benefits from semantic HTML `<table>`.

---

## 8. Sales-specific don'ts

- Don't use sand on `<th>` or as default panel background.
- Don't add a second line under customer name. Use the Flags column.
- Don't put icons in the order link — keep it text + `↗`.
- Don't invent new status colours. Map to the six functional families.

---

## 9. Follow-up decisions from design review

### Density

No density toggle. Fixed compact mode only. Row height ~30px, padding ~`6px 12px`.

### Customer column

Single line: customer name only, ellipsis on overflow. No subline / customer code in the desktop row.

### Flags column

Narrow (24px), immediately after Customer. Flags render only when the backend returns `HasDebt`, `IsVip`, `HasNote`, etc. Razor template renders classes from that data.

### Static preview status

`docs/ux/orders-static-preview.html` is the approved visual baseline reflecting all decisions above. Raw designer export preserved at `docs/ux/reference/sales-orders-redesign.html`.
