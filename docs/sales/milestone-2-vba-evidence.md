# M2 gate 0 — recovered VBA evidence

Historical recovery record. The owner subsequently supplied the current
`/Users/bykovas/Desktop/Uzsakymai.adp`; its current source/design supersedes
the older Dropbox observations below. See
[current product rules](milestone-2-current-product-rules.md),
[current edit/transition rules](milestone-2-current-edit-state-rules.md) and
[current form contracts](milestone-2-current-form-contracts.md).

Inspected read-only on 2026-10-31. No Access application/macros, SQL commands,
payments or writes were executed. This is static source evidence, **not**
confirmation that these old local ADPs match the deployed Access client.

## Sources and provenance

Source directory:
`/Users/bykovas/Library/CloudStorage/Dropbox/Lauresta/lauresta-terminal/Bykov Software/`.
The original `Desktop/uzsakymai.zip` remains absent. `oletools.olevba` extracted
VBA from the compound ADP documents without launching Access.

| File | Filesystem modified date | SHA-256 | Extracted modules |
| --- | --- | --- | --- |
| `Uzsakymai.adp` | 2018-06-19 | `94ad4093ee98f111272ccbf584b17c2d59b2fcea3b1789dde52c4eb0ad5d91eb` | 149 |
| `Uzsakymai_LT.adp` | 2015-06-18 | `29cff932f82725665ef0e9928e4ce60ecd5727a9683582179fbd2c11098a6303` | 148 |
| `Uzsakymai_LV.adp` | 2013-12-03 | `b8f471ad59bdd7f071abba5d93c975d658b8ea2dcc42ee589614fbd32371660e` | Parser did not recognize a supported VBA container |

Dates alone do not establish application version. The main and LT exports have
identical new-order, header, payment, accessory and calculation modules listed
below. `Form_frmNewBlind` differs in error handling, temporary ID variable types,
a removed debugger `Stop`, and a message; `modProject` differs in error handling
and the formatting of the eligibility procedure call. Choose the actually
used client before parity acceptance.

Private raw extracts are under `/tmp/sales-vba-source/Uzsakymai/` and
`/tmp/sales-vba-source/Uzsakymai_LT/` (private directory, not tracked). Do not copy
all exports to the repository: unrelated modules contain embedded credentials.
The allowlisted fields in `modConst` identify `Data Source=127.0.0.1`, catalog
`LKVITAIDBV1SQL`, integrated authentication. This constant does not prove the
ADP's actual runtime `CurrentProject.Connection` points to the current server.
Root OLE `PropData` contains the UTF-16 host `192.168.0.3` in both main/LT
files, matching the host inspected separately for current test API SQL. This
is a useful connection lead, not proof of current runtime/deployed-client use.
Runtime connection identity and form design/bound-field metadata need separate
verification in the active Access application.

## Action/rule map from the main ADP

Line references are to the private extracted files, counting Attribute lines.

