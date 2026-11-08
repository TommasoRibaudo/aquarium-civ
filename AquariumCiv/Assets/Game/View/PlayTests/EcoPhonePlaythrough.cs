using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P8 the way the phone plays it (the user's build 15 run, 2026-11-03): a fresh living-land box, then only touches a player
    /// makes. Wipe the glass with strokes across the whole screen, hold to mist, name it by tapping the plate, then the Kit
    /// button: the mister mists many spots, Done puts it down; the seed (once the land has earned it, reached with the debug
    /// cheats after the first run) plants the tree with one tap. No step is skipped. Screenshots of every beat go to
    /// Temp/agent/phone_*.png.
    /// </summary>
    [Category("EcoPhone")]
    public class EcoPhonePlaythrough : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView Run => FirstRunView.Instance;

        Vector2 ScreenOf(float x, float z)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z)));
        }

        /// <summary>A player's wipe: rows across the whole screen, where there's glass under the finger and no UI on it.</summary>
        static List<Vector2[]> PlayerStrokes(CareView care, float spacing)
        {
            var list = new List<Vector2[]>();
            bool right = true;
            for (float y = Screen.height * 0.98f; y > Screen.height * 0.02f; y -= spacing)
            {
                float first = -1f, last = -1f;
                for (float x = 6f; x < Screen.width - 6f; x += 10f)
                {
                    var p = new Vector2(x, y);
                    if (!care.GlassAt(p) || UiInput.Over(p)) continue;
                    if (first < 0f) first = x;
                    last = x;
                }
                if (first < 0f) continue;
                list.Add(right ? new[] { new Vector2(first, y), new Vector2(last, y) } : new[] { new Vector2(last, y), new Vector2(first, y) });
                right = !right;
            }
            return list;
        }

        /// <summary>Cells within 0.9 u of a point that are grass or have shoots (the little green a mist leaves).</summary>
        int GreenNear(float x, float z)
        {
            int n = 0;
            for (int c = 0; c < AquariumCiv.Sim.Eco.EcoLand.N; c++)
            {
                float dx = AquariumCiv.Sim.Settle.SiteMap.CXc(c) - x, dz = AquariumCiv.Sim.Settle.SiteMap.CZc(c) - z;
                if (dx * dx + dz * dz > 0.81f) continue;
                if (Sim.EcoCoverAt(c) != AquariumCiv.Sim.Eco.EcoCover.Bare || Sim.EcoSproutAt(c) > 0f) n++;
            }
            return n;
        }

        int WetNear(float x, float z)
        {
            int n = 0;
            for (int c = 0; c < AquariumCiv.Sim.Eco.EcoLand.N; c++)
            {
                float dx = AquariumCiv.Sim.Settle.SiteMap.CXc(c) - x, dz = AquariumCiv.Sim.Settle.SiteMap.CZc(c) - z;
                if (dx * dx + dz * dz <= 0.81f && Sim.EcoWetAt(c) > 0.05f) n++;
            }
            return n;
        }

        IEnumerator Hold(Vector2 at, float seconds, System.Func<bool> until = null)
        {
            int id = touchId++;
            BeginTouch(id, at, queueEventOnly: true);
            float t = Time.unscaledTime;
            while (Time.unscaledTime - t < seconds && (until == null || !until())) { MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
            EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(10);
        }

        [UnityTest, Timeout(2700000)]
        public IEnumerator FreshLivingBox_PlayedByTouch_ThenTheKit()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242, skipFirstRun: false);
                Assert.IsTrue(Sim.EcologyOn, "a barren box");
                var care = CareView.Instance;
                var hud = GameHud.Instance;
                yield return Until(() => Run.Step == FirstRunStep.Wipe, 8f);
                yield return Shot("phone_01_misted_glass");

                // ---- 1. Wipe, as a player does: whole-screen strokes, a few passes
                float spacing = care.wipeRadius * Screen.height * 1.1f;
                for (int pass = 0; pass < 6 && Run.Step == FirstRunStep.Wipe; pass++)
                {
                    foreach (var row in PlayerStrokes(care, spacing))
                    {
                        if (Run.Step != FirstRunStep.Wipe) break;
                        yield return Drag(row, 0.03f * Screen.height);
                    }
                    Debug.Log($"[phone] wipe pass {pass}: grime {Sim.State.grime:0.000} panes {care.DebugPaneGrime()}");
                    yield return Shot("phone_02_wipe_pass" + pass);
                }
                Assert.AreNotEqual(FirstRunStep.Wipe, Run.Step, $"the glass can be wiped clean by touch (grime {Sim.State.grime:0.000}, panes {care.DebugPaneGrime()})");
                yield return Frames(20);
                Assert.AreEqual(FirstRunStep.Rain, Run.Step, "a barren box: wipe, then the first mist");

                // ---- 2. One step at a time: only the mist hint, no plate, no name field
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("phone_03_mist_step");
                Assert.AreEqual("mist-hold", Run.Run.Hint.id);
                Assert.IsFalse(Shown(Run.Root.Q("firstrun-plate")), "the name plate waits for its step");

                var (rx, rz) = Run.Run.SuggestedRainSpot;
                yield return Hold(ScreenOf(rx, rz), 2.5f, () => Run.Step != FirstRunStep.Rain);
                yield return Frames(20);
                Assert.AreEqual(FirstRunStep.Name, Run.Step, "the held finger misted");
                yield return new WaitForSecondsRealtime(4.5f); // the reward line, then the name's hint
                yield return Shot("phone_04_name_step");
                Assert.IsTrue(Shown(Run.Root.Q("firstrun-plate")), "the plate");
                Assert.AreEqual("name", Run.Run.Hint.id);
                Assert.IsTrue(Run.HintShowing, "the Name it hint is on screen, pointing at the plate");
                Assert.AreNotEqual("mist-hold", Run.Run.Hint.id, "never the mist and the name at once");
                StringAssert.Contains("Tap to name", Run.Root.Q("firstrun-plate").Q<Label>().text, "the plate says what to do");

                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(Run.NameSheetOpen, "a tap on the plate opens the naming sheet");
                yield return Shot("phone_05_name_sheet");
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-name-keep"));
                yield return Frames(30);
                Assert.IsFalse(Run.Active, "named: the run is done");
                yield return new WaitForSecondsRealtime(2f); // the tools arrive
                yield return Shot("phone_06_after_first_run");
                AssertNoErrors("first run");

                // ---- 3. The kit: Kit → Mister, mist several places, Done
                Assert.IsTrue(hud.KitBox, "a living-land box shows the kit");
                Assert.IsFalse(Shown(hud.Root.Q("hud-shape")), "no Shape tray on a living land");
                var kitBtn = hud.Root.Q<Button>("hud-clean");
                Assert.AreEqual("Kit", kitBtn.text);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens the kit");
                yield return Shot("phone_07_kit");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-mister"));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(GameHud.KitTool.Mister, hud.Hand, "the mister in hand");
                Assert.AreEqual("Done", kitBtn.text);
                float bare0 = Sim.EcoBareShare;
                var spots = new[] { (-1.2f, 0.4f), (0.6f, 0.9f), (-0.2f, -1.2f), (1.4f, 0.2f) };
                // The mist shows a wet area and a little green at once; the rest greens over time (#266, #271). Per stroke: the wet area under
                // the finger is up, new green or shoots stand near it, and the bare share does not rise.
                foreach (var (x, z) in spots)
                {
                    int cell = AquariumCiv.Sim.Settle.SiteMap.CellAt(x, z);
                    float bareBefore = Sim.EcoBareShare;
                    int greenBefore = GreenNear(x, z), wetBefore = WetNear(x, z);
                    yield return Hold(ScreenOf(x, z), 1.2f);
                    float wetHere = 0f;
                    for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++) wetHere = Mathf.Max(wetHere, Sim.EcoWetAt(AquariumCiv.Sim.Settle.SiteMap.Index(AquariumCiv.Sim.Settle.SiteMap.I(cell) + di, AquariumCiv.Sim.Settle.SiteMap.J(cell) + dj)));
                    Assert.That(wetHere, Is.GreaterThan(0.2f), $"the wet area is up under the stroke at ({x:0.0}, {z:0.0})");
                    Assert.That(WetNear(x, z), Is.GreaterThan(wetBefore + 5), "and it reaches out round it");
                    Assert.That(GreenNear(x, z), Is.GreaterThan(greenBefore), $"a little green or shoots appeared near ({x:0.0}, {z:0.0})");
                    Assert.That(Sim.EcoBareShare, Is.LessThanOrEqualTo(bareBefore), "the bare share does not rise");
                }
                // and a drag mists along its path
                {
                    var a = ScreenOf(-2f, -0.6f); var b = ScreenOf(-0.4f, 0.6f);
                    yield return Drag(new[] { a, b }, 0.01f * Screen.height);
                }
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("phone_08_misted_many");
                var mist = EcoMistView.Instance;
                Debug.Log($"[phone] bare {bare0:0.000} -> {Sim.EcoBareShare:0.000}, pulses {mist.MistPulses}");
                Assert.That(Sim.EcoBareShare, Is.LessThan(bare0), "the mister greened ground over the kit session, not nothing");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return Frames(10);
                Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "Done puts it down");
                Assert.AreEqual("Kit", kitBtn.text);

                // ---- 4. The cloth from the kit
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.5f);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-cloth"));
                yield return Frames(10);
                Assert.IsTrue(hud.CleanMode, "the cloth is clean mode");
                yield return Drag(new[] { new Vector2(Screen.width * 0.2f, Screen.height * 0.55f), new Vector2(Screen.width * 0.8f, Screen.height * 0.5f) }, 0.02f * Screen.height);
                yield return Shot("phone_08b_cloth_in_hand");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return Frames(10);
                Assert.IsFalse(hud.CleanMode);

                // ---- 5-12. Only time passes (+1 season, an away) and the keeper's own touch: the Mister, held on the camp and a few spots.
                // No gate cheat, no action cheat. A beat that time does not reach within its budget is logged and counted, not faked.
                Assert.IsFalse(Sim.CampGateOpen, "the camp gate starts shut");
                // 6a. grass spreads on its own: three seasons with no mist at all
                int grass0 = GrassCells(); float yr0 = Sim.State.Year;
                for (int i = 0; i < 3; i++) { EcoDebug.SkipSeasons(1); yield return Frames(5); }
                int grass1 = GrassCells();
                yield return new WaitForSecondsRealtime(1f);
                Debug.Log($"[phone] grass on its own: {grass0} -> {grass1} cells over {Sim.State.Year - yr0:0.0} y");
                yield return Shot("phone_09_grass_spreads_alone");
                Assert.Greater(grass1, grass0, "grass spreads on its own, with no finger on the glass");
                // 6b. new grass after an away: leave the box as seen, time passes (an away of a few seasons), the fresher green shows
                var surf281 = EcoSurface.Instance;
                Sim.MarkLandSeen();
                EcoDebug.SkipSeasons(4);
                surf281.RestartFade();
                yield return Frames(30);
                Debug.Log($"[phone] away: new cells {Sim.LandNewCellCount}, shown {surf281.NewGrassCells}");
                yield return Shot("phone_10_new_grass_after_away");
                Assert.Greater(Sim.LandNewCellCount, 0, "grass grew while the box was left");
                Assert.Greater(surf281.NewGrassCells, 0, "new grass shows in the fresher green");

                // 7. P9 (#417-#419): nothing big arrives by itself. The thicket's jar opens, the Keeper places it; then the rabbits' jar
                // opens and the Keeper places the pair (their tracks come with them). Both by touch: Kit, the life slot, the glow, the ghost (#423)
                var rabbits = RabbitView.Instance;
                Assert.IsNotNull(rabbits, "the rabbit view");
                Assert.IsFalse(Sim.LifeIsPlaced(AquariumCiv.Sim.Eco.LifeSpecies.Thicket), "no thicket before the Keeper places it");
                yield return AdvanceUntil(hud, "the thicket's jar", () => Sim.JarReady(AquariumCiv.Sim.Eco.LifeSpecies.Thicket), 60);
                yield return PutDownMister(hud);
                yield return KeeperPlaces(AquariumCiv.Sim.Eco.LifeSpecies.Thicket, "phone_10b_life_thicket");
                if (!LastPlaced) Miss("the thicket placed");
                Assert.AreEqual(0, Sim.EcoRabbitCount, "no rabbit comes by itself");
                yield return AdvanceUntil(hud, "the rabbits' jar", () => Sim.JarReady(AquariumCiv.Sim.Eco.LifeSpecies.Rabbits), 60);
                yield return PutDownMister(hud);
                Assert.AreEqual(0, Sim.EcoRabbitCount, "a ready jar waits for the Keeper");
                yield return KeeperPlaces(AquariumCiv.Sim.Eco.LifeSpecies.Rabbits, "phone_10c_life_rabbits");
                if (!LastPlaced) Miss("the rabbits placed");
                yield return Until(() => rabbits.TracksShown, 5f);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("phone_11_rabbit_tracks");
                if (!rabbits.TracksShown) Miss("rabbit tracks with the placed pair");
                yield return AdvanceUntil(hud, "rabbits", () => Sim.EcoRabbitCount > 0 && rabbits.Shown > 0, 8);
                yield return new WaitForSecondsRealtime(1.5f);
                Debug.Log($"[phone] rabbits: sim {Sim.EcoRabbitCount}, shown {rabbits.Shown}, year {Sim.State.Year:0.0}");
                if (Sim.EcoRabbitCount > 0) Assert.AreEqual(Sim.EcoRabbitsShown(), rabbits.Shown, "the box shows the rabbits the sim counts");
                yield return Shot("phone_12_rabbits");

                // 8. The hunt: only time passes. The camp gate opens by the meadow and the rabbits; hunters follow them and make a catch.
                yield return AdvanceUntil(hud, "camp gate", () => Sim.CampGateOpen, 120);
                yield return PutDownMister(hud);
                var pvh = PeopleView.Find();
                int hides0 = Sim.HidesTotal, rabbits0 = Sim.EcoRabbitCount;
                bool sawStalk = false, sawSpear = false, shotStalk = false;
                for (int attempt = 0; attempt < 12 && Sim.HidesTotal <= hides0; attempt++)
                {
                    if (attempt > 0) { EcoDebug.SkipSeasons(1); }
                    PeopleView.DebugSetHour(6.5f);
                    yield return Frames(10);
                    pvh.timeScale = 12f;
                    float tH = Time.realtimeSinceStartup;
                    while (Sim.HidesTotal <= hides0 && pvh.ViewHour < 20f && Time.realtimeSinceStartup - tH < 90f)
                    {
                        foreach (var a in pvh.Activities)
                        {
                            if (a.hunt == AquariumCiv.Life.HuntPhase.Stalk) sawStalk = true;
                            if (a.hunt == AquariumCiv.Life.HuntPhase.WalkOut && a.prop == AquariumCiv.Life.Prop.Spear) sawSpear = true;
                        }
                        if (sawStalk && !shotStalk) { shotStalk = true; pvh.timeScale = 1f; yield return Frames(15); yield return Shot("phone_13_hunter_stalks"); pvh.timeScale = 12f; }
                        if (CampView.Instance != null && CampView.Instance.RaisingsActive > 0 && !campWatched) { yield return WatchCampMoment(pvh); pvh.timeScale = 12f; }
                        yield return null;
                    }
                }
                pvh.timeScale = 1f;
                Debug.Log($"[phone] hunt: year {Sim.State.Year:0.0}, hides {hides0} -> {Sim.HidesTotal}, rabbits {rabbits0} -> {Sim.EcoRabbitCount}, stalk {sawStalk}, spear {sawSpear}");
                if (Sim.HidesTotal <= hides0) Miss("hunt (a catch and a hide)");
                else if (!(sawStalk && sawSpear)) Miss("hunt seen (stalk/spear)");
                AssertNoErrors("hunt");

                // 9. The camp begins: the first tent from the hides, as a small moment (time passing only). It plays whenever the sim
                // raises it (during the hunts above or the seasons below); WatchCampMoment follows it with the camera and shoots it.
                var cv = CampView.Instance;
                Assert.IsNotNull(cv, "the camp view");
                yield return Frames(15);
                if (Sim.HidesTotal > 0 && !campWatched)
                {
                    Assert.IsTrue(cv.FrameShown, "the hide frame stands by the fire once there are hides");
                    yield return FlyToFrame(pvh);
                    yield return Shot("phone_14_hide_frame_by_fire");
                }
                for (int attempt = 0; attempt < 16 && !Sim.FirstTentRaised; attempt++)
                {
                    if (Sim.HidesDrying < SimCore.HidesPerTent) yield return HuntADay(pvh);
                    EcoDebug.SkipSeasons(1);
                    yield return Frames(10);
                    yield return WatchCampMoment(pvh);
                }
                yield return WatchCampMoment(pvh);
                Debug.Log($"[phone] camp: year {Sim.State.Year:0.0}, first tent {Sim.FirstTentRaised}, hides {Sim.HidesTotal}, moment watched {campWatched}, moments {cv.MomentsPlayed}, line '{cv.LastLine}'");
                if (!Sim.FirstTentRaised) Miss("camp's first tent");
                else
                {
                    Assert.GreaterOrEqual(cv.RaisingsStarted, 1, "the view began the raising");
                    Assert.IsNotNull(cv.LastLine, "a builder said the camp's line");
                    yield return FlyToFrame(pvh);
                    yield return Shot("phone_17_tent_up");
                }
                AssertNoErrors("camp");

                // 10. Berries: bushes the land grew; in summer gatherers walk to a real bush, reach and pick
                var bushes = BushView.Instance;
                Assert.IsNotNull(bushes, "the bush view");
                var bcells = new List<int>();
                yield return AdvanceUntil(hud, "a real bush", () => { bcells.Clear(); Sim.BushCells(bcells); return bcells.Count > 0; }, 100);
                yield return PutDownMister(hud);
                Debug.Log($"[phone] bushes after {Sim.State.Year:0.0} years: {bcells.Count}");
                if (bcells.Count > 0)
                {
                    for (int i = 0; i < 8 && AquariumCiv.Life.LifeClock.SeasonOf(Sim.State.month) != AquariumCiv.Life.Season.Summer; i++) EcoDebug.SkipSeasons(1);
                    PeopleView.DebugSetHour(8.5f);
                    yield return Frames(40);
                    yield return Until(() => bushes.DotsShown > 0, 3f);
                    yield return Shot("phone_18_berries_summer");
                    int picks10 = bushes.PicksDone, pickCell10 = -1;
                    bool carried10 = false, shot10 = false;
                    pvh.timeScale = 6f;
                    float t10 = Time.realtimeSinceStartup;
                    while ((bushes.PicksDone <= picks10 || !carried10) && Time.realtimeSinceStartup - t10 < 150f)
                    {
                        if (pvh.ViewHour > 17f) PeopleView.DebugSetHour(8.5f);
                        foreach (var a in pvh.Activities)
                        {
                            if (!a.moving && a.kind == AquariumCiv.Life.ActivityKind.Gather && a.place.kind == AquariumCiv.Life.PlaceKind.BushCell && pickCell10 < 0)
                                pickCell10 = a.place.index;
                            if (bushes.PicksDone > picks10 && a.moving && a.prop == AquariumCiv.Life.Prop.Basket) carried10 = true;
                        }
                        if (pickCell10 >= 0 && !shot10) { shot10 = true; pvh.timeScale = 1f; yield return Frames(20); yield return Shot("phone_19_picker_at_bush"); pvh.timeScale = 6f; }
                        yield return null;
                    }
                    pvh.timeScale = 1f;
                    Debug.Log($"[phone] berries: pick cell {pickCell10}, picks {bushes.PicksDone - picks10}, carried {carried10}");
                    if (pickCell10 < 0 || bushes.PicksDone <= picks10 || !carried10) Miss("berry picking at a real bush");
                }
                else Miss("bushes");
                AssertNoErrors("berries");

                // 11. The pond's first water meets the glass, with its moment, then the frogs: time and the Mister only
                var fw11 = FirstWaterView.Instance; var pond11 = PondLife.Instance;
                Assert.IsNotNull(fw11); Assert.IsNotNull(pond11);
                yield return AdvanceUntil(hud, "the pond's first water", () => Sim.EcoPondCells > 0, 110);
                yield return PutDownMister(hud);
                Debug.Log($"[phone] pond: {Sim.EcoPondCells} cells at year {Sim.State.Year:0.0}, full plays {fw11.FullPlays}");
                if (Sim.EcoPondCells > 0)
                {
                    PeopleView.DebugSetHour(10f);
                    yield return Frames(30);
                    yield return Until(() => fw11.FullPlays > 0 && fw11.SparklesShown > 0, 10f);
                    yield return Frames(60);
                    yield return Shot("phone_20_pond_first_water");
                    yield return Until(() => !fw11.Playing, 40f);
                    PeopleView.DebugSetHour(20.5f);
                    yield return Frames(30);
                    yield return Until(() => pond11.FrogsSitting > 0, 30f);
                    if (pond11.FrogsSitting == 0) Miss("frogs at the pond");
                    yield return Shot("phone_21_frogs_dusk");
                }
                AssertNoErrors("pond");

                // 12. History shows the land growing: opened by touch, scrubbed by touch on the bar; the year tag follows the finger
                var boxH = Object.FindAnyObjectByType<BoxView>();
                yield return PutDownMister(hud);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
                var tlH = TimeLapseView.Find();
                yield return Until(() => tlH != null && tlH.Active, 3f);
                Assert.IsTrue(tlH.Active, "History opened by touch");
                yield return new WaitForSecondsRealtime(1f);
                var screenH = TimeLapseScreenView.Instance;
                var yearTag = screenH.Root.Q<Label>("tl-year");
                var barEl = screenH.Root.Q("tl-bar");
                float uiScale = Screen.width / barEl.panel.visualTree.worldBound.width;
                Vector2 AlongBar(float f) { var r = barEl.worldBound; return new Vector2((r.xMin + 4f + (r.width - 8f) * f) * uiScale, Screen.height - r.center.y * uiScale); }
                yield return Drag(new[] { AlongBar(0.5f), AlongBar(0.02f) }, 0.01f * Screen.width);
                yield return new WaitForSecondsRealtime(1.2f);
                string tag0 = yearTag.text; float gH0 = tlH.DrawnGreenShare;
                var cover0 = (byte[])boxH.layout.eco.cover.Clone();
                yield return Shot("phone_22_history_start");
                yield return Drag(new[] { AlongBar(0.02f), AlongBar(0.5f) }, 0.01f * Screen.width);
                yield return new WaitForSecondsRealtime(1.2f);
                string tagMid = yearTag.text; float gHm = tlH.DrawnGreenShare;
                yield return Shot("phone_23_history_scrubbed_mid");
                yield return Drag(new[] { AlongBar(0.5f), AlongBar(0.98f) }, 0.01f * Screen.width);
                yield return new WaitForSecondsRealtime(1.2f);
                string tagEnd = yearTag.text; float gHn = tlH.DrawnGreenShare;
                Debug.Log($"[phone] history scrub: '{tag0}' green {gH0:0.000} -> '{tagMid}' {gHm:0.000} -> '{tagEnd}' {gHn:0.000}");
                yield return Shot("phone_24_history_now");
                Assert.AreNotEqual(tag0, tagMid, "the caption's year follows the scrub (start vs middle)");
                Assert.AreNotEqual(tagMid, tagEnd, "the caption's year follows the scrub (middle vs end)");
                Assert.Greater(gHn, gH0 + 0.01f, "History: the land is greener now than at the start");
                CollectionAssert.AreNotEqual(cover0, boxH.layout.eco.cover, "History: the land differs between the start and now");
                tlH.Close(); yield return Frames(6);
                AssertNoErrors("history");
                Debug.Log("[phone] NOT REACHED by time: " + (missed.Count == 0 ? "(none)" : string.Join("; ", missed)));
                Assert.IsEmpty(missed, "beats time alone did not reach: " + string.Join("; ", missed));
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        bool campWatched;
        /// <summary>The first tent's moment: camera to the lot, time at 6x until it ends; shots of the tent rising, the builder's line and the tent up.</summary>
        IEnumerator WatchCampMoment(PeopleView pvh)
        {
            var cv = CampView.Instance;
            if (campWatched || cv == null || cv.RaisingsActive == 0) yield break;
            campWatched = true;
            yield return FlyToFrame(pvh);
            pvh.timeScale = 6f;
            var tent = Sim.State.buildings[Sim.State.buildings.Count - 1];
            float maxGrow = 0f; bool shotRising = false, shotLine = false;
            float t0 = Time.realtimeSinceStartup;
            while (cv.RaisingsActive > 0 && Time.realtimeSinceStartup - t0 < 90f)
            {
                float g = pvh.Planner.TentRaised(tent.id, pvh.Now);
                maxGrow = Mathf.Max(maxGrow, g);
                if (!shotRising && g > 0.3f) { shotRising = true; yield return Shot("phone_15_tent_rising"); }
                if (cv.LastLine != null && !shotLine) { shotLine = true; yield return Shot("phone_16_camp_line"); }
                yield return null;
            }
            pvh.timeScale = 1f;
            Debug.Log($"[phone] camp moment: grow {maxGrow:0.00}, line '{cv.LastLine}', leans {cv.Leans}, moments {cv.MomentsPlayed}, year {Sim.State.Year:0.0}");
            if (maxGrow <= 0.5f) Miss("tent grew in view");
        }

        readonly List<string> missed = new List<string>();
        void Miss(string beat)
        {
            string s = $"{beat} (not reached by time, year {Sim.State.Year:0.0})";
            missed.Add(s);
            Debug.LogWarning("[phone] MISSED " + s);
        }

        /// <summary>Cells that are grass, tall grass or bush (what spreading means).</summary>
        int GrassCells()
        {
            int n = 0;
            for (int c = 0; c < AquariumCiv.Sim.Eco.EcoLand.N; c++)
            {
                var cv = Sim.EcoCoverAt(c);
                if (cv == AquariumCiv.Sim.Eco.EcoCover.Grass || cv == AquariumCiv.Sim.Eco.EcoCover.Tall || cv == AquariumCiv.Sim.Eco.EcoCover.Bush) n++;
            }
            return n;
        }

        IEnumerator PutDownMister(GameHud hud)
        {
            if (hud.Hand == GameHud.KitTool.None) yield break;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return Frames(10);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "Done puts the mister down");
        }

        /// <summary>Time passes the way a caring keeper lets it: Kit, the Mister in hand, then each season +1 season and a held mist on the camp
        /// and one more spot. Stops when the condition holds or the budget of seasons is spent (then the beat is counted as missed).</summary>
        IEnumerator AdvanceUntil(GameHud hud, string what, System.Func<bool> cond, int maxSeasons)
        {
            if (cond()) yield break;
            if (hud.Hand != GameHud.KitTool.Mister)
            {
                if (hud.Hand != GameHud.KitTool.None) yield return PutDownMister(hud);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-mister"));
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.AreEqual(GameHud.KitTool.Mister, hud.Hand, "the mister in hand");
            }
            var map = Sim.Settlement.Map;
            var spots = new[] { (map.greenX + 0.5f, map.greenZ), (-1.2f, 0.4f), (1.4f, 0.2f), (-0.2f, -1.2f), (0.6f, 0.9f) };
            float y0 = Sim.State.Year;
            int n = 0;
            for (; n < maxSeasons && !cond(); n++)
            {
                EcoDebug.SkipSeasons(1);
                var (x, z) = spots[n % spots.Length];
                yield return Hold(ScreenOf(x, z), 0.8f);
                if (CampView.Instance != null && CampView.Instance.RaisingsActive > 0 && !campWatched) yield return WatchCampMoment(PeopleView.Find());
                if (n % 8 == 7) AssertNoErrors("advance " + what);
            }
            yield return Frames(10);
            Debug.Log($"[phone] advance for '{what}': {(cond() ? "reached" : "NOT reached")} after {n} seasons, year {y0:0.0} -> {Sim.State.Year:0.0}");
            if (!cond()) Miss(what);
        }


        /// <summary>One view day of hunting (time runs at 12x from dawn) until a catch is made or the day ends.</summary>
        IEnumerator HuntADay(PeopleView pvh)
        {
            int was = Sim.HidesTotal;
            PeopleView.DebugSetHour(6.5f);
            yield return Frames(10);
            pvh.timeScale = 12f;
            float tH = Time.realtimeSinceStartup;
            while (Sim.HidesTotal <= was && pvh.ViewHour < 20f && Time.realtimeSinceStartup - tH < 90f) yield return null;
            pvh.timeScale = 1f;
        }

        /// <summary>Bring the camera to the hide frame (the player would pan there) for the screenshots.</summary>
        IEnumerator FlyToFrame(PeopleView pv)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            var cam = Camera.main.GetComponent<BoxCamera>();
            if (pv.Planner.HideFrame(out float x, out float z, out _, out _))
            {
                cam.FlyTo(box.transform.TransformPoint(box.land.OnGround(x, z)), 1.8f, 0.3f);
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        static bool Shown(VisualElement e)
        {
            if (e == null) return false;
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }
    }
}
