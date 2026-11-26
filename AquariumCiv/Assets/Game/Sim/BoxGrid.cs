using System;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #658: every box is 6 x 6 now (60 x 60 cells). A box saved when the land was 60 x 44 grows on load: the 16 new rows are the
    /// BACK of the land (z up to 3.8), so row 0 and the first 2640 cell ids are unchanged and every lot, cover cell, history entry and
    /// coordinate stays valid; each per-cell array just gets its new tail. The new land is bare, dry, base-soil ground
    /// (the user's call 2026-11-15: "grow old boxes"). Idempotent: a box that is already full size is left alone, so a second load
    /// is a no-op. Called from the <see cref="SimCore"/> constructor, so a loaded box, a shelf box, a museum copy and a run-ahead
    /// copy all go through it before anything reads a cell.
    /// What is NOT padded, on purpose: the land history and the path history store cell ids and replay up to their own length;
    /// <see cref="SimState.shapeCells"/> and every other saved cell id and lot rectangle point at old rows.
    /// </summary>
    public static class BoxGrid
    {
        /// <summary>The cells of a box before the deeper box (60 x 44).</summary>
        public const int OldN = 60 * 44;

        /// <summary>Pads every saved per-cell array of <paramref name="s"/> to <see cref="SiteMap.N"/>. Returns true when anything grew.
        /// <paramref name="land"/> is called only when the ecology needs the base soil of the new cells (it may be null: then the new soil is the start soil).</summary>
        public static bool Grow(SimState s, Func<EcoLand> land, EcoTuning tuning)
        {
            if (s == null) return false;
            bool grew = false;
            var st = s.settle;
            if (st != null)
            {
                grew |= Pad(ref st.flags); grew |= Pad(ref st.tier); grew |= Pad(ref st.wear);
                grew |= Pad(ref st.dirC); grew |= Pad(ref st.dirS);
                grew |= Pad(ref st.lot); grew |= Pad(ref st.ring); grew |= Pad(ref st.pathMonth);
            }
            var e = s.eco;
            if (e != null && e.on && e.cover != null && e.cover.Length > 0 && e.cover.Length < EcoLand.N)
            {
                int old = e.cover.Length;
                var l = land?.Invoke();
                tuning ??= new EcoTuning();
                grew |= Pad(ref e.cover); // Bare = 0
                Pad(ref e.soil);
                for (int c = old; c < EcoLand.N; c++)
                    e.soil[c] = tuning.soilStart + tuning.soilStartBase * (l != null ? l.baseSoil[c] : 0f);
                Pad(ref e.moisture);
                for (int c = old; c < EcoLand.N; c++) e.moisture[c] = 0.04f;
                // wet, puddle, sprout, watered: zeros. A layer of the wrong length is wiped whole (EcoModel.EnsureLayers).
                Pad(ref e.wet); Pad(ref e.puddle); Pad(ref e.sprout); Pad(ref e.watered);
            }
            if (e != null && e.on && e.seen != null && e.seen.Length > 0) // empty: no mask yet (HasMask false); padding it would make one
            {
                int bytes = (EcoLand.N + 7) / 8;
                if (e.seen.Length < bytes) { Array.Resize(ref e.seen, bytes); grew = true; } // the new cells are unseen (not green yet)
            }
            return grew;
        }

        static bool Pad<T>(ref T[] a)
        {
            // null or empty: not recorded (an old, unsettled save; Unity's JsonUtility loads an absent array as an empty one), the sim makes it
            if (a == null || a.Length == 0 || a.Length >= SiteMap.N) return false;
            Array.Resize(ref a, SiteMap.N);
            return true;
        }
    }
}