| Action | Source/event | Observed behavior and implementation consequence |
| --- | --- | --- |
| Series defaults | `Form_dlgNewOrder.vba:12`, `cboOrderSeries_AfterUpdate` | Looks up customer `UzaskovoID`, manager `Vad_DarbuotojasID`, place `VietaID`, consultant `Kons_DarbuotojoID` from `Zinynas_UzsakymoSerijos` by series ID. Sets date today and due date +14 days. Defaults are IDs, not labels. |
| Create header | Same file:46, `cmdOK_Click` | `Me.Dirty=False` saves bound row, then opens `frmNewOrder` with `sp_frmOrder(orderId)`. Cancel uses `Me.Undo`. Exact bound field definitions/defaults still require form metadata. |
| Open product editor | `Form_frmNewOrder.vba:373`, `lst_Items_DblClick` | Checks late-payment eligibility (`AllowEnterNewOrder`), procedure-based action and available customer credit before opening the editor. Action 0 blocks entry; 1 warns production confirmation is forbidden; 3 warns; 2 proceeds. Negative available credit blocks. This handler gates editor opening, including existing lines. |
| Add product | `Form_frmNewBlind.vba:36`, `cbo_NewiTypeID_AfterUpdate` | Calls `sp_CreateNewItem(orderId,typeId)`, reads first returned field, requeries and navigates to last row. The returned value is assigned but not used for navigation; Sales must use a proven exact returned ID instead of navigation order. |
| Visible PPP fields | Same file:244, `ShowAdditionalFields` | Caption/visibility from `Zinynas_rusys.P01–P12` joined through type. Visibility depends on caption being non-null, not truthiness of stored value. |
| PPP calculation | Same file:283, `CalculateAdditionalFields` | Reads NPP01–NPP12; for every non-null formula assigns the resulting value to the corresponding control in index order. Then saves bound form (`Me.Dirty=False`) and invokes stock check using PPP12. Width, height and quantity AfterUpdate call this method. There is no persisted manual-override marker in this code: recalculation overwrites manually edited PPPs having a formula. Reopen itself does not call this calculation method in the inspected code. |
| Colour change | Same file:315, `iColour_AfterUpdate` | Invokes stock check using current PPP12; it does not itself recompute all PPP formulas. Do not invent a universal recalculation event model. |
| Formula evaluation | `modcalculations.bas:5`, `sFormulaValue` | Text substitution for Width, Height, Quantity; optional Colour, then ControlType when non-null; uses Access `Eval`. Null colour jumps directly to Eval, also skipping control substitution. Server evaluator must use allowlisted syntax/function implementations, not execute Eval or SQL supplied by clients. |
| Custom calculation functions | `modTechno.bas:31–144` | Eight Carrier/Correctors functions use live `DIC_Constants` DLookup values. Declared module constants are not their actual source of values. See function contract below. |
| Open accessory popup | `Form_frmNewBlind.vba:350`, `lslAccessories_DblClick` | Saves parent first; passes width/height divided by 1000 (metres), quantity, parent blind default/filter, and `sp_ListAccessoriesByType(typeId)`. Opens read-only when lock image is visible. |
| Accessory quantity | `Form_dlgNewAccessories.vba:12` | Reads reference price/unit. Width unit −1789163110 = widthMetres×quantity; height unit 1046262104 = heightMetres×quantity; pieces 1664584723 = quantity; square metres −1859919715 = widthMetres×heightMetres×quantity. Other units have no assignment branch here; defaults/form metadata must be inspected. |
| Price lookup | `Form_frmNewBlind.vba:454`, `FindPrice` | Requires type, dimensions, colour and quantity. Joins type/component price groups, price list and period; chooses TOP(1) matching width/height ceilings and inclusive order-date period. **No ORDER BY** appears, so selecting a deterministic nearest ceiling without checking actual data would change behavior. Stores price/trusted-price flag. |
| Missing/list/manual price | Same file:510–564 | Found list price is trusted. Missing price for fixed-price category sets sentinel price 999 and marks it trusted after a warning. Manual price change clears trusted flag. Fixed-price editing is locked except two explicit type IDs (428417548, 2112842479). These are historical rules needing live verification, not permission to silently save a sentinel price in Sales. |
| Stock check | Same file:384, `CheckComponents` | Uses colour dropdown available quantity and tests `available−required > 0` (equality is shortage). Action 1 does nothing, 2 warns, 3 deletes bound product through Access menu commands. Optional follow-up writes `NOTIFY_UserComponents` with SYSTEM_USER, component, order note and requested quantity. The stock-check action is not read-only. |
| Delete | Same file:103 | Explicit confirmation then `sp_DeleteBlind`; error message says operations on the item may prevent deletion. Current SQL definition/state checks remain authoritative. |
| Duplicate | Same file:97, `cmd_CopyRecord_Click` | Only informs operator copying is unavailable and second product must be entered manually. No legacy duplicate implementation exists in this handler. A working Sales duplicate would be a new composition of verified create/configure operations, not a port of this handler. |
| Check parameters | Same file:93 | Only a success message, no actual parameter check. Do not label a new server validation path as already verified legacy behavior based on this button. |
| Discount/fixed final price | `Form_frmNewOrder.vba:49–124` | Direct update of `Uzsakymai`; fixed-price discount uses discountable and non-discountable amounts. SQL triggers, currency precision and zero-discountable cases need current-schema comparison. |
| Payment opening/close | Same file:168,304 | Before opening payments and on order form unload, compares known/current final cost and updates `UzsakymoVerteSuNuolaida` if different. Form unload can also require missing-address confirmation. Thus closing the old form may persist a commercial field; it is not a simple local dismiss. |
| Payment display/new | `Form_dlgPayments.vba:33` | Opens `NaujasMokejimas` with order ID/number; payment popup close requeries order. |
| Payment create | `Form_NaujasMokejimas.vba:236`, `AddRecordToDB` | Direct INSERT into `dbo.Mokejimai`: UzsakymasID, Data (date only), Pastabos, Suma, ValiutosID=1, MokeijimoTipasID, EmployeeID, BankTransferNR, PPKKvitasNR, EKA_ID. Must use parameterized typed values and examine payment triggers before enabling. |
| Payment validation | Same file:132 | All four methods require date, employee, nonzero amount and order ID. PPK type1 additionally receipt; transfer type2 additionally bank-transfer record; cash81423987 no additional document selector; EKA834019474 additionally register. Raw PPK regex is historical and unanchored: do not silently treat it as a new approved format. |
| Refund amount | Same file:204 | Negative amount explicitly switches UI to refund mode. Existing validation only rejects zero, not negative. M1 positive-only validation is not parity. Whether refund entry belongs in M2 must be stated; do not enable it casually as a consequence of parsing decimal values. |

