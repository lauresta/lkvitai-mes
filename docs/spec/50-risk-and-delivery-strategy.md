# Risk and Delivery Strategy

**Project:** LKvitai.MES Warehouse Management System  
**Document:** Risk and Delivery Strategy  
**Version:** 1.0  
**Date:** February 2026  
**Status:** Implementation Specification

---

## Document Purpose

This document provides delivery governance, risk management, rollout strategy, and monitoring approach for the warehouse system. It ensures controlled, safe deployment with clear rollback procedures.

**Audience:** Project Manager, Tech Lead, DevOps, Operations Team

---

## Table of Contents

1. [Risk Register](#1-risk-register)
2. [Mitigation Strategies](#2-mitigation-strategies)
3. [Spike Tasks](#3-spike-tasks)
4. [Rollout Strategy](#4-rollout-strategy)
5. [Feature Toggles](#5-feature-toggles)
6. [Go-Live Readiness Gates](#6-go-live-readiness-gates)
7. [Monitoring Strategy](#7-monitoring-strategy)
8. [Rollback Procedures](#8-rollback-procedures)
9. [Team Composition](#9-team-composition)

---

## 1. Risk Register

### 1.1 Technical Risks

#### Risk T-01: Event Store Performance Degradation

**Description:** Event store cannot handle 1000 movements/second sustained load

**Probability:** Medium (40%)

**Impact:** Critical (system unusable)

**Phase:** Phase 0 (Foundation)

**Indicators:**
- Event append latency > 100ms (p95)
- Database CPU > 80%
- Query timeouts

**Mitigation:** See Section 2.1

---

#### Risk T-02: Projection Lag Exceeds Threshold

**Description:** Projection lag > 30 seconds causes stale reads and operator confusion

**Probability:** High (60%)

**Impact:** High (operational disruption)

**Phase:** Phase 1 (Core Inventory)

**Indicators:**
- Projection lag monitoring alerts
- Operators report incorrect balances
- UI shows stale data indicator

**Mitigation:** See Section 2.2

---

#### Risk T-03: Pick Transaction Ordering Violations

**Description:** Developers bypass StockLedger or violate transaction ordering

**Probability:** Low (20%)

**Impact:** Critical (data corruption)

**Phase:** Phase 2 (Reservation & Picking)

**Indicators:**
- Negative balances detected
- HU contents mismatch with LocationBalance
- Consistency check failures

**Mitigation:** See Section 2.3

---

#### Risk T-04: Concurrent Allocation Conflicts

**Description:** Multiple reservations allocated to same stock cause conflicts

**Probability:** High (70%)

**Impact:** Medium (reservation failures)

**Phase:** Phase 2 (Reservation & Picking)

**Indicators:**
- High rate of StartPicking failures
- Frequent reservation bumping
- Operator complaints

**Mitigation:** See Section 2.4

---

### 1.2 Integration Risks

#### Risk I-01: Agnum API Changes

**Description:** Agnum API changes break export integration

**Probability:** Medium (40%)

**Impact:** Medium (manual export required)

**Phase:** Phase 3 (Financial & Integration)

**Indicators:**
- Export failures
- API returns 400/500 errors
- Schema validation failures

**Mitigation:** See Section 2.5

---

#### Risk I-02: ERP Integration Complexity

**Description:** ERP integration more complex than expected, delays Phase 3

**Probability:** High (60%)

**Impact:** High (schedule delay)

**Phase:** Phase 3 (Financial & Integration)

**Indicators:**
- Spike task reveals complexity
- ERP team unavailable
- Contract negotiation delays

**Mitigation:** See Section 2.6

---

### 1.3 Offline/Edge Risks

#### Risk O-01: Offline Sync Conflicts Frequent

**Description:** High rate of sync conflicts frustrates operators

**Probability:** Medium (50%)

**Impact:** Medium (operator frustration)

**Phase:** Phase 4 (Offline & Edge)

**Indicators:**
- > 10% of offline commands rejected
- Operators report confusion
- Reconciliation reports ignored

**Mitigation:** See Section 2.7

---

#### Risk O-02: Edge Device Queue Limits

**Description:** Offline queue fills up, operators cannot work

**Probability:** Low (30%)

**Impact:** High (operational disruption)

**Phase:** Phase 4 (Offline & Edge)

**Indicators:**
- Queue size approaching 100 commands
- Operators offline > 8 hours
- Sync failures

**Mitigation:** See Section 2.8

---

### 1.4 Data Consistency Risks

#### Risk D-01: Negative Balances

**Description:** Balance validation fails, negative balances occur

**Probability:** Low (20%)

**Impact:** Critical (data corruption)

**Phase:** Phase 1 (Core Inventory)

**Indicators:**
- Consistency check detects negative balance
- Operators report "insufficient stock" errors
- Cycle count discrepancies

**Mitigation:** See Section 2.9

---

#### Risk D-02: Orphaned Handling Units

**Description:** HUs created without corresponding StockMovement

**Probability:** Medium (40%)

**Impact:** Medium (inventory discrepancy)

**Phase:** Phase 1 (Core Inventory)

**Indicators:**
- Consistency check detects orphaned HUs
- HU location invalid
- Cycle count discrepancies

**Mitigation:** See Section 2.10

---

### 1.5 Performance Risks

#### Risk P-01: 3D Visualization Performance

**Description:** 3D rendering slow for large warehouses (> 1000 bins)

**Probability:** Medium (50%)

**Impact:** Low (UI sluggish)

**Phase:** Phase 5 (Visualization & Optimization)

**Indicators:**
- Initial load > 5 seconds
- Frame rate < 30 FPS
- Browser memory > 500MB

**Mitigation:** See Section 2.11

---

#### Risk P-02: Query Performance Degradation

**Description:** Queries slow as data grows (> 10M events)

**Probability:** Medium (40%)

**Impact:** Medium (slow UI)

**Phase:** Phase 5 (Visualization & Optimization)

**Indicators:**
- Query latency > 500ms (p95)
- Database CPU > 70%
- Operators report slow UI

**Mitigation:** See Section 2.12

---

## 2. Mitigation Strategies

### 2.1 Event Store Performance (Risk T-01)

**Mitigation Actions:**
1. **Spike Task:** Load test event store with 1M events (3 days, Phase 0)
2. **Optimization:** Add indexes on sequence_number, timestamp, sku, location
3. **Partitioning:** Partition event table by month for archival
4. **Monitoring:** Alert if append latency > 100ms (p95)
5. **Fallback:** Use read replicas for queries

**Success Criteria:**
- Append latency < 50ms (p95)
- Query latency < 100ms (p95)
- Sustained 1000 events/second

---

### 2.2 Projection Lag (Risk T-02)

**Mitigation Actions:**
1. **Monitoring:** Projection lag dashboard (Grafana)
2. **Alerting:** Alert if lag > 30 seconds
3. **UI Indicator:** Display "Refreshing..." if lag > 5 seconds
4. **Optimization:** Batch projection updates (10 events at a time)
5. **Fallback:** Query event stream directly if projection stale

**Success Criteria:**
- Projection lag < 5 seconds (p95)
- Alerts trigger within 1 minute
- UI indicator visible to operators

---

### 2.3 Pick Transaction Ordering (Risk T-03)

**Mitigation Actions:**
1. **Code Review:** Mandatory review for all pick-related code
2. **Architecture Tests:** Automated tests verify transaction ordering
3. **Database Permissions:** ONLY StockLedger service can INSERT into stock_movement_events
4. **Documentation:** Clear guidelines in technical implementation doc
5. **Training:** Team training on transaction ordering

**Success Criteria:**
- Zero transaction ordering violations in testing
- Architecture tests passing
- Database permissions enforced

---

### 2.4 Concurrent Allocation Conflicts (Risk T-04)

**Mitigation Actions:**
1. **Optimistic Locking:** Use version numbers for reservations
2. **Re-Validation:** Re-validate balance on StartPicking
3. **Retry Logic:** Retry allocation on conflict (max 3 attempts)
4. **Monitoring:** Track allocation failure rate
5. **Alerting:** Alert if failure rate > 5%

**Success Criteria:**
- Allocation failure rate < 5%
- StartPicking re-validation catches conflicts
- Retry logic reduces failures

---

### 2.5 Agnum API Changes (Risk I-01)

**Mitigation Actions:**
1. **API Versioning:** Use versioned Agnum API endpoints
2. **Contract Testing:** Automated tests verify API contract
3. **Fallback:** CSV export if API fails
4. **Monitoring:** Track export success rate
5. **Alerting:** Alert administrator on failure

**Success Criteria:**
- Export success rate > 99%
- CSV fallback tested
- Contract tests passing

---

### 2.6 ERP Integration Complexity (Risk I-02)

**Mitigation Actions:**
1. **Spike Task:** Prototype ERP integration (3 days, before Phase 3)
2. **Contract Definition:** Clear anti-corruption layer contract
3. **Phased Rollout:** Start with read-only integration, then write
4. **Fallback:** Manual notification if integration fails
5. **Dedicated Resource:** Assign integration specialist

**Success Criteria:**
- Spike task completes successfully
- Contract agreed with ERP team
- Integration specialist assigned

---

### 2.7 Offline Sync Conflicts (Risk O-01)

**Mitigation Actions:**
1. **Operator Training:** Clear guidance on offline operations
2. **UI Guidance:** Show which operations allowed offline
3. **Reconciliation Report:** Clear, actionable error messages
4. **Monitoring:** Track conflict rate
5. **Alerting:** Alert if conflict rate > 10%

**Success Criteria:**
- Conflict rate < 10%
- Operators understand reconciliation report
- Training materials created

---

### 2.8 Edge Device Queue Limits (Risk O-02)

**Mitigation Actions:**
1. **Queue Monitoring:** Display queue size in UI
2. **Alerting:** Alert operator if queue > 80 commands
3. **Forced Sync:** Prompt operator to sync if queue > 90 commands
4. **Increased Limit:** Increase limit to 200 if needed
5. **Offline Duration Limit:** Alert if offline > 8 hours

**Success Criteria:**
- Queue never reaches limit in testing
- Operators sync regularly
- Alerts visible in UI

---

### 2.9 Negative Balances (Risk D-01)

**Mitigation Actions:**
1. **Balance Validation:** Strict validation in StockLedger
2. **Consistency Checks:** Daily automated checks
3. **Alerting:** P0 alert on negative balance
4. **Projection Rebuild:** Tool to rebuild projections
5. **Manual Adjustment:** Workflow to correct discrepancies

**Success Criteria:**
- Zero negative balances in testing
- Consistency checks run daily
- Rebuild tool tested

---

### 2.10 Orphaned Handling Units (Risk D-02)

**Mitigation Actions:**
1. **Saga Compensation:** ReceiveGoodsSaga creates orphan alert
2. **Consistency Checks:** Daily check for orphaned HUs
3. **Reconciliation Workflow:** Manual workflow to fix orphans
4. **Monitoring:** Track orphan rate
5. **Alerting:** Alert if orphan rate > 1%

**Success Criteria:**
- Orphan rate < 1%
- Reconciliation workflow tested
- Consistency checks run daily

---

### 2.11 3D Visualization Performance (Risk P-01)

**Mitigation Actions:**
1. **Spike Task:** Prototype 3D rendering (2 days, before Phase 5)
2. **Optimization:** Use LOD (level of detail) for distant bins
3. **Lazy Loading:** Load bins on-demand (viewport culling)
4. **Mesh Optimization:** Reduce polygon count
5. **Fallback:** 2D view if 3D too slow

**Success Criteria:**
- Initial load < 2 seconds for 1000 bins
- Frame rate > 30 FPS
- Browser memory < 300MB

---

### 2.12 Query Performance Degradation (Risk P-02)

**Mitigation Actions:**
1. **Index Optimization:** Add indexes on frequently queried columns
2. **Query Optimization:** Use EXPLAIN ANALYZE to optimize queries
3. **Caching:** Cache frequently accessed data (layout, categories)
4. **Archival:** Archive old events (> 1 year) to cold storage
5. **Read Replicas:** Use read replicas for queries

**Success Criteria:**
- Query latency < 100ms (p95)
- Database CPU < 70%
- Archival process tested

---

## 3. Spike Tasks

### Spike 1: Event Store Load Testing

**Duration:** 3 days

**Phase:** Phase 0 (Foundation)

**Goal:** Validate event store can handle 1000 events/second

**Deliverables:**
- Load test script (JMeter or k6)
- Performance report (latency, throughput, resource usage)
- Optimization recommendations

**Success Criteria:**
- Append latency < 50ms (p95)
- Sustained 1000 events/second for 1 hour
- Database CPU < 80%

---

### Spike 2: HU Projection Logic

**Duration:** 2 days

**Phase:** Phase 1 (Core Inventory)

**Goal:** Prototype projection from StockMoved events

**Deliverables:**
- Working prototype (code)
- Complexity assessment
- Performance estimate

**Success Criteria:**
- Projection updates correctly
- Idempotency verified
- Projection lag < 5 seconds

---

### Spike 3: ERP Integration Contract

**Duration:** 3 days

**Phase:** Before Phase 3 (Financial & Integration)

**Goal:** Define anti-corruption layer contract with ERP team

**Deliverables:**
- Integration contract document
- Sample payloads (MaterialRequested, MaterialReserved, MaterialConsumed)
- Mapping rules (ProductionOrder → Reservation)

**Success Criteria:**
- Contract agreed with ERP team
- Sample payloads validated
- Mapping rules documented

---

### Spike 4: 3D Rendering Performance

**Duration:** 2 days

**Phase:** Before Phase 5 (Visualization & Optimization)

**Goal:** Validate Three.js can render 1000 bins smoothly

**Deliverables:**
- Performance test (load 1000 bins)
- Optimization recommendations (LOD, culling)
- Fallback plan (2D view)

**Success Criteria:**
- Initial load < 2 seconds
- Frame rate > 30 FPS
- Browser memory < 300MB

---

## 4. Rollout Strategy

### 4.1 Deployment Approach

**Strategy:** Blue-Green Deployment with Feature Flags

**Rationale:**
- Zero-downtime deployment
- Instant rollback if issues detected
- Gradual feature enablement

**Infrastructure:**
- Blue environment: Current production
- Green environment: New version
- Load balancer: Routes traffic between blue/green
- Feature flags: Control feature visibility

---

### 4.2 Rollout Phases

#### Phase 0-1: Internal Testing (Weeks 1-10)

**Environment:** Staging

**Users:** Development team, QA team

**Features:** All Phase 0-1 features

**Success Criteria:**
- All acceptance tests passing
- Performance targets met
- Zero critical bugs

---

#### Phase 2: Pilot Rollout (Weeks 11-16)

**Environment:** Production (10% traffic)

**Users:** 5 warehouse operators (pilot group)

**Features:** Phase 0-2 features (Movement Ledger, Handling Units, Inbound, Transfer, Reservation, Pick)

**Success Criteria:**
- Pilot users complete 100 operations
- Zero data corruption
- Operator feedback positive

---

#### Phase 3: Gradual Rollout (Weeks 17-21)

**Environment:** Production (50% traffic)

**Users:** 25 warehouse operators

**Features:** Phase 0-3 features (add Valuation, Agnum Export, ERP Integration)

**Success Criteria:**
- 1000 operations completed
- Agnum export successful
- ERP integration working

---

#### Phase 4-5: Full Rollout (Weeks 22-30)

**Environment:** Production (100% traffic)

**Users:** All warehouse operators

**Features:** All features (add Offline, 3D Visualization, Observability)

**Success Criteria:**
- All operators trained
- All features enabled
- System stable for 2 weeks

---

### 4.3 Rollback Triggers

**Immediate Rollback (within 5 minutes):**
- Data corruption detected (negative balances, orphaned HUs)
- System unavailable (> 5 minutes downtime)
- Critical security vulnerability

**Planned Rollback (within 1 hour):**
- Performance degradation (> 50% slower)
- High error rate (> 5% of operations failing)
- Operator confusion (> 10 support tickets/hour)

**Rollback Procedure:** See Section 8

---

## 5. Feature Toggles

### 5.1 Feature Flag Strategy

**Tool:** LaunchDarkly or custom feature flag service

**Flags:**
- `enable_reservation_engine` (Phase 2)
- `enable_agnum_export` (Phase 3)
- `enable_erp_integration` (Phase 3)
- `enable_offline_operations` (Phase 4)
- `enable_3d_visualization` (Phase 5)

**Benefits:**
- Gradual feature enablement
- A/B testing
- Instant feature disable if issues

---

### 5.2 Feature Flag Configuration

**Example:**
```json
{
  "enable_reservation_engine": {
    "enabled": true,
    "rollout_percentage": 50,
    "user_whitelist": ["operator1", "operator2"]
  },
  "enable_offline_operations": {
    "enabled": false,
    "rollout_percentage": 0,
    "user_whitelist": []
  }
}
```

---

## 6. Go-Live Readiness Gates

### Gate 1: Phase 0 Complete (Week 4)

**Criteria:**
- ✅ Event store configured and tested
- ✅ Transactional outbox working
- ✅ Command pipeline implemented
- ✅ CI/CD pipeline operational
- ✅ All infrastructure tests passing

**Approval:** Tech Lead

---

### Gate 2: Phase 1 Complete (Week 10)

**Criteria:**
- ✅ Movement Ledger operational
- ✅ Handling Units operational
- ✅ Inbound and Transfer workflows working
- ✅ Projection lag < 5 seconds
- ✅ All acceptance tests passing
- ✅ User acceptance testing passed

**Approval:** Tech Lead + Product Owner

---

### Gate 3: Phase 2 Complete (Week 16)

**Criteria:**
- ✅ Reservation Engine operational
- ✅ Pick workflow working
- ✅ Transaction ordering enforced
- ✅ Concurrent allocation tested
- ✅ All acceptance tests passing
- ✅ Pilot rollout successful

**Approval:** Tech Lead + Product Owner + Warehouse Manager

---

### Gate 4: Phase 3 Complete (Week 21)

**Criteria:**
- ✅ Valuation Engine operational
- ✅ Agnum export working
- ✅ ERP integration working
- ✅ All acceptance tests passing
- ✅ Gradual rollout successful

**Approval:** Tech Lead + Product Owner + Warehouse Manager + Accountant

---

### Gate 5: Phase 4 Complete (Week 25)

**Criteria:**
- ✅ Offline operations working
- ✅ Sync engine working
- ✅ Conflict detection tested
- ✅ All acceptance tests passing

**Approval:** Tech Lead + Product Owner + Warehouse Manager

---

### Gate 6: Phase 5 Complete (Week 30)

**Criteria:**
- ✅ 3D visualization working
- ✅ Cycle counting working
- ✅ Observability operational
- ✅ Performance targets met
- ✅ All acceptance tests passing
- ✅ System stable for 2 weeks

**Approval:** Tech Lead + Product Owner + Warehouse Manager + System Administrator

---

## 7. Monitoring Strategy

### 7.1 Business Metrics

**Metrics:**
- Stock movements per hour
- Handling units created per day
- Reservations created per day
- Pick operations per hour
- Average pick time
- Goods receipt throughput

**Dashboard:** Operations Dashboard (Grafana)

**Alerts:**
- Throughput drops > 50%
- Average pick time > 5 minutes

---

### 7.2 Technical Metrics

**Metrics:**
- Event store write latency (p50, p95, p99)
- Projection lag (per projection)
- Command processing latency (per command type)
- Saga completion time (per saga type)
- Outbox delivery latency
- Database connection pool utilization

**Dashboard:** Technical Dashboard (Grafana)

**Alerts:**
- Event store latency > 100ms (p95)
- Projection lag > 30 seconds
- Command latency > 500ms (p95)

---

### 7.3 System Health Metrics

**Metrics:**
- CPU utilization
- Memory utilization
- Disk I/O
- Network I/O
- Database query latency

**Dashboard:** System Health Dashboard (Grafana)

**Alerts:**
- CPU > 80%
- Memory > 90%
- Disk I/O > 80%

---

### 7.4 Data Quality Metrics

**Metrics:**
- Negative balance count
- Orphaned HU count
- Consistency check failures
- Projection rebuild count

**Dashboard:** Data Quality Dashboard (Grafana)

**Alerts:**
- Negative balance detected (P0)
- Orphaned HU count > 10 (P1)
- Consistency check failure (P0)

---

## 8. Rollback Procedures

### 8.1 Immediate Rollback (< 5 minutes)

**Trigger:** Data corruption, system unavailable, critical security vulnerability

**Procedure:**
1. **Alert:** Notify on-call engineer (PagerDuty)
2. **Decision:** On-call engineer decides to rollback
3. **Execute:** Switch load balancer from green to blue environment
4. **Verify:** Check system health, run smoke tests
5. **Communicate:** Notify team and stakeholders

**Rollback Time:** < 5 minutes

---

### 8.2 Planned Rollback (< 1 hour)

**Trigger:** Performance degradation, high error rate, operator confusion

**Procedure:**
1. **Alert:** Notify on-call engineer
2. **Investigation:** Analyze logs, metrics, errors
3. **Decision:** Tech Lead decides to rollback
4. **Execute:** Switch load balancer from green to blue environment
5. **Verify:** Check system health, run smoke tests
6. **Communicate:** Notify team and stakeholders
7. **Post-Mortem:** Schedule post-mortem meeting

**Rollback Time:** < 1 hour

---

### 8.3 Database Migration Rollback

**Trigger:** Database migration fails or causes issues

**Procedure:**
1. **Alert:** Notify on-call engineer
2. **Decision:** Tech Lead decides to rollback
3. **Execute:** Run rollback migration script
4. **Verify:** Check database schema, run smoke tests
5. **Communicate:** Notify team and stakeholders

**Rollback Time:** < 30 minutes

**Note:** All database migrations MUST be reversible

---

## 9. Team Composition

### 9.1 Core Team (6-8 developers)

**Architect (Part-Time):**
- Reviews architecture decisions
- Provides guidance on complex issues
- Approves major changes

**Tech Lead (Full-Time):**
- Coordinates implementation
- Reviews all code
- Manages technical risks
- Approves go-live gates

**Senior Backend Developers (2):**
- Implement event sourcing
- Implement domain logic
- Mentor mid-level developers

**Mid-Level Backend Developers (2):**
- Implement APIs
- Implement integrations
- Write tests

**Frontend Developer (1):**
- Implement UI
- Implement 3D visualization
- Integrate with backend APIs

**QA Engineer (1):**
- Write automated tests
- Perform user acceptance testing
- Manage test environments

---

### 9.2 Extended Team

**Product Owner:**
- Define requirements
- Prioritize features
- Approve user acceptance testing

**DevOps Engineer:**
- Manage infrastructure
- Manage CI/CD pipeline
- Monitor system health

**Integration Specialist:**
- Implement ERP integration
- Implement Agnum integration
- Coordinate with external teams

**Warehouse Manager:**
- Provide domain expertise
- Approve go-live gates
- Train operators

**Accountant:**
- Provide financial requirements
- Approve Agnum export
- Approve go-live gates

---

## Summary

This document provides a comprehensive risk management and delivery strategy for the warehouse system. Key takeaways:

1. **12 identified risks** with mitigation strategies
2. **4 spike tasks** to reduce technical uncertainty
3. **Blue-green deployment** with feature flags for safe rollout
4. **6 go-live readiness gates** to ensure quality
5. **Comprehensive monitoring** (business, technical, system health, data quality)
6. **Clear rollback procedures** (< 5 minutes for critical issues)
7. **Defined team composition** (6-8 core developers + extended team)

**Risk management is ongoing** - review risk register monthly and update mitigation strategies as needed.

**End of Risk and Delivery Strategy Document**
