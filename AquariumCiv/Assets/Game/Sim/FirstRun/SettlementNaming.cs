using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AquariumCiv.Sim
{
    public enum NameProblem { None, Empty, TooShort, TooLong, BadCharacters, NotEnoughLetters, Repeated, Blocked }

    /// <summary>
    /// Naming the settlement (#94; decisions.md "First session"): the brass plate's name. Rules are kind, not strict: trim,
    /// 2 to 20 characters, letters, digits and a few marks, a light filter for the worst words (whole words, leetspeak
    /// folded, and a few unmistakable stems anywhere), and a message the UI can show as it stands. Pure C#, no dice.
    /// </summary>
    public static class SettlementNaming
    {
        public const int MinLength = 2, MaxLength = 20;

        /// <summary>Names offered as the suggestion, in the order a reroll walks them (the sim's own list first).</summary>
        public static readonly string[] Defaults =
        {
            "Glasswick", "Brightmere", "Little Hollow", "Keepersend", "Pebbleford", "Dewmoor", "Lanternfold",
            "Mossbarrow", "Fernhollow", "Larkspur", "Thistledown", "Wrenfield", "Amberwick", "Cloverbank",
            "Hearthmere", "Quillby", "Rushlight", "Sunnyglen", "Tansy Green", "Willowfen", "Marrowby", "Penny Brook",
        };

        // Whole tokens that are refused. Light on purpose: a place name, not a chat room.
        static readonly HashSet<string> BlockedWords = new()
        {
            "fuck", "fucker", "fucking", "shit", "shite", "bitch", "cunt", "dick", "cock", "piss", "slut", "whore", "bastard",
            "nigger", "nigga", "faggot", "fag", "retard", "rape", "rapist", "nazi", "hitler", "asshole", "arse", "wank", "twat",
            "porn", "cum", "anus", "penis", "vagina", "tits", "boobs", "sex", "kkk",
        };

        // Stems refused even inside a longer word, once spaces are squeezed out ("Fuck Ton" -> "fuckton"). Only unmistakable ones.
        static readonly string[] BlockedStems = { "fuck", "shit", "bitch", "nigg", "faggot", "whore", "asshole", "hitler" };

        /// <summary>Checks and tidies a typed name. <paramref name="clean"/> is the tidied text (trimmed, spaces squeezed,
        /// lower-case names capitalised) whether or not it passed.</summary>
        public static NameProblem Check(string raw, out string clean)
        {
            clean = Tidy(raw);
            if (clean.Length == 0) return NameProblem.Empty;
            if (clean.Length < MinLength) return NameProblem.TooShort;
            if (clean.Length > MaxLength) return NameProblem.TooLong;
            foreach (char c in clean)
                if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'' || c == '.')) return NameProblem.BadCharacters;
            if (clean.Count(char.IsLetter) < 2) return NameProblem.NotEnoughLetters;
            if (HasRun(clean, 4)) return NameProblem.Repeated;
            if (IsBlocked(clean)) return NameProblem.Blocked;
            return NameProblem.None;
        }

        public static bool IsValid(string raw) => Check(raw, out _) == NameProblem.None;

        /// <summary>A short line the UI can show under the field for a problem ("" for none).</summary>
        public static string Message(NameProblem p) => p switch
        {
            NameProblem.Empty => "Give it a name.",
            NameProblem.TooShort => "A little longer?",
            NameProblem.TooLong => $"Up to {MaxLength} letters.",
            NameProblem.BadCharacters => "Letters and numbers, please.",
            NameProblem.NotEnoughLetters => "It needs a few letters.",
            NameProblem.Repeated => "That's a lot of the same letter.",
            NameProblem.Blocked => "Not that one. Try another?",
            _ => "",
        };

        /// <summary>Trim, squeeze runs of spaces, strip dangling marks at the ends, and capitalise a name typed all in lower case.</summary>
        public static string Tidy(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var sb = new StringBuilder();
            bool space = false;
            foreach (char c in raw.Trim())
            {
                if (char.IsWhiteSpace(c)) { space = true; continue; }
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            string s = sb.ToString().Trim('-', '\'', '.', ' ');
            if (s.Length > 0 && !s.Any(char.IsUpper))
                s = string.Join(" ", s.Split(' ').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1)));
            return s;
        }

        /// <summary>The suggested name for a world: stable for a seed, so the plate shows the same one after a restart.</summary>
        public static string Suggest(int seed, int reroll = 0)
        {
            uint h = (uint)seed * 0x9E3779B1u ^ 0x5BD1E995u;
            h ^= h >> 15; h *= 0x85EBCA77u; h ^= h >> 13;
            return Defaults[(int)((h + (uint)Math.Max(0, reroll)) % (uint)Defaults.Length)];
        }

        static bool HasRun(string s, int length)
        {
            int run = 1;
            for (int i = 1; i < s.Length; i++)
            {
                run = char.ToLowerInvariant(s[i]) == char.ToLowerInvariant(s[i - 1]) && char.IsLetter(s[i]) ? run + 1 : 1;
                if (run >= length) return true;
            }
            return false;
        }

        /// <summary>Lower case, leetspeak folded, repeats squeezed to two: "Fuuuck" and "sh1t" are seen as what they are.</summary>
        static string Fold(string s)
        {
            var sb = new StringBuilder();
            foreach (char raw in s.ToLowerInvariant())
            {
                char c = raw switch { '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's', '7' => 't', '@' => 'a', '$' => 's', '!' => 'i', _ => raw };
                int n = sb.Length;
                if (n >= 2 && sb[n - 1] == c && sb[n - 2] == c) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        static bool IsBlocked(string s)
        {
            string folded = Fold(s);
            var tokens = folded.Split(new[] { ' ', '-', '\'', '.' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in tokens)
            {
                // "fuuck" folds to "fuuck" (two u's stay); also try with every repeat squeezed to one letter.
                if (BlockedWords.Contains(t) || BlockedWords.Contains(Single(t))) return true;
            }
            string squeezed = Single(new string(folded.Where(char.IsLetter).ToArray()));
            return BlockedStems.Any(stem => squeezed.Contains(stem));
        }

        static string Single(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if (sb.Length == 0 || sb[sb.Length - 1] != c) sb.Append(c);
            return sb.ToString();
        }
    }
}
