# Production-Ready Warehouse Tasks - Master Index

**Version:** 1.0  
**Date:** February 10, 2026  
**Source:** prod-ready-universe.md  
**Total Tasks:** 180+  
**Total Duration:** ~39 weeks (9.75 months)

---

## Complete Task Index

### Foundation Tasks (PRD-0001 to PRD-0010) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0001 | Idempotency Framework Completion | M | None | Backend/API | 1.5 |
| PRD-0002 | Event Schema Versioning | M | None | Backend/API | 1.5 |
| PRD-0003 | Correlation/Trace Propagation | S | None | Infra/DevOps | 1.5 |
| PRD-0004 | Projection Rebuild Hardening | M | None | Projections | 1.5 |
| PRD-0005 | RBAC Permission Model Finalization | M | None | Backend/API | 1.5 |
| PRD-0006 | Integration Test Harness | L | None | QA | 1.5 |
| PRD-0007 | Contract Test Framework | M | None | QA | 1.5 |
| PRD-0008 | Sample Data Seeding | S | None | QA | 1.5 |
| PRD-0009 | Observability Metrics Setup | M | None | Infra/DevOps | 1.5 |
| PRD-0010 | Backup & Disaster Recovery | M | None | Infra/DevOps | 1.5 |

### Epic C: Valuation (PRD-0100 to PRD-0120) - 4 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0100 | Valuation Domain Model & Events | M | PRD-0001, PRD-0002 | Backend/API | 1.5 |
| PRD-0101 | ItemValuation Aggregate Implementation | M | PRD-0100 | Backend/API | 1.5 |
| PRD-0102 | Cost Adjustment Command & Handler | M | PRD-0101 | Backend/API | 1.5 |
| PRD-0103 | Landed Cost Allocation Logic | L | PRD-0101 | Backend/API | 1.5 |
| PRD-0104 | Write-Down Workflow & Approval | M | PRD-0101 | Backend/API | 1.5 |
| PRD-0105 | OnHandValue Projection | M | PRD-0101 | Projections | 1.5 |
| PRD-0106 | Valuation History Projection | S | PRD-0101 | Projections | 1.5 |
| PRD-0107 | COGS Calculation Integration | M | PRD-0101 | Integration | 1.5 |
| PRD-0108 | Valuation API Endpoints | M | PRD-0102, PRD-0103, PRD-0104 | Backend/API | 1.5 |
| PRD-0109 | Valuation UI - Cost Adjustment Form | M | PRD-0108 | UI | 1.5 |
| PRD-0110 | Valuation UI - Landed Cost Allocation | M | PRD-0108 | UI | 1.5 |
| PRD-0111 | Valuation UI - Write-Down Approval | M | PRD-0108 | UI | 1.5 |
| PRD-0112 | On-Hand Value Report | S | PRD-0105 | UI | 1.5 |
| PRD-0113 | Cost Adjustment History Report | S | PRD-0106 | UI | 1.5 |
| PRD-0114 | Valuation Security & RBAC | S | PRD-0005 | Backend/API | 1.5 |
| PRD-0115 | Valuation Integration Tests | M | PRD-0108 | QA | 1.5 |
| PRD-0116 | Valuation Migration & Seed Data | S | PRD-0101 | Infra/DevOps | 1.5 |
| PRD-0117 | Valuation Event Handlers | M | PRD-0101 | Backend/API | 1.5 |
| PRD-0118 | Valuation Approval Workflow | M | PRD-0104 | Backend/API | 1.5 |
| PRD-0119 | Valuation Observability | S | PRD-0009 | Infra/DevOps | 1.5 |
| PRD-0120 | Valuation Documentation | S | PRD-0115 | QA | 1.5 |

