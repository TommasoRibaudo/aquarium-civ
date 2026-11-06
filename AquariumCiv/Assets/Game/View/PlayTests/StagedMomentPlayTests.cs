using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#370 on the real scene: a wedding line made while watching is held for the Keeper and staged once (C2's risk: it
    /// also got the planner's same-day scene), its cast stands on the plan's spots, and the director hears it end
    /// (<see cref="SimCore.FinishMoment"/>). A stopped moment lets everyone go.</summary>
    public class StagedMomentPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;

        IEnumerator Box()
        {
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(3);
            yield return null;
            GameClock.Instance.config.moments = true;
            Sim.State.moments.Clear();
            PeopleView.DebugSetHour(9.5f);
            yield return Frames(10);
            Assert.IsNotNull(HeldMomentStage.Instance, "the director's stage is in the scene");
        }

        static PersonRef Ref(int key) => key > 0 ? PersonRef.Notable(key) : PersonRef.Crowd(-key - 1);

        [UnityTest, Timeout(240000)]
        public IEnumerator AWeddingMadeWhileWatching_PlaysOnce_Staged_AndTheDirectorHearsItEnd()
        {
            yield return Box();
            var pv = PeopleView.Find();
            var adults = Sim.State.notables.Where(n => n.alive && n.age >= SimCore.MarryAge).OrderBy(n => n.id).Take(2).ToList();
            Assume.That(adults.Count, Is.EqualTo(2), "a box with two grown-ups");
            int started = StagedMoments.Started, finished = StagedMoments.Finished;
            pv.timeScale = 3f; // a 35 s ceremony in about 12 s
            var e = Sim.DebugChronicle("wedding", $"{adults[0].FullName} and {adults[1].FullName} promised each other at the fire.", adults[0].id, adults[1].id);
            var m = Sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m, "the wedding's ceremony waits for the Keeper");
            pv.Planner.Invalidate();
            yield return Frames(3);
            var day = new List<PersonActivity>();
            pv.Planner.DaySchedule(PersonRef.Notable(adults[0].id), pv.Now.day, day);
            Assert.AreEqual(0, day.Count(a => a.chronicleId == e.id), "no same-day scene for the line: it plays once, staged");

            Sim.DebugMomentDueNow();
            HeldMomentStage.Instance.Check(); // the director may have started it on its own look already
            yield return Until(() => m.state == HeldMomentState.Playing, 5f);
            Assert.AreEqual(HeldMomentState.Playing, m.state, "the wedding starts: " + HeldMomentStage.Instance.LastWhy);
            Assert.AreEqual(started + 1, StagedMoments.Started);
            var sc = StagedMoments.LastScene;
            Assert.AreEqual(m.id, sc.momentId);
            Assert.IsNotNull(sc.plan.Member(adults[0].id)); Assert.IsNotNull(sc.plan.Member(adults[1].id));
            Assert.AreEqual(HeldMomentState.Playing, m.state);

            // Mid-dance: the dancers stand on the plan's ring.
            yield return Until(() => pv.Planner.HeldMomentAt(Ref(sc.plan.ringKeys[0]), pv.Now, out _, out _, out _, out float s) && s > sc.plan.ringT0 + 3f, 60f);
            int near = 0;
            foreach (int key in sc.plan.ringKeys)
            {
                if (!pv.Planner.HeldMomentAt(Ref(key), pv.Now, out _, out var mem, out int i, out float secs) || !pv.TryGetDollPosition(Ref(key), out var at)) continue;
                var want = MomentBeats.Spot(sc.plan, mem, i, secs, out _);
                if ((want - at).magnitude < 0.2f) near++;
            }
            Assert.GreaterOrEqual(near, sc.plan.ringKeys.Count / 2, "the ring stands where the plan puts it");

            yield return Until(() => m.state != HeldMomentState.Playing, 60f);
            pv.timeScale = 1f;
            Assert.AreEqual(HeldMomentState.Played, m.state, "the director heard it end: FinishMoment(played)");
            Assert.AreEqual(finished + 1, StagedMoments.Finished);
            Assert.AreEqual(started + 1, StagedMoments.Started, "staged once");
            yield return Frames(5);
            HeldMomentStage.Instance.Check();
            Assert.AreEqual(started + 1, StagedMoments.Started, "not staged again");
            Assert.IsFalse(sc.plan.members.Any(x => pv.Planner.HeldMomentAt(Ref(x.key), pv.Now, out _, out _, out _, out _)), "everyone is back in their day");
            AssertNoErrors("the staged wedding");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator StoppingAMoment_ReleasesEveryone_AndTheCheatsPlayEachKind()
        {
            yield return Box();
            var pv = PeopleView.Find();
            string line = StagedMoments.DebugPlayNow(MomentKind.Festival);
            Assert.IsNotNull(StagedMoments.LastScene, line);
            var sc = StagedMoments.LastScene;
            Assert.AreEqual(MomentKind.Festival, sc.kind, line);
            yield return Frames(60);
            Assert.AreEqual(1, StagedMoments.Playing);
            Assert.AreEqual(1, StagedMoments.StopAll());
            Assert.AreEqual(0, StagedMoments.Playing);
            Assert.IsFalse(sc.plan.members.Any(x => pv.Planner.HeldMomentAt(Ref(x.key), pv.Now, out _, out _, out _, out _)), "everyone let go at once");
            yield return Until(() => Sim.PlayingMoments().Count == 0, 5f);
            Assert.AreEqual(0, Sim.PlayingMoments().Count, "the director closes it");

            foreach (var k in new[] { MomentKind.Wedding, MomentKind.Newborn, MomentKind.NewLeader })
            {
                string r = StagedMoments.DebugPlayNow(k);
                if (r.StartsWith("nobody holds")) continue; // a box with no leader yet: nothing to stage
                Assert.AreEqual(k, StagedMoments.LastScene?.kind, $"{k}: {r}");
                yield return Frames(30);
                StagedMoments.StopAll();
                yield return Until(() => Sim.PlayingMoments().Count == 0, 5f);
            }
            pv.timeScale = 1f;
            AssertNoErrors("the cheats");
        }
    }
}
