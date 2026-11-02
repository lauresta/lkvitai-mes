# Access bigint compatibility boundary

Current Desktop source hash:
`84185a33c8390abf327cac0bf469b0240dc135d650acdc5d8d4c60fef8cabaf5`.
Observed Sales test header2148197448/product2152012295 exceed VBA Long's
signed32-bit maximum2147483647. Main order/product SQL keys are already bigint; do not reseed
identities, narrow IDs or change shared keys to accommodate old declarations.

Source evidence from the current extracted modules:

- `Form_dlgNewOrder.vba:52`: concatenates `Me!txtOrderID` into sp_frmOrder SQL;
  no intermediate Long declaration was found in this create/open handler.
- `Form_frmOrderList.vba:406`: opens by `Mid(SelectedItem.Key,3)` string;
  list key itself is string `ID` plus the SQL ID. This path does not prove an
  overflow simply because its loop counter/mouse coordinates are Long.
- `Form_frmNewBlind.vba:39,56`: declares `iNewItemID As Long` and assigns the
  first sp_CreateNewItem result. That procedure returns unsafe @@IDENTITY,
  potentially a smaller nested audit ID. The assigned value is not used for
  navigation; the bug can therefore be masked by current trigger identity.
- `Form_frmNewBlind.vba:143–153`: cmd_Refresh_Click declares `iBlindID As Long`,
  assigns txt_iBlindID, requeries and finds the row by that local value.
  Current FindPrice calls this refresh after setting a found/missing-price
  value. Thus the32-bit conversion is in a real product price/refresh path,
  not merely an unused helper. Static source proves the narrowing; this task
  has not launched Access to claim a runtime test passed.
- `Form_frmNewOrder.vba:429` and `Form_frmViewOrder.vba:196` declare a Long
  sOrderAddress parameter. No direct source call was found in the inspected
  active entry handlers; their actual use through form expressions remains
  a runtime/property check rather than proof that header opening is broken.

`legacy-bigint-compatibility.patch` is a narrow **proposed** source edit using
Variant for ADODB bigint values and CStr for exact numeric recordset-find
text. This is portable to32-bit Access; LongLong would not be. It does not fix
sp_CreateNewItem's separate unsafe return contract, add a new workflow or
alter the supplied ADP binary. Apply through the active Access development
workflow, compile and verify the above test IDs before claiming Access parity.
Source files/ADP originals were not modified by this audit.

## Cancellation audit schema boundary

The remote browser cancellation attempt for owned Testavimas order2148197450
failed without committing: direct reads showed StateID4, Sugadintas=False and
no `order.cancel.v1` receipt. Its business snapshot remained readable.

The current SQL procedure `sp_CancellOrder` accepts `@iOrderID bigint` and
updates `Uzsakymai.Sugadintas`. The enabled `trg_ChangeOrder` then inserts that
bigint into `AUDIT_Anuliavimai.OrderID`, whose actual type is **int NOT NULL**.
New IDs therefore overflow the legacy cancellation audit column. A direct
adapter UPDATE would execute the same trigger and cannot safely bypass this
boundary while preserving audit semantics.

Read-only schema inspection found no index on this column and no foreign key
on the audit table. A separately reviewed widening of this one column to
`bigint NOT NULL` would preserve existing data and trigger behavior. No such
schema change, global trigger disable, ID narrowing or audit bypass was applied
by this investigation. Cancellation acceptance and test-reservation cleanup
remain pending resolution of this boundary.

The concrete proposal is `scripts/sales/002-cancellation-audit-bigint.sql`.
It changes the shared audit table for **all branches**, not just Testavimas,
and therefore requires the owner's separate approval before execution. The
diagnostic baseline contained6732 audit rows. No other audit column is widened.
The script verifies the exact database name/GUID and native nonnullable column
type; it rejects unexpected defaults, indexes, statistics, foreign keys, CHECK
constraints, computed columns, full-text references, schema-bound references
or additional SQL module readers. It uses XACT_ABORT, TRY/CATCH rollback and a
five-second lock timeout, and verifies that the locked audit row count remains
unchanged. An already-bigint compatible column is an idempotent no-op.

Inspection of all625 current SQL module definitions and dependency metadata
found only `trg_ChangeOrder` referring to this audit table; its local order ID
is already bigint. No reference to the audit table was found in the149 current
extracted Access VBA modules. These checks cannot prove the absence of external
ad-hoc readers or dynamically assembled SQL; widening preserves existing values
but callers that narrow newly recorded IDs still require bigint compatibility.

Until the schema is compatible, the Sales snapshot exposes
`CancellationUnavailableReason` for an out-of-int-range order ID. UI disables
the action, and a forged direct cancellation returns definitive422
`LEGACY_AUDIT_ID_RANGE` before executing the procedure or writing a receipt.
Both checks use the current SQL metadata, so approval/application followed by
Refresh re-enables cancellation without a trigger bypass or another app change.
Missing or unexpected metadata fails closed as a configuration error.
