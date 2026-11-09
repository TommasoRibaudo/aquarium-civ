using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>What a deer is doing at a moment (the view picks the doll's pose from this, #425).</summary>
    public enum DeerAct : byte { Graze, LookUp, Walk, Bolt, Drink }

    /// <summary>One deer at one moment: where, which way it faces, what it does, and whether it is the stag (antlers).</summary>
    public struct DeerPose
    {
        public float x, z, yaw;
        public DeerAct act;
        public bool stag;
    }

    /// <summary>
    /// #420: the pure model of the deer you see (placed-life spec §5.3). No Unity, no dice but hashes of (seed, deer, leg), so the
    /// same seed and time give the same pose on every run (as <see cref="RabbitHerd"/>). <see cref="Field"/> holds the herd's range:
    /// dry cells within <see cref="GrazeNear"/> of a grown tree, round the home the Keeper placed them on (or, while the herd is shy,
    /// the far side of the wood: SimCore.EcoDeerHome). The deer graze, look up and walk between spots in legs of 12-20 s. At dawn
    /// (<see cref="DawnFrom"/> to <see cref="DawnTo"/> view hours) the does walk to the pond's edge and drink, when there is a pond
    /// (#427: nobody hunts at the water). <see cref="BoltSpot"/> is the flee rule the view applies (a hunt, a knock). Deer id 0 is the
    /// stag; ids below the herd's stag count are stags.
    /// </summary>
    public static class DeerHerd
    {
        /// <summary>The most deer the phone shows (the cap's top, spec §4.1).</summary>
        public const int DefaultCap = 8;
        public const float LegBase = 12f, LegSpread = 8f, WalkSpeed = 0.06f, GrazeNear = 0.3f, RangeRadius = 0.7f;
        /// <summary>The dawn drink, in view hours.</summary>
        public const float DawnFrom = 5f, DawnTo = 7f;
        /// <summary>A bolting deer runs at least this far, into the range away from the threat.</summary>
        public const float BoltRun = 0.6f;

        static uint H(uint a, uint b, uint c) => RabbitHerd.H(a, b, c);
        static float F(uint a, uint b, uint c) => RabbitHerd.F(a, b, c);

        /// <summary>How many deer to put on screen: the sim count, capped for the phone.</summary>
        public static int Shown(int simDeer, int cap = DefaultCap) => Math.Max(0, Math.Min(cap, simDeer));

        /// <summary>The herd's ground and its spots. The view (#425) calls <see cref="Refresh"/> each season and when the herd's home
        /// changes (a shy herd moves away), <see cref="Sync"/> when the count changes, then <see cref="Sample"/> per deer per frame.</summary>
        public sealed class Field
        {
            readonly List<int> range = new List<int>();
            readonly List<int> drinks = new List<int>();
            readonly List<int> alive = new List<int>();
            int seed, home = -1, nextId, stags = 1;

            public int Seed => seed;
            public int HomeCell => home;
            /// <summary>Ids of the deer shown now (stable: a deer the hunt took is gone for good).</summary>
            public IReadOnlyList<int> Alive => alive;
            /// <summary>The range has somewhere to graze (a grown tree near the home).</summary>
            public bool HasRange => range.Count > 0;
            /// <summary>There is a pond's edge to drink at (the dawn walk happens only then).</summary>
            public bool HasPond => drinks.Count > 0;
            public IReadOnlyList<int> Range => range;

            /// <summary>Rebuild the range from the land: the dry cells off the hollow within <see cref="GrazeNear"/> of a grown tree and
            /// within <see cref="RangeRadius"/> of <paramref name="homeCell"/> (the whole wood's edge if none are that near), and the dry
            /// cells at the pond's edge nearest the home for the dawn drink. <paramref name="blocked"/>: cells the deer keep off (lots,
            /// the camp's green; null none). <paramref name="stagCount"/>: how many of the shown deer are stags (ids 0..n-1).</summary>
            public void Refresh(byte[] cover, EcoLand land, int seed, int homeCell, int stagCount = 1, bool[] blocked = null)
            {
                this.seed = seed; home = homeCell; stags = Math.Max(0, stagCount);
                range.Clear(); drinks.Clear();
                if (cover == null || homeCell < 0 || homeCell >= cover.Length) return;
                float hx = SiteMap.CXc(homeCell), hz = SiteMap.CZc(homeCell);
                var all = new List<int>();
                for (int c = 0; c < cover.Length; c++)
                {
                    var cv = (EcoCover)cover[c];
                    if (cv == EcoCover.Water || cv == EcoCover.Tree || (land != null && land.InBasin(c)) || (blocked != null && blocked[c])) continue;
                    if (!NearTree(cover, c)) continue;
                    all.Add(c);
                    if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), hx, hz) <= RangeRadius) range.Add(c);
                }
                if (range.Count == 0) range.AddRange(all);
                // the pond's edge: a dry cell beside water, the nearest few to the home
                var edge = new List<(float d, int c)>();
                var nb = new int[8];
                for (int c = 0; c < cover.Length; c++)
                {
                    var cv = (EcoCover)cover[c];
                    if (cv == EcoCover.Water || cv == EcoCover.Tree || (blocked != null && blocked[c])) continue;
                    int n = EcoLand.Neighbours8(c, nb);
                    bool wet = false;
                    for (int k = 0; k < n; k++) if ((EcoCover)cover[nb[k]] == EcoCover.Water) { wet = true; break; }
                    if (wet) edge.Add((Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), hx, hz), c));
                }
                edge.Sort((a, b) => { int k = a.d.CompareTo(b.d); return k != 0 ? k : a.c.CompareTo(b.c); });
                for (int k = 0; k < edge.Count && k < 6; k++) drinks.Add(edge[k].c);
            }

            static bool NearTree(byte[] cover, int c)
            {
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                int reach = (int)MathF.Ceiling(GrazeNear / SiteMap.Cell);
                int i0 = SiteMap.I(c), j0 = SiteMap.J(c);
                for (int j = Math.Max(0, j0 - reach); j <= Math.Min(SiteMap.NZ - 1, j0 + reach); j++)
                    for (int i = Math.Max(0, i0 - reach); i <= Math.Min(SiteMap.NX - 1, i0 + reach); i++)
                    {
                        int o = SiteMap.Index(i, j);
                        if ((EcoCover)cover[o] == EcoCover.Tree && Terrain.Dist(SiteMap.CXc(o), SiteMap.CZc(o), x, z) <= GrazeNear + 1e-4f) return true;
                    }
                return false;
            }

            /// <summary>Bring the shown deer to <paramref name="count"/> (cap already applied, see <see cref="Shown"/>): extras go from the
            /// back (never the stag, id 0, while any deer is shown), new ones get fresh ids. True when anything changed.</summary>
            public bool Sync(int count)
            {
                bool changed = false;
                while (alive.Count > count && alive.Count > 0) { alive.RemoveAt(alive.Count - 1); changed = true; }
                while (alive.Count < count) { alive.Add(nextId++); changed = true; }
                return changed;
            }

            /// <summary>The hunt took this deer: it is gone (the sim count is lowered by SimCore.DeerHuntCatch).</summary>
            public bool Remove(int id) => alive.Remove(id);

            public bool IsStag(int id) => id < stags;

            /// <summary>Where deer <paramref name="id"/> is at time <paramref name="t"/> (field seconds) and view hour <paramref name="hour"/>.
            /// A pure function of (seed, id, t, hour) and the range: grazes, looks up, walks to a new spot, in legs of 12-20 s; at dawn,
            /// with a pond, the does (and the stag behind them) stand at the water and drink. With no range: a pose at the home.</summary>
            public DeerPose Sample(int id, double t, float hour)
            {
                var p = new DeerPose { act = DeerAct.Graze, stag = IsStag(id) };
                if (home >= 0) { p.x = SiteMap.CXc(home); p.z = SiteMap.CZc(home); }
                if (range.Count == 0) return p;
                if (HasPond && hour >= DawnFrom && hour < DawnTo && (!p.stag || F((uint)seed, (uint)id, 31) < 0.5f))
                {
                    int dc = drinks[(int)(H((uint)seed, (uint)id, 32) % (uint)drinks.Count)];
                    p.x = SiteMap.CXc(dc); p.z = SiteMap.CZc(dc);
                    p.yaw = F((uint)seed, (uint)id, 33) * MathF.PI * 2f;
                    p.act = F((uint)seed, (uint)(id * 7 + (int)(t / 6.0)), 34) < 0.75f ? DeerAct.Drink : DeerAct.LookUp;
                    return p;
                }
                float legLen = LegBase + LegSpread * F((uint)seed, (uint)id, 21);
                double tt = t + 200.0 * F((uint)seed, (uint)id, 22);
                long leg = (long)Math.Floor(tt / legLen);
                float u = (float)(tt - leg * (double)legLen);
                Spot(id, leg, out float ax, out float az);
                Spot(id, leg + 1, out float bx, out float bz);
                uint r = H((uint)seed, (uint)id, (uint)(leg * 17 + 5));
                if (F(r, 1, 2) < 0.35f) { bx = ax; bz = az; } // stays and grazes on
                float dx = bx - ax, dz = bz - az, dist = MathF.Sqrt(dx * dx + dz * dz);
                float walkT = dist / WalkSpeed;
                if (walkT > legLen * 0.6f) walkT = legLen * 0.6f;
                float pre = (legLen - walkT) * (0.3f + 0.4f * F(r, 3, 4));
                p.yaw = dist > 0.01f ? MathF.Atan2(dx, dz) : F(r, 5, 6) * MathF.PI * 2f;
                if (u < pre) { p.x = ax; p.z = az; p.act = F(r, 7, 8) < 0.3f ? DeerAct.LookUp : DeerAct.Graze; }
                else if (u < pre + walkT && dist > 0.01f)
                {
                    float k = (u - pre) / walkT;
                    p.x = ax + dx * k; p.z = az + dz * k; p.act = DeerAct.Walk;
                }
                else { p.x = bx; p.z = bz; p.act = F(r, 9, 10) < 0.25f ? DeerAct.LookUp : DeerAct.Graze; }
                return p;
            }

            /// <summary>The spot of a leg: a hashed cell of the range, a little off its centre.</summary>
            void Spot(int id, long leg, out float x, out float z)
            {
                uint h = H((uint)seed, (uint)id, (uint)(leg * 31 + 3));
                int c = range[(int)(h % (uint)range.Count)];
                x = SiteMap.CXc(c) + (F(h, 1, 1) - 0.5f) * SiteMap.Cell;
                z = SiteMap.CZc(c) + (F(h, 2, 2) - 0.5f) * SiteMap.Cell;
            }

            /// <summary>Where a deer bolts to from (x, z) with a threat at (fx, fz): the range cell farthest from the threat that is at least
            /// <see cref="BoltRun"/> from where it stands (else the farthest), ties by cell. False with no range.</summary>
            public bool BoltSpot(float x, float z, float fx, float fz, out float bx, out float bz)
            {
                bx = x; bz = z;
                if (range.Count == 0) return false;
                int best = -1, any = -1; float bd = -1f, ad = -1f;
                foreach (int c in range)
                {
                    float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
                    float away = Terrain.Dist(cx, cz, fx, fz);
                    if (away > ad) { ad = away; any = c; }
                    if (Terrain.Dist(cx, cz, x, z) >= BoltRun && away > bd) { bd = away; best = c; }
                }
                int pick = best >= 0 ? best : any;
                bx = SiteMap.CXc(pick); bz = SiteMap.CZc(pick);
                return true;
            }
        }
    }
}
