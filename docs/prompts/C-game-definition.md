# Prompt — Phase C: define the game + prototype plan

Paste this into a new Claude Code session started in `F:\GameDev`.

---

We're starting **Phase C: defining exactly what Aquarium Civ is**, and planning a set of small prototypes to A/B test what's actually fun.

## Read first (don't skim the synthesis)
- `CLAUDE.md`: working rules, including the 1-minute stuck-check rule for every Unity call.
- `docs/README.md`, `docs/decisions.md`: what's already decided (mobile, Unity 6.3 LTS URP, 3D glass diorama, solo dev, free assets only for now).
- `docs/research/00-synthesis.md`, then `02-idle-design.md` and `03-aquarium-references.md` in full: design theory, pattern library, competitors.
- `AquariumCiv/Assets/Editor/Agent/LookTestBuilder.cs` and `Assets/Scenes/LookTest.unity`: the current visual baseline (KayKit hex village in glass).

## Step 1: interview me before writing anything
Ask me questions in batches of 3–4 with concrete options, not open-ended questions. Cover at least:
- How neglect should *feel* (guilt, comic chaos, slow beautiful decay), and whether people can die or the civ can collapse.
- What the player actually *does* in a 3-minute check-in, and in a 20-minute session.
- How much control I have (pure caretaker, or nudging, or building).
- What "cleaning" is concretely (glass grime, waste, disease, overgrowth, weather...).
- Whether progression runs through eras, through a bigger tank, through multiple tanks on a shelf, or through prestige.
- Emotional reference points (Neko Atsume? WorldBox? Tamagotchi? Townscaper?) and anti-references.
- Business model hypothesis (premium, free + one unlock, rewarded ads).

Recommend an answer for each question, based on the research.

## Step 2: concept variants
Propose **3–4 distinct concept variants** that differ on the *riskiest* design axes (e.g. care-heavy vs watch-heavy, individual named citizens vs crowd/abstract civ, real-time vs time-compressed, builder vs pure caretaker). Give each one:
- a one-line pitch and the core fantasy
- the moment-to-moment, session, and long-term loops
- what the care/neglect mechanic is and how it avoids being a chore
- the biggest risk, and the single question a prototype must answer

## Step 3: prototype + A/B test plan
Design a series of **small, throwaway Unity prototypes**, each testing ONE hypothesis about fun. Each prototype should take 1–3 days of agent work.
- **Shared foundations:**
  - a minimal sim core
  - a debug time-scale slider (1 real minute = 1 game day, so offline/return moments can be tested in minutes)
  - a fake "you were away 8h" button
  - local event logging (session length, taps, what was looked at, what was cleaned) to a JSON file
- **Isolation:** each prototype is a separate scene under `Assets/Prototypes/<Name>/`, reusing KayKit assets and the look-test diorama.
- **A/B pairs:** where possible, pair prototypes that differ in only one variable. Examples: neglect lowers output vs neglect makes visible mess; named citizens vs anonymous crowd; tap-to-clean vs drag-to-wipe the glass.
- **Test protocol for each:**
  - what I (and a few friends via Android internal-test builds) do
  - what we measure: logged metrics plus a 5-question post-session survey
  - what counts as "this is fun / this isn't"
- **Order:** sequence the prototypes so the riskiest assumption (is caring for the tank fun, not a chore?) is tested first.

## Output
- `docs/design/game-definition.md`: pillars, core fantasy, loops, care mechanic, progression, session design, monetization hypothesis, open questions. Mark which parts are hypotheses awaiting prototype results.
- `docs/design/prototype-plan.md`: hypotheses, A/B pairs, scope per prototype, metrics, test protocol, order.
- Add new decisions to `docs/decisions.md`.

Don't build any prototype until I've approved the concept variants and the prototype plan.
