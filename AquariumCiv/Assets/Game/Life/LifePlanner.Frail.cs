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

        /// <summary>The two spells the family sit with a frail elder at the elder's door, in view hours (afternoon, then a morning one).</summary>
        public const float VisitAfternoonFrom = 16.9f, VisitAfternoonTo = 17.8f, VisitMorningFrom = 10.1f, VisitMorningTo = 11f;

        /// <summary>How much longer this person's walks take now (1 outside their last days).</summary>
        float walkPace = 1f;

        readonly Dictionary<int, List<Person>> frailVisited = new Dictionary<int, List<Person>>(); // a visitor's key -> the frail elders of their family

        /// <summary>How frail they are now: 0 outside their last days, up to 1 (the sim's rule, read when the box was last read).</summary>
        public float FrailtyOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) ? p.frail : 0f; }

        /// <summary>How much longer their walks take (1 = as everyone's).</summary>
        public float PaceOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) ? PaceFor(p) : 1f; }

        static float PaceFor(Person p) => 1f + FrailSlowest * p.frail;

        /// <summary>After the links are set: who is frail, and whose family sits with them.</summary>
        void SnapshotFrail()
        {
            frailVisited.Clear();
            foreach (var e in people)
            {
                if (e.n == null || e.frail <= 0f) continue;
                foreach (int id in sim.KinOf(e.n))
                {
                    if (!byKey.TryGetValue(id, out var k) || k.stage == LifeStage.Baby) continue;
                    if (!frailVisited.TryGetValue(k.key, out var l)) frailVisited[k.key] = l = new List<Person>();
                    l.Add(e);
                }
            }
        }

        /// <summary>Where a frail elder sits out a stretch: at their own door, on their stool, more and more of the time. Else <paramref name="otherwise"/>.</summary>
        Place FrailSit(Person p, DayInfo d, int salt, Place otherwise) =>
            p.frail > 0f && R(p.key, d.day, 210 + salt) < Math.Min(1f, 0.4f + p.frail) ? p.home : otherwise;

        /// <summary>The stool for a frail elder sitting at their own door; the usual prop elsewhere.</summary>
        static Prop FrailProp(Person p, Place at, Prop usual) => p.frail > 0f && at == p.home ? Prop.Stool : usual;

        /// <summary>The frail elder holds two spells at their own door each day, and whoever is of their family sits beside them in those
        /// (in <see cref="FrailVisits"/>). Laid over the day, under the role beats, the weather and the scenes.</summary>
        void FrailSeat(Person p, DayInfo d, List<Seg> list)
        {
            if (p.frail <= 0f) return;
            Put(list, Mk(ActivityKind.SitOut, VisitMorningFrom, VisitMorningTo, p.home, Prop.Stool), d);
            Put(list, Mk(ActivityKind.SitOut, VisitAfternoonFrom, VisitAfternoonTo, p.home, Prop.Stool), d);
        }

        /// <summary>Family of a frail elder sit beside them at the elder's door: every afternoon spell (the first elder they have), the
        /// morning one for a second, and the old and the young of the family, now and then, in the morning too.</summary>
        void FrailVisits(Person p, DayInfo d, List<Seg> list)
        {
            if (!frailVisited.TryGetValue(p.key, out var elders)) return;
            for (int i = 0; i < elders.Count && i < 2; i++)
            {
                var e = elders[i];
                if (e == p) continue;
                bool afternoon = i % 2 == 0;
                Put(list, Mk(ActivityKind.SitWithOld, afternoon ? VisitAfternoonFrom : VisitMorningFrom, afternoon ? VisitAfternoonTo : VisitMorningTo, e.home, Prop.None, ActivitySource.Schedule, e.who), d);
                if (afternoon && (p.stage == LifeStage.Elder || p.stage == LifeStage.Kid) && R(p.key, d.day, 301 + e.key) < 0.6f)
                    Put(list, Mk(ActivityKind.SitWithOld, VisitMorningFrom, VisitMorningTo, e.home, Prop.None, ActivitySource.Schedule, e.who), d);
            }
        }
    }
}
