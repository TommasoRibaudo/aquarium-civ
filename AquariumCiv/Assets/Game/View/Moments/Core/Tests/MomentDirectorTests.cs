using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;

namespace AquariumCiv.View.Moments.Tests
{
    /// <summary>The moment director (#88): beats from data, hold to hurry, the cheer as the only era move, leaving
    /// half-way replays, replays never move the era.</summary>
    public class MomentDirectorTests
    {
        static SimCore NewSim(int seed = 42)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static MomentDef Stub() => new()
        {
            id = "stub", toEra = Era.Village,
            beats =
            {
                new MomentBeat { id = "a", start = 0f, actions = { BeatAction.Gather }, camera = { new CameraKey(1f, MomentAnchor.Hearth, 10f, 30f, 0.6f) } },
                new MomentBeat { id = "b", start = 2f, actions = { BeatAction.LeaderSpeaks } },
                new MomentBeat { id = "cheer", start = 4f, actions = { BeatAction.Cheer } },
                new MomentBeat { id = "end", start = 5f, hold = 1f, freeCamera = true },
            },
        };

        static void Run(MomentDirector d, float seconds, bool hurry = false, float dt = 0.1f)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += dt) d.Tick(dt, hurry);
        }

        [Test]
        public void StubPlaysEveryBeatInOrderAndFinishes()
        {
            int cheers = 0;
            var d = new MomentDirector(() => { cheers++; return true; });
            var seen = new List<string>();
            d.BeatStarted += (b, i) => seen.Add(b.id);
            bool finished = false; d.Finished += () => finished = true;
            Assert.IsTrue(d.Start(Stub(), new MomentContext(), CameraPose.Default));
            Assert.AreEqual(6f, d.Length, 1e-4f);
            Assert.IsTrue(d.HoldsSimClock);
            Run(d, 6.05f);
            CollectionAssert.AreEqual(new[] { "a", "b", "cheer", "end" }, seen);
            Assert.AreEqual(1, cheers);
            Assert.IsTrue(finished && d.State == MomentDirector.Phase.Finished && !d.HoldsSimClock);
            Assert.IsTrue(d.EraAdvanced);
        }

        [Test]
        public void HoldToHurryPlaysAtTwiceTheSpeed()
        {
            var d = new MomentDirector(() => true);
            d.Start(Stub(), new MomentContext(), CameraPose.Default);
            Run(d, 1f, hurry: true);
            Assert.AreEqual(2f, d.Time, 1e-3f);
            Assert.IsTrue(d.Hurrying);
            Run(d, 1f, hurry: false);
            Assert.AreEqual(3f, d.Time, 1e-3f);
            Assert.IsFalse(d.Hurrying);
            Run(d, 1.6f, hurry: true);
            Assert.AreEqual(MomentDirector.Phase.Finished, d.State, "6 s of moment in 1 + 1 + 1.5 real seconds");
        }

        [Test]
        public void HurryFactorIsTunable()
        {
            var d = new MomentDirector(() => true) { HurryFactor = 4f };
            d.Start(Stub(), new MomentContext(), CameraPose.Default);
            Run(d, 1f, hurry: true);
            Assert.AreEqual(4f, d.Time, 1e-3f);
        }

        [Test]
        public void HurryHintShowsForTheFirstThreeRealSeconds()
        {
            var d = new MomentDirector(() => true);
            d.Start(VeryLong(), new MomentContext(), CameraPose.Default);
            Assert.IsTrue(d.ShowHurryHint);
            Run(d, 2.9f, hurry: true);
            Assert.IsTrue(d.ShowHurryHint, "real time, not moment time");
            Run(d, 0.2f);
            Assert.IsFalse(d.ShowHurryHint);
        }

        static MomentDef VeryLong()
        {
            var m = Stub();
            m.beats[3].hold = 100f;
            return m;
        }

        [Test]
        public void ABigFrameStillRaisesEveryBeatAndCheersOnce()
        {
            int cheers = 0;
            var d = new MomentDirector(() => { cheers++; return true; });
            var seen = new List<string>();
            d.BeatStarted += (b, i) => seen.Add(b.id);
            d.Start(Stub(), new MomentContext(), CameraPose.Default);
            d.Tick(10f, false);
            CollectionAssert.AreEqual(new[] { "a", "b", "cheer", "end" }, seen);
            Assert.AreEqual(1, cheers);
            Assert.AreEqual(MomentDirector.Phase.Finished, d.State);
        }

        [Test]
        public void LeavingBeforeTheCheerReplaysAndTheEraWaits()
        {
            var sim = NewSim();
            sim.ForceEraReady();
            var d = new MomentDirector(sim.AdvanceEra);
            var def = MomentLibrary.For(sim.NextEra);
            d.Start(def, MomentContext.From(sim), CameraPose.Default);
            Run(d, 30f, dt: 0.25f);
            Assert.AreEqual(MomentDirector.LeaveResult.Replays, d.Leave());
            Assert.AreEqual(MomentDirector.Phase.Idle, d.State);
            Assert.AreEqual(Era.Camp, sim.State.era);
            Assert.IsTrue(sim.State.eraReady, "the era still waits");

            // Watch again: it plays from the start and the era moves at the cheer.
            var seen = new List<string>();
            d.BeatStarted += (b, i) => seen.Add(b.id);
            Assert.IsTrue(d.Start(def, MomentContext.From(sim), CameraPose.Default));
            Assert.AreEqual("dawn", seen[0]);
            Run(d, def.Beat("cheer").start - 0.5f, dt: 0.25f);
            Assert.AreEqual(Era.Camp, sim.State.era, "not before the cheer");
            Run(d, 1f, dt: 0.25f);
            Assert.AreEqual(Era.Village, sim.State.era);
            Assert.IsTrue(d.EraAdvanced);
        }

        [Test]
        public void LeavingAfterTheCheerFinishesWithTheEraMoved()
        {
            var sim = NewSim();
            sim.ForceEraReady();
            var d = new MomentDirector(sim.AdvanceEra);
            d.Start(MomentLibrary.CampToVillage(), MomentContext.From(sim), CameraPose.Default);
            Run(d, 50f, dt: 0.25f);
            Assert.AreEqual(MomentDirector.LeaveResult.AlreadyCrossed, d.Leave());
            Assert.AreEqual(MomentDirector.Phase.Finished, d.State);
            Assert.AreEqual(Era.Village, sim.State.era);
            Assert.IsTrue(sim.FestivalOn);
            Assert.AreEqual(MomentDirector.LeaveResult.NotPlaying, d.Leave());
        }

        [Test]
        public void AReplayNeverMovesTheEra()
        {
            var sim = NewSim();
            sim.ForceEraReady();
            var d = new MomentDirector(sim.AdvanceEra);
            d.Start(MomentLibrary.CampToVillage(), MomentContext.From(sim, replay: true), CameraPose.Default);
            Run(d, 80f, dt: 0.5f);
            Assert.IsTrue(d.Cheered);
            Assert.IsFalse(d.EraAdvanced);
            Assert.AreEqual(Era.Camp, sim.State.era);
            Assert.IsNull(d.Beats.FirstOrDefault(b => b.id == "notify"), "a replay never asks about notifications");
        }

        [Test]
        public void StartIsRefusedWhilePlaying()
        {
            var d = new MomentDirector(() => true);
            Assert.IsTrue(d.Start(Stub(), new MomentContext(), CameraPose.Default));
            Assert.IsFalse(d.Start(Stub(), new MomentContext(), CameraPose.Default));
            Assert.IsFalse(d.Start(null, new MomentContext(), CameraPose.Default));
        }

        [Test]
        public void TheCameraIsGuidedUntilAFreeBeat()
        {
            var d = new MomentDirector(() => true);
            d.Start(Stub(), new MomentContext(), CameraPose.Default);
            Assert.IsTrue(d.CameraGuided);
            Run(d, 5.05f);
            Assert.IsFalse(d.CameraGuided);
        }

        [Test]
        public void BeatProgressRunsThroughEachBeat()
        {
            var d = new MomentDirector(() => true);
            d.Start(Stub(), new MomentContext(), CameraPose.Default);
            Run(d, 1f);
            Assert.AreEqual("a", d.CurrentBeat.id);
            Assert.AreEqual(0.5f, d.BeatProgress, 1e-3f);
            Assert.AreEqual(1, d.BeatAt(2.5f));
        }

        // ---------- conditions and context ----------
        [Test]
        public void FirstMomentAsksTheQuestionOnceThenNever()
        {
            var sim = NewSim();
            sim.ForceEraReady();
            var ctx = MomentContext.From(sim);
            Assert.IsTrue(ctx.firstMoment);
            var d = new MomentDirector(sim.AdvanceEra);
            d.Start(MomentLibrary.CampToVillage(), ctx, CameraPose.Default);
            Assert.AreEqual("notify", d.Beats.Last().id);
            Assert.AreEqual(71f, d.Length, 1e-3f);

            MomentContext.MarkNotifyAsked(sim.State);
            MomentContext.MarkNotifyAsked(sim.State);
            Assert.AreEqual(1, sim.State.storyFlags.Count(f => f == MomentContext.FlagNotifyAsked));
            var again = MomentContext.From(sim);
            Assert.IsFalse(again.firstMoment);
            d = new MomentDirector(sim.AdvanceEra);
            d.Start(MomentLibrary.CampToVillage(), again, CameraPose.Default);
            Assert.AreEqual("festival", d.Beats.Last().id);
            Assert.AreEqual(65f, d.Length, 1e-3f);
        }

        [Test]
        public void AnEmptyLeaderSeatPlaysTheVoteInPlaceOfTheSash()
        {
            var def = MomentLibrary.CampToVillage();
            var d = new MomentDirector(() => true);
            d.Start(def, new MomentContext { leaderPresent = false }, CameraPose.Default);
            var ids = d.Beats.Select(b => b.id).ToList();
            CollectionAssert.Contains(ids, "vote");
            CollectionAssert.Contains(ids, "elder");
            CollectionAssert.DoesNotContain(ids, "headman");
            CollectionAssert.DoesNotContain(ids, "chief");
        }

        [Test]
        public void ContextReadsTheSim()
        {
            var sim = NewSim(7);
            var ctx = MomentContext.From(sim);
            var chief = sim.HolderOf(RoleType.Chief);
            Assert.AreEqual(chief != null, ctx.leaderPresent);
            if (chief != null) Assert.AreEqual(chief.name, ctx.leaderName);
            Assert.AreEqual(SimCore.YearNo(sim.State.month), ctx.year);
            Assert.IsFalse(ctx.somethingCleared, "only the town moment clears the camp hearth");
            Assert.AreEqual("Headman Ada, then.", new MomentContext { leaderName = "Ada" }.Fill("Headman {leader}, then."));
            Assert.AreEqual("Glasswick · a farming village · year 14",
                new MomentContext { place = "Glasswick", year = 14 }.Fill("{place} · a farming village · year {year}"));
        }

        [Test]
        public void ContextSaysTheTownClearsTheCampHearth()
        {
            var sim = NewSim(7);
            sim.ForceNextEra();
            sim.ForceEraReady();
            var ctx = MomentContext.From(sim);
            Assert.AreEqual(sim.State.buildings.Any(b => b.type == BuildingType.Camp), ctx.somethingCleared);
        }

        // ---------- the whole thing, both moments, against the sim ----------
        [TestCase(1)]
        [TestCase(42)]
        public void BothMomentsCarryTheBoxToTheTown(int seed)
        {
            var sim = NewSim(seed);
            foreach (var to in new[] { Era.Village, Era.Town })
            {
                sim.ForceEraReady();
                var d = new MomentDirector(sim.AdvanceEra);
                int cheers = 0; d.CheerPlayed += ok => { if (ok) cheers++; };
                d.Start(MomentLibrary.For(to), MomentContext.From(sim), CameraPose.Default);
                Run(d, d.Length + 1f, hurry: true, dt: 1f / 30f);
                Assert.AreEqual(MomentDirector.Phase.Finished, d.State);
                Assert.AreEqual(1, cheers);
                Assert.AreEqual(to, sim.State.era);
            }
            Assert.IsNull(MomentLibrary.For(Era.Camp));
        }
    }
}
