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

        /// <summary>The path test for <paramref name="sim"/>, or null when no path can exist (the first run, plain prototypes).</summary>
        public static Func<float, float, bool> PathTest(SimCore sim)
            => sim?.Settlement?.Map == null || !sim.Config.settle ? null : (Func<float, float, bool>)((x, z) => PathAt(sim, x, z));

        /// <summary>The view time the rack moves are judged by: night is a quiet moment. Call it where the view knows the hour.</summary>
        public static void Observe(SimCore sim, ViewTime now)
        {
            if (sim != null) Get(sim).night = LifeClock.PhaseOf(now.hour) == DayPhase.Night;
        }

        /// <summary>How many times a rack has moved because of a path (a change counter for caches).</summary>
        public static int Moves(SimCore sim) => sim == null ? 0 : Get(sim).moves;

        /// <summary>The hide rack's angle round the fire at (<paramref name="fx"/>, <paramref name="fz"/>).</summary>
        public static float Hide(SimCore sim, float fx, float fz)
        {
            var m = Get(sim);
            var onPath = PathTest(sim);
            if (!float.IsNaN(m.hide) && (onPath == null || !CampSpots.RackOnPath(fx, fz, m.hide, LifePlanner.HideFrameDistance,
                    CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f, onPath))) return m.hide;
            if (!float.IsNaN(m.hide) && !(m.night || sim.HidesDrying == 0)) return m.hide; // in use by day: it waits for a quiet moment
            float a = CampSpots.RackAngle(fx, fz, FishRackSpot.HideAngle(sim.State.seed), LifePlanner.HideFrameDistance, onPath);
            if (!float.IsNaN(m.hide) && Math.Abs(a - m.hide) > 1e-4f) m.moves++;
            m.hide = a;
            return a;
        }

        /// <summary>The fish rack's angle: clear of the paths, the hide rack and the camp's fixed things.</summary>
        public static float Fish(SimCore sim, float fx, float fz)
        {
            var m = Get(sim);
            var onPath = PathTest(sim);
            float hide = Hide(sim, fx, fz);
            if (!float.IsNaN(m.fish)
                && (onPath == null || !CampSpots.RackOnPath(fx, fz, m.fish, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength, onPath))
                && CampSpots.AngleGap(m.fish, hide) >= CampSpots.RackGap) return m.fish;
            if (!float.IsNaN(m.fish) && !(m.night || sim.FishOnRack == 0)) return m.fish;
            float a = CampSpots.FishRackAngle(fx, fz, hide, onPath);
            if (!float.IsNaN(m.fish) && Math.Abs(a - m.fish) > 1e-4f) m.moves++;
            m.fish = a;
            return a;
        }
    }
}
