# Phase 1.5 Sprint 4 - Task Summary

**Sprint Goal:** Achieve operational completeness with end-to-end hardening, observability, integration tests, and production-ready reporting.

**Total Tasks:** 20
**Estimated Effort:** 16 days
**Status:** Ready for Execution

---

## Task Overview

| TaskId | Title | Epic | Est | Owner | Critical? |
|--------|-------|------|-----|-------|-----------|
| PRD-1541 | E2E Inbound Workflow Test | Testing | M | QA | ⭐ CRITICAL |
| PRD-1542 | E2E Outbound Workflow Test | Testing | M | QA | ⭐ CRITICAL |
| PRD-1543 | E2E Stock Movement Test | Testing | S | QA | High |
| PRD-1544 | Health Check Dashboard | Observability | M | Infra | ⭐ CRITICAL |
| PRD-1545 | Metrics Dashboards (Grafana) | Observability | L | Infra | High |
| PRD-1546 | Alerting Rules & Runbook | Observability | M | Infra | High |
| PRD-1547 | Role-Based Access Control Enforcement | RBAC | M | Backend | ⭐ CRITICAL |
| PRD-1548 | Admin User Management UI | RBAC | M | UI | High |
| PRD-1549 | Stock Movement History Report | Reports | M | UI/Backend | High |
| PRD-1550 | Transaction Log Export | Reports | S | Backend | Medium |
| PRD-1551 | Traceability Report (Lot → Order) | Reports | M | UI/Backend | High |
| PRD-1552 | Compliance Audit Report | Reports | S | Backend | Medium |
| PRD-1553 | Projection Rebuild Optimization | Performance | M | Projections | High |
| PRD-1554 | Consistency Checks (Daily Job) | Performance | M | Backend | ⭐ CRITICAL |
| PRD-1555 | Query Performance Optimization | Performance | S | Backend | Medium |
| PRD-1556 | ERP Integration Mock & Tests | Integration | M | Integration | High |
| PRD-1557 | Deployment Guide | Documentation | S | Infra | ⭐ CRITICAL |
| PRD-1558 | Operator Runbook | Documentation | S | QA | ⭐ CRITICAL |
| PRD-1559 | API Documentation (Swagger) | Documentation | S | Backend | High |
| PRD-1560 | Production Readiness Checklist | Testing | M | QA | ⭐ CRITICAL |

---

## Critical Path

**Week 1 (Days 1-5):**
- PRD-1541-1543: E2E workflow tests (Days 1-2) — VALIDATES all Sprint 3 work
- PRD-1544: Health check dashboard (Day 2) — BLOCKS deployment
- PRD-1547: RBAC enforcement (Day 3) — BLOCKS production security
- PRD-1554: Consistency checks (Day 4) — BLOCKS data integrity validation
- PRD-1557: Deployment guide (Day 5) — BLOCKS production deployment

**Week 2 (Days 6-10):**
- PRD-1545-1546: Observability (Grafana dashboards, alerting) (Days 6-7)
- PRD-1549, 1551: Reports (Movement history, Traceability) (Days 8-9)
- PRD-1558-1559: Documentation (Operator runbook, API docs) (Day 9)
- PRD-1560: Production readiness checklist (Day 10) — FINAL GATE

**Parallel Tracks (can overlap):**
- PRD-1548: Admin UI (Week 1-2, parallel with other tasks)
- PRD-1553, 1555: Performance optimization (Week 2, parallel)
- PRD-1556: ERP integration mock (Week 2, parallel)

---

## Dependencies

**External Dependencies:**
- None (all work self-contained)

**Internal Dependencies:**
- Sprint 3 complete (PRD-1521 to PRD-1540) — BLOCKS all Sprint 4 work
- PRD-1544 → PRD-1545,1546 (Health checks → Dashboards/Alerting)
- PRD-1547 → PRD-1548 (RBAC enforcement → Admin UI)
- All tasks → PRD-1560 (Production readiness checklist validates all prior work)

**Blocking Issues:**
- None expected (Sprint 3 resolves all known blockers)

---

## Key Deliverables

