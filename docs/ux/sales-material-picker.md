# Sales material picker

Product entry uses a searchable material combobox instead of a native datalist.
Each option shows its code, name, optional real thumbnail and the stock columns
returned by Access `sp_cboSpalva`: Free, Warehouse, Production, Total, Reserved.
The code ordering remains length first, then code. Search matches code or name.

`LiveColourDto` and `DemoColour` expose nullable decimal stock fields and the
native material unit. The SQL catalog reads these directly from `view_Remains`;
it does not round quantities or reinterpret stock. In particular, Reserved is
already negative, and Free already includes that subtraction. These displayed
numbers are a catalog snapshot. Existing product preview adds back its own
reservation where required, and save continues to validate against current data.

Arrow keys move through suggestions; Enter chooses one. Tab accepts the active
suggestion while editing and then follows native focus navigation. The toggle
and options introduce no extra Tab stops. Typing clears the selected identity;
arbitrary unmatched text does not establish a material ID. Errors may be shown
directly beneath the field with an explicit ARIA description.

Thumbnails reuse existing same-origin Frontline photo metadata. Lookup is
optional, delayed by 180 ms, limited to two concurrent requests, and cached for
five minutes. It queries the exact selected code plus at most four visible
two-character code prefixes, each capped at 100 rows with a 2.5 second timeout.
Only exact returned codes with service-owned thumbnail paths are rendered.
Prefix pages may omit thumbnails for some unselected choices; selecting one
performs its exact code lookup. Missing photos use a neutral image glyph.
No speculative requests are made to nonexistent photo URLs. Frontline's rounded
availability values are never used as material stock values in Sales.

The existing same-origin metadata endpoint was verified on test with prefixes
R1 and R2, including the high threshold used to include healthy stock. Both
returned HTTP 200. Their current data had no stored thumbnail URLs, so no
photograph was fabricated. A module smoke check verified exact selected-code
discovery, concurrency, cache, stale-query debounce and remote-URL rejection.

## Actual legacy image-source check

The current Desktop Access VBA export was checked, including `frmNewBlind`,
`frmViewBlind` and `frmComponents`. `frmNewBlind:215` assigns `iColour.RowSource`
to `EXEC sp_cboSpalva`. That procedure projects only the seven stock columns
listed above; it contains no image reference. Those three forms' VBA has no
`Picture`, `Image`, `LoadPicture`, bitmap-extension or UNC-image-path mapping.
Image references elsewhere in the VBA belong to menu/state ListView icons.
`TBD_Components` also has no photo/image column. No dynamic legacy material-photo
source was discovered in these current sources. This does not assert that an
Access form cannot contain a static embedded image.

Frontline's PostgreSQL/object-storage photo store is an optional independent
source, not a copy of an identified Access image directory. Remote verification
found no stored `ThumbnailUrl` among its current 757 fabric rows, including
exact `R173`. Therefore the current real-data UI legitimately displays the
neutral image glyph. A future uploaded Frontline photo will appear through the
same contract without inventing a material photograph or requiring a UI change.
