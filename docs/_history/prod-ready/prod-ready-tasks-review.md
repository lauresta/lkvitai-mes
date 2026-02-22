# Production-Ready Task Plan Review

**Reviewer:** Claude (Senior Technical Program Manager + Solution Architect)
**Date:** February 10, 2026
**Review Scope:** All task documents in `/docs/prod-ready/prod-ready-tasks*.md`
**Source of Truth:** `prod-ready-universe.md` (2573 lines)

---

## Executive Verdict: NOT OK

### Why NOT OK (Critical Issues Found)

1. **Incomplete Epic Coverage**: Only 4 epics have detailed task specifications (Foundation, C, A, and partial B). Epics D-Q have ONLY task index (title, estimate, dependencies) with NO detailed requirements, acceptance criteria, or implementation notes.

2. **Task Detail Incompleteness**: 150+ tasks (80%) are listed in master index with no detailed specification. Developers cannot execute tasks like PRD-0200 to PRD-1720 without re-reading the entire universe document.

3. **Missing Self-Contained Context**: Only 3 tasks (PRD-0001, PRD-0100, PRD-0300/PRD-0304) have the required "Context" section with 3-8 bullets. Remaining 177+ tasks lack minimum background for execution.

4. **No Acceptance Criteria for 95% of Tasks**: Only 1 task (PRD-0001) has complete Gherkin scenarios. Tasks PRD-0100, PRD-0300, PRD-0304 have partial scenarios. All other tasks have NO acceptance criteria.

5. **Missing Validation Commands**: Only 1 task (PRD-0001) has executable validation commands (curl, psql, dotnet test). Remaining 179+ tasks lack verification instructions.

6. **Inconsistent SourceRefs**: Many tasks reference only "Universe §4.Epic X" without specific section numbers (e.g., should be "Universe §4.Epic C, Entities & Data Model Changes, lines 1295-1350").

7. **Foundation Tasks Incomplete**: Only PRD-0001 has full detail. PRD-0002 to PRD-0010 have NO detailed specs despite being critical blockers for all Phase 1.5 work.

8. **Missing Cross-Cutting Tasks**: No tasks for event catalog validation, API contract consistency checks, state machine validation, or schema migration sequencing.

---

## Must-Fix Issues (Blockers)

### 1. Complete Foundation Task Details (PRD-0002 to PRD-0010)

**Problem:** Foundation tasks (PRD-0002 to PRD-0010) are listed in index but have NO detailed specifications. These are critical blockers for Phase 1.5.

**Impact:** Developers cannot start Phase 1.5 work. Cannot implement Valuation, Agnum, Outbound, Sales Orders without foundation infrastructure.

**Files Affected:** `prod-ready-tasks-02-foundation.md`

**Current State:** File contains only PRD-0001 (Idempotency) fully detailed. Tasks PRD-0002 to PRD-0010 missing.

**Fix Instruction:**
- Add detailed task specifications for PRD-0002 (Event Schema Versioning) through PRD-0010 (Backup & Disaster Recovery)
- Each task MUST include: Context (3-8 bullets), Scope (In/Out), Requirements (Functional + Non-Functional + Data Model + API Changes), Acceptance Criteria (3-5 Gherkin scenarios), Implementation Notes, Validation/Checks, Definition of Done
- Use PRD-0001 as template (168 lines)
- Total addition: ~1200 lines (9 tasks × 130 lines avg)

---

### 2. Complete Valuation Task Details (PRD-0101 to PRD-0120)

**Problem:** PRD-0100 has detailed spec (~150 lines), but PRD-0101 to PRD-0120 (19 tasks) have NO details.

**Impact:** Cannot implement Valuation epic (4 weeks of work). Blocks Agnum integration.

**Files Affected:** `prod-ready-tasks-03-valuation.md`

**Current State:** File has PRD-0100 partially detailed (events schema only, missing acceptance criteria). PRD-0101+ missing entirely.

