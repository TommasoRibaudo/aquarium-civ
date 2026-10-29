---
name: Work item (slice)
about: One piece of slice work, small enough for one agent in one sitting (≤ ~1 agent day). See docs/workflow.md.
labels: []
---

**Why:** <!-- link to game-definition section / decision / research doc -->

**What:**
<!-- 3–6 lines. What's in, what's explicitly out. -->

**Touches:** <!-- folders/assemblies it may change, e.g. AquariumCiv/Assets/Game/Sim/**, docs/... -->

**Unity:** none | Editor | play mode

**Done when:**
- [ ] <!-- testable check: tests named + green, numbers hit -->
- [ ] `dotnet test Tools/SimTests` green (lane:sim) / Unity tests green (`editor` mode, count > 0)
- [ ] tester agent pass (visual / UI / gameplay changes)
- [ ] user sign-off (only if a look/feel call)

**Depends on:** <!-- #issues -->

**Needs the user:** <!-- the decision, or "nothing" -->
