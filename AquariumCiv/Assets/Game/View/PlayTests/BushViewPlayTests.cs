using System.Collections;
using System.Collections.Generic;
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
    /// #288 phase 2: the berry bushes at a phone size. Flowers in spring, berries in summer, bare in winter; the first bush's
    /// sign (a bird lands, one points, one walks to look) plays once; a gatherer walks to a real bush cell, reaches and picks
    /// (berries drop on that bush) and carries the basket off. "+N bushes" only reaches the state; the seasons come by the
    /// time-advance cheat. Screenshots go to Temp/agent/bush_*.png.
    /// </summary>
    [Category("BushView")]
    public class BushViewPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.Sim;

        static bool Is(Season s) => LifeClock.SeasonOf(Sim.State.month) == s;

        static void ToSeason(Season s)
        {
            for (int i = 0; i < 8 && !Is(s); i++) EcoDebug.SkipSeasons(1);
        }

        IEnumerator Fly(BoxView box, float x, float z, float zoom)
        {
            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.FlyTo(box.transform.TransformPoint(box.land.OnGround(x, z)), Mathf.Max(rig.minZoom * zoom, 0.25f), 0.4f);
            for (int i = 0; i < 200 && rig.IsFlying; i++) yield return null;
            yield return Frames(10);
        }

        /// <summary>The view day turns at midnight, not when the sim's month ticks: run the clock over midnight so the view shows the new season.</summary>
        static IEnumerator TurnDay(PeopleView pv)
        {
            long d0 = pv.Now.day;
            PeopleView.DebugSetHour(23.7f);
            pv.timeScale = 20f;
            float t = Time.realtimeSinceStartup;
            while (pv.Now.day == d0 && Time.realtimeSinceStartup - t < 20f) yield return null;
            pv.timeScale = 1f;
            yield return Frames(40);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Seasons_FirstBushSign_Picking()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            PeopleView pv = null;
            try
            {
                yield return OpenSlice(seed: 4242);
                Assert.IsTrue(Sim.EcologyOn, "a living-land box");
                var box = Object.FindAnyObjectByType<BoxView>();
                var bv = BushView.Instance;
                Assert.IsNotNull(bv, "the bush view is on the box");
                pv = PeopleView.Find();
                ToSeason(Season.Spring);
                PeopleView.DebugSetHour(7.5f);
                yield return Frames(30);
                Assert.AreEqual(0, bv.DotsShown, "no bushes, no berries");

                // ---- the first bush: bird, a person pointing, another walking over to look
                StringAssert.DoesNotContain("no bush", EcoDebug.FirstBushNow());
                int first = Sim.FirstBushCell;
                Assert.GreaterOrEqual(first, 0, "the first bush stands");
                pv.timeScale = 3f;
                yield return Until(() => bv.SignsPlayed > 0, 10f);
                Assert.AreEqual(1, bv.SignsPlayed, "the sign started");
                Assert.AreEqual(first, bv.BirdCell);
                Assert.IsFalse(bv.LastPointer.IsNone, "someone near enough to point");
                bool looked = false, pointed = false, landed = false, shot = false;
                float t0 = Time.realtimeSinceStartup;
                while (bv.SignPlaying && Time.realtimeSinceStartup - t0 < 30f)
                {
                    foreach (var a in pv.Activities)
                    {
                        if (a.kind == ActivityKind.Inspect && a.place.kind == PlaceKind.BushCell && a.place.index == first) looked = true;
                        if (a.who.Equals(bv.LastPointer) && a.kind == ActivityKind.Point) pointed = true;
                    }
                    if (bv.BirdLanded) landed = true;
                    if (landed && !shot && Time.realtimeSinceStartup - t0 > 4f)
                    {
                        shot = true;
                        yield return Fly(box, SiteMap.CXc(first), SiteMap.CZc(first), 1.3f);
                        yield return Shot("bush_01_first_bush_bird_close");
                        Camera.main.GetComponent<BoxCamera>().zoom = 1f;
                        yield return Frames(15);
                        yield return Shot("bush_02_first_bush_bird_default");
                    }
                    yield return null;
                }
                Debug.Log($"[bush] sign: landed {landed}, pointed {pointed}, looked {looked}");
                Assert.IsTrue(landed, "the bird landed on the bush");
                Assert.IsTrue(pointed, "the one nearest pointed (Point activity)");
                Assert.IsTrue(looked, "a grown-up walked to look at the bush");
                Assert.IsFalse(bv.SignPlaying, "and the bird flew off");
                pv.timeScale = 1f;
                yield return Frames(60);
                Assert.AreEqual(1, bv.SignsPlayed, "once");

                // ---- spring: flowers
                Assert.Greater(bv.DotsShown, 0, "flowers on the first bush in spring");

                // ---- summer: berries; more bushes; a gatherer picks
                EcoDebug.OpenCampGate(); // before the camp everyone digs roots in the hollow (#285): the bushes are for the camp's gatherers
                EcoDebug.AddBushes(8);
                ToSeason(Season.Summer);
                PeopleView.DebugSetHour(8.5f);
                yield return Frames(40);
                Assert.Greater(bv.BushesWithBerries, 1, "berries on the bushes in summer");
                Assert.Greater(bv.DotsShown, 6);
                var cells = new List<int>(); Sim.BushCells(cells);
                yield return Fly(box, SiteMap.CXc(cells[0]), SiteMap.CZc(cells[0]), 1.6f);
                yield return Shot("bush_03_summer_berries_close");
                Camera.main.GetComponent<BoxCamera>().zoom = 1f;
                yield return Frames(15);
                yield return Shot("bush_04_summer_berries_default");

                pv.timeScale = 6f;
                int pickCell = -1; float before = 0f; bool shotPick = false; int picks0 = bv.PicksDone;
                t0 = Time.realtimeSinceStartup;
                while (bv.PicksDone <= picks0 && Time.realtimeSinceStartup - t0 < 120f)
                {
                    if (pv.ViewHour > 17f) PeopleView.DebugSetHour(8.5f);
                    if (Time.frameCount % 120 == 0)
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var a in pv.Activities) if (a.kind is ActivityKind.Gather or ActivityKind.Eat or ActivityKind.Inspect) sb.Append($"{a.kind}@{a.place}{(a.moving ? "~" : "")} ");
                        Debug.Log($"[bush] diag h{pv.ViewHour:0.0} season {LifeClock.SeasonOf(pv.Now.day)} bushes {cells.Count} berries? {Sim.BerriesGrow} jobs [{string.Join(",", Sim.OpenJobs())}] :: {sb}");
                    }
                    foreach (var a in pv.Activities)
                        if (!a.moving && a.kind == ActivityKind.Gather && a.place.kind == PlaceKind.BushCell && pickCell < 0)
                        {
                            pickCell = a.place.index;
                            before = Sim.BerriesAt(pickCell, pv.Now.day, (int)LifeClock.SeasonOf(pv.Now.day));
                        }
                    if (pickCell >= 0 && !shotPick)
                    {
                        shotPick = true;
                        pv.timeScale = 1f;
                        yield return Fly(box, SiteMap.CXc(pickCell), SiteMap.CZc(pickCell), 1.2f);
                        yield return Shot("bush_05_picker_close");
                        Camera.main.GetComponent<BoxCamera>().zoom = 1f;
                        yield return Frames(10);
                        yield return Shot("bush_06_picker_default");
                        pv.timeScale = 6f;
                    }
                    yield return null;
                }
                Debug.Log($"[bush] pick: cell {pickCell}, berries {before:0.00}, picks {bv.PicksDone - picks0}");
                Assert.GreaterOrEqual(pickCell, 0, "a gatherer walked to a real bush cell and reached");
                Assert.IsTrue(cells.Contains(pickCell), "it is a bush the land grew");
                Assert.Greater(bv.PicksDone, picks0, "the pick ended and the bush was picked");
                Assert.Less(Sim.BerriesAt(bv.LastPickCell, pv.Now.day, (int)LifeClock.SeasonOf(pv.Now.day)), 1f, "berries dropped on that bush");

                // ---- the basket is carried to the fire or the stash
                bool carried = false;
                t0 = Time.realtimeSinceStartup;
                while (!carried && Time.realtimeSinceStartup - t0 < 60f)
                {
                    foreach (var a in pv.Activities)
                        if (a.moving && a.prop == Prop.Basket) carried = true;
                    yield return null;
                }
                pv.timeScale = 1f;
                Assert.IsTrue(carried, "a basket was carried on a walk");

                // ---- winter: bare; next spring: flowers again
                ToSeason(Season.Winter);
                yield return TurnDay(pv);
                yield return Until(() => bv.DotsShown == 0, 3f);
                Assert.AreEqual(0, bv.DotsShown, $"bare in winter (month {Sim.State.month}, sim season {LifeClock.SeasonOf(Sim.State.month)}, view day {pv.Now.day}, state {Sim.BerryStateOf((int)LifeClock.SeasonOf(Sim.State.month))})");
                ToSeason(Season.Spring);
                yield return TurnDay(pv);
                yield return Until(() => bv.DotsShown > 0, 3f);
                Assert.Greater(bv.DotsShown, 0, "flowers in spring");
            }
            finally { if (pv != null) pv.timeScale = 1f; GameClock.EcologyPref = prefWas; }
        }
    }
}
