using System;

namespace AquariumCiv.Life
{
    /// <summary>What a person is doing (docs/design/life-in-the-box.md §4–§8). Each kind is one looping pose at a place,
    /// or a move between two places (Stardew's schedule of places with one pose at each).</summary>
    public enum ActivityKind
    {
        None,
        // rest and home
        Sleep, Nap, SitOut, Stretch, Eat, Toddle,
        // home chores (a missing job site falls back to one of these, rule 2)
        SweepStep, HangWashing, ChopKindling, MindBabies,
        // work loops at a site
        Gather, Stalk, Fish, Chop, Saw, StackWood, Knap, Cook, Hoe, Sow, Weed, Harvest, Repair, Herd, TendHives,
        ThrowPot, Brew, CutStone, Hammer, Weave, Knead, MindStall, Serve, Sweep, Trim, MillWork, Prepare,
        // carrying (moves with a load)
        Carry, FetchWater, RollBarrel,
        // moving empty-handed
        Walk,
        // role beats (notable-roles §2, life-in-the-box §4)
        WalkRound, Speech, HoldCourt, Inspect, Direct, Haggle, LightLanterns, NightRound, RingBell, Bless, SitWithOld,
        LookAtGlass, PacePlans, OpenSails, CarryTables, Meeting, Mediate, Service,
        // social and free time
        Chat, Argue, Court, SitTogether, Listen, Sing, Drink, StoryTell, ListenStory, Play, Swim, Snowball, Shop,
        WatchWork, Visit, Mourn, Dance, Cheer, Procession, Search, Explore, Shelter,
        // reactions to the Keeper (≤ 4 s)
        Wave, LookUp, LookUpWary, Clap, Point, RunOver, JumpAndPoint, HandsOut, Splash, Study,
        // at the glass, by belief stage (#92, relics-and-glass-voice.md §3; appended so the older values keep their numbers)
        Gaze, GazeWary, PressHands, WaveWary, Flinch, Peek, CreepUp, HopBack, LaughPoint, Measure, TapBack, WriteDown,
        // the relic moments (§1): staged by RelicMoments
        TurnLens, LookThroughLens, Queue, Freeze, StepBack, Watch, Poke, Taste, Deliver, SetUp, Bow, CopyPose, PickUp, HoldUp,
        Give, PutOn, LopsidedDance,
        /// <summary>#200: at home with the door shut, awake: nothing to draw. Appended so the older values keep their numbers.</summary>
        Indoors,
        /// <summary>P8 #266: stamping about in a mist puddle (a kid), and kneeling at one to wash hands or cup the water. Appended so the older values keep their numbers.</summary>
        Stomp, WashHands,
        /// <summary>#361: at the stash pile, tipping the carried basket onto it (a short stay after the carry). Appended so the older values keep their numbers.</summary>
        TipLoad,
    }

    /// <summary>Broad groups, for the busy share (rule 4) and the job report.</summary>
    public enum ActivityGroup { Rest, Chore, Work, Carry, Walk, Role, Social, Play, Reaction }

    /// <summary>The one big prop (Choice 5: 1.5–2× real size). The view picks the mesh.</summary>
    public enum Prop
    {
        None, Basket, Spear, Hide, Rod, FishStick, Axe, Log, Saw, Hammerstone, Hoe, SeedBag, Scythe, Sheaf, Crook, Lamb,
        Hive, Pots, Barrel, Mug, Pick, Block, Hammer, Cloth, Loaves, Goods, Broom, Handcart, Shears, Sack, Lantern, Bell,
        Flower, Ball, Stick, Buckets, Table, MeasuringRod, CoinBag, Plans, Stool, Honey, Wool, Notebook,
        // the relics (#92): a chunk of the Sky Crumb, the Festival Mask, the Keeper Doll, the Left Sandal, the Lens's tripod
        // (carried folded) and the Lens itself, the scholar's slate
        Crumb, Mask, Doll, Sandal, Tripod, Lens, Slate,
        /// <summary>#152: the bucket on the way to the water; <see cref="Buckets"/> is the full one, from the draw on.</summary>
        EmptyBucket,
        /// <summary>#283: a caught rabbit, carried over the shoulder to the fire. Appended last so the saved ints stay.</summary>
        Rabbit,
        /// <summary>P9 #426 (B13): the hunter's bow (on the back while walking, in the hand to shoot) and the carrying pole. Appended last.</summary>
        Bow, Pole,
    }

