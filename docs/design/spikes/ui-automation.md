# Spike: UI that automation can click (2026-10-29, branch vs/prep)

**Problem.** Every prototype panel is IMGUI (`OnGUI`). In this project (Input System only), no automation path reaches
IMGUI buttons: `GameView.SendEvent` and `QueueGameViewInputEvent` don't arrive, in play mode or under the touch fixture
(tooling.md 2026-10-29). So survey, debug and hub buttons were never tested by input: the tester could only call methods
by reflection, which skips the real path (layout, hit areas, overlap: the P6 "first-drawn button gets the tap" trap).

**What was built** (not used by any prototype):
- `_Shared/Runtime/UI/ToolkitSurveySpike.cs`: a survey-like card in **UI Toolkit**, built in code (no UXML): a question,
  five 1–5 buttons, Send (disabled until answered). Every control has a stable `name` (`q1-1` … `q1-5`, `send`).
- `UiProbe.ScreenCenter(root, "send")`: finds a control by name and returns its screen point for touch input.
- `_Shared/Resources/UiToolkit/SpikePanel.asset` (scale with screen size, 1080×1920 reference) and `SpikeTheme.tss`
  (the default runtime theme).
- `_Shared/PlayTests/` (new PlayMode assembly, any platform) with `ToolkitTapTests.ATouchTapsToolkitButtonsByName`:
  an `InputTestFixture` touchscreen taps Send while disabled (nothing happens), taps "4" (rating 4, Send enabled), taps
  Send (sent with 4). **Passed 1/1 on the first run**: no event pump, no manual update mode, no focus workaround beyond
  the two Input System settings. 0 console errors or warnings.

**Recommendation for the slice: UI Toolkit for all screen UI**, with these rules:
1. Every interactive element gets a stable `name`; tests and the tester agent find controls by name, never by pixel.
2. Build panels from UXML/USS once the look matters (the spike builds in code only to stay small). USS limits to
   remember: tooling.md §8 (no `gap`, no `:nth-child`, no runtime tooltips).
3. One `PanelSettings` asset, scale with screen size (1080×1920 reference, match width), respecting the safe area.
4. World input (wipe, camera, taps on people) stays on the Input System; UI Toolkit consumes touches over its panels,
   which replaces the prototypes' hand-made `GuiBlocker` / `ProtoGui.Block` rects. **Still to verify** in the slice's
   first scene: that a touch on a panel never reaches `OrbitCamera` / `WipeCleaner` (check `panel.Pick` before world
   input, or the EventSystem's pointer-over-UI test).
5. Keep one PlayMode tap test per screen (survey, settings, first run): a broken button then fails CI-style tests
   instead of a tester's session.

**Not done (out of spike scope):** no visual pass (the card uses the default theme; no screenshot judged), no port of
any prototype panel, no CI run of PlayMode tests (CI runs EditMode only; adding PlayMode to CI is a small follow-up).

**Run it:** `unity command test run --mode playmode --filter AquariumCiv.Prototypes.Shared.PlayTests --filter_type assembly --async_tests true`,
then `unity command test status`. Note `--mode playmode`: `--mode play` silently runs 0 tests (tooling.md).
