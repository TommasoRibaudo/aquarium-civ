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
    }
}
