#!/usr/bin/env bash
set -euo pipefail

STATE_DIR="scripts/canary"
mkdir -p "${STATE_DIR}"
echo "0" > "${STATE_DIR}/.canary_percent"
echo "[canary] rollback complete, traffic restored to stable"
