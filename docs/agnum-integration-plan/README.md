# Agnum Integration Plan

Created: 2026-05-18

This folder collects discovery notes and implementation planning for reworking the Warehouse module around real business needs and the Agnum accounting API.

The key direction change is important:

- Old assumption: integration through XML/CSV-like files and periodic export.
- New target: integration through the deployed Agnum API from `lauresta/agnum-api-deploy`.
- Revised first priority: import Agnum nomenclature and per-`sndid` balances into MES, represent `sndid` as MES virtual warehouses, then distribute virtual balances into real MES physical locations. Agnum document export comes later.

## Documents

- [01-as-is-warehouse.md](01-as-is-warehouse.md) - current Warehouse module, entities, flows, Agnum-related code, and gaps.
- [02-agnum-api-findings.md](02-agnum-api-findings.md) - findings from `lauresta/agnum-api-deploy` and how it differs from current assumptions.
- [03-target-solution-architecture.md](03-target-solution-architecture.md) - proposed architecture for API-based integration.
- [04-implementation-plan.md](04-implementation-plan.md) - phased implementation plan.
- [05-open-questions.md](05-open-questions.md) - questions to confirm with business/Agnum/accounting.
- [06-master-data-mapping.md](06-master-data-mapping.md) - Agnum product/warehouse/category/supplier mapping to current Warehouse fields and gaps.

Related ADR:

- [ADR-006: Warehouse-Agnum Integration Is Document-Based, Not Stock-Ledger-Event-Based](../adr/006-warehouse-agnum-document-based-integration.md)

## Primary Sources Reviewed

Local Warehouse code:

- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/Entities/MasterDataEntities.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/Aggregates/StockLedger.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts/Events/StockMovedEvent.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/AgnumController.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/AgnumExportServices.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/AgnumReconciliationServices.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Sagas/AgnumExportSaga.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Services/AgnumClient.cs`
- `docs/process/processes/P-08-agnum-integration-reconciliation/index.md`
- `docs/phase15/features/agnum-export-job.md`
- `docs/phase15/features/agnum-configuration-ui.md`

External Agnum API repo:

- Repository: `https://github.com/lauresta/agnum-api-deploy`
- Checked commit: `c059aa3` on 2026-05-18 09:47:35 +0300
- Key docs: `README.md`, `docs/AGNUM_API_NOTES.md`, `docs/AGNUM_WAREHOUSE_INTEGRATION_GUIDE.md`, `application.properties.example`