### 1. End-to-End Testing (CRITICAL)
- **Inbound Workflow:** Integration test validates Create shipment → Receive → QC → Putaway
- **Outbound Workflow:** Integration test validates Create order → Allocate → Pick → Pack → Dispatch
- **Stock Movement:** Integration test validates Transfer → Execute → Verify balances

### 2. Observability & Monitoring (CRITICAL)
- **Health Checks:** /health endpoint with DB/event store/message queue checks
- **Grafana Dashboards:** API latency, event throughput, projection lag, error rates
- **Alerting:** PagerDuty/Slack alerts for API errors, projection lag, DB issues

### 3. RBAC & Security (CRITICAL)
- **RBAC Enforcement:** All API endpoints enforce role-based permissions
- **Admin UI:** User management (create, edit, assign roles, deactivate)

### 4. Reporting Completeness
- **Movement History:** Stock movements by item/location/date range (CSV export)
- **Transaction Log:** Full audit trail export (all events)
- **Traceability:** Lot → Sales Order → Shipment linkage report
- **Compliance Audit:** Scheduled reports for regulatory compliance

### 5. Performance & Reliability
- **Projection Rebuild:** Optimized rebuild (< 5 minutes for 10k events)
- **Consistency Checks:** Daily job validates balance integrity, no negative balances
- **Query Optimization:** Indexes added, query plans reviewed

### 6. Documentation (CRITICAL)
- **Deployment Guide:** Step-by-step production deployment instructions
- **Operator Runbook:** Common tasks, troubleshooting, escalation procedures
- **API Docs:** Swagger UI with examples for all endpoints

### 7. Production Readiness (CRITICAL)
- **Checklist:** 50+ items validating data integrity, security, performance, observability, business continuity

---

## Sprint 4 Success Criteria

At the end of Sprint 4, these MUST be true:

### Operator Workflow Validation (7 CRITICAL checks)

1. ✅ **Operator can create inbound invoice/shipment in UI**
2. ✅ **Operator can receive with scan + QC in UI**
3. ✅ **Operator can putaway/move stock in UI and see balances update**
4. ✅ **Operator can create Sales Order in UI, allocate, pick, pack, dispatch**
5. ✅ **Operator can view shipment status + dispatch history in UI**
6. ✅ **RBAC/auth flow documented and local validation steps work (no 403)**
7. ✅ **Idempotency and tracing in place and validated**

### Testing & Quality (3 checks)

8. ✅ **Integration tests validate critical workflows** (Inbound, Outbound, Stock Movement)
9. ✅ **Observability dashboards operational** (Health checks, Grafana, Alerts)

### Reporting & Documentation (2 checks)

10. ✅ **Reports accessible via UI** (Receiving, Dispatch, Movement, Transaction Log, Traceability)
11. ✅ **Documentation complete** (Deployment, Runbook, API Docs, Auth Guide)

### Performance & Reliability (2 checks)

12. ✅ **Projection rebuild works correctly** (< 5 min for 10k events)
13. ✅ **Consistency checks run daily** (Balance integrity, no orphans)

---

## Operational Readiness Checklist (14 areas)

### Data Integrity
- [ ] Run consistency check job manually, verify no errors
- [ ] Verify projection lag < 1 second under normal load
- [ ] Verify all FKs enforced (no orphaned records)
- [ ] Verify audit trail complete (all events have operator, timestamp)

### Security
- [ ] Dev auth disabled in production (ASPNETCORE_ENVIRONMENT=Production)
- [ ] HTTPS enforced on all endpoints
- [ ] RBAC roles enforced (test unauthorized access returns 403)
- [ ] Sensitive data encrypted at rest

### Performance
- [ ] API latency < 500ms (p95) under normal load (50 req/sec)
- [ ] Projection lag < 1 second (p95)
- [ ] Database indexes in place (verify EXPLAIN plans)
- [ ] Health check responds < 100ms

### Observability
- [ ] Logs shipped to centralized logging (Seq, ELK, Azure Monitor)
- [ ] Metrics exported to Grafana
- [ ] Alerts configured and tested (trigger test alert, verify notification)
- [ ] Correlation IDs flow through all requests

