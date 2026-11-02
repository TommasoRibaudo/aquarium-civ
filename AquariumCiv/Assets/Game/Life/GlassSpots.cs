using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #190: where each person of a group at the front glass stands, pure (no engine types). It used to be a row along the
    /// glass in slot order ("everyone stood in a line looking out"); now it is a loose, uneven group: a few rows deep on a
    /// staggered grid, filled from a blob's middle outwards (some closer, some behind), in two small clusters once there
    /// are more than a handful, each person nudged a little by their own hash and turned a little toward a neighbour.
    ///
    /// Everything is a function of (group seed, slot, person key): arrivals never shift anyone already standing, and the
    /// same group looks different from the next one. The view maps <c>along</c> to the glass's tangent and <c>depth</c> to
    /// the way back from the glass (a kind's own base distance is added on top: the wary a step back, the kids at the pane).
    /// Nobody is closer than <see cref="SlotSpots.Gap"/> to anyone else.
    /// </summary>
    public static class GlassSpots
    {
        /// <summary>A stand: <c>along</c> the glass from the place's spot (either side), <c>depth</c> back from the front rank
        /// (never negative), and <c>turn</c> in radians away from squarely facing the glass (positive = toward +along).</summary>
        public struct Stand
        {
            public float along, depth, turn;
        }

        /// <summary>The grid: people in one rank this far apart; ranks this far behind each other, every other rank half a
        /// step across, so neighbours (also across ranks) are at least <see cref="SlotSpots.Gap"/> + the nudge apart.</summary>
        public const float AlongStep = 0.19f, RankStep = 0.17f;
        /// <summary>Each person's own nudge, at most this far from their grid site on each axis.</summary>
        public const float Nudge = 0.02f;
        /// <summary>The widest a person turns from the glass (a little over 30 degrees).</summary>
        public const float MaxTurn = 0.55f;

        const int Ranks = 4, Columns = 9;
        const float ClusterGap = 0.5f; // the second cluster's middle, along the glass

        // The sites in fill order for one group seed, cached for the last seed asked: the view asks per person per frame.
        static int cachedSeed = int.MinValue;
        static (int col, int rank)[] cachedOrder;
        static float cachedShift;
        static readonly object gate = new object();

        /// <summary>Where the person in <paramref name="slot"/> stands. <paramref name="seed"/> is the group's (the glass
        /// place and day, say), <paramref name="personKey"/> is the person's own.</summary>
        public static Stand Spot(int seed, int slot, int personKey)
        {
            (int col, int rank)[] order;
            float shift;
            lock (gate)
            {
                if (cachedOrder == null || cachedSeed != seed) { cachedOrder = Order(seed, out cachedShift); cachedSeed = seed; }
                order = cachedOrder;
                shift = cachedShift;
            }
            var (col, rank) = order[Math.Max(0, Math.Min(order.Length - 1, slot))];
            float along = (col - (Columns - 1) / 2) * AlongStep + ((rank & 1) == 1 ? AlongStep * 0.5f : 0f) + shift;
            float depth = rank * RankStep;
            along += (Unit(personKey, seed, 1) * 2f - 1f) * Nudge;
            depth += Unit(personKey, seed, 2) * Nudge * 2f; // only backwards: the front rank must not reach the glass
            // Some turned to each other: a third of them turn toward the middle of the group.
            float turn = 0f;
            if (Unit(personKey, seed, 3) < 0.4f)
            {
                float toMiddle = along > shift ? -1f : 1f;
                turn = toMiddle * MaxTurn * (0.4f + 0.6f * Unit(personKey, seed, 4));
            }
            else turn = (Unit(personKey, seed, 5) * 2f - 1f) * MaxTurn * 0.25f;
            return new Stand { along = along, depth = depth, turn = turn };
        }

        /// <summary>The sites of the grid ordered by how close they are to the group's middles: a blob growing from the
        /// middle, not a rank. A second cluster takes over the outer sites, so ten people read as two small knots.</summary>
        static (int col, int rank)[] Order(int seed, out float shift)
        {
            shift = (Unit(seed, 0, 7) * 2f - 1f) * AlongStep * 0.6f;
            float c2 = (Unit(seed, 0, 8) < 0.5f ? -1f : 1f) * ClusterGap;
            // The middles in grid units, the main one a little behind the front rank, so there is always someone in front.
            float depth1 = 0.6f + Unit(seed, 0, 9) * 0.6f;
            var sites = new List<(int col, int rank, float score)>();
            for (int rank = 0; rank < Ranks; rank++)
                for (int col = 0; col < Columns; col++)
                {
                    float x = (col - (Columns - 1) / 2) * AlongStep + ((rank & 1) == 1 ? AlongStep * 0.5f : 0f);
                    float y = rank * RankStep;
                    float d1 = Dist(x, y, 0f, depth1 * RankStep);
                    float d2 = Dist(x, y, c2, (depth1 + 0.4f) * RankStep) + 0.12f; // the second knot fills after the first has some
                    float jitter = (Unit(col * 31 + rank, seed, 10) - 0.5f) * 0.06f; // uneven: sites near the same range swap
                    sites.Add((col, rank, Math.Min(d1, d2) + jitter));
                }
            sites.Sort((a, b) => a.score != b.score ? a.score.CompareTo(b.score) : (a.rank * 100 + a.col).CompareTo(b.rank * 100 + b.col));
            var order = new (int col, int rank)[sites.Count];
            for (int i = 0; i < order.Length; i++) order[i] = (sites[i].col, sites[i].rank);
            return order;
        }

        static float Dist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /// <summary>A hash in [0, 1) of three ints (integer maths only, the same in Unity and dotnet).</summary>
        static float Unit(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA6Bu ^ (uint)salt * 0xC2B2AE35u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h >> 8) * (1f / 16777216f);
            }
        }
    }
}