    /// <summary>#283: which beat of a hunt a <see cref="PersonActivity"/> is (None = not a hunt). The hunter's beats come in this order:
    /// ToFire, TakeSpear, WalkOut, Stalk, Strike, then Bolt + WalkBack (a miss) or CarryBack, Skin, Cook (a catch), then Return.
    /// A kid who follows plays KidFollow, KidWatch, KidCheer (a catch only), KidBack.</summary>
    public enum HuntPhase : byte
    {
        None, ToFire, TakeSpear, WalkOut, Stalk, Strike, Bolt, WalkBack, CarryBack, Skin, Cook, Return,
        KidFollow, KidWatch, KidCheer, KidBack,
        // #362 (added at the end): the thrown spear's aftermath. A miss: FetchSpear, PullSpear (then WalkBack); a catch: ToCatch, PickUp, HoldUp (then CarryBack).
        // The kid of a catch: KidRun to the hunter, KidEscort home beside him.
        FetchSpear, PullSpear, ToCatch, PickUp, HoldUp, KidRun, KidEscort,
        // P9 #426 (B13, appended): the deer hunt with bows. All: ToFire, (DeerWait), DeerTakeBow, DeerWalkOut in a line, DeerCrouch (the lead
        // points), DeerStalk side by side, then the shooter's DeerShoot while the others DeerWatchShot. A hit: DeerToKill, the shooter's
        // DeerThank, the runner's DeerPoleRun / DeerTakePole / DeerPoleBack, DeerToTie, DeerTie, DeerCarry (the two carriers; the third
        // DeerAhead with the bows), DeerSetDown. A miss: DeerShrug, the shooter's DeerToArrow / DeerPullArrow. Then Return.
        DeerTakeBow, DeerWalkOut, DeerWait, DeerCrouch, DeerStalk, DeerShoot, DeerWatchShot, DeerShrug, DeerToArrow, DeerPullArrow,
        DeerToKill, DeerThank, DeerPoleRun, DeerTakePole, DeerPoleBack, DeerToTie, DeerTie, DeerCarry, DeerAhead, DeerSetDown,
    }

    /// <summary>P8 (#267): the water in a bucket. None = not a bucket; Muddy = scooped from the wet low ground while the pond is dry.</summary>
    public enum WaterKind { None, Clean, Muddy }

    /// <summary>Where the prop sits on a peg doll (§9 "Carry").</summary>
    public enum PropSlot { None, Hand, Head, Shoulder, Back, Arms, Pulled, Rolled }

    /// <summary>Abstract places (positions arrive with M2's placement layer, see <see cref="IPlacePositions"/>).
    /// Building places carry the sim building id; land places are features the view places.</summary>
    public enum PlaceKind
    {
        None,
        // buildings (Place.buildingId is a sim Building.id)
        Home, Fire, Green, Market, Field, LumberMill, Windmill, Tavern, Shrine, Workshop, Guildhall, BuildingSite,
        // land and placeholders (no building yet; the view places them)
        Pond, Stream, Woods, ForestEdge, Bushes, Pasture, Meadow, ClayBank, Outcrop, Smithy, Kiln, Hill, Remembering,
        Midden, Nudge, Road,
        // #92: the front glass (index = the wall: 0 = -z, 1 = +x, 2 = +z, 3 = -x; LifePlanner.GlassSide) and a relic's spot
        // (index = its place in RelicId.All)
        Glass, Relic,
        /// <summary>The bakery (#112); a building place like the rest. Appended last so the saved ints stay.</summary>
        Bakery,
        /// <summary>#150: the camp's stash by the fire (land; index -1 = where a carrier stands, 1 = the pile, see <see cref="CampStash"/>).</summary>
        Stash,
        /// <summary>P8 #288: one grown bush on a living land (index = the site cell, <see cref="Sim.Settle.SiteMap"/>); where berries are picked.</summary>
        BushCell,
        /// <summary>P8 #266: a mist puddle (index = the site cell).</summary>
        Puddle,
        /// <summary>#371: a thing a choice left in the box (index = <see cref="Sim.ConsequenceProp.id"/>); where a callback's people go. Appended last so the saved ints stay.</summary>
        Prop,
        /// <summary>#404: the lookout tower the people apart gave the Keeper (its foot; <see cref="Sim.SimCore.TowerGroup"/>). Appended last so the saved ints stay.</summary>
        Tower,
        /// <summary>P9 #424: a spot by placed life where people stand and look (index = site cell × 8 + slot; slot 0 is 0.4 u from the
        /// cell toward the fire, slots 1–7 on a ring at 0.6 u on the fire's side; a water cell's spots are on its bank). Appended last.</summary>
        Arrival,
    }

