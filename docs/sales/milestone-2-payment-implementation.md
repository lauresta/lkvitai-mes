# Contained Testavimas payment entry

Owner decisions: new Sales payments use actual currency2 EUR and only configured
Testavimas parent orders are writable. Initial cash verification used
method81423987 and explicitly approved receiver77249366 (`Bykovas Denisas`).
The subsequent owner instruction enables all four native methods with live
Access-style choices; manual bank/register ID arrays are no longer prerequisites.
The employee's existing branch1, hidden flag and SQL name LAURESTA\d.bykovas
remain unchanged. No employee is moved or unhidden to make a selection work.

`GET /api/sales/order-entry/orders/{id}/payment-references` returns only the
source-valid and currently existing choices. It includes SQL business date and
current order revision, actual currency code/rate, method-specific required
references, actual employee branch/hidden metadata, and approved register/bank
references. Missing required references exclude that method. Cash requires none.
Receiver choices use the current Access predicate: visible employees whose
FilialasID appears in Loc_leidimas.UKS, plus explicitly approved historical
receivers. A selected accessible EKA's actual existing hidden default is a
narrow additional exception, removed when another EKA is selected. The
receiver stays editable: neither native VBA nor the new server requires it
to equal the register default. The scoped query does not expose arbitrary
hidden employees. Existing
payment history left-joins actual method/employee/register/currency names, even
when a historical receiver is absent from the current selection catalog.

Current VBA `Form_NaujasMokejimas.AddRecordToDB` hardcodes currency1; all1000
latest observed legacy payments (2026-09-02 through2026-10-31) likewise have
paymentcurrency1 and ordercurrency2. The actual labels are1=LTL and2=EUR, with
both rates1.0000. The payment trigger adds raw `Suma` to receiver cash and order
Paid; it does not convert currencies. Currency2 for new Sales payments is the
owner's explicit new convention, not a reinterpretation or rewrite of old rows.
The command checks the order currency matches the configured payment currency.

Payment source guards remain active: actual SQL account posting period, future
date rejection, employee cash-revision cutoff, required method references,
bank assignment when applicable, cash/Paid updates and their audit/salary
triggers. New Sales test posting accepts only current SQL date and positive
two-decimal amounts. It does not issue refunds, backdate, delete payments,
disable triggers or impersonate another SQL identity.

`fn_IsNowMyWorkPeriod` reads `SYS_UsersWorkPeriods` for `SYSTEM_USER`. The initial
SA fixed period was2001-01-01 through2026-01-01; current2026-10-31 was closed.
The separate application identity TERMINAL\d.bykovas had an allowed period
through2030-01-31, but no matching SQL server login was found. Using that user's
policy function instead of the real trigger identity would bypass the source
rule and is not implemented. Parent task owns separately approved SA-period
configuration and associated work-period trigger review. The owner subsequently
approved that exact SA end-date extension and the trigger's normal notification.
The parent applied only SA IndividualPeriodLastDate=2030-01-31; other SA fields
and other users stayed unchanged, and the actual current-period function becameY.
No trigger was disabled or bypassed.

Actual private HTTP verification on owned order2148197450, before any period
change, returned only approved d.bykovas/cash/EUR references with the closed
period reason. A forged unallowlisted Rabotnik receiver returned403
PAYMENT_SCOPE; approved d.bykovas reached422 PERIOD_CLOSED. Both attempts left
payment rows, Paid, both receivers' balances, scoped audit counts, receipts and
the order's complete revision unchanged. No financial posting was claimed by
this preflight check.

After the approved period change, one0.01 EUR cash payment was posted through
the actual HTTP command mapping to owned order2148197450:
payment2149652454, command6173a906-20e8-4503-81a0-165cca91fc38. The exact command
was journalled before submission. d.bykovas cash changed103.10→103.11 and
orderPaid0→0.01, with exactly one payment and cash-audit row. Three products and
the28.43 order total were retained. A fresh service instance replayed the same
actor/key/body and recovered the same PaymentId without another credit;
`sp_frmOrder` also read the new Paid amount. The payment was retained for review;
no refund or deletion was issued. Remote browser payment evidence is separately
maintained in the main verification ledger. Its second0.01 payment committed as
payment2149652456, commandce4ded72-8077-4ecc-b040-ef496dbeaf90. A fresh service
replayed the captured browser body with its original v10 ExpectedRevision;
the receipt was recognized before current revision comparison and no payment,
cash, Paid, audit, receipt-count or revision change occurred. Final read-only
verification found two payments, Paid0.02, receiver cash103.12, three original
products and unchanged total28.43. The actual `sp_frmOrder` projection returned
Paid0.02 and debt28.41 for `XXXX-943185-26`; this is SQL readback, not an executed
Access UI acceptance test.

