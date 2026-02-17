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
- Timestamp: 2026-02-12T22:04:21Z
  TaskId: PRD-1616
  Type: INCONSISTENCY
  Evidence: `docs/prod-ready/prod-ready-tasks-PHASE15-S7.md` specifies preview endpoint `POST /api/warehouse/v1/labels/preview`, while Universe Epic G in `docs/prod-ready/prod-ready-universe.md` lists preview as `GET /api/warehouse/v1/labels/preview`.
  Impact: Implementers may wire incompatible client/server methods for label preview.
  Proposed minimal fix: Align universe/API catalog to POST for preview payload-based rendering (or formally document both POST and legacy GET compatibility).
- Timestamp: 2026-02-12T22:04:21Z
  TaskId: PRD-1616
  Type: TEST-GAP
  Evidence: Task validation curl calls returned HTTP 403 on localhost (`/tmp/prd1616-dev-token.status`, `/tmp/prd1616-preview.status`), so the required runtime token+preview flow could not be verified against the project API instance.
  Impact: End-to-end API validation for `POST /api/warehouse/v1/labels/preview` remains unconfirmed in this environment.
  Proposed minimal fix: Start the project API on an isolated port with valid auth/database connectivity and rerun the exact PRD-1616 curl sequence to produce a PDF artifact.
- Timestamp: 2026-02-12T22:09:29Z
  TaskId: PRD-1617
  Type: INCONSISTENCY
  Evidence: Task `Requirements` specify retry delay of 1 second between attempts (`RetryDelayMs`), while `Definition of Done` for the same task states "Retry logic with exponential backoff."
  Impact: Different retry policy interpretations may lead to nondeterministic operational behavior and mismatched tests.
  Proposed minimal fix: Normalize the task to one policy (fixed delay or exponential) and explicitly define expected delays per retry attempt.
- Timestamp: 2026-02-12T22:09:29Z
  TaskId: PRD-1617
  Type: TEST-GAP
  Evidence: Validation calls for token and print returned HTTP 403 on localhost (`/tmp/prd1617-dev-token.status`, `/tmp/prd1617-print.status`), preventing end-to-end verification of TCP printer/fallback response behavior against the project API runtime.
  Impact: Runtime confirmation of `POST /api/warehouse/v1/labels/print` printer retry/fallback flow is pending.
  Proposed minimal fix: Start project API with valid auth/database and run print validation against a TCP 9100 simulator or reachable Zebra printer endpoint.
- Timestamp: 2026-02-12T22:14:13Z
  TaskId: PRD-1618
  Type: TEST-GAP
  Evidence: Queue validation endpoints returned HTTP 403 on localhost (`/tmp/prd1618-queue.status`, `/tmp/prd1618-retry.status`), so runtime verification of queued job listing and manual retry could not be executed against the project API instance.
  Impact: End-to-end confirmation of `GET /api/warehouse/v1/labels/queue` and `POST /api/warehouse/v1/labels/queue/{id}/retry` remains pending.
  Proposed minimal fix: Run the project API with valid auth/database and execute queue list + retry validation against that runtime (ideally with seeded failed print jobs).
- Timestamp: 2026-02-12T22:20:00Z
  TaskId: PRD-1619
  Type: TEST-GAP
  Evidence: Full curl workflow for token/create/submit/approve/execute returned HTTP 403 on localhost (`/tmp/prd1619-token.status`, `/tmp/prd1619-create.status`, `/tmp/prd1619-submit.status`, `/tmp/prd1619-manager-token.status`, `/tmp/prd1619-approve.status`, `/tmp/prd1619-execute.status`), so runtime API flow could not be verified against the intended project service.
  Impact: End-to-end transfer workflow validation with real auth/runtime data remains pending.
  Proposed minimal fix: Start project API on known isolated port with valid auth/database and rerun the exact PRD-1619 curl sequence for create->submit->approve->execute.
