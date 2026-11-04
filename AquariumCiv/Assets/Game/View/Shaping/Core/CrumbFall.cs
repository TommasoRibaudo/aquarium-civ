using System;
using AquariumCiv.Sim;

namespace AquariumCiv.View.Shaping
{
    /// <summary>Which glass wall the Sky Crumb bounces off (the side walls; the front pane is the player's own view).</summary>
    public enum CrumbWall { Left, Right, Back }

    /// <summary>
    /// The Sky Crumb's fall (#147), as data: when it drops after the shaping that made it, which wall it bounces off, where
    /// that leaves its smudge, and the timeline of the drop. Pure C#, no sim dice (a hash of the relic and the seed picks
    /// the wall and the height), so the view and the tests read the same plan and the sim never changes. Coordinates are the
    /// box's (x, z across the floor, y up from the ground), as <c>Relic.x/z</c>; uv are a pane's 0..1 (as GlassMask).
    /// </summary>
    public static class CrumbFall
    {
        /// <summary>Real seconds the crumb waits after the shaping landed: after a herd's whole run-in plus a beat, so the
        /// two never read as one event; after anything else a short beat (#138: it "fell with" the herd at its exact spot).</summary>
        public const float BeatAfterHerd = 3f, BeatAfterOther = 1.5f;
        /// <summary>The drop from the top of the box to the wall, and from the wall down to the spot (heavy: short and sharp).</summary>
        public const float FallSeconds = 0.55f, BounceSeconds = 0.7f;
        /// <summary>Where on the wall it hits, as a fraction of the box height, and the smudge it leaves (a pane's uv radius and grime strength).</summary>
        public const float HitMin = 0.35f, HitMax = 0.6f, SmudgeRadius = 0.08f, SmudgeStrength = 0.8f;
        /// <summary>How far from the wall the crumb starts, as a fraction of the half width (it drops towards the wall).</summary>
        public const float StartInset = 0.4f;
        /// <summary>The big lump is eaten over this many game months; only then the last piece shows (#138).</summary>
        public const int EatenMonths = 12;

        public struct Plan
        {
            public float delaySeconds;        // after the shaping landed (arrivalSeconds + the beat)
            public float startX, startZ, startY;
            public CrumbWall wall;
            public float hitX, hitY, hitZ;    // the contact point on the glass
            public float smudgeU, smudgeV;    // the same point on that wall's pane, 0..1
            public float smudgeRadius, smudgeStrength;
            public float landX, landZ;
            public float hitAt, landAt;       // seconds from the start of the drop
            public float TotalSeconds => landAt;
        }

        /// <summary>The wait before the drop starts, counted from when the shaping was seen: its arrival plus a beat.</summary>
        public static float Delay(string causeKind, float arrivalSeconds) =>
            arrivalSeconds + (causeKind == SimCore.NudgeKind.Herd ? BeatAfterHerd : BeatAfterOther);

        /// <summary>The drop onto a spot in a box of the given half sizes and height. Same inputs, same plan.</summary>
        public static Plan For(string causeKind, float arrivalSeconds, float spotX, float spotZ, float halfW, float halfD, float height, int seed)
        {
            uint h = Hash((uint)seed, unchecked((uint)(int)Math.Round(spotX * 100f)), unchecked((uint)(int)Math.Round(spotZ * 100f)));
            var wall = spotX < 0f ? CrumbWall.Left : CrumbWall.Right;
            if (Math.Abs(spotX) < halfW * 0.15f && (h & 3u) == 0u) wall = CrumbWall.Back; // dead centre: sometimes the back
            float hitFrac = HitMin + (HitMax - HitMin) * ((h >> 8 & 255u) / 255f);
            float wx = wall == CrumbWall.Left ? -halfW : wall == CrumbWall.Right ? halfW : spotX;
            float wz = wall == CrumbWall.Back ? halfD : spotZ; // the camera stands on the -z side: that pane is the player's own view
            float dirX = wall == CrumbWall.Left ? -1f : wall == CrumbWall.Right ? 1f : 0f;
            float dirZ = wall == CrumbWall.Back ? 1f : 0f;
            float inset = wall == CrumbWall.Back ? halfD : halfW;
            float distToWall = wall == CrumbWall.Back ? halfD - spotZ : Math.Abs(wx - spotX);
            // It starts up and part of the way to the wall, so the fall carries it onto the glass and it comes back to the spot.
            float run = Math.Min(distToWall * 0.6f, inset * StartInset);
            var p = new Plan
            {
                delaySeconds = Delay(causeKind, arrivalSeconds),
                startX = spotX + dirX * run, startZ = spotZ + dirZ * run, startY = height,
                wall = wall, hitY = hitFrac * height,
                landX = spotX, landZ = spotZ,
                smudgeRadius = SmudgeRadius, smudgeStrength = SmudgeStrength,
                hitAt = FallSeconds, landAt = FallSeconds + BounceSeconds,
            };
            if (wall == CrumbWall.Back) { p.hitX = spotX; p.hitZ = halfD; p.smudgeU = Clamp01((spotX + halfW) / (2f * halfW)); }
            else { p.hitX = wx; p.hitZ = spotZ; p.smudgeU = Clamp01((spotZ + halfD) / (2f * halfD)); }
            p.smudgeV = Clamp01(p.hitY / height);
            return p;
        }

