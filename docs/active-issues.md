# Active issues

Updated 2026-11-07. This index contains only unresolved decisions, defects, and verification gaps. Resolved work remains in Git history. The detailed registers are [Sales milestone 2](sales/milestone-2-issues.md) and [legacy production](shopfloor/legacy-production-issues.md).

## Sales

| IDs | Current issue |
| --- | --- |
| S2-001, S2-002 | Order cancellation remains disabled: its audit column cannot hold current order IDs; old Access `Long` paths also need bigint review. The owner deferred the shared SQL repair. |
| S2-003 | Production confirmation is enabled on test, but a successful live run still needs owner verification. |
| S2-006, S2-020 | The final-price recovery warning can be unclear, and some native workflow labels still use a neutral status chip. |
| S2-009 | An active product type references a missing legacy type record. No shared reference data was changed. |
| S2-010 | The normal SQL Database Mail notification for the `sa` work-period extension failed in the existing mail infrastructure. |
| S2-012 | Intermittent product-create latency remains unexplained; reporting overload was confirmed, but the original Sales wait was not reproduced to closure. |
| S2-025 | Two Decolux formulas request metres using a width in millimetres. A guarded SQL correction is prepared in `scripts/sales/005-decolux-material-metres.sql`, but the owner explicitly deferred any shared-database change. |

## Shopfloor and production documents

| IDs | Current issue |
| --- | --- |
| P01, P03, P04, D02 | Access and web production-queue selection still need parity checks for GU composition, permissions, and partial visibility. |
| P02 | GU numbering has no unique database constraint. The single-operator workflow is accepted for now; a second concurrent operator would reopen the collision risk. |
| P05, P06, P12 | The Roletai web report needs rendered Access PDF comparison; the other 12 production reports, archive behavior, and the exact production-only report list remain open. |
| P07 | Zebra EPL output over COM1 works in Access; a web printing path usable from any operator Windows computer is not implemented or device-verified. |
| P08–P10, P13–P17 | GT scan/undo, stops, repairs, delivery, write-off, history, cancellation, and state-change rights need the remaining Access/SQL contract and live verification before writes are enabled. |
| P11 | A Sales formula preview does not test a newly edited expression before it is saved. |
| Trapezoid verification | The recent-order picker, document-load fix, and readable slat labels are merged in `main` (#275, #277); the current test deployment still needs a browser and PDF reprint check. |

## Finance and MCP

| ID | Current issue |
| --- | --- |
| Finance live verification | The Finance 403/TLS and OCR duplicate fixes are merged (#272, #273, #276), but no live bank rows were inserted or deleted during development. Confirm the import preview and guarded write flow in the test environment before operational use. |
| MCP tool visibility | `bank_payments_list` is merged (#278), with individual payer and purpose fields and paging. The user refreshed the ChatGPT plugin tools; an actual ChatGPT call after refresh has not yet been observed in this workspace. |

## Infrastructure and shared UI

| IDs | Current issue |
| --- | --- |
| S2-017, S2-023 | Warehouse header authentication and the difference between native-browser and fresh SSO sessions need separate verification. |
| S2-018 | Existing dependency advisories are still deferred. |
| S2-024 | The missing-database-container fallback omits the existing persistent bind; repair that path without recreating the healthy database. |
