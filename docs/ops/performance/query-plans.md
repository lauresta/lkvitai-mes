# Query Plans (PRD-1641)

## Scope
- `items` lookup by category/barcode
- `sales_orders` lookup by customer + status
- `outbound_orders` lookup by status + requested ship date
- `shipments` lookup by tracking/dispatched-at
- Marten event reads from `warehouse_events.mt_events`
- Available stock reads from `warehouse_events.mt_doc_availablestockview`
- On-hand valuation reads from `public.on_hand_value`

## Index Strategy
- `idx_items_category_id` on `public.items("CategoryId")`
- `idx_items_barcode` on `public.items("PrimaryBarcode")`
- `idx_items_supplier_id` on `public.supplier_item_mappings("SupplierId")`
- `idx_sales_orders_customer_id_status` on `public.sales_orders("CustomerId","Status")`
- `idx_sales_orders_order_date` on `public.sales_orders("OrderDate")`
- `idx_outbound_orders_status_requested_ship_date` on `public.outbound_orders("Status","RequestedShipDate")`
- `idx_shipments_tracking_number` on `public.shipments("TrackingNumber")`
- `idx_shipments_dispatched_at` on `public.shipments("DispatchedAt")`
- `idx_on_hand_value_category_id` on `public.on_hand_value("CategoryId")`
- `idx_mt_events_stream_id` on `warehouse_events.mt_events(stream_id)`
- `idx_mt_events_type` on `warehouse_events.mt_events(type)`
- `idx_mt_events_timestamp` on `warehouse_events.mt_events("timestamp")`
- `idx_available_stock_item_location` on `warehouse_events.mt_doc_availablestockview((data->>'itemId'), (data->>'location'))`

## EXPLAIN ANALYZE Commands
```sql
EXPLAIN ANALYZE SELECT * FROM public.items WHERE "CategoryId" = 5;
EXPLAIN ANALYZE SELECT * FROM public.sales_orders WHERE "CustomerId" = '00000000-0000-0000-0000-000000000000' AND "Status" = 'Allocated';
EXPLAIN ANALYZE SELECT * FROM public.outbound_orders WHERE "Status" = 'Packed' AND "RequestedShipDate" >= now() - interval '7 days';
EXPLAIN ANALYZE SELECT * FROM public.shipments WHERE "TrackingNumber" = 'TRACK-123';
EXPLAIN ANALYZE SELECT * FROM public.on_hand_value WHERE "CategoryId" = 1;
EXPLAIN ANALYZE SELECT * FROM warehouse_events.mt_events WHERE stream_id = 'valuation-1';
```

## Benchmarks
- Baseline and post-index timings require a DB-backed environment with `pg_stat_statements` enabled.
- See `docs/prod-ready/codex-suspicions.md` (PRD-1641 TEST-GAP) for environment constraints.
