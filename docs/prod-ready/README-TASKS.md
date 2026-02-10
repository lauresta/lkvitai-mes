# Production-Ready Warehouse Tasks - README

**Version:** 1.0  
**Date:** February 10, 2026  
**Source:** prod-ready-universe.md

---

## Document Structure

This task plan is split across multiple files for manageability:

### 1. Overview & Foundation
- **File:** `prod-ready-tasks-01-overview.md`
- **Content:** Phases, assumptions, dependency graph, foundation task index

### 2. Foundation Tasks (Detailed)
- **File:** `prod-ready-tasks-02-foundation.md`
- **Content:** Detailed specifications for PRD-0001 to PRD-0010
- **Tasks:** Idempotency, Event Versioning, Observability, Testing, etc.

### 3. Epic C: Valuation (Detailed)
- **File:** `prod-ready-tasks-03-valuation.md`
- **Content:** Detailed specifications for PRD-0100 to PRD-0120
- **Tasks:** Domain model, cost adjustments, landed cost, write-downs, projections, UI, reports

### 4. Master Index (All Tasks)
- **File:** `prod-ready-tasks-master-index.md`
- **Content:** Complete task index for all epics (PRD-0001 to PRD-1720)
- **Epics:** Foundation, C, D, A, B, E (Phase 1.5)

### 5. Master Index Part 2
- **File:** `prod-ready-tasks-master-index-part2.md`
- **Content:** Complete task index for remaining epics
- **Epics:** M, N, G, F, O, H, I, J, K, L, P, Q (Phases 2-4)

---

## How to Use This Task Plan

### For AI-Assisted Development (Codex/Cursor)

Each task is designed to be **self-contained** and **executable** without re-reading the entire universe document.

**Task Execution Workflow:**
1. **Select Task:** Choose next task from dependency graph (e.g., PRD-0001)
2. **Read Task Spec:** Open detailed task file (e.g., `prod-ready-tasks-02-foundation.md`)
3. **Review Context:** Read 3-8 bullet context (minimum background)
4. **Implement:** Follow requirements, data model, API changes
5. **Validate:** Run acceptance criteria (Gherkin scenarios)
6. **Check:** Execute validation commands, verify metrics/logs
7. **Complete:** Check off Definition of Done checklist
8. **Move to Next:** Follow dependency graph to next task

### For Human Developers

**Before Starting:**
1. Read `prod-ready-universe.md` (source of truth) - one-time
2. Review dependency graph in `prod-ready-tasks-01-overview.md`
3. Identify your phase (1.5, 2, 3, or 4)
4. Filter tasks by OwnerType (Backend, UI, QA, etc.)

**During Development:**
1. Use task specs as implementation guide (no need to re-read universe)
2. Reference SourceRefs if clarification needed
3. Follow acceptance criteria for testing
4. Use validation commands for local verification

**After Completion:**
1. Check Definition of Done
2. Update task status (e.g., in Jira/GitHub)
3. Notify dependent tasks (unblock next tasks)

---

## Task Template (Standard Format)

Every detailed task follows this structure:

```markdown
## Task PRD-XXXX: <Title>

**Epic:** <Epic Code + Name>  
**Phase:** <1.5 | 2 | 3 | 4>  
**Estimate:** <S | M | L> (S <= 0.5d, M ~1d, L 2-3d)  
**OwnerType:** <Backend/API | UI | QA | Integration | Infra/DevOps | Projections>  
**Dependencies:** <TaskIds or "None">  
**SourceRefs:** <exact refs to prod-ready-universe.md sections>

### Context
- 3-8 bullets: minimum background from universe needed to implement

### Scope
**In Scope:**
- Bullet list of what to implement

**Out of Scope:**
- Bullet list of what NOT to implement (deferred or out of bounds)

### Requirements

**Functional:**
1. Numbered list of functional requirements
2. Each requirement is testable

**Non-Functional:**
1. Idempotency, security, performance, observability
2. Specific metrics (e.g., latency < 100ms)

**Data Model / Migrations:**
```sql
-- Explicit SQL schema changes
CREATE TABLE ...
```

**API/Events:**
- Explicit endpoints to add/change
- Event schemas with versioning

**UI:**
- Pages, components, validations (if relevant)

### Acceptance Criteria

```gherkin
Scenario: <Scenario Name>
  Given <precondition>
  When <action>
  Then <expected result>
  And <additional assertions>
