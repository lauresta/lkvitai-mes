#!/usr/bin/env bash
set -euo pipefail

PRIMARY_CONTAINER="${PRIMARY_CONTAINER:-postgres-primary}"
STANDBY_CONTAINER="${STANDBY_CONTAINER:-postgres-standby}"
API_HEALTH_URL="${API_HEALTH_URL:-http://localhost:5000/health}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-240}"
SLEEP_SECONDS=5

echo "[failover] Stopping primary container: ${PRIMARY_CONTAINER}"
docker stop "${PRIMARY_CONTAINER}" >/dev/null

echo "[failover] Promoting standby container: ${STANDBY_CONTAINER}"
docker exec "${STANDBY_CONTAINER}" pg_ctl promote >/dev/null

echo "[failover] Waiting for API recovery (timeout: ${TIMEOUT_SECONDS}s)"
elapsed=0
until curl -fsS "${API_HEALTH_URL}" >/dev/null; do
  sleep "${SLEEP_SECONDS}"
  elapsed=$((elapsed + SLEEP_SECONDS))
  if [[ "${elapsed}" -ge "${TIMEOUT_SECONDS}" ]]; then
    echo "[failover] ERROR: API did not recover in time."
    exit 1
  fi
done

echo "[failover] API recovered after ${elapsed}s"
echo "[failover] Failover completed"
