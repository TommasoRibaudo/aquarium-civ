using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// fb4 #566: people visit the feather pole and pray. Once or twice a day (a midday one on some days, a dusk one most days, never
    /// none), one or two grown-ups leave what they are doing, walk to the pole (the one feather prop, <see cref="SimCore.FeatherPoleProp"/>,
    /// a <see cref="PlaceKind.Prop"/> place: the view stands them in a ring round it, facing it), hold <see cref="ActivityKind.Pray"/> (hands
    /// together, a small bow) for a few real seconds, then walk back to where the day had them. A kid sometimes tags along. Like the
    /// hunts this is an overlay on the planned day (deterministic from the seed, the person and the day; nothing is saved); a hunt, a
    /// Keeper reaction or a story scene that wants the person wins. The debug cheat sends them now (<see cref="DebugPrayNow"/>).
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>Real seconds the pray pose is held.</summary>
        public const float PraySeconds = 5.5f;
        /// <summary>The midday visit starts between these hours (on <see cref="MiddayVisitChance"/> of the days); the dusk one between the dusk hours.</summary>
        public const float MiddayFrom = 9f, MiddayTo = 14.5f, DuskFrom = 17f, DuskTo = 18.4f;
        public const float MiddayVisitChance = 0.55f, DuskVisitChance = 0.8f, TwoPrayChance = 0.5f, KidTagChance = 0.5f;

        /// <summary>One person's visit: leaves at <see cref="start"/>, stands at the pole from <see cref="arrive"/> to <see cref="end"/>, is back where the day had them at <see cref="until"/> (view hours of the visit's day).</summary>
        public struct PrayerVisit
        {
            public int key;
            public bool kid;
            public float start, arrive, end, until;
            internal Place from, back;
        }

        long prayerDay = long.MinValue;
        readonly Dictionary<int, PrayerVisit> prayerPlans = new Dictionary<int, PrayerVisit>();
        readonly Dictionary<int, (long day, PrayerVisit v)> debugPrayers = new Dictionary<int, (long, PrayerVisit)>();

        /// <summary>The pole as a place (invalid, <see cref="PlaceKind.None"/>, while no feather stands).</summary>
        public Place PolePlace
        {
            get
            {
                Sync();
                var pole = sim.FeatherPoleProp;
                return pole == null ? default : Place.Land(PlaceKind.Prop, pole.id);
            }
        }

        /// <summary>Today's prayer visits of everyone who has one (tests, the debug readout).</summary>
        public int PrayerVisitsOn(long day, List<PrayerVisit> into)
        {
            Sync();
            into.Clear();
            EnsurePrayers(day);
            foreach (var kv in prayerPlans) into.Add(kv.Value);
            into.Sort((a, b) => a.start != b.start ? a.start.CompareTo(b.start) : a.key.CompareTo(b.key));
            return into.Count;
        }

        float PrayHoursAt(float hour) => LifeClock.ViewHoursFor(hour, PraySeconds);

        void EnsurePrayers(long day)
        {
            if (prayerDay == day) return;
            prayerDay = day;
            prayerPlans.Clear();
            var pole = PolePlace;
            if (pole.kind != PlaceKind.Prop || !IsValidPlace(pole)) return;
            EnsureDay(day);
            if (dayInfo.weather == Weather.Storm || dayInfo.weather == Weather.Snow) return; // they stay in
            bool midday = R(0, day, 5661) < MiddayVisitChance;
            bool dusk = !midday || R(0, day, 5662) < DuskVisitChance;
            if (midday) PlanVisit(day, 0, MiddayFrom + R(0, day, 5663) * (MiddayTo - MiddayFrom), pole);
            if (dusk) PlanVisit(day, 1, DuskFrom + R(0, day, 5664) * (DuskTo - DuskFrom), pole);
        }

        void PlanVisit(long day, int slot, float start, Place pole)
        {
            int want = R(0, day, 5665 + slot) < TwoPrayChance ? 2 : 1, got = 0;
            Person kid = null; uint kidH = uint.MaxValue;
            var order = new List<(uint h, Person p)>();
            foreach (var p in people)
            {
                if (prayerPlans.ContainsKey(p.key)) continue;
                if (p.Grown && p.job != JobType.Toolmaker) order.Add((H(p.key, day, 5670 + slot), p));
                else if (p.stage == LifeStage.Kid)
                {
                    uint h = H(p.key, day, 5680 + slot);
                    if (h < kidH) { kidH = h; kid = p; }
                }
            }
            order.Sort((a, b) => a.h != b.h ? a.h.CompareTo(b.h) : a.p.key.CompareTo(b.p.key));
            foreach (var o in order)
            {
                if (got >= want) break;
                if (AddVisit(o.p, day, start, pole, false)) got++;
            }
            if (got > 0 && kid != null && R(0, day, 5690 + slot) < KidTagChance) AddVisit(kid, day, start + 0.04f, pole, true);
        }

        /// <summary>Plan a person's visit from <paramref name="start"/>: the walk from where the day has them, the pose, the walk back. False when they are
        /// asleep or indoors then, or the visit would cross another of theirs.</summary>
        bool AddVisit(Person p, long day, float start, Place pole, bool kid)
        {
            if (!BuildVisit(p, day, start, pole, kid, true, out var v)) return false;
            prayerPlans[p.key] = v;
            return true;
        }

        bool BuildVisit(Person p, long day, float start, Place pole, bool kid, bool checkFree, out PrayerVisit v)
        {
            v = default;
            huntBuilding = true; // PlanBase below must not see prayers (nor hunts): the plain day
            try
            {
                var a = PlanBase(p, new ViewTime(day, Math.Min(23.9f, start)));
                if (checkFree && (a.kind == ActivityKind.Sleep || a.kind == ActivityKind.Nap || a.kind == ActivityKind.Indoors)) return false;
                var from = IsValidPlace(a.place) ? a.place : ValidHome(p);
                float walk = WalkHours(from, pole);
                float arrive = start + walk, end = arrive + PrayHoursAt(arrive);
                var back = BasePlaceNoLock(p, day, end);
                float until = end + WalkHours(pole, back);
                if (until > 23.5f) return false;
                v = new PrayerVisit { key = p.key, kid = kid, start = start, arrive = arrive, end = end, until = until, from = from, back = back };
                return true;
            }
            finally { huntBuilding = false; }
        }

        Place BasePlaceNoLock(Person p, long day, float hour)
        {
            var a = PlanBase(p, new ViewTime(day, Math.Min(23.9f, hour)));
            return IsValidPlace(a.place) ? a.place : ValidHome(p);
        }

        /// <summary>The overlay: this person is on a prayer visit at <paramref name="t"/>.</summary>
        bool PrayerActivityAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (huntBuilding || sim == null) return false;
            PrayerVisit v;
            if (debugPrayers.Count > 0 && debugPrayers.TryGetValue(p.key, out var d))
            {
                if (t.Absolute >= d.day * 24.0 + d.v.until) debugPrayers.Remove(p.key);
                else if (t.day == d.day && t.hour >= d.v.start) { v = d.v; return Visit(p, t, v, pole: PolePlace, out a); }
            }
            if (t.hour < MiddayFrom || t.hour >= 23.5f) return false;
            Sync();
            EnsurePrayers(t.day);
            if (prayerPlans.Count == 0 || !prayerPlans.TryGetValue(p.key, out v)) return false;
            if (t.hour < v.start || t.hour >= v.until) return false;
            return Visit(p, t, v, PolePlace, out a);
        }

        bool Visit(Person p, ViewTime t, PrayerVisit v, Place pole, out PersonActivity a)
        {
            a = default;
            if (pole.kind != PlaceKind.Prop || !IsValidPlace(pole)) return false;
            if (t.hour < v.arrive) a = Dbg(p, t, ActivityKind.Walk, pole, v.from, Prop.None, true, v.arrive, v.start);
            else if (t.hour < v.end) a = Dbg(p, t, ActivityKind.Pray, pole, pole, Prop.None, false, v.end, v.arrive);
            else a = Dbg(p, t, ActivityKind.Walk, v.back, pole, Prop.None, true, v.until, v.end);
            return true;
        }

        /// <summary>Debug ("Feather pole: pray now"): a pole stands (made if none does) and up to two grown-ups (not the toolmaker) and a kid walk
        /// to it from where the day has them and pray, starting now. Returns how many go, or -1 when nobody can.</summary>
        public int DebugPrayNow(ViewTime now, int grownUps = 2)
        {
            Sync();
            if (sim == null || !sim.DebugEnsureFeatherPole()) return -1;
            Invalidate();
            Sync();
            var pole = PolePlace;
            debugPrayers.Clear();
            int n = 0; bool kidGone = false;
            foreach (var p in people)
            {
                bool grown = p.Grown && p.job != JobType.Toolmaker && n < grownUps, kid = !grown && p.stage == LifeStage.Kid && !kidGone && n > 0;
                if (!grown && !kid) continue;
                if (!BuildVisit(p, now.day, now.hour, pole, kid, false, out var v)) continue;
                debugPrayers[p.key] = (now.day, v);
                if (grown) n++; else kidGone = true;
            }
            return n + (kidGone ? 1 : 0) > 0 ? n + (kidGone ? 1 : 0) : -1;
        }
    }
}
