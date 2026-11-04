# Sales product picker change log

## 2026-11-04 — two-column finder

- The product picker now shows categories on the left and types on the right. Up to five frequently used categories and types appear first; those entries are omitted from the corresponding **All** section. Category rows use 22 px on desktop viewports at or below 950 px high and the specified 26 px on taller screens, so 26 categories fit within 1440×900 without scrolling.
- Search matches category and type names. Hover, click, arrow keys, Enter from search, and type shortcuts 1–9 use the same selection and existing create-product command. Unsupported types remain disabled.
- `GET /api/sales/order-entry/catalog/usage?storeId={id}&days=7` returns product-row counts for the selected store, grouped by category and type, using the order date for the seven-day window. The SQL query applies the current legacy login's `Loc_leidimas()` branch scope. An unavailable usage response leaves the picker fully usable without frequent sections; it never substitutes sample counts.
- EN and LT labels are in `SalesResources`.
- Visual checks: [1440×900](sales-product-picker-1440x900.jpg) and [1920×1080](sales-product-picker-1920x1080.jpg), with Horizontalios selected and neither picker column scrolling.
