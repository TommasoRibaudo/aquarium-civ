using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Playtest 1, batch 2 played the way a phone plays it (1179x2556, a fresh box per beat, real touches for every player action; cheats only reach a
    /// state and are logged as "[pt1b] STATE"). One test per beat, each opens its own box. Beats: 1 a camp night and a camp day with replans (#696/#697);
    /// 2 deer round the fire (#699); 3 the duck jar in winter (#703); 4 a path over the fish rack (#700); 5 the rabbit chase ends (#698); 6 History
    /// scrubbed back by touch (#669); 7 the Feedback pill over every panel (#702); 8 the HUD top by day and night (#707); 9 the fire on dry ground, grass
    /// and snow (#704) and the pond at the glass (#701); 10 the stews (#676); 11 a touch choice with its options marked (#706); 12 a house card's year
    /// (#689), the deer jar's need line (#690), Skip all clears the History dot (#561). Shots: Temp/agent/pt1b-play/*.png, notes: results.txt. Explicit.
    /// </summary>
    [Explicit("Watched play-through, about 40 minutes: run by hand"), Category("Playtest1B")]
    public class Playtest1BPlaythrough : PlayTestBase
    {
        const string Dir = "pt1b-play/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static GameHud Hud => GameHud.Instance;
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();
        static PeopleView Pv => PeopleView.Find();

        static void Log(string s) => Debug.Log("[pt1b] " + s);
        static void Note(string beat, string s)
        {
            Debug.Log($"[pt1b] {beat}: {s}");
            Directory.CreateDirectory(Shots + Dir);
            File.AppendAllText(Shots + Dir + "results.txt", $"{beat}: {s}\n");
        }
        static void Cheat(string what, string r) => Debug.LogWarning($"[pt1b] STATE CHEAT {what}: {r}");

        IEnumerator Snap(string name) { Directory.CreateDirectory(Shots + Dir); yield return Shot(Dir + name); }

        // ---------------------------------------------------------------- shared helpers
        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return e == null;
        }

        static Vector3 World(float x, float z) => Box.transform.TransformPoint(Box.land.OnGround(x, z));

        static void Look(Vector3 world, float zoom, float pitch = 52f, float yaw = float.NaN)
        {
            Rig.CancelFlight(); Rig.minZoom = 0.02f;
            if (!float.IsNaN(yaw)) Rig.yaw = yaw;
            Rig.target = world; Rig.zoom = zoom; Rig.pitch = pitch; Rig.Apply();
        }

        static void DefaultView() { Rig.CancelFlight(); Rig.target = Box.transform.position; Rig.zoom = 1f; Rig.Apply(); }

        static IEnumerator Settle(float s) { yield return new WaitForSecondsRealtime(s); }

        static IEnumerator Calm()
        {
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 8f);
            for (int calm = 0, f = 0; calm < 90 && f < 1500; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
        }

        IEnumerator CalmHud()
        {
            var hud = Hud;
            yield return Until(() => hud.IsVisible && !(PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)), 30f);
            if (hud.SheetOpen) { hud.CloseSheet(); yield return Frames(10); }
            if (hud.Hand != GameHud.KitTool.None || hud.CleanMode) { yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean")); yield return Frames(10); }
            yield return Until(() => !Rig.Leaning && !Rig.Gliding && !Rig.GlideArmed && !Rig.IsFlying, 30f);
        }

        /// <summary>A fresh living-land box (seed 4242, first run skipped), the meadow misted in, a pond, in the camp era with a log fire and its stock set down.</summary>
        IEnumerator LivedInCamp(float hour = 10f, bool pond = true, bool wood = true, int seed = 4242)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: seed);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(hour);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            Cheat("meadow, camp gate", EcoDebug.OpenCampGate());
            for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            if (pond) Cheat("pond", EcoDebug.Table(0.3f));
            if (wood) Sim.DebugGrowFarWood();
            Sim.MarkFirstWildWoodSeen();
            if (!Sim.StockSetDown) Sim.SetDownStock();
            Sim.DebugFireStage(2);
            Box.Rebuild();
            yield return Frames(20);
            yield return Calm();
            PeopleView.DebugSetHour(hour);
            yield return Settle(1f);
        }

        // ================================================================ 1a. a camp night (#696): the last sleeper walks in, nobody comes out
        IEnumerator Grown(int seed = 1234)
        {
            UseLivingLand();
            yield return OpenSlice(seed: seed);
            SimLayoutSource.DebugSkipYears(16f);
            yield return null;
            Sim.CleanAll();
            Pv?.Planner?.ClearLive(); foreach (var bd in Sim.State.buildings) Pv.Planner.ForgetTent(bd.id);
            yield return Frames(3);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat01a_CampNight_TheLastSleeperWalksInAndNobodyComesOut()
        {
            yield return Grown();
            var pv = Pv;
            var lines = new List<string>();
            pv.Planner.ReplanLog = l => lines.Add($"{pv.Now} {l}");
            pv.Planner.ClearLive(); foreach (var bd in Sim.State.buildings) pv.Planner.ForgetTent(bd.id);
            ShowMeDirector.BubblesOn = false;
            float first = 99f, lastBed = 0f; var segs = new List<PersonActivity>();
            foreach (var w in pv.Planner.People) { segs.Clear(); pv.Planner.DaySchedule(w, pv.Now.day, segs); foreach (var sg in segs) if (sg.kind == ActivityKind.Sleep && sg.start > 12f) { first = Mathf.Min(first, sg.start); lastBed = Mathf.Max(lastBed, sg.start); } }
            if (first > 90f) { first = 20f; lastBed = 21f; }
            float startHour = first - 0.12f;
            Note("1a night", $"bedtimes run from {first:0.00} h to {lastBed:0.00} h; the watch starts at {startHour:0.00} h");
            PeopleView.DebugSetHour(startHour);
            pv.timeScale = 3f;
            yield return Frames(4);
            Look(Pv.Planner.FirePoint(out float fx, out float fz) ? World(fx, fz) : Box.transform.position, 0.7f, 50f);
            yield return Snap("01a_night_start");
            var asleepSince = new HashSet<int>(); var hiddenOnce = new HashSet<int>(); var shown = new Dictionary<int, List<string>>();
            var trig = LifePlanner.ReplanTriggers.Where(t => t != "month").ToArray();
            int next = 0, shots = 0, frames = 0, visibleFrames = 0, walkedIn = 0;
            var wasWalking = new HashSet<int>();
            float t0 = Time.realtimeSinceStartup, nextForce = t0 + 4f;
            string fail = null;
            while (pv.Now.hour >= startHour - 0.05f && pv.Now.hour < lastBed + 0.4f && Time.realtimeSinceStartup - t0 < 260f)
            {
                if (Time.realtimeSinceStartup >= nextForce)
                {
                    nextForce += 2.5f;
                    if (shots == 0 && asleepSince.Count > 3) { yield return Snap("01a_night_before_replan"); shots++; }
                    PeopleView.DebugReplan(trig[next++ % trig.Length]);
                    yield return null;
                    if (shots == 1) { yield return Snap("01a_night_after_replan"); shots++; }
                }
                yield return null;
                frames++;
                foreach (var a in pv.Activities)
                {
                    int k = a.who.Key;
                    bool asleep = a.kind == ActivityKind.Sleep && !a.moving;
                    if (a.kind == ActivityKind.Sleep && a.moving) wasWalking.Add(k);
                    if (asleep) { if (wasWalking.Contains(k) && asleepSince.Add(k)) walkedIn++; asleepSince.Add(k); }
                    else if (asleepSince.Contains(k) && fail == null)
                        fail = $"person {k} got up ({a.kind}, moving {a.moving}) at {pv.Now} after going to bed; replans: {string.Join(" / ", lines.TakeLast(3))}";
                    if (asleep && !pv.TryGetDollTop(a.who, out _)) hiddenOnce.Add(k);
                    else if (asleep && hiddenOnce.Contains(k) && !(ShowMeDirector.Instance != null && ShowMeDirector.Instance.CaptionUp))
                    {
                        visibleFrames++;
                        pv.TryGetDollPosition(a.who, out var dp);
                        if (!shown.TryGetValue(k, out var sh)) shown[k] = sh = new List<string>();
                        if (sh.Count < 3) sh.Add($"{pv.Now} doll at {dp} place {a.place.kind}");
                    }
                }
            }
            yield return Snap("01a_night_end");
            pv.timeScale = 1f;
            Note("1a night", $"{frames} frames, slept {asleepSince.Count} (walked in during the watch: {walkedIn}), replans forced {next}, logged {lines.Count}, reached {pv.Now}, sleeper shown frames {visibleFrames}" + (fail != null ? " FAIL: " + fail : ""));
            Assert.IsNull(fail, fail);
            Assert.AreEqual(0, visibleFrames, "a sleeper was shown again: " + string.Join(" || ", shown.Select(kv => kv.Key + ": " + string.Join("; ", kv.Value))));
            Assert.Greater(asleepSince.Count, 3, "people went to bed");
            AssertNoErrors("a camp night");
            CloseSlice();
        }

        // ================================================================ 1b. a camp day (#697): no crowd sets off and turns back over 10 game minutes
        [UnityTest, Timeout(900000)]
        public IEnumerator Beat01b_CampDay_NoCrowdTurnsBackOverTenGameMinutes()
        {
            yield return Grown();
            var pv = Pv;
            var lines = new List<string>();
            pv.Planner.ReplanLog = l => lines.Add($"{pv.Now} {l}");
            pv.Planner.ClearLive(); foreach (var bd in Sim.State.buildings) pv.Planner.ForgetTent(bd.id);
            PeopleView.DebugSetHour(7f);
            pv.timeScale = 4f; // 10 game minutes = 600 s of the view clock = 150 s here
            yield return Frames(4);
            if (pv.Planner.FirePoint(out float fx, out float fz)) Look(World(fx, fz), 0.7f, 50f);
            var prev = new Dictionary<int, PersonActivity>();
            float t0 = Time.realtimeSinceStartup, nextForce = t0 + 30f, nextShot = t0 + 5f;
            int force = 0, worst = 0, shots = 0, frames = 0, totalRev = 0, walkers = 0;
            long day0 = pv.Now.day; double view0 = pv.Now.Absolute;
            string[] forced = { "supper", "building", "month", "chronicle", "population" };
            while (Time.realtimeSinceStartup - t0 < 150f)
            {
                if (Time.realtimeSinceStartup >= nextForce) { nextForce += 30f; PeopleView.DebugReplan(forced[force++ % forced.Length]); }
                yield return null;
                frames++;
                if (pv.Now.hour > 19.5f || pv.Now.day != day0) { PeopleView.DebugSetHour(7f); day0 = pv.Now.day; prev.Clear(); continue; }
                int rev = 0, mov = 0;
                foreach (var a in pv.Activities)
                {
                    if (a.moving) mov++;
                    if (prev.TryGetValue(a.who.Key, out var b) && b.moving && !b.enRoute && !a.enRoute && b.end > pv.Now.hour + 0.02f && !(a.moving && a.place == b.place)) rev++;
                    prev[a.who.Key] = a;
                }
                walkers = Mathf.Max(walkers, mov);
                totalRev += rev; worst = Mathf.Max(worst, rev);
                Assert.Less(rev, 3, $"{rev} walkers turned round in one frame at {pv.Now}; replans: {string.Join(" / ", lines.TakeLast(3))}");
                if (shots < 3 && Time.realtimeSinceStartup >= nextShot) { nextShot += 50f; yield return Snap($"01b_day_{++shots}"); }
            }
            pv.timeScale = 1f;
            Note("1b day", $"{frames} frames, ~10 game min at x4, replans forced {force}, logged {lines.Count}, most walkers at once {walkers}, worst reversals in one frame {worst}, total {totalRev}");
            Assert.GreaterOrEqual(lines.Count, force, "the forced replans were logged");
            AssertNoErrors("a camp day");
            CloseSlice();
        }

        // ================================================================ 2. deer graze near the camp and go round the fire (#699)
        [UnityTest, Timeout(900000)]
        public IEnumerator Beat02_DeerGrazeByTheCampAndGoRoundTheFire()
        {
            yield return LivedInCamp(10f);
            Cheat("jars", $"{Sim.DebugJarsReady()} jars opened");
            yield return CalmHud();
            yield return KeeperPlaces(LifeSpecies.Deer); // Kit, chooser, glow, ghost, ghost: by touch
            Assert.IsTrue(LastPlaced, "the deer are placed by touch");
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 30f);
            yield return Calm();
            var dv = DeerView.Instance;
            Assert.IsNotNull(dv, "the deer view");
            yield return Until(() => { var l = new List<Vector3>(); dv.WorldPositions(l); return l.Count > 0; }, 20f);
            var all = new List<Vector3>(); dv.WorldPositions(all);
            Assert.Greater(all.Count, 0, "deer are shown");
            Assert.IsTrue(Pv.Planner.FirePoint(out float fx, out float fz), "the camp has a fire");
            DefaultView();
            Look(World(fx, fz), 0.75f, 50f);
            yield return Settle(2f);
            yield return Snap("02a_deer_graze_by_the_camp");
            // the cheat: one deer on a straight path through the fire
            Cheat("deer across the fire", dv.DebugAcrossTheFire(Sim.EcoGround.campX, Sim.EcoGround.campZ) ? "a deer sets off across the camp" : "no deer shown");
            float cx = Sim.EcoGround.campX, cz = Sim.EcoGround.campZ;
            int id = -1; float bestD = float.MaxValue;
            for (int i = 0; i < 24; i++)
                if (dv.TryGetDeerById(i, out var p)) { float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(cx - 1.3f, cz)); if (d < bestD) { bestD = d; id = i; } }
            Assert.GreaterOrEqual(id, 0, "the crossing deer is found by id");
            Assert.Less(bestD, 0.3f, "and it stands where the cheat put it");
            dv.TryGetDeerById(id, out var start0);
            float minD = float.MaxValue, maxX = float.MinValue; int shots = 0, still = 0; Vector3 last = start0;
            float t0 = Time.realtimeSinceStartup;
            Look(World(cx, cz), 0.5f, 55f);
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                yield return null;
                if (!dv.TryGetDeerById(id, out var p)) continue;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(cx, cz));
                minD = Mathf.Min(minD, d); maxX = Mathf.Max(maxX, p.x - cx);
                still = (p - last).sqrMagnitude < 1e-8f ? still + 1 : 0; last = p;
                if (shots == 0 && Time.realtimeSinceStartup - t0 > 3f) { shots++; yield return Snap("02b_deer_walking_toward_the_fire"); }
                if (shots == 1 && d < 1.4f && d > 0.5f && Time.realtimeSinceStartup - t0 > 6f) { shots++; yield return Snap("02c_deer_round_the_fire"); }
                if (p.x - cx > 1.0f && Mathf.Abs(p.z - cz) < 1.4f) break; // out the far side
            }
            if (shots < 2) yield return Snap("02c_deer_round_the_fire");
            float ring = Sim.EcoGround.campX != 0 ? 0f : 0f;
            Note("2 deer", $"id {id}, nearest it came to the fire {minD:0.00} u, ended {last.x - cx:0.00} u east of the fire (started 1.30 west), still frames at the end {still}");
            Assert.Greater(minD, 0.6f, "the deer goes round the fire (it keeps its shy distance), never through it");
            Assert.Greater(maxX, 0.5f, "and gets to the far side (not stuck at the ring)");
            yield return Snap("02d_deer_after");
            AssertNoErrors("deer by the camp");
        }

        // ================================================================ 3. winter: the duck jar says the pond is frozen; after the thaw ducks placed by touch stay (#703)
        IEnumerator WinterNow()
        {
            for (int i = 0; i < 4 && !PondIce.Winter(Sim.State.month); i++) EcoDebug.SkipToNextSeason();
            if (!PondIce.Winter(Sim.State.month)) { PondIce.Force = true; Cheat("ice", "forced: the calendar did not reach winter"); }
            yield return Settle(2f);
        }

        IEnumerator ThawNow()
        {
            PondIce.Force = null;
            for (int i = 0; i < 4 && PondIce.Winter(Sim.State.month); i++) EcoDebug.SkipToNextSeason();
            if (PondIce.Winter(Sim.State.month)) { PondIce.Force = false; Cheat("thaw", "forced"); }
            yield return Settle(2f);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat03_WinterDuckJarSaysFrozen_AfterTheThawDucksPlacedByTouchStay()
        {
            try
            {
                yield return LivedInCamp(10f);
                Cheat("jars", $"{Sim.DebugJarsReady()} jars opened");
                yield return WinterNow();
                Note("3 winter", $"month {Sim.State.month}, winter {PondIce.Winter(Sim.State.month)}, forced {PondIce.Force}, jar {Sim.JarStateOf(LifeSpecies.Ducks)}");
                var hud = Hud;
                yield return CalmHud();
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean")); // Kit
                yield return Settle(0.6f);
                Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens");
                // #668: no jar to open first: the tiles are on the kit's shelves
                yield return Settle(0.5f);
                Assert.IsTrue(hud.LifeRowOpen, "the chooser opens");
                var icon = hud.Root.Q("kit-life-locked-ducks");
                Assert.IsNotNull(icon, "the ducks are greyed in the chooser while the pond is ice");
                Assert.IsTrue(icon.ClassListContains(KitTile.ShutClass));
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-locked-ducks"));
                yield return Settle(0.5f);
                Note("3 winter", "the note says: '" + hud.LifeNoteText + "'");
                StringAssert.Contains("ice", hud.LifeNoteText, "the jar says the pond is frozen");
                Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "a shut jar is never taken in hand");
                yield return Snap("03a_winter_duck_jar_frozen_note");
                hud.CloseSheet();
                yield return Frames(10);
                Look(World(Sim.EcoGround.campX, Sim.EcoGround.campZ), 0.9f, 50f);
                yield return Snap("03b_winter_pond_ice");
                DefaultView();

                // the thaw, then the ducks placed by touch: Kit, the chooser, the ducks
                yield return ThawNow();
                Assert.AreEqual(JarState.Ready, Sim.JarStateOf(LifeSpecies.Ducks), "the jar opens at the thaw");
                yield return CalmHud();
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return Settle(0.6f);
                // #668: no jar to open first: the tiles are on the kit's shelves
                yield return Settle(0.5f);
                Assert.IsTrue(hud.LifeRowOpen, "the chooser opens after the thaw");
                yield return Snap("03c_thaw_chooser_ducks_ready");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-ducks"));
                yield return Settle(0.5f);
                Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "the ducks in hand");
                var lpv = LifePlaceView.Instance;
                yield return Frames(6);
                Assert.Greater(lpv.GlowCells, 0, "the water glows");
                yield return Until(() => !Rig.IsFlying, 10f);
                yield return Settle(0.5f);
                var ground = new List<int>(); Sim.LifeGround(LifeSpecies.Ducks, ground);
                int cell = -1; float bd = float.MaxValue;
                foreach (int c in ground)
                {
                    if (!Sim.CanPlaceLife(LifeSpecies.Ducks, c)) continue;
                    var p = PondSurface(c);
                    if (p.x < Screen.width * 0.12f || p.x > Screen.width * 0.88f || p.y < Screen.height * 0.2f || p.y > Screen.height * 0.78f || UiInput.Over(p)) continue;
                    float d = Vector2.Distance(p, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                    if (d < bd) { bd = d; cell = c; }
                }
                Assert.GreaterOrEqual(cell, 0, "a glowing water cell on screen");
                yield return TapAt(PondSurface(cell));
                yield return Frames(4);
                Assert.IsTrue(lpv.GhostShown, "the first tap shows the ghost: " + lpv.Last);
                yield return Settle(0.4f);
                yield return Until(() => !Rig.IsFlying, 10f);
                yield return Snap("03d_ducks_ghost");
                int far = -1; float farD = 0f; var g = lpv.GhostScreen;
                foreach (int c in ground)
                {
                    var q = PondSurface(c);
                    if (q.x < Screen.width * 0.1f || q.x > Screen.width * 0.9f || q.y < Screen.height * 0.2f || q.y > Screen.height * 0.8f || UiInput.Over(q)) continue;
                    float d = Vector2.Distance(q, g); if (d > farD) { farD = d; far = c; }
                }
                yield return TapAt(PondSurface(far));
                yield return Frames(8);
                Assert.IsTrue(Sim.LifeIsPlaced(LifeSpecies.Ducks), "a second tap on the water places them: " + lpv.Last);
                int ducks = Sim.EcoDucks;
                yield return Settle(2f);
                yield return Snap("03e_ducks_placed_after_thaw");
                // the freeze comes again: the placing is kept
                PondIce.Force = true;
                yield return Settle(3f);
                Assert.IsTrue(Sim.LifeIsPlaced(LifeSpecies.Ducks), "the ducks stay placed through the freeze");
                Assert.AreEqual(ducks, Sim.EcoDucks, "the count is kept (they are away for the winter, #618)");
                Assert.AreEqual(JarState.Placed, Sim.JarStateOf(LifeSpecies.Ducks));
                Look(World(Sim.EcoGround.campX, Sim.EcoGround.campZ), 0.9f, 50f);
                yield return Snap("03f_frozen_again_ducks_kept");
                PondIce.Force = false;
                yield return Settle(10f);
                Assert.AreEqual(ducks, Sim.EcoDucks, "back at the thaw");
                yield return Snap("03g_thawed_ducks_back");
                Note("3 winter", $"ducks placed by touch after the thaw ({ducks}), kept through a second freeze and back");
                AssertNoErrors("winter ducks");
            }
            finally { PondIce.Force = null; }
        }

        static Vector2 PondSurface(int cell)
        {
            float w = Box.layout != null && Box.layout.eco != null ? Box.layout.eco.waterY : Land.WaterY;
            return Camera.main.WorldToScreenPoint(Box.transform.TransformPoint(new Vector3(SiteMap.CXc(cell), w, SiteMap.CZc(cell))));
        }

        // ================================================================ 4. a path grows toward the fish rack: the rack moves off it (#700), at night
        static bool FootprintOnPath(in LifePlanner.FishRackLook look)
        {
            for (float a = -FishRackSpot.HalfLength; a <= FishRackSpot.HalfLength + 1e-4f; a += 0.04f)
                for (float d = -FishRackSpot.HalfDepth; d <= FishRackSpot.HalfDepth + 1e-4f; d += 0.035f)
                    if (RackSites.PathAt(Sim, look.x + look.dirX * a - look.dirZ * d, look.z + look.dirZ * a + look.dirX * d)) return true;
            return false;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat04_APathGrowsOverTheFishRack_TheRackMovesOffItAtNight()
        {
            yield return LivedInCamp(10f);
            Cheat("fish", $"{Sim.DebugJarsReady()} jars opened; fish at {Sim.DebugPlaceNearCamp(LifeSpecies.Ducks)} / {Sim.DebugPlaceNearCamp(LifeSpecies.Fish)}");
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 30f);
            yield return Calm();
            var pv = Pv;
            PeopleView.DebugSetHour(10f);
            yield return Settle(1f);
            Assert.IsTrue(pv.Planner.TryGetFishRack(pv.Now, out var before), "the camp has a fish rack");
            Look(World(before.x, before.z), 0.18f, 50f);
            yield return Settle(1f);
            yield return Snap("04a_rack_before_the_path");
            int movesBefore = RackSites.Moves(Sim);
            Cheat("path over the rack", SimDebugPanel.DoPathOverRack(false));
            Assert.IsTrue(FootprintOnPath(before), "the rack stands on a path now");
            yield return Settle(1.5f);
            Look(World(before.x, before.z), 0.16f, 55f); yield return Settle(0.6f); Look(World(before.x, before.z), 0.16f, 55f); yield return Frames(4);
            yield return Snap("04b_path_over_the_rack_by_day");
            // night: the quiet moment. The clock goes to 21:00, the rack moves at the next look
            PeopleView.DebugSetHour(21f);
            yield return Settle(3f);
            Assert.IsTrue(pv.Planner.TryGetFishRack(pv.Now, out var after), "the rack is still there");
            yield return Settle(1f);
            Look(World(after.x, after.z), 0.2f, 50f);
            yield return Settle(1f);
            yield return Snap("04c_rack_after_the_move_at_night");
            float moved = Mathf.Abs(after.x - before.x) + Mathf.Abs(after.z - before.z);
            Note("4 rack", $"night {RackSites.IsNight(Sim)}, moves {RackSites.Moves(Sim)} (was {movesBefore}), moved {moved:0.00} u, still on the path {FootprintOnPath(after)}");
            Assert.IsFalse(FootprintOnPath(after), "the rack moved off the path");
            Assert.Greater(moved, 0.05f, "it stands somewhere else");
            Assert.Greater(RackSites.Moves(Sim), movesBefore, "the move is counted");
            Look(World(Sim.EcoGround.campX, Sim.EcoGround.campZ), 0.5f, 50f);
            yield return Snap("04d_camp_at_night_rack_off_the_path");
            AssertNoErrors("a path over the rack");
        }

        // ================================================================ 5. the rabbit chase ends: the child walks back, visible (#698)
        static PersonActivity ActOf(PeopleView pv, PersonRef who) => pv.Activities.First(a => a.who.Key == who.Key);

        static float DistToPlan(PeopleView pv, PersonActivity a, Vector2 at)
        {
            if (!pv.Planner.PositionAt(a.who, pv.Now, out float px, out float pz) && !pv.Planner.Positions.TryGetPosition(a.place, out px, out pz)) return 0f;
            return (new Vector2(px, pz) - at).magnitude;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat05_TheRabbitChaseEnds_TheChildWalksBackAndIsSeen()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 1234);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            var born = new List<Notable>();
            for (int i = 0; i < 4; i++) { var n = Sim.DebugBirth(); Assert.IsNotNull(n, "a grown-up to be a parent"); born.Add(n); }
            SimLayoutSource.DebugSkipYears(0.1f);
            yield return null;
            foreach (var n in born) n.age = 7f;
            Pv.Planner.Invalidate();
            Sim.CleanAll();
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(14f);
            yield return Frames(4);
            yield return Calm();
            PeopleView.DebugSetHour(14f);
            yield return Frames(4);
            var pv = Pv; var rv = RabbitView.Instance;
            Assert.IsNotNull(rv, "the rabbit view runs on a living-land Slice");
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            Cheat("rabbits", EcoDebug.AddRabbits(6));
            var kids = pv.Planner.People.Where(w => pv.Planner.StageOf(w) == LifeStage.Kid).ToList();
            Assert.GreaterOrEqual(kids.Count, 2, "kids in the box");
            yield return Until(() => !rv.SignPlaying && rv.Shown >= 1, 15f);
            Assert.GreaterOrEqual(rv.Shown, 1, "rabbits on view to chase");
            string said = KidPlayDirector.Debug(PlayGame.ChaseRabbits);
            Cheat("kids chase rabbits", said);
            var members = kids.Where(k => pv.KidPlay.GameOf(k.Key, out _) != null).ToList();
            Assert.GreaterOrEqual(members.Count, 1, "kids in the chase");
            if (pv.TryGetDollPosition(members[0], out var kp)) Look(World(kp.x, kp.y), 0.4f, 50f);
            yield return Settle(10f);
            if (pv.TryGetDollPosition(members[0], out kp)) Look(World(kp.x, kp.y), 0.4f, 50f);
            yield return Snap("05a_the_chase");
            var last = new Dictionary<int, Vector2>(); var wasShown = new Dictionary<int, bool>();
            foreach (var m in members) { wasShown[m.Key] = pv.TryGetDollTransform(m, out _); pv.TryGetDollPosition(m, out var p0); last[m.Key] = p0; }
            var startFar = members.Where(m => DistToPlan(pv, ActOf(pv, m), last[m.Key]) > 0.6f).Select(m => m.Key).ToHashSet();
            var shownFrames = new Dictionary<int, int>();
            pv.KidPlay.Clear(); // the chase is over: the group is handed back to the planner
            yield return Snap("05b_the_chase_is_over");
            var settled = new HashSet<int>(); var still = new Dictionary<int, float>();
            float t0 = Time.realtimeSinceStartup, lastT = t0; bool shot2 = false; int frames = 0;
            var log = new StringBuilder();
            while (Time.realtimeSinceStartup - t0 < 90f && settled.Count < members.Count)
            {
                yield return null;
                frames++;
                float dt = Time.realtimeSinceStartup - lastT; lastT = Time.realtimeSinceStartup;
                if (!shot2 && Time.realtimeSinceStartup - t0 > 2.5f)
                {
                    shot2 = true;
                    if (pv.TryGetDollPosition(members[0], out var q)) Look(World(q.x, q.y), 0.4f, 50f);
                    yield return Snap("05c_the_child_walks_back"); lastT = Time.realtimeSinceStartup;
                }
                foreach (var m in members)
                {
                    var a = ActOf(pv, m);
                    bool shown = pv.TryGetDollTransform(m, out _);
                    pv.TryGetDollPosition(m, out var pos);
                    if (shown) shownFrames[m.Key] = shownFrames.GetValueOrDefault(m.Key) + 1;
                    bool had = wasShown.TryGetValue(m.Key, out var ws);
                    if (shown && had && ws)
                    {
                        float step = (pos - last[m.Key]).magnitude;
                        if (step > 1.2f * dt + 0.25f) log.AppendLine($"JUMP {m} {step:0.000} u in {dt * 1000:0} ms at {pv.Now.hour:0.00} h");
                    }
                    if (had && ws && !shown)
                    {
                        float d = DistToPlan(pv, a, last[m.Key]);
                        bool intent = a.kind is ActivityKind.Sleep or ActivityKind.Indoors && !a.moving;
                        if (!intent || d > 0.6f) log.AppendLine($"VANISH {m} {d:0.00} u from its plan's place ({a.kind} {(a.moving ? "moving" : "still")}), hour {pv.Now.hour:0.00}");
                    }
                    if (!shown && !a.moving) settled.Add(m.Key);
                    else if (shown && !a.moving && had && (pos - last[m.Key]).magnitude < 1e-4f) { still[m.Key] = still.GetValueOrDefault(m.Key) + dt; if (still[m.Key] > 1.5f) settled.Add(m.Key); }
                    else still[m.Key] = 0f;
                    wasShown[m.Key] = shown; last[m.Key] = pos;
                }
            }
            if (pv.TryGetDollPosition(members[0], out var fin)) Look(World(fin.x, fin.y), 0.4f, 50f);
            yield return Snap("05d_the_child_at_its_place");
            Note("5 chase", $"{members.Count} kids in the chase, {frames} frames, settled {settled.Count}/{members.Count}, started far from their place: {startFar.Count}; problems: {(log.Length == 0 ? "none" : log.ToString())}");
            Assert.IsEmpty(log.ToString(), "kids hidden or jumping on the way back to their plan");
            foreach (int k in startFar) Assert.GreaterOrEqual(shownFrames.GetValueOrDefault(k), 20, $"kid {k} must be seen walking back, not gone at once");
            Assert.AreEqual(members.Count, settled.Count, "every kid reached its plan's place");
            AssertNoErrors("the chase");
            CloseSlice();
        }

        // ================================================================ 6. History scrubbed back by touch (#669)
        static Dictionary<string, int> Drawn(BoxView box)
        {
            var d = new Dictionary<string, int>();
            foreach (var r in box.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var t = r.transform;
                while (t.parent != null && t.parent != box.transform && t.parent != box.Root) t = t.parent;
                string k = t.name;
                int i = k.IndexOf('_'); if (i > 0 && char.IsDigit(k[k.Length - 1])) k = k.Substring(0, i);
                d[k] = d.TryGetValue(k, out var n) ? n + 1 : 1;
            }
            return d;
        }

        static bool Has(Dictionary<string, int> d, string key) => d.Keys.Any(k => k.StartsWith(key));

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat06_HistoryScrubbedBackByTouch_NoHouseNoBerriesNoRabbits_BackAtTheEnd()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            ShowMeDirector.BubblesOn = false;
            var box = Box; var src = SimLayoutSource.Find();
            SimLayoutSource.DebugSkipYears(1f); // year 1 passes with nothing placed or built
            yield return Frames(20);
            Sim.DebugJarsReady();
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0, "thicket placed");
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0, "rabbits placed");
            var hut = Sim.DebugBuild(BuildingType.Hut);
            var lot = Sim.State.settle.lots.Find(l => l.buildingId == hut.id);
            Assert.IsNotNull(lot, "the new house has a lot");
            Cheat("things placed and built after year 1", $"thicket, rabbits, a house, {ConsequenceCheats.PlaceEvery().Length} choice props; rack: {FishRackView.DebugRack()}");
            yield return Frames(90);
            SimLayoutSource.DebugSkipYears(1f); // the box has a year 3 behind these
            yield return Frames(30);
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 30f);
            yield return Calm();
            PeopleView.DebugSetHour(10f);
            Look(box.transform.TransformPoint(box.land.OnGround(Sim.EcoGround.campX, Sim.EcoGround.campZ)), 0.8f, 52f);
            yield return Settle(2f);
            var live = Drawn(box);
            Assert.IsNotNull(RabbitView.Instance, "live: rabbits");
            Assert.IsNotNull(BushView.Instance, "live: berries");
            Assert.IsTrue(Has(live, "ConsequenceProps"), "live: the props the choices left");
            yield return Snap("06a_now_before_history");

            // open History by touch
            var hud = Hud;
            yield return CalmHud();
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
            var screen = TimeLapseScreenView.Ensure();
            yield return Until(() => screen.IsOpen, 4f);
            Assert.IsTrue(screen.IsOpen, "the History tool opens the time-lapse");
            yield return Until(() => screen.Model.Layout.Width > 0f, 3f);
            yield return Frames(6);
            var m = screen.Model;
            Assert.IsTrue(m.AtNow, "opens at the present");
            yield return Snap("06b_history_open_at_now");

            // drag the playhead from Now back to the very start
            yield return Drag(new[] { screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.End) - 2f), screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.Start) + 1f) }, 18f);
            yield return Frames(40);
            Note("6 history", $"scrubbed to position {m.Position:0.00} (start {m.Clock.Start:0.00}, end {m.Clock.End:0.00}); gate active {ReplayGate.Active}, month {ReplayGate.Month}, at present {ReplayGate.AtPresent}");
            Assert.IsTrue(ReplayGate.Active && !ReplayGate.AtPresent, "the gate says the past");
            Assert.Less(ReplayGate.Month, SimConfig.MonthsPerYear, "in year 1");
            var y1 = Drawn(box);
            Assert.IsNull(BushView.Instance, "no berries in year 1");
            Assert.IsNull(RabbitView.Instance, "no rabbits and no tracks in year 1");
            Assert.IsFalse(Has(y1, "Rabbits") || Has(y1, "Bushes") || Has(y1, "Arrival_"), "no animals, berries or thicket mark drawn");
            Assert.IsFalse(Has(y1, "ConsequenceProps"), "no props left by later choices");
            Assert.IsFalse(Has(y1, "Fish rack"), "no rack");
            Assert.IsFalse(src.Replay.Shows(lot), "the later house is not in year 1's box");
            yield return Snap("06c_history_year_1_nothing_built_nothing_placed");

            // and back to the end of the timeline by touch
            yield return Drag(new[] { screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.Start) + 1f), screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.End) + 20f) }, 18f);
            yield return Frames(60);
            Note("6 history", $"dragged to the end: position {m.Position:0.00}, at present {ReplayGate.AtPresent}, at now {m.AtNow}");
            Assert.IsTrue(ReplayGate.AtPresent, "the end of the timeline is now");
            var end = Drawn(box);
            Assert.IsTrue(Has(end, "ConsequenceProps"), "at the end the props stand");
            Assert.IsTrue(src.Replay == null || src.Replay.Shows(lot), "and the house");
            Assert.IsNotNull(RabbitView.Instance, "at the end: rabbits");
            Assert.IsNotNull(BushView.Instance, "at the end: berries");
            Assert.IsTrue(Has(end, "Rabbits"), "at the end: rabbits drawn");
            yield return Snap("06d_history_at_the_end_everything_back");
            for (int tries = 0; tries < 3 && screen.IsOpen; tries++)
            {
                // A player closes the caption first (its X, tl-cap-close), and only then reaches the screen's Close (tl-close, hidden while the caption is up).
                // Every tap here is on an element that is really on screen: a hidden one has a zero rect and a tap on it proves nothing.
                var capClose = screen.Root.Q("tl-cap-close");
                if (capClose != null && Shown(capClose))
                {
                    Assert.Greater(capClose.worldBound.width * capClose.worldBound.height, 1f, "the caption's X has a real rect");
                    yield return TapAt(GameUiPanel.ScreenCenter(capClose));
                    yield return Frames(30);
                    Note("6 history", $"tapped the caption's X (try {tries + 1}): caption X shown {Shown(capClose)}, open {screen.IsOpen}");
                }
                var close = screen.Root.Q("tl-close");
                Assert.IsTrue(close != null && Shown(close) && close.worldBound.width > 1f && close.worldBound.height > 1f,
                    $"Close is on screen before it is tapped (shown {Shown(close)}, rect {close?.worldBound}, caption X shown {Shown(capClose)})");
                yield return TapAt(GameUiPanel.ScreenCenter(close));
                yield return Frames(40);
                Note("6 history", $"tapped Close (try {tries + 1}): open {screen.IsOpen}, close button shown {Shown(close)}");
            }
            Assert.IsFalse(screen.IsOpen, "closed");
            Assert.IsFalse(ReplayGate.Active, "gate off");
            yield return Settle(1f);
            yield return Snap("06e_history_closed");
            AssertNoErrors("history by touch");
        }

        // ================================================================ 7. the Feedback pill tapped with each panel open (#702)
        static VisualElement FindPill(VisualElement hit)
        {
            for (var e = hit; e != null; e = e.parent) if (e.name == "hud-feedback") return e;
            return hit;
        }

        IEnumerator TapPillOpensSheet(string what, string shot)
        {
            var fb = FeedbackUI.Instance; var pill = fb.Pill;
            yield return Frames(4);
            Assert.AreEqual(DisplayStyle.Flex, pill.resolvedStyle.display, what);
            var at = GameUiPanel.ScreenCenter(pill);
            var panelPt = RuntimePanelUtils.ScreenToPanel(pill.panel, new Vector2(at.x, Screen.height - at.y));
            Assert.AreEqual(pill, FindPill(pill.panel.Pick(panelPt)), $"{what}: the pill is what a touch at its place reaches");
            if (shot != null) yield return Snap(shot);
            yield return TapAt(at);
            yield return Until(() => fb.IsOpen, 4f);
            Assert.IsTrue(fb.IsOpen, $"{what}: tapping Feedback opens the sheet");
            if (shot != null) yield return Snap(shot + "_sheet");
            fb.Close();
            yield return Frames(4);
            Assert.IsFalse(fb.IsOpen);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat07_TheFeedbackPillTakesATapOverEveryPanel()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsNotNull(FeedbackUI.Instance?.Pill, "the Feedback pill is there (test build)");
            yield return Frames(10);
            var hud = Hud; var fb = FeedbackUI.Instance;
            try
            {
                yield return TapPillOpensSheet("nothing open", "07a_pill_nothing_open");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return Settle(0.6f);
                Assert.AreEqual(GameHud.Kit, hud.OpenSheet);
                yield return TapPillOpensSheet("the kit", "07b_pill_kit");
                Assert.IsNull(hud.OpenSheet, "the HUD's sheet shut when Feedback opened");
                Sim.DebugJarsReady();
                yield return Settle(0.5f);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return Settle(0.6f);
                // #668: no jar to open first: the tiles are on the kit's shelves
                yield return Settle(0.6f);
                Assert.IsTrue(hud.LifeRowOpen);
                yield return TapPillOpensSheet("the life chooser", "07c_pill_chooser");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-settings"));
                yield return Until(() => hud.OpenSheet == GameHud.Settings, 4f);
                yield return Settle(0.6f);
                yield return TapPillOpensSheet("the settings sheet", "07d_pill_settings");
                var who = Sim.State.notables.First(n => n.alive).id;
                Assert.IsTrue(PersonCardView.Instance.Open(who, false));
                yield return Settle(0.8f);
                yield return TapPillOpensSheet("the person card", "07e_pill_card");
                PersonCardView.Instance.Close();
                yield return Frames(6);
                var mp = MomentPlayer.Instance;
                Sim.State.grime = 0f;
                Assert.IsTrue(Sim.ForceEraReady());
                yield return null;
                MomentPlayer.DebugHurry(4f);
                MomentPlayer.DebugHoldHurry = true;
                Assert.IsTrue(mp.Watch(true), "the moment starts");
                yield return Settle(1.5f);
                Assert.IsTrue(mp.Playing, "the moment is playing");
                yield return TapPillOpensSheet("a moment", "07f_pill_moment");
                MomentPlayer.DebugHoldHurry = false;
                if (mp.Playing) mp.Leave();
                yield return Frames(10);
                hud.SetCameraMode(true);
                yield return Settle(0.4f);
                Assert.IsTrue(hud.CameraMode, "photo mode is on");
                yield return TapPillOpensSheet("photo mode", "07g_pill_photo");
                hud.SetCameraMode(false);
                yield return Frames(6);
                StringAssert.StartsWith("asking", PulseQuestionView.DebugAsk("know"));
                yield return Until(() => PulseQuestionView.Instance.IsOpen, 5f);
                Assert.IsTrue(PulseQuestionView.Instance.IsOpen, "the quick question is up");
                yield return Settle(0.8f);
                yield return TapPillOpensSheet("the quick-question sheet", "07h_pill_question");
                Note("7 pill", "a touch at the pill opened the sheet over: nothing, the kit, the chooser, settings, a person card, a moment, photo mode, the quick question");
                AssertNoErrors("the pill");
            }
            finally
            {
                MomentPlayer.DebugHoldHurry = false;
                if (fb != null && fb.IsOpen) fb.Close();
                CloseSlice();
            }
        }

        // ================================================================ 8. the HUD top by day and by night (#707)
        [UnityTest, Timeout(900000)]
        public IEnumerator Beat08_TheHudTopByDayAndByNight()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            var hud = Hud;
            yield return Frames(10);
            void OneLook(string when)
            {
                foreach (var d in hud.Root.Query(className: "hud-disc").ToList())
                    Assert.AreEqual(0f, d.resolvedStyle.backgroundColor.a, 0.001f, $"{when}: no backing behind a round button");
                Assert.AreEqual(0f, hud.Root.Q("hud-title").resolvedStyle.backgroundColor.a, 0.001f, $"{when}: none behind the name");
                var tints = new[] { hud.Root.Q("hud-settings"), hud.Root.Q("hud-photo"), FeedbackUI.Instance.Pill }.Select(b => b.Q<GlassIcon>().resolvedStyle.unityBackgroundImageTintColor).ToList();
                Assert.AreEqual(tints[0], tints[1], $"{when}: Settings and Photo share one ink");
                Assert.AreEqual(tints[0], tints[2], $"{when}: and so does Feedback");
            }
            PeopleView.DebugSetHour(12f);
            yield return Settle(2f);
            var title = hud.Root.Q("hud-title").worldBound; var set = hud.Root.Q("hud-settings").worldBound; var photo = hud.Root.Q("hud-photo").worldBound; var fbb = FeedbackUI.Instance.Pill.worldBound;
            Assert.Less(title.xMax, set.xMin, "Settings is right of the name");
            Assert.Less(set.xMax, fbb.xMin + 2f, "Settings, then Feedback");
            Assert.Less(fbb.xMax, photo.xMin + 2f, "then Photo");
            OneLook("day");
            yield return Snap("08a_hud_top_day");
            PeopleView.DebugSetHour(22f);
            yield return Settle(2f);
            OneLook("night");
            yield return Snap("08b_hud_top_night");
            // by touch: the gear opens Settings, the name opens the offices
            PeopleView.DebugSetHour(12f);
            yield return Settle(1.6f);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-settings"));
            yield return Until(() => hud.OpenSheet == GameHud.Settings, 4f);
            Assert.AreEqual(GameHud.Settings, hud.OpenSheet, "the gear opens Settings");
            yield return Settle(0.8f);
            OneLook("settings sheet");
            yield return Snap("08c_hud_top_settings_open");
            hud.CloseSheet();
            yield return Frames(8);
            Note("8 hud top", "one look by day and by night; Settings, Feedback, Photo in a row right of the name; the gear opens Settings by touch");
            AssertNoErrors("the hud top");
            CloseSlice();
        }

        // ================================================================ 9. the fire on dry ground, grass and snow (#704); the pond at the glass (#701)
        IEnumerator Frame(Vector2 at, float yaw, float pitch, float zoom)
        {
            Rig.minZoom = 0.02f;
            for (int i = 0; i < 40; i++)
            {
                Rig.EaseTo(World(at.x, at.y), yaw, pitch, zoom, 0.01f);
                yield return null;
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat09a_TheFireRing_DryGround_Grass_Snow()
        {
            UseLivingLand(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(12f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            yield return Settle(1f);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            yield return Settle(2f);
            var box = Box;
            Sim.DebugFireStage(2);
            box.Rebuild();
            yield return Settle(1f);
            var fire = box.layout.pieces.FirstOrDefault(q => q.kind == PieceKind.Fire);
            Assert.IsNotNull(fire, "the camp has a fire");
            var at = Pv.Planner.FirePoint(out float pfx, out float pfz) ? new Vector2(pfx, pfz) : new Vector2(fire.x, fire.z);
            Note("9 fire", $"fire piece ({fire.x:0.00},{fire.z:0.00}), planner fire point {at}");
            IEnumerator Close(string name)
            {
                PeopleView.DebugSetHour(18.4f); Look(World(at.x, at.y), 0.12f, 48f); for (int i = 0; i < 6; i++) { Look(World(at.x, at.y), 0.12f, 48f); yield return null; }
                yield return Settle(0.5f);
                Note("9 fire", name + ": " + SeasonLook.Word(Sim.State.month) + ", fire stage " + Sim.FireStage); yield return Snap(name + "_close");
                Look(World(at.x, at.y), 0.22f, 50f); for (int i = 0; i < 6; i++) { Look(World(at.x, at.y), 0.22f, 50f); yield return null; }
                yield return Settle(0.4f);
                yield return Snap(name + "_wide");
            }
            Note("9 fire", "dry/bare: " + EcoDebug.SkipToNextSeason() + ", bare cells " + Sim.State.eco.cover.Count(c => c == (byte)EcoCover.Bare));
            yield return Close("09a_fire_dry_ground_logfire");
            Sim.DebugFireStage(1); box.Rebuild(); yield return Settle(1f);
            yield return Close("09b_fire_dry_ground_grassfire");
            Sim.DebugFireStage(2); box.Rebuild(); yield return Settle(1f);
            foreach (var (x, z) in new[] { (fire.x, fire.z), (fire.x + 0.5f, fire.z), (fire.x - 0.5f, fire.z + 0.3f), (fire.x, fire.z - 0.5f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            yield return Settle(2f);
            Note("9 fire", "greener: " + EcoDebug.Status());
            yield return Close("09c_fire_on_grass");
            for (int i = 0; i < 4 && SeasonLook.Word(Sim.State.month) != "Winter"; i++) EcoDebug.SkipToNextSeason();
            Note("9 fire", "season " + SeasonLook.Word(Sim.State.month));
            yield return Settle(2f);
            yield return Close("09d_fire_in_snow");
            AssertNoErrors("the fire ring");
        }

        void AssertWaterInsideBank(BoxView box, string tag)
        {
            var mesh = box.DebugWaterMesh;
            Assert.IsNotNull(mesh, tag + ": a water mesh");
            var v = mesh.vertices;
            Assert.Greater(v.Length, 0, tag + ": the sheet has vertices");
            float y = v.Max(q => q.y), worst = float.MinValue; int n = 0;
            foreach (var q in v)
            {
                if (Mathf.Abs(q.x) >= Land.HX - 0.005f || !Land.InZ(q.z, 0.005f)) continue;
                if (q.y < y - 0.001f) continue;
                n++;
                worst = Mathf.Max(worst, box.land.Height(q.x, q.z));
            }
            Note("9 pond", $"{tag}: {n} water vertices off the glass, highest ground under one {worst:0.000} vs level {y:0.000} (margin {BoxView.RimMargin})");
            Assert.Less(worst, y - BoxView.RimMargin + 0.001f, tag + ": water ends inside the bank");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat09b_ThePondRim_AtTheGlass_Small_Grown()
        {
            UseLivingLand(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(12f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            yield return Settle(1f);
            var box = Box;
            Note("9 pond", "first water: " + EcoDebug.WaterBreaksNow());
            yield return Settle(12f);
            Note("9 pond", "pond cells after the first water: " + Sim.EcoPondCells);
            Note("9 pond", "dries: " + EcoDebug.PondDries() + " -> " + Sim.EcoPondCells + " cells");
            Note("9 pond", "floods: " + EcoDebug.WaterBreaksNow() + " -> " + Sim.EcoPondCells + " cells");
            Note("9 pond", "SMALL and GROWN ponds: this seed's basin floods whole (every table step jumps to every pond cell), so a small or grown stage is unreachable here; the whole pond is shot, with the rule asserted on the mesh");
            yield return Settle(12f);
            PondIce.Force = false;
            yield return Settle(1.5f);
            var cells = new List<Vector2>();
            for (int c = 0; c < SiteMap.N; c++) { var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)); if (box.InPond(p)) cells.Add(p); }
            Assert.Greater(cells.Count, 0, "a pond to shoot");
            try
            {
                AssertWaterInsideBank(box, "the pond");
                var front = cells.OrderBy(p => p.y).First();
                var side = cells.OrderByDescending(p => p.x).First();
                var centre = new Vector2(cells.Average(p => p.x), cells.Average(p => p.y));
                var glass = cells.OrderBy(p => Mathf.Min(Land.HX - Mathf.Abs(p.x), Land.GlassGapZ(p.y))).First();
                float glassGap = Mathf.Min(Land.HX - Mathf.Abs(glass.x), Land.GlassGapZ(glass.y));
                Note("9 pond", $"nearest pond cell to the glass: {glassGap:0.00} u off it");
                var spots = new List<(string, Vector2, float, float, float)> { ("a_whole", centre, Rig.Home.yaw, 50f, 0.32f), ("b_front", front, Rig.Home.yaw, 35f, 0.14f), ("c_side", side, Rig.Home.yaw + 40f, 35f, 0.14f), ("d_glass", glass, Rig.Home.yaw, 35f, 0.14f) };
                foreach (var (n, at, yaw, pitch, zoom) in spots)
                {
                    yield return Frame(at, yaw, pitch, zoom);
                    yield return Settle(0.4f);
                    yield return Snap("09e_pond_" + n);
                }
                PondIce.Force = true;
                yield return Settle(4f);
                yield return Frame(glass, Rig.Home.yaw, 35f, 0.14f);
                yield return Settle(0.4f);
                yield return Snap("09f_pond_ice_at_the_glass");
            }
            finally { PondIce.Force = null; }
            AssertNoErrors("the pond rim");
        }

        // ================================================================ 10. a fish stew and a rabbit stew (#676)
        [UnityTest, Timeout(900000)]
        public IEnumerator Beat10_AFishStewAndARabbitStew()
        {
            yield return LivedInCamp(17.8f);
            Cheat("camp props", ConsequenceCheats.ShowCampProps());
            Cheat("fish placed", $"{Sim.DebugJarsReady()} jars; {Sim.DebugPlaceNearCamp(LifeSpecies.Ducks)} / {Sim.DebugPlaceNearCamp(LifeSpecies.Fish)}");
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 30f);
            yield return Calm();
            var pv = Pv;
            foreach (var kind in new[] { StewKind.Fish, StewKind.Rabbit })
            {
                var got = Sim.DebugStew(kind);
                Cheat("stew", $"{kind} -> pot says {got}, StewOn(today) {Sim.StewOn(pv.Now.day)}");
                GameObject pot = null;
                foreach (float hr in new[] { 17.8f, 18.3f, 12.5f, 16.5f, 8f })
                {
                    PeopleView.DebugSetHour(hr);
                    yield return Settle(3f);
                    pot = GameObject.Find("Camp stew pot");
                    if (pot != null && pot.activeInHierarchy) { Log("pot at " + hr); break; }
                    pot = null;
                }
                Assert.IsNotNull(pot, "the camp's stew pot shows at a lit fire");
                Assert.AreEqual(kind, Sim.StewOn(pv.Now.day), "the pot makes " + kind + " today");
                var potAt = pot.transform.position + Vector3.up * 0.05f;
                Look(potAt, 0.2f, 45f);
                yield return Settle(1f);
                yield return Snap($"10_{kind.ToString().ToLowerInvariant()}_stew_mid");
                Look(potAt, 0.06f, 40f, Rig.yaw + 25f);
                yield return Settle(1.2f);
                yield return Snap($"10_{kind.ToString().ToLowerInvariant()}_stew_close");
                Note("10 stew", $"{kind}: the pot shows at {pv.Now.hour:0.0} h, StewOn says {Sim.StewOn(pv.Now.day)}");
            }
            AssertNoErrors("the stews");
        }

        // ================================================================ 11. a touch choice with its options marked, answered by touch (#706)
        static CardStackView Cards => CardStackView.Instance;
        static CardStackModel Model => Cards.Model;
        static ChoiceSceneView Choice => ChoiceSceneView.Instance;

        IEnumerator TapCard(string name)
        {
            var e = Cards.Root.Q(name);
            Assert.IsNotNull(e, name);
            Assert.IsTrue(Shown(e), name + " is on screen");
            yield return TapAt(GameUiPanel.ScreenCenter(e));
            yield return Frames(4);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Beat11_ATouchChoiceWithItsOptionsMarked_AnsweredByTouch()
        {
            ChoiceSceneView.DebugWatch = false;
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 7531);
            GameUiPanel.AcceptInputUnfocused(true);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            Assert.IsNotNull(Choice, "the choice scene is bootstrapped with the box");
            Dilemma d = null;
            for (int i = 0; i < 4 && d == null; i++)
            {
                d = Sim.DebugRaiseDilemma("camp_travellers");
                if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; }
            }
            Assert.IsNotNull(d, "the newcomers' choice can be raised");
            Cheat("raise the choice", d.defId + ", the Keeper away a day");
            GameClock.DebugAway(24);
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Peek, Model.Mode, "the peek");
            yield return TapCard("cards-peek");
            for (int guard = 0; guard < 10 && Model.Face.kind != CardKind.Dilemma; guard++) yield return TapCard("cards-next");
            Assert.AreEqual(CardKind.Dilemma, Model.Face.kind);
            yield return Frames(6);
            yield return Snap("11a_the_choice_card");
            yield return TapCard("cards-showme");
            Assert.AreEqual(ChoiceSceneView.Phase.Waiting, Choice.State, "Show me stages the choice scene");
            var pv = Pv;
            yield return Until(() => Choice.Stage == null || Choice.Stage.Pins().All(p => pv.TryGetDollPosition(p.who, out var at) && (at - new Vector2(p.x, p.z)).magnitude < 0.06f), 45f);
            yield return Settle(1.2f);
            Assert.IsNotNull(Choice.Stage);
            Assert.AreEqual("Touch one: take them in, or camp apart.", Choice.AskLine);
            yield return Until(() => Choice.TagAt(0, out _) && Choice.TagAt(1, out _), 10f);
            var expect = new[] { "take them in", "camp apart" };
            for (int i = 0; i < 2; i++)
            {
                var tag = Choice.UiRoot.Q("choice-tag-" + i);
                Assert.IsTrue(Shown(tag), "tag " + i + " is up");
                Assert.AreEqual(expect[Choice.Stage.sides[i].option], tag.Q<Label>().text, "tag " + i + " says what touching it does");
                Assert.Greater(Choice.HaloAlpha(i), 0.5f, "halo " + i);
            }
            yield return Snap("11b_options_marked_touch_one");
            Assert.IsTrue(Choice.TryScreenOf(0, out var touchAt));
            yield return TapAt(touchAt);
            yield return Frames(3);
            Assert.AreEqual(DilemmaState.Answered, d.state, "a real touch answers");
            Assert.AreEqual(DilemmaPath.Box, d.path);
            yield return Until(() => !Shown(Choice.UiRoot.Q("choice-tag-0")) && !Shown(Choice.UiRoot.Q("choice-tag-1")), 3f);
            Assert.IsFalse(Shown(Choice.UiRoot.Q("choice-tag-0")) || Shown(Choice.UiRoot.Q("choice-tag-1")), "the tags go once it is answered");
            yield return Settle(1f);
            yield return Snap("11c_answered_the_marks_go");
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, ChoiceScenes.ReactSeconds + 6f);
            Note("11 choice", $"answered by touch on option 0 ('{expect[Choice.Stage != null ? 0 : 0]}'); bar said: Touch one: take them in, or camp apart.");
            AssertNoErrors("a marked choice");
        }

        // ================================================================ 12a. a village house card's year (#689)
        [UnityTest, Timeout(900000)]
        public IEnumerator Beat12a_AVillageHouseCardSaysItsYear()
        {
            yield return OpenSlice(seed: 1234);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(14f);
            yield return Frames(3);
            Sim.CleanAll();
            PeopleView.DebugSetHour(23.5f); // indoors: nobody between the finger and the roof
            yield return Settle(0.5f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            var cards = PersonCardView.Instance;
            var cam = Camera.main; var box = Box;
            int id = -1; Vector2 at = default;
            foreach (var l in Sim.Settlement.Lots.Where(x => !x.retired && x.buildingId > 0 && x.kind != LotKind.Farm))
            {
                var p = (Vector2)cam.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(l.x, box.GroundHeight(l.x, l.z) + 0.2f, l.z)));
                if (p.x < 40f || p.x > Screen.width - 40f || p.y < Screen.height * 0.25f || p.y > Screen.height * 0.8f) continue;
                int b = cards.PickBuilding(p);
                if (b > 0 && cards.PickPerson(p).IsNone && Sim.BuildingCardOf(b).title == "A house") { id = b; at = p; break; }
            }
            Assert.Greater(id, 0, "a house in the clear");
            yield return Snap("12a_village_by_night");
            yield return TapAt(at);
            yield return Frames(4);
            Assert.AreEqual(id, cards.BuildingId, "a tap opens its card");
            yield return Settle(0.8f);
            var year = Hud.Root.panel.visualTree.Q<Label>("building-card-year");
            Assert.IsNotNull(year);
            var expected = Sim.BuildingCardOf(id);
            Note("12a house", $"the card says '{year.text}' (sim: year {expected.year}, foundedMonth {Sim.State.buildings.First(b => b.id == id).foundedMonth})");
            Assert.AreEqual(expected.yearLine, year.text);
            StringAssert.StartsWith("Built in year", year.text);
            yield return Snap("12a_house_card_year");
            yield return TapAt(GameUiPanel.ScreenCenter(Hud.Root.panel.visualTree, "building-card-close"));
            yield return Frames(3);
            AssertNoErrors("the house card");
            CloseSlice();
        }

        // ================================================================ 12b. the deer jar's need line among scattered trees (#690)
        [UnityTest, Timeout(900000)]
        public IEnumerator Beat12b_TheDeerJarSaysItWantsAThickerWood()
        {
            yield return LivedInCamp(10f, pond: false, wood: false);
            Cheat("rungs placed, then every jar shut", $"thicket {Sim.DebugPlaceNearCamp(LifeSpecies.Thicket)}, rabbits {Sim.DebugPlaceNearCamp(LifeSpecies.Rabbits)}");
            Cheat("a Keeper's tree and a few wild trees", EcoDebug.PlantHere() + " / " + EcoDebug.TreeStage(4) + " / " + EcoDebug.WildTrees(false));
            Cheat("lock jars", $"{Sim.DebugLockJars()} jars shut");
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 30f);
            yield return Calm();
            Note("12b deer jar", $"wood {Sim.EcoWoodTrees} trees in company; jar {Sim.JarStateOf(LifeSpecies.Deer)}; need '{Sim.JarNeed(LifeSpecies.Deer)}'");
            Look(World(Sim.EcoGround.campX, Sim.EcoGround.campZ), 1.2f, 50f);
            var hud = Hud;
            yield return CalmHud();
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return Settle(0.6f);
            // #668: no jar to open first: the tiles are on the kit's shelves
            yield return Settle(0.5f);
            Assert.IsTrue(hud.LifeRowOpen, "the chooser opens on the shut jars");
            var icon = hud.Root.Q("kit-life-locked-deer");
            Assert.IsNotNull(icon, "the deer are greyed");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-locked-deer"));
            yield return Settle(0.5f);
            Note("12b deer jar", "the note says: '" + hud.LifeNoteText + "'");
            StringAssert.Contains("thicker wood", hud.LifeNoteText, "the deer jar names the wood it wants");
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand);
            yield return Snap("12b_deer_jar_need_line_scattered_trees");
            AssertNoErrors("the deer jar");
        }

        // ================================================================ 12c. Skip all clears the History dot (#561)
        [UnityTest, Timeout(900000)]
        public IEnumerator Beat12c_SkipAllClearsTheHistoryDot()
        {
            yield return OpenSlice(1179, 2556, seed: 7531);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            GameClock.DebugAway(24);
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Peek, Model.Mode, "a return shows the peek");
            yield return Snap("12c_the_peek");
            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f)); // the peek goes: the cards wait
            yield return Frames(4);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode);
            Assert.IsTrue(Model.HistoryDot, "cards wait: a dot");
            var dot = Hud.Root.Q("hud-history-dot");
            Assert.IsTrue(Shown(dot), "a dot, never a number");
            yield return Snap("12c_the_dot_waits");
            yield return TapAt(GameUiPanel.ScreenCenter(Hud.Root, "hud-history-dot"));
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Open, Model.Mode, "the Stories chip opens what's waiting");
            yield return TapCard("cards-skip");
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode);
            yield return Frames(3);
            Assert.IsFalse(Model.HistoryDot, "Skip all answers the stack");
            Assert.IsFalse(Shown(dot), "the dot is gone from the screen");
            yield return Snap("12c_skip_all_no_dot");
            yield return TapAt(GameUiPanel.ScreenCenter(Hud.Root, "hud-history"));
            var tl = TimeLapseView.Find();
            yield return Until(() => tl != null && tl.Active, 3f);
            Assert.IsTrue(tl != null && tl.Active, "History opens the time-lapse, not the stack");
            Note("12c skip all", "Skip all cleared the dot; History opened the time-lapse");
            AssertNoErrors("skip all");
        }
    }
}
