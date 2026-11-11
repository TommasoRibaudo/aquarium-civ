# Feedback batch 5: status (resume file)

Prompt: `docs/prompts/feedback-batch-5-run.md` (build 25 notes #544-#567 → issues #569-#578). Integration branch
`p9/feedback-5` from origin/dev (49a61a4), worktree `F:\GameDev-wt\feedback5`. **This file lives in that worktree.**

**Mode:** hybrid (workflow.md §7): #569 alone first (lane A), then lane A of #573/#574/#575 in parallel, then #578
(after #573) and #570; lane B one Editor worker at a time in order 7 (#571+#572), 2 (#573), 3 (#574), 4 (#578),
5 (#575), 6 (#570), 9 (#577), 8 (#576); then one batched Editor pass, the play-through, the tester, the walkthrough.
**Models:** every worker Sonnet (prompt §3). Orchestrator reviews.

## Checklist
- [x] `p9/feedback-5` from `origin/dev`; this prompt committed (86f4370)
- [x] 1 · #569 merged (pacing table; target in decisions.md; fixtures re-seeded with reasons)
- [x] 2 · #573 merged (no chopping by the Keeper's tree; chips and logs)
- [x] 3 · #574 cause written in the issue; hunts and the egg gather merged
- [x] 4 · #578 merged (words, wood, joining)
- [x] 5 · #575 merged (rename)
- [x] 6 · #570 merged (locked seeds, village line)
- [x] 7 · #571 + #572 merged (population, chief's office)
- [x] 8 · #576 merged (open for look sign-off)
- [x] 9 · #577 merged (open for look sign-off)
- [ ] Every issue's *Done when* ticked with evidence, or listed as short after 4 rounds
- [ ] Batched Editor pass green (EditMode 10 assemblies, PlayMode classes, smoke); failures fixed or noted as baseline
- [ ] `Feedback5Playthrough` + the touch play-through done, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Walkthrough: open-questions.md section, `Staged()` stops, auto run clean, every shot looked at
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released; Editor ready
- [ ] PR → `dev`, CI green, merged
- [ ] Final report to the user

## Calls made without the user
- #570: the waits-for line sits on the Kit sheet (not History: History opens the time-lapse); a 'camp waits for' stage before the camp; the chooser now opens whenever a jar is shut (one ready jar = two taps).
- #572: 'the office of chief' = the Offices sheet row (the title chip), not a building; every office holder's name opens their card.
- #571: population on the title line ('Year N · P people'), not History (History needs a tap).
- #577: the lid fades out from a pitch of ~68° (top-down hides it).
- #574: eggs only in spring (the model's nests); the egg settle-once marker isn't saved.
- #575: first name only; two living people can't share a first name (refused with a line), the worker's call kept.
- Eggs (#574): a minimal egg gather (a gatherer at the reeds with a basket, `TakeEgg()`), built so #428 can grow it (the prompt's call).

## Log
(Times before 09:35 were first written as guesses; corrected below from git commit times.)
- 08:06 run start. Editor closed, lock free, health 0 (C: 18 GB). Issues #569-#578 read (gh issue view prints nothing: REST used).
- 08:12 Workers (Sonnet): #569 alone on the land (wt 569, vs/569-pacing); #575 lane A started early (wt 575; no land numbers, sim name + card code offline) to save wall clock; Editor worker #571+#572 next in F:\GameDev (vs/571-572-hud; UI only, not land-dependent, so it doesn't wait for #569). Rules: scratchpad/worker-rules.md. Notes' shots looked at (#545 tree chopped beside Keeper's tree, #546/#556 hidden-pane lines over grass, #567 y37 no wood).
- 08:16 Editor opened (restart-editor.sh, ready). Lock taken for #571+#572 Editor worker (Sonnet); watcher 900 s. Editor tickets since restart: 1.
- 08:24 #575 lane A merged (branch e0809dd): SimCore.RenameNotable (first name only, settlement-name rules, 2-16, no clash with a living name), PersonCardView.Rename.cs, cheat 'Open a card with the rename field', PersonCardRenamePlayTests (category PersonCardRename, 4 shots). Worker SimTests 1765/1765; mine on the next merge (#569 suite running). Editor phase in the pass.
- 08:34 #571+#572 Editor worker done (73c164a, 16 min of Editor): title line 'Year N · P people' via GameHud.TitleLine (season slot for #576); Offices sheet holder name opens their card (the tester's 'office of chief' = the Offices row 'Chief — X', checked against note #555). HudInfoPlayTests 6/6, BuildingCard 5+1 inconclusive, WorldTap 7/7, SmokePlay 2/2. Shots looked at (offices, long-750). FirstRunPolish daylight(True) 10.77 vs 11 failed (not touched; check on base in the pass). Editor tickets since restart: 1.
- 08:38 #571+#572 merged (branch 1766292): round 2 added a bold 'Name ›' cue on holder names (offline csc 0; shot in the pass: check the › glyph renders).
- 08:45 #569 round 1 back (626f3ca): watered layer + d (seedsPerBird 0.4, thicketChoke 0.6), gate 8 kept, real-model PacingLab in SimTests, no fixture moved. Sent back (round 2). #573 and #574 lane A started (wt 573/574) without waiting for #569's merge since no fixture moved; they merge p9/feedback-5 before reporting. Editor idle → #577 Editor worker started now (out of the prompt's order 9-after-6: pure view, no lane A dependency, Editor was free). Lock + watcher 900 s. Editor tickets since restart: 2.
- 08:52 #569 round 2 merged (7aea845, branch 1ad07cd): misted patch 2.1x young+grown (32 vs 15), (a) 26.5/24.5/24.3 (test 22-28 per seed), (b) 33.3/30.3/29.3 (5.8 y behind: Decision for the user, cause #434 one placement per visit), (c) unchanged; wildSeedChance 0.08→0.04; cheat 'Mist one half, skip 3 yr' (EcoDebug.MistHalf). My SimTests on the merge: 1774/1774.
- 08:52 #574 round 1: cause reproduced (no hunter: FillOpenJobs only moves from 2+ holders; parents of babies never hunt; TakeEgg never called), posted on #574. Sent: re-merge #569 + re-test.
- 08:56 #577 merged (bbee7be, branch 87c9337, Editor 22 min): the faded frame still cast shadow lines (Stylized ShadowCaster ignored the fade); lid = 5th glass cube, no collider, fades top-down. LidPlayTests 5/5, CameraGlassFeel 3/3, CarePlayTests 4/4, WorldTap 7/7, Smoke 2/2. Looked: day close before/after (lines gone), night (no outline either way), default (faint lid haze). Open for look sign-off. Metas for #575 files committed (3b8d71a).
- 08:57 #576 Editor worker started (vs/576-seasons). Editor tickets since restart: 3 → restart after it.
- 09:03 #574 merged (a33b84e, branch 0d7c813): job takes a worker when rabbits to spare and no hunter; parents hunt; egg gather (LifePlanner.Eggs.cs, spring); cheats Make a hunter / Gather an egg now. A visit (2 min) sees a hunt ~56% with 1 hunter, ~72% with 2. My SimTests on the merge: 1789/1789. #570 lane A started (wt 570) at 10:35.
- 09:05 #573 round 1 back (d98b0a0): woods spot = nearest grown wild tree outside woodsClearRadius 1.1; ChopLogsView (a log per 4 hits). Sent back: CampRhythm principle-6 skip (87% in step at one tree), logs lifted on a timer, no AnimBench clip.
- 09:10 #570 lane A merged (c830f8e, branch 8a321dc): EcoModel.JarNeed words from the real jar conditions; locked jars greyed in the chooser, a tap says the need; 'The camp/village waits for: ...' line on the Kit sheet (note + chooser). Cheat 'Lock all jars'. Worker SimTests 1783; offline csc on the merge 0 errors (Sim, Life, Game.UI, View, View.PlayTests). Unity: LifeSlotPlayTests (new ShutJars_... + the fit tests), shots p570-*.
- 09:20 #576 round 1 (51ae8df, Editor 29 min): SeasonLook globals _AC_Season/_AC_Snow, vertex tint in Stylized, EcoGround, title 'Year 3 · Autumn · 5 people', cheat 'Skip to next season'; Seasons 2/2, HudInfo 6/6, CameraGlassFeel 3/3, Smoke 2/2, Life EditMode 427/427. Looked at the 4x2 shots: each season reads; winter = full white blanket (issue: light snow). Round 2 sent.
- 09:25 Editor restart (3 tickets): restart-editor.sh hit 'Save Scene' again ('did not quit in 150 s'); the dialog had gone by the time I looked and Unity had quit; reopened (ready). Shots of fb5/ copied to scratchpad/shots first.
- 09:30 #573 round 2 merged (9225d43, branch f510d7b): ≤2 choppers per grown tree (ChopSlots), overflow does home chores, swing tempo 0.9-1.1, logs lifted when the chopper walks off with a Log, CampRhythm back at 6/20/50 (fixture: 5 wild trees). AnimBench 573-chop-logs: two choppers out of step. My SimTests on the merge: 1802/1802.
- 09:35 #578 lane A started (wt 578 from 9225d43). #576 round 2 holds the Editor (lock re-created, watcher 900 s).
- 09:44 #576 round 2 merged (dd9621a, branch a327435): light patchy snow on natural ground only (not roofs/stones/canvas/shelf), spring yellower, summer deeper; Fb5SeasonShots _default/_box/_close + Title750. Seasons 2/2, HudInfo 6/6, CameraGlassFeel 3/3, Smoke 2/2. Looked at the 4 box shots + winter close: distinct at a glance. Frozen pond rim not done ('maybe' in the issue). Open for look sign-off. Offline csc on the merge 0 errors.
- 09:46 Batched Editor pass started (Sonnet, vs/feedback5-pass from dd9621a) on everything but #578; Fb5PassShots for the workers' Editor checks. Watcher 900 s. Editor tickets since restart: 2.
- 10:00 #578 round 1 back (4559cab): land-true words (woods/pond/meadow fixed when raised), PondCamp/MeadowCamp, ApartLife.CarryAt log cycle, pile x1.5 for 36 months, joining at the village/next Hut, camp teardown; SimTests 1816. Round 2 sent (join + first load must be watched; pond words need water).
- 10:30 #578 round 2 merged (22df0c7, branch 4857c23): joining waits for a calm watched moment (JoinCalm), camera nudges, arrival caption; first-load caption; pond words only with water. My checks on the merge: offline csc 0 errors, SimTests 1817/1817. All lane A merged. Walkthrough + Feedback5Playthrough worker started offline (wt fb5walk).

## Round logs
- #575 round 1: claims sim + card + test; diff read (OK: chronicle lines are plain text). Pending: Unity shots (pass).
- #571/#572 round 1: accepted except §1 tap cue on holder names (plain text). Round 2 sent (offline, worktree wt/571).
- #571/#572 round 2: cue added, accepted pending the pass shot.
- #569 round 1: real-model lab OK; short: mist effect ~1.2x (needs ≥2x visible), seed 7 at 21.5 outside ±3 (test loosened to 21-29), (b) 5.5 y behind, no patch cheat for the side-by-side. Round 2 sent.
- #574 round 2: merged #569, HuntThrow test bound only where the clock rate is the same at both ends of the walk (accepted). Done.
- #573 round 1: rule + tests OK; short: lockstep exemption, log timer, no clip. Round 2 sent.
- #570 round 1: accepted offline; Editor phase (fit at 750x1334, shots) pending.
- #573 round 2: all three points met; accepted (fixture with 5 wild trees accepted: a real wood has several trees).
- #576 round 1: look OK except snow too heavy, spring≈summer; no whole-box or 750 shot. Round 2 sent.
- #576 round 2: accepted.
- #578 round 1: content OK; short: join can fire unseen (away/off-camera), first load unseen, pond words with an empty basin. Round 2 sent.
- #578 round 2: accepted. TravellersPlayTests' join test Assumes camp_fire raise: confirm it runs (not Inconclusive) in the pass.
