using System.Collections;
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
                        if (rv.TryGetPosition(id, out var p)) { checks++; if (!rv.Field.IsGreenAt(p.x, p.z)) off++; }
                    yield return null;
                }
                pv.timeScale = 1f;
                Assert.Greater(checks, 1000, "sampled");
                Assert.AreEqual(0, off, "rabbits only stand on green");
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
                yield return Frames(200);
                AssertNoErrors("rabbits");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        /// <summary>#333: a real touch on a rabbit: it freezes a beat, then bolts to another part of the meadow and stays there; none is lost.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Touch_StartlesARabbit_ItRunsToAnotherPartOfTheMeadow_AndStaysThere()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242);
                var box = Object.FindAnyObjectByType<BoxView>();
                var rv = RabbitView.Instance;
                PeopleView.DebugSetHour(11f);
                Meadow();
                EcoDebug.AddRabbits(6);
                yield return Frames(30);
                yield return Until(() => rv.SignsPlayed > 0, 10f);
                yield return Until(() => !rv.SignPlaying, 10f);
                yield return Frames(30);
                int r0 = rv.Field.Alive[1];
                Assert.IsTrue(rv.TryGetPosition(r0, out var at0));
                yield return Fly(box, at0.x, at0.z, 1.6f);
                Assert.IsTrue(rv.TryGetPosition(r0, out at0));
                int shown = rv.Shown;
                var screen = (Vector2)Camera.main.WorldToScreenPoint(box.transform.TransformPoint(at0));
                Assert.IsTrue(screen.x > 0 && screen.y > 0 && screen.x < Screen.width && screen.y < Screen.height, "the rabbit is on screen");
                yield return TapAt(screen);
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.IsTrue(rv.TryGetAct(r0, out bool fleeing) && fleeing, "startled: it freezes");
                yield return new WaitForSecondsRealtime(1.0f);
                rv.TryGetPosition(r0, out var mid);
                yield return new WaitForSecondsRealtime(6f);
                Assert.IsTrue(rv.TryGetPosition(r0, out var end), "still there");
                Assert.AreEqual(shown, rv.Shown, "a tap removes nobody");
                float away = new Vector2(end.x - at0.x, end.z - at0.z).magnitude;
                Assert.Greater(away, 0.5f, "it ran to another part of the meadow");
                Assert.IsTrue(rv.Field.IsGreenAt(end.x, end.z), "and stands on green");
                Assert.Greater(new Vector2(mid.x - at0.x, mid.z - at0.z).magnitude, 0.15f, "fast: a second in, it is already well away");
                yield return new WaitForSecondsRealtime(4f);
                rv.TryGetPosition(r0, out var later);
                Assert.Greater(new Vector2(later.x - at0.x, later.z - at0.z).magnitude, 0.4f, "and it does not drift back");
                AssertNoErrors("rabbit tap");
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
