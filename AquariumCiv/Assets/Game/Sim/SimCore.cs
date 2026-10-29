using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// Coarse aggregate civ sim. Pure C#, deterministic from the seed; one step = one game month.
    /// Live play and offline catch-up both go through Advance(), so they produce the same world.
    /// </summary>
    public partial class SimCore
    {
        public SimConfig Config { get; }
        public SimState State { get; }
        public event Action<ChronicleEntry> Chronicled;

        public SimCore(SimConfig config, SimState state) { Config = config; State = state; }

        public static SimState NewWorld(int seed, SimConfig config)
        {
            var s = new SimState
            {
                seed = seed, rng = new SimRandom(seed), econRng = new SimRandom(seed ^ 0x2C1B3C6D), storyRng = new SimRandom(seed ^ 0x51ED270B),
                population = 5, food = 20, wood = 4, mood = 0.7f,
            };
            var core = new SimCore(config, s);
            core.AddBuilding(BuildingType.Camp);
            core.AddBuilding(BuildingType.Hut);
            for (int i = 0; i < config.startNotables; i++) core.AddNotable(s.rng.Range(18f, 40f), -1, null);
            core.JobsStep(); // the founders arrive with their work (SimCore.Jobs.cs)
            core.Chronicle("founding", "Five wanderers settled in the glass and made camp.", 0.9f);
            core.RecordYear();
            return s;
        }

        // ---------- derived values ----------
        /// <summary>0..1. The glass is half of it: it's what the player sees first and wipes most.</summary>
        public float MessLevel => State.grime * 0.5f + State.overgrowth * 0.25f + State.trash.Count / (float)Config.trashMax * 0.25f;
        /// <summary>Gate 1: the work-pace drop, linear in mess (shown to the player as sunlight and work pace).</summary>
        public float OutputPenalty => Config.outputPenaltyMax * MessLevel;
        public float OutputMultiplier => (0.8f + 0.4f * State.mood) * (1f - OutputPenalty);
        public int Housing => 4 + Count(BuildingType.Hut) * 4;
        public float FoodCap => 20f + State.population * 6f;
        public float NextBuildingCost => 10f + State.buildings.Count * 6f + State.buildings.Count * State.buildings.Count * 0.6f;
        public int Count(string type) => State.buildings.Count(b => b.type == type);

        // ---------- time ----------
        /// <summary>Advance by simulated real-hours. Steps happen on whole-month boundaries only.</summary>
        public int Advance(double realHours)
        {
            if (realHours <= 0) return 0;
            State.simHours += realHours;
            long target = (long)Math.Floor(State.simHours / Config.StepHours + 1e-7);
            int steps = 0;
            while (State.month < target) { Step(); steps++; }
            return steps;
        }

        /// <summary>
        /// Offline catch-up (game definition v0.2): the civ lives the first <see cref="SimConfig.fullSpeedHours"/> at full
        /// speed and the rest at <see cref="SimConfig.slowSpeed"/>; mess runs on real hours, so the real hours the civ didn't
        /// live are topped up as mess afterwards (capped like natural mess). Live play keeps calling <see cref="Advance"/>.
        /// </summary>
        public CatchUpResult CatchUp(double realHoursAway)
        {
            var r = new CatchUpResult { realHours = Math.Max(0, realHoursAway), simHours = Config.OfflineSimHours(realHoursAway) };
            r.months = Advance(r.simHours);
            double unlived = r.realHours - r.simHours;
            if (unlived > 0) AccrueMess((float)unlived);
            return r;
        }

        public void Step()
        {
            var s = State;
            float birthRoll = s.econRng.Value(), deathRoll = s.econRng.Value(), festivalRoll = s.econRng.Value();
            s.month++;
            int season = (int)(s.month % SimConfig.MonthsPerYear);
            bool winter = season == 11 || season == 0 || season == 1;
            float mult = OutputMultiplier;
            int pop = s.population;

            // Economy. Subsistence ignores the Keeper's care, so neglect slows growth but never starves the civ;
            // the surplus (what pays for growth) is scaled by mood and mess.
            float subsistence = pop * (winter ? 0.85f : 1f);
            float surplus = (2f + pop * 0.2f + Count(BuildingType.Farm) * 3f + Count(BuildingType.Windmill) * 3f) * mult * (winter ? 0.5f : 1f) * NudgeFood;
            s.food = Math.Clamp(s.food + subsistence + surplus - pop, 0f, FoodCap);
            s.wood = Math.Min(s.wood + (1f + pop * 0.15f + Count(BuildingType.Lumbermill) * 1.5f) * mult * NudgeWood, 2000f);

            if (s.food > pop * 1.5f && pop < Housing && birthRoll < 0.025f * (0.5f + s.mood) * (1f - OutputPenalty)) s.population++; // happier = more births; a mess penalty slows growth too
            if (pop > 5 && deathRoll < 0.008f) s.population--; // crowd old age

            // Mood ignores mess (Gate 1): the pace drop is the whole cost, and mood x mess would stack on it.
            float target = 0.75f + (s.food > pop * 2f ? 0.15f : 0f) - (s.food <= 0f ? 0.3f : 0f);
            s.mood += (Math.Clamp(target, 0f, 1f) - s.mood) * 0.1f;

            TryBuild();
            if (s.nudges.Count > 0) NudgeStep();
            AccrueMess((float)Config.StepHours);
            if (season == 0) YearlyNotables();
            NarrativeChecks(winter, festivalRoll);
            StoryStep(season, winter); // draws only from its own stream (SimCore.Story.cs)
            if (season == 0) RecordYear();
            EraStep(); // no dice: only marks the next era ready (SimCore.Eras.cs)
            JobsStep(); // no dice: everyday jobs follow the era and the buildings (SimCore.Jobs.cs)
        }

        void TryBuild()
        {
            var s = State;
            if (s.buildings.Count >= Config.buildingSlots) return;
            float cost = NextBuildingCost;
            if (s.wood < cost) return;
            int pop = s.population;
            string type;
            // Types from a later era are skipped: a camp waiting at its threshold keeps building, but only camp things.
            if (Wants(BuildingType.Farm, Count(BuildingType.Farm) < 1 + pop / 8)) type = BuildingType.Farm;
            else if (Wants(BuildingType.Hut, pop >= Housing - 1)) type = BuildingType.Hut;
            else if (Wants(BuildingType.Lumbermill, pop >= 6 && Count(BuildingType.Lumbermill) == 0)) type = BuildingType.Lumbermill;
            else if (Wants(BuildingType.Well, pop >= 8 && Count(BuildingType.Well) == 0)) type = BuildingType.Well;
            else if (Wants(BuildingType.Windmill, pop >= 12 && Count(BuildingType.Windmill) == 0)) type = BuildingType.Windmill;
            else if (Wants(BuildingType.Market, pop >= 15 && Count(BuildingType.Market) == 0)) type = BuildingType.Market;
            else if (Wants(BuildingType.Tavern, pop >= 18 && Count(BuildingType.Tavern) == 0)) type = BuildingType.Tavern;
            else if (Wants(BuildingType.Church, pop >= 24 && Count(BuildingType.Church) == 0)) type = BuildingType.Church;
            else return; // nothing needed: save wood
            s.wood -= cost;
            AddBuilding(type);
        }

        /// <summary>Mess for <paramref name="h"/> real hours, capped (48 h fills it).</summary>
        void AccrueMess(float h)
        {
            var s = State;
            s.grime = Math.Min(1f, s.grime + h / Config.messFullHours);
            s.overgrowth = Math.Min(1f, s.overgrowth + h / Config.messFullHours);
            if (s.trash.Count >= Config.trashMax) { s.trashAccum = 0; return; }
            s.trashAccum += s.population * Config.trashPerPersonHour * h;
            while (s.trashAccum >= 1f && s.trash.Count < Config.trashMax)
            {
                s.trashAccum -= 1f;
                AddTrash();
            }
        }

        void AddTrash()
        {
            var s = State;
            s.trash.Add(new TrashItem
            {
                id = s.NextId(), slot = s.rng.Range(0, Config.buildingSlots),
                offsetX = s.rng.Range(-0.35f, 0.35f), offsetZ = s.rng.Range(-0.35f, 0.35f), month = s.month
            });
        }

        void YearlyNotables()
        {
            var s = State;
            var rng = s.rng;
            foreach (var n in s.notables.Where(n => n.alive).ToList())
            {
                float before = n.age;
                n.age += Config.notableAgeFactor;
                if (before < 16f && n.age >= 16f)
                    Chronicle("coming_of_age", ComingOfAgeLine(n), 0.4f, n.id);
                if (n.age >= n.lifespan)
                {
                    n.alive = false;
                    n.diedMonth = s.month;
                    Chronicle("death", $"{n.FullName}, {n.trait}, died of old age at {Math.Floor(n.age)}.", 0.85f, n.id);
                    continue;
                }
                int alive = s.notables.Count(x => x.alive);
                if (n.age >= 20f && n.age <= 40f && alive < Config.maxNotables && rng.Chance(0.12f))
                {
                    var child = AddNotable(0f, n.id, n.family);
                    Chronicle("birth", $"{child.FullName} was born to {n.FullName}.", 0.6f, child.id);
                }
            }
            if (s.notables.Count(x => x.alive) < Config.minNotables)
            {
                var n = AddNotable(rng.Range(18f, 30f), -1, null);
                Chronicle("rise", $"{n.FullName}, {n.trait}, rose to prominence among the people.", 0.55f, n.id);
            }
        }

        void NarrativeChecks(bool winter, float festivalRoll)
        {
            var s = State;
            int milestone = s.population / 10 * 10;
            if (milestone > s.lastPopMilestone && milestone > 0)
            {
                s.lastPopMilestone = milestone;
                Chronicle("milestone", $"The settlement grew to {milestone} people.", 0.7f);
            }
            long year = s.month / SimConfig.MonthsPerYear;
            if (winter && s.food < s.population && year - s.lastLeanYear >= 5)
            {
                s.lastLeanYear = year;
                Chronicle("lean_winter", "A lean winter: the granaries ran low and the people tightened their belts.", 0.5f);
            }
            if (!winter && s.food > FoodCap * 0.9f && festivalRoll < 0.02f)
            {
                s.mood = Math.Min(1f, s.mood + 0.15f);
                var host = AliveNotable();
                // A festival makes the chronicle at most every 6 years (they still happen and lift the mood).
                if (!Fresh("festival", -1, 6)) return;
                Chronicle("festival", host != null
                    ? $"{host.FullName} threw a harvest festival. There was dancing until dawn."
                    : "The people held a harvest festival.", 0.65f, host?.id ?? -1);
            }
        }

        // ---------- player actions ----------
        public void CleanGrime(float amount) => State.grime = Math.Max(0f, State.grime - amount);
        public void Prune(float amount) => State.overgrowth = Math.Max(0f, State.overgrowth - amount);
        public bool RemoveTrash(int id) => State.trash.RemoveAll(t => t.id == id) > 0;

        /// <summary>Debug / lab: add mess directly (capped like natural mess).</summary>
        public void AddMess(float grime, float overgrowth, int trashCount)
        {
            State.grime = Math.Clamp(State.grime + grime, 0f, 1f);
            State.overgrowth = Math.Clamp(State.overgrowth + overgrowth, 0f, 1f);
            for (int i = 0; i < trashCount && State.trash.Count < Config.trashMax; i++) AddTrash();
        }

        public void CleanAll()
        {
            State.grime = 0; State.overgrowth = 0; State.trash.Clear();
        }

        // ---------- helpers ----------
        Building AddBuilding(string type)
        {
            var s = State;
            var used = s.buildings.Select(b => b.slot).ToHashSet();
            if (ReservedSlots != null) // groves and channels take a tile (unless that would leave no room at all)
            {
                var withReserved = new HashSet<int>(used);
                withReserved.UnionWith(ReservedSlots());
                if (Enumerable.Range(0, Config.buildingSlots).Any(i => !withReserved.Contains(i))) used = withReserved;
            }
            int slot = Enumerable.Range(0, Config.buildingSlots).First(i => !used.Contains(i));
            var b = new Building { id = s.NextId(), type = type, slot = slot, foundedMonth = s.month };
            s.buildings.Add(b);
            if (!s.foundedTypes.Contains(type))
            {
                s.foundedTypes.Add(type);
                if (s.month > 0)
                {
                    var by = AliveNotable();
                    Chronicle("first_building", NameData.FirstBuildingLine(type, by), 0.75f, by?.id ?? -1).buildingId = b.id;
                }
            }
            return b;
        }

        Notable AddNotable(float age, int parentId, string family)
        {
            var s = State;
            var rng = s.rng;
            var n = new Notable
            {
                id = s.NextId(), name = rng.Pick(NameData.Given), family = family ?? rng.Pick(NameData.Families),
                trait = rng.Pick(NameData.Traits), age = age, lifespan = rng.Range(Config.lifespanMin, Config.lifespanMax), parentId = parentId, bornMonth = s.month
            };
            // No two living people share a full name ("Ada Thorn was born to Ada Thorn" muddles recall).
            // Extra draws come from the story stream, so they never shift the main dice.
            for (int tries = 0; tries < 20 && s.notables.Any(o => o.alive && o.FullName == n.FullName); tries++)
                n.name = s.storyRng.Pick(NameData.Given);
            s.notables.Add(n);
            return n;
        }

        Notable AliveNotable()
        {
            var alive = State.notables.Where(n => n.alive && n.age >= 16f).ToList();
            return alive.Count == 0 ? null : State.rng.Pick(alive);
        }

        public ChronicleEntry Chronicle(string kind, string text, float salience, int notableId = -1)
        {
            var e = new ChronicleEntry { id = State.NextId(), month = State.month, kind = kind, text = text, salience = salience, notableId = notableId };
            State.chronicle.Add(e);
            Chronicled?.Invoke(e);
            return e;
        }
    }
}