        /// <summary>The height of the crumb at a time into the drop, so a view can place it without its own physics: a fast
        /// fall to the hit height, then a rise-and-fall arc back to the ground. Position along x/z is a straight lerp.</summary>
        public static void At(in Plan p, float t, out float x, out float y, out float z)
        {
            if (t <= 0f) { x = p.startX; y = p.startY; z = p.startZ; return; }
            if (t >= p.landAt) { x = p.landX; y = 0f; z = p.landZ; return; }
            if (t < p.hitAt)
            {
                float u = t / p.hitAt;
                x = p.startX + (p.hitX - p.startX) * u; z = p.startZ + (p.hitZ - p.startZ) * u;
                y = p.startY + (p.hitY - p.startY) * (u * u);   // accelerating
                return;
            }
            float b = (t - p.hitAt) / (p.landAt - p.hitAt);
            x = p.hitX + (p.landX - p.hitX) * b; z = p.hitZ + (p.landZ - p.hitZ) * b;
            y = p.hitY * (1f - b * b);              // falls from the hit height, speeding up
            if (y < 0f) y = 0f;
        }

        // ---------------- #210 #211: the crumbs knocked off on the glass ----------------

        /// <summary>A crumb bit: a bit of cookie dough, or (the few) a choc chip.</summary>
        public enum BitKind { Dough, Chip }

        /// <summary>One small square crumb knocked off the lump where it hit the glass (#210): it clings there a moment, drops
        /// off under gravity, lands, scatters with one small hop, flattens a little (its smear), then rests and is gone. All
        /// times are seconds after the hit; positions are box space with y up from the ground.</summary>
        public struct Bit
        {
            public BitKind kind;
            public float size;                     // edge length, box units
            public float startX, startY, startZ;   // on the glass, just inside it
            public float cling;                    // how long it sticks to the glass before it lets go
            public float driftX, driftZ;           // off the glass while it falls, units/s
            public float scatterX, scatterZ;       // its slide when it lands, units/s
            public float hop;                      // the hop's height after landing
            public float yaw, spin;                // degrees, and degrees/s while it tumbles
            public float landAt;                   // when it reaches the ground
        }

        /// <summary>How many bits break off, and how many of them are choc chips (the minority, #211).</summary>
        public const int BitCount = 14, ChipCount = 3;
        /// <summary>Box units/s²: a crumb falls the lower half of the box in about half a second.</summary>
        public const float Gravity = 6f;
        public const float BitMin = 0.06f, BitMax = 0.1f, ClingMax = 0.35f;
        /// <summary>How far round the hit the bits start, along the glass and up/down (box units).</summary>
        public const float SpreadSide = 0.3f, SpreadUp = 0.16f;
        /// <summary>The landing: how fast the slide dies away, how long the hop lasts, how flat it ends, then the rest and the fade.</summary>
        public const float ScatterTau = 0.2f, HopSeconds = 0.26f, SquashSeconds = 0.12f, Squashed = 0.6f, RestSeconds = 5f, FadeSeconds = 1.5f;

