// #789 tester fix (2026-11-27): the last evening seen from the player's own camera. From the home view at 750x1334 the cheat "Last evening: play
// now" plays it, and the test measures what the player sees: (a) the #783 lean holds the family in the middle 60 % of the screen for most of the
// scene, (b) the elder is at least the #783 goal tall (5 % of the screen's height), or the most the zoom floor allows, (c) the elder and the partner
// face the camera (within 70 degrees) while they sit. Shots from the player's camera at the six beats go to Temp/agent/789live/ (the gather, the
// hand, the stars, the help up, the walk, the wave). Run: PROJ=... bash Tools/run-tests.sh "playmode|LastEveningLeanPlayTests||le-lean".
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("LastEvening789Lean")]
    public class LastEveningLeanPlayTests : PlayTestBase
    {
        /// <summary>A sitter faces the camera when its facing is within this of the way to the camera (degrees, on the ground).</summary>
        const float FacingMax = 70f;
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static PeopleView Pv => PeopleView.Find();
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();
        static readonly FieldInfo ClockField = typeof(CampLife).GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
        static float SceneClock(PeopleView pv) => ClockField != null ? (float)ClockField.GetValue(pv.Camp) : 0f;

        [TearDown]
        public void Restore() { CampLife.OnlyCheats = false; }

        /// <summary>A standing doll's height on screen at a spot (px), as the #783 lean measures it.</summary>
        static float StandingPx(Vector2 at)
        {
            var cam = Camera.main; var box = Box; var rig = Rig;
            float h = CampFrame.DollHeight * CameraReach.DollScale(rig.zoom, PeopleView.DollScaleAmount);
            float y = box.SurfaceHeight(at.x, at.y);
            var a = cam.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, y, at.y)));
            var b = cam.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, y + h, at.y)));
            return Mathf.Abs(b.y - a.y);
        }

        /// <summary>The way from a spot to the camera, on the ground (box-local x, z).</summary>
        static Vector2 ToCamera(Vector2 at)
        {
            var box = Box; var cam = Camera.main;
            var p = box.Root.InverseTransformPoint(cam.transform.position);
            var d = new Vector2(p.x - at.x, p.z - at.y);
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.down;
        }

        static float FacingOff(PersonRef who)
        {
            var pv = Pv;
            if (!pv.TryGetDollFacing(who, out var f) || !pv.TryGetDollPosition(who, out var at)) return -1f;
            return Vector2.Angle(f, ToCamera(at));
        }

        static Vector3 Viewport(Vector2 at)
        {
            var box = Box;
            return Camera.main.WorldToViewportPoint(box.Root.TransformPoint(new Vector3(at.x, box.SurfaceHeight(at.x, at.y) + 0.06f, at.y)));
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator FromTheHomeView_TheLeanHoldsTheFamily_TallEnough_FacingTheCamera_750x1334()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return new WaitForSeconds(2f);
            var sim = Sim; var pv = Pv; var rig = Rig;
            HeldMomentLean.Enabled = true; HeldMomentLean.DebugForget();
            float yaw0 = rig.yaw, zoom0 = rig.zoom;
            int leans0 = HeldMomentLean.Leans;
            string r = CampCheats.LastEveningPlayNow();
            Debug.Log("[789 lean] " + r);
            yield return Until(() => pv.Camp.LastEveningScene != null, 20f);
            var sc = pv.Camp.LastEveningScene;
            Assert.IsNotNull(sc, $"it plays ({r}; why: {pv.Camp.LastEveningWhy})");
            int elder = sc.keys[0];
            var en = sim.FindNotable(elder);
            int partner = en != null && sc.keys.Contains(en.partnerId) ? en.partnerId : -1;
            if (partner < 0) partner = sc.keys.Skip(1).FirstOrDefault(k => sim.FindNotable(k) is Notable n && n.age >= SimCore.AdultAge);
            var who = PersonRef.Notable(elder); var mate = PersonRef.Notable(partner);
            // the beats on the scene's own clock (LastEveningScenes.Evening)
            float tHand = sc.a, tStars = sc.b, tStood = sc.d, tIn = sc.c;
            float tRise = tStars + LastEveningScenes.LookFor + LastEveningScenes.NodFor + LastEveningScenes.StillFor;
            float tWave = tIn - LastEveningScenes.InFor - LastEveningScenes.WaveFor, tDoor = tWave - LastEveningScenes.TurnFor;
            var beats = new (string name, float at)[]
            {
                ("1_gather", tHand - 1.5f), ("2_hand", tHand + 3f), ("3_stars", tStars + 2f), ("4_helpup", tStood - 0.4f),
                ("5_walk", (tStood + tDoor) * 0.5f), ("6_wave", tWave + LastEveningScenes.WaveFor * 0.45f),
            };
            int nextBeat = 0;
            Debug.Log($"[789 lean] beats: hand {tHand:0.0} stars {tStars:0.0} rise {tRise:0.0} stood {tStood:0.0} door {tDoor:0.0} wave {tWave:0.0} in {tIn:0.0} length {sc.Length:0.0}; seat {sc.focus}; yaw {rig.yaw:0} pitch {rig.pitch:0} zoom {rig.zoom:0.00} floor {rig.minZoom:0.00}");
            Debug.Log("[789 lean] geo: " + pv.Camp.LastEveningGeo);
            var frames = new StringBuilder();
            int seen = 0, centred = 0, seatedN = 0, elderFacing = 0, mateFacing = 0, underRim = 0;
            var px = new List<float>();
            float started = Time.time;
            while (pv.Camp.Scenes.Contains(sc) && Time.time - started < 200f)
            {
                float t = SceneClock(pv) - sc.t0;
                var drawn = new List<Vector2>();
                foreach (int k in sc.keys) if (pv.TryGetDollTop(PersonRef.Notable(k), out _) && pv.TryGetDollPosition(PersonRef.Notable(k), out var p)) drawn.Add(p);
                if (t > 3f && drawn.Count > 0)
                {   // the family: the elder and those with them (once they walk home, the ones by the fire are not the scene)
                    var lead = pv.TryGetDollPosition(who, out var ep) && pv.TryGetDollTop(who, out _) ? ep : drawn[0];
                    var near = drawn.Where(q => (q - lead).magnitude < 0.5f).ToList();
                    Vector2 lo = near[0], hi = near[0];
                    foreach (var q in near) { lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
                    var v = Viewport((lo + hi) * 0.5f);
                    seen++;
                    if (v.z > 0f && v.x >= 0.2f && v.x <= 0.8f && v.y >= 0.2f && v.y <= 0.8f) centred++;
                    if (t > tHand && t < tRise)
                    {
                        seatedN++;
                        px.Add(StandingPx(lead));
                        float fe = FacingOff(who), fm = partner > 0 ? FacingOff(mate) : 0f;
                        if (pv.TryGetDollFeet(who, out var feetW) && pv.TryGetDollTop(who, out var topW) && (RimAcross(feetW) || RimAcross(topW) || RimAcross((feetW + topW) * 0.5f))) underRim++;
                        if (fe >= 0f && fe <= FacingMax) elderFacing++;
                        if (fm >= 0f && fm <= FacingMax) mateFacing++;
                        if (seatedN % 30 == 1) frames.AppendLine($"t {t:0.0}: middle ({v.x:0.00}, {v.y:0.00}) elder px {px[px.Count - 1]:0} facing off: elder {fe:0} partner {fm:0}; zoom {rig.zoom:0.00} pitch {rig.pitch:0}");
                    }
                }
                if (nextBeat < beats.Length && t >= beats[nextBeat].at)
                {
                    string name = beats[nextBeat++].name;
                    Debug.Log($"[789 lean] shot {name} at t {t:0.0}: zoom {rig.zoom:0.00} pitch {rig.pitch:0} yaw {rig.yaw:0}; lean {HeldMomentLean.LastWhy}; look {HeldMomentLean.LastLook}");
                    if (pv.TryGetDollPosition(who, out var wp)) Debug.Log($"[789 lean] {name}: elder at {wp}, viewport {Viewport(wp)}, facing off {FacingOff(who):0}");
                    if (pv.TryGetDollTop(who, out var wtop)) Debug.Log($"[789 lean] {name}: in front of the elder: {Occluders(wtop)}");
                    yield return Shot("789live/" + name);
                }
                yield return null;
            }
            Assert.IsFalse(pv.Camp.Scenes.Contains(sc), "the scene ends");
            px.Sort();
            float medPx = px.Count > 0 ? px[px.Count / 2] : 0f, want = StoryLean.ScenePx(Screen.height);
            bool atFloor = rig.minZoom > 0f && HeldMomentLean.LastZoomTo <= rig.minZoom + 0.015f;
            string report = $"leans {HeldMomentLean.Leans - leans0} ({HeldMomentLean.LastWhy}); centred {centred}/{seen}; under the glass's top edge {underRim}/{seatedN}; elder {medPx:0} px (goal {want:0}, at the floor {atFloor}, zoomTo {HeldMomentLean.LastZoomTo:0.00}); seated {seatedN}: elder facing {elderFacing}, partner facing {mateFacing}; yaw {yaw0:0} -> {rig.yaw:0}, zoom {zoom0:0.00}\n{frames}";
            Debug.Log("[789 lean] " + report);
            Assert.Greater(HeldMomentLean.Leans, leans0, "the last evening takes the lean: " + report);
            Assert.AreEqual(yaw0, rig.yaw, 0.5f, "yaw is never touched");
            Assert.GreaterOrEqual(centred, seen * 0.6f, "(a) the family in the middle 60 % for most of the scene: " + report);
            Assert.GreaterOrEqual(medPx, atFloor ? want * 0.55f : want * 0.7f, "(b) the elder is tall enough: " + report);
            Assert.GreaterOrEqual(elderFacing, seatedN * 0.8f, "(c) the elder faces the camera while seated: " + report);
            if (partner > 0) Assert.GreaterOrEqual(mateFacing, seatedN * 0.8f, "(c) the partner faces the camera while seated: " + report);
            Assert.LessOrEqual(underRim, seatedN * 0.2f, "the front glass's top edge is not across the elder while they sit: " + report);
            AssertNoErrors("the last evening");
        }

        /// <summary>Is the front glass's top edge (a bright band, with the lid seen edge-on) drawn across this world point? Its screen row is within
        /// a few px of the point's, and it is nearer the camera.</summary>
        static bool RimAcross(Vector3 world)
        {
            var box = Box; var cam = Camera.main;
            var local = box.Root.InverseTransformPoint(world);
            var back = (box.Root.InverseTransformPoint(cam.transform.position) - local).normalized; // the real ray to the camera
            if (back.z > -1e-3f) return false;
            float t = (Land.CZ - (Land.HZ + 0.075f) - local.z) / back.z;
            return Mathf.Abs(local.y + back.y * t - Land.GlassTop) < 0.06f;
        }

        /// <summary>Diagnostics: the renderers drawn over a world point (their screen box holds it and they are nearer the camera).</summary>
        static string Occluders(Vector3 world)
        {
            var cam = Camera.main; var sp = cam.WorldToScreenPoint(world); float dist = Vector3.Distance(cam.transform.position, world);
            var sb = new StringBuilder();
            foreach (var rr in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!rr.enabled || !rr.gameObject.activeInHierarchy) continue;
                var b = rr.bounds; if (b.Contains(world)) continue;
                float near = Vector3.Distance(cam.transform.position, b.ClosestPoint(cam.transform.position));
                if (near > dist - 0.05f) continue;
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                for (int i = 0; i < 8; i++) { var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)); var s = cam.WorldToScreenPoint(c); x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y); }
                if (sp.x < x0 || sp.x > x1 || sp.y < y0 || sp.y > y1) continue;
                sb.Append($"{rr.name}/{(rr.transform.parent != null ? rr.transform.parent.name : "-")} c{b.center} s{b.size}; ");
            }
            return sb.ToString();
        }

        /// <summary>Diagnostics: what stands round a spot (renderers whose bounds come within 0.6 u), and the camera.</summary>
        static void LogNear(Vector2 at, string what)
        {
            var box = Box;
            var w = box.Root.TransformPoint(new Vector3(at.x, box.SurfaceHeight(at.x, at.y), at.y));
            var sb = new StringBuilder($"[789 lean] near {what} {at} (world {w}): camera at {Camera.main.transform.position}, toward camera {ToCamera(at)}\n");
            foreach (var rr in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!rr.enabled || !rr.gameObject.activeInHierarchy) continue;
                var b = rr.bounds;
                if (b.size.magnitude > 3f) continue;
                var c = b.ClosestPoint(w);
                if ((new Vector2(c.x - w.x, c.z - w.z)).magnitude > 0.6f) continue;
                var lc = box.Root.InverseTransformPoint(b.center);
                sb.AppendLine($"  {rr.name} (parent {(rr.transform.parent != null ? rr.transform.parent.name : "-")}): centre local ({lc.x:0.00}, {lc.z:0.00}) size {b.size}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