**Fix Instruction:**
- Complete PRD-0100: add Acceptance Criteria (Gherkin), Validation/Checks, DoD
- Add detailed specs for PRD-0101 to PRD-0120 (19 tasks)
- For each: Context, Scope, Requirements (with SQL schema for aggregates/projections, API endpoint specs with request/response shapes, UI wireframe descriptions), Acceptance Criteria (3-5 scenarios), Validation commands, DoD checklist
- Prioritize: PRD-0101 (Aggregate), PRD-0102 (Adjust Cost), PRD-0105 (OnHandValue Projection), PRD-0108 (API Endpoints) — these are critical path
- Total addition: ~2500 lines (20 tasks × 125 lines avg)

---

### 3. Complete Outbound Task Details (PRD-0301 to PRD-0325)

**Problem:** PRD-0300 and PRD-0304 have partial specs (~150 lines each), but PRD-0301 to PRD-0303, PRD-0305 to PRD-0325 (23 tasks) have NO details.

**Impact:** Cannot implement Outbound epic (3 weeks of work). Blocks Sales Orders.

**Files Affected:** `prod-ready-tasks-04-outbound.md`

**Current State:** File has PRD-0300 (OutboundOrder Entity) and PRD-0304 (Pack Order Command) with partial detail. Missing acceptance criteria, validation, and 23 other tasks.

**Fix Instruction:**
- Complete PRD-0300 and PRD-0304: add Acceptance Criteria (Gherkin), Validation/Checks, DoD
- Add detailed specs for PRD-0301 to PRD-0303, PRD-0305 to PRD-0325 (23 tasks)
- Prioritize critical path: PRD-0301 (Shipment Entity), PRD-0302/0303 (State Machines), PRD-0306 (Dispatch Command), PRD-0308 (Events), PRD-0312 (API Endpoints)
- For UI tasks (PRD-0313 to PRD-0316): include wireframe descriptions, form field specs, validation rules, error states
- For integration tasks (PRD-0309, PRD-0325): include API contract specs, retry logic, idempotency keys, error handling
- Total addition: ~3000 lines (25 tasks × 120 lines avg)

---

### 4. Complete Sales Orders Task Details (PRD-0400 to PRD-0425)

**Problem:** Epic B (Sales Orders) has only task index (26 tasks), NO detailed specs.

**Impact:** Cannot implement Sales Orders (3 weeks of work). Blocks Phase 1.5 completion.

**Files Affected:** NEW FILE REQUIRED: `prod-ready-tasks-05-sales-orders.md`

**Current State:** Does not exist.

**Fix Instruction:**
- Create new file: `prod-ready-tasks-05-sales-orders.md`
- Add detailed specs for PRD-0400 to PRD-0425 (26 tasks)
- Prioritize: PRD-0400 (Customer Entity), PRD-0401 (SalesOrder Entity), PRD-0402 (State Machine), PRD-0403 (Create Command), PRD-0404 (Allocation Saga), PRD-0409 (API Endpoints)
- For Allocation Saga (PRD-0404): include saga state machine diagram (ASCII), step-by-step flow, compensation logic, retry strategy, conflict resolution
- For Customer entity (PRD-0400): include credit limit validation, payment terms enum, address value object schema
- Total addition: ~3200 lines (26 tasks × 123 lines avg)

---

### 5. Complete Epic D (Agnum Integration) Task Details (PRD-0200 to PRD-0215)

**Problem:** Epic D has only task index (16 tasks), NO detailed specs.

**Impact:** Cannot implement Agnum integration (2 weeks of work). Blocks financial reconciliation.

**Files Affected:** NEW FILE REQUIRED: `prod-ready-tasks-06-agnum.md`

**Current State:** Does not exist.