    /// <summary>Where a person is. <see cref="buildingId"/> is the sim building (−1 for land); <see cref="index"/> is the
    /// family for a home, the nudge for a shaping (−1 otherwise). default(Place) is None.</summary>
    public readonly struct Place : IEquatable<Place>
    {
        public readonly PlaceKind kind;
        readonly int building1, index1; // stored +1 so default(Place) means "none"

        public Place(PlaceKind kind, int buildingId = -1, int index = -1) { this.kind = kind; building1 = buildingId + 1; index1 = index + 1; }

        public int buildingId => building1 - 1;
        public int index => index1 - 1;
        public static Place Land(PlaceKind kind, int index = -1) => new Place(kind, -1, index);
        public static readonly Place None = default;
        public bool IsBuilding => building1 > 0;
        public bool IsNone => kind == PlaceKind.None;

        public bool Equals(Place o) => kind == o.kind && building1 == o.building1 && index1 == o.index1;
        public override bool Equals(object obj) => obj is Place p && Equals(p);
        public override int GetHashCode() => ((int)kind * 397 ^ building1) * 397 ^ index1;
        public static bool operator ==(Place a, Place b) => a.Equals(b);
        public static bool operator !=(Place a, Place b) => !a.Equals(b);
        public override string ToString() => IsBuilding ? $"{kind}#{buildingId}" : index >= 0 ? $"{kind}[{index}]" : kind.ToString();
    }

    /// <summary>A person in the box: a notable by id, or a crowd member by stable index (rule 13). default(PersonRef) is None.</summary>
    public readonly struct PersonRef : IEquatable<PersonRef>
    {
        readonly int notable1, crowd1; // stored +1 so default(PersonRef) means "nobody"

        PersonRef(int notableId, int crowdIndex) { notable1 = notableId + 1; crowd1 = crowdIndex + 1; }

        public static PersonRef Notable(int id) => new PersonRef(id, -1);
        public static PersonRef Crowd(int index) => new PersonRef(-1, index);
        public static readonly PersonRef None = default;

        /// <summary>The notable's id, or −1 for the crowd.</summary>
        public int notableId => notable1 - 1;
        /// <summary>The crowd index, or −1 for notables.</summary>
        public int crowdIndex => crowd1 - 1;
        public bool IsNotable => notable1 > 0;
        public bool IsCrowd => crowd1 > 0;
        public bool IsNone => notable1 == 0 && crowd1 == 0;
        /// <summary>One int per person: notable ids are ≥ 1, crowd indices map to −1, −2, ...</summary>
        public int Key => IsNotable ? notableId : IsCrowd ? -crowd1 : 0;

        public bool Equals(PersonRef o) => notable1 == o.notable1 && crowd1 == o.crowd1;
        public override bool Equals(object obj) => obj is PersonRef p && Equals(p);
        public override int GetHashCode() => Key;
        public static bool operator ==(PersonRef a, PersonRef b) => a.Equals(b);
        public static bool operator !=(PersonRef a, PersonRef b) => !a.Equals(b);
        public override string ToString() => IsNotable ? $"notable {notableId}" : IsCrowd ? $"crowd {crowdIndex}" : "nobody";
    }

    /// <summary>Why a person is doing it (rule 10: reaction > story scene > role beat > schedule).</summary>
    public enum ActivitySource { Schedule, RoleBeat, Scene, Reaction }

