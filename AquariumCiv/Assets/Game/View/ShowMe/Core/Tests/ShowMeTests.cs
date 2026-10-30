using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>#76 phase 1: "Show me" finds the right place and people for every kind of card (the building, the person,
    /// the relic, the shaping, the remembering stone, the year's spot, the whole box), with a fallback when the person or the
    /// building is gone; the echo is the §2.5 table, staged by the planner at the walking pace for 3–5 s; nothing here
    /// touches the sim.</summary>
    public class ShowMeTests
    {
        internal static SimCore NewSim(int seed)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        internal static SimCore SimIn(Era era, int seed)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assert.AreEqual(era, sim.State.era, $"seed {seed} reaches {era}");
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        static SimCore town, village;
        static SimCore Town => town ??= SimIn(Era.Town, 1234);
        static SimCore Village => village ??= SimIn(Era.Village, 7);

        const float BoundX = 3.3f, BoundZ = 2.5f;

        static void InBox(ShowMeTarget t)
        {
            Assert.IsTrue(t.hasPoint, $"{t}: a point to fly to");
            Assert.LessOrEqual(Math.Abs(t.x), BoundX, $"{t}: inside the glass");
            Assert.LessOrEqual(Math.Abs(t.z), BoundZ, $"{t}: inside the glass");
            Assert.That(t.zoom, Is.InRange(0.3f, 1f));
            Assert.That(t.anchorY, Is.InRange(0.55f, 0.85f), "above the card");
        }

        static Notable Grown(SimCore sim, int skip = 0) =>
            sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).Skip(skip).First();

        static ChronicleEntry Entry(SimCore sim, string kind, int a = -1, int b = -1, int building = -1, int id = -7) =>
            new ChronicleEntry { id = id, month = sim.State.month, kind = kind, text = kind, salience = 0.8f, notableId = a, otherNotableId = b, buildingId = building };

        /// <summary>A building standing now (the camp's hearth is kept on a retired lot after the era moment).</summary>
        static Building Standing(SimCore sim) =>
            sim.State.buildings.First(b => b.lotId > 0 && sim.State.settle.lots.Find(l => l.id == b.lotId)?.retired == false);

        static string Snapshot(SimCore sim) => JsonUtility.ToJson(sim.State);

        // ------------------------------------------------------------------ targets

        [Test]
        public void EveryEntryOfABoxHasAPlaceInsideTheGlass()
        {
            var sim = Town;
            string before = Snapshot(sim);
            var show = new ShowMeCore(sim);
            var subjects = new HashSet<ShowMeSubject>();
            var kinds = new HashSet<string>();
            foreach (var e in sim.State.chronicle)
            {
                var plan = show.Plan(e);
                InBox(plan.target);
                Assert.IsNotNull(plan.echo, "always an echo record, playing or not");
                subjects.Add(plan.target.subject);
                kinds.Add(e.kind);
                if (plan.target.followId >= 0) Assert.AreEqual(ShowMeSubject.Person, plan.target.subject);
                foreach (int id in plan.target.people) Assert.IsTrue(sim.FindNotable(id).alive, "only the living are framed");
            }
            TestContext.WriteLine($"{sim.State.chronicle.Count} entries, kinds {string.Join(",", kinds)}; subjects {string.Join(",", subjects)}");
            Assert.GreaterOrEqual(subjects.Count, 3, "several kinds of place");
            Assert.AreEqual(before, Snapshot(sim), "Show me never touches the sim");
        }

        [Test]
        public void TheReturnCardsAllResolve()
        {
            var sim = Town;
            var show = new ShowMeCore(sim);
            var stack = sim.ReturnCardsCapped(Math.Max(0, sim.State.month - 60), 7);
            Assert.IsNotEmpty(stack.cards);
            foreach (var c in stack.cards.Concat(stack.also))
            {
                var plan = show.Plan(c);
                InBox(plan.target);
                TestContext.WriteLine($"{c.kind} {c.lead.kind}: {plan.target}; echo {(plan.HasEcho ? plan.echo.kind.ToString() : plan.echo.skip.ToString())}");
            }
        }

        [Test]
        public void ABuildingStoryFliesToTheBuilding()
        {
            var sim = Town;
            var b = Standing(sim);
            var t = new ShowMeCore(sim).Target(Entry(sim, "keeper", Grown(sim).id, -1, b.id));
            Assert.AreEqual(ShowMeSubject.Building, t.subject);
            Assert.AreEqual(b.id, t.buildingId);
            Assert.AreEqual(ShowMeFallback.None, t.fallback);
            Assert.Less(Math.Abs(t.x - b.x) + Math.Abs(t.z - b.z), 1.2f, "at the building's lot");
            Assert.Contains(Grown(sim).id, t.people);
        }

        [Test]
        public void APersonStoryFollowsThePerson()
        {
            var sim = Town;
            Notable a = Grown(sim), b = Grown(sim, 1);
            var t = new ShowMeCore(sim).Target(Entry(sim, "friendship", a.id, b.id));
            Assert.AreEqual(ShowMeSubject.Person, t.subject);
            Assert.AreEqual(a.id, t.followId, "the camera follows the lead's doll");
            CollectionAssert.AreEqual(new[] { a.id, b.id }, t.people);
            Assert.AreEqual(ShowMeCore.PersonZoom, t.zoom);
            InBox(t);
        }

        [Test]
        public void ABirthFollowsTheParent()
        {
            var sim = Town;
            var child = sim.State.notables.FirstOrDefault(n => n.alive && n.parentId >= 0 && sim.FindNotable(n.parentId)?.alive == true);
            Assume.That(child, Is.Not.Null, "a child with a living parent");
            var plan = new ShowMeCore(sim).Plan(Entry(sim, "birth", child.id));
            Assert.AreEqual(child.parentId, plan.target.followId);
            Assert.IsTrue(plan.HasEcho);
            Assert.AreEqual(child.parentId, plan.echo.leadId, "a parent holds the baby up");
            Assert.AreEqual(EchoWhere.Home, plan.echo.where);
        }

        [Test]
        public void ADeathGoesToTheRememberingStoneWithAFlower()
        {
            var sim = Town;
            var dead = sim.State.notables.FirstOrDefault(n => !n.alive) ?? Grown(sim);
            var plan = new ShowMeCore(sim).Plan(Entry(sim, "death", dead.id));
            Assert.AreEqual(ShowMeSubject.Remembering, plan.target.subject);
            InBox(plan.target);
            Assert.IsFalse(plan.HasEcho, "no echo for a death");
            Assert.AreEqual(EchoSkip.Death, plan.echo.skip);
            Assert.IsTrue(plan.echo.flower, "a flower at the stone");
        }

        [Test]
        public void TheRetiredHearthIsGoneToo()
        {
            var sim = Town;
            var hearth = sim.State.buildings.FirstOrDefault(b => b.lotId > 0 && sim.State.settle.lots.Find(l => l.id == b.lotId)?.retired == true);
            Assume.That(hearth, Is.Not.Null, "a building on a retired lot");
            var t = new ShowMeCore(sim).Target(Entry(sim, "first_building", -1, -1, hearth.id));
            Assert.AreEqual(ShowMeFallback.BuildingGone, t.fallback);
            Assert.AreEqual(ShowMeSubject.Spot, t.subject, "where it stood");
            InBox(t);
        }

        [Test]
        public void AGoneBuildingFallsBackToWhereItStood()
        {
            var sim = Town;
            var e = Entry(sim, "invention", Grown(sim).id, -1, 999_999);
            var t = new ShowMeCore(sim).Target(e);
            Assert.AreEqual(ShowMeFallback.BuildingGone, t.fallback);
            Assert.AreEqual(ShowMeSubject.Person, t.subject, "then the maker");
            InBox(t);
        }

        [Test]
        public void AStoryWhosePeopleAreGoneGoesToTheirSpotOrTheStone()
        {
            var sim = Town;
            var dead = sim.State.notables.Where(n => !n.alive).Take(2).ToList();
            Assume.That(dead.Count, Is.EqualTo(2), "two of the dead");
            var plan = new ShowMeCore(sim).Plan(Entry(sim, "wedding", dead[0].id, dead[1].id));
            Assert.AreEqual(ShowMeFallback.PersonDied, plan.target.fallback);
            Assert.AreEqual(-1, plan.target.followId);
            Assert.That(plan.target.subject, Is.Not.EqualTo(ShowMeSubject.Person).And.Not.EqualTo(ShowMeSubject.None));
            InBox(plan.target);
            Assert.AreEqual(EchoSkip.PeopleGone, plan.echo.skip, "nobody to play it: fly-to only");
        }

        [Test]
        public void ARelicStoryFliesToTheRelicAndItsOwnerGestures()
        {
            var sim = SimIn(Era.Village, 7);
            var r = sim.DebugGiveRelic(RelicId.KeeperDoll);
            Assert.IsNotNull(r);
            var e = sim.State.chronicle.Last(x => x.relicId == RelicId.KeeperDoll && x.kind == "relic");
            var plan = new ShowMeCore(sim).Plan(e);
            Assert.AreEqual(ShowMeSubject.Relic, plan.target.subject);
            Assert.AreEqual(r.x, plan.target.x, 1e-4f);
            Assert.AreEqual(r.z, plan.target.z, 1e-4f);
            Assert.IsTrue(plan.HasEcho);
            Assert.AreEqual(EchoWhere.Relic, plan.echo.where);
            Assert.IsTrue(plan.echo.def.beats.Any(b => b.cast == RelicCast.Grown && b.kind == ActivityKind.Point), "people around it pointing");
        }

        [Test]
        public void AKeepersGiftWithoutABuildingGoesToTheShaping()
        {
            var sim = SimIn(Era.Village, 7);
            var who = Grown(sim);
            var n = new Nudge { id = 4242, kind = SimCore.NudgeKind.Grove, x = 1.1f, z = -0.7f, month = sim.State.month - 9, credited = true, answerMonth = sim.State.month, buildingId = -1 };
            sim.State.nudges.Add(n);
            var plan = new ShowMeCore(sim).Plan(Entry(sim, "keeper", who.id));
            Assert.AreEqual(ShowMeSubject.Shaping, plan.target.subject);
            Assert.AreEqual(4242, plan.target.nudgeId);
            Assert.AreEqual(1.1f, plan.target.x, 1e-4f);
            Assert.IsTrue(plan.HasEcho);
            Assert.AreEqual(EchoWhere.Shaping, plan.echo.where);
            var raise = plan.echo.def.beats.First(b => b.cast == RelicCast.Owner);
            Assert.AreEqual(ActivityKind.HoldUp, raise.kind, "a tool raised");
            Assert.AreEqual(Prop.Axe, raise.prop);
            Assert.IsTrue(raise.facesGlass, "to the Keeper");
        }

        [Test]
        public void TheWeatherAndTheErasAreThePlaceOnly()
        {
            var sim = Town;
            var show = new ShowMeCore(sim);
            foreach (var k in new[] { "storm", "fog", "snow", "rainbow" })
            {
                var plan = show.Plan(Entry(sim, k));
                Assert.AreEqual(EchoSkip.Weather, plan.echo.skip, k);
                InBox(plan.target);
            }
            var era = show.Plan(Entry(sim, "era"));
            Assert.AreEqual(EchoSkip.BoxWide, era.echo.skip);
            Assert.AreEqual(ShowMeSubject.Box, era.target.subject);
            Assert.AreEqual(ShowMeCore.BoxZoom, era.target.zoom);
        }

        [Test]
        public void AnOldStoryIsFlyToOnly()
        {
            var sim = Town;
            var e = Entry(sim, "friendship", Grown(sim).id, Grown(sim, 1).id);
            e.month = sim.State.month - EchoScenes.MaxAgeMonths - 1;
            var plan = new ShowMeCore(sim).Plan(e);
            Assert.AreEqual(EchoSkip.TooOld, plan.echo.skip);
            Assert.AreEqual(ShowMeSubject.Person, plan.target.subject, "the fly-to still finds them");
        }

        [Test]
        public void FlyTimeIsAtMostOnePointTwoSeconds()
        {
            Assert.LessOrEqual(ShowMeCore.FlySeconds(100f), 1.2f);
            Assert.GreaterOrEqual(ShowMeCore.FlySeconds(0f), 0.5f);
            Assert.Less(ShowMeCore.FlySeconds(0.5f), ShowMeCore.FlySeconds(4f));
        }

        [Test]
        public void TheSameEntryGivesTheSamePlan()
        {
            var sim = Town;
            var a = new ShowMeCore(sim);
            var b = new ShowMeCore(sim);
            foreach (var e in sim.State.chronicle.Skip(Math.Max(0, sim.State.chronicle.Count - 40)))
            {
                var pa = a.Plan(e); var pb = b.Plan(e);
                Assert.AreEqual(pa.target.ToString(), pb.target.ToString());
                Assert.AreEqual(pa.echo.skip, pb.echo.skip);
                Assert.AreEqual(pa.echo.line, pb.echo.line);
            }
        }

        // ------------------------------------------------------------------ the echo, staged

        static readonly string[] EchoKinds = { "wedding", "friendship", "rivalry", "reconcile", "keeper", "invention", "role_won", "song" };

        [Test]
        public void EchoesPlayForThreeToFiveSecondsAtValidPlaces([ValueSource(nameof(EchoKinds))] string kind)
        {
            var sim = Town;
            Notable a = Grown(sim), b = Grown(sim, 1);
            var e = Entry(sim, kind, a.id, b.id, kind == "keeper" ? Standing(sim).id : -1);
            var plan = new ShowMeCore(sim).Plan(e);
            Assert.IsTrue(plan.HasEcho, $"{kind} has an echo");
            Assert.That(plan.echo.Seconds, Is.InRange(EchoScenes.MinSeconds, EchoScenes.MaxSeconds));

            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            while (planner.IsRestDay(day)) day++;
            var at = new ViewTime(day, 10.5f);
            var info = planner.StageEcho(plan.echo, at);
            Assert.IsNotNull(info, $"{kind}: staged");
            Assert.IsTrue(info.cast.Any(c => c.who.notableId == plan.echo.leadId && c.role == RelicCast.Owner), "the lead plays it");
            float secs = LifeClock.RealSecondsBetween(at.hour, info.end);
            Assert.That(secs, Is.InRange(EchoScenes.MinSeconds - 0.05f, EchoScenes.MaxSeconds + 0.05f), "3–5 real seconds");
            Assert.IsTrue(planner.IsValidPlace(info.spot), $"echo spot {info.spot}");

            var acts = new List<PersonActivity>();
            var echoes = new List<RelicMomentInfo>();
            var lines = new List<RelicLine>();
            var leadKinds = new HashSet<ActivityKind>();
            var owner = plan.echo.def.beats.Where(x => x.cast == RelicCast.Owner).Select(x => x.kind).ToHashSet();
            bool spoke = false;
            for (float h = at.hour; h < info.end; h += 0.002f)
            {
                var t = new ViewTime(day, h);
                planner.PlanAll(t, acts);
                foreach (var x in acts)
                {
                    Assert.IsTrue(planner.IsValidPlace(x.place) && planner.IsValidPlace(x.from), $"{x.who} {x.kind} at a real place");
                    if (x.who.notableId == plan.echo.leadId && x.source == ActivitySource.Scene) leadKinds.Add(x.kind);
                }
                Assert.AreEqual(1, planner.ActiveEchoes(t, echoes));
                if (planner.EchoLinesAt(t, lines) > 0) { spoke = true; Assert.AreEqual(plan.echo.line, lines[0].text); }
            }
            TestContext.WriteLine($"{kind}: {plan.target}; at {info.spot}; cast {string.Join(", ", info.cast.Select(c => $"{c.who} {c.role}"))}; lead {string.Join(",", leadKinds)}; line \"{plan.echo.line}\"");
            Assert.IsTrue(leadKinds.Overlaps(owner), $"the lead does the story's pose ({string.Join(",", owner)})");
            if (!string.IsNullOrEmpty(plan.echo.line)) Assert.IsTrue(spoke, "the echo's line is said");
            Assert.AreEqual(0, planner.FallbackCount);
            Assert.AreEqual(0, planner.ActiveEchoes(new ViewTime(day, info.end + 0.01f), echoes), "then they go back to their day");
        }

        [Test]
        public void ANewEchoEndsTheLastAndEndEchoesClearsThem()
        {
            var sim = Town;
            var show = new ShowMeCore(sim);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            while (planner.IsRestDay(day)) day++;
            var at = new ViewTime(day, 11f);
            Assert.IsNotNull(planner.StageEcho(show.Echo(Entry(sim, "invention", Grown(sim).id)), at));
            Assert.IsNotNull(planner.StageEcho(show.Echo(Entry(sim, "friendship", Grown(sim, 2).id, Grown(sim, 3).id)), at));
            var echoes = new List<RelicMomentInfo>();
            Assert.AreEqual(1, planner.ActiveEchoes(new ViewTime(day, at.hour + 0.01f), echoes), "one echo at a time");
            planner.EndEchoes();
            Assert.AreEqual(0, planner.ActiveEchoes(new ViewTime(day, at.hour + 0.01f), echoes));
        }

        [Test]
        public void NoEchoIsStagedWhenItHasNone()
        {
            var sim = Town;
            var planner = LifePlanner.Wired(sim);
            Assert.IsNull(planner.StageEcho(EchoScenes.For(sim, Entry(sim, "storm")), new ViewTime(sim.State.month, 10f)));
            Assert.IsNull(planner.StageEcho(null, new ViewTime(sim.State.month, 10f)));
        }

        [Test]
        public void EveryRecentEchoInABoxStagesCleanly()
        {
            var sim = Town;
            string before = Snapshot(sim);
            var show = new ShowMeCore(sim);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            while (planner.IsRestDay(day)) day++;
            var acts = new List<PersonActivity>();
            int staged = 0, plays = 0;
            foreach (var e in sim.State.chronicle.Where(x => sim.State.month - x.month <= EchoScenes.MaxAgeMonths))
            {
                var echo = show.Echo(e);
                if (!echo.Plays) continue;
                plays++;
                var at = new ViewTime(day, 9f + (staged % 8));
                var info = planner.StageEcho(echo, at);
                if (info == null) continue;
                staged++;
                for (float h = at.hour; h < info.end; h += 0.01f)
                {
                    planner.PlanAll(new ViewTime(day, h), acts);
                    foreach (var x in acts) Assert.IsTrue(planner.IsValidPlace(x.place) && planner.IsValidPlace(x.from), $"{e.kind}: {x.who} {x.kind} at a real place");
                }
                planner.EndEchoes();
            }
            TestContext.WriteLine($"{plays} recent stories have an echo, {staged} staged");
            Assert.Greater(staged, 0);
            Assert.AreEqual(0, planner.FallbackCount);
            Assert.AreEqual(before, Snapshot(sim), "the echo never touches the sim");
        }
    }
}
