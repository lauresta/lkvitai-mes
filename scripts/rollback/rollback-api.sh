#!/usr/bin/env bash
set -euo pipefail

TARGET_VERSION="${1:-}"
if [[ -z "${TARGET_VERSION}" ]]; then
  echo "Usage: $0 <api-version-tag>"
  exit 1
fi

echo "[rollback-api] Rolling back API/WebUI containers to tag: ${TARGET_VERSION}"
export IMAGE_TAG="${TARGET_VERSION}"
docker compose -f docker-compose.test.yml pull api webui || true
docker compose -f docker-compose.test.yml up -d api webui

echo "[rollback-api] Waiting for API health"
for _ in {1..30}; do
  if curl -fsS http://localhost:5001/health >/dev/null; then
    echo "[rollback-api] API healthy on version ${TARGET_VERSION}"
    exit 0
  fi
  sleep 2
done

echo "[rollback-api] ERROR: API health check did not recover in time"
exit 1
