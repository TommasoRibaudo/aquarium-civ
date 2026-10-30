using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>How one person's trait colours their day (life-in-the-box rule 9). All zero = no colour.</summary>
    public struct PersonHints
    {
        /// <summary>Chance of a nap on the green after lunch (the lazy), per day.</summary>
        public float napChance;
        /// <summary>Chance of singing at the evening gathering (the singer), per day.</summary>
        public float singChance;
        /// <summary>Chance of sitting with the old in free time (the kind), per day.</summary>
        public float sitWithOldChance;
        /// <summary>Chance of a walk up the Keeper's hill to look at the glass (the dreamer), per day.</summary>
        public float hillChance;
        /// <summary>Hours earlier this person goes home at night (the grump, the worrier).</summary>
        public float homeEarlyHours;
    }

    /// <summary>How a role-holder's trait colours the whole box while they hold the role (notable-roles §4).</summary>
    public struct BoxHints
    {
        /// <summary>Hours earlier everyone goes home at night (the grump's quiet place, the worrier).</summary>
        public float homeEarlyHours;
        /// <summary>Extra sweeping blocks for the sweepers (the grump, the worrier).</summary>
        public int sweeperExtraBlocks;
        /// <summary>Chance of a feast evening at the seat, per day (the singer, the joker; no mood bonus).</summary>
        public float feastChance;
        /// <summary>Chance the crowd gathers to listen to the holder at midday (the loud), per day.</summary>
        public float announceChance;
    }

    /// <summary>
    /// Trait flavour for the planner, from #28 (the role-holder's trait colours their term). <see cref="TermLifeHints"/>
    /// reads #28's <see cref="TermFlavor"/>; <see cref="NeutralLifeHints"/> (no colour) is the default.
    /// </summary>
    public interface ILifeHints
    {
        /// <summary>This person's own flavour (a hobby in free time; the holder's behaviour in office). <paramref name="role"/>
        /// is their role or null.</summary>
        PersonHints Person(Notable n, string role);

        /// <summary>The box-wide flavour while <paramref name="holder"/> holds <paramref name="role"/>.</summary>
        BoxHints Office(Notable holder, string role);
    }

    /// <summary>No trait colour: everyone follows the plain schedule.</summary>
    public sealed class NeutralLifeHints : ILifeHints
    {
        public static readonly NeutralLifeHints Instance = new NeutralLifeHints();
        public PersonHints Person(Notable n, string role) => default;
        public BoxHints Office(Notable holder, string role) => default;
    }

    /// <summary>A point on a walk (world x, z on the box floor).</summary>
    public struct Waypoint
    {
        public float x, z;
        public Waypoint(float x, float z) { this.x = x; this.z = z; }
        public override string ToString() => $"({x:0.00}, {z:0.00})";
    }

    /// <summary>
    /// Where things are, from M2's placement layer (#32, Sim/Settle; <see cref="SettlePlacePositions"/>): positions and
    /// doors of the abstract places, the walking cost and routes along the path network, and who lives where. Without it
    /// (<see cref="NoPlacePositions"/>, or the sim's settling off) the planner runs on abstract places: a stand-in home
    /// per family, a flat quarter hour per walk.
    /// </summary>
    public interface IPlacePositions
    {
        /// <summary>Where the activity at a place happens (a lot's door, a field's middle, the fire, a heap). False when
        /// the place has no spot (yet).</summary>
        bool TryGetPosition(Place place, out float x, out float z);

        /// <summary>The walking cost from one place to another along the people's walking cost (paths are cheaper; lots and
        /// the pond can't be crossed), in world units of plain ground. False when either end has no spot or there is no way.</summary>
        bool TryWalkLength(Place from, Place to, out float length);

        /// <summary>The walk itself, door to door along the path network: <paramref name="into"/> gets the cell centres from
        /// start to end. False when either end has no spot or there is no way.</summary>
        bool TryRoute(Place from, Place to, List<Waypoint> into);

        /// <summary>The home a notable sleeps in (a sim building id) and the household's head (a notable id): children live
        /// with a parent, partners share a home. False = no placed home; the planner uses its stand-in.</summary>
        bool TryGetHome(Notable n, out int buildingId, out int householdId);

        /// <summary>Building ids of the homes standing now, in a stable order (the crowd lives in them). Empty = stand-in.</summary>
        void GetHomes(List<int> buildingIds);
    }

    public sealed class NoPlacePositions : IPlacePositions
    {
        public static readonly NoPlacePositions Instance = new NoPlacePositions();
        public bool TryGetPosition(Place place, out float x, out float z) { x = z = 0f; return false; }
        public bool TryWalkLength(Place from, Place to, out float length) { length = 0f; return false; }
        public bool TryRoute(Place from, Place to, List<Waypoint> into) { into?.Clear(); return false; }
        public bool TryGetHome(Notable n, out int buildingId, out int householdId) { buildingId = householdId = -1; return false; }
        public void GetHomes(List<int> buildingIds) => buildingIds.Clear();
    }
}
