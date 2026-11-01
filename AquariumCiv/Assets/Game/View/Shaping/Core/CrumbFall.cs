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
