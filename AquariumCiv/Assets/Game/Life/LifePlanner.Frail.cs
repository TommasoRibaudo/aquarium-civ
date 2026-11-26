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
                    if (!byKey.TryGetValue(id, out var k) || k.stage == LifeStage.Baby) continue;
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

        /// <summary>The frail elder is at their own door for today's spells.</summary>
        void FrailSeat(Person p, DayInfo d, List<Seg> list)
        {
            if (p.frail <= 0f) return;
            foreach (bool morning in new[] { true, false })
                if (Spell(p, d.day, morning, out float s, out float e)) Put(list, Mk(ActivityKind.SitOut, s, e, p.home, Prop.Stool), d);
        }

        /// <summary>Who of the family comes to a spell: one of them by the day's hash, and now and then a second.</summary>
        bool Comes(Person elder, Person visitor, long day, bool morning)
        {
            var kin = frailKin[elder.key];
            int n = kin.Count, at = kin.IndexOf(visitor);
            int first = (int)(H(elder.key, day, morning ? 430 : 431) % (uint)n);
            if (at == first) return true;
            return n > 1 && at == (first + 1) % n && R(elder.key, day, morning ? 432 : 433) < 0.4f;
        }

        /// <summary>Family of a frail elder sit beside them at the elder's door in the elder's spells (one or two of them each time).</summary>
        void FrailVisits(Person p, DayInfo d, List<Seg> list)
        {
            if (!frailVisited.TryGetValue(p.key, out var elders)) return;
            foreach (var e in elders)
            {
                if (e == p) continue;
                foreach (bool morning in new[] { true, false })
                    if (Spell(e, d.day, morning, out float s, out float en) && Comes(e, p, d.day, morning))
                        Put(list, Mk(ActivityKind.SitWithOld, s, en, e.home, Prop.None, ActivitySource.Schedule, e.who), d);
            }
        }
    }
}
