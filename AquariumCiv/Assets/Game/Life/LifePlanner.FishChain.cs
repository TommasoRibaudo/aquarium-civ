using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #543: the fish chain as the planner plays it. A fisher's catch lands in the fish basket beside them (<see cref="FishCaught"/>); the
    /// block's carry takes the basket to the camp's fish rack (<see cref="PlaceKind.FishRack"/>) and a <see cref="ActivityKind.HangFish"/>
    /// beat hangs the fish up (the sim moves them at the beat's end); a fish dries over one view day; the next working morning the
    /// day's rack-keeper walks to the rack first, takes the dry ones down (<see cref="ActivityKind.TakeDownFish"/>) and carries them to
    /// the food pile (the sim moves them when the carry ends). The views read <see cref="TryGetFishRack"/> and <see cref="CampStash"/>.
    /// Duties are found in the people's planned days (so they follow whatever the day's layers did) and settled by <see cref="ResolveFishChain"/>,
    /// which the view calls every frame; every settle is a safe move of a count (SimCore.FishChain.cs), so a reload that settles one again
    /// moves nothing.
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>The hang beat at the rack (real seconds, as the tip's), and the take-down's (view hours).</summary>
        public const float HangBlockSeconds = 3.2f, TakeDownHours = 0.3f;

        /// <summary>The camp's fish chain runs on a living land's camp: the rack stands by the fire there (no ecology, no rack: the old fish stick to the stash).</summary>
        bool FishRackOn => era == Era.Camp && sim != null && sim.EcologyOn;
        static readonly Place FishRackPlace = Place.Land(PlaceKind.FishRack);
        /// <summary>What a camp fisher carries from the pond, and where.</summary>
        Prop FishCarry => FishRackOn ? Prop.FishBasket : Prop.FishStick;
        Place FishDest => FishRackOn ? FishRackPlace : stash;

        /// <summary>The camp fisher's work: the pond, the rod, the catch carried to the fish rack (the stash where there is no rack).</summary>
        Work FishWork() => W(Pond, ActivityKind.Fish, Prop.Rod, FishCarry, FishDest, 0.25f);

        // ---------------------------------------------------------------- the day's rack-keeper

        /// <summary>The one grown-up who walks to the rack first thing on a working morning to take the dry fish down (hashed per day; never the
        /// toolmaker nor the cook). Null when nobody qualifies.</summary>
        Person RackKeeper(DayInfo d)
        {
            Person best = null; uint bestH = uint.MaxValue;
            foreach (var q in people)
            {
                if (!q.Adult || q.job == JobType.Toolmaker || q.job == CrowdCook) continue;
                uint h = H(q.key, d.day, 543);
                if (best == null || h < bestH || (h == bestH && q.key < best.key)) { best = q; bestH = h; }
            }
            return best;
        }

        /// <summary>Does this person take the dry fish down first thing today?</summary>
        bool TakesDownFish(Person p, DayInfo d, Work work)
            => FishRackOn && !work.fetchFirst && sim.FishDryOn(d.day) > 0 && RackKeeper(d) == p;

        /// <summary>The walk to the rack, the take-down, the dry fish carried to the pile; the next walk (to work) starts from the pile.</summary>
        void TakeDownFirst()
        {
            var rack = FishRackPlace;
            At(ActivityKind.TakeDownFish, bt + WalkHours(bcur, rack) + TakeDownHours, rack, Prop.None, default, TripType.HomeToWork);
            if (bcur == rack) Move(ActivityKind.Carry, stash, bt + Math.Max(0.3f, WalkHours(rack, stash)), Prop.FishStick, TripType.Carry);
        }

        float TakeDownFirstHours(Place from, Work w)
            => WalkHours(from, FishRackPlace) + TakeDownHours + Math.Max(0.3f, WalkHours(FishRackPlace, stash)) + WalkHours(stash, w.site);

        // ---------------------------------------------------------------- the duties

        sealed class FishDuty
        {
            public bool hang;
            public PersonRef who;
            public double startAbs, beatEndAbs, settleAbs;
            public bool started, settled, debug;
            public int n;
            public long day;
        }

        readonly List<FishDuty> fishDuties = new List<FishDuty>();
        readonly List<FishDuty> debugFishDuties = new List<FishDuty>();
        long fishDutiesDay = long.MinValue;
        /// <summary>Fish each fisher has landed and not yet hung (the fish basket beside them; a reload forgets it and the first hang takes the sim's whole basket).</summary>
        readonly Dictionary<int, int> fishCarried = new Dictionary<int, int>();

        /// <summary>A fisher's catch landed in their fish basket (the view's flying fish arrived). False when the basket was full.</summary>
        public bool FishCaught(int personKey)
        {
            Sync();
            if (!FishRackOn || !sim.FishToBasket()) return false;
            fishCarried.TryGetValue(personKey, out int c);
            fishCarried[personKey] = c + 1;
            return true;
        }

        void BuildFishDuties(long day)
        {
            fishDutiesDay = day;
            fishDuties.Clear();
            foreach (var p in people)
            {
                var segs = DayOf(p);
                for (int i = 0; i < segs.Length; i++)
                {
                    var s = segs[i];
                    if (s.kind == ActivityKind.HangFish)
                        fishDuties.Add(new FishDuty { hang = true, who = p.who, day = day, startAbs = day * 24.0 + s.start, beatEndAbs = day * 24.0 + s.end, settleAbs = day * 24.0 + s.end });
                    else if (s.kind == ActivityKind.TakeDownFish)
                    {
                        double settle = day * 24.0 + s.end;
                        for (int j = i + 1; j < segs.Length; j++)
                            if (segs[j].kind == ActivityKind.Carry && segs[j].place == stash) { settle = day * 24.0 + segs[j].end; break; }
                        fishDuties.Add(new FishDuty { hang = false, who = p.who, day = day, startAbs = day * 24.0 + s.start, beatEndAbs = day * 24.0 + s.end, settleAbs = settle });
                    }
                }
            }
        }

        /// <summary>Settle the fish chain up to <paramref name="now"/> (the view calls this every frame): the camp eats from the pile once a
        /// day, hangs and take-downs whose time has come move their fish in the sim. Returns how many duties settled now.</summary>
        public int ResolveFishChain(ViewTime now)
        {
            Sync();
            if (!FishRackOn) return 0;
            EnsureDay(now.day);
            if (fishDutiesDay != now.day) BuildFishDuties(now.day);
            sim.FishDayTurn(now.day, people.Count);
            int count = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                var list = pass == 0 ? fishDuties : debugFishDuties;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var d = list[i];
                    if (!d.started && now.Absolute >= d.startAbs) Start(d, now);
                    if (d.started && !d.settled && now.Absolute >= d.settleAbs) { Settle(d, now); count++; }
                    if (pass == 1 && d.settled) list.RemoveAt(i);
                }
            }
            return count;
        }

        void Start(FishDuty d, ViewTime now)
        {
            d.started = true;
            if (d.hang) d.n = !d.debug && fishCarried.TryGetValue(d.who.Key, out int c) ? c : sim.FishInBasket; // the cheat's hanger takes the whole basket
            else d.n = sim.FishDryOn(d.day);
            if (d.hang && d.n > sim.FishInBasket) d.n = sim.FishInBasket;
        }

        void Settle(FishDuty d, ViewTime now)
        {
            d.settled = true;
            if (d.hang)
            {
                int took = sim.FishHang(d.n, d.day);
                if (fishCarried.TryGetValue(d.who.Key, out int c)) fishCarried[d.who.Key] = Math.Max(0, c - Math.Max(took, d.n));
            }
            else sim.FishToPile(d.n, d.day);
        }

        // ---------------------------------------------------------------- what the views read

        /// <summary>The fish rack as a view draws it: its middle, the way along its bar (unit x/z), and the fish on it now: fresh ones (silver) and dry ones
        /// (brown), counting a hang in progress (the fish go up one by one) and a take-down in progress (they come off one by one).</summary>
        public struct FishRackLook
        {
            public float x, z, dirX, dirZ;
            public int fresh, dry;
            public int Fish => fresh + dry;
        }

        /// <summary>The rack and what hangs on it at <paramref name="now"/>. False off a camp's living land, or without positions.</summary>
        public bool TryGetFishRack(ViewTime now, out FishRackLook look)
        {
            look = default;
            Sync();
            if (!FishRackOn) return false;
            if (!FirePoint(out float fx, out float fz)) return false;
            FishRackSpot.Frame(fx, fz, sim.State.seed, out look.x, out look.z, out look.dirX, out look.dirZ);
            int dry = sim.FishDryOn(now.day), all = sim.FishOnRack, fresh = all - dry;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var d in pass == 0 ? fishDuties : debugFishDuties)
                {
                    if (!d.started || d.settled) continue;
                    if (d.hang)
                    {
                        float k = (float)Math.Max(0.0, Math.Min(1.0, (now.Absolute - d.startAbs) / Math.Max(1e-4, d.beatEndAbs - d.startAbs)));
                        fresh += FishRackSpot.HungBy(Math.Min(d.n, sim.FishInBasket), k);
                    }
                    else
                    {
                        float k = (float)Math.Max(0.0, Math.Min(1.0, (now.Absolute - d.startAbs) / Math.Max(1e-4, d.beatEndAbs - d.startAbs)));
                        dry -= Math.Min(dry, FishRackSpot.TakenBy(d.n, k));
                    }
                }
            }
            look.fresh = Math.Max(0, Math.Min(SimCore.FishRackMax, fresh));
            look.dry = Math.Max(0, Math.Min(SimCore.FishRackMax - look.fresh, dry));
            return true;
        }

        // ---------------------------------------------------------------- debug (the panel's "fill fish basket" / "fish on the rack")

        readonly Dictionary<int, (bool hang, float start, float until)> debugRack = new Dictionary<int, (bool, float, float)>();

        /// <summary>Debug ("fill fish basket"): the basket holds fish and one grown-up (not the toolmaker) walks the basket to the rack now and hangs
        /// them. Returns the fish in the basket, or -1 with no rack (no camp on a living land) or nobody free.</summary>
        public int DebugFillBasketNow(ViewTime now, int fish = 6)
        {
            Sync();
            if (!FishRackOn) return -1;
            int n = sim.DebugFillFishBasket(fish);
            return SendToRack(now, true) ? n : -1;
        }

        /// <summary>Debug ("fish on the rack"): dry fish hang on the rack and one grown-up takes them down and carries them to the pile now.
        /// Returns the fish on the rack, or -1.</summary>
        public int DebugRackNow(ViewTime now, int fish = 6)
        {
            Sync();
            if (!FishRackOn) return -1;
            int n = sim.DebugFishOnRack(fish, now.day);
            return SendToRack(now, false) ? n : -1;
        }

        bool SendToRack(ViewTime now, bool hang)
        {
            debugRack.Clear();
            foreach (var p in people)
            {
                if (!p.Adult || p.job == JobType.Toolmaker) continue;
                // the walk there and the beat, then (a take-down) the carry to the pile
                float walk = Math.Max(0.1f, WalkHours(BasePlace(p, now.day, now.hour), FishRackPlace));
                float beat = hang ? LifeClock.ViewHoursFor(now.hour + walk + 0.2f, HangBlockSeconds) : TakeDownHours;
                float start = now.hour + walk, end = start + beat;
                debugRack[p.key] = (hang, now.hour, end + (hang ? 0f : Math.Max(0.3f, WalkHours(FishRackPlace, stash))));
                var d = new FishDuty
                {
                    hang = hang, debug = true, who = p.who, day = now.day, startAbs = now.day * 24.0 + start, beatEndAbs = now.day * 24.0 + end,
                    settleAbs = now.day * 24.0 + end + (hang ? 0f : Math.Max(0.3f, WalkHours(FishRackPlace, stash))),
                };
                debugFishDuties.Add(d);
                return true;
            }
            return false;
        }

        bool DebugRackAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (debugRack.Count == 0 || !debugRack.TryGetValue(p.key, out var r)) return false;
            if (t.hour >= r.until) { debugRack.Remove(p.key); return false; }
            if (t.hour < r.start) return false;
            // the duty's own times decide the beat: before it, the walk (a Carry with the basket / Walk); in it, the beat; after it, the carry to the pile
            foreach (var d in debugFishDuties)
            {
                if (d.who != p.who || d.hang != r.hang) continue;
                double abs = t.Absolute, day0 = t.day * 24.0;
                var rack = FishRackPlace;
                if (abs < d.startAbs)
                    a = Dbg(p, t, r.hang ? ActivityKind.Carry : ActivityKind.Walk, rack, BasePlace(p, t.day, r.start), r.hang ? Prop.FishBasket : Prop.None, true, (float)(d.startAbs - day0), r.start);
                else if (abs < d.beatEndAbs)
                    a = Dbg(p, t, r.hang ? ActivityKind.HangFish : ActivityKind.TakeDownFish, rack, rack, r.hang ? Prop.FishBasket : Prop.None, false, (float)(d.beatEndAbs - day0), (float)(d.startAbs - day0));
                else
                    a = Dbg(p, t, ActivityKind.Carry, stash, rack, Prop.FishStick, true, (float)(d.settleAbs - day0), (float)(d.beatEndAbs - day0));
                return true;
            }
            return false;
        }

        PersonActivity Dbg(Person p, ViewTime t, ActivityKind kind, Place place, Place from, Prop prop, bool moving, float end, float start)
            => new PersonActivity
            {
                who = p.who, kind = kind, place = place, from = from, moving = moving && from != place, prop = prop, start = start, end = end,
                progress = Math.Max(0f, Math.Min(1f, (t.hour - start) / Math.Max(1e-4f, end - start))), pace = 1f, chronicleId = -1,
            };
    }
}
