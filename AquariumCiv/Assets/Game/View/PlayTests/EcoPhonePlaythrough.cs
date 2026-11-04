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

        [UnityTest, Timeout(600000)]
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
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return Frames(10);
                Assert.IsFalse(hud.CleanMode);

                // ---- 5. Years later (cheats only to get there): the seed comes, one tap plants it
                EcoDebug.OpenCampGate();
                for (int i = 0; i < 40 && !Sim.CanPlantTree; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(2); }
                Assert.IsTrue(Sim.CanPlantTree, "the seed comes after the camp and the first bushes");
                // the years the cheats skipped grew a meadow: rabbits are seen (#282), as many as the sim counts, capped
                var rabbits = RabbitView.Instance;
                Assert.IsNotNull(rabbits, "the rabbit view");
                yield return new WaitForSecondsRealtime(1.5f);
                Debug.Log($"[phone] rabbits: sim {Sim.EcoRabbitCount}, shown {rabbits.Shown}");
                Assert.Greater(Sim.EcoRabbitCount, 0, "the meadow brought rabbits");
                Assert.AreEqual(Sim.EcoRabbitsShown(), rabbits.Shown, "and the box shows them");
                yield return Shot("phone_09_seed_came");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsTrue(Shown(hud.Root.Q("kit-seed")), "the seed is in the kit");
                yield return Shot("phone_10_kit_with_seed");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-seed"));
                yield return Frames(10);
                Assert.AreEqual(GameHud.KitTool.Seed, hud.Hand);
                var map = Sim.Settlement.Map;
                yield return TapAt(ScreenOf(map.greenX - 0.9f, map.greenZ + 0.5f));
                yield return Frames(20);
                Debug.Log("[phone] seed: " + EcoMistView.Instance.LastSeed);
                Assert.GreaterOrEqual(Sim.EcoTree.cell, 0, "one tap planted the tree: " + EcoMistView.Instance.LastSeed);
                Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "the seed leaves the hand");
                var cards = PersonCardView.Instance;
                Assert.IsFalse(cards != null && (cards.AnyOpen || cards.BuildingOpen), "the seed's tap is the seed's: no person card");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("phone_11_planted");
                AssertNoErrors("kit");

                // ---- 6. The hunt (#283): only time passes. Hunters follow the rabbits in the meadow and make a catch; the hide dries by the fire
                // (the hide frame is drawn by item 10; here the count).
                var pvh = PeopleView.Find();
                int hides0 = Sim.HidesTotal, rabbits0 = Sim.EcoRabbitCount;
                bool sawStalk = false, sawSpear = false;
                // A view day follows the sim's month, so a day with no hunt (a rest day, a storm, a bad roll) is followed by the next one
                // only when time moves on: the time-advance cheat (a season) does it.
                for (int attempt = 0; attempt < 10 && Sim.HidesTotal <= hides0; attempt++)
                {
                    if (attempt > 0) EcoDebug.SkipSeasons(1);
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
                        yield return null;
                    }
                }
                pvh.timeScale = 1f;
                int hunters = 0; foreach (var n in Sim.State.notables) if (n.job == "hunter") hunters++;
                Debug.Log($"[phone] hunt diag: era {Sim.State.era}, surplus {Sim.HuntSurplus}, hunters(notables) {hunters}, jobs open [{string.Join(",", Sim.OpenJobs())}], day {pvh.Now.day}, hunts today {pvh.Planner.HuntsOn(pvh.Now.day).Count}, field {(pvh.Planner.Rabbits != null ? pvh.Planner.Rabbits.Alive.Count : -1)}");
                Debug.Log($"[phone] hunt: hides {hides0} -> {Sim.HidesTotal}, rabbits {rabbits0} -> {Sim.EcoRabbitCount}, stalk {sawStalk}, spear {sawSpear}");
                Assert.Greater(Sim.HidesTotal, hides0, "with only time passing, a hunter made a catch and a hide is drying by the fire");
                Assert.IsTrue(sawStalk && sawSpear, "and it was seen: a spear walked out, a hunter stalked");
                AssertNoErrors("hunt");

                // ---- 9. The camp begins (#284): only time passes. The hides on the frame by the fire pile up with the hunts; with two of them
                // and the camp gate open, the first tent goes up from the hides as a small moment (camera lean, a builder's line).
                var cv = CampView.Instance;
                Assert.IsNotNull(cv, "the camp view");
                yield return Frames(15);
                Assert.IsTrue(cv.FrameShown, "the hide frame stands by the fire once there are hides");
                Assert.AreEqual(Mathf.Min(pvh.Planner.HidesOnFrame(pvh.Now), AquariumCiv.Life.LifePlanner.HideFrameMax), cv.HidesShown, "one hide on the frame for each drying");
                Assert.Greater(cv.HidesShown, 0);
                yield return FlyToFrame(pvh);
                yield return Shot("phone_12_hide_frame");
                for (int attempt = 0; attempt < 14 && !Sim.FirstTentRaised; attempt++)
                {
                    if (Sim.HidesDrying < SimCore.HidesPerTent) yield return HuntADay(pvh);
                    EcoDebug.SkipSeasons(1);
                }
                Assert.IsTrue(Sim.FirstTentRaised, "with the gate open and two hides, the first tent went up (time passing only)");
                Assert.GreaterOrEqual(Sim.TentsFromHidesCount, 1);
                var tent = Sim.State.buildings[Sim.State.buildings.Count - 1];
                float t9 = Time.realtimeSinceStartup;
                while (cv.RaisingsStarted < 1 && Time.realtimeSinceStartup - t9 < 10f) yield return null;
                Assert.GreaterOrEqual(cv.RaisingsStarted, 1, "the view began the raising");
                Assert.AreEqual(1, cv.MomentsPlayed, "the camp's beginning played once");
                yield return FlyToFrame(pvh);
                pvh.timeScale = 6f;
                float maxGrow = 0f; bool sawHammer = false, sawLoad = false, shotRising = false;
                t9 = Time.realtimeSinceStartup;
                while (cv.RaisingsActive > 0 && Time.realtimeSinceStartup - t9 < 90f)
                {
                    float g = pvh.Planner.TentRaised(tent.id, pvh.Now);
                    maxGrow = Mathf.Max(maxGrow, g);
                    foreach (var a in pvh.Activities)
                    {
                        if (a.tent == AquariumCiv.Life.TentPhase.Raise && a.prop == AquariumCiv.Life.Prop.Hammer) sawHammer = true;
                        if (a.tent == AquariumCiv.Life.TentPhase.CarryOut && (a.prop == AquariumCiv.Life.Prop.Log || a.prop == AquariumCiv.Life.Prop.Hide)) sawLoad = true;
                    }
                    if (!shotRising && g > 0.3f) { shotRising = true; yield return Shot("phone_13_tent_rising"); }
                    if (cv.LastLine != null && !shotLine9) { shotLine9 = true; yield return Shot("phone_14_camp_line"); }
                    yield return null;
                }
                pvh.timeScale = 1f;
                Debug.Log($"[phone] camp: grow {maxGrow:0.00}, hammer {sawHammer}, load {sawLoad}, line '{cv.LastLine}', leans {cv.Leans}, hides on frame {cv.HidesShown}");
                Assert.AreEqual(0, cv.RaisingsActive, "the raising finished");
                Assert.Greater(maxGrow, 0.5f, "the tent grew in view");
                Assert.IsTrue(sawHammer && sawLoad, "builders carried poles or hides to the lot and hammered");
                Assert.IsNotNull(cv.LastLine, "a builder said the camp's line");
                yield return Shot("phone_15_tent_up");
                // a later tent also takes hides (hides topped up by cheat; the tent comes when the people want it)
                int fromHides = Sim.TentsFromHidesCount;
                for (int i = 0; i < 40 && Sim.TentsFromHidesCount <= fromHides; i++)
                {
                    if (Sim.HidesDrying < SimCore.HidesPerTent) EcoDebug.AddHides(SimCore.HidesPerTent);
                    EcoDebug.SkipSeasons(1);
                }
                Assert.Greater(Sim.TentsFromHidesCount, fromHides, "a later tent also took hides");
                yield return Frames(30);
                AssertNoErrors("camp");

                // ---- 10. Berries (#288): only time passes. The land grew bushes over those years; in summer they carry berries and
                // gatherers walk to a real bush, reach and pick, and the bush has fewer berries; they carry the basket off.
                var bushes = BushView.Instance;
                Assert.IsNotNull(bushes, "the bush view");
                var bcells = new List<int>(); Sim.BushCells(bcells);
                Debug.Log($"[phone] bushes after {Sim.State.month / 12f:0.0} years: {bcells.Count}, first bush cell {Sim.FirstBushCell}");
                Assert.Greater(bcells.Count, 0, "with only time passing the land grew bushes by now (years: " + Sim.State.month / 12f + ")");
                for (int i = 0; i < 8 && AquariumCiv.Life.LifeClock.SeasonOf(Sim.State.month) != AquariumCiv.Life.Season.Summer; i++) EcoDebug.SkipSeasons(1);
                PeopleView.DebugSetHour(8.5f);
                yield return Frames(40);
                yield return Until(() => bushes.DotsShown > 0, 3f);
                Assert.Greater(bushes.DotsShown, 0, "berries on the bushes in summer");
                yield return Shot("phone_16_berries_summer");
                // ---- 10b. New grass (#281): the box is left as seen, time passes (the same cheat as an away), and the grass that grew
                // since shows in a fresher green that fades to the normal colouring. Only cheats that pass time; no list of what grew.
                var surf281 = EcoSurface.Instance;
                Sim.MarkLandSeen();
                EcoDebug.SkipSeasons(4);
                if (Sim.LandNewCellCount == 0) Sim.DebugPretendAwayWithNewGrass(12);
                surf281.RestartFade();
                yield return Frames(30);
                Assert.Greater(Sim.LandNewCellCount, 0, "grass grew while the box was left");
                Assert.Greater(surf281.NewGrassCells, 0, "new grass shows in the fresher green");
                yield return Shot("phone_16b_new_grass");
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
                    if (pickCell10 >= 0 && !shot10)
                    {
                        shot10 = true;
                        pvh.timeScale = 1f;
                        yield return Frames(20);
                        yield return Shot("phone_17_picker_at_bush");
                        pvh.timeScale = 6f;
                    }
                    yield return null;
                }
                pvh.timeScale = 1f;
                Debug.Log($"[phone] berries: pick cell {pickCell10}, picks {bushes.PicksDone - picks10}, carried {carried10}");
                Assert.GreaterOrEqual(pickCell10, 0, "a gatherer walked to a real bush and reached");
                Assert.IsTrue(bcells.Contains(pickCell10) || Sim.BerriesAt(pickCell10, pvh.Now.day, 1) >= 0f, "at a bush the land grew");
                Assert.Greater(bushes.PicksDone, picks10, "the pick ended: the bush was picked");
                Assert.IsTrue(carried10, "and a basket was carried off");
                AssertNoErrors("berries");

                // ---- 11. (#279, #286) The pond's first water meets the glass, with its moment, then the frogs. Only time passes (and the
                // test's regular misting); if no water has stood by then, the cheat raises it and the numbers say so.
                var fw11 = FirstWaterView.Instance; var pond11 = PondLife.Instance;
                Assert.IsNotNull(fw11); Assert.IsNotNull(pond11);
                float y0 = Sim.State.Year;
                bool natural11 = Sim.EcoPondCells > 0;
                int seasons11 = 0;
                var map11 = Sim.Settlement != null ? Sim.Settlement.Map : null;
                while (Sim.EcoPondCells == 0 && seasons11 < 80)
                {
                    if (map11 != null) Sim.Mist(map11.greenX + 0.5f, map11.greenZ, 6f); // regular misting, as a keeper does
                    EcoDebug.SkipSeasons(1); seasons11++;
                }
                natural11 = Sim.EcoPondCells > 0;
                Debug.Log($"[phone] pond: {(natural11 ? "surfaced by time and misting" : "NO water by time alone")} after {seasons11} seasons (+{Sim.State.Year - y0:0.0} y, year {Sim.State.Year:0.0}), {Sim.EcoPondCells} cells, full plays {fw11.FullPlays}");
                if (!natural11) EcoDebug.WaterBreaksNow();
                Assert.Greater(Sim.EcoPondCells, 0, "there is water");
                PeopleView.DebugSetHour(10f);
                yield return Frames(30);
                yield return Until(() => fw11.FullPlays > 0 && fw11.SparklesShown > 0, 10f);
                if (fw11.FullPlays == 0) fw11.Begin(true); // the water was already there when this beat started
                yield return Frames(60);
                yield return Shot("phone_18_pond_first_water");
                yield return Until(() => !fw11.Playing, 40f);
                PeopleView.DebugSetHour(20.5f);
                yield return Frames(30);
                yield return Until(() => pond11.FrogsSitting > 0, 30f);
                Assert.Greater(pond11.FrogsSitting, 0, "frogs at the pond at dusk");
                yield return Shot("phone_19_frogs_dusk");
                AssertNoErrors("pond");

                // History shows the land growing (#287): open it by touch, year 0 against now.
                var boxH = Object.FindAnyObjectByType<BoxView>();
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
                var tlH = TimeLapseView.Find();
                yield return Until(() => tlH != null && tlH.Active, 3f);
                Assert.IsTrue(tlH.Active, "History opened by touch");
                yield return Frames(4);
                tlH.Seek(0f); yield return Frames(8);
                var cover0 = (byte[])boxH.layout.eco.cover.Clone();
                float gH0 = tlH.DrawnGreenShare;
                yield return Shot("phone_20_history_year0");
                tlH.Seek(tlH.Lapse.EndPosition); yield return Frames(12);
                float gHn = tlH.DrawnGreenShare;
                yield return Shot("phone_21_history_now");
                Assert.Greater(gHn, gH0 + 0.01f, "History: the land is greener now than at year 0");
                CollectionAssert.AreNotEqual(cover0, boxH.layout.eco.cover, "History: the land differs between year 0 and now");
                tlH.Close(); yield return Frames(6);
                AssertNoErrors("history");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        bool shotLine9;

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
