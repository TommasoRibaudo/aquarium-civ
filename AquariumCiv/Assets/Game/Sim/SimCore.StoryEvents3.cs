using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #722 batch 3 (docs/design/new-events.md §1.4, §2.1-2.4, §3.2, §3.3; animation-specs B80.b-h): the sim side of the last story events.
    /// <list type="bullet">
    /// <item>1.4 the antlers passed: a new leader takes the seat while the antlers hang (<see cref="AntlersAfterSeat"/>, from the role line, so its
    /// Story moment queues right after the new-leader moment); the antlers go to the new door (<see cref="StoryMarks.antlersAtId"/>), the old
    /// post stays empty (<see cref="StoryMarks.antlersFromId"/>). Later passes are silent.</item>
    /// <item>2.1 the tinkerer's bucket, 2.3 the gardener's flowers, 2.4 the joker's face: once a box, the first grown-up with the trait, gated
    /// on the thing (open water, the name stone, the woodpile).</item>
    /// <item>3.2 the dry spell: the first one (<see cref="ThirstyBox"/>) with the Keeper's tree planted names who carries it water; in every later
    /// dry spell they do again (a view life use), after them their child. It changes no sim numbers.</item>
    /// <item>3.3 the first snow: the old "snow" line (SimCore.Story.cs) says the spec's line with a kid and is a Story moment; its snow figure
    /// stands until the thaw.</item>
    /// </list>
    /// At most one new event a game year (the story step; the snow and the seat wait for a free year). No dice. All text is [est].
    /// </summary>
    public partial class SimCore
    {
        /// <summary>#687: the share of the grass thirsty before the people look up for rain; 3.2: a dry spell (the view's CampLife.ThirstyBox).</summary>
        public const float ThirstyBox = 0.5f;

        /// <summary>3.2: most of the grass is thirsty now.</summary>
        public bool InDrySpell => EcoThirst >= ThirstyBox;

        void StoryStep3()
        {
            if (TryTinkererBucket(false) != null) return;
            if (TryGardenerFlowers(false) != null) return;
            if (TryJokerFace(false) != null) return;
            TryDrySpell(false);
        }

        /// <summary>The first living grown-up with this trait (the spec: "the first grown-up with that trait").</summary>
        Notable FirstWithTrait(string trait) => State.notables.Where(n => n.alive && n.age >= AdultAge && n.trait == trait).OrderBy(n => n.id).FirstOrDefault();

        /// <summary>The eldest living grown-up child of <paramref name="n"/>, or null.</summary>
        Notable EldestChild(Notable n) => n == null ? null
            : State.notables.Where(c => c.alive && c.parentId == n.id && c.age >= AdultAge).OrderByDescending(c => c.age).ThenBy(c => c.id).FirstOrDefault();

        /// <summary>Who keeps a thing a person left: them while they live, else their eldest grown-up child (a generation at a time), else null.</summary>
        Notable KeeperOf(int id)
        {
            var n = FindNotable(id);
            for (int depth = 0; n != null && !n.alive && depth < 8; depth++) n = EldestChild(n);
            return n != null && n.alive ? n : null;
        }

        // ---------- 1.4 the antlers passed ----------

        /// <summary>The antlers hang (the first stag's or the white stag's).</summary>
        public bool AntlersHang => DeerAntlersUp || WhiteStagAntlers;

        /// <summary>Whose door the antlers hang at: the one they passed to while they live, else the leader (as before the first pass).</summary>
        public Notable AntlersDoor => Alive(StoryMarks.antlersAtId) ?? HolderOf(LeaderOf(State.era)) ?? HolderOf(RoleType.Chief);

        /// <summary>From <see cref="RoleLine"/>: <paramref name="nu"/> took the leader's seat. With the antlers up they go from the old door to the
        /// new one; the first pass (in a year with no other new event) is the event: its line queues a Story moment after the new-leader moment.</summary>
        void AntlersAfterSeat(Notable nu, RoleSeat seat, bool force = false)
        {
            if (nu == null || !AntlersHang) return;
            var m = StoryMarks;
            int from = m.antlersAtId >= 0 ? m.antlersAtId : seat?.lastHolderId ?? -1;
            if (from < 0 || from == nu.id) return;
            m.antlersFromId = from; m.antlersAtId = nu.id;
            if (StoryHappened(StoryEvent.AntlersPassed) || !force && StoryFirstThisYear()) return; // a later pass, or the year's slot is taken: silent
            StoryLine(StoryEvent.AntlersPassed, $"The antlers on the post went to {nu.name}, with the seat.", 0.65f, nu.id, from);
        }

        /// <summary>The role lines a seat change of the era's leader writes (SeatsALeader's and the era's own).</summary>
        static bool LeaderSeatLine(string kind) => MomentRules.SeatsALeader(kind) || kind == "role_era";

        // ---------- 2.1 the tinkerer's bucket ----------

        ChronicleEntry TryTinkererBucket(bool force, Notable who = null)
        {
            if (StoryHappened(StoryEvent.TinkererBucket) || StoryMarks.bucketOwnerId >= 0) return null;
            if (!PondFetched) return null; // people fetch water from the pond (its scene is at the pond's edge)
            var t = who ?? FirstWithTrait("the tinkerer");
            if (t == null) return null;
            StoryMarks.bucketOwnerId = t.id;
            return StoryLine(StoryEvent.TinkererBucket, $"{t.name} made a bucket with a spout. It poured to the left, so everyone stood to the right.", 0.6f, t.id, -1);
        }

        /// <summary>2.1's gate: the living land's pond holds water, so people fetch water from it (the slice's stream has no edge to stage at).</summary>
        public bool PondFetched => EcologyOn && EcoPondCells > 0;

        /// <summary>2.1 "Stays": who carries the crooked bucket now (the tinkerer, after them their eldest child), -1 none.</summary>
        public int SpoutBucketUserId => StoryMarks.bucketOwnerId < 0 ? -1 : KeeperOf(StoryMarks.bucketOwnerId)?.id ?? -1;

        // ---------- 2.3 the gardener's flowers ----------

        ChronicleEntry TryGardenerFlowers(bool force, Notable who = null)
        {
            if (StoryHappened(StoryEvent.GardenerFlowers) || StoryMarks.flowersStoneId >= 0) return null;
            var stone = FirstNameStone;
            if (stone == null) return null;
            var g = who ?? FirstWithTrait("the gardener");
            if (g == null) return null;
            StoryMarks.flowersStoneId = stone.id; StoryMarks.gardenerId = g.id;
            return StoryLine(StoryEvent.GardenerFlowers, $"{g.name} planted flowers round the name stone, and someone watered them every morning.", 0.6f, g.id, -1);
        }

        /// <summary>2.3 "Stays": the ring of flowers round the name stone (the stone still stands).</summary>
        public bool FlowersAtStone => StoryMarks.flowersStoneId >= 0 && FindConsequenceProp(StoryMarks.flowersStoneId) is ConsequenceProp p && p.placed;

        // ---------- 2.4 the joker's face ----------

        /// <summary>The woodpile under its roof, standing (a choice's thing), or null.</summary>
        public ConsequenceProp WoodpileProp => WoodpileStands ? ConsequenceProps.FirstOrDefault(p => p.kind == ConsequenceKind.WoodpileRoof && p.placed) : null;

        ChronicleEntry TryJokerFace(bool force, Notable who = null)
        {
            if (StoryHappened(StoryEvent.JokerFace) || StoryMarks.faceLogPropId >= 0) return null;
            var pile = WoodpileProp;
            if (pile == null) return null;
            var j = who ?? FirstWithTrait("the joker");
            if (j == null) return null;
            StoryMarks.faceLogPropId = pile.id; StoryMarks.jokerId = j.id;
            return StoryLine(StoryEvent.JokerFace, $"{j.name} carved a grinning face into a log on the woodpile. Nobody would burn it.", 0.55f, j.id, -1);
        }

        /// <summary>2.4 "Stays": the face log on the woodpile (the pile still stands).</summary>
        public bool FaceLogOn => StoryMarks.faceLogPropId >= 0 && WoodpileProp is ConsequenceProp p && p.id == StoryMarks.faceLogPropId;

        // ---------- 3.2 the dry spell ----------

        /// <summary>The Keeper's tree is planted (any stage).</summary>
        public bool KeeperTreePlanted => EcologyOn && State.eco.treeCell >= 0;

        ChronicleEntry TryDrySpell(bool force)
        {
            if (StoryHappened(StoryEvent.DrySpell) || StoryMarks.treeWatererId >= 0) return null;
            if (!KeeperTreePlanted || (!force && !InDrySpell)) return null;
            // a grown-up under 50, by the year (no dice); the eldest grown-up if all are older
            var adults = State.notables.Where(n => n.alive && n.age >= AdultAge && n.age < 50f).OrderBy(n => n.id).ToList();
            var who = adults.Count > 0 ? adults[(int)(RitualYear % adults.Count)]
                : State.notables.Where(n => n.alive && n.age >= AdultAge).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
            who = DebugPick(State.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.id), who);
            if (who == null) return null;
            StoryMarks.treeWatererId = who.id;
            return StoryLine(StoryEvent.DrySpell, $"The grass went brown in the dry spell. {who.name} carried water to the Keeper's tree every evening.", 0.6f, who.id, -1);
        }

        /// <summary>3.2 "Stays": who carries water to the Keeper's tree in a dry spell now (the first one named, after them their eldest child).</summary>
        public Notable TreeWaterer => StoryMarks.treeWatererId < 0 ? null : KeeperOf(StoryMarks.treeWatererId);

        /// <summary>3.2 "Read later": the person card's line ("" none).</summary>
        string WatererCardLine(Notable n) => n == null || StoryMarks.treeWatererId < 0 ? ""
            : n.alive && TreeWaterer == n ? "Waters the Keeper's tree in a dry spell."
            : !n.alive && StoryMarks.treeWatererId == n.id ? "Watered the Keeper's tree in the dry spells." : "";

        // ---------- 3.3 the first snow ----------

        /// <summary>The kid who catches the first flake: a kid of 4 or more, by the year (no dice), or null.</summary>
        Notable SnowKid()
        {
            var kids = State.notables.Where(n => n.alive && n.age >= 4f && n.age < AdultAge).OrderBy(n => n.id).ToList();
            return kids.Count == 0 ? null : DebugPick(kids, kids[(int)((RitualYear + 1) % kids.Count)]);
        }

        /// <summary>From the story stream (SimCore.Story.cs, its dice unchanged): the box's first snow. With a kid it is the event (the spec's
        /// line, a Story moment); with none it keeps a plain line, a card (no one to stage it).</summary>
        ChronicleEntry FirstSnowLine()
        {
            State.storyFlags.Add(FlagSnow);
            var kid = SnowKid();
            StoryMarks.snowFigureMonth = State.month;
            if (kid == null) return Chronicle(StoryEvent.SnowKind, "The first snow came. " + SnowKidClause(), 0.6f, -1, -1);
            return StoryLine(StoryEvent.FirstSnow, $"The first snow came. {kid.name} caught a flake on their tongue, and the others copied.", 0.6f, kid.id, -1);
        }

        /// <summary>3.3 "Stays": the snow figure by the fire, from the first snow until the thaw (the end of that winter).</summary>
        public bool SnowFigureStands
        {
            get
            {
                long m = StoryMarks.snowFigureMonth;
                return m >= 0 && State.month >= m && State.month - m <= 3 && Eco.PondIce.WinterMonth(CalendarMonth);
            }
        }

        // ---------- 2.2 the song rides the ritual's moment ----------

        /// <summary>B80.d: the line a held moment's caption says for its ritual line: the singer's song made at it (its first year, while the song
        /// has no name), else null. Once the Keeper has named it (4.3) the name wins: the caption is <see cref="RitualCaption"/>'s
        /// "The harvest feast: The Long Table", that first year too.</summary>
        public string SongLineOn(int ritualEntryId)
        {
            var m = StoryMarks;
            if (m.songRideId < 0 || m.songRideId != ritualEntryId || !string.IsNullOrEmpty(m.songName)) return null;
            for (int i = State.chronicle.Count - 1; i >= 0; i--)
                if (State.chronicle[i].id == m.songLineId) return State.chronicle[i].text;
            return null;
        }

        /// <summary>B80.d: the singer who leads this ritual's song (its maker, alive), or null: they get the notes first every year.</summary>
        public Notable SongSingerOf(string ritual) => ritual != null && StoryMarks.songRitual == ritual ? Alive(StoryMarks.songOwnerId) : null;

        /// <summary>The line whose held moment shows <paramref name="e"/>: the song's line rides its ritual's (B80.d); every other line its own.</summary>
        int HeldEntryOf(ChronicleEntry e)
        {
            var m = State.storyMarks;
            return m != null && m.songLineId >= 0 && e.id == m.songLineId && m.songRideId >= 0 ? m.songRideId : e.id;
        }

        // ---------- debug (SimDebugPanel's "Stories (#722)" block) ----------

        /// <summary>A grown-up with <paramref name="trait"/>: the first one, else the youngest grown-up becomes one (a cheat); with
        /// <see cref="DebugCastable"/> set, one the view can cast now (they take the trait). Null: no grown-up.</summary>
        Notable DebugTrait(string trait)
        {
            var n = FirstWithTrait(trait);
            if (n != null && DebugOk(n)) return n;
            var grown = State.notables.Where(x => x.alive && x.age >= AdultAge).OrderBy(x => x.age).ThenBy(x => x.id).ToList();
            var pick = grown.FirstOrDefault(DebugOk) ?? n ?? grown.FirstOrDefault();
            if (pick != null) pick.trait = trait;
            return pick;
        }

        /// <summary>Debug (the antlers' cheat): <paramref name="n"/>'s household gets a home of its own: a new tent (a home in the camp) is put
        /// up and given to them. False when no home could be placed.</summary>
        bool DebugOwnHome(Notable n)
        {
            var st = Settlement;
            if (st == null || n == null) return false;
            var mine = st.HomeOf(n);
            DebugBuild(BuildingType.Hut);
            Lot fresh = null;
            foreach (var l in st.Homes()) if (!ReferenceEquals(l, mine) && !State.settle.homes.Exists(h => h.lot == l.id) && (fresh == null || l.id > fresh.id)) fresh = l;
            if (fresh == null) return false;
            int head = st.HouseholdHead(n).id;
            int i = State.settle.homes.FindIndex(h => h.head == head);
            if (i >= 0) State.settle.homes[i].lot = fresh.id; else State.settle.homes.Add(new HomeAssign { head = head, lot = fresh.id });
            return true;
        }

        /// <summary>Debug: a batch-3 event happens now, its gate eased where a cheat must (a trait given, the antlers hung, the seat passed, the
        /// grass dried, the season). The line, or null and why. Its mark is forgotten first so the cheat can play it again.</summary>
        ChronicleEntry DebugStory3(string id, out string why)
        {
            why = "";
            var m = StoryMarks;
            switch (id)
            {
                case StoryEvent.AntlersPassed:
                {
                    if (!EcologyOn) { why = "no living land (the antlers come from the deer hunt)"; return null; }
                    if (!AntlersHang) State.eco.antlersMonth = State.month; // the antlers go up now
                    var seat = SeatOf(LeaderOf(State.era));
                    if (seat == null) { why = "no leader's seat"; return null; }
                    var adults = State.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.id).ToList();
                    // a cheat (DebugCastable set): two the view can cast now, each with a home of their own (the antlers go door to door)
                    bool cheat = DebugCastable != null;
                    var home = Settlement;
                    bool Home(Notable n) => home != null && home.HomeOf(n) != null;
                    var old0 = Alive(m.antlersAtId) ?? Alive(seat.holderId) ?? adults.FirstOrDefault();
                    var old = !cheat ? old0 : new[] { old0 }.Concat(adults).Where(n => n != null).OrderByDescending(n => DebugOk(n) && Home(n)).ThenByDescending(DebugOk).FirstOrDefault();
                    bool OtherHome(Notable n) => Home(n) && !ReferenceEquals(home.HomeOf(n), home.HomeOf(old));
                    var nu = adults.Where(n => n != old).OrderByDescending(n => RoleOf(n) == null).ThenByDescending(n => !cheat || n.partnerId != old.id && old.partnerId != n.id).ThenByDescending(n => !cheat || OtherHome(n)).ThenByDescending(DebugOk)
                        .ThenByDescending(n => n.age < Config.retireAge).ThenBy(n => n.id).FirstOrDefault();
                    if (old == null || nu == null) { why = "two grown-ups needed"; return null; }
                    if (cheat && home != null && (!Home(old) || !OtherHome(nu))) // the gate made true: the two doors are two homes
                    {
                        if (!Home(old)) DebugOwnHome(old);
                        if (!OtherHome(nu)) DebugOwnHome(nu);
                        if (!Home(old) || !OtherHome(nu)) { why = "no room for a second home (the antlers go door to door)"; return null; }
                    }
                    if (seat.holderId != old.id) { if (seat.holderId >= 0) Vacate(seat); Assign(seat, old); }
                    m.antlersAtId = old.id;
                    // the seat passes now, with no role line: the cheat plays the antlers alone (in play they follow the new-leader moment)
                    Vacate(seat); Assign(seat, nu);
                    AntlersAfterSeat(nu, seat, true);
                    var e = State.chronicle.LastOrDefault(x => x.kind == StoryEvent.Kind(id));
                    if (e == null || !StoryHappened(id)) { why = "the antlers didn't pass"; return null; }
                    return e;
                }
                case StoryEvent.TinkererBucket:
                {
                    if (!PondFetched) { why = "no water in the pond (the pond first)"; return null; }
                    var who = DebugTrait("the tinkerer");
                    if (who == null) { why = "no grown-up"; return null; }
                    m.bucketOwnerId = -1;
                    var e = TryTinkererBucket(true, who);
                    if (e == null) why = "the bucket wasn't made";
                    return e;
                }
                case StoryEvent.GardenerFlowers:
                {
                    if (FirstNameStone == null) { why = "no name stone stands (answer 'camp_remember' with the stone first)"; return null; }
                    var who = DebugTrait("the gardener");
                    if (who == null) { why = "no grown-up"; return null; }
                    m.flowersStoneId = -1;
                    var e = TryGardenerFlowers(true, who);
                    if (e == null) why = "the flowers weren't planted";
                    return e;
                }
                case StoryEvent.JokerFace:
                {
                    if (!WoodpileStands) DebugFirstWildWoodNow(); // a living land's woodpile comes with its first wild wood
                    if (WoodpileProp == null) { why = "no woodpile stands (answer 'camp_winter' with the woodpile roof first)"; return null; }
                    var who = DebugTrait("the joker");
                    if (who == null) { why = "no grown-up"; return null; }
                    m.faceLogPropId = -1;
                    var e = TryJokerFace(true, who);
                    if (e == null) why = "the face wasn't carved";
                    return e;
                }
                case StoryEvent.DrySpell:
                {
                    if (!KeeperTreePlanted) { why = "the Keeper's tree isn't planted"; return null; }
                    DebugDrySpell(); // the "Dry spell now" cheat: the grass goes brown
                    m.treeWatererId = -1;
                    var e = TryDrySpell(true);
                    if (e == null) why = "no grown-up to carry the water";
                    return e;
                }
                case StoryEvent.FirstSnow:
                {
                    State.storyFlags.Remove(FlagSnow);
                    if (SnowKid() == null) { why = "no kid in the box ('Two kids join the camp' first)"; return null; }
                    return FirstSnowLine();
                }
            }
            why = "unknown event " + id;
            return null;
        }
    }
}
