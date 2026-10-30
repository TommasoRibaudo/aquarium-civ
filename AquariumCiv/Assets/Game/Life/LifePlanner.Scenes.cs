using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>Story scenes (life-in-the-box §6, rule 11), "Show me" echoes (chronicle-and-history §2.5) and the people's
    /// reactions to the Keeper (§8, decisions 2026-10-29: hold a finger and tap the glass; how the people understand the
    /// glass by stage).</summary>
    public sealed partial class LifePlanner
    {
        struct Scene
        {
            public SceneKind kind;
            public long day;
            public float start, end;
            public int a, b;             // notable ids (−1 = none)
            public Place place;
            public int chronicleId;
            public bool echo, contest;
            // For a mourning: the dead are no longer in the planner's people, so their links are kept here.
            public string family;
            public int aPartner, aFriend, aParent;
            // #92: a relic moment, a gathering at the glass or a tap-back, staged in full when it starts.
            public Staging staging;
        }

        // ---------------------------------------------------------------- scenes from this month's chronicle

        void AddChronicleScene(DayInfo d, ChronicleEntry e)
        {
            var sc = new Scene { day = d.day, a = e.notableId, b = e.otherNotableId, chronicleId = e.id, aPartner = -1, aFriend = -1, aParent = -1 };
            switch (e.kind)
            {
                case "wedding": sc.kind = SceneKind.Wedding; sc.start = 16f; sc.end = 19f; sc.place = churchB != null ? PlaceOf(churchB) : gather; break;
                case "birth":
                {
                    var child = Alive(e.notableId);
                    if (child == null || child.parent == null) return;
                    sc.kind = SceneKind.Birth; sc.start = 12f; sc.end = 13f; sc.place = child.parent.home; sc.b = child.parent.key;
                    break;
                }
                case "festival": case "era": case "anniversary":
                    sc.kind = SceneKind.Festival; sc.start = 13f; sc.end = 20f; sc.place = gather; break;
                case "death": case "role_death":
                {
                    var dead = sim.FindNotable(e.notableId);
                    if (dead == null) return;
                    sc.kind = SceneKind.Mourning; sc.start = 17.2f; sc.end = 18.6f; sc.place = remembering;
                    sc.family = dead.family; sc.aPartner = dead.partnerId; sc.aFriend = dead.friendId; sc.aParent = dead.parentId;
                    break;
                }
                case "reconcile": sc.kind = SceneKind.Reconcile; sc.start = 18f; sc.end = 19f; sc.place = fire; break;
                case "lost": sc.kind = SceneKind.Lost; sc.start = 7f; sc.end = 21f; sc.place = Place.Land(PlaceKind.Hill); break;
                case "rivalry": sc.kind = SceneKind.Rivalry; sc.start = 17.2f; sc.end = 17.55f; sc.place = gather; break;
                default:
                    if (!e.kind.StartsWith("role_") || e.kind == "role_stepdown") return;
                    sc.kind = SceneKind.RoleMoment; sc.start = 17f; sc.end = 17.6f; sc.contest = e.kind == "role_contest";
                    sc.place = SeatOfHolder(e.notableId);
                    break;
            }
            d.scenes.Add(sc);
        }

        Place SeatOfHolder(int id)
        {
            var roles = sim.State.roles;
            if (roles != null)
                foreach (var r in roles)
                    if (r.holderId == id) { var b = sim.SeatBuilding(r.role); if (b != null) return PlaceOf(b); }
            return gather;
        }

        /// <summary>Lays one scene's part for <paramref name="p"/> over the day (or into a live list).</summary>
        void SceneLayer(Scene sc, Person p, DayInfo d, List<Seg> list, bool live)
        {
            if (p.stage == LifeStage.Baby) return;
            if (sc.staging != null) { StagedLayer(sc, p, d, list); return; } // #92
            var A = Alive(sc.a);
            var B = Alive(sc.b);
            float len = sc.end - sc.start;
            float T(float f) => sc.start + f * len;
            void S(ActivityKind k, float f0, float f1, Place place, Prop prop = Prop.None, Person with = null)
            {
                var s = Mk(k, T(f0), T(f1), place, prop, ActivitySource.Scene, Ref(with), default, sc.chronicleId);
                Put(list, s, d);
            }
            bool isA = A == p, isB = B == p;
            switch (sc.kind)
            {
                case SceneKind.Wedding:
                    if (sc.echo) { if (isA || isB) S(ActivityKind.SitTogether, 0f, 1f, sc.place, Prop.Flower, isA ? B : A); return; }
                    S(ActivityKind.Procession, 0f, 1f / 6f, sc.place, isA || isB ? Prop.Flower : Prop.None, isA ? B : isB ? A : null);
                    if (isA || isB) S(ActivityKind.Dance, 1f / 6f, 1f, sc.place, Prop.None, isA ? B : A);
                    else
                    {
                        S(ActivityKind.Cheer, 1f / 6f, 1f / 3f, sc.place);
                        S(p.stage == LifeStage.Kid ? ActivityKind.Play : p.stage == LifeStage.Elder ? ActivityKind.SitOut : ActivityKind.Dance, 1f / 3f, 1f, sc.place);
                    }
                    return;
                case SceneKind.Birth:
                {
                    var parent = B;
                    if (parent == null) return;
                    if (p == parent) { S(ActivityKind.Visit, 0f, 1f, sc.place, Prop.None, A); return; } // the bundle held up
                    bool family = p == parent.partner || p == parent.parent || (p.n != null && parent.n != null && p.n.family == parent.n.family) || p == parent.friend;
                    bool neighbour = p.n == null && p.Grown && p.home.buildingId == parent.home.buildingId && R(p.key, sc.day, 120) < 0.5f;
                    if (family || neighbour) S(ActivityKind.Visit, sc.echo ? 0f : 0.1f, 1f, sc.place, Prop.None, parent);
                    return;
                }
                case SceneKind.Festival:
                {
                    if (p == leader) { S(ActivityKind.Speech, 3f / 7f, 3.5f / 7f, sc.place); }
                    for (int i = 0; i < 7; i++)
                    {
                        if (leader != null && p != leader && i == 3) { S(ActivityKind.Listen, 3f / 7f, 3.5f / 7f, sc.place, Prop.None, leader); S(Festive(p, sc, i), 3.5f / 7f, 4f / 7f, sc.place); continue; }
                        if (p == leader && i == 3) { S(Festive(p, sc, i), 3.5f / 7f, 4f / 7f, sc.place); continue; }
                        S(Festive(p, sc, i), i / 7f, (i + 1) / 7f, sc.place, Prop.None);
                    }
                    return;
                }
                case SceneKind.Mourning:
                {
                    if (sc.echo) return; // no echo for a death: the stone and a flower
                    bool close = p.key == sc.aPartner || p.key == sc.aFriend || p.key == sc.aParent || (p.n != null && (p.n.parentId == sc.a || (sc.family != null && p.n.family == sc.family)));
                    if (p.role == RoleType.Priest) S(ActivityKind.Service, 0f, 1f, sc.place);
                    else if (close) S(ActivityKind.Mourn, 0f, 1f, sc.place, p.key == sc.aPartner || p.n?.parentId == sc.a ? Prop.Flower : Prop.None);
                    return;
                }
                case SceneKind.RoleMoment:
                    if (isA) S(ActivityKind.Speech, 0f, 1f, sc.place);
                    else if (isB && sc.contest) { S(ActivityKind.Walk, 0f, 0.4f, p.home); S(ActivityKind.SitOut, 0.4f, 1f, p.home); } // the loser walks home alone
                    else if ((p.n != null && p.Grown) || (p.Grown && R(p.key, sc.day, 121) < (sc.echo ? 0.15f : 0.5f))) S(ActivityKind.Listen, 0f, 1f, sc.place, Prop.None, A);
                    return;
                case SceneKind.Reconcile:
                    if (isA || isB) S(sc.echo ? ActivityKind.Eat : ActivityKind.SitTogether, 0f, 1f, sc.place, Prop.None, isA ? B : A); // sharing a pot
                    return;
                case SceneKind.Friendship: case SceneKind.Chat:
                    if (sc.echo && B == null) { if (isA) S(ActivityKind.Point, 0f, 1f, sc.place); return; } // the maker next to the thing
                    if (isA || isB) S(ActivityKind.Chat, 0f, 1f, sc.place, Prop.None, isA ? B : A);
                    return;
                case SceneKind.Rivalry: case SceneKind.Argument:
                    if (isA || isB) S(ActivityKind.Argue, 0f, 1f, sc.place, Prop.None, isA ? B : A);
                    else if (p == leader && !sc.echo) S(ActivityKind.Mediate, 0.1f, 1f, sc.place, Prop.None, A);
                    return;
                case SceneKind.Lost:
                {
                    if (A == null) return;
                    float searchFrom = (18f - sc.start) / len, cheerFrom = (20.5f - sc.start) / len;
                    if (sc.echo) { if (isA) S(ActivityKind.Point, 0f, 1f, sc.place); return; }
                    if (isA) { S(ActivityKind.Explore, 0f, cheerFrom, sc.place); S(ActivityKind.Cheer, cheerFrom, 1f, gather); return; }
                    bool searches = p.Grown && (p == A.partner || p == A.parent || p.parent == A || p == A.friend || p.role == RoleType.Captain || p == leader
                        || (p.n != null && A.n != null && p.n.family == A.n.family) || (p.n == null && p.Adult && R(p.key, sc.day, 122) < 0.25f));
                    if (searches) { S(ActivityKind.Search, searchFrom, cheerFrom, sc.place, Prop.Lantern); S(ActivityKind.Cheer, cheerFrom, 1f, gather); }
                    return;
                }
                case SceneKind.Apprentice:
                {
                    var master = B ?? A?.mentor;
                    var app = master == B ? A : null;
                    if (master == null) return;
                    var w = master.Adult ? WorkOf(master, d, true) : default;
                    var loop = master.Adult ? w.loop : ActivityKind.StoryTell;
                    var place = sc.place.IsNone ? (master.Adult ? w.site : gather) : sc.place;
                    if (p == master) S(loop, 0f, 1f, place, master.Adult ? w.tool : Prop.None, app);
                    else if (p == app) { var s = Mk(loop, sc.start, sc.end, place, master.Adult ? w.tool : Prop.None, ActivitySource.Scene, master.who, default, sc.chronicleId); s.copies = true; Put(list, s, d); }
                    return;
                }
                case SceneKind.EveningTalk:
                {
                    var speaker = leader ?? EldestGrown();
                    if (p == speaker) S(ActivityKind.Speech, 0f, 1f, sc.place);
                    else S(p.stage == LifeStage.Kid ? ActivityKind.ListenStory : ActivityKind.Listen, 0f, 1f, sc.place, Prop.None, speaker);
                    return;
                }
                case SceneKind.EraMoment:
                    S(ActivityKind.LookUp, 0f, 1f, sc.place); // everyone gathers at the centre and looks up together
                    return;
            }
        }

        ActivityKind Festive(Person p, Scene sc, int i)
        {
            float r = R(p.key, sc.day, 130 + i);
            if (p.hints.singChance > 0f && r < p.hints.singChance) return ActivityKind.Sing;
            if (p.stage == LifeStage.Kid) return r < 0.6f ? ActivityKind.Play : ActivityKind.Dance;
            if (p.stage == LifeStage.Elder) return r < 0.5f ? ActivityKind.SitOut : ActivityKind.Eat;
            return r < 0.4f ? ActivityKind.Dance : r < 0.65f ? ActivityKind.Eat : r < 0.9f ? ActivityKind.Chat : ActivityKind.Cheer;
        }

        Person EldestGrown()
        {
            Person e = null;
            foreach (var p in people) if (p.n != null && p.Grown && (e == null || p.age > e.age)) e = p;
            return e;
        }

        // ---------------------------------------------------------------- live scenes: echoes, the evening talk, debug

        readonly List<Scene> live = new List<Scene>();
        readonly List<Seg> liveScratch = new List<Seg>(8);

        /// <summary>The festival year's evenings (view hours) [est].</summary>
        public const float FestivalEveningFrom = 17.5f, FestivalEveningTo = 21.5f;

        static float DefaultHours(SceneKind k) => k switch
        {
            SceneKind.Wedding => 3f, SceneKind.Birth => 1f, SceneKind.Festival => 7f, SceneKind.Mourning => 1.4f,
            SceneKind.RoleMoment => 0.6f, SceneKind.Reconcile => 1f, SceneKind.Lost => 3f, SceneKind.Rivalry => 0.35f,
            SceneKind.Argument => 0.35f, SceneKind.EveningTalk => 1f, SceneKind.EraMoment => 0.5f, SceneKind.Apprentice => 1f,
            _ => 0.8f,
        };

        /// <summary>Start a story scene now (the debug panel's "start a wedding", the evening talk at a long session's close,
        /// the era's gathering). <paramref name="a"/>, <paramref name="b"/> are notable ids; the place defaults to the scene's
        /// usual one. Lasts <paramref name="hours"/> view hours (default per kind), at most to the end of the view day.</summary>
        public void StartScene(SceneKind kind, ViewTime at, int a = -1, int b = -1, int buildingId = -1, float hours = 0f)
        {
            Sync();
            var sc = new Scene
            {
                kind = kind, day = at.day, start = at.hour, a = a, b = b, chronicleId = -1, aPartner = -1, aFriend = -1, aParent = -1,
            };
            sc.end = Math.Min(24f, at.hour + (hours > 0f ? hours : DefaultHours(kind)));
            sc.place = buildingId >= 0 && buildingIds.Contains(buildingId) ? PlaceOf(FindBuilding(buildingId)) : DefaultScenePlace(kind, a, b);
            if (kind == SceneKind.Mourning)
            {
                var dead = sim.FindNotable(a);
                if (dead != null) { sc.family = dead.family; sc.aPartner = dead.partnerId; sc.aFriend = dead.friendId; sc.aParent = dead.parentId; }
            }
            if (kind == SceneKind.Birth && Alive(a)?.parent is Person parent) { sc.b = parent.key; sc.place = parent.home; }
            if (kind == SceneKind.RoleMoment && buildingId < 0) sc.place = SeatOfHolder(a);
            live.Add(sc);
            LiveChanged();
        }

        /// <summary>"Show me" (chronicle-and-history §2.5): the people involved do a 3–5 s echo of the story, then go back to
        /// their day. Returns false when the story has no echo (a death, the weather, too old): fly-to only.</summary>
        public bool Echo(ChronicleEntry e, ViewTime at, float seconds = 4f)
        {
            if (e == null) return false;
            Sync();
            if (sim.State.month - e.month > 60) return false; // older than ~5 years [est]
            if (!string.IsNullOrEmpty(e.relicId) && e.kind == "relic") return EchoRelic(e, at, seconds); // #92
            SceneKind kind;
            switch (e.kind)
            {
                case "wedding": kind = SceneKind.Wedding; break;
                case "birth": kind = SceneKind.Birth; break;
                case "friendship": kind = SceneKind.Friendship; break;
                case "rivalry": kind = SceneKind.Rivalry; break;
                case "reconcile": kind = SceneKind.Reconcile; break;
                case "apprentice": kind = SceneKind.Apprentice; break;
                case "lost": kind = SceneKind.Lost; break;
                default:
                    if (e.kind.StartsWith("role_") && e.kind != "role_death") { kind = SceneKind.RoleMoment; break; }
                    if (e.notableId >= 0 && Alive(e.notableId) != null && e.kind != "death" && e.kind != "retired") { kind = SceneKind.Chat; break; } // the maker next to the thing
                    return false;
            }
            var A = Alive(e.notableId);
            if (A == null && kind != SceneKind.Birth) return false;
            var sc = new Scene
            {
                kind = kind, day = at.day, start = at.hour, a = e.notableId, b = e.otherNotableId, chronicleId = e.id, echo = true,
                aPartner = -1, aFriend = -1, aParent = -1,
            };
            sc.end = Math.Min(24f, at.hour + LifeClock.ViewHoursFor(at.hour, Math.Max(3f, Math.Min(5f, seconds))));
            // Place = the entry's building, else the person's current spot.
            if (e.buildingId >= 0 && buildingIds.Contains(e.buildingId)) sc.place = PlaceOf(FindBuilding(e.buildingId));
            else if (kind == SceneKind.Wedding) sc.place = wellB != null ? PlaceOf(wellB) : gather; // the bench by the green
            else if (kind == SceneKind.RoleMoment) sc.place = SeatOfHolder(e.notableId);
            else if (A != null) { EnsureDay(at.day); var cur = DayOf(A)[Find(DayOf(A), at.hour)]; sc.place = cur.place; }
            if (kind == SceneKind.Birth)
            {
                var child = Alive(e.notableId);
                if (child?.parent == null) return false;
                sc.b = child.parent.key; sc.place = child.parent.home;
            }
            if (kind == SceneKind.Chat) sc.b = -1;
            live.Add(sc);
            LiveChanged();
            return true;
        }

        /// <summary>Debug / view: end every live scene and reaction.</summary>
        public void ClearLive() { live.Clear(); reactions.Clear(); LiveChanged(); }

        Building FindBuilding(int id)
        {
            foreach (var b in sim.State.buildings) if (b.id == id) return b;
            return null;
        }

        Place DefaultScenePlace(SceneKind kind, int a, int b) => kind switch
        {
            SceneKind.Wedding => churchB != null ? PlaceOf(churchB) : gather,
            SceneKind.Mourning => remembering,
            SceneKind.Reconcile => fire,
            SceneKind.Lost => Place.Land(PlaceKind.Hill),
            SceneKind.Apprentice => Place.None,
            _ => gather,
        };

        // ---------------------------------------------------------------- Keeper reactions (≤ 4 s, rule 10)

        struct Reaction
        {
            public KeeperGesture gesture;
            public long day;
            public float start, end;
            public Place near;
            public bool hasPoint;
            public float x, z, radius;
            public PersonRef target;
            public float share;
        }

        readonly List<Reaction> reactions = new List<Reaction>();

        static float DefaultSeconds(KeeperGesture g) => g switch
        {
            KeeperGesture.Return => 2.5f, KeeperGesture.Wipe => 2f, KeeperGesture.FullClean => 3f, KeeperGesture.FlickTrash => 1.5f,
            KeeperGesture.Prune => 2.5f, KeeperGesture.Rain => 4f, KeeperGesture.Shape => 3f, KeeperGesture.HoldFinger => 4f,
            KeeperGesture.TapGlass => 1.5f, KeeperGesture.TapPerson => 3f, _ => 1.5f,
        };

        /// <summary>
        /// The people react to the Keeper (§8). Interrupts anything for ≤ 4 s, then they pick up their day. <paramref name="near"/>
        /// limits it to those at that place (or within <paramref name="radius"/> of it once positions exist); without it a local
        /// gesture reaches a share of the box. <paramref name="share"/> scales how many react (a return after a long time
        /// away: more of them wave). <paramref name="person"/> is the tapped or pinned person.
        /// Holding a finger: call it again while the finger stays (each call lasts ≤ 4 s), <see cref="Release"/> when lifted.
        /// </summary>
        public void React(KeeperGesture gesture, ViewTime at, Place near = default, PersonRef person = default, float share = 1f, float seconds = 0f, float radius = 1f)
        {
            float secs = Math.Min(4f, seconds > 0f ? seconds : DefaultSeconds(gesture));
            reactions.Add(new Reaction
            {
                gesture = gesture, day = at.day, start = at.hour, end = Math.Min(24f, at.hour + LifeClock.ViewHoursFor(at.hour, secs)),
                near = near, target = person, share = Math.Max(0f, Math.Min(1f, share)), radius = radius,
            });
            LiveChanged();
            // #92 (§3 "Gathering"): in stage 1 a wipe brings a few to the glass, a step back, looking up; later a return sends
            // the kids running to it to look for you. At most once a view day.
            Sync();
            if ((gesture == KeeperGesture.Wipe && belief == 1) || (gesture == KeeperGesture.Return && belief >= 2))
                GatherAtGlass(at);
        }

        /// <summary>A gesture at a world point (needs <see cref="Positions"/>): those within <paramref name="radius"/> react.</summary>
        public void ReactAt(KeeperGesture gesture, ViewTime at, float x, float z, float radius = 1f, float share = 1f, float seconds = 0f)
        {
            React(gesture, at, default, default, share, seconds, radius);
            var r = reactions[reactions.Count - 1];
            r.hasPoint = true; r.x = x; r.z = z;
            reactions[reactions.Count - 1] = r;
            LiveChanged();
        }

        /// <summary>The finger lifted: the people under it scatter back to their day.</summary>
        public void Release(ViewTime at)
        {
            for (int i = reactions.Count - 1; i >= 0; i--)
                if (reactions[i].gesture == KeeperGesture.HoldFinger) reactions.RemoveAt(i);
            LiveChanged();
        }

        bool Reacts(Person p, Reaction r, ViewTime t, out ActivityKind kind, out Prop prop, out Place to)
        {
            kind = ActivityKind.LookUp; prop = Prop.None; to = Place.None;
            if (p.stage == LifeStage.Baby) return false;
            var segs = DayOf(p);
            var under = segs[Find(segs, t.hour)];
            bool asleep = under.kind == ActivityKind.Sleep;
            bool kid = p.stage == LifeStage.Kid;
            float roll = R(p.key, (long)(r.start * 1000) + r.day * 100000, 140 + (int)r.gesture);
            float f = r.end > r.start ? Math.Max(0f, Math.Min(1f, (t.hour - r.start) / (r.end - r.start))) : 0f;
            if (!ReactsPlain(p, r, t, under, asleep, kid, roll, out kind, out prop, out to)) return false;
            if (prop == Prop.Lantern) return true; // stepping out of the door at night with a lantern
            kind = StagedReaction(r.gesture, p, f, kind, out var staged);
            if (staged != Prop.None) prop = staged;
            return true;
        }

        bool ReactsPlain(Person p, Reaction r, ViewTime t, Seg under, bool asleep, bool kid, float roll, out ActivityKind kind, out Prop prop, out Place to)
        {
            kind = ActivityKind.LookUp; prop = Prop.None; to = Place.None;
            if (r.gesture == KeeperGesture.TapPerson || r.gesture == KeeperGesture.Glance)
            {
                if (r.target != p.who) return false;
                kind = r.gesture == KeeperGesture.Glance || (t.hour - r.start) < (r.end - r.start) * 0.4f ? ActivityKind.LookUp : ActivityKind.Wave;
                return true;
            }
            if (asleep)
            {
                // At night only a return brings them out, with lanterns (Choice 2).
                if (r.gesture != KeeperGesture.Return || roll >= r.share * 0.6f) return false;
                kind = ActivityKind.Wave; prop = Prop.Lantern;
                return true;
            }
            bool near = Near(p, under, r);
            bool global = r.near.IsNone && !r.hasPoint;
            switch (r.gesture)
            {
                case KeeperGesture.Return:
                    if (roll >= r.share) return false;
                    kind = ActivityKind.Wave; return true;
                case KeeperGesture.FullClean:
                    kind = ActivityKind.Cheer; return roll < r.share;
                case KeeperGesture.Rain:
                    if (!(near || global) || roll >= r.share) return false;
                    kind = kid ? ActivityKind.Splash : roll < 0.3f * r.share ? ActivityKind.Shelter : ActivityKind.HandsOut;
                    return true;
                case KeeperGesture.Wipe:
                    if (!(near || (global && roll < 0.5f * r.share))) return false;
                    kind = ActivityKind.LookUp; return true; // by stage: StagedReaction
                case KeeperGesture.TapGlass:
                    if (!(near || global) || roll >= r.share) return false;
                    kind = ActivityKind.LookUp; return true; // by stage: StagedReaction
                case KeeperGesture.FlickTrash:
                    if (!(near || (global && roll < 0.3f * r.share))) return false;
                    if (kid && !r.near.IsNone) { kind = ActivityKind.RunOver; to = r.near; return true; }
                    kind = ActivityKind.Clap; return true;
                case KeeperGesture.Prune:
                    if (!near) return false;
                    kind = ActivityKind.Wave; return true;
                case KeeperGesture.Shape:
                    if (!(near || (global && roll < 0.3f * r.share))) return false;
                    kind = ActivityKind.Point; return true;
                case KeeperGesture.HoldFinger:
                {
                    // How the people understand the glass, by stage (decisions 2026-10-29): the camp gathers but is a bit
                    // scared; the village plays with it and looks for you; the town's scholars study it.
                    bool reach = near || (global && (kid || roll < 0.3f * r.share));
                    if (!reach) return false;
                    if (kid && belief == 1) { kind = ActivityKind.LookUpWary; return true; } // kids creep in (StagedReaction)
                    if (kid)
                    {
                        bool arriving = (t.hour - r.start) < (r.end - r.start) * 0.3f && !r.near.IsNone && under.place != r.near;
                        kind = arriving ? ActivityKind.RunOver : ActivityKind.JumpAndPoint;
                        if (arriving) to = r.near;
                        return true;
                    }
                    kind = ActivityKind.LookUp; return true; // a town scholar studies or measures it (StagedReaction)
                }
            }
            return false;
        }

        bool Near(Person p, Seg under, Reaction r)
        {
            var here = under.place;
            if (!r.near.IsNone)
            {
                if (here == r.near || (here.IsBuilding && here.buildingId == r.near.buildingId)) return true;
                if (r.gesture == KeeperGesture.Prune && r.near.IsBuilding && p.home.buildingId == r.near.buildingId) return true; // the family steps out
                if (positions.TryGetPosition(here, out float hx, out float hz) && positions.TryGetPosition(r.near, out float nx, out float nz))
                    return (hx - nx) * (hx - nx) + (hz - nz) * (hz - nz) <= r.radius * r.radius;
                return false;
            }
            if (r.hasPoint && positions.TryGetPosition(here, out float px, out float pz))
                return (px - r.x) * (px - r.x) + (pz - r.z) * (pz - r.z) <= r.radius * r.radius;
            return false;
        }

        // ---------------------------------------------------------------- the job report (tap a person, the debug panel)

        /// <summary>One line of what a person is doing (RimWorld's job report): "Ivo Lund is taking logs to the lumber mill."</summary>
        public string Describe(PersonActivity a)
        {
            Sync();
            string who = "Someone";
            if (a.who.IsNotable && byKey.TryGetValue(a.who.Key, out var p) && p.n != null) who = p.n.FullName;
            else if (a.who.IsCrowd && byKey.TryGetValue(a.who.Key, out var c)) who = c.stage == LifeStage.Kid || c.stage == LifeStage.Baby ? "A child" : c.stage == LifeStage.Elder ? "An old one" : "One of the people";
            string with = "";
            if (!a.partner.IsNone && byKey.TryGetValue(a.partner.Key, out var q)) with = q.n != null ? " with " + q.n.FullName : " with a neighbour";
            string where = PlaceNoun(a.place);
            string what = a.kind switch
            {
                ActivityKind.Carry => $"taking {PropNoun(a.prop)} to {where}",
                ActivityKind.FetchWater => a.moving ? $"carrying water to {where}" : $"drawing water at {where}",
                ActivityKind.RollBarrel => $"rolling a barrel to {where}",
                ActivityKind.Walk => $"walking to {where}",
                ActivityKind.Sleep => "asleep at home",
                ActivityKind.Toddle => "toddling about at home",
                ActivityKind.Speech => $"speaking to everyone at {where}",
                ActivityKind.Listen => $"listening at {where}",
                ActivityKind.ListenStory => $"listening to a story at {where}",
                ActivityKind.StoryTell => $"telling the children a story at {where}",
                ActivityKind.Mourn => $"remembering someone at {where}",
                _ => $"{Verb(a.kind)} at {where}",
            };
            return $"{who} is {what}{with}.";
        }

        static string Verb(ActivityKind k) => k switch
        {
            ActivityKind.Nap => "napping", ActivityKind.SitOut => "sitting out", ActivityKind.Stretch => "stretching", ActivityKind.Eat => "eating",
            ActivityKind.SweepStep => "sweeping the step", ActivityKind.HangWashing => "hanging out the washing", ActivityKind.ChopKindling => "chopping kindling",
            ActivityKind.MindBabies => "minding the babies", ActivityKind.Gather => "gathering", ActivityKind.Stalk => "waiting for game",
            ActivityKind.Fish => "fishing", ActivityKind.Chop => "chopping wood", ActivityKind.Saw => "sawing planks", ActivityKind.StackWood => "splitting and stacking wood",
            ActivityKind.Knap => "knapping tools", ActivityKind.Cook => "tending the pot", ActivityKind.Hoe => "hoeing", ActivityKind.Sow => "sowing seed",
            ActivityKind.Weed => "weeding", ActivityKind.Harvest => "bringing in the harvest", ActivityKind.Repair => "mending things",
            ActivityKind.Herd => "minding the flock", ActivityKind.TendHives => "tending the hives", ActivityKind.ThrowPot => "throwing pots",
            ActivityKind.Brew => "brewing", ActivityKind.CutStone => "cutting stone", ActivityKind.Hammer => "hammering at the anvil",
            ActivityKind.Weave => "weaving", ActivityKind.Knead => "kneading bread", ActivityKind.MindStall => "minding a stall", ActivityKind.Serve => "serving",
            ActivityKind.Sweep => "sweeping", ActivityKind.Trim => "trimming and weeding", ActivityKind.MillWork => "working the mill",
            ActivityKind.Prepare => "getting ready for what's coming", ActivityKind.WalkRound => "walking the rounds", ActivityKind.HoldCourt => "holding court",
            ActivityKind.Inspect => "looking things over", ActivityKind.Direct => "directing the builders", ActivityKind.Haggle => "haggling",
            ActivityKind.LightLanterns => "lighting the lanterns", ActivityKind.NightRound => "walking the night round", ActivityKind.RingBell => "ringing the bell",
            ActivityKind.Bless => "blessing the harvest", ActivityKind.SitWithOld => "sitting with the old", ActivityKind.LookAtGlass => "looking up at the glass",
            ActivityKind.PacePlans => "pacing with a roll of plans", ActivityKind.OpenSails => "opening the sails", ActivityKind.CarryTables => "carrying tables out",
            ActivityKind.Meeting => "at the guild meeting", ActivityKind.Mediate => "stepping between two who are arguing", ActivityKind.Service => "leading the gathering",
            ActivityKind.Chat => "chatting", ActivityKind.Argue => "arguing", ActivityKind.Court => "walking out", ActivityKind.SitTogether => "sitting together",
            ActivityKind.Sing => "singing", ActivityKind.Drink => "having a drink", ActivityKind.Play => "playing", ActivityKind.Swim => "splashing in the water",
            ActivityKind.Snowball => "throwing snowballs", ActivityKind.Shop => "shopping", ActivityKind.WatchWork => "watching the work",
            ActivityKind.Visit => "visiting", ActivityKind.Dance => "dancing", ActivityKind.Cheer => "cheering", ActivityKind.Procession => "walking in the procession",
            ActivityKind.Search => "searching with a lantern", ActivityKind.Explore => "wandering the hills", ActivityKind.Shelter => "sheltering from the weather",
            ActivityKind.Wave => "waving", ActivityKind.LookUp => "looking up", ActivityKind.LookUpWary => "peering up warily", ActivityKind.Clap => "clapping",
            ActivityKind.Point => "pointing", ActivityKind.RunOver => "running over", ActivityKind.JumpAndPoint => "jumping and pointing",
            ActivityKind.HandsOut => "holding out their hands", ActivityKind.Splash => "splashing", ActivityKind.Study => "studying the glass",
            ActivityKind.Gaze => "looking up", ActivityKind.GazeWary => "looking up from a step back", ActivityKind.PressHands => "pressing their hands to it",
            ActivityKind.WaveWary => "waving, a little warily", ActivityKind.Flinch => "flinching", ActivityKind.Peek => "peeking up",
            ActivityKind.CreepUp => "creeping closer", ActivityKind.HopBack => "hopping back", ActivityKind.LaughPoint => "laughing and pointing",
            ActivityKind.Measure => "measuring", ActivityKind.TapBack => "knocking back", ActivityKind.WriteDown => "writing it down",
            ActivityKind.TurnLens => "turning the lens", ActivityKind.LookThroughLens => "looking through the lens", ActivityKind.Queue => "waiting for a turn",
            ActivityKind.Freeze => "frozen to the spot", ActivityKind.StepBack => "stepping back", ActivityKind.Watch => "watching",
            ActivityKind.Poke => "poking it with a stick", ActivityKind.Taste => "tasting it", ActivityKind.Deliver => "setting something down",
            ActivityKind.SetUp => "setting it up", ActivityKind.Bow => "bowing", ActivityKind.CopyPose => "copying the pose",
            ActivityKind.PickUp => "picking something up", ActivityKind.HoldUp => "holding it up", ActivityKind.Give => "handing it over",
            ActivityKind.PutOn => "putting on a sandal", ActivityKind.LopsidedDance => "dancing, a little lopsided",
            _ => "busy",
        };

        static string PropNoun(Prop p) => p switch
        {
            Prop.Log => "logs", Prop.Handcart => "a handcart", Prop.Sheaf => "sheaves", Prop.Basket => "a basket", Prop.Hide => "a hide",
            Prop.FishStick => "fish", Prop.Pots => "pots", Prop.Block => "a stone block", Prop.Cloth => "cloth", Prop.Loaves => "loaves",
            Prop.Goods => "goods", Prop.Sack => "flour sacks", Prop.Honey => "honey", Prop.Wool => "wool", Prop.Lamb => "a lamb",
            Prop.Buckets => "water", Prop.Spear => "new tools", Prop.SeedBag => "seed baskets", Prop.Barrel => "a barrel", _ => "things",
        };

        static string RelicNoun(int index) => index switch
        {
            0 => "the sky bread", 1 => "the dance", 2 => "the Keeper doll", 3 => "the old gathering ground", 4 => "the great eye-glass", _ => "the relic",
        };

        string PlaceNoun(Place p) => p.kind switch
        {
            PlaceKind.Home => "home",
            PlaceKind.Fire => "the fire",
            PlaceKind.Green => "the green",
            PlaceKind.Market => "the market",
            PlaceKind.Field => era == Era.Camp ? "the gathering grounds" : "the field",
            PlaceKind.LumberMill => era == Era.Camp ? "the woodpile" : "the lumber mill",
            PlaceKind.Windmill => "the windmill",
            PlaceKind.Tavern => era == Era.Town ? "the inn" : "the tavern",
            PlaceKind.Shrine => "the shrine",
            PlaceKind.Workshop => "the workshop",
            PlaceKind.Guildhall => "the guildhall",
            PlaceKind.BuildingSite => "the building site",
            PlaceKind.ForestEdge => "the forest edge",
            PlaceKind.ClayBank => "the clay bank",
            PlaceKind.TrashHeap => "a trash heap",
            PlaceKind.Nudge => "the Keeper's gift",
            PlaceKind.Remembering => p.index == 1 ? "the churchyard" : "the cairn",
            PlaceKind.Hill => "the hill",
            PlaceKind.Glass => belief <= 1 ? "the edge of the sky" : belief == 2 ? "the window" : beliefLater ? "the glass" : "the wall",
            PlaceKind.Relic => RelicNoun(p.index),
            _ => "the " + p.kind.ToString().ToLowerInvariant(),
        };
    }
}
