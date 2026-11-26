// #776 (animation-specs B81.a): the raising of the founder's statue, the one held Story scene (see StatueScenes.cs).
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    public static partial class StatueScenes
    {
        /// <summary>A scene's members with no gaps: a part that starts after the member's last one ended is preceded by a hold where they stood (a gap
        /// would send them back to their plan in the middle of the scene).</summary>
        sealed class Track
        {
            readonly CampScene s;
            readonly Dictionary<int, float> end = new();
            readonly Dictionary<int, Vector2> pos = new(), face = new();
            public Track(CampScene scene) { s = scene; }

            public CampPart Part(int m, float from, float to, Vector2 at, Vector2 faceTo, DollStyle style, CampPoseFn pose = null, PropKind prop = PropKind.None, float speed = Walk)
            {
                if (end.TryGetValue(m, out float e) && from > e + 1e-3f) s.Part(m, e, from, pos[m], face[m], DollStyle.Stand);
                var p = s.Part(m, from, to, at, faceTo, style, pose, prop, speed);
                end[m] = to; pos[m] = at; face[m] = faceTo;
                return p;
            }

            /// <summary>Walks to <paramref name="to"/> from where they last were (a part as long as the walk needs), returns the time they arrive.</summary>
            public float WalkTo(int m, float from, Vector2 to, Vector2 faceTo, float speed, Vector2? start = null)
            {
                float w = WalkTime(pos.TryGetValue(m, out var p) ? p : start ?? to, to, speed);
                Part(m, from, from + w, to, faceTo, DollStyle.Stand, null, PropKind.None, speed);
                return from + w;
            }

            public float EndOf(int m) => end.TryGetValue(m, out float e) ? e : 0f;
        }

        /// <summary>
        /// B81.a (about 30 s): the carver (<paramref name="carver"/>; a mason first) and the helper (Nobody: alone) roll a block (<see cref="CampScene.p0"/>
        /// → <paramref name="spot"/>) onto the plinth, both leaning in, a puff of dust at the seat (<see cref="CampScene.a"/>); the helper wipes the
        /// hands and steps away; the carver circles the block once and stands at the camera's side working three rounds of three strokes, a hit at the end
        /// of each (<see cref="CampScene.b"/>, <see cref="CampScene.c"/>, <see cref="CampScene.d"/>: the block becomes the shoulders, the head, the
        /// figure); the family and the watchers stand in a half ring on the camera's side looking at it (kids peeking); the carver steps back, hands on
        /// the hips, and the oldest of the family nods; then the youngest kid (else the oldest) carries a bunch to the foot, kneels and lays it on the
        /// lower step (<see cref="CampScene.p1"/>.y), the others come one by one and put a hand on the plinth, and all step back.
        /// <see cref="CampScene.p1"/>.x: the roll's start. The plinth, the block and the carved stages are drawn by CampLife.
        /// </summary>
        public static CampScene Raise(int carver, Vector2 carverAt, int helper, Vector2 helperAt,
            IList<int> family, IList<Vector2> familyAt, IList<bool> familyKid, IList<int> watch, IList<Vector2> watchAt, IList<bool> watchKid,
            Vector2 spot, Vector2 from, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.StatueRaise, t0, spot) { holdFocus = true, p0 = from };
            var T = new Track(s);
            int mc = s.Add(carver), mh = helper != Nobody ? s.Add(helper) : -1;
            var pushers = mh >= 0 ? new[] { mc, mh } : new[] { mc };
            var dir = Unit(spot - from, -acr); float dist = (spot - from).magnitude;
            var perp = new Vector2(-dir.y, dir.x);
            Vector2 PushAt(Vector2 block, int i) => block - dir * Behind + perp * (pushers.Length == 1 ? 0f : i == 0 ? 0.03f : -0.03f);
            var ats = mh >= 0 ? new[] { carverAt, helperAt } : new[] { carverAt };
            // 1 the block: both hurry to it, brace, and push it onto the plinth
            var walks = new float[pushers.Length]; var speeds = new float[pushers.Length];
            float t = 0f;
            for (int i = 0; i < pushers.Length; i++)
            {
                speeds[i] = Mathf.Max(Walk, (PushAt(from, i) - ats[i]).magnitude / ApproachFor);
                walks[i] = WalkTime(ats[i], PushAt(from, i), speeds[i]);
                t = Mathf.Max(t, walks[i]);
            }
            for (int i = 0; i < pushers.Length; i++)
            {
                T.Part(pushers[i], 0f, walks[i], PushAt(from, i), PushAt(from, i) + dir, DollStyle.Stand, null, PropKind.None, speeds[i]);
                T.Part(pushers[i], t, t + 0.4f, PushAt(from, i), PushAt(from, i) + dir, DollStyle.Stand, Brace);
            }
            t += 0.4f;
            float roll = Mathf.Max(1.6f, dist / RollSpeed);
            s.p1 = new Vector2(t, 0f);
            for (int i = 0; i < pushers.Length; i++)
            {
                var p = T.Part(pushers[i], t, t + roll, PushAt(spot, i), PushAt(spot, i) + dir, DollStyle.Stand, Push, PropKind.None, Mathf.Max(0.02f, dist / roll));
                p.poseWalking = true;
            }
            for (int q = 1; q <= 3; q++) s.Fx(t + roll * q / 4f, CampFxKind.Dust, G(Vector2.Lerp(from, spot, q / 4f), 0.005f), 0.3f, 2);
            t += roll;
            s.a = t; // the block seats: it hops up onto the plinth (BlockNow)
            s.Fx(t, CampFxKind.Dust, G(spot, 0.01f), 0.8f, 5);
            s.Fx(t, CampFxKind.Chips, G(spot, FeetY + 0.05f), 0.4f, 3);
            // the helper wipes the hands and steps out of the way
            var helperAway = spot + Rot(toCam, 75f) * 0.55f;
            if (mh >= 0)
            {
                T.Part(mh, t, t + 1.2f, PushAt(spot, 1), spot, DollStyle.Stand, Wipe);
                T.WalkTo(mh, t + 1.2f, helperAway, spot, Walk);
            }
            // 2 the carving: the carver circles the block once, then stands at the camera's side for three rounds
            var work = Front(spot, toCam, WorkAt);
            float r = 0.17f;
            var from0 = PushAt(spot, 0);
            float a0 = Mathf.Atan2(from0.y - spot.y, from0.x - spot.x) * Mathf.Rad2Deg, a1 = Mathf.Atan2(toCam.y, toCam.x) * Mathf.Rad2Deg;
            float sweep = Mathf.Repeat(a1 - a0, 360f); if (sweep < 180f) sweep += 360f;
            int steps = Mathf.Max(3, Mathf.CeilToInt(sweep / 60f));
            var prev = from0; float tc = t + 0.4f;
            T.Part(mc, t, tc, from0, spot, DollStyle.Stand);
            float pace = 2f * Mathf.PI * r / 3f; // one turn in about 3 s
            for (int k = 1; k <= steps; k++)
            {
                float ang = (a0 + sweep * k / steps) * Mathf.Deg2Rad;
                var at = k == steps ? work : spot + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                float w = Mathf.Max(0.25f, (at - prev).magnitude / pace);
                T.Part(mc, tc, tc + w, at, spot, DollStyle.Stand, null, PropKind.None, Mathf.Max(Walk, (at - prev).magnitude / w));
                tc += w; prev = at;
            }
            var hits = new float[Rounds];
            for (int rd = 0; rd < Rounds; rd++)
            {
                T.Part(mc, tc, tc + RoundFor, work, spot, DollStyle.Stand, Chisel);
                s.Tip(mc, tc, tc + RoundFor, CampTipKind.Stone, 1.8f);
                for (int k = 0; k < Strokes; k++) s.Fx(tc + 0.15f + (k + 0.5f) * StrokeEvery, CampFxKind.Chips, G(spot, FeetY + 0.1f), 0.3f, 2);
                hits[rd] = tc + RoundFor - 0.05f;
                s.Fx(hits[rd], CampFxKind.Chips, G(spot, FeetY + 0.1f), 0.5f, 4); // the hit: the stage steps on
                tc += RoundFor;
                if (rd < Rounds - 1) { T.Part(mc, tc, tc + RoundGap, work, spot, DollStyle.Stand); tc += RoundGap; }
            }
            s.b = hits[0]; s.c = hits[1]; s.d = hits[2];
            // the family and the watchers: a half ring on the camera's side, looking at the block (kids peeking), from the roll on
            var ringM = new List<int>(); var ringSpots = new List<Vector2>(); var ringSpeed = new List<float>(); var ringKid = new List<bool>(); var ringIsFamily = new List<bool>();
            void Seat(IList<int> keys, IList<Vector2> now, IList<bool> kid, float radius, float angBase, float angStep, bool fam)
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    float ang = (i % 2 == 0 ? 1f : -1f) * (angBase + angStep * (i / 2));
                    float rr = kid[i] ? Mathf.Min(radius, KidR) : radius;
                    int m = s.Add(keys[i]);
                    var to = spot + Rot(toCam, ang) * rr; float sp = kid[i] ? KidWalk : Walk;
                    ringM.Add(m); ringSpots.Add(to); ringSpeed.Add(sp); ringKid.Add(kid[i]); ringIsFamily.Add(fam);
                    float arrive = T.WalkTo(m, 1.0f, to, spot, sp, now[i]);
                    if (tc + 0.4f > arrive + 1e-3f) T.Part(m, arrive, tc + 0.4f, to, spot, kid[i] ? DollStyle.Peek : DollStyle.Look);
                }
            }
            Seat(family, familyAt, familyKid, FamilyR, 22f, 26f, true);
            Seat(watch, watchAt, watchKid, WatchR, 55f, 24f, false);
            // 3 done: the carver steps back, hands on the hips, looks at the family; the oldest of the family nods
            var back = Front(spot, toCam, 0.32f);
            float tb = tc + 0.2f;
            T.Part(mc, tb, tb + 1.2f, back, spot, DollStyle.Stand, null, PropKind.None, Mathf.Max(Walk, (back - work).magnitude / 1.2f));
            T.Part(mc, tb + 1.2f, tb + 2.2f, back, spot, DollStyle.Stand);
            T.Part(mc, tb + 2.2f, tb + 3.4f, back, spot, DollStyle.Stand, HipsPose);
            float tn = tb + 3.4f;
            var famCentre = ringSpots.Count > 0 ? ringSpots[0] : back + toCam * 0.1f;
            T.Part(mc, tn, tn + 0.8f, back, famCentre, DollStyle.Stand);
            if (family.Count > 0) T.Part(ringM[0], tn, tn + 0.8f, ringSpots[0], spot, DollStyle.Stand, SlowNod);
            else T.Part(mc, tn + 0.8f, tn + 1.6f, back, spot, DollStyle.Stand, SlowNod);
            float t4 = tn + (family.Count > 0 ? 1.0f : 1.7f);
            // 4 the first flowers: the youngest kid (else the oldest) carries a bunch to the foot, kneels, lays it, touches the plinth
            int bearer = -1;
            for (int i = 0; i < family.Count; i++) if (familyKid[i]) bearer = i; // the youngest kid: the family is oldest first
            if (bearer < 0 && family.Count > 0) bearer = 0;
            int bm = bearer >= 0 ? ringM[bearer] : mc;
            float bSpeed = bearer >= 0 ? ringSpeed[bearer] : Walk;
            var foot = Front(spot, toCam, KneelAt);
            float arrived = T.WalkTo(bm, t4, foot, spot, bSpeed);
            s.Tip(bm, t4, arrived + 0.8f, CampTipKind.Bunch, 1.2f);
            T.Part(bm, arrived, arrived + 1.6f, foot, spot, DollStyle.Stand, KneelPose);
            float lay = arrived + 0.8f;
            s.p1 = new Vector2(s.p1.x, lay);
            s.Fx(lay, CampFxKind.Dust, G(Front(spot, toCam, 0.07f), StepH), 0.2f, 1);
            float up = arrived + 1.6f;
            T.Part(bm, up, up + 1.4f, foot, spot, DollStyle.Stand, HandOn);
            // the others come one by one and put a hand on the plinth, then everyone steps back
            var sideSpots = new[] { 55f, -55f, 105f, -105f, 150f, -150f };
            float tq = up + 0.4f, end = up + 1.4f; int si = 0;
            for (int i = 0; i < ringM.Count; i++)
            {
                if (ringM[i] == bm || si >= sideSpots.Length) continue;
                var to = spot + Rot(toCam, sideSpots[si++]) * 0.14f;
                float at2 = T.WalkTo(ringM[i], tq, to, spot, ringSpeed[i]);
                T.Part(ringM[i], at2, at2 + 1.0f, to, spot, DollStyle.Stand, HandOn);
                end = Mathf.Max(end, at2 + 1.0f);
                tq += 0.8f;
            }
            float tEnd = Mathf.Max(end, tq + 0.4f) + 0.2f;
            for (int i = 0; i < ringM.Count; i++)
            {
                var outTo = spot + Rot(toCam, (i % 2 == 0 ? 1f : -1f) * (30f + 20f * (i / 2))) * (WatchR - 0.1f);
                T.Part(ringM[i], tEnd, tEnd + 1.0f, outTo, outTo + toCam, DollStyle.Stand, null, PropKind.None, ringSpeed[i]);
            }
            T.Part(mc, tEnd, tEnd + 1.0f, back + toCam * 0.15f, back + toCam, DollStyle.Stand, null, PropKind.None, Walk);
            if (mh >= 0) T.Part(mh, tEnd, tEnd + 1.0f, helperAway + toCam * 0.1f, helperAway + toCam, DollStyle.Stand, null, PropKind.None, Walk);
            return s;
        }
    }
}
