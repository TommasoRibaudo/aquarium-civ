// Visit loop (play-lots fix): a play group's targets never stand inside a building's lot. Pure (System only): Tools/SimTests compile it.
using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// Moves a point off the lots. A game's geometry (the flee area in tag, the ball's circle, the chase toward the herd, hide spots,
    /// the woods ring) is drawn round the group's centre without knowing where the houses are; every member target goes through
    /// <see cref="Nudge"/> as the last step, so a runner that would enter a lot slides along its edge instead.
    /// </summary>
    public static class OpenGround
    {
        /// <summary>The ring step and the farthest search distance (box units), and the angles tried per ring.</summary>
        public const float Step = 0.04f, MaxReach = 2.4f;
        const int Angles = 24;

        /// <summary>
        /// <paramref name="p"/> itself when it is on open ground; else the nearest open point (rings of growing radius round it, the
        /// angle nearest <paramref name="prefer"/> first so the result is steady from frame to frame); else <paramref name="p"/>.
        /// <paramref name="blocked"/> says whether a point is inside a lot (or otherwise not allowed).
        /// </summary>
        public static (float x, float z) Nudge(float x, float z, Func<float, float, bool> blocked, float preferX = 0f, float preferZ = 0f)
        {
            if (blocked == null || !blocked(x, z)) return (x, z);
            float pref = (preferX == 0f && preferZ == 0f) ? 0f : (float)Math.Atan2(preferZ, preferX);
            for (float r = Step; r <= MaxReach; r += Step)
                for (int k = 0; k < Angles; k++)
                {
                    // 0, +1, -1, +2, -2 ... steps away from the preferred angle
                    int j = (k + 1) / 2 * ((k & 1) == 1 ? 1 : -1);
                    float a = pref + j * (float)Math.PI * 2f / Angles;
                    float qx = x + (float)Math.Cos(a) * r, qz = z + (float)Math.Sin(a) * r;
                    if (!blocked(qx, qz)) return (qx, qz);
                }
            return (x, z);
        }

        /// <summary>#770: the gap between sample points round a dance ring (box units; a map cell is 0.1 wide, so no footprint slips between two),
        /// and the margin kept past the ring's edge.</summary>
        public const float RingGap = 0.035f, RingMargin = 0.05f;

        /// <summary>How many sample points of a ring of radius <paramref name="r"/> round (cx, cz) are blocked: the middle, then rings at 0.5, 0.6,
        /// 0.8, 0.9 and 1.0 r and at r plus <see cref="RingMargin"/>, so a ring that turns and steps in (to 0.6 r) never meets a lot.</summary>
        public static int RingHits(float cx, float cz, float r, Func<float, float, bool> blocked)
        {
            if (blocked == null) return 0;
            int n = blocked(cx, cz) ? 1 : 0;
            float[] radii = { 0.5f * r, 0.6f * r, 0.8f * r, 0.9f * r, r, r + RingMargin };
            for (int ri = 0; ri < radii.Length; ri++)
            {
                int k = Math.Max(16, (int)Math.Ceiling(Math.PI * 2.0 * radii[ri] / RingGap));
                for (int j = 0; j < k; j++)
                {
                    float a = j * (float)Math.PI * 2f / k;
                    if (blocked(cx + (float)Math.Cos(a) * radii[ri], cz + (float)Math.Sin(a) * radii[ri])) n++;
                }
            }
            return n;
        }

        /// <summary>
        /// #770: the ring that fits: <see cref="RingCentre"/> for <paramref name="r"/>; when none is clear within <paramref name="reach"/>, the ring is
        /// drawn a step smaller (down to <paramref name="minR"/>) and searched again. Returns the centre and the radius used; the least blocked
        /// centre at <paramref name="minR"/> when even that finds no clear ground.
        /// </summary>
        public static (float x, float z, float r) RingFit(float cx, float cz, float r, float minR, Func<float, float, bool> blocked, Func<float, float, bool> ok = null,
            float reach = 1.6f, float preferX = 0f, float preferZ = 0f)
        {
            minR = Math.Min(minR, r);
            for (float rr = r; ; rr = Math.Max(minR, rr * 0.9f))
            {
                var (x, z) = RingCentre(cx, cz, rr, blocked, ok, reach, preferX, preferZ);
                if (RingHits(x, z, rr, blocked) == 0 || rr <= minR + 1e-4f) return (x, z, rr);
            }
        }

        /// <summary>
        /// #770: the centre nearest (cx, cz) whose whole dance ring (<paramref name="r"/>) keeps off the blocked ground (a building's footprint),
        /// found on rings of growing distance round it, the angle nearest <paramref name="prefer"/> first. <paramref name="ok"/> says whether a
        /// centre may be used at all (inside the box, dry). (cx, cz) itself when it is already clear; when nothing within
        /// <paramref name="reach"/> is clear, the least blocked centre tried (so a crowded box still gets the best it has).
        /// </summary>
        public static (float x, float z) RingCentre(float cx, float cz, float r, Func<float, float, bool> blocked, Func<float, float, bool> ok = null,
            float reach = 1.6f, float preferX = 0f, float preferZ = 0f)
        {
            int hits0 = RingHits(cx, cz, r, blocked);
            if (hits0 == 0) return (cx, cz);
            float pref = (preferX == 0f && preferZ == 0f) ? 0f : (float)Math.Atan2(preferZ, preferX);
            float bx = cx, bz = cz; int best = hits0;
            for (float d = 0.1f; d <= reach; d += 0.1f)
                for (int k = 0; k < Angles; k++)
                {
                    int j = (k + 1) / 2 * ((k & 1) == 1 ? 1 : -1);
                    float a = pref + j * (float)Math.PI * 2f / Angles;
                    float qx = cx + (float)Math.Cos(a) * d, qz = cz + (float)Math.Sin(a) * d;
                    if (ok != null && !ok(qx, qz)) continue;
                    int h = RingHits(qx, qz, r, blocked);
                    if (h == 0) return (qx, qz);
                    if (h < best) { best = h; bx = qx; bz = qz; }
                }
            return (bx, bz);
        }
    }
}
