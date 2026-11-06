using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>
    /// User 2026-11-06 "Toddling is fine": the youngest walking-age children toddle after their carer in the normal game. #192 kept babies
    /// off their feet ("babies are not separate walkers, which keeps them cheap on a phone"), so this keeps it cheap: only a baby of
    /// <see cref="ToddleAge"/> or more (a walker; under that it is a babe in arms), only on its carer's own carry (the same 1–3 hour
    /// window, the same carer), only while the carer's hands are free and they stay about the camp (standing, sitting, a short walk of
    /// <see cref="ShortWalk"/> u or less), never in a storm or snow, and at most <see cref="MaxOnFoot"/> toddlers in the box on a day
    /// (a hash of (baby, day) picks them: no sim dice, no sim writes). A long walk, busy hands or the night: carried, as before.
    /// The view draws the baby as a small walker following the carer (View/Beats/ToddleFollow.cs, PeopleView).
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>Walking age (years): a baby this old or more toddles; younger ones are always carried. The sim's baby stage ends at 4.</summary>
        public const float ToddleAge = 1f;
        /// <summary>#192's cap: the most toddlers on their feet in a box on one day.</summary>
        public const int MaxOnFoot = 3;
        /// <summary>A walk this long or shorter (box units, straight line) is toddled; a longer one is carried.</summary>
        public const float ShortWalk = 1.2f;

        /// <summary>The cheat "Toddlers walk now": every walking-age baby is out all day with its carer and toddles whenever it may, past the
        /// daily cap. Static so it outlives a box swap; <see cref="SetDebugToddlers"/> re-plans.</summary>
        public static bool DebugToddlers { get; private set; }

        public void SetDebugToddlers(bool on) { DebugToddlers = on; Invalidate(); carrySig = toddleSig = long.MinValue; } // the carers are rebuilt with the people

        /// <summary>The cheat "Toddlers walk: all / normal". Turning it on with no walking-age baby in the box adds one (a birth two years back).</summary>
        public string DebugToddlersToggle()
        {
            bool on = !DebugToddlers;
            string born = "";
            if (on)
            {
                Sync();
                bool any = false;
                foreach (var q in people) if (q.stage == LifeStage.Baby && q.age >= ToddleAge) { any = true; break; }
                if (!any)
                {
                    var n = sim.DebugBirth();
                    if (n == null) return "toddlers: no grown-up to be a parent";
                    n.age = 2f;
                    born = $", {n.FullName} (2) added";
                }
            }
            SetDebugToddlers(on);
            return on ? $"toddlers walk after their carer all day (8:00–17:30){born}" : "toddlers: as the day says";
        }

        long toddleDay = long.MinValue;
        readonly HashSet<int> toddlersToday = new HashSet<int>();
        readonly List<Person> toddleBuf = new List<Person>();

        /// <summary>Is this baby on its own feet after its carer now (the carer's activity <paramref name="carer"/>, already planned)?</summary>
        bool OnFoot(Person baby, in PersonActivity carer, ViewTime t)
        {
            if (baby.stage != LifeStage.Baby || baby.age < ToddleAge) return false;
            if (!CanHold(carer.kind, false) || carer.kind is ActivityKind.Indoors or ActivityKind.Toddle) return false; // busy hands, asleep, in the water, indoors
            if (carer.hunt != HuntPhase.None || carer.tent != TentPhase.None) return false;
            if (dayInfo.weather is Weather.Storm or Weather.Snow) return false;
            if (carer.moving && !ShortWalkBetween(carer.from, carer.place)) return false;
            return DebugToddlers || ToddlesToday(baby);
        }

        bool ShortWalkBetween(Place from, Place to)
        {
            if (from == to) return true;
            if (!positions.TryGetPosition(from, out float ax, out float az) || !positions.TryGetPosition(to, out float bx, out float bz)) return false;
            float dx = ax - bx, dz = az - bz;
            return dx * dx + dz * dz <= ShortWalk * ShortWalk;
        }

        /// <summary>The <see cref="MaxOnFoot"/> walking-age babies picked today (a hash of (baby, day) orders them).</summary>
        bool ToddlesToday(Person baby)
        {
            long day = dayInfo.day;
            if (toddleDay != day || toddleSig != snapSig)
            {
                toddleDay = day;
                toddleSig = snapSig;
                toddlersToday.Clear();
                toddleBuf.Clear();
                foreach (var q in people) if (q.stage == LifeStage.Baby && q.age >= ToddleAge) toddleBuf.Add(q);
                toddleBuf.Sort((x, y) => { int c = H(x.key, day, 1996).CompareTo(H(y.key, day, 1996)); return c != 0 ? c : x.key.CompareTo(y.key); });
                for (int i = 0; i < toddleBuf.Count && i < MaxOnFoot; i++) toddlersToday.Add(toddleBuf[i].key);
            }
            return toddlersToday.Contains(baby.key);
        }

        long toddleSig = long.MinValue;

        // ---------------------------------------------------------------- family, for the view's hugs (View/HugDirector.cs)

        /// <summary>A notable's partner (None for the crowd or the unpartnered).</summary>
        public PersonRef PartnerOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) && p.partner != null ? p.partner.who : PersonRef.None; }

        /// <summary>A child's parent (None when unknown: the crowd).</summary>
        public PersonRef ParentOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) && p.parent != null ? p.parent.who : PersonRef.None; }

        /// <summary>Age in years as the planner sees it.</summary>
        public float AgeOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) ? p.age : 0f; }
    }
}
