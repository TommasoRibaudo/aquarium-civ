using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #282 phase 1: the sim side of the rabbits you see. Tracks come first (a milestone at half the meadow the first rabbit
    /// needs, at least a season before it), then the first rabbit with an arrival cell at the meadow's edge, then the count
    /// the view shows (<see cref="RabbitHerd.Shown"/>). A hunt's catch (<see cref="CatchRabbit"/>) lowers the same
    /// <c>EcoState.rabbits</c> the grazing reads, and the model regrows it toward meadow / 35. With the ecology off none of
    /// this does anything.
    /// </summary>
    public partial class SimCore
    {
        public const int TrackCellCount = 5;

        /// <summary>The first rabbit hopped in at the meadow's edge: the cell (the view plays the sign, then <see cref="AckRabbitArrival"/>).</summary>
        public event Action<int> RabbitArrived;

        public int EcoRabbitCount => EcologyOn ? (int)MathF.Round(State.eco.rabbits) : 0;
        /// <summary>How many rabbit dolls to show: the sim count, capped for the phone.</summary>
        public int EcoRabbitsShown(int cap = RabbitHerd.DefaultCap) => EcologyOn ? RabbitHerd.Shown(State.eco.rabbits, cap) : 0;
        public bool EcoRabbitTracksSeen => EcologyOn && State.eco.Reached(EcoMilestones.RabbitTracks);

        /// <summary>The cells where the tracks show (the meadow's edge), while they are there: from the tracks milestone until
        /// two seasons after the first rabbit. Empty before, after, and with the ecology off.</summary>
        public IReadOnlyList<int> EcoRabbitTrackCells
        {
            get
            {
                var e = State.eco;
                if (!EcologyOn || e.trackCells == null || !e.Reached(EcoMilestones.RabbitTracks)) return Array.Empty<int>();
                long first = e.MonthOf(EcoMilestones.FirstRabbit);
                if (first >= 0 && State.month - first >= 6) return Array.Empty<int>();
                return e.trackCells;
            }
        }

        /// <summary>The warren: the cell the first rabbit came in at, or -1 before it.</summary>
        public int EcoRabbitWarren => EcologyOn ? State.eco.rabbitArrivalCell : -1;
        public int EcoRabbitsCaught => EcologyOn ? State.eco.rabbitsCaught : 0;

        /// <summary>True from the first rabbit until the view has played its arrival (then <see cref="AckRabbitArrival"/>).</summary>
        public bool EcoRabbitArrivalPending => EcologyOn && State.eco.rabbitArrivalCell >= 0 && !State.eco.rabbitArrivalSeen;
        /// <summary>The view played the first rabbit's sign. True the first time only.</summary>
        public bool AckRabbitArrival()
        {
            if (!EcoRabbitArrivalPending) return false;
            State.eco.rabbitArrivalSeen = true;
            return true;
        }

        /// <summary>Rebuilds the view's <see cref="RabbitHerd.Field"/> from the land as it is now (call at start, each season).</summary>
        public void EcoRabbitsRefresh(RabbitHerd.Field field)
        {
            if (!EcologyOn) return;
            field.Refresh(State.eco.cover, EcoGround.yard, State.eco.seed, State.eco.rabbitArrivalCell, EcoRabbitBlocked());
        }

        readonly bool[] rabbitBlocked = new bool[EcoLand.N];

        /// <summary>#400 (playtest: rabbits "jump up and down when hitting buildings and fields"): the cells the rabbits keep off whatever
        /// grows there: every lot (houses, fields, pastures, the camp's tents) and the camp's green round the fire. Null when nothing
        /// is settled. The array is reused: copy it to keep it.</summary>
        public bool[] EcoRabbitBlocked()
        {
            var map = Config.settle ? Settlement?.Map : null;
            if (map == null || map.lot == null) return null;
            for (int c = 0; c < EcoLand.N; c++) rabbitBlocked[c] = map.lot[c] != 0 || map.Green(c);
            return rabbitBlocked;
        }

        /// <summary>#400: a key that changes when the rabbits' ground does (the green cover or the blocked cells): the view refreshes its field then.</summary>
        public int EcoRabbitGroundKey()
        {
            if (!EcologyOn) return 0;
            var cover = State.eco.cover;
            var blocked = EcoRabbitBlocked();
            int h = 17;
            for (int c = 0; c < cover.Length; c++)
                h = h * 31 + (RabbitHerd.IsGreen((EcoCover)cover[c]) && (blocked == null || !blocked[c]) ? c + 1 : 0);
            return h;
        }

        void PickRabbitTracks()
        {
            var e = State.eco; var cells = new List<int>();
            RabbitHerd.EdgeCells(e.cover, EcoGround.yard, e.seed, 17, TrackCellCount, cells);
            e.trackCells = cells.ToArray();
        }

        void PickRabbitArrival()
        {
            var e = State.eco; var cells = new List<int>();
            // prefer one of the tracks' cells that is still green: the rabbit comes where the prints were
            if (e.trackCells != null)
                foreach (int c in e.trackCells) if (RabbitHerd.IsGreen((EcoCover)e.cover[c])) { cells.Add(c); break; }
            if (cells.Count == 0) RabbitHerd.EdgeCells(e.cover, EcoGround.yard, e.seed, 91, 1, cells);
            e.rabbitArrivalCell = cells.Count > 0 ? cells[0] : -1;
            if (e.rabbitArrivalCell >= 0) RabbitArrived?.Invoke(e.rabbitArrivalCell);
        }

        /// <summary>A hunt caught a rabbit: the sim count goes down by one, the same count the grazing reads, and the model regrows it
        /// toward meadow / 35 at the next seasons (never under 2 by then). False when there is none to catch.</summary>
        public bool CatchRabbit()
        {
            if (!EcologyOn || State.eco.rabbits < 0.5f) return false;
            State.eco.rabbits = MathF.Max(0f, State.eco.rabbits - 1f);
            State.eco.rabbitsCaught++;
            return true;
        }

        // ---------- debug ----------
        /// <summary>Debug ("rabbit tracks now"): the tracks show at the meadow's edge now, whatever the meadow says. False when
        /// the ecology is off or they are already there.</summary>
        public bool DebugRabbitTracksNow()
        {
            if (!EcologyOn || !EcoM.Reach(EcoMilestones.RabbitTracks, State.month)) return false;
            EcoTell();
            return true;
        }

        /// <summary>Debug ("+N rabbits"): add <paramref name="n"/> rabbits to the sim count (the first ones through the tracks and the
        /// first-rabbit sign). The model pulls the count back toward the meadow at the next season. Returns the new count.</summary>
        public int DebugAddRabbits(int n)
        {
            if (!EcologyOn || n <= 0) return EcoRabbitCount;
            var e = State.eco;
            EcoM.Reach(EcoMilestones.RabbitTracks, State.month);
            EcoM.Reach(EcoMilestones.FirstRabbit, State.month);
            e.rabbits = MathF.Max(e.rabbits, 0f) + n;
            EcoTell();
            return EcoRabbitCount;
        }
    }
}
