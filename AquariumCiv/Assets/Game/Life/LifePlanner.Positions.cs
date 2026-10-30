using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>
    /// Where a person is at a view time (#64), and interrupted walks.
    ///
    /// Scheduled interruptions (role beats, this month's scenes, the weather, relationships) are layered over the day
    /// before it is stitched (<see cref="Stitch"/>): a walk they cut heads for the interruption's place instead of its old
    /// destination, and its rest re-routes from wherever the interruption left the person.
    ///
    /// Unplanned interruptions (Keeper reactions, live scenes) arrive while the view is running, so they are laid over
    /// the stitched day at query time as <see cref="Block"/>s: the person stops where they are (their progress point on
    /// the route), reacts there (or walks from there to the scene), then re-routes from that spot to the next place their
    /// day takes them, waiting there if they arrive early. A person's position is a pure function of (seed, person, day,
    /// time, the live interruptions) and never jumps: every move starts where the last one ended.
    /// </summary>
    public sealed partial class LifePlanner
    {
        // ======================================================================== public API

        /// <summary>Where a person is on the box floor (world x, z) at a view time: at the place they are doing something, or
        /// the current point along the route of the walk they are on. Continuous in time, also across interruptions. False
        /// without positions (<see cref="NoPlacePositions"/>) or when a place has no spot yet.</summary>
        public bool PositionAt(PersonRef who, ViewTime t, out float x, out float z)
        {
            x = z = 0f;
            Sync();
            EnsureDay(t.day);
            PruneLive(t);
            if (!byKey.TryGetValue(who.Key, out var p)) return false;
            var blocks = BlocksOf(p);
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                if (t.hour < b.bs || t.hour >= b.be) continue;
                if (!b.placed) break;
                BlockPos(b, t.hour, out x, out z);
                return true;
            }
            return PlanPos(p, t.hour, out x, out z);
        }

        // ======================================================================== routes as polylines

        sealed class Poly
        {
            public Waypoint[] pts;
            public float[] cum;
            public float len;

            public static Poly Of(List<Waypoint> list)
            {
                var p = new Poly { pts = list.ToArray(), cum = new float[list.Count] };
                for (int i = 1; i < p.pts.Length; i++)
                {
                    float dx = p.pts[i].x - p.pts[i - 1].x, dz = p.pts[i].z - p.pts[i - 1].z;
                    p.cum[i] = p.cum[i - 1] + (float)Math.Sqrt(dx * dx + dz * dz);
                }
                p.len = p.pts.Length > 0 ? p.cum[p.pts.Length - 1] : 0f;
                return p;
            }

            /// <summary>The point a fraction <paramref name="f"/> of the way along (by length).</summary>
            public void At(float f, out float x, out float z)
            {
                int n = pts.Length;
                if (n == 1 || len <= 1e-6f || f <= 0f) { x = pts[0].x; z = pts[0].z; return; }
                if (f >= 1f) { x = pts[n - 1].x; z = pts[n - 1].z; return; }
                float d = f * len;
                int lo = 0, hi = n - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (cum[mid] <= d) lo = mid; else hi = mid;
                }
                float seg = cum[hi] - cum[lo];
                float u = seg > 1e-6f ? (d - cum[lo]) / seg : 0f;
                x = pts[lo].x + (pts[hi].x - pts[lo].x) * u;
                z = pts[lo].z + (pts[hi].z - pts[lo].z) * u;
            }
        }

        static readonly Poly NoPoly = new Poly();
        readonly Dictionary<(Place, Place), Poly> polyCache = new Dictionary<(Place, Place), Poly>();
        readonly List<Waypoint> wpLine = new List<Waypoint>(64), wpRoute = new List<Waypoint>(64);

        /// <summary>The walk from one place to another: the activity spot, the path network's route door to door, the
        /// other spot. Straight when there is no route; null when either place has no spot. Cached until the sim moves.</summary>
        Poly RouteBetween(Place a, Place b)
        {
            if (polyCache.TryGetValue((a, b), out var poly)) return poly == NoPoly ? null : poly;
            poly = null;
            if (positions.TryGetPosition(a, out float ax, out float az) && positions.TryGetPosition(b, out float bx, out float bz))
            {
                wpLine.Clear();
                wpLine.Add(new Waypoint(ax, az));
                if (a != b && positions.TryRoute(a, b, wpRoute)) wpLine.AddRange(wpRoute);
                wpLine.Add(new Waypoint(bx, bz));
                poly = Poly.Of(wpLine);
            }
            polyCache[(a, b)] = poly ?? NoPoly;
            return poly;
        }

        /// <summary>The walk from a world point to a place (<see cref="IPointRoutes"/>, else straight); null if the place
        /// has no spot.</summary>
        Poly RouteFromPoint(float x, float z, Place to)
        {
            if (!positions.TryGetPosition(to, out float bx, out float bz)) return null;
            wpLine.Clear();
            wpLine.Add(new Waypoint(x, z));
            if (positions is IPointRoutes pr && pr.TryRouteFrom(x, z, to, wpRoute)) wpLine.AddRange(wpRoute);
            wpLine.Add(new Waypoint(bx, bz));
            return Poly.Of(wpLine);
        }

        /// <summary>From where someone stands (at <paramref name="at"/> if they are at a place) to a place.</summary>
        Poly RouteFrom(float x, float z, Place at, Place to) => (at.IsNone ? null : RouteBetween(at, to)) ?? RouteFromPoint(x, z, to);

        /// <summary>View hours to walk a route of this length (as <see cref="WalkHours"/> times a walk between places).</summary>
        static float WalkTimeOf(float len) => len <= 1e-4f ? 0f : Math.Max(MinWalk, Math.Min(MaxWalk, len / WalkSpeed));

        const float SamePoint = 0.02f; // world units: closer than this is the same spot

        static bool Near(float ax, float az, float bx, float bz) => (ax - bx) * (ax - bx) + (az - bz) * (az - bz) <= SamePoint * SamePoint;

        /// <summary>Where the stitched day has the person at <paramref name="hour"/>: a move's route at its time fraction,
        /// else the place's spot.</summary>
        bool PlanPos(Person p, float hour, out float x, out float z)
        {
            var segs = DayOf(p);
            var s = segs[Find(segs, hour)];
            var place = s.place;
            var from = s.Moving ? s.from : s.place;
            if (!IsValidPlace(place) || !IsValidPlace(from)) place = from = ValidHome(p);
            if (from != place)
            {
                var poly = RouteBetween(from, place);
                if (poly == null) { x = z = 0f; return false; }
                float f = Math.Max(0f, Math.Min(1f, (hour - s.start) / Math.Max(1e-4f, s.end - s.start)));
                poly.At(f, out x, out z);
                return true;
            }
            return positions.TryGetPosition(place, out x, out z);
        }

        // ======================================================================== unplanned interruptions

        /// <summary>One reaction, or one segment of a live scene, for one person.</summary>
        struct Piece
        {
            public float t0, t1;
            public bool reaction;
            public int index;    // into reactions or live
            public Place target; // where it happens; None = where they stand
            public Seg seg;      // a live scene's segment
            public int order;
        }

        /// <summary>A stretch of a person's position: standing at a point, or walking part of a route.</summary>
        struct Leg
        {
            public float t0, t1;
            public Poly poly;    // null = standing at (x, z)
            public float f0, f1; // the part of the route walked
            public float x, z;
            public Place at;     // standing at this place (None = somewhere along a route), or the walk's destination
            public bool tail;    // the walk back into the day (or waiting where the day goes next)
        }

        /// <summary>Overlapping interruptions of one person, the walk back into their day after them, and the legs their
        /// position follows meanwhile. [bs, ie) is the interruptions; [ie, be) the way back.</summary>
        sealed class Block
        {
            public float bs, ie, be;
            public readonly List<Piece> pieces = new List<Piece>(2);
            public readonly List<Leg> legs = new List<Leg>(4);
            public bool placed;       // positions known: the legs are valid
            public float sx, sz;      // where they stopped
            public Place stopAt;      // the place they stopped at (None = halfway along a walk)
            public Place stopPlace;   // abstract: the place they were at or nearest to (for the activity)
            public Seg after;         // the stop in their day they head for afterwards
            public bool hasAfter;
        }

        static readonly Block[] NoBlocks = new Block[0];
        readonly Dictionary<int, Block[]> blockCache = new Dictionary<int, Block[]>();
        readonly List<Piece> pieceScratch = new List<Piece>(8);

        /// <summary>A reaction or a live scene was added or removed: re-plan the interruptions.</summary>
        void LiveChanged() => blockCache.Clear();

        /// <summary>Drops reactions and live scenes that are over (a day old).</summary>
        void PruneLive(ViewTime t)
        {
            bool changed = false;
            for (int i = reactions.Count - 1; i >= 0; i--)
            {
                var r = reactions[i];
                if (r.day < t.day - 1 || (r.day == t.day - 1 && t.hour > 1f)) { reactions.RemoveAt(i); changed = true; }
            }
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].day < t.day - 1) { live.RemoveAt(i); changed = true; }
            if (changed) LiveChanged();
        }

        Block[] BlocksOf(Person p)
        {
            if (reactions.Count == 0 && live.Count == 0) return NoBlocks;
            if (blockCache.TryGetValue(p.key, out var bl)) return bl;
            bl = BuildBlocks(p);
            blockCache[p.key] = bl;
            return bl;
        }

        Block[] BuildBlocks(Person p)
        {
            long day = dayInfo.day;
            var pieces = pieceScratch;
            pieces.Clear();
            for (int i = 0; i < reactions.Count; i++)
            {
                var r = reactions[i];
                if (r.day != day || r.end <= r.start) continue;
                if (!Reacts(p, r, new ViewTime(day, r.start), out _, out _, out var to)) continue;
                pieces.Add(new Piece { t0 = r.start, t1 = r.end, reaction = true, index = i, target = to, order = pieces.Count });
            }
            for (int i = 0; i < live.Count; i++)
            {
                var sc = live[i];
                if (sc.day != day) continue;
                liveScratch.Clear();
                SceneLayer(sc, p, dayInfo, liveScratch, true);
                foreach (var s in liveScratch)
                    pieces.Add(new Piece { t0 = s.start, t1 = s.end, index = i, target = s.place, seg = s, order = pieces.Count });
            }
            if (pieces.Count == 0) return NoBlocks;
            pieces.Sort((a, b) => a.t0 != b.t0 ? a.t0.CompareTo(b.t0) : a.order.CompareTo(b.order));

            var blocks = new List<Block>(1);
            Block cur = null;
            foreach (var pc in pieces)
            {
                if (cur == null || pc.t0 >= cur.be)
                {
                    cur = NewBlock(p, pc.t0);
                    blocks.Add(cur);
                }
                AddPiece(p, cur, pc);
            }
            return blocks.ToArray();
        }

        Block NewBlock(Person p, float t0)
        {
            var segs = DayOf(p);
            var s = segs[Find(segs, t0)];
            var b = new Block { bs = t0, ie = t0, be = t0 };
            b.stopPlace = s.Moving && (t0 - s.start) < (s.end - s.start) * 0.5f ? s.from : s.place;
            b.stopAt = s.Moving ? Place.None : IsValidPlace(s.place) ? s.place : ValidHome(p);
            b.placed = PlanPos(p, t0, out b.sx, out b.sz);
            return b;
        }

        void AddPiece(Person p, Block b, Piece pc)
        {
            b.pieces.Add(pc);
            float coreEnd = b.ie, t0 = pc.t0, t1 = Math.Max(pc.t0, pc.t1);
            if (b.placed)
            {
                Truncate(b, t0);
                EndPoint(b, out float cx, out float cz, out Place cat);
                float tx = 0f, tz = 0f;
                bool goes = !pc.target.IsNone && pc.target != cat && positions.TryGetPosition(pc.target, out tx, out tz) && !Near(cx, cz, tx, tz);
                if (goes)
                {
                    var poly = RouteFrom(cx, cz, cat, pc.target);
                    // #92: the walk takes its time at the walking pace, in real seconds (a walk squeezed into half the piece
                    // rushed at up to 8x across the box). One that outlasts the piece is cut where the next piece (or the
                    // walk back into the day) starts, and goes on from there.
                    float w = LiveWalkHours(dayInfo.day, t0, poly.len);
                    if (w > 0f) b.legs.Add(new Leg { t0 = t0, t1 = t0 + w, poly = poly, f0 = 0f, f1 = 1f, at = pc.target });
                    if (t0 + w < t1) b.legs.Add(new Leg { t0 = t0 + w, t1 = t1, x = tx, z = tz, at = pc.target });
                }
                else
                {
                    var at = !pc.target.IsNone && (pc.target == cat || positions.TryGetPosition(pc.target, out tx, out tz) && Near(cx, cz, tx, tz)) ? pc.target : cat;
                    b.legs.Add(new Leg { t0 = t0, t1 = t1, x = cx, z = cz, at = at });
                }
                if (coreEnd > t1)
                {
                    // A short interruption inside a longer one: the longer one goes on where this one left them.
                    Truncate(b, t1);
                    EndPoint(b, out cx, out cz, out cat);
                    b.legs.Add(new Leg { t0 = t1, t1 = coreEnd, x = cx, z = cz, at = cat });
                }
            }
            b.ie = Math.Max(coreEnd, t1);
            Tail(p, b);
        }

        /// <summary>The way back into the day: from where the interruptions left them to the next place their day has them
        /// stay (re-routing from that spot, never jumping to where the plan has got to), waiting there if early.</summary>
        void Tail(Person p, Block b)
        {
            b.be = b.ie;
            b.hasAfter = false;
            if (!b.placed) return;
            Truncate(b, b.ie); // a walk still going when the interruptions end goes on from where it has got to (#92)
            EndPoint(b, out float ex, out float ez, out Place eat);
            var segs = DayOf(p);
            for (int i = Find(segs, b.ie); i < segs.Length; i++)
            {
                var s = segs[i];
                if (s.Moving) continue;
                var place = IsValidPlace(s.place) ? s.place : ValidHome(p);
                if (!positions.TryGetPosition(place, out float px, out float pz)) continue;
                Poly poly = null;
                float w = 0f;
                if (place != eat && !Near(ex, ez, px, pz))
                {
                    poly = RouteFrom(ex, ez, eat, place);
                    w = LiveWalkHours(dayInfo.day, b.ie, poly.len); // #92: at the pace in real seconds
                }
                if (b.ie + w > s.end - 0.01f && i < segs.Length - 1) continue; // gone before they'd get there: the next stop
                if (w > 0f) b.legs.Add(new Leg { t0 = b.ie, t1 = b.ie + w, poly = poly, f0 = 0f, f1 = 1f, at = place, tail = true });
                float arrive = b.ie + w;
                if (arrive < s.start) b.legs.Add(new Leg { t0 = arrive, t1 = s.start, x = px, z = pz, at = place, tail = true });
                b.after = s;
                b.after.place = b.after.from = place;
                b.hasAfter = true;
                b.be = Math.Max(arrive, s.start);
                return;
            }
        }

        /// <summary>Cut the legs off at <paramref name="t"/> (a new interruption starts there).</summary>
        static void Truncate(Block b, float t)
        {
            for (int i = b.legs.Count - 1; i >= 0; i--)
            {
                var l = b.legs[i];
                if (l.t0 >= t) { b.legs.RemoveAt(i); continue; }
                if (l.t1 > t)
                {
                    if (l.poly != null)
                    {
                        l.f1 = l.f0 + (l.f1 - l.f0) * (t - l.t0) / (l.t1 - l.t0);
                        l.at = l.f1 >= 0.9999f ? l.at : Place.None;
                    }
                    l.t1 = t;
                    b.legs[i] = l;
                }
                break;
            }
        }

        /// <summary>Where the legs end so far, and at which place (None = along a route).</summary>
        static void EndPoint(Block b, out float x, out float z, out Place at)
        {
            if (b.legs.Count == 0) { x = b.sx; z = b.sz; at = b.stopAt; return; }
            var l = b.legs[b.legs.Count - 1];
            if (l.poly == null) { x = l.x; z = l.z; at = l.at; return; }
            l.poly.At(l.f1, out x, out z);
            at = l.f1 >= 0.9999f ? l.at : Place.None;
        }

        static void LegPos(in Leg l, float t, out float x, out float z)
        {
            if (l.poly == null) { x = l.x; z = l.z; return; }
            float u = LegU(l, t);
            l.poly.At(l.f0 + (l.f1 - l.f0) * u, out x, out z);
        }

        /// <summary>How far along a leg at <paramref name="t"/>, by real seconds (#92): a walk that crosses a phase boundary
        /// (into the night's faster hours) keeps one pace instead of speeding up 3x past it.</summary>
        static float LegU(in Leg l, float t)
        {
            if (l.t1 <= l.t0) return 1f;
            if (t <= l.t0) return 0f;
            if (t >= l.t1) return 1f;
            float all = LifeClock.RealSecondsBetween(l.t0, Math.Min(24f, l.t1));
            return all > 1e-5f ? Math.Max(0f, Math.Min(1f, LifeClock.RealSecondsBetween(l.t0, t) / all)) : (t - l.t0) / (l.t1 - l.t0);
        }

        static int LegAt(Block b, float t)
        {
            for (int i = b.legs.Count - 1; i >= 0; i--) if (b.legs[i].t0 <= t) return i;
            return -1;
        }

        static void BlockPos(Block b, float t, out float x, out float z)
        {
            int i = LegAt(b, t);
            if (i < 0) { x = b.sx; z = b.sz; return; }
            LegPos(b.legs[i], t, out x, out z);
        }

        /// <summary>What someone does inside an interruption block: a reaction beats a live scene; between and after them,
        /// the walk back into the day.</summary>
        PersonActivity BlockActivity(Person p, Block b, ViewTime t)
        {
            float h = t.hour;
            int li = b.placed ? LegAt(b, h) : -1;
            bool walking = li >= 0 && b.legs[li].poly != null && h < b.legs[li].t1;
            if (h < b.ie)
            {
                for (int i = b.pieces.Count - 1; i >= 0; i--)
                {
                    var pc = b.pieces[i];
                    if (pc.reaction && h >= pc.t0 && h < pc.t1) return ReactionActivity(p, pc, t);
                }
                for (int i = b.pieces.Count - 1; i >= 0; i--)
                {
                    var pc = b.pieces[i];
                    if (pc.reaction || h < pc.t0 || h >= pc.t1) continue;
                    var seg = pc.seg;
                    if (seg.Moving || ActivityInfo.IsMove(seg.kind)) seg.from = b.stopPlace;
                    if (seg.place.IsNone)
                    {
                        // #92: a staged beat done where they stand: the place they stand at (or walked to), else the nearest.
                        var here = li >= 0 && !b.legs[li].at.IsNone ? b.legs[li].at : b.stopPlace;
                        seg.place = seg.from = IsValidPlace(here) ? here : ValidHome(p);
                    }
                    var a = ToActivity(p, seg, t);
                    if (walking && !b.legs[li].tail)
                    {
                        var load = a.prop;
                        WalkLeg(ref a, b, li, h);
                        if (seg.carries) { a.kind = ActivityKind.Carry; a.prop = load; } // #92: a chunk to the fire, the doll to its post
                    }
                    return a;
                }
            }
            if (li >= 0 && b.legs[li].tail)
            {
                if (walking)
                {
                    var a = new PersonActivity { who = p.who, partner = PersonRef.None, source = ActivitySource.Schedule, trip = TripType.Errand, pace = dayInfo.pace, chronicleId = -1 };
                    WalkLeg(ref a, b, li, h);
                    return a;
                }
                if (b.hasAfter) return ToActivity(p, b.after, t); // arrived early: waiting where the day goes next
            }
            var segs = DayOf(p);
            return ToActivity(p, segs[Find(segs, h)], t);
        }

        void WalkLeg(ref PersonActivity a, Block b, int li, float h)
        {
            var l = b.legs[li];
            a.kind = ActivityKind.Walk;
            a.moving = true;
            a.place = l.at.IsNone ? a.place : l.at;
            a.from = b.stopPlace;
            a.prop = Prop.None;
            a.start = l.t0;
            a.end = l.t1;
            a.progress = LegU(l, h);
            a.step = 0;
            a.facesGlass = false;
        }

        PersonActivity ReactionActivity(Person p, Piece pc, ViewTime t)
        {
            var r = reactions[pc.index];
            if (!Reacts(p, r, t, out var kind, out var prop, out var to))
                Reacts(p, r, new ViewTime(t.day, r.start), out kind, out prop, out to);
            var segs = DayOf(p);
            var under = segs[Find(segs, t.hour)];
            var here = under.Moving && (t.hour - under.start) < (under.end - under.start) * 0.5f ? under.from : under.place;
            if (under.kind == ActivityKind.Sleep) here = p.home; // stepping out of the door
            var s = new Seg
            {
                start = r.start, end = r.end, kind = kind, place = to.IsNone ? here : to, from = here, prop = prop, partner = PersonRef.None,
                src = ActivitySource.Reaction, trip = TripType.None, chronicleId = -1,
            };
            return ToActivity(p, s, t);
        }
    }
}
