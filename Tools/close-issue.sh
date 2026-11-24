#!/usr/bin/env bash
# Close a slice issue and move its board card to Done in one step (docs/workflow.md §2, §5).
# Usage: Tools/close-issue.sh <number> "<result note>"
# Uses gh --jq only (no python/jq needed). Board has 450+ cards: keep --limit above that.
set -eu
n=$1; note=$2
OWNER=TommasoRibaudo
gh issue close "$n" --comment "$note"
id=$(gh project item-list 18 --owner "$OWNER" --limit 1000 --format json \
  --jq ".items[] | select(.content.number==$n) | .id" | head -n1)
if [ -z "$id" ]; then
  id=$(gh project item-add 18 --owner "$OWNER" --url "https://github.com/$OWNER/aquarium-civ/issues/$n" --format json --jq .id)
fi
gh project item-edit --id "$id" --project-id PVT_kwHOAvEYac4BpXBl --field-id PVTSSF_lAHOAvEYac4BpXBlzhnzA7s \
  --single-select-option-id 41a92f1c >/dev/null
echo "#$n closed, card in Done"
