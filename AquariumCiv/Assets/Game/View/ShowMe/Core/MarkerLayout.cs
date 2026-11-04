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

    /// <summary>A label's box: its centre and size.</summary>
    public struct LabelBox
    {
        public float x, y, w, h;
        public LabelBox(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    /// <summary>A leader line: from a label's edge (ax, ay) to the person (bx, by), by way of (vx, vy) when it has to bend, and then (wx, wy) if twice.</summary>
    public struct LeaderRoute
    {
        public float ax, ay, vx, vy, wx, wy, bx, by;
        public bool bends, twice;
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

        /// <summary>Tester run 9: from its nearest edge, how far a label (x, y its middle) is from the person it names. Past <see cref="LeaderGap"/> it needs a line back.</summary>
        public static float GapTo(LabelBox me, float toX, float toY)
        {
            float dx = Math.Max(0f, Math.Abs(toX - me.x) - me.w / 2f), dy = Math.Max(0f, Math.Abs(toY - me.y) - me.h / 2f);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The widest gap (panel units) a label keeps from its person with no line: about one label's height and a half.</summary>
        public const float LeaderGap = 100f;

        /// <summary>Tester run 8: a leader that ran behind another label read as ending there. The way from a label to its person that stays out from under every
        /// other label: straight from whichever edge can, else round the corner of what's in the way, else (the person is under a label) straight.
        /// #209: labels stacked close together (FarEnd puts them 8 apart, its own label among them) are one block to go round, so no leg runs
        /// through the gaps of a stack and none runs back across its own label: every leg keeps <paramref name="air"/> off every label.</summary>
        public static LeaderRoute RouteLeader(LabelBox me, float toX, float toY, IList<LabelBox> others, float air = 6f)
        {
            float halfW = me.w / 2f, halfH = me.h / 2f;
            var starts = new[] { (me.x, me.y + halfH), (me.x, me.y - halfH), (me.x + halfW, me.y), (me.x - halfW, me.y) };
            System.Array.Sort(starts, (a, b) => Dist(a.Item1, a.Item2, toX, toY).CompareTo(Dist(b.Item1, b.Item2, toX, toY)));
            var inner = new LabelBox(me.x, me.y, Math.Max(0f, me.w - 2f), Math.Max(0f, me.h - 2f)); // its own label: a leg may leave its edge, never cross it
            bool First(float ax, float ay, float bx, float by) => Clear(ax, ay, bx, by, others, air) && !Crosses(ax, ay, bx, by, inner);
            bool Later(float ax, float ay, float bx, float by) => Clear(ax, ay, bx, by, others, air) && !Crosses(ax, ay, bx, by, me, air);
            foreach (var s in starts)
                if (First(s.Item1, s.Item2, toX, toY)) return new LeaderRoute { ax = s.Item1, ay = s.Item2, bx = toX, by = toY };
            var best = new LeaderRoute { ax = starts[0].Item1, ay = starts[0].Item2, bx = toX, by = toY };
            float bestLen = float.MaxValue;
            var blocks = new List<LabelBox>(others);
            foreach (var b in Stacks(me, others, air)) if (!blocks.Contains(b)) blocks.Add(b);
            foreach (var s in starts)
                foreach (var o in blocks)
                {
                    float cx = o.w / 2f + air * 2f, cy = o.h / 2f + air * 2f;
                    var corners = new[] { (o.x - cx, o.y - cy), (o.x + cx, o.y - cy), (o.x + cx, o.y + cy), (o.x - cx, o.y + cy) };
                    for (int i = 0; i < 4; i++)
                    {
                        var (vx, vy) = corners[i];
                        if (!First(s.Item1, s.Item2, vx, vy)) continue;
                        float there = Dist(s.Item1, s.Item2, vx, vy);
                        float len = there + Dist(vx, vy, toX, toY);
                        if (len < bestLen && Later(vx, vy, toX, toY))
                        {
                            bestLen = len;
                            best = new LeaderRoute { ax = s.Item1, ay = s.Item2, vx = vx, vy = vy, bx = toX, by = toY, bends = true };
                        }
                        // A wide label (or a stack) in the way: along its side, corner to corner.
                        for (int step = 1; step <= 3; step += 2)
                        {
                            var (wx, wy) = corners[(i + step) % 4];
                            len = there + Dist(vx, vy, wx, wy) + Dist(wx, wy, toX, toY);
                            if (len >= bestLen || !Later(vx, vy, wx, wy) || !Later(wx, wy, toX, toY)) continue;
                            bestLen = len;
                            best = new LeaderRoute { ax = s.Item1, ay = s.Item2, vx = vx, vy = vy, wx = wx, wy = wy, bx = toX, by = toY, bends = true, twice = true };
                        }
                    }
                }
            return best;
        }

        /// <summary>#209: the stacks: labels (its own among them) whose gaps are under two airs, merged into the box round them all.
        /// Only stacks of two or more come back.</summary>
        public static List<LabelBox> Stacks(LabelBox me, IList<LabelBox> others, float air = 6f)
        {
            var boxes = new List<(LabelBox box, int n)> { (me, 1) };
            foreach (var o in others) boxes.Add((o, 1));
            for (bool merged = true; merged;)
            {
                merged = false;
                for (int i = 0; i < boxes.Count && !merged; i++)
                    for (int j = i + 1; j < boxes.Count && !merged; j++)
                    {
                        var a = boxes[i].box; var b = boxes[j].box;
                        float gx = Math.Abs(a.x - b.x) - (a.w + b.w) / 2f, gy = Math.Abs(a.y - b.y) - (a.h + b.h) / 2f;
                        if (gx >= air * 2f || gy >= air * 2f) continue;
                        float x0 = Math.Min(a.x - a.w / 2f, b.x - b.w / 2f), x1 = Math.Max(a.x + a.w / 2f, b.x + b.w / 2f);
                        float y0 = Math.Min(a.y - a.h / 2f, b.y - b.h / 2f), y1 = Math.Max(a.y + a.h / 2f, b.y + b.h / 2f);
                        boxes[i] = (new LabelBox((x0 + x1) / 2f, (y0 + y1) / 2f, x1 - x0, y1 - y0), boxes[i].n + boxes[j].n);
                        boxes.RemoveAt(j);
                        merged = true;
                    }
            }
            return boxes.Where(b => b.n > 1).Select(b => b.box).ToList();
        }

        static bool Clear(float ax, float ay, float bx, float by, IList<LabelBox> others, float air)
        {
            foreach (var o in others) if (Crosses(ax, ay, bx, by, o, air)) return false;
            return true;
        }

        /// <summary>Does the segment pass through the box, grown by <paramref name="air"/>?</summary>
        public static bool Crosses(float ax, float ay, float bx, float by, LabelBox o, float air = 0f)
        {
            float x0 = o.x - o.w / 2f - air, x1 = o.x + o.w / 2f + air, y0 = o.y - o.h / 2f - air, y1 = o.y + o.h / 2f + air;
            float lo = 0f, hi = 1f, dx = bx - ax, dy = by - ay;
            bool Clip(float p, float q)
            {
                if (Math.Abs(p) < 1e-6f) return q >= 0f;
                float r = q / p;
                if (p < 0f) { if (r > hi) return false; if (r > lo) lo = r; }
                else { if (r < lo) return false; if (r < hi) hi = r; }
                return true;
            }
            return Clip(-dx, ax - x0) && Clip(dx, x1 - ax) && Clip(-dy, ay - y0) && Clip(dy, y1 - ay);
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
