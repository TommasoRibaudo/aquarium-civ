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
        /// <summary>B3.a (#359): two kids splash each other at the real water (the pond's edge, a puddle); now and then one jumps in.</summary>
        Splash,
        /// <summary>B3.b (#359): two kids round a real grown tree: chasing round the trunk and peeking, then a stick duel.</summary>
        Woods,
        /// <summary>B3.c (#359): one counts at a tent or a tree, the others hide behind real cover; found, they race back.</summary>
        HideSeek,
        /// <summary>#359 (B2's stretch): a snowball fight on a snow day.</summary>
        Snowball,
        /// <summary>#618: on a frozen pond big enough: in turn, a run-up and a long slide across the ice, arms out; now and then a fall.</summary>
        IceSlide,
    }

    /// <summary>#359: what a group's spot offers (the owner looks at the real land): the B3 games need their real thing.</summary>
    [Flags]
    public enum PlaySites
    {
        None = 0,
        /// <summary>Rabbits within reach (B2.c).</summary>
        Rabbits = 1,
        /// <summary>The kids are at the water (the pond with water in it, a puddle), not in winter: they splash (B3.a).</summary>
        Water = 2,
        /// <summary>A grown tree within reach (B3.b).</summary>
        Tree = 4,
        /// <summary>A base (a tent, a tree, the pile) with cover round it for every hider (B3.c).</summary>
        Cover = 8,
        /// <summary>Snow on the ground: a snowball fight.</summary>
        Snow = 16,
        /// <summary>#618: the kids are at a frozen pond big enough to go on: they slide.</summary>
        Ice = 32,
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
            PlayGame.Splash => size >= 2 && size <= MaxGroup, // #359
            PlayGame.Woods => size == 2,
            PlayGame.HideSeek => size >= 3 && size <= MaxGroup,
            PlayGame.Snowball => size >= 2 && size <= MaxGroup,
            PlayGame.IceSlide => size >= 2 && size <= MaxGroup, // #618
            _ => false,
        };

        /// <summary>
        /// #359: the game for a group by what its spot offers. At the water the water is the game (splash); snow makes a snowball fight;
        /// a grown tree near (a pair) or cover round a base (three or four) each come first one time in two, so the woods and hide and
        /// seek turn up as often as the rabbit chase; otherwise B2's pick. Never a game another group here is playing (principle 6),
        /// except the splash (the groups stand ≥ <see cref="Apart"/> apart along the bank).
        /// </summary>
        public static PlayGame Pick(int seed, int size, PlaySites sites, ICollection<PlayGame> taken, bool roomy = true)
        {
            bool Free(PlayGame g) => Fits(g, size) && (taken == null || !taken.Contains(g));
            if ((sites & PlaySites.Ice) != 0 && Fits(PlayGame.IceSlide, size)) return PlayGame.IceSlide; // #618: the ice is the game
            if ((sites & PlaySites.Water) != 0 && Fits(PlayGame.Splash, size)) return PlayGame.Splash;
            if (roomy)
            {
                if ((sites & PlaySites.Snow) != 0 && Free(PlayGame.Snowball)) return PlayGame.Snowball;
                bool woods = (sites & PlaySites.Tree) != 0 && Free(PlayGame.Woods), hide = (sites & PlaySites.Cover) != 0 && Free(PlayGame.HideSeek);
                if (woods && U01(seed, size, 14) < 0.5f) return PlayGame.Woods;
                if (hide && U01(seed, size, 15) < 0.5f) return PlayGame.HideSeek;
                var g = Pick(seed, size, (sites & PlaySites.Rabbits) != 0, taken, true);
                if (taken != null && taken.Contains(g)) { if (woods) return PlayGame.Woods; if (hide) return PlayGame.HideSeek; } // B2's fallback repeats a game: a free B3 one instead
                return g;
            }
            return Pick(seed, size, false, taken, false);
        }

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

        /// <summary>#541: one place's free kids, for <see cref="Gather"/>: an id (any stable int), where it is, how many kids, and a class
        /// (a home's yard and open ground don't mix: only spots of one class gather).</summary>
        public readonly struct KidSpot
        {
            public readonly int id, count, cls; public readonly float x, z;
            public KidSpot(int id, float x, float z, int count, int cls = 0) { this.id = id; this.x = x; this.z = z; this.count = count; this.cls = cls; }
        }

        /// <summary>#541 reach: lone kids this near each other (box units) play one game together instead of two solo copies.</summary>
        public const float GatherReach = 3.2f;

        /// <summary>
        /// #541: two kids on view never play alone side by side. Each spot holding one kid joins the nearest other spot (same class, within
        /// <paramref name="reach"/>) whose group still has room (<see cref="MaxGroup"/>), so two free kids become one group that plays a together
        /// game (tag, splash, a partner game). Returns, per spot, the index of the spot whose group it plays in (itself when it stays).
        /// Pure and order-stable: sorted by (count, id), ties to the nearest then the lowest id.
        /// </summary>
        public static int[] Gather(IReadOnlyList<KidSpot> spots, float reach = GatherReach)
        {
            int n = spots.Count;
            var host = new int[n]; var size = new int[n];
            for (int i = 0; i < n; i++) { host[i] = i; size[i] = spots[i].count; }
            var order = new List<int>();
            for (int i = 0; i < n; i++) if (spots[i].count == 1) order.Add(i);
            order.Sort((a, b) => spots[a].id.CompareTo(spots[b].id));
            foreach (int i in order)
            {
                if (host[i] != i || size[i] != 1) continue; // already someone's host
                int best = -1; float bestD = reach * reach;
                for (int j = 0; j < n; j++)
                {
                    if (j == i || host[j] != j || spots[j].cls != spots[i].cls || size[j] + 1 > MaxGroup) continue;
                    float dx = spots[j].x - spots[i].x, dz = spots[j].z - spots[i].z, d = dx * dx + dz * dz;
                    if (d < bestD || (d == bestD && best >= 0 && spots[j].id < spots[best].id)) { bestD = d; best = j; }
                }
                if (best < 0) continue;
                host[i] = best; size[best]++;
            }
            return host;
        }

        /// <summary>#541: a game for a group that never repeats a game another group in view is playing (two solo ball games side by side
        /// look like a copy): a game that fits <paramref name="size"/> and isn't in <paramref name="inView"/>, the pick first, else the first free.</summary>
        public static PlayGame Differ(PlayGame pick, int size, ICollection<PlayGame> inView, bool roomy = true)
        {
            if (inView == null || !inView.Contains(pick)) return pick;
            if (!roomy) return pick; // a yard has the one game
            foreach (var g in new[] { PlayGame.Tag, PlayGame.Ball, PlayGame.ChaseRabbits })
                if (g != PlayGame.ChaseRabbits && Fits(g, size) && !inView.Contains(g)) return g;
            return pick;
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
            => Split(kids, placeHash, day, hour, px, pz, rabbitsAt == null ? null : (x, z) => rabbitsAt(x, z) ? PlaySites.Rabbits : PlaySites.None, open, into, roomy);

        /// <summary>#359: <see cref="Split(IReadOnlyList{int}, int, long, float, float, float, Func{float, float, bool}, Func{float, float, ValueTuple{float, float}}, List{PlayGroupPlan}, bool)"/>
        /// with everything a spot offers (<paramref name="sitesAt"/>: the rabbits, the water, a tree, cover, snow).</summary>
        public static void Split(IReadOnlyList<int> kids, int placeHash, long day, float hour, float px, float pz,
                                 Func<float, float, PlaySites> sitesAt, Func<float, float, (float x, float z)> open, List<PlayGroupPlan> into, bool roomy = true)
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
                var sites = sitesAt != null ? sitesAt(x, z) : PlaySites.None;
                var g = Pick(seed, size, sites, taken, roomy);
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
