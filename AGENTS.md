# LKvitai.MES Agent Instructions

These instructions are mandatory for AI agents working in this repository.

## UX / Design Source of Truth

For any Portal, Sales Orders, Fabric Availability, Warehouse WebUI, Blazor, MudBlazor, CSS, layout, table, chip, button, filter, or mobile UI work, use this handoff as the primary design source:

- `docs/ux/lkvitai-mes-ux-handoff.html`

Supporting source artifacts:

- `src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/wwwroot/index.html`
- `src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/wwwroot/styles.css`
- `docs/ux/orders-static-preview.html`
- `docs/ux/sales-orders-codex-rules.md`
- `docs/ux/fabric-availability-static-preview.html`
- `docs/ux/fabric-availability-codex-rules.md`

Do not invent a new visual language. The approved baseline is:

- dark Portal topbar/header;
- LKvitai.MES brand block;
- neutral grey application surfaces;
- teal active, focus, link, and primary states;
- warm sand only for operational warning/scaffold accents;
- compact enterprise/product UI;
- dense operational tables;
- no marketing layout, hero marketing copy, decorative gradients, random bright colors, or oversized cards.

## Token Rules

Use the token families documented in `docs/ux/lkvitai-mes-ux-handoff.html`:

- neutrals: `--n-0` through `--n-900`;
- teal accent: `--accent-50` through `--accent-700`;
- sand: `--sand-25` through `--sand-700`;
- functional chip/status families: `ok`, `warn`, `info`, `slate`, `danger`, `done`;
- fonts: UI = `Inter` / system, mono = `JetBrains Mono` / `SF Mono` / `Consolas`.

Do not add new color families unless the user explicitly approves a design-system change. If a new state is needed, map it to the existing functional families.

## Blazor / MudBlazor Rules

Blazor and MudBlazor are allowed, but MudBlazor defaults are not the visual source of truth.

- Use `MudPaper Outlined="true" Elevation="0"` for panels, then skin it with local classes such as `.panel`.
- Use `MudDataGrid Dense="true" FixedHeader="true" ServerData="@..."` for dense server-side operational tables.
- Use `MudAppBar Dense="true"` only as a structural shell; apply custom `.topbar`, `.brand`, `.env-badge`, `.user-block` styling.
- Use `MudTextField`, `MudSelect`, `MudCheckBox`, `MudButton`, and `MudToggleGroup` only when they are skinned to match the handoff classes.
- Do not use MudBlazor default chip colors, `Color.Primary`, default `MudAlert`, or default table styling as final UI.
- Render status chips as custom markup: `<span class="chip chip--...">...</span>`.
- Keep money, dates, dimensions, fabric codes, and order ids in mono/tabular styling.

## Sales Orders Specifics

- Desktop table is compact only; do not add a density toggle.
- Target row height is about 30px.
- Customer cell is single-line only.
- Flags column comes immediately after Customer and uses compact glyphs: `€`, `★`, `!`.
- Header background is neutral, never sand.
- Selected row uses teal-50 background plus a 3px teal inset.
- Order links are mono teal text with `↗`.
- Details panel stays below the list and includes Items, Amounts, and Employees.

## Fabric Availability Specifics

- Mobile search uses dark FA header, code input, Check button, empty state, and interactive recent checks.
- Mobile result includes fabric image, campaign ribbon, width chips with quantities, status block, incoming meters, notes, disabled reserve-soon state, and alternatives scroll.
- Desktop is a full-width low-stock list, not a single-fabric lookup.
- Desktop filters include search, highlighted threshold selector, status, width, and supplier.
- Desktop table includes inline available-meter progress bars and actions: Open, Reserve, Notify, Replace where relevant.

## Implementation Discipline

- Prefer existing repo patterns over new abstractions.
- Keep UI compact and operational.
- Add annotations/docs when changing UX behavior or data contracts.
- Do not commit unless explicitly asked.