- Timestamp: 2026-02-12T22:24:03Z
  TaskId: PRD-1620
  Type: MISSING-REF
  Evidence: UI requirement includes `Cancel` action in transfer list, but Sprint 7 transfer API contract for PRD-1619 does not define a cancel endpoint (`POST /api/warehouse/v1/transfers/{id}/cancel` absent in `src/LKvitai.MES.Api/Api/Controllers/TransfersController.cs`).
  Impact: Cancel action cannot be API-backed; UI can only provide placeholder behavior.
  Proposed minimal fix: Add transfer cancel endpoint/command/state transition (`DRAFT|PENDING_APPROVAL -> CANCELLED`) or remove cancel action from UI acceptance criteria.
- Timestamp: 2026-02-12T22:24:03Z
  TaskId: PRD-1620
  Type: TEST-GAP
  Evidence: Required validation command `dotnet run --project src/LKvitai.MES.WebUI` failed due missing HTTPS dev certificate (`/tmp/prd1620-webui-run.log`); fallback on `http://127.0.0.1:5001` failed with address in use, while alternate-port startup succeeded (`http://127.0.0.1:5010`), but browser/manual workflow checks were not executable in this terminal-only run.
  Impact: End-to-end manual verification of create/submit/approve/execute transfer UI scenarios remains pending.
  Proposed minimal fix: Free standard UI port or choose explicit available port, install/trust dev HTTPS cert, then run browser walkthrough for `/warehouse/transfers`, `/warehouse/transfers/create`, and `/warehouse/transfers/{id}/execute`.
- Timestamp: 2026-02-13T05:07:10Z
  TaskId: PRD-1621
  Type: INCONSISTENCY
  Evidence: `docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:126-127` posts `{"username":"admin","roles":["Admin"]}` to `/api/auth/dev-token`, but API contract requires `DevTokenRequest(string Username, string Password)` in `src/LKvitai.MES.Api/Security/IDevAuthService.cs:3`.
  Impact: Task validation token command cannot produce a valid bearer token for this repository contract.
  Proposed minimal fix: Align Sprint 8 validation payload to include `password` (or update dev-token endpoint contract to accept role override payload).
- Timestamp: 2026-02-13T05:07:10Z
  TaskId: PRD-1621
  Type: TEST-GAP
  Evidence: Task validation calls to `http://localhost:5000/api/warehouse/v1/admin/settings` returned `HTTP/1.1 403` from `Server: AirTunes/925.5.1` (`/tmp/prd1621-get.out:1-4`, `/tmp/prd1621-put.out:1-4`) instead of the project API.
  Impact: Could not execute required curl acceptance checks against the implemented endpoint in this environment.
  Proposed minimal fix: Run project API on an isolated known port and rerun the exact PRD-1621 curl sequence against that host/port.
- Timestamp: 2026-02-13T05:20:24Z
  TaskId: PRD-1622
  Type: AMBIGUITY
  Evidence: Task requires usage tracking for reason codes in adjustment and revaluation (`docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:168`), but valuation API contracts expose only free-text `Reason` fields and no structured `ReasonCode` (`src/LKvitai.MES.Api/Api/Controllers/ValuationController.cs:585-615`).
  Impact: Deterministic usage counting for revaluation reason codes is not guaranteed unless reason text equals a configured code.
  Proposed minimal fix: Add optional `reasonCode` to valuation write-down/revaluation requests and increment usage from that field; keep `reason` as descriptive text.
- Timestamp: 2026-02-13T05:20:24Z
  TaskId: PRD-1622
  Type: TEST-GAP
  Evidence: Required validation calls for `GET/POST /api/warehouse/v1/admin/reason-codes` returned `HTTP/1.1 403` from `Server: AirTunes/925.5.1` (`/tmp/prd1622-get.out:1-4`, `/tmp/prd1622-post.out:1-4`) instead of the project API.
  Impact: Could not execute task curl acceptance checks against the implemented endpoint in this environment.
  Proposed minimal fix: Start the project API on a known isolated port and rerun the exact PRD-1622 curl sequence with a valid token.