**Fix Instruction:**
- Create new file: `prod-ready-tasks-06-agnum.md`
- Add detailed specs for PRD-0200 to PRD-0215 (16 tasks)
- Prioritize: PRD-0200 (Config Model), PRD-0202 (Export Saga), PRD-0203 (CSV Generation), PRD-0204 (API Integration)
- For Export Saga (PRD-0202): include saga flow diagram, query logic (JOIN AvailableStock + ItemValuation + LogicalWarehouse), CSV format example (from universe Appendix D), retry logic
- For CSV Generation (PRD-0203): include exact CSV schema (columns, data types, delimiters), example rows (from universe)
- For API Integration (PRD-0204): include Agnum API contract (POST endpoint, JSON schema, idempotency headers), mock implementation for testing
- Total addition: ~2000 lines (16 tasks × 125 lines avg)

---

### 6. Complete Epic E (3D Visualization) Task Details (PRD-0500 to PRD-0515)

**Problem:** Epic E has only task index (16 tasks), NO detailed specs.

**Impact:** Cannot implement 3D Visualization (2 weeks of work). Blocks core value proposition (visual warehouse).

**Files Affected:** NEW FILE REQUIRED: `prod-ready-tasks-07-3d-viz.md`

**Current State:** Does not exist.

**Fix Instruction:**
- Create new file: `prod-ready-tasks-07-3d-viz.md`
- Add detailed specs for PRD-0500 to PRD-0515 (16 tasks)
- Prioritize: PRD-0500 (Location Coordinates Schema), PRD-0502 (3D API Endpoint), PRD-0504 (Three.js Integration), PRD-0506 (Click-to-Details)
- For PRD-0500: include SQL migration (ALTER TABLE locations ADD COLUMN coordinate_x, coordinate_y, coordinate_z), validation (coords must be unique in 3D space)
- For PRD-0502: include API response schema (bins array with coords, colors, HU details), JSON example
- For PRD-0504: include Three.js setup code (scene, camera, renderer), bin rendering logic (BoxGeometry), color mapping (status → hex color)
- For PRD-0506: include raycasting logic (click detection), detail panel spec (location code, capacity, HUs, items)
- Total addition: ~2000 lines (16 tasks × 125 lines avg)

---

### 7. Add Acceptance Criteria to All Tasks

**Problem:** 179 out of 180 tasks have NO Gherkin acceptance criteria. Only PRD-0001 has scenarios.

**Impact:** QA cannot write test cases. Developers don't know when task is "done".

**Files Affected:** ALL task detail files (02-foundation, 03-valuation, 04-outbound, plus NEW files 05-sales, 06-agnum, 07-3d-viz, and future Phase 2-4 files)

**Fix Instruction:**
- For EACH task, add Acceptance Criteria section with 3-5 Gherkin scenarios
- Template:
  ```gherkin
  Scenario: <Happy path>
    Given <precondition>
    When <action>
    Then <expected result>
    And <assertion>

  Scenario: <Error case>
    Given <precondition>
    When <invalid action>
    Then <error message>
    And <rollback verification>

  Scenario: <Edge case or concurrency>
    Given <edge condition>
    When <action>
    Then <expected behavior>
  ```
- Minimum 3 scenarios per task: happy path, error case, edge case
- For saga tasks: add idempotency scenario (replay after crash)
- For projection tasks: add rebuild scenario (replay events)
- For API tasks: add validation scenario (invalid input)

---

### 8. Add Validation Commands to All Tasks

**Problem:** 179 out of 180 tasks have NO executable validation commands. Only PRD-0001 has curl/psql examples.

**Impact:** Developers cannot locally verify task completion. No smoke test instructions.

**Files Affected:** ALL task detail files

**Fix Instruction:**
- For EACH task, add "Validation / Checks" section with:
  - **Local Testing:** 3-5 bash commands (dotnet test, psql queries, curl API calls, file checks)
  - **Metrics:** List of Prometheus metrics that must be exposed (counters, gauges, histograms)
  - **Logs:** Expected log messages (INFO, WARN, ERROR with correlation IDs)
  - **Backwards Compatibility:** Checks for schema changes (e.g., `SELECT column_name FROM information_schema.columns WHERE table_name='...'`)
