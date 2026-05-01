# Fabric Availability — Codex implementation rules (LKvitai.MES)

Reference design:

- `docs/ux/fabric-availability-static-preview.html`
- raw designer reference: `docs/ux/reference/fabric-availability-redesign.html`

Continuation of approved Portal/Login visual language. Do not invent a new style from scratch. Use the same token grammar as Portal, Sales Orders, and Login.

---

## 1. Design Direction

The Fabric Availability UI has two product surfaces:

1. **Mobile lookup** for frontline/sales floor:
   - search by fabric code;
   - show one fabric result;
   - switch width;
   - show status, ETA, incoming meters, alternatives.

2. **Desktop low-stock list** for purchasing/ops:
   - show all fabrics where available stock is below threshold X meters;
   - filter by threshold, status, width, supplier;
   - dense table with stock progress, ETA, incoming meters, alternatives, last checked, actions.

The UI must feel like the approved Portal/Login design:

- dark topbar/header;
- neutral grey surfaces;
- teal primary/active/focus;
- warm sand for low/warn operational states;
- compact enterprise/product UI;
- no marketing-style layout.

---

## 2. Tokens

Use the same token contract as Sales Orders:

- neutrals `--n-0…900`;
- teal `--accent-50…700`;
- sand `--sand-25…700`;
- functional families:
  - `--ok-{fg|bg|bd|dot}`;
  - `--warn-{fg|bg|bd|dot}`;
  - `--info-{fg|bg|bd|dot}`;
  - `--slate-{fg|bg|bd|dot}`;
  - `--danger-{fg|bg|bd|dot}`;
  - `--done-{fg|bg|bd|dot}`;
- `--dark`, `--dark-2`, `--dark-line`;
- `--font-ui`, `--font-mono`.

Hard rules:

- default surfaces are neutral grey;
- teal = active selection, primary buttons, focus rings, important links;
- sand = low/warn operational states and notes/quotes, not default headers;
- numeric values, fabric codes, widths, dates, meters use mono/tabular styling where useful.

---

## 3. Mobile Search

Structure:

- dark header using Portal DNA;
- brand mark `FA`;
- search bar with mono code input;
- primary button text: `Check`;
- empty state with teal-50 illustration;
- recent checks below.

Recent checks:

- must be interactive;
- tap restores the previous search;
- each row shows code, width, and status chip;
- use compact rows, not large cards.

---

## 4. Mobile Result

Structure:

- search bar remains at top;
- fabric hero/photo;
- campaign ribbon:
  - mono text like `-15% · CAMPAIGN`;
  - dark/neutral treatment, not bright yellow;
- fabric code/name;
- width selector;
- status block;
- notes;
- reserve row;
- alternatives horizontal scroll.

Width selector:

- segmented chips;
- active width uses teal active state;
- each chip shows quantity/meters per width;
- quantity pill uses semantic color:
  - ok = enough;
  - low = sand/warn;
  - out = danger.

Status block:

- this is the main signal on mobile;
- use a custom block, not `MudAlert`;
- class grammar:
  - `status-block status-block--ok`;
  - `status-block status-block--low`;
  - `status-block status-block--out`;
  - `status-block status-block--disc`;
- show big quantity, ETA, and incoming chip when available.

Incoming meters:

- mobile status subline must show incoming stock when known;
- example: `+ 60 m incoming`;
- use `chip chip--incoming`.

Notes:

- sand left-border quote treatment;
- compact italic text.

Reserve:

- disabled until implemented;
- show `SOON` pill.

Alternatives:

- horizontal scroll;
- compact cards with photo, code, status/qty chip, ETA;
- alternative links should be actionable in real implementation.

---

## 5. Desktop Low-Stock List

This is a new workflow and should be implemented as a dense operational list.

Metrics strip:

- 4 columns with no gaps;
- use border-left separators;
- metrics:
  - Below threshold;
  - Out of stock;
  - Expected this week;
  - Discontinued;
- values use semantic colors.

Toolbar:

- search;
- threshold selector;
- status selector;
- width selector;
- supplier selector;
- refresh action.

Threshold selector:

