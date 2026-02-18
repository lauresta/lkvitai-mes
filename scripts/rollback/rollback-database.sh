#!/usr/bin/env bash
set -euo pipefail

TARGET_MIGRATION="${1:-}"
if [[ -z "${TARGET_MIGRATION}" ]]; then
  echo "Usage: $0 <target-migration-name>"
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
INFRA_DIR="${ROOT_DIR}/src/LKvitai.MES.Infrastructure"

echo "[rollback-db] Rolling back DB to migration: ${TARGET_MIGRATION}"
cd "${INFRA_DIR}"
dotnet ef database update "${TARGET_MIGRATION}" --startup-project ../LKvitai.MES.Api

echo "[rollback-db] Database rollback complete"
