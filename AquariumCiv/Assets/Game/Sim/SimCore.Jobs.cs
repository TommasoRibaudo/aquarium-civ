using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>The everyday jobs (docs/design/notable-roles.md §1). Saved as plain strings on <see cref="Notable.job"/>.</summary>
    public static class JobType
    {
        public const string None = "", Elder = "elder";
        public const string Gatherer = "gatherer", Hunter = "hunter", Fisher = "fisher", Woodcutter = "woodcutter", Toolmaker = "toolmaker";
        public const string Farmer = "farmer", Shepherd = "shepherd", Beekeeper = "beekeeper", Potter = "potter", Brewer = "brewer";
        public const string Mason = "mason", Smith = "smith", Weaver = "weaver", Baker = "baker", Stallholder = "stallholder", Sweeper = "sweeper";
    }

    /// <summary>
    /// Every notable holds an everyday job (#25, notable-roles.md §1): none as a child, one from 16, chosen from the era's
    /// list among the jobs whose building or land feature exists, weighted by trait. Past <see cref="SimConfig.retireAge"/>
    /// a notable retires to elder. At an era change, or when a job's building is gone, the notable takes the nearest job
    /// still open. Jobs are view and story only: no dice (choices come from a hash of the seed, the person and the month),
    /// and nothing in the economy reads them.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>A job in an era, and what it needs: every entry must exist; "a|b" means either. An entry is a building
        /// type (<see cref="BuildingType"/>) or a land feature (<see cref="HasFeature"/>).</summary>
        static readonly (Era era, string job, string[] needs)[] JobTable =
        {
            (Era.Camp, JobType.Gatherer, new string[0]),
            (Era.Camp, JobType.Hunter, new[] { "forest" }),
            (Era.Camp, JobType.Fisher, new[] { "pond" }),
            (Era.Camp, JobType.Woodcutter, new[] { "trees" }),
            (Era.Camp, JobType.Toolmaker, new[] { BuildingType.Camp }),

            (Era.Village, JobType.Farmer, new[] { BuildingType.Farm }),
            (Era.Village, JobType.Woodcutter, new[] { BuildingType.Lumbermill + "|grove" }),
            (Era.Village, JobType.Fisher, new[] { "pond" }),
            (Era.Village, JobType.Shepherd, new[] { "herd" }),
            (Era.Village, JobType.Beekeeper, new[] { "meadow" }),
            (Era.Village, JobType.Potter, new[] { "clay" }),
            (Era.Village, JobType.Brewer, new[] { BuildingType.Tavern }),

            (Era.Town, JobType.Farmer, new[] { BuildingType.Farm }),
            (Era.Town, JobType.Woodcutter, new[] { BuildingType.Lumbermill + "|grove" }),
            (Era.Town, JobType.Fisher, new[] { "pond" }),
            (Era.Town, JobType.Shepherd, new[] { "herd" }),
            (Era.Town, JobType.Beekeeper, new[] { "meadow" }),
            (Era.Town, JobType.Mason, new[] { "stone", "quarry" }),
            (Era.Town, JobType.Potter, new[] { "clay", "kiln" }),
            (Era.Town, JobType.Smith, new[] { "ore", "smithy" }),
            (Era.Town, JobType.Weaver, new[] { "herd|dye", BuildingType.Workshop }),
            (Era.Town, JobType.Baker, new[] { BuildingType.Windmill, BuildingType.Market }),
            (Era.Town, JobType.Stallholder, new[] { BuildingType.Market }),
            (Era.Town, JobType.Sweeper, new string[0]), // care moves up: the people sweep and weed in the town
        };

        /// <summary>When a job is gone, the nearest ones to take, in order (§1: hunter → shepherd, gatherer → farmer).</summary>
        static readonly Dictionary<string, string[]> NearestJobs = new()
        {
            [JobType.Gatherer] = new[] { JobType.Farmer, JobType.Beekeeper },
            [JobType.Hunter] = new[] { JobType.Shepherd, JobType.Fisher },
            [JobType.Toolmaker] = new[] { JobType.Smith, JobType.Potter, JobType.Woodcutter },
            [JobType.Brewer] = new[] { JobType.Baker, JobType.Stallholder },
            [JobType.Woodcutter] = new[] { JobType.Mason, JobType.Farmer },
            [JobType.Farmer] = new[] { JobType.Baker, JobType.Shepherd },
            [JobType.Baker] = new[] { JobType.Farmer, JobType.Stallholder },
            [JobType.Stallholder] = new[] { JobType.Baker, JobType.Sweeper },
            [JobType.Potter] = new[] { JobType.Mason, JobType.Smith },
        };

        /// <summary>The jobs a trait leans to (gardener → farmer, brave → hunter [est]). Four times as likely as others.</summary>
        static readonly Dictionary<string, string[]> TraitJobs = new()
        {
            ["the brave"] = new[] { JobType.Hunter, JobType.Woodcutter, JobType.Smith },
            ["the gardener"] = new[] { JobType.Gatherer, JobType.Farmer, JobType.Beekeeper, JobType.Sweeper },
            ["the tinkerer"] = new[] { JobType.Toolmaker, JobType.Smith, JobType.Potter },
            ["the clever"] = new[] { JobType.Toolmaker, JobType.Weaver, JobType.Brewer },
            ["the singer"] = new[] { JobType.Brewer, JobType.Shepherd, JobType.Stallholder },
            ["the loud"] = new[] { JobType.Hunter, JobType.Stallholder, JobType.Shepherd },
            ["the kind"] = new[] { JobType.Gatherer, JobType.Baker, JobType.Shepherd },
            ["the stubborn"] = new[] { JobType.Woodcutter, JobType.Mason, JobType.Farmer },
            ["the dreamer"] = new[] { JobType.Fisher, JobType.Shepherd, JobType.Weaver },
            ["the lazy"] = new[] { JobType.Fisher, JobType.Gatherer, JobType.Shepherd },
            ["the worrier"] = new[] { JobType.Gatherer, JobType.Farmer, JobType.Potter },
            ["the gossip"] = new[] { JobType.Stallholder, JobType.Brewer, JobType.Weaver },
            ["the grump"] = new[] { JobType.Woodcutter, JobType.Mason, JobType.Sweeper },
            ["the joker"] = new[] { JobType.Brewer, JobType.Baker, JobType.Potter },
        };

        /// <summary>Land features the slice box always has. Herds, meadows, clay, stone and ore come with the shaping tools
        /// (research 10 "Decision"), which the sim doesn't have yet, so their jobs stay closed for now.</summary>
        bool HasFeature(string f) => f switch
        {
            "pond" or "trees" or "forest" => true,
            "grove" => State.nudges.Any(n => n.kind == NudgeKind.Grove && n.credited),
            // The later shaping tools (research 10, #32): an answered shaping opens its trade. Read from the shaping and
            // the terrain only, never from placement.
            "herd" => State.nudges.Any(n => n.kind == NudgeKind.Herd && n.credited),
            "meadow" or "dye" => State.nudges.Any(n => n.kind == NudgeKind.DyeMeadow && n.credited),
            "stone" or "quarry" => State.nudges.Any(n => n.kind == NudgeKind.Outcrop && n.credited && Deposit(n) == "stone"),
            "clay" or "kiln" => State.nudges.Any(n => n.kind == NudgeKind.Outcrop && n.credited && Deposit(n) == "clay"),
            "ore" or "smithy" => State.nudges.Any(n => n.kind == NudgeKind.Outcrop && n.credited && Deposit(n) == "ore"),
            _ => false,
        };

        bool Has(string need) => need.Split('|').Any(x => Count(x) > 0 || HasFeature(x));

        /// <summary>The era's job list (whatever exists), in table order.</summary>
        public static IEnumerable<string> JobsOf(Era era) => JobTable.Where(j => j.era == era).Select(j => j.job);

        /// <summary>Whether this job can be held now: it is on the era's list and what it needs exists.</summary>
        public bool JobOpen(string job) => JobTable.Any(j => j.era == State.era && j.job == job && j.needs.All(Has));

        /// <summary>The jobs open now, in table order.</summary>
        public List<string> OpenJobs() => JobTable.Where(j => j.era == State.era && j.needs.All(Has)).Select(j => j.job).ToList();

        /// <summary>The job this notable should have the right to hold: none as a child, elder when retired, else an open job.</summary>
        public bool JobValid(Notable n)
        {
            if (!n.alive) return true;
            if (n.age < 16f) return string.IsNullOrEmpty(n.job);
            if (n.age >= Config.retireAge) return n.job == JobType.Elder;
            return JobOpen(n.job);
        }

        /// <summary>Every month (end of Step): children have no job, the old retire, newcomers get one, and a job whose
        /// building is gone is swapped for the nearest. Also fixes saves from before jobs.</summary>
        void JobsStep() { ReassignJobs(false); FillOpenJobs(); }

        /// <summary>The era moved: jobs that aren't on the new era's list change, in one chronicle line.</summary>
        void JobsOnEraChange() => ReassignJobs(true);

        void ReassignJobs(bool eraChange)
        {
            List<(Notable n, string from)> moved = null;
            foreach (var n in State.notables)
            {
                if (!n.alive || JobValid(n)) continue;
                string from = n.job ?? JobType.None;
                if (n.age < 16f) { n.job = JobType.None; continue; }
                if (n.age >= Config.retireAge)
                {
                    n.job = JobType.Elder;
                    if (from != JobType.None)
                        Chronicle("retired", string.Format(RetireLines[(int)(JobRoll(n) * RetireLines.Length) % RetireLines.Length],
                            n.FullName, A(from), Math.Floor(n.age)), 0.45f, n.id);
                    continue;
                }
                n.job = ChooseJob(n, from);
                jobChangedMonth[n.id] = State.month;
                if (from == JobType.None || from == JobType.Elder) continue; // a first job: the coming-of-age or rise line covers it
                bool eraGone = !JobsOf(State.era).Contains(from);
                if (eraChange || eraGone) (moved ??= new List<(Notable, string)>()).Add((n, from));
                else Chronicle("job_change", $"{n.FullName} had no {NeedLabel(from)} to work at any more, and became {A(n.job)}.", 0.5f, n.id);
            }
            if (moved != null) Chronicle("job_era", EraJobsLine(moved), 0.6f, moved[0].n.id);
        }


        // ---------- an open job gets someone (#67) ----------
        /// <summary>A notable doesn't switch again for this many months after a change (3 years).</summary>
        const int JobChangeCooldownMonths = 36;

        /// <summary>When each notable last changed job through a fill or a forced move. Not saved: after a load the
        /// cooldown starts fresh, which only means one person may switch a little sooner.</summary>
        readonly Dictionary<int, long> jobChangedMonth = new();

        /// <summary>A job that is open (its building or land is there) but no one holds: one suitable notable takes it,
        /// at most one a month. Trait-weighted, preferring someone from a crowded job; never an elder, a child, someone
        /// who switched lately, or the only holder of their job. No dice, no effect on the economy.</summary>
        void FillOpenJobs()
        {
            var workers = State.notables.Where(n => n.alive && n.age >= 16f && n.age < Config.retireAge).ToList();
            if (workers.Count < 2) return;
            foreach (var job in OpenJobs())
            {
                if (workers.Any(n => n.job == job)) continue;
                var cands = workers.Where(n => !string.IsNullOrEmpty(n.job) && n.job != JobType.Elder &&
                    workers.Count(o => o.job == n.job) >= 2 &&
                    (!jobChangedMonth.TryGetValue(n.id, out long m) || State.month - m >= JobChangeCooldownMonths)).ToList();
                if (cands.Count == 0) continue;
                var weights = new float[cands.Count];
                float total = 0;
                for (int i = 0; i < cands.Count; i++)
                {
                    TraitJobs.TryGetValue(cands[i].trait ?? "", out var liked);
                    int holders = workers.Count(o => o.job == cands[i].job);
                    weights[i] = (liked != null && Array.IndexOf(liked, job) >= 0 ? 4f : 1f) * (holders - 1);
                    total += weights[i];
                }
                float pick = JobRoll(cands[0]) * total;
                var who = cands[^1];
                for (int i = 0; i < cands.Count; i++)
                    if ((pick -= weights[i]) < 0) { who = cands[i]; break; }
                string from = who.job;
                who.job = job;
                jobChangedMonth[who.id] = State.month;
                Chronicle("job_fill", $"{OpenedLabel(job)}, {who.FullName} left {LeftLabel(from)} to {Verb(job)}.", 0.5f, who.id);
                return; // one a month: the next open job waits its turn
            }
        }

        static string OpenedLabel(string job) => job switch
        {
            JobType.Farmer => "When the farm opened",
            JobType.Baker => "When the bakery opened",
            JobType.Brewer => "When the tavern opened",
            JobType.Stallholder => "When the market opened",
            JobType.Woodcutter => "When the lumber mill opened",
            JobType.Weaver => "When the workshop opened",
            JobType.Toolmaker => "When the camp was set",
            _ => "When work as " + A(job) + " opened",
        };

        static string LeftLabel(string job) => job switch
        {
            JobType.Gatherer => "the foraging",
            JobType.Hunter => "the hunt",
            JobType.Fisher => "the pond",
            JobType.Woodcutter => "the trees",
            JobType.Farmer => "the fields",
            JobType.Shepherd => "the flock",
            JobType.Beekeeper => "the hives",
            JobType.Potter => "the clay",
            JobType.Brewer => "the brewing",
            JobType.Mason => "the stone",
            JobType.Smith => "the forge",
            JobType.Weaver => "the loom",
            JobType.Baker => "the oven",
            JobType.Stallholder => "the stall",
            JobType.Sweeper => "the broom",
            JobType.Toolmaker => "the tool bench",
            _ => "their work",
        };

        static string Verb(string job) => job switch
        {
            JobType.Gatherer => "gather",
            JobType.Hunter => "hunt",
            JobType.Fisher => "fish",
            JobType.Woodcutter => "cut wood",
            JobType.Toolmaker => "make tools",
            JobType.Farmer => "farm",
            JobType.Shepherd => "tend the flock",
            JobType.Beekeeper => "keep bees",
            JobType.Potter => "throw pots",
            JobType.Brewer => "brew",
            JobType.Mason => "cut stone",
            JobType.Smith => "forge iron",
            JobType.Weaver => "weave",
            JobType.Baker => "bake",
            JobType.Stallholder => "keep a stall",
            JobType.Sweeper => "sweep",
            _ => "work as " + A(job),
        };

        /// <summary>The nearest open job to the one lost, else a trait-weighted pick that spreads people over the open jobs.</summary>
        string ChooseJob(Notable n, string from)
        {
            var open = OpenJobs();
            if (open.Count == 0) return JobType.Gatherer; // never happens: every era has a job that needs nothing
            if (from != null && NearestJobs.TryGetValue(from, out var near))
                foreach (var j in near)
                    if (open.Contains(j)) return j;

            TraitJobs.TryGetValue(n.trait ?? "", out var liked);
            var weights = new float[open.Count];
            float total = 0;
            for (int i = 0; i < open.Count; i++)
            {
                int holders = State.notables.Count(o => o != n && o.alive && o.job == open[i]);
                float w = (liked != null && Array.IndexOf(liked, open[i]) >= 0 ? 4f : 1f) / ((1 + holders) * (1 + holders));
                weights[i] = w;
                total += w;
            }
            float pick = JobRoll(n) * total;
            for (int i = 0; i < open.Count; i++)
                if ((pick -= weights[i]) < 0) return open[i];
            return open[^1];
        }

        /// <summary>0..1 from the seed, the person and the month: deterministic, and it draws from no stream.</summary>
        float JobRoll(Notable n)
        {
            uint h = (uint)State.seed * 0x9E3779B1u ^ (uint)n.id * 0x85EBCA77u ^ (uint)State.month * 0xC2B2AE3Du;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        string EraJobsLine(List<(Notable n, string from)> moved)
        {
            string era = State.era == Era.Village ? "the village" : State.era == Era.Town ? "the town" : "the new age";
            var parts = moved.Take(3).Select(m => $"{m.n.FullName} went from {m.from} to {m.n.job}").ToList();
            string rest = moved.Count > 3 ? $", and {moved.Count - 3} more found new work" : "";
            return $"With {era} came new work: {string.Join(", ", parts)}{rest}.";
        }

        /// <summary>The building a job worked at, for the "it's gone" line.</summary>
        string NeedLabel(string job)
        {
            var row = JobTable.FirstOrDefault(j => j.era == State.era && j.job == job);
            var need = row.needs?.FirstOrDefault(x => !Has(x));
            if (need == null) return "place";
            string first = need.Split('|')[0];
            return first == BuildingType.Lumbermill ? "lumber mill" : first.ToLowerInvariant();
        }

        /// <summary>{0} = the person, {1} = "a fisher", {2} = their age.</summary>
        static readonly string[] RetireLines =
        {
            "{0} put down their work as {1} at {2} and sat down by the fire, to tell stories and mind the children.",
            "At {2}, {0} stopped being {1} and became an elder. They still tell everyone how it should be done.",
            "{0} retired at {2} after a lifetime as {1}. The children now get their stories every evening.",
        };

        static string A(string job) => job.Length > 0 && "aeiou".IndexOf(job[0]) >= 0 ? "an " + job : "a " + job;

        /// <summary>Gives a notable coming of age their first job and returns the chronicle line for it.</summary>
        string ComingOfAgeLine(Notable n)
        {
            n.job = ChooseJob(n, null);
            return $"{n.FullName} came of age and took up work as {A(n.job)}.";
        }

        // ---------- debug ----------
        /// <summary>Debug: give this notable a job now (any job, valid or not; the next month fixes an invalid one).</summary>
        public void DebugSetJob(Notable n, string job) { if (n != null) n.job = job ?? JobType.None; }

        /// <summary>Debug: every working adult draws their job again (as if they had just come of age this month).</summary>
        public void DebugRedrawJobs()
        {
            foreach (var n in State.notables.Where(x => x.alive && x.age >= 16f && x.age < Config.retireAge))
                n.job = ChooseJob(n, null);
        }
    }
}
