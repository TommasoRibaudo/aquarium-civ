using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Tester run 1 (P8 polish) fixes, in the real Slice scene at 1179x2556: #294 the first tent still rises with its line when the
    /// camp gate opens at night; #295 the first-run mist shows a wet area and a little green at once and keeps spraying while the
    /// finger stays down after the step moves on; #296 no long frame on the first spray of a session or when the first puddle forms.
    /// Frame times go to Temp/agent/t1_frames.txt (Editor numbers; a phone runs IL2CPP with no JIT and its own shader cache).
    /// </summary>
    [Category("Tester1Fix")]
    public class Tester1FixPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        /// <summary>The longest frame allowed on a first spray or a first puddle (ms, Editor).</summary>
        const double FrameBudgetMs = 33.0;

        Vector2 ScreenOf(float x, float z)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z)));
        }

        static int WetShownNear(EcoSurface s, float x, float z, float r, float min)
        {
            int n = 0;
            for (int c = 0; c < AquariumCiv.Sim.Eco.EcoLand.N; c++)
            {
                float dx = AquariumCiv.Sim.Settle.SiteMap.CXc(c) - x, dz = AquariumCiv.Sim.Settle.SiteMap.CZc(c) - z;
                if (dx * dx + dz * dz <= r * r && s.WetShown(c) >= min) n++;
            }
            return n;
        }

        static void Report(string tag, List<double> ms)
        {
            double max = 0; int worst = -1; var slow = new StringBuilder();
            for (int i = 0; i < ms.Count; i++) { if (ms[i] > max) { max = ms[i]; worst = i; } if (ms[i] > 20) slow.Append($"#{i}:{ms[i]:0} "); }
            string line = $"[t1 frames] {tag}: {ms.Count} frames, worst {max:0.0} ms at frame {worst}; over 20 ms: {slow}";
            Debug.Log(line);
            Directory.CreateDirectory(Shots);
            File.AppendAllText(Shots + "t1_frames.txt", System.DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
        }

        static double Max(List<double> ms) { double m = 0; foreach (var v in ms) m = System.Math.Max(m, v); return m; }

        /// <summary>#295 + #296: the first-run mist, held by touch on the suggested spot.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator FirstRunMist_WetAndGreenAtOnce_SprayHoldsPastTheStep_NoLongFrame()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242, skipFirstRun: false);
                Assert.IsTrue(Sim.EcologyOn, "a barren box");
                var run = FirstRunView.Instance;
                Assert.IsNotNull(run);
                Debug.Log("[t1] " + FirstRunView.DebugSkipTo(FirstRunStep.Rain));
                yield return Until(() => run.Step == FirstRunStep.Rain, 5f);
                Assert.AreEqual(FirstRunStep.Rain, run.Step);
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot("t1_first_mist_before");
                var mist = EcoMistView.Instance; var surf = EcoSurface.Instance;
                Assert.IsNotNull(mist); Assert.IsNotNull(surf);
                Assert.IsTrue(mist.WarmedUp, "the spray was made and drawn once at load (#296)");
                var (rx, rz) = run.Run.SuggestedRainSpot;
                var at = ScreenOf(rx, rz);
                yield return new WaitForSecondsRealtime(0.5f); // the screenshot's own long frame is not the spray's

                int id = touchId++;
                var ms = new List<double>();
                BeginTouch(id, at, queueEventOnly: true);
                float t0 = Time.realtimeSinceStartup, last = t0;
                bool stepMoved = false, sprayAfterStep = false; float stepAt = -1f; int wetAt1s = -1, freshAt1s = -1;
                while (Time.realtimeSinceStartup - t0 < 3.0f)
                {
                    MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true);
                    yield return null;
                    float now = Time.realtimeSinceStartup;
                    ms.Add((now - last) * 1000.0); last = now;
                    if (ms[ms.Count - 1] > 20) Debug.Log($"[t1 slow] frame {ms.Count - 1} {ms[ms.Count - 1]:0.0} ms: mist {mist.LastUpdateMs:0.0}, surface build {surf.LastBuildMs:0.0}, box rebuild total {Object.FindAnyObjectByType<BoxView>().RebuildMsTotal:0.0}, people {PeopleView.Find()?.FrameMs:0.0}, step {run.Step}, gc {System.GC.CollectionCount(0)}");
                    if (!stepMoved && run.Step != FirstRunStep.Rain) { stepMoved = true; stepAt = now - t0; }
                    if (stepMoved && now - t0 > stepAt + 0.8f && now - t0 > 1.6f) sprayAfterStep |= mist.Spraying;
                    if (wetAt1s < 0 && now - t0 >= 1.0f) { wetAt1s = WetShownNear(surf, rx, rz, 0.6f, 0.3f); freshAt1s = surf.FreshCells + surf.ShootCells; }
                }
                Report("first-run mist (first spray of the session)", ms);
                bool sprayingAtEnd = mist.Spraying;
                yield return Shot("t1_first_mist_mid");
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                Debug.Log($"[t1] step moved at {stepAt:0.00}s; wet cells shown near the spot at 1 s {wetAt1s}, fresh+shoots {freshAt1s}; surf wet {surf.WetCells} fresh {surf.FreshCells} shoots {surf.ShootCells}");
                Assert.IsTrue(stepMoved, "the held finger misted and the run moved on");
                Assert.IsTrue(sprayAfterStep && sprayingAtEnd, "the spray keeps going while the finger stays down after the step moved on");
                Assert.GreaterOrEqual(wetAt1s, 20, "a visible wet area under the first mist within a second");
                Assert.Greater(freshAt1s, 0, "and a little green at once");
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.IsFalse(mist.Spraying, "the spray stops when the finger lifts");
                Assert.GreaterOrEqual(WetShownNear(surf, rx, rz, 0.6f, 0.25f), 15, "the wet area is still there a moment later");
                yield return Shot("t1_first_mist_after");
                foreach (var nm in new[] { "EcoWet", "EcoSheen", "EcoFresh", "EcoPuddles" })
                {
                    var go = GameObject.Find(nm);
                    if (go == null) { Debug.Log($"[t1 diag] {nm} missing"); continue; }
                    var r = go.GetComponent<MeshRenderer>(); var mf = go.GetComponent<MeshFilter>();
                    var m = r.sharedMaterial;
                    Debug.Log($"[t1 diag] {nm} active {go.activeInHierarchy} enabled {r.enabled} visible {r.isVisible} verts {(mf.sharedMesh != null ? mf.sharedMesh.vertexCount : -1)} bounds {r.bounds} layer {go.layer} mat {(m != null ? m.name + " " + m.shader.name + " q" + m.renderQueue + " supported " + m.shader.isSupported + " mode " + m.GetFloat("_Mode") + " src " + m.GetFloat("_SrcBlend") + " dst " + m.GetFloat("_DstBlend") : "null")} parent {go.transform.parent?.name} cam mask {Camera.main.cullingMask}");
                }
                Assert.Less(Max(ms), FrameBudgetMs, "no long frame on the first spray of the session");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        /// <summary>#296 + the misting look: a 6 s hold with the Kit mister until puddles form; shots of the spray and puddles at the
        /// default zoom.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator KitMister_FirstPuddles_NoLongFrame_Shots()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242);
                Assert.IsTrue(Sim.EcologyOn);
                var hud = GameHud.Instance;
                if (hud.CleanMode) hud.SetCleanMode(false);
                hud.SetHand(GameHud.KitTool.Mister);
                yield return Frames(30);
                var mist = EcoMistView.Instance; var surf = EcoSurface.Instance;
                var st = Sim.Settlement;
                float x = st.Map.greenX + 0.6f, z = st.Map.greenZ - 0.5f;
                var at = ScreenOf(x, z);
                Assert.AreEqual(0, surf.PuddleCount);
                int id = touchId++;
                var ms = new List<double>();
                BeginTouch(id, at, queueEventOnly: true);
                float t0 = Time.realtimeSinceStartup, last = t0; int firstPuddle = -1; bool shot = false;
                while (Time.realtimeSinceStartup - t0 < 6.5f)
                {
                    MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true);
                    yield return null;
                    float now = Time.realtimeSinceStartup;
                    ms.Add((now - last) * 1000.0); last = now;
                    if (firstPuddle < 0 && surf.PuddleCount > 0) firstPuddle = ms.Count;
                    if (!shot && now - t0 > 5.5f) { shot = true; yield return Shot("t1_spray_default"); last = Time.realtimeSinceStartup; }
                }
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                // frames around the first puddle and the start, not the one with a screenshot in it
                Report($"kit hold 6 s (first puddle at frame {firstPuddle})", ms);
                Assert.Greater(firstPuddle, 0, "a long hold left a puddle");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("t1_puddles_default");
                var rig = Camera.main.GetComponent<BoxCamera>();
                rig.FlyTo(Object.FindAnyObjectByType<BoxView>().transform.TransformPoint(Object.FindAnyObjectByType<BoxView>().land.OnGround(x, z)), Mathf.Max(rig.minZoom * 1.2f, 0.3f), 0.4f);
                yield return Until(() => !rig.IsFlying, 4f);
                yield return Frames(10);
                yield return Shot("t1_puddles_close");
                Assert.Less(Max(ms), FrameBudgetMs, "no long frame on the first spray or when the first puddles form");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        /// <summary>#294: the camp gate opens at 20 h; the first tent still rises and its builder says the line.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Camp_GateAtNight_FirstTentStillRisesWithItsLine()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242);
                Assert.IsTrue(Sim.EcologyOn);
                var cv = CampView.Instance;
                Assert.IsNotNull(cv);
                foreach (var (mx, mz) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(mx, mz);
                EcoDebug.SkipSeasons(1);
                EcoDebug.AddRabbits(4);
                EcoDebug.AddHides(3);
                PeopleView.DebugSetHour(20f);
                yield return Frames(30);
                EcoDebug.OpenCampGate();
                for (int i = 0; i < 14 && !Sim.FirstTentRaised; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
                Assert.IsTrue(Sim.FirstTentRaised, "the first tent rose from the hides");
                var pv = PeopleView.Find();
                Debug.Log($"[t1 camp] gate at hour {pv.ViewHour:0.0}");
                float t = Time.realtimeSinceStartup;
                while (cv.RaisingsStarted < 1 && Time.realtimeSinceStartup - t < 10f) yield return null;
                Assert.GreaterOrEqual(cv.RaisingsStarted, 1, "a raising was planned (gave up " + cv.GaveUp + ")");
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.Greater(cv.RaisingsActive, 0, "the raising is still under way 1.5 s later (not just standing)");
                // the tester's next "+1 season" tap while the tent goes up: the view day jumps at the next midnight (or at once)
                EcoDebug.SkipSeasons(1);
                PeopleView.DebugSetHour(23.9f);
                yield return new WaitForSecondsRealtime(1.5f);
                Debug.Log($"[t1 camp] after a season tap and midnight: active {cv.RaisingsActive}, carried {cv.Carried}, hour {pv.ViewHour:0.0}, day {pv.Now.day}");
                Assert.Greater(cv.RaisingsActive, 0, "a view-clock jump does not end the raising");
                var tent = Sim.State.buildings[Sim.State.buildings.Count - 1];
                pv.timeScale = 4f;
                bool shotRise = false; float seenRaise = 0f;
                t = Time.realtimeSinceStartup;
                while (cv.RaisingsActive > 0 && Time.realtimeSinceStartup - t < 120f)
                {
                    float g = pv.Planner.TentRaised(tent.id, pv.Now);
                    if (g > 0f && g < 1f) seenRaise = Mathf.Max(seenRaise, g);
                    if (!shotRise && g > 0.3f) { shotRise = true; yield return Shot("t1_tent_rising_night"); }
                    yield return null;
                }
                pv.timeScale = 1f;
                Debug.Log($"[t1 camp] carried {cv.Carried}, line '{cv.LastLine}', hour now {pv.ViewHour:0.0}, max partial raise {seenRaise:0.00}");
                yield return Shot("t1_tent_up_night");
                Assert.Greater(seenRaise, 0f, "the tent was seen part-raised");
                Assert.IsNotNull(cv.LastLine, "the builder said the first tent's line");
                Assert.AreEqual(1, cv.MomentsPlayed);
                AssertNoErrors("camp at night");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }
    }
}
