using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AquariumCiv.Sim
{
    /// <summary>Who says a line: the narrator (cards, History) or the people (quotes, bubbles, asks), who never name the game.</summary>
    public enum LineVoice { Narrator, People }

    /// <summary>
    /// #723 (research 21 §4.7, the closed-world rule): what every line of text must be true to, in one place, so a content test can read
    /// every template on every PR. Five checks: no outside world, none of the words that state the game in the people's voice, nothing
    /// the box never has, the era's words (the people say "window" from belief stage 2 and "glass" from stage 3), and the writing
    /// guide's style (no em or en dashes, straight quotes, asks of 15 words or fewer). The gated things (a line about the well needs a
    /// well) are <see cref="Gates"/>. Pure text; the tests call it, the game never does.
    /// </summary>
    public static class ContentRules
    {
        /// <summary>The outside world. "From a far-away tank" is the one allowed phrase (the travellers' fiction).</summary>
        public static readonly string[] OutsideWorld = { "over the hill", "far away", "far-away", "outside", "the world beyond", "the sea" };
        public const string AllowedFarAway = "from a far-away tank";

        /// <summary>Words the people never say: they state the game.</summary>
        public static readonly string[] NeverSaid = { "aquarium", "box", "player", "phone", "game", "terrarium", "screen" };

        /// <summary>Things the box never has.</summary>
        public static readonly string[] NotInTheBox = { "goat", "goats", "sheep", "hen", "hens", "fox", "kitten", "cat", "hills", "chimney", "hat day", "sack race", "pies", "library" };

        /// <summary>A people's ask (a choice's question) has this many words at most.</summary>
        public const int AskMaxWords = 15;

        static IEnumerable<string> Words(string text) => Regex.Split(text.ToLowerInvariant(), "[^a-z'-]+").Select(w => w.Trim('\'', '-')).Where(w => w.Length > 0);

        static bool Says(string lower, IEnumerable<string> words, string phrase) =>
            phrase.Contains(' ') ? Regex.IsMatch(lower, @"(^|[^a-z])" + Regex.Escape(phrase) + "($|[^a-z])") : words.Contains(phrase);

        /// <summary>
        /// The problems with one line (empty = it is fine). <paramref name="stage"/>: the people's belief stage when it is said (0 = any,
        /// not checked); <paramref name="ask"/>: a choice's question (the word cap). Placeholders ({a}, {0}) are fine: fill them first if
        /// the line is checked as said.
        /// </summary>
        public static List<string> Problems(string line, LineVoice voice, int stage = 0, bool ask = false)
        {
            var bad = new List<string>();
            if (string.IsNullOrEmpty(line)) return bad;
            string lower = line.ToLowerInvariant();
            var words = Words(line).ToList();
            string outside = lower.Replace(AllowedFarAway, "");
            foreach (var p in OutsideWorld) if (Says(outside, Words(outside).ToList(), p)) bad.Add($"outside world: \"{p}\"");
            foreach (var p in NotInTheBox) if (Says(lower, words, p)) bad.Add($"not in the box: \"{p}\"");
            if (voice == LineVoice.People)
            {
                foreach (var p in NeverSaid) if (words.Contains(p)) bad.Add($"the people never say \"{p}\"");
                if (stage > 0 && stage < 3 && (words.Contains("glass") || words.Contains("glass's"))) bad.Add("\"glass\" before stage 3");
                if (stage == 1 && (words.Contains("window") || words.Contains("window's") || words.Contains("windows"))) bad.Add("\"window\" in stage 1");
            }
            if (line.IndexOf('—') >= 0 || line.IndexOf('–') >= 0) bad.Add("an em or en dash");
            if (line.IndexOfAny(new[] { '“', '”', '‘', '’' }) >= 0) bad.Add("a curly quote");
            if (ask && words.Count > AskMaxWords) bad.Add($"an ask of {words.Count} words (at most {AskMaxWords})");
            return bad;
        }

        /// <summary>
        /// The gated things: a line that names one is said only when the box has it. Keyed by the words that name it; the predicate
        /// is the box's own state (the sim test checks every chronicle line as it is written).
        /// </summary>
        public static readonly (string thing, string[] words, Func<SimCore, bool> has)[] Gates =
        {
            ("the well", new[] { "the well" }, s => s.Count(BuildingType.Well) > 0),
            ("a dog", new[] { "dog", "dogs" }, s => s.AllDogs.Count > 0 || !s.EcologyOn),
            ("the Keeper's tree", new[] { "the keeper's tree" }, s => s.KeeperTreeGrown),
            ("the pond", new[] { "pond" }, s => s.OpenWater),
            ("the stream", new[] { "stream" }, s => s.OpenWater), // #652: no "stream" while the land shows no water (its dry ground is "the hollow")
            ("the market", new[] { "the market" }, s => s.Count(BuildingType.Market) > 0),
            ("two or more kids", new[] { "children", "kids" }, s => s.KidCount >= 2), // a line about "the children" needs at least two; one kid is "the baby" or "the little one"
            ("water for mud", new[] { "mud", "muddy" }, s => s.OpenWater), // no water, no mud: the dry land is "the dry hollow"
            ("a bench", new[] { "bench", "benches" }, s => false), // the box has no bench prop (people-and-things.md); the fire is where they sit
            ("a broom", new[] { "broom", "brooms" }, s => s.Sweepers > 0), // only the town's sweepers carry one
        };

        /// <summary>The gated things a line names that the box does not have now (empty = fine).</summary>
        public static List<string> MissingThings(string line, SimCore sim)
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(line) || sim == null) return missing;
            string lower = line.ToLowerInvariant();
            var words = Words(line).ToList();
            foreach (var g in Gates)
                if (g.words.Any(w => Says(lower, words, w)) && !g.has(sim)) missing.Add(g.thing);
            return missing;
        }
    }
}
