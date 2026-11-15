using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #570 what is missing, in the people's words. A locked jar says what it waits for (<see cref="JarNeed"/>), read from the same
    /// conditions as <see cref="LandReady"/> (the tests hold the two together), and the camp and the village say which pieces they
    /// still wait for (<see cref="WaitsFor"/>), read from the same conditions as the gates (<see cref="Gates"/>).
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>The species as the people say it, capitalised, for the start of a line.</summary>
        public static string PlainName(LifeSpecies s) => s switch
        {
            LifeSpecies.Thicket => "Bushes",
            LifeSpecies.Rabbits => "Rabbits",
            LifeSpecies.Deer => "Deer",
            LifeSpecies.Ducks => "Ducks",
            LifeSpecies.Fish => "Fish",
            _ => "Wolves",
        };

        /// <summary>The first thing a shut jar still waits for, as a short line ("Rabbits need a meadow"), or null when its land is
        /// ready (or the jar is not shut). The rung below comes first, then the land, in the order the Keeper can act on them.</summary>
        public string JarNeed(LifeSpecies s, long month)
        {
            if (Jar(s).State != JarState.Locked) return null;
            var below = LifeJar.RungBelow(s);
            string n = PlainName(s);
            if (below.HasValue && !IsPlaced(below.Value))
                return n + " need the " + (below.Value == LifeSpecies.Thicket ? "bushes" : PlainName(below.Value).ToLowerInvariant()) + " first";
            bool anyMeadow = st.cGrass + st.cTall > 0;
            switch (s)
            {
                case LifeSpecies.Thicket:
                    return MeadowFrac >= t.jarThicketMeadow ? null : n + (anyMeadow ? " need a bigger meadow" : " need a meadow");
                case LifeSpecies.Rabbits:
                    return MeadowFrac >= t.jarRabbitsMeadow ? null : n + (anyMeadow ? " need a bigger meadow" : " need a meadow");
                case LifeSpecies.Deer:
                    return st.cTrees >= t.jarDeerTrees ? null : $"{n} need more trees ({st.cTrees}/{t.jarDeerTrees})";
                case LifeSpecies.Ducks:
                    return st.cPond >= t.jarDucksPond ? null : n + (st.cPond > 0 ? " need a bigger pond" : " need a pond");
                case LifeSpecies.Fish:
                {
                    if (st.cReeds < t.jarFishReeds) return $"{n} need reeds round the pond ({st.cReeds}/{t.jarFishReeds})";
                    var d = Jar(LifeSpecies.Ducks);
                    if (d.placedMonth < 0 || month - d.placedMonth < t.jarFishAfterDucks) return n + " wait for the ducks to settle in";
                    return null;
                }
                case LifeSpecies.Wolves:
                    if (st.deerFiveSince < 0) return $"{n} need a herd of {t.wolvesDeer} deer";
                    if (st.cTrees < t.wolvesTrees) return $"{n} need a deeper wood ({st.cTrees}/{t.wolvesTrees} trees)";
                    if (!AnyDen()) return n + " need a hidden den spot in the wood";
                    if (!DenRoom()) return n + " need a bigger wood to hide a den in"; // #643
                    if (month - st.deerFiveSince < t.wolvesDeerMonths) return n + " wait for the herd to hold its numbers";
                    return null;
            }
            return null;
        }

        /// <summary>What the camp (or, once it stands, the village) still waits for, piece by piece, in the order of
        /// <see cref="Gates"/>: "more meadow", "more trees (3/8)", "fish in the pond". Empty when the village stands, and when
        /// every piece is there (the gate opens at the next step). <paramref name="village"/>: the stage the pieces belong to.</summary>
        public void WaitsFor(List<string> into, out bool village)
        {
            into.Clear();
            village = st.campGateMonth >= 0;
            if (st.villageGateMonth >= 0) return;
            if (!village)
            {
                if (MeadowFrac < t.campMeadow) into.Add(st.cGrass + st.cTall > 0 ? "more meadow" : "a meadow");
                if (!IsPlaced(LifeSpecies.Rabbits)) into.Add("rabbits in the meadow");
                else if (st.rabbits < t.campRabbits) into.Add("the rabbits to grow");
                return;
            }
            if (st.cTrees < t.villageTrees) into.Add($"more trees ({st.cTrees}/{t.villageTrees})");
            if (st.cPond < t.pondGateCells) into.Add(st.cPond > 0 ? "a bigger pond" : "a pond");
            if (!IsPlaced(LifeSpecies.Ducks)) into.Add("ducks on the pond");
            if (!IsPlaced(LifeSpecies.Fish)) into.Add("fish in the pond");
        }
    }
}
