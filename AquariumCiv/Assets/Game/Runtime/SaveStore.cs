using System;
using System.Globalization;
using System.IO;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.Game
{
    /// <summary>
    /// The slice's save (vertical-slice-plan.md §5.2, from the save-versioning spike). What it adds over the prototype save:
    /// - a version number and one migration step per version (old saves load; a save from a newer build is refused,
    ///   never overwritten);
    /// - atomic writes (write a temp file, then swap; the last good save stays as .bak, read if the main file is broken);
    /// - the last-seen time lives in the file only (the prototypes also keep a PlayerPrefs copy, which wins);
    /// - clock sanity: a clock moved backwards counts as no time away, and a jump beyond <see cref="MaxAwayHours"/> is capped.
    /// A save without a version (the prototypes' <c>{lastSeenUtc, state}</c>) reads as version 0.
    /// </summary>
    public static class SaveStore
    {
        public const int CurrentVersion = 3;
        public const double MaxAwayHours = 30 * 24;

        [Serializable]
        public class Envelope
        {
            public int v;
            public string lastSeenUtc;
            public SimState state;      // the box being played (the shelf's active box)
            public ShelfState shelf;    // version 3 (#87): the boxes on the shelf; the active box's state is the one above
        }

        public enum LoadResult { Loaded, LoadedFromBackup, Missing, Corrupt, TooNew }

        /// <summary>Migrations[i] turns a version-i envelope into version i+1, in place.</summary>
        static readonly Action<Envelope>[] Migrations =
        {
            e => { }, // 0 → 1: prototype saves; same fields, only the version is new
            e => { }, // 1 → 2 (#32, placement): the new state.settle reads as unsettled (version 0); the sim gives every
                      // building a lot from its old slot order once, when the SimCore is made (Sim/Settle, Settlement.Found)
            e => e.shelf = ShelfState.Single(e.state), // 2 → 3 (#87, the shelf): today's single box becomes a shelf of one
        };

        public static void Write(string path, SimState state, DateTime nowUtc, ShelfState shelf = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var json = JsonUtility.ToJson(new Envelope
            {
                v = CurrentVersion, lastSeenUtc = nowUtc.ToString("o", CultureInfo.InvariantCulture), state = state,
                shelf = (shelf ?? ShelfState.Single(state)).ForSave(),
            });
            string tmp = path + ".tmp", bak = path + ".bak";
            File.WriteAllText(tmp, json);
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, bak); return; }
                catch (Exception) { File.Copy(path, bak, true); File.Delete(path); } // platforms without Replace
            }
            File.Move(tmp, path);
        }

        public static LoadResult Read(string path, out Envelope envelope)
        {
            envelope = null;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return LoadResult.Missing;
            var main = TryParse(path, out envelope);
            if (main == LoadResult.Loaded || main == LoadResult.TooNew) return main;
            var bak = TryParse(path + ".bak", out envelope);
            return bak == LoadResult.Loaded ? LoadResult.LoadedFromBackup : bak == LoadResult.TooNew ? bak : main;
        }

        static LoadResult TryParse(string path, out Envelope e)
        {
            e = null;
            if (!File.Exists(path)) return LoadResult.Missing;
            try { e = JsonUtility.FromJson<Envelope>(File.ReadAllText(path)); }
            catch (Exception) { return LoadResult.Corrupt; }
            if (e?.state?.rng == null) { e = null; return LoadResult.Corrupt; }
            if (e.v > CurrentVersion) { e = null; return LoadResult.TooNew; }
            while (e.v < CurrentVersion) Migrations[e.v++](e);
            e.shelf = ShelfState.Attach(e.shelf, e.state); // the active box's state is the envelope's own
            return LoadResult.Loaded;
        }

        /// <summary>Real hours away since the save, clamped to [0, MaxAwayHours]. capped = the clock jumped further.</summary>
        public static double AwayHours(Envelope e, DateTime nowUtc, out bool capped)
        {
            capped = false;
            if (e == null || !DateTime.TryParse(e.lastSeenUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last))
                return 0;
            double h = (nowUtc - last.ToUniversalTime()).TotalHours;
            if (h > MaxAwayHours) { capped = true; return MaxAwayHours; }
            return Math.Max(0, h);
        }
    }
}
