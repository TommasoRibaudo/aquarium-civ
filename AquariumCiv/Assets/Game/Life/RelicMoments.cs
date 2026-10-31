using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>Who in a relic moment a beat is for (#92). The planner casts them when the moment starts
    /// (<see cref="LifePlanner.StartRelicMoment"/>): the principals from the relic, the rest from whoever is near enough to
    /// walk there at the walking pace.</summary>
    public enum RelicCast
    {
        /// <summary>The relic's owner (<see cref="Relic.ownerId"/>): the taster, the mask's wearer, the doll's maker, the
        /// Lens's scholar.</summary>
        Owner,
        /// <summary>A second notable: the Sky Crumb's bravest (pokes it), the Sandal's finder.</summary>
        Second,
        /// <summary>Who keeps it now (<see cref="Relic.holderId"/>): the Sandal's owner, or the heir who sets it on the stone.</summary>
        Holder,
        /// <summary>The Sandal's holder when that is an heir, not the owner (the owner died): the remembering stone instead of a dance.</summary>
        Heir,
        /// <summary>Children near enough (at most <see cref="RelicMomentDef.maxKids"/>).</summary>
        Kids,
        /// <summary>Grown-ups near enough (at most <see cref="RelicMomentDef.maxGrown"/>).</summary>
        Grown,
        /// <summary>The Lens's queue: each takes a turn to look (the beat is split into one turn each).</summary>
        Queue,
    }

    /// <summary>Where a beat happens.</summary>
    public enum RelicWhere
    {
        /// <summary>Where they stand (no walk).</summary>
        Here,
        /// <summary>The relic's spot (<see cref="PlaceKind.Relic"/>): where the crumb fell, the doll's post, the vines, the tripod.</summary>
        Spot,
        /// <summary>The fire (the camp's fire, the village hearth).</summary>
        Fire,
        /// <summary>The gathering place (the fire, the green, the square).</summary>
        Gather,
        /// <summary>The front glass (<see cref="PlaceKind.Glass"/>).</summary>
        Glass,
        /// <summary>The remembering stone (the cairn, later the churchyard).</summary>
        Remembering,
    }

    /// <summary>One beat of a relic moment: a cast group does one thing at one place over a part of the moment.</summary>
    public sealed class RelicBeat
    {
        public string id;
        /// <summary>The part of the moment, as fractions of its length [from, to).</summary>
        public float from, to;
        public RelicCast cast;
        public ActivityKind kind;
        public Prop prop;
        public RelicWhere where;
        public bool facesGlass;
        /// <summary>They walk there carrying <see cref="prop"/> (a chunk to the fire, the doll to its post).</summary>
        public bool carry;
        /// <summary>A line of the bank (<see cref="SimCore.LineBank"/>) said at the beat's start by the group's first member
        /// (0 = none). <see cref="LifePlanner.RelicLinesAt"/> fills the name.</summary>
        public int lineNo;

        public float Length => to - from;
    }

    /// <summary>A relic moment as data: its length in real seconds and its beats. Tune here, not in code.</summary>
    public sealed class RelicMomentDef
    {
        public string relicId;
        /// <summary>Real seconds (converted to view hours from the start with <see cref="LifeClock.Advance"/>).</summary>
        public float seconds;
        /// <summary>The view hour it plays best at (the Lens at dusk), or -1 = whenever it happens.</summary>
        public float preferredHour = -1f;
        public int maxKids = 3, maxGrown = 5, maxQueue = 5;
        /// <summary>A principal who can't walk there in time is stood in for by the nearest grown-up who can (true), or
        /// plays their part where they stand (false: the mask's wearer, the Sandal's owner, nobody else is them).</summary>
        public bool standIns = true;
        public readonly List<RelicBeat> beats = new List<RelicBeat>();

        public IEnumerable<RelicBeat> For(RelicCast c) { foreach (var b in beats) if (b.cast == c) yield return b; }
    }

    /// <summary>
    /// The five relic moments (relics-and-glass-voice.md §1), staged in the world by the Life planner (#92). They are
    /// short scenes of the people round a thing, not cinematics: the camera stays the player's (the era moment's director,
    /// #88, owns the camera for the era changes). Each group's beats tile the whole moment, so nobody idles mid-scene;
    /// a walk to a new place takes at most half its beat at the walking pace, or the person doesn't join (the planner's
    /// cast check).
    /// </summary>
    public static class RelicMoments
    {
        public static RelicMomentDef For(string relicId) => relicId switch
        {
            RelicId.SkyCrumb => SkyCrumb(),
            RelicId.FestivalMask => FestivalMask(),
            RelicId.KeeperDoll => KeeperDoll(),
            RelicId.LeftSandal => LeftSandal(),
            RelicId.Lens => Lens(),
            _ => null,
        };

        static void B(RelicMomentDef d, string id, RelicCast c, float from, float to, ActivityKind k, RelicWhere w, Prop prop = Prop.None,
                      bool faces = false, bool carry = false, int line = 0)
            => d.beats.Add(new RelicBeat { id = id, cast = c, from = from, to = to, kind = k, where = w, prop = prop, facesGlass = faces, carry = carry, lineNo = line });

        /// <summary>§1.1: a crumb the size of a hut thuds down by the shaping. Everyone freezes and steps back; the kids creep
        /// up first, the bravest pokes it, the chief tastes it, a cheer, and chunks go to the fire.</summary>
        public static RelicMomentDef SkyCrumb()
        {
            var d = new RelicMomentDef { relicId = RelicId.SkyCrumb, seconds = 40f };
            foreach (var c in new[] { RelicCast.Kids, RelicCast.Grown })
            {
                B(d, "thud", c, 0f, 0.12f, ActivityKind.Freeze, RelicWhere.Here);
                B(d, "back", c, 0.12f, 0.25f, ActivityKind.StepBack, RelicWhere.Here);
            }
            B(d, "creep", RelicCast.Kids, 0.25f, 0.55f, ActivityKind.CreepUp, RelicWhere.Spot);
            B(d, "watch", RelicCast.Kids, 0.55f, 0.74f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "keep back", RelicCast.Grown, 0.25f, 0.55f, ActivityKind.GazeWary, RelicWhere.Here);
            B(d, "come closer", RelicCast.Grown, 0.55f, 0.74f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "thud", RelicCast.Second, 0f, 0.12f, ActivityKind.Freeze, RelicWhere.Here);
            B(d, "behind the kids", RelicCast.Second, 0.12f, 0.55f, ActivityKind.GazeWary, RelicWhere.Spot);
            B(d, "poke", RelicCast.Second, 0.55f, 0.64f, ActivityKind.Poke, RelicWhere.Spot, Prop.Stick);
            B(d, "watch", RelicCast.Second, 0.64f, 0.74f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "thud", RelicCast.Owner, 0f, 0.12f, ActivityKind.Freeze, RelicWhere.Here);
            B(d, "come over", RelicCast.Owner, 0.12f, 0.62f, ActivityKind.GazeWary, RelicWhere.Spot);
            B(d, "taste", RelicCast.Owner, 0.62f, 0.74f, ActivityKind.Taste, RelicWhere.Spot, Prop.Crumb);
            foreach (var c in new[] { RelicCast.Owner, RelicCast.Second, RelicCast.Grown, RelicCast.Kids })
                B(d, "cheer", c, 0.74f, 0.86f, ActivityKind.Cheer, RelicWhere.Spot, line: c == RelicCast.Grown ? 9 : 0);
            foreach (var c in new[] { RelicCast.Owner, RelicCast.Second, RelicCast.Grown })
                B(d, "to the fire", c, 0.86f, 1f, ActivityKind.Deliver, RelicWhere.Fire, Prop.Crumb, carry: true);
            B(d, "to the fire", RelicCast.Kids, 0.86f, 1f, ActivityKind.Play, RelicWhere.Fire);
            return d;
        }

        /// <summary>§1.2: at the first era festival the leader speaks, then wears the sky's face in the ring dance, everyone
        /// dancing facing the glass. Nobody hides; that is the point.</summary>
        public static RelicMomentDef FestivalMask()
        {
            var d = new RelicMomentDef { relicId = RelicId.FestivalMask, seconds = 30f, standIns = false, maxGrown = 8 };
            B(d, "gather", RelicCast.Owner, 0f, 0.25f, ActivityKind.Cheer, RelicWhere.Gather);
            B(d, "speech", RelicCast.Owner, 0.25f, 0.4f, ActivityKind.Speech, RelicWhere.Gather, Prop.Mask, line: 14);
            B(d, "dance", RelicCast.Owner, 0.4f, 1f, ActivityKind.Dance, RelicWhere.Gather, Prop.Mask, faces: true);
            foreach (var c in new[] { RelicCast.Grown, RelicCast.Kids })
            {
                B(d, "gather", c, 0f, 0.4f, c == RelicCast.Kids ? ActivityKind.ListenStory : ActivityKind.Listen, RelicWhere.Gather);
                B(d, "dance", c, 0.4f, 1f, ActivityKind.Dance, RelicWhere.Gather, faces: true);
            }
            return d;
        }

        /// <summary>§1.3: the maker carries the doll to its post by the shrine (or the green) and sets it up facing the glass;
        /// people gather, some bow, kids copy its pose, then wave at the doll and at the glass.</summary>
        public static RelicMomentDef KeeperDoll()
        {
            var d = new RelicMomentDef { relicId = RelicId.KeeperDoll, seconds = 36f };
            B(d, "carry", RelicCast.Owner, 0f, 0.35f, ActivityKind.Deliver, RelicWhere.Spot, Prop.Doll, carry: true);
            B(d, "set up", RelicCast.Owner, 0.35f, 0.5f, ActivityKind.SetUp, RelicWhere.Spot, Prop.Doll);
            B(d, "proud", RelicCast.Owner, 0.5f, 1f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "gather", RelicCast.Grown, 0f, 0.5f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "bow", RelicCast.Grown, 0.5f, 0.75f, ActivityKind.Bow, RelicWhere.Spot, line: 23);
            B(d, "look", RelicCast.Grown, 0.75f, 1f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "gather", RelicCast.Kids, 0f, 0.5f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "copy", RelicCast.Kids, 0.5f, 0.7f, ActivityKind.CopyPose, RelicWhere.Spot);
            B(d, "wave at the doll", RelicCast.Kids, 0.7f, 0.82f, ActivityKind.Wave, RelicWhere.Spot);
            B(d, "wave at the glass", RelicCast.Kids, 0.82f, 1f, ActivityKind.Wave, RelicWhere.Spot, faces: true);
            return d;
        }

        /// <summary>§1.4: a sandal flips out of the cut vines. The finder picks it up and holds it up to the glass; the owner
        /// comes over, puts it on and does a lopsided little dance. An heir takes it to the remembering stone instead.</summary>
        public static RelicMomentDef LeftSandal()
        {
            var d = new RelicMomentDef { relicId = RelicId.LeftSandal, seconds = 32f, standIns = false };
            B(d, "go over", RelicCast.Second, 0f, 0.25f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "pick up", RelicCast.Second, 0.25f, 0.35f, ActivityKind.PickUp, RelicWhere.Spot, Prop.Sandal);
            B(d, "hold up", RelicCast.Second, 0.35f, 0.5f, ActivityKind.HoldUp, RelicWhere.Spot, Prop.Sandal, faces: true);
            B(d, "give", RelicCast.Second, 0.5f, 0.58f, ActivityKind.Give, RelicWhere.Spot, Prop.Sandal);
            B(d, "clap", RelicCast.Second, 0.58f, 1f, ActivityKind.Clap, RelicWhere.Spot);
            foreach (var c in new[] { RelicCast.Holder, RelicCast.Heir })
                B(d, "come over", c, 0f, 0.5f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "put on", RelicCast.Holder, 0.5f, 0.66f, ActivityKind.PutOn, RelicWhere.Spot, Prop.Sandal, line: 28);
            B(d, "dance", RelicCast.Holder, 0.66f, 1f, ActivityKind.LopsidedDance, RelicWhere.Spot);
            B(d, "take it", RelicCast.Heir, 0.5f, 0.58f, ActivityKind.HoldUp, RelicWhere.Spot, Prop.Sandal, line: 28);
            B(d, "to the stone", RelicCast.Heir, 0.58f, 1f, ActivityKind.Deliver, RelicWhere.Remembering, Prop.Sandal, carry: true);
            foreach (var c in new[] { RelicCast.Grown, RelicCast.Kids })
            {
                B(d, "look", c, 0f, 0.5f, ActivityKind.Watch, RelicWhere.Spot);
                B(d, "laugh", c, 0.5f, 1f, c == RelicCast.Kids ? ActivityKind.LaughPoint : ActivityKind.Clap, RelicWhere.Spot);
            }
            return d;
        }

        /// <summary>§1.5: at dusk the scholar sets a brass tube on a tripod, points it at the lantern, then past it at the
        /// camera. A queue forms; each looks, leans back startled, laughs, and lets the next one look.</summary>
        public static RelicMomentDef Lens()
        {
            var d = new RelicMomentDef { relicId = RelicId.Lens, seconds = 48f, preferredHour = 18.4f };
            B(d, "carry", RelicCast.Owner, 0f, 0.2f, ActivityKind.Deliver, RelicWhere.Spot, Prop.Tripod, carry: true);
            B(d, "set up", RelicCast.Owner, 0.2f, 0.3f, ActivityKind.SetUp, RelicWhere.Spot, Prop.Lens);
            B(d, "look", RelicCast.Owner, 0.3f, 0.4f, ActivityKind.LookThroughLens, RelicWhere.Spot, Prop.Lens, line: 33);
            B(d, "turn it", RelicCast.Owner, 0.4f, 0.46f, ActivityKind.TurnLens, RelicWhere.Spot, Prop.Lens, faces: true, line: 34);
            B(d, "watch", RelicCast.Owner, 0.46f, 1f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "a queue forms", RelicCast.Queue, 0f, 0.46f, ActivityKind.Watch, RelicWhere.Spot);
            B(d, "turns", RelicCast.Queue, 0.46f, 0.94f, ActivityKind.LookThroughLens, RelicWhere.Spot, Prop.Lens);
            B(d, "cheer", RelicCast.Queue, 0.94f, 1f, ActivityKind.Cheer, RelicWhere.Spot);
            return d;
        }
    }
}
