# Phased Delivery Plan

**Project:** LKvitai.MES Warehouse Management System  
**Document:** Phased Delivery Plan  
**Version:** 1.0  
**Date:** February 2026  
**Status:** Implementation Specification

---

## Document Purpose

This document defines the phased delivery strategy for the warehouse system, organizing feature groups into incremental releases that deliver business value early while managing technical risk.

**Delivery Principles:**
- Each phase delivers working, testable functionality
- Phases build on previous phases (dependencies respected)
- Early phases focus on foundation and core workflows
- Later phases add optimization and advanced features
- Each phase has clear exit criteria

---

## Phase Overview

```
Phase 0: Foundation (4 weeks)
├─ Event Store Infrastructure
├─ Core Domain Model
└─ Basic CRUD Operations

Phase 1: Core Inventory (6 weeks)
├─ Movement Ledger
├─ Handling Units
├─ Inbound Operations
└─ Basic Transfers

Phase 2: Reservation & Picking (6 weeks)
├─ Reservation Engine
├─ Allocation Logic
├─ Pick Workflows
└─ Process Managers

Phase 3: Financial & Integration (5 weeks)
├─ Valuation Engine
├─ Agnum Export
├─ MES/ERP Integration
└─ Reporting

Phase 4: Offline & Edge (4 weeks)
├─ Edge Agent
├─ Offline Operations
├─ Conflict Resolution
└─ Reconciliation

Phase 5: Visualization & Optimization (5 weeks)
├─ 3D Warehouse View
├─ Advanced Analytics
├─ Performance Tuning
└─ Observability

Total Duration: 30 weeks (~7.5 months)
```

---

## Phase 0: Foundation (4 weeks)

### Goals

Establish technical foundation for event-driven architecture and domain model implementation.

### Delivered Capabilities

**Infrastructure:**
- Event store (PostgreSQL or EventStoreDB)
- Transactional outbox pattern
- Event bus (RabbitMQ or Azure Service Bus)
- Command/query separation (CQRS)
- Basic API gateway

**Domain Model:**
- Aggregate base classes
- Value objects
- Domain events
- Command handlers
- Event handlers

**Development Environment:**
- CI/CD pipeline
- Automated testing framework
- Database migrations
- Local development setup

### Feature Groups Included

- None (infrastructure only)

### Technical Deliverables

**TD-00.1:** Event Store Setup
- Database schema for event streams
- Append-only event table with indexes
- Event versioning support
- Snapshot mechanism

**TD-00.2:** Transactional Outbox
- Outbox table schema
- Outbox processor (polling or CDC)
- At-least-once delivery guarantee
- Idempotency keys

**TD-00.3:** Command/Query Infrastructure
- Command bus implementation
- Query bus implementation
- Command validation pipeline
- Query optimization layer

**TD-00.4:** Testing Framework
- Unit test setup (xUnit, NUnit, or Jest)
- Integration test setup (Testcontainers)
- Property-based testing library (FsCheck or fast-check)
- Test data builders

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Event store performance issues | Medium | High | Spike: Load test with 1M events |
| Outbox processor lag | Medium | Medium | Implement monitoring, tune polling interval |
| Team unfamiliar with CQRS | High | Medium | Training session, pair programming |

### Exit Criteria

- ✅ Event store can append 1000 events/second
- ✅ Outbox processor delivers events within 1 second (p95)
- ✅ All infrastructure tests passing
- ✅ CI/CD pipeline deploys to staging environment
- ✅ Team trained on CQRS patterns

### Dependencies

- None (foundation phase)

---

## Phase 1: Core Inventory (6 weeks)

### Goals

Implement core inventory tracking with movement ledger, handling units, and basic inbound/transfer operations.

### Delivered Capabilities

**Business Value:**
- Receive goods from suppliers
- Track handling units with barcodes
- Transfer stock between locations
- Query current stock levels
- Audit trail of all movements

**User Stories:**
- As a warehouse operator, I can receive goods and assign them to locations
- As a warehouse operator, I can transfer pallets between bins
- As a warehouse manager, I can view current stock levels by location
- As an auditor, I can query movement history for any SKU

