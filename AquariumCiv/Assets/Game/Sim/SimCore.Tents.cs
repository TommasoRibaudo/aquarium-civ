using System;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #284 phase 1: on a living land (ecology on) the camp's tents are made of the hunt's hides, not of wood. Each tent takes
    /// <see cref="HidesPerTent"/> hides from the frame by the fire. When the camp gate opens the first new tent is raised as a
    /// small moment (<see cref="CampBegan"/>); until the hides are there the moment waits. Before the gate nothing is built
    /// (SimCore.cs PickBuilding). With the ecology off none of this does anything and tents cost wood as before.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Hides a tent takes. Two: the herd's floor (2) leaves the hunt little at the gate, and a hunter lands about 0.6 of a
        /// hunt, but the hunt opens with the first rabbit (before the gate), so hides are usually drying by the time the gate opens; a
        /// tent costing two means the first tent is up within the first weeks after the gate, and each later one is a few days' hunt.</summary>
        public const int HidesPerTent = 2;

        /// <summary>A tent is built from hides now: the ecology is on and the town is still a camp (later eras build houses from wood).</summary>
        public bool TentsFromHides => EcologyOn && State.era == Era.Camp;

        /// <summary>The gate is open and the first new tent has not been raised yet (the moment is waiting, perhaps for hides).</summary>
        public bool FirstTentWaiting => EcologyOn && State.eco.campTentState == 1;
        /// <summary>The camp's first new tent was raised from hides (the moment has played).</summary>
        public bool FirstTentRaised => EcologyOn && State.eco.campTentState == 2;
        /// <summary>Tents raised from hides so far.</summary>
        public int TentsFromHidesCount => EcologyOn ? State.eco.tentsFromHides : 0;

        /// <summary>A tent was raised from hides: the new building and whether it is the camp's first (the moment). The view stages the
        /// people carrying poles and hides from the frame (LifePlanner.BeginTentRaising).</summary>
        public event Action<Building, bool> TentRaised;
        /// <summary>The camp begins: the first new tent was raised from the hides. The view leans the camera and the glass voice says its line.</summary>
        public event Action<Building> CampBegan;

        void ArmFirstTent()
        {
            if (!EcologyOn) return;
            if (State.eco.campTentState == 0) State.eco.campTentState = 1;
            if (State.buildings.Count < Config.buildingSlots) TryTentFromHides(); // the hides were drying already: no wait
        }

        /// <summary>Raise a tent from hides if one is wanted (the first one always is, once the gate is open) and the hides are there.</summary>
        bool TryTentFromHides()
        {
            var s = State;
            if (BeforeCamp || s.buildings.Count >= Config.buildingSlots || State.eco.hidesDrying < HidesPerTent) return false;
            bool first = State.eco.campTentState == 1;
            if (!first && PickBuilding(s.population) != BuildingType.Hut) return false;
            return RaiseTent(first) != null;
        }

        Building RaiseTent(bool first)
        {
            if (TakeHides(HidesPerTent) < HidesPerTent) return null;
            var b = AddBuilding(BuildingType.Hut);
            State.eco.tentsFromHides++;
            if (first) State.eco.campTentState = 2;
            TentRaised?.Invoke(b, first);
            if (first) CampBegan?.Invoke(b);
            return b;
        }

        /// <summary>Debug ("open the camp gate now"): the gate opens if shut, the hides are given if short, and the first tent goes up as the
        /// moment (also when the moment already played: it is armed again so it can be seen). The new tent gets its lot at once.
        /// Returns the tent, or null when the ecology is off or the box is full.</summary>
        public Building DebugCampMoment()
        {
            if (!EcologyOn || State.buildings.Count >= Config.buildingSlots) return null;
            if (State.eco.hidesDrying < HidesPerTent) DebugAddHides(HidesPerTent - State.eco.hidesDrying);
            State.eco.campTentState = 1;
            if (State.eco.campGateMonth < 0) DebugOpenCampGate(); // arms and (hides being there) raises the tent through the same path
            if (State.eco.campTentState == 1) RaiseTent(true);
            Settlement?.PlacePending();
            return State.eco.campTentState == 2 ? State.buildings[State.buildings.Count - 1] : null;
        }
    }
}
