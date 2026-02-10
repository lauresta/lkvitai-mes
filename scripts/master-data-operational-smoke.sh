#!/usr/bin/env bash

set -euo pipefail

API_BASE="${API_BASE:-https://localhost:5001}"
USER_ID="${USER_ID:-smoke-admin}"
USER_ROLES="${USER_ROLES:-WarehouseAdmin,WarehouseManager,QCInspector,Operator}"

ts="$(date +%s)"
supplier_code="SMOKE-SUP-${ts}"
location_code="SMOKE-A-${ts}"
location_barcode="SMOKE-LOC-${ts}"
item_barcode="SMOKE-ITEM-${ts}"
order_id="SMOKE-ORD-${ts}"
reference_no="SMOKE-PO-${ts}"

curl_json() {
  local method="$1"
  local url="$2"
  local payload="${3:-}"

  if [[ -n "${payload}" ]]; then
    curl -skS \
      -X "${method}" \
      -H "X-User-Id: ${USER_ID}" \
      -H "X-User-Roles: ${USER_ROLES}" \
      -H "Content-Type: application/json" \
      --data "${payload}" \
      "${API_BASE}${url}"
  else
    curl -skS \
      -X "${method}" \
      -H "X-User-Id: ${USER_ID}" \
      -H "X-User-Roles: ${USER_ROLES}" \
      "${API_BASE}${url}"
  fi
}

echo "[1/9] Health check"
curl_json GET "/api/warehouse/v1/health" | jq '.status'

echo "[2/9] Resolve RECEIVING location id"
receiving_id="$(curl_json GET "/api/warehouse/v1/locations?search=RECEIVING&pageNumber=1&pageSize=10" | jq -r '.items[0].id')"
if [[ -z "${receiving_id}" || "${receiving_id}" == "null" ]]; then
  echo "RECEIVING location not found. Ensure seed data is applied."
  exit 1
fi

echo "[3/9] Create supplier"
supplier_json="$(curl_json POST "/api/warehouse/v1/suppliers" "{\"code\":\"${supplier_code}\",\"name\":\"Smoke Supplier\",\"contactInfo\":\"smoke@test.local\"}")"
supplier_id="$(echo "${supplier_json}" | jq -r '.id')"

echo "[4/9] Create storage location"
location_json="$(curl_json POST "/api/warehouse/v1/locations" "{\"code\":\"${location_code}\",\"barcode\":\"${location_barcode}\",\"type\":\"Bin\",\"parentLocationId\":null,\"isVirtual\":false,\"maxWeight\":1000,\"maxVolume\":100,\"status\":\"Active\",\"zoneType\":\"General\"}")"
location_id="$(echo "${location_json}" | jq -r '.id')"

echo "[5/9] Create item"
item_json="$(curl_json POST "/api/warehouse/v1/items" "{\"internalSKU\":null,\"name\":\"Smoke Item ${ts}\",\"description\":\"Smoke test\",\"categoryId\":1,\"baseUoM\":\"PCS\",\"weight\":1.0,\"volume\":0.1,\"requiresLotTracking\":false,\"requiresQC\":false,\"status\":\"Active\",\"primaryBarcode\":\"${item_barcode}\",\"productConfigId\":null}")"
item_id="$(echo "${item_json}" | jq -r '.id')"

echo "[6/9] Create inbound shipment + receive"
shipment_json="$(curl_json POST "/api/warehouse/v1/receiving/shipments" "{\"referenceNumber\":\"${reference_no}\",\"supplierId\":${supplier_id},\"type\":\"PurchaseOrder\",\"expectedDate\":null,\"lines\":[{\"itemId\":${item_id},\"expectedQty\":100}]}")"
shipment_id="$(echo "${shipment_json}" | jq -r '.id')"
line_id="$(curl_json GET "/api/warehouse/v1/receiving/shipments?pageNumber=1&pageSize=10" | jq -r ".items[] | select(.id == ${shipment_id}) | .id")"
if [[ -z "${line_id}" || "${line_id}" == "null" ]]; then
  line_id=1
fi
curl_json POST "/api/warehouse/v1/receiving/shipments/${shipment_id}/receive" "{\"lineId\":${line_id},\"receivedQty\":100,\"lotNumber\":null,\"productionDate\":null,\"expiryDate\":null,\"notes\":\"smoke\"}" >/dev/null

echo "[7/9] Putaway from RECEIVING"
curl_json POST "/api/warehouse/v1/putaway" "{\"itemId\":${item_id},\"qty\":100,\"fromLocationId\":${receiving_id},\"toLocationId\":${location_id},\"lotId\":null,\"notes\":\"smoke\"}" >/dev/null

echo "[8/9] Pick flow"
task_json="$(curl_json POST "/api/warehouse/v1/picking/tasks" "{\"orderId\":\"${order_id}\",\"itemId\":${item_id},\"qty\":10,\"assignedToUserId\":null}")"
task_id="$(echo "${task_json}" | jq -r '.taskId')"
curl_json POST "/api/warehouse/v1/picking/tasks/${task_id}/complete" "{\"fromLocationId\":${location_id},\"pickedQty\":10,\"lotId\":null,\"scannedBarcode\":\"${item_barcode}\",\"scannedLocationBarcode\":\"${location_barcode}\",\"notes\":\"smoke\"}" >/dev/null

echo "[9/9] Adjustment + verify history"
curl_json POST "/api/warehouse/v1/adjustments" "{\"itemId\":${item_id},\"locationId\":${location_id},\"qtyDelta\":-1,\"reasonCode\":\"INVENTORY\",\"notes\":\"smoke\",\"lotId\":null}" >/dev/null
curl_json GET "/api/warehouse/v1/adjustments?pageNumber=1&pageSize=10" | jq '.totalCount'

echo "Smoke flow completed successfully."
