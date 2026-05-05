# LKvitai.MES Shell — Binding visual system (all modules)

This document is the binding shell + visual system for every LKvitai.MES module. Every module-specific rules file (Sales Orders, Warehouse, Production, Admin, …) extends this and only adds module-specific content (IA, columns, status mapping, module-specific don'ts). If a module rule contradicts this document, this document wins.

---

## 1. Design tokens

Single source of truth lives in `_content/LKvitai.MES.BuildingBlocks.WebUI/css/portal-tokens.css`. Do not copy the token block into module CSS — reference it.

```css
:root {
  /* Neutrals — cool graphite */
  --n-0:#fff; --n-25:#fbfcfd; --n-50:#f5f6f8; --n-100:#eef1f4;
  --n-150:#e2e7ec; --n-200:#d5dce4; --n-300:#b9c3ce; --n-400:#8996a4;
  --n-500:#66717f; --n-600:#4e5966; --n-700:#323b45; --n-800:#1f2632; --n-900:#151922;

  /* Teal — primary accent (active, links, focus, primary buttons) */
  --accent-50:#eaf8f7; --accent-100:#d1eeec; --accent-300:#7cc4c1;
  --accent-400:#56aaa7; --accent-500:#2f8f8b; --accent-600:#257773; --accent-700:#1d5d5a;

  /* Sand — warn/operational only: status chips, group rows, scaffold accents.
     NEVER use for nav, panel headers, or active states. */
  --sand-25:#faf6ee; --sand-50:#f7f1e5; --sand-100:#ede2c8;
  --sand-150:#e4dbc5; --sand-300:#daceb7; --sand-500:#c79f63; --sand-600:#b08a4a; --sand-700:#7c5f2a;

  /* Functional chip families — single grammar for all status chips */
  --ok-fg:#19744a;     --ok-bg:#e6f4ec;     --ok-bd:#bfe0cd;     --ok-dot:#1f9d61;
  --warn-fg:#7c5f2a;   --warn-bg:#f7eed5;   --warn-bd:#e2cf9a;   --warn-dot:#b08a4a;
  --info-fg:#1d5d5a;   --info-bg:#e3f2f1;   --info-bd:#b4dad7;   --info-dot:#2f8f8b;
  --slate-fg:#3a4554;  --slate-bg:#eaeef3;  --slate-bd:#cdd5de;  --slate-dot:#66717f;
  --danger-fg:#8a1f12; --danger-bg:#fbe7e3; --danger-bd:#f0c2b8; --danger-dot:#c0392b;
  --done-fg:#2a4a3a;   --done-bg:#e3eee7;   --done-bd:#bdd2c4;   --done-dot:#4a7a5d;

  /* Dark topbar surfaces */
  --dark:#20242c; --dark-2:#171b22; --dark-line:#3a404c;

  /* Typography */
  --font-ui:'Inter','Segoe UI',system-ui,sans-serif;
  --font-mono:'JetBrains Mono','SF Mono',Consolas,monospace;

  /* Radii */
  --r-xs:3px; --r-sm:4px; --r-md:6px; --r-lg:8px;
}
```

**Hard rules:**
- Default surfaces are neutral grey. Sand is **only** for warn/inprogress chips and group-rows.
- Teal = primary, links, active selection, focus rings, primary buttons.
- All money/dates/codes/quantities use `font-mono` + `font-variant-numeric: tabular-nums`.

---

## 2. Test strip

Shown when `env != production`. Full-bleed. Repeating diagonal sand-stripe pattern.

```
| ⚠ TEST ENVIRONMENT · Mock data · mes-test.lauresta.com           |
```

- Background: repeating-linear-gradient(-45deg, #ead19b … #dfc07a).
- Text: `--n-900`, 12px, weight 600.
- Note span: weight 400, opacity 0.82.
- Right: mono env URL in 11px.

---

## 3. Topbar grammar

Full-bleed. Dark surface (`--dark`). Always visible.

```
[brand-mark]  [brand-name / tagline]  [▣ ModuleName]  │  [search … ⌘K]  ········  [ENV badge]  [user block]
```

- **Brand mark** — 36×36px `--dark-2` bg, teal `LK` monogram.
- **Brand name** — 15px weight 600, `.MES` in `#9ba4b0` weight 400.
- **Module pill** (`.module-pill`) — `--dark-2` bg, `--accent-300` mono uppercase text, 11px, border `--dark-line`. Clicking opens a **popover module switcher** (Sales / Warehouse / Production / Admin) — not a dropdown of sub-pages.
- **Separator** — 1px `--dark-line`, 28px tall.
- **Global search** (`.portal-search`) — `#1b2028` bg, placeholder "search SKU/location/document…", `⌘K` kbd badge. Opens with `⌘K`, closes with `Esc`, `↑↓` navigate, `Enter` open.
- **Env badge** — two-cell block: left cell `--dark-2` bg with coloured env label (`#dfc07a` for TEST), right cell with mono version string.
- **User block** — avatar initials circle + name + sign-out link-button.

**Mobile wrap:** Under 980px: env badge hides; search becomes full-width row 3; module pill collapses to icon only.

---

## 4. Shell layout & width rules

```
<test-strip>       full-bleed (100% viewport)
<topbar>           full-bleed (100% viewport)
<shell-body>       row: sidebar + main, fills viewport height under topbar
  <sidebar>        240px expanded / 52px collapsed — fixed width, never flex
  <main>
    <main__inner>  max-width: var(--content-max, 1600px);
                   padding: clamp(20px, 2.4vw, 36px);
                   margin: 0 auto;
```

**Binding rules — apply identically across Sales, Warehouse, Production, Admin, Reports:**

- `test-strip`, `topbar`, `shell-body` are **full-bleed** (width: 100%; no max-width).
- `sidebar` is **fixed width**: 240px expanded, 52px collapsed. Never grows or shrinks with content.
- `main__inner` is **content-capped**: `max-width: var(--content-max, 1600px)`, padding `clamp(20px, 2.4vw, 36px)`. Centered with `margin: 0 auto`.
- **Tables, KPI strips, and panels live inside `main__inner`.** They never escape the cap — operators on 4K monitors must not get lost in wide blank space.
- No per-module variation of these values.

---

## 5. Sidebar grammar

- **Width:** 240px expanded / 52px collapsed icon-only rail.
- **Collapse toggle:** in `sidebar__head`. Click-only — **do not auto-expand on hover**.
- **Persistence:** `localStorage` key `lkvitai.<module>.sidebar.collapsed`.
- **Accordion:** one section open at a time. Section auto-expands when its child is the active route. Click section header to open/close.
- **Active item:** `--accent-50` background + `--accent-700` text + `inset 2px 0 0 var(--accent-500)` left shadow.
- **Section header:** 10px, weight 700, `--n-500`, mono-uppercase. Has-active state: `--accent-700` text.
- **Item font:** 12.5px, `--n-600`. Hover: `--n-50` bg, `--n-900` text.
- **Count chip** (`.nav-link__count`): mono 10px, `--n-100` bg, `--n-500` text. `.is-warn` modifier for actionable counts (sand bg, warn-fg).
- **Divider** (1px `--n-150`) separates operational sections from secondary sections (Reports / Analytics).
- **Admin** module shell is reached from the topbar module switcher — **not** nested in any module's sidebar.
- **Mobile (<980px):** sidebar hides off-canvas. Topbar gets a hamburger that slides it in over a scrim.
- **ARIA:** `<nav aria-label="[Module] navigation">`, section toggles use `aria-expanded`, `aria-controls`.

---

## 6. Chip grammar

One grammar for all status chips across all modules:

```css
.chip {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 3px 9px; font-family: var(--font-mono); font-size: 10.5px;
  font-weight: 700; letter-spacing: .4px; text-transform: uppercase;
  line-height: 1; border-radius: 999px; border: 1px solid transparent; white-space: nowrap;
}
.chip::before {
  content: ""; width: 6px; height: 6px; border-radius: 50%;
  background: currentColor; flex-shrink: 0;
}
```

Six functional families (exhaustive — no new families without design sign-off):

| Family | Class suffix | Trigger |
|---|---|---|
| ok | `chip--ok` | Healthy / complete |
| warn | `chip--low`, `chip--inprogress` | Needs attention / in progress |
| info | `chip--approved`, `chip--reserved` | Informational / teal-dim |
| slate | `chip--entered`, `chip--locked` | Neutral / pending |
| danger | `chip--out`, `chip--paused` | Error / stopped |
| done | `chip--delivered` | Finished / archived |

Sand only for warn/inprogress families. No bright/random hues. Cancelled: `chip--cancelled` neutral with line-through.

---

## 7. Table grammar

- **Compact only.** No density toggle. Row height ~34px, `td` padding `6px 12px`.
- **Sticky `<thead>`:** `position: sticky; top: 0; z-index: 1`. Header bg `--n-50`, border-bottom `--n-200`. Header text: 10.5px, weight 700, uppercase, `--n-600`. **Never sand background on `<th>`.**
- **Row hover:** `--n-25` background.
- **Selected row:** `--accent-50` background + `inset 3px 0 0 var(--accent-500)` left shadow on first cell. Class `is-selected`.
- **Numbers / codes / dates / money:** `font-mono` + `tabular-nums`, right-aligned (class `is-num`). `<td>` never shrinks to fewer than the value width.
- **No second line in cells.** Use a dedicated Flags column for inline indicators.
- **`<table>` gets `<caption class="sr-only">`** with a descriptive label for screen readers.

---

## 8. Toolbar grammar

```
[🔍 Search …  /]  [Status ▾]  [Location ▾]  [☐ Has reservations]    ········  [1–50 of 1284 ◄ ► ]
————————————————————————————————————————————————————
[Status: Low ×]  [Location: A ×]          ← active-filter chip strip
```

- Search field (`.field--search`): `/` keyboard hint badge. Debounce ~200ms. Hits server-side.
- Selects: label-inside pattern (`.field__lbl` above the value). Neutral border, teal focus ring.
- Checkbox toggles: inline, `--accent-500` checked color.
- Top pagination: right-aligned.
- Active-filter chips strip: under toolbar when any filter is active. Each chip has `×` dismiss. Never hide active filters inside collapsed selects.
- Empty state inside the table area: neutral icon + one-sentence message + "Clear filters" link. **No giant illustrations.**

---

## 9. KPI strip grammar

```
┌──────────────┬──────────────┬──────────────┬──────────────┐
│ Total SKUs   │ Low stock    │ Hard-locked  │ Locations    │
│ 1 284        │ 47           │ 12           │ 892          │
│ ↑ 14 vs last │ ↑ bad (down) │              │              │
└──────────────┴──────────────┴──────────────┴──────────────┘
```

- Equal columns. Border-left tick in the metric's semantic color.
- Label: 10.5px, uppercase, `--n-500`.
- Value: mono, 22px, weight 600, tabular-nums.
- Delta line: `up` class = `--ok-fg`, `down` class = `--danger-fg`. **Semantic direction** — for "Low stock" an increase is bad, so it gets `down` color even though the number went up.
- KPIs do not tick during auto-refresh. Deltas are computed at page load.

---

## 10. Pagination

- Active page: `--accent-600` fill, white text.
- Inactive: neutral outline, `--n-700` text, hover `--n-50` bg.
- Disabled: `--n-300`, no hover.

---

## 11. Empty / loading / error states

Inside the table area, not pushed to a full-page:

- **Empty:** neutral icon (24px, `--n-400`) + one sentence ("No SKUs match these filters") + single "Clear filters" link-button (`--accent-700`).
- **Loading:** table rows replaced by 3–5 skeleton rows (bg `--n-100`, animated shimmer). No spinner overlay.
- **Error:** danger icon + one sentence + "Retry" link. Color `--danger-fg`.

No giant illustrations. No marketing-style empty states.

---

## 12. Keyboard shortcuts

| Key | Action |
|---|---|
| `/` | Focus search field |
| `↑` / `↓` | Move row selection |
| `Enter` | Open selected row / item |
| `Esc` | Close detail panel / clear search |
| `[` / `]` | Collapse / expand sidebar |
| `⌘K` | Open global search |

---

## 13. Mobile breakpoints

| Breakpoint | Change |
|---|---|
| < 980px | Sidebar hides off-canvas; topbar gets hamburger |
| < 760px | Table → mobile-cards; toolbar fields wrap full-width |
| KPI strip | 4 columns → 2 columns at < 980px |

---

## 14. MudBlazor mapping (cross-module overrides)

- **`MudAppBar`** — use only as structural shell; apply `.topbar`, `.brand`, `.portal-search`, `.env-badge`, `.user-block` custom CSS. Never use Mud's default appbar colour.
- **`MudPaper`** — `Outlined="true" Elevation="0"` + class `panel`. Override radius to `var(--r-md)` globally.
- **`MudDataGrid`** — `Dense="true" FixedHeader="true" ServerData="@…"`. Wrap in class `lk-grid` and override thead/cell/selected-row per §7. Never use `Color.Primary` for selected rows.
- **`MudDatePicker` triggers** — skin to `.field` class (label-inside, neutral border, teal focus ring).
- **`MudNavMenu`** — prefer custom `<nav class="sidebar">` with plain `<a>` + `<button>` and route-bound active state. If MudNavMenu is mandated, override `.mud-nav-link.mud-nav-link-active` to match §5 exactly and disable Mud's ripple.
- **Chips** — render as custom `<span class="chip chip--*">`. Never use MudBlazor's built-in chip colours.
- **Bootstrap** — **do not use** in new pages. No `container`, no `row/col`, no `btn-warning`. Existing pages using Bootstrap buttons must have the Bootstrap button colours overridden to teal/neutral via site.css.

---

## 15. Global don'ts

- **No Bootstrap** in new pages or components.
- **No yellow/orange active states.** Active = teal (`--accent-500`). Yellow does not exist in this system.
- **No density toggle.** Compact only.
- **No proportional fonts for numbers, codes, dates, or money.** Always mono + tabular-nums.
- **No MudBlazor default palette colors** (`Color.Primary`, `Color.Secondary`, etc.) on chips, buttons, or selected rows. Always go through token classes.
- **No second line in table cells.** Use a Flags column for inline indicators.
- **No auto-expand sidebar on hover.** Click-only.
- **No full-bleed content panels.** Tables and panels always live inside `main__inner` (capped at 1600px).
- **No giant illustrations** for empty/loading/error states.
- **No per-module variation** of the shell layout width rules.