### Feature Groups Included

- **FG-01:** Movement Ledger (Event Store)
- **FG-02:** Warehouse Layout & Configuration
- **FG-03:** Handling Units (Physical Containers)
- **FG-07:** Read Models & Projections (LocationBalance, HandlingUnitLocation)
- **FG-09:** Inbound Operations (Receive Goods)
- **FG-10:** Movement Operations (Transfer only, Pick deferred to Phase 2)
- **FG-15:** Process Managers & Sagas (ReceiveGoodsSaga, TransferStockSaga)

### Implementation Tasks

**Week 1-2: Movement Ledger**
- Implement StockLedger aggregate
- RecordStockMovement command handler
- Balance validation logic
- StockMoved event schema
- Event stream indexes

**Week 2-3: Warehouse Layout**
- Implement WarehouseLayout aggregate
- Define bins, aisles, racks
- Location validation
- 3D coordinates storage

**Week 3-4: Handling Units**
- Implement HandlingUnit aggregate
- Create/AddLine/Seal operations
- HU projection from StockMoved events
- Barcode generation

**Week 4-5: Inbound Operations**
- ReceiveGoodsSaga implementation
- UI for goods receipt
- Label printing integration (basic)
- Validation and error handling

**Week 5-6: Transfer Operations**
- TransferStockSaga implementation
- UI for stock transfer
- Multi-line HU transfer logic
- Testing and bug fixes

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Projection lag causes stale reads | High | Medium | Implement projection lag monitoring, fallback to event stream query |
| Balance validation race conditions | Medium | High | Optimistic concurrency control, retry logic |
| HU projection complexity | Medium | Medium | Spike: Prototype projection logic |

### Exit Criteria

- ✅ Can receive 100 pallets/hour
- ✅ Can transfer 50 pallets/hour
- ✅ Projection lag < 5 seconds (p95)
- ✅ Zero negative balances in testing
- ✅ Audit trail query < 2 seconds
- ✅ All acceptance criteria for FG-01, FG-02, FG-03, FG-09, FG-10 met
- ✅ User acceptance testing passed

### Dependencies

- Phase 0 complete (infrastructure ready)

---

## Phase 2: Reservation & Picking (6 weeks)

### Goals

Implement reservation engine with hybrid locking and picking workflows for production orders.

### Delivered Capabilities

**Business Value:**
- Reserve stock for production orders
- Allocate handling units to reservations
- Pick stock with HARD lock safety
- Prevent conflicts during picking
- Track reservation consumption

**User Stories:**
- As a production planner, I can reserve materials for production orders
- As a warehouse operator, I can start picking against a reservation
- As a warehouse operator, I cannot pick stock that's hard-locked by another operator
- As a warehouse manager, I can see all active reservations and their status

### Feature Groups Included

- **FG-04:** Reservation Engine
- **FG-07:** Read Models & Projections (AvailableStock)
- **FG-10:** Movement Operations (Pick workflow)
- **FG-15:** Process Managers & Sagas (AllocationSaga, PickStockSaga)

### Implementation Tasks

**Week 1-2: Reservation Aggregate**
- Implement Reservation aggregate (event-sourced)
- CreateReservation command
- AllocateReservation command (SOFT lock)
- StartPicking command (SOFT → HARD transition)
- ConsumeReservation command
- Bumping logic

**Week 2-3: Allocation Logic**
- AllocationSaga implementation
- Query AvailableStock projection
- Find suitable HUs for allocation
- Handle insufficient stock scenarios
- Conflict detection

**Week 3-4: Pick Workflow**
- PickStockSaga implementation
- Enforce transaction ordering (Decision 2)
- UI for picking operations
- Validation and error handling

**Week 4-5: AvailableStock Projection**
- Project from StockMoved + StockAllocated events
- Compute available = physical - reserved
- Query optimization
- Real-time updates

