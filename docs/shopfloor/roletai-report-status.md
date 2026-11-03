# G05 · rptGamDokRoletai

Scope: only the roller-blinds production report and its template editor. The live rows come from `dbo.SP_GamRoletaiByTask` through the existing GU visibility and mapped legacy-login checks. Templates live in Shopfloor PostgreSQL, not in the shared legacy SQL database.

The first published layout follows the designer handoff in `Новые экраны модуля Sales.zip`: six sections, 13 detail columns, A4 landscape, grouped by workshop, order and sold product. The generated report includes the linked `rsubParduotiPriedai` accessories already returned by the report API. A report with no rows cannot be printed. Supervisor, Technologist and Admin roles may edit; operators may only use the published version. Drafts and immutable published template JSON versions persist in Shopfloor PostgreSQL. Unknown tokens, duplicate fields, bad widths and non-dataset fields are rejected server-side.

The designer handoff spells the guide-type field `Kreipianciuju_tipas`, but the actual Access `ControlSource` and SQL result use `Krepianciuju_tipas`; the implementation uses the real dataset field to avoid a blank column.

Open verification and scope items:

- Compare the print output with Access PDF samples for a short, a multipage and an empty GU. The supplied screenshot shows Design view, not a rendered report; exact page breaks and font metrics are therefore unverified.
- The browser print dialog can save a PDF, but automatic archival of generated PDFs with their template version is not implemented. Existing legacy UNC PDF archives are not touched.
- Preview page numbers are illustrative; print uses CSS page counters. Check them in the target Windows Chrome print driver on a multipage GU.
- The designer's default column proportions are retained. The Access `SaveAsText` export in `docs/shopfloor/access-report-layouts/rptGamDokRoletai.txt` has exact twip positions; compare against a rendered Access PDF before changing the approved layout.
- Zebra COM label printing belongs to the separate label flow and is outside this one-report change.
