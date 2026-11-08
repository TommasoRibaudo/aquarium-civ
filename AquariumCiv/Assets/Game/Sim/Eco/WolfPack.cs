using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>What a wolf is doing at a moment (the view picks the doll's pose from this, P0 / #439-#441). <see cref="Hidden"/>: no
    /// spot was safe (a person stands at the den, say): the wolf is in the trees, not drawn.</summary>
    public enum WolfAct : byte { Lie, Play, Trot, Howl, Watch, Hidden }

    /// <summary>One wolf at one moment: where, which way it faces, what it does; the pups, and the grey wolf (B22.c, the pale silver
    /// face). <see cref="visible"/> is false for <see cref="WolfAct.Hidden"/>.</summary>
    public struct WolfPose
    {
        public float x, z, yaw;
        public WolfAct act;
        public bool pup, grey, visible;
    }

    /// <summary>
    /// #437 the hard rules, as the planner's check (docs/design/placed-life.md §5.6, animation-specs "Placed life: hard in code" 5):
    /// a wolf never touches a person, and wolves are never among the tents. Every wolf point the sim or a view plans goes through
    /// <see cref="Allowed"/> (or <see cref="Keep"/>, which moves a point back toward a safe anchor, else hides the wolf):
    /// <list type="bullet">
    /// <item>the <b>tent ring</b>: every building but the fire, its half-diagonal plus <see cref="TentPad"/>, is closed to wolves;</item>
    /// <item>a wolf keeps at least <see cref="PersonGap"/> from every person (the caller's positions: the planner's, or the view's);</item>
    /// <item>boldness sets how near the fire a wolf may come (<see cref="MinFromFire"/>), never nearer than <see cref="FirelightEdge"/>;</item>
    /// <item>and a wolf stays on the land, inside the glass.</item>
    /// </list>
    /// Pure: no Unity, no dice.
    /// </summary>
    public static class WolfRules
    {
        /// <summary>The ring round each tent, the gap to any person, the firelight's edge (the boldest wolf's limit), the shy pack's.</summary>
        public const float TentPad = 0.25f, PersonGap = 0.3f, FirelightEdge = 1.0f, ShyApproach = 2.0f;
        /// <summary>A building with no size yet still closes this much round its centre (before the pad).</summary>
        public const float MinTentR = 0.15f;
        /// <summary>A wolf keeps this far inside the glass.</summary>
        public const float GlassMargin = 0.1f;

        /// <summary>How near the fire a wolf may come at this boldness: from <see cref="ShyApproach"/> (0) to <see cref="FirelightEdge"/> (1).</summary>
        public static float MinFromFire(float boldness) => ShyApproach + (FirelightEdge - ShyApproach) * Math.Clamp(boldness, 0f, 1f);

        /// <summary>The tent ring as (x, z, r) triples: every placed building but the fire (the camp's own).</summary>
        public static void TentRing(IEnumerable<Building> buildings, List<float> into)
        {
            into.Clear();
            if (buildings == null) return;
            foreach (var b in buildings)
            {
                if (b == null || b.lotId == 0 || b.type == BuildingType.Camp) continue;
                float r = MathF.Max(MinTentR, 0.5f * MathF.Sqrt(b.w * b.w + b.d * b.d)) + TentPad;
                into.Add(b.x); into.Add(b.z); into.Add(r);
            }
        }

        public static bool InTentRing(float x, float z, IReadOnlyList<float> ring)
        {
            if (ring == null) return false;
            for (int k = 0; k + 2 < ring.Count; k += 3)
                if (Terrain.Dist(x, z, ring[k], ring[k + 1]) < ring[k + 2]) return true;
            return false;
        }

        /// <summary>A person (x, z pairs) is nearer than <paramref name="gap"/>.</summary>
        public static bool NearPerson(float x, float z, IReadOnlyList<float> people, float gap = PersonGap)
        {
            if (people == null) return false;
            for (int k = 0; k + 1 < people.Count; k += 2)
                if (Terrain.Dist(x, z, people[k], people[k + 1]) < gap) return true;
            return false;
        }

        public static bool OnLand(float x, float z) => MathF.Abs(x) <= Terrain.HX - GlassMargin && MathF.Abs(z) <= Terrain.HZ - GlassMargin;

        /// <summary>The wolf may stand here: on the land, outside the tent ring, at least <paramref name="minFire"/> (never less than the
        /// firelight's edge) from the fire, and at least <see cref="PersonGap"/> from every person.</summary>
        public static bool Allowed(float x, float z, WolfGround g, IReadOnlyList<float> people, float minFire)
        {
            if (!OnLand(x, z)) return false;
            if (Terrain.Dist(x, z, g.fireX, g.fireZ) < MathF.Max(FirelightEdge, minFire) - 1e-4f) return false;
            return !InTentRing(x, z, g.ring) && !NearPerson(x, z, people);
        }

        /// <summary>Makes a planned point safe: it stays if <see cref="Allowed"/>; else it walks back toward the anchor (the den, the
        /// wood) and takes the first safe point; else the first safe point on rings round the anchor. False (the wolf is in the trees,
        /// not drawn) when none is safe. Deterministic.</summary>
        public static bool Keep(ref float x, ref float z, float anchorX, float anchorZ, WolfGround g, IReadOnlyList<float> people, float minFire)
        {
            if (Allowed(x, z, g, people, minFire)) return true;
            const int steps = 12;
            for (int k = 1; k <= steps; k++)
            {
                float f = k / (float)steps, px = x + (anchorX - x) * f, pz = z + (anchorZ - z) * f;
                if (Allowed(px, pz, g, people, minFire)) { x = px; z = pz; return true; }
            }
            float a0 = MathF.Atan2(anchorZ - g.fireZ, anchorX - g.fireX); // the far side first
            foreach (float r in Rings)
                for (int k = 0; k < 16; k++)
                {
                    float a = a0 + (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * (MathF.PI / 8f);
                    float px = anchorX + MathF.Cos(a) * r, pz = anchorZ + MathF.Sin(a) * r;
                    if (Allowed(px, pz, g, people, minFire)) { x = px; z = pz; return true; }
                }
            return false;
        }

        static readonly float[] Rings = { 0.35f, 0.5f, 0.7f, 1.0f, 1.4f };

        /// <summary>The first point on the ray from the fire toward (tx, tz), at least <paramref name="fromFire"/> out, that is on the land and
        /// outside the tent ring (people aside: a spot, not a moment). False when the ray leaves the land first.</summary>
        public static bool EdgeSpot(WolfGround g, float tx, float tz, float fromFire, out float x, out float z)
        {
            float dx = tx - g.fireX, dz = tz - g.fireZ, len = MathF.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f) { dx = 1f; dz = 0f; len = 1f; }
            dx /= len; dz /= len;
            for (float r = MathF.Max(fromFire, FirelightEdge); r <= 4f; r += 0.05f)
            {
                x = g.fireX + dx * r; z = g.fireZ + dz * r;
                if (!OnLand(x, z)) break;
                if (!InTentRing(x, z, g.ring)) return true;
            }
            x = g.fireX + dx * FirelightEdge; z = g.fireZ + dz * FirelightEdge;
            return false;
        }
    }

    /// <summary>#437: the ground the wolf rules read: the fire and the tent ring (x, z, r triples, <see cref="WolfRules.TentRing"/>).</summary>
    public sealed class WolfGround
    {
        public float fireX, fireZ;
        public readonly List<float> ring = new List<float>();

        public WolfGround() { }
        public WolfGround(float fireX, float fireZ, IEnumerable<Building> buildings = null)
        {
            this.fireX = fireX; this.fireZ = fireZ;
            WolfRules.TentRing(buildings, ring);
        }
    }

    /// <summary>
    /// #437: the pure model of the wolves you see (placed-life spec §5.6; B20-B23), as <see cref="DeerHerd"/>: no Unity, no dice but hashes
    /// of (seed, wolf, leg). The pack lives at its den in the far wood: by day the adults lie at the den or trot between spots of the
    /// wood's edge round it, the pups play at the den mouth; at dusk they sit and howl (<see cref="HowlFrom"/>..<see cref="HowlTo"/>); at
    /// night, on a night the eyes show (<see cref="EyesTonight"/>), one or two come to the edge of the firelight and watch. Every pose goes
    /// through <see cref="WolfRules.Keep"/> with the caller's people, so no pose is ever inside the tent ring or near a person. Wolf id 0 is
    /// the grey wolf; the last <c>pups</c> ids are this spring's pups.
    /// </summary>
    public static class WolfPack
    {
        public const float HowlFrom = 18f, HowlTo = 20f, NightFrom = 20.5f, NightTo = 5f;
        public const float LegBase = 14f, LegSpread = 8f, TrotSpeed = 0.12f, RangeRadius = 0.9f;
        public const int GreyWolf = 0;

        static uint H(uint a, uint b, uint c) => RabbitHerd.H(a, b, c);
        static float F(uint a, uint b, uint c) => RabbitHerd.F(a, b, c);

        /// <summary>Do the eyes show at the firelight tonight (view day <paramref name="day"/>), at this chance (SimCore.WolfEyesChance)?</summary>
        public static bool EyesTonight(int seed, long day, float chance) => chance > 0f && F((uint)seed, (uint)(day & 0x7fffffff), 77) < chance;

        /// <summary>The spot of eyes pair <paramref name="k"/> on a night: toward the den, turned by a hash, at the boldness's distance from
        /// the fire, outside the tent ring (people aside; <see cref="WolfRules.Keep"/> them at the frame).</summary>
        public static void EyesSpot(WolfGround g, int seed, long day, int k, float denX, float denZ, float boldness, out float x, out float z)
        {
            float a = MathF.Atan2(denZ - g.fireZ, denX - g.fireX) + (F((uint)seed, (uint)(day * 4 + k), 78) - 0.5f) * 1.6f;
            WolfRules.EdgeSpot(g, g.fireX + MathF.Cos(a), g.fireZ + MathF.Sin(a), WolfRules.MinFromFire(boldness) + 0.05f, out x, out z);
        }

        /// <summary>The pack as the view draws it. The view calls <see cref="Refresh"/> each season (and when the pack, the den, boldness
        /// or the buildings change), then <see cref="Sample"/> per wolf per frame with the people's positions.</summary>
        public sealed class Field
        {
            readonly List<int> range = new List<int>();
            int seed, den = -1, count, pups;
            float denX, denZ, bold, eyes;
            WolfGround ground = new WolfGround();

            public int Seed => seed;
            public int Den => den;
            public int Count => count;
            public int Pups => pups;
            public float Boldness => bold;
            public WolfGround Ground => ground;
            public IReadOnlyList<int> Range => range;

            /// <summary>Rebuild from the land: the den, the pack's count and pups, boldness, the chance the eyes show at night, and the
            /// ground (the fire and the tent ring). The range is the dry cells beside a grown tree within <see cref="RangeRadius"/> of the den
            /// that keep the boldness's distance from the fire.</summary>
            public void Refresh(byte[] cover, int seed, int denCell, int pack, int pupCount, float boldness, float eyesChance, WolfGround g)
            {
                this.seed = seed; den = denCell; count = Math.Max(0, pack); pups = Math.Clamp(pupCount, 0, count); bold = boldness; eyes = eyesChance;
                ground = g ?? new WolfGround();
                range.Clear();
                if (cover == null || denCell < 0 || denCell >= cover.Length) { count = 0; return; }
                denX = SiteMap.CXc(denCell); denZ = SiteMap.CZc(denCell);
                float minFire = WolfRules.MinFromFire(bold) + 0.3f;
                var nb = new int[8];
                for (int c = 0; c < cover.Length; c++)
                {
                    var cv = (EcoCover)cover[c];
                    if (cv == EcoCover.Water || cv == EcoCover.Tree) continue;
                    float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                    if (Terrain.Dist(x, z, denX, denZ) > RangeRadius || Terrain.Dist(x, z, ground.fireX, ground.fireZ) < minFire) continue;
                    if (WolfRules.InTentRing(x, z, ground.ring) || !WolfRules.OnLand(x, z)) continue;
                    int n = EcoLand.Neighbours8(c, nb);
                    for (int k = 0; k < n; k++) if ((EcoCover)cover[nb[k]] == EcoCover.Tree) { range.Add(c); break; }
                }
            }

            public bool IsPup(int id) => id >= count - pups;

            /// <summary>Where wolf <paramref name="id"/> is at time <paramref name="t"/> (field seconds), view day <paramref name="day"/> and
            /// hour <paramref name="hour"/>, kept off the tent ring and <see cref="WolfRules.PersonGap"/> from every person in
            /// <paramref name="people"/> (x, z pairs; null none). A pure function of those and the field.</summary>
            public WolfPose Sample(int id, double t, long day, float hour, IReadOnlyList<float> people)
            {
                var p = new WolfPose { act = WolfAct.Lie, pup = IsPup(id), grey = id == GreyWolf, x = denX, z = denZ, visible = true };
                if (den < 0 || id < 0 || id >= count) { p.visible = false; p.act = WolfAct.Hidden; return p; }
                float minFire = WolfRules.MinFromFire(bold);
                float ox = (F((uint)seed, (uint)id, 61) - 0.5f) * 0.16f, oz = (F((uint)seed, (uint)id, 62) - 0.5f) * 0.16f;
                bool night = hour >= NightFrom || hour < NightTo;
                if (p.pup)
                {
                    float w = (float)(t * 0.7 + id);
                    p.x = denX + ox * 0.6f + MathF.Sin(w) * 0.04f; p.z = denZ + oz * 0.6f + MathF.Cos(w * 1.3f) * 0.03f;
                    p.act = night ? WolfAct.Lie : WolfAct.Play;
                    p.yaw = w;
                }
                else if (hour >= HowlFrom && hour < HowlTo)
                {
                    p.x = denX + ox; p.z = denZ + oz; p.act = WolfAct.Howl;
                    p.yaw = MathF.Atan2(ground.fireX - denX, ground.fireZ - denZ);
                }
                else if (night)
                {
                    long nightDay = hour < NightTo ? day - 1 : day;
                    if (id < 2 && EyesTonight(seed, nightDay, eyes))
                    {
                        EyesSpot(ground, seed, nightDay, id, denX, denZ, bold, out p.x, out p.z);
                        p.act = WolfAct.Watch;
                        p.yaw = MathF.Atan2(ground.fireX - p.x, ground.fireZ - p.z);
                    }
                    else { p.x = denX + ox; p.z = denZ + oz; p.act = WolfAct.Lie; }
                }
                else Day(id, t, ref p);
                if (!WolfRules.Keep(ref p.x, ref p.z, denX, denZ, ground, people, minFire)) { p.visible = false; p.act = WolfAct.Hidden; }
                return p;
            }

            void Day(int id, double t, ref WolfPose p)
            {
                if (range.Count == 0) { p.act = WolfAct.Lie; return; }
                float legLen = LegBase + LegSpread * F((uint)seed, (uint)id, 63);
                double tt = t + 300.0 * F((uint)seed, (uint)id, 64);
                long leg = (long)Math.Floor(tt / legLen);
                float u = (float)(tt - leg * (double)legLen);
                Spot(id, leg, out float ax, out float az);
                Spot(id, leg + 1, out float bx, out float bz);
                uint r = H((uint)seed, (uint)id, (uint)(leg * 13 + 7));
                if (F(r, 1, 2) < 0.45f) { bx = ax; bz = az; } // lies on there
                float dx = bx - ax, dz = bz - az, dist = MathF.Sqrt(dx * dx + dz * dz);
                float walkT = MathF.Min(dist / TrotSpeed, legLen * 0.5f);
                float pre = (legLen - walkT) * 0.5f;
                p.yaw = dist > 0.01f ? MathF.Atan2(dx, dz) : F(r, 3, 4) * MathF.PI * 2f;
                if (u < pre) { p.x = ax; p.z = az; p.act = WolfAct.Lie; }
                else if (u < pre + walkT && dist > 0.01f) { float k = (u - pre) / walkT; p.x = ax + dx * k; p.z = az + dz * k; p.act = WolfAct.Trot; }
                else { p.x = bx; p.z = bz; p.act = WolfAct.Lie; }
            }

            /// <summary>A leg's spot: the den on some legs, else a hashed cell of the range.</summary>
            void Spot(int id, long leg, out float x, out float z)
            {
                uint h = H((uint)seed, (uint)id, (uint)(leg * 29 + 11));
                if (F(h, 5, 5) < 0.3f) { x = denX; z = denZ; return; }
                int c = range[(int)(h % (uint)range.Count)];
                x = SiteMap.CXc(c) + (F(h, 1, 1) - 0.5f) * SiteMap.Cell;
                z = SiteMap.CZc(c) + (F(h, 2, 2) - 0.5f) * SiteMap.Cell;
            }
        }
    }
}
