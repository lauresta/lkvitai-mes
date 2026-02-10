# Production-Ready Warehouse Tasks - Part 1: Overview & Foundation

**Version:** 1.0  
**Date:** February 10, 2026  
**Source:** prod-ready-universe.md  
**Purpose:** Executable implementation task plan for production-ready WMS features

---

## 1. Overview

### 1.1 Phases

**Phase 1.5 (Must-Have for Production - 14 weeks):**
- Epic C: Valuation & Revaluation (4 weeks)
- Epic D: Agnum Accounting Integration (2 weeks)
- Epic A: Outbound/Shipment/Dispatch (3 weeks)
- Epic B: Sales Orders / Customer Orders (3 weeks)
- Epic E: 3D/2D Warehouse Visualization (2 weeks)

**Phase 2 (Operational Excellence - 8 weeks):**
- Epic M: Cycle Counting (2 weeks)
- Epic N: Returns / RMA (2 weeks)
- Epic G: Label Printing (1 week)
- Epic F: Inter-Warehouse Transfers (1 week)
- Epic O: Advanced Reporting & Audit (2 weeks)

**Phase 3 (Advanced Features - 12 weeks):**
- Epic H: Wave Picking (3 weeks)
- Epic I: Cross-Docking (2 weeks)
- Epic J: Multi-Level QC Approvals (2 weeks)
- Epic K: Handling Unit Hierarchy (2 weeks)
- Epic L: Serial Number Tracking (3 weeks)

**Phase 4 (Enterprise Hardening - 5 weeks):**
- Epic P: Admin & Configuration (2 weeks)
- Epic Q: Security Hardening (3 weeks)

**Total Duration:** ~39 weeks (9.75 months)

### 1.2 Assumptions (from Universe §0)

1. **Single Physical Warehouse, Multiple Logical Warehouses**
2. **B2B and Production-Focused, with B2C Extensibility**
3. **Accounting Integration Required (Agnum)**
4. **Offline Edge Operation Required** (limited scope)
5. **Event Sourcing for Auditability**
6. **Technology Stack:** .NET 8+, PostgreSQL, Marten, EF Core, MassTransit, Blazor Server
7. **3D Visualization is Core Value Proposition**

---

## 2. Dependency Graph

### 2.1 Epic Dependencies

```
Foundation Tasks (PRD-0001 to PRD-0010)
    ↓
Epic C: Valuation (PRD-0100 to PRD-0120)
    ↓
Epic D: Agnum Integration (PRD-0200 to PRD-0215)
    ↓
Epic A: Outbound/Shipment (PRD-0300 to PRD-0325)
    ↓
Epic B: Sales Orders (PRD-0400 to PRD-0425)
    ↓
Epic E: 3D Visualization (PRD-0500 to PRD-0515) [Can parallelize with A/B]
    ↓
Epic M: Cycle Counting (PRD-0600 to PRD-0615)
Epic N: Returns/RMA (PRD-0700 to PRD-0715)
Epic G: Label Printing (PRD-0800 to PRD-0810)
Epic F: Inter-Warehouse Transfers (PRD-0900 to PRD-0910)
Epic O: Advanced Reporting (PRD-1000 to PRD-1015)
    ↓
Epic H: Wave Picking (PRD-1100 to PRD-1115)
Epic I: Cross-Docking (PRD-1200 to PRD-1210)
Epic J: Multi-Level QC (PRD-1300 to PRD-1315)
Epic K: HU Hierarchy (PRD-1400 to PRD-1415)
Epic L: Serial Tracking (PRD-1500 to PRD-1520)
    ↓
Epic P: Admin Config (PRD-1600 to PRD-1610)
Epic Q: Security Hardening (PRD-1700 to PRD-1720)
```

### 2.2 Critical Path

**Foundation → Valuation → Agnum → Outbound → Sales Orders**

This is the minimum viable path for production B2B/B2C operations.

### 2.3 Blockers

- **Agnum Integration** blocked by Valuation (needs cost data)
- **Sales Orders** blocked by Outbound (needs shipment workflow)
- **Returns/RMA** blocked by Sales Orders (needs order entity)
- **Wave Picking** blocked by Phase 1 Picking (already exists)
- **COGS Calculation** blocked by Valuation (needs cost data)

---

## 3. Task Index - Foundation Tasks (PRD-0001 to PRD-0010)

| TaskId | Epic | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|------|-------|-----|--------------|-----------|------------|
| PRD-0001 | Foundation | Idempotency Framework Completion | M | None | Backend/API | Universe §5 |
| PRD-0002 | Foundation | Event Schema Versioning | M | None | Backend/API | Universe §5 |
| PRD-0003 | Foundation | Correlation/Trace Propagation | S | None | Infra/DevOps | Universe §5 |
| PRD-0004 | Foundation | Projection Rebuild Hardening | M | None | Projections | Universe §5 |
| PRD-0005 | Foundation | RBAC Permission Model Finalization | M | None | Backend/API | Universe §5 |
| PRD-0006 | Foundation | Integration Test Harness | L | None | QA | Universe §5 |
| PRD-0007 | Foundation | Contract Test Framework | M | None | QA | Universe §5 |
| PRD-0008 | Foundation | Sample Data Seeding | S | None | QA | Universe §5 |
| PRD-0009 | Foundation | Observability Metrics Setup | M | None | Infra/DevOps | Universe §5 |
| PRD-0010 | Foundation | Backup & Disaster Recovery | M | None | Infra/DevOps | Universe §5 |

