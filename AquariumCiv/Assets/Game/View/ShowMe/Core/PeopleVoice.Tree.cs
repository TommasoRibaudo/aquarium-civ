using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>
    /// P8 #318 (tester #308): once the Keeper tends the tree, the people talk about it, not only while it is tended: at the fire,
    /// on walks, passing by. Lines are gated by the tree's stage (sprout, sapling, young, grown) and coloured by trait; a few
    /// need the people to have learnt to water it. Pure and read-only (the sim is only asked: <see cref="SimCore.EcoTree"/>).
    /// Closed world: nothing from outside, nothing the people don't have yet (no huts, no bread), the Keeper named as the stage
    /// names it ({keeper}).
    /// </summary>
    public static partial class PeopleVoice
    {
        /// <summary>The trait of the "children" lines: said by a child of the camp (or the crowd when none is named).</summary>
        public const string ChildTrait = "child";

        public sealed class TreeLine
        {
            public string id, trait, text;
            /// <summary>The tree stages (1 sprout .. 4 grown) it can be said in.</summary>
            public int minTree, maxTree;
            /// <summary>Keeper mistings the people must have seen first (the water-carrying lines need them to have learnt).</summary>
            public int minMist;
        }

        static TreeLine L(string id, string trait, int minTree, int maxTree, string text, int minMist = 1)
            => new TreeLine { id = id, trait = trait, minTree = minTree, maxTree = maxTree, text = text, minMist = minMist };

        /// <summary>Said by the crowd (trait null) or by someone with the trait. {keeper} / {Keeper} are the stage's name for the Keeper.</summary>
        public static readonly IReadOnlyList<TreeLine> TreeLines = new List<TreeLine>
        {
            // anyone, by the tree's stage
            L("any-sprout1", null, 1, 1, "There's a tiny green thing by the grass. It wasn't there yesterday."),
            L("any-sprout2", null, 1, 1, "Mind your feet. The sprout is trying very hard."),
            L("any-sapling1", null, 2, 2, "It's up to my knee now. Don't tell it I measured."),
            L("any-sapling2", null, 2, 3, "It has proper leaves. Four of them. I counted twice."),
            L("any-young1", null, 3, 3, "It's nearly to my shoulder, and still showing off."),
            L("any-young2", null, 3, 3, "You can almost sit in its shade. Almost. Bring a small stool."),
            L("any-grown1", null, 4, 4, "There's shade at noon now. Who thought of that? The tree did."),
            L("any-grown2", null, 4, 4, "We sit under the tree after supper now. It listens better than most."),
            L("any-grown3", null, 4, 4, "A leaf fell in the soup. I'm calling it seasoning."),
            L("any-grown4", null, 4, 4, "The wind goes through the tree and the tree answers. Listen."),
            L("any-keeper1", null, 1, 4, "That's {keeper}'s tree. I walk round it the long way. Out of respect."),
            L("any-keeper2", null, 2, 4, "{Keeper} waters it and it grows. I'm taking notes on how."),
            L("any-keeper3", null, 1, 3, "{Keeper} planted that tree, so I'm keeping an eye on it."),

            // by trait
            L("worrier1", "the worrier", 1, 2, "I check on the little tree every morning. Still there. Good. Still there."),
            L("worrier2", "the worrier", 3, 4, "What if it keeps growing and never stops? I'd like it to stop. A little."),
            L("gardener1", "the gardener", 1, 1, "Hello, little one. Drink up. Slowly."),
            L("gardener2", "the gardener", 2, 3, "I loosened the soil round it. It never said thanks. Plants rarely do."),
            L("gardener3", "the gardener", 4, 4, "Look at those roots go. I'm not crying, it's the pollen."),
            L("dreamer1", "the dreamer", 1, 2, "I dreamt the sprout told me its name. I forgot it. Rude of me."),
            L("dreamer2", "the dreamer", 3, 4, "I think the tree dreams too. Slower than us. Better, maybe."),
            L("kind1", "the kind", 1, 3, "I left a cup of water by the tree. For the tree. And whoever's passing."),
            L("kind2", "the kind", 4, 4, "I tied a ribbon on a low branch. In case anyone's feeling lonely."),
            L("grump1", "the grump", 1, 2, "Another thing to step round. Lovely."),
            L("grump2", "the grump", 3, 4, "It's a decent tree. Don't tell it I said so."),
            L("clever1", "the clever", 1, 3, "By my count it grew a finger this week. A tree finger."),
            L("joker1", "the joker", 1, 3, "I told the tree a joke. It leaned in. Great audience."),
            L("stubborn1", "the stubborn", 1, 3, "It's going to be a good tree. I've decided. It's decided."),
            L("singer1", "the singer", 1, 3, "I sing it a song every morning. It's growing for the chorus."),
            L("loud1", "the loud", 1, 4, "HELLO, TREE! ...Too loud? It can't say. That's a yes."),
            L("lazy1", "the lazy", 2, 3, "Somebody should water the tree. Somebody energetic. I'll supervise."),
            L("lazy2", "the lazy", 4, 4, "Best napping spot there is, under the tree. Don't wake me."),
            L("gossip1", "the gossip", 1, 4, "They say {keeper} waters it when nobody's looking. Just repeating."),
            L("brave1", "the brave", 4, 4, "I climbed to the high branch. I'm not coming down for a while."),
            L("tinkerer1", "the tinkerer", 1, 3, "I built the tree a little fence of sticks. Against rabbits. And toddlers."),

            // the children
            L("child1", ChildTrait, 2, 2, "I carry water to the baby tree. Both hands. Don't spill.", 3),
            L("child2", ChildTrait, 3, 3, "Can I climb it yet? ...Now? ...Now?"),
            L("child3", ChildTrait, 4, 4, "I climbed the tree! Only the first bit! Only a bit!"),
            L("child4", ChildTrait, 1, 3, "The tree is my height. We're growing together. I'm winning."),
        };

        /// <summary>True once the Keeper has begun to tend the tree: it is planted and has been misted at least once.</summary>
        public static bool TreeTended(SimCore sim)
        {
            if (sim == null || !sim.EcologyOn) return false;
            var t = sim.EcoTree;
            return t.planted && t.stage >= 1 && t.keeperMistings >= 1;
        }

        /// <summary>The tree lines the people can say now (tending has begun, the tree is at a stage the line fits), most
        /// stage-specific first, then by the table's order. Who says each: the alive adult with the trait (lowest id), a child for
        /// the children's lines, -1 (the crowd) for the rest. Nothing before the tree is tended.</summary>
        public static IEnumerable<(TreeLine line, int speakerId, string text)> TreeLinesNow(SimCore sim)
        {
            if (!TreeTended(sim)) yield break;
            var tree = sim.EcoTree;
            string keeper = KeeperName(sim);
            foreach (var l in TreeLines.Where(x => tree.stage >= x.minTree && tree.stage <= x.maxTree && tree.keeperMistings >= x.minMist)
                                       .OrderBy(x => x.maxTree - x.minTree))
            {
                int who = -1;
                if (l.trait == ChildTrait)
                {
                    var kid = sim.State.notables.Where(x => x.alive && x.age < SimCore.AdultAge).OrderBy(x => x.id).FirstOrDefault();
                    if (kid != null) who = kid.id;
                }
                else if (l.trait != null)
                {
                    var n = sim.State.notables.Where(x => x.alive && x.age >= SimCore.AdultAge && x.trait == l.trait).OrderBy(x => x.id).FirstOrDefault();
                    if (n == null) continue;
                    who = n.id;
                }
                string text = l.text.Replace("{keeper}", keeper).Replace("{Keeper}", Cap(keeper));
                if (!Allowed(text, sim)) continue;
                yield return (l, who, text);
            }
        }
    }
}
