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
- [ ] Lane A: writing #713–#716 (+ sim parts of #718, #721) (vs/713-writing, b3-713, Opus)
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
- [ ] Editor: AnimLab clips for approved specs; #708 fish; #709 nest view
- [ ] Editor: writing #714 boat, #717, #718 UI, #719, #720, #721, #723
- [ ] Editor: #668 build of the chosen mock-up
- [ ] People and things #692–#695 builds (after clip sign-off)
- [ ] Batched pass, smoke, tester, walkthrough stops, PR to dev

## Log
- 2026-11-15: p9/batch3 created and pushed; four lane A workers started. Implement says its Editor (pt1b) frees in
  ~20–30 min, left on p9/playtest1-b-signoff; #737 auto-merges into dev, then #736.