### Epic D: Agnum Integration (PRD-0200 to PRD-0215) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0200 | Agnum Export Configuration Model | S | PRD-0105 | Backend/API | 1.5 |
| PRD-0201 | Agnum Mapping Configuration | M | PRD-0200 | Backend/API | 1.5 |
| PRD-0202 | Agnum Export Saga Implementation | L | PRD-0201 | Backend/API | 1.5 |
| PRD-0203 | CSV Export Generation | M | PRD-0202 | Backend/API | 1.5 |
| PRD-0204 | Agnum API Integration (JSON) | M | PRD-0202 | Integration | 1.5 |
| PRD-0205 | Export Scheduler (Daily Job) | S | PRD-0202 | Infra/DevOps | 1.5 |
| PRD-0206 | Export History Logging | S | PRD-0202 | Backend/API | 1.5 |
| PRD-0207 | Export Retry Logic | M | PRD-0202 | Backend/API | 1.5 |
| PRD-0208 | Agnum UI - Configuration Page | M | PRD-0201 | UI | 1.5 |
| PRD-0209 | Agnum UI - Export History | S | PRD-0206 | UI | 1.5 |
| PRD-0210 | Agnum UI - Reconciliation Report | M | PRD-0203 | UI | 1.5 |
| PRD-0211 | Agnum Export Tests | M | PRD-0203 | QA | 1.5 |
| PRD-0212 | Agnum API Mock (Testing) | S | PRD-0204 | QA | 1.5 |
| PRD-0213 | Agnum Security (API Key Encryption) | S | PRD-0005 | Backend/API | 1.5 |
| PRD-0214 | Agnum Observability | S | PRD-0009 | Infra/DevOps | 1.5 |
| PRD-0215 | Agnum Documentation | S | PRD-0211 | QA | 1.5 |

### Epic A: Outbound/Shipment (PRD-0300 to PRD-0325) - 3 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0300 | OutboundOrder Entity & Schema | M | PRD-0001 | Backend/API | 1.5 |
| PRD-0301 | Shipment Entity & Schema | M | PRD-0300 | Backend/API | 1.5 |
| PRD-0302 | OutboundOrder State Machine | M | PRD-0300 | Backend/API | 1.5 |
| PRD-0303 | Shipment State Machine | M | PRD-0301 | Backend/API | 1.5 |
| PRD-0304 | Pack Order Command & Handler | L | PRD-0302 | Backend/API | 1.5 |
| PRD-0305 | Generate Shipping Label Command | M | PRD-0303 | Backend/API | 1.5 |
| PRD-0306 | Dispatch Shipment Command | M | PRD-0303 | Backend/API | 1.5 |
| PRD-0307 | Confirm Delivery Command | S | PRD-0303 | Backend/API | 1.5 |
| PRD-0308 | Outbound Events (Packed, Dispatched, Delivered) | M | PRD-0304, PRD-0306 | Backend/API | 1.5 |
| PRD-0309 | Carrier API Integration (FedEx) | L | PRD-0305 | Integration | 1.5 |
| PRD-0310 | Shipping Label Generation (ZPL/PDF) | M | PRD-0305 | Backend/API | 1.5 |
| PRD-0311 | Proof of Delivery Storage (Blob) | S | PRD-0307 | Infra/DevOps | 1.5 |
| PRD-0312 | Outbound API Endpoints | M | PRD-0304, PRD-0306 | Backend/API | 1.5 |
| PRD-0313 | Outbound UI - Orders List | M | PRD-0312 | UI | 1.5 |
| PRD-0314 | Outbound UI - Create Order Form | M | PRD-0312 | UI | 1.5 |
| PRD-0315 | Outbound UI - Packing Station | L | PRD-0312 | UI | 1.5 |
| PRD-0316 | Outbound UI - Dispatch Confirmation | M | PRD-0312 | UI | 1.5 |
| PRD-0317 | Outbound Reports (Summary, Late Shipments) | M | PRD-0312 | UI | 1.5 |
| PRD-0318 | Outbound Security & RBAC | S | PRD-0005 | Backend/API | 1.5 |
| PRD-0319 | Outbound Integration Tests | L | PRD-0312 | QA | 1.5 |
| PRD-0320 | Carrier API Mock (Testing) | M | PRD-0309 | QA | 1.5 |
| PRD-0321 | Outbound Migration & Seed Data | S | PRD-0300 | Infra/DevOps | 1.5 |
| PRD-0322 | Outbound Observability | M | PRD-0009 | Infra/DevOps | 1.5 |
| PRD-0323 | Outbound Documentation | S | PRD-0319 | QA | 1.5 |
| PRD-0324 | Outbound Saga (Pack → Dispatch) | M | PRD-0304 | Backend/API | 1.5 |
| PRD-0325 | Customer Notification (Email/SMS) | M | PRD-0308 | Integration | 1.5 |

