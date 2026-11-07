# Roletai paperless production document

The `/shopfloor/roletai` page preserves the printed `rptGamDokRoletai` columns and grouping while adding personal Step 1–3 marks and shared worker sign-offs. Marks and sign-offs never change the legacy order, stock or production state.

The default read shows non-cancelled roller products in `Gaminamas` (`BusenosID=1`) for order dates from Vilnius today minus 14 days through today. The selected range is always visible below the header. Order search uses a case-insensitive, whitespace-insensitive substring across every date; it applies the legacy `Loc_leidimas()` branch restriction and returns all matching roller products. A missing order, an order without rollers, and a cancelled order have separate messages. Search does not grant marking rights to products outside `Gaminamas`.

The projection matches the 21 fields of `dbo.SP_GamRoletaiByTask`; the task number is found from the product barcode's GU document. Accessories come from `V_ParduotiPriedai`. The printed view sorts Cechas → Uzsakymas → Spalva → ParduotoGaminioID.

The Shopfloor PostgreSQL tables keep each mark per `(product, user, step)` and sign-offs per `(product, user)`. The API never reads another user's marks; all active sign-offs are visible. A sign-off can be withdrawn by its owner, retaining its withdrawn record for audit. Open Blazor circuits on one server receive immediate sign-off refresh; the page also polls every 25 seconds. The PWA service worker is network only and never caches order data.
