#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${1:-${API_BASE_URL:-http://localhost:5001}}"

echo "[smoke] Checking /health"
curl -fsS "${API_BASE_URL}/health" >/dev/null

echo "[smoke] Checking /metrics"
curl -fsS "${API_BASE_URL}/metrics" >/dev/null

echo "[smoke] Smoke checks passed"
