- Timestamp: 2026-02-11T05:28:42Z
  TaskId: PRD-1510
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:3048, docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:3062, src:11
  Impact: Task specifies React/Tailwind app in src/LKvitai.MES.UI, but repository UI project is Blazor in src/LKvitai.MES.WebUI; validation steps are not directly executable as written.
  Proposed resolution: Implement UI requirements using existing Blazor app and map validation to Possible reasons for this include:
  * You misspelled a built-in dotnet command.
  * You intended to execute a .NET program, but dotnet-build/test does not exist.
  * You intended to run a global tool, but a dotnet-prefixed executable with this name could not be found on the PATH. + manual page checks in WebUI routes.

- Timestamp: 2026-02-11T05:28:42Z
  TaskId: PRD-1518
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1750, src:11
  Impact: Task validation depends on non-existent frontend folder (), creating a tooling mismatch for Sprint 2 UI execution.
  Proposed resolution: Deliver equivalent 3D/2D visualization features in  and validate with the existing ASP.NET host.

- Timestamp: 2026-02-11T05:28:42Z
  TaskId: PRD-1501
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:190, docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:395, src/tests (no  markers found)
  Impact: Task docs rely on MSBUILD : error MSB1003: Specify a project or solution file. The current working directory does not contain a project or solution file., but current tests are not categorized, so filter commands can return zero tests.
  Proposed resolution: Add xUnit  tags to new/updated task tests or run precise FullyQualifiedName filters and record substitution.
- Timestamp: 2026-02-11T05:28:54Z
  TaskId: PRD-1510
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:3048, docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:3062, src:11
  Impact: Task specifies React/Tailwind app in src/LKvitai.MES.UI, but repository UI project is Blazor in src/LKvitai.MES.WebUI; validation steps are not directly executable as written.
  Proposed resolution: Implement UI requirements using existing Blazor app and map validation to dotnet build/test plus manual page checks in WebUI routes.

- Timestamp: 2026-02-11T05:28:54Z
  TaskId: PRD-1518
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1750, src:11
  Impact: Task validation depends on non-existent frontend folder src/LKvitai.MES.UI, creating a tooling mismatch for Sprint 2 UI execution.
  Proposed resolution: Deliver equivalent 3D/2D visualization features in src/LKvitai.MES.WebUI and validate with the existing ASP.NET host.

- Timestamp: 2026-02-11T05:28:54Z
  TaskId: PRD-1501
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:190, docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:395, src/tests (no Trait Category markers found)
  Impact: Task docs rely on dotnet test --filter Category=..., but current tests are not categorized, so filter commands can return zero tests.
  Proposed resolution: Add xUnit Trait Category tags to new and updated task tests or run precise FullyQualifiedName filters and record substitution.
- Timestamp: 2026-02-11T05:29:23Z
  TaskId: PRD-1501
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:67, src/LKvitai.MES.Infrastructure/Persistence/MartenConfiguration.cs:38
  Impact: Task demands explicit event_processing_checkpoints tracking, while Marten async daemon already tracks projection progress internally; dual checkpoint systems may diverge.
  Proposed resolution: Add the required table for compatibility and use it only for custom handlers outside Marten projections; keep Marten internal checkpointing for projection daemon.
- Timestamp: 2026-02-11T05:33:40Z
  TaskId: PRD-1501
  Type: TEST-GAP
  Evidence: Validation command psql -d warehouse ... failed with 'command not found: psql'
  Impact: Cannot execute SQL-level validation of processed command retention/checkpoint tables in this environment.
  Proposed resolution: Install PostgreSQL client tools or run validation inside a DB-enabled CI job/container.

- Timestamp: 2026-02-11T05:33:40Z
  TaskId: PRD-1501
  Type: TEST-GAP
  Evidence: curl POST http://localhost:5000/api/admin/idempotency/cleanup returned HTTP 403 without auth token
  Impact: Manual endpoint validation for cleanup execution and replay behavior could not be completed anonymously.
  Proposed resolution: Re-run with Warehouse Admin credentials in Authorization header.
- Timestamp: 2026-02-11T05:40:20Z
  TaskId: PRD-1502
  Type: RISK
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:278-283, src/LKvitai.MES.Infrastructure/Persistence/MartenConfiguration.cs:45-47
  Impact: Only the sample StockMoved v1->v2 upcaster is wired into Marten; additional event-type/version chains still require explicit registration to satisfy full coverage.
  Proposed resolution: Register all future upcasters through a centralized composition step and add integration tests that read old events through projections.

- Timestamp: 2026-02-11T05:40:20Z
  TaskId: PRD-1502
  Type: TEST-GAP
  Evidence: dotnet test --filter FullyQualifiedName~EventUpcastingTests and ~EventUpcastingPerformanceTests completed without matching dedicated test classes
  Impact: Task-specific validation command names from spec do not map 1:1 to existing test class names, reducing observability of intended checks.
  Proposed resolution: Add explicit EventUpcastingTests and EventUpcastingPerformanceTests class names or update validation script to match actual test filters.
- Timestamp: 2026-02-11T05:44:59Z
  TaskId: PRD-1503
  Type: TEST-GAP
  Evidence: dotnet run src/LKvitai.MES.Api failed at startup with Npgsql timeout to 10.211.55.2:5432
  Impact: End-to-end runtime verification of response headers, logs, and trace propagation could not be executed in this environment.
  Proposed resolution: Run API validation with reachable PostgreSQL instance (or test container profile) and re-run curl/log checks.

- Timestamp: 2026-02-11T05:44:59Z
  TaskId: PRD-1503
  Type: TEST-GAP
  Evidence: curl http://localhost:5000/api/warehouse/v1/items returned headers from non-project service (Server: AirTunes)
  Impact: Validation target port in task doc does not map to this local API process, so direct header checks are unreliable.
  Proposed resolution: Use configured API URL from launch settings or explicit Kestrel URLs during run command.
- Timestamp: 2026-02-11T05:45:53Z
  TaskId: PRD-1504
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:754, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:18
  Impact: Task model specifies SalesOrderLine.ItemId as GUID, but existing Item primary key is int across the codebase and database.
  Proposed resolution: Implement SalesOrderLine.ItemId as int for compatibility, keep foreign key to existing items table.