- visually highlighted as the key filter;
- use teal-50 background / teal label;
- do not render it as just another equal dropdown.

Width selector:

- required because users need to filter low stock for a specific width, e.g. `2000 mm`.

Supplier selector:

- required for purchasing workflows.

Desktop table columns:

1. Photo
2. Code
3. Name
4. Width
5. Available
6. Status
7. ETA
8. Incoming
9. Alternatives
10. Last checked
11. Actions

Available column:

- show number of meters;
- include inline progress bar relative to the current threshold;
- color progress semantically:
  - ok green;
  - low sand;
  - out danger;
  - discontinued neutral.

Incoming column:

- required;
- operator must see "60 m coming in 4 days" without opening details;
- table can show meters, ETA remains in ETA column.

Last checked:

- required;
- show who/when last checked if backend supports it;
- if not yet available, preserve the column and fill with timestamp only.

Actions:

- `Open` always available;
- `Reserve` for low stock where reservation is possible;
- `Notify` for out-of-stock fabrics;
- `Replace` for discontinued fabrics.

---

## 6. Status Chips

Use the same chip grammar as Sales Orders:

- mono caps;
- dot prefix via `::before`;
- low chroma;
- token-based semantic families.

Mapping:

| Status | Class | Family |
|---|---|---|
| Enough / OK | `chip--ok` | green |
| Low | `chip--low` | warn / sand |
| Out of stock | `chip--out` | danger |
| Discontinued | `chip--disc` | neutral |
| Incoming | `chip--incoming` | info / teal |

Do not use MudBlazor default chip colors.

---

## 7. MudBlazor Mapping

General:

- `MudPaper Outlined="true" Elevation="0"` for `.preview-panel`, `.fabric-card`, desktop panels and metrics containers;
- override Mud defaults with local token classes;
- keep custom CSS class grammar as the source of truth.

Mobile:

- search input can be `MudTextField`, skinned as `.mobile-search__input`;
- primary button can be `MudButton`, but CSS must map it to `btn-primary`;
- width chips can be `MudToggleGroup` or plain Razor buttons with custom classes;
- do not rely on Mud built-in colors;
- status block must be plain markup: `<div class="status-block status-block--low">…</div>`, not `MudAlert`.

Desktop:

- `MudDataGrid Dense="true" FixedHeader="true" ServerData="@..."` for low-stock table;
- override `.mud-table-head`, `.mud-table-cell`, pagination and hover states to match tokens;
- render status chips with custom `<span class="chip chip--...">`;
- render available meters with custom progress markup inside a template column;
- server-side filtering/sorting/paging.

---

## 8. Backend / Data Contract Notes

Mobile lookup needs:

- fabric code;
- name;
- photo URL;
- discount/campaign info;
- notes;
- widths with stock status and meters;
- selected width;
- ETA;
- incoming meters/date;
- alternatives with width/status/meters/ETA.

Desktop low-stock list needs:

- code;
- name;
- thumbnail/photo;
- width;
- available meters;
- threshold used for progress calculation;
- status;
- ETA;
- incoming meters;
- supplier;
- alternatives;
- last checked timestamp/user if available;
- action availability flags: can reserve, can notify, can replace.

Search/filter should be server-side:

- search by code/name/supplier/alternative code;
- threshold;
- status;
- width;
- supplier;
- paging/sorting.

---

## 9. Things Missing From Original Preview That Must Exist

1. Incoming meters column/chip.
2. Threshold selector visually highlighted as the key filter.
3. Width selector in filters.
4. Supplier filter/column support for purchasing.
5. Last checked column.
6. `Notify` action for out-of-stock fabrics.
7. `Replace` action for discontinued fabrics.
8. Interactive recent checks on mobile.
9. Width chips with quantity/meters per width.
10. Inline progress bar in Available column.

---

## 10. Don't

- Do not invent a new visual language.
- Do not use bright yellow campaign badges.
- Do not use `MudAlert` for the mobile status block.
- Do not use built-in MudBlazor chip colors.
- Do not make the desktop screen a single-fabric lookup only; it must be a low-stock list.
- Do not hide width/supplier filtering on desktop.
- Do not remove Incoming or Last checked columns from the production table.
