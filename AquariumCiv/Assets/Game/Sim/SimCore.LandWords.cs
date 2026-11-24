using System;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #656/#677, #720: on a living land the chronicle's lines must fit the land the Keeper sees. Since #720 every line that needed a
    /// phrase swap has its living-land wording written at its source, gated on the thing it talks about (the AgentGameTemplate lesson:
    /// gate a line on its state, don't patch its words afterwards). What is left here is vocabulary, not patches: the camp's dwellings
    /// are tents, not huts. Old boxes and the old slice (ecology off) keep their words.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The living land's words for a finished chronicle line: only the era's own nouns. Pure text; no dice.</summary>
        public string LandWords(string text)
        {
            if (!EcologyOn || string.IsNullOrEmpty(text)) return text;
            if (State.era != Era.Camp) return text;
            // The camp lives in tents (era vocabulary, as the Bright One is the camp's name for the Keeper).
            return text.Replace("the hut roof", "the tent poles").Replace("the hut", "the tent");
        }

        /// <summary>#720: the land has wood to stack (the slice always; a living land once a wild tree gives wood, or a lumbermill past
        /// the camp). Lines about the woodpile are gated on it at their source.</summary>
        public bool LandHasWood => !EcologyOn || WildWoodTreeCount > 0 || FirstWildWoodReached || Count(BuildingType.Lumbermill) > 0 && State.era != Era.Camp;
    }
}