- Timestamp: 2026-02-13T05:25:52Z
  TaskId: PRD-1623
  Type: INCONSISTENCY
  Evidence: Task examples require `approverRole="Manager"` (`docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:278,297,304`), while runtime RBAC roles are defined as `WarehouseManager` (`src/LKvitai.MES.Api/Security/WarehouseRoles.cs:5-7`).
  Impact: Approval-rule role values may not align with actual authorization claims unless alias mapping is applied.
  Proposed minimal fix: Standardize task/API role values to `WarehouseManager` (or formally define accepted aliases such as `Manager -> WarehouseManager`).
- Timestamp: 2026-02-13T05:25:52Z
  TaskId: PRD-1623
  Type: TEST-GAP
  Evidence: Required validation calls for `POST /api/warehouse/v1/admin/approval-rules` and `/evaluate` returned `HTTP/1.1 403` from `Server: AirTunes/925.5.1` (`/tmp/prd1623-post-rule.out:1-4`, `/tmp/prd1623-evaluate.out:1-4`) instead of the project API runtime.
  Impact: Could not execute task curl acceptance checks against the implemented approval-rules endpoints in this environment.
  Proposed minimal fix: Run project API on a known isolated port and rerun the exact PRD-1623 curl sequence with a valid token.
- Timestamp: 2026-02-13T05:36:24Z
  TaskId: PRD-1624
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:408, docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:426, src/LKvitai.MES.Api/Api/Controllers/AdminUsersController.cs:11
  Impact: Task examples/documentation assume integer user IDs and a single `AdminController.cs`, while the codebase uses GUID user IDs and split admin controllers; implementing literal examples would break compatibility with existing admin user model.
  Proposed minimal fix: Keep GUID-based `/api/warehouse/v1/admin/users/{userId}/roles` and implement role CRUD in `AdminRolesController`, then update task text to reference GUID IDs and controller split.

- Timestamp: 2026-02-13T05:36:24Z
  TaskId: PRD-1624
  Type: TEST-GAP
  Evidence: Validation curl commands to http://localhost:5000/api/warehouse/v1/admin/roles returned `HTTP/1.1 403 Forbidden` with `Server: AirTunes/925.5.1` (see `/tmp/prd1624_roles_get.headers`, `/tmp/prd1624_roles_post.headers`).
  Impact: Task-mandated API validation could not verify PRD-1624 endpoints because localhost:5000 is occupied by a non-project service in this environment.
  Proposed minimal fix: Run validation against a started LKvitai.MES.Api instance on its bound port (or rebind project to 5000) and replay the same curl commands.
- Timestamp: 2026-02-13T05:46:21Z
  TaskId: PRD-1625
  Type: TEST-GAP
  Evidence: `dotnet run --project src/LKvitai.MES.WebUI/LKvitai.MES.WebUI.csproj` fails at startup with `Unable to configure HTTPS endpoint... default developer certificate could not be found`.
  Impact: Task validation command cannot launch WebUI as documented, so manual navigation/form validation could not be executed with the default run profile.
  Proposed minimal fix: Install/trust a local dev certificate (`dotnet dev-certs https --trust`) or run with a non-HTTPS development profile.

- Timestamp: 2026-02-13T05:46:21Z
  TaskId: PRD-1625
  Type: TEST-GAP
  Evidence: Running WebUI with `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5001 dotnet run --no-launch-profile ...` starts, but page requests to `/warehouse/admin/*` return HTTP 500 because server-side rendering calls `https://localhost:5001/api/warehouse/v1/admin/*` and fails SSL handshake (see `/tmp/prd1625__warehouse_admin_settings.html` and runtime logs).
  Impact: Manual CRUD scenario validation for settings/reason-codes/approval-rules/roles is blocked in this environment due API connectivity/profile mismatch.
  Proposed minimal fix: Run WebUI against a reachable Warehouse API base URL and matching scheme/port, then execute the manual scenarios.
