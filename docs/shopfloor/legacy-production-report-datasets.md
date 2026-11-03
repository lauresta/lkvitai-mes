# Gamyba GU report datasets

Read-only inventory from the current Desktop Access VBA and shared SQL database on 2026-11-03. The 13 active `DIC_ProjectReports` rows in category `gam` map to 12 stored procedures; XL Plise and Pleated Blinds share one procedure. These are data sources, not report layouts.

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
| 20 | rptGamDokPleated | SP_GamPlisuotasByTask |

All 12 definitions currently contain a `@TaskId` filter and no DML or nested `EXEC`; none checks `Loc_leidimas`. The web endpoint therefore accepts only these catalog IDs and exact report names, checks the entire GU composition under the mapped legacy SQL login, and executes the procedure in the same serializable transaction. `TAB_ProductionOutputDocumentDetails` has a composite primary key beginning with `iProductionOutputDocumentID`, allowing a key-range lock against concurrent Access inserts while the report is read. Empty datasets return `NoData`; the web preview shows data rows only.

Read-only probe under `TERMINAL\d.bykovas`: 13 catalog rows, a visible GU passes the guard, and `SP_GamRoletaiByTask` returned 62 rows and 21 columns. This does not verify layout, physical printing, archive, or the other report families. See P05 and D02 in [open issues](legacy-production-issues.md).
