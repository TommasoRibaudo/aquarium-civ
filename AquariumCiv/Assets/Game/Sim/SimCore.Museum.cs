using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>What a museum visit did: real hours since the last visit, game months lived, people born and who died.</summary>
    public struct MuseumResult
    {
        public double realHours;
        public int months, births, deaths;
        public bool capped;     // the time away was longer than a visit lives through
    }

    /// <summary>
    /// Museum mode and the next box's seed (#87; era-moment.md sections 8-10, Choice 8 "alive but steady"). A finished box is
    /// not stepped by <see cref="SimCore.Step"/>: it runs <see cref="MuseumAdvance"/> only when it is visited. Its people age,
    /// marry, have children, die and have stories; nothing is built, there is no mess, no era and no relic is made.
    /// It draws only from the story stream and the notables' dice, and the box stays small: the museum years add at most
    /// <see cref="MuseumChronicleMax"/> lines and <see cref="MuseumDeadMax"/> dead people.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>A visit lives through at most this many game years, however long the box sat unvisited.</summary>
        public const int MuseumMaxYears = 40;
        public const int MuseumChronicleMax = 120;  // chronicle lines made after the box was finished that are kept
        public const int MuseumDeadMax = 40;        // dead people born after the box was finished that are kept

        /// <summary>Lives a finished box for <paramref name="realHours"/> (clamped to <see cref="MuseumMaxYears"/>). No effect on a
        /// box that is not complete.</summary>
        public MuseumResult MuseumAdvance(double realHours)
        {
            var s = State;
            var r = new MuseumResult { realHours = Math.Max(0, realHours) };
            if (!s.boxComplete || r.realHours <= 0) return r;
            int months = (int)Math.Floor(r.realHours / Config.StepHours + 1e-7);
            int cap = MuseumMaxYears * SimConfig.MonthsPerYear;
            if (months > cap) { months = cap; r.capped = true; }
            bool relics = RelicsOn;
            RelicsOn = false; // a finished box makes no new relics
            int aliveBefore = s.notables.Count(n => n.alive), countBefore = s.notables.Count;
            try
            {
                for (int i = 0; i < months; i++) MuseumMonth();
            }
            finally { RelicsOn = relics; }
            r.months = months;
            r.births = s.notables.Count - countBefore;
            r.deaths = aliveBefore + r.births - s.notables.Count(n => n.alive);
            s.simHours += months * Config.StepHours;
            MuseumTrim();
            return r;
        }

        void MuseumMonth()
        {
            var s = State;
            s.month++;
            int season = (int)(s.month % SimConfig.MonthsPerYear);
            if (season == 0) YearlyNotables(); // age, births, deaths (the notables' own dice)
            JobsStep();
            RolesStep();
            StoryStep(season, season == 11 || season == 0 || season == 1); // weddings, friendships, quarrels: the story stream
        }

        /// <summary>Keeps a visited museum small: the least salient museum-era lines and the oldest dead museum-born people
        /// go, with every mention of them.</summary>
        void MuseumTrim()
        {
            var s = State;
            long since = s.boxCompleteMonth;
            var late = s.chronicle.Where(e => e.month > since).ToList();
            if (late.Count > MuseumChronicleMax)
            {
                var drop = late.OrderBy(e => e.salience).ThenBy(e => e.month).Take(late.Count - MuseumChronicleMax).ToHashSet();
                s.chronicle.RemoveAll(drop.Contains);
            }
            var dead = s.notables.Where(n => !n.alive && n.bornMonth > since).OrderBy(n => n.diedMonth).ToList();
            if (dead.Count <= MuseumDeadMax) return;
            var gone = dead.Take(dead.Count - MuseumDeadMax).Select(n => n.id).ToHashSet();
            s.notables.RemoveAll(n => gone.Contains(n.id));
            s.chronicle.RemoveAll(e => gone.Contains(e.notableId) || gone.Contains(e.otherNotableId));
            foreach (var n in s.notables)
            {
                if (gone.Contains(n.partnerId)) n.partnerId = -1;
                if (gone.Contains(n.friendId)) n.friendId = -1;
                if (gone.Contains(n.rivalId)) n.rivalId = -1;
                if (gone.Contains(n.mentorId)) n.mentorId = -1;
                if (gone.Contains(n.parentId)) n.parentId = -1;
            }
        }

        // ---------- the next box ----------
        /// <summary>Box <paramref name="boxIndex"/>'s first moment: a camp (a fire and a hut) with the finished box's founders
        /// (<see cref="SimState.founderIds"/>) copied in as grown people with a link back, the leader first and chief, and the
        /// relics that crossed (<see cref="Relic.carriedFromBox"/>). Fresh dice from <paramref name="seed"/>; nothing of the
        /// old box's economy crosses.</summary>
        public static SimCore SeedNextBox(SimConfig config, SimCore from, int seed, int boxIndex, BoxCompletion completion, out List<FounderLink> links)
        {
            links = new List<FounderLink>();
            var old = from.State;
            int fromBox = old.boxIndex;
            var s = new SimState
            {
                seed = seed, rng = new SimRandom(seed), econRng = new SimRandom(seed ^ 0x2C1B3C6D), storyRng = new SimRandom(seed ^ 0x51ED270B),
                population = 5, food = 20, wood = 4, mood = 0.7f, boxIndex = boxIndex,
            };
            var core = new SimCore(config, s);
            core.AddBuilding(BuildingType.Camp);
            core.AddBuilding(BuildingType.Hut);
            var crossed = old.founderIds.Select(from.FindNotable).Where(n => n != null && n.alive).ToList();
            var map = new Dictionary<int, int>();
            foreach (var o in crossed)
            {
                var n = new Notable
                {
                    id = s.NextId(), name = o.name, family = o.family, trait = o.trait, age = o.age, lifespan = o.lifespan,
                    bornMonth = -(long)(o.age * SimConfig.MonthsPerYear),
                };
                s.notables.Add(n);
                map[o.id] = n.id;
                links.Add(new FounderLink { notableId = n.id, fromBox = fromBox, fromNotableId = o.id });
            }
            foreach (var o in crossed) // a couple that crossed together stays one
                if (map.TryGetValue(o.partnerId, out var p)) core.FindNotable(map[o.id]).partnerId = p;
            while (s.notables.Count < config.campNotables.min) core.AddNotable(s.rng.Range(18f, 40f), -1, null); // too few crossed
            core.JobsStep();
            var leader = crossed.Count > 0 ? core.FindNotable(map[crossed[0].id]) : null;
            string line = completion?.foundersLine;
            core.Chronicle("founders", line ?? (leader != null
                ? $"{leader.FullName} and their people crossed to a new land, carrying a little of home wrapped in cloth."
                : "A few wanderers crossed to a new land and made camp."), 0.95f, leader?.id ?? -1);
            core.RolesStep();
            var chief = core.SeatOf(RoleType.Chief);
            if (leader != null && chief != null && chief.holderId != leader.id)
            {
                if (chief.holderId >= 0) core.Vacate(chief);
                core.Assign(chief, leader); // the leader of the founders is the camp's first chief
            }
            // The relics that crossed: already found when the box begins, kept by the leader. The box makes its own others.
            foreach (var id in from.RelicsForCrossing().Where(from.HasRelic))
            {
                var o = from.FindRelic(id);
                s.relics.Add(new Relic
                {
                    id = id, foundMonth = 0, carriedFromBox = fromBox, ownerId = leader?.id ?? -1, holderId = leader?.id ?? -1,
                    prop = o.prop, cause = o.cause,
                });
            }
            core.RecordYear();
            if (config.settle) _ = core.Settlement;
            return core;
        }
    }
}