        /// <summary>The bits knocked off at the plan's hit point. Same plan and seed, same bits; the chips are spread through the list.</summary>
        public static Bit[] Bits(in Plan p, int seed)
        {
            var bits = new Bit[BitCount];
            float nx = p.wall == CrumbWall.Left ? 1f : p.wall == CrumbWall.Right ? -1f : 0f; // into the box, off the glass
            float nz = p.wall == CrumbWall.Back ? -1f : 0f;
            float tx = nz != 0f ? 1f : 0f, tz = nx != 0f ? 1f : 0f;                             // along the glass, sideways
            int every = BitCount / ChipCount;
            for (int i = 0; i < BitCount; i++)
            {
                uint h = Hash((uint)seed, 0x210u + (uint)i, (uint)p.wall + 1u);
                uint h2 = Hash(h, 0x211u, (uint)i);
                float R(int k) => ((h >> (k * 5)) & 31u) / 31f;
                float R2(int k) => ((h2 >> (k * 5)) & 31u) / 31f;
                bool chip = i % every == 2 && i / every < ChipCount;
                float size = (BitMin + (BitMax - BitMin) * R(0)) * (chip ? 0.8f : 1f);
                float side = (R(1) - 0.5f) * SpreadSide, up = (R(2) - 0.5f) * SpreadUp, inset = size * 0.5f + 0.004f;
                float fan = (R2(0) - 0.5f) * 1.6f;                    // the slide fans out along the glass
                float speed = 0.45f + 0.6f * R2(1);
                float ox = nx + tx * fan, oz = nz + tz * fan, len = (float)Math.Sqrt(ox * ox + oz * oz);
                float y0 = Math.Max(0.05f, p.hitY + up);
                float drift = 0.04f + 0.1f * R(4);
                var b = new Bit
                {
                    kind = chip ? BitKind.Chip : BitKind.Dough,
                    size = size,
                    startX = p.hitX + nx * inset + tx * side, startY = y0, startZ = p.hitZ + nz * inset + tz * side,
                    cling = ClingMax * R(3),
                    driftX = nx * drift, driftZ = nz * drift,
                    scatterX = ox / len * speed, scatterZ = oz / len * speed,
                    hop = size * (0.6f + 1.2f * R2(2)),
                    yaw = 360f * R2(3), spin = (R2(4) - 0.5f) * 900f,
                };
                b.landAt = b.cling + (float)Math.Sqrt(2f * y0 / Gravity);
                bits[i] = b;
            }
            return bits;
        }

        /// <summary>When every bit is gone (seconds after the hit).</summary>
        public static float BitsDone(Bit[] bits)
        {
            float last = 0f;
            foreach (var b in bits) last = Math.Max(last, b.landAt);
            return last + RestSeconds + FadeSeconds;
        }

        /// <summary>A bit at <paramref name="t"/> seconds after the hit, kept inside a box of the given half sizes: where it is
        /// (y above the ground), how flat (1 = a cube, <see cref="Squashed"/> once it lands), how big (1, then 0 as it fades),
        /// and its turn. It falls with gravity (y = y0 − g t²/2), so it speeds up; once down it slides out, slowing, with one hop.</summary>
        public static void BitAt(in Bit b, float t, float halfW, float halfD, out float x, out float y, out float z, out float flat, out float scale, out float yaw)
        {
            flat = 1f; scale = 1f; yaw = b.yaw;
            if (t <= b.cling) { x = b.startX; y = b.startY; z = b.startZ; return; }
            if (t < b.landAt)
            {
                float f = t - b.cling;
                x = b.startX + b.driftX * f; z = b.startZ + b.driftZ * f;
                y = Math.Max(0f, b.startY - 0.5f * Gravity * f * f);
                yaw = b.yaw + b.spin * f;
                Keep(b, halfW, halfD, ref x, ref z);
                return;
            }
            float fall = b.landAt - b.cling, s = t - b.landAt;
            float slide = ScatterTau * (1f - (float)Math.Exp(-s / ScatterTau));
            x = b.startX + b.driftX * fall + b.scatterX * slide;
            z = b.startZ + b.driftZ * fall + b.scatterZ * slide;
            float u = s / HopSeconds;
            y = u < 1f ? b.hop * 4f * u * (1f - u) : 0f;
            yaw = b.yaw + b.spin * fall + b.spin * 0.3f * slide;
            flat = 1f - (1f - Squashed) * Clamp01(s / SquashSeconds);
            scale = 1f - Clamp01((s - RestSeconds) / FadeSeconds);
            Keep(b, halfW, halfD, ref x, ref z);
        }

        static void Keep(in Bit b, float halfW, float halfD, ref float x, ref float z)
        {
            float m = b.size;
            x = Math.Max(-halfW + m, Math.Min(halfW - m, x));
            z = Math.Max(-halfD + m, Math.Min(halfD - m, z));
        }

        /// <summary>How much of the big lump is left: 1 when it fell, 0 when eaten.</summary>
        public static float Left(long foundMonth, long month) =>
            Clamp01(1f - (month - foundMonth) / (float)EatenMonths);

        /// <summary>The last piece by the fire shows only once the big lump is eaten, never from the start (#138).</summary>
        public static bool ShowLastPiece(bool found, long foundMonth, long month) => found && Left(foundMonth, month) <= 0f;

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        static uint Hash(uint a, uint b, uint c)
        {
            unchecked
            {
                uint h = a * 2654435761u ^ b * 2246822519u ^ c * 3266489917u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return h;
            }
        }
    }
}
