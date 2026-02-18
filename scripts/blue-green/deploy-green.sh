#!/usr/bin/env bash
set -euo pipefail

GREEN_VERSION="${1:-v1.2.4}"
export GREEN_IMAGE_TAG="${GREEN_VERSION}"
docker compose -f docker-compose.blue-green.yml up -d api-green
echo "[blue-green] Green deployed with version ${GREEN_VERSION}"