### Business Continuity
- [ ] Backup strategy in place (daily PostgreSQL backups)
- [ ] Disaster recovery plan documented
- [ ] Runbook tested (simulate outage, follow recovery steps)
- [ ] Rollback plan tested (deploy old version, verify no data loss)

### User Acceptance
- [ ] Operator training completed (demonstrate all workflows)
- [ ] Operator can complete full workflow without assistance
- [ ] Feedback collected and critical issues resolved
- [ ] Sign-off from warehouse manager and finance

---

## Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Integration tests flaky | CI unreliable | Medium | Isolate tests, use test containers |
| Grafana setup complex | Observability delayed | Low | Use pre-built dashboards |
| Consistency check job slow | Daily job times out | Low | Optimize queries, run incrementally |
| Production deployment fails | Go-live delayed | Medium | Test in staging, have rollback plan |
| Operator training insufficient | Low adoption | Medium | Record videos, create quick-start guide |

---

## Team Allocation (Recommended)

**If 1 Developer:**
- Week 1: Critical path (PRD-1541-1544, 1547, 1554, 1557)
- Week 2: Observability + Reports + Docs (PRD-1545-1546, 1549-1552, 1558-1560)
- Total: 16 days (3.2 weeks)

**If 2 Developers:**
- Dev 1: Testing + Observability + Docs (PRD-1541-1546, 1557-1560)
- Dev 2: RBAC + Reports + Performance (PRD-1547-1556)
- Total: 2 weeks (parallel execution)

---

## Testing Strategy

**Integration Testing Priority:**
1. E2E Inbound Workflow (PRD-1541) — validates Sprint 3 inbound UI
2. E2E Outbound Workflow (PRD-1542) — validates Sprint 3 outbound UI
3. E2E Stock Movement (PRD-1543) — validates Sprint 3 transfer UI

**Manual Testing:**
1. Health check dashboard (verify all checks green)
2. Grafana dashboards (verify metrics populating)
3. RBAC enforcement (test unauthorized access)
4. Reports UI (generate all reports, verify data)
5. Operator training (walk through all workflows)

**Production Readiness:**
- PRD-1560: Run full checklist, document pass/fail for each item

---

## Go-Live Plan

**Pre-Go-Live (1 week before):**
1. Deploy to staging environment
2. Run PRD-1560 production readiness checklist
3. Conduct operator training
4. Perform load testing (simulate 100 concurrent users)
5. Validate backup/restore procedures

**Go-Live Day:**
1. Deploy to production (off-hours, e.g., Saturday 6 AM)
2. Run smoke tests (PRD-1541-1543 integration tests)
3. Verify health checks green
4. Verify Grafana dashboards showing metrics
5. Conduct operator walkthrough of live system
6. Monitor for 4 hours, then hand off to on-call

**Post-Go-Live (1 week after):**
1. Daily check-ins with operators
2. Monitor alerts and error logs
3. Collect feedback, prioritize issues for Phase 2
4. Conduct retrospective

---

## Notes

1. **Testing:** Integration tests > Manual testing > Unit tests (priority order)
2. **Observability:** Leverage existing OpenTelemetry, add Grafana dashboards
3. **RBAC:** Enforce at API layer (Authorize attributes), not DB
4. **Reports:** Use existing projections, add CSV export endpoints
5. **Performance:** Query optimization (indexes) > Caching (defer to Phase 2)
6. **Documentation:** Markdown in `docs/`, Swagger for API, videos in Wiki
7. **Deployment:** Docker Compose for staging, Kubernetes optional for prod
8. **Monitoring:** Seq for logs, Grafana for metrics, PagerDuty for alerts
9. **Backup:** Automated PostgreSQL backups, 30-day retention
10. **Go-Live:** Phased rollout (pilot warehouse → full deployment)

---

## Files Created

- `prod-ready-tasks-PHASE15-S4.md` — Full task details (all 20 tasks)
- `prod-ready-tasks-PHASE15-S4-summary.md` — This summary file

---

**Next Action:** Begin Sprint 4 execution with PRD-1541 (E2E Inbound Test)