```

At least 2-5 scenarios per task, including negative/error cases.

### Implementation Notes
- Only constraints and pitfalls
- No code, no long prose
- Pointers to libraries, patterns, gotchas

### Validation / Checks

**Local Testing:**
```bash
# Commands to verify locally
dotnet test --filter "Category=..."
psql -d warehouse -c "SELECT ..."
curl -X POST ...
```

**Metrics:**
- List of metrics that must be exposed

**Logs:**
- Expected log messages (INFO, WARN, ERROR)

**Backwards Compatibility:**
- Checks for breaking changes

### Definition of Done
- [ ] Checklist item 1
- [ ] Checklist item 2
- [ ] Tests pass
- [ ] Migrations applied
- [ ] Docs updated
- [ ] Code review completed
```

---

## Dependency Management

### Critical Path (Must Follow Order)

```
Foundation (PRD-0001 to PRD-0010)
    ↓
Valuation (PRD-0100 to PRD-0120)
    ↓
Agnum Integration (PRD-0200 to PRD-0215)
    ↓
Outbound/Shipment (PRD-0300 to PRD-0325)
    ↓
Sales Orders (PRD-0400 to PRD-0425)
```

### Parallel Tracks (Can Work Simultaneously)

**Track 1: Backend/API**
- Foundation → Valuation → Agnum → Outbound → Sales Orders

**Track 2: UI**
- Wait for API endpoints (PRD-0108, PRD-0208, PRD-0312, PRD-0409)
- Then implement UI tasks in parallel

**Track 3: 3D Visualization (Independent)**
- Can start after PRD-0500 (Location Coordinates Schema)
- No blockers from other epics

**Track 4: QA**
- Integration tests after API complete
- Contract tests can start early (PRD-0007)

### Blockers to Watch

1. **Agnum Integration** blocked by Valuation (needs cost data)
2. **Sales Orders** blocked by Outbound (needs shipment workflow)
3. **Returns/RMA** blocked by Sales Orders (needs order entity)
4. **Wave Picking** needs Phase 1 Picking (already exists)
5. **COGS Calculation** blocked by Valuation (needs cost data)

---

## Estimation Guide

**S (Small): 0.5 days or less**
- Schema changes only
- Simple CRUD endpoints
- Configuration updates
- Documentation tasks

**M (Medium): ~1 day**
- Domain model implementation
- Command handlers with business logic
- UI pages with forms
- Integration with external APIs
- Projection implementations

**L (Large): 2-3 days**
- Complex sagas (multi-step workflows)
- Route optimization algorithms
- Comprehensive UI dashboards
- End-to-end integration tests
- Performance optimization

**Spike: Variable (mark as "Spike")**
- Research tasks (e.g., evaluate 3D libraries)
- Proof of concept
- Architecture decisions

---

## Quality Gates (All Tasks Must Pass)

### 1. Idempotency
- All commands include CommandId
- Duplicate commands return cached result
- Event handlers check checkpoints

### 2. Observability
- Metrics exposed (Prometheus format)
- Structured logs (JSON, correlation ID)
- Distributed tracing (OpenTelemetry)

### 3. RBAC
- All endpoints check permissions
- Role-based access enforced
- Audit trail for sensitive operations

### 4. Migrations
- EF Core migrations for state-based entities
- Marten schema auto-upgrade for event store
- Backwards compatible (no breaking changes)

### 5. Testing
- Unit tests: 80%+ coverage
- Integration tests: happy path + error cases
- Contract tests: API schemas validated
- Property-based tests: where applicable

### 6. Documentation
- ADRs for architectural decisions
- API documentation (OpenAPI/Swagger)
- Runbooks for operations
- User guides for UI features

---

## Phase Rollout Strategy

### Phase 1.5 (Production MVP) - 14 weeks

**Goal:** Minimum viable product for B2B/B2C warehouse operations

**Deliverables:**
- Valuation & cost tracking
- Agnum accounting integration
- Outbound shipment & dispatch
- Sales order management
- 3D warehouse visualization

**Success Criteria:**
- Can receive goods, track costs, allocate to sales orders, pick, pack, ship, deliver
- Daily export to Agnum for GL posting
- Visual warehouse map for operators

