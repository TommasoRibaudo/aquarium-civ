# Playtest feedback → GitHub

Testers never copy-paste anything. Everything lands as a **GitHub issue** in `TommasoRibaudo/aquarium-civ`, and
the project **[Aquarium Civ Playtest Feedback](https://github.com/users/TommasoRibaudo/projects/17)** collects them.

## Sources in the app
| Where | What it sends |
|---|---|
| **Feedback** button (bottom-left, every build) | Kind + free text, plus the session context |
| Survey **Send** ("Done for now") | The 5 answers, plus that session's summary |

Code: `Assets/Prototypes/_Shared/Runtime/Feedback/FeedbackSender.cs`, `UI/FeedbackPanel.cs`, `UI/SurveyPanel.cs`.
Offline or with no token, reports wait in `persistentDataPath/feedback_outbox/` and are retried on the next launch.

## Issue format (for people and agents)
- **Title:** `[P1·A] Feel: first line of the text`, or `[P1·A] Survey from <nick>: feel 4 · return 5 · Q4 5`.
- **Labels:**
  - `feedback` (all of them; the board's auto-add filter)
  - one kind: `fb:bug`, `fb:feel`, `fb:confusing`, `fb:idea`, `fb:praise`, `fb:survey`
  - `proto:P1`
  - `variant:A` / `variant:B`
- **Body sections:**
  - *What the tester said* (quoted), or *Survey answers* (a table)
  - *Context* table: kind, tester nickname + id, prototype / variant / test mode, build (version · CI run · commit),
    device, session id, world state (year, population, mess split), camera zoom
  - *Session stats* (the EventLog summary JSON: taps, look-time, cleans, voluntary cleans, full-clean time, ...)
  - *Recent log events* (the last 40 JSONL lines, in a collapsed `<details>`)
  - A machine-readable footer: `<!-- aquarium-civ-feedback v1 kind=… proto=… variant=… tester=… build=… -->`

Reading it as an agent:
`gh issue list -R TommasoRibaudo/aquarium-civ --label feedback --state open --json number,title,labels,body`.
Filter with `--label fb:survey`, `--label proto:P1`, and so on. Issue #2 is a closed format sample.

## Setup
1. **Token (the user):** github.com → Settings → Developer settings → Fine-grained tokens → Generate.
   - Repository access: **Only select repositories → aquarium-civ**.
   - Permissions: **Issues: Read and write** (Metadata: read is added automatically). Nothing else.
   - Store it without pasting it into the chat: `gh secret set FEEDBACK_TOKEN -R TommasoRibaudo/aquarium-civ`.
   - CI writes it into `Assets/Prototypes/_Shared/Resources/FeedbackToken.txt` at build time (gitignored). The
     same step writes `BuildInfo.txt`.
   - For Editor tests, put a token in `AquariumCiv/UserSettings/FeedbackToken.txt` (gitignored, Editor only).
2. **Board auto-add (the user, once):** open the project → ⋯ → **Workflows** → **Auto-add to project** → Edit →
   repository `aquarium-civ`, filter `is:issue label:feedback` → Save and turn it on. The free plan allows one
   auto-add workflow per project.

## Security trade-off
The token ships inside the app, so anyone holding the build could extract it. It can only read and write issues of
this one private repo: an attacker could spam issues or read them, but couldn't touch code or anything else. That's
acceptable for a friends-only TestFlight. Before any public build, replace it with a small relay (for example a
serverless function holding the token, with rate limits) and rotate the token.
