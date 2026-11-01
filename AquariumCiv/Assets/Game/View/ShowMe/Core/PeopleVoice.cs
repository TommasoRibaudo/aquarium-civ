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
    public static partial class PeopleVoice
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
            int v = Math.Abs(e.id); // which of a kind's wordings: from the entry, so no dice and the same story reads the same
            switch (e.kind)
            {
                case "wedding": text = B != null ? Pick(v,
                    $"I married {B}. I'd do it all again tomorrow.",
                    $"{B} said yes. I'm still checking it's true.",
                    $"Married {B} today. The soup was cold and I didn't notice.") : null; break;
                case "birth":
                {
                    var child = sim.FindNotable(a);
                    speaker = child?.parentId ?? -1;
                    // Brothers and sisters get different lines: the wording follows the child's place in the family.
                    int rank = child == null ? 0 : sim.State.notables.Count(c => c.parentId == child.parentId && c.id < child.id);
                    text = child != null ? Pick(rank + Math.Max(0, speaker),
                        $"Little {child.name} has my nose. And my appetite.",
                        $"{child.name} cried all night. Best sound I've ever heard.",
                        $"{child.name} grabbed my finger and won't let go. I'm not trying hard.",
                        $"I counted {child.name}'s toes twice. All there. I may count again.",
                        $"Everyone's holding {child.name} but me. My turn. My turn now.") : null;
                    break;
                }
                case "friendship": text = B != null ? Pick(v,
                    $"{B} is the best friend a person could have.",
                    $"{B} laughs at my jokes. All of them. Even the bad ones.",
                    $"If {B} says it's a good idea, it's at least half a good idea.") : null; break;
                case "rivalry": text = B != null ? Pick(v,
                    $"{B}? We don't speak. Well. I don't.",
                    $"I'm not saying {B} is wrong. I'm saying I was there first.",
                    $"Ask {B} who's better at it. Then ask anyone else.") : null; break;
                case "reconcile": text = B != null ? Pick(v,
                    $"{B} and I made up. Over soup, mostly.",
                    $"{B} and I are talking again. Neither of us mentioned it. That's how you know.",
                    $"I shared my bread with {B}. We never said sorry. We didn't need to.") : null; break;
                case "apprentice": text = B != null ? Pick(v,
                    $"{B} is teaching me everything. Slowly.",
                    $"{B} says I'm getting better. {B} says it through gritted teeth.",
                    $"I've learned one thing from {B}: it's harder than it looks.") : null; break;
                case "keeper":
                    text = sim.BeliefStage <= 1 ? Pick(v,
                            "The sky sent us a sign. I was the one who answered it.",
                            "Something came from the sky and we were ready. Nearly ready.")
                        : sim.BeliefStage == 2 ? Pick(v,
                            "The Keeper gave, and I answered. Hats off.",
                            "The Keeper gave us a gift and I said thank you first. Write that down.",
                            "A gift from the Keeper! I waved. I hope that was right.")
                        : Pick(v,
                            "The Keeper sends, we build. I wrote it all down.",
                            "The Keeper sent something and we put it to use. I have notes.");
                    break;
                case "invention": text = Pick(v,
                    "I made a thing. It mostly works.",
                    "I invented something today. Ask me what it's for tomorrow.",
                    "Nobody asked for it, and now nobody can do without it."); break;
                case "giant_turnip": text = Pick(v,
                    "That turnip? Grew it myself. Talked to it every morning.",
                    "I knew that turnip was special. I told it so, daily.",
                    "Biggest turnip anyone's seen. I'm trying to be humble about it."); break;
                case "prank": text = Pick(v,
                    "Nobody can prove it was me.",
                    "I was nowhere near the well. Ask anyone who isn't looking at me.",
                    "It was funny, though. Admit it was funny."); break;
                case "song": text = Pick(v,
                    "I made up a song. You'll be humming it by supper.",
                    "That song stuck, didn't it? I'm very sorry. Slightly.",
                    "I've got a new song. It has a part where you clap."); break;
                case "brave": text = Pick(v,
                    "I wasn't scared. Much.",
                    "I did it before I thought about it. That's the secret.",
                    "My knees were shaking. They were just warming up."); break;
                case "kind": text = Pick(v,
                    "It was nothing. Anyone would have.",
                    "Please don't make a fuss. All right, a small fuss.",
                    "I only did what was in front of me."); break;
                case "dreamer": text = Pick(v,
                    "I had the strangest dream. I'll tell you at supper.",
                    "I dreamt I could fly. I woke up on the floor, so close.",
                    "I dreamt of a table as long as the river. Everyone had a seat."); break;
                case "lazy": text = Pick(v,
                    "I'm resting. It's harder than it looks.",
                    "I'm saving my energy. For what, I'll know when it comes.",
                    "I'm thinking about getting up. It's going well."); break;
                case "loud": text = Pick(v,
                    "WHAT? I'm not shouting.",
                    "I only have the one volume. It's a good volume.",
                    "Did I say that too loudly? Sorry. Sorry! Sorry."); break;
                case "lost": text = Pick(v,
                    "I got lost in the hills. The hills started it.",
                    "I wasn't lost. I was finding a longer way.",
                    "Every path looked like the right one. That was the problem."); break;
                case "found": text = Pick(v,
                    "Lost and found. Mostly found.",
                    "They found me before I'd finished getting lost. Rude.",
                    "I knew the way home all along. I just wanted the view."); break;
                case "elder": text = Pick(v,
                    "I'm not old. I'm early.",
                    "I've seen it all before. Not this exact bit, but near it.",
                    "Sit down, I'll tell you how it used to be. It was worse. Or better. Sit down."); break;
                case "newcomer": text = Pick(v,
                    "I'm new here. Everyone waves. I like it.",
                    "They gave me a bowl of soup before they asked my name. I'm staying.",
                    "I don't know where anything is yet. Everyone points. It's lovely."); break;
                case "tapback": text = Pick(v,
                    $"{Cap(keeper)} knocked. I knocked back. I wrote it down.",
                    $"{Cap(keeper)} knocked, so I knocked. Politely. Same number of times."); break;
                case "death":
                case "role_death":
                {
                    var dead = sim.FindNotable(a);
                    if (dead == null) return null;
                    speaker = Alive(sim, dead.partnerId) ? dead.partnerId : Alive(sim, dead.friendId) ? dead.friendId : -1;
                    text = Pick(v,
                        $"I miss {dead.name}. I still save a seat by the fire.",
                        $"{dead.name} would have laughed at this. I'm laughing for both of us.",
                        $"I still say good morning to {dead.name}'s chair.");
                    break;
                }
                default:
                    if (e.kind.StartsWith("role_") && e.kind != "role_stepdown" && e.kind != "role_offices")
                    { text = Pick(v,
                        "I'll do my best for all of us. Mostly my best.",
                        "They asked me and I said yes before I could think. That's how it goes.",
                        "Now I have to be sensible. In public."); break; }
                    return null;
            }
            if (text == null || !Alive(sim, speaker) || !Allowed(text, sim)) return null;
            return new PeopleQuote { lineNo = 0, speakerId = speaker, text = text };
        }

        /// <summary>One of the wordings, by <paramref name="seed"/> (deterministic).</summary>
        static string Pick(int seed, params string[] options) => options[(int)((uint)seed % (uint)options.Length)];

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
