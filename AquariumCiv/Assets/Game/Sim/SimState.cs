using System;
using System.Collections.Generic;

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
        public float messFullHours = 48f;       // mess reaches its cap after this many real hours
        public int trashMax = 12;
        public float trashPerPersonHour = 0.02f;
        public float outputPenaltyMax = 0.25f;  // work-pace drop at full mess (Gate 1)
        public int buildingSlots = 20;
        public int startNotables = 5;
        public int minNotables = 5;
        public int maxNotables = 12;
        public float storyChancePerMonth = 0.07f;

        public const int MonthsPerYear = 12;
        public double StepHours => 1.0 / MonthsPerYear / yearsPerRealHour;
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

    public static class BuildingType
    {
        public const string Camp = "Camp", Hut = "Hut", Farm = "Farm", Lumbermill = "Lumbermill", Well = "Well",
            Windmill = "Windmill", Market = "Market", Tavern = "Tavern", Church = "Church";
    }

    [Serializable]
    public class Building
    {
        public int id;
        public string type;
        public int slot;
        public long foundedMonth;
    }

    [Serializable]
    public class TrashItem
    {
        public int id;
        public int slot;
        public float offsetX, offsetZ;
        public long month;
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
    }

    /// <summary>One of the Keeper's land shapings (rain, a grove, a channel; Gate 3).</summary>
    [Serializable]
    public class Nudge
    {
        public int id;
        public string kind;
        public int slot;
        public long month;
        public bool credited;       // the people answered it
        public long answerMonth = -1;
        public int buildingId = -1;
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

        public float Year => month / (float)SimConfig.MonthsPerYear;
        public int NextId() => nextId++;
    }
}