## Custom function contract

`modTechno` queries constants by names `MaxStep127`, `MaxStep89`,
`EdgeFix127_AB`, `EdgeFix89_AB`, `EdgeFix127_CDE`, `EdgeFix89_CDE`.
For width w, edge e and max-step s, base carrier count is Access
`Round(((w−e)/s + 1) + 0.5, 0)`. AB returns base count; CDE increases an odd
base count by one to force an even count. Corrector count returns zero for
carrier count zero; otherwise rounds `((s−(w−e)/(carriers−1))*carriers)/6`.
The source guards only zero carriers; a count of one has a division-by-zero
edge requiring explicit parity evidence rather than an invented fallback.
Access arithmetic uses Double/Variant and Access Round, so decimal-only
reimplementation needs midpoint and floating-point boundary comparisons.

## Extracted source hashes

SHA-256 of the private extracted UTF-8 files (CRLF preserved):

| Module | SHA-256 |
| --- | --- |
| `Form_dlgNewOrder.vba` | `95da4fc85ffe1633bd63b40b51d8315d82b6eef440e876e417ed6a7c6cc41f9c` |
| `Form_frmNewBlind.vba` | `913aab3ae0ebd54b8958c9360afa5eab1849372a32291994bc0c3d28733712d3` |
| `Form_frmNewOrder.vba` | `a7404a4b2413337da22fcf404c697b55119163331b16bed1a0bae9f42a1500d8` |
| `modcalculations.bas` | `4bfe92a50998954f894a7f06a5a71355432ac3171afc9ea1c6c2cc61b391f131` |
| `modTechno.bas` | `9710ad1fec97638ffdf0f163ef1cf71bc791a94308c1c9269bbdceb50236c70c` |
| `modProject.bas` | `a02153bc5de39a3b353e317dfafbdf3cd0c97d049532c5e5635035589a4f3519` |
| `Form_dlgPayments.vba` | `3563e1fd91c74016ea776291da9d70e119b286771b84f4cd811c6a1fbc48fef2` |
| `Form_NaujasMokejimas.vba` | `0c89756348c8490ff7cd3675fb526d25ff2aa773fff3b726e19813ad15f2c219` |
| `Form_dlgNewAccessories.vba` | `8945a425efb3f8cb15823a73919355222d1a57d4b7ba362b96d2bf0adad7f328` |

## Remaining acceptance dependencies

Confirm active deployed ADP version and runtime connection; recover form design
metadata for bound fields, default/control sources, edit locks and record
locking/concurrency properties; compare current SQL triggers/procedure/schema;
read live DIC_Constants/price mapping/stock behavior; run representative inputs
in Access on permitted Testavimas records. Static extraction does not prove
these runtime checks passed. No Access/Sales parity scenario has been executed
by this source-recovery task.
