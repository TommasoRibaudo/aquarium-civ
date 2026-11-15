using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#697/#696: the replan log on a long camp session. Counts walks dropped half way (a reversal), the ones who got up and went
    /// back to bed, and how many people a replan puts doing something else at the very instant it lands.</summary>
    public class ReplanDiagnosisTests
    {
        internal static void Fire(SimCore sim, LifePlanner planner, string trigger) => ReplanKeepsPlansTests.Fire(sim, planner, trigger);

        static string Sig(in PersonActivity a) => $"{a.kind}/{a.place.kind}{a.place.index}{a.place.buildingId}/{a.from.kind}{a.from.index}{a.from.buildingId}/{a.moving}/{a.prop}";

        internal struct Session { public int replans, worst, mass, reversals, risers, massReversals, otherReversals; public string hist; public List<string> lines; }

        /// <summary>A session with the sim's own months plus an injected trigger now and then.</summary>
        internal static Session Natural(int seed, int days, bool keepEveryone)
        {
            var (sim, planner) = CampRhythmTests.Camp(10, seed);
            planner.KeepEveryone = keepEveryone;
            var ses = new Session { lines = new List<string>() };
            string last = "";
            planner.ReplanLog = l => { ses.replans++; last = l.Substring(0, Math.Min(70, l.IndexOf('|') < 0 ? l.Length : l.IndexOf('|'))); };
            var rng = new Random(seed);
            var prevActs = new List<PersonActivity>(); var acts = new List<PersonActivity>(); var redo = new List<PersonActivity>();
            long day = sim.State.month + 1;
            long first = day;
            float nextStep = day * 24 + 12;
            var hist = new SortedDictionary<int, int>();
            var slept = new HashSet<int>(); var woke = new HashSet<int>();
            string[] pool = { "population", "chronicle", "nudge", "supper", "month", "season", "waterrun" };
            for (; day < first + days; day++)
                for (float h = 0f; h < 23.99f; h += 0.05f)
                {
                    if (day * 24 + h >= nextStep)
                    {
                        nextStep = day * 24 + h + 6 + (float)rng.NextDouble() * 16;
                        if (rng.Next(3) == 0) sim.Step();
                        else Fire(sim, planner, pool[rng.Next(pool.Length)]);
                    }
                    int before = ses.replans;
                    planner.PlanAll(new ViewTime(day, h), acts);
                    int frameReversals = 0;
                    foreach (var a in acts)
                    {
                        int k = a.who.Key;
                        var b = prevActs.Find(x => x.who.Key == k);
                        if (b.who.Key == k && h > 0.06f && b.moving && !b.enRoute && !a.enRoute && b.end > h + 1e-3f && !(a.moving && a.place == b.place))
                        {
                            frameReversals++;
                            if (ses.replans != before) { ses.reversals++; ses.lines.Add($"reversal at a replan: person {k} day {day} {h:0.00}: {Sig(b)} -> {Sig(a)} [{last}]"); }
                            else ses.otherReversals++; // a walk ended early by something that is not a replan (a run-in, a chat on the way)
                        }
                        bool asleep = a.kind == ActivityKind.Sleep && !a.moving;
                        if (h >= 5f && h < 19f) { slept.Remove(k); woke.Remove(k); }
                        else if (asleep) { if (woke.Remove(k)) { ses.risers++; ses.lines.Add($"riser: person {k} day {day} {h:0.00} [{last}]"); } slept.Add(k); }
                        else if (slept.Contains(k) && !(a.moving && a.place.kind == PlaceKind.Home)) woke.Add(k);
                    }
                    if (frameReversals >= 3) { ses.massReversals++; ses.lines.Add($"MASS reversal: {frameReversals} in one frame, day {day} {h:0.00} [{last}] replan this frame: {ses.replans != before}"); }
                    if (ses.replans != before && prevActs.Count > 0 && h > 0.06f)
                    {
                        planner.PlanAll(new ViewTime(day, h - 0.05f), redo);
                        planner.PlanAll(new ViewTime(day, h), acts);
                        int n = 0;
                        foreach (var a in redo)
                        {
                            var b = prevActs.Find(x => x.who.Key == a.who.Key);
                            if (b.who.Key == a.who.Key && Sig(b) != Sig(a) && !(b.kind == ActivityKind.Sleep && a.kind == ActivityKind.Sleep && !b.moving && !a.moving)) n++;
                        }
                        hist.TryGetValue(n, out int c); hist[n] = c + 1;
                        if (n >= 3) ses.mass++;
                        ses.worst = Math.Max(ses.worst, n);
                    }
                    prevActs.Clear(); prevActs.AddRange(acts);
                }
            ses.hist = string.Join(",", hist.Select(kv => kv.Key + ":" + kv.Value));
            return ses;
        }

        /// <summary>People whose activity at one instant is different just after a trigger fires than just before it.</summary>
        static int Changed(SimCore sim, LifePlanner planner, string trigger, long day, float hour, List<string> why)
        {
            var before = new List<PersonActivity>(); var after = new List<PersonActivity>();
            var t = new ViewTime(day, hour);
            planner.PlanAll(t, before);
            Fire(sim, planner, trigger);
            planner.PlanAll(t, after);
            int n = 0;
            foreach (var a in after)
            {
                var b = before.Find(x => x.who.Key == a.who.Key);
                if (b.who.Key != a.who.Key) continue;
                if (Sig(b) != Sig(a) && !(b.kind == ActivityKind.Sleep && !b.moving && a.kind == ActivityKind.Sleep && !a.moving)) { n++; why.Add($"{a.who.Key}: {Sig(b)} -> {Sig(a)}"); }
            }
            return n;
        }

        /// <summary>Which triggers put several people on something else at the instant they land, and when (a diagnosis, no assertion).</summary>
        [Test, Explicit("diagnosis: prints, asserts nothing")]
        public void Trigger_sweep()
        {
            foreach (bool keep in new[] { false, true })
                foreach (var trig in LifePlanner.ReplanTriggers)
                {
                    int worst = 0, total = 0; float at = 0;
                    for (float h = 5.0f; h < 24f; h += 0.5f)
                    {
                        var (sim, planner) = CampRhythmTests.Camp(8, 7);
                        planner.KeepEveryone = keep;
                        long day = trig == "supper" ? sim.State.month : sim.State.month + 2;
                        int n = Changed(sim, planner, trig, day, h, new List<string>());
                        total += n;
                        if (n > worst) { worst = n; at = h; }
                    }
                    TestContext.WriteLine($"keepEveryone={keep} {trig}: worst {worst} at {at:0.0}, total {total}");
                }
        }

        /// <summary>A Keeper gesture's reaction is on (the people walk to the finger), and a replan lands a moment into it.</summary>
        internal static (int reversals, int jumps, string first) ReactionThenReplan(string trigger, KeeperGesture g, float hour, bool keep, int pop = 8)
        {
            var (sim, planner) = CampRhythmTests.Camp(pop, 7);
            planner.KeepEveryone = keep;
            long day = sim.State.month + 1;
            var acts = new List<PersonActivity>(); var prev = new Dictionary<int, PersonActivity>();
            int rev = 0; string first = null;
            planner.PlanAll(new ViewTime(day, hour - 0.1f), acts);
            planner.React(g, new ViewTime(day, hour));
            bool fired = false;
            for (float h = hour; h < hour + 0.5f; h += 0.01f)
            {
                if (!fired && h >= hour + 0.03f) { fired = true; Fire(sim, planner, trigger); }
                planner.PlanAll(new ViewTime(day, h), acts);
                foreach (var a in acts)
                {
                    if (prev.TryGetValue(a.who.Key, out var b) && b.moving && !b.enRoute && b.end > h + 1e-3f && !(a.moving && a.place == b.place) && !a.enRoute)
                    { rev++; first ??= $"{a.who.Key} at {h:0.00}: {b.kind}>{b.place.kind} -> {a.kind}{(a.moving ? '>' : '=')}{a.place.kind}"; }
                    prev[a.who.Key] = a;
                }
            }
            return (rev, 0, first);
        }

        [Test, Explicit("diagnosis: prints, asserts nothing")]
        public void Reaction_probe()
        {
            foreach (bool keep in new[] { false, true })
                foreach (var g in new[] { KeeperGesture.Return, KeeperGesture.Wipe, KeeperGesture.Rain })
                    foreach (var trig in new[] { "month", "chronicle", "nudge", "supper" })
                    {
                        int tot = 0; string first = null;
                        foreach (float hour in new[] { 7.3f, 9.4f, 12.2f, 15.1f, 17.4f })
                        {
                            var r = ReactionThenReplan(trig, g, hour, keep);
                            tot += r.reversals; first ??= r.first;
                        }
                        TestContext.WriteLine($"keep={keep} {g} + {trig}: reversals {tot} {first}");
                    }
        }

        [Test, Explicit("diagnosis: prints, asserts nothing")]
        public void Natural_long()
        {
            foreach (bool keep in new[] { false, true })
                foreach (int seed in new[] { 3, 5, 13 })
                {
                    var r = Natural(seed, 120, keep);
                    TestContext.WriteLine($"keepEveryone={keep} seed {seed}: {r.replans} replans, worst {r.worst}, 3+ changed {r.mass}, reversals at a replan {r.reversals}, mass frames {r.massReversals}, other reversals {r.otherReversals}, risers {r.risers}, histogram {r.hist}");
                    foreach (var l in r.lines.Take(8)) TestContext.WriteLine("  " + l);
                }
        }
    }
}
