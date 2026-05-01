# Sales Orders — Codex implementation rules (LKvitai.MES)

Reference design: `Sales Orders (redesign).html` (and bundled `sales-orders-redesign.html`).
Continuation of approved Portal/Login visual language. Don't invent a new style — lift the tokens, classes and structure below.

---

## 1. Design tokens (lift into `tokens.css` / MudTheme partial)

```css
:root{
  /* Neutrals — cool graphite */
  --n-0:#fff; --n-25:#fbfcfd; --n-50:#f5f6f8; --n-100:#eef1f4;
  --n-150:#e2e7ec; --n-200:#d5dce4; --n-300:#b9c3ce; --n-400:#8996a4;
  --n-500:#66717f; --n-600:#4e5966; --n-700:#323b45; --n-800:#1f2632; --n-900:#151922;

  /* Teal — primary accent (active, links, focus) */
  --accent-50:#eaf8f7; --accent-100:#d1eeec; --accent-400:#56aaa7;
  --accent-500:#2f8f8b; --accent-600:#257773; --accent-700:#1d5d5a;

  /* Sand — scaffolded / operational accent ONLY (status chips, group rows). Never default headers. */
  --sand-25:#faf6ee; --sand-50:#f7f1e5; --sand-100:#ede2c8;
  --sand-150:#e4dbc5; --sand-300:#daceb7; --sand-500:#c79f63; --sand-600:#b08a4a; --sand-700:#7c5f2a;

  /* Functional families — single grammar for all chips */
  --ok-fg:#19744a;     --ok-bg:#e6f4ec;     --ok-bd:#bfe0cd;     --ok-dot:#1f9d61;
  --warn-fg:#7c5f2a;   --warn-bg:#f7eed5;   --warn-bd:#e2cf9a;   --warn-dot:#b08a4a;
  --info-fg:#1d5d5a;   --info-bg:#e3f2f1;   --info-bd:#b4dad7;   --info-dot:#2f8f8b;
  --slate-fg:#3a4554;  --slate-bg:#eaeef3;  --slate-bd:#cdd5de;  --slate-dot:#66717f;
  --danger-fg:#8a1f12; --danger-bg:#fbe7e3; --danger-bd:#f0c2b8; --danger-dot:#c0392b;
  --done-fg:#2a4a3a;   --done-bg:#e3eee7;   --done-bd:#bdd2c4;   --done-dot:#4a7a5d;

  --dark:#20242c; --dark-2:#171b22; --dark-line:#3a404c;

  --font-ui:'Inter','Segoe UI',system-ui,sans-serif;
  --font-mono:'JetBrains Mono','SF Mono',Consolas,monospace;
}
```

**Hard rules:**
- Default surfaces are neutral grey. Sand is **only** for: scaffolded module accents (Portal), Items group-row, and inprogress/warn chips.
- Teal = primary, links, active selection, focus rings, primary buttons.
- All money/dates/dimensions use `font-mono` + `font-variant-numeric: tabular-nums`.

---

## 2. Page structure

```
<test-strip>            (only when env != production)
<topbar>                (dark, brand + global search + env badge + user)
<main>
  <page-head>           (eyebrow + h1 + sub) + (page-actions: Export, +New order)
  <panel.orders-list>
    <panel__head>       (title + meta)
    <toolbar>           (search, status, store, date, has-debt, paging-top)
    <table.lk-table>    (sticky head, dense)
    <table-foot>        (showing X of N + paging-bottom)
  <panel.details>
    <breadcrumb>
    <details__title>    (id, customer, sub-meta, status chip, actions)
    <section> Items     (with group-rows + acc-rows)
    <section> Amounts   (6-card grid)
    <section> Employees
```

---

## 3. Orders table — density, columns, behaviour

**Density:** single fixed Compact mode. No toggle. Row height ~30px, td padding `6px 12px`. As many rows as fit — operators scan, not read.

**Customer column = single line.** No second-line meta. Use the **Flags** column (24px wide) for inline indicators. Flags render only when set:

| Flag | Class | Meaning | Color |
|---|---|---|---|
| `€` | `cust__flag--debt` | Has debt | danger |
| `★` | `cust__flag--vip` | VIP / important | warn (sand) |
| `!` | `cust__flag--note` | Has internal note | info (teal) |

Add more flag types as needed (`--blocked`, `--prepaid`, `--branch`). Always single 14×14 mono glyph; never text.

