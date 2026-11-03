using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// The saved layers of the ecology (SimState.eco): cover, soil and moisture per site cell, the box's air and glass,
    /// the water table, the Keeper's tree, the animals, the gates. Plain fields and flat arrays so JsonUtility round-trips
    /// it; an old save has none of this and loads with <see cref="on"/> false. No dice are stored: every chance is a hash
    /// of (seed, cell, season) (EcoModel), so the ecology never draws from rng, econRng or storyRng.
    /// </summary>
    [Serializable]
    public class EcoState
    {
        public bool on;
        public int seed;                // keys the hash dice
        public int season;              // ecology steps done (one a season); season % 4: 0 spring .. 3 winter

        public byte[] cover;            // EcoCover per cell
        public float[] soil, moisture;
        /// <summary>#266, per cell, 0..1. wet = how wet the ground LOOKS (the mist's wet area; dries within a few game months);
        /// puddle = water standing on soaked low ground (a drinking source while there is no pond); sprout = first shoots on
        /// bare ground, 1 = it turns to grass. Null in an old save (EcoModel makes them).</summary>
        public float[] wet, puddle, sprout;
        public float humidity, grime, droplets;
        /// <summary>The water table (terrain height units), its peak so far, and the pond's level (-1000 = no pond yet).</summary>
        public float table, tablePeak, pondLevel = -1000f;
        public float soaked;            // water the Keeper's mist has sent down since the last step
        public float rabbits, birds;
        /// <summary>#282: the cells where rabbit tracks show (chosen once, at the meadow's edge, when the tracks come), the cell
        /// the first rabbit hops in at (-1 = none yet), whether the view has played that arrival, and rabbits caught so far.</summary>
        public int[] trackCells;
        public int rabbitArrivalCell = -1;
        public bool rabbitArrivalSeen;
        public int rabbitsCaught;
        public int wildSeeds;
        // The cover counts taken at the end of the last step. The model reads these mid-step (as the lab's did), so they
        // are saved rather than recounted: a mist between steps must not change what the next step sees.
        public int cGrass, cTall, cBush, cSeedlings, cYoung, cTrees, cReeds, cPond;

        // The Keeper's one tree (keepers-hands 4). Planted when treeCell >= 0.
        public int treeCell = -1;
        public float treeProgress, treeWeeds;
        public int treeStage;           // 0 seed, 1 sprout, 2 sapling, 3 young, 4 grown
        public int keeperMistings;      // the people learn from these
        public bool treeWilting;

        // Gates: the game month each opened (-1 = closed).
        public int campGateMonth = -1, villageGateMonth = -1;

        // P8 #288: the bushes. The first one that came up (a sign the people notice; -1 = none yet) and what has been picked:
        // parallel lists, cell / the view day of the last pick / berries left (0..1) at that day (see EcoBushes).
        public int firstBushCell = -1;
        public List<int> bushPickCell = new List<int>();
        public List<long> bushPickDay = new List<long>();
        public List<float> bushPickLeft = new List<float>();

        /// <summary>Milestones reached and the game month of each (parallel lists; see EcoMilestones).</summary>
        public List<string> milestoneKeys = new List<string>();
        public List<long> milestoneMonths = new List<long>();

        public long MonthOf(string key)
        {
            int i = milestoneKeys.IndexOf(key);
            return i < 0 ? -1 : milestoneMonths[i];
        }

        public bool Reached(string key) => milestoneKeys.Contains(key);
    }

    /// <summary>The milestone keys of <see cref="SimCore.EcoMilestone"/>: one beat each of the land's arc.</summary>
    public static class EcoMilestones
    {
        public const string FirstGreen = "first_green", FirstBush = "first_bush", FirstRabbit = "first_rabbit", RabbitTracks = "rabbit_tracks", FirstBird = "first_bird",
            SeedUnlocked = "seed_unlocked", TreePlanted = "tree_planted", PeopleWater = "people_water",
            TreeSprout = "tree_sprout", TreeSapling = "tree_sapling", TreeYoung = "tree_young", TreeGrown = "tree_grown",
            FirstWoodTree = "first_grown_tree", WaterSurfaced = "water_surfaced", CampGate = "camp_gate", VillageGate = "village_gate";

        /// <summary>The tree's stage milestones, indexed by stage - 1.</summary>
        public static readonly string[] TreeStages = { TreeSprout, TreeSapling, TreeYoung, TreeGrown };
    }

    /// <summary>What one spot of the box is like (SimCore.EcoAt).</summary>
    public struct EcoCell
    {
        public int cell;
        public EcoCover cover;
        public float moisture, soil, height;
        public bool belowFloodLine;     // a hollow the water can reach: nobody builds here
    }

    /// <summary>The Keeper's tree and what it needs (SimCore.EcoTree).</summary>
    public struct EcoTreeInfo
    {
        public bool planted, grown, wilting;
        public int cell, stage;         // stage 0 seed, 1 sprout, 2 sapling, 3 young, 4 grown
        public float x, z;
        public float progress;          // toward the next stage's threshold: see stageProgress
        public float stageProgress;     // 0..1 within the current stage
        public float water, light, weeds; // 0..1: how wet its foot is, how much light reaches it, how overgrown
        public int keeperMistings;
    }
}