Production confirmation remains independently disabled. It requires its own
contained production-document namespace and validation of the actual document
and status side effects; payment approval does not enable that command.

## Native cascades and reference validation

Current Desktop form-design streams and `Form_NaujasMokejimas.vba` establish:

- Methods come from Zinynas_mokejimotipai, ordered by label. All four actual
  methods are functional; unknown future methods remain explicitly unsupported.
- Bank method2 calls sp_cboBankTransfersTableRowNR. Its exact membership is
  Country='LT' and Suma-ISNULL(mAssignedSum,0)>0, with no customer, order-branch
  or date filter. Selection defaults the receiver to2138337249. The new search
  endpoint adds deterministic date/NR ordering, literal search and bounded pages;
  selected LT rows remain explainable even when depleted or outside the page.
- EKA method834019474 lists registers whose DeptID appears in Loc_leidimas.UKS,
  not only department7. Its default receiver comes from DefaulEmployeeID; the
  operator can choose another visible/approved receiver.
- PPK method1 is a text receipt, not a lookup into a payment-document catalog.
  Exactly10 characters must match executable `[RV][RVS][LS][0-9]{7}` ignoring
  case. No invented receipt-series dropdown or duplicate-receipt prohibition
  is introduced. Order-number blank catalogs are not PPK membership data.
- Changing method clears irrelevant references and downstream values; selecting
  another bank/register applies its receiver default while leaving the editable
  amount/note behavior consistent with the native AfterUpdate handlers.

Lookup reads use committed data and bounded pages/lock waits. They do not hold
serializable ranges across the entire bank/employee catalogs. Financial commands
still revalidate and lock the exact bank/receiver, parent scope, register
department, selected-EKA default exception, transfer date and work period in the
same durable-receipt transaction before its first DML. The enabled native bank
AFTER UPDATE trigger ProhibitAssignBankTranserWhenISFullyAssigned also rejects
assigned>sum and rolls back. The precheck preserves that end-to-end source rule.

## Actual storage precision

Live schema inspection found Mokejimai.Suma DECIMAL(18,2), bank Suma/assigned and
receiver CashAmountOnHand MONEY(19,4), and Uzsakymai.Paid SMALLMONEY(10,4). Payment
input therefore remains two-decimal, with no silent fraction rounding. The old
blanket214748.36 test cap is replaced by actual cumulative Paid/Cash range checks
before DML; reference metadata exposes the remaining Paid storage capacity.
Raw bank amounts retain up to four decimals. Of286 native positive LT references,
16 had fractional cents and13 were below0.01 in the read-only snapshot. Subcent
references remain visible with their exact raw value and an explicit unavailable
reason; no rounding or allocation is performed.

Bank rows contain no currency column. SourceCurrencyCode is deliberately NULL;
AmountRule is `legacy-raw-no-conversion`, while PostingCurrencyCode is EUR under
the owner's new-payment convention. Country LT does not invent a source LTL/EUR
label or exchange rate.

Read-only/negative integration on owned450 verified all four methods, bank
page25/286, numeric search, selected depleted and0.0007 references, real bank
receiver default, source-visible receivers, and EKA hidden-default switching.
Seventeen actual HTTP payment requests were rejected before DML for LV/null/
missing/depleted/subcent/over-assigned bank references, missing register, stale
hidden default after EKA change, department11/unknown receivers, irrelevant
method references, invalid PPK/method and subcent payment input. Parent/product/
PPP/payment revisions, selected bank assignment/notes, receiver cash, audits
and receipts remained unchanged. All12 actual EKA records were accessible;
no out-of-scope register or test bank reference was fabricated. There was no
identified disposable native bank fixture, so no arbitrary bank funds or made-up
PPK were booked for a positive test. Earlier cash/replay evidence remains valid.