**Columns (in order):**
`select | Number | Date | Price | Debt | Customer | Flags | Status | Store | Address`

**Sticky table head** (`position: sticky; top: 0; z-index: 1`). Header is `--n-50` neutral + `--n-200` border-bottom. Never sand.

**Selected row state** (when its details are shown below):
- background `--accent-50`
- 3px teal `box-shadow: inset 3px 0 0 var(--accent-500)` on first cell
- class `is-selected`

**Row hover:** `--n-25` background only.

**Order link** (`KVT-…`): mono, `--accent-700`, with subtle `↗` for "open in new tab" affordance.

**Debt color tiers** on the Debt column:
- `0,00 €` → `--n-400` (muted)
- positive, on-time → `--n-900` bold
- overdue → `--danger-fg` bold (class `is-debt-overdue`)

**Toolbar:** search (`/` keyboard hint), Status / Store / Date selects, "Has debt" checkbox toggle, top pagination on the right.

**Pagination chip:** active page = teal-600 fill, others = neutral outline. Disabled = `--n-300` no hover.

---

## 4. Status chips

One grammar for all chips: mono caps, 10.5px, 3px×9px padding, 999px radius, **dot-prefix** (the `::before`).

```css
.chip{ display:inline-flex; align-items:center; gap:6px; padding:3px 9px;
       font-family:var(--font-mono); font-size:10.5px; font-weight:700;
       letter-spacing:.4px; text-transform:uppercase; line-height:1;
       border-radius:999px; border:1px solid transparent; white-space:nowrap; }
.chip::before{ content:""; width:6px; height:6px; border-radius:50%;
               background:currentColor; flex-shrink:0; }
```

**Sales status mapping (proposed — confirm before locking):**

| Status | Class | Family |
|---|---|---|
| Įvestas | `chip--entered` | slate (neutral grey) |
| Patvirtintas | `chip--approved` | info (teal) |
| Gaminamas | `chip--inprogress` | warn (sand) |
| Pagamintas | `chip--made` | ok (green) |
| Filialui | `chip--shipped` | info dimmed |
| Atiduotas | `chip--delivered` | done (muted green) |
| Sustabdytas | `chip--paused` | danger |
| (Atšauktas) | `chip--cancelled` | neutral, line-through |

Never use bright/random hues. If a new status appears, pick from the 6 functional families above.

---

## 5. Order details

- **Breadcrumb** in panel head, teal links, mono order id at the end.
- **details__title:** mono id (small) → `<h2>` customer name → flexed sub-meta row (Date, Address, Store, Status chip) → operator+timestamp italic muted.
- **Title actions** (right side): `Print`, `Email customer`, primary `Open in PoS` (or whatever the primary intent ends up being).
- **Items table:**
  - `group-row` per product (sand-25 background, sand-700 text) — visually separates a parent product from its accessories.
  - `acc-row` per accessory (n-25 background, italic muted, `↳` prefix on item name).
- **Amounts grid:** 6 equal cards (responsive 6 → 3 → 2). Three accented:
  - `amount-card--total` (After discount) — teal.
  - `amount-card--paid` — ok green text.
  - `amount-card--debt` — sand-pink bg + danger text.
- **Employees table:** same `lk-table` grammar, with `emp-avatar` initials + `duty` colored dot prefix per role (sales=teal, prod=sand, install=ok).

---

## 6. MudBlazor mapping

- **`MudAppBar Dense="true"`** for topbar — but layout via custom CSS classes (`.topbar`, `.brand`, `.portal-search`, `.env-badge`, `.user-block`). Don't rely on Mud's default appbar styling.
- **`MudPaper Outlined="true" Elevation="0"`** + class `panel`. Override Mud's default radius to `var(--r-md)` globally.
- **`MudDataGrid Dense="true" FixedHeader="true" ServerData="@…"`** for orders list. Wrap in class `lk-grid` and override:
  - `.mud-table-head` → `--n-50` background, mono caps
  - `.mud-table-cell` → padding `6px 12px`, font-size `12.5px`
  - `.mud-table-row.is-selected` → teal-50 + inset teal border
