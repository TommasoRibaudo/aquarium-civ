using System;
using System.Collections.Generic;

namespace AquariumCiv.Prototypes.Sim
{
    /// <summary>Tunables shared by every prototype. Defaults are the design values from prototype-plan.md.</summary>
    [Serializable]
    public class SimConfig
    {
        public float yearsPerRealHour = 1f;     // 1 real hour = 1 game year
        public float notableAgeFactor = 0.5f;   // notables age at half speed
        public float messFullHours = 48f;       // mess reaches its cap after this many real hours
        public int trashMax = 12;
        public float trashPerPersonHour = 0.02f;
        public bool outputPenaltyEnabled = true;
        public float outputPenaltyMax = 0.25f;  // output drop at full mess
        public int buildingSlots = 20;
        public int startNotables = 5;
        public int minNotables = 5;
        public int maxNotables = 12;

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
        public float age;        // in notable-years (advances at notableAgeFactor per game year)
        public float lifespan;
        public bool alive = true;
        public int parentId = -1;
        public long bornMonth;
        public long diedMonth = -1;

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
        public long lastGrumbleMonth = -999;
        public long lastLeanYear = -1;
        public SimRandom rng;

        public float Year => month / (float)SimConfig.MonthsPerYear;
        public int NextId() => nextId++;
    }
}
