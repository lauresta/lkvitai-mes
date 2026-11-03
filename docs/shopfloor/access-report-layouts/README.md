# Access production report layouts

These 13 `rptGamDok*` definitions and the shared `rsubParduotiPriedai` subreport were exported on 2026-11-03 from the current `Uzsakymai.adp` on `TERMINAL-DBYKOVAS` using Access `Application.SaveAsText acReport`. They are source evidence for reproducing the production paperwork; they are not generated PDFs or approved print templates.

Each text file records the report's SQL RecordSource, section bands, grouping levels, control positions in Access twips, captions, expressions, fonts, page and printer settings. Keep the exported definitions unchanged so a renderer can be checked against the source. The report ID and SQL procedure mapping is in [legacy-production-report-datasets.md](../legacy-production-report-datasets.md).

The 13 main files cover the active `DIC_ProjectReports` category `gam` entries. Twelve of them embed `rsubParduotiPriedai`, which reads `V_ParduotiPriedai` by `ParduotoGaminioID`; the web report-data endpoint currently does not include those rows. They do not cover the four issue/transport/invoice documents or barcode labels; those have separate Access objects and VBA. A populated PDF, a long multipage PDF and an empty case still need to be captured from Access before claiming visual parity. The Excel action in the Access dialog has its VBA body commented out.

`rptGamDokPleated` has a design-time RecordSource of `SP_GamPleatedByTask`, but its `Report_Open` VBA switches to `SP_GamPlisuotasByTask`. Runtime parity uses the latter until the owner explicitly changes legacy logic.
