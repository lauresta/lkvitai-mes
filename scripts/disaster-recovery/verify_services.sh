#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-http://localhost:5000}"

echo "[DR] Verifying service health on ${BASE_URL}"
echo "[DR] Command template:"
echo "  curl -fsS ${BASE_URL}/health"
echo "  curl -fsS ${BASE_URL}/api/warehouse/v1/items?top=1"
