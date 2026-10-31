# Sales product entry, milestone 1 design behavior

The primary Portal and Sales visual source remains
[`lkvitai-mes-ux-handoff.html`](lkvitai-mes-ux-handoff.html). The 2026-10-31
product picker/editor review adds the compact operator layout below.

- The picker shows all 26 categories from the embedded legacy reference snapshot.
  Each category uses its own supplied SVG symbol. Search filters categories and
  types; the category area scrolls so the type list stays visible.
- Selecting a type opens the full-width product editor. Its rail, editable
  parameters, price/stock panel and scaled SVG preview share the screen. The
  preview changes with width and height and uses neutral fabric fill until a
  real colour hex is available.
- Width and height use 10 mm step buttons and a 200 mm minimum. Quantity uses
  step buttons, while service/accessory quantities retain fractional input.
  PPP fields with supported arithmetic formulas show calculated previews and
  become editable on click; unsupported formulas remain manual.
- The product fragment in browser history closes the picker/editor on Back.
  Reloading still discards the local draft. No product, accessory, payment or
  order data is sent to the Sales API in this milestone.
- Price-list, stock, production and warranty actions occupy their final layout
  but stay disabled until milestone 2. Their current values are placeholders,
  not estimates from the legacy database.
