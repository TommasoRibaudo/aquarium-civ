// #710 fewer animals for the box: count every kind of animal a normal player's box holds (the pacing lab's Active Keeper on the
// game's real sim, every jar placed the moment it opens, the village taken the moment it is ready), sim count and what is drawn.
// Lives here (dotnet test only) beside PacingLabTests, whose Keeper it reuses. Table: docs/design/animal-caps.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    public static class AnimalCapsLab
    {
        /// <summary>One box at one moment: the sim's count and what the views draw (the budget caps; frogs are view only, at night).</summary>
        public sealed class Census
        {
            public float year; public Era era;
            public int rabbits, rabbitsShown, deer, deerShown, ducks, ducksShown, fish, fishShown, frogsNight, wolves, wolvesShown, dogs;
            public float birds; public int pop, buildings; public long gate; public string wolfNeed;
            public int Drawn => rabbitsShown + deerShown + ducksShown + fishShown + frogsNight + wolvesShown;
        }

        /// <summary>The frogs PondLife draws at night (5, one more after 20:00, capped by the budget; none without a pond).</summary>
        public static int FrogsAtNight(SimCore sim) => sim.State.eco.cPond > 0 ? Math.Min(6, AnimalBudget.Frogs) : 0;

        public static Census Take(SimCore sim) => new Census
        {
            year = sim.State.month / 12f, era = sim.State.era,
            rabbits = sim.EcoRabbitCount, rabbitsShown = sim.EcoRabbitsShown(),
            deer = sim.EcoDeerCount, deerShown = sim.EcoDeerShown(),
            ducks = sim.EcoDucks, ducksShown = sim.EcoDucksShown(),
            fish = sim.EcoFish, fishShown = sim.EcoFishShown(),
            frogsNight = FrogsAtNight(sim),
            wolves = sim.EcoWolfCount, wolvesShown = Math.Min(AnimalBudget.Wolves, sim.EcoWolfCount),
            dogs = sim.DogCount, birds = sim.State.eco.birds, pop = sim.State.population, buildings = sim.State.buildings.Count, gate = sim.State.eco.villageGateMonth, wolfNeed = sim.JarNeed(LifeSpecies.Wolves) ?? "ready",
        };

        /// <summary>Runs the Active Keeper on <paramref name="seed"/> to <paramref name="years"/>, taking the village when it is ready;
        /// <paramref name="perMonth"/> sees every month.</summary>
        public static void Run(int seed, int years, Action<SimCore> perMonth, EcoTuning tuning = null)
        {
            PacingLab.Run(Habit.Active, seed, tuning, years, perMonth: sim =>
            {
                // the village: taken the moment the land's gate opens. Headless, the people do not grow to the era's 10 (the
                // planner's hunts and births are not run here), so the lab marks the era ready itself; no animal count reads the era.
                if (sim.State.era == Era.Camp && sim.State.eco.villageGateMonth >= 0) { sim.State.eraReady = true; sim.AdvanceEra(); }
                perMonth(sim);
            });
        }

        /// <summary>Census at the given game years (the camp at year 20; the village opens at about year 25).</summary>
        public static Dictionary<int, Census> Samples(int seed, int[] years, EcoTuning tuning = null)
        {
            var got = new Dictionary<int, Census>();
            Run(seed, years.Max() + 1, sim =>
            {
                if (sim.State.month % 12 != 6) return; // mid-year, summer: the frogs are out, the ducks are on the pond
                int y = (int)(sim.State.month / 12);
                if (years.Contains(y) && !got.ContainsKey(y)) got[y] = Take(sim);
            }, tuning);
            return got;
        }
    }

    public class AnimalCapsLabTests
    {
        static readonly int[] Seeds = { 7, 21, 42 };

        /// <summary>#710: no kind is ever drawn above its budget cap, and the sim's own caps hold, through 30 years of three seeds.</summary>
        [Test]
        public void EveryKind_StaysAtOrUnderItsCap_Through30Years()
        {
            var t = new EcoTuning();
            foreach (var s in Seeds)
            {
                int months = 0, maxDucks = 0, maxDeer = 0, maxFish = 0, maxRabbits = 0, maxWolves = 0, maxDogs = 0;
                AnimalCapsLab.Run(s, 30, sim =>
                {
                    months++;
                    var c = AnimalCapsLab.Take(sim);
                    string at = $"seed {s} y{c.year:0.0}";
                    Assert.LessOrEqual(c.rabbitsShown, AnimalBudget.Rabbits, $"{at}: rabbits drawn");
                    Assert.LessOrEqual(c.deerShown, AnimalBudget.Deer, $"{at}: deer drawn");
                    Assert.LessOrEqual(c.ducksShown, AnimalBudget.Ducks, $"{at}: ducks drawn");
                    Assert.LessOrEqual(c.fishShown, AnimalBudget.Fish, $"{at}: fish drawn");
                    Assert.LessOrEqual(c.frogsNight, AnimalBudget.Frogs, $"{at}: frogs drawn");
                    Assert.LessOrEqual(c.wolvesShown, AnimalBudget.Wolves, $"{at}: wolves drawn");
                    Assert.LessOrEqual(c.deer, t.deerMax, $"{at}: the sim herd under deerMax");
                    Assert.LessOrEqual(c.ducks, t.duckCap, $"{at}: the sim flock under duckCap");
                    Assert.LessOrEqual(c.wolves, t.wolfCap, $"{at}: the sim pack under wolfCap");
                    Assert.LessOrEqual(c.dogs, SimCore.DogCap, $"{at}: the dogs under DogCap");
                    Assert.LessOrEqual(c.Drawn, AnimalBudget.Total(), $"{at}: all drawn within the budget");
                    maxDucks = Math.Max(maxDucks, c.ducksShown); maxDeer = Math.Max(maxDeer, c.deerShown); maxFish = Math.Max(maxFish, c.fishShown);
                    maxRabbits = Math.Max(maxRabbits, c.rabbitsShown); maxWolves = Math.Max(maxWolves, c.wolvesShown); maxDogs = Math.Max(maxDogs, c.dogs);
                });
                Assert.Greater(months, 30 * 12 - 13, $"seed {s}: the run reached year 30");
                // the box still fills: the rabbits and ducks reach their caps, so the hunts, the dog's flush and the duck feeding have their animals
                Assert.AreEqual(AnimalBudget.Rabbits, maxRabbits, $"seed {s}: the rabbits reach their cap");
                Assert.AreEqual(AnimalBudget.Ducks, maxDucks, $"seed {s}: the ducks reach their cap");
                TestContext.Out.WriteLine($"seed {s}: max drawn rabbits {maxRabbits} deer {maxDeer} ducks {maxDucks} fish {maxFish} wolves {maxWolves}; dogs {maxDogs}");
            }
        }

        [Test, Explicit("#710: the census table (docs/design/animal-caps.md): sim count (drawn) per kind, the camp at year 20, the village ~10 and ~20 years in (years 35 and 45)")]
        public void Census_Measure()
        {
            var years = new[] { 10, 15, 20, 25, 30, 35, 40, 45, 50, 55 };
            var all = Seeds.Concat(new[] { 3, 5 }).ToArray();
            var rows = new Dictionary<int, List<AnimalCapsLab.Census>>();
            foreach (var s in all)
                foreach (var kv in AnimalCapsLab.Samples(s, years))
                {
                    if (!rows.TryGetValue(kv.Key, out var l)) rows[kv.Key] = l = new List<AnimalCapsLab.Census>();
                    l.Add(kv.Value);
                    var c = kv.Value;
                    TestContext.Out.WriteLine($"seed {s,2} y{kv.Key} {c.era,-7} rabbits {c.rabbits}({c.rabbitsShown}) deer {c.deer}({c.deerShown}) ducks {c.ducks}({c.ducksShown}) fish {c.fish}({c.fishShown}) frogs {c.frogsNight} wolves {c.wolves}({c.wolvesShown}) dogs {c.dogs} birds {c.birds:0}  drawn {c.Drawn}  [pop {c.pop} bld {c.buildings} gate {c.gate}] wolves: {c.wolfNeed}");
                }
            var sb = new StringBuilder("\nmean over seeds " + string.Join(",", all) + "  (sim count / drawn)\n");
            foreach (var y in years)
            {
                if (!rows.TryGetValue(y, out var l)) continue;
                string M(Func<AnimalCapsLab.Census, float> f) => l.Average(f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                sb.AppendLine($"y{y} [{string.Join("/", l.Select(c => c.era.ToString()[0]))}] rabbits {M(c => c.rabbits)}/{M(c => c.rabbitsShown)} deer {M(c => c.deer)}/{M(c => c.deerShown)} ducks {M(c => c.ducks)}/{M(c => c.ducksShown)} fish {M(c => c.fish)}/{M(c => c.fishShown)} frogs {M(c => c.frogsNight)} wolves {M(c => c.wolves)}/{M(c => c.wolvesShown)} dogs {M(c => c.dogs)}  drawn {M(c => c.Drawn)}");
            }
            TestContext.Out.WriteLine(sb.ToString());
        }
    }
}
