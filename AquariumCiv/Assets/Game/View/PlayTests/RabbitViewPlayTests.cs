using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #282 phase 2: the rabbits in the box at a phone size. Tracks at the meadow's edge before the first rabbit; the first rabbit's
    /// sign (it hops in, one person points, once); rabbits that follow the sim count (capped), stay on the green and scatter from a
    /// person; the cheats. Screenshots go to Temp/agent/rabbit_*.png.
    /// </summary>
    [Category("RabbitView")]
    public class RabbitViewPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        static void Meadow()
        {
            // green away from the camp's yard, a few spots, then a season so it spreads
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
        }

        IEnumerator Fly(BoxView box, float x, float z, float zoomMul)
        {
            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.FlyTo(box.transform.TransformPoint(box.land.OnGround(x, z)), Mathf.Max(rig.minZoom * zoomMul, 0.25f), 0.4f);
            for (int i = 0; i < 200 && rig.IsFlying; i++) yield return null;
            yield return Frames(10);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Tracks_FirstRabbitSign_Herd_Flee_Cheats()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242);
                Assert.IsTrue(Sim.EcologyOn, "a living-land box");
                var box = Object.FindAnyObjectByType<BoxView>();
                var rv = RabbitView.Instance;
                Assert.IsNotNull(rv, "the rabbit view is on the box");
                PeopleView.DebugSetHour(10f);
                yield return Frames(30);
                Assert.IsFalse(rv.TracksShown, "no tracks on a bare box");
                Assert.AreEqual(0, rv.Shown, "no rabbits on a bare box");
                Meadow();
                yield return Frames(30);
                Assert.IsFalse(rv.TracksShown, "a little green is not tracks yet");

                // ---- tracks first (the cheat)
                StringAssert.DoesNotContain("off", EcoDebug.RabbitTracksNow());
                yield return Frames(20);
                Assert.IsTrue(Sim.EcoRabbitTracksSeen, "the sim says tracks");
                Assert.Greater(Sim.EcoRabbitTrackCells.Count, 0, "track cells at the meadow's edge");
                Assert.IsTrue(rv.TracksShown, "the prints are drawn");
                Assert.AreEqual(0, Sim.EcoRabbitCount, "tracks come before the first rabbit");
                Assert.AreEqual(0, rv.Shown, "and no rabbit yet");
                int tc = Sim.EcoRabbitTrackCells[0];
                yield return Fly(box, SiteMap.CXc(tc), SiteMap.CZc(tc), 1.15f);
                yield return Shot("rabbit_01_tracks_close");
                Camera.main.GetComponent<BoxCamera>().zoom = 1f;
                yield return Frames(20);
                yield return Shot("rabbit_02_tracks_default");

                // ---- the first rabbit: its sign plays once, someone points
                var pv = PeopleView.Find();
                StringAssert.DoesNotContain("off", EcoDebug.AddRabbits(1));
                bool pointed = false;
                yield return Until(() => rv.SignsPlayed > 0, 10f);
                Assert.AreEqual(1, rv.SignsPlayed, "the sign started");
                Assert.IsFalse(Sim.EcoRabbitArrivalPending, "and it is acknowledged");
                Assert.IsFalse(rv.LastPointer.IsNone, "someone is standing near enough to point");
                float t0 = Time.realtimeSinceStartup; bool shot = false;
                while (Time.realtimeSinceStartup - t0 < 4f)
                {
                    foreach (var a in pv.Activities) if (a.who == rv.LastPointer && a.kind == ActivityKind.Point) pointed = true;
                    if (!shot && Time.realtimeSinceStartup - t0 > 2.5f) { shot = true; yield return Shot("rabbit_03_first_rabbit_pointing"); }
                    yield return null;
                }
                Assert.IsTrue(pointed, "the person nearest the rabbit points");
                yield return Until(() => !rv.SignPlaying, 10f);
                Assert.IsFalse(rv.SignPlaying, "the sign ends");
                Assert.AreEqual(1, rv.Shown, "the first rabbit is on screen");
                Assert.AreEqual(1, rv.SignsPlayed, "once");

                // ---- the herd follows the sim count and stays on the green
                EcoDebug.AddRabbits(4);
                yield return Frames(30);
                Assert.AreEqual(Sim.EcoRabbitsShown(), rv.Shown, "dolls follow the sim");
                Assert.AreEqual(5, rv.Shown);
                EcoDebug.AddRabbits(30);
                yield return Frames(30);
                Assert.AreEqual(12, Sim.EcoRabbitsShown(), "capped");
                Assert.AreEqual(12, rv.Shown, "dolls capped");
                Assert.AreEqual(1, rv.SignsPlayed, "the sign still once");
                pv.timeScale = 6f;
                int off = 0, checks = 0;
                for (int i = 0; i < 400; i++)
                {
                    foreach (int id in rv.Field.Alive)
                        if (rv.TryGetPosition(id, out var p) && !rv.InTransit(id)) { checks++; if (!rv.Field.IsGreenAt(p.x, p.z)) { off++; if (off <= 6) Debug.Log($"[rabbit-green] off green {id} at ({p.x:0.00},{p.z:0.00}) frame {i}: {rv.DebugScare(id)}"); } }
                    yield return null;
                }
                pv.timeScale = 1f;
                Assert.Greater(checks, 1000, "sampled");
                Assert.AreEqual(0, off, "rabbits only stand on green (one hopping over to a new spot may cross a strip of bare earth)");
                int wc = rv.Field.WarrenCell;
                yield return Fly(box, SiteMap.CXc(wc), SiteMap.CZc(wc), 2f);
                yield return Shot("rabbit_04_meadow_closer");
                Camera.main.GetComponent<BoxCamera>().zoom = 1f;
                yield return Frames(30);
                yield return Shot("rabbit_05_meadow_default");

                // ---- they scatter when someone comes close
                int r0 = rv.Field.Alive[0];
                Assert.IsTrue(rv.TryGetPosition(r0, out var at0));
                rv.Scarers.Add(new Vector2(at0.x + 0.15f, at0.z));
                bool fled = false;
                float tf = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tf < 1.2f)
                {
                    yield return null;
                    if (rv.TryGetAct(r0, out bool f) && f) fled = true;
                }
                rv.TryGetPosition(r0, out var at1);
                float moved = new Vector2(at1.x - at0.x, at1.z - at0.z).magnitude;
                rv.Scarers.Clear();
                Assert.IsTrue(fled, "a rabbit with a person at 0.15 flees");
                Assert.Greater(moved, 0.1f, "and it got away from where it was");
                Assert.IsTrue(rv.Field.IsGreenAt(at1.x, at1.z), "onto green");
                // #400: a person who stays put near a few rabbits: they run to a spot and settle, none bounds on the spot
                for (int k = 1; k < 4 && k < rv.Field.Alive.Count; k++)
                    if (rv.TryGetPosition(rv.Field.Alive[k], out var ak)) rv.Scarers.Add(new Vector2(ak.x - 0.2f, ak.z + 0.1f));
                yield return Frames(240);
                rv.Scarers.Clear();
                yield return Frames(200);
                Assert.AreEqual(0, rv.BoundsInPlace, "no rabbit hopped or bounded on the spot (#395)");
                AssertNoErrors("rabbits");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        /// <summary>The user (2026-11-05): a tap ON a rabbit does nothing to it; a knock on the GLASS close to a rabbit (on screen) makes
        /// it run off, to another part of the meadow, and it stays away; none is lost.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Touch_TapOnARabbitDoesNothing_AKnockOnTheGlassNearItSendsItOff()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242);
                var box = Object.FindAnyObjectByType<BoxView>();
                var rv = RabbitView.Instance;
                var care = CareView.Instance;
                PeopleView.DebugSetHour(11f);
                Meadow();
                EcoDebug.AddRabbits(6);
                yield return Frames(30);
                yield return Until(() => rv.SignsPlayed > 0, 10f);
                yield return Until(() => !rv.SignPlaying, 10f);
                yield return Frames(30);
                int shown = rv.Shown;
                // a rabbit with glass beside it on screen: a knock spot a few fingertips off, on glass, with no rabbit under the finger and
                // nothing there that opens a card (pieces and people are a card's, before the knock)
                int r0 = -1; Vector2 onRabbit = default, knockAt = default;
                var shaping = ShapingView.Instance;
                bool Clean(Vector2 k)
                {
                    if (k.x < 0 || k.y < 0 || k.x > Screen.width || k.y > Screen.height || UiInput.Over(k) || !care.GlassAt(k)) return false;
                    foreach (int other in rv.Field.Alive)
                        if (rv.TryGetPosition(other, out var q) && ((Vector2)Camera.main.WorldToScreenPoint(box.transform.TransformPoint(q)) - k).magnitude < RabbitView.OnRabbitScreen * Screen.height * 1.5f) return false;
                    if (shaping == null || !shaping.GroundPoint(k, out float gx, out float gz)) return false;
                    foreach (var pc in box.layout.pieces) if (new Vector2(pc.x - gx, pc.z - gz).magnitude < 0.45f) return false;
                    return true;
                }
                foreach (int id in rv.Field.Alive)
                {
                    if (!rv.TryGetPosition(id, out var p)) continue;
                    var sp = (Vector2)Camera.main.WorldToScreenPoint(box.transform.TransformPoint(p));
                    foreach (float f in new[] { 0.05f, 0.07f, 0.09f })
                        for (int dIx = 0; dIx < 8 && r0 < 0; dIx++)
                        {
                            float ang = dIx * Mathf.PI / 4f;
                            var k = sp + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * f * Screen.height;
                            if (Clean(k)) { r0 = id; onRabbit = sp; knockAt = k; }
                        }
                    if (r0 >= 0) break;
                }
                Assert.GreaterOrEqual(r0, 0, "a rabbit with glass beside it on screen");
                Assert.IsTrue(rv.TryGetPosition(r0, out var at0));
                Debug.Log($"[rabbit-knock] rabbit {r0} at ({at0.x:0.00},{at0.z:0.00}) screen {onRabbit}, knock at {knockAt}, glass on the rabbit {care.GlassAt(onRabbit)}");

                // 1. a tap right on the rabbit does nothing to it (even through the glass)
                yield return TapAt(onRabbit);
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsFalse(rv.IsStartled(r0), "a tap on the rabbit does not startle it");
                yield return new WaitForSecondsRealtime(1.5f); // the knock's count settles (no double tap with the next one)

                // 2. a knock on the glass beside it: it freezes, then runs off, and stays away
                Assert.IsTrue(rv.TryGetPosition(r0, out at0));
                int knocks0 = GlassVoice.Instance != null ? GlassVoice.Instance.Knocks : -1;
                float nearestOther = float.MaxValue;
                foreach (int id in rv.Field.Alive)
                    if (rv.TryGetPosition(id, out var q)) nearestOther = Mathf.Min(nearestOther, ((Vector2)Camera.main.WorldToScreenPoint(box.transform.TransformPoint(q)) - knockAt).magnitude);
                yield return TapAt(knockAt);
                yield return new WaitForSecondsRealtime(0.15f);
                Debug.Log($"[rabbit-knock] knocks {knocks0} -> {(GlassVoice.Instance != null ? GlassVoice.Instance.Knocks : -1)} (counts when the taps stop), taps {GlassVoice.Instance?.LastTaps}, nearest rabbit to the knock {nearestOther:0} px, card open {PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)}, fleeing {(rv.TryGetAct(r0, out bool flx) && flx)}");
                Assert.IsTrue(rv.TryGetAct(r0, out bool fl) && fl, "the knock beside it startles the rabbit");
                for (int k = 0; k < 10; k++) { yield return new WaitForSecondsRealtime(0.5f); rv.TryGetPosition(r0, out var pk); Debug.Log($"[rabbit-knock] {0.15f + 0.5f * (k + 1):0.0}s at ({pk.x:0.00},{pk.z:0.00}) {new Vector2(pk.x - at0.x, pk.z - at0.z).magnitude:0.00} from start: {rv.DebugScare(r0)}"); }
                Assert.IsTrue(rv.TryGetPosition(r0, out var end), "still there");
                Assert.AreEqual(shown, rv.Shown, "a knock removes nobody");
                Assert.Greater(new Vector2(end.x - at0.x, end.z - at0.z).magnitude, 0.5f, "it ran to another part of the meadow");
                Assert.IsTrue(rv.Field.IsGreenAt(end.x, end.z), "and stands on green");
                yield return new WaitForSecondsRealtime(5f);
                rv.TryGetPosition(r0, out var later);
                Assert.Greater(new Vector2(later.x - at0.x, later.z - at0.z).magnitude, 0.4f, "and it does not drift back");
                AssertNoErrors("rabbit knock");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Herd_AtSmallPhone_Reads()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(1080, 1920, seed: 4242);
                var rv = RabbitView.Instance;
                PeopleView.DebugSetHour(11f);
                Meadow();
                EcoDebug.AddRabbits(6);
                yield return Frames(30);
                yield return Until(() => rv.SignsPlayed > 0, 10f);
                yield return Until(() => !rv.SignPlaying, 10f);
                yield return new WaitForSecondsRealtime(2f);
                Assert.AreEqual(6, rv.Shown);
                yield return Shot("rabbit_06_herd_1080");
                // close up, next to a person if one is near: the look and the size against the people
                var box = Object.FindAnyObjectByType<BoxView>();
                int r0 = rv.Field.Alive[0];
                Assert.IsTrue(rv.TryGetPosition(r0, out var at0));
                yield return Fly(box, at0.x, at0.z, 1f);
                yield return Shot("rabbit_07_close_a");
                yield return new WaitForSecondsRealtime(1.3f);
                yield return Shot("rabbit_08_close_b");
                rv.TryGetPosition(r0, out at0);
                rv.Scarers.Add(new Vector2(at0.x + 0.2f, at0.z));
                yield return new WaitForSecondsRealtime(0.15f);
                yield return Shot("rabbit_09_startled");
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Shot("rabbit_10_bolting");
                rv.Scarers.Clear();
                AssertNoErrors("small phone");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }
    }
}