- Timestamp: 2026-02-13T05:56:36Z
  TaskId: PRD-1626
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1046-1050, src/LKvitai.MES.Api/Security/WarehousePolicies.cs:6
  Impact: Task scenario maps OAuth claim to role `Manager`, but runtime authorization policies protect manager endpoints with `WarehouseManager`; direct `Manager` mapping would not satisfy existing policy checks.
  Proposed minimal fix: Normalize OAuth role aliases (`Manager` -> `WarehouseManager`, `Admin` -> `WarehouseAdmin`) while preserving original mapped role claim.

- Timestamp: 2026-02-13T05:56:36Z
  TaskId: PRD-1626
  Type: TEST-GAP
  Evidence: Validation calls to `http://localhost:5000/api/auth/oauth/login` and `http://localhost:5000/api/warehouse/v1/items` returned `HTTP/1.1 403 Forbidden` with `Server: AirTunes/925.5.1`.
  Impact: Task-specified manual OAuth browser flow and expired-token curl verification could not be executed against LKvitai.MES.Api in this environment.
  Proposed minimal fix: Run validation against a live LKvitai.MES.Api instance with configured OAuth provider credentials (Azure AD/Okta) on the expected port.
- Timestamp: 2026-02-13T06:12:17Z
  TaskId: PRD-1627
  Type: AMBIGUITY
  Evidence: `docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1104` states MFA reset "requires approval", but API contract only declares `POST /api/auth/mfa/reset/{userId}` as admin-only (`docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1130`) with no approval payload schema.
  Impact: Implementations can diverge on how approval is captured/audited, causing incompatible clients.
  Proposed minimal fix: Extend task API contract with explicit approval fields (for example `approved`, `reason`, `approvedBy`) and expected failure behavior when missing.

- Timestamp: 2026-02-13T06:12:17Z
  TaskId: PRD-1627
  Type: TEST-GAP
  Evidence: Task validation curl calls to `http://localhost:5000/api/auth/mfa/*` returned `HTTP/1.1 403 Forbidden` from `Server: AirTunes/925.5.1` (see `/tmp/prd1627-enroll.headers`, `/tmp/prd1627-verify-enrollment.headers`, `/tmp/prd1627-backup-codes.headers`).
  Impact: Task-mandated runtime validation could not be executed against LKvitai.MES.Api in this environment.
  Proposed minimal fix: Run LKvitai.MES.Api on an isolated known port and rerun the PRD-1627 curl validation sequence with a valid bearer token.
- Timestamp: 2026-02-14T00:00:00Z
  TaskId: PRD-1634
  Type: TEST-GAP
  Evidence: SMTP delivery for scheduled compliance reports is configurable but not validated in this environment (no SMTP host/credentials provided). See src/LKvitai.MES.Api/Services/ComplianceReportService.cs.
  Impact: Automated email delivery of scheduled PDF/CSV reports remains unverified; reports are generated and stored locally only.
  Proposed minimal fix: Configure `Compliance:Reports:Smtp` settings with reachable SMTP server and rerun scheduled report trigger to confirm email dispatch, or add integration test with mock SMTP server.
- Timestamp: 2026-02-13T06:19:42Z
  TaskId: PRD-1628
  Type: INCONSISTENCY
  Evidence: Task acceptance/validation references `POST /api/warehouse/v1/orders` (`docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1265`), while repository order write endpoint is `src/LKvitai.MES.Api/Api/Controllers/SalesOrdersController.cs:16` (`/api/warehouse/v1/sales-orders`).
  Impact: Scope-validation scenarios can target a non-existent route if implemented literally.
  Proposed minimal fix: Align task text to repository route (`/api/warehouse/v1/sales-orders`) or explicitly require alias route support.

- Timestamp: 2026-02-13T06:19:42Z
  TaskId: PRD-1628
  Type: TEST-GAP
  Evidence: Validation curl calls to `http://localhost:5000/api/warehouse/v1/admin/api-keys` and `/api/warehouse/v1/items` returned `HTTP/1.1 403 Forbidden` from `Server: AirTunes/925.5.1` (see `/tmp/prd1628-generate.headers`, `/tmp/prd1628-use.headers`).
  Impact: Task-specified runtime API-key generation and usage checks were not executable against LKvitai.MES.Api in this environment.
  Proposed minimal fix: Run LKvitai.MES.Api on an isolated known port and rerun PRD-1628 curl validation with a valid admin token and generated API key.
