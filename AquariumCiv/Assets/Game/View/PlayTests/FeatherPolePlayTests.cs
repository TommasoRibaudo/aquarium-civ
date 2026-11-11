using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>fb4 #566 on the real Slice scene: one feather pole however many feather answers, and the "pray now" cheat sends
    /// people who stand round it, facing it.</summary>
    [Category("FeatherPolePlay")]
    public class FeatherPolePlayTests : PlayTestBase
    {
        static int PolesInScene() =>
            ConsequencePropsView.Instance.Standing.Count(kv => kv.Value != null && kv.Value.name.StartsWith("Consequence_feather"));

        [UnityTest, Timeout(240000)]
        public IEnumerator OnePole_AndThePrayNowCheatBringsPeopleThatFaceIt()
        {
            yield return OpenSlice(1179, 2556, seed: 4242);
            var sim = GameClock.Instance.ActiveSim;
            var pv = PeopleView.Find();
            Assert.IsNotNull(pv);

            ConsequenceCheats.PlaceEvery();
            yield return Frames(20);
            Assert.AreEqual(1, PolesInScene(), "exactly one feather pole stands after every answer asked for a feather");
            Assert.IsTrue(sim.FeatherPoleSpot(out float px, out float pz), "the pole has a spot");
            var pole = new Vector2(px, pz);

            // by day, so the visit isn't something the clock plans for later
            PeopleView.DebugSetHour(13f);
            yield return Frames(10);
            var msg = ConsequenceCheats.PrayNow();
            TestContext.WriteLine(msg);
            Assert.IsFalse(msg.StartsWith("nobody") || msg.StartsWith("no "), msg);

            bool seen = false, faced = false;
            float closest = 99f, bestDot = -9f; int frames = 0, walkingFrames = 0; string lastAt = "";
            pv.timeScale = 2f;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 100f && !(seen && faced))
            {
                foreach (var a in pv.Activities.Where(a => a.kind == ActivityKind.Pray))
                {
                    seen = true; frames++;
                    if (!pv.TryGetDollPosition(a.who, out var at)) continue;
                    if (pv.IsWalking(a.who)) { walkingFrames++; continue; }
                    float d = (at - pole).magnitude; { pv.Planner.PositionAt(a.who, pv.Now, out float qx, out float qz); pv.Planner.Positions.TryGetPosition(a.place, out float tx, out float tz); lastAt = $"{at} vs pole {pole}; planner pos {qx:0.00},{qz:0.00}; place pos {tx:0.00},{tz:0.00}; {a.place.kind}#{a.place.index} moving {a.moving} start {a.start:0.00} end {a.end:0.00} hour {pv.Now.hour:0.00}"; }
                    closest = Mathf.Min(closest, d);
                    if (d > 1.0f) continue;
                    if (pv.TryGetDollFacing(a.who, out var fwd)) { float dot = Vector2.Dot(fwd.normalized, (pole - at).normalized); bestDot = Mathf.Max(bestDot, dot); if (dot > 0.6f) faced = true; }
                }
                yield return null;
            }
            pv.timeScale = 1f;
            TestContext.WriteLine($"prayers seen {seen}, faced the pole {faced}, closest stopped distance {closest:0.00}");
            Assert.IsTrue(seen, "a Pray activity appears in PeopleView");
            Assert.IsTrue(faced, $"someone stands by the pole and faces it (pray frames {frames}, walking {walkingFrames}, closest stopped {closest:0.00}, best facing dot {bestDot:0.00}; last stopped at {lastAt})");

            // and the visit ends: nobody is still praying a game day on
            PeopleView.DebugSetHour(pv.Now.hour + 6f);
            pv.timeScale = 8f;
            yield return Until(() => !pv.Activities.Any(a => a.kind == ActivityKind.Pray), 40f);
            pv.timeScale = 1f;
            Assert.IsFalse(pv.Activities.Any(a => a.kind == ActivityKind.Pray), "the visit ends");
            Assert.AreEqual(1, PolesInScene(), "still one pole");
            AssertNoErrors("feather pole");
        }
    }

    /// <summary>fb4 #566 pictures: the pole at the camp's edge, people praying round it, a dusk visit. Explicit.</summary>
    [Explicit("Writes images; run by hand"), Category("FeatherPoleShots")]
    public class FeatherPoleShots : PlayTestBase
    {
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        static void Look(Vector3 world, float zoom, float pitch = 52f, float yawAdd = 0f)
        {
            var rig = Cam;
            rig.CancelFlight();
            rig.target = world; rig.zoom = zoom; rig.pitch = pitch; rig.yaw += yawAdd;
            rig.Apply();
        }

        static IEnumerator Jpg(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var dir = Shots + "fb4/566/";
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(dir + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        [UnityTest, Timeout(300000)]
        public IEnumerator TakeShots()
        {
            UseLivingLand();
            yield return OpenSlice(1179, 2556, 4242);
            var sim = GameClock.Instance.ActiveSim;
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            ConsequenceCheats.PlaceEvery();
            PeopleView.DebugSetHour(13f);
            yield return Wait(2f);
            Assert.IsTrue(sim.FeatherPoleSpot(out float px, out float pz));
            pv.Planner.FirePoint(out float fx, out float fz);
            var pole = box.transform.TransformPoint(box.land.OnGround(px, pz));
            var fire = box.transform.TransformPoint(box.land.OnGround(fx, fz));
            Look((pole + fire) * 0.5f, 0.6f);
            yield return Wait(1.2f);
            yield return Jpg("wide_camp");

            Debug.Log("[pole] " + ConsequenceCheats.PrayNow());
            pv.timeScale = 4f;
            yield return Until(() => pv.Activities.Count(a => a.kind == ActivityKind.Pray && !pv.IsWalking(a.who)) >= 2, 60f);
            pv.timeScale = 1f;
            Look(pole, 0.09f, 32f, 110f);
            yield return Wait(0.8f);
            yield return Jpg("close_pray_a");
            Look(pole, 0.08f, 25f, 60f);
            yield return Wait(1.6f);
            yield return Jpg("close_pray_b");
            yield return Wait(1.6f);
            yield return Jpg("close_pray_c");

            PeopleView.DebugSetHour(18.2f);
            yield return Wait(1f);
            Debug.Log("[pole] " + ConsequenceCheats.PrayNow());
            pv.timeScale = 4f;
            yield return Until(() => pv.Activities.Count(a => a.kind == ActivityKind.Pray && !pv.IsWalking(a.who)) >= 2, 60f);
            pv.timeScale = 1f;
            Look(pole, 0.25f, 42f, 20f);
            yield return Wait(0.8f);
            yield return Jpg("dusk_visit");
        }
    }
}
