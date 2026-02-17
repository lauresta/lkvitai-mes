#!/usr/bin/env bash
set -euo pipefail

BACKUP_PATH="${1:-}"
TARGET_DB="${2:-warehouse_dr}"

if [[ -z "${BACKUP_PATH}" ]]; then
  echo "Usage: $0 <backup.sql.gz> [target_db]"
  exit 1
fi

echo "[DR] Restoring backup '${BACKUP_PATH}' into '${TARGET_DB}'"
echo "[DR] Command template:"
echo "  gunzip -c \"${BACKUP_PATH}\" | psql -d \"${TARGET_DB}\""
