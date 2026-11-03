using System;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// Every number the land model is tuned by, in one place (ported from the Ecology Lab's Tuning.cs, #228; the rounds
    /// that set them are in docs/design/spikes/ecology-lab.md §5-6). All [est]. Lives on <see cref="SimConfig.eco"/>.
    /// </summary>
    [Serializable]
    public class EcoTuning
    {
        // Seasons: spring, summer, autumn, winter.
        public float[] growSeason = { 1.25f, 1f, 0.6f, 0.08f };    // how much cover may move this season
        public float[] treeSeason = { 1.5f, 1f, 0.55f, 0f };       // the one tree: a spurt in spring, dormant in winter
        public float[] drySeason = { 0.18f, 0.30f, 0.15f, 0.07f };  // share of moisture lost per season (before humidity)

        // Moisture
        public float dew = 0.025f, dewHumid = 0.035f;             // dew per season (+ more when the air is humid)
        public float showerChance = 0.14f, showerAmount = 0.40f, showerRadius = 0.9f; // a rare passing shower
        public float drainShare = 0.22f, drainFloor = 0.25f;      // moisture above the floor runs to the lowest neighbour
        public float fieldCap = 0.65f, soakShare = 0.5f;          // above field capacity, part soaks to the water table
        public float vegHolds = 0.75f, humidSlowsDrying = 0.6f;
        public float capillaryWet = 0.05f, capillaryDamp = 0.14f; // depth to the table that keeps the soil wet / damp

        // Misting
        public float mistRadius = 0.45f, mistWet = 0.6f, mistAir = 0.05f, mistDeep = 0.15f; // deep = straight to the table
        public float mistGreensAt = 0.6f;                         // the core of a mist greens at once
        public float mistSecondsPerUnit = 2f, mistMaxAmount = 2f; // seconds of mist for one lab "spot"; a long press is capped

        // Humidity (one value for the box's air) and the glass
        public float humidBase = 0.20f, humidVeg = 0.20f, humidPond = 0.9f, humidToRoom = 0.35f;
        public float dropletsFrom = 0.55f, dropletsFull = 0.85f, dropletRise = 0.25f, dropletFall = 0.5f;
        public float grimePerSeason = 0.01f, grimeCap = 0.6f;
        public float lightGrime = 0.5f, lightDroplets = 0.5f, lightMin = 0.3f;
        public float tooDryHumid = 0.2f, tooDryGrowth = 0.45f, tooHumid = 0.75f;

        // The water table (one level for the whole box, in terrain height units)
        public float tableStartBelow = 0.13f;                     // below the lowest ground at the start
        public float tablePerSoak = 0.0003f, soakCapPerSeason = 12f; // rise per unit soaked down; the soil takes only so much a season
        public float tablePondDamp = 50f;                         // a bigger pond takes more water per unit of rise
        public float tableSummerLoss = 0.002f, tableSwing = 0.015f; // falls a little in summer, never below peak - swing
        public int pondGateCells = 20;                            // "water at the surface" for the village gate

        // Cover succession
        public float grassAt = 0.18f, grassFull = 0.55f, grassRate = 0.5f, grassAlone = 0.12f; // alone = no green neighbour
        public float tallAt = 0.35f, tallSoil = 0.25f, tallRate = 0.12f;
        public float bushAt = 0.30f, bushSoil = 0.28f, bushRate = 0.008f, bushNeighbour = 2f;
        public float soilGrass = 0.012f, soilTall = 0.02f, soilBush = 0.025f, soilTree = 0.03f, soilStart = 0.08f, soilStartBase = 0.15f;
        public float reedRate = 0.3f, reedBand = 0.03f;

        // Animals (counts)
        public float rabbitsAtMeadow = 0.10f, meadowPerRabbit = 35f, rabbitGrow = 0.2f, rabbitFall = 0.05f;
        public float grazeTall = 0.7f, grazeBush = 0.7f, grazeSeedling = 0.3f;
        public float birdsPerBush = 1f / 15f, birdsPerTree = 2f;
        public float firstBirdAt = 0.5f;                          // birds (a smoothed count) before the first one is told

        // The forest
        public float seedsPerBird = 0.3f, seedFalloff = 1.8f;
        public int seedReach = 8, maxSeedsPerSeason = 25;
        public float seedTakeGrass = 0.5f, seedTakeTall = 0.4f, seedTakeBush = 0.9f, seedTakeBare = 0.3f;
        public float seedlingRate = 0.06f, youngRate = 0.045f;
        public float wildSeedChance = 0.08f, wildSeedAfterBush = 0f; // a bird drops a seed from outside (a box with no Keeper)

        // The one tree
        public float treeRate = 0.35f;                            // progress per season at full care
        public float[] treeStages = { 0.5f, 1.5f, 3.0f, 4.5f };   // progress to sprout, sapling, young, grown
        public float treeWetAt = 0.15f, treeWetFull = 0.5f;
        public float weedGrow = 0.12f, weedSlows = 0.6f;
        public int weedNeighbours = 3;
        public int learnWater = 3, learnRing = 6;                 // Keeper mistings before the people water it / ring it
        public float peopleWater = 0.18f, ringSlowsWeeds = 0.5f;
        public float seedUnlockBush = 0.003f;                     // the seed comes after the first bushes

        // Gates
        public float campMeadow = 0.15f;
        public int campRabbits = 2;
        public int villageTrees = 8;
    }
}