- Timestamp: 2026-02-13T06:25:21Z
  TaskId: PRD-1629
  Type: INCONSISTENCY
  Evidence: Permission-check API examples use numeric `UserId=5` and `OwnerId=10` (`docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1349,1355`), while repository user identifiers are GUIDs (`src/LKvitai.MES.Api/Security/AdminUserStore.cs:8` and role assignment model in `src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:1222`).
  Impact: Literal task payloads do not match runtime contracts and can produce invalid-request behavior.
  Proposed minimal fix: Update task examples/contracts to GUID user IDs (or introduce explicit numeric-to-GUID mapping layer).

- Timestamp: 2026-02-13T06:25:21Z
  TaskId: PRD-1629
  Type: TEST-GAP
  Evidence: Validation curl call to `http://localhost:5000/api/warehouse/v1/admin/permissions/check` returned `HTTP/1.1 403 Forbidden` from `Server: AirTunes/925.5.1` (`/tmp/prd1629-check.headers`).
  Impact: Task-specified runtime permission-check validation could not be executed against LKvitai.MES.Api in this environment.
  Proposed minimal fix: Run LKvitai.MES.Api on an isolated known port and rerun the PRD-1629 curl command with a valid admin token.
- Timestamp: 2026-02-13T06:33:42Z
  TaskId: PRD-1630
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1420, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:1265
  Impact: Task data model specifies `AuditLog.UserId` as nullable int, but system identity uses string subject/user ids across auth flows; forcing int would break OAuth/API-key actor identifiers.
  Proposed resolution: Keep `UserId` as nullable string in audit log schema and clarify spec model to identity-subject string.

- Timestamp: 2026-02-13T06:33:42Z
  TaskId: PRD-1630
  Type: TEST-GAP
  Evidence: /tmp/prd1630-create.headers:1, /tmp/prd1630-query.headers:1 (HTTP/1.1 403, Server: AirTunes), /tmp/token.txt missing
  Impact: Task-specified localhost API curl validation for audit log creation/query could not be executed against project API in this environment.
  Proposed resolution: Run validations against a reachable LKvitai API host/port with generated dev token (`/api/auth/dev-token`) and valid bearer token.
- Timestamp: 2026-02-13T06:42:48Z
  TaskId: PRD-1631
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:532, src/LKvitai.MES.Api/Services/TransactionExportService.cs:120, src/LKvitai.MES.Api/Services/TransactionExportService.cs:124
  Impact: Spec requires `UserId` in exported event rows, but Marten raw events do not guarantee a normalized user-id field; exports may contain null `UserId` for events without `X-User-Id` header metadata.
  Proposed resolution: Define canonical event metadata contract for actor identity (required header key) and backfill strategy for historical events.

- Timestamp: 2026-02-13T06:42:48Z
  TaskId: PRD-1631
  Type: TEST-GAP
  Evidence: /tmp/prd1631-export.headers:1, /tmp/prd1631-history.headers:1 (HTTP/1.1 403, Server: AirTunes), /tmp/token.txt missing
  Impact: Task curl validation against `localhost:5000` could not hit LKvitai API, so live endpoint validation for export/history remained unverified in this environment.
  Proposed resolution: Run validation against a reachable API host and generate token via `/api/auth/dev-token` before invoking compliance export endpoints.
- Timestamp: 2026-02-13T07:15:00Z
  TaskId: PRD-1632
  Type: TEST-GAP
  Evidence: `dotnet build src/LKvitai.MES.sln -v minimal` and repeated `dotnet restore` (with --disable-parallel, --ignore-failed-sources, NUGET_CERT_REVOCATION_MODE=offline) hung without emitting progress for several minutes and required manual Ctrl+C.
  Impact: Unable to complete required `dotnet build` / `dotnet test` validations for PRD-1632 during this session; build status remains unverified.
  Proposed resolution: Re-run restore/build in an environment with NuGet feed access or warmed package cache; if offline, configure local package source/credentials and retry validations.