- Example (for every task):
  ```bash
  # Run unit tests
  dotnet test --filter "TaskId=PRD-XXXX"

  # Verify DB schema
  psql -d warehouse -c "SELECT * FROM <table> LIMIT 1;"

  # Test API endpoint
  curl -X POST /api/.../... -d '{"...":"..."}' -H "Content-Type: application/json"

  # Check metrics endpoint
  curl http://localhost:5000/metrics | grep <metric_name>
  ```

---

### 9. Fix SourceRefs Precision

**Problem:** Many tasks reference only "Universe §4.Epic X" without specific section numbers or line ranges.

**Impact:** Developers must search entire epic section (100-400 lines) instead of jumping to exact spec.

**Files Affected:** ALL task detail files

**Current State:**
- Good: `SourceRefs: Universe §5 (Idempotency Rules)` (section title provided)
- Bad: `SourceRefs: Universe §4.Epic C` (too vague, epic spans 423 lines)

**Fix Instruction:**
- Update ALL SourceRefs to include:
  - Section number (§X)
  - Subsection title
  - Line range (optional but helpful)
- Format: `SourceRefs: Universe §4.Epic C > Entities & Data Model Changes (lines 1295-1350), Events (lines 1370-1420)`
- For cross-cutting concerns: `SourceRefs: Universe §5 (Cross-Cutting Architecture > Idempotency Rules, lines 2310-2380)`
- For appendices: `SourceRefs: Universe §7.Appendix A (Event Catalog, lines 2440-2520)`

---

### 10. Add Missing Cross-Cutting Tasks

**Problem:** No tasks for cross-cutting validation and consistency checks.

**Impact:** Risk of inconsistent names, schemas, statuses across epics. No automated checks for universe compliance.

**Files Affected:** NEW TASKS REQUIRED in `prod-ready-tasks-02-foundation.md`

**Fix Instruction:**
- Add new foundation tasks:
  - **PRD-0011: Event Catalog Validation** (M, Backend/API, Phase 1.5)
    - Verify all events in code match universe Appendix A (Event Catalog table)
    - Check: event names, payload fields, schema versions, producer/consumer lists
    - Automated test: parse universe markdown, compare with codebase events

  - **PRD-0012: API Contract Validation** (M, QA, Phase 1.5)
    - Verify all API endpoints match universe Appendix B (API Catalog table)
    - Check: route paths, HTTP methods, request/response schemas, auth
    - Tool: OpenAPI schema auto-generated from code, compare with universe

  - **PRD-0013: Status Enum Consistency** (S, Backend/API, Phase 1.5)
    - Verify all status enums match universe Appendix C (Status Matrices)
    - Check: OutboundOrderStatus, ShipmentStatus, SalesOrderStatus, ReservationStatus, RMAStatus
    - Test: parse enums from code, compare with universe tables

  - **PRD-0014: Migration Sequencing** (M, Infra/DevOps, Phase 1.5)
    - Define migration numbering scheme (001_Foundation, 002_Valuation, ...)
    - Enforce dependency order (Valuation migrations must run after Foundation)
    - Tool: migration validator (checks sequence, dependencies, rollback)

  - **PRD-0015: State Machine Validation** (M, QA, Phase 1.5)
    - Verify state machines match universe diagrams
    - Check allowed transitions: OutboundOrder, Shipment, SalesOrder, Reservation
    - Test: property-based test (generate random state sequences, verify only valid transitions allowed)

- Update master index with these 5 tasks
- Total addition: ~650 lines (5 tasks × 130 lines avg)

---

## Should-Fix Improvements (High Priority)

### 11. Add Implementation Patterns Section to Foundation Tasks

