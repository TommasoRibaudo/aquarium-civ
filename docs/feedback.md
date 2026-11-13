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

## Quick questions (#636, first outside playtest)
One tap-sized question per session, on a small sheet at the bottom (no scrim), at a calm moment: never during the first run, a
moment, an open sheet or a tool in hand, and never in a visit that already had a choice. At most one per session and three a day;
"Not now" asks again in a later session, twice at most. The set, its days and rules: `Runtime/Telemetry/PulseQuestions.cs`
(`know`, `back`, `name`, `time`, `event`, `mine`, `tidy`, `lost`, `miss`, `keep`); the sheet: `View/PulseQuestionView.cs`.
While it runs, the old day-2/3 recall question stays quiet (its words are in `name` and `event` now).

- **Every answer goes out with the full package**, like a note: an issue labelled `feedback`, `fb:pulse`, `proto:slice`, titled
  `[Slice] Pulse <id> (day D, session S): <answer>[ - <typed>]`, with the answer table, the screenshot taken just before the sheet
  opened, the Context table, the session stats, the last 40 events, the game log, and **the box now**: the living and gone
  people's names and the 15 latest stories, to check a recall answer against what really happened.
- Typed words go to the issue only; the telemetry log gets `pulse_shown`, `pulse_answer` (question, answer index, day, session,
  whether something was typed) and `pulse_dismissed`.
- "Anything confusing?" answered Yes opens the Feedback sheet with Confusing picked.
- Report: `bash Tools/pulse-report.sh [tester-id]`, one table per tester (day, session, question, answer, issue).
- Debug panel (telemetry block): "Ask <id>", "Reset questions", "Questions: on/off". PlayMode tests run with the questions off
  (`SaveSandbox`); `PulseQuestionPlayTests` asks on purpose.
- `fb:pulse` issues need no triage: read them with the report.

## In the slice (#55)
Code: `Assets/Game/UI/` (`FeedbackUI`, `FeedbackSender`, `FeedbackReport`, `LogRing`, `DeviceInfo`). A **Feedback** pill (bottom-left,
safe area) opens a sheet: kind (bug / confusing / idea / feel / praise), text, optional screenshot of the box taken just before it opens.
Issues get `feedback`, `fb:<kind>`, `proto:slice`, with the box's key numbers, full device info and the last 150 game-log lines attached.
Notes go to `persistentDataPath/feedback_outbox` first and leave it only after GitHub accepts them (offline, no token or a kill mid-send: sent on a
later launch or resume). The same `FeedbackToken.txt` is used.
- **Screenshot (#153):** the sheet leaves the picture; the HUD and the bubbles stay in it. It goes in the issue itself, base64 in a folded block
  (decode to view: `base64 -d > shot.jpg`). `ScreenShot.Jpg` steps the size and quality down until it's within 40,000 base64 characters; a note with long logs
  drops log lines first and shrinks the picture further, so the body stays under GitHub's 65,536. There is no upload: the token keeps **Issues** only, because it
  ships in the build, and the `feedback-assets` branch was never needed.
- **Long notes (#619):** the text box takes up to 4,000 characters (it was 600, which cut a dictated note mid-word) and shows "N left" in
  the last 10 %. The tester's words win: `FeedbackReport.FitNote` lets the log give way first (halved until none), then the recent events,
  the session stats and the screenshot (shrunk, then left out under 6,000 base64 characters); only then is the text cut, with
  "(cut at N characters)" in the issue.
- **Duplicates (#155, #171):** each note carries `<!-- note-id ... -->`. A `.sending` file beside it marks a send under way; at the next flush a note with that
  mark is looked for in the latest 30 feedback issues before it goes again, and if GitHub can't say, it waits.
- Debug panel: "Send test feedback". The Settings page of the HUD shell (#36) should call `FeedbackUI.Instance.Open()`.
- Before any public build, replace the shipped token with a relay (see Security trade-off).
