# TestFlight builds (iOS, no Mac)

Workflow: [`.github/workflows/ios-testflight.yml`](../.github/workflows/ios-testflight.yml), run by hand
(GitHub → Actions → "iOS TestFlight" → Run workflow, or `gh workflow run ios-testflight.yml -f notes="P1 lab"`).

1. **Linux job:** GameCI exports the Unity project as an Xcode project (Unity Personal license from secrets).
2. **macOS job** (`macos-26`, Xcode 26): archives with **cloud-managed signing**. `xcodebuild -allowProvisioningUpdates` +
   the App Store Connect API key create the distribution certificate and profile on Apple's side, so there is no
   fastlane `match` repo and no certificates to manage. It then uploads straight to App Store Connect.
   Build number = the workflow run number. Export compliance is pre-answered (`ITSAppUsesNonExemptEncryption = false`).

Cost: free. The repo is public (since 2026-10-28), and standard GitHub-hosted runners, macOS included, cost nothing
on public repos. Limits that still apply: at most 5 macOS jobs at once, 10 GB of Actions cache, and Git LFS
storage/bandwidth quotas (LFS is billed separately from Actions). When the repo was private, a build cost about $1.50.

## App identity
| | |
|---|---|
| Name | Aquarium Civ |
| Bundle ID | `com.tommasoribaudo.aquariumciv` |
| Version | **0.<prototype>.<n>** (PlayerSettings): 0.1.x = P1, 0.2.x = P2, ... Build = run number. See the build log below. |
| Orientation | Portrait only |
| Icon | `AquariumCiv/Assets/Art/Icon/AppIcon.png` (placeholder render of the look-test diorama) |

## One-time setup (the user, logged in)

### Unity license for CI
1. Unity Hub → Preferences (gear) → Licenses → **Add** → "Get a free personal license".
   This writes `C:\ProgramData\Unity\Unity_lic.ulf`. On this machine it didn't exist yet: the Hub's newer
   entitlement license isn't enough for CI.
2. Claude sets `UNITY_LICENSE` from that file.
3. **You** set `UNITY_EMAIL` and `UNITY_PASSWORD` in your own terminal, so the password never passes through the chat:
   `gh secret set UNITY_EMAIL -R TommasoRibaudo/aquarium-civ` and `gh secret set UNITY_PASSWORD -R TommasoRibaudo/aquarium-civ`
   (each one prompts for the value). If your Unity account uses 2FA on login, CI activation can fail. In that case
   we need a separate Unity ID for CI.

### Apple
1. **Bundle ID:** developer.apple.com → Account → Certificates, IDs & Profiles → Identifiers → **+** → App IDs → App →
   Description "Aquarium Civ", Bundle ID **Explicit** `com.tommasoribaudo.aquariumciv`, no capabilities → Register.
2. **App record:** appstoreconnect.apple.com → Apps → **+** → New App → iOS, Name "Aquarium Civ" (if taken, try
   "Aquarium Civ Prototype"), language English, Bundle ID from step 1, SKU `aquariumciv`, User Access: Full.
3. **API key:** App Store Connect → Users and Access → Integrations → App Store Connect API → **Team Keys** →
   Generate. Name "GitHub CI", Access **Admin** (cloud-managed signing needs it). Download the `.p8` (you can only
   download it once) and note the **Key ID** plus the **Issuer ID** shown above the table.
4. **Team ID:** developer.apple.com → Account → Membership details → Team ID.
5. Claude sets `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID`, `APPLE_TEAM_ID` and `APPSTORE_P8` (from the downloaded file,
   without printing it). Then delete the `.p8` from Downloads and keep a copy in your password manager.

### Testers
- **Internal group "Team"** (created 2026-10-29): automatic distribution on, so every uploaded build reaches it
  with no review. Members must be users on the White Canvas App Store Connect team (Users and Access).

- **You:** after the first build has processed, App Store Connect → the app → TestFlight → Internal Testing → **+**
  group "Team" → add yourself. Install TestFlight on the iPhone and accept the email.
- **Friends** (choose one):
  - *External group + public link* (recommended): TestFlight → External Testing → **+** group "Friends" → add the
    build → fill in test info (feedback email, what to test) → Apple runs a short Beta App Review (usually under a
    day, first build only per version) → enable the **public link** and send it. Up to 10,000 testers, no accounts.
  - *Internal:* add each friend as an App Store Connect user (Users and Access → **+**, role "Developer" or
    "Marketing"), then put them in the internal group. No review, but they get access to your App Store Connect account.

