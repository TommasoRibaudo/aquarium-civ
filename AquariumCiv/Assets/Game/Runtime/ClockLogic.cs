using System;
using System.Globalization;
using System.IO;
using AquariumCiv.Sim;

namespace AquariumCiv.Game
{
    /// <summary>What happened when the box opened: how it loaded, how long the player was away, how much the civ lived.</summary>
    public class ClockStart
    {
        public SimCore sim;
        /// <summary>The boxes on the shelf (#87); <see cref="sim"/> is the active one.</summary>
        public ShelfState shelf;
        public SaveStore.LoadResult load;
        public bool newWorld;
        /// <summary>False when the save on disk is from a newer build: this session never writes over it.</summary>
        public bool canSave = true;
        /// <summary>Where a broken save was moved, or null.</summary>
        public string corruptKeptAs;
        public double awayHours;
        /// <summary>The phone clock jumped further than <see cref="SaveStore.MaxAwayHours"/>; the absence was capped.</summary>
        public bool awayCapped;
        public CatchUpResult catchUp;

        public string Summary()
        {
            var c = CultureInfo.InvariantCulture;
            var s = sim.State;
            string how = newWorld ? $"new world (seed {s.seed}, save {load})" : $"loaded ({load})";
            string away = newWorld ? "" : string.Format(c, " · away {0:0.00} h{1} → lived {2:0.00} h ({3} months{4})",
                awayHours, awayCapped ? " (capped)" : "", catchUp.simHours, catchUp.months, catchUp.Slowed ? ", slowed" : "");
            string extra = (corruptKeptAs != null ? $" · broken save kept as {Path.GetFileName(corruptKeptAs)}" : "") +
                           (canSave ? "" : " · NOT SAVING (save is from a newer build)");
            return string.Format(c, "{0}{1} · year {2:0.0} · pop {3} · {4} buildings · mess {5:0.00}{6}",
                how, away, s.Year, s.population, s.buildings.Count, sim.MessLevel, extra);
        }
    }

    /// <summary>
    /// The decisions behind <see cref="GameClock"/>, kept out of the MonoBehaviour so they can be tested (vertical-slice-plan
    /// §5.2): load or start a world, keep broken saves aside, never overwrite a save from a newer build, run the catch-up.
    /// </summary>
    public static class ClockLogic
    {
        public static ClockStart Start(string path, DateTime nowUtc, SimConfig config, int newSeed)
        {
            var r = new ClockStart { load = SaveStore.Read(path, out var e) };
            switch (r.load)
            {
                case SaveStore.LoadResult.Loaded:
                case SaveStore.LoadResult.LoadedFromBackup:
                    // A broken main file next to a good .bak would become the next .bak: move it aside first.
                    if (r.load == SaveStore.LoadResult.LoadedFromBackup && File.Exists(path)) r.corruptKeptAs = KeepAside(path);
                    r.sim = new SimCore(config, e.state);
                    r.shelf = e.shelf;
                    r.awayHours = SaveStore.AwayHours(e, nowUtc, out r.awayCapped);
                    r.catchUp = r.sim.CatchUp(r.awayHours);
                    return r;
                case SaveStore.LoadResult.Corrupt:
                    r.corruptKeptAs = KeepAside(path);
                    string bak = KeepAside(path + ".bak");
                    r.corruptKeptAs ??= bak;
                    break;
                case SaveStore.LoadResult.TooNew:
                    r.canSave = false;
                    break;
            }
            r.newWorld = true;
            r.sim = new SimCore(config, SimCore.NewWorld(newSeed, config));
            r.shelf = ShelfState.Single(r.sim.State);
            return r;
        }

        /// <summary>Back from the background: the time away runs as a catch-up (clock sanity as on open).</summary>
        public static CatchUpResult Resume(SimCore sim, DateTime pausedUtc, DateTime nowUtc)
        {
            double h = Math.Clamp((nowUtc - pausedUtc).TotalHours, 0, SaveStore.MaxAwayHours);
            return sim.CatchUp(h);
        }

        /// <summary>Writes the save unless this session must not (a newer build's save is on disk).</summary>
        public static bool Save(string path, ClockStart session, DateTime nowUtc)
        {
            if (!session.canSave) return false;
            SaveStore.Write(path, session.sim.State, nowUtc, session.shelf);
            return true;
        }

        /// <summary>The finished box becomes a museum and box 2 starts as the active box (#87). The session's sim is the new box
        /// afterwards. Returns it. Saves are the caller's.</summary>
        public static SimCore StartNextBox(ClockStart session, SimConfig config, BoxCompletion completion, string setting, int seed, DateTime nowUtc)
        {
            session.shelf ??= ShelfState.Single(session.sim.State);
            Shelf.FinishActive(session.shelf, session.sim, completion, nowUtc);
            session.sim = Shelf.StartNextBox(session.shelf, config, setting, seed, nowUtc, completion);
            return session.sim;
        }

        /// <summary>Moves a broken file to <c>name.corrupt</c> (replacing an older one) and returns the new path.</summary>
        static string KeepAside(string file)
        {
            if (!File.Exists(file)) return null;
            string to = file + ".corrupt";
            if (File.Exists(to)) File.Delete(to);
            File.Move(file, to);
            return to;
        }
    }
}
