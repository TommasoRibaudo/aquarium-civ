using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#92 (relics-and-glass-voice.md §1, §3): the people at the glass per belief stage and half, the relic moments
    /// and the tap-back, as the planner stages them. Every walk to the glass or a relic keeps the walking pace, nobody is
    /// left at a missing place, no more than six stand at the glass, and the planner never touches the sim.</summary>
    public class LifeGlassTests
    {
        static SimCore NewSim(int seed)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static void Month(SimCore sim)
        {
            sim.Step();
            if (sim.State.eraReady) sim.AdvanceEra();
        }

        static SimCore SimIn(Era era, int seed)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++) Month(sim);
            Assert.AreEqual(era, sim.State.era, $"seed {seed} reaches {era}");
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        /// <summary>A box in <paramref name="era"/> at a belief stage and half (the relic of the later half given).</summary>
        static SimCore Believing(Era era, int seed, int stage, bool later)
        {
            var sim = era == Era.Camp ? NewSim(seed) : SimIn(era, seed);
            if (era == Era.Camp) for (int m = 0; m < 24; m++) sim.Step();
            if (stage == 3) EnsureScholar(sim);
            sim.DebugSetBelief(stage, later);
            if (later && stage == 1) sim.DebugGiveRelic(RelicId.SkyCrumb);
            if (later && stage == 2) sim.DebugGiveRelic(RelicId.KeeperDoll);
            if (later && stage == 3) sim.DebugGiveRelic(RelicId.Lens);
            Assert.AreEqual(stage, sim.BeliefStage);
            Assert.AreEqual(later ? BeliefHalf.Later : BeliefHalf.Early, sim.BeliefHalfNow);
            return sim;
        }

        /// <summary>A town with no clever, tinkerer or dreamer gets one (the scholars, Choice 5), so the scholars' parts play.</summary>
        static void EnsureScholar(SimCore sim)
        {
            if (sim.Scholars.Count > 0) return;
            var n = sim.State.notables.Where(x => x.alive && x.age >= SimCore.AdultAge && x.age < 55f).OrderBy(x => x.id).FirstOrDefault();
            if (n != null) n.trait = "the clever";
        }

        static List<PersonActivity> GlassVisitsOver(LifePlanner planner, long day0, int days)
        {
            var all = new List<PersonActivity>();
            var one = new List<PersonActivity>();
            for (long d = day0; d < day0 + days; d++) { planner.GlassVisits(d, one); all.AddRange(one); }
            return all;
        }

        /// <summary>The day as planned (after Stitch): who actually reaches the glass, and the most there at once.</summary>
        static (int reached, int most, HashSet<ActivityKind> kinds) AtTheGlass(LifePlanner planner, long day0, int days)
        {
            var acts = new List<PersonActivity>();
            int reached = 0, most = 0;
            var kinds = new HashSet<ActivityKind>();
            var seen = new HashSet<(long, int)>();
            for (long d = day0; d < day0 + days; d++)
                for (float h = 0.05f; h < 24f; h += 0.1f)
                {
                    planner.PlanAll(new ViewTime(d, h), acts);
                    int here = 0;
                    foreach (var a in acts)
                    {
                        if (a.moving || a.place.kind != PlaceKind.Glass) continue;
                        here++;
                        kinds.Add(a.kind);
                        if (seen.Add((d, a.who.Key))) reached++;
                    }
                    most = Math.Max(most, here);
                }
            return (reached, most, kinds);
        }

        // ------------------------------------------------------------------ the day at the glass, per stage (§3 "Gathering")

        [TestCase(Era.Camp, 1, 1, false)]
        [TestCase(Era.Camp, 1, 1, true)]
        [TestCase(Era.Village, 7, 2, false)]
        [TestCase(Era.Village, 7, 2, true)]
        [TestCase(Era.Town, 1234, 3, false)]
        [TestCase(Era.Town, 1234, 3, true)]
        public void ThePeopleAtTheGlassFollowTheirBelief(Era era, int seed, int stage, bool later)
        {
            var sim = Believing(era, seed, stage, later);
            var planner = LifePlanner.Wired(sim);
            long day0 = sim.State.month;
            const int Days = 24;
            var visits = GlassVisitsOver(planner, day0, Days);
            var at = AtTheGlass(planner, day0, Days);
            string summary = $"{era} stage {stage}{(later ? " later" : " early")}: {visits.Count} visits planned " +
                             $"({string.Join(", ", visits.GroupBy(v => v.kind + "@" + v.place.kind).Select(g => $"{g.Key} x{g.Count()}"))}); " +
                             $"{at.reached} reached the glass, at most {at.most} at once ({string.Join(", ", at.kinds)})";
            TestContext.WriteLine(summary);
            Assert.LessOrEqual(at.most, LifePlanner.MaxAtGlass, "never more than ~6 at the glass, so work goes on");
            Assert.AreEqual(0, planner.FallbackCount, "every glass visit is at a real place");
            if (stage == 1 && !later)
            {
                Assert.IsEmpty(visits, "stage 1 early: they only come after a wipe");
                return;
            }
            Assert.IsNotEmpty(visits, summary);
            Assert.Greater(at.reached, 0, "some of them get there (a stay too far to walk to is skipped)");
            switch (stage)
            {
                case 1:
                    Assert.IsTrue(visits.All(v => v.kind == ActivityKind.GazeWary && v.facesGlass), "stage 1: a step back, looking up");
                    break;
                case 2:
                    Assert.IsTrue(visits.Any(v => v.kind == ActivityKind.PressHands && planner.StageOf(v.who) == LifeStage.Kid), "kids press their hands to the window");
                    Assert.IsTrue(visits.Any(v => v.kind == ActivityKind.SitOut && v.facesGlass && v.start >= 18f), "a few adults sit facing it at dusk");
                    if (later) Assert.IsTrue(visits.Any(v => v.kind == ActivityKind.Wave && v.place == LifePlanner.RelicPlace(RelicId.KeeperDoll)), "kids wave at the doll first");
                    else Assert.IsFalse(visits.Any(v => v.place.kind == PlaceKind.Relic), "no doll yet");
                    break;
                case 3:
                    Assume.That(sim.Scholars.Count, Is.GreaterThan(0), "the town has a scholar");
                    if (later)
                    {
                        Assert.IsTrue(visits.Any(v => v.kind == ActivityKind.Measure && v.prop == Prop.MeasuringRod && v.start < 12f), "a scholar measures along the glass most mornings");
                        Assert.IsTrue(visits.Any(v => v.kind == ActivityKind.LookThroughLens && v.place == LifePlanner.RelicPlace(RelicId.Lens)), "the lens's scholar looks through it some evenings");
                    }
                    else
                    {
                        Assert.IsTrue(visits.Any(v => v.kind == ActivityKind.Argue || v.kind == ActivityKind.Study), "the clever argue about the wall");
                        Assert.IsFalse(visits.Any(v => v.kind == ActivityKind.Measure), "no measuring before the Lens");
                    }
                    break;
            }
        }

        [Test]
        public void GlassVisitsAreDeterministicAndNeverTouchTheSim()
        {
            var a = Believing(Era.Village, 7, 2, true);
            var b = Believing(Era.Village, 7, 2, true);
            string before = JsonUtility.ToJson(a.State);
            var pa = LifePlanner.Wired(a);
            string Dump(LifePlanner p) => string.Join(";", GlassVisitsOver(p, a.State.month, 12).Select(v => $"{v.who.Key}:{v.kind}@{v.place}/{v.start:0.00}"));
            string first = Dump(pa);
            AtTheGlass(pa, a.State.month, 3);
            pa.GatherAtGlass(new ViewTime(a.State.month, 10f), true);
            pa.StartRelicMoment(RelicId.KeeperDoll, new ViewTime(a.State.month, 10f));
            AtTheGlass(pa, a.State.month, 1);
            Assert.AreEqual(before, JsonUtility.ToJson(a.State), "the glass, the gatherings and the moments never write the sim");
            Assert.AreEqual(first, Dump(LifePlanner.Wired(b)), "same seed, same visits");
            for (int m = 0; m < 12; m++) { Month(a); Month(b); }
            Assert.AreEqual(JsonUtility.ToJson(b.State), JsonUtility.ToJson(a.State), "a box whose people went to the glass lives like one whose didn't (no sim dice)");
        }

        // ------------------------------------------------------------------ the front glass

        [Test]
        public void TheGlassIsJustInsideTheWallTheCameraLooksThrough()
        {
            Assert.AreEqual(0, LifePlanner.GlassSideFacing(28f), "BoxCamera's default yaw looks in through the -z wall");
            Assert.AreEqual(0, LifePlanner.GlassSideFacing(0f));
            Assert.AreEqual(2, LifePlanner.GlassSideFacing(180f));
            Assert.AreEqual(3, LifePlanner.GlassSideFacing(90f));
            Assert.AreEqual(1, LifePlanner.GlassSideFacing(-90f));
            var sim = SimIn(Era.Village, 7);
            var planner = LifePlanner.Wired(sim);
            for (int side = 0; side < 4; side++)
            {
                planner.GlassSide = side;
                Assert.IsTrue(planner.Positions.TryGetPosition(planner.GlassPlace, out float x, out float z), $"side {side} has a spot");
                float dWall = side switch { 0 => z + 2.2f, 2 => 2.2f - z, 1 => 3f - x, _ => x + 3f };
                TestContext.WriteLine($"side {side}: ({x:0.00}, {z:0.00}), {dWall:0.00} from the wall");
                Assert.That(dWall, Is.InRange(0f, 0.6f), $"side {side}: just inside the wall");
                Assert.IsTrue(planner.RouteOf(Place.Land(PlaceKind.Hill), planner.GlassPlace, new List<Waypoint>()), "the glass can be walked to");
            }
            planner.GlassSide = 0;
            Assert.AreEqual(Place.Land(PlaceKind.Glass, 0), planner.GlassPlace);
        }

        // ------------------------------------------------------------------ gatherings and reactions by stage

        [Test]
        public void AWipeInTheCampBringsAFewToTheGlass_OnceAViewDay()
        {
            var sim = Believing(Era.Camp, 1, 1, false);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            while (planner.IsRestDay(day)) day++;
            var t = new ViewTime(day, 10f);
            planner.React(KeeperGesture.Wipe, t);
            var acts = new List<PersonActivity>();
            planner.PlanAll(new ViewTime(day, 10.9f), acts);
            var there = acts.Where(a => a.place.kind == PlaceKind.Glass && !a.moving).ToList();
            TestContext.WriteLine($"{there.Count} at the glass: {string.Join(", ", there.Select(a => $"{a.who} {a.kind}"))}");
            Assert.That(there.Count, Is.InRange(1, LifePlanner.MaxAtGlass), "a few come, never a crowd");
            Assert.IsTrue(there.All(a => a.kind == ActivityKind.GazeWary && a.facesGlass), "a step back, looking up");
            Assert.AreEqual(0, planner.GatherAtGlass(new ViewTime(day, 14f)), "at most once a view day");
            Assert.Greater(planner.GatherAtGlass(new ViewTime(day, 14f), true), 0, "the debug panel can force one");
        }

        [Test]
        public void AReturnInTheVillageSendsTheKidsToLookForYou()
        {
            var sim = Believing(Era.Village, 7, 2, false);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            planner.React(KeeperGesture.Return, new ViewTime(day, 15f));
            var acts = new List<PersonActivity>();
            planner.PlanAll(new ViewTime(day, 15.6f), acts);
            var there = acts.Where(a => a.place.kind == PlaceKind.Glass && !a.moving).ToList();
            TestContext.WriteLine(string.Join(", ", there.Select(a => $"{a.who} {a.kind}")));
            Assert.IsNotEmpty(there, "kids run to the window");
            Assert.IsTrue(there.All(a => planner.StageOf(a.who) == LifeStage.Kid && a.kind == ActivityKind.PressHands), "only the kids, hands on the glass");
            Assert.LessOrEqual(there.Count, 3);
        }

        [Test]
        public void ATapLooksLikeTheStage()
        {
            // Stage 1: look up, flinch, then peek.
            var camp = Believing(Era.Camp, 1, 1, false);
            var p1 = new LifePlanner(camp);
            long day = camp.State.month;
            var t = new ViewTime(day, 10f);
            p1.React(KeeperGesture.TapGlass, t);
            var seq = new List<ActivityKind>();
            var acts = new List<PersonActivity>();
            for (float f = 0.05f; f < 1f; f += 0.1f)
            {
                p1.PlanAll(new ViewTime(day, 10f + LifeClock.ViewHoursFor(10f, 1.5f) * f), acts);
                var r = acts.FirstOrDefault(a => a.source == ActivitySource.Reaction);
                if (r.kind != ActivityKind.None && (seq.Count == 0 || seq[seq.Count - 1] != r.kind)) seq.Add(r.kind);
            }
            TestContext.WriteLine("camp tap: " + string.Join(" > ", seq));
            CollectionAssert.AreEqual(new[] { ActivityKind.LookUpWary, ActivityKind.Flinch, ActivityKind.Peek }, seq, "look up, a flinch and a step back, then a peek");

            // Stage 2: the kids laugh and point.
            var village = Believing(Era.Village, 7, 2, false);
            var p2 = new LifePlanner(village);
            day = village.State.month;
            p2.React(KeeperGesture.TapGlass, new ViewTime(day, 10f));
            p2.PlanAll(new ViewTime(day, 10f + LifeClock.ViewHoursFor(10f, 1.2f)), acts);
            var reacting = acts.Where(a => a.source == ActivitySource.Reaction).ToList();
            Assume.That(reacting.Any(a => p2.StageOf(a.who) == LifeStage.Kid), "a kid reacts");
            Assert.IsTrue(reacting.Where(a => p2.StageOf(a.who) == LifeStage.Kid).All(a => a.kind == ActivityKind.LaughPoint), "kids laugh and point");
            Assert.IsTrue(reacting.Where(a => p2.StageOf(a.who) != LifeStage.Kid).All(a => a.kind == ActivityKind.LookUp), "the grown-ups look up");
        }

        [Test]
        public void AFingerInTheTownIsMeasuredOnceTheLensIsUp()
        {
            foreach (bool later in new[] { false, true })
            {
                var sim = Believing(Era.Town, 1234, 3, later);
                Assume.That(sim.Scholars.Count, Is.GreaterThan(0));
                var planner = new LifePlanner(sim);
                long day = sim.State.month;
                // The finger held over where the first scholar is.
                var where = planner.Plan(PersonRef.Notable(sim.Scholars[0].id), new ViewTime(day, 10f)).place;
                planner.React(KeeperGesture.HoldFinger, new ViewTime(day, 10f), where);
                var acts = new List<PersonActivity>();
                planner.PlanAll(new ViewTime(day, 10.1f), acts);
                var scholarIds = new HashSet<int>(sim.Scholars.Select(n => n.id));
                var theirs = acts.Where(a => a.source == ActivitySource.Reaction && scholarIds.Contains(a.who.notableId)).ToList();
                Assume.That(theirs, Is.Not.Empty, "a scholar is in reach of the finger");
                var want = later ? ActivityKind.Measure : ActivityKind.Study;
                Assert.IsTrue(theirs.All(a => a.kind == want), $"later {later}: the scholars {want}");
                Assert.IsFalse(acts.Any(a => a.source == ActivitySource.Reaction && !scholarIds.Contains(a.who.notableId) && (a.kind == ActivityKind.Measure || a.kind == ActivityKind.Study)), "only scholars study");
            }
        }

        // ------------------------------------------------------------------ the tap-back (Choice 9)

        [TestCase(1234)]
        [TestCase(7)]
        public void AScholarTapsBackTheSameNumberOfTimes(int seed)
        {
            var sim = Believing(Era.Town, seed, 3, true);
            Assume.That(sim.Scholars.Count, Is.GreaterThan(0));
            Assert.AreEqual(-1, Believing(Era.Town, seed, 3, false).KeeperTapped(3), "no tap-back before the Lens");
            int scholar = sim.KeeperTapped(3);
            Assert.GreaterOrEqual(scholar, 0, "the sim names a scholar");
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            var at = new ViewTime(day, 9.5f);
            Assert.IsTrue(planner.TapBack(at, scholar, 3));
            var who = PersonRef.Notable(scholar);
            var seen = new List<ActivityKind>();
            PersonActivity knock = default;
            for (float h = at.hour; h < at.hour + 3f; h += 0.01f)
            {
                var a = planner.Plan(who, new ViewTime(day, h));
                if (seen.Count == 0 || seen[seen.Count - 1] != a.kind) seen.Add(a.kind);
                if (a.kind == ActivityKind.TapBack) knock = a;
            }
            TestContext.WriteLine($"seed {seed}: {string.Join(" > ", seen)}");
            Assert.AreEqual(ActivityKind.TapBack, knock.kind, "the scholar knocks back");
            Assert.AreEqual(3, knock.count, "the same number of times");
            Assert.AreEqual(PlaceKind.Glass, knock.place.kind, "on the glass");
            Assert.IsTrue(knock.facesGlass);
            Assert.Less(seen.IndexOf(ActivityKind.TapBack), seen.IndexOf(ActivityKind.WriteDown), "then writes it on a slate");
            // They are at the glass's spot while knocking.
            Assert.IsTrue(planner.Positions.TryGetPosition(planner.GlassPlace, out float gx, out float gz));
            Assert.IsTrue(planner.PositionAt(who, new ViewTime(day, (knock.start + knock.end) * 0.5f), out float x, out float z));
            Assert.Less(Math.Sqrt((x - gx) * (x - gx) + (z - gz) * (z - gz)), 0.05, "knocking at the glass, not somewhere else");
            Assert.IsFalse(planner.TapBack(at, 999999, 3), "nobody to tap back");
        }

        // ------------------------------------------------------------------ the relic moments (§1)

        static readonly (string relic, Era era, int stage)[] Moments =
        {
            (RelicId.SkyCrumb, Era.Camp, 1), (RelicId.FestivalMask, Era.Village, 2), (RelicId.KeeperDoll, Era.Village, 2),
            (RelicId.LeftSandal, Era.Village, 2), (RelicId.Lens, Era.Town, 3),
        };

        static SimCore WithRelic(string relic, Era era, int seed)
        {
            var sim = era == Era.Camp ? NewSim(seed) : SimIn(era, seed);
            if (era == Era.Camp) for (int m = 0; m < 24; m++) sim.Step();
            if (era == Era.Town) EnsureScholar(sim);
            Assert.IsNotNull(sim.DebugGiveRelic(relic), $"{relic} given");
            Assert.IsTrue(sim.HasRelic(relic));
            return sim;
        }

        static readonly Dictionary<string, (ActivityKind kind, RelicCast role)> Signature = new Dictionary<string, (ActivityKind, RelicCast)>
        {
            [RelicId.SkyCrumb] = (ActivityKind.Taste, RelicCast.Owner),
            [RelicId.FestivalMask] = (ActivityKind.Dance, RelicCast.Owner),
            [RelicId.KeeperDoll] = (ActivityKind.SetUp, RelicCast.Owner),
            [RelicId.LeftSandal] = (ActivityKind.HoldUp, RelicCast.Second),
            [RelicId.Lens] = (ActivityKind.LookThroughLens, RelicCast.Queue),
        };

        [TestCase(RelicId.SkyCrumb)]
        [TestCase(RelicId.FestivalMask)]
        [TestCase(RelicId.KeeperDoll)]
        [TestCase(RelicId.LeftSandal)]
        [TestCase(RelicId.Lens)]
        public void EachRelicMomentPlays(string relic)
        {
            var m = Moments.First(x => x.relic == relic);
            var sim = WithRelic(relic, m.era, m.era == Era.Town ? 1234 : m.era == Era.Camp ? 1 : 7);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            while (planner.IsRestDay(day)) day++;
            var def = RelicMoments.For(relic);
            var at = new ViewTime(day, def.preferredHour >= 0f ? def.preferredHour : 10f);
            var info = planner.StartRelicMoment(relic, at);
            Assert.IsNotNull(info, $"{relic}: the moment plays");
            TestContext.WriteLine($"{relic} {at} to {info.end:0.00}h: {string.Join(", ", info.cast.Select(c => $"{c.who} {c.role}"))}");
            var r = sim.FindRelic(relic);
            if (relic != RelicId.LeftSandal && Alive(sim, r.ownerId) && !def.standIns)
                Assert.IsTrue(info.cast.Any(c => c.who.notableId == r.ownerId && c.role == RelicCast.Owner), "the relic's owner plays their part");
            if (relic == RelicId.LeftSandal)
                Assert.IsTrue(info.cast.Any(c => c.who.notableId == r.holderId && (c.role == RelicCast.Holder || c.role == RelicCast.Heir)), "the sandal goes back to its owner or heir");
            var (sigKind, sigRole) = Signature[relic];
            Assert.IsTrue(info.cast.Any(c => c.role == sigRole), $"someone plays the {sigRole}");
            Assert.LessOrEqual(info.cast.Count, def.maxKids + def.maxGrown + def.maxQueue + 3);

            var acts = new List<PersonActivity>();
            var kinds = new HashSet<ActivityKind>();
            var lines = new List<RelicLine>();
            var said = new HashSet<int>();
            var moments = new List<RelicMomentInfo>();
            int sig = 0;
            for (float h = at.hour; h < info.end; h += 0.01f)
            {
                var t = new ViewTime(day, h);
                planner.PlanAll(t, acts);
                foreach (var a in acts)
                {
                    Assert.IsTrue(planner.IsValidPlace(a.place) && planner.IsValidPlace(a.from), $"{a.who} {a.kind} at a real place");
                    if (a.source != ActivitySource.Scene) continue;
                    kinds.Add(a.kind);
                    if (a.kind == sigKind) sig++;
                }
                planner.RelicLinesAt(t, lines);
                foreach (var l in lines) { said.Add(l.lineNo); Assert.IsFalse(l.text.Contains("{0}"), "the name is filled in"); }
                Assert.AreEqual(1, planner.ActiveRelicMoments(t, moments));
            }
            TestContext.WriteLine($"  kinds: {string.Join(", ", kinds)}; lines {string.Join(", ", said)}");
            Assert.Greater(sig, 0, $"{relic}: someone {sigKind}");
            Assert.IsNotEmpty(said, "a line from the bank is said");
            Assert.AreEqual(0, planner.FallbackCount);
            planner.PlanAll(new ViewTime(day, info.end + 0.01f), acts);
            Assert.AreEqual(0, planner.ActiveRelicMoments(new ViewTime(day, info.end + 0.01f), moments), "then it's over");
            switch (relic)
            {
                case RelicId.SkyCrumb:
                    Assert.IsTrue(kinds.Contains(ActivityKind.Freeze) && kinds.Contains(ActivityKind.Cheer), "freeze at the thud, then a cheer");
                    break;
                case RelicId.FestivalMask:
                    Assert.IsTrue(kinds.Contains(ActivityKind.Speech), "the leader speaks first");
                    break;
                case RelicId.Lens:
                    Assert.IsTrue(kinds.Contains(ActivityKind.SetUp) && kinds.Contains(ActivityKind.Queue), "set up, then a queue");
                    break;
            }
        }

        static string Show(PersonActivity a) => $"{a.kind} {a.from}->{a.place} {a.start:0.000}-{a.end:0.000} {a.source}";

        static bool Alive(SimCore sim, int id) => sim.State.notables.Any(n => n.id == id && n.alive);

        [Test]
        public void TheLensQueueTakesTurns()
        {
            var sim = WithRelic(RelicId.Lens, Era.Town, 1234);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            var info = planner.StartRelicMoment(RelicId.Lens, new ViewTime(day, 18.4f));
            Assert.IsNotNull(info);
            var queue = info.cast.Where(c => c.role == RelicCast.Queue).Select(c => c.who).ToList();
            Assume.That(queue.Count, Is.GreaterThanOrEqualTo(2), "a queue forms");
            var acts = new List<PersonActivity>();
            int most = 0;
            var looked = new HashSet<int>();
            for (float h = 18.4f; h < info.end; h += 0.005f)
            {
                planner.PlanAll(new ViewTime(day, h), acts);
                int looking = acts.Count(a => a.kind == ActivityKind.LookThroughLens && queue.Contains(a.who));
                most = Math.Max(most, looking);
                foreach (var a in acts) if (a.kind == ActivityKind.LookThroughLens && queue.Contains(a.who)) looked.Add(a.who.Key);
            }
            Assert.AreEqual(1, most, "one at a time");
            Assert.AreEqual(queue.Count, looked.Count, "everyone in the queue gets a look");
        }

        [Test]
        public void AShowMeOfARelicCardEchoesItsOwnersGesture()
        {
            var sim = WithRelic(RelicId.SkyCrumb, Era.Camp, 1);
            var r = sim.FindRelic(RelicId.SkyCrumb);
            var e = sim.State.chronicle.First(x => x.id == r.entryId);
            var planner = LifePlanner.Wired(sim);
            var at = new ViewTime(sim.State.month, 11f);
            Assert.IsTrue(planner.Echo(e, at));
            var a = planner.Plan(PersonRef.Notable(r.ownerId), new ViewTime(at.day, 11f + LifeClock.ViewHoursFor(11f, 3f)));
            Assert.AreEqual(ActivityKind.Taste, a.kind, "the taster tastes again");
            Assert.AreEqual(Prop.Crumb, a.prop);
        }

        /// <summary>Every walk in a relic moment, a gathering or a tap-back keeps the walking pace (≤ 2x, as the day's walks,
        /// #30/#109): sampled from where the planner has each person, frame by frame.</summary>
        [Test]
        public void LiveGlassAndRelicWalksKeepThePace()
        {
            float worst = 0f;
            string worstWhat = "";
            int walked = 0, moments = 0;
            foreach (int seed in new[] { 1, 2, 3, 7, 1234 })
                foreach (var m in Moments)
                {
                    SimCore sim;
                    try { sim = WithRelic(m.relic, m.era, seed); }
                    catch (AssertionException) { continue; } // this seed doesn't reach the era
                    var planner = LifePlanner.Wired(sim);
                    long day = sim.State.month;
                    foreach (float hour in new[] { 6f, 10f, 15f, 18.4f, 19.6f })
                    {
                        planner.ClearLive();
                        var at = new ViewTime(day, hour);
                        var info = planner.StartRelicMoment(m.relic, at);
                        if (info == null) continue;
                        moments++;
                        float end = info.end;
                        if (m.stage == 3) { int s = sim.Scholars.FirstOrDefault()?.id ?? -1; if (s >= 0 && planner.TapBack(at, s, 2)) end = Math.Max(end, hour + 0.5f); }
                        planner.GatherAtGlass(at, true);
                        var who = planner.People.ToList();
                        // The plain day's own walks are the day sweeps' (NoWalkRushes_*; a cross-box walk capped at MaxWalk
                        // is brisk by design there): only the live walks are judged here, into, within and out of the moments.
                        var plain = new Dictionary<int, HashSet<(float, float)>>();
                        var daySegs = new List<PersonActivity>();
                        foreach (var p in who)
                        {
                            planner.DaySchedule(p, day, daySegs);
                            plain[p.Key] = new HashSet<(float, float)>(daySegs.Select(a => (a.start, a.end)));
                        }
                        var last = new Dictionary<int, (float x, float z, float h)>();
                        for (float h = hour; h < Math.Min(24f, end + 0.02f); h += 0.004f)
                            foreach (var p in who)
                            {
                                if (!planner.PositionAt(p, new ViewTime(day, h), out float x, out float z)) continue;
                                if (last.TryGetValue(p.Key, out var l))
                                {
                                    float d = (float)Math.Sqrt((x - l.x) * (x - l.x) + (z - l.z) * (z - l.z));
                                    float secs = LifeClock.RealSecondsBetween(l.h, h);
                                    var now = planner.Plan(p, new ViewTime(day, h));
                                    if (d > 1e-4f && secs > 0f && !plain[p.Key].Contains((now.start, now.end)))
                                    {
                                        walked++;
                                        float pace = d / secs / LifePlanner.RealWalkSpeed;
                                        if (pace > worst) { worst = pace; worstWhat = $"{m.relic} seed {seed} {hour}h {p} at {h:0.000}h: {Show(planner.Plan(p, new ViewTime(day, h)))}"; }
                                    }
                                }
                                last[p.Key] = (x, z, h);
                            }
                    }
                }
            TestContext.WriteLine($"{moments} moments, {walked} steps sampled; fastest {worst:0.00}x the walking pace ({worstWhat})");
            Assert.Greater(moments, 10);
            Assert.LessOrEqual(worst, 2f, worstWhat);
        }

        /// <summary>The walk back into the day after a relic moment or an echo keeps the pace too, sampled until everyone has
        /// rejoined their day, from early to late-evening starts (view hours run faster after 20h).</summary>
        [Test]
        public void MomentTailsKeepThePace_RelicMomentsAndEchoes()
        {
            float worst = 0f;
            string worstWhat = "";
            int moments = 0, echoes = 0;
            foreach (int seed in new[] { 1, 2, 3, 7, 1234 })
                foreach (var m in Moments)
                {
                    SimCore sim;
                    try { sim = WithRelic(m.relic, m.era, seed); }
                    catch (AssertionException) { continue; }
                    var planner = LifePlanner.Wired(sim);
                    long day = sim.State.month;
                    var entry = sim.State.chronicle.FirstOrDefault(x => x.kind == "relic" && x.relicId == m.relic);
                    foreach (float hour in new[] { 10f, 18.4f, 19.6f, 20.5f, 21.5f, 22.2f, 22.7f, 23.2f })
                    {
                        planner.ClearLive();
                        var info = planner.StartRelicMoment(m.relic, new ViewTime(day, hour));
                        if (info != null)
                        {
                            moments++;
                            var r = MomentTailPace.Worst(planner, day, hour, info.end, $"{m.relic} seed {seed}");
                            if (r.worst > worst) { worst = r.worst; worstWhat = r.what; }
                        }
                        planner.ClearLive();
                        var echo = entry == null ? null : EchoScenes.For(sim, entry);
                        var einfo = echo == null ? null : planner.StageEcho(echo, new ViewTime(day, hour));
                        if (einfo != null)
                        {
                            echoes++;
                            var r = MomentTailPace.Worst(planner, day, hour, einfo.end, $"echo {m.relic} seed {seed}");
                            if (r.worst > worst) { worst = r.worst; worstWhat = r.what; }
                        }
                    }
                }
            TestContext.WriteLine($"{moments} moments, {echoes} echoes; fastest {worst:0.00}x the walking pace ({worstWhat})");
            Assert.Greater(moments, 10);
            Assert.Greater(echoes, 5);
            Assert.LessOrEqual(worst, 2f, worstWhat);
        }

        // ------------------------------------------------------------------ the line bank in the moments

        [Test]
        public void TheCampSaysSkyNotWindowWhenTheSandalIsFound()
        {
            var sim = WithRelic(RelicId.LeftSandal, Era.Camp, 1);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            var info = planner.StartRelicMoment(RelicId.LeftSandal, new ViewTime(day, 10f));
            Assume.That(info, Is.Not.Null);
            var lines = new List<RelicLine>();
            var all = new List<string>();
            for (float h = 10f; h < info.end; h += 0.01f) { planner.RelicLinesAt(new ViewTime(day, h), lines); all.AddRange(lines.Select(l => l.text)); }
            Assume.That(all, Is.Not.Empty);
            Assert.IsTrue(all.All(t => !t.Contains("window")), "the camp has no window: " + string.Join(" | ", all.Distinct()));
        }
    }
}