    /// <summary>The Growth Lab's wear-pass trip types (rule 3), so the paths worn are the paths walked.</summary>
    public enum TripType { None, HomeToGreen, HomeToWork, HomeToMarket, Carry, Errand, Home }

    /// <summary>What the Keeper did (life-in-the-box §8; decisions 2026-10-29: hold a finger and tap the glass).</summary>
    public enum KeeperGesture { Return, Wipe, FullClean, FlickTrash, Prune, Rain, Shape, HoldFinger, TapGlass, TapPerson, Glance, WishAnswered, Puddle,
        /// <summary>P8 #282: the first rabbit hops in; the one person it names (the target) stops and points at it.</summary>
        Sighting,
        /// <summary>P8 #279: water broke the surface in the hollow; the people near come over to look at it.</summary>
        Marvel,
        /// <summary>P9 #424 (B12.0): the Keeper placed life. The one person it names (the target) goes to the arrival's spot (the
        /// reaction's place, a <see cref="PlaceKind.Arrival"/>): a kid runs over and cheers, a grown-up walks over and watches.</summary>
        Arrival,
        /// <summary>P9 #424 (B12.f): the wolves came. The target turns and stands still where they are, watching; nobody walks over.</summary>
        ArrivalStill }

    /// <summary>Story scenes (§6) and the era's gathering. Started from this month's chronicle, by "Show me" (an echo), or by
    /// the debug panel.</summary>
    public enum SceneKind
    {
        None, Wedding, Birth, Festival, Mourning, RoleMoment, Reconcile, Lost, Friendship, Rivalry, Apprentice, EveningTalk,
        EraMoment, Argument, Chat,
        // #92: a relic's moment (RelicMoments), a gathering at the glass, a scholar tapping back
        Relic, GlassGathering, TapBack,
        // #85: the people's answer to a Keeper's shaping (LifePlanner.StartShapingAnswer)
        ShapingAnswer,
        // #142: the people's first reaction to a gift (LifePlanner.StartShapingReaction)
        ShapingReaction,
        // #224: the choice scene: the people wait on the Keeper's touch, then react to it (LifePlanner.StageChoice)
        Choice,
        // #370: a held ceremony staged for the Keeper (LifePlanner.StageHeldMoment)
        HeldMoment,
        // #367: a small everyday story the background-life director starts (a water run, a pick at a bush; Life/StoryDirector.Planner.cs)
        LifeStory,
    }

    /// <summary>Babies stay home, kids play, adults work, elders sit out and tell stories.</summary>
    public enum LifeStage { None, Baby, Kid, Adult, Elder }

    public enum Weather { Clear, KeeperRain, Storm, Snow, Fog, Rainbow }

