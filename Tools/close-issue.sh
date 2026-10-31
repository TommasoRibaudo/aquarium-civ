#!/usr/bin/env bash
# Close a slice issue and move its board card to Done in one step (docs/workflow.md §5).
# Usage: Tools/close-issue.sh <number> "<result note>"
set -eu
n=$1; note=$2
OWNER=TommasoRibaudo
gh issue close "$n" --comment "$note"
id=$(gh project item-list 18 --owner "$OWNER" --limit 400 --format json \
  | python -c "import json,sys; print(next((i['id'] for i in json.load(sys.stdin)['items'] if i['content'].get('number')==$n),''))")
if [ -z "$id" ]; then
  id=$(gh project item-add 18 --owner "$OWNER" --url "https://github.com/$OWNER/aquarium-civ/issues/$n" --format json \
    | python -c "import json,sys; print(json.load(sys.stdin)['id'])")
fi
gh project item-edit --id "$id" --project-id PVT_kwHOAvEYac4BpXBl --field-id PVTSSF_lAHOAvEYac4BpXBlzhnzA7s \
  --single-select-option-id 41a92f1c >/dev/null
echo "#$n closed, card in Done"
