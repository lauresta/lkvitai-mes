#!/usr/bin/env bash
set -euo pipefail

ROOT="src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI"
STRICT=0

if [[ "${1:-}" == "--strict" ]]; then
  STRICT=1
fi

found=0

check() {
  local message="$1"
  shift

  local output
  if output="$("$@")"; then
    echo "WARN: $message"
    echo "$output" | sed -n '1,20p'
    local count
    count="$(echo "$output" | wc -l | tr -d ' ')"
    if [[ "$count" -gt 20 ]]; then
      echo "WARN: ... ${count} total matches"
    fi
    found=1
  fi
}

check "Bootstrap CDN reference detected in _Layout.cshtml." \
  rg -n "bootstrap(\.min)?\.css|bootstrap-icons" "$ROOT/Pages/_Layout.cshtml"

check "Bootstrap icon usage detected in WebUI source." \
  rg -n "bi-[a-z0-9-]+|\bbi\b" "$ROOT" -g "*.razor" -g "*.cs"

if [[ -d "$ROOT/wwwroot/css/bootstrap" ]]; then
  echo "WARN: Legacy bootstrap static assets directory detected: $ROOT/wwwroot/css/bootstrap"
  found=1
fi

check "Legacy UI wrapper component usage detected in Razor pages/components." \
  rg -n "<(/)?(ErrorBanner|ConfirmDialog|LoadingSpinner|Pagination|DataTable|ToastContainer)\b" "$ROOT" -g "*.razor"

check "Bootstrap class token usage detected in Razor/CSHTML markup." \
  rg -n --glob "*.razor" --glob "*.cshtml" 'class="[^"]*\b(btn|table|form-control|form-select|card|alert|badge|row|col-)\b[^"]*"' "$ROOT"

if [[ "$found" -eq 0 ]]; then
  echo "No bootstrap CDN, bi-* icon usage, or legacy wrapper usage detected in WebUI."
  exit 0
fi

if [[ "$STRICT" -eq 1 ]]; then
  echo "Bootstrap/Mud migration guard failed in strict mode."
  exit 1
fi

echo "Bootstrap/Mud migration guard completed in warn-only mode."
