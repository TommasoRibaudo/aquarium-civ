using System;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>Where an echo is staged (#76). The planner resolves it to a place when it stages the echo
    /// (<see cref="LifePlanner.StageEcho"/>); a place that isn't in the box falls back to where the lead stands.</summary>
    public enum EchoWhere
    {
        /// <summary>No echo.</summary>
        None,
        /// <summary>Where the lead stands now (the maker next to the thing, the two who chat).</summary>
        LeadHere,
        /// <summary>The entry's building (<see cref="EchoScene.buildingId"/>).</summary>
        Building,
        /// <summary>The relic's spot (<see cref="EchoScene.relicId"/>).</summary>
        Relic,
        /// <summary>The lead's home (a parent holding up the baby).</summary>
        Home,
        /// <summary>The seat of the lead's role (the hall, the shrine), else the gathering place.</summary>
        Seat,
        /// <summary>The bench by the green (the well, else the gathering place).</summary>
        Bench,
        /// <summary>The fire (sharing a pot).</summary>
        Fire,
        /// <summary>The gathering place.</summary>
        Gather,
        /// <summary>The front glass (a scholar tapping back).</summary>
        Glass,
        /// <summary>The remembering stone (a death: no people, a flower).</summary>
        Remembering,
        /// <summary>The Keeper's shaping (<see cref="EchoScene.nudgeId"/>) when the answer has no building.</summary>
        Shaping,
    }

    /// <summary>Why a "Show me" has no echo (fly-to only, chronicle-and-history §2.5).</summary>
    public enum EchoSkip
    {
        /// <summary>It has an echo.</summary>
        None,
        /// <summary>A death: no echo, the remembering stone and a flower (<see cref="EchoScene.flower"/>).</summary>
        Death,
        /// <summary>The weather and the world (storm, fog, snow, a rainbow...): the place only.</summary>
        Weather,
        /// <summary>The whole box (an era, the naming, a festival, a milestone): the place only.</summary>
        BoxWide,
        /// <summary>Older than <see cref="EchoScenes.MaxAgeMonths"/>.</summary>
        TooOld,
        /// <summary>Nobody it is about is alive in the box.</summary>
        PeopleGone,
        /// <summary>The thing it's about isn't in the box any more (a relic not found, a building gone).</summary>
        PlaceGone,
        /// <summary>No entry, or a kind that never echoes (came of age, a job change).</summary>
        NoStory,
    }

    /// <summary>
    /// "Show me"'s echo for one chronicle entry as data (#76; chronicle-and-history §2.5, Choice 4): who plays it (the lead,
    /// the other one, a few nearby), where, the beats and how long. Pure: worked out from the sim, no dice, no planner.
    /// The planner stages it with <see cref="LifePlanner.StageEcho"/> using the relic moments' staging (#92): the same cast
    /// groups (<see cref="RelicCast.Owner"/> = the lead, <see cref="RelicCast.Second"/> = the other one,
    /// <see cref="RelicCast.Grown"/>/<see cref="RelicCast.Kids"/> = whoever is near enough) and the walking pace.
    /// </summary>
    public sealed class EchoScene
    {
        public ChronicleEntry entry;
        /// <summary>The scene kind it echoes (for the debug readout and the people view; the beats are in <see cref="def"/>).</summary>
        public SceneKind kind;
        /// <summary>The beats and their length (real seconds, 3–5). Null when it has no echo.</summary>
        public RelicMomentDef def;
        /// <summary>The lead (<see cref="RelicCast.Owner"/>) and the other one (<see cref="RelicCast.Second"/>); -1 = none.</summary>
        public int leadId = -1, otherId = -1;
        public EchoWhere where;
        public int buildingId = -1, nudgeId = -1;
        public string relicId = "";
        /// <summary>The thing next to its maker (the five-wheeled barrow, the pink roof, the turnip): the story kind, for the
        /// view to set a prop by the lead. "" = none.</summary>
        public string thing = "";
        /// <summary>A death: no echo, a flower at the remembering stone (the view lays it).</summary>
        public bool flower;
        /// <summary>The line said in the echo (the entry's quote, or the lead's own words) and who says it (-1 = the crowd:
        /// the first of the cast says it). Said from <see cref="lineAt"/> (a fraction of the echo) to the end.</summary>
        public string line = "";
        public int lineSpeakerId = -1;
        public float lineAt = 0.2f;
        public EchoSkip skip;

        public bool Plays => skip == EchoSkip.None && def != null;
        public float Seconds => def?.seconds ?? 0f;
    }

    /// <summary>The echo table (chronicle-and-history §2.5): which stories have an echo and how it is played.</summary>
    public static class EchoScenes
    {
        /// <summary>A story older than ~5 years is fly-to only [est].</summary>
        public const int MaxAgeMonths = 60;
        /// <summary>The echo's length: 3–5 real seconds (Choice 4).</summary>
        public const float Seconds = 4f, MinSeconds = 3f, MaxSeconds = 5f;

        /// <summary>The echo for <paramref name="e"/> now: always an <see cref="EchoScene"/>; <see cref="EchoScene.Plays"/>
        /// false (with <see cref="EchoScene.skip"/> saying why) means fly-to only.</summary>
        public static EchoScene For(SimCore sim, ChronicleEntry e, float seconds = Seconds)
        {
            var sc = new EchoScene { entry = e };
            if (sim == null || e == null || string.IsNullOrEmpty(e.kind)) { sc.skip = EchoSkip.NoStory; return sc; }
            string kind = e.kind;
            string group = SimCore.GroupOf(kind);
            if (kind == "death" || kind == "role_death")
            {
                sc.skip = EchoSkip.Death; sc.flower = true; sc.where = EchoWhere.Remembering; sc.leadId = e.notableId;
                return sc;
            }
            if (group == "world") { sc.skip = EchoSkip.Weather; return sc; }
            if (group == "era" || group == "milestone" || group == "office" || group == "hidden" || kind == "role_offices" || kind == "sweepers")
            { sc.skip = EchoSkip.BoxWide; return sc; }
            if (kind == "retired" || kind == "job_change" || kind == "job_fill" || kind == "job_era" || kind == "coming_of_age" || kind == "role_stepdown")
            { sc.skip = EchoSkip.NoStory; return sc; }
            if (sim.State.month - e.month > MaxAgeMonths) { sc.skip = EchoSkip.TooOld; return sc; }

            var d = new RelicMomentDef { relicId = e.relicId ?? "", seconds = Math.Max(MinSeconds, Math.Min(MaxSeconds, seconds)), standIns = false, maxKids = 0, maxGrown = 0, maxQueue = 0 };
            int A = AliveId(sim, e.notableId), B = AliveId(sim, e.otherNotableId);
            sc.leadId = A; sc.otherId = B; sc.where = EchoWhere.LeadHere;

            if (kind == "relic" && !string.IsNullOrEmpty(e.relicId))
            {
                var r = sim.FindRelic(e.relicId);
                if (r == null || !r.Found) { sc.skip = EchoSkip.PlaceGone; return sc; }
                sc.kind = SceneKind.Relic; sc.where = EchoWhere.Relic; sc.relicId = e.relicId;
                sc.leadId = AliveId(sim, e.relicId == RelicId.LeftSandal ? r.holderId : r.ownerId);
                if (sc.leadId < 0) sc.leadId = A;
                sc.otherId = -1;
                var (k, prop, faces) = RelicGesture(e.relicId);
                if (sc.leadId >= 0) Beat(d, RelicCast.Owner, 0f, 1f, k, RelicWhere.Spot, prop, faces);
                // §2.5: the relic in the world, people around it pointing.
                d.maxGrown = 3; d.maxKids = 2;
                Beat(d, RelicCast.Grown, 0f, 1f, ActivityKind.Point, RelicWhere.Spot);
                Beat(d, RelicCast.Kids, 0f, 0.5f, ActivityKind.Point, RelicWhere.Spot);
                Beat(d, RelicCast.Kids, 0.5f, 1f, ActivityKind.LaughPoint, RelicWhere.Spot);
                return Done(sc, d, e);
            }
            if (A < 0 && kind != "birth" && kind != "apprentice") { sc.skip = EchoSkip.PeopleGone; return sc; }

            switch (kind)
            {
                case "wedding":
                    // The couple sit side by side on the bench by the green.
                    if (B < 0) { sc.skip = EchoSkip.PeopleGone; return sc; }
                    sc.kind = SceneKind.Wedding; sc.where = EchoWhere.Bench;
                    Beat(d, RelicCast.Owner, 0f, 1f, ActivityKind.SitTogether, RelicWhere.Spot, Prop.Flower);
                    Beat(d, RelicCast.Second, 0f, 1f, ActivityKind.SitTogether, RelicWhere.Spot);
                    break;
                case "birth":
                {
                    // A parent holds the baby up; neighbours lean in. The lead is the parent; the child is the other one.
                    var child = sim.FindNotable(e.notableId);
                    int parent = child != null ? AliveId(sim, child.parentId) : -1;
                    if (parent < 0) { sc.skip = EchoSkip.PeopleGone; return sc; }
                    sc.kind = SceneKind.Birth; sc.where = EchoWhere.Home; sc.leadId = parent; sc.otherId = A;
                    Beat(d, RelicCast.Owner, 0f, 1f, ActivityKind.HoldUp, RelicWhere.Spot);
                    d.maxGrown = 3;
                    Beat(d, RelicCast.Grown, 0f, 1f, ActivityKind.Watch, RelicWhere.Spot);
                    break;
                }
                case "friendship":
                    // The two chat, a shared bob.
                    sc.kind = SceneKind.Friendship;
                    Beat(d, RelicCast.Owner, 0f, 1f, ActivityKind.Chat, RelicWhere.Spot);
                    if (B >= 0) Beat(d, RelicCast.Second, 0f, 1f, ActivityKind.Chat, RelicWhere.Spot);
                    break;
                case "rivalry":
                    // They turn their backs: a sharp word, then each sits out on their own.
                    sc.kind = SceneKind.Rivalry;
                    Beat(d, RelicCast.Owner, 0f, 0.5f, ActivityKind.Argue, RelicWhere.Spot);
                    Beat(d, RelicCast.Owner, 0.5f, 1f, ActivityKind.SitOut, RelicWhere.Here);
                    if (B >= 0)
                    {
                        Beat(d, RelicCast.Second, 0f, 0.5f, ActivityKind.Argue, RelicWhere.Spot);
                        Beat(d, RelicCast.Second, 0.5f, 1f, ActivityKind.SitOut, RelicWhere.Here);
                    }
                    break;
                case "reconcile":
                    // They share a pot by the fire.
                    sc.kind = SceneKind.Reconcile; sc.where = EchoWhere.Fire;
                    Beat(d, RelicCast.Owner, 0f, 1f, ActivityKind.Eat, RelicWhere.Spot, Prop.Pots);
                    if (B >= 0) Beat(d, RelicCast.Second, 0f, 1f, ActivityKind.Eat, RelicWhere.Spot);
                    break;
                case "apprentice":
                {
                    // The master shows, the apprentice watches the work.
                    int master = B, app = A;
                    if (master < 0) { sc.skip = EchoSkip.PeopleGone; return sc; }
                    sc.kind = SceneKind.Apprentice; sc.leadId = master; sc.otherId = app;
                    Beat(d, RelicCast.Owner, 0f, 1f, ActivityKind.Direct, RelicWhere.Spot);
                    if (app >= 0) Beat(d, RelicCast.Second, 0f, 1f, ActivityKind.WatchWork, RelicWhere.Spot);
                    break;
                }
                case "keeper":
                {
                    // The worker at the new field or building, with a tool raised (to the Keeper).
                    sc.kind = SceneKind.Chat; sc.otherId = -1;
                    var n = NudgeOf(sim, e);
                    if (e.buildingId >= 0 && sim.State.buildings.Exists(b => b.id == e.buildingId)) { sc.where = EchoWhere.Building; sc.buildingId = e.buildingId; }
                    else if (n != null) { sc.where = EchoWhere.Shaping; sc.nudgeId = n.id; }
                    Beat(d, RelicCast.Owner, 0f, 1f, ActivityKind.HoldUp, RelicWhere.Spot, ToolOf(n?.kind), faces: true);
                    d.maxGrown = 2;
                    Beat(d, RelicCast.Grown, 0f, 0.5f, ActivityKind.Watch, RelicWhere.Spot);
                    Beat(d, RelicCast.Grown, 0.5f, 1f, ActivityKind.Cheer, RelicWhere.Spot);
                    break;
                }
                case "tapback":
                    sc.kind = SceneKind.TapBack; sc.where = EchoWhere.Glass; sc.otherId = -1;
                    Beat(d, RelicCast.Owner, 0f, 0.6f, ActivityKind.TapBack, RelicWhere.Spot, faces: true);
                    Beat(d, RelicCast.Owner, 0.6f, 1f, ActivityKind.WriteDown, RelicWhere.Spot, Prop.Slate, faces: true);
                    break;
                case "lost":
                    // Lost in the hills: they point the way they went.
                    sc.kind = SceneKind.Lost; sc.otherId = -1;
                    Beat(d, RelicCast.Owner, 0f, 1f, ActivityKind.Point, RelicWhere.Spot);
                    break;
                default:
                    if (kind.StartsWith("role_"))
                    {
                        // Role won: the new holder stands at the seat; a few people gather.
                        sc.kind = SceneKind.RoleMoment; sc.where = EchoWhere.Seat; sc.otherId = -1;
                        Beat(d, RelicCast.Owner, 0f, 1f, ActivityKind.Speech, RelicWhere.Spot);
                        d.maxGrown = 3;
                        Beat(d, RelicCast.Grown, 0f, 1f, ActivityKind.Listen, RelicWhere.Spot);
                        break;
                    }
                    // Invention, prank, turnip and the other trait stories: the thing itself next to its maker.
                    sc.kind = SceneKind.Chat; sc.otherId = -1;
                    if (group == "trait") sc.thing = kind;
                    if (e.buildingId >= 0 && sim.State.buildings.Exists(b => b.id == e.buildingId)) { sc.where = EchoWhere.Building; sc.buildingId = e.buildingId; }
                    Beat(d, RelicCast.Owner, 0f, 1f, kind == "song" ? ActivityKind.Sing : ActivityKind.Point, RelicWhere.Spot);
                    break;
            }
            return Done(sc, d, e);
        }

        static EchoScene Done(EchoScene sc, RelicMomentDef d, ChronicleEntry e)
        {
            if (d.beats.Count == 0) { sc.skip = EchoSkip.PeopleGone; return sc; }
            sc.def = d;
            if (!string.IsNullOrEmpty(e.quote)) { sc.line = e.quote; sc.lineSpeakerId = e.speakerId; }
            return sc;
        }

        static void Beat(RelicMomentDef d, RelicCast c, float from, float to, ActivityKind k, RelicWhere w, Prop prop = Prop.None, bool faces = false)
            => d.beats.Add(new RelicBeat { id = k.ToString().ToLowerInvariant(), cast = c, from = from, to = to, kind = k, where = w, prop = prop, facesGlass = faces });

        static int AliveId(SimCore sim, int id)
        {
            if (id < 0) return -1;
            var n = sim.FindNotable(id);
            return n != null && n.alive ? id : -1;
        }

        /// <summary>The relic card's gesture (the same as the planner's relic echo, #92).</summary>
        public static (ActivityKind kind, Prop prop, bool faces) RelicGesture(string relicId) => relicId switch
        {
            RelicId.SkyCrumb => (ActivityKind.Taste, Prop.Crumb, false),
            RelicId.FestivalMask => (ActivityKind.Dance, Prop.Mask, true),
            RelicId.KeeperDoll => (ActivityKind.Point, Prop.None, false),
            RelicId.LeftSandal => (ActivityKind.LopsidedDance, Prop.Sandal, false),
            RelicId.Lens => (ActivityKind.LookThroughLens, Prop.Lens, false),
            _ => (ActivityKind.Point, Prop.None, false),
        };

        /// <summary>The Keeper's gift a "keeper" entry answered: the nudge whose answer is the entry's building, else the one
        /// credited that month without a building (the town was full). Null if none is left in the box.</summary>
        public static Nudge NudgeOf(SimCore sim, ChronicleEntry e)
        {
            if (sim == null || e == null) return null;
            var nudges = sim.State.nudges;
            if (nudges == null) return null;
            if (e.buildingId >= 0)
                foreach (var n in nudges) if (n.credited && n.buildingId == e.buildingId) return n;
            foreach (var n in nudges) if (n.credited && n.answerMonth == e.month && n.buildingId < 0) return n;
            return null;
        }

        /// <summary>The tool raised for a Keeper's gift.</summary>
        public static Prop ToolOf(string nudgeKind) => nudgeKind switch
        {
            SimCore.NudgeKind.Rain => Prop.Sheaf,
            SimCore.NudgeKind.Grove => Prop.Axe,
            SimCore.NudgeKind.Herd => Prop.Crook,
            SimCore.NudgeKind.Channel => Prop.Hoe,
            SimCore.NudgeKind.Lamp => Prop.Lantern,
            SimCore.NudgeKind.Spring => Prop.Buckets,
            SimCore.NudgeKind.Outcrop => Prop.Pick,
            SimCore.NudgeKind.DyeMeadow => Prop.Basket,
            _ => Prop.Hoe,
        };
    }
}
