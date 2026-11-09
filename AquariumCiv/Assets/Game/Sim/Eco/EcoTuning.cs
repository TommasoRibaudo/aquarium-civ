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
        public float mistRadius = 0.7f, mistRadiusMax = 1.0f;     // a short mist's reach; a full hold (mistMaxAmount) reaches mistRadiusMax
        public float mistWet = 0.35f, mistAir = 0.05f, mistDeep = 0.15f; // deep = straight to the table
        public float mistGreensAt = 0.82f;                        // only the very middle of a mist greens at once (#266: a little green, not the whole core)
        public int mistMinGreen = 3;                              // ... but every mist leaves at least this many green cells at its heart
        public float mistSproutAt = 0.6f, mistSprout = 0.8f;     // shoots (they start at sproutCommit and up to mistSprout): bare ground in the wet area (f above this) starts sprouting
        public float mistShow = 1.0f;                             // how wet the surface looks per unit of mist at the middle (EcoState.wet)
        public float puddleAt = 0.85f, puddleGain = 1.0f;         // amount * falloff above this soaks to a puddle (low ground only)
        public float puddleMaxSlope = 0.3f;
        public float mistSecondsPerUnit = 2f, mistMaxAmount = 2f; // seconds of mist for one lab "spot"; a long press is capped

        // The wet area and the first shoots (#266, #271). All per MONTH (the land's weather steps a season; these step monthly).
        public float wetDry = 0.20f, wetDryHumid = 0.5f, wetGone = 0.03f;   // share of the surface wetness lost a month (slower in humid air)
        public float puddleDry = 0.22f, puddleSink = 0.35f, puddleGone = 0.04f; // strength lost a month (x season dryness); share that sinks into wet
        public float puddleDrinkAt = 0.25f;                       // a puddle this strong can be drunk from (the water source while there is no pond)
        public float sproutWetAt = 0.3f, sproutRate = 0.2f, sproutWetRate = 0.05f; // shoots grow on a month at sproutRate (more beside green); wet ground alone adds sproutWetRate * wetness
        public float sproutKeep = 0.97f, sproutCommit = 0.5f; // shoots past sproutCommit grow on by themselves;                          // shoots linger a little when the wet leaves
        public float creepChance = 0.014f, creepStep = 0.5f, creepMoist = 0.10f; // a green edge nudges a bare neighbour on, if the ground has any damp
        public float dewSprout = 0.002f, dewSproutMoist = 0.14f, showerWet = 0.9f; // an odd sprout after dew; a shower's wet area

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
        public float grassAt = 0.18f, grassFull = 0.55f, grassRate = 0.1f, grassAlone = 0.12f; // alone = no green neighbour
        public float tallAt = 0.35f, tallSoil = 0.25f, tallRate = 0.12f;
        public float bushAt = 0.30f, bushSoil = 0.28f, bushRate = 0.008f, bushNeighbour = 2f;
        // #385: bushes grow in one or two thickets, not a carpet. Inside a thicket they spread as before; outside, a stray now and then
        // (bushStray of the rate) that dies back (bushStrayDie a season). The count settles at the cap (bushPerMeadow of the meadow, at
        // most bushCap); above it the bushes thin, outside the thickets first (bushThin a season at most): old carpeted saves thin in a few years.
        public float thicketBoost = 6f, thicketChoke = 0.9f, thicketR1 = 0.52f, thicketR2 = 0.36f, thicketEdge = 0.35f, thicketFromCamp = 1.45f, bushCampMargin = 0.9f, bushMarginDie = 0.5f, thicketApart = 1.4f;
        public int thicketBAfter = 15, bushCap = 60;
        public float bushPerMeadow = 0.04f, bushStray = 0.03f, bushStrayDie = 0.04f, bushThin = 0.12f;
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
        public float seedUnlockBush = 0.003f;                     // unused since #418: the seed comes after the thicket is placed

        // #314: the creeping vine (slower than the weeds: a strand grows a cell a season, a new one starts now and then)
        public int vineCap = 30;
        public float vineSeedChance = 0.3f, vineGrowChance = 0.5f, vineDamp = 0.4f;
        // #311: a wild sapling inside a wood (this many grown trees round it) is the forest's own heart, not its edge
        public int heartTrees = 3;

        // #420 the deer (spec §4.1, §5.3): cap 1 per deerTreesPer grown trees, between deerMin and deerMax; one fawn per doe in spring
        // under the cap. The white stag shows once per box, at most once a season with this chance, when the herd holds whiteStagHerd
        // for whiteStagAfter months after the placing.
        public int deerTreesPer = 3, deerMin = 3, deerMax = 8, whiteStagHerd = 5, whiteStagAfter = 24;
        public float whiteStagChance = 0.2f, deerRange = 0.6f;
        // #421 the ducks (spec §5.4): a wild cap; a clutch per pair in spring (duckClutchMin..Max eggs), duckGrowShare of them grow up.
        // Reeds come duckReedBoost times as fast within duckReedNear of where the ducks were placed (the reeds grow first there).
        public int duckCap = 8, duckClutchMin = 3, duckClutchMax = 5;
        public float duckGrowShare = 0.5f, duckReedBoost = 3f, duckReedNear = 0.6f;
        // #421 the fish (spec §5.5): a school of fishSchool; cap 1 per fishPondPer pond cells; it grows toward the cap within a year.
        public int fishSchool = 6, fishPondPer = 3, fishMin = 3;
        public float fishGrow = 0.35f;

        // #437 the wolves (spec §3, §4.1, §5.6; research 19 §3-§5). The jar: the herd at wolvesDeer or more for wolvesDeerMonths, a wood of
        // wolvesTrees grown trees, and a den spot (a grown tree in the wood: denTrees grown trees round it, the wood between it and the
        // fire) at least denFromFire from the fire. The pack: 2..wolfCap, pups wolfPupsMin..Max in spring. A chase a month with
        // wolfChase (x wolfWinter in winter), one in 1/wolfCatch ends in a kill; kills stay at least wolfKillFromFire from the fire.
        // The white wolf: once, in a winter after the first pups, with whiteWolfChance a winter.
        public int wolvesDeer = 5, wolvesDeerMonths = 12, wolvesTrees = 20, denTrees = 3, wolfCap = 6, wolfPupsMin = 2, wolfPupsMax = 4;
        public float denFromFire = 1.5f, wolfChase = 0.35f, wolfWinter = 1.8f, wolfCatch = 0.25f, wolfKillFromFire = 1.2f, whiteWolfChance = 0.35f;
        // Boldness (0..1): its start, and what moves it. Scraps and a kill left raise it; a chase or a kill taken lower it; the first guard
        // bark lowers it; each month scraps wear it up and a grown dog on guard wears it down.
        public float boldStart = 0.2f, boldScraps = 0.15f, boldChase = 0.15f, boldKillLeft = 0.08f, boldKillTaken = 0.1f, boldGuard = 0.1f,
            boldScrapsMonth = 0.01f, boldDogMonth = 0.01f;
        // The deer's human shield (research 19 §1): with the pack about, the herd's home moves up to this share of the way to the fire.
        public float deerShieldPull = 0.35f;
        // The meat thief: a chance a season (wolfThiefBase + wolfThiefBold x boldness); never while food would drop below
        // wolfThiefFood x population. It takes one strip (food) or, now and then, a hide.
        public float wolfThiefBase = 0.3f, wolfThiefBold = 0.5f, wolfThiefFood = 1.5f, wolfThiefStrip = 1f;

        // #417 the life jar (placed-life spec §3, §4.3; all [est] until #434). Each jar opens on its land condition once the rung below is placed.
        public float jarThicketMeadow = 0.10f, jarRabbitsMeadow = 0.15f; // meadow share for the thicket's jar, the rabbits'
        public int jarDeerTrees = 5, jarDucksPond = 12;                   // grown trees (the Keeper's counts) for the deer; pond cells for the ducks
        public int jarFishReeds = 6, jarFishAfterDucks = 6;               // reed cells, and months since the ducks were placed, for the fish
        public int jarRestockMonths = 24;                                 // at its floor this long: the jar opens again for one more group
        public float deerNearTree = 0.3f;                                 // the deer's valid ground: within this of a grown tree
        // #418 the one placed thicket (spec §5.1): a disc round the placed spot, 3 founders, a bush a season until it holds thicketBushes.
        // It replaces #385's self-founding thickets and strays (thicketBoost, bushStray, thicketBAfter, bushPerMeadow, bushCap: unused now).
        public float thicketRadius = 0.45f;
        public float rabbitThicketMargin = 0.12f; // tester 2026-11-08: the rabbits' ground stays this far outside the thicket's widest edge
        public int thicketBushes = 22, thicketFounders = 3;

        // Gates
        public float campMeadow = 0.20f;
        public int campRabbits = 2;
        public int villageTrees = 8;
    }
}
