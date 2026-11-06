# The visit loop: events you see, choices you own, life you can watch (draft, 2026-11-06)

Status: **draft, waiting for the user.** Research: [16 events and choices](../research/16-visit-loop-events-and-choices.md),
[17 readable animations](../research/17-readable-life-animations.md). Feedback behind it: playtest notes #345, #348,
#341 and the user's words of 2026-11-06 (below). Every number here is [est] until a diary test.

## 1. What the user said (2026-11-06)

- "The events that are supposed to become stories, I never see them. I only read about them." Checking in every 4–5 h
  gives a stack of cards (today `SimCore.CardCap`: 3 / 5 / 7 by time away).
- The choice between them was already made while they were away (P7: a dilemma lapses after 24 game months ≈ 2 real
  hours), so "I don't care about the event or the repercussions, because I didn't make it."
- Fewer stories, more impact. **Not all at once or in quick succession.** How often scales with the box: a small camp
  gets one, then another after a long play; a big town sometimes several at once. Each must matter: marriages,
  newborns, the consequences of choices. The history keeps them.
- A meaningful choice on every visit. Visible change matters (seeing the new chief worked).
- Animations: "a lot of abstract animations … a dead world". Cute, not realistic, but they show what is happening: a
  real hunt with the rabbits running off, drawing water, picking at the bushes, talking, kids playing (today "all the
  same circle").

## 2. The model: hold, stage, ask, and show

1. **The sim keeps living while you're away; the moments wait for you.** Pairing, births and growth run as now. The
   *ceremony* (the wedding at the fire, the newborn held up to the glass, the new chief taking the seat, a choice's
   callback, a festival) waits in a small queue and plays **in the box while you watch**. Research 16 §4.1.
2. **A director spaces them by the size of the box.** Roughly: tribe 1 per ~15 watched min (max 2 a session), camp 1
   per ~10 min, village 1 per ~8 min, town 1 per ~6 min with now and then a "busy day" of 2–3 at once. A rest gap after
   each; never two of a kind in a row. The top waiting moment plays on open. A moment held over 8 game years becomes
   a card. Research 16 §4.2.
3. **Fewer cards:** 1 / 2 / 3 by time away, down from 3 / 5 / 7. The rest go straight to History. Research 16 §4.3.
4. **About one choice per visit, unmissable but not forced.** Big dilemmas alternate with small ones (first: naming a
   notable's newborn from two names the parents offer). A choice is raised only while you watch, or held for your next
   open, where it is staged in the box first. It **never resolves unseen**: it closes when you answer, when you pick
   "Let them decide" (credited as your choice), after being shown on 3 visits, or if its people are gone.
   **Not a blocking pop-up** (research 16 §4.4: the failure was unseen lapses, not skipping; games that keep running
   never force a choice; a modal hides the box you answer in; pillars 2 and 4).
5. **Every choice leaves something in the box** within a game year (a feather, a roof, a stone), its callback is a
   live moment, and the person card says "chosen by the Keeper". The book gets "The Keeper's choices" per era.
6. **Life you can watch** (research 17 §4): every work verb touches its real target; the target answers on the contact
   frame (a puff, a shake, a bolt); every animal reacts to people; a 3-second read at the default camera with the
   sound off; each loop ends in a visible beat; neighbours never move in step; there is always a story in progress on
   screen.

Across the ~21 h Camp this gives roughly 10–15 moments seen live and 5–6 owned choices, instead of ~100 card lines
and ~2 answered choices.

## 3. Build order (proposed)

| Step | What | Research | Size |
|---|---|---|---|
| 1 | **Fix the choice:** raise only while watching or on open, never lapse unseen, "Let them decide", cards 1/2/3 (research 16 option B, the core of A) | 16 §4.3–4.4 | S–M |
| 2 | **Contact puffs and target shakes** (uses the `DollPose.hit` flag nobody reads yet) | 17 §5 #2 | S |
| 3 | **Kids play real games** (tag, a drawn ball, chasing rabbits, hide and seek, splashing) instead of one hop circle | 17 §5 #1 | M |
| 4 | **Water and berries you can see**; **the hunt made watchable** (#288, #283) | 17 §5 #3–5 | S + S + M |
| 5 | **The moment queue and director**, weddings / newborns / new chief / callbacks staged live | 16 §4.1–4.2 | M |
| 6 | **Small choices** (naming first), consequence props, the Keeper's choices page | 16 §4.4–4.6 | M |
| 7 | **Diary test** on a phone: moments seen per visit, "did you see it happen?", recall of seen vs read | 16 §6 | — |

Steps 1–4 can start once the user signs off on §2; step 5 waits for step 1's feel on a phone.

## 4. Open questions for the user

From research 16 §5–6 and 17 §7, the ones that change what gets built:

1. **Model A ("hold, stage, ask")** as above, or start smaller with only the choice fix (option B)?
2. **"Let them decide"** as a third answer to every choice: yes / no.
3. **A choice nobody answers** closes after being shown on 3 visits / 2 / never.
4. **Holding a wedding** for your return when the couple paired years ago: honest enough ("they waited to show the
   Keeper"), or only stage what happens while you watch?
5. **How busy:** a story always in progress and a new one every ~10 s, or calmer?
6. **Kids' games first:** tag, ball and chasing rabbits (★), plus hide and seek / splashing? May kids scare the rabbits?
7. **The hunt:** the spear is thrown and sticks (★, reads better) or a lunge?
8. **Puffs:** cartoon-style solid shapes (★) or soft and subtle? Hugs between peg dolls: in tone or too much?
9. **Polish phase:** this is a "Visit loop" milestone (the user agreed in principle, 2026-11-06); confirm it may add
   the director and small choices, which are new systems.
