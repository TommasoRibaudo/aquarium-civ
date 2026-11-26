using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// Last days (#788, research 23 §3 A, the cheap version): an elder in their last window (<see cref="SimCore.FrailtyOf(Notable)"/>) walks slower, sits
    /// longer on their own stool, goes home earlier, and family come and sit beside them (<see cref="ActivityKind.SitWithOld"/>). It
    /// bends the elder's existing day with existing uses and clips only: no new beat, no new animation. It rises by degrees across the
    /// window, so it never reads as a countdown and nothing switches on at a date.
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>How much slower a fully frail elder walks (a walk takes this much longer than <c>1</c>): +50 %, on top of the hunger pace.</summary>
        public const float FrailSlowest = 0.5f;

        /// <summary>#788 (B82): the family's spells at a frail elder's door are never a timetable. Per elder and day (a hash, no dice): the
        /// afternoon spell lands anywhere in <see cref="AfternoonFrom"/> to <see cref="AfternoonTo"/>, lasts 0.4 to 1 h, and comes most days;
        /// the morning one is rarer. Two elders' families never gather on the same minute by design.</summary>
        public const float AfternoonFrom = 15.5f, AfternoonTo = 19f, MorningFrom = 9.5f, MorningTo = 11.5f, AfternoonDays = 0.85f, MorningDays = 0.3f;

        /// <summary>How much longer this person's walks take now (1 outside their last days).</summary>
        float walkPace = 1f;

        readonly Dictionary<int, List<Person>> frailVisited = new Dictionary<int, List<Person>>(); // a visitor's key -> the frail elders of their family
        readonly Dictionary<int, List<Person>> frailKin = new Dictionary<int, List<Person>>(); // a frail elder's key -> the family who may come

        /// <summary>How frail they are now: 0 outside their last days, up to 1 (the sim's rule, read when the box was last read).</summary>
        public float FrailtyOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) ? p.frail : 0f; }

        /// <summary>How much longer their walks take (1 = as everyone's).</summary>
        public float PaceOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) ? PaceFor(p) : 1f; }

        static float PaceFor(Person p) => 1f + FrailSlowest * p.frail;

        /// <summary>After the links are set: who is frail, and whose family sits with them.</summary>
        void SnapshotFrail()
        {
            frailVisited.Clear(); frailKin.Clear();
            foreach (var e in people)
            {
                if (e.n == null || e.frail <= 0f) continue;
                foreach (int id in sim.KinOf(e.n))
                {
                    if (!byKey.TryGetValue(id, out var k) || k.stage == LifeStage.Baby || k.frail > 0f) continue; // a frail elder is visited, they don't go visiting
                    if (!frailVisited.TryGetValue(k.key, out var l)) frailVisited[k.key] = l = new List<Person>();
                    l.Add(e);
                    if (!frailKin.TryGetValue(e.key, out var kl)) frailKin[e.key] = kl = new List<Person>();
                    kl.Add(k);
                }
            }
        }

        /// <summary>Where a frail elder sits out a stretch: at their own door, on their stool, more and more of the time. Else <paramref name="otherwise"/>.</summary>
        Place FrailSit(Person p, DayInfo d, int salt, Place otherwise) =>
            p.frail > 0f && R(p.key, d.day, 210 + salt) < Math.Min(1f, 0.4f + p.frail) ? p.home : otherwise;

        /// <summary>The stool for a frail elder sitting at their own door; the usual prop elsewhere.</summary>
        static Prop FrailProp(Person p, Place at, Prop usual) => p.frail > 0f && at == p.home ? Prop.Stool : usual;

        /// <summary>The frail elder's spell today: when it starts and ends (view hours), false when there is none today.</summary>
        public bool VisitSpell(PersonRef elder, long day, bool morning, out float start, out float end)
        {
            Sync();
            start = end = 0f;
            return byKey.TryGetValue(elder.Key, out var e) && e.frail > 0f && Spell(e, day, morning, out start, out end);
        }

        bool Spell(Person e, long day, bool morning, out float start, out float end)
        {
            int salt = morning ? 420 : 410;
            start = end = 0f;
            if (R(e.key, day, salt) >= (morning ? MorningDays : AfternoonDays)) return false;
            float len = morning ? 0.4f + 0.4f * R(e.key, day, salt + 1) : 0.4f + 0.6f * R(e.key, day, salt + 1);
            float from = morning ? MorningFrom : AfternoonFrom, to = morning ? MorningTo : AfternoonTo;
            start = from + R(e.key, day, salt + 2) * (to - from - len);
            end = start + len;
            return true;
        }

        /// <summary>The frail elder is at their own door for today's spells. The seat starts early enough for the walk home from
        /// wherever the day had them just before (at their own, slower pace), so the walk never eats into the spell: a spell the
        /// stitch could not walk them home for in time was dropped, and the family sat at an empty door (Editor, 2026-11-26).</summary>
        void FrailSeat(Person p, DayInfo d, List<Seg> list)
        {
            if (p.frail <= 0f) return;
            foreach (bool morning in new[] { true, false })
            {
                if (!Spell(p, d.day, morning, out float s, out float e)) continue;
                float lead = 0f;
                foreach (var o in list)
                    if (o.start < s && o.end >= s) { lead = WalkHours(o.place, p.home); if (lead > 0f) lead += SeatSlack; break; }
                Put(list, Mk(ActivityKind.SitOut, Math.Max(0f, s - lead), e, p.home, Prop.Stool), d);
            }
        }

        /// <summary>How much earlier than the walk home needs a frail elder sets off for their spell (view hours).</summary>
        const float SeatSlack = 0.05f;

        /// <summary>The shortest stretch of a spell worth the family's walk to the door (view hours).</summary>
        public const float ShortestVisit = 0.15f;

        /// <summary>Who of the family comes to a spell: one of them by the day's hash, and now and then a second.</summary>
        bool Comes(Person elder, Person visitor, long day, bool morning)
        {
            var kin = frailKin[elder.key];
            int n = kin.Count, at = kin.IndexOf(visitor);
            int first = (int)(H(elder.key, day, morning ? 430 : 431) % (uint)n);
            if (at == first) return true;
            return n > 1 && at == (first + 1) % n && R(elder.key, day, morning ? 432 : 433) < 0.4f;
        }

        /// <summary>Family of a frail elder sit beside them at the elder's door in the elder's spells (one or two of them each time),
        /// only while the elder is really on the stool there: the visit is cut to the elder's own planned day, so a scene, the weather
        /// or a beat that takes the elder away also takes the visit (Editor, 2026-11-26: 73 frames of family at an empty door). A frail
        /// elder is visited, they don't go visiting: their own seat comes first (and two frail elders' days never wait on each other).</summary>
        void FrailVisits(Person p, DayInfo d, List<Seg> list)
        {
            if (p.frail > 0f || !frailVisited.TryGetValue(p.key, out var elders)) return;
            foreach (var e in elders)
            {
                if (e == p) continue;
                foreach (bool morning in new[] { true, false })
                {
                    if (!Spell(e, d.day, morning, out float s, out float en) || !Comes(e, p, d.day, morning)) continue;
                    foreach (var o in DayOf(e))
                    {
                        if (!SeatedAtDoor(e, o)) continue;
                        float a = Math.Max(s, o.start), z = Math.Min(en, o.end);
                        if (z - a >= ShortestVisit) Put(list, Mk(ActivityKind.SitWithOld, a, z, e.home, Prop.None, ActivitySource.Schedule, e.who), d);
                    }
                }
            }
        }

        /// <summary>After the stitch: a visit at a frail elder's door never outlasts the elder's seat there. The stitch may stretch a stay
        /// up to the walk after it (the visitor's supper at the fire set off later than the elder's); the part after the elder got up
        /// is the visitor getting up too, stretching at the door before they go.</summary>
        Seg[] KeepVisitsToTheSeat(Person p, Seg[] segs)
        {
            if (!frailVisited.ContainsKey(p.key)) return segs;
            List<Seg> outList = null;
            for (int i = 0; i < segs.Length; i++)
            {
                var v = segs[i];
                if (v.kind != ActivityKind.SitWithOld || v.place.kind != PlaceKind.Home || v.Moving || !byKey.TryGetValue(v.partner.Key, out var e) || e.frail <= 0f || v.place != e.home)
                {
                    outList?.Add(v);
                    continue;
                }
                outList ??= new List<Seg>(segs.Length + 4);
                if (outList.Count == 0) for (int j = 0; j < i; j++) outList.Add(segs[j]);
                float t = v.start;
                foreach (var o in DayOf(e))
                {
                    if (o.end <= t || !SeatedAtDoor(e, o)) continue;
                    if (o.start >= v.end) break;
                    float a = Math.Max(t, o.start), z = Math.Min(v.end, o.end);
                    if (a > t) outList.Add(Stand(v, t, a));
                    var sit = v; sit.start = a; sit.end = z; outList.Add(sit);
                    t = z;
                }
                if (t < v.end) outList.Add(Stand(v, t, v.end));
            }
            return outList == null ? segs : outList.ToArray();

            static Seg Stand(Seg v, float a, float z) { v.start = a; v.end = z; v.kind = ActivityKind.Stretch; v.partner = PersonRef.None; return v; }
        }

        /// <summary>The elder on the stool at their own door (not walking, not indoors).</summary>
        static bool SeatedAtDoor(Person e, Seg o) => o.kind == ActivityKind.SitOut && o.place == e.home && !o.Moving && o.prop == Prop.Stool;
    }
}