- **Don't** use MudBlazor `Color.Primary` etc. on chips — render a custom `<span class="chip chip--*">` template. The built-in palette won't match.
- **MudToolBar** for `ToolBarContent` slot — replace internals with the custom `field` / `seg` markup.
- **Pagination:** keep MudDataGrid's pager but skin `.mud-table-pagination` to match `.pagination` (teal active, neutral outline).
- **Order details:** plain Razor components inside a `MudPaper` — `<OrderDetailsHeader/>`, `<OrderItemsTable/>`, `<OrderAmountsGrid/>`, `<OrderEmployeesTable/>`. Don't use MudDataGrid here — these are display-only and benefit from semantic HTML tables.
- **Selected row → details:** state is server-side; selection updates a `SelectedOrderId` parameter that drives the details panel.

---

## 7. Search & filtering (server-side)

- Single search box hits **server-side** across: number, customer name, customer code, address, **product text** (the legacy "hidden searchable product" field).
- Status / Store / Date / HasDebt are independent filters AND'd with search.
- Debounce ~200ms. Show inline loading on the table (`MudDataGrid` built-in is fine if skinned).
- Empty state inside the table area: neutral icon + "No orders match these filters" + "Clear filters" link button. Don't push it as a giant illustration.

---

## 8. Mobile (≤760px)

- Topbar wraps; env badge hidden; search becomes full-width row 3.
- Table hidden; `mobile-cards` shown.
- Each card: mono order id (top-left), status chip (top-right), customer name, then meta row with date, store, debt (debt bolded, red if overdue).
- Details sections stack vertically; Amounts grid → 2 columns.
- Title actions wrap; primary stays last (Open in PoS).

---

## 9. Things missing from the original static page that must exist

1. Server-side search across hidden product text.
2. Filter strip (Status / Store / Date / HasDebt + future flags).
3. Sticky table head.
4. Selected-row state synced with details panel.
5. Debt color tiering (zero / pending / overdue).
6. Customer Flags column (extensible).
7. Operator + timestamp on details title.
8. Loading / empty / error states for the grid.
9. Keyboard: `/` focuses search, `↑/↓` moves selection, `Enter` opens, `Esc` clears.
10. Right-click row → copy id / open in new tab.
11. Bulk select for future bulk actions (export, mark, etc.) — checkbox column already there.

---

## 10. Don't

- Don't use sand on `<th>` or as default panel background.
- Don't use bright blue / indigo / cyan / orange chip palettes.
- Don't add a second line under customer name. Use Flags column.
- Don't introduce a density toggle. Compact only.
- Don't render money/dates in proportional fonts.
- Don't put icons in the order link — keep it text + `↗`.
- Don't use MudBlazor's default chip/button colors. Always go through token classes.

---

## 11. Follow-up decisions from design review chat

These decisions supersede earlier open questions in this file.

### Density

`Density` meant a visual row-density mode selector. We are **not** adding a density toggle.

The orders list must be compact by default because operators need maximum visible rows. Target:

- fixed compact mode only;
- row height around `30px`;
- table cell padding around `6px 12px`;
- no large multiline row layout in the desktop table.

### Customer column

Do **not** use a two-line customer cell by default. The second line may be useful in theory, but it costs vertical space on every row. For this product surface, row count is more valuable.

The customer cell is a single line:

- customer name only;
- ellipsis on overflow;
- no customer code/legal metadata/subline in the main desktop row.

If we later need customer hints, use the narrow Flags column instead of adding row height.

### Flags column

Add a narrow `Flags` column immediately after `Customer`.

Default state is empty. Render flags only when the backend says something is present.

Current approved flag types:

| Flag | Class | Meaning |
|---|---|---|
| `€` | `cust__flag--debt` | Customer/order has debt |
| `★` | `cust__flag--vip` | VIP / important customer |
| `!` | `cust__flag--note` | Internal note exists |

Implementation guidance:

- column width around `24px`;
- each flag is a single compact glyph, not text;
- glyph box around `14x14px`;
- backend should return flags as data, e.g. `HasDebt`, `IsVip`, `HasNote`, or a future enum/list;
- Razor template just renders classes from that data;
- future flags can be added as `cust__flag--blocked`, `cust__flag--prepaid`, `cust__flag--branch`, etc.

### Static preview status

`docs/ux/orders-static-preview.html` has been updated to reflect these decisions:

- no density toggle;
- compact fixed desktop rows;
- single-line customer cells;
- dedicated flags column;
- selected-row teal inset;
- neutral table header;
- sand only on group rows and warning/in-progress accents;
- status chips use the unified dot grammar;
- details remain below the list.

The original designer-exported bundled file is preserved as raw reference:

`docs/ux/reference/sales-orders-redesign.html`