**Week 5-6: Testing & Refinement**
- Concurrent allocation testing
- Bumping scenarios testing
- HARD lock enforcement testing
- Performance tuning

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Concurrent allocation conflicts | High | High | Optimistic locking, re-validation on StartPicking |
| Pick transaction ordering violations | Medium | Critical | Code review, architecture tests, database permissions |
| Bumping logic complexity | Medium | Medium | Comprehensive test scenarios, state machine validation |

### Exit Criteria

- ✅ Can allocate 100 reservations/hour
- ✅ Can pick 50 reservations/hour
- ✅ Zero HARD lock violations in testing
- ✅ Bumping logic works correctly (SOFT bumped, HARD not bumped)
- ✅ Pick transaction ordering enforced (StockLedger first)
- ✅ All acceptance criteria for FG-04 met
- ✅ User acceptance testing passed

### Dependencies

- Phase 1 complete (Movement Ledger, Handling Units operational)

---

## Phase 3: Financial & Integration (5 weeks)

### Goals

Implement financial valuation, Agnum export, and MES/ERP integration for end-to-end workflows.

### Delivered Capabilities

**Business Value:**
- Track unit costs and on-hand value
- Revalue stock (write-downs, landed costs)
- Export inventory to Agnum accounting
- Integrate with MES/ERP for material requests
- Financial reporting

**User Stories:**
- As an inventory accountant, I can adjust unit costs for SKUs
- As an inventory accountant, I can export stock snapshot to Agnum
- As a production planner (in ERP), I can request materials and see reservations created in warehouse
- As a warehouse manager, I can view on-hand value by location

### Feature Groups Included

- **FG-05:** Valuation Engine
- **FG-06:** Logical Warehouses & Categories
- **FG-07:** Read Models & Projections (OnHandValue, StockByCategory)
- **FG-13:** Financial Integration (Agnum Export)
- **FG-14:** Process Integration (MES/ERP)

### Implementation Tasks

**Week 1-2: Valuation Engine**
- Implement Valuation aggregate (event-sourced)
- ApplyCostAdjustment command
- AllocateLandedCost command
- WriteDownStock command
- Approval workflow

**Week 2-3: Logical Warehouses & Categories**
- Implement LogicalWarehouse aggregate
- Category assignment
- Multi-categorization support
- Agnum mapping configuration

**Week 3-4: Agnum Export**
- AgnumExportSaga implementation
- Query balances, costs, categories
- Generate CSV export
- Agnum API integration
- Reconciliation report email

**Week 4-5: MES/ERP Integration**
- Anti-corruption layer implementation
- MaterialRequested → CreateReservation translation
- StockMoved → MaterialConsumed translation
- Mapping storage (ProductionOrder → Reservation)
- Error handling and retry logic

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Agnum API changes | Medium | Medium | Version API calls, maintain backward compatibility |
| ERP integration complexity | High | High | Spike: Prototype integration, define clear contract |
| Cost adjustment approval workflow | Low | Medium | Reuse existing IAM, simple approval table |

### Exit Criteria

- ✅ Can revalue 100 SKUs in batch
- ✅ Agnum export completes in < 60 seconds
- ✅ ERP material request creates reservation within 30 seconds
- ✅ On-hand value query < 500ms
- ✅ All acceptance criteria for FG-05, FG-06, FG-13, FG-14 met
- ✅ User acceptance testing passed

### Dependencies

- Phase 2 complete (Reservation Engine operational)

---

## Phase 4: Offline & Edge (4 weeks)

### Goals

Enable offline warehouse operations with conflict detection and reconciliation.

### Delivered Capabilities

**Business Value:**
- Continue picking during network outages
- Queue operations offline
- Detect conflicts on reconnect
- Reconciliation report for operators

**User Stories:**
- As a warehouse operator, I can pick stock offline (if reservation already started)
- As a warehouse operator, I can transfer stock offline (if HU assigned to me)
- As a warehouse operator, I see a reconciliation report when I reconnect
- As a warehouse operator, I cannot allocate reservations offline (blocked)

### Feature Groups Included

