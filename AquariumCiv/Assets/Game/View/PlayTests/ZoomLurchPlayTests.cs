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
    /// #377 (playtest #336, "zooming in and out feels janky, the view jumps up or down"): a two finger pinch through the real input path, frame by frame.
    /// The camera's height and pitch must change in one direction while the fingers move in one direction, with no jump from frame to frame.
    /// Traces go to Temp/agent/p377/.
    /// </summary>
    [Category("ZoomLurch")]
    public class ZoomLurchPlayTests : PlayTestBase
    {
        const string Dir = "F:/GameDev/AquariumCiv/Temp/agent/p377/";
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        struct Sample { public float zoom, pitch, height, dist; public Vector3 target; }

        IEnumerator Snap(string name)
        {
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Dir + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        /// <summary>Two fingers spread by <paramref name="ratio"/> (spread end / spread start) in <paramref name="steps"/> frames, then held for <paramref name="hold"/> frames and released.</summary>
        IEnumerator Pinch(float ratio, int steps, int hold, List<Sample> log)
        {
            var mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float s0 = Screen.width * 0.12f;
            int ia = touchId++, ib = touchId++;
            BeginTouch(ia, mid - new Vector2(s0, 0), queueEventOnly: true); BeginTouch(ib, mid + new Vector2(s0, 0), queueEventOnly: true);
            yield return null; yield return null; yield return null;
            for (int i = 1; i <= steps + hold; i++)
            {
                float s = s0 * Mathf.Pow(ratio, Mathf.Min(i, steps) / (float)steps);
                MoveTouch(ia, mid - new Vector2(s, 0), delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(ib, mid + new Vector2(s, 0), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
                log.Add(Take());
            }
            EndTouch(ia, mid, delta: Vector2.zero, queueEventOnly: true); EndTouch(ib, mid, delta: Vector2.zero, queueEventOnly: true);
            for (int i = 0; i < 40; i++) { yield return null; log.Add(Take()); }
        }

        static Sample Take() => new Sample { zoom = Rig.zoom, pitch = Rig.pitch, height = Camera.main.transform.position.y, dist = Vector3.Distance(Camera.main.transform.position, Rig.target), target = Rig.target };

        static void Dump(string name, List<Sample> l)
        {
            var sb = new StringBuilder("frame zoom pitch height dist targetX targetY targetZ\n");
            for (int i = 0; i < l.Count; i++) sb.AppendLine($"{i} {l[i].zoom:F4} {l[i].pitch:F3} {l[i].height:F4} {l[i].dist:F4} {l[i].target.x:F3} {l[i].target.y:F3} {l[i].target.z:F3}");
            File.WriteAllText(Dir + name + ".txt", sb.ToString());
        }

        /// <summary>Largest frame-to-frame step and the number of direction reversals (past a 0.002 wobble) of a series.</summary>
        static void Stats(List<Sample> l, System.Func<Sample, float> f, out float maxStep, out int reversals, out float sign)
        {
            maxStep = 0f; reversals = 0; sign = 0f; float dir = 0f;
            for (int i = 1; i < l.Count; i++)
            {
                float d = f(l[i]) - f(l[i - 1]);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(d));
                if (Mathf.Abs(d) < 0.002f) continue;
                if (dir == 0f) dir = Mathf.Sign(d); else if (Mathf.Sign(d) != dir) { reversals++; dir = Mathf.Sign(d); }
            }
            sign = dir;
        }

        IEnumerator Run(string name, float zoom0, float pitch0, float ratio, List<Sample> log)
        {
            Rig.EaseTo(new Vector3(0f, 1f, 0f), 0f, pitch0, zoom0, 0f);
            for (int i = 0; i < 60; i++) yield return null;
            Rig.pitch = pitch0; Rig.zoom = zoom0; Rig.Apply();
            for (int i = 0; i < 20; i++) yield return null;
            log.Add(Take());
            yield return Pinch(ratio, 40, 5, log);
            Dump(name, log);
        }

        [UnityTest]
        public IEnumerator Pinch_OutFromClose_PitchAndHeightMoveOneWay_NoJump()
        {
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(1179, 2556, seed: 1234);
            Directory.CreateDirectory(Dir);
            var log = new List<Sample>();
            // fingers closing (ratio < 1) = zoom out, from the closest zoom to the far limit
            yield return Run("out_from_close", CameraReach.MinZoom, 14f, 0.12f, log);
            var sb = new StringBuilder();
            Stats(log, s => s.pitch, out float pStep, out int pRev, out _);
            Stats(log, s => s.height, out float hStep, out int hRev, out _);
            Stats(log, s => s.dist, out float dStep, out int dRev, out _);
            Debug.Log($"#377 out_from_close: pitch {log[0].pitch:F2}->{log[log.Count - 1].pitch:F2} maxStep {pStep:F3} rev {pRev}; height {log[0].height:F2}->{log[log.Count - 1].height:F2} maxStep {hStep:F3} rev {hRev}; dist maxStep {dStep:F3} rev {dRev}");
            Assert.LessOrEqual(pRev, 0, "pitch never reverses"); Assert.LessOrEqual(hRev, 0, "height never reverses"); Assert.LessOrEqual(dRev, 0, "distance never reverses");
            Assert.Less(pStep, 1.5f, "no pitch jump in a frame");
            int stop = log.FindIndex(x => Mathf.Approximately(x.zoom, CameraReach.MaxZoom));
            Assert.GreaterOrEqual(stop, 0, "the pinch reached the far limit");
            Assert.AreEqual(log[stop].pitch, log[log.Count - 1].pitch, 0.3f, "pitch has stopped when the zoom stops (no drift after the fingers)");
            Assert.AreEqual(log[stop].height, log[log.Count - 1].height, 0.3f, "height has stopped when the zoom stops");
            Assert.AreEqual(CameraReach.PitchFloor(CameraReach.MaxZoom), log[log.Count - 1].pitch, 0.3f);
            ShowMeDirector.BubblesOn = true; CloseSlice();
        }

        [UnityTest]
        public IEnumerator Pinch_InAndOut_FromDefault_OneWayEachTime_BackToStart_Shots()
        {
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(1179, 2556, seed: 1234);
            Directory.CreateDirectory(Dir);
            var inLog = new List<Sample>(); var outLog = new List<Sample>();
            Rig.EaseTo(new Vector3(0f, 1f, 0f), 0f, 30f, 1f, 0f);
            for (int i = 0; i < 60; i++) yield return null;
            yield return Snap("default"); inLog.Add(Take());
            yield return Pinch(1f / 0.12f, 40, 5, inLog);
            Dump("in_from_default", inLog);
            yield return Snap("close");
            outLog.Add(Take());
            yield return Pinch(0.12f, 40, 5, outLog);
            Dump("out_to_far", outLog);
            yield return Snap("far");
            foreach (var (name, l) in new[] { ("in", inLog), ("out", outLog) })
            {
                Stats(l, s => s.dist, out float dStep, out int dRev, out _);
                Stats(l, s => s.pitch, out float pStep, out int pRev, out _);
                Stats(l, s => s.height, out float hStep, out int hRev, out _);
                Debug.Log($"#377 {name}: pitch {l[0].pitch:F2}->{l[l.Count - 1].pitch:F2} step {pStep:F3} rev {pRev}; height {l[0].height:F2}->{l[l.Count - 1].height:F2} step {hStep:F3} rev {hRev}; dist rev {dRev}");
                Assert.LessOrEqual(pRev, 0, name + " pitch"); Assert.LessOrEqual(hRev, 0, name + " height"); Assert.LessOrEqual(dRev, 0, name + " dist");
                Assert.Less(pStep, 1.5f, name + " pitch jump");
            }
            ShowMeDirector.BubblesOn = true; CloseSlice();
        }
    }
}
