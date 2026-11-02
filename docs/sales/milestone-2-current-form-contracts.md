# Current Desktop Access: form-design and bound-input evidence

Inspected read-only on 2026-10-31. The owner identified
`/Users/bykovas/Desktop/Uzsakymai.adp` as the current version. Its SHA-256 is
`84185a33c8390abf327cac0bf469b0240dc135d650acdc5d8d4c60fef8cabaf5`.
The current file differs from the old Dropbox copy despite identical file size.
Current source/design takes precedence over the older recovery document.

No Access application, VBA macro, SQL procedure or business mutation was run.
`oletools.olevba` recovered 149 VBA modules. Seven differ from the old main ADP:
Module1, modProject, frmViewBlind, frmNewBlind, frmEmployeeCashRevision,
frmNewReports, frmOrderList. New-order/payment/address/duty/accessory module
code listed below is byte-identical to the old main export. Identical VBA
alone does not establish identical form design properties.

## Recoverable design evidence versus opaque properties

The OLE `Forms/DirData` name index identifies the following design streams.
Offsets below are byte offsets within that current file's Blob stream; they
are evidence locations, not stable application IDs.

| Form | Design stream | Confirmed textual design evidence |
| --- | --- | --- |
| dlgNewOrder | Forms/3/Blob | Record source/table text Uzsakymai at81. txtGroup→UzsakymoGrupe at381/399; validation at427 uses current-year-prefix ALike patterns; SQL nvarchar(4) limits the suffix to two characters. Default expression `Right(Year(Now()),2)` at903. txtNumber→UzsakymoNumeris at1331/1351. Series/customer/manager/place/consultant/date/due controls bind to legacy fields. |
| frmNewOrder | Forms/74/Blob | Saved RecordSource text `exec sp_frmOrder '<saved id>'` at620; item list source `exec dbo.sp_ListOrderItems '<saved id>'` at11598. Runtime code replaces saved example IDs; they must never become defaults in Sales. |
| frmNewBlind | Forms/11/Blob | Saved RecordSource at533 selects `pg.*` and category `isnull(r.KainosIvestis,'any') as KainosIvestis`, by saved order ID. sControlType→Valdymas at7481/7523; iWidth→Plotis at8133/8163; iHeight→Aukstis at8817/8849; dQuantity→Kiekis at9507/9543; mPrice→Kaina at10215/10245; iColour→Spalva at11390/11422. |
| dlgNewAccessories | Forms/8/Blob | Table ParduotiPriedai at596; accessory combo→PrduotoPriedoID at5883/5933; quantity→ParduotoPriedoKiekis at6391/6453; price→ParduotoPriedoKaina at6726/6782; parent→ParduotoGaminioID at7106/7148. Saved parent default/example appears at7200; caller overwrites it. |
| dlgOrderAddress | Forms/14/Blob | Table TCR_OrderAddresses at112, saved filtered source at150; cbo_Street→iStreetID at2969/2991; building/room/phone/notes/order bind to corresponding table columns. Region/city are hierarchy selectors, not independent text address fields. |
| frmOrdersDuties | Forms/58/Blob | RecordSource SELECT TCR_OrdersDutiesEmployees at69; iDutyID, iEmployeeID, iOrderID, sNotes, dtDate bindings at2717/3038/3586/2549/3669. Acquisition fields are also bound in legacy; Sales must expose only intended operator fields. |
| NaujasMokejimas | Forms/129/Blob | Method dropdown reads Zinynas_mokejimotipai at11427; bank selector calls sp_cboBankTransfersTableRowNR at18850; employee lookup at19900 excludes hidden employees and uses Loc_leidimas. EKA query at21859 reads EKA_ID/EKA_NAME/DefaulEmployeeID filtered by department. DTPicker is MSComCtl2.DTPicker.2 at17913. |

`iColour` RowSource at11512 is `EXEC sp_cboSpalva '<saved type id>'` with seven
columns; source modules replace the type parameter at runtime. Stock checks
therefore depend on the result contract of sp_cboSpalva, not just a component
master-table quantity. Refer to current product-rule evidence for column use.

These are strings physically present in **form design Blob streams**, not
strings found only in compiled VBA. The numeric property records are not
reliably decoded by the tools available here. Consequently this inspection
**does not establish** the saved AllowEdits, AllowAdditions, AllowDeletions,
RecordLocks, RecordsetType, Locked/Enabled flags, all scalar DefaultValues or
all control ValidationRule property IDs. Do not infer their values from byte
positions, type-library symbols or generic Access defaults. Use explicit VBA
assignments where verified, SQL constraints, and runtime/exported form-property
evidence for the remainder.

No currency control or currency assignment was identified in the recovered
new-order design/source. This supports checking SQL defaults, not silently
assuming a particular currency: current header SQL default is currency2 while
payment VBA explicitly uses1. Meaning and effective runtime values still need
verification before write activation. No universal200mm dimension validation
was established by these inspected design strings.