- Timestamp: 2026-02-13T19:05:00Z
  TaskId: PRD-1632
  Type: AMBIGUITY
  Evidence: Full `dotnet restore`, `dotnet build`, and `dotnet test` succeeded after network access was available; previous TEST-GAP entry for PRD-1632 no longer reproduces.
  Impact: Validation for PRD-1632 is now confirmed; prior gap can be treated as resolved for this environment.
  Proposed resolution: None. Leave historical TEST-GAP entry for traceability; no further action required.
- Timestamp: 2026-02-17T20:54:01Z
  TaskId: PRD-1636
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1489-1493
  Impact: Retention requirements list multiple data types (EVENTS, PROJECTIONS, AUDIT_LOGS, CUSTOMER_DATA) but archival/deletion mechanics are only concretely defined for events/audit logs; project has no existing generalized retention substrate for projections/customer data.
  Proposed resolution: Minimal safe implementation processes AUDIT_LOGS end-to-end (policy CRUD, archive, delete, legal hold) and provisions events archive schema; extend execution handlers per data type in follow-up PRDs.

- Timestamp: 2026-02-17T20:54:01Z
  TaskId: PRD-1636
  Type: TEST-GAP
  Evidence: dotnet test and dotnet vstest fail in sandbox with System.Net.Sockets.SocketException (13) Permission denied while test platform opens local socket
  Impact: Full automated test execution cannot run in this environment despite successful compile.
  Proposed resolution: Run dotnet test in CI or local environment permitting loopback socket bind for testhost.

- Timestamp: 2026-02-17T20:54:01Z
  TaskId: PRD-1636
  Type: TEST-GAP
  Evidence: Spec validation requires authenticated curl calls to /api/warehouse/v1/admin/retention-policies and /execute; no running API/token in current CLI session.
  Impact: Endpoint-level manual validation scenarios were not executed.
  Proposed resolution: Start API with reachable DB and execute the documented curl flow with admin token.
- Timestamp: 2026-02-17T21:07:02Z
  TaskId: PRD-1637
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1595-1600
  Impact: Spec requires key storage in Azure Key Vault or environment variable and key rotation with 30-day grace, but no Key Vault integration contract/config exists in repository.
  Proposed resolution: Implemented environment-variable keyring plus runtime-generated key rotation metadata (`pii_encryption_keys` table) and 30-day grace tracking; external vault integration can replace runtime key provider without schema changes.

- Timestamp: 2026-02-17T21:07:02Z
  TaskId: PRD-1637
  Type: TEST-GAP
  Evidence: dotnet test and dotnet vstest fail in sandbox with System.Net.Sockets.SocketException (13) Permission denied while test platform opens local socket
  Impact: Automated test execution for encryption/rotation scenarios cannot run in this environment.
  Proposed resolution: Execute dotnet test in CI or local environment that permits loopback socket bind.

- Timestamp: 2026-02-17T21:07:02Z
  TaskId: PRD-1637
  Type: TEST-GAP
  Evidence: Spec validation requires SQL inspection of ciphertext and authenticated POST /api/warehouse/v1/admin/encryption/rotate-key against running API.
  Impact: End-to-end runtime/key-rotation verification was not executed in this CLI-only session.
  Proposed resolution: Run API with DB connectivity, create customer via API, inspect ciphertext in DB, then trigger rotation endpoint and verify re-encryption completion.
- Timestamp: 2026-02-17T21:25:48Z
  TaskId: PRD-1638
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1694-1696
  Impact: Data discovery/anonymization scope mentions SalesOrders, Shipments, AuditLogs, but current domain model stores customer-facing names mainly in customer/sales-order addresses and summary read models; shipment entities do not directly reference customer IDs.
  Proposed resolution: Minimal safe implementation anonymizes customer PII, sales-order shipping addresses, and customer-name summary projections (outbound/shipment summaries), with immutable audit records for workflow execution.

