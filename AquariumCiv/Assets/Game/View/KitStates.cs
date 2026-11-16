using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.View
{
    /// <summary>
    /// #668: every state of the Keeper's kit (kit-mockups.html s0 to s6), staged from cheats, for the debug panel ("Kit states") and
    /// the KitShots class. Each call stages one state on the box as it is (it plants the Keeper's tree, places the first thicket, opens or
    /// shuts jars and fills the pouch as the state needs) and returns a line for the panel. Two are staged views of the kit, not game
    /// states: "tools only" and "a new tool" (every tool is in the kit from the start, and no tool arrives yet).
    /// </summary>
    public static class KitStates
    {
        public const string ToolsOnly = "s1 tools only", NewTool = "s2 a new tool", Seed = "s3 the seed", Waits = "s0 something waits (shut)",
            OneReady = "s4 one group ready", Full = "s5 full kit", HandRabbits = "s6 the rabbits in hand", HandWolves = "the wolves in hand (longest)",
            HandMister = "mister in hand", HandTweezers = "tweezers in hand", HandSpade = "spade in hand", HandCloth = "cloth in hand",
            HandSeed = "seed in hand", HandBush = "bush in hand";

        public static readonly string[] All =
        {
            ToolsOnly, NewTool, Seed, Waits, OneReady, Full, HandRabbits, HandWolves, HandMister, HandTweezers, HandSpade, HandCloth, HandSeed, HandBush,
        };

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        /// <summary>Stages <paramref name="state"/> (one of <see cref="All"/>). Returns what it did.</summary>
        public static string Stage(string state)
        {
            var sim = Sim; var hud = GameHud.Instance;
            if (sim == null || hud == null || !sim.EcologyOn) return "kit states need a living-land box";
            hud.CloseSheet();
            hud.SetCleanMode(false);
            hud.SetHand(GameHud.KitTool.None);
            GameHud.DebugToolsOnly = false;
            hud.DebugNewTool(null);
            hud.DebugDropToasts(); // the last state's hint would show over this one
            switch (state)
            {
                case ToolsOnly:
                    GameHud.DebugToolsOnly = true;
                    hud.Open(GameHud.Kit);
                    return "the kit with its tools only (staged)";
                case NewTool:
                    GameHud.DebugToolsOnly = true;
                    hud.Open(GameHud.Kit);
                    hud.DebugNewTool("kit-tweezers");
                    return "the tweezers wear New (staged)";
                case Seed:
                    if (!sim.CanPlantAnySeed) sim.DebugGiveSeeds(1);
                    sim.DebugLockJars();
                    hud.Open(GameHud.Kit);
                    return sim.CanPlantTree ? "the Keeper's seed waits, the jars shut" : "an earned seed waits (the Keeper's tree stands)";
                case Waits:
                    OneGroup(sim);
                    return "the rabbits' jar opened: the toast, Kit · and its pulse";
                case OneReady:
                    OneGroup(sim);
                    hud.Open(GameHud.Kit);
                    return "the rabbits ready, the rest shut";
                case Full:
                    TreeAndThicket(sim);
                    sim.DebugLockJars();
                    sim.DebugTakeFromPouch(sim.SeedsInPouch, sim.BushesInPouch);
                    sim.DebugGiveSeeds(2); sim.DebugGiveBushes(1);
                    sim.DebugJarReady(LifeSpecies.Rabbits); sim.DebugJarReady(LifeSpecies.Ducks);
                    hud.Open(GameHud.Kit);
                    hud.DebugPickShut(LifeSpecies.Deer);
                    return "2 seeds, 1 bush, rabbits and ducks ready, the rest shut, the deer picked";
                case HandRabbits:
                    OneGroup(sim);
                    hud.TakeLife(LifeSpecies.Rabbits);
                    return "the rabbits in hand";
                case HandWolves:
                    TreeAndThicket(sim);
                    sim.DebugJarReady(LifeSpecies.Wolves);
                    hud.TakeLife(LifeSpecies.Wolves);
                    return "the wolves in hand";
                case HandMister: hud.SetHand(GameHud.KitTool.Mister); return "the mister in hand";
                case HandTweezers: hud.SetHand(GameHud.KitTool.Tweezers); return "the tweezers in hand";
                case HandSpade: hud.SetHand(GameHud.KitTool.Landscape); return "the spade in hand";
                case HandCloth: hud.SetCleanMode(true); return "the cloth in hand";
                case HandSeed:
                    if (!sim.CanPlantAnySeed) sim.DebugGiveSeeds(1);
                    hud.SetHand(GameHud.KitTool.Seed);
                    return "the seed in hand";
                case HandBush:
                    TreeAndThicket(sim);
                    if (sim.BushesInPouch == 0) sim.DebugGiveBushes(1);
                    hud.SetHand(GameHud.KitTool.Bush);
                    return "a bush in hand";
            }
            return "unknown state";
        }

        /// <summary>The Keeper's tree stands and the first thicket is down (so the later jars and the pouch apply).</summary>
        static void TreeAndThicket(SimCore sim)
        {
            if (sim.CanPlantTree) sim.DebugTreeStage(2);
            if (!sim.LifeIsPlaced(LifeSpecies.Thicket)) sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
        }

        /// <summary>One group ready (the rabbits), every other jar shut, nothing in the pouch.</summary>
        static void OneGroup(SimCore sim)
        {
            TreeAndThicket(sim);
            sim.DebugLockJars();
            sim.DebugTakeFromPouch(sim.SeedsInPouch, sim.BushesInPouch);
            if (!sim.DebugJarReady(LifeSpecies.Rabbits)) sim.DebugJarAgain(LifeSpecies.Rabbits);
        }
    }
}
