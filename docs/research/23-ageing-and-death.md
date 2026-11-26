# 23. Ageing and death: leaning into the feeling (#777)

Direction (decisions.md, 2026-11-25): the feeling of watching a person grow old, and knowing they may be gone by morning, is something the game leans on. This note maps what the game does now, what other games do, and what could be built inside the polish phase. Nothing here is decided: the user picks (#777).

## 1. What the game does now

**Time.** 1 real hour is 1 game year (decisions.md, 2026-10-28). A notable draws a lifespan of 55 to 80 years (`lifespanMin/Max`, `Sim/SimState.cs`): about 2.5 to 3.5 real days of watching. Retirement is at 65, the elder stage starts at 45 (`retireAge`, `elderAge`). The long 110-160 lifespan is parked for the final game (decisions.md, 2026-10-29).

**Ageing.** Notables age with the calendar (once a game year, `YearlyNotables`, `Sim/SimCore.cs:292`, called at `SimCore.cs:151` and `SimCore.Museum.cs:60`). Stages: baby, kid, adult, elder (`LifeStage`, `Life/LifeTypes.cs`). At 65 a notable leaves their job and becomes an Elder (`SimCore.Jobs.cs`). Masters can take apprentices from 50 (`masterAge`).

**How people die.** One way: old age. When age reaches the drawn lifespan the notable dies, and the chronicle gets "died of old age at N" at salience 0.85 (`SimCore.cs`, `SimCore.cs:303-308`). Crowd people die by a small dice roll with no scene. There is no illness, accident or visible decline. The check runs once a game year (about once an hour of play), so a death lands on that yearly tick with no warning shown.

**What elders do.** `ElderDay` (`Life/LifePlanner.Day.cs:807-832`, picked by life stage at `Day.cs:362`): sleep, sit on a stool or with a stick, watch farm work, mind babies, eat with their partner, tell a story at the fire (`StoryTell`), sit with the old (`SitWithOld`), go home early. The only input from age is the stage: nothing in `ElderDay` reads age or remaining lifespan, so a 65-year-old and a 79-year-old live the same day (`Day.cs:807-832`). Nothing changes in the last days.

**After a death.**
- A mourning scene the same evening, 17:12 to 18:36, at the remembering place (`SceneKind.Mourning`, `LifePlanner.Scenes.cs:46-52`). Family, partner, friend and parent come; close ones hold a flower (`ActivityKind.Mourn`).
- A quiet visit to the Keeper's tree (the user's favourite beat, `docs/design/people-and-things.md`).
- The name stone, a Keeper's choice prop (`ConsequenceKind.NameStone`). Later names are added; a second stone and the gardener's flowers exist (#722, 1.3 and 2.3). Kids polish it, people leave gifts (B51).
- A **death card** in the return chronicle: warm, after good news, with Remember, which opens their book page (`docs/design/chronicle-and-history.md`).
- The **Book** keeps legends. A dead legend's page says "Died in year N, aged M", who remembers them, their stories, family and relics (`BookLegend`, `SimCore.Book.cs`).
- The **person card** turns into a memorial (`Memorial`, `SimCore.People.cs`). Some dilemmas need a death first (`needsDeath`). An elder leader triggers the succession dilemma (`leaderElder`).

**The Keeper's tie.** One pin at a time. A pinned person's stories rank higher on return. When they die the pin stays, and their death card ranks first among the sad ones (`SimCore.People.cs`, the pin rules). This is the strongest hook now.

**What carries on.** Children are notables from birth. Apprentices inherit a trade. Relic keepers keep a relic. A seat passes with the antlers (`AntlersPassed`). An elder hands the Keeper's child a tool (#722, 5.1). The pieces exist but are not tied to "this person is leaving".

**The gap in one line.** The death is handled well, but the days before it are flat, and the moment itself is a line on a card you read after the fact.

## 2. Games that do it well

Checked by web search on 2026-11-25 (sources at the end, mostly wikis, press and forums); paraphrased.

| Game | Mechanic | Feeling | Fit for us |
|---|---|---|---|
| **Wildermyth** | Heroes age. Past retirement age a hero retires at a chapter's end, can pass experience to a rookie, and joins your legacy; later they return as a younger version with their traits, and some games let children join. | Pride, a story that carries on. | Strong: a last beat, then the line continues. |
| **Spiritfarer** | You care for a spirit, then give a last hug before they cross the Everdoor. Hugging is a core, simple mechanic. | Closure. The goodbye is the point. | Strong for "last days with visits". Heavy for an idle toy: keep it light. |
| **Crusader Kings 3** | When your character dies you play on as the heir of the main title. Succession laws decide who inherits. | Tension about who comes next. | Our succession dilemma is a small cousin. |
| **Dwarf Fortress** | A dead dwarf with no burial leaves a ghost. A memorial slab, engraved by a craftsman, calms it. | Duty to the dead, slow attachment. | The name stone is this. The craft-to-remember idea is a good model. |
| **RimWorld** | Graves, funerals, and a mood thought for a colonist left unburied. Colonists visit graves for meditative joy. | Loss shared by the group. | Funeral scenes and visited graves fit. Skip the mood penalty. |
| **The Sims 4** | Only elders die of old age. The Grim Reaper clears the body. Ghosts are tied to an urn or tombstone and fade unless the bond is strengthened. | Playful, a little funny. | A tombstone you can still visit fits. Ghosts do not (tone, closed world). |
| **Kynseed** | The player character ages and dies, and a family member takes over. Deaths are marked on a monument, relatives mention the dead, the family keeps a reputation. | Warm, slow passing of time. | Closest match in tone. |
| **Animal Crossing** | A villager who wants to leave goes through a packing stage: the last chance to talk to them and get a parting item. | A small, real sadness. | Shows a short, visible goodbye is enough. |

**What the good ones share.**
1. The goodbye is a scene you can walk up to and take part in (Spiritfarer, Animal Crossing).
2. Something of the person keeps going: an heir, a legacy, a monument (Wildermyth, Kynseed).
3. The place keeps a trace (Dwarf Fortress slab, Kynseed monument, Sims tombstone).
4. Grief is shared by the group (RimWorld) rather than put on the player.
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
- **Cost:** M. Today the scene is built after the fact from the chronicle entry (`Scenes.cs:46`); the planner would need to know the death day ahead.
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

## Sources

- Wildermyth: https://wildermyth.com/wiki/Memories_and_Dreams, https://wildermyth.itch.io/wildermyth/devlog/284868/patch-notes-11345-aremella-errowlair
- Spiritfarer: https://www.gamesradar.com/spiritfarer-offers-a-rewarding-heartfelt-experience-about-moving-on-and-learning-to-let-go, https://vice.com/en/article/spiritfarer-a-game-of-hugging-and-letting-go-is-perfect-for-right-now
- Crusader Kings 3: https://www.gamewatcher.com/crusader-kings-3-succession-guide
- Dwarf Fortress: https://dwarffortresswiki.org/Slab, https://dwarffortresswiki.org/Ghost
- RimWorld: https://rimworldwiki.com/wiki/Grave, https://rimworldwiki.com/wiki/Thoughts/Memory_Death
- The Sims 4: https://help.ea.com/en/articles/the-sims/the-sims-4/sims-4-deaths/, https://help.ea.com/help/the-sims/the-sims-4/sims-4-ghost-guide
- Kynseed: https://www.siliconera.com/kynseed-developers-talk-creating-legacies-death-farming/
- Animal Crossing: https://nookipedia.com/wiki/Moving