## New-order defaults confirmed in current VBA

`Form_dlgNewOrder.vba:12` sets customer, manager, place and consultant from live
series ID; the source assigns date today and due date today+14. Group default
is the design expression above. `cmdOK_Click:46` saves bound header through
`Me.Dirty=False` before opening sp_frmOrder; Cancel:41 uses Undo.
Series/customer/people/place selectors in design use SQL-login Loc_leidimas
filters; agreed Sales all-branch policy is a deliberate authorization adaptation,
not proof those legacy filters already cover every branch.

## Payment rules that the M1 scaffold must not replace

Source is current `Form_NaujasMokejimas.vba`:

- Form_Load:110 assigns payment date Now() at118 then immediately blanks it
  at119. The effective legacy initial date is blank, not automatically today.
- Method change:22 hides/clears tagged optional selectors and disables/clears
  downstream tagged fields. Switching method must not carry an old bank/EKA/
  receipt value into a different method's command.
- Transfer selection:12 enables downstream fields and chooses employee ID
  `2138337249`. EKA selection:17 chooses employee from dropdown column2. These
  are specific source defaults requiring reference validation, not Portal
  actor mapping. Receiver/employee and authenticated actor are different fields.
- PPK field change:225 enables downstream fields only at length10; otherwise
  disables them for method1. Validation:132 uses a case-insensitive unanchored
  regex `[RV][RVS][LS][0-9]{7}` and requires exactly one match. Its source intent
  comment differs from the executable pattern; do not substitute a new receipt
  grammar without an explicit decision.
- All four methods require date, employee, order, nonzero amount. Transfer2
  also requires actual bank-transfer reference; PPK1 receipt; EKA834019474
  register; cash81423987 has no extra document selector. Negative amounts are
  valid refund input in this code; zero is rejected. Refund/backdate side
  effects are checked separately in SQL and stay gated until contained.
- AddRecordToDB:236 creates Mokejimai with currency1 and method-specific values.
  It catches errors and exits, after which caller cmd_Save_Click:92 can still
  close the form at100. Sales must retain the input and show confirmed error/
  unknown outcome instead of reproducing this silent-close failure behavior.

### Payment selectors and cascade: source checked again on2026-11-01

The current `Forms/129/Blob` contains these native RowSources:

| Selector | Exact scope and returned values |
| --- | --- |
| Method, offset11427 | `SELECT Zinynas_mokejimotipai.MokejimoTipasID, Zinynas_mokejimotipai.MokejimoTipas FROM Zinynas_mokejimotipai ORDER BY MokejimoTipas`; no branch or hidden filter. Current four rows are EKA834019474, Pavedimu2, Per kasą81423987, Per PPK1. |
| Bank transfer, offset18850 | `EXEC dbo.sp_cboBankTransfersTableRowNR`; current SQL returns NR, Firma, Suma−ISNULL(mAssignedSum,0) as Likutis, Paskirtis where remaining>0 and Country='LT'. No order-customer, branch or receiver predicate. |
| Employee, offset19900 | `SELECT DarbuotojoID, DarbuotojoPavadinimas FROM Zinynas_darbuotojai WHERE (FilialasID IN (SELECT UKS FROM dbo.Loc_leidimas() ll)) AND (bHideInApplicationFlag = 0) ORDER BY DarbuotojoPavadinimas`. |
| EKA, offset21859 | `SELECT EKA_ID, EKA_NAME, DefaulEmployeeID FROM Zinynas_EKA WHERE (DeptID IN (SELECT UKS FROM dbo.Loc_leidimas() AS l))`; default employee is the third result column, hidden by design column widths `0;2835;0`. There is no same-order-branch filter. |

`Loc_leidimas()` reads SYS_SQLLoginsDepartments for SYSTEM_USER. The current
shared SQL login returned UKS0–10. Sales' agreed all-branch policy is an
explicit application scope; an employee/register label does not confer a new
permission. Filtering every EKA to Testavimas DeptID7 or requiring the receiver
to equal its register default is not a rule from this Access form.

Recovered tag strings identify `pap` on bank/EKA/PPK selectors and their labels;
`tol` occurs on amount, notes and receiver controls. Every method change
hides and clears all `pap` inputs and disables and clears all `tol` inputs.
Cash then enables `tol` immediately; PPK enables it only while receipt length
equals10; bank/EKA enable it after selection. Date has no `tol` tag, so it is
not part of that clearing cascade. Save is not tagged `tol`; validation still
has to reject missing inputs. PPK length changes away from10 clear downstream
amount/notes/receiver again. Negative amount styling means refund input, not
a separate payment method.

