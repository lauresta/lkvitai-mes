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
