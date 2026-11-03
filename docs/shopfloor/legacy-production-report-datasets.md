# Gamyba GU report datasets

Read-only inventory from the current Desktop Access VBA, embedded report designs and shared SQL database on 2026-11-03. The 13 active `DIC_ProjectReports` rows in category `gam` map to 13 stored procedures. These are data sources, not report layouts.

| ID | Access report | SQL dataset |
|---:|---|---|
| 1 | rptGamDokXLPlise | SP_GamPlisuotasByTask |
| 2 | rptGamDokTinkleliai | SP_GamTinkleliaiByTask |
| 3 | rptGamDokRoletai | SP_GamRoletaiByTask |
| 4 | rptGamDokApsaugines | SP_GamApsauginesByTask |
| 5 | rptGamDokHorizontalios | SP_GamHorizontaliosByTask |
| 6 | rptGamDokPanelTrack | SP_GamPanelTrackByTask |
| 7 | rptGamDokRemontasPriedai | SP_GamRemontasPriedaiByTask |
| 8 | rptGamDokVertikalios | SP_GamVertikaliosByTask |
| 9 | rptGamDokRomanetes | SP_GamRomanetesByTask |
| 17 | rptGamDokGarantiniai | SP_GamInWarrantyFaultsByTask |
| 18 | rptGamDokNatureConcept | SP_rptGamNatureConceptByTask |
| 19 | rptGamDokGrotos | SP_GamGrotosByTask |
| 20 | rptGamDokPleated | SP_GamPleatedByTask |

All 13 definitions currently contain a `@TaskId` filter and no DML or nested `EXEC`; none checks `Loc_leidimas`. The web endpoint therefore accepts only these catalog IDs and exact report names, checks the entire GU composition under the mapped legacy SQL login, and executes the procedure in the same serializable transaction. `TAB_ProductionOutputDocumentDetails` has a composite primary key beginning with `iProductionOutputDocumentID`, allowing a key-range lock against concurrent Access inserts while the report is read. Empty datasets return `NoData`; the web preview shows data rows only.

Read-only probe under `TERMINAL\d.bykovas`: 13 catalog rows, a visible GU passes the guard, and `SP_GamRoletaiByTask` returned 62 rows and 21 columns. Under another existing mapped SQL principal with narrower branch access, a foreign barcode, direct GU and report guard returned no rows. This does not verify layout, physical printing, archive, or the other report families. See P05 and D02 in [open issues](legacy-production-issues.md).

On 2026-11-03 the current `Uzsakymai.adp` was opened on `TERMINAL-DBYKOVAS` through Guacamole. Its `rptGamDokPleated` report was previewed with an existing GU containing 18 pieces. The visible page has an `XL-Plise (bendras kiekis: … vnt.)` title, groups by order and product, a table with type/control/size/quantity/colour/`Klostes`/`Remas`/fabric width/cord/notes, and a page/date footer. This is a visual reference from Access, not yet a web PDF implementation. Its embedded RecordSource and the live SQL definition both confirm `dbo.SP_GamPleatedByTask`, which differs from `SP_GamPlisuotasByTask` used by `rptGamDokXLPlise`; the web data mapping was corrected.

The same Access production form's `Spausdinimo parametrai` dialog has three tabs. `Gamybiniai popieriai` lists the 13 production report families (Apsauginės, Garantiniai remontai, Grotos, Horizontalios, Panel Trackai, Pleated Blinds, Remontai / Priedai, Roletai, Romanetės, Sistemos "Nature Concept", Tinkleliai, Vertikalios, XL-Plise) and actions `Peržiūra`, `Spausdinti`, `*.pdf failą`, `*.xls failą`. `Išdavimo dokumentai` has four forms: `Išdavimo važtaraštis klientui`, `Išsiuntimo važtaraštis filialams`, `Krovinio važtaraštis`, `Sąskaita-faktūra`. `Barkododiniai lipdukai` offers `Visus pagal gamybos užduotį`, `Vieną pagal užsakymą ir gaminį`, and `Vieną pagal įvestas reikšmes` with a GU number picker. The report designer shows an Access report with group header/footer bands, expressions, and a page footer, so a raw dataset table is not a visual or print equivalent.
