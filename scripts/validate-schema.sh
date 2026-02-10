#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${WAREHOUSE_DB_CONNECTION:-}" ]]; then
  echo "ERROR: WAREHOUSE_DB_CONNECTION is not set"
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "ERROR: psql is required to run schema validation"
  exit 1
fi

fail=0

check_schema() {
  local schema="$1"
  local exists
  exists=$(psql "$WAREHOUSE_DB_CONNECTION" -tAc "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname='${schema}')")
  if [[ "$exists" != "t" ]]; then
    echo "ERROR: Missing schema '${schema}'"
    fail=1
  fi
}

check_table() {
  local schema="$1"
  local table="$2"
  local exists
  exists=$(psql "$WAREHOUSE_DB_CONNECTION" -tAc "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='${schema}' AND table_name='${table}')")
  if [[ "$exists" != "t" ]]; then
    echo "ERROR: Missing table '${schema}.${table}'"
    fail=1
  fi
}

check_schema "warehouse_events"

check_table "warehouse_events" "mt_events"
check_table "warehouse_events" "mt_streams"
check_table "warehouse_events" "mt_event_progression"
check_table "warehouse_events" "mt_doc_availablestockview"
check_table "warehouse_events" "mt_doc_locationbalanceview"

if [[ "$fail" -ne 0 ]]; then
  echo "Schema validation FAILED"
  exit 1
fi

echo "Schema validation passed"
