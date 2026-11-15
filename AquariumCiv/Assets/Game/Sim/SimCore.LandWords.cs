using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #656/#677 (tester, 2026-11-14): on a living land the chronicle's stories must fit the land the Keeper sees. The old story
    /// tables were written for the fixed slice (a stream, a pond, goats and hens, a hut, a woodpile); a new camp has a tent, a dry
    /// hollow, no animals the Keeper did not place and no wood before the first wild tree. The swap happens on the finished line,
    /// after the dice (like <c>TownOnlyLines</c>), so the story stream draws exactly as before and nothing in the economy moves.
    /// Old boxes and the old slice (ecology off) keep their words.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The living land's words for a finished chronicle line. Pure text; no dice.</summary>
        public string LandWords(string text)
        {
            if (!EcologyOn || string.IsNullOrEmpty(text)) return text;
            bool water = OpenWater, camp = State.era == Era.Camp;
            bool wood = WildWoodTreeCount > 0 || FirstWildWoodReached || Count(BuildingType.Lumbermill) > 0 && !camp;
            bool trees = TreesGrow || State.eco.treeCell >= 0;
            var sb = new System.Text.StringBuilder(text);
            void R(string from, string to) => sb.Replace(from, to);

            // ---- water: no stream on a living land; the pond only once it is there
            R("lost a sandal crossing the stream", water ? "lost a sandal in the mud at the pond's edge" : "lost a sandal in the mud of the hollow");
            if (!water)
            {
                R("A dry summer: the stream ran low and everyone shared their water.", "A dry summer: the ground cracked and everyone shared their water.");
                R("The pond shrank all summer.", "The ground cracked all summer.");
                R("carried water up from the pond in a line", "carried muddy water up from the hollow in a line");
                R("A rainbow arched over the pond.", "A rainbow arched over the glass.");
                R("A rainbow came down right by the pond.", "A rainbow came down right by the camp.");
                R("built a little fence around the pond, just in case", "put a ring of stones round the camp, just in case");
                R("lingered over the pond", "lingered over the camp");
                R("left the pond to", "left the water to");
            }
            else R("the stream ran low", "the pond ran low");

            // ---- animals the land has never had: goats, hens, a fox, a kitten, a dog
            R("came home with firewood and a goat as well.", wood ? "came home with an armful of firewood as well." : "came home with an armful of dry grass as well.");
            R("chased a fox away from the hens with a frying pan.", "shooed a wasp out of the tent with a broom.");
            R(" to save a kitten.", " to tie the cover back down.");
            R("took in a stray goat. It follows them everywhere now.", "took in a hurt bird and fed it until it flew.");
            R("called the goats home from the far side of the glass", "called the children home from the far side of the glass");
            R("Someone danced with a goat, and the goat led.", "Someone danced with a broom, and the broom led.");
            R("looking for the same lost goat. They found it together.", "looking for the same lost basket. They found it together.");
            R(" over a goat", " over the warmest spot in the tent");
            R("smelled of wet dog", "smelled of wet hides");
            R("a goat", "the warmest spot in the tent"); // any quarrel or line left with a goat in it

            // ---- the camp's things: tents, not huts; no fence, no roof, no apples, no bread
            if (camp)
            {
                R("the hut roof", "the tent poles");
                R("the hut", "the tent");
                R("a fence post", "a tent peg");
                R("fixing a roof together", "fixing a tent together");
                R("sharing the last apple", "sharing the last of the basket");
                R("baked bread for every hearth after the storm", "shared out their supper at every tent after the storm");
            }

            // ---- wood: none before the first wild tree gives it
            if (!wood)
            {
                R("sulks by the woodpile", "sulks by the tents");
                R("the woodpile", "the camp");
            }
            if (!trees) R("walked into the same tree four times", "walked into the same tent four times");
            return sb.ToString();
        }
    }
}
