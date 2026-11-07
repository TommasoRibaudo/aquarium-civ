using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// t1-camera (decisions.md 2026-11-06, "The default camera"; tester run 1 items 1-2): after an Away the camera eases to the camp
    /// frame by itself: dolls ~55 px tall on a 1179x2556 phone, the near glass and its frame gone, and a pinch out still gets the whole
    /// box back. Shots go to Temp/agent/t1camera/.
    /// </summary>
    [Category("CampFrame")]
    public class CampFramePlayTests : PlayTestBase
    {
        const string Dir = Shots + "t1camera/";
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        [UnityTest, Timeout(180000)]
        public IEnumerator AfterAnAwayTheCameraEasesToTheCampFrame()
        {
            yield return OpenSlice(1179, 2556, seed: 4242);
            Directory.CreateDirectory(Dir);
            var rig = Rig;
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            var view = CampFrameView.Instance;
            Assert.IsNotNull(view, "the bootstrap made the camp frame view");
            Assert.AreEqual(1f, rig.zoom, 0.02f, "a new box opens on the whole box (the first run's framing)");
            Assert.AreEqual(0, view.Framed, "and a new box isn't framed");

            CampFrameView.Auto = true;
            Debug.Log("[t1camera] " + GameClock.DebugAway(6));
            yield return Until(() => view.Framed > 0, 8f);
            Assert.Greater(view.Framed, 0, "the return framed the camp: " + view.LastWhy);
            yield return Until(() => !rig.IsFlying, 4f);
            yield return new WaitForSecondsRealtime(1f); // the glass's fade follows
            Debug.Log("[t1camera] " + CampFrameView.Status());

            Assert.AreEqual(view.LastZoom, rig.zoom, 0.01f, "it arrived");
            Assert.Less(rig.zoom, 0.9f, "closer than the whole box");
            Assert.AreEqual(CampFrame.Pitch, rig.pitch, 0.5f);
            var centre = pv.transform.TransformPoint(new Vector3(view.Last.x, 0f, view.Last.z));
            Assert.Less(new Vector2(rig.target.x - centre.x, rig.target.z - centre.z).magnitude, 0.35f, "the camp is in the middle");

            // Dolls in the frame: their height on screen.
            var cam = Camera.main;
            var px = new List<float>();
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++)
            {
                if (new Vector2(xs[i] - view.Last.x, zs[i] - view.Last.z).magnitude > view.Last.radius) continue;
                if (!pv.TryGetDollFeet(who[i], out var feet)) continue;
                var a = cam.WorldToScreenPoint(feet);
                var b = cam.WorldToScreenPoint(feet + Vector3.up * CampFrame.DollHeight * pv.DollZoom * pv.transform.lossyScale.y);
                if (a.z > 0f && a.y > 0f && a.y < Screen.height) px.Add(Mathf.Abs(b.y - a.y));
            }
            Debug.Log($"[t1camera] dolls in the frame: {px.Count}, px {string.Join(", ", px.Select(p => p.ToString("0")))}");
            Assert.Greater(px.Count, 0, "people are in the frame");
            px.Sort();
            float median = px[px.Count / 2];
            Assert.That(median, Is.InRange(CampFrame.MinDollPx - 3f, CampFrame.MaxDollPx + 3f), "a doll reads at ~55 px on the phone");

            // The near glass is gone: no rim across the camp.
            Assert.LessOrEqual(box.PaneOpacity(0), 0.05f, "front pane");
            Assert.LessOrEqual(box.PaneOpacity(2), 0.05f, "left pane");
            Assert.LessOrEqual(box.FrameOpacity(0), 0.05f, "front frame and rim");
            Assert.LessOrEqual(box.FrameOpacity(2), 0.05f, "left frame and rim");
            Assert.LessOrEqual(box.FrameOpacity(4), 0.05f, "the near post");
            Assert.AreEqual(1f, box.PaneOpacity(1), 1e-3f, "the back of the box stays");
            yield return Shot("t1camera/after_away_1179x2556");

            // A pinch out still shows the whole box.
            var mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float s0 = Screen.width * 0.3f;
            int ia = touchId++, ib = touchId++;
            BeginTouch(ia, mid - new Vector2(s0, 0), queueEventOnly: true); BeginTouch(ib, mid + new Vector2(s0, 0), queueEventOnly: true);
            yield return null; yield return null; yield return null;
            for (int i = 1; i <= 40; i++)
            {
                float s = s0 * Mathf.Pow(0.25f, i / 40f);
                MoveTouch(ia, mid - new Vector2(s, 0), delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(ib, mid + new Vector2(s, 0), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(ia, mid, delta: Vector2.zero, queueEventOnly: true); EndTouch(ib, mid, delta: Vector2.zero, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log($"[t1camera] after the pinch out: zoom {rig.zoom:0.00}, front pane {box.PaneOpacity(0):0.00}");
            Assert.GreaterOrEqual(rig.zoom, 0.99f, "the pinch reaches the whole box");
            Assert.AreEqual(1f, box.PaneOpacity(0), 0.02f, "and the glass is back");
            yield return Shot("t1camera/pinched_out_1179x2556");
            AssertNoErrors("camp frame");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheCampFrameAt1080x1920()
        {
            yield return OpenSlice(1080, 1920, seed: 4242);
            var rig = Rig;
            var view = CampFrameView.Instance;
            CampFrameView.Auto = true;
            GameClock.DebugAway(6);
            yield return Until(() => view.Framed > 0, 8f);
            yield return Until(() => !rig.IsFlying, 4f);
            yield return new WaitForSecondsRealtime(1f);
            Assert.Greater(view.Framed, 0, view.LastWhy);
            var box = Object.FindAnyObjectByType<BoxView>();
            Debug.Log($"[t1camera] 1080x1920: {CampFrameView.Status()}; front pane {box.PaneOpacity(0):0.00}, front frame {box.FrameOpacity(0):0.00}");
            Assert.LessOrEqual(box.FrameOpacity(0), 0.05f, "no rim across the camp");
            yield return Shot("t1camera/after_away_1080x1920");
            AssertNoErrors("camp frame 1080");
        }
    }
}
