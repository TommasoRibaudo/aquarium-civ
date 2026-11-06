using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>
    /// Tester run 1 #3 (visit loop): in a living-land Camp of 8 grown-ups, all 8 chatted at the fire at 09:30 and 16:15, all ate at
    /// 12:40, or 6-7 sat out there, and most daylight samples had no story in progress (research 17 principles 6 and 7). The camp's
    /// day is now spread: chats in pairs and trios at different places and times, meals in shifts with jobs that keep going, short
    /// partial sit-outs; the evening fire stays the one time most gather. Measured over whole days (rest days included), every
    /// quarter view hour of daylight.
    /// </summary>
    public class CampRhythmTests
    {
        static readonly (float dx, float dz)[] Spots = { (0.9f, 0f), (-1.2f, 0.8f), (1.6f, -0.9f), (-0.4f, -1.3f), (2.2f, 0.6f), (0.2f, 1.4f), (-2f, -0.3f), (1f, 1.2f) };

        /// <summary>A living-land camp a few years on (bushes, water, rabbits, the camp's jobs open), with <paramref name="population"/>
        /// people (the crowd fills up past the notables).</summary>
        internal static (SimCore sim, LifePlanner planner) Camp(int population, int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            sim.DebugOpenCampGate();
            for (int i = 0; i < 12; i++) { var s = Spots[i % Spots.Length]; sim.DebugMistBig(sim.EcoGround.campX + s.dx, sim.EcoGround.campZ + s.dz); sim.Advance(1.0 / 12); }
            foreach (var key in new[] { EcoMilestones.FirstBush, EcoMilestones.WaterSurfaced, EcoMilestones.FirstWoodTree })
                if (!sim.State.eco.milestoneKeys.Contains(key)) { sim.State.eco.milestoneKeys.Add(key); sim.State.eco.milestoneMonths.Add(sim.State.month); }
            sim.DebugAddRabbits(8);
            sim.DebugRedrawJobs();
            for (int m = 0; m < 24; m++) sim.Step();
            Assert.AreEqual(Era.Camp, sim.State.era, "still a camp");
            int alive = sim.State.notables.Count(n => n.alive);
            sim.State.population = Math.Max(population, alive);
            var field = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(field);
            field.Sync(sim.EcoRabbitsShown());
            var planner = LifePlanner.Wired(sim);
            planner.Rabbits = field;
            return (sim, planner);
        }

        internal struct Rhythm
        {
            public int samples, inStep, withStory, days, people;
            /// <summary>The mean number at the fire for each view hour 7..17 (index 0 = 07:00-08:00).</summary>
            public float[] fireByHour;
            /// <summary>The most at the fire doing one same thing at one sample in daylight.</summary>
            public int worstSame;
            /// <summary>The most at the fire doing one same thing at one daylight sample of a rest day (the tester's day: 8 of 8).</summary>
            public int restWorstAtFire;
            /// <summary>The mean at the fire 18:00-19:00: the evening fire is still the one time most gather.</summary>
            public float eveningFire;
            public int InStepPct => samples == 0 ? 0 : inStep * 100 / samples;
            public int StoryPct => samples == 0 ? 0 : withStory * 100 / samples;
            public override string ToString() =>
                $"pop {people}: {samples} samples over {days} days; >=3 in step {InStepPct}% (worst {worstSame} together); a story {StoryPct}%; at the fire by hour 7-17: " +
                string.Join(" ", fireByHour.Select(f => f.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))) +
                $"; rest day worst at the fire {restWorstAtFire}; evening fire {eveningFire:0.0}";
        }

        internal static Dictionary<string, int> Kinds;
        /// <summary>Seen and judged: not asleep or indoors, and not a baby's toddle (a baby is drawn by its carer, and the births are
        /// another lane's).</summary>
        static bool Visible(in PersonActivity a) => a.kind != ActivityKind.Sleep && a.kind != ActivityKind.Indoors && a.kind != ActivityKind.Toddle;

        /// <summary>Every quarter view hour of daylight (07:00-17:30) over <paramref name="days"/> days.</summary>
        internal static Rhythm Measure(SimCore sim, LifePlanner planner, int days)
        {
            var r = new Rhythm { fireByHour = new float[11], people = planner.People.Count };
            var n = new int[11];
            var acts = new List<PersonActivity>();
            var pos = new Dictionary<int, (float x, float z, bool ok)>();
            long day0 = sim.State.month;
            int evenings = 0;
            for (long day = day0; day < day0 + days; day++)
            {
                r.days++;
                bool rest = planner.IsRestDay(day);
                for (float h = 18f; h < 19f; h += 0.25f)
                {
                    planner.PlanAll(new ViewTime(day, h), acts);
                    r.eveningFire += acts.Count(a => !a.moving && (a.place.kind == PlaceKind.Fire || a.place.kind == PlaceKind.Stash));
                    evenings++;
                }
                for (float h = 7f; h < 17.5f; h += 0.25f)
                {
                    var t = new ViewTime(day, h);
                    planner.PlanAll(t, acts);
                    pos.Clear();
                    foreach (var a in acts) { bool ok = planner.PositionAt(a.who, t, out float x, out float z); pos[a.who.Key] = (x, z, ok); }
                    r.samples++;
                    // Principle 6: 3+ within 0.6 u doing the same still thing (the planner's spot is the place's; the view spreads them on slots round it).
                    bool step = false;
                    for (int i = 0; i < acts.Count; i++)
                    {
                        var a = acts[i];
                        if (a.moving || !Visible(a)) continue;
                        var pa = pos[a.who.Key];
                        if (!pa.ok) continue;
                        int same = 0;
                        foreach (var b in acts)
                        {
                            if (b.moving || b.kind != a.kind || !Visible(b)) continue;
                            var pb = pos[b.who.Key];
                            if (!pb.ok) continue;
                            if ((pa.x - pb.x) * (pa.x - pb.x) + (pa.z - pb.z) * (pa.z - pb.z) <= 0.36f) same++;
                        }
                        if (same >= 3) { step = true; if (Kinds != null) { Kinds.TryGetValue(a.kind + "@" + a.place.kind, out int kc); Kinds[a.kind + "@" + a.place.kind] = kc + 1; } }
                        r.worstSame = Math.Max(r.worstSame, same);
                    }
                    if (step) r.inStep++;
                    if (rest)
                        foreach (var g in acts.Where(a => !a.moving && Visible(a) && a.place.kind == PlaceKind.Fire).GroupBy(a => a.kind))
                            r.restWorstAtFire = Math.Max(r.restWorstAtFire, g.Count());
                    // Principle 7: at least one story in progress (the planner's half of B11's "counts").
                    if (acts.Any(a => StoryRules.StoryOf(a, out _) != StoryKind.None)) r.withStory++;
                    int hi = (int)h - 7;
                    if (hi >= 0 && hi < 11)
                    {
                        n[hi]++;
                        r.fireByHour[hi] += acts.Count(a => !a.moving && (a.place.kind == PlaceKind.Fire || a.place.kind == PlaceKind.Stash));
                    }
                }
            }
            for (int i = 0; i < 11; i++) if (n[i] > 0) r.fireByHour[i] /= n[i];
            if (evenings > 0) r.eveningFire /= evenings;
            return r;
        }

        // Before (fcaf61a, this harness, 12 days from seed 7): see the report in the branch's commit; the camp of 8 had all 8 at the
        // fire eating or chatting on its rest day. Thresholds [est]: a camp of 6-8 is calm (almost never 3 in step), 12 a little
        // busier, 20 (a camp about to be a village, the crowd's helpers at a few tents) busier still.
        [TestCase(6, 5, 70, 4)]
        [TestCase(8, 6, 70, 4)]
        [TestCase(12, 20, 70, 5)]
        [TestCase(20, 50, 70, 7)]
        public void TheCampsDayIsSpread_StoriesOnScreen_AndTheEveningStillGathers(int population, int maxInStepPct, int minStoryPct, int maxRestAtFire)
        {
            var (sim, planner) = Camp(population);
            Kinds = new Dictionary<string, int>();
            var m = Measure(sim, planner, 12);
            TestContext.WriteLine("RHYTHM " + m);
            TestContext.WriteLine("  in step: " + string.Join(", ", Kinds.OrderByDescending(k => k.Value).Take(12).Select(k => k.Key + " " + k.Value)));
            Kinds = null;
            Assert.LessOrEqual(m.InStepPct, maxInStepPct, "principle 6: daylight samples with 3+ neighbours doing the same thing");
            Assert.GreaterOrEqual(m.StoryPct, minStoryPct, "principle 7: daylight samples with a story in progress");
            Assert.LessOrEqual(m.restWorstAtFire, maxRestAtFire, "a rest day: never the whole camp at the fire doing one thing");
            float dayMax = m.fireByHour.Max();
            Assert.Greater(m.eveningFire, dayMax, "the evening fire is still the one time most gather");
        }
    }
}
