// #692 part 5 (docs/design/animation-specs.md "#692 part 5: the rest of the camp"), part two: B47-B53 (reeds, the stream bed, the hill, paths,
// the remembering stone, the tents' lanterns, weeds and the rope swing) as pure builders. Shared helpers and B40-B46: CampScenes5.cs.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    public static class CampScenes5b
    {
        /// <summary>#695: no one in an optional part (a crowd member's key is negative, so −1 is a person).</summary>
        const int Nobody = VillageScenes.Nobody;
        // ================================================================== B47.a cut reeds to weave (work)
        public const float WeaveFor = 4f;

        /// <summary>B47.a: the weaver wades in at the reeds (sunk 0.03 u), grabs a bunch (arm x 80), cuts low (x 80 → 40, Snap → hit: the tuft
        /// flattens, reed-coloured blades), carries an armful (the reed bundle, in both arms) to the tent and weaves at the door (SitWeave).
        /// <see cref="CampScene.a"/>: the cut.</summary>
        public static CampScene CutReeds(int weaver, Vector2 at, Vector2 reeds, Vector2 toWater, Vector2 door, Vector2 doorOut, float t0)
        {
            toWater = Unit(toWater, Vector2.up);
            var s = new CampScene(CampUse.CutReeds, t0, reeds) { followReach = 4f }; // #692 p5 retakes: the camera stays with the armful all the way to the tent
            int m = s.Add(weaver);
            var edge = reeds - toWater * 0.05f;
            float t = WalkTime(at, edge, Walk);
            s.Part(m, 0f, t, edge, reeds, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.6f, reeds, reeds + toWater, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y -= 0.03f * In(u, 0f, 0.5f); }, PropKind.None, 0.1f).poseWalking = true;
            t += 0.6f;
            for (int i = 0; i < 2; i++)
            {
                s.Part(m, t, t + 0.4f, reeds, reeds + toWater, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y -= 0.03f; p.armR = Lerp(p.armR, new Vector3(80f, 0f, 6f), In(u, 0f, 0.3f)); p.lean += 10f; });
                t += 0.4f;
                s.Part(m, t, t + 0.35f, reeds, reeds + toWater, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y -= 0.03f; float k = Smooth(u / 0.15f); p.armR = new Vector3(Mathf.Lerp(80f, 40f, k), 0f, 6f); p.lean += 10f + 15f * k; });
                if (i == 0) { s.a = t + 0.15f; Leave(s, t + 0.15f, CampMark.ReedsFlat, reeds); }
                s.Fx(t + 0.15f, CampFxKind.Grass, G(reeds, 0.05f), 0.6f, 4);
                t += 0.35f;
            }
            float wade = 0.5f;
            s.Part(m, t, t + wade, edge, edge - toWater, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y -= 0.03f * (1f - In(u, 0f, 0.4f)); }, PropKind.None, 0.15f).poseWalking = true;
            t += wade;
            doorOut = Unit(doorOut, Vector2.down);
            var seat = door + doorOut * 0.07f;
            float w = WalkTime(edge, seat, Walk);
            s.Part(m, t, t + w, seat, seat + doorOut, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(60f, 0f, 24f); p.armR = new Vector3(60f, 0f, -24f); }, PropKind.None, Walk).poseWalking = true;
            s.Tip(m, s.a + 0.2f, t + w + 0.3f, CampTipKind.Reeds);
            t += w;
            s.Part(m, t, t + WeaveFor, seat, seat + doorOut, DollStyle.SitWeave);
            return s;
        }

        // ================================================================== B47.b reed whistle
        /// <summary>B47.b: B33.d's whistle with a reed held like a flute (arms x 110 forward, hands 0.03 u apart): the grass whistle's timeline, the
        /// blade swapped for a reed and the hold turned forward.</summary>
        public static CampScene ReedWhistle(int kid, Vector2 at, Vector2 reeds, IList<int> others, IList<Vector2> othersAt, float t0, Vector2 across)
        {
            var s = CampScenes3.GrassWhistle(kid, at, reeds, others, othersAt, t0, across);
            s.use = CampUse.ReedWhistle;
            for (int i = 0; i < s.tips.Count; i++) if (s.tips[i].kind == CampTipKind.Blade) { var tp = s.tips[i]; tp.kind = CampTipKind.Reed; s.tips[i] = tp; }
            if (s.Count > 0)
                foreach (var part in s.parts[0])
                {
                    var orig = part.pose;
                    if (orig == null) continue;
                    part.pose = (ref DollPose p, float u, float l) =>
                    {
                        orig(ref p, u, l);
                        if (p.armR.x > 100f && p.armL.x > 100f) { p.armL = new Vector3(110f, 0f, 6f); p.armR = new Vector3(110f, 0f, -6f); }
                    };
                }
            return s;
        }

        // ================================================================== B48.a follow the stream bed
        /// <summary>B48.a: 2–3 kids walk in a line along the stream bed's points (<paramref name="bed"/>), the leader balancing (arms z ∓90, roll ±6 at
        /// 1 Hz), hopping across it twice (0.05 u up). <see cref="CampScene.a"/>: the first hop.</summary>
        public static CampScene FollowStream(IList<int> kids, IList<Vector2> at, IList<Vector2> bed, float t0)
        {
            var s = new CampScene(CampUse.FollowStream, t0, bed.Count > 1 ? bed[1] : bed[0]) { followReach = 1f };
            int n = kids.Count;
            for (int i = 0; i < n; i++) s.Add(kids[i]);
            var first = bed[0];
            var dir0 = bed.Count > 1 ? Unit(bed[1] - bed[0], Vector2.right) : Vector2.right;
            float t = 0f;
            for (int i = 0; i < n; i++) t = Mathf.Max(t, WalkTime(at[i], first - dir0 * (0.08f * i), KidWalk));
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, first - dir0 * (0.08f * i), first + dir0, DollStyle.Stand, null, PropKind.None, KidWalk);
            float pace = KidWalk * 0.7f;
            int hops = 0;
            s.a = -1f;
            for (int q = 1; q < bed.Count; q++)
            {
                var a = bed[q - 1]; var b = bed[q];
                var d = Unit(b - a, dir0);
                float leg = (b - a).magnitude / pace + 0.2f;
                for (int i = 0; i < n; i++)
                {
                    var to = b - d * (0.08f * i);
                    bool lead = i == 0;
                    var part = s.Part(i, t, t + leg, to, to + d, DollStyle.Stand, lead ? (ref DollPose p, float u, float l) => { p.armL = new Vector3(10f, 0f, -90f); p.armR = new Vector3(10f, 0f, 90f); p.roll += 6f * Mathf.Sin(u * Mathf.PI * 2f); } : (CampPoseFn)null, PropKind.None, pace);
                    part.poseWalking = lead;
                }
                t += leg;
                if (hops < 2)
                {
                    // a hop across the bed: a side step 0.06 u with a hop (0.05 u up)
                    var side = new Vector2(-d.y, d.x) * (hops == 0 ? 1f : -1f);
                    for (int i = 0; i < n; i++)
                    {
                        var to = b - d * (0.08f * i) + side * (hops == 0 ? 0.06f : 0f);
                        s.Part(i, t + i * 0.15f, t + i * 0.15f + 0.45f, to, to + d, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += 0.05f * Bump(u, 0f, 0.4f); }, PropKind.None, 0.2f);
                        if (i < n - 1 && i * 0.15f + 0.45f < (n - 1) * 0.15f + 0.45f) s.Part(i, t + i * 0.15f + 0.45f, t + (n - 1) * 0.15f + 0.45f, to, to + d, DollStyle.Stand);
                        if (i > 0) s.Part(i, t, t + i * 0.15f, b - d * (0.08f * i), b + d, DollStyle.Stand);
                    }
                    if (s.a < 0f) s.a = t + 0.2f;
                    t += (n - 1) * 0.15f + 0.45f;
                    hops++;
                }
            }
            var end = s.parts[0][s.parts[0].Count - 1].pos;
            for (int i = 0; i < n; i++) s.Part(i, t, t + 1.2f, s.parts[i][s.parts[i].Count - 1].pos, end + Vector2.down, i == 0 ? DollStyle.Cheer : DollStyle.Laugh);
            return s;
        }

        // ================================================================== B48.b a little dam
        /// <summary>B48.b: two kids carry stones (B34.b's carry) and set 5 across the bed (a squash, droplets), pat mud on (arms x 40, brown drops);
        /// a small pool shows behind it (a <see cref="CampMark.DamPool"/>, r 0.06 u, a view day); then one pushes a stone out: a gush (6 droplets,
        /// <see cref="CampMark.DamBreak"/>) and they cheer. <see cref="CampScene.a"/>: the pool; <see cref="CampScene.b"/>: the gush.</summary>
        public static CampScene BuildDam(int k1, int k2, Vector2 at1, Vector2 at2, Vector2 bed, Vector2 along, Vector2 pile, float t0)
        {
            along = Unit(along, Vector2.right);
            var across = new Vector2(-along.y, along.x);
            var s = new CampScene(CampUse.BuildDam, t0, bed) { holdFocus = true };
            int a = s.Add(k1), b = k2 != Nobody ? s.Add(k2) : -1;
            var sa = bed - along * 0.06f + across * 0.03f; var sb = bed - along * 0.06f - across * 0.05f;
            float t = Mathf.Max(WalkTime(at1, pile, KidWalk), b >= 0 ? WalkTime(at2, pile + across * 0.06f, KidWalk) : 0f);
            s.Part(a, 0f, t, pile, bed, DollStyle.Stand, null, PropKind.None, KidWalk);
            if (b >= 0) s.Part(b, 0f, t, pile + across * 0.06f, bed, DollStyle.Stand, null, PropKind.None, KidWalk);
            // 5 stones in two trips (each kid carries some; the second kid a beat behind), set one by one across the bed
            int set = 0, kidsN = b >= 0 ? 2 : 1;
            int[,] per = b >= 0 ? new int[,] { { 2, 1 }, { 1, 1 } } : new int[,] { { 3, 0 }, { 2, 0 } };
            for (int trip = 0; trip < 2; trip++)
            {
                float tEnd = t;
                for (int who = 0; who < kidsN; who++)
                {
                    int m = who == 0 ? a : b;
                    var from = who == 0 ? pile : pile + across * 0.06f;
                    var st = who == 0 ? sa : sb;
                    float tt = t;
                    if (who == 1) { s.Part(m, tt, tt + 0.4f, from, bed, DollStyle.Stand); tt += 0.4f; }
                    s.Part(m, tt, tt + 0.6f, from, bed, DollStyle.Stand, CampScenes3.BendPickPose); tt += 0.6f;
                    float w = WalkTime(from, st, KidWalk);
                    s.Part(m, tt, tt + w, st, bed, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(60f, 0f, 24f); p.armR = new Vector3(60f, 0f, -24f); }, PropKind.None, KidWalk).poseWalking = true;
                    s.Tip(m, tt - 0.2f, tt + w + 0.4f, CampTipKind.Stones);
                    tt += w;
                    int n = Mathf.Min(per[trip, who], 5 - set);
                    s.Part(m, tt, tt + 0.5f * Mathf.Max(1, n), st, bed, DollStyle.Stand, CampScenes3.CrouchSetPose);
                    for (int i = 0; i < n; i++)
                    {
                        var stone = bed + across * ((set - 2) * 0.025f);
                        s.Fx(tt + 0.4f + 0.5f * i, CampFxKind.Droplets, G(stone, 0.01f), 0.35f, 3);
                        s.Fx(tt + 0.4f + 0.5f * i, CampFxKind.Leave, G(stone), 1f, (int)CampMark.Pebble); // a dam stone
                        set++;
                    }
                    tt += 0.5f * Mathf.Max(1, n);
                    if (trip == 0) { float back = WalkTime(st, from, KidWalk); s.Part(m, tt, tt + back, from, bed, DollStyle.Stand, null, PropKind.None, KidWalk); tt += back; }
                    tEnd = Mathf.Max(tEnd, tt);
                }
                // keep both in step: whoever finished first waits
                for (int who = 0; who < kidsN; who++)
                {
                    int m = who == 0 ? a : b;
                    float last = 0f; Vector2 lp = default;
                    foreach (var p in s.parts[m]) if (p.to > last) { last = p.to; lp = p.pos; }
                    if (last < tEnd) s.Part(m, last, tEnd, lp, bed, DollStyle.Stand);
                }
                t = tEnd;
            }
            // pat mud on
            s.Part(a, t, t + 1.2f, sa, bed, DollStyle.Stand, (ref DollPose p, float u, float l) => { CampScenes3.CrouchSetPose(ref p, 1f, l); p.armR.x = 40f + 12f * Mathf.Abs(Mathf.Sin(u * 3f * Mathf.PI)); p.armL = new Vector3(40f + 12f * Mathf.Abs(Mathf.Cos(u * 3f * Mathf.PI)), 0f, -6f); });
            if (b >= 0) s.Part(b, t, t + 1.2f, sb, bed, DollStyle.Stand, CampScenes3.CrouchSetPose);
            s.Fx(t + 0.4f, CampFxKind.Dust, G(bed, 0.01f), 0.3f, 3);
            t += 1.2f;
            s.a = t;
            Leave(s, t, CampMark.DamPool, bed - along * 0.07f, 0f, CampGeo5.DamPoolR);
            s.Part(a, t, t + 2f, sa, bed, DollStyle.Point);
            if (b >= 0) s.Part(b, t, t + 2f, sb, bed, DollStyle.Laugh);
            t += 2f;
            // one pushes a stone out: the gush
            s.Part(a, t, t + 0.5f, sa, bed, DollStyle.Stand, (ref DollPose p, float u, float l) => { CampScenes3.CrouchSetPose(ref p, 1f, l); float k = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(60f, 0f, 6f), k); p.lean += 10f * k; });
            s.b = t + 0.3f;
            s.Fx(t + 0.3f, CampFxKind.Droplets, G(bed + along * 0.03f, 0.01f), 0.7f, 6);
            Leave(s, t + 0.3f, CampMark.DamBreak, bed);
            t += 0.5f;
            s.Part(a, t, t + 1.3f, sa, bed, DollStyle.Cheer);
            if (b >= 0) s.Part(b, t - 0.5f, t + 1.3f, sb, bed, DollStyle.Cheer);
            return s;
        }

        // ================================================================== B49.a sit on top at dusk
        /// <summary>B49.a: 1–3 grown-ups walk up the hill and sit on the top facing the camp and the fire, chatting quietly (a nod, a hand now and
        /// then); they stay <paramref name="sitFor"/> s (till the lamps come on).</summary>
        public static CampScene HillDusk(IList<int> who, IList<Vector2> at, Vector2 top, Vector2 fire, float sitFor, float t0)
        {
            var toFire = Unit(fire - top, Vector2.down);
            var across = new Vector2(-toFire.y, toFire.x);
            var s = new CampScene(CampUse.HillDusk, t0, top) { holdFocus = true };
            float t = 0f;
            var spots = new Vector2[who.Count];
            for (int i = 0; i < who.Count; i++)
            {
                s.Add(who[i]);
                spots[i] = top + across * ((i - (who.Count - 1) * 0.5f) * 0.08f);
                t = Mathf.Max(t, WalkTime(at[i], spots[i], Walk));
            }
            for (int i = 0; i < who.Count; i++) s.Part(i, 0f, t, spots[i], spots[i] + toFire, DollStyle.Stand, null, PropKind.None, Walk);
            sitFor = Mathf.Max(8f, sitFor);
            for (int i = 0; i < who.Count; i++)
            {
                int ii = i;
                s.Part(i, t, t + sitFor, spots[i], spots[i] + toFire, DollStyle.Sit, (ref DollPose p, float u, float l) =>
                {
                    float talk = Bump(u % 4f, 1f + ii * 1.3f, 1.2f);
                    p.yaw += (ii % 2 == 0 ? 25f : -25f) * talk; // a turn to the other to talk
                    p.armR = Lerp(p.armR, new Vector3(60f, 0f, 12f), talk * 0.6f);
                    p.lean += 6f * Bump(u % 5f, 3f, 0.5f);
                });
            }
            return s;
        }

        // ================================================================== B49.b roll down it
        public const float RollEach = 0.25f, RollStep = 0.08f;

        /// <summary>B49.b: kids at the top lie down and roll down (B33.a's roll, faster: 0.25 s a roll, <paramref name="rolls"/> rolls of 0.08 u down
        /// the slope), grass blades; at the bottom they stand up dizzy (roll ±12 at 3 Hz, fading over 1.2 s), sit down hard, laugh, and run back up.
        /// <see cref="CampScene.a"/>: the first roll.</summary>
        public static CampScene HillRoll(IList<int> kids, IList<Vector2> at, Vector2 top, Vector2 down, int rolls, float t0)
        {
            down = Unit(down, Vector2.down);
            var across = new Vector2(-down.y, down.x);
            rolls = Mathf.Clamp(rolls, 6, 8);
            var s = new CampScene(CampUse.HillRoll, t0, top + down * RollStep * rolls * 0.5f) { followReach = 0.8f };
            float first = 99f;
            for (int i = 0; i < kids.Count; i++)
            {
                int m = s.Add(kids[i]);
                var start = top + across * ((i - (kids.Count - 1) * 0.5f) * 0.1f);
                // they lie across the slope (facing along it), so the log roll carries them down
                var face = start + across;
                float t = WalkTime(at[i], start, KidRun) + 0.6f * i;
                Go(s, m, 0f, t, start, face, KidRun, true);
                s.Part(m, t, t + 0.25f, start, face, DollStyle.Stand, CampScenes3.FlopPose);
                first = Mathf.Min(first, t); t += 0.25f;
                var p0 = start;
                for (int r = 0; r < rolls; r++)
                {
                    var p1 = start + down * RollStep * (r + 1);
                    s.Part(m, t, t + RollEach, p1, p1 + across, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                    {
                        float k = Mathf.Clamp01(u / l); p.roll = 90f; p.lean = 90f - 360f * k; p.offset.y = 0.03f + 0.008f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)); p.squash = 1f;
                        p.armL = new Vector3(80f, 0f, -10f); p.armR = new Vector3(80f, 0f, 10f);
                    }, PropKind.None, RollStep / RollEach * 1.05f);
                    if (r % 2 == 1) s.Fx(t + RollEach * 0.5f, CampFxKind.Grass, G(Vector2.Lerp(p0, p1, 0.5f), 0.02f), 0.5f, 3);
                    p0 = p1;
                    t += RollEach;
                }
                var bottom = p0;
                s.Part(m, t, t + 0.5f, bottom, bottom + down, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.5f); p.roll = 90f * (1f - k); p.lean = 90f * (1f - k); p.offset.y = 0.03f * (1f - k); }, PropKind.None, Snap);
                t += 0.5f;
                s.Part(m, t, t + 1.2f, bottom, bottom + down, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.roll += 12f * Mathf.Sin(u * 3f * Mathf.PI * 2f) * (1f - Mathf.Clamp01(u / 1.2f)); });
                t += 1.2f;
                s.Part(m, t, t + 1.2f, bottom, bottom + down, DollStyle.Laugh, (ref DollPose p, float u, float l) => Seated(ref p)); t += 1.2f;
                Go(s, m, t, WalkTime(bottom, start, KidRun * 0.8f), start, start - down, KidRun * 0.8f, true);
            }
            s.a = first + 0.25f;
            return s;
        }

        // ================================================================== B50.a hop and race on a path
        public const float RacePace = 0.55f;

        /// <summary>B50.a: 2–3 kids race from one end of a path (<paramref name="from"/>) to the other (0.55 u/s); the winner jumps with the arms up.
        /// With <paramref name="hop"/> they hop along it instead (0.03 u at 2.5 Hz). <see cref="CampScene.a"/>: the finish.</summary>
        public static CampScene PathRace(IList<int> kids, IList<Vector2> at, Vector2 from, Vector2 to, bool hop, float t0)
        {
            var d = Unit(to - from, Vector2.right);
            var across = new Vector2(-d.y, d.x);
            var s = new CampScene(CampUse.PathRace, t0, Vector2.Lerp(from, to, 0.5f)) { followReach = 0.9f };
            int n = kids.Count;
            float t = 0f;
            for (int i = 0; i < n; i++) { s.Add(kids[i]); t = Mathf.Max(t, WalkTime(at[i], from + across * ((i - (n - 1) * 0.5f) * 0.06f), KidWalk)); }
            for (int i = 0; i < n; i++) { var st = from + across * ((i - (n - 1) * 0.5f) * 0.06f); s.Part(i, 0f, t, st, to, DollStyle.Stand, null, PropKind.None, KidWalk); }
            for (int i = 0; i < n; i++) { var st = from + across * ((i - (n - 1) * 0.5f) * 0.06f); s.Part(i, t, t + 0.6f, st, to, DollStyle.Stand, CrouchPose); }
            t += 0.6f;
            float len = (to - from).magnitude;
            float winEnd = float.MaxValue; int winner = 0;
            var ends = new float[n];
            for (int i = 0; i < n; i++)
            {
                var en = to + across * ((i - (n - 1) * 0.5f) * 0.06f);
                float pace = (hop ? 0.3f : RacePace) * (1f - 0.07f * i);
                float leg = len / pace;
                var part = s.Part(i, t, t + leg, en, en + d, DollStyle.Stand, hop ? (ref DollPose p, float u, float l) => { p.offset.y += 0.03f * Mathf.Abs(Mathf.Sin(u * 2.5f * Mathf.PI)); } : (CampPoseFn)null, PropKind.None, pace);
                part.run = !hop; part.poseWalking = hop;
                ends[i] = t + leg;
                if (ends[i] < winEnd) { winEnd = ends[i]; winner = i; }
            }
            s.a = winEnd;
            float last = 0f; foreach (float e in ends) last = Mathf.Max(last, e);
            for (int i = 0; i < n; i++)
            {
                var en = to + across * ((i - (n - 1) * 0.5f) * 0.06f);
                if (i == winner) { s.Part(i, ends[i], ends[i] + 0.5f, en, en + d, DollStyle.Stand, ArmsUpPose); s.Part(i, ends[i] + 0.5f, last + 1.4f, en, from, DollStyle.Cheer); }
                else s.Part(i, ends[i], last + 1.4f, en, from, DollStyle.Laugh);
            }
            return s;
        }

        // ================================================================== B50.b meet and chat on a path
        /// <summary>B50.b: two walkers whose ways cross on a path stop and chat (B7.a) <paramref name="chatFor"/> s (4–6), then go on.</summary>
        public static CampScene PathChat(int a, int b, Vector2 aAt, Vector2 bAt, Vector2 meet, Vector2 along, float chatFor, float t0)
        {
            along = Unit(along, Vector2.right);
            var s = new CampScene(CampUse.PathChat, t0, meet) { holdFocus = true };
            int ma = s.Add(a), mb = s.Add(b);
            var pa = meet - along * 0.06f; var pb = meet + along * 0.06f;
            float t = Mathf.Max(WalkTime(aAt, pa, Walk), WalkTime(bAt, pb, Walk));
            s.Part(ma, 0f, t, pa, pb, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(mb, 0f, t, pb, pa, DollStyle.Stand, null, PropKind.None, Walk);
            chatFor = Mathf.Clamp(chatFor, 4f, 6f);
            s.Part(ma, t, t + chatFor, pa, pb, DollStyle.Talk);
            s.Part(mb, t, t + chatFor, pb, pa, DollStyle.Talk);
            t += chatFor;
            var ea = meet + along * 0.4f; var eb = meet - along * 0.4f;
            s.Part(ma, t, t + WalkTime(pa, ea, Walk), ea, ea + along, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(mb, t, t + WalkTime(pb, eb, Walk), eb, eb - along, DollStyle.Stand, null, PropKind.None, Walk);
            return s;
        }

        // ================================================================== B51 a small gift at the remembering stone
        /// <summary>B51: a family member walks with a gift (<paramref name="gift"/>: 0 a flower, 1 a small bowl of berries, 2 a pebble), kneels at the
        /// stone (B4's kneel), sets it at the foot (arm x 40, lean 35 → a <see cref="CampMark.Gift"/>), touches the stone (arm x 90, 1 s), bows and
        /// walks back. A second (−1: none) walks beside and bows too. <see cref="CampScene.a"/>: the gift set down.</summary>
        /// <summary>B51: the kneeler's middle this far out from the stone's footprint (a doll's 0.055 foot plus a little) [est].</summary>
        public const float GiftGap = 0.07f;

        public static CampScene StoneGift(int who, Vector2 at, int second, Vector2 secondAt, Vector2 stone, Vector2 toCam, int gift, float t0, float stoneR = 0.03f)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.StoneGift, t0, stone) { holdFocus = true, kind = gift };
            int m = s.Add(who);
            // #692 p5 notes (the user: "he is clipped in the stone"): outside the stone's own footprint (stoneR: its drawn half-width)
            var kneel = stone + toCam * (stoneR + GiftGap);
            float t = WalkTime(at, kneel, Walk);
            var tip = gift == 0 ? CampTipKind.Flower : gift == 1 ? CampTipKind.GiftBowl : CampTipKind.Pebble;
            s.Part(m, 0f, t, kneel, stone, DollStyle.Stand, null, PropKind.None, Walk);
            s.Tip(m, 0f, t + 0.6f + 0.5f, tip, gift == 2 ? 1.3f : 1f);
            s.Part(m, t, t + 0.6f, kneel, stone, DollStyle.Stand, KneelPose); t += 0.6f;
            s.Part(m, t, t + 0.7f, kneel, stone, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, 1f, l); float k = Bump(u, 0f, l); p.lean += 5f * k; p.armR = Lerp(p.armR, new Vector3(40f, 0f, 6f), k); });
            s.a = t + 0.5f;
            Leave(s, t + 0.5f, CampMark.Gift, stone + toCam * (stoneR + 0.012f), 0f, gift);
            t += 0.7f;
            s.Part(m, t, t + 1.2f, kneel, stone, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, 1f, l); float k = In(u, 0f, 0.2f) * (1f - In(u, 1f, 0.2f)); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), k); });
            t += 1.2f;
            s.Part(m, t, t + 1.2f, kneel + toCam * 0.02f, stone, DollStyle.Bow, null, PropKind.None, 0.2f);
            if (second != Nobody)
            {
                int b = s.Add(second);
                var bs = kneel + new Vector2(-toCam.y, toCam.x) * 0.08f + toCam * 0.03f;
                s.Part(b, 0f, WalkTime(secondAt, bs, Walk), bs, stone, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(b, t, t + 1.2f, bs, stone, DollStyle.Bow);
            }
            t += 1.2f;
            Go(s, m, t, WalkTime(kneel, at, Walk) * 0.5f, Vector2.Lerp(kneel, at, 0.5f), at, Walk);
            return s;
        }

        // ================================================================== B52.a light each lantern at dusk (work)
        public const float LightGap = 1.5f;

        /// <summary>B52.a: the lamplighter walks tent to tent with a burning stick (a twig with an ember tip) and reaches up to each tent's lantern
        /// (arm x 150) → hit: it lights (a <see cref="CampMark.LanternLit"/> at the pole) with one spark; 1.5 s apart plus the walk.
        /// <see cref="CampScene.a"/>: the first light.</summary>
        public static CampScene LightLanterns(int lighter, Vector2 at, IList<Vector2> poles, Vector2 toCam, float t0, IList<Vector2> stands = null)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.LightLanterns, t0, poles.Count > 0 ? poles[0] : at) { followReach = 1.4f };
            int m = s.Add(lighter);
            float t = 0f; var from = at;
            s.a = -1f;
            for (int i = 0; i < poles.Count; i++)
            {
                // #692 p5 retakes (the lighter hid the lantern): beside the pole across the camera's view, on the side they come from
                var side = new Vector2(-toCam.y, toCam.x); if (Vector2.Dot(side, from - poles[i]) < 0f) side = -side;
                var st = stands != null && i < stands.Count ? stands[i] : poles[i] + side * 0.065f + toCam * 0.02f; // 2026-11-24: at a house, in front of its wall
                float w = WalkTime(from, st, Walk);
                s.Part(m, t, t + w, st, poles[i], DollStyle.Stand, null, PropKind.Twig, Walk); t += w;
                s.Part(m, t, t + LightGap, st, poles[i], DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.3f) * (1f - In(u, 1.0f, 0.3f)); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 6f), k); p.offset.y += 0.01f * k; }, PropKind.Twig);
                float hit = t + 0.45f;
                if (s.a < 0f) s.a = hit;
                Leave(s, hit, CampMark.LanternLit, poles[i], CampGeo5.PoleH);
                s.Fx(hit, CampFxKind.Sparks, G(poles[i], CampGeo5.PoleH), 0.3f, 1);
                t += LightGap;
                from = st;
            }
            s.Tip(m, 0f, t, CampTipKind.Ember);
            return s;
        }

        // ================================================================== B52.b kids chase moths
        /// <summary>B52.b: pale specks circle a lit lantern (r 0.04 u at 2 Hz: CampLife draws them while this plays); the kid jumps at them (hop 0.05 u,
        /// arms up x 170) and claps (both arms meet → hit: a <see cref="CampMark.MothClap"/>: the specks scatter and come back), <paramref name="jumps"/>
        /// times. <see cref="CampScene.a"/>: the first clap.</summary>
        public static CampScene ChaseMoths(int kid, Vector2 at, Vector2 pole, Vector2 toCam, int jumps, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.ChaseMoths, t0, pole) { holdFocus = true };
            int m = s.Add(kid);
            // #692 p5 retakes (the kid hid the lantern): beside the pole across the camera's view, a little in front
            var side = new Vector2(-toCam.y, toCam.x); if (Vector2.Dot(side, at - pole) < 0f) side = -side;
            var st = pole + side * 0.06f + toCam * 0.02f;
            float t = WalkTime(at, st, KidWalk);
            s.Part(m, 0f, t, st, pole, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(m, t, t + 1f, st, pole, DollStyle.LookUp); t += 1f;
            jumps = Mathf.Clamp(jumps, 2, 4);
            s.a = t + 0.22f;
            for (int i = 0; i < jumps; i++)
            {
                var j = st + new Vector2(-toCam.y, toCam.x) * (i % 2 == 0 ? 0.015f : -0.015f);
                s.Part(m, t, t + 0.5f, j, pole, DollStyle.Stand, ClapJumpPose, PropKind.None, 0.2f);
                Leave(s, t + 0.22f, CampMark.MothClap, pole, CampGeo5.PoleH);
                t += 0.5f;
                s.Part(m, t, t + 0.8f, j, pole, i == jumps - 1 ? DollStyle.Laugh : DollStyle.LookUp); t += 0.8f;
            }
            return s;
        }

        // ================================================================== B53.a pull a weed in the camp (work)
        /// <summary>B53.a: bend, grab (arm x 40), tug twice (lean 30 → 20 → 35, 0.2 s each), pull (lean → −10, Back 0.4 s → hit: 3 dust, the
        /// seedling comes up with a dangling root: a <see cref="CampMark.WeedPulled"/>, the cell clears in the sim), toss it to the edge (an arm
        /// throw, it lands with a dust puff). <see cref="CampScene.a"/>: the pull.</summary>
        public static CampScene PullWeed(int who, Vector2 at, Vector2 weed, Vector2 toss, float t0)
        {
            var s = new CampScene(CampUse.PullWeed, t0, weed) { holdFocus = true };
            int m = s.Add(who);
            var st = weed + Unit(at - weed, Vector2.down) * 0.06f;
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, weed, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.4f, st, weed, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.3f); p.lean += 30f * k; p.armR = Lerp(p.armR, new Vector3(40f, 0f, 6f), k); p.armL = Lerp(p.armL, new Vector3(40f, 0f, -6f), k); }); t += 0.4f;
            float[] leans = { 30f, 20f, 35f, 20f, 35f };
            for (int i = 0; i < 4; i++)
            {
                float a = leans[i], b = leans[i + 1];
                s.Part(m, t, t + 0.2f, st, weed, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean += Mathf.Lerp(a, b, Smooth(u / l)); p.armR = new Vector3(40f, 0f, 6f); p.armL = new Vector3(40f, 0f, -6f); });
                t += 0.2f;
            }
            s.Part(m, t, t + 0.4f, st, weed, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Easing.Apply(Ease.Back, Mathf.Clamp01(u / l)); p.lean += Mathf.Lerp(35f, -10f, k); p.armR = new Vector3(Mathf.Lerp(40f, 80f, k), 0f, 6f); p.armL = new Vector3(Mathf.Lerp(40f, 80f, k), 0f, -6f); });
            t += 0.4f;
            s.a = t;
            Leave(s, t, CampMark.WeedPulled, weed);
            s.Fx(t, CampFxKind.Dust, G(weed, 0.01f), 0.5f, 3);
            s.Tip(m, t, t + 1.4f, CampTipKind.Weed);
            s.Part(m, t, t + 1f, st, toss, DollStyle.Stand, HoldUp); t += 1f;
            s.Part(m, t, t + 0.4f, st, toss, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Smooth(u / l); p.armR = Lerp(new Vector3(140f, 0f, 10f), new Vector3(60f, 0f, 10f), k); });
            s.Fx(t + 0.6f, CampFxKind.Dust, G(toss, 0.01f), 0.3f, 2);
            t += 0.4f;
            s.Part(m, t, t + 0.5f, st, toss, DollStyle.Stand, NodPose);
            return s;
        }

        static void HoldUp(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.25f); p.armR = Lerp(p.armR, new Vector3(140f, 0f, 10f), u); }

        // ================================================================== B53.c a crown
        /// <summary>B53.c: a kid twists weeds and flowers into a ring (both hands x 90, fiddle 1.5 s) and puts it on another kid's head (arm x 150) → a
        /// <see cref="CampMark.Crown"/> worn until night; the crowned kid walks proudly (lean −8, the chin up). <see cref="CampScene.a"/>: the crowning.</summary>
        public static CampScene Crown(int maker, Vector2 mAt, int wearer, Vector2 wAt, Vector2 spot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var across = new Vector2(-toCam.y, toCam.x);
            var s = new CampScene(CampUse.Crown, t0, spot) { followReach = 0.5f };
            int a = s.Add(maker), b = s.Add(wearer);
            var pa = spot + across * 0.05f; var pb = spot - across * 0.05f;
            float t = Mathf.Max(WalkTime(mAt, pa, KidWalk), WalkTime(wAt, pb, KidWalk));
            s.Part(a, 0f, t, pa, pb, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(b, 0f, t, pb, pa, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(a, t, t + 0.6f, pa, pa + toCam, DollStyle.Stand, CampScenes3.BendPickPose);
            s.Fx(t + 0.4f, CampFxKind.Grass, G(pa + toCam * 0.04f, 0.02f), 0.4f, 2);
            s.Part(b, t, t + 0.6f + 1.5f, pb, pa, DollStyle.Stand);
            t += 0.6f;
            s.Part(a, t, t + 1.5f, pa, pa + toCam, DollStyle.Stand, FiddleHandsPose);
            s.Tip(a, t, t + 1.5f + 0.5f, CampTipKind.CrownRing);
            t += 1.5f;
            s.Part(a, t, t + 0.6f, pa, pb, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 6f), k); p.offset.y += 0.01f * k; });
            s.Part(b, t, t + 0.6f, pb, pa, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean += 10f * Bump(u, 0f, l); });
            s.a = t + 0.4f;
            Worn(s, t + 0.4f, CampMark.Crown, b);
            t += 0.6f;
            var walk = pb + toCam * 0.25f - across * 0.1f;
            s.Part(b, t, t + WalkTime(pb, walk, KidWalk * 0.6f), walk, walk + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean -= 8f; p.offset.y += 0.004f * Mathf.Abs(Mathf.Sin(u * 5f)); }, PropKind.None, KidWalk * 0.6f).poseWalking = true;
            s.Part(a, t, t + 2.2f, pa, walk, DollStyle.Laugh);
            return s;
        }

        // ================================================================== B53.d the rope swing: hang it (work, once)
        /// <summary>B53.d hanging it (~8 s): the grown-up walks to the tree with the coiled rope and the plank; throws the rope over the bough (arm back
        /// x −30, up x 160, Snap 0.15 s → it drops on the far side: a dust puff; <paramref name="miss"/>: a second throw), ties the ends to the plank
        /// crouched (both arms x 60, a fiddle ±10 at 4 Hz, 1.5 s: a <see cref="CampMark.SwingRope"/> at the throw, <see cref="CampMark.SwingHung"/> at
        /// the tie), hangs on it once to test it (lean −15, squash 0.9 → hit: the crown sways) and steps back with a nod.
        /// <see cref="CampScene.a"/>: the test; <see cref="CampScene.b"/>: hung.</summary>
        public static CampScene HangSwing(int grown, Vector2 at, Vector2 pivot, Vector2 trunk, Vector2 fwd, bool miss, float t0)
        {
            fwd = Unit(fwd, Vector2.right);
            var s = new CampScene(CampUse.HangSwing, t0, pivot) { holdFocus = true };
            int m = s.Add(grown);
            var stand = pivot + fwd * 0.1f;
            float t = WalkTime(at, stand, Walk);
            s.Part(m, 0f, t, stand, pivot, DollStyle.Stand, null, PropKind.None, Walk);
            s.Tip(m, 0f, t + 0.2f, CampTipKind.Rope);
            int throws = miss ? 2 : 1;
            for (int i = 0; i < throws; i++)
            {
                s.Part(m, t, t + 0.4f, stand, pivot, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.35f); p.armR = Lerp(p.armR, new Vector3(-30f, 0f, 10f), k); p.lean -= 6f * k; });
                t += 0.4f;
                s.Part(m, t, t + 0.15f, stand, pivot, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Smooth(u / 0.15f); p.armR = Lerp(new Vector3(-30f, 0f, 10f), new Vector3(160f, 0f, 6f), k); });
                t += 0.15f;
                bool hits = i == throws - 1;
                var fall = hits ? pivot - fwd * 0.04f : pivot + fwd * 0.05f;
                s.Fx(t + 0.45f, CampFxKind.Dust, G(fall, 0.01f), 0.3f, 1);
                if (hits) Leave(s, t + 0.45f, CampMark.SwingRope, pivot);
                s.Part(m, t, t + 0.8f, stand, pivot, DollStyle.LookUp); t += 0.8f;
                if (!hits) { s.Part(m, t, t + 0.6f, stand, pivot, DollStyle.Stand, CampScenes3.BendPickPose); t += 0.6f; }
            }
            // tie the ends to the plank under the bough, crouched
            var tie = pivot + fwd * 0.04f;
            s.Part(m, t, t + 0.4f, tie, pivot, DollStyle.Stand, null, PropKind.None, 0.2f); t += 0.4f;
            s.Part(m, t, t + 1.5f, tie, pivot, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                CrouchPose(ref p, u, l); float f = Mathf.Sin(u * 4f * Mathf.PI * 2f) * 10f;
                p.armL = new Vector3(60f + f, 0f, 10f); p.armR = new Vector3(60f - f, 0f, -10f);
            });
            s.Tip(m, t - 0.4f, t + 1.5f, CampTipKind.Plank);
            t += 1.5f;
            s.b = t;
            Leave(s, t, CampMark.SwingHung, pivot);
            // the test: both hands up on the ropes, leaning back, squashed
            s.Part(m, t, t + 0.9f, tie, pivot, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float k = In(u, 0f, 0.3f) * (1f - In(u, 0.7f, 0.2f));
                p.armL = Lerp(p.armL, new Vector3(165f, 0f, -10f), k); p.armR = Lerp(p.armR, new Vector3(165f, 0f, 10f), k); p.lean -= 15f * k; p.squash = Mathf.Lerp(1f, 0.9f, k);
            });
            s.a = t + 0.5f;
            s.Fx(t + 0.5f, CampFxKind.TreeShake, G(trunk), 3f);
            s.Fx(t + 0.5f, CampFxKind.Creak, G(pivot), 1f);
            t += 0.9f;
            s.Part(m, t, t + 0.5f, stand, pivot, DollStyle.Stand, null, PropKind.None, 0.2f); t += 0.5f;
            s.Part(m, t, t + 0.5f, stand, pivot, DollStyle.Stand, NodPose);
            return s;
        }

        // ================================================================== B53.d kids on the swing
        /// <summary>One ride on the swing, for the drawn swing to follow: from (scene s) for <see cref="len"/> s.</summary>
        public struct Ride { public float from, len; }

        /// <summary>B53.d swinging (a kid, 6–10 s): walks to the plank, sits on it (+0.05 u, 0.4 s), both hands on the ropes (arms x 160, z ∓12), pushes
        /// off: leaning back −20, carried round the arc (±25 deg at 0.5 Hz [est], <see cref="CampGeo5.SwingTheta"/>), a legs-out kick at each front end
        /// (squash 1.05, lean −25, 0.15 s); a second kid (−1: none) pushes from behind at each back end (arms x 90 → Snap forward, a step 0.03 u), then
        /// they swap. A lone kid pumps by leaning (±15 deg with the arc). At the top of the last swing the rider jumps off forward (hop 0.06 u, arms up
        /// x 150, a dust landing) and laughs. <paramref name="sc"/>: the riders' doll scale (offsets are in adult units). <paramref name="rides"/>: the rides,
        /// for the drawn plank. <see cref="CampScene.a"/>: the first push-off.</summary>
        public static CampScene Swing(int rider, Vector2 rAt, int pusher, Vector2 pAt, Vector2 pivot, Vector2 fwd, float rideFor, float sc, List<Ride> rides, float t0)
        {
            fwd = Unit(fwd, Vector2.right);
            sc = Mathf.Max(0.3f, sc);
            var s = new CampScene(CampUse.Swing, t0, pivot) { holdFocus = true };
            int r = s.Add(rider), p2 = pusher != Nobody ? s.Add(pusher) : -1;
            float ride = CampGeo5.RideLength(Mathf.Clamp(rideFor, 6f, 10f) * (pusher != Nobody ? 0.5f : 1f) + (pusher != Nobody ? 1.5f : 0f));
            var behind = pivot - fwd * (CampGeo5.ArcReach + 0.07f);
            float t = WalkTime(rAt, pivot, KidWalk);
            if (p2 >= 0) t = Mathf.Max(t, WalkTime(pAt, behind, KidWalk));
            s.Part(r, 0f, t, pivot, pivot + fwd, DollStyle.Stand, null, PropKind.None, KidWalk);
            if (p2 >= 0) s.Part(p2, 0f, t, behind, pivot, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.a = -1f;
            int turns = p2 >= 0 ? 2 : 1;
            for (int k = 0; k < turns; k++)
            {
                int on = k == 0 ? r : p2, push = k == 0 ? p2 : r;
                // sit on the plank
                s.Part(on, t, t + 0.4f, pivot, pivot + fwd, DollStyle.Sit, (ref DollPose p, float u, float l) => { p.offset.y += (CampGeo5.SeatTop / sc + CampGeo5.SitDrop) * In(u, 0f, 0.4f); }, PropKind.None, 0.3f);
                t += 0.4f;
                float from = t;
                if (s.a < 0f) s.a = from;
                rides?.Add(new Ride { from = from, len = ride });
                bool alone = push < 0;
                s.Part(on, t, t + ride, pivot, pivot + fwd, DollStyle.Sit, (ref DollPose p, float u, float l) =>
                {
                    float th = CampGeo5.SwingTheta(u, ride);
                    var o = CampGeo5.SeatOffset(th);
                    p.offset.y += (CampGeo5.SeatTop + o.y) / sc + CampGeo5.SitDrop; p.offset.z -= o.x / sc; // forward is −z; #692 p5 retakes: the sit sank the rider through the plank (SitDrop)
                    p.armL = new Vector3(160f, 0f, -12f); p.armR = new Vector3(160f, 0f, 12f);
                    p.lean += -20f + (alone ? -15f * Mathf.Cos(Mathf.PI * 2f * CampGeo5.ArcHz * u) : 0f);
                    // the kick at each front end (sin = 1 at u = 0.5, 2.5, ...)
                    float kick = Bump(Mathf.Repeat(u - 0.425f, 2f), 0f, 0.15f);
                    p.squash = Mathf.Lerp(p.squash, 1.05f, kick); p.lean -= 5f * kick;
                });
                for (float e = 0.5f; e < ride; e += 1f) s.Fx(from + e, CampFxKind.Creak, G(pivot), 0.6f);
                if (push >= 0)
                {
                    // the pusher behind: at each back end (u = 1.5, 3.5, ...) both arms forward, a step in
                    s.Part(push, k == 0 ? t - 0.4f : t - 0.4f, from, behind, pivot, DollStyle.Stand, null, PropKind.None, KidWalk);
                    s.Part(push, from, from + ride, behind, pivot, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                    {
                        float near = Bump(Mathf.Repeat(u - 1.25f, 2f), 0f, 0.5f);
                        p.armL = Lerp(new Vector3(60f, 0f, -8f), new Vector3(90f, 0f, -6f), near); p.armR = Lerp(new Vector3(60f, 0f, 8f), new Vector3(90f, 0f, 6f), near);
                        p.offset.z -= 0.03f * near; p.lean += 10f * near;
                    });
                }
                t += ride;
                // the jump off forward at the top of the front swing
                var land = pivot + fwd * (CampGeo5.ArcReach + 0.07f);
                s.Part(on, t, t + 0.5f, land, land + fwd, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float k2 = In(u, 0f, 0.4f); p.offset.y += Mathf.Lerp(CampGeo5.SeatH / sc, 0f, k2) + 0.06f * Mathf.Sin(k2 * Mathf.PI);
                    p.armL = new Vector3(150f, 0f, -20f); p.armR = new Vector3(150f, 0f, 20f); p.squash = u > 0.4f ? 0.85f : 1f;
                }, PropKind.None, (land - pivot).magnitude / 0.4f);
                s.Fx(t + 0.42f, CampFxKind.Dust, G(land, 0.01f), 0.4f, 2);
                t += 0.5f;
                if (k == 0 && turns > 1)
                {
                    // the swap: the rider laughs and goes round behind to push; the pusher walks to the plank as it slows
                    s.Part(on, t, t + 0.6f, land, pivot, DollStyle.Laugh);
                    s.Part(on, t + 0.6f, t + 0.6f + WalkTime(land, behind, KidWalk), behind, pivot, DollStyle.Stand, null, PropKind.None, KidWalk);
                    s.Part(p2, from + ride, t + 0.6f, behind, pivot, DollStyle.Laugh);
                    s.Part(p2, t + 0.6f, t + 1.4f, pivot, pivot + fwd, DollStyle.Stand, null, PropKind.None, KidWalk);
                    t += Mathf.Max(1.4f, 0.6f + WalkTime(land, behind, KidWalk));
                    if (s.parts[p2][s.parts[p2].Count - 1].to < t) s.Part(p2, s.parts[p2][s.parts[p2].Count - 1].to, t, pivot, pivot + fwd, DollStyle.Stand);
                    float rl = s.parts[r][s.parts[r].Count - 1].to;
                    if (rl < t) s.Part(r, rl, t, behind, pivot, DollStyle.Stand);
                }
                else
                {
                    s.Part(on, t, t + 1.2f, land, pivot, DollStyle.Laugh);
                    if (push >= 0) s.Part(push, from + ride, t + 1.2f, behind, pivot, DollStyle.Cheer);
                    t += 1.2f;
                }
            }
            return s;
        }
    }
}
