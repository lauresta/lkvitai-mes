#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-current}"
echo "Starting launch procedure for version: $VERSION"
echo "Step 1: Validate go/no-go criteria"
./scripts/go-live/check-criteria.sh

echo "Step 2: Trigger blue-green deployment"
./scripts/blue-green/deploy-green.sh "$VERSION"
./scripts/blue-green/switch-to-green.sh

echo "Step 3: Execute smoke tests"
./scripts/smoke-tests.sh

echo "Launch procedure completed"
