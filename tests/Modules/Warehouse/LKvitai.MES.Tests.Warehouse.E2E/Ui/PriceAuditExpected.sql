WITH latest_wac AS (
  SELECT DISTINCT ON ("ItemId") "ItemId", "UnitCost"
  FROM public.on_hand_value
  ORDER BY "ItemId", "LastUpdated" DESC
),
stock AS (
  SELECT data->>'SKU' AS sku,
         COALESCE(data->>'LocationCode', data->>'Location') AS location_code,
         data->>'WarehouseId' AS warehouse_id,
         (data->>'OnHandQty')::numeric AS qty,
         (data->>'AvailableQty')::numeric AS available_qty
  FROM warehouse_events.mt_doc_available_stock_view
  WHERE (data->>'OnHandQty')::numeric <> 0
),
priced_stock AS (
  SELECT s.*, i."Id" AS item_id,
         CASE WHEN w."UnitCost" > 0 THEN w."UnitCost"
              WHEN i."PurchasePrice" > 0 THEN i."PurchasePrice"
              ELSE NULL END AS cost,
         i."PurchasePrice" AS purchase,
         i."BasePrice" AS sale,
         COALESCE(l."IsVirtual", false) OR upper(s.location_code) = 'AGNUM' AS virtual,
         left(upper(s.location_code), 11) = 'IN_TRANSIT_' AS in_transit
  FROM stock s
  LEFT JOIN public.items i ON lower(i."InternalSKU") = lower(s.sku)
  LEFT JOIN latest_wac w ON w."ItemId" = i."Id"
  LEFT JOIN public.locations l ON l."Code" = s.location_code
),
latest_import AS (
  SELECT DISTINCT ON ("SndId") "SndId", "Id"
  FROM public.agnum_balance_import_runs
  WHERE "Status" = 'Completed'
  ORDER BY "SndId", "FinishedAt" DESC
),
agnum_rows AS (
  SELECT b."SndId" AS snd_id, b."Sku" AS sku, b."Quantity" AS qty, i."Id" AS item_id,
         CASE WHEN w."UnitCost" > 0 THEN w."UnitCost"
              WHEN i."PurchasePrice" > 0 THEN i."PurchasePrice"
              ELSE NULL END AS cost,
         i."BasePrice" AS sale
  FROM public.agnum_virtual_warehouse_balances b
  JOIN latest_import r ON r."SndId" = b."SndId" AND r."Id" = b."ImportRunId"
  LEFT JOIN public.items i ON lower(i."InternalSKU") = lower(b."Sku")
  LEFT JOIN latest_wac w ON w."ItemId" = i."Id"
  WHERE b."Sku" IN ('1422073200', '0000001')
)
SELECT jsonb_build_object(
  'capturedAt', now(),
  'stock', (SELECT coalesce(jsonb_agg(jsonb_build_object(
      'sku', sku, 'location', location_code, 'warehouse', warehouse_id,
      'qty', qty, 'available', available_qty, 'cost', cost, 'purchase', purchase,
      'sale', sale, 'value', CASE WHEN cost IS NULL THEN NULL ELSE round(qty * cost, 2) END,
      'virtual', virtual, 'inTransit', in_transit
    ) ORDER BY sku, location_code), '[]'::jsonb) FROM priced_stock),
  'agnum', (SELECT coalesce(jsonb_agg(jsonb_build_object(
      'sndId', snd_id, 'sku', sku, 'itemId', item_id, 'qty', qty, 'cost', cost, 'sale', sale,
      'costValue', CASE WHEN cost IS NULL THEN NULL ELSE round(qty * cost, 2) END,
      'saleValue', CASE WHEN sale IS NULL THEN NULL ELSE round(qty * sale, 2) END
    ) ORDER BY snd_id, sku), '[]'::jsonb) FROM agnum_rows),
  'sales', (SELECT coalesce(jsonb_agg(jsonb_build_object(
      'orderId', o."Id", 'orderNumber', o."OrderNumber", 'orderTotal', o."TotalAmount",
      'lineId', line."Id", 'sku', item."InternalSKU", 'qty', line."OrderedQty", 'unitPrice', line."UnitPrice",
      'lineAmount', line."LineAmount",
      'calculatedLineAmount', round(line."OrderedQty" * line."UnitPrice", 2)
    ) ORDER BY o."Id", line."Id"), '[]'::jsonb)
    FROM public.sales_orders o
    JOIN public.sales_order_lines line ON line."SalesOrderId" = o."Id"
    LEFT JOIN public.items item ON item."Id" = line."ItemId"
    WHERE o."IsDeleted" = false),
  'ids', jsonb_build_object(
    'item', (SELECT "Id" FROM public.items WHERE "InternalSKU" = '000-000-101' LIMIT 1),
    'inbound', (SELECT "Id" FROM public.inbound_shipments ORDER BY "CreatedAt" DESC LIMIT 1),
    'outbound', (SELECT "Id" FROM public.outbound_orders WHERE "IsDeleted" = false AND "SalesOrderId" =
      (SELECT "Id" FROM public.sales_orders WHERE "IsDeleted" = false AND "OrderNumber" = 'SO-0001' LIMIT 1)
      ORDER BY "CreatedAt" DESC LIMIT 1),
    'cycleCount', (SELECT "Id" FROM public.cycle_counts ORDER BY "CreatedAt" DESC LIMIT 1),
    'transfer', (SELECT "Id" FROM public.transfers ORDER BY "CreatedAt" DESC LIMIT 1),
    'salesOrder', (SELECT "Id" FROM public.sales_orders WHERE "IsDeleted" = false AND "OrderNumber" = 'SO-0001' LIMIT 1)
  )
);
