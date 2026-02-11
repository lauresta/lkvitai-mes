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
