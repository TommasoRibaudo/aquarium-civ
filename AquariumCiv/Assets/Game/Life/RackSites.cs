using System;
using System.Runtime.CompilerServices;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #700: where the camp's two racks (the hide rack and the fish rack) stand once paths exist. A rack is never placed on a path cell
    /// (<see cref="CampSpots.RackAngle"/> / <see cref="CampSpots.FishRackAngle"/> take the path test), and when a path grows over a
    /// standing rack it moves to the nearest free angle at a quiet moment: at night (<see cref="LifeClock.PhaseOf"/> = Night) or when
    /// nothing hangs on it. Until then it stays put. The committed angles live here (not in the saved sim), one memo per sim.
    /// </summary>
    public static class RackSites
    {
        sealed class Memo
        {
            public float hide = float.NaN, fish = float.NaN;
            public bool night;
            public int moves;
            // #700 (B27): with the beat on, a rack a path covers waits for the people to take it down (the planner commits the new angle at the plant)
            public bool beats, force;
            public bool wantsHide, wantsFish;
            public float hideTo, fishTo;
            public int hideSkip, fishSkip;
        }

        static readonly ConditionalWeakTable<SimCore, Memo> memos = new ConditionalWeakTable<SimCore, Memo>();
        static Memo Get(SimCore sim) => memos.GetValue(sim, _ => new Memo());

        /// <summary>The path tier a rack may not stand on (a walked path or road; a faint trail is still grass underfoot).</summary>
        public const byte PathTierMin = PathNet.PathTier;

        /// <summary>True when (x, z) is on a path cell of the settled box. False without a settlement.</summary>
        public static bool PathAt(SimCore sim, float x, float z)
        {
            var map = sim?.Settlement?.Map;
            if (map == null || !sim.Config.settle) return false;
            int c = SiteMap.CellAt(x, z);
            return c >= 0 && map.tier[c] >= PathTierMin;
        }

        /// <summary>#700 (B27): the view plays the take-down and set-up beat, so a rack a path covers stays where it is until
        /// <see cref="Commit"/> (the planner calls it as the carry ends). Without it (tests, plain prototypes) the rack jumps at a quiet moment.</summary>
        public static void EnableBeats(SimCore sim) { if (sim != null) Get(sim).beats = true; }
        public static bool BeatsOn(SimCore sim) => sim != null && Get(sim).beats;

        /// <summary>The cheat's "move now": a rack on a path may be taken down at any hour, loaded or not.</summary>
        public static void ForceQuiet(SimCore sim, bool on) { if (sim != null) Get(sim).force = on; }
        public static bool Forced(SimCore sim) => sim != null && Get(sim).force;

        /// <summary>A rack a path covers that has somewhere to go (beat mode): where it stands and where it moves to.</summary>
        public static bool Pending(SimCore sim, RackKind kind, out float from, out float to)
        {
            var m = Get(sim);
            bool w = kind == RackKind.Hide ? m.wantsHide : m.wantsFish;
            from = kind == RackKind.Hide ? m.hide : m.fish; to = kind == RackKind.Hide ? m.hideTo : m.fishTo;
            return w && !float.IsNaN(from);
        }

        /// <summary>The beat's carry ended: the rack stands at <paramref name="angle"/> from now on.</summary>
        public static void Commit(SimCore sim, RackKind kind, float angle)
        {
            var m = Get(sim);
            if (kind == RackKind.Hide) { m.hide = angle; m.wantsHide = false; } else { m.fish = angle; m.wantsFish = false; }
            m.moves++;
        }

        /// <summary>#762: the lots a rack may not stand under (the strip fields, orchards and pastures).</summary>
        public static bool IsField(LotKind k) => k == LotKind.Farm || k == LotKind.Orchard || k == LotKind.Pasture;

        /// <summary>#762: true when (x, z) lies on a standing field lot (its polygon): a rack never stands under one, and a field laid
        /// over a standing rack makes the rack move (at night, as for a path) when it has somewhere to go. The placer in turn keeps a new
        /// field off the racks (<see cref="Publish"/>).</summary>
        public static bool FieldAt(SimCore sim, float x, float z)
        {
            var w = sim?.Settlement;
            if (w == null || !sim.Config.settle) return false;
            int c = SiteMap.CellAt(x, z);
            if (c < 0) return false;
            int ci = SiteMap.I(c), cj = SiteMap.J(c);
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) continue;
                    int id = w.Map.lot[SiteMap.Index(i, j)];
                    if (id == 0) continue;
                    var l = w.LotById(id);
                    if (l == null || l.retired || l.open || !IsField(l.kind)) continue;
                    // the lot's polygon (its oriented rect), not only the cells it claimed: a cell is 0.1 wide
                    float r = l.yaw * (float)(Math.PI / 180.0), cs = (float)Math.Cos(r), sn = (float)Math.Sin(r);
                    float dx = x - l.x, dz = z - l.z, lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
                    if (Math.Abs(lx) <= l.w / 2f && Math.Abs(lz) <= l.d / 2f) return true;
                }
            return false;
        }

        /// <summary>What a rack may not stand on: a path (#700) or a field (#762). Null when neither can exist.</summary>
        static Func<float, float, bool> RackBlock(SimCore sim)
        {
            var path = PathTest(sim);
            return path == null ? null : (Func<float, float, bool>)((x, z) => path(x, z) || FieldAt(sim, x, z));
        }

        /// <summary>The path test for <paramref name="sim"/>, or null when no path can exist (the first run, plain prototypes).</summary>
        public static Func<float, float, bool> PathTest(SimCore sim)
            => sim?.Settlement?.Map == null || !sim.Config.settle ? null : (Func<float, float, bool>)((x, z) => PathAt(sim, x, z));

        /// <summary>The view time the rack moves are judged by: night is a quiet moment. Call it where the view knows the hour.</summary>
        public static void Observe(SimCore sim, ViewTime now)
        {
            if (sim != null) Get(sim).night = LifeClock.PhaseOf(now.hour) == DayPhase.Night;
        }

        /// <summary>True when the last observed hour was night (a quiet moment for moving a camp thing).</summary>
        public static bool IsNight(SimCore sim) => sim != null && Get(sim).night;

        /// <summary>How many times a rack has moved because of a path (a change counter for caches).</summary>
        public static int Moves(SimCore sim) => sim == null ? 0 : Get(sim).moves;

        /// <summary>#762: tells the settlement where the racks stand, so the placer keeps a new field off them (the rack that came first stays).</summary>
        static void Publish(SimCore sim, float fx, float fz)
        {
            var w = sim?.Settlement;
            if (w == null) return;
            var m = Get(sim);
            w.KeepClear.Clear();
            if (!float.IsNaN(m.hide)) Add(w, fx, fz, m.hide, LifePlanner.HideFrameDistance, CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f);
            if (!float.IsNaN(m.fish)) Add(w, fx, fz, m.fish, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength);
            // #776 (B81.0): the founder's statue on the green: its footprint (0.14 u) and the 0.2 u kept clear all round, so nothing is built on those cells
            if (sim.StatueStands && sim.FindStatueSite(out float sx, out float sz, out float syaw))
            {
                float side = StatueSpot.Footprint + 2f * StatueSpot.Clear;
                w.KeepClear.Add(new Footprint(sx, sz, syaw, side, side));
            }
        }

        static void Add(Settlement w, float fx, float fz, float angle, float dist, float halfDepth, float halfLength)
        {
            float ux = (float)Math.Cos(angle), uz = (float)Math.Sin(angle);
            w.KeepClear.Add(new Footprint(fx + ux * dist, fz + uz * dist, (float)Math.Atan2(-ux, -uz) * (180f / (float)Math.PI), 2f * halfLength, 2f * halfDepth));
        }

        /// <summary>The hide rack's angle round the fire at (<paramref name="fx"/>, <paramref name="fz"/>).</summary>
        public static float Hide(SimCore sim, float fx, float fz)
        {
            float a = HideCore(sim, fx, fz);
            Publish(sim, fx, fz);
            return a;
        }

        static float HideCore(SimCore sim, float fx, float fz)
        {
            var m = Get(sim);
            var onPath = RackBlock(sim);
            if (!float.IsNaN(m.hide) && (onPath == null || !CampSpots.RackOnPath(fx, fz, m.hide, LifePlanner.HideFrameDistance,
                    CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f, onPath))) return m.hide;
            if (!float.IsNaN(m.hide) && m.beats)
            {   // #700 (B27): the people take it down; until they do it stays
                if (!m.wantsHide && m.hideSkip-- <= 0)
                {
                    float to = CampSpots.RackAngle(fx, fz, FishRackSpot.HideAngle(sim.State.seed), LifePlanner.HideFrameDistance, onPath);
                    bool free = !CampSpots.RackOnPath(fx, fz, to, LifePlanner.HideFrameDistance, CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f, onPath);
                    if (free && Math.Abs(to - m.hide) > 1e-4f) { m.hideTo = to; m.wantsHide = true; } else m.hideSkip = 60; // no free spot: it stays, and is asked again later
                }
                return m.hide;
            }
            if (!float.IsNaN(m.hide) && !(m.night || sim.HidesDrying == 0)) return m.hide; // in use by day: it waits for a quiet moment
            float a = CampSpots.RackAngle(fx, fz, FishRackSpot.HideAngle(sim.State.seed), LifePlanner.HideFrameDistance, onPath);
            if (!float.IsNaN(m.hide) && Math.Abs(a - m.hide) > 1e-4f) m.moves++;
            m.hide = a;
            return a;
        }

        /// <summary>The fish rack's angle: clear of the paths, the hide rack and the camp's fixed things.</summary>
        public static float Fish(SimCore sim, float fx, float fz)
        {
            float a = FishCore(sim, fx, fz);
            Publish(sim, fx, fz);
            return a;
        }

        static float FishCore(SimCore sim, float fx, float fz)
        {
            var m = Get(sim);
            var onPath = RackBlock(sim);
            float hide = HideCore(sim, fx, fz);
            if (!float.IsNaN(m.fish)
                && (onPath == null || !CampSpots.RackOnPath(fx, fz, m.fish, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength, onPath))
                && CampSpots.AngleGap(m.fish, hide) >= CampSpots.RackGap) return m.fish;
            if (!float.IsNaN(m.fish) && m.beats)
            {   // #700 (B27): the people take it down; until they do it stays
                if (!m.wantsFish && m.fishSkip-- <= 0)
                {
                    float to = CampSpots.FishRackAngle(fx, fz, hide, onPath);
                    bool free = !CampSpots.RackOnPath(fx, fz, to, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength, onPath) && CampSpots.AngleGap(to, hide) >= CampSpots.RackGap;
                    if (free && Math.Abs(to - m.fish) > 1e-4f) { m.fishTo = to; m.wantsFish = true; } else m.fishSkip = 60;
                }
                return m.fish;
            }
            if (!float.IsNaN(m.fish) && !(m.night || sim.FishOnRack == 0)) return m.fish;
            float a = CampSpots.FishRackAngle(fx, fz, hide, onPath);
            if (!float.IsNaN(m.fish) && Math.Abs(a - m.fish) > 1e-4f) m.moves++;
            m.fish = a;
            return a;
        }
    }
}
