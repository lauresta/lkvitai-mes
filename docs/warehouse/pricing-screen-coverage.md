# Warehouse price coverage

This follow-up covers Warehouse screens that show item quantities and were not fully covered by the original pricing stages. The implementation reuses the current item price snapshot: `EffectiveCost` for cost and `BasePrice` for sale. Missing prices display `–`; a failed price lookup must not hide quantities.

| Screen group | Price display |
| --- | --- |
| Stock dashboard, location balances, inbound shipment list, receiving history, main dashboard incoming shipments | Cost total and sale total across the displayed items |
| Main dashboard low stock, expiring lots, recent movements; 3D warehouse; traceability | Current unit cost and sale price beside the item quantity |
| Inbound shipment details, packing, picking, cross dock, RMAs, stock adjustments, transfers, cycle counts, lots, Agnum reconciliation | Current unit cost and sale price beside each item quantity |
| Stock movement and pick history reports, valuation dashboard | Existing cost and current sale price beside each item quantity |
| Available stock, stock level report, Agnum balances, putaway, receiving QC, outbound order details, reservations, item details, item selection on inbound and sales forms | Both prices already existed; visible price labels were aligned with the EN/LT resources where touched |

Aggregate cost and sale totals are complete only when every constituent item has the respective price. Purchase totals on receiving screens use the recorded inbound line price and remain separate from current cost and sale prices. Historical transactions are not repriced; the history screens label current snapshot values separately from transaction amounts. No legacy SQL data or schema is changed.
