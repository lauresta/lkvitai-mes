#!/usr/bin/env bash
set -euo pipefail

ROOT="src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI"

if rg -n "bootstrap(\.min)?\.css|bootstrap-icons" "$ROOT/Pages/_Layout.cshtml"; then
  echo "Bootstrap CDN reference detected in _Layout.cshtml"
  exit 1
fi

if rg -n "bi-[a-z0-9-]+|\bbi\b" "$ROOT" -g "*.razor" -g "*.cs"; then
  echo "Bootstrap icon usage detected in WebUI source."
  exit 1
fi

echo "No bootstrap CDN or bi-* icon usage detected in WebUI."
