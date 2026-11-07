#!/usr/bin/env bash
# Set Slice-board cards' Status (adds the issue to the board if missing). docs/workflow.md §2, §5.
# Usage: Tools/board-status.sh <Backlog|Ready|"In progress"|Review|Done> <issue#> [<issue#> ...]
set -eu
OWNER=TommasoRibaudo; PROJ=PVT_kwHOAvEYac4BpXBl; FIELD=PVTSSF_lAHOAvEYac4BpXBlzhnzA7s
case "$1" in
  Backlog) opt=049ba022;; Ready) opt=2e97cbdc;; "In progress") opt=1e45f9be;; Review) opt=9de750fb;; Done) opt=41a92f1c;;
  *) echo "unknown status $1"; exit 2;;
esac
status=$1; shift
items=$(gh project item-list 18 --owner "$OWNER" --limit 800 --format json)
for n in "$@"; do
  id=$(printf '%s' "$items" | /c/Python311/python -c "import json,sys; print(next((i['id'] for i in json.load(sys.stdin)['items'] if i.get('content',{}).get('number')==$n),''))")
  if [ -z "$id" ]; then
    id=$(gh project item-add 18 --owner "$OWNER" --url "https://github.com/$OWNER/aquarium-civ/issues/$n" --format json \
      | /c/Python311/python -c "import json,sys; print(json.load(sys.stdin)['id'])")
  fi
  gh project item-edit --id "$id" --project-id "$PROJ" --field-id "$FIELD" --single-select-option-id "$opt" >/dev/null
  echo "#$n -> $status"
done