- **FG-16:** Edge/Offline Agent

### Implementation Tasks

**Week 1: Edge Agent Infrastructure**
- Local SQLite database setup
- Offline command queue schema
- Cache management (reservations, HUs)
- Sync protocol design

**Week 2: Offline Operations**
- Implement allowed commands (PickStock, TransferStock)
- Block forbidden commands (AllocateReservation, etc.)
- Local validation logic
- Queue management

**Week 3: Sync & Conflict Detection**
- Sync protocol implementation
- Server-side re-validation
- Conflict detection rules
- Reconciliation report generation

**Week 4: Testing & Refinement**
- Offline scenario testing
- Conflict resolution testing
- Queue size limits
- Performance tuning

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Sync conflicts frequent | High | Medium | Clear operator guidance, reconciliation UI |
| Offline duration too long | Medium | Medium | Alert if offline > 8 hours, force sync |
| Cache staleness | Medium | Medium | Cache expiration, re-fetch on reconnect |

### Exit Criteria

- ✅ Can operate offline for 8 hours
- ✅ Queue up to 100 commands offline
- ✅ Sync completes in < 30 seconds for 100 commands
- ✅ Conflict detection works correctly (all scenarios tested)
- ✅ Reconciliation report clear and actionable
- ✅ All acceptance criteria for FG-16 met
- ✅ User acceptance testing passed

### Dependencies

- Phase 2 complete (Reservation and Pick workflows operational)

---

## Phase 5: Visualization & Optimization (5 weeks)

### Goals

Deliver 3D warehouse visualization, advanced analytics, and performance optimization.

### Delivered Capabilities

**Business Value:**
- Interactive 3D warehouse view
- Real-time stock status visualization
- Advanced reporting and analytics
- Performance optimization
- Comprehensive observability

**User Stories:**
- As a warehouse manager, I can view 3D warehouse layout with real-time stock status
- As a warehouse manager, I can click on a bin to see contents
- As a warehouse manager, I can run advanced reports (movement trends, accuracy metrics)
- As a system administrator, I can monitor system health and performance

### Feature Groups Included

- **FG-08:** 3D Warehouse Visualization
- **FG-11:** Inventory Adjustments & Cycle Counting
- **FG-12:** Operational Integration (Labels, Scanners, Equipment)
- **FG-17:** Observability & Audit
- **FG-18:** Security & Permissions

### Implementation Tasks

**Week 1-2: 3D Visualization**
- Three.js or Babylon.js setup
- Render warehouse layout
- Display handling units
- Color-coding by status
- Click-to-drill-down
- WebSocket real-time updates

**Week 2-3: Cycle Counting & Adjustments**
- Cycle count workflow
- Adjustment wizard
- Accuracy metrics
- Approval workflow

**Week 3-4: Observability**
- Consistency checks (self-test)
- Projection lag monitoring
- Metrics collection (Prometheus)
- Dashboards (Grafana)
- Alert rules

**Week 4-5: Performance Optimization**
- Query optimization
- Index tuning
- Projection optimization
- Load testing
- Capacity planning

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| 3D rendering performance | Medium | Medium | Optimize mesh count, use LOD (level of detail) |
| Observability overhead | Low | Low | Sampling, async metrics collection |
| Performance bottlenecks | Medium | Medium | Load testing early, identify hotspots |

### Exit Criteria

- ✅ 3D view loads in < 2 seconds for 1000 bins
- ✅ Real-time updates within 1 second
- ✅ Consistency checks run daily without issues
- ✅ All metrics collected and dashboards operational
- ✅ System handles 1000 movements/hour sustained
- ✅ All acceptance criteria for FG-08, FG-11, FG-12, FG-17, FG-18 met
- ✅ User acceptance testing passed

### Dependencies

- Phase 1-4 complete (all core functionality operational)

---

## Cross-Phase Considerations

### Testing Strategy

**Per Phase:**
- Unit tests (80%+ coverage)
- Integration tests (key workflows)
- Property-based tests (invariants)
- User acceptance testing

