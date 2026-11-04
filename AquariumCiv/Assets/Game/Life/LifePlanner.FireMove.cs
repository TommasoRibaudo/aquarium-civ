using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #246 (keepers-hands §2.1): the tribe carries its fire to the first green, as a scene the view plays
    /// (<see cref="FireMoveScene"/>). The sim moves the hearth when the Keeper's first green comes up
    /// (<see cref="SimCore.EcoFireMove"/>), so from then the fire, the gathering, the stash and the fire-side work are at
    /// the new spot; this stages the people who carry it over from the old one (<see cref="PlaceKind.OldFire"/>). Staging
    /// only: no sim dice, no sim writes.
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>Where the fire stood before the tribe carried it (a place while a carry is on record).</summary>
        public static readonly Place OldFirePlace = Place.Land(PlaceKind.OldFire);

        /// <summary>The sim's carry number this planner staged last (0 = none). Not saved: after a restart the fire simply
        /// burns at its new spot.</summary>
        int fireMoveStaged;

        /// <summary>
        /// A carry the people haven't played yet: the sim moved the fire this month or last, and this planner hasn't staged it.
        /// The view asks <see cref="StartFireMove"/> while this is true (and draws the fire at the old spot meanwhile).
        /// </summary>
        public bool FireMovePending
        {
            get
            {
                Sync();
                var m = sim.EcoFireMove;
                return m.moved && m.count != fireMoveStaged && sim.State.month - m.month <= 1 && m.Distance >= SimCore.FireStaysWithin;
            }
        }

        /// <summary>
        /// Stage the carry now: the chief (or the nearest grown-up who can get there) scoops the embers, up to two grown-ups lift
        /// the hearth stones, up to two kids run along, and those too far off point. Returns the scene playing (also while it
        /// plays, if asked again), or null when there's nothing to carry or nobody can (night, or everyone asleep or in a moment:
        /// ask again, every frame if you like, while <see cref="FireMovePending"/>). <paramref name="force"/> (debug) plays the
        /// last carry again.
        /// </summary>
        public RelicMomentInfo StartFireMove(ViewTime at, bool force = false)
        {
            Sync();
            EnsureDay(at.day);
            var m = sim.EcoFireMove;
            if (!m.moved) return null;
            foreach (var sc in live)
                if (sc.kind == SceneKind.FireMove && sc.day == at.day && at.hour < sc.end) return sc.staging.Info(sc.day);
            if (!force && !FireMovePending) return null;
            if (!IsValidPlace(OldFirePlace) || !IsValidPlace(fire)) return null;

            float seconds = FireMoveScene.SecondsFor(WalkSecs(WalkLen(OldFirePlace, fire)));
            if (at.hour < LifeClock.Dawn || at.hour >= LifeClock.Night) return null; // not in the dark: in the morning
            var done = LifeClock.Advance(at, seconds);
            if (done.day != at.day || done.hour >= LifeClock.Night) return null;

            var st = StageFireMove(seconds, at);
            if (st == null) return null;
            AddLive(SceneKind.FireMove, at, st.end, st);
            fireMoveStaged = m.count;
            return st.Info(at.day);
        }

        Staging StageFireMove(float seconds, ViewTime at)
        {
            var def = FireMoveScene.For(seconds);
            var st = new Staging { def = def, glass = GlassPlace, spot = fire, start = at.hour, end = EndAfter(at, seconds) };
            var used = new HashSet<int>();
            // The chief carries the embers if they can get to the old fire in time; else whoever is nearest and up.
            if (leader != null && FreeToReact(leader, at))
            {
                var part = Part(st, leader, def.For(RelicCast.Owner), at, false);
                if (part != null) Assign(st, leader, RelicCast.Owner, part, used);
            }
            if (used.Count == 0) CastCrowd(st, RelicCast.Owner, at, used, 1, q => q.Grown && FreeToReact(q, at));
            if (used.Count == 0) return null; // nobody carries it: try again later
            CastCrowd(st, RelicCast.Grown, at, used, def.maxGrown, q => q.Grown && FreeToReact(q, at));
            CastCrowd(st, RelicCast.Kids, at, used, def.maxKids, q => q.stage == LifeStage.Kid && FreeToReact(q, at));
            CastCrowd(st, RelicCast.Onlookers, at, used, def.maxQueue, q => q.stage != LifeStage.Baby && FreeToReact(q, at));
            return st;
        }

        /// <summary>The carry playing at <paramref name="t"/> (the view's fire and camera hint, the debug readout); null if none.</summary>
        public RelicMomentInfo ActiveFireMove(ViewTime t)
        {
            foreach (var sc in live)
                if (sc.kind == SceneKind.FireMove && sc.day == t.day && t.hour >= sc.start && t.hour < sc.end) return sc.staging.Info(sc.day);
            return null;
        }

        /// <summary>
        /// How to draw the fire while it is carried: false when no carry plays at <paramref name="t"/> (draw the fire where the
        /// sim has it, unless <see cref="FireMovePending"/>: then it still burns at the old spot).
        /// </summary>
        public bool FireMoveAt(ViewTime t, out FireMoveLook look)
        {
            look = default;
            foreach (var sc in live)
            {
                if (sc.kind != SceneKind.FireMove || sc.day != t.day || t.hour < sc.start || t.hour >= sc.end) continue;
                var m = sim.EcoFireMove;
                var st = sc.staging;
                look = new FireMoveLook
                {
                    fromX = m.fromX, fromZ = m.fromZ, toX = m.toX, toZ = m.toZ, day = sc.day,
                    start = st.start, liftHour = st.T(FireMoveScene.Lift), setHour = st.T(FireMoveScene.Set), end = st.end,
                };
                look.phase = t.hour < look.liftHour ? 0 : t.hour < look.setHour ? 1 : 2;
                return true;
            }
            return false;
        }
    }
}
