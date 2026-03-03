# LKvitai.MES — Process Documentation Root

This directory contains **internal process documentation** for the LKvitai.MES Warehouse module.
It is intended for developers, architects, and business analysts.

> **User-facing manuals (Lithuanian)** are in [`docs/manuals/`](../manuals/README.md).

---

## Directory Layout

```
docs/process/
├── README.md                        ← this file
├── specs/                           ← process-related specs and implementation plans
├── testing/                         ← process-related testing plans
├── universe/                        ← process discovery artefacts
│   ├── process-universe.md          ← pointer to authoritative universe doc
│   ├── ui-route-index.md            ← all Blazor @page routes
│   ├── api-route-index.md           ← all ASP.NET Core controller routes
│   ├── nav-index.md                 ← NavMenu structure
│   └── gaps-and-unknowns.md         ← items not provable from repo
└── processes/                       ← one folder per top-level process
    ├── P-01-goods-receiving-inbound/
    ├── P-02-putaway-location-assignment/
    ├── P-03-outbound-order-fulfillment/
    ├── P-04-internal-stock-transfer/
    ├── P-05-cycle-count-stock-reconciliation/
    ├── P-06-stock-adjustments-writeoffs/
    ├── P-07-inventory-valuation-costing/
    ├── P-08-agnum-integration-reconciliation/
    ├── P-09-returns-rma/
    ├── P-10-cross-dock-operations/
    ├── P-11-lot-serial-traceability/
    ├── P-12-warehouse-visualization-location-discovery/
    ├── P-13-reporting-analytics/
    ├── P-14-system-administration-compliance/
    └── P-15-master-data-management/
```

## Process List

| ID | Process | Status |
|----|---------|--------|
| P-01 | Goods Receiving (Inbound) | 🟡 Placeholder |
| P-02 | Putaway & Location Assignment | 🟡 Placeholder |
| P-03 | Outbound Order Fulfillment | 🟡 Placeholder |
| P-04 | Internal Stock Transfer | 🟡 Placeholder |
| P-05 | Cycle Count / Stock Reconciliation | 🟡 Placeholder |
| P-06 | Stock Adjustments & Write-offs | 🟡 Placeholder |
| P-07 | Inventory Valuation & Costing | 🟡 Placeholder |
| P-08 | Agnum Integration & Reconciliation | 🟡 Placeholder |
| P-09 | Returns / RMA | 🟡 Placeholder |
| P-10 | Cross-Dock Operations | 🟡 Placeholder |
| P-11 | Lot & Serial Number Traceability | 🟡 Placeholder |
| P-12 | Warehouse Visualization & Location Discovery | 🟡 Placeholder |
| P-13 | Reporting & Analytics | 🟡 Placeholder |
| P-14 | System Administration & Compliance | 🟡 Placeholder |
| P-15 | Master Data Management | 🟡 Placeholder |

## Conventions

- Every section must cite **evidence**: file path + route or controller.
- Do NOT add business logic that cannot be traced to the repo.
- Unknown / unimplemented items → `universe/gaps-and-unknowns.md`.
- Architecture is frozen at **v2.0 FINAL BASELINE** (`docs/04-system-architecture.md`). Do not redesign.
