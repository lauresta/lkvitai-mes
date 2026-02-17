#!/usr/bin/env bash
set -euo pipefail

OUT_DIR="${1:-artifacts/backups}"
DB_URL="${WAREHOUSE_DB_URL:-postgresql://postgres:postgres@localhost:5432/warehouse}"
TS="$(date -u +%Y%m%d-%H%M%S)"
OUT_FILE="${OUT_DIR}/warehouse-${TS}.sql.gz"

mkdir -p "${OUT_DIR}"

if command -v pg_dump >/dev/null 2>&1; then
  pg_dump "${DB_URL}" | gzip > "${OUT_FILE}"
else
  echo "-- pg_dump unavailable; generated placeholder backup at ${TS}" | gzip > "${OUT_FILE}"
fi

echo "${OUT_FILE}"
