// #692 people and things (docs/design/animation-specs.md "Batch 3", D rules and parts 1-5): a camp use is a short scene with a cast
// borrowed from their free time. Each member has a timeline of parts (walk to a spot, hold a pose, hold a prop); the scene also
// carries its effects (sparks, steam, droplets) at set times, and the small things it draws (an ember on a twig's tip, a berry).
// Pure: Mathf, Vector2/3, DollPose and the beat kit, so the timelines can be tested in EditMode and AnimBench could compile it.
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>#692: the uses a <see cref="CampScene"/> plays (one per spec row; see the B numbers).</summary>
    public enum CampUse
    {
        None,
        /// <summary>B29.a: a sitter at the fire warms the hands in winter.</summary>
        WarmHands,
        /// <summary>B29.b: a kid pokes a twig in the fire, a grown-up takes it.</summary>
        KidPoke,
        /// <summary>B29.c: a berry on a stick over the fire.</summary>
        BerryStick,
        /// <summary>B29.d: meat on a spit after a hunt (the set-up, the turning, the round of pieces).</summary>
        Spit,
        /// <summary>B30.a: the pot filled and lifted onto the fire.</summary>
        PotFill,
        /// <summary>B30.e: after supper the pot is scraped and lifted off; a kid stacks the bowls.</summary>
        PotOff,
        /// <summary>B30.b: the bowl-giver's dips at the pot between hand-overs.</summary>
        Ladle,
        /// <summary>B30.c: kids peek in the simmering pot.</summary>
        PotPeek,
        /// <summary>B30.d: a grown-up passing the pot sniffs and nods.</summary>
        PotSniff,
        /// <summary>B31.a: a new feather tied on the pole after a good event.</summary>
        FeatherTie,
        /// <summary>B31.b: kids dare each other to touch the pole.</summary>
        PoleDare,
        // #692 part 2: the Keeper's tree visits (View/Beats/CampScenes2.cs, View/CampLife.Tree.cs)
        /// <summary>B32.a: sit under it, back to the trunk, dozing.</summary>
        TreeSit,
        /// <summary>B32.b: a hand on the bark in passing (a kid slaps it).</summary>
        TreeTouch,
        /// <summary>B32.c: kids circle it, hands joined.</summary>
        TreeCircle,
        /// <summary>B32.d: the quiet visit after a death.</summary>
        TreeQuiet,
        /// <summary>B32.e: the newlyweds walk to the tree.</summary>
        TreeWed,
        /// <summary>B32.f: a parent brings the newborn.</summary>
        TreeNewborn,
        /// <summary>B32.g: the old one's afternoons there.</summary>
        TreeOld,
        /// <summary>B32.h: the eldest tells the kids who planted it.</summary>
        TreeTell,
        // #692 part 3: grass, saplings, rocks, fallen branches (View/Beats/CampScenes3.cs, View/CampLife.Land.cs)
        /// <summary>B33.a: kids roll in the grass.</summary>
        GrassRoll,
        /// <summary>B33.b: two lie back in the grass and look up.</summary>
        LieBack,
        /// <summary>B33.c: pick flowers and give the bunch away.</summary>
        PickFlowers,
        /// <summary>B33.d: a kid blows a grass whistle.</summary>
        GrassWhistle,
        /// <summary>B34.a: water a sapling (work).</summary>
        WaterSapling,
        /// <summary>B34.b: ring a sapling with stones (work, once per sapling).</summary>
        RingSapling,
        /// <summary>B34.c: a kid plants a twig beside it.</summary>
        PlantTwig,
        /// <summary>B35.a: sit on a rock; a kid climbs on, king of the rock.</summary>
        SitRock,
        /// <summary>B35.b: stack pebbles.</summary>
        StackPebbles,
        /// <summary>B36.a: kids sword fight with fallen sticks.</summary>
        StickFight,
        /// <summary>B36.b: a kid rides a stick horse.</summary>
        StickHorse,
        // #692 part 4: animals (View/Beats/CampScenes4.cs, View/CampLife.Animals.cs)
        /// <summary>B37: kids catch a frog and let it go (or miss and flop).</summary>
        FrogCatch,
        /// <summary>B38.a: watch the deer grazing from the wood's edge.</summary>
        DeerWatch,
        /// <summary>B38.b: kids creep up on the deer and the deer bolt.</summary>
        DeerCreep,
        /// <summary>B39.a: a kid holds a rabbit kit.</summary>
        KitHold,
        /// <summary>B15.d (B39.b's rate check): kids feed the ducks.</summary>
        FeedDucks,
        // #692 part 5: the rest of the camp (View/Beats/CampScenes5.cs, View/CampLife.Camp.cs)
        /// <summary>B40.a: a kid's head pops out of the tent door, looks round, ducks back, then runs out.</summary>
        TentPeek,
        /// <summary>B40.b: a toy or a string of beads tied on the tent's pole.</summary>
        TentToy,
        /// <summary>B40.c: sitting in the doorway at dusk, a kid against them.</summary>
        TentDoorSit,
        /// <summary>B41.a: two kids count the stash's baskets.</summary>
        CountStash,
        /// <summary>B41.b: a snack sneaked from the stash, and caught.</summary>
        SnackSneak,
        /// <summary>B42.a: tending the racks (work): each hanging thing checked, the smoke fanned, the hide tugged taut.</summary>
        TendRacks,
        /// <summary>B42.b: kids in a chase duck under the hide rack.</summary>
        DuckUnderHides,
        /// <summary>B42.c: two kids count the fish on the rack.</summary>
        CountFish,
        /// <summary>B42.d: a kid sneaks a fish off the rack and shares it.</summary>
        SneakFish,
        /// <summary>B43.b: kids lie at the thicket's edge and peek at the rabbits.</summary>
        ThicketPeek,
        /// <summary>B43.c: a kid eats berries (a purple mouth); a parent wipes it off.</summary>
        BerryWipe,
        /// <summary>B44.a: a kid climbs onto a wild tree's low branch.</summary>
        ClimbTree,
        /// <summary>B44.b: a nap in a wild tree's shade (B32.a's sit).</summary>
        TreeNap,
        /// <summary>B44.c: a chat, one leaning on a wild tree's trunk.</summary>
        TrunkLean,
        /// <summary>B45.a: skip stones on the pond.</summary>
        SkipStones,
        /// <summary>B45.b: a kid floats a leaf boat.</summary>
        LeafBoat,
        /// <summary>B45.c: grown-ups sit on the bank, feet in the water.</summary>
        FeetInWater,
        /// <summary>B46.a: kids look at their reflection in a puddle.</summary>
        PuddleLook,
        /// <summary>B46.b: kids race from puddle to puddle, stomping.</summary>
        PuddleRace,
        /// <summary>B47.a: cut reeds to weave (work).</summary>
        CutReeds,
        /// <summary>B47.b: a kid blows a reed whistle.</summary>
        ReedWhistle,
        /// <summary>B48.a: kids follow the stream bed.</summary>
        FollowStream,
        /// <summary>B48.b: two kids build a little dam in the stream bed.</summary>
        BuildDam,
        /// <summary>B49.a: grown-ups sit on the hill at dusk.</summary>
        HillDusk,
        /// <summary>B49.b: kids roll down the hill.</summary>
        HillRoll,
        /// <summary>B50.a: kids race (or hop) along a path.</summary>
        PathRace,
        /// <summary>B50.b: two walkers meet on a path and chat.</summary>
        PathChat,
        /// <summary>B51: a small gift left at the remembering stone.</summary>
        StoneGift,
        /// <summary>B52.a: the tents' lanterns lit one after another at dusk (work).</summary>
        LightLanterns,
        /// <summary>B52.b: kids chase the moths round a lit lantern.</summary>
        ChaseMoths,
        /// <summary>B53.a: a weed pulled out of the camp (work).</summary>
        PullWeed,
        /// <summary>B53.c: a kid makes a crown of weeds and flowers for another.</summary>
        Crown,
        /// <summary>B53.d: the rope swing hung on a wild tree (once).</summary>
        HangSwing,
        /// <summary>B53.d: kids on the rope swing.</summary>
        Swing,
        // #693: the choice props, a work use and a life use each (View/Beats/CampScenes6.cs, View/CampLife.Props.cs). Keep these last.
        /// <summary>B54.a: a gatherer takes a basket off the stack and brings it back full.</summary>
        BasketTake,
        /// <summary>B54.c: a log stacked under the woodpile's roof (or one fetched for the fire on a wet day).</summary>
        WoodStack,
        /// <summary>B54.d: people squeeze under the woodpile's roof when it rains (kids sit on the logs on a dry day).</summary>
        WoodShelter,
        /// <summary>B54.e: a new name is cut in the name stone.</summary>
        NameCut,
        /// <summary>B54.f: kids trace the names and polish the stone with their sleeves.</summary>
        NameTrace,
        /// <summary>B54.g: a kid jumps and flicks a charm so it spins.</summary>
        CharmFlick,
        /// <summary>B54.h: the together supper ladled at the big stew pot.</summary>
        BigPot,
        /// <summary>B54.i: kids drum on the big pot with sticks.</summary>
        PotDrum,
        /// <summary>B54.j: wood split on the axe stump.</summary>
        StumpSplit,
        /// <summary>B54.k: kids sit on the stump beside the axe.</summary>
        StumpSit,
        /// <summary>B54.l: the headman takes up the staff for the walk round and puts it back.</summary>
        StaffWalk,
        /// <summary>B54.m: kids play headman with the staff.</summary>
        StaffPlay,
        /// <summary>B54.n: flour sacks carried from the mill to a door.</summary>
        FlourCarry,
        /// <summary>B54.o: the flour fight.</summary>
        FlourFight,
        /// <summary>B54.p: a roofer straightens the carved bird after a storm.</summary>
        BirdStraighten,
        /// <summary>B54.q: kids fly round under the carved bird.</summary>
        BirdFly,
        /// <summary>B54.r: the shared meal at the long table.</summary>
        TableMeal,
        /// <summary>B54.s: kids crawl under the long table; an old one naps on the bench.</summary>
        TableUnder,
        /// <summary>B54.t: the offering basket filled with the best food.</summary>
        OfferingFill,
        /// <summary>B54.u: kids wait by the offering, watching the glass.</summary>
        OfferingWait,
        /// <summary>B54.x: the fish from the crate carried to the rack.</summary>
        CrateFish,
        /// <summary>B54.y: kids on the crate wave across the pond.</summary>
        CrateWave,
        /// <summary>B54.z (#693 notes): the name re-carved on the carved name post.</summary>
        PostCarve,
        /// <summary>B54.zz (#693 notes): a kid's height scratched on the name post.</summary>
        PostMeasure,
        // 2026-11-16 "Bring a pen back": the pond family's goat pen (View/CampLife.Goat.cs)
        /// <summary>B54.v: the gnawed rail mended.</summary>
        PenMend,
        /// <summary>B54.w: kids sit on the rails; one crawls through the gnawed gap and waves.</summary>
        PenRails,
        /// <summary>B58.d at the pen: a kid pets the goat over the rail; it butts gently.</summary>
        PenPet,
        // #694 dogs and wolves in the people's day (View/Beats/DogScenes.cs, View/CampLife.Dogs.cs)
        /// <summary>B24.b: the pup chews a hide by its keeper.</summary>
        PupChew,
        /// <summary>B24.b: the pup play-bows to the kids; a kid bows back.</summary>
        PupPlay,
        /// <summary>B24.b: the pup sleeps curled by the embers.</summary>
        PupSleep,
        /// <summary>B24.b: the pup steals a fish from the stash and runs; a grown-up chases and gives up.</summary>
        PupSteal,
        /// <summary>B24.b: the pup is shooed off the meat (it growls, head low).</summary>
        PupShoo,
        /// <summary>B24.b: the pup runs along in the kids' race.</summary>
        PupRunAlong,
        /// <summary>B25.a + B56.a: the hunter whistles the dog to heel, it flushes the rabbits, the pat after the catch.</summary>
        DogHunt,
        /// <summary>B25.b + B56.b: the dog barks at a wolf by night and it turns back; a grown-up scratches its head.</summary>
        DogGuard,
        /// <summary>B56.c: a kid throws a stick, the dog fetches it.</summary>
        DogFetch,
        /// <summary>B56.d: a small kid rides a big dog.</summary>
        DogRide,
        /// <summary>B56.e: the dog asleep at the fire ring's edge; a kid lies with the head on it.</summary>
        DogFire,
        /// <summary>B25: the grown dog lies in the sun by its keeper's tent.</summary>
        DogSun,
        /// <summary>B55.a: the grown-ups drive a wolf off with shouts and a torch.</summary>
        WolfDriveOff,
        /// <summary>B55.b: grown-ups hold the kids close while a wolf is in view.</summary>
        KidsClose,
        // #695 part 1: the village and the town (View/Beats/VillageScenes.cs, View/CampLife.Village.cs). Keep these last.
        /// <summary>B57.a: kids play tag along the furrows; the farmer waves them out.</summary>
        FieldTag,
        /// <summary>B57.b: a scarecrow made together after sowing.</summary>
        Scarecrow,
        /// <summary>B57.c: the harvest dance round the last sheaf.</summary>
        HarvestDance,
        /// <summary>B57.d: a home mended and dressed (work).</summary>
        HomeMend,
        /// <summary>B57.e: gossip at the well.</summary>
        WellGossip,
        /// <summary>B57.f: a kid drops a pebble down the well and listens.</summary>
        WellPebble,
        /// <summary>B57.g: kids watch the windmill's sails.</summary>
        SailWatch,
        /// <summary>B57.h: kids race the sails' shadow round the mill.</summary>
        SailShadow,
        /// <summary>B57.i: a break on the lumber mill's log pile.</summary>
        LogPileSit,
        /// <summary>B57.j: a kid balances along a lying log.</summary>
        LogBalance,
        /// <summary>B58.c: kids wait at the bakery door for a crust.</summary>
        CrustWait,
        /// <summary>B58.f: kids play hopscotch in a street.</summary>
        Hopscotch,
        /// <summary>B58.g: two who meet in a street stop to talk.</summary>
        StreetChat,
        /// <summary>B58.j: a leaning fence post set straight and tapped in (work).</summary>
        FenceFix,
        /// <summary>B58.k: kids climb and sit on a field's fence, a grown-up leans on it.</summary>
        FenceLean,
        /// <summary>B59.k: barn chores: hay pitched from the heap into the barn, a bucket set down for the animals (work).</summary>
        BarnChores,
        /// <summary>B59.l: kids jump in the hay heap by the barn.</summary>
        HayJump,
        // S-ritual (View/Beats/RitualScenes.cs, View/CampLife.Rituals.cs)
        /// <summary>B75.b: a kid finds the offering basket empty in spring and runs to the fire with it held up.</summary>
        OfferingEmpty,
        /// <summary>B77.a: a fisher sorts the catch sitting on the upside-down boat (work).</summary>
        BoatSort,
        /// <summary>B77.b: the kids' house round the boat: a knock, a head pops up, they sit on it (life).</summary>
        BoatHouse,
        /// <summary>#687 (the thirst signs): in a dry spell a grown-up stops, looks up at the glass for rain and shakes their head (life).</summary>
        SkyLook,
        // #722 the story events (View/Beats/StoryScenes.cs, View/CampLife.Stories.cs). Appended: keep these last.
        /// <summary>B78.a: the Keeper's tree's first fruit: a kid picks it and runs it to the fire, everyone has a bite.</summary>
        FirstFruit,
        /// <summary>B78.b: a newly-wed couple cut their two marks into the Keeper's tree.</summary>
        TwoMarks,
        /// <summary>B78.c: the second name stone rolled in, stood up beside the first, the first name cut.</summary>
        SecondStone,
        /// <summary>#722 (1.1 "Read later"): in summer a kid picks a fruit from the Keeper's tree and eats it (life).</summary>
        FruitPick,
    }

    /// <summary>#694: what a dog or a visiting wolf does in a <see cref="QuadPart"/> (P0's moves; CampLife.Dogs poses them).</summary>
    public enum QuadAct { Stand, Sit, Lie, Sleep, PlayBow, Sniff, Bark, Alert, Chew, Growl, Wag, HeadDown }

    /// <summary>#694: what a dog carries in its mouth for a part.</summary>
    public enum QuadHeld { None, Stick, Fish, Hide }

    /// <summary>#694: one stretch of a scene's dog (<see cref="actor"/> 0) or visiting wolf (1): it goes to <see cref="pos"/> at
    /// <see cref="speed"/> (the gait from the speed), then does <see cref="act"/> facing <see cref="face"/>.</summary>
    public sealed class QuadPart
    {
        public int actor;
        public float from, to;
        public Vector2 pos, face;
        public QuadAct act;
        public float speed = 0.3f;
        public QuadHeld held;
        /// <summary>Off the ground (a pup carried in the arms; a kid's weight is not one).</summary>
        public float lift;
        public float Len => to - from;
    }

    /// <summary>A pose laid over the doll's pose for one part: <paramref name="into"/> seconds into the part, <paramref name="len"/> its length.</summary>
    public delegate void CampPoseFn(ref DollPose p, float into, float len);

    /// <summary>One stretch of one member's timeline (scene seconds).</summary>
    public sealed class CampPart
    {
        public float from, to;
        /// <summary>Where they stand (box x, z) and what they face. With <see cref="keepSpot"/> the plan's own spot and facing stay.</summary>
        public Vector2 pos, face;
        public bool keepSpot;
        /// <summary>The move drawn while they stand still (walking: the walk, or a run with <see cref="run"/>).</summary>
        public DollStyle style = DollStyle.Stand;
        public PropKind prop;
        public PropSlot slot = PropSlot.Hand;
        /// <summary>Their pace toward <see cref="pos"/> (box units a second).</summary>
        public float speed = 0.3f;
        public bool run;
        /// <summary>The pose over the move (null: the move as it is).</summary>
        public CampPoseFn pose;
        /// <summary>Lay the pose even while walking (a heavy carry, a slow bent walk).</summary>
        public bool poseWalking;
        /// <summary>#694 (B56.d): they are carried along (a kid on a dog's back): moving keeps <see cref="style"/>, no walking legs.</summary>
        public bool carried;
        /// <summary>B30.b (the user's review): this part is a dip at the pot; seconds into the dip at the part's start (−1: not a dip).
        /// CampLife puts the pot's ladle in the right hand while the dip says so.</summary>
        public float dipFrom = -1f;
        public float Len => to - from;
    }

    /// <summary>What an effect is.</summary>
    public enum CampFxKind { Sparks, Flare, Steam, Dust, Droplets, Ring, Grass, Chime, Jolt,
        /// <summary>The user's review of part 1: the pot's own soft steam swells for a moment (several white wisps rising and fading), not a puff disc.</summary>
        PotSteam,
        /// <summary>#692 part 2: a leaf or two falling (HitFx leaves).</summary>
        Leaves,
        /// <summary>#692 part 2: the Keeper's tree's crown shakes (a pat, a slap, a touch; HitFx's baked-tree shake).</summary>
        TreeShake,
        /// <summary>#692 part 2 (B32.d): the tree's dusk glow comes up a little for 4 s.</summary>
        Glow,
        /// <summary>#692 part 3: wood chips (a stick snapped to size, B36.a).</summary>
        Chips,
        /// <summary>#692 part 3 (B33.d): the grass whistle works: two short white lines at the hands.</summary>
        Whistle,
        /// <summary>#692 part 3: a mark is made on the land at <see cref="CampFx.at"/> (what kind: <see cref="CampFx.count"/>, a <see cref="CampMark"/>).</summary>
        Mark,
        /// <summary>#692 part 3 (B34.a): the little tree perks up (the baked-tree shake at the sapling).</summary>
        Perk,
        /// <summary>#692 part 3 (B36.a): the sticks cross: a small white flash and a light wood knock.</summary>
        Clash,
        // #692 part 5 (View/CampLife.Camp.cs)
        /// <summary>A ring (or droplets) on the pond's surface at <see cref="CampFx.at"/> (x, z; y above the water).</summary>
        WaterRing, WaterDrops,
        /// <summary>A part 5 mark on the camp at <see cref="CampFx.at"/> (what: <see cref="CampFx.count"/>, a <see cref="CampMark"/>); with
        /// <see cref="CampAnchor.Head"/> it is worn by the member (a crown, a purple mouth).</summary>
        Leave,
        /// <summary>B53.d: the bough's soft creak at an end of the swing's arc.</summary>
        Creak,
        /// <summary>B43: a bush at <see cref="CampFx.at"/> sways (HitFx's bush shake).</summary>
        BushSway,
        /// <summary>#693: something happens to the scene's choice prop (what: <see cref="CampFx.count"/>, a <see cref="PropFx"/>; a value in
        /// <see cref="CampFx.scale"/>). CampLife.Props handles it.</summary>
        Prop,
        /// <summary>#695: something happens to a village thing (what: <see cref="CampFx.count"/>, a <see cref="VillageFx"/>). CampLife.Village handles it.</summary>
        Village }

    /// <summary>#693: what a <see cref="CampFxKind.Prop"/> does to the scene's prop (or draws beside it).</summary>
    public enum PropFx { None,
        /// <summary>The prop jolts (a small wobble and squash).</summary>
        Jolt,
        /// <summary>Its look changes to variant <see cref="CampFx.scale"/> (baskets taken, the axe out, the staff up, a new name row, fish
        /// taken, the cloth lifted...: ConsequenceMeshes.Build's variant).</summary>
        Look,
        /// <summary>It tips back upright (the roof bird, 20 → 0 deg over 0.3 s).</summary>
        Upright,
        /// <summary>White flour on member <see cref="CampFx.member"/> until the evening wash.</summary>
        Flour,
        /// <summary>The name stone's new row is cut (the shown rows go up by one).</summary>
        NameRow,
        /// <summary>The offering basket is full today (B54.u waits by it).</summary>
        Offered,
        /// <summary>#693 retakes: the thing swings (15 deg, dying away over 1.2 s).</summary>
        Swing,
        /// <summary>#693 notes: a log laid on the woodpile (it is part of the pile from then on); one taken off.</summary>
        Stack, Unstack,
        /// <summary>#693 notes: the name post's mark freshly re-cut; a kid's height scratched on it.</summary>
        Recut, Measure }

    /// <summary>#692 part 3: what a <see cref="CampFxKind.Mark"/> leaves on the land (CampLife.Land draws them).</summary>
    public enum CampMark { None, WetPatch, RingStone, Twig, Pebble, Topple, Horse, GrassBits, EarFlower, Flatten,
        // #692 part 5 (CampFxKind.Leave; View/CampLife.Camp.cs)
        Toy, Beads, ToySpin, LanternLit, Crown, PurpleMouth, WipeMouth, Gift, DamPool, DamBreak, Boat, SkipSplash, SwingRope, SwingHung,
        WeedPulled, FishTaken, ReedsFlat, MothClap, StripTouch,
        /// <summary>S-ritual (B72): purple berry dots on both hands, worn until the next meal.</summary>
        PurpleHands }

    /// <summary>Where an effect is: on the ground at a box point (<see cref="CampFx.at"/> x, z; y above the ground), or at a member's hand,
    /// head or held twig's tip (plus <see cref="CampFx.at"/> as a world-up offset in box units).</summary>
    public enum CampAnchor { Ground, Hand, Head, TwigTip }

    public struct CampFx
    {
        public float time;
        public CampFxKind kind;
        public CampAnchor anchor;
        public int member;
        public Vector3 at;
        public float scale;
        public int count;
    }

    /// <summary>A small thing the scene draws at a member's hand or twig tip for a while (B29.b the ember, B29.c the berry, B31.a the feather).</summary>
    public enum CampTipKind { Ember, Berry, MeatPiece,
        /// <summary>#692 part 2: a leaf drifting down onto a shoulder (B32.d), a flower in the hand (B32.g), a charm to tie (B32.d).</summary>
        Leaf, Flower, Charm,
        /// <summary>#692 part 3: a stone in the hand (B34.b), three in the arms, a pebble (B35.b), a bunch of flowers (B33.c), a grass blade at the
        /// mouth (B33.d), the stick horse from the hand back to the ground (B36.b).</summary>
        Stone, Stones, Pebble, Bunch, Blade, Horse,
        /// <summary>#692 part 5 (drawn by View/CampLife.Camp.cs; keep these last): a flat skipping stone, an armful of reeds, one reed held like a
        /// flute, a weed with its dangling root, the crown being twisted, a small wooden toy, a string of beads, the coiled rope, the swing's plank,
        /// a folded leaf boat, a small bowl of berries for the stone.</summary>
        FlatStone, Reeds, Reed, Weed, CrownRing, Toy, Beads, Rope, Plank, LeafFold, GiftBowl }

    /// <summary>#692 part 4: a small thing thrown from a member's hand along an arc to a box point (B15.d's crumbs), drawn by CampLife.Animals.</summary>
    public struct CampArc
    {
        public int member;
        public float from, seconds, h;
        /// <summary>Where it lands (box x, z; y above the ground or the water).</summary>
        public Vector3 to;
    }

    public struct CampTip
    {
        public int member;
        public float from, to;
        public CampTipKind kind;
        /// <summary>How it changes over its life: grows from 1 to <see cref="grow"/>, and darkens toward <see cref="darken"/> (0..1).</summary>
        public float grow, darken;
        /// <summary>#722: drawn this many times its usual size all its life (0 = 1: as before; the first fruit is a berry drawn bigger).</summary>
        public float scale;
    }

    /// <summary>
    /// #692: one camp use playing now: its cast (people keys), each member's parts, its effects and its small drawn things. Times are
    /// scene seconds from <see cref="t0"/> (the PeopleView pose clock). Build it with <see cref="Add"/>, <see cref="Part"/> and <see cref="Fx"/>.
    /// </summary>
    public sealed class CampScene
    {
        public CampUse use;
        public float t0;
        /// <summary>Where the camera should look (AnimLab's "camp" follow, the debug readout).</summary>
        public Vector2 focus;
        /// <summary>The thing it is about (the fire, the pot, the pole), so one use per thing plays at a time.</summary>
        public int thing;
        public readonly List<int> keys = new();
        public readonly List<List<CampPart>> parts = new();
        public readonly List<CampFx> fx = new();
        public readonly List<CampTip> tips = new();
        /// <summary>#692 part 2: pair beats inside the scene (B32.d's hug), handed to PairStage when the scene is added (absolute times).</summary>
        public readonly List<PairEvent> pairs = new();
        /// <summary>#692 part 4: things thrown along an arc (B15.d's crumbs).</summary>
        public readonly List<CampArc> arcs = new();
        /// <summary>#694: the scene's dog and visiting wolf, part by part (<see cref="QuadPart.actor"/> 0 the dog <see cref="dog"/>, 1 the wolf).</summary>
        public readonly List<QuadPart> quads = new();
        /// <summary>#694: the sim dog the scene casts (−1 none).</summary>
        public int dog = -1;
        /// <summary>Extra state a use carries (the spit's meat, the pot's lift), read by the director.</summary>
        public float a, b;
        /// <summary>#692 part 4: more moments and spots a use hands its animal (the frog's wriggle, the kit's hops: B37, B39.a).</summary>
        public float c, d;
        public Vector2 p0, p1;
        public int kind;
        /// <summary>The camera keeps to <see cref="focus"/> (the thing), not pulled toward the lead actor (the feather pole: the user's review).</summary>
        public bool holdFocus;
        /// <summary>How far the camera may be pulled from <see cref="focus"/> toward the lead actor (B32.d follows the mourner back to the fire).</summary>
        public float followReach = 0.6f;

        public CampScene(CampUse use, float t0, Vector2 focus, int thing = 0) { this.use = use; this.t0 = t0; this.focus = focus; this.thing = thing; }

        public int Count => keys.Count;
        /// <summary>The scene's end (scene seconds): the last part's or effect's.</summary>
        public float Length
        {
            get
            {
                float e = 0f;
                foreach (var l in parts) foreach (var p in l) e = Mathf.Max(e, p.to);
                foreach (var f in fx) e = Mathf.Max(e, f.time);
                foreach (var r in arcs) e = Mathf.Max(e, r.from + r.seconds);
                foreach (var q in quads) e = Mathf.Max(e, q.to);
                return e;
            }
        }
        public float End => t0 + Length;

        public int Add(int key) { keys.Add(key); parts.Add(new List<CampPart>()); return keys.Count - 1; }
        public int IndexOf(int key) => keys.IndexOf(key);

        public CampPart Part(int member, float from, float to, Vector2 pos, Vector2 face, DollStyle style, CampPoseFn pose = null, PropKind prop = PropKind.None, float speed = 0.3f)
        {
            var p = new CampPart { from = from, to = to, pos = pos, face = face, style = style, pose = pose, prop = prop, speed = speed };
            parts[member].Add(p);
            return p;
        }

        /// <summary>A part that leaves them where the plan has them (a sitter's overlay).</summary>
        public CampPart Stay(int member, float from, float to, DollStyle style, CampPoseFn pose = null, PropKind prop = PropKind.None)
        {
            var p = new CampPart { from = from, to = to, keepSpot = true, style = style, pose = pose, prop = prop };
            parts[member].Add(p);
            return p;
        }

        public void Fx(float time, CampFxKind kind, Vector3 at, float scale = 1f, int count = 0, CampAnchor anchor = CampAnchor.Ground, int member = -1) =>
            fx.Add(new CampFx { time = time, kind = kind, at = at, scale = scale, count = count, anchor = anchor, member = member });

        /// <summary>#694: a part of the dog (actor 0) or the visiting wolf (1).</summary>
        public QuadPart Quad(int actor, float from, float to, Vector2 pos, Vector2 face, QuadAct act, float speed = 0.3f, QuadHeld held = QuadHeld.None)
        {
            var q = new QuadPart { actor = actor, from = from, to = to, pos = pos, face = face, act = act, speed = speed, held = held };
            quads.Add(q);
            return q;
        }

        /// <summary>#694: the part actor <paramref name="actor"/> is in at scene second <paramref name="u"/> (null before its first and after its last).</summary>
        public QuadPart QuadAt(int actor, float u)
        {
            foreach (var q in quads) if (q.actor == actor && u >= q.from && u < q.to) return q;
            return null;
        }

        public void Tip(int member, float from, float to, CampTipKind kind, float grow = 1f, float darken = 0f) =>
            tips.Add(new CampTip { member = member, from = from, to = to, kind = kind, grow = grow, darken = darken });

        /// <summary>The part member <paramref name="key"/> is in at pose clock <paramref name="t"/>, and the seconds into it.</summary>
        public bool TryPart(int key, float t, out CampPart part, out float into)
        {
            part = null; into = 0f;
            int i = keys.IndexOf(key);
            if (i < 0) return false;
            float s = t - t0;
            foreach (var p in parts[i])
                if (s >= p.from && s < p.to) { part = p; into = s - p.from; return true; }
            return false;
        }

        /// <summary>The member's first part start (scene seconds); before it, the scene doesn't have them yet.</summary>
        public float FirstFrom(int member) { float f = float.MaxValue; foreach (var p in parts[member]) f = Mathf.Min(f, p.from); return f; }
    }

    /// <summary>#692 shared helpers for the scenes' poses.</summary>
    public static class CampPose
    {
        public static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
        /// <summary>0 → 1 over [a, a+len] of <paramref name="t"/>, smoothed.</summary>
        public static float In(float t, float a, float len) => Smooth((t - a) / Mathf.Max(1e-4f, len));
        /// <summary>A bump 0 → 1 → 0 over [a, a+len].</summary>
        public static float Bump(float t, float a, float len) => t < a || t > a + len ? 0f : Mathf.Sin((t - a) / len * Mathf.PI);
        public static Vector3 Lerp(Vector3 x, Vector3 y, float k) => Vector3.LerpUnclamped(x, y, k);
        /// <summary>A seated pose (low, squashed), as the Sit loop and the meal draw it.</summary>
        public static void Seated(ref DollPose p) { p.offset.y = -0.03f; p.squash = 0.78f; }
    }
}