Bank selection sets2138337249; current live row is visible employee
Stropus Vilius in branch0. EKA selection assigns its current DefaulEmployeeID
and leaves receiver editable. All twelve current EKA defaults exist, but
EKA1→2138337262, EKA4→2138337261, EKA5→2138337263 and EKA10→2138337283
refer to hidden employees excluded from the employee RowSource. This is a
source/data inconsistency, not evidence that a missing default can be silently
replaced or that all hidden employees are selectable. Numeric design flags
such as LimitToList remain unverified; no Access runtime test was performed.

PPK is `txt_sPPKKvitas`, a textbox with a Change handler, not a reference combo.
This form and the inspected INSERT trigger do not query a PPK series/receipt
catalog or reject an existing receipt number. TAB_BlancIssues and its unused
blanc procedures match order series/order numbers in Uzsakymai; they must not
be reused as payment-receipt eligibility. The executable PPK regex permits
the three-character class combination `[RV][RVS][LS]`, not just the prefixes
listed in comments or older reporting views. Its unanchored one-match save
check and exact-length10 enable event are distinct source behaviors.

The bank source lists only transfers with positive remaining balance. Although
neither this VBA validator nor trg_NewPayments checks payment amount≤remaining
before incrementing mAssignedSum, the enabled BankTransfersTable AFTER UPDATE
trigger ProhibitAssignBankTranserWhenISFullyAssigned rejects
ISNULL(mAssignedSum,0)>ISNULL(Suma,0) and rolls back the transaction. The native
end-to-end cascade therefore prevents overassignment; the atomic server check
preserves that rule before the mutation. This bank-table trigger has no other
writes, audit, notification or procedure calls. The existing positive-only/current-date test-posting limits and
explicit historical-receiver exceptions are deployment restrictions, not
the form's native nonzero-signed-amount/date rules.

Live lookup evidence is retained privately in
`/tmp/sales-m2-evidence/payment-cascade-readonly-current.json`; at inspection
there were286 selectable LT transfers and12 accessible EKA rows, none with
DeptID7. Counts are observations, not constants or selector rules. No payment,
reference row, default employee, SQL-login scope or work period was changed.
An additional read-only search of currently selectable LT transfers found no
word TEST/Testavimas or owned order RY-TEST-913322, XXXX-926404 or XXXX-943185
in payer/purpose fields. No real transfer was allocated merely for testing;
the existing picker and rejected/no-DML cases provide reference validation
evidence, separately from the owned cash-payment positive proof.

## Address and duties rules

`Form_dlgOrderAddress.vba:46` defaults region/city from the first accessible
branch ordered by FilialoID through Loc_leidimas, not from the current order's
series automatically. Region changes clear city and reload child cities;
city changes reload child streets. Street query is by iParentID. Form_Unload:63
requeries each open frmNewOrder/frmViewOrder. Do not create new region/city/
street reference records from free text as a side effect of saving an order.

`Form_frmOrdersDuties.vba` contains only dtDate_Enter selection behavior, no
business validation. Design lookup requires visible employees from accessible
branches; duty lookup selects TBD_Duties. The design displays/binds acquisition
amount/quantity/date fields too, but their original Enabled/Locked scalar
properties were not decoded. Sales cannot assume absence of VBA validation
means permission to edit payroll/acquisition results. Schema and triggers
remain authoritative, and intended operator input is duty/person/date/notes.

## Current source hashes

Hashes refer to private current extracted files in
`/tmp/sales-vba-source/Desktop-current/`, UTF-8 with original line endings.

| Module | SHA-256 |
| --- | --- |
| Form_dlgNewOrder.vba | 95da4fc85ffe1633bd63b40b51d8315d82b6eef440e876e417ed6a7c6cc41f9c |
| Form_frmNewBlind.vba | d479ad442be18d08b86d2b5cd1cb2450f1669f8b4ab2b161471c94f46bbb4ec0 |
| Form_frmNewOrder.vba | a7404a4b2413337da22fcf404c697b55119163331b16bed1a0bae9f42a1500d8 |
| Form_dlgOrderAddress.vba | 085fcb4cf7d7f74611f511d9d6fd55558b95509fb4561f1c67e55c209c53573e |
| Form_frmOrdersDuties.vba | 26393d10e089b39d235363151ce53cd52a3a8addbd839cf4047c27f260622447 |
| Form_dlgNewAccessories.vba | 8945a425efb3f8cb15823a73919355222d1a57d4b7ba362b96d2bf0adad7f328 |
| Form_NaujasMokejimas.vba | 0c89756348c8490ff7cd3675fb526d25ff2aa773fff3b726e19813ad15f2c219 |
| modProject.bas | c44aef2b1a3a147e780a05cb56d05bc147529b0e9f22cff51ca5022592df11a4 |

Root OLE PropData contains host192.168.0.3 and catalogLKVITAIDBV1SQL. Together
with owner confirmation this identifies the current source target lead; it is
not a runtime observation of CurrentProject.Connection or executed Access UI.
No Access/Sales write-parity scenario passed as a result of static extraction.
