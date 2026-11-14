using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Life
{
    /// <summary>The activity tables (life-in-the-box §4 jobs and roles, §5 the crowd's building slots, §7 care).</summary>
    public sealed partial class LifePlanner
    {
        /// <summary>A working day's loop: where, which pose and tool, and what gets carried where.</summary>
        struct Work
        {
            public Place site;
            public ActivityKind loop;
            public Prop tool;
            public Place altSite;
            public ActivityKind altLoop;
            public Prop altTool;
            public float altShare;
            public Prop load;
            public ActivityKind carryKind;
            public Place dest;
            public Place fetchFrom;     // set: the carry comes from here to dest (restocking, water)
            public float carryShare;
            public bool keepsRestDay, sweeps, chores;
            /// <summary>#405: the day's work starts with a fetch: to <see cref="fetchFrom"/> with the empty bucket, then the load to
            /// <see cref="dest"/>, before the first loop at the site (the cook draws water before stirring the pot).</summary>
            public bool fetchFirst;
            /// <summary>#543: the day's work starts at the fish rack: the dry fish taken down and carried to the pile, then the walk to the site.</summary>
            public bool takeDownFirst;
            public Person with;         // a work partner (the two-man saw)
        }

        Work W(Place site, ActivityKind loop, Prop tool, Prop load = Prop.None, Place dest = default, float carry = 0.3f)
            => new Work { site = site, loop = loop, tool = tool, load = load, dest = dest.IsNone ? site : dest, carryShare = carry, carryKind = ActivityKind.Carry };

        static Work Alt(Work w, Place site, ActivityKind loop, Prop tool, float share)
        {
            w.altSite = site; w.altLoop = loop; w.altTool = tool; w.altShare = share;
            return w;
        }

        Place Store(Prop load)
        {
            switch (load)
            {
                case Prop.Sheaf: return windmillB != null ? PlaceOf(windmillB) : stash;
                case Prop.Log: case Prop.Handcart: return lumberB != null ? PlaceOf(lumberB) : stash;
                case Prop.Block: return newestB != null && era == Era.Town ? new Place(PlaceKind.BuildingSite, newestB.id) : gather;
            }
            if (era == Era.Camp) return stash; // #150: set down by the fire, not on it
            return marketB != null ? PlaceOf(marketB) : gather;
        }

        static readonly Place Woods = Place.Land(PlaceKind.Woods), Pond = Place.Land(PlaceKind.Pond), ForestEdge = Place.Land(PlaceKind.ForestEdge),
            Bushes = Place.Land(PlaceKind.Bushes), Stream = Place.Land(PlaceKind.Stream);

        // ---- P8 (#285, #267): the land decides the work and the water
        bool OnLiving => sim != null && sim.EcologyOn;
        bool WoodsStand => !OnLiving || (sim.TreesGrow && sim.WoodsExist); // #573: a grown wild tree away from the Keeper's tree
        /// <summary>Where berries are gathered. On a living land (P8 #288): a real grown bush cell near the camp that has berries
        /// today (spread over the few nearest by the person's hash, <paramref name="salt"/> for a second spot); with none in
        /// fruit (spring flowers, winter bare, all picked), or before the first bush, <see cref="Place.None"/>: #656, no roots are
        /// dug, the caller finds the camp's other work (<see cref="CampOtherWork"/>). Off the ecology: the old fixed bushes spot.</summary>
        Place BerryPlaceFor(Person p, DayInfo d, int salt = 0)
        {
            if (!OnLiving) return Bushes;
            if (!sim.BerriesGrow) return Place.None;
            var g = sim.EcoGround;
            sim.NearestBushes(g.campX, g.campZ, 4, d.day, (int)d.season, true, bushScratch);
            if (bushScratch.Count == 0) return Place.None;
            return Place.Land(PlaceKind.BushCell, bushScratch[(int)(H(p.key, d.day, 76 + salt) % (uint)bushScratch.Count)]);
        }
        /// <summary>P8 #288: some days, in summer and autumn, a child eats from a bush that has berries instead of the late-morning play.</summary>
        Place KidBerryPlace(Person p, DayInfo d)
        {
            if (!OnLiving || !sim.BerriesGrow || (d.season != Season.Summer && d.season != Season.Autumn) || R(p.key, d.day, 77) >= 0.35f) return Place.None;
            var b = BerryPlaceFor(p, d, 2);
            return b.kind == PlaceKind.BushCell ? b : Place.None;
        }
        readonly List<int> bushScratch = new List<int>();

        /// <summary>The tribe's own work, for anyone whose job the land hasn't opened (or whose hunt isn't built yet): gathering
        /// berries and bringing them to the pile; with no berries to pick today, the camp's other work (#656: never a root dig).</summary>
        Work TribeWork(Person p, DayInfo d)
        {
            var b = BerryPlaceFor(p, d);
            return b.IsNone ? CampOtherWork(p, d) : W(b, ActivityKind.Gather, Prop.Basket, Prop.Basket, stash, 0.35f);
        }

        /// <summary>#656/#677: the fire as the land lets it burn (none, grass and twigs, logs). Off the living land, or an old box: logs.</summary>
        public CampFireStage FireNow => sim != null ? sim.FireStage : CampFireStage.Logs;

        /// <summary>#656/#677: tending the grass-and-twig fire: an armful of dry grass and twigs gathered at the meadow's edge (by the
        /// bushes) and carried to the fire. Never "chopping kindling", never wood. The view tells it by <see cref="IsTinderGather"/>.</summary>
        Work TinderWork() => W(Place.Land(PlaceKind.Meadow), ActivityKind.Gather, Prop.Stick, Prop.Stick, fire, 0.35f);

        /// <summary>#656/#677: a gather of dry grass and twigs for the grass fire (the loop at the meadow's edge; its carry is a
        /// <see cref="ActivityKind.Carry"/> of <see cref="Prop.Stick"/> to the fire).</summary>
        public static bool IsTinderGather(PersonActivity a) => a.kind == ActivityKind.Gather && a.prop == Prop.Stick;

        /// <summary>#521/#656: a camp's work when the land gives nothing to gather today (no bush yet, none in fruit, no fish): the cook tends
        /// the fire, the rest fetch water, fish, chop where the land allows it, tend the grass fire (gathering dry grass and twigs), or do the
        /// chores at their tent (hashed per person and day [est]). This was the "everyone else" of the root dig, which is gone (#656).</summary>
        Work CampOtherWork(Person p, DayInfo d)
        {
            if (p.job == CrowdCook && FireNow != CampFireStage.None)
            {
                var cook = W(fire, ActivityKind.Cook, Prop.Stick, Prop.Buckets, fire, 0.2f);
                cook.keepsRestDay = true;
                if (HasRealWater) { cook = Fetch(cook, WaterPlace); cook.dest = stash; cook.fetchFirst = true; }
                return cook;
            }
            float r = R(p.key, d.day, 149);
            bool water = FishingOn(d.day); // #421: fishing waits for the fish; #618: and for the thaw on a small pond
            if (water && r < 0.45f) return FishWork();
            if (WoodsStand && r < 0.8f) return W(Woods, ActivityKind.Chop, Prop.Axe, Prop.Log, stash, 0.3f);
            // #656: the rest share three things out by the day's order (not a dice each, so never most of the camp on one): the chores at
            // their tent and the water; the grass fire's dry grass and twigs (#677), or, with no fire yet, a look over the land for what it
            // may give; and a sit together where the camp is (with nothing to burn, they huddle).
            switch (IdleIndex(p, d) % 3)
            {
                case 1:
                    if (FireNow == CampFireStage.GrassAndTwig) return TinderWork(); // #677: the grass fire is fed by hand
                    if (OnLiving && era == Era.Camp) return W(Place.Land(PlaceKind.Hill), ActivityKind.Explore, Prop.None, Prop.None, default, 0f);
                    break;
                case 2:
                    if (OnLiving && era == Era.Camp) return W(gather, ActivityKind.SitOut, Prop.None, Prop.None, default, 0f);
                    break;
            }
            var c = W(p.home, ActivityKind.SweepStep, Prop.Broom, Prop.Buckets, p.home, 0.3f);
            c.chores = true;
            return Fetch(c, WaterPlace);
        }

        long idleDay = long.MinValue;
        readonly List<int> idleOrder = new List<int>();

        /// <summary>#656: the grown-ups' order for the day (the day's hash; stable for the day), for <see cref="CampOtherWork"/>'s share-out.</summary>
        int IdleIndex(Person p, DayInfo d)
        {
            if (idleDay != d.day)
            {
                idleDay = d.day; idleOrder.Clear();
                var order = new List<Person>();
                foreach (var q in people) if (q.Adult && q.job != JobType.Toolmaker && q.job != CrowdCook) order.Add(q);
                order.Sort((a, b) => { int c = H(a.key, d.day, 148).CompareTo(H(b.key, d.day, 148)); return c != 0 ? c : a.key.CompareTo(b.key); });
                foreach (var q in order) idleOrder.Add(q.key);
            }
            int i = idleOrder.IndexOf(p.key);
            return i < 0 ? (int)(H(p.key, d.day, 148) % 3u) : i;
        }

        /// <summary>P8 #288: the first bush is news for a month: one grown-up of the tribe walks over to look at it instead of
        /// their morning work. Stable for the day (the lowest hash), never the toolmaker.</summary>
        bool IsFirstBushLooker(Person p, DayInfo d)
        {
            if (!OnLiving || !p.Adult || p.job == JobType.Toolmaker || sim.FirstBushCell < 0 || !sim.FirstBushSignActive) return false;
            if (d.lookerDay != d.day)
            {
                d.lookerDay = d.day; d.lookerKey = int.MinValue;
                uint best = uint.MaxValue;
                foreach (var q in people)
                {
                    if (!q.Adult || q.job == JobType.Toolmaker) continue;
                    uint h = H(q.key, 0, 95);
                    if (h < best || (h == best && q.key < d.lookerKey)) { best = h; d.lookerKey = q.key; }
                }
            }
            return d.lookerKey == p.key;
        }
        /// <summary>Where the water is drawn: the well; else the pond once it has water; else the wet low ground (the stream bed).</summary>
        Place WaterPlace => wellB != null ? PlaceOf(wellB) : OnLiving && sim.OpenWater ? Pond : OnLiving ? PuddleOr(Stream) : Stream;
        /// <summary>Is there water to draw: a well, the pond with water, a drinkable puddle, or (off the living land) the stream.</summary>
        bool HasRealWater => wellB != null || !OnLiving || sim.OpenWater || (sim.EcoHasPuddles && sim.EcoNearestPuddle(0f, 0f, 99f) >= 0);
        /// <summary>P8 (#267/#266): with no pond, a drinkable mist puddle (the nearest to the camp) is where the muddy water is scooped; else the wet low ground.</summary>
        Place PuddleOr(Place fallback)
        {
            if (!sim.EcoHasPuddles) return fallback;
            int cell = sim.EcoNearestPuddle(0f, 0f, 99f);
            return cell >= 0 ? Place.Land(PlaceKind.Puddle, cell) : fallback;
        }
        /// <summary>What the bucket holds from <see cref="WaterPlace"/>: muddy from the wet low ground while the pond is dry.</summary>
        WaterKind WaterNow => wellB == null && OnLiving && !sim.OpenWater ? WaterKind.Muddy : WaterKind.Clean;

        /// <summary>The day's work for a working adult: the role's work first, then the job (notable-roles §2).</summary>
        Work WorkOf(Person p, DayInfo d, bool morning)
        {
            if (morning && IsFirstBushLooker(p, d))
                return W(Place.Land(PlaceKind.BushCell, sim.FirstBushCell), ActivityKind.Inspect, Prop.None, Prop.None, default, 0f);
            if (p.n != null && p.role != null)
            {
                var rw = RoleWork(p, d, morning);
                if (rw.HasValue) return rw.Value;
            }
            var jw = JobWork(p, d); // #656: no root dig, so nobody is spread over the stream bed any more (CampOtherWork)
            return OnLiving && !chopScan ? ChopSlots(p, d, jw) : jw;
        }

        // #573: at most two choppers to a grown wild tree (Place.Land(Woods, slot/2)); the rest go to the next tree, or to the tribe's work when the trees are taken.
        long chopDay = long.MinValue;
        bool chopScan;
        readonly System.Collections.Generic.List<int> chopCrew = new System.Collections.Generic.List<int>();
        public const int ChoppersPerTree = 2;
        Work ChopSlots(Person p, DayInfo d, Work w)
        {
            if (w.loop != ActivityKind.Chop || w.site.kind != PlaceKind.Woods) return w;
            if (chopDay != d.day)
            {
                chopDay = d.day; chopCrew.Clear();
                chopScan = true;
                try
                {
                    var order = new System.Collections.Generic.List<Person>();
                    foreach (var q in people) if (q.Adult) order.Add(q);
                    order.Sort((a, b) => a.key.CompareTo(b.key));
                    foreach (var q in order) { var qw = JobWork(q, d); if (qw.loop == ActivityKind.Chop && qw.site.kind == PlaceKind.Woods) chopCrew.Add(q.key); }
                }
                finally { chopScan = false; }
            }
            int slot = chopCrew.IndexOf(p.key);
            if (slot < 0) return w;
            if (slot >= sim.WoodsTreeCount * ChoppersPerTree)
            {   // every tree has its two: the rest do the chores at home (not all at one berry spot)
                var c = W(p.home, ActivityKind.SweepStep, Prop.Broom, Prop.Buckets, p.home, 0.3f);
                c.chores = true;
                return Fetch(c, WaterPlace);
            }
            w.site = Place.Land(PlaceKind.Woods, slot / ChoppersPerTree);
            return w;
        }

        Work JobWork(Person p, DayInfo d)
        {
            // P8, before the camp (keepers-hands §2.1): one work for everyone who works: the tribe's (berries once a bush has fruit,
            // else the camp's other work: #656, they live off the carried basket and never dig roots). The toolmaker still knaps by the fire.
            if (sim != null && sim.BeforeCamp && p.job != JobType.Toolmaker)
                return TribeWork(p, d);
            var season = d.season;
            bool camp = era == Era.Camp;
            var jb = p.jobBuilding != null ? PlaceOf(p.jobBuilding) : Place.None;
            Work w;
            switch (p.job)
            {
                case JobType.Gatherer:
                {
                    var site = !jb.IsNone ? jb : farms.Count > 0 ? PlaceOf(farms[(int)(H(p.key, 0, 72) % (uint)farms.Count)]) : BerryPlaceFor(p, d);
                    if (site.IsNone) return CampOtherWork(p, d); // #656: no berries to pick today (or the job came with the rabbits): no root dig
                    w = W(site, ActivityKind.Gather, Prop.Basket, Prop.Basket, Store(Prop.Basket));
                    if (season == Season.Autumn && WoodsStand) w = Alt(w, Woods, ActivityKind.Gather, Prop.Basket, 0.5f);       // mushrooms and nuts
                    else if (season == Season.Winter && WoodsStand) w = CarryLogs(w, Woods, stash, 0.4f); // wood carried in
                    else { var second = BerryPlaceFor(p, d, 1); if (!second.IsNone) w = Alt(w, second, ActivityKind.Gather, Prop.Basket, 0.4f); }
                    return w;
                }
                case JobType.Hunter:
                    if (sim != null && sim.EcologyOn) return TribeWork(p, d); // P8 (#285, #283): the job opens with the first rabbit; the hunt itself is planned in LifePlanner.Hunt.cs (HuntPlan) and plays over the day; between hunts: the tribe's work
                    return W(ForestEdge, ActivityKind.Stalk, Prop.Spear, Prop.Hide, stash, season == Season.Winter ? 0.35f : 0.25f);
                case JobType.Fisher:
                    if (sim != null && (!sim.FishingWater || PondIce.FishingStopped(sim, d.day))) // #618: a small frozen pond isn't fished; P8: no water yet (#421: or no fish placed)
                        return CampOtherWork(p, d); // #656: the camp's other work, not a root dig
                    return camp ? FishWork() : W(Pond, ActivityKind.Fish, Prop.Rod, Prop.Basket, Store(Prop.Goods), 0.25f); // #543: a camp's catch goes to the fish rack
                case JobType.Woodcutter:
                    if (sim != null && sim.EcologyOn && !WoodsStand) return TribeWork(p, d); // P8 (#285): no trees, no chopping (#573: a sapling or the Keeper's tree is not a wood)
                    w = W(Woods, ActivityKind.Chop, Prop.Axe, camp ? Prop.Log : Prop.Handcart, Store(Prop.Log), 0.3f);
                    if (lumberB != null) w = Alt(w, PlaceOf(lumberB), camp ? ActivityKind.StackWood : ActivityKind.Saw, camp ? Prop.Log : Prop.Saw, 0.35f);
                    return w;
                case JobType.Toolmaker:
                    w = W(fire, ActivityKind.Knap, Prop.Hammerstone, Prop.Spear, ForestEdge, 0.15f); // hands tools to the hunters
                    return w;
                case JobType.Farmer:
                {
                    var field = !jb.IsNone ? jb : farms.Count > 0 ? PlaceOf(farms[0]) : Bushes;
                    switch (season)
                    {
                        case Season.Spring: return Alt(W(field, ActivityKind.Sow, Prop.SeedBag, Prop.None, field, 0f), field, ActivityKind.Hoe, Prop.Hoe, 0.4f);
                        case Season.Summer: return Alt(W(field, ActivityKind.Weed, Prop.Hoe, Prop.None, field, 0f), field, ActivityKind.Hoe, Prop.Hoe, 0.3f);
                        case Season.Autumn: return W(field, ActivityKind.Harvest, Prop.Scythe, Prop.Sheaf, Store(Prop.Sheaf), 0.4f);
                        default: // winter: wood. #201: no hammer at a finished house, nothing here has a site under way to hammer at.
                            // #651 (tester): no wood on the land yet, no logs to fetch nor kindling to chop: the field's winter hoeing instead
                            if (!WoodsStand) return W(field, ActivityKind.Hoe, Prop.Hoe, Prop.None, field, 0f);
                            return Fetch(Alt(W(p.home, ActivityKind.StackWood, Prop.Log, Prop.Log, fire, 0.25f), p.home, ActivityKind.ChopKindling, Prop.Axe, 0.4f), Woods); // tester run 9: the axe is for chopping: stacking is logs
                    }
                }
                case JobType.Shepherd:
                    return W(Place.Land(PlaceKind.Pasture), ActivityKind.Herd, Prop.Crook,
                        season == Season.Spring ? Prop.Lamb : era == Era.Town ? Prop.Wool : Prop.None,
                        season == Season.Spring ? p.home : workshops.Count > 0 ? PlaceOf(workshops[0]) : Store(Prop.Goods), 0.15f);
                case JobType.Beekeeper:
                    return W(Place.Land(PlaceKind.Meadow), ActivityKind.TendHives, Prop.Hive, Prop.Honey, Store(Prop.Goods), 0.2f);
                case JobType.Potter:
                    w = W(Place.Land(PlaceKind.ClayBank), ActivityKind.ThrowPot, Prop.Pots, Prop.Pots, Store(Prop.Goods), 0.25f);
                    if (era == Era.Town) w = Alt(w, Place.Land(PlaceKind.Kiln), ActivityKind.Cook, Prop.Log, 0.3f); // feeding the kiln
                    return w;
                case JobType.Brewer:
                    w = W(p.home, ActivityKind.Brew, Prop.Barrel, Prop.Barrel, tavernB != null ? PlaceOf(tavernB) : gather, 0.35f);
                    w.carryKind = ActivityKind.RollBarrel;
                    return w;
                case JobType.Mason:
                    return W(Place.Land(PlaceKind.Outcrop), ActivityKind.CutStone, Prop.Pick, Prop.Block, Store(Prop.Block), 0.3f);
                case JobType.Smith:
                    return W(Place.Land(PlaceKind.Smithy), ActivityKind.Hammer, Prop.Hammer, Prop.Goods, workshops.Count > 0 ? PlaceOf(workshops[0]) : Store(Prop.Goods), 0.2f);
                case JobType.Weaver:
                {
                    var site = !jb.IsNone ? jb : workshops.Count > 0 ? PlaceOf(workshops[(int)(H(p.key, 0, 73) % (uint)workshops.Count)]) : p.home;
                    w = W(site, ActivityKind.Weave, Prop.Cloth, Prop.Cloth, Store(Prop.Cloth), 0.25f);
                    return Alt(w, site, ActivityKind.HangWashing, Prop.Cloth, 0.25f); // cloth drying on lines
                }
                case JobType.Baker:
                    return W(bakeryB != null ? PlaceOf(bakeryB) : p.home, ActivityKind.Knead, Prop.Loaves, Prop.Loaves, Store(Prop.Loaves), 0.25f);
                case JobType.Stallholder:
                {
                    var site = !jb.IsNone ? jb : marketB != null ? PlaceOf(marketB) : gather;
                    w = W(site, ActivityKind.MindStall, Prop.Goods, Prop.Goods, site, 0.15f);
                    w.keepsRestDay = true;
                    return Fetch(w, windmillB != null ? PlaceOf(windmillB) : p.home);
                }
                case JobType.Sweeper:
                {
                    // Town care (§7): mornings the streets (the trash heaps are gone, #313); afternoons the green and the vines.
                    bool morning = R(p.key, d.day, 80) < 0.5f || box.sweeperExtraBlocks > 0;
                    if (morning)
                    {
                        w = W(StreetOf(p, d.day), ActivityKind.Sweep, Prop.Broom, Prop.Handcart, Place.Land(PlaceKind.Midden), 0.35f);
                        w.sweeps = true;
                        return Alt(w, gather, ActivityKind.Trim, Prop.Shears, box.sweeperExtraBlocks > 0 ? 0.1f : 0.3f);
                    }
                    return Alt(W(gather, ActivityKind.Trim, Prop.Shears, Prop.Basket, Place.Land(PlaceKind.Midden), 0.2f), p.home, ActivityKind.Trim, Prop.Shears, 0.4f);
                }
                // ---- crowd slot jobs
                case CrowdCook:
                    if (FireNow == CampFireStage.None) return CampOtherWork(p, d); // #656: no fire yet, no pot over it
                    w = W(fire, ActivityKind.Cook, Prop.Stick, Prop.Buckets, fire, 0.2f);
                    w.keepsRestDay = true;
                    if (!HasRealWater) return w; // verify run 2026-11-07: a living-land box with no well, pond or puddle has nothing to draw; the cook doesn't dip into dry grass
                    w = Fetch(w, WaterPlace);
                    w.dest = stash; // #150: the water too is set down by the fire
                    w.fetchFirst = true; // #405 (user, 2026-11-07): water from the well first, carried to the fire, then the pot
                    return w;
                case CrowdSplitter:
                    return Fetch(W(jb, ActivityKind.StackWood, Prop.Log, Prop.Log, jb, 0.3f), Woods);
                case CrowdSawyer:
                    w = W(jb, ActivityKind.Saw, Prop.Saw, Prop.Handcart, newestB != null && era == Era.Town ? new Place(PlaceKind.BuildingSite, newestB.id) : Store(Prop.Goods), 0.1f);
                    w.with = p.coworker;
                    return Alt(w, jb, ActivityKind.StackWood, Prop.Log, p.coworker == null ? 0.5f : 0f);
                case CrowdMillhand:
                    return W(jb, ActivityKind.MillWork, Prop.Sack, Prop.Sack, marketB != null ? PlaceOf(marketB) : homes.Count > 0 ? PlaceOf(homes[(int)(H(p.key, d.day, 74) % (uint)homes.Count)]) : gather, 0.35f);
                case CrowdServer:
                    w = W(jb, ActivityKind.SweepStep, Prop.Broom, Prop.Barrel, jb, 0.3f);
                    w.carryKind = ActivityKind.RollBarrel;
                    w.keepsRestDay = true;
                    return Fetch(Alt(w, jb, ActivityKind.Serve, Prop.Mug, 0.4f), windmillB != null ? PlaceOf(windmillB) : p.home);
            }
            // No job site (a crowd helper, or a notable without work): chores at home, shopping, water; in the harvest,
            // everyone helps in the fields (§3 seasons). Never wandering (rule 2).
            if (season == Season.Autumn && era != Era.Camp && farms.Count > 0 && p.Adult)
                return W(PlaceOf(farms[(int)(H(p.key, d.day, 75) % (uint)farms.Count)]), ActivityKind.Harvest, Prop.Scythe, Prop.Sheaf, Store(Prop.Sheaf), 0.35f);
            if (sim != null && sim.NoWorkYet) return CampOtherWork(p, d); // #656: no work on the land yet (no root dig): the camp's other work, shared out
            var campHelp = CampHelperWork(p, d); // tester run 1 #3: a camp's helpers out on the land, not all sweeping at a few tents
            if (campHelp.HasValue) return campHelp.Value;
            w = W(p.home, ActivityKind.SweepStep, Prop.Broom, marketB != null ? Prop.Basket : Prop.Buckets, p.home, 0.3f);
            w.chores = true;
            return Fetch(w, marketB != null ? PlaceOf(marketB) : WaterPlace);
        }

        /// <summary>Where a sweeper sweeps the streets today: the door of one of the homes (hashed per person and day), so the
        /// sweepers spread through the town instead of all standing at the market; the gathering place with no homes.</summary>
        Place StreetOf(Person p, long day) => homes.Count > 0 ? PlaceOf(homes[(int)(H(p.key, day, 77) % (uint)homes.Count)]) : gather;

        /// <summary>#573 (reused by #578, travellers carrying logs): turns <paramref name="w"/> into "take logs from <paramref name="from"/> to <paramref name="to"/>
        /// (the stash)": the person works at <c>from</c>, the last <paramref name="carryShare"/> of the block carries a <see cref="Prop.Log"/> to <c>to</c>.
        /// The ground logs at a chopping spot are the view's (ChopLogsView); the pile at the stash grows with the stock.</summary>
        static Work CarryLogs(Work w, Place from, Place to, float carryShare = 0.4f)
        {
            w.site = from; w.carryShare = carryShare; w.load = Prop.Log; w.dest = to;
            return w;
        }

        static Work Fetch(Work w, Place from)
        {
            w.fetchFrom = from;
            w.dest = w.site;
            return w;
        }

        /// <summary>Roles whose seat is a workplace replace the job for part of the day (the miller's mill in the morning,
        /// the master builder at the site, the merchant at the big stall).</summary>
        Work? RoleWork(Person p, DayInfo d, bool morning)
        {
            switch (p.role)
            {
                case RoleType.Miller when morning && windmillB != null:
                    return W(PlaceOf(windmillB), ActivityKind.MillWork, Prop.Sack, Prop.Sack, marketB != null ? PlaceOf(marketB) : gather, 0.35f);
                case RoleType.MasterBuilder when morning:
                {
                    var site = newestB != null && sim.State.month - newestB.foundedMonth < 24 ? new Place(PlaceKind.BuildingSite, newestB.id)
                        : lumberB != null ? PlaceOf(lumberB) : gather;
                    return Alt(W(site, ActivityKind.Direct, Prop.MeasuringRod), site, ActivityKind.Inspect, Prop.MeasuringRod, 0.3f);
                }
                case RoleType.Merchant when marketB != null:
                {
                    var w = W(PlaceOf(marketB), ActivityKind.Haggle, Prop.CoinBag, Prop.Handcart, Place.Land(PlaceKind.Road), morning ? 0.2f : 0.5f);
                    w.keepsRestDay = true;
                    return w;
                }
                case RoleType.Innkeeper when tavernB != null && morning:
                {
                    var w = W(PlaceOf(tavernB), ActivityKind.SweepStep, Prop.Broom, Prop.Barrel, PlaceOf(tavernB), 0.4f);
                    w.carryKind = ActivityKind.RollBarrel;
                    return Fetch(w, windmillB != null ? PlaceOf(windmillB) : p.home);
                }
            }
            return null;
        }

        /// <summary>Role beats at fixed times (rule 7): the leader's evening speech, the captain's dusk lanterns, the bell at
        /// dawn, the lord's court, the headman's round of the fields...</summary>
        void RoleBeats(Person p, DayInfo d, List<Seg> list)
        {
            var src = ActivitySource.RoleBeat;
            var seatB = sim.SeatBuilding(p.role);
            var seat = seatB != null ? PlaceOf(seatB) : gather;
            bool leads = p == leader;
            switch (p.role)
            {
                case RoleType.Chief:
                    Put(list, Mk(ActivityKind.WalkRound, 7f, 8f, seat, Prop.None, src), d);
                    break;
                case RoleType.Headman:
                    if (farms.Count == 0) Put(list, Mk(ActivityKind.WalkRound, 7f, 8.5f, seat, Prop.None, src), d);
                    else
                    {
                        float step = Math.Min(0.6f, 2f / farms.Count), t = 7f;
                        foreach (var f in farms) { Put(list, Mk(ActivityKind.Inspect, t, t + step, PlaceOf(f), Prop.None, src), d); t += step; } // a nod at each field
                    }
                    break;
                case RoleType.Lord:
                {
                    float t = 7f;
                    if (marketB != null) { Put(list, Mk(ActivityKind.Inspect, t, t + 0.6f, PlaceOf(marketB), Prop.None, src), d); t += 0.6f; }
                    foreach (var w in workshops) { if (t >= 8.4f) break; Put(list, Mk(ActivityKind.Inspect, t, t + 0.5f, PlaceOf(w), Prop.None, src), d); t += 0.5f; }
                    Put(list, Mk(ActivityKind.HoldCourt, 9f, 11f, seat, Prop.Stool, src), d);
                    break;
                }
                case RoleType.Miller:
                    if (windmillB != null) Put(list, Mk(ActivityKind.OpenSails, 5.7f, 6.3f, PlaceOf(windmillB), Prop.None, src), d);
                    break;
                case RoleType.Priest:
                {
                    var shrine = churchB != null ? PlaceOf(churchB) : seat;
                    Put(list, Mk(ActivityKind.RingBell, 5.6f, 5.9f, shrine, Prop.Bell, src), d);
                    if (d.rest) Put(list, Mk(ActivityKind.Service, 8.6f, 10.5f, shrine, Prop.None, src), d);
                    else
                    {
                        Person old = null;
                        foreach (var q in people) if (q.stage == LifeStage.Elder && q != p) { old = q; if (R(q.key, d.day, 90) < 0.5f) break; }
                        if (old != null) Put(list, Mk(ActivityKind.SitWithOld, 9f, 10f, gather, Prop.None, src, old.who), d);
                        if (d.season == Season.Autumn && farms.Count > 0)
                            Put(list, Mk(ActivityKind.Bless, 15f, 15.6f, PlaceOf(farms[(int)(H(p.key, d.day, 91) % (uint)farms.Count)]), Prop.None, src), d);
                        if (R(p.key, d.day, 92) < 1f / 6f + p.hints.hillChance)
                            Put(list, Mk(ActivityKind.LookAtGlass, 16f, 16.7f, Place.Land(PlaceKind.Hill), Prop.None, src), d); // up the Keeper's hill
                    }
                    break;
                }
                case RoleType.Innkeeper:
                    if (tavernB != null)
                    {
                        var inn = PlaceOf(tavernB);
                        Put(list, Mk(ActivityKind.CarryTables, 17.6f, 18.3f, inn, Prop.Table, src), d); // more under the Keeper's lamp
                        Put(list, Mk(ActivityKind.Serve, 18.3f, 22f, inn, Prop.Mug, src), d);
                    }
                    break;
                case RoleType.Guildmaster:
                {
                    float t = 8f;
                    foreach (var w in workshops) { if (t >= 10f) break; Put(list, Mk(ActivityKind.Inspect, t, t + 0.5f, PlaceOf(w), Prop.None, src), d); t += 0.5f; }
                    if (t < 10f && marketB != null) Put(list, Mk(ActivityKind.Inspect, t, 10f, PlaceOf(marketB), Prop.None, src), d);
                    if (d.day % 3 == 0) Put(list, Mk(ActivityKind.Meeting, 16f, 17f, seat, Prop.None, src), d);
                    break;
                }
                case RoleType.Captain:
                    Put(list, Mk(ActivityKind.LightLanterns, 19.2f, 19.9f, gather, Prop.Lantern, src), d);
                    Put(list, Mk(ActivityKind.NightRound, 19.9f, 22f, gather, Prop.Lantern, src), d);
                    Put(list, Mk(ActivityKind.NightRound, 0.5f, 1.5f, gather, Prop.Lantern, src), d);
                    break;
            }
            if (leads)
            {
                Put(list, Mk(ActivityKind.Speech, 17.6f, 18.3f, gather, Prop.None, src), d); // the evening speech at the seat
                if (d.announce) Put(list, Mk(ActivityKind.Speech, 12.1f, 12.6f, gather, Prop.None, src), d);
            }
        }

        /// <summary>The heir trails the lord on the morning round (§4).</summary>
        void Trail(Person p, List<Seg> list)
        {
            foreach (var s in DayOf(p.trails))
            {
                if (s.end <= 7f || s.start >= 8.6f) continue;
                if (s.kind != ActivityKind.Inspect && s.kind != ActivityKind.Walk) continue;
                var c = s;
                c.partner = p.trails.who;
                c.copies = true;
                c.src = ActivitySource.RoleBeat;
                Put(list, c, dayInfo);
            }
        }
    }
}
