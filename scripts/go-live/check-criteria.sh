#!/usr/bin/env bash
set -euo pipefail

CHECKLIST="docs/operations/go-live-checklist.md"

if [[ ! -f "$CHECKLIST" ]]; then
  echo "Checklist file not found: $CHECKLIST"
  exit 1
fi

checked_count=$(grep -c "^- \[x\]" "$CHECKLIST" || true)
signoff_count=$(grep -c "^- Sign-off:" "$CHECKLIST" || true)

if [[ "$checked_count" -lt 100 ]]; then
  echo "NO-GO: checklist completion is $checked_count/100"
  exit 1
fi

if [[ "$signoff_count" -lt 10 ]]; then
  echo "NO-GO: sign-off count is $signoff_count/10"
  exit 1
fi

echo "GO: criteria met (items=$checked_count, sign-offs=$signoff_count)"
