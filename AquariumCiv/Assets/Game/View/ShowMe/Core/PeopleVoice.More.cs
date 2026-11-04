using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>Why the people are talking about the town (#181).</summary>
    public enum WantCase { FoodLow, Crowded, Wood, People, Content }

    /// <summary>One want line that is true now: the case, the building it names (null for food and content), its text.</summary>
    public sealed class WantLine
    {
        public WantCase kind;
        public string building;
        public string text;
        /// <summary>Which wording of the case this is (for the bubble's key).</summary>
        public int variant;
    }

    /// <summary>
    /// More of the people's own words (#120, #181, #191): lines said by someone with a trait, lines about what the town wants
    /// to build and lacks, and the thanks of someone whose wish was given. Same rules as the rest of <see cref="PeopleVoice"/>:
    /// warm, a bit comic, never blame, only what the belief stage may say, the era's own words ("camp", "village", "town").
    /// </summary>
    public static partial class PeopleVoice
    {
        // ======================================================================================== trait lines (#120)

        /// <summary>A first-person line for anyone with a trait, by belief stage. The key is stable: it's what is remembered as said.</summary>
        public sealed class TraitLine
        {
            public string id, trait, text;
            public int stage;
        }

        static TraitLine T(int stage, string trait, string id, string text) => new TraitLine { stage = stage, trait = trait, id = id, text = text };

        /// <summary>Three stages, one line per trait each. Stage 1 knows the sky and the Bright One, stage 2 the Keeper and the
        /// window, stage 3 the scholars and the wall; none says "glass" (the scholars aren't there yet in the early half).</summary>
        public static readonly IReadOnlyList<TraitLine> TraitLines = new List<TraitLine>
        {
            T(1, "the stubborn", "stubborn1", "I said the berries were east. They were east. Eventually."),
            T(1, "the dreamer", "dreamer1", "The clouds made a hand again. Nobody believes me. Fine."),
            T(1, "the kind", "kind1", "I left a bowl out for the sky. In case it's hungry."),
            T(1, "the loud", "loud1", "Has anyone seen my spoon? ...Never mind. It's in my hand."),
            T(1, "the clever", "clever1", "I've counted the steps to the edge of the sky. Don't ask. It's a lot."),
            T(1, "the lazy", "lazy1", "I'd help, but someone has to watch the fire. It's a lot of watching."),
            T(1, "the brave", "brave1", "I'll go first. Somebody remember that I went first."),
            T(1, "the gossip", "gossip1", "I heard the Bright One tidied the sky last night. Ask me who told me."),
            T(1, "the gardener", "gardener1", "Talked to the roots again. They're listening. Slowly."),
            T(1, "the grump", "grump1", "Nothing wrong with the weather. I just like to mention it."),
            T(1, "the singer", "singer1", "I've got a song about the big sky. It's mostly chorus."),
            T(1, "the tinkerer", "tinkerer1", "I made the stick pointier. It's a start."),
            T(1, "the worrier", "worrier1", "What if the sky leans? I'm only asking."),
            T(1, "the joker", "joker1", "I told the fire a joke. It crackled. I'll take it."),

            T(2, "the stubborn", "stubborn2", "The fence is straight. I checked. The Keeper can check too."),
            T(2, "the dreamer", "dreamer2", "I dreamt the Keeper was humming. I can't remember the tune. I keep trying."),
            T(2, "the kind", "kind2", "I put an extra loaf by the window. For the Keeper, or for whoever."),
            T(2, "the loud", "loud2", "I WAVED AT THE KEEPER. I wasn't shouting. It's just a big wave."),
            T(2, "the clever", "clever2", "I'm timing the Keeper's visits. So far: whenever."),
            T(2, "the lazy", "lazy2", "The Keeper never hurries. I'm taking notes."),
            T(2, "the brave", "brave2", "I looked the Keeper right in the eye. Near the eye."),
            T(2, "the gossip", "gossip2", "They say the Keeper waved at me first. I'm not saying it's true. I'm saying it's true."),
            T(2, "the gardener", "gardener2", "The beans lean toward the window. I think they like the Keeper's light."),
            T(2, "the grump", "grump2", "The Keeper looks in and the dog barks. Neither of them is sorry."),
            T(2, "the singer", "singer2", "I wrote a hymn for the window. It has a part where everyone takes their hats off."),
            T(2, "the tinkerer", "tinkerer2", "I hung a little bell by the window. It rings when the Keeper's near. Or when it's windy."),
            T(2, "the worrier", "worrier2", "What if the Keeper's busy? Right. Busy is fine. Busy is good."),
            T(2, "the joker", "joker2", "I told the Keeper a joke through the window. No laugh. Tough crowd, very large."),

            T(3, "the stubborn", "stubborn3", "The wall isn't going anywhere. Neither am I. We understand each other."),
            T(3, "the dreamer", "dreamer3", "I think there's a room out there with a lamp in it. I think I've been there."),
            T(3, "the kind", "kind3", "I took the scholars some soup. They forget to eat when they're measuring."),
            T(3, "the loud", "loud3", "Quiet in the library, they said. I AM quiet. This is quiet."),
            T(3, "the clever", "clever3", "I've measured the wall four ways. It's the same wall every time. Annoyingly."),
            T(3, "the lazy", "lazy3", "Somebody will invent a chair that stands up for you. Not me. Somebody."),
            T(3, "the brave", "brave3", "I took the night watch. The Keeper's lamp makes it easy. Mostly."),
            T(3, "the gossip", "gossip3", "Word at the market is the Keeper reads. I'm only repeating it."),
            T(3, "the gardener", "gardener3", "A pot of flowers on every sill. The Keeper might like flowers. Everyone does."),
            T(3, "the grump", "grump3", "Too many scholars and not enough benches."),
            T(3, "the singer", "singer3", "The new song's about the Keeper's lamp. It rhymes with nothing. That's the charm."),
            T(3, "the tinkerer", "tinkerer3", "I'm building a better ladder. For nothing in particular. Just taller."),
            T(3, "the worrier", "worrier3", "I check the wall every night. Still a wall. Good."),
            T(3, "the joker", "joker3", "Why did the scholar cross the market? To measure it. That's the whole joke. They really did."),
        };

        /// <summary>Trait lines the people can say now (this belief stage): who says each is the alive adult with the trait, lowest id.</summary>
        public static IEnumerable<(TraitLine line, int speakerId)> TraitLinesNow(SimCore sim)
        {
            if (sim == null || !sim.RelicsOn) yield break;
            int stage = sim.BeliefStage;
            foreach (var l in TraitLines)
            {
                if (l.stage != stage) continue;
                var n = sim.State.notables.Where(x => x.alive && x.age >= SimCore.AdultAge && x.trait == l.trait).OrderBy(x => x.id).FirstOrDefault();
                if (n == null || !Allowed(l.text, sim)) continue;
                yield return (l, n.id);
            }
        }

        // ======================================================================================== what the town wants (#181)

        /// <summary>The era's own noun for the place: camp, village, town.</summary>
        public static string PlaceWord(SimCore sim) => sim.State.era switch { Era.Camp => "camp", Era.Village => "village", _ => "town" };

        /// <summary>What the people call a building in this era, with its article ("the windmill"). Null when they have no word for it.</summary>
        public static string Thing(SimCore sim, string type)
        {
            bool camp = sim.State.era == Era.Camp, town = sim.State.era == Era.Town;
            switch (type)
            {
                case BuildingType.Hut: return town ? "a new house" : sim.EcologyOn && camp ? "another tent" : "another hut";
                case BuildingType.Farm: return camp ? "the new gathering grounds" : "the new field";
                case BuildingType.Lumbermill: return camp ? "the woodpile" : "the lumbermill";
                case BuildingType.Well: return "the well";
                case BuildingType.Windmill: return "the windmill";
                case BuildingType.Market: return "the market";
                case BuildingType.Tavern: return "the tavern";
                case BuildingType.Church: return "the church";
                case BuildingType.Workshop: return "the workshop";
                case BuildingType.Bakery: return "the bakery";
                case BuildingType.Guildhall: return "the guildhall";
                case BuildingType.Shop: return "the new shop";
                default: return null;
            }
        }

        static string UpFirst(string s) => Cap(s);

        /// <summary>The wordings of a case (each with {thing}, {Thing}, {place}, {n} to fill). A few buildings have their own.</summary>
        static string[] WantWordings(SimCore sim, WantCase c, string building)
        {
            bool town = sim.State.era == Era.Town, tent = sim.EcologyOn && sim.State.era == Era.Camp;
            switch (c)
            {
                case WantCase.Wood:
                    switch (building)
                    {
                        case BuildingType.Windmill: return new[] { "The windmill's all drawn out. It only needs every plank we don't have.", "If only we had more wood to start on the windmill." };
                        case BuildingType.Market: return new[] { "The market's chalked out on the ground. The stalls just need planks.", "A market needs stalls and stalls need wood. We have a lot of chalk." };
                        case BuildingType.Tavern: return new[] { "The tavern's name is decided. The roof isn't.", "I've chosen my stool already. The tavern can follow when the wood does." };
                        case BuildingType.Church: return new[] { "The church is planned down to the last bench. We're missing the planks.", "Give the woodcutters a good week and the church goes up." };
                        case BuildingType.Well: return new[] { "I can already taste the water from the new well. It needs wood for the cover first." };
                        case BuildingType.Bakery: return new[] { "The oven's all drawn out. We need wood to build it, and then wood to burn in it." };
                    }
                    return new[]
                    {
                        "{Thing} is all drawn out. It only wants some wood we haven't got yet.",
                        "If only we had more wood to start on {thing}.",
                        "I keep pacing out where {thing} will go. The planks keep not turning up.",
                        "Give the woodcutters a good week and we'll have {thing}.",
                    };
                case WantCase.People:
                    return new[]
                    {
                        "{n} more of us and we'll start on {thing}. I'm only saying.",
                        "I've picked the spot for {thing}. We just need a few more hands.",
                        "Once there are enough of us, {thing} goes up. I can already see it.",
                    };
                case WantCase.Crowded:
                    return new[]
                    {
                        "I know everyone's feet by now. We could use another hut.",
                        "Somebody's elbow is in my ear every night. A new hut would be lovely.",
                        "We're a tight bunch. Tighter every spring. A new hut wouldn't hurt.",
                    }.Select(t => town ? t.Replace("hut", "house") : tent ? t.Replace("hut", "tent") : t).ToArray();
                case WantCase.FoodLow:
                    return town
                        ? new[] { "I've started counting the loaves. There aren't many.", "Lean week. We've had worse. Probably.", "The soup's very clear today. You can see the bottom of the pot." }
                        : new[] { "I've started naming the berries. There aren't many.", "Lean week. We've had worse. Probably.", "The soup's very clear today. You can see the bottom of the pot." };
                default:
                    return new[]
                    {
                        "Best patch of dirt in the whole world. I've seen all of it.",
                        "This {place} is perfect for me!",
                        "The fire's warm, the soup's hot and nobody's lost. Good day.",
                        "Everything I need is a short walk away. Because everything is.",
                    };
            }
        }

        /// <summary>
        /// The want lines that are true now, most pressing first: food running low, the people crowded, a wanted building short
        /// of wood, a building waiting for more people, and, when nothing is missing, a contented line. Each is checked against
        /// the sim now (<see cref="SimCore.NextWant"/>, the same choice <c>TryBuild</c> makes), so nobody asks for a building that's
        /// up or isn't theirs yet. <paramref name="variantSeed"/> picks the wording of each case.
        /// </summary>
        public static IEnumerable<WantLine> WantLines(SimCore sim, int variantSeed = 0)
        {
            if (sim == null || sim.State.population <= 0) yield break;
            var w = sim.NextWant();
            if (w.foodLow) { var l = BuildWant(sim, WantCase.FoodLow, null, w, variantSeed); if (l != null) yield return l; }
            if (w.crowded) { var l = BuildWant(sim, WantCase.Crowded, BuildingType.Hut, w, variantSeed); if (l != null) yield return l; }
            if (w.building != null && w.stop == WantStop.Wood && w.building != BuildingType.Hut) { var l = BuildWant(sim, WantCase.Wood, w.building, w, variantSeed); if (l != null) yield return l; }
            if (w.building != null && w.stop == WantStop.People) { var l = BuildWant(sim, WantCase.People, w.building, w, variantSeed); if (l != null) yield return l; }
            if (!w.foodLow && !w.crowded && (w.stop == WantStop.People || w.stop == WantStop.Full || w.stop == WantStop.Nothing))
            { var l = BuildWant(sim, WantCase.Content, null, w, variantSeed); if (l != null) yield return l; }
        }

        /// <summary>The want line for one case, only if it is true now (null otherwise): the debug panel asks for each case.</summary>
        public static WantLine WantLineFor(SimCore sim, WantCase c, int variantSeed = 0) =>
            WantLines(sim, variantSeed).FirstOrDefault(l => l.kind == c);

        static WantLine BuildWant(SimCore sim, WantCase c, string building, TownWantInfo w, int seed)
        {
            string thing = building != null ? Thing(sim, building) : null;
            if (building != null && (thing == null || !sim.Allowed(building))) return null; // never a building from a later era
            var wordings = WantWordings(sim, c, building);
            int v = (int)((uint)seed % (uint)wordings.Length);
            string t = wordings[v]
                .Replace("{Thing}", thing == null ? "" : UpFirst(thing)).Replace("{thing}", thing ?? "")
                .Replace("{place}", PlaceWord(sim)).Replace("{n}", NumberWord(Math.Max(1, w.needPop - w.pop)));
            return Allowed(t, sim) ? new WantLine { kind = c, building = building, text = t, variant = v } : null;
        }

        static string NumberWord(int n) => n switch { 1 => "One", 2 => "Two", 3 => "Three", 4 => "Four", 5 => "Five", _ => n.ToString() };

        // ======================================================================================== a wish given (#191)

        /// <summary>
        /// The wisher's own words when their wish is given (the Given beat) or met (the smaller Met beat): glad, a little
        /// surprised, never owing anything. Null if the wisher is gone or the line isn't allowed.
        /// </summary>
        public static PeopleQuote WishReaction(SimCore sim, Wish wish, bool met)
        {
            if (sim == null || wish == null || !Alive(sim, wish.notableId)) return null;
            string keeper = KeeperName(sim);
            string[] options;
            if (met)
                options = new[] { "It's all just as I dreamed. Well. Wetter.", "Look at it. I made a wish and now there's a thing.", "I'm going to go and stand in it for a bit." };
            else switch (wish.kind)
                {
                    case SimCore.NudgeKind.Rain: options = new[] { $"Rain, right where I pointed. {Cap(keeper)} heard me!", "I only wished for rain. I never thought I'd get rain.", "Look at it come down. I'm taking the credit." }; break;
                    case SimCore.NudgeKind.Grove: options = new[] { "A grove, right where I pointed. I'm going to name every tree.", $"{Cap(keeper)} planted my trees! Mind the roots.", "Trees. Actual trees. Where I said." }; break;
                    case SimCore.NudgeKind.Herd: options = new[] { "They came! I asked, and they came.", "Look who turned up. I asked for them by kind.", $"{Cap(keeper)} heard me. Look at them go." }; break;
                    case SimCore.NudgeKind.Channel: options = new[] { "A channel, exactly there. I'm going to go and stand in it.", $"The water's coming through. {Cap(keeper)} heard me!", "I wished for water and the ground just opened. Lovely." }; break;
                    default: options = new[] { "I wished and it came. I'm still blinking." }; break;
                }
            string text = Pick(wish.id + (int)wish.state, options);
            if (!Allowed(text, sim)) return null;
            return new PeopleQuote { lineNo = 0, speakerId = wish.notableId, text = text };
        }

        /// <summary>What someone awake says at night when somebody else's wish lands (the wisher is asleep and says theirs on waking).</summary>
        public static PeopleQuote WishWitness(SimCore sim, int witnessId, Wish wish)
        {
            if (sim == null || wish == null || !Alive(sim, witnessId)) return null;
            string[] options = { "Something new turned up out there. Somebody's wish, I'd bet.", "Did you see that? Somebody asked and somebody answered." };
            string text = Pick(wish.id, options);
            return Allowed(text, sim) ? new PeopleQuote { lineNo = 0, speakerId = witnessId, text = text } : null;
        }
    }
}