- Timestamp: 2026-02-11T05:50:13Z
  TaskId: PRD-1504
  Type: TEST-GAP
  Evidence: dotnet ef database update ... failed with Npgsql timeout to 10.211.55.2:5432
  Impact: Migration application and live schema verification could not be completed against database in this environment.
  Proposed resolution: Re-run migration apply in environment with reachable warehouse PostgreSQL.

- Timestamp: 2026-02-11T05:50:13Z
  TaskId: PRD-1504
  Type: TEST-GAP
  Evidence: psql -d warehouse -c "\d customers" failed: command not found: psql
  Impact: SQL schema/index manual checks from task validation cannot be executed locally.
  Proposed resolution: Install psql client or run checks inside DB container shell.
- Timestamp: 2026-02-11T06:00:53Z
  TaskId: PRD-1505
  Type: RISK
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:1264, src/LKvitai.MES.Api/Services/SalesOrderCommandHandlers.cs:131
  Impact: Submit/approve/allocate handlers do not validate available stock before allocation, so the documented 409 insufficient-stock path is not enforced yet.
  Proposed resolution: Add available-stock projection check per order line before status transition to ALLOCATED and return validation/conflict when requested qty exceeds availability.

- Timestamp: 2026-02-11T06:00:53Z
  TaskId: PRD-1505
  Type: RISK
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:1062, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:141
  Impact: Optimistic locking with RowVersion for sales orders is required by task but not implemented, which can allow lost updates under concurrent writes.
  Proposed resolution: Add byte[] RowVersion concurrency token to SalesOrder EF mapping and generate migration to enforce update conflicts.
- Timestamp: 2026-02-11T06:01:31Z
  TaskId: PRD-1505
  Type: RISK
  Evidence: src/LKvitai.MES.Api/LKvitai.MES.Api.csproj (OpenTelemetry.Instrumentation.AspNetCore 1.7.0, OpenTelemetry.Instrumentation.Http 1.7.0), dotnet build output warning NU1902
  Impact: Known moderate package vulnerabilities are present during build/test, which can affect production readiness criteria.
  Proposed resolution: Upgrade affected OpenTelemetry instrumentation packages to patched versions and re-run full regression tests.
- Timestamp: 2026-02-11T06:01:31Z
  TaskId: PRD-1505
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:1290, environment DB connection failures to 10.211.55.2:5432 from dotnet ef database update
  Impact: Task-specified curl API workflow checks (create/submit/list sales orders) could not be executed end-to-end in this environment.
  Proposed resolution: Run API with reachable PostgreSQL (or test container profile) and execute the documented curl/Postman scenarios.
- Timestamp: 2026-02-11T06:04:18Z
  TaskId: PRD-1506
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:1456, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:506
  Impact: Task model specifies OutboundOrderLine/ShipmentLine ItemId as Guid, while repository item key is int; direct Guid FK would break existing schema.
  Proposed resolution: Keep ItemId as int in outbound/shipment lines for compatibility and document conversion boundary at API layer.
- Timestamp: 2026-02-11T06:04:43Z
  TaskId: PRD-1506
  Type: TEST-GAP
  Evidence: dotnet ef database update failed with Npgsql timeout to 10.211.55.2:5432; psql checks for outbound_orders/shipments failed with 'command not found: psql'
  Impact: Migration application and SQL-level schema verification for outbound/shipment tables could not be completed locally.
  Proposed resolution: Re-run migration apply and schema inspection in environment with reachable PostgreSQL and installed psql client.
- Timestamp: 2026-02-11T06:06:01Z
  TaskId: PRD-1507
  Type: RISK
  Evidence: src/LKvitai.MES.Api/Services/OutboundOrderCommandHandlers.cs:64-72, docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:1904
  Impact: Packing barcode validation currently maps only Item.PrimaryBarcode; alternate barcodes from item_barcodes are not considered, which can reject valid scans.
  Proposed resolution: Extend lookup to include ItemBarcode rows (all active barcodes per item) and aggregate scans across barcode aliases.

- Timestamp: 2026-02-11T06:06:01Z
  TaskId: PRD-1507
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:1916, environment lacks runnable local API+DB/psql verification
  Impact: Manual pack endpoint validation and SQL checks for shipment/HU creation were not executed end-to-end.
  Proposed resolution: Run documented curl + SQL checks against an environment with reachable PostgreSQL and API instance.
- Timestamp: 2026-02-11T06:08:04Z
  TaskId: PRD-1508
  Type: RISK
  Evidence: src/LKvitai.MES.Api/Services/FedExApiService.cs:9-43, docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:2090
  Impact: Carrier API adapter currently generates local tracking numbers and does not call an external FedEx endpoint, so real integration behavior (HTTP failures/auth/payload contract) is unverified.
  Proposed resolution: Replace stub with HttpClient-based FedEx API client and integration tests against a mock/staging endpoint.

- Timestamp: 2026-02-11T06:08:04Z
  TaskId: PRD-1508
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:2192, local environment lacks running API workflow validation against reachable DB
  Impact: Dispatch curl scenarios and shipment/outbound SQL checks were not executed end-to-end.
  Proposed resolution: Run documented dispatch API flow in environment with reachable PostgreSQL and authenticated API instance.
- Timestamp: 2026-02-11T06:21:52Z
  TaskId: PRD-1509
  Type: RISK
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:2295, src/LKvitai.MES.Infrastructure/Projections/ProjectionRebuildService.cs:559
  Impact: Task requests projection rebuild via event-store replay, but outbound/dispatch projections are rebuilt from persisted relational state because outbound operational events are not currently stored in Marten event streams.
  Proposed resolution: Persist outbound operational events to event store (or dedicated append-only log) and switch rebuild methods to sequence-ordered replay from that source.

- Timestamp: 2026-02-11T06:21:52Z
  TaskId: PRD-1509
  Type: TEST-GAP
  Evidence: dotnet ef database update failed with timeout to 10.211.55.2:5432; psql command unavailable; curl http://localhost:5000/api/warehouse/v1/outbound/orders/summary returned 403
  Impact: Task-specified SQL verification and end-to-end projection endpoint validation could not be fully executed in this environment.
  Proposed resolution: Re-run migration/API validation in an environment with reachable warehouse PostgreSQL, installed psql, and authenticated API access.
