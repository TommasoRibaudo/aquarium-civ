using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.View.ShowMe
{
    public enum RelKind { Partner, Parent, Child, Sibling, Friend, Rival, Mentor, Other }

    /// <summary>A relationship marker wanted at a screen point (the related person's head).</summary>
    public struct RelMarker
    {
        public int id; public RelKind kind; public float x, y;
        public RelMarker(int id, RelKind kind, float x, float y) { this.id = id; this.kind = kind; this.x = x; this.y = y; }
    }

    /// <summary>A marker as drawn: where, whether to draw a leader line back to the person, and who it stands for.</summary>
    public sealed class PlacedMarker
    {
        public RelKind kind;
        /// <summary>The people it stands for (more than one = a merged label, "kids x3").</summary>
        public List<int> ids = new List<int>();
        public float anchorX, anchorY, x, y;
        public bool leader;
        public int Count => ids.Count;
    }

    public sealed class MarkerLayoutResult
    {
        public readonly List<PlacedMarker> placed = new List<PlacedMarker>();
        /// <summary>People whose marker didn't fit and isn't drawn: show "+N" and list them in the card.</summary>
        public int overflow;
        public readonly List<int> overflowIds = new List<int>();
    }

    /// <summary>
    /// Relationship markers that stay readable when the people stand close together (#194). The rule, in order:
    /// 1. Markers of the same mergeable kind (children, siblings, friends) whose anchors are within <c>mergeDist</c> collapse
    ///    into one label with a count at their centre ("kids x3").
    /// 2. At most <c>maxShown</c> labels survive, by priority: partner, parent, child, sibling, friend, mentor, rival, other
    ///    (a merged group counts as one and ranks by its kind). The rest are counted in <c>overflow</c> (the card lists them).
    /// 3. Survivors are placed in priority order above their person; one that would overlap a placed label or leave the
    ///    bounds moves to the nearest free slot on rings around it (up, sides, down), and gets a leader line when it moved
    ///    further than <c>leaderDist</c>. If nothing is free it takes the least overlapping spot.
    /// Pure and deterministic (ties by id); units are whatever the view uses (screen points).
    /// </summary>
    public static class MarkerLayout
    {
        public static readonly HashSet<RelKind> Mergeable = new HashSet<RelKind> { RelKind.Child, RelKind.Sibling, RelKind.Friend };

        public static int Priority(RelKind k)
        {
            switch (k)
            {
                case RelKind.Partner: return 0;
                case RelKind.Parent: return 1;
                case RelKind.Child: return 2;
                case RelKind.Sibling: return 3;
                case RelKind.Friend: return 4;
                case RelKind.Mentor: return 5;
                case RelKind.Rival: return 6;
                default: return 7;
            }
        }

        /// <param name="w">Label width, <paramref name="h"/> height (the same for all).</param>
        /// <param name="mergeDist">0 = 1.5 label widths. <paramref name="leaderDist"/>: 0 = 1.2 label heights.</param>
        public static MarkerLayoutResult Layout(IEnumerable<RelMarker> markers, float w, float h, float boundsW, float boundsH,
            int maxShown = 4, float mergeDist = 0f, float leaderDist = 0f)
        {
            if (mergeDist <= 0f) mergeDist = w * 1.5f;
            if (leaderDist <= 0f) leaderDist = h * 1.2f;
            var res = new MarkerLayoutResult();
            var groups = new List<PlacedMarker>();
            foreach (var m in markers.OrderBy(m => Priority(m.kind)).ThenBy(m => m.id))
            {
                PlacedMarker into = null;
                if (Mergeable.Contains(m.kind))
                    into = groups.FirstOrDefault(g => g.kind == m.kind && Dist(g.anchorX, g.anchorY, m.x, m.y) <= mergeDist);
                if (into != null)
                {
                    float c = into.ids.Count;
                    into.anchorX = (into.anchorX * c + m.x) / (c + 1); into.anchorY = (into.anchorY * c + m.y) / (c + 1);
                    into.ids.Add(m.id);
                }
                else
                {
                    var p = new PlacedMarker { kind = m.kind, anchorX = m.x, anchorY = m.y };
                    p.ids.Add(m.id); groups.Add(p);
                }
            }
            var order = groups.OrderBy(g => Priority(g.kind)).ThenByDescending(g => g.Count).ThenBy(g => g.ids.Min()).ToList();
            foreach (var g in order.Skip(maxShown)) { res.overflow += g.Count; res.overflowIds.AddRange(g.ids); }
            foreach (var g in order.Take(maxShown))
            {
                float bx = g.anchorX, by = g.anchorY - h; // above the head
                var spot = Free(res.placed, bx, by, w, h, boundsW, boundsH);
                g.x = spot.x; g.y = spot.y;
                g.leader = Dist(g.x, g.y, bx, by) > leaderDist;
                res.placed.Add(g);
            }
            return res;
        }

        /// <summary>True if two placed labels overlap.</summary>
        public static bool Overlaps(PlacedMarker a, PlacedMarker b, float w, float h) =>
            Math.Abs(a.x - b.x) < w && Math.Abs(a.y - b.y) < h;

        static (float x, float y) Free(List<PlacedMarker> placed, float cx, float cy, float w, float h, float bw, float bh)
        {
            (float x, float y) best = (Clamp(cx, w / 2, bw - w / 2), Clamp(cy, h / 2, bh - h / 2));
            float bestScore = float.MaxValue;
            var dirs = new (float x, float y)[] { (0, -1), (1, -1), (-1, -1), (1, 0), (-1, 0), (1, 1), (-1, 1), (0, 1) };
            for (int ring = 0; ring <= 5; ring++)
            {
                for (int di = 0; di < (ring == 0 ? 1 : dirs.Length); di++)
                {
                    float x = cx + (ring == 0 ? 0 : dirs[di].x * ring * w * 0.55f), y = cy + (ring == 0 ? 0 : dirs[di].y * ring * h * 1.05f);
                    if (x - w / 2 < 0 || x + w / 2 > bw || y - h / 2 < 0 || y + h / 2 > bh) continue;
                    float over = 0f;
                    foreach (var p in placed) over += Math.Max(0f, w - Math.Abs(p.x - x)) * Math.Max(0f, h - Math.Abs(p.y - y));
                    if (over <= 0f) return (x, y);
                    if (over < bestScore) { bestScore = over; best = (x, y); }
                }
            }
            return best;
        }

        static float Dist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