### Epic B: Sales Orders (PRD-0400 to PRD-0425) - 3 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0400 | Customer Entity & Schema | M | PRD-0001 | Backend/API | 1.5 |
| PRD-0401 | SalesOrder Entity & Schema | M | PRD-0400 | Backend/API | 1.5 |
| PRD-0402 | SalesOrder State Machine | M | PRD-0401 | Backend/API | 1.5 |
| PRD-0403 | Create Sales Order Command | M | PRD-0402 | Backend/API | 1.5 |
| PRD-0404 | Allocation Saga Implementation | L | PRD-0403 | Backend/API | 1.5 |
| PRD-0405 | Release to Picking Command | M | PRD-0404 | Backend/API | 1.5 |
| PRD-0406 | Sales Order Events (Created, Allocated, Shipped) | M | PRD-0403, PRD-0405 | Backend/API | 1.5 |
| PRD-0407 | Link SalesOrder to OutboundOrder | M | PRD-0300, PRD-0401 | Backend/API | 1.5 |
| PRD-0408 | Customer API Endpoints | M | PRD-0400 | Backend/API | 1.5 |
| PRD-0409 | Sales Order API Endpoints | M | PRD-0403, PRD-0405 | Backend/API | 1.5 |
| PRD-0410 | Customer UI - List & CRUD | M | PRD-0408 | UI | 1.5 |
| PRD-0411 | Sales Order UI - List | M | PRD-0409 | UI | 1.5 |
| PRD-0412 | Sales Order UI - Create Form | L | PRD-0409 | UI | 1.5 |
| PRD-0413 | Sales Order UI - Details Page | M | PRD-0409 | UI | 1.5 |
| PRD-0414 | Sales Order UI - Allocation Dashboard | M | PRD-0404 | UI | 1.5 |
| PRD-0415 | Sales Order Reports (Summary, Pending Stock) | M | PRD-0409 | UI | 1.5 |
| PRD-0416 | Sales Order Security & RBAC | S | PRD-0005 | Backend/API | 1.5 |
| PRD-0417 | Sales Order Integration Tests | L | PRD-0409 | QA | 1.5 |
| PRD-0418 | Sales Order Migration & Seed Data | S | PRD-0400 | Infra/DevOps | 1.5 |
| PRD-0419 | Sales Order Observability | M | PRD-0009 | Infra/DevOps | 1.5 |
| PRD-0420 | Sales Order Documentation | S | PRD-0417 | QA | 1.5 |
| PRD-0421 | Billing Integration (Invoice Trigger) | M | PRD-0406 | Integration | 1.5 |
| PRD-0422 | Customer Notification (Order Confirmation) | M | PRD-0406 | Integration | 1.5 |
| PRD-0423 | Credit Limit Check | M | PRD-0403 | Backend/API | 1.5 |
| PRD-0424 | Backorder Management (PENDING_STOCK) | M | PRD-0404 | Backend/API | 1.5 |
| PRD-0425 | Sales Order Approval Workflow | M | PRD-0423 | Backend/API | 1.5 |

### Epic E: 3D Visualization (PRD-0500 to PRD-0515) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0500 | Location Coordinates Schema | S | None | Backend/API | 1.5 |
| PRD-0501 | WarehouseLayout Configuration Model | M | PRD-0500 | Backend/API | 1.5 |
| PRD-0502 | 3D Visualization API Endpoint | M | PRD-0501 | Backend/API | 1.5 |
| PRD-0503 | Location Coordinates Migration | S | PRD-0500 | Infra/DevOps | 1.5 |
| PRD-0504 | 3D Rendering (Three.js Integration) | L | PRD-0502 | UI | 1.5 |
| PRD-0505 | 2D Floor Plan (SVG/Canvas) | M | PRD-0502 | UI | 1.5 |
| PRD-0506 | Interactive Click-to-Details | M | PRD-0504 | UI | 1.5 |
| PRD-0507 | Color Coding by Status | M | PRD-0504 | UI | 1.5 |
| PRD-0508 | Search & Highlight Location | M | PRD-0504 | UI | 1.5 |
| PRD-0509 | Camera Controls (Rotate, Zoom, Pan) | S | PRD-0504 | UI | 1.5 |
| PRD-0510 | Layout Configuration UI | M | PRD-0501 | UI | 1.5 |
| PRD-0511 | 3D Visualization Tests | M | PRD-0504 | QA | 1.5 |
| PRD-0512 | 3D Performance Optimization | M | PRD-0504 | UI | 1.5 |
| PRD-0513 | 3D Mobile Responsiveness | M | PRD-0504 | UI | 1.5 |
| PRD-0514 | 3D Observability | S | PRD-0009 | Infra/DevOps | 1.5 |
| PRD-0515 | 3D Documentation | S | PRD-0511 | QA | 1.5 |

