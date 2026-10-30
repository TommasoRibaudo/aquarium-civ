using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// The people's answer to a Keeper's shaping, staged in the world (#85; <see cref="ShapingAnswers"/>): the credited
    /// notable walks to the gift at the walking pace, works it, helpers and kids gather, and they thank the Keeper at the
    /// glass. Runs on the relic moments' staging (<see cref="Staging"/>, <see cref="Cast"/>): no sim dice, no sim writes.
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>
        /// Stage the people's answer to shaping <paramref name="nudgeId"/> now: the view calls it when the sim credits the
        /// shaping (<see cref="Nudge.credited"/>) while it watches, the debug panel after "skip to the answer". The spot is
        /// the answer's building (the new field, mill, pasture, jetty) once it stands on its lot, else the gift itself (a
        /// harvest when the box is full). The credited notable plays the owner; the nearest grown-up who can walk there in
        /// time stands in for one who can't. Null when the shaping isn't answered or nobody can play it.
        /// </summary>
        public RelicMomentInfo StartShapingAnswer(int nudgeId, ViewTime at)
        {
            Sync();
            EnsureDay(at.day);
            Nudge n = null;
            foreach (var x in sim.State.nudges) if (x.id == nudgeId) { n = x; break; }
            if (n == null || !n.credited) return null;
            var spot = ShapingAnswerPlace(n);
            if (spot.IsNone) return null;
            string type = null;
            if (n.buildingId >= 0) foreach (var b in sim.State.buildings) if (b.id == n.buildingId) { type = b.type; break; }
            var def = ShapingAnswers.For(n.kind, n.variant, era, type);
            float end = EndAfter(at, def.seconds);
            var st = new Staging { nudgeId = nudgeId, def = def, glass = GlassPlace, spot = spot, start = at.hour, end = end };
            var used = new HashSet<int>();
            var credited = Alive(ShapingAnswers.CreditedNotableId(sim, n));
            // Far off: the credited notable comes over for longer and works less, so it's still them who answers.
            if (credited != null && credited.stage != LifeStage.Baby && Part(st, credited, def.For(RelicCast.Owner), at, false) == null)
            {
                var late = ShapingAnswers.For(n.kind, n.variant, era, type, true);
                st.def = late;
                if (Part(st, credited, late.For(RelicCast.Owner), at, false) == null) st.def = def;
            }
            Cast(st, RelicCast.Owner, credited, at, used);
            CastCrowd(st, RelicCast.Grown, at, used, st.def.maxGrown, q => q.Adult);
            CastCrowd(st, RelicCast.Kids, at, used, st.def.maxKids, q => q.stage == LifeStage.Kid);
            if (st.segs.Count == 0) return null;
            AddLive(SceneKind.ShapingAnswer, at, end, st);
            return st.Info(at.day);
        }

        /// <summary>Where a shaping's answer is shown: its building's place once the building is on a lot, else the gift.</summary>
        public Place ShapingAnswerPlace(Nudge n)
        {
            if (n == null) return Place.None;
            Sync();
            if (n.buildingId >= 0)
                foreach (var b in sim.State.buildings)
                    if (b.id == n.buildingId && b.lotId != 0)
                    {
                        var p = PlaceOf(b);
                        if (IsValidPlace(p)) return p;
                    }
            var g = Place.Land(PlaceKind.Nudge, n.id);
            return IsValidPlace(g) ? g : Place.None;
        }

        /// <summary>The answer moments playing at <paramref name="t"/> (the view's highlight and bubble, the debug readout).</summary>
        public int ActiveShapingAnswers(ViewTime t, List<RelicMomentInfo> into)
        {
            into.Clear();
            foreach (var sc in live)
                if (sc.kind == SceneKind.ShapingAnswer && sc.staging != null && sc.day == t.day && t.hour >= sc.start && t.hour < sc.end)
                    into.Add(sc.staging.Info(sc.day));
            return into.Count;
        }
    }
}