- Timestamp: 2026-02-11T06:29:10Z
  TaskId: PRD-1510
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:2690, docs/prod-ready/prod-ready-tasks-PHASE15-S1.md:3048, src/LKvitai.MES.WebUI:1
  Impact: Task requires React pages under src/LKvitai.MES.UI, but repository only contains Blazor Server UI under src/LKvitai.MES.WebUI.
  Proposed resolution: Deliver equivalent routes/components in Blazor WebUI and treat npm/React validation steps as non-applicable for this repository baseline.

- Timestamp: 2026-02-11T06:29:10Z
  TaskId: PRD-1510
  Type: TEST-GAP
  Evidence: "cd src/LKvitai.MES.UI && npm run dev" failed (directory missing); manual browser navigation/scan workflow not executable in this CLI-only run
  Impact: Task-specified interactive UI validation (page navigation, barcode scan UX, modal dispatch flow) could not be completed end-to-end.
  Proposed resolution: Run the Blazor WebUI locally and execute manual route/workflow checks with an authenticated API endpoint.
- Timestamp: 2026-02-11T06:34:35Z
  TaskId: PRD-1511
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:89, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:17
  Impact: Valuation stream model uses Guid ItemId while operational master-data item key is int; direct cross-module joins will require an explicit mapping policy in command handlers.
  Proposed resolution: Define deterministic key translation (int item id -> Guid valuation stream id) in PRD-1512 handlers and document it in valuation ADR.

- Timestamp: 2026-02-11T06:34:35Z
  TaskId: PRD-1511
  Type: TEST-GAP
  Evidence: psql validation commands for mt_events failed with 'command not found: psql'
  Impact: Direct SQL verification of valuation stream rows and schema-version payloads could not be performed in this environment.
  Proposed resolution: Re-run mt_events SQL checks in a DB-enabled shell/container with PostgreSQL client tools installed.
- Timestamp: 2026-02-11T06:45:06Z
  TaskId: PRD-1512
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:361, docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:465
  Impact: Approval rule text says CFO approval is required for impact > $10,000, but acceptance scenario expects CFO approval at exactly $10,000.
  Proposed resolution: Clarify threshold boundary in the task (either "> $10,000" or ">= $10,000") and align validation and scenario text.

- Timestamp: 2026-02-11T06:45:06Z
  TaskId: PRD-1512
  Type: RISK
  Evidence: src/LKvitai.MES.Api/Services/ValuationCommandHandlers.cs:94, src/LKvitai.MES.Api/Services/ValuationCommandHandlers.cs:102
  Impact: Approval authorization is derived from the current authenticated user roles, while ApproverId is treated as a required marker only; this can diverge from a strict "approver identity must hold role" workflow.
  Proposed resolution: Resolve ApproverId via identity/user-role lookup and enforce approval role on that principal (not just current caller).

- Timestamp: 2026-02-11T06:45:06Z
  TaskId: PRD-1512
  Type: TEST-GAP
  Evidence: psql --version failed with "command not found: psql"; curl POST http://localhost:5000/api/warehouse/v1/valuation/1/adjust-cost returned 403
  Impact: Task-specified SQL event verification and full authenticated API workflow validation were not completed end-to-end in this environment.
  Proposed resolution: Re-run documented curl + mt_events SQL checks in an environment with PostgreSQL client tools and authenticated API access.
- Timestamp: 2026-02-11T06:56:27Z
  TaskId: PRD-1513
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:618, docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:624, docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:712
  Impact: Task contracts model `ItemId/CategoryId/locationId` as GUID values, while repository master-data keys for item/category/location are `int`.
  Proposed resolution: Keep projection/query filters aligned to existing `int` keys and document GUID↔int translation boundary in valuation API docs.

- Timestamp: 2026-02-11T06:56:27Z
  TaskId: PRD-1513
  Type: RISK
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:605, src/LKvitai.MES.Infrastructure/Projections/ProjectionRebuildService.cs:635, src/LKvitai.MES.Infrastructure/Projections/ProjectionRebuildService.cs:650
  Impact: OnHandValue rebuild derives quantities from the current `AvailableStockView` snapshot plus valuation event replay; it does not replay `StockMoved` deltas directly into quantity state, so rebuild correctness depends on `AvailableStockView` integrity.
  Proposed resolution: Add dedicated on-hand quantity replay path from stock movement/reservation events (or assert/rebuild AvailableStock first in the same command).

- Timestamp: 2026-02-11T06:56:27Z
  TaskId: PRD-1513
  Type: TEST-GAP
  Evidence: psql command failed (`command not found: psql`); curl POST/GET valuation endpoints returned 403 against localhost:5000
  Impact: Task-specified SQL verification (`on_hand_value` table) and authenticated manual API workflow checks were not completed in this environment.
  Proposed resolution: Re-run documented curl + SQL validation in an environment with PostgreSQL client tools and valid API auth context.
- Timestamp: 2026-02-11T07:05:45Z
  TaskId: PRD-1514
  Type: RISK
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:861, docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:865, src/LKvitai.MES.Api/Services/AgnumExportServices.cs:170
  Impact: Export scopes `BY_WAREHOUSE` and `BY_LOGICAL_WH` are configured, but current export data source (`on_hand_value`) does not carry warehouse/logical warehouse dimensions; those scopes currently resolve via DEFAULT mappings only.
  Proposed resolution: Extend projection source for export to include warehouse/logical warehouse dimensions (or constrain allowed scope until that data is available).

- Timestamp: 2026-02-11T07:05:45Z
  TaskId: PRD-1514
  Type: RISK
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:873, src/LKvitai.MES.Api/Program.cs:154, src/LKvitai.MES.Api/Program.cs:161
  Impact: Scheduler persistence falls back to in-memory storage when no DB connection string is resolved, which does not meet the 99.9% reliability expectation for durable recurring jobs.
  Proposed resolution: Require PostgreSQL-backed Hangfire storage in non-dev environments and fail startup when persistent storage is not configured.