    /// <summary>One person's activity at a view time: the planner's output.</summary>
    public struct PersonActivity
    {
        public PersonRef who;
        public ActivityKind kind;
        /// <summary>Where they are; for a move, where they are going.</summary>
        public Place place;
        /// <summary>A move's start (equals <see cref="place"/> when they stay put).</summary>
        public Place from;
        public bool moving;
        /// <summary>Who they're doing it with (friend, rival, partner, master, the lord for the heir), or None.</summary>
        public PersonRef partner;
        public Prop prop;
        /// <summary>P8 (#267): what the bucket holds, for the view to colour (Muddy while the pond is dry).</summary>
        public WaterKind water;
        /// <summary>View hours of this activity [start, end); a move goes from <see cref="from"/> to <see cref="place"/> over it.</summary>
        public float start, end;
        public float progress;
        /// <summary>Step of the work loop (kneel, pick, stand, move...): changes every ~quarter view hour.</summary>
        public int step;
        public ActivitySource source;
        public TripType trip;
        /// <summary>Work strokes and walks slow with the sim's pace penalty (rule 5, up to −25 %) and in fog.</summary>
        public float pace;
        /// <summary>The chronicle entry a story scene stages (−1 = none).</summary>
        public int chronicleId;
        /// <summary>An apprentice copies the master a beat late (§4).</summary>
        public bool copiesPartner;
        /// <summary>Rule 6: the slot at the place (0, 1, 2 ...), set by <see cref="LifePlanner.PlanAll"/>; at most one doll per slot.</summary>
        public int slot;
        /// <summary>#92: they face the front glass (a dance for the sky, a wave, sitting at the glass), not their partner or work.</summary>
        public bool facesGlass;
        /// <summary>#92: how many times (a scholar's <see cref="ActivityKind.TapBack"/> knocks as often as the Keeper tapped).</summary>
        public int count;
        /// <summary>#192: the baby this parent has on them now (None = nobody). Babies are not walkers: the view draws the
        /// baby on the carrier (<see cref="carryOnBack"/>: on their back, else in their arms).</summary>
        public PersonRef carrying;
        public bool carryOnBack;
        /// <summary>#197: a run-in on the way (a chat, a bicker, a wave): stopped on the route where PositionAt says, not at the place.</summary>
        public bool enRoute;
        /// <summary>#192: on a baby, who has them now (None = at home, out of sight).</summary>
        public PersonRef carriedBy;
        /// <summary>User 2026-11-06 "toddling is fine": the walking-age baby is on its own feet, toddling after the carer (on both the carer,
        /// with <see cref="carrying"/>, and the baby, with <see cref="carriedBy"/>), not in their arms (LifePlanner.Toddle.cs).</summary>
        public bool carryOnFoot;
        /// <summary>#283: the beat of a hunt this is (None = not a hunt). The step's coordinates come from
        /// <see cref="LifePlanner.HuntStepAt"/>.</summary>
        public HuntPhase hunt;
        /// <summary>#284: the beat of a tent raising this is (None = not one). See <see cref="LifePlanner.TentStepAt"/>.</summary>
        public TentPhase tent;

        public PropSlot PropSlot => ActivityInfo.SlotOf(prop, kind);
        public ActivityGroup Group => ActivityInfo.GroupOf(kind);
        public bool IsBusy => ActivityInfo.IsBusy(kind);
    }

    /// <summary>A move coming up in a person's day (for the view's path planning and the paths' wear).</summary>
    public struct Trip
    {
        public ActivityKind kind;
        public Place from, to;
        public float start, end;
        public Prop prop;
        /// <summary>P8 (#267): what the bucket holds, for the view to colour (Muddy while the pond is dry).</summary>
        public WaterKind water;
        public TripType type;
    }

    /// <summary>Static facts about activity kinds and props.</summary>
    public static class ActivityInfo
    {
        public static ActivityGroup GroupOf(ActivityKind k)
        {
            switch (k)
            {
                // #92 (appended after the reactions, so named here before the ranges below)
                case ActivityKind.PressHands:
                    return ActivityGroup.Play;
                case ActivityKind.Measure: case ActivityKind.SetUp: case ActivityKind.TurnLens:
                    return ActivityGroup.Role;
                case ActivityKind.Flinch: case ActivityKind.Peek: case ActivityKind.CreepUp: case ActivityKind.HopBack:
                case ActivityKind.LaughPoint: case ActivityKind.WaveWary: case ActivityKind.TapBack: case ActivityKind.WriteDown:
                case ActivityKind.Freeze: case ActivityKind.StepBack:
                    return ActivityGroup.Reaction;
                case ActivityKind.Gaze: case ActivityKind.GazeWary: case ActivityKind.LookThroughLens: case ActivityKind.Queue:
                case ActivityKind.Watch: case ActivityKind.Poke: case ActivityKind.Taste: case ActivityKind.Deliver: case ActivityKind.Bow:
                case ActivityKind.CopyPose: case ActivityKind.PickUp: case ActivityKind.HoldUp: case ActivityKind.Give: case ActivityKind.PutOn:
                case ActivityKind.LopsidedDance:
                    return ActivityGroup.Social;
                case ActivityKind.Sleep: case ActivityKind.Nap: case ActivityKind.SitOut: case ActivityKind.Stretch:
                case ActivityKind.Eat: case ActivityKind.Toddle: case ActivityKind.Indoors:
                    return ActivityGroup.Rest;
                case ActivityKind.SweepStep: case ActivityKind.HangWashing: case ActivityKind.ChopKindling: case ActivityKind.MindBabies:
                    return ActivityGroup.Chore;
                case ActivityKind.Carry: case ActivityKind.FetchWater: case ActivityKind.RollBarrel: case ActivityKind.TipLoad:
                    return ActivityGroup.Carry;
                case ActivityKind.Walk:
                    return ActivityGroup.Walk;
                case ActivityKind.Play: case ActivityKind.Swim: case ActivityKind.Snowball:
                    return ActivityGroup.Play;
            }
            if (k >= ActivityKind.Gather && k <= ActivityKind.Prepare) return ActivityGroup.Work;
            if (k >= ActivityKind.WalkRound && k <= ActivityKind.Service) return ActivityGroup.Role;
            if (k >= ActivityKind.Wave) return ActivityGroup.Reaction;
            return ActivityGroup.Social;
        }

