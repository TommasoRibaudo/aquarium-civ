using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// Tunables for the slice's world. Ported from the prototypes with the A/B switches resolved by the gates
    /// (decisions.md 2026-10-29): mess slows the work pace (Gate 1, at most −25 % at the 48 h cap) but never touches the
    /// people's mood; story events and yearly snapshots are always on (Gate 2).
    /// </summary>
    [Serializable]
    public class SimConfig
    {
        public float yearsPerRealHour = 1f;     // 1 real hour = 1 game year (Gate 2)
        public float notableAgeFactor = 1f;     // notables age with the calendar (user, 2026-10-29: half-speed aging confused people)
        // Life stages. For the slice notables live a normal lifespan, as in the prototypes (user, 2026-10-29: "for this
        // playthrough make the notables live like the others, for the final game we will expand it"). Adulthood (16),
        // parenthood (20–40) and marriage (≤ 50) are fixed in the sim.
        // Planned final-game profile (decisions.md 2026-10-29 "Notable aging" / "Life stages": "Only stretch elderly"):
        // lifespan 110–160, elderAge 90, eldestStoryAge 120, masterAge 50.
        public float lifespanMin = 55f;         // a notable dies of old age at a lifespan drawn from [min, max)
        public float lifespanMax = 80f;
        public float masterAge = 50f;           // old enough to take an apprentice (user, 2026-10-29: "apprentices 50")
        public float elderAge = 45f;            // elders
        public float eldestStoryAge = 60f;      // the eldest tells the founding story in winter from this age
        public float retireAge = 65f;           // a notable leaves their everyday job and becomes an elder (user, 2026-10-29: "retirement at 65 fine")
        public float messFullHours = 48f;       // mess reaches its cap after this many real hours
        public int trashMax = 12;
        public float trashPerPersonHour = 0.02f;
        public float outputPenaltyMax = 0.25f;  // work-pace drop at full mess (Gate 1)
        // Care moves up (#60, life-in-the-box §7, choice 6 ★): in the town, sweepers really clear trash heaps and trim
        // overgrowth, slowly, in real hours like the mess. Never the glass. However many there are, they clear at most
        // careMaxShare of what the mess brings in, so mess still grows and the Keeper always has care to do.
        public int peoplePerCrowdSweeper = 12;   // the crowd puts one sweeper out per this many people (§5 "Town care")
        public float sweepHeapsPerHour = 0.1f;   // trash heaps one sweeper clears per real hour (one every 10 h)
        public float trimPerHour = 0.005f;       // overgrowth one sweeper trims per real hour (mess brings 1/48 ≈ 0.021)
        public float careMaxShare = 0.5f;        // all sweepers together clear at most this share of the incoming trash/overgrowth
        public int buildingSlots = 20;          // the economy's building cap (where they stand is Settle's: Building.x/z, lotId)
        // Organic placement (M2, #32; Sim/Settle): every building gets a lot, paths wear in from walking. Off only for
        // tests that compare the economy with and without it. terrainPreset picks the box's land (0 = the style kit's).
        public bool settle = true;
        public int terrainPreset = 0;
        // Named notables per era (game definition §4, #26): about 2–4 in the camp, 4–8 in the village, up to 12 in the
        // town. The band counts adults (16+). Births pause while the adults are at the era's max; children under 16 don't
        // count against it, up to familyRoom of them over the max, so notable families go on across generations (user,
        // 2026-10-29: "notables give birth to people that will become notables"). Below the min the crowd fills in, one
        // notable a year (SimCore.YearlyNotables).
        public int familyRoom = 2;              // living notables (children included) never exceed the era's max + this
        public int startNotables = 3;           // fits the camp's band
        public NotableBand campNotables = new NotableBand { min = 2, max = 4 };
        public NotableBand villageNotables = new NotableBand { min = 4, max = 8 };
        public NotableBand townNotables = new NotableBand { min = 8, max = 12 };
        public float storyChancePerMonth = 0.07f;
        public double fullSpeedHours = 8;       // offline: the first hours away run at full speed (game definition v0.2)
        public double slowSpeed = 0.25;         // ... and the rest at this fraction (a weekend away ≈ 18 game years)
        // Era thresholds (game definition §8): all three must hold; years count from the start of the current era.
        // Over 100 seeds, watched: the village is ready at a median of year 15 (p90 34), the town ~43 years after it.
        public EraThreshold villageAt = new EraThreshold { population = 10, buildings = 6, years = 10 };
        public EraThreshold townAt = new EraThreshold { population = 22, buildings = 13, years = 20 };

        public const int MonthsPerYear = 12;
        public double StepHours => 1.0 / MonthsPerYear / yearsPerRealHour;

        /// <summary>Sim hours the civ lives through for a real absence: full speed, then the slow tail.</summary>
        public static double OfflineSimHours(double realHoursAway, double fullSpeedHours, double slowSpeed)
        {
            if (realHoursAway <= 0) return 0;
            return realHoursAway <= fullSpeedHours ? realHoursAway : fullSpeedHours + (realHoursAway - fullSpeedHours) * slowSpeed;
        }

        public double OfflineSimHours(double realHoursAway) => OfflineSimHours(realHoursAway, fullSpeedHours, slowSpeed);

        /// <summary>How many living notables an era wants.</summary>
        public NotableBand NotablesIn(Era era) => era switch
        {
            Era.Camp => campNotables,
            Era.Village => villageNotables,
            _ => townNotables,
        };
    }

    /// <summary>The adult-notable count an era wants: the crowd fills up to <see cref="min"/>, births pause at <see cref="max"/>
    /// (children get <see cref="SimConfig.familyRoom"/> on top).</summary>
    [Serializable]
    public class NotableBand
    {
        public int min;
        public int max;
    }

    /// <summary>What a box needs to be ready for its next era. Reaching it only makes the era ready; it waits for the player.</summary>
    [Serializable]
    public class EraThreshold
    {
        public int population;
        public int buildings;
        public int years;   // game years spent in the current era
    }

    /// <summary>What an offline catch-up did: real hours away, sim hours the civ lived, game months stepped.</summary>
    public struct CatchUpResult
    {
        public double realHours, simHours;
        public int months;
        public bool Slowed => simHours < realHours;
    }

    /// <summary>xorshift32 with serializable state, so catch-up and live stepping produce identical worlds.</summary>
    [Serializable]
    public class SimRandom
    {
        public uint state;

        public SimRandom() { }
        public SimRandom(int seed) { state = (uint)seed * 2654435761u; if (state == 0) state = 0x9E3779B9u; }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            return state = x;
        }

        public float Value() => (NextUInt() >> 8) * (1f / 16777216f);
        public int Range(int minInclusive, int maxExclusive) => minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        public float Range(float min, float max) => min + (max - min) * Value();
        public bool Chance(float p) => Value() < p;
        public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];
    }

    /// <summary>The three eras of a box (game definition §8). Saved as an int, so the order is fixed.</summary>
    public enum Era { Camp, Village, Town }

    public static class BuildingType
    {
        public const string Camp = "Camp", Hut = "Hut", Farm = "Farm", Lumbermill = "Lumbermill", Well = "Well",
            Windmill = "Windmill", Market = "Market", Tavern = "Tavern", Church = "Church";
        // Industrious town: stubs, nothing builds them yet.
        public const string Workshop = "Workshop", Guildhall = "Guildhall";

        /// <summary>The era a type first appears in; a box builds it from that era on. Types are roles, not looks: the
        /// view dresses a camp's Farm as gathering grounds and its Lumbermill as a woodpile.</summary>
        public static Era EraOf(string type) => type switch
        {
            Camp or Hut or Farm or Lumbermill => Era.Camp,
            Well or Windmill or Market or Tavern or Church => Era.Village,
            Workshop or Guildhall => Era.Town,
            _ => Era.Camp,
        };
    }

    [Serializable]
    public class Building
    {
        public int id;
        public string type;
        /// <summary>Legacy (prototypes, saves before #32): the hex slot index. Still assigned as before so the economy
        /// and old views keep working; where the building stands is <see cref="x"/>, <see cref="z"/> and its lot.</summary>
        public int slot;
        public long foundedMonth;
        // Placement (Sim/Settle, #32): set when the building gets its lot; lotId 0 = still waiting for a good lot.
        public float x, z, yaw, w, d;   // lot centre, facing (degrees; the front is local −z), size along / back from the front
        public int lotId;
        public int yardId;              // the family yard a home stands round (0 = none)
        public float doorX, doorZ;      // just outside the front, on the path network
    }

    [Serializable]
    public class TrashItem
    {
        public int id;
        public int slot;                // drawn by the economy's dice (kept, so placement never shifts them)
        public float offsetX, offsetZ;
        public long month;
        public float x, z;              // where it lies (Settle: by the door of the lot its slot picks)
    }

    [Serializable]
    public class Notable
    {
        public int id;
        public string name;
        public string family;
        public string trait;
        public float age;        // in years (advances at notableAgeFactor per game year)
        public float lifespan;
        public bool alive = true;
        public int parentId = -1;
        public long bornMonth;
        public long diedMonth = -1;
        // Story links; -1 = none.
        public int partnerId = -1;
        public int friendId = -1;
        public int rivalId = -1;
        public int mentorId = -1;
        public string job = "";  // everyday job (JobType; SimCore.Jobs.cs): "" as a child, "elder" when retired

        public string FullName => $"{name} {family}";
    }

    [Serializable]
    public class ChronicleEntry
    {
        public int id;
        public long month;
        public string kind;
        public string text;
        public float salience;
        public int notableId = -1;
        public int otherNotableId = -1; // a second person in the story (wedding, rivalry...)
        public int buildingId = -1;     // the building it happened at, for fly-to
        public bool keeper;             // the people acted on a Keeper's nudge
        public int threadId = -1;       // its StoryThread (SimCore.Cards.cs): -1 = not looked at yet, 0 = none
    }

    /// <summary>One of the Keeper's land shapings (rain, a grove, a channel; Gate 3).</summary>
    [Serializable]
    public class Nudge
    {
        public int id;
        public string kind;
        public int slot;            // legacy (prototypes): the slot it was placed near
        public float x, z;          // the shaped spot (#32)
        public long month;
        public bool credited;       // the people answered it
        public long answerMonth = -1;
        public int buildingId = -1;
        public int lotId;           // the answer's lot (Settle; 0 = none yet)
        public bool reported;       // the view has shown and logged the answer
        public int answerAfter = 8; // game months before the people act (varies, so answers don't feel mechanical)
        public long placedUnixS;    // real time placed, for "time to visible effect"
    }

    /// <summary>One game year, compact: enough to replay the village growing in the time-lapse.</summary>
    [Serializable]
    public class YearSnapshot
    {
        public int year;
        public int population;
        public int buildings;
        public int notables;
        public float mood;
        public Era era;
    }

    [Serializable]
    public class SimState
    {
        public int seed;
        public double simHours;     // total simulated real-hours since the world began
        public long month;          // game months stepped
        public int population;
        public float food, wood, mood;
        public float grime, overgrowth, trashAccum;
        public float sweepAccum;    // town sweepers' progress toward the next heap, 0..1 (SimCore.Care.cs)
        public List<TrashItem> trash = new();
        public List<Building> buildings = new();
        public List<Notable> notables = new();
        public List<ChronicleEntry> chronicle = new();
        public List<string> foundedTypes = new();
        public int nextId = 1;
        public int lastPopMilestone;
        public long lastLeanYear = -99;
        public SimRandom rng;
        /// <summary>Births, deaths and festivals: drawn every month whether used or not, so the economy's dice never
        /// depend on what else happened.</summary>
        public SimRandom econRng;
        /// <summary>Story events and nudge answers draw only from here, so they never shift the economy's dice.</summary>
        public SimRandom storyRng;
        public List<string> storyFlags = new();
        public string settlementName = "";
        public List<YearSnapshot> years = new();
        public int pinnedNotableId = -1;
        public List<Nudge> nudges = new();
        public Era era;                 // the box's era; moves only through SimCore.AdvanceEra
        public bool eraReady;           // the next era's threshold was reached: it waits for the player to watch
        public long eraStartMonth;      // month the current era began
        public long eraReadyMonth = -1; // month eraReady was set (-1 = not ready)
        public List<RoleSeat> roles = new(); // the important roles and their holders (SimCore.Roles.cs, #27)
        public int termLineNextId = SimCore.TermLineIdBase; // term lines' own ids (SimCore.Office.cs, #28), so the shared ids never shift
        public List<StoryThread> threads = new(); // teaser threads and story links (SimCore.Cards.cs, #59)
        public int threadNextId = 1;    // threads' own ids, so the shared ids never shift
        public int teaserThreadId = -1; // the evening talk's latest teaser (-1 = none yet)
        public int glassStage = 1;      // how the people understand the glass, 1–3 (never below the era's own; SimCore.Cards.cs)
        /// <summary>Where everything stands: lots, family yards, paths, the Keeper's marks on the land (Sim/Settle, #32).</summary>
        public SettleState settle = new();

        public float Year => month / (float)SimConfig.MonthsPerYear;
        public int NextId() => nextId++;
    }
}
