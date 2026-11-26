# 23. Ageing and death: leaning into the feeling (#777)

Direction (decisions.md, 2026-11-25): the feeling of watching a person grow old, and knowing they may be gone by morning, is something the game leans on. This note maps what the game does now, what other games do, and what could be built inside the polish phase. Nothing here is decided: the user picks (#777).

## 1. What the game does now

**Time.** 1 real hour is 1 game year (decisions.md, 2026-10-28). A notable draws a lifespan of 55 to 80 years (`lifespanMin/Max`, `Sim/SimState.cs`): about 2.5 to 3.5 real days of watching. Retirement is at 65, the elder stage starts at 45 (`retireAge`, `elderAge`). The long 110-160 lifespan is parked for the final game (decisions.md, 2026-10-29).

**Ageing.** Notables age with the calendar (monthly step, `Sim/SimCore.cs`). Stages: baby, kid, adult, elder (`LifeStage`, `Life/LifeTypes.cs`). At 65 a notable leaves their job and becomes an Elder (`SimCore.Jobs.cs`). Masters can take apprentices from 50 (`masterAge`).

**How people die.** One way: old age. When age reaches the drawn lifespan the notable dies, and the chronicle gets "died of old age at N" at salience 0.85 (`SimCore.cs`, near line 303). Crowd people die by a small dice roll with no scene. There is no illness, accident or visible decline. The death lands on a month tick with no warning shown.

**What elders do.** `ElderDay` (`Life/LifePlanner.Day.cs`): sleep, sit on a stool or with a stick, watch farm work, mind babies, eat with their partner, tell a story at the fire (`StoryTell`), sit with the old (`SitWithOld`), go home early. A 65-year-old and a 79-year-old live the same day. Nothing changes in the last days.

**After a death.**
- A mourning scene the same evening, 17:12 to 18:36, at the remembering place (`SceneKind.Mourning`, `LifePlanner.Scenes.cs`). Family, partner, friend and parent come; close ones hold a flower (`ActivityKind.Mourn`).
- A quiet visit to the Keeper's tree (the user's favourite beat, `docs/design/people-and-things.md`).
- The name stone, a Keeper's choice prop (`ConsequenceKind.NameStone`). Later names are added; a second stone and the gardener's flowers exist (#722, 1.3 and 2.3). Kids polish it, people leave gifts (B51).
- A **death card** in the return chronicle: warm, after good news, with Remember, which opens their book page (`docs/design/chronicle-and-history.md`).
- The **Book** keeps legends. A dead legend's page says "Died in year N, aged M", who remembers them, their stories, family and relics (`BookLegend`, `SimCore.Book.cs`).
- The **person card** turns into a memorial (`Memorial`, `SimCore.People.cs`). Some dilemmas need a death first (`needsDeath`). An elder leader triggers the succession dilemma (`leaderElder`).

**The Keeper's tie.** One pin at a time. A pinned person's stories rank higher on return. When they die the pin stays, and their death card ranks first among the sad ones (`SimCore.People.cs`, the pin rules). This is the strongest hook now.

**What carries on.** Children are notables from birth. Apprentices inherit a trade. Relic keepers keep a relic. A seat passes with the antlers (`AntlersPassed`). An elder hands the Keeper's child a tool (#722, 5.1). The pieces exist but are not tied to "this person is leaving".

**The gap in one line.** The death is handled well, but the days before it are flat, and the moment itself is a line on a card you read after the fact.

## 2. Games that do it well

From general knowledge of these games, paraphrased; I did not run a fresh web search for this draft, so check details before quoting any of it.

| Game | Mechanic | Feeling | Fit for us |
|---|---|---|---|
| **Wildermyth** | Heroes age across chapters. Old heroes gain traits and a final legacy: retire into a statue, a home, a handed-down item. | Pride, a sweet ending. | Strong: a last beat that leaves something. |
| **Spiritfarer** | You care for a spirit, then share a long goodbye (a hug, a ferry-off) and they leave something behind. | Closure. The goodbye is the point. | Strong for "last days with visits". Heavy for an idle toy: keep it light. |
| **Crusader Kings** | Characters age, fall ill, die. You play on as the heir; traits pass down. | Tension about who comes next. | Our succession dilemma is a small cousin. |
| **Dwarf Fortress** | Memorial slabs, engravings of a life, restless ghosts if unburied. | Slow attachment from detail. | The name stone is this. Marks on props are the next step. |
| **RimWorld** | Graves, funerals, a mood hit for the people who loved them. | Loss shared as a mood. | Funeral scenes fit. Skip the mood penalty. |
| **The Sims** | Age stages, birthdays, the grim reaper, a tombstone you can visit. | Playful, a little funny. | A tombstone you can still visit fits. Ghosts do not (tone, closed world). |
| **Kynseed** | Play across generations; the family ages, children take over, the town remembers. | Warm, slow time passing. | Closest match in tone. |
| **Animal Crossing** | A villager moves out; a farewell on the last day, a keepsake left. | Small, real sadness from one scene. | Proves a short, visible goodbye is enough. |
| **Tamagotchi** | One pet, a short life, death if neglected. | Strong care bond, guilt. | Avoid the guilt: the Keeper is never to blame. |
| **Gone Home, A Long Walk** | A place holds traces of someone absent. | Quiet ache from objects. | Strong for "what they leave behind". |

**What the good ones share.**
1. The goodbye is a scene you watch, not a message.
2. Something of the person keeps going: an item, a trade, an heir.
3. The place keeps a trace.
4. The player is not blamed.
5. The sadness is short and gentle, and leads into continuity.

## 3. Options inside the polish phase

Rules applied: seen, not read; no big new systems; a new animation needs a spec and the user's sign-off; closed-world writing; a new thing needs a people-use row and a debug cheat.

### A. Last days
- **Sees:** in about the last two game years the person walks slower, sits longer in the sun on the same stool, naps there. Family come and sit beside them (`SitWithOld`).
- **Cost:** M. A "frail" window from lifespan minus age; the planner bends `ElderDay`.
- **Reuses:** `ElderDay`, `SitWithOld`, stool and stick, the pin.
- **Risks:** low if gentle. Use a window, not an exact date, so it never reads as a timer.
- **New animation:** a slower elder walk and a nap pose need specs. The cheap first step is only speed and sit time, no new clip.

### B. The last evening (goodbye)
- **Sees:** on the evening of death the person is at their stool, the fire or the Keeper's tree. Family sit round, a hand on a shoulder. Then the existing mourning scene runs.
- **Cost:** M. Today the scene is built after the fact from the chronicle entry; the planner would need to know the death day ahead.
- **Reuses:** `SceneKind.Mourning`, the tree visit, `Mourn` with a flower.
- **Risks:** the idle pace. If the Keeper is away, the goodbye happens unseen; the return card says it happened. If the Keeper is there, it is the strongest moment.
- **New animation:** a slump or lying pose and a hand-hold need specs. Cheapest honest version: they sit, the family gather, the figure fades like other quiet beats.

### C. What they leave behind
- **Sees:** the stool nobody takes for a day, then a child sits on it. The worn tool goes to an apprentice or child (the 5.1 hand-over, extended).
- **Cost:** S to M. Mostly data: pick the object by job, place it for a day, hand it on.
- **Reuses:** props, apprentices, the tool hand-over, `Memorial.rememberedBy`.
- **Risks:** low.
- **New animation:** none for the stool; the hand-over reuses the 5.1 clip.

### D. Who carries it on
- **Sees:** the apprentice takes the master's tool and seat. A child repeats one small gesture of the dead person, once. The return card says "X took up Y's basket".
- **Cost:** M. The links exist (child, apprentice, seat); the new part is picking an heir and one echo.
- **Reuses:** `parentId`, apprentice links, relic keepers, `AntlersPassed`.
- **Risks:** can read as a replacement. One echo, once.
- **New animation:** the echo gesture needs a spec; existing clips (flower, tool) are free.

### E. How the Keeper notices
- **Sees:** a soft sign on the pinned elder (a gentle glow, no number). A glance line: "Mara is slowing down". A return card: "the family sits with Mara". After death the existing death card, ranked first for a pinned person.
- **Cost:** S for text and ranks; M for a world marker.
- **Reuses:** the pin, chronicle cards, glance line, person card.
- **Risks:** a death notification can feel grim or pushy. Keep it off by default; at most a soft "something happened at the tree".
- **New animation:** none (a marker is a view effect).

### F. Statue of the founder (#776)
- **Sees:** long after the founder's (or longest-pinned person's) death, a small statue stands on the green. Kids climb the base, flowers come on its day.
- **Cost:** M to L (prop, placement, three use beats, anniversary).
- **Reuses:** the name stone, the gift at the stone, `Memorial`, the pin.
- **Risks:** a payoff long after the feeling; it serves legacy, not the last days.
- **New animation:** climbing and flowers need specs (the issue already says so).

### G. Family and stories on the card, family tree in History (#774)
- **Sees:** open an old person and see partner, children and stories first. A tree shows who came from whom, so a loss reads as a branch, not a deletion.
- **Cost:** M to L. UI, mock-ups signed off first.
- **Reuses:** `PersonCardOf` ties, the Book.
- **Risks:** read, not seen. Good for return visits, weak for a watched moment.
- **New animation:** none.

## 4. Recommendation

**Build first, in this order:**

1. **A. Last days**, starting with the cheap version (speed, longer sits, family beside them, no new clips). It is the feeling the user named: the knowing before the goodbye. It uses what exists. A window of about two years keeps it from reading as a timer.
2. **E. How the Keeper notices** (card text, glance line, a soft sign on the pinned elder, notification off by default). It is small, and without it A is invisible on a short visit.
3. **C. What they leave behind** (the stool nobody takes, the tool handed on). Small, a visible trace, and it closes the loop after the mourning scene.

**Then:** B once its animation spec is signed off. D and F as later payoffs. G (#774) for the legacy side, since it is already planned and mock-up gated.

**For the user to decide:** whether to show any sign of frailty at all (the lifespan is hidden today), and whether an unseen death should ever send a notification.