**Problem:** Foundation tasks lack implementation pattern guidance (e.g., which MediatR pipeline behavior for idempotency, which Marten API for projections).

**Impact:** Developers must research patterns, slowing velocity.

**Fix Instruction:**
- For each foundation task, add "Implementation Patterns" subsection after "Implementation Notes"
- Include:
  - Code snippet (C# interface or abstract class)
  - Library/package reference (MediatR, Marten, MassTransit)
  - Gotchas (thread safety, transaction boundaries, error handling)
- Example for PRD-0001:
  ```markdown
  ### Implementation Patterns

  **MediatR Pipeline Behavior:**
  ```csharp
  public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
  {
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct) {
      var commandId = (request as ICommand)?.CommandId;
      if (commandId == null) return await next();

      // Check processed_commands table...
    }
  }
  ```

  **Gotcha:** Use READ COMMITTED isolation to avoid phantom reads in concurrent scenarios.
  ```

---

### 12. Add Data Migration Examples

**Problem:** Tasks mention "EF Core migration" but don't show migration code examples.

**Impact:** Developers unsure of migration syntax, especially for complex changes (add column with default, rename column, split table).

**Fix Instruction:**
- For tasks with schema changes, add "Data Migration Example" subsection
- Include Up/Down migration code (C#)
- Example for PRD-0500 (Location Coordinates):
  ```markdown
  ### Data Migration Example

  ```csharp
  public partial class AddLocationCoordinates : Migration
  {
    protected override void Up(MigrationBuilder mb) {
      mb.AddColumn<decimal>(name: "CoordinateX", table: "Locations", nullable: true);
      mb.AddColumn<decimal>(name: "CoordinateY", table: "Locations", nullable: true);
      mb.AddColumn<decimal>(name: "CoordinateZ", table: "Locations", nullable: true);

      // Backfill: virtual locations have NULL coords (acceptable)
      // Physical locations need manual coord entry via UI (PRD-0510)
    }

    protected override void Down(MigrationBuilder mb) {
      mb.DropColumn(name: "CoordinateX", table: "Locations");
      mb.DropColumn(name: "CoordinateY", table: "Locations");
      mb.DropColumn(name: "CoordinateZ", table: "Locations");
    }
  }
  ```
  ```

---

### 13. Add Projection Rebuild Time Estimates

**Problem:** Tasks for projections (PRD-0105, PRD-0106, etc.) don't mention rebuild time, which is critical for production planning.

**Impact:** Unknown downtime for production projection rebuilds (could be minutes to hours for 10k+ events).

**Fix Instruction:**
- For ALL projection tasks, add "Projection Rebuild" subsection in Non-Functional Requirements
- Include:
  - Estimated rebuild time (test with 1k, 10k, 100k events)
  - Rebuild strategy (offline vs online, blue-green projection swap)
  - Distributed lock mechanism (prevent concurrent rebuilds)
- Example for PRD-0105 (OnHandValue Projection):
  ```markdown
  **Projection Rebuild:**
  - Estimated time: 10k events = ~30 sec, 100k events = ~5 min, 1M events = ~45 min
  - Strategy: Blue-green swap (rebuild new projection table, swap atomically)
  - Lock: Use `projection_rebuild_locks` table (INSERT ... ON CONFLICT DO NOTHING) to prevent concurrent rebuilds
  - Downtime: 0 seconds (read from old projection during rebuild, swap when complete)
  ```

---

### 14. Add Contract Test Specifications

**Problem:** PRD-0007 (Contract Test Framework) listed in index but no detail. Critical for integration testing.

**Impact:** No contract validation for external APIs (Agnum, Carrier, Billing).

**Fix Instruction:**
- Add detailed spec for PRD-0007 in `prod-ready-tasks-02-foundation.md`
- Include:
  - Tool selection (Pact, Spring Cloud Contract, or custom JSON schema validator)
  - Contract definition format (JSON schema for request/response)
  - Contract verification flow (provider generates contract, consumer validates)
  - Example contract for Agnum API
- Content: ~150 lines (Context, Scope, Requirements with JSON schema examples, Acceptance Criteria, Validation, DoD)

---

### 15. Add Test Data Generation Strategy

**Problem:** PRD-0008 (Sample Data Seeding) listed but no detail. Critical for QA and demo environments.

**Impact:** Developers manually create test data, inconsistent across environments.

**Fix Instruction:**
- Add detailed spec for PRD-0008 in `prod-ready-tasks-02-foundation.md`
- Include:
  - Test data scope (50 items, 10 customers, 20 sales orders, 100 handling units, 500 stock movements)
  - Data generation tool (Bogus library for .NET, or SQL script)
  - Idempotent seed logic (check if data exists, skip if present)
  - Seed data for all epics (not just Phase 1)
- Example:
  ```csharp
  public class WarehouseSeedData {
    public static void Seed(WarehouseDbContext db) {
      if (db.Items.Any()) return; // Already seeded

      var faker = new Faker<Item>()
        .RuleFor(i => i.SKU, f => $"RM-{f.IndexFaker:0000}")
        .RuleFor(i => i.Name, f => f.Commerce.ProductName())
        .RuleFor(i => i.BaseUoM, "EA");

      var items = faker.Generate(50);
      db.Items.AddRange(items);
      db.SaveChanges();
    }
  }
  ```
- Content: ~180 lines

---

## Optional Enhancements

### 16. Add Performance Benchmarks to API Tasks

For all API endpoint tasks (PRD-0108, PRD-0208, PRD-0312, PRD-0409, etc.):
- Add "Performance Benchmarks" subsection in Non-Functional Requirements
- Include: p50, p95, p99 latency targets (from universe: API latency < 2s p95)
- Example test: BenchmarkDotNet for .NET, or k6 load test script

### 17. Add ADR (Architecture Decision Record) References

For tasks with architectural decisions (saga vs event handler, SOFT vs HARD lock, event sourcing vs state-based):
- Add "ADR References" subsection
- Link to ADR documents (e.g., ADR-001: Why Event Sourcing for StockLedger, ADR-002: Hybrid Lock Strategy for Reservations)
- Format: `ADRs: See ADR-005 (Valuation Independence from Quantities) for rationale`

### 18. Add Rollback Procedures

For all migration tasks:
- Add "Rollback Procedure" subsection in Definition of Done
- Include: steps to revert schema changes, data backfill rollback, feature flag toggle
- Example: "Rollback: Run migration Down(), restore DB from backup if data loss, toggle feature flag `EnableValuation=false`"

### 19. Add Epic Completion Checklists

At end of each epic's task file:
- Add "Epic Completion Checklist" section
- Include: end-to-end workflow test (receive → value → export for Valuation epic), performance test results, documentation completeness, demo video recorded
- Format:
  ```markdown
  ## Epic C Completion Checklist
  - [ ] End-to-end workflow test passed (receive goods with cost → adjust cost → export to Agnum)
  - [ ] Performance test: OnHandValue query < 3s for 10k items
  - [ ] All 20 tasks have status=DONE
  - [ ] API documentation updated (Swagger)
  - [ ] User guide written (Valuation dashboard)
  - [ ] Demo video recorded (5 min walkthrough)
  ```

### 20. Add Dependency Graph Validation Task

Add new task PRD-0016 (Dependency Graph Validator):
- Tool to validate task dependencies (no cycles, critical path correct, all blockers listed)
- Parse master index, build directed graph, detect cycles, compute critical path
- Output: Gantt chart (ASCII or SVG), critical path tasks highlighted
- Run daily in CI/CD, alert if dependency added that creates cycle

---

## Quick Sanity Checklist for Kiro (Post-Fix Verification)

After applying all must-fix changes, verify:

### Coverage Completeness
- [ ] All 17 epics have detailed task files (Foundation, C, D, A, B, E for Phase 1.5; M, N, G, F, O for Phase 2; H, I, J, K, L for Phase 3; P, Q for Phase 4)
- [ ] All 180+ tasks have detailed specs (Context, Scope, Requirements, Acceptance Criteria, Validation, DoD)
- [ ] All foundation tasks (PRD-0001 to PRD-0015) fully detailed
- [ ] All Phase 1.5 critical path tasks fully detailed (PRD-0100 to PRD-0120, PRD-0200 to PRD-0215, PRD-0300 to PRD-0325, PRD-0400 to PRD-0425, PRD-0500 to PRD-0515)

### Task Quality
- [ ] Every task has 3-8 bullet Context section (minimum background from universe)
- [ ] Every task has 3-5 Gherkin scenarios in Acceptance Criteria
- [ ] Every task has 3-5 bash commands in Validation/Checks (dotnet test, psql, curl)
- [ ] Every task has 8-15 item DoD checklist
- [ ] Every task has precise SourceRefs (section + subsection + line range)

### Consistency
- [ ] Event names consistent with universe Appendix A (Event Catalog)
- [ ] API endpoints consistent with universe Appendix B (API Catalog)
- [ ] Status enums consistent with universe Appendix C (Status Matrices)
- [ ] Entity schemas match universe §4 (Epic sections, Entities & Data Model Changes)
- [ ] Saga flows match universe §4 (Epic sections, Saga diagrams)

### Dependencies
- [ ] No dependency cycles (PRD-XXXX → PRD-YYYY → PRD-XXXX)
- [ ] Critical path correct (Foundation → Valuation → Agnum → Outbound → Sales Orders)
- [ ] All blockers listed (e.g., PRD-0108 depends on PRD-0102, PRD-0103, PRD-0104)
- [ ] Parallel tracks identified (Backend vs UI vs QA can work simultaneously)

### Engineering Quality
- [ ] Idempotency addressed in all command tasks (CommandId, processed_commands check)
- [ ] Observability addressed in all epics (metrics, logs, traces, correlation IDs)
- [ ] RBAC addressed in all epics (role checks, permission validation)
- [ ] Migrations addressed in all schema-changing tasks (Up/Down, backwards compat)
- [ ] Test coverage addressed in all tasks (unit, integration, contract, property-based)

### Risk Items
- [ ] Data migration tasks exist for all schema changes (Foundation, Valuation, Outbound, Sales, 3D Viz, Phase 2-4 epics)
- [ ] Backfill tasks exist for new columns with non-null constraints
- [ ] Projection rebuild tasks exist for all projections (OnHandValue, LocationBalance, AvailableStock, etc.)
- [ ] Contract test tasks exist for all external integrations (Agnum, Carrier, Billing)
- [ ] Test data seeding task complete and covers all epics

### File Structure
- [ ] README-TASKS.md complete (how to use task plan)
- [ ] TASK-PLAN-SUMMARY.md complete (overview, statistics, phases)
- [ ] prod-ready-tasks-master-index.md complete (all 180+ tasks indexed with title, estimate, deps, owner, phase)
- [ ] prod-ready-tasks-master-index-part2.md complete (Phase 2-4 tasks)
- [ ] Detailed task files exist for ALL epics:
  - [ ] prod-ready-tasks-01-overview.md
  - [ ] prod-ready-tasks-02-foundation.md (PRD-0001 to PRD-0015, 15 tasks)
  - [ ] prod-ready-tasks-03-valuation.md (PRD-0100 to PRD-0120, 21 tasks)
  - [ ] prod-ready-tasks-04-outbound.md (PRD-0300 to PRD-0325, 26 tasks)
  - [ ] prod-ready-tasks-05-sales-orders.md (PRD-0400 to PRD-0425, 26 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-06-agnum.md (PRD-0200 to PRD-0215, 16 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-07-3d-viz.md (PRD-0500 to PRD-0515, 16 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-08-cycle-counting.md (PRD-0600 to PRD-0615, 16 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-09-returns.md (PRD-0700 to PRD-0715, 16 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-10-label-printing.md (PRD-0800 to PRD-0810, 11 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-11-transfers.md (PRD-0900 to PRD-0910, 11 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-12-reporting.md (PRD-1000 to PRD-1015, 16 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-13-wave-picking.md (PRD-1100 to PRD-1115, 16 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-14-cross-docking.md (PRD-1200 to PRD-1210, 11 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-15-multi-level-qc.md (PRD-1300 to PRD-1315, 16 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-16-hu-hierarchy.md (PRD-1400 to PRD-1415, 16 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-17-serial-tracking.md (PRD-1500 to PRD-1520, 21 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-18-admin-config.md (PRD-1600 to PRD-1610, 11 tasks) — **MISSING, MUST CREATE**
  - [ ] prod-ready-tasks-19-security.md (PRD-1700 to PRD-1720, 21 tasks) — **MISSING, MUST CREATE**

---

## Summary of Required Work

### Must-Fix (Blockers)
- **Fix #1-6:** Create 6 new task detail files + complete 3 existing files = ~15,000 lines
- **Fix #7:** Add Acceptance Criteria to all tasks = ~9,000 lines (180 tasks × 50 lines avg)
- **Fix #8:** Add Validation Commands to all tasks = ~3,600 lines (180 tasks × 20 lines avg)
- **Fix #9:** Update SourceRefs for all tasks = ~360 lines (180 tasks × 2 lines avg)
- **Fix #10:** Add 5 cross-cutting tasks = ~650 lines

**Total Must-Fix:** ~28,610 lines of detailed specifications

### Should-Fix (High Priority)
- **Fix #11-15:** Add implementation patterns, migration examples, rebuild estimates, contract tests, test data = ~1,500 lines

**Total Should-Fix:** ~1,500 lines

### Optional Enhancements
- **Fix #16-20:** Performance benchmarks, ADRs, rollback procedures, epic checklists, dependency validator = ~1,000 lines

**Total Optional:** ~1,000 lines

### Grand Total Work Required
**~31,110 lines** of additional task specifications to make the plan execution-ready for developers (Codex/Cursor) without requiring them to re-read the entire 2573-line universe document for every task.

---

## Recommendation

**Action:** Return task plan to Kiro for comprehensive completion. Focus on Must-Fix issues (1-10) first, especially Phase 1.5 critical path (Foundation → Valuation → Agnum → Outbound → Sales Orders → 3D Viz).

**Priority Order:**
1. Complete Foundation tasks (PRD-0002 to PRD-0015) — blocks everything
2. Complete Valuation tasks (PRD-0101 to PRD-0120) — blocks Agnum
3. Complete Agnum tasks (PRD-0200 to PRD-0215) — blocks financial reconciliation
4. Complete Outbound tasks (PRD-0301 to PRD-0325) — blocks Sales Orders
5. Complete Sales Orders tasks (PRD-0400 to PRD-0425) — completes Phase 1.5 MVP
6. Complete 3D Viz tasks (PRD-0500 to PRD-0515) — completes Phase 1.5 USP
7. Add Acceptance Criteria to all tasks (Gherkin scenarios)
8. Add Validation Commands to all tasks (bash scripts)
9. Complete Phase 2-4 task details (Epics M, N, G, F, O, H, I, J, K, L, P, Q)

**Estimated Time to Fix:** 3-4 weeks of AI task generation (Kiro) to produce ~31k lines of detailed specifications following the established template (see PRD-0001 as gold standard).

---

**Review Status:** NOT OK — Plan requires substantial completion before handoff to development team.

**Next Steps:** Address must-fix issues #1-10, then re-review for acceptance.
