using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #578 (the travellers): the words that hold (the woods only when there are woods; else the meadow or the pond, and the camp sits where
    /// the words said), the wood their people carry to the camp's pile (the pile grows with <see cref="ApartBringsWood"/>, faster while the
    /// wood lean lasts), and the ending: when the village is made (or, in a village, at the next new house) they walk in and join.
    /// No dice: the land is picked from a hash.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Months a joining may wait for the view's walk to finish before the sim finishes it (the view is asleep, or absent).</summary>
        public const int JoinFallbackMonths = 3;

        /// <summary>Debug and tests: "woods", "meadow" or "pond" forces where the next travellers are found (empty = by the land).</summary>
        public string DebugLand = "";

        /// <summary>A pond is somewhere in the box (a cell of the full basin, or of the settlement's map).</summary>
        bool PondInBox
        {
            get
            {
                var m = FutureWaterMask(0f);
                if (m != null) return m.Any(b => b);
                var map = Settlement?.Map;
                return map != null && map.pond.Any(b => b);
            }
        }

        /// <summary>Where the travellers are found: the woods when a grown wild stand stands (<see cref="WoodsExist"/>), else by the pond (when there
        /// is one) or the meadow, picked from the dilemma id and month.</summary>
        string PickLand(int dilemmaId)
        {
            if (!string.IsNullOrEmpty(DebugLand)) return DebugLand;
            if (WoodsExist) return "woods";
            return PondInBox && DilemmaRoll(120, dilemmaId, State.month) < 0.5f ? "pond" : "meadow";
        }

        /// <summary>{land}: "the woods", "the meadow" or "the pond" (a dilemma without a land is the woods).</summary>
        public static string LandWords(Dilemma d) => d?.land == "meadow" ? "the meadow" : d?.land == "pond" ? "the pond" : "the woods";

        /// <summary>{camp}: where the camp they make stands, as a phrase ("in the far woods").</summary>
        public static string CampWords(Dilemma d) => ApartWhere(ApartKindFor(d, ApartKind.WoodsCamp));

        /// <summary>Where a camp of <paramref name="kind"/> stands, as a phrase.</summary>
        public static string ApartWhere(string kind) => kind == ApartKind.MeadowCamp ? "out in the meadow" : kind == ApartKind.PondCamp ? "by the pond"
            : kind == ApartKind.ShoreHut ? "on the far shore" : "in the far woods";

        /// <summary>The camp kind an option makes, by where the dilemma's words put it (only the woods camp is reshaped).</summary>
        public static string ApartKindFor(Dilemma d, string kind)
        {
            if (kind != ApartKind.WoodsCamp || d == null) return kind;
            return d.land == "meadow" ? ApartKind.MeadowCamp : d.land == "pond" ? ApartKind.PondCamp : kind;
        }

        /// <summary>How fast their pile grows against the usual: the wood lean (x1.5, <c>NudgeWood</c>) while it lasts. The view paces the carries by it.</summary>
        public float ApartWoodRate(ApartGroup g) => NudgeWood;

        /// <summary>The group by id, or null.</summary>
        public ApartGroup ApartGroupById(int id) => State.apartGroups?.FirstOrDefault(x => x.id == id);

        /// <summary>A load of logs reaches the camp's pile (the view calls it when the carrier sets it down). The first one is told, once, in a
        /// person's voice. False if the group is gone.</summary>
        public bool ApartBringsWood(int groupId)
        {
            var g = ApartGroupById(groupId);
            if (g == null) return false;
            g.woodLoads++;
            if (!g.firstLoadTold)
            {
                g.firstLoadTold = true;
                var who = g.members.FirstOrDefault(n => n.alive);
                if (who != null)
                    Chronicle("newcomer", $"{who.name} set down the first full load of logs on the camp's pile and called across to {DilemmaPlace}: \"The notch in the axe handle works. Come and see the pile!\"", 0.6f, who.id);
            }
            return true;
        }

        /// <summary>Monthly: a group waiting for a house joins at the next new one; one the view should have walked in by now is finished.</summary>
        void JoinStep()
        {
            var s = State;
            foreach (var g in s.apartGroups.ToList())
            {
                if (!g.joins || g.tower || g.joinState == 0) continue;
                if (g.joinState == 1)
                {
                    if (s.buildings.Any(b => b.type == BuildingType.Hut && b.id > g.joinAfterId)) { g.joinState = 2; g.joinMonth = s.month; }
                }
                else if (s.month - g.joinMonth >= JoinFallbackMonths) FinishApartJoin(g.id);
            }
        }

        /// <summary>The village is made: every travellers' camp starts to pack up (the view walks them to the green, then finishes the join).</summary>
        void ApartJoinOnVillage()
        {
            var s = State;
            foreach (var g in s.apartGroups.ToList())
            {
                if (!g.joins || g.tower || g.joinState == 2) continue;
                if (!g.AnyAlive) { s.apartGroups.Remove(g); continue; }
                g.joinState = 2; g.joinMonth = s.month;
            }
        }

        /// <summary>They have walked in: the living become the settlement's (notables and population), the camp's group is gone (the view
        /// clears the camp with it), a line is written. Not for a group that builds the Keeper's tower. False if there is no such group.</summary>
        public bool FinishApartJoin(int groupId)
        {
            var s = State;
            var g = ApartGroupById(groupId);
            if (g == null || g.tower) return false;
            var alive = g.members.Where(n => n.alive).ToList();
            bool keeper = FindDilemma(g.dilemmaId)?.state == DilemmaState.Answered;
            foreach (var n in alive) { s.notables.Add(n); s.population++; if (keeper) n.chosenByKeeper = true; }
            s.apartGroups.Remove(g);
            if (alive.Count > 0)
                Chronicle("newcomer", $"The {g.family} family, {string.Join(" and ", alive.Select(n => n.name))}, packed up their tents, walked in to the green and joined {DilemmaPlace}. Their pile of logs came with them.", 0.75f, alive[0].id);
            return true;
        }

        // ---------------------------------------------------------------- debug (the debug panel)

        /// <summary>The travellers' group (one that joins, with someone alive), made with a woods camp if none stands.</summary>
        ApartGroup DebugTravellers() =>
            State.apartGroups.FirstOrDefault(x => x.joins && x.AnyAlive && !x.tower) ?? DebugSettlersArrive(ApartKind.WoodsCamp, true);

        /// <summary>Debug: make sure the travellers' camp stands and say which group it is (the view starts a carry; see ApartGroupsView.DebugCarryNow).</summary>
        public ApartGroup DebugTravellersBringWood() => DebugTravellers();

        /// <summary>Debug: the travellers (made if none stand) start to pack up and walk in now, whatever the era.</summary>
        public ApartGroup DebugTravellersJoin()
        {
            var g = DebugTravellers();
            g.joinState = 2; g.joinMonth = State.month;
            return g;
        }
    }
}
