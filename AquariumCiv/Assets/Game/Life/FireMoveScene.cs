using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #246 (keepers-hands §2.1): the tribe carries its fire to the first green. The chief (or whoever is nearest) scoops the
    /// embers into a pot, a couple of grown-ups lift the hearth stones, and they carry them over to the green's edge; there the
    /// stones go down, the embers are blown back to life, and everyone cheers round the new fire. The kids run along. Those too
    /// far off look up and point. Staged by <see cref="LifePlanner.StartFireMove"/> on the relic moments' machinery; data,
    /// not code: tune here.
    /// </summary>
    public static class FireMoveScene
    {
        /// <summary>Real seconds the move lasts by default [est]; a longer carry stretches it (<see cref="SecondsFor"/>).</summary>
        public const float Seconds = 40f;
        /// <summary>The longest it stretches, so a carry across the box still walks at the walking pace [est].</summary>
        public const float MaxSeconds = 75f;

        /// <summary>The beats' fractions: they see the green, go to the old fire and pick up, carry, set the new fire, warm.</summary>
        public const float See = 0.08f, Lift = 0.4f, Set = 0.76f, Kindle = 0.88f;

        /// <summary>The scene's length for a carry of <paramref name="carrySeconds"/> at the walking pace: the carry beat
        /// walks in at most half its length (the planner's rule), with a little to spare.</summary>
        public static float SecondsFor(float carrySeconds)
            => Math.Min(MaxSeconds, Math.Max(Seconds, carrySeconds * 1.15f / (0.5f * (Set - Lift))));

        /// <summary>The move as beats (relic-moment data).</summary>
        public static RelicMomentDef For(float seconds)
        {
            var d = new RelicMomentDef { relicId = null, seconds = seconds, maxGrown = 2, maxKids = 2, maxQueue = 3, standIns = true };
            void B(string id, RelicCast c, float from, float to, ActivityKind k, RelicWhere w, Prop prop = Prop.None, bool carry = false)
                => d.beats.Add(new RelicBeat { id = id, cast = c, from = from, to = to, kind = k, where = w, prop = prop, carry = carry });

            // The chief: the embers.
            B("see the green", RelicCast.Owner, 0f, See, ActivityKind.LookUp, RelicWhere.Here);
            B("scoop the embers", RelicCast.Owner, See, Lift, ActivityKind.PickUp, RelicWhere.OldFire, Prop.Embers);
            B("carry the embers", RelicCast.Owner, Lift, Set, ActivityKind.SetUp, RelicWhere.Spot, Prop.Embers, carry: true);
            B("blow them alive", RelicCast.Owner, Set, Kindle, ActivityKind.Poke, RelicWhere.Spot, Prop.Stick);
            B("warm", RelicCast.Owner, Kindle, 1f, ActivityKind.Cheer, RelicWhere.Spot);
            // Grown-ups: the hearth stones.
            B("see the green", RelicCast.Grown, 0f, See, ActivityKind.LookUp, RelicWhere.Here);
            B("lift the stones", RelicCast.Grown, See, Lift, ActivityKind.PickUp, RelicWhere.OldFire, Prop.Block);
            B("carry the stones", RelicCast.Grown, Lift, Set, ActivityKind.SetUp, RelicWhere.Spot, Prop.Block, carry: true);
            B("ring the fire", RelicCast.Grown, Set, Kindle, ActivityKind.SetUp, RelicWhere.Spot, Prop.Block);
            B("warm", RelicCast.Grown, Kindle, 1f, ActivityKind.Cheer, RelicWhere.Spot);
            // The kids run along.
            B("see the green", RelicCast.Kids, 0f, See, ActivityKind.Point, RelicWhere.Here);
            B("watch them lift it", RelicCast.Kids, See, Lift, ActivityKind.Watch, RelicWhere.OldFire);
            B("run along", RelicCast.Kids, Lift, Set, ActivityKind.Play, RelicWhere.Spot);
            B("jump", RelicCast.Kids, Set, Kindle, ActivityKind.JumpAndPoint, RelicWhere.Spot);
            B("warm", RelicCast.Kids, Kindle, 1f, ActivityKind.Cheer, RelicWhere.Spot);
            // Too far off: they look up and point from where they are.
            B("look up", RelicCast.Onlookers, 0f, See, ActivityKind.LookUp, RelicWhere.Here);
            B("point", RelicCast.Onlookers, See, Lift, ActivityKind.Point, RelicWhere.Here);
            return d;
        }
    }

    /// <summary>#246: what the view needs to draw the fire while it is carried (<see cref="LifePlanner.FireMoveAt"/>).</summary>
    public struct FireMoveLook
    {
        /// <summary>Where it stood and where it goes (box units).</summary>
        public float fromX, fromZ, toX, toZ;
        public long day;
        /// <summary>View hours: the scene's start, when the embers leave the old fire, when the new fire is set, the end.</summary>
        public float start, liftHour, setHour, end;
        /// <summary>0: the old fire still burns (they come to it); 1: carried (the old spot is cold stones, the embers are in
        /// the pot); 2: the new fire burns.</summary>
        public int phase;
    }
}
