using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AquariumCiv.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #273 phase B, the feel by touch in the real Slice scene: two fingers drag the pivot to each of the four walls at close and far
    /// zoom. It reaches the glass (the rest limit, near enough), never goes past the hard stop, the camera never sits in a pane, and on
    /// release it settles at the rest limit. Pivot traces go to Temp/agent/p273/, shots at each wall too (1179 x 2556).
    /// </summary>
    [Category("CameraGlassFeel")]
    public class CameraGlassFeelPlayTests : PlayTestBase
    {
        const string Dir = "F:/GameDev/AquariumCiv/Temp/agent/p273/";
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        IEnumerator Open()
        {
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(1179, 2556, seed: 1234);
            Directory.CreateDirectory(Dir);
        }

        void Close() { ShowMeDirector.BubblesOn = true; CloseSlice(); }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Dir + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        int fa, fb;

        static bool CameraInBox(BoxCamera r)
        {
            var p = r.transform.position;
            return CameraGlass.CameraInsideBox(p.x, p.y, p.z, Land.HX + BoxCameraMath.HullSide, Land.HZ + BoxCameraMath.HullSide, BoxCameraMath.ShelfY, Land.GlassTop, 0.3f);
        }

        /// <summary>Two fingers drag by <paramref name="dir"/> (screen pixels) in steps; records the pivot each frame, then optionally holds and releases.</summary>
        IEnumerator Pan(Vector2 dir, int steps, List<Vector3> trace, List<bool> inBox, bool release = true)
        {
            var a = new Vector2(Screen.width * 0.4f, Screen.height * 0.5f); var b = new Vector2(Screen.width * 0.6f, Screen.height * 0.5f);
            int ia = touchId++, ib = touchId++; fa = ia; fb = ib;
            BeginTouch(ia, a, queueEventOnly: true); BeginTouch(ib, b, queueEventOnly: true);
            yield return null; yield return null; yield return null;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                MoveTouch(ia, a + dir * t, delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(ib, b + dir * t, delta: Vector2.zero, queueEventOnly: true);
                yield return null;
                trace.Add(Rig.target); inBox.Add(CameraInBox(Rig));
            }
            if (!release) yield break;
            EndTouch(ia, a + dir, delta: Vector2.zero, queueEventOnly: true); EndTouch(ib, b + dir, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
        }

        // yaw 0: finger left moves the pivot +x, finger right -x, finger down +z (away), finger up -z.
        static readonly (string name, Vector2 dir, int axis, float sign)[] Walls =
        {
            ("xPlus", new Vector2(-1f, 0f), 0, 1f), ("xMinus", new Vector2(1f, 0f), 0, -1f),
            ("zPlus", new Vector2(0f, -1f), 1, 1f), ("zMinus", new Vector2(0f, 1f), 1, -1f),
        };

        [UnityTest]
        public IEnumerator Pan_ReachesTheGlassOnEverySide_NeverPastIt_CloseAndFar()
        {
            yield return Open();
            
            var log = new StringBuilder();
            foreach (var (zoomName, zoom) in new[] { ("close", CameraReach.MinZoom), ("far", 1f) })
                foreach (var w in Walls)
                {
                    Rig.EaseTo(new Vector3(0f, 1f, 0f), 0f, 26f, zoom, 0f);
                    for (int i = 0; i < 40; i++) yield return null;
                    var trace = new List<Vector3>(); var inBox = new List<bool>();
                    yield return Pan(w.dir * (Screen.height * 1.2f), 45, trace, inBox, release: false);
                    for (int i = 0; i < 15; i++) { yield return null; trace.Add(Rig.target); inBox.Add(CameraInBox(Rig)); }
                    float held = w.axis == 0 ? Rig.target.x * w.sign : Rig.target.z * w.sign;
                    var cam = Camera.main;
                    var vb = BoxCameraMath.PanBoundsFor(Rig.yaw, Rig.pitch, Rig.FitDistance() * Rig.zoom, Rig.target.y, Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), cam.aspect);
                    float restLim = w.axis == 0 ? vb.x : vb.y, stopLim = restLim + CameraGlass.Gap;
                    // the tank on screen at far zoom: every corner of the box inside the viewport; the glass at the screen edge at close zoom
                    float worstOut = 0f;
                    for (int c = 0; c < 8; c++)
                    {
                        var v = cam.WorldToViewportPoint(new Vector3((c & 1) == 0 ? -Land.HX : Land.HX, (c & 2) == 0 ? 0f : Land.GlassTop, (c & 4) == 0 ? -Land.HZ : Land.HZ));
                        worstOut = Mathf.Max(worstOut, -v.x, v.x - 1f, -v.y, v.y - 1f);
                    }
                    float wallX = -1f;
                    if (w.axis == 0) wallX = cam.WorldToViewportPoint(new Vector3(w.sign * Land.HX, Rig.target.y, Rig.target.z)).x;
                    log.AppendLine($"   limit {restLim:F3} worst corner outside screen {worstOut:F3} wall viewport x {wallX:F3}");
                    if (zoom >= 1f) Assert.LessOrEqual(worstOut, 0.03f, $"{zoomName} {w.name}: the tank stays on screen");
                    else if (w.axis == 0) Assert.AreEqual(w.sign > 0 ? 1f : 0f, wallX, 0.15f, $"{zoomName} {w.name}: the glass wall is at the screen's edge");
                    yield return Shot($"{zoomName}_{w.name}_held");
                    var maxAbs = 0f; foreach (var p in trace) maxAbs = Mathf.Max(maxAbs, (w.axis == 0 ? p.x : p.z) * w.sign);
                    log.AppendLine($"{zoomName} {w.name}: held {held:F3} max {maxAbs:F3} rest {restLim:F3} stop {stopLim:F3} glass {(w.axis == 0 ? Land.HX : Land.HZ):F3}");
                    // release: settles to the rest limit
                    EndTouch(fa, new Vector2(Screen.width * 0.4f, Screen.height * 0.5f), delta: Vector2.zero, queueEventOnly: true);
                    EndTouch(fb, new Vector2(Screen.width * 0.6f, Screen.height * 0.5f), delta: Vector2.zero, queueEventOnly: true);
                    for (int i = 0; i < 90; i++) yield return null;
                    float settled = w.axis == 0 ? Rig.target.x * w.sign : Rig.target.z * w.sign;
                    log.AppendLine($"   settled {settled:F3}");
                    File.WriteAllText(Dir + $"trace_{zoomName}_{w.name}.csv", "x,y,z\n" + string.Join("\n", trace.ConvertAll(p => $"{p.x:F4},{p.y:F4},{p.z:F4}")));
                    Assert.LessOrEqual(maxAbs, stopLim + 1e-3f, $"{zoomName} {w.name}: never past the soft band's end");
                    Assert.GreaterOrEqual(held, restLim - 0.02f, $"{zoomName} {w.name}: reaches the limit of the view");
                    Assert.LessOrEqual(settled, restLim + 0.03f, $"{zoomName} {w.name}: settles at the rest limit");
                    Assert.IsFalse(inBox.Contains(true), $"{zoomName} {w.name}: the camera never sits inside the box");
                }
            File.WriteAllText(Dir + "summary.txt", log.ToString());
            Debug.Log(log.ToString());
            Close();
        }

        /// <summary>#273 (5): at the far corner, close, an orbit to max yaw draws the range in smoothly (no jump of the pivot) and the camera never enters the box.</summary>
        [UnityTest]
        public IEnumerator Orbit_ToMaxYaw_AtACorner_DrawsTheRangeInSmoothly()
        {
            yield return Open();
            var stop = BoxCameraMath.PanHardStop;
            foreach (float sx in new[] { 1f, -1f })
            {
                Rig.EaseTo(new Vector3(sx * stop.x, 1f, -stop.y), 0f, 15f, CameraReach.MinZoom, 0f);
                for (int i = 0; i < 40; i++) yield return null;
                int id = touchId++;
                var a = new Vector2(Screen.width * 0.3f, Screen.height * 0.5f);
                BeginTouch(id, a, queueEventOnly: true); yield return null; yield return null;
                var trace = new List<Vector3>(); bool inBox = false; float maxStep = 0f;
                for (int i = 1; i <= 60; i++)
                {
                    MoveTouch(id, a + new Vector2(sx * -Screen.width * 0.9f * i / 60f, 0f), delta: Vector2.zero, queueEventOnly: true);
                    yield return null;
                    if (trace.Count > 0) maxStep = Mathf.Max(maxStep, (Rig.target - trace[trace.Count - 1]).magnitude);
                    trace.Add(Rig.target); inBox |= CameraInBox(Rig);
                }
                for (int i = 0; i < 60; i++) { yield return null; if (trace.Count > 0) maxStep = Mathf.Max(maxStep, (Rig.target - trace[trace.Count - 1]).magnitude); trace.Add(Rig.target); inBox |= CameraInBox(Rig); }
                yield return Shot(sx > 0 ? "orbit_corner_a" : "orbit_corner_b");
                EndTouch(id, a, delta: Vector2.zero, queueEventOnly: true); yield return null;
                File.WriteAllText(Dir + $"orbit_{(sx > 0 ? "a" : "b")}.txt", $"yaw {Rig.yaw:F1} pitch {Rig.pitch:F1} maxStep {maxStep:F4} inBox {inBox}");
                Debug.Log($"orbit corner {sx}: yaw {Rig.yaw:F1} maxStep {maxStep:F4} inBox {inBox}");
                Assert.IsFalse(inBox, "the camera never enters the box while orbiting");
                Assert.Less(maxStep, 0.25f, "the range draws in smoothly, no jump");
            }
            Close();
        }
    }
}
