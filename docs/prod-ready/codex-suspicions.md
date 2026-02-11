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
