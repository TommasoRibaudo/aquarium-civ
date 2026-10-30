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
    /// Trait flavour for the planner, from #28 (the role-holder's trait colours their term). Kept to this small interface
    /// so #28 can plug its table in; <see cref="NeutralLifeHints"/> is the default until then.
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

    /// <summary>
    /// World positions for abstract places, from M2's placement layer (SiteMap / Placer / PathNet). Until it exists the
    /// planner runs on abstract places only (<see cref="NoPlacePositions"/>). When plugged in, the planner uses it for walk
    /// lengths and for who stands near a Keeper gesture.
    /// </summary>
    public interface IPlacePositions
    {
        bool TryGetPosition(Place place, out float x, out float z);
    }

    public sealed class NoPlacePositions : IPlacePositions
    {
        public static readonly NoPlacePositions Instance = new NoPlacePositions();
        public bool TryGetPosition(Place place, out float x, out float z) { x = z = 0f; return false; }
    }
}
