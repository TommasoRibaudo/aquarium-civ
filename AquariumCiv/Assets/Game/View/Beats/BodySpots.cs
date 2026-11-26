// #779: where people stand in a story scene. Every standing spot of a scene is its own: a body width from the others of the scene, from the
// people who are not in it, and clear of the water and the props (the room). A spot that is taken is moved to the nearest free one on the
// side the scene prefers (a ring round the wanted spot, nearest first), so the watchers form a row or a ring, never a heap.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>#779: what a scene's spots must keep clear of beside its own people: the people who are not in it, and the ground nobody stands on (the
    /// pond, a tent, the glass).</summary>
    public sealed class StoryRoom
    {
        /// <summary>Where the others in the box stand now (people the scene does not cast).</summary>
        public readonly List<Vector2> others = new List<Vector2>();
        /// <summary>True where nobody may stand (water, a prop, outside the box); null = anywhere.</summary>
        public Func<Vector2, bool> blocked;
        public bool Blocked(Vector2 p) => blocked != null && blocked(p);
    }

    public static class BodySpots
    {
        /// <summary>A body's width, shoulder to shoulder (PegDoll.ShoulderX is 0.046 each side): nobody stands closer than this to another.</summary>
        public const float BodyW = 0.09f;
        /// <summary>How far the search for a free spot goes from the wanted one, and its steps.</summary>
        public const float SearchReach = 0.6f, SearchStep = 0.03f;

        /// <summary>Is <paramref name="p"/> a body width from every spot in <paramref name="taken"/> and every other in <paramref name="room"/>, and on open ground?</summary>
        public static bool Clear(Vector2 p, IList<Vector2> taken, StoryRoom room = null, float gap = BodyW)
        {
            if (room != null && room.Blocked(p)) return false;
            if (taken != null) for (int i = 0; i < taken.Count; i++) if ((taken[i] - p).magnitude < gap - 1e-4f) return false;
            if (room != null) for (int i = 0; i < room.others.Count; i++) if ((room.others[i] - p).magnitude < gap - 1e-4f) return false;
            return true;
        }

        /// <summary>
        /// The spot nearest <paramref name="want"/> that is <see cref="Clear"/>: <paramref name="want"/> itself when it is, else the nearest free spot on
        /// a ring round it, trying the side <paramref name="prefer"/> (a direction) first and then the sides either way of it. The spot is added to
        /// <paramref name="taken"/> (so the next one keeps clear of it). If nothing is free within <see cref="SearchReach"/> it is <paramref name="want"/>.
        /// </summary>
        public static Vector2 Free(Vector2 want, Vector2 prefer, IList<Vector2> taken, StoryRoom room = null, float gap = BodyW)
        {
            var at = want;
            if (!Clear(want, taken, room, gap))
            {
                var dir = prefer.sqrMagnitude > 1e-6f ? prefer.normalized : Vector2.right;
                bool found = false;
                for (float r = SearchStep; r <= SearchReach && !found; r += SearchStep)
                {
                    int n = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / (gap * 0.5f)));
                    for (int k = 0; k < n && !found; k++)
                    {
                        // 0, +1, -1, +2, -2 ... steps off the preferred side, so the nearest free spot on that side wins
                        int off = (k + 1) / 2 * ((k & 1) == 1 ? 1 : -1);
                        float a = off * 2f * Mathf.PI / n;
                        var q = want + Rot(dir, a) * r;
                        if (Clear(q, taken, room, gap)) { at = q; found = true; }
                    }
                }
            }
            taken?.Add(at);
            return at;
        }

        static Vector2 Rot(Vector2 v, float rad) { float c = Mathf.Cos(rad), s = Mathf.Sin(rad); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }

        /// <summary>The least distance between two spots of <paramref name="spots"/> (a large number for fewer than two).</summary>
        public static float Closest(IList<Vector2> spots, out int a, out int b)
        {
            a = b = -1; float best = float.MaxValue;
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                {
                    float d = (spots[i] - spots[j]).magnitude;
                    if (d < best) { best = d; a = i; b = j; }
                }
            return best;
        }
    }
}
