# Warehouse — Codex implementation rules (LKvitai.MES)

> **Extends: `lkvitai-shell-rules.md` — read it first. This file only adds Warehouse-specific rules.**

Reference design: `docs/ux/warehouse-shell-redesign.html`.

---

## 1. Information architecture

```
Dashboard                        (single link, no section)
─── Operational ──────────────────────────────────────────
Stock                            ← section, expandable
  Available stock                ← default landing page
  Low stock           (47)       ← count chip is-warn when > 0
  Search by image
  Stock dashboard
  Location balance
  Adjustments
  Reservations
Inbound                          ← section
  Receivings
  Returns from customer
  Cross-dock
  Quality hold
Outbound                         ← section
  Picking
  Packing
  Shipments
  Returns to supplier
Operations                       ← section
  Transfers
  Counts / cycle counts
  Re-bin
  Print labels
Finance                          ← section
  Stock value
  Write-offs
  Cost adjustments
─── divider (1px --n-150) ────────────────────────────────
Reports                          ← section
Analytics                        ← section
Admin                            ← section (warehouse-admin role only:
                                    locations, ABC, settings, users)
```

**Admin note:** the system-wide Admin module (users, roles, integrations, audit) is **its own module shell** reached from the topbar module switcher — not nested in the Warehouse sidebar. The `Admin` section visible in the Warehouse sidebar covers only warehouse-specific configuration (slot types, location rules, cycle-count schedules, etc.).

---

## 2. Available Stock page — spec

**Page head:**

- Breadcrumb: `Warehouse › Stock › Available stock`. Last segment in `--n-700`, no link.
- Eyebrow mono: `Warehouse · Stock`.
- `<h1>` = "Available stock".
- Sub: factual line — `On-hand minus hard-locked quantities · auto-refresh 5s · 1 284 SKUs across 892 locations`. No marketing copy.
- Page actions (right): `Export CSV` (outline) + `+ New adjustment` (primary).

**KPI strip (`.kpis`) — 4 KPIs:**

| KPI | Class modifier | Notes |
|---|---|---|
| Total SKUs | — | neutral mono value |
| Low stock | `kpi--low` | warn color on value; increase = bad → use `down` delta color |
| Hard-locked | `kpi--info` | slate/info color |
| Locations used | — | neutral |

Deltas computed at page load, not updated during auto-refresh.

**Toolbar:**

- Search: `Search SKU, description…` with `/` kbd hint.
- Selects: `Status` (All / OK / Low / Out / Locked / Reserved), `Location` (warehouse / zone), `Group` (Fabric / Hardware / Accessories / …).
- Checkbox toggle: `Has reservations`.
- Top pagination right-aligned.
- Active-filter chip strip under toolbar per `lkvitai-shell-rules §8`.

**Table columns (in order):**

`select | SKU | Description | Location | On hand | Reserved | Available | Status | row-action`

- **SKU** — mono link `--accent-700` with `↗`.
- **Description** — single line, ellipsis. **No second line.** Use a Flags column if lot-tracked / serial / hazardous need to surface.
- **Location** — mono, format `A-02-03`. Multi-location: show primary + `(+3)` chip linking to Location balance.
- **On hand / Reserved** — mono, tabular, right-aligned (`is-num`).
- **Available** — mono, tabular, right-aligned, **bold**.
- **Status** — chip per §3.
- **Row action** — `↗ Open` opens detail; right-click for context menu.

---

## 3. Stock status chips

| Status | Class | Family | Trigger |
|---|---|---|---|
| OK | `chip--ok` | ok (green) | available ≥ reorder point |
| Low | `chip--low` | warn (sand) | 0 < available < reorder point |
| Out | `chip--out` | danger | available ≤ 0 |
| Locked | `chip--locked` | slate | hard-locked, on-hand > 0, available 0 |
| Reserved | `chip--reserved` | info (teal-dim) | majority of on-hand reserved against open orders |

**Tie-break rule:** if a SKU is in two states simultaneously (e.g. low **and** mostly reserved), show the more actionable chip and let the operator see the raw numbers in the columns. Priority order: Out > Locked > Low > Reserved > OK.

---

## 4. Auto-refresh & deltas

- **5-second server poll** of the visible table. Not WebSocket — keeps it simple and cache-friendly.
- **Cell flash** on change: `--ok-bg` background if Available went up, `--warn-bg` if down, for ~1.2s then fade. **No persistent highlight.**
- **KPI deltas** (`↑ 12 vs last week`) computed server-side at page load; they do **not** update during the session auto-refresh.
- **Pause toggle** (`⏸ Pause refresh`) in `panel__head` for mid-edit scenarios.
- **Timestamp** mono "Updated HH:MM:SS" stamp in `panel__head` next to row count.

---

## 5. Search scope

Single search box queries server-side across: SKU, description, location code, **alternate codes** (supplier SKU, EAN, internal alternate codes). Debounce ~200ms. Status / Location / Group / HasReservations AND'd with search.

---

## 6. MudBlazor mapping (Warehouse-specific)

- **Topbar** — custom CSS per `lkvitai-shell-rules §3`. Module pill shows "Warehouse". Not `MudAppBar` defaults.
- **Sidebar** — prefer custom `<nav class="sidebar">` over `MudNavMenu`. If `MudNavMenu` is used, override `.mud-nav-link.mud-nav-link-active` to match `lkvitai-shell-rules §5` exactly and disable Mud ripple.
- **`MudDataGrid`** for stock list — class `lk-grid`. `ServerData="@…"`. Auto-refresh via `System.Threading.Timer` that re-queries server-side every 5s. Show "Updated HH:MM:SS" mono stamp in `panel__head`.
- **`MudDatePicker` / `MudDateRangePicker`** — skin trigger to `.field` class.

---

## 7. Things missing from the legacy Bootstrap shell that must exist

1. Persistent sidebar collapse (icon-only 52px rail). ← GH issue #142
2. Module switcher in topbar (popover, not dropdown of pages). ← GH issue #143
3. Sticky table head per `lkvitai-shell-rules §7`.
4. Active-route auto-expand of matching sidebar section. ← already implemented in NavMenu.razor
5. Server-side combined search + filter including alternate SKU codes.
6. Filter chip strip under toolbar.
7. 5-second auto-refresh with cell-flash deltas + pause control.
8. Empty / loading / error states inside table area per `lkvitai-shell-rules §11`.
9. Keyboard shortcuts per `lkvitai-shell-rules §12`.
10. Right-click row → copy SKU / open in new tab / open in Adjustments.
11. ARIA on sidebar nav. ← GH issue #145

---

## 8. Warehouse-specific don'ts

- **Don't bring back yellow/orange active states.** Active = teal. Yellow does not exist in this system. (Legacy Bootstrap shell used `btn-warning` yellow for active nav — explicitly removed.)
- **Don't use Bootstrap classes** anywhere in the Warehouse module. No `container`, no `row/col`, no `btn-warning`. (GH issues #140–#146 track the remaining Bootstrap cleanup.)
- **Don't put Admin links** for the system-wide Admin module in the Warehouse sidebar — Admin has its own module shell.
- **Don't auto-expand sidebar sections on hover.** Click only.
- **Don't render quantities or location codes in proportional fonts.** Always `--font-mono` + tabular-nums.
- **Don't push stock tables full-bleed** on wide screens. Content stays capped at `--content-max` per `lkvitai-shell-rules §4`.
- **Don't make cell-flash persistent** — 1.2s animation then fade, no lingering background.
- **Don't ship a density toggle.** Compact only.
