#!/usr/bin/env bash
# #636: the playtest's quick questions, one table per tester (question x day -> answer).
#
#   bash Tools/pulse-report.sh            # every tester
#   bash Tools/pulse-report.sh 052c49e1   # one tester id
#
# Reads the `fb:pulse` issues the game files (docs/feedback.md, "Quick questions"). Each title is
# "[Slice] Pulse <id> (day D, session S): <answer>[ - <text>]"; the tester id is in the issue's Context table.
set -euo pipefail
REPO="${REPO:-TommasoRibaudo/aquarium-civ}"
ONLY="${1:-}"
gh issue list -R "$REPO" --label fb:pulse --state all --limit 500 --json number,title,body,createdAt \
  --jq '.[] | [.number, .createdAt, .title, ((.body | capture("\\| Tester \\| (?<t>[^|]+) \\|").t // "?") | gsub(" "; ""))] | @tsv' |
python -c '
import sys, re, collections
only = sys.argv[1] if len(sys.argv) > 1 else ""
rows = collections.defaultdict(list)
for line in sys.stdin:
    num, created, title, tester = line.rstrip("\n").split("\t")
    m = re.match(r"\[Slice\] Pulse (\w+) \(day (\d+), session (\d+)\): (.*)", title)
    if not m or (only and tester != only): continue
    q, day, sess, ans = m.group(1), int(m.group(2)), int(m.group(3)), m.group(4)
    rows[tester].append((day, sess, q, ans, num))
if not rows: print("no pulse answers yet"); sys.exit(0)
for tester, items in sorted(rows.items()):
    print(f"## Tester {tester}  ({len(items)} answers)\n")
    print("| day | session | question | answer | issue |\n|---|---|---|---|---|")
    for day, sess, q, ans, num in sorted(items):
        print(f"| {day} | {sess} | {q} | {ans.replace(\"|\", \"/\")} | #{num} |")
    print()
' "$ONLY"
