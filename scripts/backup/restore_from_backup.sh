#!/usr/bin/env bash
set -euo pipefail

BACKUP_FILE="${1:-}"
DB_URL="${WAREHOUSE_DB_URL:-postgresql://postgres:postgres@localhost:5432/warehouse}"

if [[ -z "${BACKUP_FILE}" ]]; then
  echo "Usage: $0 <backup-file.sql.gz>" >&2
  exit 1
fi

if [[ ! -f "${BACKUP_FILE}" ]]; then
  echo "Backup file not found: ${BACKUP_FILE}" >&2
  exit 1
fi

if command -v psql >/dev/null 2>&1; then
  gunzip -c "${BACKUP_FILE}" | psql "${DB_URL}"
else
  echo "psql unavailable; restore dry-run only for ${BACKUP_FILE}" >&2
fi