- Timestamp: 2026-02-17T21:25:48Z
  TaskId: PRD-1638
  Type: TEST-GAP
  Evidence: dotnet test and dotnet vstest fail in sandbox with System.Net.Sockets.SocketException (13) Permission denied while test platform opens local socket
  Impact: Automated test execution for erasure workflow cannot run in this environment.
  Proposed resolution: Execute dotnet test in CI or local environment that permits loopback socket bind.

- Timestamp: 2026-02-17T21:25:48Z
  TaskId: PRD-1638
  Type: TEST-GAP
  Evidence: Spec validation requires authenticated POST/PUT calls for erasure request/approval plus email confirmation checks against running API.
  Impact: End-to-end workflow and notification validation were not executed in this CLI-only session.
  Proposed resolution: Run API with admin/customer tokens, execute request→approve flow, and verify anonymized persisted data + confirmation channel in runtime environment.
- Timestamp: 2026-02-17T22:06:45Z
  TaskId: PRD-1639
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1795-1798, docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1802-1805
  Impact: Spec requires blob storage (Azure/S3), PITR WAL workflow, and monthly automated restore validation, but deployment/storage provider contracts are not defined in repository configuration.
  Proposed resolution: Implemented provider-agnostic backup execution tracking + local artifact generation scripts/runbook, scheduled backup/restore-test jobs, and endpoint contracts; external storage/wal destinations can be wired through environment-specific scripts.

- Timestamp: 2026-02-17T22:06:45Z
  TaskId: PRD-1639
  Type: TEST-GAP
  Evidence: dotnet test and dotnet vstest fail in sandbox with System.Net.Sockets.SocketException (13) Permission denied while test platform opens local socket
  Impact: Automated backup service tests cannot run in this environment.
  Proposed resolution: Execute dotnet test in CI or local environment that permits loopback socket bind.

- Timestamp: 2026-02-17T22:06:45Z
  TaskId: PRD-1639
  Type: TEST-GAP
  Evidence: scripts/backup/restore_from_backup.sh output: "psql unavailable; restore dry-run only"; API curl validations for backup endpoints not executed (no running API/token).
  Impact: Real pg_dump/restore and authenticated API backup workflow were not validated end-to-end.
  Proposed resolution: Run backup/restore scripts in DB-enabled environment with pg_dump/psql and execute the endpoint flow against running API.
- Timestamp: 2026-02-17T22:34:01Z
  TaskId: PRD-1640
  Type: AMBIGUITY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1912, src/LKvitai.MES.Api/Security/WarehouseRoles.cs:1
  Impact: Spec requires DevOps/Admin RBAC, but repository role/policy catalog has no DevOps role constant or policy.
  Proposed resolution: Enforce existing Admin policy for DR endpoints now and introduce an explicit DevOps role/policy in a dedicated RBAC extension task.

- Timestamp: 2026-02-17T22:34:01Z
  TaskId: PRD-1640
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S8.md:1952, local run had no authenticated running API for curl drill endpoints.
  Impact: Manual endpoint validation for POST /api/warehouse/v1/admin/dr/drill and GET /api/warehouse/v1/admin/dr/drills was not executed end-to-end.
  Proposed resolution: Run API with dev token auth and execute documented curl flow in a DB-enabled environment.

- Timestamp: 2026-02-17T22:34:01Z
  TaskId: PRD-1640
  Type: TEST-GAP
  Evidence: dotnet test src/LKvitai.MES.sln failed in pre-existing tests under src/LKvitai.MES.Infrastructure/Persistence/PiiEncryption.cs:63 (Destination is too short).
  Impact: Full-solution regression suite is red from unrelated failures, so green baseline cannot be confirmed from this run.
  Proposed resolution: Fix/stabilize existing PII encryption tests, then rerun full solution tests.
- Timestamp: 2026-02-17T22:38:25Z
  TaskId: PRD-1641
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S9.md:84-90, src/LKvitai.MES.Domain/Entities/MasterDataEntities.cs:14-33
  Impact: PRD requires `items.supplier_id` index, but `items` entity/table has no supplier FK column in this repository.
  Proposed resolution: Use `supplier_item_mappings.SupplierId` as the supplier lookup index surface (`idx_items_supplier_id`) and add a direct `items.supplier_id` only if data model is redesigned.

