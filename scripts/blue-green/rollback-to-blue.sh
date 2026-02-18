#!/usr/bin/env bash
set -euo pipefail

STATE_FILE="${STATE_FILE:-scripts/blue-green/.active_environment}"
mkdir -p "$(dirname "${STATE_FILE}")"
echo "blue" > "${STATE_FILE}"
echo "[blue-green] Traffic rolled back to BLUE"
