using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #192: parents carry babies outside. Babies used to stay indoors until four, so a family with young children looked
    /// like it was missing them. Now a baby is out on most days for a stretch of one to three hours (about nine in ten days, between eight and half past five;
    /// the window is a hash of (baby, day): no sim dice, no sim writes), held in a carer's arms or, now and then, on their
    /// back. The carer is a parent (a notable baby's parent, else the parent's partner; a crowd baby has two crowd adults
    /// who take turns), whichever is free at the middle of the window; if both are busy the baby stays in that day. While the
    /// carer is doing something with both hands (a work loop, a carry, a chore, a role's duty), asleep or in the water, they
    /// have set the baby down: the baby is out of sight for that spell and back on them after it.
    ///
    /// Babies are not separate walkers, which keeps them cheap on a phone: the carer's <see cref="PersonActivity.carrying"/>
    /// names the baby and the view draws it on them. The baby's own activity stays at home and says who has them
    /// (<see cref="PersonActivity.carriedBy"/>).
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>The stretch of the day a baby is out, from a hash of (baby, day).</summary>
        struct CarryPlan
        {
            public Person carer;
            public float start, end;
            public bool onBack;
        }

        readonly Dictionary<int, CarryPlan> carryPlans = new Dictionary<int, CarryPlan>();
        readonly Dictionary<int, List<Person>> carerBabies = new Dictionary<int, List<Person>>();
        readonly Dictionary<int, Person[]> babyCarers = new Dictionary<int, Person[]>();
        long carrySig = long.MinValue;

        /// <summary>Hours of the day a baby may be out in.</summary>
        public const float CarryFrom = 8f, CarryUntil = 17.5f;
        /// <summary>How many days in ten a baby is out.</summary>
        public const float CarryDayShare = 0.9f;

        void BuildCarers()
        {
            if (carrySig == snapSig) return;
            carrySig = snapSig;
            babyCarers.Clear();
            carerBabies.Clear();
            var crowdAdults = new List<Person>();
            foreach (var q in people) if (q.n == null && q.stage == LifeStage.Adult) crowdAdults.Add(q);
            int crowdBaby = 0;
            foreach (var b in people)
            {
                if (b.stage != LifeStage.Baby) continue;
                Person first = null, second = null;
                if (b.n != null)
                {
                    first = b.parent;
                    second = b.parent?.partner;
                }
                else if (crowdAdults.Count > 0)
                {
                    first = crowdAdults[(crowdBaby * 3 + 1) % crowdAdults.Count];
                    if (crowdAdults.Count > 1) second = crowdAdults[(crowdBaby * 3 + 2) % crowdAdults.Count];
                    crowdBaby++;
                }
                var list = new List<Person>(2);
                if (first != null && first.Grown) list.Add(first);
                if (second != null && second.Grown && second != first) list.Add(second);
                if (list.Count == 0) continue;
                babyCarers[b.key] = list.ToArray();
                foreach (var c in list)
                {
                    if (!carerBabies.TryGetValue(c.key, out var babies)) carerBabies[c.key] = babies = new List<Person>(2);
                    babies.Add(b);
                }
            }
        }

        /// <summary>Today's carry for a baby (a null carer = the baby stays in today).</summary>
        CarryPlan CarryOf(Person baby)
        {
            if (carryPlans.TryGetValue(baby.key, out var plan)) return plan;
            plan = default;
            long day = dayInfo.day;
            if (R(baby.key, day, 1990) < CarryDayShare && babyCarers.TryGetValue(baby.key, out var carers))
            {
                float start = CarryFrom + (CarryUntil - CarryFrom - 3f) * R(baby.key, day, 1991);
                float end = Math.Min(CarryUntil, start + 1f + 2f * R(baby.key, day, 1992));
                float mid = (start + end) * 0.5f;
                // The parent first, the other if the first is busy at the middle of the window.
                int turn = (int)(H(baby.key, day, 1993) % (uint)carers.Length);
                for (int k = 0; k < carers.Length; k++)
                {
                    var c = carers[(turn + k) % carers.Length];
                    var seg = DayOf(c)[Find(DayOf(c), mid)];
                    bool free = CanHold(seg.kind, false);
                    // Free hands: in the arms (now and then on the back). Busy but not carrying a load: on the back, so a family
                    // at work is still seen with its baby.
                    if (free || CanHold(seg.kind, true))
                    {
                        plan = new CarryPlan { carer = c, start = start, end = end, onBack = !free || R(baby.key, day, 1994) < BackShare };
                        break;
                    }
                }
            }
            carryPlans[baby.key] = plan;
            return plan;
        }

        /// <summary>How many of the babies carried while their carer's hands are free ride on the back.</summary>
        public const float BackShare = 0.3f;

        /// <summary>Can they have a baby on them: in the arms, not in a work loop, a carry, a chore or a duty; on the back, in
        /// any of those but a carry. Never asleep, in the water or in the snow.</summary>
        static bool CanHold(ActivityKind k, bool onBack) =>
            (!ActivityInfo.IsBusy(k) || (onBack && ActivityInfo.GroupOf(k) != ActivityGroup.Carry)) && k != ActivityKind.None && k != ActivityKind.Sleep && k != ActivityKind.Nap &&
            k != ActivityKind.Swim && k != ActivityKind.Splash && k != ActivityKind.Stomp && k != ActivityKind.Snowball;

        void ApplyCarry(Person p, ref PersonActivity a, ViewTime t)
        {
            BuildCarers();
            if (p.stage == LifeStage.Baby)
            {
                var plan = CarryOf(p);
                if (plan.carer != null && t.hour >= plan.start && t.hour < plan.end)
                {
                    var ca = PlanBase(plan.carer, t);
                    if (CanHold(ca.kind, plan.onBack)) { a.carriedBy = plan.carer.who; a.carryOnBack = plan.onBack; }
                }
                return;
            }
            if (!carerBabies.TryGetValue(p.key, out var babies) || !CanHold(a.kind, true)) return;
            foreach (var b in babies)
            {
                var plan = CarryOf(b);
                if (plan.carer != p || t.hour < plan.start || t.hour >= plan.end || !CanHold(a.kind, plan.onBack)) continue;
                a.carrying = b.who;
                a.carryOnBack = plan.onBack;
                return;
            }
        }

        /// <summary>The baby this person has on them at a view time (None when nobody). For the view and the person card.</summary>
        public PersonRef CarryingAt(PersonRef who, ViewTime t) => Plan(who, t).carrying;

        /// <summary>Who has this baby at a view time (None when the baby is at home).</summary>
        public PersonRef CarriedByAt(PersonRef baby, ViewTime t) => Plan(baby, t).carriedBy;

        /// <summary>Phrase for the job report: ", with Name in their arms" (empty when they carry no one).</summary>
        string CarryClause(PersonActivity a)
        {
            if (a.carrying.IsNone || !byKey.TryGetValue(a.carrying.Key, out var b)) return "";
            string name = b.n != null ? b.n.FullName : "a baby";
            return a.carryOnBack ? $", {name} on their back" : $", {name} in their arms";
        }
    }
}
