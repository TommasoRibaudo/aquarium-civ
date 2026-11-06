// #358 Kids' play 1: which game a play group plays, and how the kids at one place split into groups (docs/design/animation-specs.md
// B2, research 17 §4.6). Pure (System only): Tools/SimTests and Tools/AnimBench compile it. The view (KidPlayDirector) asks this
// every time the kids at a place change; the motion of each game is View/Beats/KidPlay.cs.
using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>The games a play group can play (B2). Appended values keep their numbers.</summary>
    public enum PlayGame
    {
        None,
        /// <summary>B2.a: one chases, the others scatter; a touch swaps who's "it".</summary>
        Tag,
        /// <summary>B2.b: a big ball tossed between 2–3 kids (alone: tossed ahead and fetched).</summary>
        Ball,
        /// <summary>B2.c: creep up on the real rabbits, charge, they scatter, the kids laugh.</summary>
        ChaseRabbits,
        /// <summary>B2.d: a little one wobbles after its carer.</summary>
        Toddle,
        /// <summary>B2.e: a ring that turns as one (weddings, the festival).</summary>
        DanceRing,
        // B3 (#359) adds its games here, after DanceRing: Splash, Woods, HideSeek.
    }

    /// <summary>One play group the split asks for: its game, its members (person keys, in order) and where it plays.</summary>
    public sealed class PlayGroupPlan
    {
        public PlayGame game;
        public int seed;
        public int[] members;
        /// <summary>Where the group plays (box units): the place's spot moved out onto open ground, groups ≥ <see cref="PlayGroups.Apart"/> apart.</summary>
        public float x, z;
    }

    /// <summary>
    /// The planner side of kids' play (B2): the kids playing at one place split into groups of 2–<see cref="MaxGroup"/>, each a game the
    /// group next to it isn't playing (principle 6: neighbours never share one move), spread round the place so each group has the space
    /// a game needs. The choice is a hash of (place, day, 2-hour block): no sim dice, no sim writes, so the view, AnimBench and the tests agree.
    /// </summary>
    public static class PlayGroups
    {
        /// <summary>Tag takes up to four (more and three runners share one move), the ball and the rabbit chase three.</summary>
        public const int MaxGroup = 4, MaxBall = 3, MaxChase = 3;
        /// <summary>Two groups at one place play at least this far apart (§0.8).</summary>
        public const float Apart = 1f;
        /// <summary>How far from the place's spot a group plays (off the fire, onto open ground), and the hours a game lasts before the group picks again.</summary>
        public const float Out = 0.6f, BlockHours = 2f;

        public static uint Hash(int a, long b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)(b * 0x85EBCA77L) ^ ((uint)c + 0x632BE5ABu) * 0xC2B2AE35u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return h;
            }
        }

        public static float U01(int a, long b, int c) => (Hash(a, b, c) >> 8) / 16777216f;

        /// <summary>The group's own seed: the place, the day, the block of the day and the group's index there.</summary>
        public static int Seed(int placeHash, long day, float hour, int index)
            => (int)(Hash(placeHash, day, (int)Math.Floor(hour / BlockHours) * 31 + index * 7 + 358) & 0x7fffffff);

        /// <summary>The games a group of <paramref name="size"/> can play.</summary>
        public static bool Fits(PlayGame g, int size) => g switch
        {
            PlayGame.Tag => size >= 2 && size <= MaxGroup,
            PlayGame.Ball => size >= 1 && size <= MaxBall,
            PlayGame.ChaseRabbits => size >= 1 && size <= MaxChase,
            _ => false,
        };

        static readonly PlayGame[] KidGames = { PlayGame.Tag, PlayGame.Ball, PlayGame.ChaseRabbits };

        /// <summary>
        /// The game for a group: one that fits its size, isn't <paramref name="taken"/> by another group at the place (principle 6), and the
        /// rabbit chase only with <paramref name="rabbits"/> near (and a hunt not on). A hashed order, so the box doesn't play one game all day.
        /// </summary>
        public static PlayGame Pick(int seed, int size, bool rabbits, ICollection<PlayGame> taken, bool roomy = true)
        {
            if (!roomy) return PlayGame.Ball; // by a home: the ball (a small triangle); tag and the chase need the open green
            int start = (int)(Hash(seed, size, 11) % (uint)KidGames.Length);
            // The rabbits are the best show when they are there: the chase comes first one time in two.
            if (rabbits && Fits(PlayGame.ChaseRabbits, size) && (taken == null || !taken.Contains(PlayGame.ChaseRabbits)) && U01(seed, size, 12) < 0.5f)
                return PlayGame.ChaseRabbits;
            for (int k = 0; k < KidGames.Length; k++)
            {
                var g = KidGames[(start + k) % KidGames.Length];
                if (!Fits(g, size)) continue;
                if (g == PlayGame.ChaseRabbits && !rabbits) continue;
                if (taken != null && taken.Contains(g)) continue;
                return g;
            }
            // Everything taken (four groups at one place): a ball for small groups, else tag; the 1 u spacing keeps them apart.
            return size <= MaxBall ? PlayGame.Ball : PlayGame.Tag;
        }

        /// <summary>How many groups <paramref name="n"/> kids make (at most <see cref="MaxGroup"/> each, as even as can be).</summary>
        public static int GroupCount(int n, bool roomy = true) { int m = roomy ? MaxGroup : MaxBall; return n <= 0 ? 0 : (n + m - 1) / m; }

        /// <summary>
        /// Splits the kids playing at one place (<paramref name="kids"/>: person keys, any order) into groups, picks each a game and a spot.
        /// <paramref name="px"/>, <paramref name="pz"/> is the place's spot; <paramref name="rabbitsAt"/> says whether rabbits are near a spot
        /// (null: none anywhere); <paramref name="open"/> moves a spot onto open ground in the box (null: as it is). Not
        /// <paramref name="roomy"/> (a home's yard): groups of up to three, the ball only.
        /// </summary>
        public static void Split(IReadOnlyList<int> kids, int placeHash, long day, float hour, float px, float pz,
                                 Func<float, float, bool> rabbitsAt, Func<float, float, (float x, float z)> open, List<PlayGroupPlan> into, bool roomy = true)
        {
            into.Clear();
            int n = kids?.Count ?? 0;
            if (n == 0) return;
            var sorted = new List<int>(kids);
            sorted.Sort();
            int groups = GroupCount(n, roomy);
            float turn = U01(placeHash, day, 13) * (float)Math.PI * 2f;
            var taken = new List<PlayGame>();
            int at = 0;
            for (int gi = 0; gi < groups; gi++)
            {
                int size = n / groups + (gi < n % groups ? 1 : 0);
                var members = sorted.GetRange(at, size).ToArray();
                at += size;
                // One group plays just off the spot; several spread round it, far enough apart (a chord of ≥ Apart).
                float r = groups == 1 ? (roomy ? Out : 0f) : Math.Max(Out, Apart * 0.5f / (float)Math.Sin(Math.PI / groups));
                float a = turn + gi * (float)Math.PI * 2f / groups;
                float x = px + (float)Math.Cos(a) * r, z = pz + (float)Math.Sin(a) * r;
                if (open != null) (x, z) = open(x, z);
                int seed = Seed(placeHash, day, hour, gi);
                bool rabbits = rabbitsAt != null && rabbitsAt(x, z);
                var g = Pick(seed, size, rabbits, taken, roomy);
                taken.Add(g);
                into.Add(new PlayGroupPlan { game = g, seed = seed, members = members, x = x, z = z });
            }
        }
    }

    public sealed partial class LifePlanner
    {
        /// <summary>#358: the gathering place (the fire in camp, the green later): where the kids' play cheats gather kids.</summary>
        public Place GatherPlace { get { Sync(); return gather; } }
    }
}
