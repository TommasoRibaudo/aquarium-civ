using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Samples every frame: each new caption line, who it names, and whether the speaker is one of the crowd (feedback batch 3, #522).
    /// Grabs the screen the first time a crowd line is up (Temp/agent/f3_d_crowd_caption.png).</summary>
    public class CaptionWatch : MonoBehaviour
    {
        public readonly List<string> Lines = new List<string>();
        public int Crowd, CrowdNamed, Someone;
        public string FirstCrowd;
        string last;
        static readonly FieldInfo WhoField = typeof(ShowMeDirector).GetField("lineWho", BindingFlags.Instance | BindingFlags.NonPublic);

        void Update()
        {
            var d = ShowMeDirector.Instance;
            if (d == null || d.ShownCaption == null) return;
            string key = d.LastLine + "|" + d.LastWho;
            if (key == last) return;
            last = key;
            bool crowd = WhoField != null && WhoField.GetValue(d) is PersonRef p && p.IsCrowd;
            Lines.Add($"{(crowd ? "crowd" : "named")} [{d.LastWho ?? "-"}] {d.LastLine}");
            if (d.LastWho != null && d.LastWho.ToLowerInvariant().Contains("someone")) Someone++;
            if (!crowd) return;
            Crowd++;
            if (!string.IsNullOrWhiteSpace(d.LastWho)) CrowdNamed++;
            if (FirstCrowd == null)
            {
                FirstCrowd = $"[{d.LastWho ?? "-"}] {d.LastLine}";
                ScreenCapture.CaptureScreenshot("F:/GameDev/AquariumCiv/Temp/agent/f3_d_crowd_caption.png");
            }
        }
    }

    /// <summary>
    /// Playtest-feedback batch 3 played the way a phone plays it (1179x2556, a fresh living-land box, seed 4242): wipe, mist, name; the first
    /// camp morning (#521); weeds off the trodden ground (#523); crowd caption lines name the speaker (#522); the seed's ghost, two-finger pans
    /// with the seed and the mister, "Put away" (#518, #516); the thicket and the rabbits placed by touch with species ghosts, the glow and the
    /// group's hint (#516); the wet band at the glass (#524); ducks only on water (#519); the tree's night pool (#525); the barren wind's two
    /// voices (#520). Real HUD taps; time advances by season skips and hour sets only. Soft checks: every beat runs, the misses and failures
    /// are listed at the end. Shots: Temp/agent/f3_*.png. Explicit.
    /// </summary>
    [Explicit, Category("Feedback3Play")]
    public class Feedback3Playthrough : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView Run => FirstRunView.Instance;
        readonly List<string> missed = new List<string>();
        readonly List<string> failed = new List<string>();

        void Miss(string beat) { string s = $"{beat} (year {Sim.State.Year:0.0})"; missed.Add(s); Debug.LogWarning("[f3] MISSED " + s); }
        void Check(bool ok, string what) { Debug.Log($"[f3] {(ok ? "OK  " : "FAIL")} {what}"); if (!ok) failed.Add(what); }
        static void Log(string s) => Debug.Log("[f3] " + s);
        readonly List<string> cheats = new List<string>();
        void Cheat(string what, string r) { cheats.Add(what); Debug.LogWarning($"[f3] STATE CHEAT {what}: {r}"); }

        Vector2 ScreenOf(float x, float z)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z)));
        }

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

        IEnumerator TwoFingerPan(Vector2 a, int stagger)
        {
            var b = a + new Vector2(Screen.width * 0.18f, 0f);
            var move = new Vector2(0f, Screen.height * 0.08f);
            int ia = touchId++, ib = touchId++;
            BeginTouch(ia, a, queueEventOnly: true);
            if (stagger > 0) yield return Frames(stagger);
            BeginTouch(ib, b, queueEventOnly: true);
            yield return null;
            for (int i = 1; i <= 12; i++)
            {
                var d = move * (i / 12f);
                MoveTouch(ia, a + d, delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(ib, b + d, delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(ib, b + move, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(4);
            EndTouch(ia, a + move, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(6);
        }

        static string KitWord(GameHud hud) => (hud.Root.Q("hud-clean") as TextElement)?.text ?? "?";

        IEnumerator CalmHud(GameHud hud)
        {
            yield return Until(() => hud.IsVisible && !(PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)), 30f);
            if (PersonCardView.Instance != null && PersonCardView.Instance.AnyOpen) { yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.93f)); yield return Frames(10); }
            if (hud.SheetOpen) { hud.CloseSheet(); yield return Frames(10); }
        }

        IEnumerator PutDownTool(GameHud hud)
        {
            if (hud.Hand == GameHud.KitTool.None && !hud.CleanMode) yield break;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return Frames(10);
        }

        IEnumerator PickFromKit(GameHud hud, string button)
        {
            yield return PutDownTool(hud);
            yield return CalmHud(hud);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.5f);
            if (hud.OpenSheet != GameHud.Kit) { Log("Kit did not open the kit: sheet " + hud.OpenSheet); yield break; }
            if (button == "kit-seed")
            {
                // #558: the seed is a choice in the life jar, not a tool of its own
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life"));
                yield return new WaitForSecondsRealtime(0.4f);
                if (hud.LifeRowOpen) yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-seed"));
            }
            else yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, button));
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>Time passes the way a caring keeper lets it: the mister in hand, +1 season and a held mist per step.</summary>
        IEnumerator AdvanceUntil(GameHud hud, string what, System.Func<bool> cond, int maxSeasons)
        {
            if (cond()) yield break;
            DefaultView(); // the holds below aim at land the default view shows (a close-up left them off screen)
            yield return Frames(5);
            if (hud.Hand != GameHud.KitTool.Mister) yield return PickFromKit(hud, "kit-mister");
            var map = Sim.Settlement.Map;
            var spots = new[] { (map.greenX + 0.5f, map.greenZ), (-1.2f, 0.4f), (1.4f, 0.2f), (-0.2f, -1.2f), (0.6f, 0.9f) };
            float y0 = Sim.State.Year;
            int n = 0;
            for (; n < maxSeasons && !cond(); n++)
            {
                EcoDebug.SkipSeasons(1);
                var (x, z) = spots[n % spots.Length];
                if (hud.Hand == GameHud.KitTool.Mister) yield return Hold(ScreenOf(x, z), 0.8f);
                else yield return Frames(5);
                if (n % 6 == 5) CheckWeeds("advance " + what);
            }
            yield return Frames(10);
            Log($"advance for {what}: {(cond() ? "reached" : "NOT reached")} after {n} seasons, year {y0:0.0} -> {Sim.State.Year:0.0}");
            if (!cond()) Miss(what);
            yield return PutDownTool(hud);
        }

        static void DefaultView()
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.CancelFlight(); rig.target = box.transform.position; rig.zoom = 1f; rig.Apply();
        }

        int weedChecks, weedsSeen, weedsTrodden;
        void CheckWeeds(string when)
        {
            var care = CareView.Instance; var box = Object.FindAnyObjectByType<BoxView>();
            if (care == null || care.Props == null || box == null) return;
            weedChecks++;
            foreach (var p in care.Props.AllPieces)
            {
                if (p.leaving || (p.kind != MessProps.Kind.Weed && p.kind != MessProps.Kind.Vine)) continue;
                weedsSeen++;
                var l = box.transform.InverseTransformPoint(p.Center);
                if (Sim.IsTroddenGround(l.x, l.z)) { weedsTrodden++; Log($"weed/vine on trodden ground at ({l.x:0.00},{l.z:0.00}) {when}"); }
            }
        }

        IEnumerator Fly(BoxView box, Vector3 local, float zoomMul)
        {
            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.FlyTo(box.transform.TransformPoint(local), Mathf.Max(rig.minZoom * zoomMul, 0.25f), 0.4f);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Until(() => !rig.IsFlying, 10f);
            yield return Frames(10);
        }

        /// <summary>The Keeper places a ready jar's group by touch, with shots of the kit, the glow, the ghost and the hint (#516).</summary>
        IEnumerator Place(GameHud hud, LifeSpecies s, string tag)
        {
            if (!Sim.JarReady(s)) { Miss(s + " jar ready"); yield break; }
            yield return PutDownTool(hud);
            yield return CalmHud(hud);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.5f);
            Check(hud.OpenSheet == GameHud.Kit && hud.LifeSlotShown, $"{s}: Kit opens with the life slot");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life"));
            yield return new WaitForSecondsRealtime(0.4f);
            if (hud.LifeRowOpen)
            {
                yield return Shot($"f3_f_{tag}_row");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-" + s.ToString().ToLowerInvariant()));
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Check(hud.Hand == GameHud.KitTool.Life && hud.HeldLife == s, $"{s}: in hand ({hud.Hand}, {hud.HeldLife})");
            Check(KitWord(hud).Contains(GameHud.PutAway), $"{s}: the Kit button reads Put away ('{KitWord(hud)}')");
            var lpv = LifePlaceView.Instance;
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot($"f3_f_{tag}_glow");
            int cell = GlowingCellOnScreen(Sim, s);
            if (cell < 0) { Miss($"{s}: a glowing spot on screen"); yield return PutDownTool(hud); yield break; }
            yield return TapAt(lpv.ScreenOf(cell));
            yield return Frames(5);
            Check(lpv.GhostShown, $"{s}: the first tap shows the ghost ({lpv.Last})");
            var bc = Camera.main.GetComponent<BoxCamera>();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Until(() => !bc.IsFlying, 8f);
            yield return Until(() => GameUiPanel.Toasts.Current == LifePlaceView.TapAgainHintFor(s), 3f);
            Check(GameUiPanel.Toasts.Current == LifePlaceView.TapAgainHintFor(s), $"{s}: the hint names the group ('{GameUiPanel.Toasts.Current}')");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot($"f3_f_{tag}_ghost");
            yield return TapAt(lpv.ScreenOf(lpv.GhostCell));
            yield return Frames(10);
            Check(Sim.LifeIsPlaced(s), $"{s}: a tap on the ghost places it ({lpv.Last})");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot($"f3_f_{tag}_placed");
            yield return PutDownTool(hud);
        }

        static int CountDuckStories() => Sim.State.chronicle.Count(c => c.kind == "ducks");

        [UnityTest, Timeout(3600000)]
        public IEnumerator Feedback3_PlayedByTouch()
        {
            UseLivingLand(true);
            CaptionWatch watch = null;
            try
            {
                yield return OpenSlice(seed: 4242, skipFirstRun: false);
                watch = new GameObject("CaptionWatch").AddComponent<CaptionWatch>(); // a scene object: it goes with the scene, never leaks
                Assert.IsTrue(Sim.EcologyOn, "a barren living-land box");
                var care = CareView.Instance;
                var hud = GameHud.Instance;
                var box = Object.FindAnyObjectByType<BoxView>();
                var pv = PeopleView.Find();
                yield return Until(() => Run.Step == FirstRunStep.Wipe, 8f);
                yield return Shot("f3_a1_dirty");

                // ---- a) first run: wipe by touch, mist by a held finger, name by the plate
                float spacing = care.wipeRadius * Screen.height * 1.1f;
                for (int pass = 0; pass < 6 && Run.Step == FirstRunStep.Wipe; pass++)
                    foreach (var row in PlayerStrokes(care, spacing))
                    {
                        if (Run.Step != FirstRunStep.Wipe) break;
                        yield return Drag(row, 0.03f * Screen.height);
                    }
                Check(Run.Step != FirstRunStep.Wipe, $"a: the glass wipes clean by touch (grime {Sim.State.grime:0.000})");
                yield return Frames(20);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("f3_a2_wiped");
                var (rx, rz) = Run.Run.SuggestedRainSpot;
                yield return Hold(ScreenOf(rx, rz), 2.5f, () => Run.Step != FirstRunStep.Rain);
                yield return Frames(20);
                Check(Run.Step == FirstRunStep.Name, "a: the held finger misted");
                yield return new WaitForSecondsRealtime(4.5f);
                yield return Shot("f3_a3_misted_plate");
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.4f);
                Check(Run.NameSheetOpen, "a: a tap on the plate opens the naming sheet");
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-name-keep"));
                yield return Frames(30);
                Check(!Run.Active, "a: named, the run is done");
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot("f3_a4_named");
                Log($"a done, year {Sim.State.Year:0.00}");

                // ---- j) the barren wind's two voices (#520): sample the audio status over a few seconds on the bare box
                var audio = AudioDirector.Instance;
                if (audio != null)
                {
                    var samples = new List<string>();
                    for (int i = 0; i < 4; i++) { samples.Add(audio.BarrenWindStatus); yield return new WaitForSecondsRealtime(2.5f); }
                    Log("j barren wind: " + string.Join(" || ", samples));
                    Check(samples.Distinct().Count() > 1, "j: the barren wind's status changes over 7 s");
                    var srcs = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(a => a.name.StartsWith("Amb_barren")).Select(a => $"{a.name} vol {a.volume:0.000} pitch {a.pitch:0.000} playing {a.isPlaying}").ToList();
                    Log("j barren sources: " + string.Join(" | ", srcs));
                }
                else Miss("j: no AudioDirector");

                // ---- b) the first camp morning (#521): at most two dig, at different patches
                pv.Planner.FirePoint(out float fx, out float fz);
                var fireLocal = box.land.OnGround(fx, fz);
                yield return Fly(box, fireLocal, 2.2f);
                foreach (float h in new[] { 8.25f, 9.0f, 9.75f })
                {
                    PeopleView.DebugSetHour(h);
                    yield return new WaitForSecondsRealtime(4f);
                    var digs = pv.Activities.Where(a => a.kind == ActivityKind.Gather && a.place.kind == PlaceKind.Stream && !a.moving).ToList();
                    var kinds = pv.Activities.Where(a => !a.moving).GroupBy(a => a.kind).Select(g => $"{g.Key}x{g.Count()}");
                    Log($"b {h:0.00} h: {pv.Activities.Count} acts; digging {digs.Count} at {string.Join(",", digs.Select(d => d.place.ToString()).Distinct())}; {string.Join(" ", kinds)}");
                    Check(digs.Count <= 2, $"b {h:0.00} h: at most two digging ({digs.Count})");
                    if (digs.Count == 2) Check(!digs[0].place.Equals(digs[1].place), $"b {h:0.00} h: the two dig at different patches");
                    var most = pv.Activities.Where(a => !a.moving && a.kind != ActivityKind.Sleep && a.kind != ActivityKind.Indoors && !a.kind.ToString().Contains("Wary")).GroupBy(a => a.place).Select(g => g.Count()).DefaultIfEmpty(0).Max();
                    Check(most <= 3, $"b {h:0.00} h: no crowd at work at one spot ({most} at the busiest place; a wary look at the Keeper does not count)");
                    yield return Shot($"f3_b_morning_{h:00.00}".Replace('.', '_'));
                }
                CheckWeeds("first morning");

                // ---- e1) the mister: a two-finger pan mists nothing; Put away
                yield return PickFromKit(hud, "kit-mister");
                Check(hud.Hand == GameHud.KitTool.Mister, "e: the mister in hand");
                Check(KitWord(hud).Contains(GameHud.PutAway), $"e: the Kit button reads Put away with the mister ('{KitWord(hud)}')");
                yield return Shot("f3_e1_mister_putaway");
                var mist = EcoMistView.Instance;
                int pulses0 = mist.MistPulses;
                float yaw0 = Camera.main.GetComponent<BoxCamera>().yaw;
                yield return TwoFingerPan(ScreenOf(-0.6f, 0.2f), 1);
                yield return TwoFingerPan(ScreenOf(0.4f, -0.3f), 0);
                Check(mist.MistPulses == pulses0 && !mist.Misting, $"e: two-finger pans with the mister mist nothing ({pulses0} -> {mist.MistPulses})");
                yield return PutDownTool(hud);
                Check(hud.Hand == GameHud.KitTool.None, "e: Put away puts the mister down");

                // ---- c) weeds come with time; none on the fire's ring or the camp's ground (#523); d) the caption watch runs all along
                yield return AdvanceUntil(hud, "camp gate", () => Sim.CampGateOpen, 60);
                if (!Sim.CampGateOpen) Cheat("camp gate", EcoDebug.OpenCampGate());
                yield return AdvanceUntil(hud, "some weeds", () => care.Props.WeedCount > 2, 16);
                CheckWeeds("after the gate");
                PeopleView.DebugSetHour(11f);
                yield return Fly(box, fireLocal, 2.0f);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("f3_c_weeds_camp");
                Log($"c weeds now {care.Props.WeedCount}, vine {care.Props.VineCount}");

                // ---- h1) no drawn water yet: the duck ground is empty, no duck story
                var duckGround = new List<int>();
                Sim.LifeGround(LifeSpecies.Ducks, duckGround);
                int pondCells = Enumerable.Range(0, EcoLand.N).Count(c => Sim.EcoOpenWater(c));
                Log($"h no-water check: pond cells {pondCells}, duck ground {duckGround.Count}, ducks {Sim.EcoDucks}, duck stories {CountDuckStories()}");
                if (pondCells == 0) Check(duckGround.Count == 0 && Sim.EcoDucks == 0, "h: no drawn water, no duck ground and no ducks");
                Check(CountDuckStories() == 0, "h: no duck story on a living-land box");

                // ---- e2) the seed when it is unlocked: ghost, two-finger pan, second tap plants
                yield return AdvanceUntil(hud, "the seed", () => Sim.CanPlantTree, 8);
                if (!Sim.CanPlantTree) { Cheat("the seed", EcoDebug.SeedInHand()); hud.SetHand(GameHud.KitTool.None); yield return Frames(5); }
                if (Sim.CanPlantTree)
                {
                    var sp = SeedPlaceView.Instance;
                    yield return PickFromKit(hud, "kit-seed");
                    Check(hud.Hand == GameHud.KitTool.Seed, "e: the seed in hand by touch");
                    Check(KitWord(hud).Contains(GameHud.PutAway), $"e: Put away with the seed ('{KitWord(hud)}')");
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return Shot("f3_e2_seed_in_hand");
                    int cell = -1; float best = float.MaxValue;
                    var mid = new Vector2(Screen.width * 0.45f, Screen.height * 0.5f);
                    for (int c = 0; c < SiteMap.N; c++)
                    {
                        if (Sim.TreeCellFor(SiteMap.CXc(c), SiteMap.CZc(c)) != c) continue;
                        var p = sp.ScreenOf(c);
                        if (p.x < Screen.width * 0.15f || p.x > Screen.width * 0.7f || p.y < Screen.height * 0.25f || p.y > Screen.height * 0.75f || UiInput.Over(p)) continue;
                        if (!mist.GroundPoint(p, out var at) || SiteMap.CellAt(at.x, at.z) != c) continue;
                        float d = (p - mid).sqrMagnitude; if (d < best) { best = d; cell = c; }
                    }
                    if (cell >= 0)
                    {
                        yield return TwoFingerPan(sp.ScreenOf(cell), 3);
                        Check(!sp.GhostShown && Sim.State.eco.treeCell < 0, "e: a two-finger pan with the seed shows and plants nothing");
                        yield return TapAt(sp.ScreenOf(cell));
                        yield return Frames(5);
                        Check(sp.GhostShown && Sim.State.eco.treeCell < 0, $"e: the first tap shows the seed's ghost, plants nothing ({sp.Last})");
                        yield return new WaitForSecondsRealtime(0.8f);
                        yield return Shot("f3_e3_seed_ghost");
                        yield return TapAt(sp.ScreenOf(sp.GhostCell));
                        yield return Frames(10);
                        Check(Sim.State.eco.treeCell >= 0, $"e: the second tap plants ({sp.Last})");
                        yield return new WaitForSecondsRealtime(1f);
                        yield return Shot("f3_e4_seed_planted");
                    }
                    else Miss("e: open ground for the seed on screen");
                    yield return PutDownTool(hud);
                }

                // ---- f) the life jar: the thicket, then the rabbits
                yield return AdvanceUntil(hud, "the thicket's jar", () => Sim.JarReady(LifeSpecies.Thicket) || Sim.LifeIsPlaced(LifeSpecies.Thicket), 12);
                if (!Sim.JarReady(LifeSpecies.Thicket)) Cheat("the thicket's jar", "jars ready " + Sim.DebugJarsReady());
                yield return Place(hud, LifeSpecies.Thicket, "thicket");
                yield return AdvanceUntil(hud, "the rabbits' jar", () => Sim.JarReady(LifeSpecies.Rabbits) || Sim.LifeIsPlaced(LifeSpecies.Rabbits), 8);
                if (!Sim.JarReady(LifeSpecies.Rabbits)) Cheat("the rabbits' jar", "jars ready " + Sim.DebugJarsReady());
                yield return Place(hud, LifeSpecies.Rabbits, "rabbits");
                CheckWeeds("after the jars");

                // ---- g) mist near the glass: the soil face shows a wet band (#524)
                PeopleView.DebugSetHour(12f);
                {
                    var rig = Camera.main.GetComponent<BoxCamera>();
                    rig.CancelFlight(); rig.target = box.transform.position; rig.zoom = 1f; rig.Apply();
                    yield return new WaitForSecondsRealtime(1f);
                    // the edge cell lowest on screen (the front glass), clear of the HUD
                    int edge = -1; float lowY = float.MaxValue;
                    for (int i = 4; i < SiteMap.NX - 4; i++)
                        foreach (int j in new[] { 0, SiteMap.NZ - 1 })
                        {
                            int c = SiteMap.Index(i, j);
                            var p = ScreenOf(SiteMap.CXc(c), SiteMap.CZc(c));
                            if (p.y < Screen.height * 0.2f || UiInput.Over(p) || p.x < Screen.width * 0.2f || p.x > Screen.width * 0.8f) continue;
                            if (p.y < lowY) { lowY = p.y; edge = c; }
                        }
                    if (edge >= 0)
                    {
                        float ex = SiteMap.CXc(edge), ez = SiteMap.CZc(edge);
                        var mz = (ez < 0 ? 0.25f : -0.25f);
                        yield return PickFromKit(hud, "kit-mister");
                        int wet0 = EcoSurface.Instance != null ? EcoSurface.Instance.WetCells : -1;
                        yield return Hold(ScreenOf(ex, ez + mz), 2.0f);
                        yield return Hold(ScreenOf(ex + 0.3f, ez + mz), 1.5f);
                        yield return PutDownTool(hud);
                        yield return new WaitForSecondsRealtime(1.5f);
                        Log($"g mist at the glass ({ex:0.00},{ez:0.00}): wet cells {wet0} -> {EcoSurface.Instance?.WetCells}, wet at edge {EcoSurface.Instance?.WetShown(edge):0.00}");
                        Check(EcoSurface.Instance != null && EcoSurface.Instance.WetShown(edge) > 0.05f, "g: the edge cell reads wet");
                        yield return Shot("f3_g_wet_default");
                        yield return Fly(box, box.land.OnGround(ex, ez), 1.2f);
                        yield return new WaitForSecondsRealtime(0.5f);
                        yield return Shot("f3_g_wet_close");
                    }
                    else Miss("g: a front glass cell on screen");
                }

                // ---- h2) ducks: when the pond holds enough water, the ducks' jar; placed by touch, they swim on the water
                yield return AdvanceUntil(hud, "the ducks' jar", () => Sim.JarReady(LifeSpecies.Ducks) || Sim.LifeIsPlaced(LifeSpecies.Ducks), 8);
                if (!Sim.JarReady(LifeSpecies.Ducks))
                {
                    // with no pond the jar must wait for water (#519): check that first, then make the pond
                    Sim.DebugJarsReady();
                    Log($"h ducks' jar readied with no pond: ready {Sim.JarReady(LifeSpecies.Ducks)}, ducks {Sim.EcoDucks}");
                    Cheat("a pond", EcoDebug.Table(0.3f));
                    yield return Frames(10);
                    Cheat("the ducks' jar", "jars ready " + Sim.DebugJarsReady());
                }
                {
                    int pc = Enumerable.Range(0, EcoLand.N).Count(c => Sim.EcoOpenWater(c));
                    Sim.LifeGround(LifeSpecies.Ducks, duckGround);
                    Log($"h pond cells {pc}, duck ground {duckGround.Count}, all on water {duckGround.All(c => Sim.EcoOpenWater(c))}, duck stories {CountDuckStories()}");
                    Check(duckGround.All(c => Sim.EcoOpenWater(c)), "h: the duck ground is all open water");
                }
                if (Sim.JarReady(LifeSpecies.Ducks))
                {
                    yield return Place(hud, LifeSpecies.Ducks, "ducks");
                    PeopleView.DebugSetHour(10f);
                    yield return new WaitForSecondsRealtime(3f);
                    var pl = PondLife.Instance;
                    Log($"h ducks {Sim.EcoDucks}, shown {pl?.DucksShown}, home on water {Sim.EcoOpenWater(Sim.State.eco.duckHome)}");
                    Check(pl != null && pl.DucksShown > 0, "h: ducks placed by touch are visible");
                    Check(Sim.EcoOpenWater(Sim.State.eco.duckHome), "h: the ducks' home is on the water");
                    var home = Sim.State.eco.duckHome;
                    if (home >= 0) yield return Fly(box, box.land.OnGround(SiteMap.CXc(home), SiteMap.CZc(home)), 1.4f);
                    yield return Shot("f3_h_ducks_on_water");
                }
                Check(CountDuckStories() == 0, "h: still no old duck story");

                // ---- i) night with a grown Keeper's tree: a faint warm pool (#525)
                if (Sim.State.eco.treeCell >= 0)
                {
                    yield return AdvanceUntil(hud, "the tree grown (stage 3+)", () => Sim.EcoTree.stage >= KeeperTreeKit.PoolStage, 12);
                    if (Sim.EcoTree.stage < KeeperTreeKit.PoolStage) Cheat("the tree grown", EcoDebug.TreeStage(KeeperTreeKit.PoolStage));
                    var tr = Sim.EcoTree;
                    var tl = box.land.OnGround(tr.x, tr.z);
                    PeopleView.DebugSetHour(22.5f);
                    yield return new WaitForSecondsRealtime(2f);
                    var rig = Camera.main.GetComponent<BoxCamera>();
                    rig.CancelFlight(); rig.target = box.transform.TransformPoint(tl); rig.zoom = 1f; rig.Apply();
                    yield return new WaitForSecondsRealtime(1f);
                    yield return Shot("f3_i_tree_night_default");
                    yield return Fly(box, tl, 1.3f);
                    yield return Shot("f3_i_tree_night_close");
                    Log($"i tree stage {tr.stage}, glow {KeeperTreeKit.Glow(22.5f):0.00}");
                }
                else Miss("i: no Keeper's tree planted");

                // ---- d) the caption lines seen over the whole run
                CheckWeeds("end");
                Log($"c weeds: {weedChecks} checks, {weedsSeen} pieces seen, {weedsTrodden} on trodden ground");
                Check(weedsTrodden == 0, $"c: no weed or vine on the fire's ring or camp ground ({weedsTrodden} of {weedsSeen})");
                Log($"d caption lines {watch.Lines.Count}, crowd {watch.Crowd}, crowd named {watch.CrowdNamed}, 'someone' {watch.Someone}; first crowd {watch.FirstCrowd}");
                foreach (var l in watch.Lines.Take(30)) Log("d line: " + l);
                Check(watch.Someone == 0, "d: no 'someone' on the caption");
                if (watch.Crowd == 0) Miss("d: no crowd line on the caption");
                else Check(watch.CrowdNamed == watch.Crowd, $"d: every crowd line names its speaker ({watch.CrowdNamed}/{watch.Crowd})");

                Log("RESULT missed: " + string.Join("; ", missed));
                Log("RESULT state cheats: " + string.Join("; ", cheats));
                Log("RESULT failed: " + string.Join("; ", failed));
                Assert.IsEmpty(failed, "failed: " + string.Join("; ", failed));
            }
            finally
            {
                if (watch != null) Object.DestroyImmediate(watch.gameObject);
                var p = PeopleView.Find();
                if (p != null) p.timeScale = 1f;
                UseLivingLand(false);
            }
        }
    }
}
