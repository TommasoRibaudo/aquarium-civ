# Batch 3 status (started 2026-11-15)

User, 2026-11-15: "do 708, 709, full landscaping, full weeding, full people and things, and 668 kit mock ups. I will
have to approve the animations and assets." Another session (Implement) held the Editor at the start: lane A only until
it hands over.

Integration branch: `p9/batch3` (worktree F:\GameDev-wt\batch3), from p9/playtest1-b-signoff (= deeper box #737 +
sign-off #736 + dev). PR into dev at the end. Mode: hybrid (workflow.md §7). Worker rules: scratchpad batch3-rules.md.

## Gates the user approves
- Animation + asset specs (#692–#695, #708 fish, #709 nest) → docs/design/batch3-approvals.md, BEFORE AnimLab clips.
- Then each AnimLab clip, before merge (user rule 2026-11-14).
- Kit mock-up pick + words (#668) → docs/design/kit-mockups.html, kit-words.md.
- Writing lines [est] (#714–#721) and the plucking tool name / grown-tree pluck (#730).

## Plan
- [x] Lane A: landscaping sim #726–#730 (vs/724-landscaping-sim, Sonnet) — round 2 re-tuned earning (first seed y2–5, 4–5 by the village, village year 23.0); merged 2d6f6c1
- [x] Lane A: writing #713–#716 + sim parts of #718, #721 (vs/713-writing, Opus) — round 2 fixed the closed-world
  lines; merged (3c29c1a + csproj fix d41f73d); full sim suite 2081/2081. Lines added to the approvals page (W714–W721, W718).
- [x] Editor: writing view side #718 #721 #714-boat #717 #719 #720 #723 (vs/718-writing-view in pt1b, Opus) — merged 5f78e9b.
  #718 removed wishes/Sky Crumb/Lens/tap-back UI (PlayMode 666→661, EditMode 2574→2559 incl. new tests); #721 rituals as
  held moments (festival gathering); new Sim/ContentRules.cs + ContentRulesTests/ContentRulesViewTests; fixed
  LaundryLines.ColourOf negative index. Failing on base too: SettleTests.NudgeAnswers, EveningTalk teaser, ShapingTray (2),
  CardStack LongCards, Feedback5 rabbit stalk, PeopleViewTests (2, LaundryView Destroy), WorkHitTests. Shots in scratchpad
  shots-718. Open: boat still not a boat (needs an asset OK); ritual/teller staging needs specs; stage 1/3 later bank lines
  unreachable since #718; fewer story bubbles; sandal label "Lost to the stream". Editor ready, lock released.
- [x] Lane A: specs #692–#695, #708, #709 (vs/692-people-things-specs, b3-692, Opus) — merged d53de79
- [x] Lane A: kit inventory, mock-ups, words #668 (vs/668-kit-mockups, b3-668, Opus) — merged e0e9d3c
- [x] Approvals page for the user: https://claude.ai/artifact/DJ8y9LKcqkKctRhsWjT7U9 (186 specs/assets, Q1–Q13, kit K1–K6; answers come back pasted)
- [x] Lane A: #709 nest sim (vs/709-nest-sim, Sonnet) — merged 0a62cdd. Q4–Q6 defaults as one value each
  (NestBirdSlots 0, nestChicks 2, nestWhichThicket 0), nestWinterGone false. **Editor pass:** move
  `ResolveNestVisits` out of FishRackView into the nest view (it rides the rack view's Update today); draw B71.a–d
  after the user's spec OK.
- [x] Editor: landscaping view/UI #726/#730/#731/#732 — merged d5452a0 (incl. the #658 save fix d10720a from Implement).
  Unity: Nest 19, NestPlan 6, ThicketList 11, PluckTool 14, LandscapingPlay 1, Smoke 2, Eco 51, LifeJar 19, PlacedLife 23,
  BoxGrow 6, all pass. Shots in scratchpad shots-731. Open [est]: tool name, earned-seed ghost is still the Keeper's mound,
  small pluck ring, the rail wraps to two rows (both for #668). pt1b worktree left on vs/731-landscaping-view, Editor ready, lock released.
- [x] Editor: #668 kit design A + #714 Kenney boat (vs/668-kit-build) — merged ba397df; sim 2068/2068.
- [x] Editor: #708 fish, #709 nest view, boat flat and apart from the pot (vs/708-709-view) — merged 4bd7aaf; sim 2069/2069.
  **All 10 clips signed off by the user 2026-11-15** (https://claude.ai/artifact/NEbta4ca1WZotqvACcyX4k). Known: AnimBench
  build broken (`Principles.cs(46)` `DollStyle.Peer`), fix in the next Editor pass.
- [ ] Base failures listed above (present on p9/batch3 before the writing merge): triage before the PR
- [ ] People and things #692–#695 builds, with Q3 rope swing and Q7 goat; clips signed off per issue
- [ ] Lane A: ritual and teller staging specs (S-ritual) into animation-specs.md for the user's OK
- [ ] Batched pass, smoke, tester, walkthrough stops, PR to dev

## Log
- 2026-11-15: p9/batch3 created and pushed; four lane A workers started. Implement says its Editor (pt1b) frees in
  ~20–30 min, left on p9/playtest1-b-signoff; #737 auto-merges into dev, then #736.
