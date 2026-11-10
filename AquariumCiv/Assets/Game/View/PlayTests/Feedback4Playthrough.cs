using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Playtest-feedback batch 4 played the way a phone plays it (1179x2556, living-land box, seed 4242). Real HUD taps and touches for every
    /// feature beat; cheats only reach a state and each logs "[f4] STATE CHEAT". Beats: a) the free pan at close/mid/far zoom (#538);
    /// b) the camp by day and night: feather stakes, stew pot, drying rack, fish rack, tent (#526/#540); c) two kids free at once play together (#541);
    /// d) ducks then fish placed by Kit and touch on the pond (#542); e) the pond growing with fishers on dry feet (#495); f) settlers while the pond
    /// is small, then "Pond to full" (#539); g) a catch goes basket, rack, pile (#543). Each beat is in its own try/catch: a failure is recorded as a
    /// soft failure and the run goes on; the test asserts at the end that there are none. Shots: Temp/agent/f4/*.png. Explicit.
    /// Note: in this build one finger orbits and TWO fingers pan, so beat a pans with two fingers (as CameraGlassFeelPlayTests does).
    /// </summary>
    [Explicit, Category("Feedback4Play")]
    public class Feedback4Playthrough : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        readonly List<string> soft = new List<string>();
        readonly List<string> cheats = new List<string>();

        static void Log(string s) => Debug.Log("[f4] " + s);
        void Cheat(string what, string r) { cheats.Add(what); Debug.LogWarning($"[f4] STATE CHEAT {what}: {r}"); }
        void Soft(string beat, string why) { string s = $"{beat}: {why}"; soft.Add(s); Debug.LogWarning("[f4] SOFT FAIL " + s); }
        void Check(string beat, bool ok, string what) { Log($"{(ok ? "OK  " : "FAIL")} {beat}: {what}"); if (!ok) soft.Add($"{beat}: {what}"); }

        /// <summary>Runs a beat, catching anything it throws as a soft failure so the next beat still runs.</summary>
        IEnumerator Beat(string name, IEnumerator body)
        {
            Log($"---- beat {name}");
            while (true)
            {
                object cur;
                try { if (!body.MoveNext()) break; cur = body.Current; }
                catch (Exception e) { Soft(name, e.GetType().Name + ": " + e.Message); break; }
                yield return cur;
            }
            // leave nothing in hand for the next beat
            var hud = GameHud.Instance;
            if (hud != null) { if (hud.SheetOpen) hud.CloseSheet(); if (hud.Hand != GameHud.KitTool.None) hud.SetHand(GameHud.KitTool.None); }
            yield return Frames(5);
        }

        // ---------------------------------------------------------------- shared helpers
        static void DefaultView()
        {
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            Rig.CancelFlight(); Rig.target = box.transform.position; Rig.zoom = 1f; Rig.Apply();
        }

        static void Look(Vector3 world, float zoom, float pitch = 52f)
        {
            Rig.CancelFlight(); Rig.target = world; Rig.zoom = zoom; Rig.pitch = pitch; Rig.Apply();
        }

        static Vector3 World(BoxView box, float x, float z) => box.transform.TransformPoint(box.land.OnGround(x, z));

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

        static bool CameraInBox(BoxCamera r)
        {
            var p = r.transform.position;
            return CameraGlass.CameraInsideBox(p.x, p.y, p.z, Land.HX + BoxCameraMath.HullSide, Land.HZ + BoxCameraMath.HullSide, BoxCameraMath.ShelfY, Land.GlassTop, 0.3f);
        }

        // ---------------------------------------------------------------- a) the free pan (#538)
        IEnumerator PanBy(Vector2 dir, int steps, List<Vector3> trace, List<bool> inBox, int[] ids)
        {
            var a = new Vector2(Screen.width * 0.4f, Screen.height * 0.5f); var b = new Vector2(Screen.width * 0.6f, Screen.height * 0.5f);
            int ia = touchId++, ib = touchId++; ids[0] = ia; ids[1] = ib;
            BeginTouch(ia, a, queueEventOnly: true); BeginTouch(ib, b, queueEventOnly: true);
            yield return Frames(3);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                MoveTouch(ia, a + dir * t, delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(ib, b + dir * t, delta: Vector2.zero, queueEventOnly: true);
                yield return null;
                trace.Add(Rig.target); inBox.Add(CameraInBox(Rig));
            }
        }

        IEnumerator BeatCamera()
        {
            float reach = Land.HX - CameraGlass.Margin - CameraGlass.Gap - 0.3f;
            foreach (var (zoomName, zoom) in new[] { ("close", CameraReach.MinZoom), ("mid", 0.6f), ("far", 1f) })
                foreach (float side in new[] { -1f, 1f })
                {
                    Rig.EaseTo(new Vector3(0f, 1f, 0f), 28f, 34f, zoom, 0f); // the camp's opening yaw / pitch
                    yield return Frames(40);
                    var trace = new List<Vector3>(); var inBox = new List<bool>(); var ids = new int[2];
                    yield return PanBy(new Vector2(-side, 0f) * (Screen.height * 1.5f), 60, trace, inBox, ids);
                    for (int i = 0; i < 15; i++) { yield return null; trace.Add(Rig.target); inBox.Add(CameraInBox(Rig)); }
                    float x = Rig.target.x;
                    float maxAbs = trace.Max(p => Mathf.Abs(p.x));
                    string tag = $"{zoomName}_{(side < 0 ? "left" : "right")}";
                    Log($"a {zoomName} side {side:+0;-0}: pivot x {x:F2} (max |x| {maxAbs:F2}) against the wall {side * Land.HX:F2}; reach target {reach:F2}; margin to the glass {Land.HX - Mathf.Abs(x):F2}; camera inside the box ever: {inBox.Contains(true)}");
                    yield return Shot("f4/a_pan_" + tag);
                    EndTouch(ids[0], new Vector2(Screen.width * 0.4f, Screen.height * 0.5f), delta: Vector2.zero, queueEventOnly: true);
                    EndTouch(ids[1], new Vector2(Screen.width * 0.6f, Screen.height * 0.5f), delta: Vector2.zero, queueEventOnly: true);
                    yield return Frames(30);
                    Check("a", !inBox.Contains(true), $"{tag}: the camera never sits inside the box");
                    Check("a", maxAbs < Land.HX, $"{tag}: the pivot never reaches the glass ({maxAbs:F2} < {Land.HX:F2})");
                    Check("a", Mathf.Abs(x) > reach, $"{tag}: a drag across reaches the side wall (|x| {Mathf.Abs(x):F2} > {reach:F2})");
                }
            DefaultView();
        }

        // ---------------------------------------------------------------- b) the camp by day and night (#526/#540)
        IEnumerator BeatCamp(BoxView box, PeopleView pv)
        {
            Cheat("camp props: show all", ConsequenceCheats.ShowCampProps()); // SimDebugPanel fb4 #540 button's call
            yield return new WaitForSecondsRealtime(2.5f);
            pv.Planner.FirePoint(out float fx, out float fz);
            pv.Planner.HideFrame(out float rx, out float rz, out _, out _);
            float rackAngle = Mathf.Atan2(rz - fz, rx - fx);
            float fishAngle = FishRackSpot.AngleFor(fx, fz, Sim.State.seed);
            Transform pot = null; var feathers = new List<Transform>();
            foreach (var kv in ConsequencePropsView.Instance.Standing)
            {
                if (kv.Value == null) continue;
                if (kv.Value.name.StartsWith("Consequence_feather")) feathers.Add(kv.Value);
                if (kv.Value.name.StartsWith("Consequence_stew_pot")) pot = kv.Value;
            }
            Vector2 LocalXZ(Transform t) { var l = box.transform.InverseTransformPoint(t.position); return new Vector2(l.x, l.z); }
            Log($"b fire ({fx:0.00},{fz:0.00}) rack ({rx:0.00},{rz:0.00}) stakes {feathers.Count} pot {(pot != null ? "yes" : "none")}");
            Check("b", feathers.Count > 0, "a feather stake stands");
            Check("b", pot != null, "the stew pot stands");
            if (pot != null)
            {
                var pp = LocalXZ(pot);
                float toRack = CampSpots.DistanceToRack(fx, fz, rackAngle, LifePlanner.HideFrameDistance, pp.x, pp.y);
                float toFish = CampSpots.DistanceToRack(fx, fz, fishAngle, CampSpots.FishDistance, pp.x, pp.y, CampSpots.FishHalfDepth, CampSpots.FishHalfLength);
                Log($"b pot<->drying rack footprint {toRack:0.000} u (centre {Vector2.Distance(pp, new Vector2(rx, rz)):0.00}); pot<->fish rack footprint {toFish:0.000} u");
                Check("b", toRack >= CampSpots.PropRadius * 0.5f, $"the pot clears the drying rack ({toRack:0.000} u)");
                Check("b", toFish >= CampSpots.PropRadius * 0.5f, $"the pot clears the fish rack ({toFish:0.000} u)");
            }
            var tents = Sim.State.buildings.Where(b => b.lotId != 0).Select(b => new Vector2(b.x, b.z)).ToList();
            Log($"b tents {tents.Count}");
            foreach (var f in feathers)
            {
                var fp = LocalXZ(f);
                float d = tents.Count == 0 ? float.NaN : tents.Min(t => Vector2.Distance(t, fp));
                Log($"b stake ({fp.x:0.00},{fp.y:0.00}) nearest tent {d:0.00} u");
                if (tents.Count > 0) Check("b", d >= 0.2f, $"the stake stands off the tent ({d:0.00} u)");
            }
            var fire = World(box, fx, fz); var rack = World(box, rx, rz);
            foreach (float hour in new[] { 11.5f, 22.5f })
            {
                string t = hour < 18f ? "day" : "night";
                PeopleView.DebugSetHour(hour);
                yield return new WaitForSecondsRealtime(2.5f);
                Look((rack + fire) * 0.5f, 0.55f);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot($"f4/b_camp_{t}_default");
                Look(fire, 0.3f);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot($"f4/b_camp_{t}_fire");
                if (feathers.Count > 0 && feathers[0] != null)
                {
                    Look(feathers[0].position, 0.2f);
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Shot($"f4/b_camp_{t}_stake");
                }
            }
            PeopleView.DebugSetHour(10f);
            DefaultView();
        }

        // ---------------------------------------------------------------- c) two kids free at once (#541)
        IEnumerator BeatKids(PeopleView pv)
        {
            var sim = Sim;
            var born = new[] { sim.DebugBirth(), sim.DebugBirth() };
            Cheat("two births", "two kids added with DebugBirth, aged 7");
            SimLayoutSource.DebugSkipYears(0.1f);
            yield return null;
            foreach (var n in born) n.age = 7f;
            pv.Planner.Invalidate();
            sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return Frames(4);
            for (int calm = 0, f = 0; calm < 120 && f < 1200; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.DebugSetHour(10f);
            yield return Frames(4);
            string said = KidPlayDirector.DebugTwo();
            Cheat("two kids play", said);
            if (!said.StartsWith("two kids")) { Soft("c", "the cheat did not start: " + said); yield break; }
            yield return Frames(120);
            var kids = pv.Planner.People.Where(w => pv.Planner.StageOf(w) == LifeStage.Kid).Select(w => w.Key).ToList();
            Log($"c kids {kids.Count}; group count {pv.KidPlay.GroupCount}; groups holding them {pv.KidPlay.GroupsOf(kids)}; {pv.KidPlay.Status()}");
            Check("c", kids.Count >= 2, $"two kids or more ({kids.Count})");
            Check("c", pv.KidPlay.GroupCount == 1, $"group count = 1 ({pv.KidPlay.GroupCount})");
            Check("c", pv.KidPlay.GroupsOf(kids) == 1, "both kids in the same group");
            yield return Frames(60);
            yield return Shot("f4/c_two_kids");
        }

        // ---------------------------------------------------------------- d) ducks, then fish, by Kit and touch (#542)
        static Vector2 SurfaceOf(int cell)
        {
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            float w = box.layout != null && box.layout.eco != null ? box.layout.eco.waterY : Land.WaterY;
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(SiteMap.CXc(cell), w, SiteMap.CZc(cell))));
        }

        static int WaterCell(LifeSpecies s)
        {
            var ground = new List<int>(); Sim.LifeGround(s, ground);
            int best = -1; float bestD = float.MaxValue;
            foreach (int c in ground)
            {
                if (!Sim.CanPlaceLife(s, c)) continue;
                var p = SurfaceOf(c);
                if (p.x < Screen.width * 0.12f || p.x > Screen.width * 0.88f || p.y < Screen.height * 0.2f || p.y > Screen.height * 0.78f || UiInput.Over(p)) continue;
                var d = Vector2.Distance(p, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        IEnumerator PlaceOnWater(GameHud hud, LifeSpecies s)
        {
            string tag = s.ToString().ToLowerInvariant();
            var lpv = LifePlaceView.Instance;
            int taps = 0;
            if (!Sim.JarReady(s)) { Soft("d", $"{s}: jar not ready"); yield break; }
            yield return PutDownTool(hud);
            yield return CalmHud(hud);
            yield return Until(() => !Rig.Leaning && !Rig.Gliding && !Rig.GlideArmed && !Rig.IsFlying, 30f);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean")); taps++; // Kit
            yield return new WaitForSecondsRealtime(0.5f);
            Check("d", hud.OpenSheet == GameHud.Kit && hud.LifeSlotShown, $"{s}: Kit opens with the life slot");
            yield return Shot($"f4/d_{tag}_0_kit");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life")); taps++;
            yield return new WaitForSecondsRealtime(0.4f);
            if (hud.LifeRowOpen)
            {
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-" + tag)); taps++;
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Check("d", hud.Hand == GameHud.KitTool.Life && hud.HeldLife == s, $"{s}: in hand ({hud.Hand}, {hud.HeldLife})");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Until(() => !Rig.IsFlying, 10f);
            yield return new WaitForSecondsRealtime(0.4f);
            Log($"d {s}: camera zoom after taking the tool {Rig.zoom:0.00}");
            Check("d", Rig.zoom < 0.65f && Rig.zoom >= 0.29f, $"{s}: the camera frames the whole pond (zoom {Rig.zoom:0.00})");
            int cell = WaterCell(s);
            if (cell < 0) { Soft("d", $"{s}: no glowing water on screen"); yield break; }
            yield return Shot($"f4/d_{tag}_1_glow");
            yield return TapAt(SurfaceOf(cell)); taps++;
            yield return Frames(4);
            Check("d", lpv.GhostShown, $"{s}: a tap on the water shows the ghost ({lpv.Last})");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Until(() => !Rig.IsFlying, 10f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot($"f4/d_{tag}_2_ghost");
            var g = lpv.GhostScreen; int far = -1; float farD = 0f;
            var ground = new List<int>(); Sim.LifeGround(s, ground);
            foreach (int c in ground)
            {
                var q = SurfaceOf(c);
                if (q.x < Screen.width * 0.1f || q.x > Screen.width * 0.9f || q.y < Screen.height * 0.2f || q.y > Screen.height * 0.8f || UiInput.Over(q)) continue;
                float d = Vector2.Distance(q, g);
                if (d > farD) { farD = d; far = c; }
            }
            if (far < 0) { Soft("d", $"{s}: no water to confirm on"); yield break; }
            yield return TapAt(SurfaceOf(far)); taps++; // any tap on the water confirms
            yield return Frames(8);
            Check("d", Sim.LifeIsPlaced(s), $"{s}: placed ({lpv.Last})");
            Log($"d {s}: taps needed {taps} (Kit, life slot{(taps >= 5 ? ", species row" : "")}, water, water); confirming tap {farD:0} px from the ghost");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot($"f4/d_{tag}_3_placed");
            DefaultView();
        }

        IEnumerator BeatPlace(GameHud hud)
        {
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            Cheat("pond", "five big mists + " + EcoDebug.Table(0.3f));
            Sim.DebugGrowFarWood();
            yield return Frames(20);
            Cheat("jars ready", "ducks and fish jars: " + Sim.DebugJarsReady());
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(4f);
            DefaultView();
            yield return Frames(10);
            foreach (var s in new[] { LifeSpecies.Ducks, LifeSpecies.Fish }) yield return PlaceOnWater(hud, s);
        }

        // ---------------------------------------------------------------- e) the pond grows, the fishers keep dry feet (#495)
        static float NearestWater(float x, float z)
        {
            float best = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
                if (Sim.EcoCoverAt(c) == EcoCover.Water) best = Mathf.Min(best, AquariumCiv.Sim.Settle.Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z));
            return best;
        }

        static int Fishers(PeopleView pv, List<Vector2> feet)
        {
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            feet.Clear();
            foreach (var a in pv.Activities)
            {
                if (a.kind != ActivityKind.Fish || a.moving || a.enRoute) continue;
                int i = who.FindIndex(w => w.Key == a.who.Key);
                if (i >= 0) feet.Add(new Vector2(xs[i], zs[i]));
            }
            return feet.Count;
        }

        IEnumerator BeatGrowingPond(BoxView box, PeopleView pv)
        {
            if (!Sim.LifeIsPlaced(LifeSpecies.Ducks) || !Sim.LifeIsPlaced(LifeSpecies.Fish))
            {
                if (Sim.DebugPlaceNearCamp(LifeSpecies.Ducks) >= 0 | Sim.DebugPlaceNearCamp(LifeSpecies.Fish) >= 0) Cheat("ducks and fish", "d did not place them: placed near the camp");
            }
            Cheat("small pond", EcoDebug.PondDries() + " / " + EcoDebug.WaterBreaksNow());
            yield return Frames(20);
            PeopleView.DebugSetHour(10f);
            var feet = new List<Vector2>();
            int prev = 0;
            for (int step = 0; step < 4; step++)
            {
                if (step > 0) Cheat("pond +1 step", EcoDebug.PondStep());
                yield return Frames(20);
                pv.Planner.ClearLive();
                Cheat("fishers sent", FishingView.DebugBiteNow());
                yield return Until(() => Fishers(pv, feet) > 0, 30f);
                yield return Frames(30);
                int n = Fishers(pv, feet);
                int dry = 0; float worstD = 0f;
                foreach (var f in feet)
                {
                    bool wet = PondShore.IsWet(Sim, f.x, f.y);
                    float d = NearestWater(f.x, f.y);
                    if (!wet) dry++;
                    worstD = Mathf.Max(worstD, d);
                    Log($"e step {step}: fisher feet ({f.x:0.00},{f.y:0.00}) dry {!wet} distance to open water {d:0.00} u");
                }
                Log($"e step {step}: pond {Sim.EcoPondCells} cells, fishers {n}, dry {dry}, farthest from water {worstD:0.00} u");
                Check("e", n > 0, $"step {step}: somebody fishes");
                Check("e", dry == n, $"step {step}: every fisher on dry ground ({dry}/{n})");
                Check("e", worstD <= 0.3f, $"step {step}: within 0.3 u of open water ({worstD:0.00})");
                Check("e", Sim.EcoPondCells >= prev, $"step {step}: the pond never shrinks ({prev} -> {Sim.EcoPondCells})");
                prev = Sim.EcoPondCells;
                var fp = feet.Count > 0 ? feet[0] : Vector2.zero;
                if (feet.Count > 0) Look(World(box, fp.x, fp.y), 0.35f, 50f); else DefaultView();
                yield return Frames(6);
                yield return Shot($"f4/e_pond_step{step}");
            }
            DefaultView();
        }

        // ---------------------------------------------------------------- f) settlers on a small pond, then it fills (#539)
        IEnumerator BeatSettlers()
        {
            Cheat("small pond", EcoDebug.PondDries() + " / " + EcoDebug.WaterBreaksNow());
            yield return Frames(10);
            Cheat("settlers (shore)", EcoDebug.SettlersArrive(true));
            var g = Sim.ApartGroups[Sim.ApartGroups.Count - 1];
            PeopleView.DebugSetHour(19.2f);
            Rig.GoHome(0.01f, true);
            yield return new WaitForSecondsRealtime(2.5f);
            var view = ApartGroupsView.Instance;
            foreach (bool full in new[] { false, true })
            {
                string tag = full ? "full" : "small";
                if (full)
                {
                    Cheat("pond to full", EcoDebug.PondToFull());
                    yield return new WaitForSecondsRealtime(5.5f); // the view re-asks the spot every 5 s
                    yield return Frames(10);
                }
                if (!view.TryGetCamp(g.id, out var fire, out _, out _)) { Soft("f", tag + ": the camp is not drawn"); continue; }
                bool wet = PondShore.IsWet(Sim, fire.x, fire.y), future = PondShore.InFutureWater(Sim, fire.x, fire.y, 0f);
                bool lit = view.TryGetPoolRadius(g.id, out float r);
                float nearest = float.MaxValue;
                for (int c = 0; c < EcoLand.N; c++)
                    if (Sim.EcoCoverAt(c) == EcoCover.Water) nearest = Mathf.Min(nearest, AquariumCiv.Sim.Settle.Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), fire.x, fire.y));
                Log($"f {tag} pond ({Sim.EcoPondCells} cells): fire ({fire.x:0.00},{fire.y:0.00}) wet={wet} inFutureWater={future}; pool radius {(lit ? r.ToString("0.00") : "none")}; nearest water cell {nearest:0.00} u");
                Check("f", !wet, $"{tag}: camp/fire wet = false");
                Check("f", !future, $"{tag}: fire outside the pond's full basin");
                Check("f", lit, $"{tag}: the fire is lit");
                if (lit) Check("f", nearest > r, $"{tag}: the fire's pool ({r:0.00}) reaches no wet cell (nearest {nearest:0.00})");
                ApartGroupsView.DebugFly(g.id, 0.4f);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot($"f4/f_settlers_{tag}_pond");
                if (!full) Rig.GoHome(0.01f, true);
            }
            PeopleView.DebugSetHour(10f);
            DefaultView();
        }

        // ---------------------------------------------------------------- g) a catch goes basket, rack, pile (#543)
        IEnumerator BeatFishChain(BoxView box, PeopleView pv)
        {
            var sim = Sim;
            if (!sim.FishingWater)
            {
                sim.DebugPlaceNearCamp(LifeSpecies.Ducks); sim.DebugPlaceNearCamp(LifeSpecies.Fish);
                Cheat("ducks and fish", "re-placed near the camp; fishing water " + sim.FishingWater);
            }
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            PeopleView.DebugSetHour(10f);
            yield return Frames(20);
            pv.Planner.ClearLive();
            var fv = FishingView.Ensure(); var rv = FishRackView.Ensure(box);
            var props = GlassVoice.Instance.GetComponent<StashProps>();
            Check("g", fv != null && rv != null && props != null, "fishing, rack and stash views exist");
            if (fv == null || rv == null || props == null) yield break;
            // 1. a real catch lands in the basket
            int kept0 = fv.FishKept;
            Cheat("fishers sent", FishingView.DebugBiteNow());
            yield return Until(() => fv.FishKept > kept0, 60f);
            Log($"g catch: kept {fv.FishKept - kept0}, in basket {sim.FishInBasket}");
            Check("g", fv.FishKept > kept0 && sim.FishInBasket > 0, "a catch lands in the fish basket");
            yield return Frames(30);
            var fisher = pv.Activities.FirstOrDefault(a => a.kind == ActivityKind.Fish && !a.moving);
            if (fisher.kind == ActivityKind.Fish && pv.TryGetDollPosition(fisher.who, out var at)) Look(World(box, at.x, at.y), 0.18f, 45f);
            yield return Frames(6);
            yield return Shot("f4/g_1_basket");
            // 2. the basket goes to the rack, fish by fish
            bool haveRack = pv.Planner.TryGetFishRack(pv.Now, out var rack);
            Check("g", haveRack, "the fish rack stands by the fire");
            if (!haveRack) yield break;
            var rackAt = World(box, rack.x, rack.z) + Vector3.up * 0.06f;
            pv.Planner.FirePoint(out float fx, out float fz);
            var fire = World(box, fx, fz);
            Look((rackAt + fire) * 0.5f, 0.42f);
            yield return Frames(8);
            yield return Shot("f4/g_2_rack_wide");
            pv.Planner.ClearLive();
            Cheat("fill fish basket", FishRackView.DebugFillBasket()); // only to speed the chain up
            int hung = sim.FishInBasket, last = 0, steps = 0; bool shot = false;
            Look(rackAt, 0.2f, 40f);
            for (int i = 0; i < 1800 && sim.FishOnRack < hung; i++)
            {
                yield return null;
                int now = rv.FreshShown + rv.DryShown;
                if (now > last) steps++;
                last = now;
                if (!shot && now >= 3) { shot = true; yield return Shot("f4/g_3_hanging"); }
            }
            Log($"g rack: hung {sim.FishOnRack}/{hung}, rack grew in {steps} steps, basket {sim.FishInBasket}");
            Check("g", sim.FishOnRack >= hung && sim.FishInBasket == 0, "the basket's fish hang on the rack");
            Check("g", steps >= 3, $"the rack fills fish by fish ({steps} steps)");
            yield return Frames(10);
            yield return Shot("f4/g_4_rack_fresh");
            // 3. dry fish go to the pile
            pv.Planner.ClearLive();
            Cheat("dry fish on the rack", FishRackView.DebugRack());
            yield return Until(() => rv.DryShown >= 6, 5f);
            Look(rackAt, 0.2f, 40f);
            yield return Frames(6);
            yield return Shot("f4/g_5_rack_dry");
            yield return Until(() => sim.FishInPile >= 6, 90f);
            Log($"g pile: {sim.FishInPile} fish in the pile, {sim.FishOnRack} on the rack, pile shows {props.FishShown}");
            Check("g", sim.FishInPile >= 6 && sim.FishOnRack == 0, "the dry fish reached the pile");
            Check("g", props.FishShown > 0, "the pile shows the fish");
            if (pv.Planner.TryGetStash(out var pile))
            {
                StashProps.DebugStock(sim, true);
                yield return Frames(6);
                Look(World(box, pile.x, pile.z) + Vector3.up * 0.03f, 0.2f, 45f);
                yield return Frames(8);
                yield return Shot("f4/g_6_pile_with_fish");
            }
            DefaultView();
        }

        // ---------------------------------------------------------------- the run
        [UnityTest, Timeout(3600000)]
        public IEnumerator Feedback4_PlayedByTouch()
        {
            UseLivingLand(true);
            ShowMeDirector.BubblesOn = false;
            try
            {
                yield return OpenSlice(1179, 2556, seed: 4242);
                GameUiPanel.AcceptInputUnfocused(true);
                Assert.IsTrue(Sim.EcologyOn, "a living-land box");
                var hud = GameHud.Instance;
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var pv = PeopleView.Find();
                Cheat("first run skipped + 50 seasons", EcoDebug.SkipSeasons(50)); // a pass-sized run can't reach a camp by time alone (tooling 2026-11-09)
                if (Sim.State.era != Era.Camp) Cheat("camp gate", EcoDebug.CampGate());
                Log($"era {Sim.State.era}, year {Sim.State.Year:0.0}, buildings {Sim.State.buildings.Count}");
                PeopleView.DebugSetHour(10f);
                yield return new WaitForSecondsRealtime(2f);
                DefaultView(); // reset the camera before anything else
                yield return Frames(10);

                yield return Beat("a camera", BeatCamera());
                yield return Beat("b camp", BeatCamp(box, pv));
                yield return Beat("c kids", BeatKids(pv));
                yield return Beat("d place", BeatPlace(hud));
                yield return Beat("e growing pond", BeatGrowingPond(box, pv));
                yield return Beat("f settlers", BeatSettlers());
                yield return Beat("g fish chain", BeatFishChain(box, pv));

                Log("RESULT state cheats: " + string.Join("; ", cheats));
                Log("RESULT soft failures (" + soft.Count + "): " + string.Join(" || ", soft));
                Assert.AreEqual(0, soft.Count, "soft failures: " + string.Join(" || ", soft));
            }
            finally
            {
                ShowMeDirector.BubblesOn = true;
                var p = PeopleView.Find();
                if (p != null) p.timeScale = 1f;
                UseLivingLand(false);
            }
        }
    }
}
