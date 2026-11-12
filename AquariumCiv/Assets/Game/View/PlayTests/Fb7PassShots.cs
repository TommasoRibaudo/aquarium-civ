using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Feedback batch 7 Editor pass: shots for the eye (#604 HUD ink, #609 seed pad, #603 spring start, #607 tent at night).
    /// Writes to Temp/agent/fb7/. Run one [Category] at a time.</summary>
    [Explicit("Screenshots for the eye: run by hand")]
    public class Fb7PassShots : PlayTestBase
    {
        const string Dir = "fb7/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => Object.FindAnyObjectByType<BoxView>();
        readonly StringBuilder log = new();
        public override void Setup() { base.Setup(); Directory.CreateDirectory(Shots + Dir); }
        void Log(string s) { log.AppendLine(s); Debug.Log("[fb7] " + s); }
        void WriteLog(string name) { Directory.CreateDirectory(Shots + Dir); File.WriteAllText(Shots + Dir + name + ".txt", log.ToString()); }

        IEnumerator Camp(int w, int h, int seed = 4242)
        {
            GameClock.EcologyPref = true;
            yield return OpenSlice(w, h, seed: seed);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(12f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            yield return new WaitForSecondsRealtime(1f);
        }

        static string YearText()
        {
            var hud = GameHud.Instance;
            var f = typeof(GameHud).GetField("yearLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return hud == null || f == null ? "?" : ((Label)f.GetValue(hud))?.text;
        }

        static IEnumerator ToSeason(Season s)
        {
            for (int i = 0; i < 4 && LifeClock.SeasonOf(Sim.State.month) != s; i++) EcoDebug.SkipToNextSeason();
            yield return new WaitForSecondsRealtime(1.2f);
        }

        IEnumerator At(float hour, string name)
        {
            PeopleView.DebugSetHour(hour);
            yield return new WaitForSecondsRealtime(1.8f); // the ink eases over a second
            Log($"{name}: hour {hour}, daylight {HudInk.Daylight:0.00}, season {LifeClock.SeasonOf(Sim.State.month)}, offHome {Rig.OffHome}, title '{YearText()}'");
            yield return Shot(Dir + name);
        }

        // ---------------------------------------------------------------- #604
        [UnityTest, Timeout(600000), Category("Fb7Hud")]
        public IEnumerator HudInkShots()
        {
            foreach (var (w, h) in new[] { (1179, 2556), (750, 1334) })
            {
                yield return Camp(w, h);
                var rig = Rig; var home = rig.Home;
                yield return ToSeason(Season.Summer);
                rig.EaseTo(home.target, home.yaw, home.pitch, home.zoom, 0.2f);
                string p = $"604_{w}_";
                yield return At(12f, p + "noon");
                yield return At(18f, p + "dusk18");
                yield return At(19.5f, p + "dusk19h30");
                yield return At(22f, p + "night");
                // off home: pulled back and slid aside, so the room fills the top of the screen
                rig.EaseTo(home.target + new Vector3(1.2f, 0.6f, 0f), home.yaw + 20f, home.pitch - 8f, 0.4f, 0.3f); // closer than home (zoom < home) and aside
                yield return new WaitForSecondsRealtime(0.8f);
                yield return At(12f, p + "offhome_noon");
                yield return At(22f, p + "offhome_night");
                rig.EaseTo(home.target, home.yaw, home.pitch, home.zoom, 0.3f);
                yield return ToSeason(Season.Winter);
                yield return At(12f, p + "winter_noon");
                yield return At(22f, p + "winter_night");
                CloseSlice();
            }
            WriteLog("604");
        }

        // ---------------------------------------------------------------- #609
        [UnityTest, Timeout(600000), Category("Fb7Seed")]
        public IEnumerator SeedPadShots()
        {
            yield return Camp(1179, 2556);
            var box = Box; var land = box.land;
            EcoDebug.SeedInHand();
            yield return Frames(5);
            int flat = -1, steep = -1; float flatS = float.MaxValue, steepS = -1f;
            for (int c = 0; c < SiteMap.N; c++)
            {
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                if (Sim.TreeCellFor(x, z) != c) continue;
                const float s = KeeperTreeKit.PadSpan;
                var n = KeeperTreeKit.PadNormal(land.Height(x + s, z), land.Height(x - s, z), land.Height(x, z + s), land.Height(x, z - s), s);
                float deg = Vector3.Angle(Vector3.up, n);
                if (deg > steepS) { steepS = deg; steep = c; }
                if (deg < flatS) { flatS = deg; flat = c; }
            }
            Log($"plantable: flat cell {flat} ({flatS:0.0} deg), steepest {steep} ({steepS:0.0} deg)");
            Assert.GreaterOrEqual(steep, 0, "a plantable cell");
            var sp = SeedPlaceView.Instance;
            foreach (var (cell, tag) in new[] { (steep, "steep"), (flat, "flat") })
            {
                float x = SiteMap.CXc(cell), z = SiteMap.CZc(cell);
                var world = box.transform.TransformPoint(land.OnGround(x, z));
                foreach (float yaw in new[] { Rig.Home.yaw, -25f })
                {
                    Rig.EaseTo(world, yaw, 14f, 0.25f, 0.3f);
                    yield return new WaitForSecondsRealtime(1.2f);
                    if (Sim.State.eco.treeCell < 0)
                    {
                        // the ghost first: tap the cell (the first tap only shows it)
                        if (!sp.GhostShown) sp.TapAt(Sim, sp.ScreenOf(cell)); // a second tap on the ghost would plant it
                        yield return new WaitForSecondsRealtime(0.8f);
                        Log($"{tag} ghost yaw {yaw:0}: {sp.Last} shown {sp.GhostShown}");
                        yield return Shot($"{Dir}609_{tag}_ghost_y{yaw:0}");
                    }
                }
                if (Sim.State.eco.treeCell < 0)
                {
                    bool ok = Sim.PlantTree(x, z); box.Refresh();
                    Log($"{tag} planted {ok} at cell {Sim.State.eco.treeCell}");
                }
                foreach (float yaw in new[] { Rig.Home.yaw, -25f, 50f })
                {
                    Rig.EaseTo(world, yaw, 10f, 0.25f, 0.3f);
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Shot($"{Dir}609_{tag}_planted_y{yaw:0}");
                }
                if (tag == "steep")
                {
                    // the flat one needs the seed back: a fresh box
                    CloseSlice();
                    yield return Camp(1179, 2556);
                    box = Box; land = box.land; sp = SeedPlaceView.Instance;
                    EcoDebug.SeedInHand();
                    yield return Frames(5);
                }
            }
            CloseSlice();
            WriteLog("609");
        }

        // ---------------------------------------------------------------- #603
        [UnityTest, Timeout(600000), Category("Fb7Spring")]
        public IEnumerator SpringStartShots()
        {
            GameClock.EcologyPref = true;
            yield return OpenSlice(1179, 2556, seed: 4242, skipFirstRun: false);
            GameUiPanel.AcceptInputUnfocused(true);
            var fr = FirstRunView.Instance;
            Log($"fresh box: month {Sim.State.month}, shift {Sim.State.seasonShift}, season {LifeClock.SeasonOf(Sim.State.month)}, first run {(fr != null && fr.Active)}, title '{YearText()}', SeasonLook snow? see shot");
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot(Dir + "603_firstrun_start");
            if (fr != null && fr.Active)
            {
                fr.Run.DebugSkipTo(FirstRunStep.Name);
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot(Dir + "603_firstrun_name");
                FirstRunView.DebugSkipAll();
            }
            yield return new WaitForSecondsRealtime(3f);
            Log($"after the first run: month {Sim.State.month}, season {LifeClock.SeasonOf(Sim.State.month)}, title '{YearText()}'");
            yield return Shot(Dir + "603_after_firstrun");
            Rig.GoWholeBox(0.3f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot(Dir + "603_after_firstrun_box");
            StringAssert.Contains("Spring", YearText(), "the HUD reads spring after the first run");
            CloseSlice();
            WriteLog("603");
        }

        // ---------------------------------------------------------------- #607
        [UnityTest, Timeout(900000), Category("Fb7Tent")]
        public IEnumerator TentAtNight()
        {
            yield return Camp(1179, 2556);
            var pv = PeopleView.Find();
            int adults = Sim.State.notables.Count(n => n.alive);
            Log($"camp: era {Sim.State.era}, notables {adults}, pop {Sim.State.population}");
            PeopleView.DebugSetHour(19f);
            Rig.FlyToPlace(Place.Land(PlaceKind.Fire), 0.35f, 0.3f);
            yield return new WaitForSecondsRealtime(1.5f);
            pv.timeScale = 4f; // 5 view hours in about 20 s
            var asleep = new Dictionary<string, float>();  // who -> view hour they went to sleep
            var woke = new List<string>();
            bool birthDone = false, monthDone = false; int shot = 0; float nextShot = 19.5f;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 120f)
            {
                float hr = pv.Now.hour;
                if (hr < 12f) hr += 24f; // past midnight
                foreach (var a in pv.Activities)
                {
                    if (a.kind == ActivityKind.None) continue;
                    string k = a.who.IsNotable ? "n" + a.who.notableId : a.who.Key.ToString();
                    if (a.kind == ActivityKind.Sleep && !a.moving) { if (!asleep.ContainsKey(k)) { asleep[k] = hr; Log($"{hr:00.00} {k} asleep at {a.place.kind}"); } }
                    else if (asleep.ContainsKey(k))
                    {
                        woke.Add($"{hr:00.00} {k} up again: {a.kind} moving {a.moving} -> {a.place.kind} (asleep since {asleep[k]:00.00})");
                        Log(woke[^1]);
                        asleep.Remove(k);
                    }
                }
                if (!birthDone && hr >= 21.3f)
                {
                    birthDone = true;
                    var n = Sim.DebugBirth();
                    Log($"{hr:00.00} REPLAN 1: a person added ({(n != null ? n.name : "none")}); asleep now {asleep.Count}");
                }
                if (!monthDone && hr >= 22.6f)
                {
                    monthDone = true;
                    long m = Sim.State.month;
                    Sim.Advance(Sim.Config.StepHours + 1e-6);
                    Log($"{hr:00.00} REPLAN 2: month {m} -> {Sim.State.month}; asleep now {asleep.Count}");
                }
                if (hr >= nextShot) { yield return Shot($"{Dir}607_{shot++:00}_{hr:00.0}h"); nextShot += 0.75f; }
                if (hr >= 24.5f) break;
                yield return null;
            }
            pv.timeScale = 1f;
            Log($"end: asleep {asleep.Count} of {pv.Activities.Count(a => a.kind != ActivityKind.None)}; woke during the night: {woke.Count}");
            WriteLog("607");
            CloseSlice();
            Assert.IsEmpty(woke, "nobody gets up again once asleep: " + string.Join(" | ", woke.Take(6)));
        }
    }
}
