using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Samples every rendered frame: is the recall question open while the Kit sheet or a tool is up? (Feedback batch 2: it must never be.)</summary>
    public class RecallWatch : MonoBehaviour
    {
        public int Samples, Busy, Violations;
        public string First;
        void Update()
        {
            var hud = GameHud.Instance;
            var recall = RecallPromptView.Instance;
            if (hud == null) return;
            Samples++;
            bool busy = hud.OpenSheet != null || hud.Hand != GameHud.KitTool.None || hud.CleanMode;
            if (busy) Busy++;
            if (busy && recall != null && recall.IsOpen)
            {
                Violations++;
                if (First == null) First = $"sheet {hud.OpenSheet}, hand {hud.Hand}, clean {hud.CleanMode}";
            }
        }
    }

    /// <summary>
    /// Playtest-feedback batch 2 played the way a phone plays it: a fresh living-land box (seed 4242), wipe, mist, name; Kit with the mister, the
    /// cloth and the tweezers (weed patch, tuft round the tree, vine, sapling); one rabbit tapped and watched bolting; time only until the camp
    /// has hides drying on the rack (no log seats, rack built), shots at the default camera and close; the recall question must never open over the
    /// Kit or a tool; sand gusts over the bare early box. State cheats only make the tweezers' targets exist. Shots: Temp/agent/f2_*.png. Explicit.
    /// </summary>
    [Explicit, Category("Feedback2Play")]
    public class Feedback2Playthrough : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView Run => FirstRunView.Instance;
        readonly List<string> missed = new List<string>();

        void Miss(string beat)
        {
            string s = $"{beat} (not reached by time, year {Sim.State.Year:0.0})";
            missed.Add(s);
            Debug.LogWarning("[f2] MISSED " + s);
        }

        Vector2 ScreenOf(float x, float z)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z)));
        }

        static Vector2 ScreenOf(MessProps.Piece p)
        {
            var s = Camera.main.WorldToScreenPoint(p.Center);
            return new Vector2(s.x, s.y);
        }

        static bool OnScreen(Vector2 s) => s.x > 40 && s.y > 200 && s.x < Screen.width - 40 && s.y < Screen.height - 300;

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

        IEnumerator Hold(Vector2 at, float seconds, System.Func<bool> until = null)
        {
            int id = touchId++;
            BeginTouch(id, at, queueEventOnly: true);
            float t = Time.unscaledTime;
            while (Time.unscaledTime - t < seconds && (until == null || !until())) { MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
            EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(10);
        }

        static bool Shown(UnityEngine.UIElements.VisualElement e)
        {
            if (e == null) return false;
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.None) return false;
            return true;
        }

        IEnumerator PutDownTool(GameHud hud)
        {
            if (hud.Hand == GameHud.KitTool.None && !hud.CleanMode) yield break;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return Frames(10);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "Done puts the tool down");
            Assert.IsFalse(hud.CleanMode, "Done puts the cloth down");
        }

        IEnumerator PickFromKit(GameHud hud, string button)
        {
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens the kit");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, button));
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>Time passes the way a caring keeper lets it: the Mister in hand, +1 season and a held mist per step.</summary>
        IEnumerator AdvanceUntil(GameHud hud, string what, System.Func<bool> cond, int maxSeasons)
        {
            if (cond()) yield break;
            if (hud.Hand != GameHud.KitTool.Mister)
            {
                yield return PutDownTool(hud);
                yield return PickFromKit(hud, "kit-mister");
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
                if (n % 8 == 7) AssertNoErrors("advance " + what);
            }
            yield return Frames(10);
            Debug.Log($"[f2] advance for {what}: {(cond() ? "reached" : "NOT reached")} after {n} seasons, year {y0:0.0} -> {Sim.State.Year:0.0}");
            if (!cond()) Miss(what);
        }

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

        IEnumerator Fly(BoxView box, Vector3 local, float zoomMul)
        {
            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.FlyTo(box.transform.TransformPoint(local), Mathf.Max(rig.minZoom * zoomMul, 0.25f), 0.4f);
            for (int i = 0; i < 200 && rig.IsFlying; i++) yield return null;
            yield return Frames(10);
        }

        static float Dist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        [UnityTest, Timeout(2700000)]
        public IEnumerator Feedback2_PlayedByTouch_WipeKitTweezersRabbitCampRecall()
        {
            UseLivingLand(true);
            var watch = new GameObject("RecallWatch") { hideFlags = HideFlags.DontSave }.AddComponent<RecallWatch>();
            try
            {
                yield return OpenSlice(seed: 4242, skipFirstRun: false);
                Assert.IsTrue(Sim.EcologyOn, "a barren living-land box");
                var care = CareView.Instance;
                var hud = GameHud.Instance;
                var box = Object.FindAnyObjectByType<BoxView>();
                var audio = AudioDirector.Instance;
                int gusts0 = audio != null ? audio.SandGustsPlayed : 0;
                float tOpen = Time.realtimeSinceStartup;
                yield return Until(() => Run.Step == FirstRunStep.Wipe, 8f);

                // ---- 1. wipe by touch, mist by a held finger, name by tapping the plate
                float spacing = care.wipeRadius * Screen.height * 1.1f;
                for (int pass = 0; pass < 6 && Run.Step == FirstRunStep.Wipe; pass++)
                    foreach (var row in PlayerStrokes(care, spacing))
                    {
                        if (Run.Step != FirstRunStep.Wipe) break;
                        yield return Drag(row, 0.03f * Screen.height);
                    }
                Assert.AreNotEqual(FirstRunStep.Wipe, Run.Step, $"the glass wipes clean by touch (grime {Sim.State.grime:0.000})");
                yield return Frames(20);
                Assert.AreEqual(FirstRunStep.Rain, Run.Step, "wipe, then the first mist");
                yield return new WaitForSecondsRealtime(1.5f);
                var (rx, rz) = Run.Run.SuggestedRainSpot;
                yield return Hold(ScreenOf(rx, rz), 2.5f, () => Run.Step != FirstRunStep.Rain);
                yield return Frames(20);
                Assert.AreEqual(FirstRunStep.Name, Run.Step, "the held finger misted");
                yield return new WaitForSecondsRealtime(4.5f);
                Assert.IsTrue(Shown(Run.Root.Q("firstrun-plate")), "the plate");
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(Run.NameSheetOpen, "a tap on the plate opens the naming sheet");
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-name-keep"));
                yield return Frames(30);
                Assert.IsFalse(Run.Active, "named: the run is done");
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot("f2_01_wiped_misted_named");
                Debug.Log($"[f2] 1 first run done, grime {Sim.State.grime:0.000}, real s {Time.realtimeSinceStartup - tOpen:0}");
                AssertNoErrors("first run");

                // ---- 6. (optional) sand gusts on the bare early box: sampled over the first minute of real time
                if (audio != null)
                {
                    float left = 60f - (Time.realtimeSinceStartup - tOpen);
                    if (audio.SandGustsPlayed <= gusts0 && left > 0f) yield return Until(() => audio.SandGustsPlayed > gusts0, left);
                    Debug.Log($"[f2] 6 sand gusts: {gusts0} -> {audio.SandGustsPlayed} over {Time.realtimeSinceStartup - tOpen:0} s real");
                    if (audio.SandGustsPlayed <= gusts0) Debug.LogWarning("[f2] 6 no sand gust within a minute (optional beat)");
                    yield return Shot("f2_02_sand_gust_box");
                }
                else Debug.LogWarning("[f2] 6 no AudioDirector (optional beat skipped)");

                // ---- 2a. Kit -> Mister, a few holds, Done
                Assert.IsTrue(hud.KitBox, "a living-land box shows the kit");
                yield return PickFromKit(hud, "kit-mister");
                Assert.AreEqual(GameHud.KitTool.Mister, hud.Hand, "the mister in hand");
                float bare0 = Sim.EcoBareShare;
                foreach (var (x, z) in new[] { (-1.2f, 0.4f), (0.6f, 0.9f), (-0.2f, -1.2f) }) yield return Hold(ScreenOf(x, z), 1.2f);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("f2_03_mister");
                Debug.Log($"[f2] 2a mister: bare {bare0:0.000} -> {Sim.EcoBareShare:0.000}");
                Assert.That(Sim.EcoBareShare, Is.LessThan(bare0), "the mister greened ground");
                Assert.IsFalse(RecallPromptView.Instance != null && RecallPromptView.Instance.IsOpen, "no recall question over the mister");
                yield return PutDownTool(hud);

                // ---- 2b. Kit -> Cloth, a drag, Done
                yield return PickFromKit(hud, "kit-cloth");
                Assert.IsTrue(hud.CleanMode, "the cloth is clean mode");
                yield return Drag(new[] { new Vector2(Screen.width * 0.2f, Screen.height * 0.55f), new Vector2(Screen.width * 0.8f, Screen.height * 0.5f) }, 0.02f * Screen.height);
                yield return Shot("f2_04_cloth");
                Debug.Log($"[f2] 2b cloth in hand, grime {Sim.State.grime:0.000}");
                yield return PutDownTool(hud);

                // ---- 5b. (#328) the recall question made due: it waits while the Kit is open and while a tool is in hand (both by real taps),
                // then shows at the next calm moment, and a tap on "Not now" closes it
                {
                    var recall = RecallPromptView.Instance;
                    Assert.IsNotNull(recall, "the recall view");
                    recall.checkSeconds = 0.2f; recall.calmSeconds = 1f;
                    RecallPromptView.DebugAskWhenCalm();
                    yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "a tap on Kit opens the kit");
                    for (float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 3f; ) { Assert.IsFalse(recall.IsOpen, "never over the open Kit"); yield return null; }
                    yield return Shot("f2_04b_kit_open_question_waits");
                    yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-mister"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    Assert.AreEqual(GameHud.KitTool.Mister, hud.Hand, "the mister in hand");
                    for (float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 3f; ) { Assert.IsFalse(recall.IsOpen, "never with a tool in hand"); yield return null; }
                    yield return PutDownTool(hud);
                    yield return Until(() => recall.IsOpen, 10f);
                    yield return Shot("f2_04c_question_when_calm");
                    Debug.Log($"[f2] 5b recall: open after calm = {recall.IsOpen}");
                    Assert.IsTrue(recall.IsOpen, "once the kit is down and nothing is touched, the question shows");
                    yield return TapAt(GameUiPanel.ScreenCenter(recall.Root.Q("recall-dismiss")));
                    yield return new WaitForSecondsRealtime(0.5f);
                    Assert.IsFalse(recall.IsOpen, "Not now closes it");
                    recall.checkSeconds = 2f; recall.calmSeconds = (float)RecallCalm.CalmSeconds;
                }

                // ---- 2c. Kit -> Tweezers: the targets are made with state cheats, plucked by a tap at their screen position
                var sim = Sim;
                var e = sim.State.eco;
                sim.DebugTreeStage(1);
                sim.AddMess(0f, 0.4f);
                sim.DebugGrowTreeWeeds();
                var tree = sim.EcoTree;
                sim.DebugGrowVine(tree.x + 0.5f, tree.z + 0.5f, 8);
                yield return Frames(40);
                Assert.Greater(care.Props.WeedCount, 0, "weed patches are drawn");
                Assert.Greater(care.Props.VineCount, 0, "the vine is drawn");
                yield return Shot("f2_05_overgrown");
                yield return PickFromKit(hud, "kit-tweezers");
                Assert.AreEqual(GameHud.KitTool.Tweezers, hud.Hand, "the tweezers in hand");
                var rig = Camera.main.GetComponent<BoxCamera>();
                float yaw0 = rig.yaw, og0 = sim.State.overgrowth;
                var weed = care.Props.AllPieces.FirstOrDefault(p => !p.leaving && p.kind == MessProps.Kind.Weed && OnScreen(ScreenOf(p)));
                Assert.IsNotNull(weed, "a weed patch on screen");
                yield return TapAt(ScreenOf(weed));
                Assert.Less(sim.State.overgrowth, og0 - 0.05f, "a weed patch came out");
                Assert.AreEqual("weed", care.LastPluck);
                Assert.AreEqual(yaw0, rig.yaw, 0.01f, "the tweezers never turned the camera");
                int tuftsBefore = sim.EcoTreeWeedPieces;
                var tuft = care.Props.AllPieces.FirstOrDefault(p => !p.leaving && p.kind == MessProps.Kind.TreeWeed && OnScreen(ScreenOf(p)));
                if (tuft != null)
                {
                    yield return TapAt(ScreenOf(tuft));
                    Assert.AreEqual(tuftsBefore - 1, sim.EcoTreeWeedPieces, "one tuft round the tree came out (last pluck: " + care.LastPluck + ")");
                    Assert.AreEqual("treeweed", care.LastPluck);
                }
                else Miss("a tree tuft on screen");
                int vines0 = sim.EcoVine.Count;
                var vine = care.Props.AllPieces.FirstOrDefault(p => !p.leaving && p.kind == MessProps.Kind.Vine && OnScreen(ScreenOf(p)));
                Assert.IsNotNull(vine, "a vine strand on screen");
                yield return TapAt(ScreenOf(vine));
                Assert.AreEqual(vines0 - 1, sim.EcoVine.Count, "a cell of vine came away");
                Assert.AreEqual("vine", care.LastPluck);
                int made = sim.DebugCreepForestEdge(4);
                box.Refresh();
                yield return Frames(10);
                int cell = Enumerable.Range(0, EcoLand.N).FirstOrDefault(c => (e.cover[c] == (byte)EcoCover.Seedling || e.cover[c] == (byte)EcoCover.Young) && c != e.treeCell && sim.EcoCanPluckSapling(c) && OnScreen(ScreenOf(AquariumCiv.Sim.Settle.SiteMap.CXc(c), AquariumCiv.Sim.Settle.SiteMap.CZc(c))));
                if (made > 0 && sim.EcoCanPluckSapling(cell))
                {
                    var before = e.cover[cell];
                    yield return TapAt(ScreenOf(AquariumCiv.Sim.Settle.SiteMap.CXc(cell), AquariumCiv.Sim.Settle.SiteMap.CZc(cell)));
                    Assert.AreNotEqual(before, e.cover[cell], "the sapling came out (last pluck: " + care.LastPluck + ")");
                    Assert.AreEqual("sapling", care.LastPluck);
                }
                else Miss($"a pluckable sapling on screen (crept {made})");
                yield return Shot("f2_06_after_tweezers");
                Debug.Log($"[f2] 2c plucks {care.Plucks}, last {care.LastPluck}, overgrowth {og0:0.00} -> {sim.State.overgrowth:0.00}, vine {vines0} -> {sim.EcoVine.Count}");
                yield return PutDownTool(hud);
                AssertNoErrors("kit");

                // ---- 4. rabbits (time passes with the mister): then ONE rabbit is tapped by touch
                var rv = RabbitView.Instance;
                var pvh = PeopleView.Find();
                Assert.IsNotNull(rv, "the rabbit view");
                yield return AdvanceUntil(hud, "rabbits", () => Sim.EcoRabbitCount > 0 && rv.Shown > 0, 150);
                yield return PutDownTool(hud);
                if (Sim.EcoRabbitCount > 0 && rv.Shown > 0)
                {
                    PeopleView.DebugSetHour(11f);
                    yield return Until(() => !rv.SignPlaying, 10f);
                    yield return Frames(30);
                    int count0 = Sim.EcoRabbitCount, hides0 = Sim.HidesTotal;
                    int r0 = rv.Field.Alive[0];
                    Assert.IsTrue(rv.TryGetPosition(r0, out var at0));
                    yield return Fly(box, at0, 1.6f);
                    Assert.IsTrue(rv.TryGetPosition(r0, out at0));
                    int shown = rv.Shown;
                    var screen = (Vector2)Camera.main.WorldToScreenPoint(box.transform.TransformPoint(at0));
                    Assert.IsTrue(screen.x > 0 && screen.y > 0 && screen.x < Screen.width && screen.y < Screen.height, "the rabbit is on screen");
                    yield return TapAt(screen);
                    yield return Shot("f2_07_rabbit_tap");
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return Shot("f2_08_rabbit_bolting");
                    rv.TryGetPosition(r0, out var p05);
                    yield return new WaitForSecondsRealtime(2.5f);
                    yield return Shot("f2_09_rabbit_3s");
                    Assert.IsTrue(rv.TryGetPosition(r0, out var p3), "still there");
                    Debug.Log($"[f2] 4 rabbit {r0}: at tap ({at0.x:0.00},{at0.z:0.00}); 0.5 s {Dist(p05, at0):0.00}; 3 s {Dist(p3, at0):0.00} away; shown {shown}");
                    Assert.Greater(Dist(p3, at0), 0.5f, "it ran to another part of the meadow");
                    Assert.IsTrue(rv.Field.IsGreenAt(p3.x, p3.z), "and stands on green");
                    double f0 = rv.FieldNow;
                    pvh.timeScale = 6f;
                    yield return new WaitForSecondsRealtime(10f);
                    pvh.timeScale = 1f;
                    Assert.IsTrue(rv.TryGetPosition(r0, out var pLater), "still there a minute on");
                    Debug.Log($"[f2] 4 rabbit after {rv.FieldNow - f0:0} s field time: {Dist(pLater, at0):0.00} from the tap; count {count0} -> {Sim.EcoRabbitCount}, shown {rv.Shown}");
                    Assert.Greater(Dist(pLater, at0), 0.4f, "and it has not come back to where it was tapped");
                    Assert.GreaterOrEqual(Sim.EcoRabbitCount, count0 - (Sim.HidesTotal - hides0), "a tap removes nobody");
                    yield return Shot("f2_10_rabbit_a_minute_on");
                }
                else Miss("rabbits shown");
                AssertNoErrors("rabbit tap");

                // ---- 3. time passes (season skips, hunting days) until hides dry on the camp's rack
                yield return AdvanceUntil(hud, "camp gate", () => Sim.CampGateOpen, 150);
                yield return PutDownTool(hud);
                for (int attempt = 0; attempt < 14 && Sim.HidesDrying < 1; attempt++)
                {
                    if (Sim.CampGateOpen) yield return HuntADay(pvh);
                    if (Sim.HidesDrying < 1) { EcoDebug.SkipSeasons(1); yield return Frames(10); }
                }
                if (Sim.HidesDrying < 1)
                {
                    Miss("hides drying on the rack (the rack is then shown with the ShowRack cheat)");
                    EcoDebug.ShowRack(3);
                }
                PeopleView.DebugSetHour(11.5f);
                yield return new WaitForSecondsRealtime(2f);
                Debug.Log($"[f2] 3 camp: year {Sim.State.Year:0.0}, hides total {Sim.HidesTotal}, drying {Sim.HidesDrying}");
                var cv = CampView.Instance;
                Assert.IsNotNull(cv, "the camp view");
                Assert.IsTrue(Sim.HidesDrying > 0 && cv.FrameShown, "hides dry on a rack that stands");
                Assert.IsTrue(pvh.Planner.HideFrame(out float hx, out float hz, out _, out _), "the planner knows the rack");
                pvh.Planner.FirePoint(out float fx, out float fz);
                var rackLocal = box.land.OnGround(hx, hz);
                var fireLocal = box.land.OnGround(fx, fz);
                var rig2 = Camera.main.GetComponent<BoxCamera>();
                rig2.CancelFlight();
                rig2.target = box.transform.TransformPoint((rackLocal + fireLocal) * 0.5f); rig2.zoom = 1f; rig2.Apply();
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("f2_11_camp_default");
                yield return Fly(box, rackLocal, 1.3f);
                yield return Shot("f2_12_rack_close");
                int logSeats = box.layout.pieces.Count(p => p.kind == PieceKind.LogSeat);
                int seatObjects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name.Contains("LogSeat"));
                var rackFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(m => m.name == "HideFrame").ToList();
                Debug.Log($"[f2] 3 log-seat pieces {logSeats}, log-seat objects {seatObjects}, rack meshes {rackFilters.Count} ({(rackFilters.Count > 0 ? rackFilters[0].sharedMesh.vertexCount : 0)} verts)");
                Assert.AreEqual(0, logSeats, "no log seats in the camp");
                Assert.AreEqual(0, seatObjects, "nothing named for a log seat is drawn");
                Assert.AreEqual(1, rackFilters.Count, "the rack is built");
                Assert.Greater(rackFilters[0].sharedMesh.vertexCount, 0, "and it has geometry");
                AssertNoErrors("camp");

                // ---- 5. the recall question never opened over the Kit or a tool
                Debug.Log($"[f2] 5 recall watch: {watch.Samples} frames, {watch.Busy} with the kit or a tool up, {watch.Violations} with the recall question open ({watch.First})");
                Assert.Greater(watch.Busy, 100, "the watch saw the kit or a tool up for many frames");
                Assert.AreEqual(0, watch.Violations, "the recall question never opened over the kit or a tool: " + watch.First);

                Assert.IsEmpty(missed, "beats time alone did not reach: " + string.Join("; ", missed));
            }
            finally
            {
                if (watch != null) Object.Destroy(watch.gameObject);
                var pv = PeopleView.Find();
                if (pv != null) pv.timeScale = 1f;
                UseLivingLand(false);
            }
        }
    }
}