- Timestamp: 2026-02-17T22:38:25Z
  TaskId: PRD-1641
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S9.md:173-208 validation commands require `psql`, `k6`, and live DB telemetry (`pg_stat_statements`), which are not available in this run.
  Impact: Slow-query-log analysis, EXPLAIN ANALYZE timing baselines, and load-test SLA verification were not executed end-to-end.
  Proposed resolution: Run the documented psql/k6 workflow in a DB-enabled perf environment and capture before/after metrics into `docs/performance/query-plans.md`.

- Timestamp: 2026-02-17T22:38:25Z
  TaskId: PRD-1641
  Type: TEST-GAP
  Evidence: dotnet test src/LKvitai.MES.sln failed in pre-existing tests under src/LKvitai.MES.Infrastructure/Persistence/PiiEncryption.cs:63 (Destination is too short).
  Impact: Full-solution regression remains red from unrelated tests.
  Proposed resolution: Stabilize existing PII encryption tests, then rerun full solution tests.
- Timestamp: 2026-02-17T22:48:30Z
  TaskId: PRD-1642
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S9.md:233-239, repository root contains `docker-compose.test.yml` but not baseline `docker-compose.yml` before this task.
  Impact: Spec assumes an existing compose stack to extend with Redis; repository required creation of a new compose file with Redis-only service.
  Proposed resolution: Treat `docker-compose.yml` as cache-dev runtime baseline and merge with environment-specific compose overlays as needed.

- Timestamp: 2026-02-17T22:48:30Z
  TaskId: PRD-1642
  Type: INCONSISTENCY
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S9.md:263-268, src/LKvitai.MES.Api/Api/Controllers/ValuationController.cs:253
  Impact: Spec key pattern `value:{itemId}` implies direct item-id query path, but current on-hand-value endpoint has no itemId filter parameter.
  Proposed resolution: Populate/invalidate `value:{itemId}` keys during projection/query flows and add explicit itemId query support in a follow-up API refinement.

- Timestamp: 2026-02-17T22:48:30Z
  TaskId: PRD-1642
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S9.md:342-387 requires live Redis/API load validation (`redis-cli`, k6, end-to-end latency checks), not fully executable in this run.
  Impact: Cache hit-rate and latency SLA targets (>80% hit, <5ms p95) were not empirically verified under load.
  Proposed resolution: Run documented Redis + k6 workflow in a perf environment and capture results in dashboard/metrics artifacts.

- Timestamp: 2026-02-17T22:48:30Z
  TaskId: PRD-1642
  Type: TEST-GAP
  Evidence: dotnet test src/LKvitai.MES.sln failed in pre-existing tests under src/LKvitai.MES.Infrastructure/Persistence/PiiEncryption.cs:63 (Destination is too short).
  Impact: Full-solution regression remains red from unrelated tests.
  Proposed resolution: Stabilize existing PII encryption tests, then rerun full solution tests.
- Timestamp: 2026-02-17T22:52:59Z
  TaskId: PRD-1643
  Type: TEST-GAP
  Evidence: docs/prod-ready/prod-ready-tasks-PHASE15-S9.md:508-535 requires live `k6`/`psql`/running API validation for 1000 VUs and pool behavior; not executable in this run.
  Impact: Pool exhaustion and p95 acquisition SLA (<10ms) were not empirically validated under load.
  Proposed resolution: Run the documented load workflow in a DB-backed perf environment and capture metrics snapshots during test.

- Timestamp: 2026-02-17T22:52:59Z
  TaskId: PRD-1643
  Type: TEST-GAP
  Evidence: dotnet test src/LKvitai.MES.sln failed in pre-existing tests under src/LKvitai.MES.Infrastructure/Persistence/PiiEncryption.cs:63 (Destination is too short).
  Impact: Full-solution regression remains red from unrelated tests.
  Proposed resolution: Stabilize existing PII encryption tests, then rerun full solution tests.
