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

        public SimCore(SimConfig config, SimState state)
        {
            Config = config; State = state;
            // A save from before placement (#32): every building gets a lot now, once (Sim/Settle migrates from slots).
            if (config.settle && state.settle != null && state.settle.version == 0 && state.buildings.Count > 0) _ = Settlement;
        }

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
            // Clamped into the camp's band (#26): a scene saved before the bands may still carry the old 5.
            int start = Math.Clamp(config.startNotables, config.campNotables.min, config.campNotables.max);
            for (int i = 0; i < start; i++) core.AddNotable(s.rng.Range(18f, 40f), -1, null);
            core.JobsStep(); // the founders arrive with their work (SimCore.Jobs.cs)
            core.Chronicle("founding", "A few wanderers settled in the glass and made camp.", 0.9f);
            core.RolesStep(); // the founders choose a chief (SimCore.Roles.cs)
            core.RecordYear();
            if (config.settle) _ = core.Settlement; // the camp and the first hut get their lots (Sim/Settle, #32)
            core.HistoryStep(); // year 0 for the time-lapse (SimCore.History.cs)
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

        /// <summary>True while <see cref="CatchUp"/> runs the months the player was away.</summary>
        public bool CatchingUp { get; private set; }

        /// <summary>
        /// Offline catch-up (game definition v0.2): the civ lives the first <see cref="SimConfig.fullSpeedHours"/> at full
        /// speed and the rest at <see cref="SimConfig.slowSpeed"/>; mess runs on real hours, so the real hours the civ didn't
        /// live are topped up as mess afterwards (capped like natural mess). Live play keeps calling <see cref="Advance"/>.
        /// </summary>
        public CatchUpResult CatchUp(double realHoursAway)
        {
            var r = new CatchUpResult { realHours = Math.Max(0, realHoursAway), simHours = Config.OfflineSimHours(realHoursAway) };
            CatchingUp = true; // no relics are found while the player is away (SimCore.Relics.cs)
            try { r.months = Advance(r.simHours); }
            finally { CatchingUp = false; }
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
            float target = 0.75f + (s.food > pop * 2f ? 0.15f : 0f) - (s.food <= 0f ? 0.3f : 0f) + FestivalMoodLift; // era festival (SimCore.EraMoment.cs)
            s.mood += (Math.Clamp(target, 0f, 1f) - s.mood) * 0.1f;

            TryBuild();
            if (s.nudges.Count > 0) NudgeStep();
            if (s.firstRun.active) FirstRunStep(); // opt-in, no dice: the first rain is answered inside the first year (SimCore.FirstRun.cs, #94)
            AccrueMess((float)Config.StepHours);
            if (season == 0) YearlyNotables();
            NarrativeChecks(winter, festivalRoll);
            StoryStep(season, winter); // draws only from its own stream (SimCore.Story.cs)
            if (season == 0) RecordYear();
            EraStep(); // no dice: only marks the next era ready (SimCore.Eras.cs)
            JobsStep(); // no dice: everyday jobs follow the era and the buildings (SimCore.Jobs.cs)
            RolesStep(); // no dice: important roles, contests and succession (SimCore.Roles.cs)
            OfficeStep(); // no dice: the holders' traits colour their terms, story only (SimCore.Office.cs)
            ShapingStep(); // no dice: charges refill while the people are happy (SimCore.Shaping.cs)
            WishStep(); // no dice: a notable sometimes wishes for a gift (SimCore.Wishes.cs)
            CareStep(); // no dice: the first sweepers make the chronicle (SimCore.Care.cs)
            RelicsStep(); // no dice: the Keeper Doll, the Lens, quotes (SimCore.Relics.cs)
            CardsStep(); // no dice: teaser threads, story links (SimCore.Cards.cs)
            SettleStep(); // no dice: where the new buildings go, paths wear in (SimCore.Settle.cs, Sim/Settle)
            PeakStep(); // no dice: leaders for the book, the town's peak (SimCore.EraMoment.cs)
            HistoryStep(); // no dice: records for the time-lapse and the book (SimCore.History.cs)
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
            // The twenty slots can't hold every farm a growing town would sow plus its own buildings: from the third farm on, a
            // farm waits while the town's workshop, guildhall or shops are still wanted (#114), so the box keeps room for them.
            if (Wants(BuildingType.Farm, Count(BuildingType.Farm) < Math.Min(MaxFarms, 1 + pop / 8) && !(Count(BuildingType.Farm) >= 3 && TownWant(pop) != null))) type = BuildingType.Farm;
            else if (Wants(BuildingType.Hut, pop >= Housing - 1)) type = BuildingType.Hut;
            else if (Wants(BuildingType.Lumbermill, pop >= 6 && Count(BuildingType.Lumbermill) == 0)) type = BuildingType.Lumbermill;
            else if (Wants(BuildingType.Well, pop >= 8 && Count(BuildingType.Well) == 0)) type = BuildingType.Well;
            else if (Wants(BuildingType.Windmill, pop >= 12 && Count(BuildingType.Windmill) == 0)) type = BuildingType.Windmill;
            else if (Wants(BuildingType.Market, pop >= 15 && Count(BuildingType.Market) == 0)) type = BuildingType.Market;
            else if (Wants(BuildingType.Tavern, pop >= 18 && Count(BuildingType.Tavern) == 0)) type = BuildingType.Tavern;
            else if (Wants(BuildingType.Church, pop >= 24 && Count(BuildingType.Church) == 0)) type = BuildingType.Church;
            else if (TownWant(pop) is string town) type = town;
            else return; // nothing needed: save wood
            s.wood -= cost;
            AddBuilding(type);
        }

        /// <summary>Slots the town's own buildings leave free for shaping answers (#114). None: a full box is the goal, and a shaping in a full town becomes a harvest line.</summary>
        const int TownSlotsKept = 0;

        /// <summary>What the town wants once the essentials stand (#114), or null. Order is the order of need: the market's
        /// shops, the first workshop (even with no shaping), the guildhall, then more workshops, one per 14 people. The bakery (#112) comes right after the first workshop. Stops short of the cap so a shaping can still be answered with a building.</summary>
        string TownWant(int pop)
        {
            if (State.era < Era.Town || State.buildings.Count >= Config.buildingSlots - TownSlotsKept) return null;
            bool market = Count(BuildingType.Market) > 0;
            if (Wants(BuildingType.Workshop, pop >= 20 && Count(BuildingType.Workshop) == 0)) return BuildingType.Workshop;
            if (Wants(BuildingType.Bakery, pop >= 20 && Count(BuildingType.Bakery) == 0 && (Count(BuildingType.Farm) > 0 || Count(BuildingType.Windmill) > 0))) return BuildingType.Bakery; // #112
            if (Wants(BuildingType.Guildhall, pop >= 24 && market && Count(BuildingType.Guildhall) == 0)) return BuildingType.Guildhall;
            if (Wants(BuildingType.Shop, market && pop >= 22 && Count(BuildingType.Shop) < Math.Min(MaxShops, 1 + (pop - 22) / 8))) return BuildingType.Shop;
            if (Wants(BuildingType.Workshop, pop >= 34 && Count(BuildingType.Workshop) < Math.Min(MaxWorkshops, 1 + (pop - 20) / 14))) return BuildingType.Workshop;
            return null;
        }

        const int MaxShops = 3, MaxWorkshops = 3, MaxFarms = 4; // (the fifth farm of a 30-person town left no slot for a guildhall)

        /// <summary>Mess for <paramref name="h"/> real hours, capped (48 h fills it).</summary>
        void AccrueMess(float h)
        {
            var s = State;
            s.grime = Math.Min(1f, s.grime + h / Config.messFullHours);
            float trim = TrimPerHour; // town sweepers weed and trim (SimCore.Care.cs); 0 outside the town
            if (trim > 0f) s.overgrowth = Math.Clamp(s.overgrowth + (1f / Config.messFullHours - trim) * h, 0f, 1f);
            else s.overgrowth = Math.Min(1f, s.overgrowth + h / Config.messFullHours);
            if (Sweepers > 0) Sweep(h); // ... and take the oldest heaps to the midden; never the glass
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
            var t = new TrashItem
            {
                id = s.NextId(), slot = s.rng.Range(0, Config.buildingSlots),
                offsetX = s.rng.Range(-0.35f, 0.35f), offsetZ = s.rng.Range(-0.35f, 0.35f), month = s.month
            };
            s.trash.Add(t);
            Settlement?.TrashSpot(t); // where it lies: from the drawn slot and offsets, no new dice
        }

        /// <summary>The adult-notable band of the current era (#26).</summary>
        public NotableBand NotablesWanted => Config.NotablesIn(State.era);

        public const float AdultAge = 16f, ParentMinAge = 20f, ParentMaxAge = 40f;
        const float BirthChance = 0.12f;

        /// <summary>Living notables of 16 and over: the ones the era's band counts.</summary>
        public int AdultNotables => State.notables.Count(n => n.alive && n.age >= AdultAge);

        /// <summary>Is there room for another notable child? Births pause while the adults fill the era's max; children
        /// don't count against it, but everyone alive stays within the max + <see cref="SimConfig.familyRoom"/>.</summary>
        public bool RoomForABirth
        {
            get
            {
                var band = NotablesWanted;
                return AdultNotables < band.max && State.notables.Count(n => n.alive) < band.max + Config.familyRoom;
            }
        }

        void YearlyNotables()
        {
            var s = State;
            var rng = s.rng;
            var band = NotablesWanted;
            foreach (var n in s.notables.Where(n => n.alive).ToList())
            {
                float before = n.age;
                n.age += Config.notableAgeFactor;
                if (before < AdultAge && n.age >= AdultAge)
                    Chronicle("coming_of_age", ComingOfAgeLine(n), 0.4f, n.id);
                if (n.age >= n.lifespan)
                {
                    n.alive = false;
                    n.diedMonth = s.month;
                    Chronicle("death", $"{n.FullName}, {n.trait}, died of old age at {Math.Floor(n.age)}.", 0.85f, n.id);
                }
            }
            // Births: a notable's child is a notable from birth and grows up one (never back into the crowd). Families
            // with fewer children come first: parents try in order of children born (then id, so it stays deterministic),
            // and each child already born lowers the chance of another, so the room goes round and more lines carry on.
            var parents = s.notables.Where(n => n.alive && n.age >= ParentMinAge && n.age <= ParentMaxAge)
                .Select(n => (n, kids: s.notables.Count(c => c.parentId == n.id)))
                .OrderBy(x => x.kids).ThenBy(x => x.n.id).ToList();
            foreach (var (p, kids) in parents)
            {
                if (!RoomForABirth) break;
                if (!rng.Chance(BirthChance / (1 + kids))) continue;
                // A couple's children share one family name, whichever of them is born to it (#61: full siblings came out
                // Gale and Hearth); the line names both parents.
                var partner = Alive(p.partnerId);
                var child = AddNotable(0f, p.id, HouseOf(p));
                Chronicle("birth", partner != null
                    ? $"{child.FullName} was born to {p.FullName} and {partner.FullName}."
                    : $"{child.FullName} was born to {p.FullName}.", 0.6f, child.id);
            }
            // Below the era's band (adults), one notable a year comes out of the crowd: after a death someone rises to
            // take their place; when a new era wants more people than the last one did, a newcomer arrives. Not while the
            // family room is full: the children growing up there will fill the band.
            int adults = AdultNotables;
            if (adults < band.min && s.notables.Count(n => n.alive) < band.max + Config.familyRoom)
            {
                var n = AddNotable(rng.Range(18f, 30f), -1, null);
                bool growing = s.era > Era.Camp && adults >= Config.NotablesIn(s.era - 1).min;
                if (growing) Chronicle("newcomer", $"A newcomer, {n.FullName}, {n.trait}, joined the {EraNoun(s.era)}.", 0.55f, n.id);
                else Chronicle("rise", $"{n.FullName}, {n.trait}, rose to prominence among the people.", 0.55f, n.id);
            }
        }

        /// <summary>The family name a notable's children take. A couple's first child takes the rarer of their two names
        /// (fewer living notables bear it; the lower id on a tie), and the brothers and sisters after take the same, so a
        /// couple's children share one name and the box keeps a few names going instead of one swallowing the rest. A single
        /// parent's children take their name.</summary>
        public string HouseOf(Notable p)
        {
            var q = FindNotable(p.partnerId);
            if (q == null) return p.family;
            var wed = State.chronicle.LastOrDefault(e => e.kind == "wedding" && Pair(e, p.id, q.id));
            long since = wed?.month ?? long.MinValue;
            var first = State.notables.FirstOrDefault(c => (c.parentId == p.id || c.parentId == q.id) && c.bornMonth >= since &&
                                                           (c.family == p.family || c.family == q.family));
            if (first != null) return first.family;
            int Bearers(string f) => State.notables.Count(n => n.alive && n.family == f);
            int bp = Bearers(p.family), bq = Bearers(q.family);
            return bp < bq ? p.family : bq < bp ? q.family : (p.id < q.id ? p.family : q.family);
        }

        static string EraNoun(Era era) => era switch { Era.Camp => "camp", Era.Village => "village", _ => "town" };

        void NarrativeChecks(bool winter, float festivalRoll)
        {
            var s = State;
            int milestone = s.population / 10 * 10;
            if (milestone > s.lastPopMilestone && milestone > 0)
            {
                s.lastPopMilestone = milestone;
                Chronicle("milestone", $"{Place} grew to {milestone} people.", 0.7f); // the name once it has one (#61)
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
                    ? string.Format(Nth(FestivalLines, Told("festival", host.id) + host.id), host.FullName) // told in turn (#61)
                    : "The people held a harvest festival.", 0.65f, host?.id ?? -1);
            }
        }

        // ---------- player actions ----------
        public void CleanGrime(float amount)
        {
            float was = State.grime;
            if (amount > 0f && was > 0f) CountKeeperAction(KeeperAction.Wipe, true); // the Keeper Doll's prop (SimCore.Relics.cs)
            State.grime = Math.Max(0f, was - amount);
            CleaningRefill((was - State.grime) * Config.shapingPerGrime);
        }

        public void Prune(float amount)
        {
            float was = State.overgrowth;
            State.overgrowth = Math.Max(0f, was - amount);
            CleaningRefill((was - State.overgrowth) * Config.shapingPerOvergrowth);
            RelicsOnPrune(was - State.overgrowth); // the Left Sandal turns up under the weeds (SimCore.Relics.cs)
        }

        public bool RemoveTrash(int id)
        {
            bool gone = State.trash.RemoveAll(t => t.id == id) > 0;
            if (gone) { CleaningRefill(Config.shapingPerTrash); CountKeeperAction(KeeperAction.Trash, true); }
            return gone;
        }

        /// <summary>Debug / lab: add mess directly (capped like natural mess).</summary>
        public void AddMess(float grime, float overgrowth, int trashCount)
        {
            State.grime = Math.Clamp(State.grime + grime, 0f, 1f);
            State.overgrowth = Math.Clamp(State.overgrowth + overgrowth, 0f, 1f);
            for (int i = 0; i < trashCount && State.trash.Count < Config.trashMax; i++) AddTrash();
        }

        public void CleanAll()
        {
            CleaningRefill(State.grime * Config.shapingPerGrime + State.overgrowth * Config.shapingPerOvergrowth + State.trash.Count * Config.shapingPerTrash);
            State.grime = 0; State.overgrowth = 0; State.trash.Clear();
        }

        // ---------- helpers ----------
        Building AddBuilding(string type, bool tellFirst = true)
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
                if (s.month > 0 && tellFirst) // (the era's own well is told by the festival line: no extra draw)
                {
                    var by = AliveNotable();
                    Chronicle("first_building", NameData.FirstBuildingLine(type, by, s.era), 0.75f, by?.id ?? -1).buildingId = b.id;
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
            MakeNameUnique(n, s.storyRng);
            s.notables.Add(n);
            return n;
        }

        /// <summary>No two living notables share a given name, and nobody (living or dead) repeats a full name ("Moss Thorn and
        /// Moss Cole" muddles recall; "Dov Quarry" must not come back). Retries come from <paramref name="r"/> (never the main
        /// dice); if the luck runs out, a deterministic scan of the table finishes the job.</summary>
        void MakeNameUnique(Notable n, SimRandom r)
        {
            var s = State;
            bool Clash(string given) => s.notables.Any(o => o != n && (o.alive && o.name == given || o.name == given && o.family == n.family));
            for (int tries = 0; tries < 30 && Clash(n.name); tries++) n.name = r.Pick(NameData.Given);
            if (!Clash(n.name)) return;
            int start = System.Array.IndexOf(NameData.Given, n.name);
            for (int i = 1; i <= NameData.Given.Length; i++)
            {
                string g = NameData.Given[(Math.Max(start, 0) + i) % NameData.Given.Length];
                if (!Clash(g)) { n.name = g; return; }
            }
        }

        Notable AliveNotable()
        {
            var alive = State.notables.Where(n => n.alive && n.age >= 16f).ToList();
            return alive.Count == 0 ? null : State.rng.Pick(alive);
        }

        public ChronicleEntry Chronicle(string kind, string text, float salience, int notableId = -1) => Record(kind, text, salience, notableId, -1);

        /// <summary>Adds an entry about one or two people (the second for a wedding, a feud...).</summary>
        ChronicleEntry Record(string kind, string text, float salience, int notableId, int otherId)
        {
            text = TermCoda(kind, text, notableId, otherId); // a role-holder's clause on a few story lines (SimCore.Office.cs)
            var e = new ChronicleEntry { id = State.NextId(), month = State.month, kind = kind, text = text, salience = salience, notableId = notableId, otherNotableId = otherId };
            State.chronicle.Add(e);
            Chronicled?.Invoke(e);
            return e;
        }
    }
}
