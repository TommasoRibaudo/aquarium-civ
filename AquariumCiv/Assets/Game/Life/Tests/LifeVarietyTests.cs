using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#197: "they are all either all working or all in the plaza". Across a day the awake people of a town are
    /// spread over places and activities (staggered starts, breaks and lunches), outside a real event.</summary>
    public class LifeVarietyTests
    {
        static SimCore TownWith(int seed, int minPeople, Era era = Era.Town)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 160 * 12 && (sim.State.era < era || sim.State.population < minPeople); m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            for (int m = 0; m < 6; m++) sim.Step();
            return sim;
        }

        struct Spread { public List<float> shares; public float p90, maxPlace, maxWork, maxGroup; public string where, what; public int people, samples; public float lowWork; public float homeSum, homeMax; public int homeN; public float HomeMean => homeN > 0 ? homeSum / homeN : 0f; }

        /// <summary>Over the plain days of a few months (clear weather, no rest day, no scene): at each quarter hour from 5 to
        /// 24, the biggest share of the awake people at one place, and the biggest share doing one group of things.</summary>
        static Spread Measure(SimCore sim, int days, bool rest = false)
        {
            var planner = LifePlanner.Wired(sim);
            planner.ForcedWeather = Weather.Clear;
            var acts = new List<PersonActivity>();
            var r = new Spread { people = sim.State.population, shares = new List<float>() };
            int done = 0;
            for (int d = 0; d < 400 && done < days; d++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
                long day = sim.State.month;
                bool restDay = day % LifeClock.RestDayEvery == 0;
                if (restDay != rest) continue;
                planner.PlanAll(new ViewTime(day, 12f), acts);
                bool event_ = false;
                for (float hh = 5f; hh < 24f && !event_; hh += 0.5f)
                {
                    planner.PlanAll(new ViewTime(day, hh), acts);
                    event_ = acts.Any(a => a.source == ActivitySource.Scene || (a.source == ActivitySource.RoleBeat && a.kind == ActivityKind.Eat));
                }
                if (event_) continue;
                done++;
                for (float h = 6f; h < 20f; h += 0.25f)
                {
                    planner.PlanAll(new ViewTime(day, h), acts);
                    var awake = acts.Where(a => a.kind != ActivityKind.Sleep && a.kind != ActivityKind.None && !(a.kind == ActivityKind.Toddle)).ToList();
                    if (awake.Count < 8) continue;
                    r.samples++;
                    var byPlace = awake.Where(a => !a.moving).GroupBy(a => a.place.kind == PlaceKind.Home ? Place.Land(PlaceKind.Home) : a.place).OrderByDescending(g => g.Count()).FirstOrDefault();
                    if (byPlace != null)
                    {
                        float share = byPlace.Count() / (float)awake.Count;
                        // a family's own door is not a crowd: homes are counted one by one
                        var homes = awake.Where(a => !a.moving && a.place.kind == PlaceKind.Home).GroupBy(a => a.place).Select(g => g.Count()).DefaultIfEmpty(0).Max() / (float)awake.Count;
                        var open = awake.Where(a => !a.moving && a.place.kind != PlaceKind.Home).GroupBy(a => a.place).Select(g => new { g.Key, n = g.Count() }).OrderByDescending(g => g.n).FirstOrDefault();
                        float s2 = Math.Max(homes, open == null ? 0f : open.n / (float)awake.Count);
                        r.shares.Add(s2);
                        if (s2 > r.maxPlace) { r.maxPlace = s2; r.where = $"day {day} {h:0.00}h {(open != null ? open.Key.ToString() : "home")}"; r.what = string.Join(",", awake.GroupBy(a => a.kind + "/" + a.source).Select(g => g.Key + ":" + g.Count())); }
                    }
                    if (h >= 11.5f && h <= 13.5f)
                    {
                        // all doors together: nine of fourteen at their own doors reads as everyone at home, however many doors
                        float atHome = awake.Count(a => !a.moving && a.place.kind == PlaceKind.Home) / (float)awake.Count;
                        r.homeSum += atHome; r.homeN++;
                        if (atHome > r.homeMax) r.homeMax = atHome;
                    }
                    if (h >= 8f && h < 16.5f)
                    {
                        // 'all at work' in the work hours is the other complaint
                        float work = awake.Count(a => a.Group == ActivityGroup.Work || a.Group == ActivityGroup.Carry) / (float)awake.Count;
                        if (work > r.maxWork) r.maxWork = work;
                    }
                    float grp = awake.GroupBy(a => a.Group).Max(g => g.Count()) / (float)awake.Count;
                    if (grp > r.maxGroup) r.maxGroup = grp;
                }
            }
            r.shares.Sort();
            r.p90 = r.shares.Count > 0 ? r.shares[(int)(r.shares.Count * 0.9f)] : 0f;
            return r;
        }

        static void Profile(SimCore sim)
        {
            var planner = LifePlanner.Wired(sim);
            planner.ForcedWeather = Weather.Clear;
            var acts = new List<PersonActivity>();
            long day = 0;
            for (int d = 0; d < 20; d++) { sim.Step(); day = sim.State.month; if (day % LifeClock.RestDayEvery != 0) break; }
            var sb = new StringBuilder();
            for (float h = 5f; h < 24f; h += 0.5f)
            {
                planner.PlanAll(new ViewTime(day, h), acts);
                var g = acts.Where(a => a.kind != ActivityKind.Sleep).GroupBy(a => a.Group).OrderByDescending(x => x.Count()).Select(x => x.Key + ":" + x.Count());
                var pl = acts.Where(a => a.kind != ActivityKind.Sleep && !a.moving && a.place.kind != PlaceKind.Home).GroupBy(a => a.place).OrderByDescending(x => x.Count()).Take(2).Select(x => x.Key + ":" + x.Count());
                sb.AppendLine($"  {h:00.0} {string.Join(" ", g)} | {string.Join(" ", pl)}");
            }
            TestContext.WriteLine(sb.ToString());
        }

        // Measured 2026-11-01 (see the MeasureTheSpread output): before #197 the evening put 90-100 % of the awake people at one place
        // (p90 60-92 %); after, the evening splits between the green, the tavern, friends' doors and home.
        // Measured 2026-11-01, midday 11.5-13.5, all doors together. Before the village lunch varied: Village mean 25 %, max 77 % / 70 %; Town mean 7 %, max 41 % / 50 %.
        // After: Village mean 14-15 %, max 62 % / 50 %. Limits sit between (mean 20 %, max 65 %).
        const float HomeMeanLimit = 0.20f, HomeMaxLimit = 0.65f;

        [TestCase(1, 10, Era.Village)]
        [TestCase(3, 10, Era.Village)]
        [TestCase(1, 10, Era.Town)]
        [TestCase(7, 25, Era.Town)]
        public void MeasureTheSpread(int seed, int minPeople, Era era)
        {
            var sim = TownWith(seed, minPeople, era);
            var a = Measure(sim, 6);
            TestContext.WriteLine($"{era} seed {seed} pop {a.people}: samples {a.samples}, p90 one place {a.p90:P0}, max {a.maxPlace:P0} ({a.where}) {a.what}, max work+carry {a.maxWork:P0}, max one group {a.maxGroup:P0}, midday 11.5-13.5 at any home: mean {a.HomeMean:P0}, max {a.homeMax:P0}");
            Assert.LessOrEqual(a.HomeMean, HomeMeanLimit, "at midday not everyone is at a door");
            Assert.LessOrEqual(a.homeMax, HomeMaxLimit, "and no quarter hour of it has most of the people at home");
            Assert.LessOrEqual(a.p90, 0.65f, "no place holds more than 65 % of the awake people for nine in ten quarter hours of an ordinary day");
            Assert.LessOrEqual(a.maxPlace, era == Era.Town ? 0.85f : 1f, "and never (a village of ten has its story hour on the green; #588: the cook works at the same pit, so a story hour can be one place)");
            Assert.LessOrEqual(a.maxWork, 0.85f, "the work hours are never all at work");
            var b = Measure(sim, 3, true);
            TestContext.WriteLine($"  rest days: max one place {b.maxPlace:P0} ({b.where}), max one group {b.maxGroup:P0}");
        }
    }
}
