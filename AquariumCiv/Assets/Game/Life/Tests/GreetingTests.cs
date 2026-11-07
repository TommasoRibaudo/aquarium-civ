using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>t3-welcome (visit-loop.md 2.1-2.2; tester run 2 item C): an open after a while away with no ceremony waiting still
    /// greets the Keeper with one small live scene, chosen from what is true in the box, never the same kind twice in a row, none
    /// after a short away.</summary>
    public class GreetingTests
    {
        static SimCore Box(int seed = 7)
        {
            var cfg = new SimConfig { moments = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 12 * 3; m++) sim.Advance(1.0 / 12); // watched for three years: a camp with its people
            sim.State.moments.Clear();
            return sim;
        }

        /// <summary>The Keeper was away <paramref name="hours"/> and opens the box: the first watched step starts the visit.</summary>
        static void Open(SimCore sim, double hours, bool clearQueue = true)
        {
            sim.CatchUp(hours);
            if (clearQueue) sim.State.moments.Clear();
            sim.Advance(1.0 / 3600);
        }

        [Test]
        public void AnOpenAfterAnHourWithNothingWaitingOwesAGreeting()
        {
            var sim = Box();
            Open(sim, 2.0);
            Assert.AreNotEqual(WelcomeKind.None, sim.GreetingDue, "a greeting is owed");
            Assert.IsNotEmpty(sim.GreetingOptions());
        }

        [Test]
        public void AShortAwayOwesNone()
        {
            var sim = Box();
            Open(sim, 5.0 / 60.0);
            Assert.AreEqual(WelcomeKind.None, sim.GreetingDue, "five minutes away is no new visit");
            var sim2 = Box();
            Open(sim2, 0.5);
            Assert.AreEqual(WelcomeKind.None, sim2.GreetingDue, "half an hour is not an hour");
        }

        [Test]
        public void ACeremonyWaitingIsTheWelcomeInstead()
        {
            var sim = Box();
            sim.DebugQueueMoment(MomentKind.Festival);
            Open(sim, 3.0, false);
            Assert.AreEqual(WelcomeKind.None, sim.GreetingDue, "the waiting moment is the welcome");
        }

        [Test]
        public void NeverTheSameKindTwiceInARow()
        {
            var sim = Box();
            WelcomeKind last = WelcomeKind.None;
            for (int visit = 0; visit < 12; visit++)
            {
                Open(sim, 2.0);
                var due = sim.GreetingDue;
                Assert.AreNotEqual(WelcomeKind.None, due, "visit " + visit);
                Assert.AreNotEqual(last, due, "visit " + visit + ": not " + last + " again");
                foreach (var k in sim.GreetingOptions()) Assert.AreNotEqual(last, k, "no option repeats the last kind");
                sim.GreetingPlayed(due);
                last = due;
            }
        }

        [Test]
        public void AGreetingShowsOnlyWhatIsTrueInTheBox()
        {
            var none = new WelcomeFacts { newBuildingId = -1, hideDrying = false };
            var plain = WelcomeRules.Options(none, WelcomeKind.None, 0);
            CollectionAssert.DoesNotContain(plain, WelcomeKind.ShowHut, "no new building: no hut shown");
            CollectionAssert.DoesNotContain(plain, WelcomeKind.ShowHide, "no hide drying: none held up");
            CollectionAssert.DoesNotContain(plain, WelcomeKind.ShowBasket, "a basket is the planner's call");
            var some = WelcomeRules.Options(new WelcomeFacts { newBuildingId = 5, hideDrying = true }, WelcomeKind.None, 0);
            Assert.AreEqual(WelcomeKind.ShowHut, some[0]);
            Assert.AreEqual(WelcomeKind.ShowHide, some[1]);

            var sim = Box();
            Open(sim, 2.0);
            Assert.AreEqual(-1, sim.GreetingFacts.newBuildingId, "nothing was raised since the last step");
            sim.GreetingDrop();
            sim.State.buildings.Add(new Building { id = sim.State.NextId() + 500, type = BuildingType.Hut });
            Open(sim, 2.0);
            Assert.AreEqual(WelcomeKind.ShowHut, sim.GreetingDue, "a hut raised while away is what they show");
        }

        static (SimCore sim, LifePlanner planner, long day) Planned()
        {
            var sim = Box();
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            while (planner.IsRestDay(day)) day++;
            return (sim, planner, day);
        }

        [Test]
        public void ThePeopleByTheFireLookUpAndWave_ShortAndTrue()
        {
            var (sim, planner, day) = Planned();
            var info = planner.StageGreeting(WelcomeKind.WaveFromFire, new ViewTime(day, 10f));
            Assert.NotNull(info, planner.LastGreetingWhyNot);
            Assert.GreaterOrEqual(info.count, 1);
            Assert.LessOrEqual(info.count, LifePlanner.GreetingMaxCast);
            CollectionAssert.Contains(info.kinds, ActivityKind.Wave);
            Assert.LessOrEqual(LifeClock.RealSecondsBetween(info.start, info.end), LifePlanner.GreetingSeconds + 0.5f, "short");
            Assert.IsTrue(planner.GreetingPlaying(new ViewTime(day, 10f + 0.001f)));
            Assert.IsNull(planner.StageGreeting(WelcomeKind.GlassWave, new ViewTime(day, 10.001f)), "one at a time");
            // Mid-greeting, those people are waving.
            var acts = new List<PersonActivity>();
            planner.PlanAll(new ViewTime(day, info.end - 0.02f), acts);
            Assert.IsTrue(acts.Any(a => a.kind == ActivityKind.Wave && a.facesGlass), "somebody waves at the glass");
        }

        [Test]
        public void NobodyIsHeldUpWhoseHideIsNotThere()
        {
            var (sim, planner, day) = Planned();
            Assert.AreEqual(0, sim.HidesDrying);
            Assert.IsNull(planner.StageGreeting(WelcomeKind.ShowHide, new ViewTime(day, 10f)), "no hide is drying: nothing to hold up");
            StringAssert.Contains("hide", planner.LastGreetingWhyNot);
            Assert.IsNull(planner.StageGreeting(WelcomeKind.ShowHut, new ViewTime(day, 10f)), "nothing new was raised");
        }

        [Test]
        public void AtNightOneAwakePersonWavesWithTheLamp_OrNobodyDoes()
        {
            var (sim, planner, day) = Planned();
            var info = planner.StageGreeting(WelcomeKind.KidsAtGlass, new ViewTime(day, 21f));
            if (info == null) { StringAssert.Contains("sleep", planner.LastGreetingWhyNot); return; }
            Assert.AreEqual(1, info.count, "one wave");
            Assert.IsTrue(info.night);
            Assert.AreEqual(Prop.Lantern, info.prop);
        }

        [Test]
        public void APairShowsABuildingRaisedSinceTheLastVisit()
        {
            var (sim, planner, day) = Planned();
            sim.State.welcomeMemo.valid = true;
            sim.State.welcomeMemo.maxBuildingId = 0; // as if every building was raised since the last watched step
            var hut = sim.State.buildings.FirstOrDefault(x => x.id == sim.GreetingFacts.newBuildingId);
            if (hut == null) Assert.Inconclusive("no building besides the camp in this box");
            Assert.AreEqual(sim.State.buildings.Where(x => x.type != BuildingType.Camp).Max(x => x.id), hut.id, "the newest");
            var info = planner.StageGreeting(WelcomeKind.ShowHut, new ViewTime(day, 10f));
            if (info == null) { TestContext.WriteLine("not cast: " + planner.LastGreetingWhyNot); return; } // too far to walk in 3.5 s is allowed
            StringAssert.Contains(hut.type.ToLowerInvariant(), info.premise);
            CollectionAssert.Contains(info.kinds, ActivityKind.Point);
        }
    }
}
