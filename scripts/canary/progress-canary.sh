#!/usr/bin/env bash
set -euo pipefail

PERCENT="${1:-}"
if [[ -z "${PERCENT}" ]]; then
  echo "Usage: $0 <traffic-percent>"
  exit 1
fi

STATE_DIR="scripts/canary"
mkdir -p "${STATE_DIR}"
echo "${PERCENT}" > "${STATE_DIR}/.canary_percent"
echo "[canary] progressed canary to ${PERCENT}%"