## Secrets checklist
| Secret | From |
|---|---|
| `UNITY_LICENSE` | `C:\ProgramData\Unity\Unity_lic.ulf` contents |
| `UNITY_EMAIL`, `UNITY_PASSWORD` | your Unity ID (you set these) |
| `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID` | App Store Connect API key page |
| `APPSTORE_P8` | the downloaded `AuthKey_XXXX.p8` contents |
| `APPLE_TEAM_ID` | Membership details |
| `FEEDBACK_TOKEN` | fine-grained PAT, aquarium-civ only, Issues read/write ([feedback.md](feedback.md)). Optional: without it, feedback stays on the device |

## Build log: which build tests what
The version says the prototype: TestFlight shows "0.2.0 (6)", meaning P2, build 6. Bump the minor version when a
new prototype ships, the patch for fixes to the same one, and add a row here plus the "What to Test" text.

| Build | Version | Prototype | Tests | What to Test |
|---|---|---|---|---|
| 2 | 0.1.0 | P0 sandbox | first install | - |
| 3 | 0.1.0 | P1 Glass & Mess | H1 cleaning gesture: wipe vs tap (lab) | - |
| 5 | 0.1.0 | P1 + in-app feedback | same, plus Feedback button | - |
| 6 | **0.2.0** | **P2 Return to Mess** | H2: coming back to mess is a pull, not guilt. A/B by day: A cosmetic mess, B mess slows work. Diary, 4 days | [below](#p2-what-to-test) |
| 7 | **0.3.0** | **Test Hub: P3–P6** (branch `overnight/p3-p6`) | Gate 2 (P3 return, P4 history) and Gate 3 (P5 nudges, P6 eras). Lab A/B from the hub, P3 4-day and P5 3-day diaries; P2 diary still reachable | [test-session guide](design/test-session-gate2-3.md) |
| (not built yet) | **0.4.0** | **Perf check of the style kit** (branch `vs/35-perf-build`, issue #35) | Frame time / GPU ms of the new art style on the user's iPhone: Growth Lab town y110, style-kit camp and village. Not a prototype: Build Settings switched, see [below](#perf-build-04x-issue-35) | "Perf check of the new art style. Tap to switch scenes; send a screenshot of the numbers." |

### Slice build 0.4.0 (issue #98)
The first build of the vertical slice, for the user alone on their iPhone.

- **Scenes:** Build Settings enable only `Assets/Game/Scenes/Slice.unity`. The prototype, perf and sample scenes stay in the
  list but disabled. Set with the menu *AquariumCiv/Agent/Slice/Build Settings: slice build* (`SliceBuild.UseSliceBuildSettings`,
  `Assets/Editor/Agent/SliceBuild.cs`). The iOS workflow uses GameCI's builder with no scene argument, so it builds exactly the
  enabled scenes: nothing in `ios-testflight.yml` needs to change. CI's EditMode/PlayMode tests open scenes by path and don't
  depend on the list.
- **Version:** 0.4.0 (App Store versions are numeric). The build number is still the workflow run number.
- **Debug panels:** present, hidden. The "dbg" toggle (and the sim tab behind it) appears after a **2 s press on the top-left
  corner** (a 96-unit square at the safe-area corner; moving out of it or letting go starts over; it stays for the session).
  The Editor shows it from the start. Code: `DebugDock.cs`, tests `DebugDockLockTests`.
- **Feedback button:** shown in this build. `FeedbackUI.InTestBuild` is now true in the Editor, in development builds and in any
  build with the `SLICE_TEST` scripting define. `SliceBuild.UseSliceBuildSettings` sets that define for iOS and Android in
  ProjectSettings, so CI's release export has it too. A real send still needs the `FEEDBACK_TOKEN` secret (below).
- **Feedback relay:** no token ships unless CI adds one. The workflow's *Bake feedback token* step writes `FEEDBACK_TOKEN` into
  `Resources/FeedbackToken.txt` if the secret is set; with it, notes go straight to GitHub issues from the phone (the
  security trade-off in [feedback.md](feedback.md) applies: friends-only, one user here). Without it, notes wait in the
  on-device outbox. A relay is only needed before anything public.
- **Local builds (no upload):** menu *Slice/Build Android (apk)* and *Build iOS (Xcode project)*, or
  `unity command script eval --code "AquariumCiv.EditorTools.SliceBuild.Android()"` (also `.Ios()`). Output goes to
  `F:\GameDevBuilds\` (outside git, off C:). Windows can only produce the Xcode project for iOS; archiving and signing stay in
  the macOS CI job. Results: see below.

### Perf build 0.4.x (issue #35)
A one-off build to measure the style kit on a phone (60 fps / GPU ≤ 10 ms). It ships **only**
`Assets/Spikes/Perf/PerfCheck.unity`; the prototypes are left out. Set with the menu *AquariumCiv/Agent/Perf Check/Build
Settings: perf build* (`PerfSceneBuilder.UsePerfBuildSettings`); the scene comes from *Perf Check/Build Scene*.

**What changed** (committed on `vs/35-perf-build` only; `vs/slice` and `main` keep the prototype settings):
- `EditorBuildSettings`: `PerfCheck.unity` added first and enabled; every prototype scene stays in the list in its old
  order but **disabled** (Hub, P2, P6, P5, P4, P3, P1, Sandbox, SampleScene).
- `PlayerSettings.bundleVersion` 0.3.0 → **0.4.0** (App Store versions must be numeric, so no "-perf" suffix).
- `PlayerSettings.enableFrameTimingStats` 0 → **1** (Player → Other Settings → Frame Timing Stats). Without it,
  FrameTimingManager reports no CPU/GPU times in a release build. Harmless to leave on.

**Restore the 0.3.x prototype build:** menu *AquariumCiv/Agent/Perf Check/Build Settings: prototype build*
(`PerfSceneBuilder.UsePrototypeBuildSettings`: Hub first, all prototypes enabled, perf scene dropped), then set
`bundleVersion` back to `0.3.x` by hand. Or don't merge this branch's `ProjectSettings/` changes.

**On the phone:** the app opens straight into the perf scene. The top panel shows:
- fps and frame ms;
- CPU frame ms, which includes the wait for the next vsync (≈ 16.7 at 60 fps); main and render are the real CPU work;
- **GPU ms**;
- batches, SetPass calls, draws and triangles drawn (Unity's render counters; "n/a" if the release build doesn't report them);
- scene mesh size, then device, screen size, render scale, quality level.

Controls:
- Tap anywhere below the panel (or *Next*) to switch box. Each switch rebuilds the box (a short freeze), and stats
  start counting after 90 frames of warm-up.
- *Orbit* pauses the slow camera sweep.
- *Log* writes one CSV row a second to `persistentDataPath/perf/perf_*.csv`.
- *Send* posts the per-scene table (avg / p95) as a feedback issue (`fb:feel`, title "[-] Feel: Perf numbers from
  <device>") and keeps a copy in `perf/summary_*.md`. Without a token, or offline, it waits in the outbox.

Stay about 30 s on each box, then *Send*, or screenshot the panel.

Notes:
- The prototypes never set `Application.targetFrameRate`, so on iOS they ran at Unity's default **30 fps**. The perf
  scene sets 60.
- iOS uses the *Mobile* quality level and `Mobile_RPAsset` (render scale 0.8). The panel shows both.

### P2 What to Test
Paste into App Store Connect → TestFlight → the build → Test Details:

> **Aquarium Civ 0.2: Return to Mess.** A tiny village lives in your glass tank. Keep the app for 4 days and open it
> whenever you feel like it: no reminders, it runs on real time. Wipe the glass with a finger; touch junk or weeds and
> flick them out.
> When you're done, tap "Done for now" (one short survey a day). Tell us anytime with the Feedback button:
> 1. Did you want to open it again? What made you?
> 2. When you came back to a dirty tank, did it feel like fun or like a chore?
> 3. Was anything confusing?

Lab session (sitting with a tester): hold the top-left info box for 2 s until "LAB" shows, then use "Step away".

## Notes
- The upload job uses the `testflight` environment. Its deployment-branch policy allows `main` and `overnight/p3-p6`
  (checked 2026-10-29). On any other branch the upload job is refused. Adding a branch is a repo-settings change the
  user makes (Settings → Environments → testflight).
- Debug panels: hidden behind a 2 s top-left long-press in every non-Editor build (see Slice build 0.4.0). Lab tests that need the away buttons still run in the Editor.
- If the macOS job fails on signing, check that the API key has **Admin** access and that the bundle ID is
  registered under the same team.
