#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-}"
PERCENT="${2:-10}"
if [[ -z "${VERSION}" ]]; then
  echo "Usage: $0 <version> <traffic-percent>"
  exit 1
fi

STATE_DIR="scripts/canary"
mkdir -p "${STATE_DIR}"
echo "${VERSION}" > "${STATE_DIR}/.canary_version"
echo "${PERCENT}" > "${STATE_DIR}/.canary_percent"
echo "[canary] deployed ${VERSION} at ${PERCENT}% traffic"
