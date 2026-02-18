#!/usr/bin/env bash
set -euo pipefail

STATE_DIR="scripts/canary"
mkdir -p "${STATE_DIR}"
echo "error_rate=0.10" > "${STATE_DIR}/.canary_alert_state"
echo "[canary] injected synthetic error rate for rollback testing"
