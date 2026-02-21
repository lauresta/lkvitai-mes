#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILDING_BLOCKS_DIR="$ROOT_DIR/src/BuildingBlocks"

if [[ ! -d "$BUILDING_BLOCKS_DIR" ]]; then
  echo "Missing directory: $BUILDING_BLOCKS_DIR"
  exit 1
fi

if command -v rg >/dev/null 2>&1; then
  violations="$(rg -n --glob '*.csproj' 'ProjectReference Include=.*Modules[\\/]' "$BUILDING_BLOCKS_DIR" || true)"
else
  violations="$(grep -RIn --include='*.csproj' -E 'ProjectReference Include=.*Modules[\\/]' "$BUILDING_BLOCKS_DIR" || true)"
fi

if [[ -n "$violations" ]]; then
  echo "BuildingBlocks must not reference Modules. Violations:"
  echo "$violations"
  exit 1
fi

echo "OK: BuildingBlocks do not reference Modules."
