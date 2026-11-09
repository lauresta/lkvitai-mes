# Finance · Bankiniai pavedimai

The Finance tile opens `/finance/` (also `/finance/bank-payments`) after deployment. The Blazor Server WebUI uses the shared Portal cookie, shell, red live-production-data warning, and EN/LT resources. The Finance API is routed at `/api/finance/bank-payments` and requires an authenticated Cikada account whose **current** trusted legacy SQL mapping is in `Finance:AllowedLegacyLogins`; test allows `TERMINAL\d.bykovas` only. The database connection runs unpooled under `EXECUTE AS LOGIN` for that mapped identity. No client-supplied login or role header selects SQL identity.

The screen follows `design_handoff_finance_bank_payments` in the supplied archive: LT/LV selector, search, 30/90/all filter, compact scrollable table, sortable headers (default **`NR DESC`**), paste zone, inline parse preview, duplicate selection, import, row delete, and immediate undo toast. All displayed text is in EN/LT `.resx` files; the shared self-hosted fonts are linked before tokens.

## Verified legacy table

`/Users/bykovas/Desktop/bankas1.mde` links `dbo_BankTransfersTable` to `LKVITAIDBV1SQL.dbo.BankTransfersTable`. Its LT and LV forms select the latest 250 rows by `Data DESC`, then display that subset by `NR ASC`. The web list sorts the complete filtered set before paging, with `NR DESC` initially. Sales already references this table by `NR` when assigning bank payments.

Read-only SQL metadata on 2026-11-06 confirmed:

| Column | SQL type | Null? | Role |
| --- | --- | --- | --- |
| `NR` | `int` | No | Primary key, **not identity**, no default |
| `Data` | `datetime` | No | Transfer date |
| `Firma` | `nvarchar(255)` | No | Payer |
| `Paskirtis` | `nvarchar(1024)` | No | Purpose |
| `Suma` | `money` | No | Amount |
| `Country` | `char(2)` | Yes | LT/LV filter |
| `sAssignmentNotes` | `nvarchar(1024)` | Yes | Payment assignment notes |
| `mAssignedSum` | `money` | Yes | Amount assigned to orders |
| `PakeiteSuma` | `nvarchar(256)` | Yes | Legacy change information |

The Access import procedure `sproc_AppendNewBankTransfersToExisingTable` copies `NR` from Excel sheets `Pavedimai$` and `Ryga$` and skips existing `NR` values. The handoff assumption that `NR` auto-increments is incorrect. The web import locks the shared table and assigns consecutive `NR` above its current maximum within a serializable transaction. It writes only the existing six columns; **no production schema change** is part of this implementation.

Exact date, payer (case-insensitive), and amount matches are `Duplicate`. With `Finance:DuplicateKey=DateSum`, date and amount alone are an exact duplicate. Otherwise a same-date, same-amount match with a different payer is `PossibleDuplicate`, including OCR truncation or spelling errors. The preview shows the matching legacy NR and payer and leaves both duplicate states unselected. The operator may select a specific row after reviewing that match. The API rechecks same-date, same-amount records in the write transaction to prevent stale previews from silently inserting a duplicate. Row delete and undo refuse a transfer with a nonzero assigned amount or any `Mokejimai.BankTransferNR` reference. Undo stores only the latest import per Cikada user in API memory for 30 minutes and verifies every row is unchanged before deleting the batch. An API restart removes the undo option; this is the scoped alternative to adding `ImportId`/`ImportedAt` columns to the shared legacy table.

The Finance API requires `ConnectionStrings:LKvitaiDb`, the shared Portal key ring, and `CikadaUsers:BaseUrl`. On the first Bank page request, the WebUI forwards the shared Portal cookie only to the internal Finance API, prerenders the first 100 rows into a private, non-cacheable HTML response, and carries a compressed snapshot across Blazor hydration. If that request fails, the existing browser fetch supplies the normal error state. Filters, sorting, paging, and all writes continue to call the API from the browser with the Portal cookie. Unsafe requests require the same origin. Production routing is not configured by this test slice. No live bank rows were inserted or deleted during development.