**End-to-End:**
- Load testing (Phase 5)
- Security testing (Phase 5)
- Disaster recovery testing (Phase 5)

### Documentation

**Per Phase:**
- API documentation (Swagger/OpenAPI)
- User guides (per feature)
- Deployment guides

**End-to-End:**
- System architecture document (updated)
- Operations runbook
- Training materials

### Deployment Strategy

**Per Phase:**
- Deploy to staging environment
- User acceptance testing
- Deploy to production (blue-green or canary)

**Rollback Plan:**
- Database migrations reversible
- Feature flags for new functionality
- Rollback procedure documented

---

## Risk Management

### High-Priority Risks (Across All Phases)

| Risk | Mitigation | Owner |
|------|------------|-------|
| Event store performance | Load testing in Phase 0, optimize indexes | Tech Lead |
| Pick transaction ordering violations | Code review, architecture tests, database permissions | Architect |
| Concurrent allocation conflicts | Optimistic locking, comprehensive testing | Tech Lead |
| ERP integration complexity | Spike in Phase 3, clear contract definition | Integration Lead |
| Offline sync conflicts | Clear operator guidance, reconciliation UI | Product Owner |

### Suggested Spike Tasks (Before Implementation)

**Spike 1: Event Store Load Testing (Phase 0)**
- Duration: 3 days
- Goal: Validate event store can handle 1000 events/second
- Deliverable: Load test report, performance recommendations

**Spike 2: HU Projection Logic (Phase 1)**
- Duration: 2 days
- Goal: Prototype projection from StockMoved events
- Deliverable: Working prototype, complexity assessment

**Spike 3: ERP Integration Contract (Phase 3)**
- Duration: 3 days
- Goal: Define anti-corruption layer contract with ERP team
- Deliverable: Integration contract document, sample payloads

**Spike 4: 3D Rendering Performance (Phase 5)**
- Duration: 2 days
- Goal: Validate Three.js can render 1000 bins smoothly
- Deliverable: Performance test, optimization recommendations

---

## Parallelization Opportunities

### Phase 1 (Core Inventory)

**Parallel Tracks:**
- Track A: Movement Ledger + Warehouse Layout (2 developers)
- Track B: Handling Units + Projections (2 developers)
- Track C: Inbound Operations + Transfer Operations (2 developers)

### Phase 2 (Reservation & Picking)

**Parallel Tracks:**
- Track A: Reservation Aggregate + Allocation Logic (2 developers)
- Track B: Pick Workflow + AvailableStock Projection (2 developers)

### Phase 3 (Financial & Integration)

**Parallel Tracks:**
- Track A: Valuation Engine + Logical Warehouses (2 developers)
- Track B: Agnum Export (1 developer)
- Track C: MES/ERP Integration (2 developers)

### Phase 4 (Offline & Edge)

**Sequential** (Edge Agent is single component, limited parallelization)

### Phase 5 (Visualization & Optimization)

**Parallel Tracks:**
- Track A: 3D Visualization (2 developers)
- Track B: Cycle Counting + Observability (2 developers)
- Track C: Performance Optimization (1 developer)

---

## Recommended Team Composition

**Core Team (6-8 developers):**
- 1 Architect (part-time, reviews and guidance)
- 1 Tech Lead (full-time, coordinates implementation)
- 2 Senior Backend Developers (event sourcing, domain logic)
- 2 Mid-Level Backend Developers (APIs, integrations)
- 1 Frontend Developer (UI, 3D visualization)
- 1 QA Engineer (testing, automation)

**Extended Team:**
- 1 Product Owner (requirements, acceptance testing)
- 1 DevOps Engineer (infrastructure, CI/CD)
- 1 Integration Specialist (ERP/Agnum integration)

---

## Summary

This phased delivery plan provides a structured approach to implementing the warehouse system over 30 weeks, with clear goals, deliverables, and exit criteria for each phase. Early phases focus on foundation and core workflows, while later phases add optimization and advanced features.

**Next Document:** 30-epics-and-stories.md (Detailed Epics and User Stories)

