using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Sim;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>
    /// The people's own words (#76; chronicle-and-history §5.1, relics-and-glass-voice §5): a story in the first person by the
    /// one it's about, and the bank's story lines (the narrator's, "{0} says...") said by the one they're about. Only where
    /// a line reads well in the first person; the rest stay the narrator's. Warm, never blame, never the words that know too
    /// much (<see cref="Allowed"/>), the Keeper named as the stage names it.
    /// </summary>
    public static class PeopleVoice
    {
        /// <summary>The bank's story lines (kind C) in the speaker's own words, by line number. Line 39 (the tap-back count)
        /// stays the narrator's.</summary>
        public static readonly IReadOnlyDictionary<int, string> BankFirstPerson = new Dictionary<int, string>
        {
            [4] = "I moved the tents back from the edge of the sky. Just a little. Just in case.",
            [6] = "I touched the edge of the sky. It was cold. I'm not scared of it.",
            [11] = "The Bright One sneezed last spring. I heard it. Half the camp believes me.",
            [12] = "I dreamt I climbed to the edge of the sky and knocked. Something knocked back.",
            [21] = "The Keeper looks in at all hours. It isn't polite. I combed my hair anyway.",
            [25] = "The Keeper hums while working. Prove me wrong.",
            [26] = "I've been practising a wink for the Keeper. Nearly got it.",
            [35] = "I made a mirror for signalling the Keeper. It works. Probably.",
            [36] = "I can tell the time from the glass's shadow now. Ask me.",
            [37] = "I dreamt of a city beyond the glass. I've drawn a map of it.",
            [38] = "The Keeper has a cat. I'm only saying what I heard.",
        };

        /// <summary>Words the people never say (§5.1 "Never stated").</summary>
        static readonly string[] Never = { "aquarium", "box", "player", "phone", "game", "terrarium", "screen" };

        /// <summary>
        /// True if the people may say <paramref name="text"/> now: none of the words that state the game, and no "glass" before
        /// the scholars name it (stage 3, after the Lens), no "window" in the camp (the sky wall has none yet).
        /// </summary>
        public static bool Allowed(string text, SimCore sim)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var w in Words(text))
            {
                if (Array.IndexOf(Never, w) >= 0) return false;
                if (sim != null && (w == "glass" || w == "glass's") && !(sim.BeliefStage >= 3 && sim.BeliefHalfNow == BeliefHalf.Later)) return false;
                if (sim != null && (w == "window" || w == "window's") && sim.BeliefStage < 2) return false;
            }
            return true;
        }

        static IEnumerable<string> Words(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsLetter(c) || c == '\'') { sb.Append(c); continue; }
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        /// <summary>The Keeper as the people name it in this stage (camp: the Bright One; later: the Keeper).</summary>
        public static string KeeperName(SimCore sim) => sim != null && sim.BeliefStage <= 1 ? "the Bright One" : "the Keeper";

        static string First(SimCore sim, int id) => id < 0 ? null : sim.FindNotable(id)?.name;

        static bool Alive(SimCore sim, int id) => id >= 0 && sim.FindNotable(id) is Notable n && n.alive;

        /// <summary>
        /// A recent story in the words of the person it's about (the speaker must be alive; for a death, the partner or friend
        /// left behind). Null for kinds that don't read well in the first person (the weather, an era, a job change) or when
        /// nobody is left to say it.
        /// </summary>
        public static PeopleQuote StoryLine(SimCore sim, ChronicleEntry e)
        {
            if (sim == null || e == null || string.IsNullOrEmpty(e.kind)) return null;
            int a = e.notableId, b = e.otherNotableId;
            string B = First(sim, b);
            string keeper = KeeperName(sim);
            int speaker = a;
            string text;
            switch (e.kind)
            {
                case "wedding": text = B != null ? $"I married {B}. I'd do it all again tomorrow." : null; break;
                case "birth":
                {
                    var child = sim.FindNotable(a);
                    speaker = child?.parentId ?? -1;
                    text = child != null ? $"Little {child.name} has my nose. And my appetite." : null;
                    break;
                }
                case "friendship": text = B != null ? $"{B} is the best friend a person could have." : null; break;
                case "rivalry": text = B != null ? $"{B}? We don't speak. Well. I don't." : null; break;
                case "reconcile": text = B != null ? $"{B} and I made up. Over soup, mostly." : null; break;
                case "apprentice": text = B != null ? $"{B} is teaching me everything. Slowly." : null; break;
                case "keeper":
                    text = sim.BeliefStage <= 1 ? "The sky sent us a sign. I was the one who answered it."
                        : sim.BeliefStage == 2 ? "The Keeper gave, and I answered. Hats off."
                        : "The Keeper sends, we build. I wrote it all down.";
                    break;
                case "invention": text = "I made a thing. It mostly works."; break;
                case "giant_turnip": text = "That turnip? Grew it myself. Talked to it every morning."; break;
                case "prank": text = "Nobody can prove it was me."; break;
                case "song": text = "I made up a song. You'll be humming it by supper."; break;
                case "brave": text = "I wasn't scared. Much."; break;
                case "kind": text = "It was nothing. Anyone would have."; break;
                case "dreamer": text = "I had the strangest dream. I'll tell you at supper."; break;
                case "lazy": text = "I'm resting. It's harder than it looks."; break;
                case "loud": text = "WHAT? I'm not shouting."; break;
                case "lost": text = "I got lost in the hills. The hills started it."; break;
                case "found": text = "Lost and found. Mostly found."; break;
                case "elder": text = "I'm not old. I'm early."; break;
                case "newcomer": text = "I'm new here. Everyone waves. I like it."; break;
                case "tapback": text = $"{Cap(keeper)} knocked. I knocked back. I wrote it down."; break;
                case "death":
                case "role_death":
                {
                    var dead = sim.FindNotable(a);
                    if (dead == null) return null;
                    speaker = Alive(sim, dead.partnerId) ? dead.partnerId : Alive(sim, dead.friendId) ? dead.friendId : -1;
                    text = $"I miss {dead.name}. I still save a seat by the fire.";
                    break;
                }
                default:
                    if (e.kind.StartsWith("role_") && e.kind != "role_stepdown" && e.kind != "role_offices")
                    { text = "I'll do my best for all of us. Mostly my best."; break; }
                    return null;
            }
            if (text == null || !Alive(sim, speaker) || !Allowed(text, sim)) return null;
            return new PeopleQuote { lineNo = 0, speakerId = speaker, text = text };
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>The bank's story lines the people can say now (this stage and half, as <see cref="SimCore.LinesNow"/>),
        /// in the speaker's own words, with who says it: the notable with the line's trait (alive, grown; the lowest id, as
        /// the bank picks). Lines nobody fits wait.</summary>
        public static IEnumerable<PeopleQuote> BankLinesNow(SimCore sim)
        {
            if (sim == null || !sim.RelicsOn) yield break;
            foreach (var l in sim.LinesNow(false))
            {
                if (l.quote || !BankFirstPerson.TryGetValue(l.no, out var text)) continue;
                if (!l.speaker.StartsWith("the ")) continue;
                var n = sim.State.notables.Where(x => x.alive && x.age >= SimCore.AdultAge && x.trait == l.speaker).OrderBy(x => x.id).FirstOrDefault();
                if (n == null || !Allowed(text, sim)) continue;
                yield return new PeopleQuote { lineNo = l.no, speakerId = n.id, text = text };
            }
        }

        /// <summary>Real seconds a bubble stays: about a reading pace, 3–6 s.</summary>
        public static float SecondsFor(string text) => Math.Max(3f, Math.Min(6f, 2.2f + 0.05f * (text?.Length ?? 0)));

        /// <summary>Phone-sized bubble lines: at most <paramref name="perLine"/> characters a line, breaking at spaces.</summary>
        public static List<string> Wrap(string text, int perLine = BubbleLineChars)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            var cur = new StringBuilder();
            foreach (var w in text.Split(' '))
            {
                if (w.Length == 0) continue;
                if (cur.Length > 0 && cur.Length + 1 + w.Length > perLine) { lines.Add(cur.ToString()); cur.Clear(); }
                if (cur.Length > 0) cur.Append(' ');
                cur.Append(w);
            }
            if (cur.Length > 0) lines.Add(cur.ToString());
            return lines;
        }

        /// <summary>A bubble line on a phone: ~26 characters at the bubble's font size; at most <see cref="BubbleMaxLines"/>.</summary>
        public const int BubbleLineChars = 26, BubbleMaxLines = 4;
    }
}