- Timestamp: 2026-02-11T07:05:45Z
  TaskId: PRD-1514
  Type: TEST-GAP
  Evidence: curl PUT/POST/GET /api/warehouse/v1/agnum/* returned 403; curl http://localhost:5000/hangfire returned 403
  Impact: Task-specified authenticated API and Hangfire dashboard validation could not be completed end-to-end in this environment.
  Proposed resolution: Re-run Agnum API + dashboard checks with valid auth context and a running local API profile.
- Timestamp: 2026-02-11T07:13:04Z
  TaskId: PRD-1515
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1136, src/LKvitai.MES.Api/LKvitai.MES.Api.csproj:25, dotnet build output (NU1605 downgrade when Polly 7.2.4 was introduced)
  Impact: Task notes require Polly retry, but this solution already depends on Polly 8.x transitively (via Marten); adding Polly 7.x causes restore failure.
  Proposed resolution: Keep Polly usage on v8 APIs (ResiliencePipeline) and pin direct package reference to 8.2.1.

- Timestamp: 2026-02-11T07:13:04Z
  TaskId: PRD-1515
  Type: TEST-GAP
  Evidence: curl -X POST http://localhost:5000/api/warehouse/v1/agnum/export returned HTTP 403
  Impact: Task-specified manual API export validation could not be verified without authenticated API context.
  Proposed resolution: Re-run export trigger validation with Inventory Accountant/Manager auth credentials.

- Timestamp: 2026-02-11T07:13:04Z
  TaskId: PRD-1515
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1151, src/LKvitai.MES.Api/Services/AgnumExportServices.cs:117, ls /agnum-exports/2026-02-10/ failed (path missing)
  Impact: Validation path in task doc assumes absolute `/agnum-exports/...`, while implementation default root is configured via `Agnum:ExportRootPath` (defaults under app base `exports/agnum`).
  Proposed resolution: Validate file existence using configured export root path (or set `Agnum:ExportRootPath=/agnum-exports` in runtime config).
- Timestamp: 2026-02-11T07:19:20Z
  TaskId: PRD-1516
  Type: TEST-GAP
  Evidence: curl POST http://localhost:5000/api/warehouse/v1/labels/print returned HTTP 403; curl GET /api/warehouse/v1/labels/preview returned HTTP 403
  Impact: Task-specified manual API validation for live print/preview flow could not be verified without authenticated API context.
  Proposed resolution: Re-run label print/preview checks with Operator-or-above auth credentials against a running API instance.

- Timestamp: 2026-02-11T07:19:20Z
  TaskId: PRD-1516
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1322 (requires Zebra printer or simulator), current environment has no printer/simulator bound on TCP 9100
  Impact: End-to-end hardware validation of TCP 9100 print latency/retry behavior cannot be fully executed in this environment.
  Proposed resolution: Run integration checks with a Zebra printer or TCP 9100 simulator and inspect queue/retry/fallback logs.
- Timestamp: 2026-02-11T07:25:36Z
  TaskId: PRD-1517
  Type: TEST-GAP
  Evidence: curl PUT http://localhost:5000/api/warehouse/v1/locations/R3-C6-L3 returned HTTP 403; curl GET /api/warehouse/v1/visualization/3d returned HTTP 403
  Impact: Task-specified manual endpoint validation for coordinate updates and 3D payload could not be verified without authenticated API context.
  Proposed resolution: Re-run both API checks with Operator/Manager credentials against running service.

- Timestamp: 2026-02-11T07:25:36Z
  TaskId: PRD-1517
  Type: RISK
  Evidence: src/LKvitai.MES.Api/Api/Controllers/WarehouseVisualizationController.cs:265-275
  Impact: Bin utilization is inferred from `AvailableStockView.OnHandQty` versus configured weight/volume capacities; if quantities are not weight/volume-normalized, LOW/FULL thresholds can be approximate.
  Proposed resolution: Normalize utilization using item master weight/volume conversions before threshold evaluation.
- Timestamp: 2026-02-11T07:30:09Z
  TaskId: PRD-1518
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1580-1660, repository contains src/LKvitai.MES.WebUI (Blazor) and does not contain src/LKvitai.MES.UI (React)
  Impact: Task implementation snippets/validation steps target a React frontend that does not exist in this codebase.
  Proposed resolution: Implement equivalent 2D/3D visualization UX in Blazor WebUI and validate via dotnet build plus manual route checks.

- Timestamp: 2026-02-11T07:30:09Z
  TaskId: PRD-1518
  Type: TEST-GAP
  Evidence: `cd src/LKvitai.MES.UI && npm run dev` failed with "no such file or directory"
  Impact: Task-specified manual browser interaction checks tied to the React dev server could not be executed as written.
  Proposed resolution: Run manual interaction checks against Blazor route `/warehouse/visualization/3d` in `src/LKvitai.MES.WebUI`.
- Timestamp: 2026-02-11T07:41:00Z
  TaskId: PRD-1519
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1847, docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1858, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:624
  Impact: Task data model describes , , and  as GUIDs, while repository master-data keys for Item and Location are .
  Proposed resolution: Implement transfer line foreign keys as  to match existing schema and keep API contract aligned with repository identity types.
- Timestamp: 2026-02-11T07:41:11Z
  TaskId: PRD-1519
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1847, docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:1858, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:624
  Impact: Task data model describes `TransferLine.ItemId`, `FromLocationId`, and `ToLocationId` as GUIDs, while repository master-data keys for Item and Location are `int`.
  Proposed resolution: Implement transfer line foreign keys as `int` to match existing schema and keep API contract aligned with repository identity types.
- Timestamp: 2026-02-11T07:43:52Z
  TaskId: PRD-1519
  Type: TEST-GAP
  Evidence: curl POST http://localhost:5000/api/warehouse/v1/transfers, /approve, /execute returned HTTP 403
  Impact: Task-specified manual API workflow validation could not be completed without authenticated API context.
  Proposed resolution: Re-run transfer create/approve/execute checks with Operator/Manager credentials against a running API instance.
- Timestamp: 2026-02-11T07:49:26Z
  TaskId: PRD-1520
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:2014, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:17
  Impact: Task requires ABC classification by item value bands, but existing item master model has no explicit ABC class/value tier fields.
  Proposed resolution: Use category-code prefix heuristic (A/B/C) as Phase 1.5 default and introduce explicit ABC/value configuration in a follow-up task.
- Timestamp: 2026-02-11T07:49:26Z
  TaskId: PRD-1520
  Type: TEST-GAP
  Evidence: curl POST http://localhost:5000/api/warehouse/v1/cycle-counts/schedule, /record-count, /apply-adjustment returned HTTP 403
  Impact: Task-specified manual API cycle-count workflow validation could not be completed without authenticated API context.
  Proposed resolution: Re-run cycle count schedule/record/apply checks with Operator/Manager credentials against a running API instance.
- Timestamp: 2026-02-11T07:49:47Z
  TaskId: PRD-1520
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:2037, docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:2046, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:709
  Impact: Task model expresses cycle-count line / as GUIDs, while repository identity keys for locations/items are .
  Proposed resolution: Keep cycle count foreign keys as  for schema compatibility and map API contracts to existing key types.
- Timestamp: 2026-02-11T07:49:55Z
  TaskId: PRD-1520
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:2037, docs/prod-ready/prod-ready-tasks-PHASE15-S2.md:2046, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:709
  Impact: Task model expresses cycle-count line LocationId and ItemId as GUIDs, while repository identity keys for locations and items are int.
  Proposed resolution: Keep cycle count foreign keys as int for schema compatibility and map API contracts to existing key types.
- Timestamp: 2026-02-12T05:48:10Z
  TaskId: PRD-1521
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S3.md:86-124, src/LKvitai.MES.Api/Security/WarehouseAuthenticationHandler.cs:14-74
  Impact: Spec mandates JWT issuance/validation, but the running API auth scheme is a custom header/bearer parser (`user|roles`) without JWT middleware. Replacing auth stack would risk broad RBAC regressions mid-sprint.
  Proposed resolution: Implement `/api/auth/dev-token` on top of the existing auth scheme (dev-only token format with expiry) and document that it is compatibility token, not JWT.

- Timestamp: 2026-02-12T05:48:10Z
  TaskId: PRD-1581
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S6.md:170-214
  Impact: Wave API examples use order IDs like `"order-001"` while existing sales-order identity type in codebase is `Guid`; strict contract is underspecified for real integration.
  Proposed resolution: Keep API contract aligned to existing Guid identifiers and accept only Guid order IDs for wave operations.

- Timestamp: 2026-02-12T05:48:10Z
  TaskId: PRD-1588
  Type: MISSING-INFO
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S6.md (QC attachments section), src has no configured blob provider
  Impact: Photo upload requirement references Azure Blob/S3 but no storage provider/configuration contract is defined for Phase 1.5 runtime environments.
  Proposed resolution: Implement local filesystem-backed attachment storage with stable file URLs and mark cloud blob adapter as follow-up.
- Timestamp: 2026-02-12T06:06:02Z
  TaskId: PRD-1521
  Type: TEST-GAP
  Evidence: Manual curl flow for `/api/auth/dev-token` + protected API endpoints was not executed against a live local API process in this run.
  Impact: End-to-end runtime validation of token issuance and authenticated request flow remains unverified in this environment.
  Proposed resolution: Start API in Development mode with reachable DB config and run documented curl checks from `docs/dev-auth-guide.md`.

- Timestamp: 2026-02-12T06:06:02Z
  TaskId: PRD-1541
  Type: TEST-GAP
  Evidence: `dotnet vstest` integration suite reports many tests skipped due environment prerequisites (dockerized infra/DB dependencies).
  Impact: Full E2E inbound/outbound/stock workflow regression was not fully executed in this environment.
  Proposed resolution: Run integration test suite in CI/local environment with Docker and required services enabled.

- Timestamp: 2026-02-12T06:06:02Z
  TaskId: PRD-1580
  Type: TEST-GAP
  Evidence: `k6 version` failed with `command not found: k6`.
  Impact: Load/stress smoke script (`scripts/load/warehouse-load-smoke.js`) could not be executed in this environment.
  Proposed resolution: Install k6 and run documented load command with `BASE_URL` and `TOKEN` variables.

- Timestamp: 2026-02-12T06:06:02Z
  TaskId: PRD-1583
  Type: TEST-GAP
  Evidence: Wave/cross-dock/RMA/analytics Blazor routes were implemented but no browser session is available in this CLI run for interactive verification.
  Impact: Manual UX and operator interaction validation remains pending.
  Proposed resolution: Launch `src/LKvitai.MES.WebUI` and execute route walkthrough for `/warehouse/waves`, `/warehouse/cross-dock`, `/warehouse/rmas`, `/analytics/fulfillment`, `/analytics/quality`.

- Timestamp: 2026-02-12T06:06:02Z
  TaskId: PRD-1598
  Type: TEST-GAP
  Evidence: Contract tests run against mocked/stubbed HTTP handlers only.
  Impact: Real external endpoint compatibility (FedEx/Agnum live sandboxes) is not validated by this run.
  Proposed resolution: Execute contract suite against sandbox endpoints with non-production credentials.

- Timestamp: 2026-02-12T15:16:26Z
  TaskId: PRD-1523
  Type: IMPLEMENTATION-GAP
  Evidence: No Blazor inbound shipment create page route found (expected scope mentions receiving invoice entry UI).
  Impact: Operators cannot create inbound receiving documents from WebUI as specified.
  Proposed resolution: Implement inbound shipments list/create pages and wire to receiving API endpoints.

- Timestamp: 2026-02-12T15:16:26Z
  TaskId: PRD-1524
  Type: IMPLEMENTATION-GAP
  Evidence: No Blazor receiving scan/QC execution route found in `src/LKvitai.MES.WebUI/Pages`.
  Impact: Inbound scan/QC workflow remains API-only, reducing operator usability and acceptance coverage.
  Proposed resolution: Add receiving execution UI with scan actions, QC outcomes, and putaway transitions.

- Timestamp: 2026-02-12T15:16:26Z
  TaskId: PRD-1526
  Type: IMPLEMENTATION-GAP
  Evidence: No transfer/stock-movement create/approval pages exist in WebUI routes.
  Impact: Stock movement workflow cannot be run end-to-end from UI.
  Proposed resolution: Implement transfer list/create/detail pages and approval/execute actions.

- Timestamp: 2026-02-12T15:16:26Z
  TaskId: PRD-1534
  Type: IMPLEMENTATION-GAP
  Evidence: Dispatch history report API exists (`DispatchController.GetHistoryAsync`) but no corresponding Blazor report page.
  Impact: Dispatch history visibility requested by UI task remains unavailable to operators.
  Proposed resolution: Add dispatch history report page with filters and CSV export.

- Timestamp: 2026-02-12T15:16:26Z
  TaskId: PRD-1548
  Type: IMPLEMENTATION-GAP
  Evidence: No admin user management route/page (`/admin/users` equivalent) exists in WebUI.
  Impact: RBAC user lifecycle remains unmanaged from UI.
  Proposed resolution: Implement user list/create/edit/role-assignment admin pages.

- Timestamp: 2026-02-12T15:16:26Z
  TaskId: PRD-1549
  Type: IMPLEMENTATION-GAP
  Evidence: No stock movement history report page route found in WebUI.
  Impact: Operators/managers cannot review movement history report via UI as specified.
  Proposed resolution: Add movement history report page and integrate report API/filter/export flow.

- Timestamp: 2026-02-12T15:16:26Z
  TaskId: PRD-1551
  Type: IMPLEMENTATION-GAP
  Evidence: No lot traceability report UI route found in WebUI pages.
  Impact: Traceability workflow remains unavailable from the operator-facing interface.
  Proposed resolution: Implement lot->order traceability report page with search and drill-down.

- Timestamp: 2026-02-12T15:16:26Z
  TaskId: PRD-1552
  Type: IMPLEMENTATION-GAP
  Evidence: No compliance audit report page found in WebUI pages.
  Impact: Compliance reporting requirements are not met at UI level.
  Proposed resolution: Implement compliance report dashboard/export page and connect to backend query endpoints.
- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1524
  Type: AMBIGUITY
  Evidence: `POST /api/warehouse/v1/qc/fail` validates `reasonCode`, but no dedicated endpoint exists to enumerate allowed adjustment reason codes for UI dropdown.
  Impact: QC fail UX requires operators to know reason codes out-of-band, increasing input errors.
  Proposed resolution: Add `GET /api/warehouse/v1/adjustments/reason-codes` (or similar) and bind QC UI to server-provided values.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1548
  Type: INCONSISTENCY
  Evidence: Admin user management backend uses `InMemoryAdminUserStore` without database persistence.
  Impact: Created/updated users are lost on API restart; production-grade RBAC lifecycle is incomplete.
  Proposed resolution: Replace in-memory store with persistent table-backed implementation and audited password reset flow.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1551
  Type: RISK
  Evidence: Traceability endpoint returns `IsApproximate=true`; downstream linkage inferred via item-level shipped quantities, not strict lot-level shipment lines.
  Impact: Lot-to-order traceability may over-include related sales orders for the same item.
  Proposed resolution: Persist lot identifiers through pick/pack/shipment line flow and query exact lot lineage.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1523
  Type: TEST-GAP
  Evidence: No browser runtime available in this CLI session for manual walkthrough of inbound create/list/detail pages.
  Impact: Interactive UX validation (form behavior, navigation, operator workflow) was not executed end-to-end.
  Proposed resolution: Run `src/LKvitai.MES.WebUI` locally and execute manual scenario for `/warehouse/inbound/shipments` and `/warehouse/inbound/shipments/create`.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1524
  Type: TEST-GAP
  Evidence: No browser runtime available for scan/QC panel interaction tests.
  Impact: Manual validation of barcode-driven line resolution and QC pass/fail UX remains pending.
  Proposed resolution: Execute manual flow on `/warehouse/inbound/shipments/{id}` and `/warehouse/inbound/qc` with authenticated API data.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1526
  Type: TEST-GAP
  Evidence: Transfer UI pages were build-validated only; no interactive create->approve->execute run performed in browser.
  Impact: End-user operational flow confirmation is pending.
  Proposed resolution: Manually validate `/warehouse/transfers`, `/warehouse/transfers/create`, `/warehouse/transfers/{id}` with real stock/location data.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1534
  Type: TEST-GAP
  Evidence: Dispatch history page added but not manually verified in browser session.
  Impact: Filter interactions and CSV UX behavior remain unconfirmed by manual test.
  Proposed resolution: Run manual checks on `/reports/dispatch-history` including date filter and CSV export.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1548
  Type: TEST-GAP
  Evidence: Admin users UI compiled and API-tested via build only; no browser interaction run.
  Impact: Role checkbox UX and edit/create behavior are not manually validated.
  Proposed resolution: Validate `/admin/users` manually with create/edit role/status scenarios.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1549
  Type: TEST-GAP
  Evidence: Stock movements report UI integrated but not manually exercised in browser.
  Impact: Filter composition and CSV export interactions remain unverified at UX level.
  Proposed resolution: Manually validate `/reports/stock-movements` workflow.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1551
  Type: TEST-GAP
  Evidence: Traceability page rendering validated by compile only; no manual report walkthrough.
  Impact: Drill-down readability and search combinations were not interactively tested.
  Proposed resolution: Manually validate `/reports/traceability` for lot/item/order/supplier filters.

- Timestamp: 2026-02-12T16:09:38Z
  TaskId: PRD-1552
  Type: TEST-GAP
  Evidence: Compliance report UI compiled, but manual export (CSV/PDF) flow not executed in browser.
  Impact: Document download UX and type/date filter behavior remain unverified.
  Proposed resolution: Manually validate `/reports/compliance-audit` including CSV and PDF exports.
- Timestamp: 2026-02-12T16:14:20Z
  TaskId: PRD-1525
  Type: AMBIGUITY
  Evidence: Spec requires `MinStockLevel`-based alerts and dedicated dashboard endpoints (`/stock/dashboard-summary`, `/stock/by-location`, `/stock/low-stock`, `/stock/expiring-soon`), but current API surface exposes only stock-level style queries.
  Impact: Low-stock logic in UI uses a heuristic threshold instead of item-configured min level.
  Proposed resolution: Add first-class stock dashboard endpoints and persist per-item min-stock thresholds for deterministic alerts.

- Timestamp: 2026-02-12T16:14:20Z
  TaskId: PRD-1529
  Type: INCONSISTENCY
  Evidence: Spec references endpoints `/sales-orders/pending-approvals`, `/pending-stock`, `/allocated`, and `/reallocate`; current API exposes generic list-by-status plus approve/release actions, no reallocate endpoint.
  Impact: Allocation dashboard cannot perform API-backed manual reallocation in current backend contract.
  Proposed resolution: Implement explicit reallocate API contract and dedicated allocation query endpoints, then enable reallocation UI action.

- Timestamp: 2026-02-12T16:14:20Z
  TaskId: PRD-1525
  Type: TEST-GAP
  Evidence: Stock dashboard page compiled and integrated, but no browser-driven validation executed for summary cards/sections/CSV workflow.
  Impact: Interactive UX coverage for dashboard scenarios remains pending.
  Proposed resolution: Manually validate `/warehouse/stock/dashboard` with seeded low-stock and expiring-lot data.

- Timestamp: 2026-02-12T16:14:20Z
  TaskId: PRD-1529
  Type: TEST-GAP
  Evidence: Allocation dashboard page compiled and action wiring validated by build only; no manual browser workflow run.
  Impact: Operator/manager interaction flow for approve/release remains unverified in runtime UI.
  Proposed resolution: Manually validate `/warehouse/sales/allocations` for pending approval and release-to-picking scenarios.
- Timestamp: 2026-02-12T20:45:20Z
  TaskId: PRD-1601
  Type: TEST-GAP
  Evidence: Validation command `curl -X POST http://localhost:5000/api/auth/dev-token ...` returned HTTP 403 and no token (`/tmp/prd1601-dev-token.out`), follow-up `POST /api/warehouse/v1/valuation/initialize` also returned HTTP 403 (`/tmp/prd1601-init.out`).
  Impact: Task-specified API validation could not confirm valuation initialization/event append behavior in this environment.
  Proposed resolution: Re-run validation against the project API instance with valid Warehouse auth roles (WarehouseAdmin/InventoryAccountant) and reachable DB-backed runtime.
- Timestamp: 2026-02-12T20:47:42Z
  TaskId: PRD-1602
  Type: TEST-GAP
  Evidence: Validation command `POST /api/warehouse/v1/valuation/adjust-cost` returned HTTP 403 with empty response (`/tmp/prd1602-adjust.out`).
  Impact: Could not confirm runtime API behavior for delta-based approval/idempotent adjust-cost flow in this environment.
  Proposed resolution: Execute validation against the project API runtime with valid Warehouse auth token and database connectivity.
- Timestamp: 2026-02-12T20:50:07Z
  TaskId: PRD-1603
  Type: TEST-GAP
  Evidence: Validation command `POST /api/warehouse/v1/valuation/apply-landed-cost` returned HTTP 403 (`/tmp/prd1603-landed.out`).
  Impact: Runtime verification of proportional landed-cost allocation API behavior could not be completed in this environment.
  Proposed resolution: Re-run endpoint validation on the project API instance with valid auth and seeded on-hand valuation data.
- Timestamp: 2026-02-12T20:52:10Z
  TaskId: PRD-1604
  Type: TEST-GAP
  Evidence: Validation command `POST /api/warehouse/v1/valuation/write-down` returned HTTP 403 (`/tmp/prd1604-write-down.out`).
  Impact: Could not validate runtime approval workflow and write-down API behavior end-to-end in this environment.
  Proposed resolution: Re-run write-down API validation with a valid Warehouse auth token against the project API runtime.
- Timestamp: 2026-02-12T21:04:07Z
  TaskId: PRD-1605
  Type: TEST-GAP
  Evidence: `dotnet run --project src/LKvitai.MES.WebUI` failed with HTTPS developer certificate error; `DOTNET_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5001 dotnet run --no-launch-profile --project src/LKvitai.MES.WebUI /p:UseAppHost=false` started successfully, but browser/manual form/report verification was not executable in this terminal run.
  Impact: Full task-required manual validation for valuation dashboard/forms/reports (navigation, toasts, CSV download behavior) could not be completed end-to-end.
  Proposed minimal fix: Install/trust local dev certificate (`dotnet dev-certs https --trust`) and execute the documented UI walkthrough in a browser against `http://127.0.0.1:5001` or the HTTPS profile.
- Timestamp: 2026-02-12T21:08:26Z
  TaskId: PRD-1606
  Type: TEST-GAP
  Evidence: `dotnet run --project src/LKvitai.MES.WebUI` failed due missing HTTPS developer certificate; service startup verified via `DOTNET_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5001 dotnet run --no-launch-profile --project src/LKvitai.MES.WebUI /p:UseAppHost=false`, but browser-based manual checks (field interactions, mapping add/remove UX, toasts) were not executed in this terminal session.
  Impact: Full manual UI validation for `/warehouse/agnum/config` scenarios is incomplete in this environment.
  Proposed minimal fix: Install/trust local dev HTTPS certificate and run the documented browser walkthrough for save/test-connection/mapping validation flows.
- Timestamp: 2026-02-12T21:15:17Z
  TaskId: PRD-1607
  Type: TEST-GAP
  Evidence: Task validation commands requiring runtime API (`POST /api/auth/dev-token`, `POST /api/warehouse/v1/agnum/export`, `GET /api/warehouse/v1/agnum/history`) could not run because `dotnet run --no-launch-profile --project src/LKvitai.MES.Api /p:UseAppHost=false` crashed at startup with `Marten.Exceptions.InvalidDocumentException` for `LKvitai.MES.Domain.Aggregates.ItemValuation` (stack points to `src/LKvitai.MES.Api/Program.cs:225`).
  Impact: End-to-end manual validation for export trigger/history endpoints is blocked in this environment.
  Proposed minimal fix: Fix Marten document mapping for `ItemValuation` so API can start, then re-run the exact PRD-1607 curl validation sequence.
- Timestamp: 2026-02-12T21:24:02Z
  TaskId: PRD-1608
  Type: TEST-GAP
  Evidence: WebUI startup verified via `DOTNET_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5001 dotnet run --no-launch-profile --project src/LKvitai.MES.WebUI /p:UseAppHost=false`, but API runtime required for reconciliation upload (`POST /api/warehouse/v1/agnum/reconcile`) failed to start due `Marten.Exceptions.InvalidDocumentException` for `LKvitai.MES.Domain.Aggregates.ItemValuation` (`src/LKvitai.MES.Api/Program.cs:227`).
  Impact: End-to-end reconciliation UI/API workflow (upload CSV, generate report, verify variance numbers in browser) could not be fully executed in this environment.
  Proposed minimal fix: Resolve Marten document identity mapping for `ItemValuation`, start API successfully, then run manual reconciliation scenario on `/warehouse/agnum/reconcile`.
- Timestamp: 2026-02-12T21:27:34Z
  TaskId: PRD-1609
  Type: TEST-GAP
  Evidence: Task validation requiring runtime API calls (`POST /api/auth/dev-token`, `PUT /api/warehouse/v1/locations/{code}`) is blocked because API startup fails with `Marten.Exceptions.InvalidDocumentException` for `ItemValuation`; additionally `dotnet ef database update --project src/LKvitai.MES.Infrastructure` failed at design-time DbContext creation (`Unable to resolve service for type DbContextOptions<WarehouseDbContext>`).
  Impact: Full environment validation for migration application and curl-based coordinate update could not be completed end-to-end.
  Proposed minimal fix: Add a design-time `IDesignTimeDbContextFactory<WarehouseDbContext>` for EF tooling and fix Marten identity mapping so API can start, then re-run the exact PRD-1609 validation commands.
- Timestamp: 2026-02-12T21:35:24Z
  TaskId: PRD-1610
  Type: TEST-GAP
  Evidence: `dotnet run --project src/LKvitai.MES.WebUI` failed with HTTPS developer certificate error (`src/LKvitai.MES.WebUI/Program.cs:58`); fallback startup succeeded via `DOTNET_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5001 dotnet run --no-launch-profile --project src/LKvitai.MES.WebUI /p:UseAppHost=false`, but browser interaction checks (render timing, click latency, camera controls) were not executable in this terminal-only run.
  Impact: Full manual acceptance validation for `/warehouse/visualization/3d` could not be completed end-to-end.
  Proposed minimal fix: Install/trust local HTTPS dev certificate and execute the documented browser walkthrough for 3D rendering, interaction, and refresh behavior.
- Timestamp: 2026-02-12T21:39:06Z
  TaskId: PRD-1611
  Type: TEST-GAP
  Evidence: Task validation command `dotnet run --project src/LKvitai.MES.WebUI` failed with missing HTTPS developer certificate (`src/LKvitai.MES.WebUI/Program.cs:58`); HTTP fallback startup succeeded via `DOTNET_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5001 dotnet run --no-launch-profile --project src/LKvitai.MES.WebUI /p:UseAppHost=false`, but interactive browser checks (toggle UX, autocomplete dropdown behavior, fly-to animation timing, 2D zoom clicks) were not executable in this terminal-only run.
  Impact: Full manual acceptance verification for 2D/3D interaction scenarios is pending.
  Proposed minimal fix: Install/trust local HTTPS dev certificate and execute browser validation on `/warehouse/visualization/3d` and `/warehouse/visualization/2d`.
- Timestamp: 2026-02-12T21:43:59Z
  TaskId: PRD-1612
  Type: TEST-GAP
  Evidence: Task validation API flow could not run end-to-end because `dotnet run --no-launch-profile --project src/LKvitai.MES.Api /p:UseAppHost=false` failed at startup with `Npgsql.PostgresException: 3D000 database "lkvitai_warehouse" does not exist` (`src/LKvitai.MES.Api/Program.cs:169`, `src/LKvitai.MES.Api/Program.cs:213`); direct curl on `http://localhost:5000/api/warehouse/v1/cycle-counts/schedule` returned HTTP 403 from another local service.
  Impact: Could not verify authenticated schedule API behavior in the intended project runtime.
  Proposed minimal fix: Provision the expected PostgreSQL database and run API on an isolated known port, then execute the exact PRD-1612 curl sequence with a valid dev token.
- Timestamp: 2026-02-12T21:47:08Z
  TaskId: PRD-1613
  Type: TEST-GAP
  Evidence: Record-count validation call `POST /api/warehouse/v1/cycle-counts/{id}/record-count` returned HTTP 403 on localhost (`/tmp/prd1613-record.status`), and project API startup remains blocked by missing database (`dotnet run --no-launch-profile --project src/LKvitai.MES.Api /p:UseAppHost=false` -> `database "lkvitai_warehouse" does not exist`).
  Impact: Could not execute authenticated end-to-end cycle count execution workflow against the intended runtime.
  Proposed minimal fix: Create/configure the expected PostgreSQL database for API startup and rerun the exact token + record-count curl sequence.
- Timestamp: 2026-02-12T21:50:32Z
  TaskId: PRD-1614
  Type: TEST-GAP
  Evidence: Validation calls `GET /api/warehouse/v1/cycle-counts/{id}/discrepancies` and `POST /api/warehouse/v1/cycle-counts/{id}/approve-adjustment` both returned HTTP 403 on localhost (`/tmp/prd1614-discrepancies.status`, `/tmp/prd1614-approve.status`), while project API runtime remains blocked by missing DB.
  Impact: Could not verify runtime authorization and end-to-end discrepancy approval flow with real auth tokens/data.
  Proposed minimal fix: Start the project API against the expected PostgreSQL database and rerun discrepancy/approval curl validations using a valid manager/CFO token.
- Timestamp: 2026-02-12T21:59:50Z
  TaskId: PRD-1615
  Type: TEST-GAP
  Evidence: Validation command `dotnet run --project src/LKvitai.MES.WebUI` failed with missing HTTPS developer certificate (`/tmp/prd1615-webui-run.log`), with stack ending at `src/LKvitai.MES.WebUI/Program.cs:59`; HTTP fallback startup succeeded via `DOTNET_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5001 dotnet run --no-launch-profile --project src/LKvitai.MES.WebUI /p:UseAppHost=false`, but browser scenario walkthroughs could not be executed in this terminal-only run.
  Impact: Full manual acceptance verification for schedule, execute, and discrepancy approval pages remains pending.
  Proposed minimal fix: Install/trust local dev HTTPS certificate and run the documented browser validation on `/warehouse/cycle-counts`, `/warehouse/cycle-counts/schedule`, `/warehouse/cycle-counts/{id}/execute`, and `/warehouse/cycle-counts/{id}/discrepancies`.