        /// <summary>Rule 4: a work loop, a carry, a home chore or a role's duty.</summary>
        public static bool IsBusy(ActivityKind k)
        {
            var g = GroupOf(k);
            return g == ActivityGroup.Work || g == ActivityGroup.Carry || g == ActivityGroup.Chore || g == ActivityGroup.Role;
        }

        /// <summary>Does the person move from <c>from</c> to <c>place</c> during it?</summary>
        public static bool IsMove(ActivityKind k) =>
            k == ActivityKind.Walk || k == ActivityKind.Carry || k == ActivityKind.FetchWater || k == ActivityKind.RollBarrel ||
            k == ActivityKind.Procession || k == ActivityKind.WalkRound || k == ActivityKind.NightRound || k == ActivityKind.RunOver;

        /// <summary>Steps in the loop (the view's sub-poses); 1 = a single held pose.</summary>
        public static int LoopSteps(ActivityKind k) => k switch
        {
            ActivityKind.Gather or ActivityKind.Sow or ActivityKind.Weed or ActivityKind.Harvest or ActivityKind.Hoe => 4,
            ActivityKind.Stalk or ActivityKind.Fish or ActivityKind.ThrowPot or ActivityKind.Weave or ActivityKind.MindStall => 3,
            ActivityKind.Chop or ActivityKind.Saw or ActivityKind.Knap or ActivityKind.CutStone or ActivityKind.Hammer or
                ActivityKind.Sweep or ActivityKind.Trim or ActivityKind.Cook or ActivityKind.Knead or ActivityKind.Brew => 2,
            ActivityKind.StackWood or ActivityKind.MillWork or ActivityKind.Prepare or ActivityKind.Serve or ActivityKind.TendHives or
                ActivityKind.Herd or ActivityKind.Repair => 3,
            ActivityKind.Chat or ActivityKind.Argue or ActivityKind.StoryTell or ActivityKind.Speech or ActivityKind.Play => 2,
            ActivityKind.PressHands or ActivityKind.CopyPose or ActivityKind.LopsidedDance => 2,
            ActivityKind.Measure or ActivityKind.LookThroughLens => 3, // look, lean back startled, laugh (§1.5)
            _ => 1,
        };

        public static PropSlot SlotOf(Prop p, ActivityKind k)
        {
            if (p == Prop.None) return PropSlot.None;
            bool carrying = k == ActivityKind.Carry || k == ActivityKind.FetchWater;
            return p switch
            {
                Prop.Sheaf or Prop.Loaves or Prop.Pots => carrying ? PropSlot.Head : PropSlot.Hand,
                Prop.Log or Prop.Sack or Prop.Hide or Prop.Rabbit or Prop.Buckets or Prop.EmptyBucket or Prop.Table or Prop.Tripod => PropSlot.Shoulder,
                Prop.Mask => PropSlot.Head,
                Prop.Wool or Prop.Cloth or Prop.Goods => carrying ? PropSlot.Back : PropSlot.Hand,
                Prop.Lamb or Prop.Basket or Prop.Honey or Prop.Crumb or Prop.Doll => PropSlot.Arms,
                Prop.Handcart => PropSlot.Pulled,
                Prop.Barrel => PropSlot.Rolled,
                Prop.Bow => k == ActivityKind.Walk ? PropSlot.Back : PropSlot.Hand, // #426: bows on their backs on the way out
                _ => PropSlot.Hand,
            };
        }
    }
}