**Go-Live Checklist:**
- [ ] All Phase 1.5 tasks completed (PRD-0001 to PRD-0515)
- [ ] Integration tests passing (>95%)
- [ ] UAT completed with stakeholders
- [ ] Performance benchmarks met (API latency < 2s p95)
- [ ] Security audit passed
- [ ] Disaster recovery tested
- [ ] Training materials prepared
- [ ] Production deployment plan reviewed

### Phase 2 (Operational Excellence) - 8 weeks

**Goal:** Improve accuracy, efficiency, and customer service

**Deliverables:**
- Cycle counting for inventory accuracy
- Returns/RMA workflow
- Label printing automation
- Inter-warehouse transfers
- Advanced reporting & audit

**Success Criteria:**
- Inventory accuracy >99%
- Returns processed within 7 days
- Labels auto-printed on HU creation
- Compliance reports available (FDA, ISO)

### Phase 3 (Advanced Features) - 12 weeks

**Goal:** High-volume optimization and granular tracking

**Deliverables:**
- Wave picking (batch picking)
- Cross-docking (fast throughput)
- Multi-level QC approvals
- Handling unit hierarchy
- Serial number tracking

**Success Criteria:**
- Pick throughput 3x faster (wave picking)
- Same-day ship via cross-docking
- ISO 9001 compliance (multi-level QC)
- Warranty tracking per serial number

### Phase 4 (Enterprise Hardening) - 5 weeks

**Goal:** Multi-tenant ready, enterprise security

**Deliverables:**
- Admin configuration UI
- SSO, MFA, API key management
- Granular RBAC
- SOC 2, ISO 27001 compliance

**Success Criteria:**
- SSO integration with Azure AD/Okta
- API keys with scopes
- Audit log for all user actions
- Compliance audit passed

---

## Contact & Support

**Questions about tasks?**
- Reference SourceRefs in task spec (points to exact universe section)
- Check prod-ready-universe.md for full context
- Consult architecture docs (docs/04-system-architecture.md)

**Found an issue?**
- Update task spec with clarifications
- Document in ADR if architectural decision needed
- Notify dependent tasks if scope changes

**Need to add a task?**
- Follow task template format
- Assign TaskId (next available in epic range)
- Update master index
- Check dependencies (update dependency graph)

---

## Appendix: Task Naming Conventions

**TaskId Format:** `PRD-XXXX`
- PRD = Production-Ready
- XXXX = 4-digit number

**Epic Ranges:**
- 0001-0010: Foundation
- 0100-0120: Epic C (Valuation)
- 0200-0215: Epic D (Agnum)
- 0300-0325: Epic A (Outbound)
- 0400-0425: Epic B (Sales Orders)
- 0500-0515: Epic E (3D Visualization)
- 0600-0615: Epic M (Cycle Counting)
- 0700-0715: Epic N (Returns/RMA)
- 0800-0810: Epic G (Label Printing)
- 0900-0910: Epic F (Inter-Warehouse Transfers)
- 1000-1015: Epic O (Advanced Reporting)
- 1100-1115: Epic H (Wave Picking)
- 1200-1210: Epic I (Cross-Docking)
- 1300-1315: Epic J (Multi-Level QC)
- 1400-1415: Epic K (HU Hierarchy)
- 1500-1520: Epic L (Serial Tracking)
- 1600-1610: Epic P (Admin Config)
- 1700-1720: Epic Q (Security Hardening)

**Epic Codes (from Universe):**
- A: Outbound/Shipment/Dispatch
- B: Sales Orders / Customer Orders
- C: Valuation & Revaluation
- D: Agnum Accounting Integration
- E: 3D/2D Warehouse Visualization
- F: Inter-Warehouse Transfers
- G: Label Printing (ZPL Integration)
- H: Wave Picking (Batch Picking)
- I: Cross-Docking
- J: Multi-Level QC Approvals
- K: Handling Unit Hierarchy (Nested HUs)
- L: Serial Number Tracking
- M: Cycle Counting (Scheduled Physical Inventory)
- N: Returns / RMA
- O: Advanced Reporting & Audit
- P: Admin & Configuration
- Q: Security Hardening (SSO, OAuth, MFA, API Keys)

