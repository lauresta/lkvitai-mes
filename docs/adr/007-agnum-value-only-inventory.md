# ADR-007: Agnum Keeps Inventory by Value Only; Warehouse Is the Single Source of Truth for Stock

**Status:** Accepted
**Date:** 2026-11-20
**Decision Makers:** Owner (D. Bykovas)
**Relates To:** Warehouse module, Agnum integration, ADR-006

---

## Context

Until now the plan was to keep item lists and stock quantities in both systems: Warehouse imported products,
prices and balances from Agnum, distributed "virtual Agnum balances" into locations, exported a per-SKU stock
snapshot back, and reconciled per SKU. Two sources of truth for the same quantities produced an integration that
was large, fragile, and dead in practice (the vendor API has returned 500 since 2026-08-01).

## Decision

1. Agnum keeps inventory **by value only** ("suminė apskaita"). No item lists, quantities or stock balances are
   imported from Agnum or exported to Agnum.
2. Warehouse is the single source of truth for items, prices, quantities and stock value.
3. Warehouse pushes each **purchase document** to Agnum as a document without item lines: document number, date,
   supplier, currency and rate, net, VAT, total. The document arrives in Agnum already posted.
4. The Agnum document date is the supplier document date.
5. Agnum warehouse, posting scheme, accounts and VAT codes are configuration edited in the Warehouse UI.
6. Receipts may be entered in any currency. The rate is the official Bank of Lithuania rate for the document date.
   Both the document-currency amounts and the EUR amounts are stored.
7. Suppliers are created in Agnum first; Warehouse reads the supplier list from Agnum.
8. Production starts on 2027-01-01 as a fresh start, not a migration of history.

ADR-006 stays in force: the integration is document-based with an explicit outbox. This ADR narrows it to
purchase documents and removes the item/balance side entirely.

## Consequences

- Warehouse becomes an accounting sub-ledger: stock value must be kept as EUR amounts per operation, not only as a
  unit cost, and must reconcile to the Agnum stock accounts to the cent.
- Month end needs a stock value statement, valued write-off acts and an explicit month close in Warehouse.
- Intrastat arrivals data can no longer come from Agnum item lines and must be produced by Warehouse.
- The product/balance import, "Distribute", the per-SKU snapshot export and both per-SKU reconciliations are
  removed.
- The company's own Agnum HTTP service gains a writer for posted line-less purchase documents.

Implementation: `docs/warehouse/agnum-sum-accounting-blueprint.md`.
