using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

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
            public Place fetchFrom;     // set: the carry comes from here back to the site (restocking, water)
            public float carryShare;
            public bool keepsRestDay, sweeps, chores;
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
                case Prop.Sheaf: return windmillB != null ? PlaceOf(windmillB) : fire;
                case Prop.Log: case Prop.Handcart: return lumberB != null ? PlaceOf(lumberB) : fire;
                case Prop.Block: return newestB != null && era == Era.Town ? new Place(PlaceKind.BuildingSite, newestB.id) : gather;
            }
            if (era == Era.Camp) return fire;
            return marketB != null ? PlaceOf(marketB) : gather;
        }

        static readonly Place Woods = Place.Land(PlaceKind.Woods), Pond = Place.Land(PlaceKind.Pond), ForestEdge = Place.Land(PlaceKind.ForestEdge),
            Bushes = Place.Land(PlaceKind.Bushes), Stream = Place.Land(PlaceKind.Stream);

        /// <summary>The day's work for a working adult: the role's work first, then the job (notable-roles §2).</summary>
        Work WorkOf(Person p, DayInfo d, bool morning)
        {
            if (p.n != null && p.role != null)
            {
                var rw = RoleWork(p, d, morning);
                if (rw.HasValue) return rw.Value;
            }
            return JobWork(p, d);
        }

        Work JobWork(Person p, DayInfo d)
        {
            var season = d.season;
            bool camp = era == Era.Camp;
            var jb = p.jobBuilding != null ? PlaceOf(p.jobBuilding) : Place.None;
            Work w;
            switch (p.job)
            {
                case JobType.Gatherer:
                {
                    var site = !jb.IsNone ? jb : farms.Count > 0 ? PlaceOf(farms[(int)(H(p.key, 0, 72) % (uint)farms.Count)]) : Bushes;
                    w = W(site, ActivityKind.Gather, Prop.Basket, Prop.Basket, Store(Prop.Basket));
                    if (season == Season.Autumn) w = Alt(w, Woods, ActivityKind.Gather, Prop.Basket, 0.5f);       // mushrooms and nuts
                    else if (season == Season.Winter) { w.site = Woods; w.carryShare = 0.4f; w.load = Prop.Log; w.dest = fire; } // wood carried in
                    else w = Alt(w, Bushes, ActivityKind.Gather, Prop.Basket, 0.4f);
                    return w;
                }
                case JobType.Hunter:
                    return W(ForestEdge, ActivityKind.Stalk, Prop.Spear, Prop.Hide, fire, season == Season.Winter ? 0.35f : 0.25f);
                case JobType.Fisher:
                    return W(Pond, ActivityKind.Fish, Prop.Rod, camp ? Prop.FishStick : Prop.Basket, camp ? fire : Store(Prop.Goods), 0.25f);
                case JobType.Woodcutter:
                    w = W(Woods, ActivityKind.Chop, Prop.Axe, camp ? Prop.Log : Prop.Handcart, Store(Prop.Log), 0.3f);
                    if (lumberB != null) w = Alt(w, PlaceOf(lumberB), camp ? ActivityKind.StackWood : ActivityKind.Saw, camp ? Prop.Axe : Prop.Saw, 0.35f);
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
                        default: // winter: wood, repairs
                            return Fetch(Alt(W(p.home, ActivityKind.Repair, Prop.Hammer, Prop.Log, fire, 0.25f), p.home, ActivityKind.ChopKindling, Prop.Axe, 0.4f), Woods);
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
                    // Town care (§7): mornings the streets and the trash heaps to the midden; afternoons the green and the vines.
                    bool morning = R(p.key, d.day, 80) < 0.5f || box.sweeperExtraBlocks > 0;
                    if (morning || trashList.Count > 0)
                    {
                        int first = SweepHeap(p, d.day, 0);
                        w = W(first >= 0 ? Place.Land(PlaceKind.TrashHeap, first) : gather, ActivityKind.Sweep, Prop.Broom, Prop.Handcart, Place.Land(PlaceKind.Midden), 0.35f);
                        w.sweeps = true;
                        return Alt(w, gather, ActivityKind.Trim, Prop.Shears, box.sweeperExtraBlocks > 0 ? 0.1f : 0.3f);
                    }
                    return Alt(W(gather, ActivityKind.Trim, Prop.Shears, Prop.Basket, Place.Land(PlaceKind.Midden), 0.2f), p.home, ActivityKind.Trim, Prop.Shears, 0.4f);
                }
                // ---- crowd slot jobs
                case CrowdCook:
                    w = W(fire, ActivityKind.Cook, Prop.Stick, Prop.Buckets, fire, 0.2f);
                    w.keepsRestDay = true;
                    return Fetch(w, wellB != null ? PlaceOf(wellB) : Stream);
                case CrowdSplitter:
                    return Fetch(W(jb, ActivityKind.StackWood, Prop.Axe, Prop.Log, jb, 0.3f), Woods);
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
            w = W(p.home, ActivityKind.SweepStep, Prop.Broom, marketB != null ? Prop.Basket : Prop.Buckets, p.home, 0.3f);
            w.chores = true;
            return Fetch(w, marketB != null ? PlaceOf(marketB) : wellB != null ? PlaceOf(wellB) : Stream);
        }

        /// <summary>The <paramref name="block"/>-th heap a sweeper sweeps today, or -1 for the streets. A town sweeper i takes
        /// <see cref="SimCore.SweeperTarget"/>(i) first, then every sweepersTotal-th heap after it, oldest first (one heap
        /// per sweeper, as the sim clears them); anyone else sweeping goes round the heaps in a hashed order.</summary>
        int SweepHeap(Person p, long day, int block)
        {
            if (trashList.Count == 0) return -1;
            if (p.sweeper < 0 || sweepOrder.Count == 0)
                return trashList[(int)((H(p.key, day, 77) + (uint)block) % (uint)trashList.Count)];
            if (block == 0 && p.sweeper < sweepTargets.Count) return sweepTargets[p.sweeper];
            long i = p.sweeper + (long)block * sweepersTotal;
            return i < sweepOrder.Count ? sweepOrder[(int)i] : -1;
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
