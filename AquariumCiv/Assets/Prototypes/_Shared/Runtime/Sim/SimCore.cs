using System;
using System.Linq;

namespace AquariumCiv.Prototypes.Sim
{
    /// <summary>
    /// Coarse aggregate civ sim. Pure C#, deterministic from the seed; one step = one game month.
    /// Live play and offline catch-up both go through Advance(), so they produce the same world.
    /// </summary>
    public class SimCore
    {
        public SimConfig Config { get; }
        public SimState State { get; }
        public event Action<ChronicleEntry> Chronicled;

        public SimCore(SimConfig config, SimState state) { Config = config; State = state; }

        public static SimState NewWorld(int seed, SimConfig config)
        {
            var s = new SimState { seed = seed, rng = new SimRandom(seed), population = 5, food = 20, wood = 4, mood = 0.7f };
            var core = new SimCore(config, s);
            core.AddBuilding(BuildingType.Camp);
            core.AddBuilding(BuildingType.Hut);
            for (int i = 0; i < config.startNotables; i++) core.AddNotable(s.rng.Range(18f, 40f), -1, null);
            core.Chronicle("founding", "Five wanderers settled in the glass and made camp.", 0.9f);
            return s;
        }

        // ---------- derived values ----------
        public float MessLevel => (State.grime + State.overgrowth + State.trash.Count / (float)Config.trashMax) / 3f;
        public float OutputPenalty => Config.outputPenaltyEnabled ? Config.outputPenaltyMax * MessLevel : 0f;
        public float OutputMultiplier => (0.8f + 0.4f * State.mood) * (1f - OutputPenalty);
        public int Housing => 4 + Count(BuildingType.Hut) * 4;
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

        public void Step()
        {
            var s = State;
            var rng = s.rng;
            s.month++;
            int season = (int)(s.month % SimConfig.MonthsPerYear);
            bool winter = season == 11 || season == 0 || season == 1;
            float mult = OutputMultiplier;
            int pop = s.population;

            // Economy
            float foodProd = (2f + pop * 1.0f + Count(BuildingType.Farm) * 2.5f + Count(BuildingType.Windmill) * 2f) * mult * (winter ? 0.5f : 1f);
            s.food = Math.Max(0f, s.food + foodProd - pop);
            s.wood += (1f + pop * 0.25f + Count(BuildingType.Lumbermill) * 2f) * mult;

            if (s.food > pop * 1.5f && pop < Housing && rng.Chance(0.25f)) s.population++;
            if (pop > 5 && rng.Chance(0.015f)) s.population--; // crowd old age

            float target = 0.75f - MessLevel * 0.5f + (s.food > pop * 2f ? 0.15f : 0f) - (s.food <= 0f ? 0.3f : 0f);
            s.mood += (Math.Clamp(target, 0f, 1f) - s.mood) * 0.1f;

            TryBuild();
            AccrueMess();
            if (season == 0) YearlyNotables();
            NarrativeChecks(winter);
        }

        void TryBuild()
        {
            var s = State;
            if (s.buildings.Count >= Config.buildingSlots) return;
            float cost = 10f + s.buildings.Count * 4f;
            if (s.wood < cost) return;
            int pop = s.population;
            string type;
            if (pop >= Housing - 1) type = BuildingType.Hut;
            else if (s.food < pop * 2f && Count(BuildingType.Farm) < 1 + pop / 6) type = BuildingType.Farm;
            else if (pop >= 6 && Count(BuildingType.Lumbermill) == 0) type = BuildingType.Lumbermill;
            else if (pop >= 8 && Count(BuildingType.Well) == 0) type = BuildingType.Well;
            else if (pop >= 12 && Count(BuildingType.Windmill) == 0) type = BuildingType.Windmill;
            else if (pop >= 15 && Count(BuildingType.Market) == 0) type = BuildingType.Market;
            else if (pop >= 18 && Count(BuildingType.Tavern) == 0) type = BuildingType.Tavern;
            else if (pop >= 24 && Count(BuildingType.Church) == 0) type = BuildingType.Church;
            else return; // nothing needed: save wood
            s.wood -= cost;
            AddBuilding(type);
        }

        void AccrueMess()
        {
            var s = State;
            float h = (float)Config.StepHours;
            s.grime = Math.Min(1f, s.grime + h / Config.messFullHours);
            s.overgrowth = Math.Min(1f, s.overgrowth + h / Config.messFullHours);
            if (s.trash.Count >= Config.trashMax) { s.trashAccum = 0; return; }
            s.trashAccum += s.population * Config.trashPerPersonHour * h;
            while (s.trashAccum >= 1f && s.trash.Count < Config.trashMax)
            {
                s.trashAccum -= 1f;
                s.trash.Add(new TrashItem
                {
                    id = s.NextId(), slot = s.rng.Range(0, Config.buildingSlots),
                    offsetX = s.rng.Range(-0.35f, 0.35f), offsetZ = s.rng.Range(-0.35f, 0.35f), month = s.month
                });
            }
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
                    Chronicle("coming_of_age", $"{n.FullName} came of age and took up their work.", 0.4f, n.id);
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

        void NarrativeChecks(bool winter)
        {
            var s = State;
            var rng = s.rng;
            int milestone = s.population / 10 * 10;
            if (milestone > s.lastPopMilestone && milestone > 0)
            {
                s.lastPopMilestone = milestone;
                Chronicle("milestone", $"The settlement grew to {milestone} people.", 0.7f);
            }
            long year = s.month / SimConfig.MonthsPerYear;
            if (winter && s.food < s.population && s.lastLeanYear != year)
            {
                s.lastLeanYear = year;
                Chronicle("lean_winter", "A lean winter: the granaries ran low and the people tightened their belts.", 0.5f);
            }
            if (!winter && s.food > s.population * 4f && rng.Chance(0.04f))
            {
                s.mood = Math.Min(1f, s.mood + 0.15f);
                var host = AliveNotable();
                Chronicle("festival", host != null
                    ? $"{host.FullName} threw a harvest festival. There was dancing until dawn."
                    : "The people held a harvest festival.", 0.65f, host?.id ?? -1);
            }
            if (MessLevel > 0.6f && s.month - s.lastGrumbleMonth > 12)
            {
                s.lastGrumbleMonth = s.month;
                Chronicle("grumble", "The people grumbled about the grimy glass and the heaps of junk.", 0.45f);
            }
        }

        // ---------- player actions ----------
        public void CleanGrime(float amount) => State.grime = Math.Max(0f, State.grime - amount);
        public void Prune(float amount) => State.overgrowth = Math.Max(0f, State.overgrowth - amount);
        public bool RemoveTrash(int id) => State.trash.RemoveAll(t => t.id == id) > 0;

        public void CleanAll()
        {
            State.grime = 0; State.overgrowth = 0; State.trash.Clear();
        }

        // ---------- helpers ----------
        Building AddBuilding(string type)
        {
            var s = State;
            var used = s.buildings.Select(b => b.slot).ToHashSet();
            int slot = Enumerable.Range(0, Config.buildingSlots).First(i => !used.Contains(i));
            var b = new Building { id = s.NextId(), type = type, slot = slot, foundedMonth = s.month };
            s.buildings.Add(b);
            if (!s.foundedTypes.Contains(type))
            {
                s.foundedTypes.Add(type);
                if (s.month > 0) Chronicle("first_building", NameData.FirstBuildingLine(type, AliveNotable()), 0.75f);
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
                trait = rng.Pick(NameData.Traits), age = age, lifespan = rng.Range(55f, 80f), parentId = parentId, bornMonth = s.month
            };
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
