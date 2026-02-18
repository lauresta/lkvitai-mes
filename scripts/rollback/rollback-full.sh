#!/usr/bin/env bash
set -euo pipefail

TARGET_VERSION="${1:-}"
TARGET_MIGRATION="${2:-}"

if [[ -z "${TARGET_VERSION}" || -z "${TARGET_MIGRATION}" ]]; then
  echo "Usage: $0 <api-version-tag> <target-migration-name>"
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

echo "[rollback-full] Starting full rollback"
"${ROOT_DIR}/scripts/rollback/rollback-api.sh" "${TARGET_VERSION}"
"${ROOT_DIR}/scripts/rollback/rollback-database.sh" "${TARGET_MIGRATION}"
"${ROOT_DIR}/scripts/smoke-tests.sh"
echo "[rollback-full] Full rollback finished successfully"
