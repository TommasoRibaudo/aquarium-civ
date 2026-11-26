# Polish night 1 (2026-11-25/26): sign-off

The user signs off on the private Artifact page (link in the PR). Same items, questions and media here, without the
media files. Media were recorded under `AquariumCiv/Temp/agent/` in `F:\GameDev-wt\pt1b` (local only) and in the
orchestrator's scratchpad (`pn1page/m/`). Verdicts go into `docs/decisions.md` when the user answers.

Branch: `p9/polish-night-1` → PR to `dev`. Spec-gated, not in the PR: `vs/776-statue-spec`.

| # | Item | State | Media | Question |
|---|---|---|---|---|
| 766 | One sheet at a time (remembered note) | closed | 766-other-card | Book and the other card clean after remembering? |
| 764 | The wedding's "I do" waits for the vows | merged, open | 779-5.2 (gif) | Line at the right moment? |
| 765 | Two fishers on one spot | closed | 765-fishers | Spread enough? |
| 762 | Fields over the leather rack | closed | 768-7-after | Good? |
| 763 | Feather pole flicker (cause: z-fighting inside each feather) | merged, open | none (a flicker; check on the phone) | Steady when panning? |
| 779 | Nobody inside anybody in 1.2, 2.1, 5.2 | merged, open | 779-1.2, 779-2.1 (gifs) | Read as people together, not merged? |
| 784 | Two story cheats that refused | closed | none (CI play test) | Fine to close? |
| 782 | "Nothing to eat" during the fruit pick | closed | 782-fruit-pick | Good? |
| 780 | The choice card fits small phones (grows 30 to 60 %) | merged, open | 780-4.1-750, 780-4.4-750 | OK that a long question makes the card taller? |
| 781 | The song card's hint names the ritual | merged, open | 781-song-750 | Which words (★1 wired; options in docs/design/polish-night-1-words.md)? |
| 773 | Supper: one endless stew or a fresh one each time | merged, open | 773-supper-750 | Which words; two evenings enough or should the stew persist (a feature)? |
| 564 | The glass voice's first line on the first mist (launch blocker) | merged, open | 564-first-mist | Which line (★1 "The sky wept. The dust drank it.")? |
| 767 | Fishers chatting keep their rods over the water (22° body turn) | merged, open | 767-chat (gif) | Reads as chatting, or do we need a separate head? |
| 770 | The dance keeps off buildings (lots incl. the well; dressing follows the ring) | merged, open | 770-dance (gif), 770-ring | Good? |
| 771 | Wolves' eased turn on the spot | merged, open | 771-wolf-turn (gif, new left / old right) | Good? Deer and dogs too? |
| 772 | The glass stops a thrown ball | merged, open | 772 clip (reshoot) | Fine as built? |
| 768 | Fields on good ground, away from the centre | merged, open | 768-{7,11,1}-{before,after} | Fields where you'd expect? |
| 769 | The church out of the wood (eco seeding kept off its lot) | merged, open | 769-{7,11}-{before,after} | Good? |
| 775 | Dirty glass reads dirtier, a film stays zoomed in | merged, open | 775-after (sheet) | The look you wanted? |
| 559 | Weeds read as weeds; trash stays removed (launch blocker) | merged, open | 559-after, 559-close | Weeds right; trash stays removed? |
| 783 | Story camera lean-in (spec, docs/design/783-story-camera.md) | spec | 783-speck-750 | Build it? Every held scene or only story events? |
| 774 | Person card and family tree (mock-ups, docs/design/mockups/774) | mock-ups | 774-* | Card A or B; tree A, B or both? Dead stay in the tree? |
| 776 | Statue of the founder (spec, branch vs/776-statue-spec, B81) | spec | none | Spec approved? Founder rule, one-year wait, caption? |
| 777 | Research 23: ageing and death | research | none | ★ A then E then C? Frailty shown? Notify unseen deaths? |
| 118 | Flaky person-card test (+ a real LaundryView bug found) | closed | none | Fine to close? |

Calls made without the user (also in the PR body): the #780 card grows instead of shrinking text; #767 is a 22° body
turn because the head is part of the body mesh; #559 left trash removed (decision 2026-11-02); #775 raised the first
box's opening grime limit 0.30 → 0.33; #771 changed wolves only; #770 moved the festival dressing with the ring.
