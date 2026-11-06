// Visit loop, tester run 1 #3: the camp's day spread out. In a living-land Camp of 8 all 8 chatted at the fire at 09:30 and
// 16:15, all ate at 12:40, or 6-7 sat out there, and most daylight samples had no story on screen (research 17 principles 6:
// no 3+ neighbours in step, and 7: a story in progress in daylight). The camp's day is now a handful of small things at
// different places and times: pairs and couples, meals in shifts at the fire, the door or where they work, the work that
// keeps going over the meal, short partial sit-outs; the evening fire stays the one time most gather. All hashed per person
// (a couple by the pair) and day: no new dice from any stream.
using System;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    public sealed partial class LifePlanner
    {
        /// <summary>The camp spreads its day (only the camp: the village and the town already spread theirs, #197).</summary>
        bool CampSpread => era == Era.Camp;

        /// <summary>A couple decides together (where they eat, what they do on a rest morning); a single person alone.</summary>
        int CoupleKey(Person p) => p.partner != null && p.partner.Grown ? PairKey(p, p.partner) : p.key;

        // ---------------------------------------------------------------- the meal

        /// <summary>Where a camp household eats at midday [est shares]: about four in ten at the fire, three at their own door, one
        /// at a friend's door, and the rest where they work when that is out on the land (a fisher by the water, a woodcutter in the
        /// woods: they eat from the basket and go on; a bush is the kids' berries, #288). Elders and kids follow the household (kids eat with their parent).</summary>
        Place CampMealPlace(Person p, DayInfo d, Place workSite)
        {
            float r = R(CoupleKey(p), d.day, 140);
            if (r < 0.40f) return fire;
            if (r < 0.70f) return IsValidPlace(p.home) && !p.home.IsNone ? p.home : fire;
            if (r < 0.80f)
            {
                var f = p.friend;
                if (f != null && f.Grown && !f.home.IsNone && f.home != p.home) return f.home;
                return IsValidPlace(p.home) && !p.home.IsNone ? p.home : fire;
            }
            bool outOnTheLand = !workSite.IsNone && !workSite.IsBuilding && workSite.kind != PlaceKind.Fire && workSite != stash
                && workSite.kind != PlaceKind.BushCell; // eating at a bush is the kids' berries (#288)
            return outOnTheLand ? workSite : fire;
        }

        /// <summary>A camp's lunch is spread over a longer stretch than the village's (11:15-12:50 start) so the fire never holds
        /// the whole camp eating at once [est].</summary>
        void CampShift(ref Shift s, Person p, DayInfo d)
        {
            int lk = p.partner != null && p.partner.Adult ? PairKey(p, p.partner) : p.key;
            s.lunch = 11.25f + 1.6f * R(lk, d.day, 141);
            s.back = s.lunch + 0.55f + 0.35f * R(lk, d.day, 82);
            s.end = Math.Max(s.end, s.back + 1.2f);
        }

        // ---------------------------------------------------------------- the rest day

        /// <summary>
        /// A camp's rest day: no work, but not everyone at the fire. A chore at the door, then two free stretches before the meal and
        /// two after it, each something small somewhere (fishing, picking at a bush, drawing water, a visit to a friend's door,
        /// sitting at their own door with the partner, a nap), with the fire as one place among them; the meal in shifts; the
        /// evening as every day. A couple's choices are the pair's, so partners do them together.
        /// </summary>
        void CampRestDay(Person p, DayInfo d)
        {
            int k = p.key, ck = CoupleKey(p);
            long day = d.day;
            At(Chore(p, d, 5), 7.6f + 0.9f * R(k, day, 142), p.home, ChoreProp(Chore(p, d, 5)));
            float lunch = 11.2f + 1.6f * R(ck, day, 143);
            float mid = bt + (lunch - bt) * (0.4f + 0.2f * R(ck, day, 144));
            Pastime(p, d, 0, mid);
            Pastime(p, d, 1, lunch);
            float lunchEnd = lunch + 0.55f + 0.3f * R(ck, day, 145);
            At(ActivityKind.Eat, lunchEnd, CampMealPlace(p, d, Place.None), Prop.None, Ref(p.partner));
            if (p.hints.napChance > 0f && R(k, day, 7) < p.hints.napChance) At(ActivityKind.Nap, Math.Min(15f, bt + 1f), p.home);
            float after = Math.Max(bt + 0.8f, 14.2f + 0.8f * R(ck, day, 146));
            Pastime(p, d, 2, Math.Min(after, 16.4f));
            Pastime(p, d, 3, LifeClock.Evening);
        }

        /// <summary>One free stretch of a camp rest day, until <paramref name="until"/> [est shares]: fishing by the water, picking at a
        /// bush, drawing water and taking it home, a visit at a friend's door, sitting with the partner at their own door (or a chore
        /// there), a sit at the fire, the toolmaker knapping. Each stretch is hashed per couple and day, so a camp's people are spread
        /// over the land and the fire holds a few, not all.</summary>
        void Pastime(Person p, DayInfo d, int slot, float until)
        {
            if (until <= bt + MinStay) return;
            int ck = CoupleKey(p);
            float r = R(ck, d.day, 150 + slot);
            bool water = !OnLiving || sim.OpenWater;
            bool berries = !OnLiving || sim.BerriesGrow;
            var with = p.partner != null && p.partner.Grown ? Ref(p.partner) : default;
            if (r < 0.16f && water && WaterHere(Pond)) { At(ActivityKind.Fish, until, Pond, Prop.Rod, with); return; }
            if (r < 0.32f && berries) { At(ActivityKind.Gather, until, BerryPlaceFor(p, d, slot), Prop.Basket, with); return; }
            if (r < 0.42f)
            {
                // Water for the tent: the walk with the empty bucket, the draw, the full one home, then a chore at the door.
                var w = WaterPlace;
                float there = WalkHours(bcur, w), back = WalkHours(w, p.home);
                if (bt + there + 0.35f + back + MinStay < until)
                {
                    Move(ActivityKind.Walk, w, bt + there, Prop.EmptyBucket, TripType.Errand);
                    At(ActivityKind.FetchWater, bt + 0.35f, w, Prop.EmptyBucket);
                    Move(ActivityKind.FetchWater, p.home, bt + back, Prop.Buckets, TripType.Home);
                    var ch = Chore(p, d, 160 + slot);
                    At(ch, until, p.home, ChoreProp(ch));
                    return;
                }
            }
            if (r < 0.56f && p.friend != null && p.friend.Grown && !p.friend.home.IsNone && p.friend.home != p.home)
            { At(ActivityKind.Visit, until, p.friend.home, Prop.None, Ref(p.friend)); return; }
            if (r < 0.74f)
            {
                // At their own door: with the partner a talk, alone a chore or a sit.
                if (!with.IsNone) At(ActivityKind.Chat, until, p.home, Prop.None, with);
                else { var ch = Chore(p, d, 170 + slot); At(ch, until, p.home, ChoreProp(ch)); }
                return;
            }
            if (p.job == JobType.Toolmaker && r < 0.86f) { At(ActivityKind.Knap, until, fire, Prop.Hammerstone); return; }
            // A sit at the fire: one of a few, a short one, then home to the door.
            float sit = Math.Min(until, bt + 0.6f + 0.4f * R(p.key, d.day, 180 + slot));
            At(r < 0.88f ? ActivityKind.Chat : ActivityKind.SitOut, sit, fire, Prop.None, with);
            if (sit < until - MinStay) { var ch = Chore(p, d, 190 + slot); At(ch, until, p.home, ChoreProp(ch)); }
        }

        /// <summary>The pond has water to fish (a living land's pond may still be dry).</summary>
        bool WaterHere(Place pond) => !OnLiving || sim.EcoPondCells > 0;

        // ---------------------------------------------------------------- the crowd's helpers

        /// <summary>A camp helper with no building slot did chores at their tent all day, so a big camp's helpers stood sweeping
        /// together at a few tents. In the camp they now do the tribe's own work for the day (hashed per person and day, [est]):
        /// picking at a bush, fishing by the water, chopping in the woods, or the chores at home and water from the pond.</summary>
        Work? CampHelperWork(Person p, DayInfo d)
        {
            if (!CampSpread || !p.Adult) return null;
            float r = R(p.key, d.day, 147);
            bool water = !OnLiving || sim.EcoPondCells > 0;
            if (r < 0.35f && (!OnLiving || sim.BerriesGrow)) return TribeWork(p, d);
            if (r < 0.55f && water) return W(Pond, ActivityKind.Fish, Prop.Rod, Prop.FishStick, stash, 0.25f);
            if (r < 0.70f && WoodsStand) return W(Woods, ActivityKind.Chop, Prop.Axe, Prop.Log, stash, 0.3f);
            return null; // the chores at home, as before
        }

        // ---------------------------------------------------------------- the evening fire

        /// <summary>
        /// The evening fire is the one time most of the camp gathers, and even then with roles: before the speech one tends the pot
        /// (the camp's cook, else the day's first by hash), one brings and stacks the wood by the pile, and a few finish a chore at
        /// their door and come later; the rest sit down at the fire. True when <paramref name="p"/> has a role (its block is laid).
        /// </summary>
        bool CampEveningRole(Person p, DayInfo d)
        {
            if (!CampSpread || !p.Adult || p == leader) return false;
            int rank = EveningRank(p, d);
            if (rank == 0) { At(ActivityKind.Cook, 17.6f, fire, Prop.Stick, default, TripType.HomeToGreen); return true; }
            if (rank == 1) { At(ActivityKind.StackWood, 17.6f, stash, Prop.Log, default, TripType.HomeToGreen); return true; }
            if (R(p.key, d.day, 210) < 0.35f)
            {
                var ch = Chore(p, d, 211);
                if (ch == ActivityKind.SitOut) ch = ActivityKind.SweepStep;
                At(ch, 17.25f + 0.2f * R(p.key, d.day, 212), p.home, ChoreProp(ch));
                At(ActivityKind.SitOut, 17.6f, gather, Prop.None, default, TripType.HomeToGreen);
                return true;
            }
            return false;
        }

        /// <summary>The cook first (the camp's crowd cook, else none), then the grown-ups at the fire tonight by the day's hash.</summary>
        int EveningRank(Person p, DayInfo d)
        {
            int rank = 0;
            bool hasCook = false;
            foreach (var q in people) if (q.job == CrowdCook && q.Adult) { hasCook = true; break; }
            if (p.job == CrowdCook) return 0;
            if (hasCook) rank++;
            uint mine = H(p.key, d.day, 213);
            foreach (var q in people)
            {
                if (q == p || !q.Adult || q == leader || q.job == CrowdCook || StyleOf(q, d) != EveningStyle.Gather) continue;
                uint h = H(q.key, d.day, 213);
                if (h < mine || (h == mine && q.key < p.key)) rank++;
            }
            return rank;
        }

        // ---------------------------------------------------------------- the stream bed

        long diggersDay = long.MinValue;
        readonly System.Collections.Generic.HashSet<int> diggers = new System.Collections.Generic.HashSet<int>();

        /// <summary>How many may dig roots in the stream bed at once in a camp: the bed is one spot, and four or more diggers there
        /// knelt and dug in step [est].</summary>
        int DiggerSlots => people.Count >= 14 ? 3 : 2;

        /// <summary>Today's diggers: the grown-ups by the day's hash, the first <see cref="DiggerSlots"/> (stable for the day).</summary>
        bool MayDig(Person p, DayInfo d)
        {
            if (diggersDay != d.day)
            {
                diggersDay = d.day;
                diggers.Clear();
                var order = new System.Collections.Generic.List<Person>();
                foreach (var q in people) if (q.Adult && q.job != JobType.Toolmaker && q.job != CrowdCook) order.Add(q);
                order.Sort((a, b) => { int c = H(a.key, d.day, 148).CompareTo(H(b.key, d.day, 148)); return c != 0 ? c : a.key.CompareTo(b.key); });
                for (int i = 0; i < order.Count && diggers.Count < DiggerSlots; i++) diggers.Add(order[i].key);
            }
            return diggers.Contains(p.key);
        }

        /// <summary>A camp's work in the stream bed goes to today's few diggers; the others fish, chop or carry water and do the
        /// chores at their own tent (hashed per person and day [est]).</summary>
        Work SpreadDiggers(Person p, DayInfo d, Work w)
        {
            if (!CampSpread || sim.BeforeCamp || w.site != Stream || w.loop != ActivityKind.Gather || MayDig(p, d)) return w;
            float r = R(p.key, d.day, 149);
            bool water = !OnLiving || sim.EcoPondCells > 0;
            if (water && r < 0.45f) return W(Pond, ActivityKind.Fish, Prop.Rod, Prop.FishStick, stash, 0.25f);
            if (WoodsStand && r < 0.8f) return W(Woods, ActivityKind.Chop, Prop.Axe, Prop.Log, stash, 0.3f);
            var c = W(p.home, ActivityKind.SweepStep, Prop.Broom, Prop.Buckets, p.home, 0.3f);
            c.chores = true;
            return Fetch(c, WaterPlace);
        }

        // ---------------------------------------------------------------- elders

        /// <summary>Where a camp elder sits out this stretch: at their own door some of the time, else at the fire [est].</summary>
        Place ElderSitPlace(Person p, DayInfo d, int salt) => CampSpread && R(p.key, d.day, 200 + salt) < 0.55f ? p.home : gather;
    }
}
