using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Feedback batch 5 editor pass: the beats a screenshot has to show (#573 woods spot / chop logs, #574 hunts and eggs, #569 mist one
    /// half). <see cref="Stage"/>, <see cref="Look"/>, <see cref="Find"/> and <see cref="Snap"/> are public so the walkthrough can reuse them.
    /// Shots (1179x2556 PNG) to Temp/agent/fb5/pass/. Explicit.</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("Fb5Pass")]
    public class Fb5PassShots : PlayTestBase
    {
        public static string Dir = "F:/GameDev/AquariumCiv/Temp/agent/fb5/pass/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        /// <summary>A living-land camp with people and rabbits, optionally with a pond; the hour is set.</summary>
        public IEnumerator Stage(int seed = 4242, int grownUps = 0, bool pond = false, float hour = 12f, int w = 1179, int h = 2556)
        {
            GameClock.EcologyPref = true;
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(w, h, seed: seed);
            GameUiPanel.AcceptInputUnfocused(true);
            PeopleView.DebugSetHour(hour);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            for (int k = 0; k < 6; k++) foreach (var (x, z) in new[] { (-2.4f, 1.2f), (0f, 1.4f), (2.4f, -1f), (0.5f, -1.6f), (-0.5f, 0.2f), (2f, 1.5f) }) EcoDebug.MistBig(x, z);
            EcoDebug.AddRabbits(4);
            int adults = Sim.State.notables.Count(n => n.alive && n.age >= 16f);
            for (int i = adults; i < grownUps; i++) if (Sim.DebugBirth() == null) break;
            EcoDebug.SkipSeasons(4);
            if (pond) Debug.Log("[fb5pass] " + EcoDebug.WaterBreaksNow());
            // the season skips leave staged moments (a wedding, a new chief) holding the adults: stop them until none comes for 2 s
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            if (pond) yield return Until(() => FirstWaterView.Instance == null || !FirstWaterView.Instance.Playing, 90f);
            PeopleView.DebugSetHour(hour);
            yield return Frames(30);
            var rv = RabbitView.Instance;
            if (rv != null) yield return Until(() => !rv.SignPlaying && rv.Shown >= 3, 15f);
            Debug.Log($"[fb5pass] staged: era {Sim.State.era}, people {Sim.State.notables.Count(n => n.alive)}, rabbits {Sim.EcoRabbitCount} shown {(rv != null ? rv.Shown : -1)}, meadow {Sim.EcoMeadowShare:0.00}");
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>Camera on a box-local spot.</summary>
        public static void Look(float x, float z, float zoom = 0.4f, float pitch = 48f)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            var rig = Rig;
            rig.CancelFlight();
            rig.target = box.transform.TransformPoint(box.land.OnGround(x, z)); rig.zoom = zoom; rig.pitch = pitch;
            rig.Apply();
        }

        public static IEnumerator Snap(string name)
        {
            Directory.CreateDirectory(Dir);
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Dir + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        /// <summary>The first doll showing an activity that matches, with its box-local spot.</summary>
        public static bool Find(Func<PersonActivity, bool> pred, out PersonActivity act, out Vector2 at)
        {
            var pv = PeopleView.Find();
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++)
                if (pv.TryGetActivityOf(who[i], out var a) && pred(a)) { act = a; at = new Vector2(xs[i], zs[i]); return true; }
            act = default; at = default;
            return false;
        }

        static string Census()
        {
            var pv = PeopleView.Find();
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            var parts = new List<string>();
            for (int i = 0; i < who.Count; i++)
                if (pv.TryGetActivityOf(who[i], out var a)) parts.Add($"{a.kind}/{a.prop}{(a.moving ? ">" : "")}@{xs[i]:0.0},{zs[i]:0.0}");
            return string.Join(" ", parts);
        }

        /// <summary>Everyone grown (not retired) takes the axe, as the Life tests do (AllWoodcutters), and the planner replans.</summary>
        public static void AllWoodcutters()
        {
            foreach (var n in Sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < Sim.Config.retireAge)) Sim.DebugSetJob(n, JobType.Woodcutter);
            PeopleView.Find().Planner.Invalidate();
        }

        static Vector2 KeeperTree()
        {
            var e = Sim.State.eco;
            return e.treeCell < 0 ? Vector2.zero : new Vector2(SiteMap.CXc(e.treeCell), SiteMap.CZc(e.treeCell));
        }

        /// <summary>Waits real seconds in steps of 0.25 s; true when the predicate held (shot-ready at that instant).</summary>
        static IEnumerator Until2(Func<bool> done, float seconds)
        {
            float t = 0f;
            while (t < seconds && !done()) { t += 0.25f; yield return new WaitForSecondsRealtime(0.25f); }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator WoodsBeside_NobodyChopsByTheKeepersTree()
        {
            yield return Stage(hour: 8f);
            Debug.Log("[fb5pass] " + EcoDebug.WildTrees(true));
            AllWoodcutters();
            var kt = KeeperTree();
            var tl = new List<string>();
            for (int c = 0; c < EcoLand.N; c++) { var cv = (EcoCover)Sim.State.eco.cover[c]; if (cv == EcoCover.Tree || cv == EcoCover.Young) tl.Add($"{cv}{c}@{SiteMap.CXc(c):0.0},{SiteMap.CZc(c):0.0}"); }
            Debug.Log($"[fb5pass] beside: Keeper's tree {kt}, trees {string.Join(" ", tl)}");
            var bv0 = SimLayoutSource.Find().GetComponent<BoxView>();
            for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)Sim.State.eco.cover[c] == EcoCover.Tree) { bool drawn; lock (bv0.TreeSpots) drawn = bv0.TreeSpots.ContainsKey(c); Debug.Log($"[fb5pass] beside: grown cell {c} drawn {drawn} dist {Vector2.Distance(new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)), kt):0.00}"); }
            int choppersNear = 0, choppers = 0;
            for (float hr = 8f; hr <= 17f; hr += 0.5f)
            {
                PeopleView.DebugSetHour(hr);
                yield return new WaitForSecondsRealtime(1.2f);
                var pv = PeopleView.Find();
                var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
                pv.StandingNow(who, xs, zs);
                for (int i = 0; i < who.Count; i++)
                    if (pv.TryGetActivityOf(who[i], out var a) && a.kind == ActivityKind.Chop)
                    {
                        choppers++;
                        float d = Vector2.Distance(new Vector2(xs[i], zs[i]), kt);
                        if (d < 0.9f) choppersNear++;
                        Debug.Log($"[fb5pass] beside: chopper at hour {hr:0.0} {d:0.00} u from the Keeper's tree");
                    }
            }
            Debug.Log($"[fb5pass] beside: {choppers} chopper samples, {choppersNear} within 0.9 u of the Keeper's tree");
            Look(kt.x, kt.y, 0.4f, 50f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Snap("573_beside_keepers_tree");
            Assert.AreEqual(0, choppersNear, "nobody chops beside the Keeper's tree");
            CloseSlice();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator WoodsAway_ChipsLogsAndTheCarry()
        {
            yield return Stage(hour: 8f);
            Debug.Log("[fb5pass] " + EcoDebug.WildTrees(false));
            AllWoodcutters();
            var pvw = PeopleView.Find();
            var trees = new List<string>();
            for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)Sim.State.eco.cover[c] == EcoCover.Tree) trees.Add($"{c}@{SiteMap.CXc(c):0.0},{SiteMap.CZc(c):0.0}");
            Debug.Log($"[fb5pass] away: Keeper's tree {KeeperTree()}, tree cells {string.Join(" ", trees)}");
            if (pvw.Planner.Positions.TryGetPosition(Place.Land(PlaceKind.Woods), out float wx, out float wz))
            {
                Debug.Log($"[fb5pass] away: woods spot {wx:0.00},{wz:0.00}");
                Look(wx, wz, 0.35f, 46f);
                yield return new WaitForSecondsRealtime(1f);
                yield return Snap("573_woods_spot");
            }
            bool chop = false, carry = false;
            for (float hr = 8f; hr <= 17f && !(chop && carry); hr += 0.25f)
            {
                PeopleView.DebugSetHour(hr);
                yield return new WaitForSecondsRealtime(1.0f);
                if (!chop && Find(a => a.kind == ActivityKind.Chop && !a.moving, out var ca, out var cat))
                {
                    chop = true;
                    Debug.Log($"[fb5pass] chop at hour {hr:0.00} {cat}");
                    for (int k = 0; k < 4; k++)
                    {
                        if (Find(a => a.kind == ActivityKind.Chop, out _, out cat)) Look(cat.x, cat.y, 0.22f, 40f);
                        yield return new WaitForSecondsRealtime(1.2f);
                        yield return Snap("573_chop_" + k);
                    }
                }
                else if (chop && !carry && Find(a => a.kind == ActivityKind.Carry && a.prop == Prop.Log, out _, out var rat))
                {
                    carry = true;
                    Debug.Log($"[fb5pass] carry at hour {hr:0.00} {rat}");
                    Look(rat.x, rat.y, 0.3f, 44f);
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Snap("573_carry");
                }
            }
            Debug.Log("[fb5pass] away: chop " + chop + " carry " + carry + " census " + Census());
            Assert.IsTrue(chop, "someone chopped at the wild tree");
            CloseSlice();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Hunt_OnScreen()
        {
            yield return Stage(hour: 7.5f);
            Debug.Log("[fb5pass] " + EcoDebug.MakeHunter());
            var pv = PeopleView.Find();
            HuntPlan plan = null;
            for (int t = 0; t < 40 && plan == null; t++) { if (!pv.Planner.DebugStartHunt(pv.Now, out plan)) { plan = null; yield return Frames(15); } }
            Assert.IsNotNull(plan, "a hunt starts (a rabbit shown, a hunter free)");
            var who = plan.hunter;
            Debug.Log($"[fb5pass] hunt: hunter {who.Key}, rabbit {plan.rabbitId}, catches {plan.catches}");
            pv.timeScale = 4f;
            var seen = new HashSet<HuntPhase>(); var shot = new HashSet<HuntPhase>();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 240f)
            {
                yield return null;
                PersonActivity a = default;
                foreach (var q in pv.Activities) if (q.who == who) a = q;
                if (a.hunt == HuntPhase.None && seen.Contains(HuntPhase.Return)) break;
                if (a.hunt != HuntPhase.None) seen.Add(a.hunt);
                if (a.hunt == HuntPhase.Stalk) pv.timeScale = 1f;
                if (a.hunt == HuntPhase.Strike) pv.timeScale = 0.5f;
                if (a.hunt == HuntPhase.CarryBack) pv.timeScale = 1f;
                bool want = (a.hunt == HuntPhase.WalkOut && a.progress > 0.5f) || (a.hunt == HuntPhase.Stalk && a.progress > 0.5f) || (a.hunt == HuntPhase.Strike && a.progress > 0.55f) || (a.hunt == HuntPhase.CarryBack && a.progress > 0.4f);
                if (want && shot.Add(a.hunt))
                {
                    float was = pv.timeScale; pv.timeScale = 0f;
                    if (Find(x => x.who == who, out _, out var at)) Look(at.x, at.y, 0.3f, 46f);
                    yield return Frames(8);
                    yield return Snap($"574_hunt_{a.hunt}");
                    Rig.zoom = 1f;
                    pv.timeScale = was;
                }
                if (a.hunt is HuntPhase.Skin or HuntPhase.Cook or HuntPhase.Return or HuntPhase.WalkBack) pv.timeScale = 6f;
            }
            pv.timeScale = 1f;
            Debug.Log("[fb5pass] hunt phases " + string.Join(",", seen) + "; shot " + string.Join(",", shot));
            Assert.IsTrue(seen.Contains(HuntPhase.Stalk) && seen.Contains(HuntPhase.Strike), "a hunt on screen");
            CloseSlice();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Eggs_KneelingAtThePondAndTheCarry()
        {
            yield return Stage(hour: 10f, pond: true);
            Debug.Log("[fb5pass] pond cells " + Sim.EcoPondCells);
            // ducks above the floor, or the cheat refuses (an egg is never taken at the floor): place the pair, let the flock grow
            Debug.Log("[fb5pass] ducks placed at cell " + Sim.DebugPlaceNearCamp(LifeSpecies.Ducks));
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            EcoDebug.SkipSeasons(8);
            Debug.Log($"[fb5pass] ducks {Sim.EcoDucks}");
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.DebugSetHour(10f);
            yield return Frames(30);
            var pv = PeopleView.Find();
            Assert.Greater(Sim.DebugEggsToNests(2), 0, "eggs in the nests");
            EggPlan eplan = null;
            for (int t = 0; t < 40 && eplan == null; t++) { if (!pv.Planner.DebugStartEggGather(pv.Now, out eplan)) { eplan = null; yield return Frames(15); } }
            Assert.IsNotNull(eplan, "an egg gather started (a grown-up free, ducks, open water)");
            Debug.Log($"[fb5pass] egg plan: person {eplan.key}, from {eplan.from}, resolve {eplan.resolveHour:0.00}, now {pv.Now.hour:0.00}");
            pv.timeScale = 3f;
            bool kneel = false, carry = false;
            for (int i = 0; i < 400 && !carry; i++)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                PersonActivity a = default;
                foreach (var q in pv.Activities) if (q.who.Key == eplan.key) a = q;
                if (!kneel && a.kind == ActivityKind.Gather && a.prop == Prop.Basket && !a.moving)
                {
                    kneel = true; pv.timeScale = 0.6f;
                    if (Find(x => x.who.Key == eplan.key, out _, out var gat)) { float wd = float.MaxValue; for (int c = 0; c < EcoLand.N; c++) if (Sim.EcoCoverAt(c) == EcoCover.Water) wd = Mathf.Min(wd, Vector2.Distance(new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)), gat));
                        Debug.Log($"[fb5pass] kneel at {gat}, {wd:0.00} u from the water"); Look(gat.x, gat.y, 0.25f, 40f); }
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Snap("574_egg_kneel");
                    Rig.zoom = 1f;
                }
                else if (kneel && a.kind == ActivityKind.Carry && a.prop == Prop.Basket)
                {
                    carry = true;
                    if (Find(x => x.who.Key == eplan.key, out _, out var cat)) { Debug.Log("[fb5pass] carry at " + cat); Look(cat.x, cat.y, 0.3f, 44f); }
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return Snap("574_egg_carry");
                }
                if (a.kind == ActivityKind.TipLoad || a.kind == ActivityKind.None && kneel) break;
            }
            pv.timeScale = 1f;
            Debug.Log("[fb5pass] eggs: kneel " + kneel + " carry " + carry + " census " + Census());
            Assert.IsTrue(kneel, "the gatherer kneels at the pond");
            CloseSlice();
        }

        /// <summary>#569 staging: a young box with the same seedlings on both halves (mirrored about x = 0, on bare land), so the two halves differ only in the mist.</summary>
        public static int PlantSeedlingPairs(int pairs)
        {
            var e = Sim.State.eco; var g = Sim.EcoGround; int made = 0;
            foreach (float z in new[] { -1.3f, 1.2f, -0.6f, 0.7f, 0.1f, -1.5f, 1.45f, -0.2f, -1.0f, 0.4f })
            {
                if (made >= pairs * 2) break;
                foreach (float x in new[] { 1.6f, 0.9f, 2.0f, 1.3f })
                {
                    int a = SiteMap.CellAt(-x, z), b = SiteMap.CellAt(x, z);
                    if (a < 0 || b < 0 || e.cover[a] != (byte)EcoCover.Bare || e.cover[b] != (byte)EcoCover.Bare || g.InBasin(a) || g.InBasin(b) || g.yard[a] || g.yard[b]) continue;
                    e.cover[a] = e.cover[b] = (byte)EcoCover.Seedling; made += 2; break;
                }
            }
            return made;
        }

        /// <summary>#569: per half (x &lt; 0 misted, x &gt;= 0 control) the land cells by cover.</summary>
        static string HalfCounts()
        {
            var e = Sim.State.eco; var n = new int[2, 9];
            for (int c = 0; c < EcoLand.N; c++) n[SiteMap.CXc(c) < 0f ? 0 : 1, Mathf.Clamp(e.cover[c], 0, 8)]++;
            string One(int h) { var sb = new System.Text.StringBuilder(); for (int k = 0; k < 9; k++) sb.Append((EcoCover)k).Append(' ').Append(n[h, k]).Append(k < 8 ? ", " : ""); return sb.ToString(); }
            return "MISTED [" + One(0) + "] CONTROL [" + One(1) + "]";
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator MistHalf_GreenerWithMoreYoungTrees()
        {
            GameClock.EcologyPref = true;
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            PeopleView.DebugSetHour(12f);
            // a seed first, so there is something to spread: a few big mists on both sides, then the half run
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            Debug.Log("[fb5pass] " + EcoDebug.TreeStage(4)); // a grown Keeper's tree: the seed source the young trees come from
            Debug.Log("[fb5pass] seedlings planted: " + PlantSeedlingPairs(8));
            Debug.Log("[fb5pass] before: " + HalfCounts());
            var box0 = (target: new Vector3(0f, 1f, 0f), yaw: 28f, pitch: 34f, zoom: 1f);
            Rig.EaseTo(box0.target, box0.yaw, box0.pitch, box0.zoom, 0.3f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Snap("569_before");
            Debug.Log("[fb5pass] " + EcoDebug.MistHalf(12));
            Debug.Log("[fb5pass] after: " + HalfCounts());
            yield return Fb5SeasonShots.ToSeason(1, 2f); // the half-year to the middle of summer: snow would hide the green
            var e = Sim.State.eco; int left = 0, right = 0, leftGreen = 0, rightGreen = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                float x = SiteMap.CXc(c);
                var cv = (EcoCover)e.cover[c];
                bool young = cv == EcoCover.Young || cv == EcoCover.Tree || cv == EcoCover.Seedling;
                bool green = cv != EcoCover.Bare;
                if (x < 0f) { if (young) left++; if (green) leftGreen++; } else { if (young) right++; if (green) rightGreen++; }
            }
            Debug.Log($"[fb5pass] mist half: left young+trees {left}, right (control) {right}; left not-bare {leftGreen}, right {rightGreen}");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Snap("569_mist_half_default");
            var box = (target: new Vector3(0f, 1f, 0f), yaw: 28f, pitch: 34f, zoom: 1f);
            Rig.EaseTo(box.target, box.yaw, box.pitch, box.zoom, 0.3f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Snap("569_mist_half_box");
            Assert.Greater(leftGreen, rightGreen, "the misted half is greener");
            CloseSlice();
        }
    }
}
