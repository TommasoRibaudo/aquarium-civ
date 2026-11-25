// #722 (docs/design/new-events.md §1.1-1.3, docs/design/animation-specs.md B78.a-c): the first three story events as camp scenes, pure
// builders with the beat kit (CampScene parts, effects, tips), as RitualScenes. B78.a the tree's first fruit: a kid picks it from the ground
// (on the toes, never climbing) and runs it to the fire held high (B75.b's run), a bite each, the last to the youngest. B78.b two marks on
// the tree: the couple, the name cut's strokes, a pale notch each. B78.c the second stone: two grown-ups roll it in and heave it up beside
// the first; the name is #693's NameCut as it is; the family round with heads down (Mourn), the kids 0.3 u clear of the roll. And the life
// use the fruit leaves: a kid picks one in summer. Every number is [est] until the clip.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    public static partial class StoryScenes
    {
        const int Nobody = VillageScenes.Nobody;
        static Vector2 Across(Vector2 toCam) => new Vector2(-toCam.y, toCam.x);

        // ================================================================== B78.a the tree's first fruit
        /// <summary>The user's review (2026-11-25): a fruit hanging at a kid's reach looked wrong, so it hangs up in the crown and falls. It
        /// falls for <see cref="FallFor"/> s (gravity), bounces once (<see cref="BounceH"/> u, <see cref="BounceFor"/> s) and lies at the kid's
        /// feet until picked up.</summary>
        public const float FallFor = 0.45f, BounceH = 0.012f, BounceFor = 0.22f;
        /// <summary>The bend and pick up.</summary>
        public const float PickFor = 1.1f;
        /// <summary>The fruit (B78.a "Fruit": a red ball 0.018 u, a leaf).</summary>
        public const float FruitR = 0.018f;
        /// <summary>The fruit in the hands: the berry tip drawn this much bigger (a kid's berry is 0.015 u: the fruit is 0.018).</summary>
        public const float HeldScale = 1.2f;
        /// <summary>B78.a: each bite (Nod, 0.6 s).</summary>
        public const float BiteFor = 0.6f;
        /// <summary>On the toes: never more than this off the ground (the kid never climbs).</summary>
        public const float Toes = 0.02f;
        /// <summary>"Stays": the fruits on the tree each summer after the first.</summary>
        public const int TreeFruits = 5;

        /// <summary>The side of the tree the fruit falls on: off to the side of the trunk as the camera sees it (the review of the clip: straight in
        /// front it hung against the bark and the trunk hid its fall), a little toward the camera, away from <paramref name="awayFrom"/> (the
        /// fire's side, where the camp's things stand; zero: either).</summary>
        public static Vector2 FruitDir(Vector2 toCam, Vector2 awayFrom)
        {
            toCam = Unit(toCam, Vector2.down);
            var acr = Across(toCam);
            if (Vector2.Dot(acr, awayFrom) > 0f) acr = -acr;
            return (acr * 0.9f + toCam * 0.45f).normalized;
        }

        /// <summary>Where a kid stands under the tree to pick: under the fruit that falls, on <paramref name="dir"/>'s side (the camera's).</summary>
        public static Vector2 PickSpot(Vector2 tree, int stage, Vector2 dir) => tree + Unit(dir, Vector2.down) * (HangOut(stage) + 0.03f);

        /// <summary>How far out from the trunk the fruit that falls hangs: under the crown's edge, clear of the trunk (the review of the clip: at
        /// the ring it looked stuck to the bark) [est].</summary>
        public static float HangOut(int stage) => Mathf.Max(TreeGeo.TrunkR(stage) + 0.1f, 0.17f * TreeGeo.CrownY(stage) / TreeGeo.CrownY(4));

        /// <summary>The fruit that falls (box x, height over the ground, box z): up in the crown at the fruits' height, over the pick spot.</summary>
        public static Vector3 LowFruit(Vector2 tree, int stage, Vector2 dir)
        {
            var p = tree + Unit(dir, Vector2.down) * HangOut(stage);
            return new Vector3(p.x, FallFromY(stage), p.y);
        }

        /// <summary>How high the fruits hang on a crown of <paramref name="stage"/> (the first of <see cref="TreeFruit"/>'s ring).</summary>
        public static float HangY(int stage) => TreeFruit(Vector2.zero, stage, 0).y;

        /// <summary>The fruit that falls hangs a little lower than the crown's ring: dangling just under the leaves.</summary>
        public static float FallFromY(int stage) => HangY(stage) - 0.04f;

        /// <summary>Where it lands: on the ground just in front of the kid's feet, toward the trunk.</summary>
        public static Vector2 FruitLands(Vector2 under, Vector2 tree) => under + Dir(under, tree, Vector2.up) * 0.045f;

        /// <summary>The falling fruit <paramref name="u"/> scene seconds in (box x, height over the ground, box z), or false once picked up
        /// (<see cref="CampScene.a"/>). It hangs at <see cref="CampScene.p0"/> until <see cref="CampScene.b"/>, falls to <see cref="CampScene.p1"/>,
        /// bounces, lies there. <see cref="CampScene.kind"/>: the tree's stage.</summary>
        public static bool FruitNow(CampScene s, float u, out Vector3 at)
        {
            at = default;
            if (u >= s.a) return false;
            float hang = FallFromY(s.kind);
            if (u < s.b) { at = new Vector3(s.p0.x, hang, s.p0.y); return true; }
            float f = (u - s.b) / FallFor;
            if (f < 1f) { var h = Vector2.Lerp(s.p0, s.p1, f); at = new Vector3(h.x, Mathf.Lerp(hang, FruitR, f * f), h.y); return true; }
            float b = (u - s.b - FallFor) / BounceFor;
            at = new Vector3(s.p1.x, FruitR + (b < 1f ? BounceH * Mathf.Sin(b * Mathf.PI) : 0f), s.p1.y);
            return true;
        }

        /// <summary>Looks up (<paramref name="look"/> s) as the fruit lets go with a leaf, looks down as it lands at the feet, bends and picks it up
        /// (the hit <see cref="CampScene.a"/>, in the hand), stands. Sets <see cref="CampScene.b"/> (it lets go). Returns the time after.</summary>
        static float DropAndPick(CampScene s, int k, Vector2 under, Vector2 tree, Vector3 low, float t, float look)
        {
            var land = FruitLands(under, tree);
            s.Part(k, t, t + look, under, tree, DollStyle.LookUp);
            s.b = t + look * 0.55f;
            s.Fx(s.b, CampFxKind.Leaves, G(new Vector2(low.x, low.z), low.y), 0.35f, 1); // a leaf comes down with it
            t += look;
            float landed = s.b + FallFor + BounceFor;
            if (landed > t) { s.Part(k, t, landed, under, land, DollStyle.Stand); t = landed; } // watches it land
            s.Part(k, t, t + PickFor, under, land, DollStyle.Stand, PickUp);
            s.a = t + PickFor * 0.5f;
            return t + PickFor;
        }

        /// <summary>Bends down to the fruit at the feet (squash 0.8, lean 35, the arms down to it), then up with it held in front (x 90).</summary>
        static void PickUp(ref DollPose p, float t, float len)
        {
            float down = In(t, 0f, 0.3f) * (1f - In(t, len * 0.5f, 0.3f));
            p.squash = Mathf.Lerp(1f, 0.8f, down); p.offset.y -= 0.012f * down; p.lean += 35f * down;
            float hold = In(t, len * 0.5f, 0.3f);
            p.armL = Lerp(Lerp(p.armL, new Vector3(55f, 0f, -8f), down), new Vector3(90f, 0f, -8f), hold);
            p.armR = Lerp(Lerp(p.armR, new Vector3(60f, 0f, 8f), down), new Vector3(90f, 0f, 8f), hold);
        }

        /// <summary>Fruit <paramref name="i"/> of <see cref="TreeFruits"/> on the Keeper's crown (box x, height, z): round the ring the charms hang
        /// from (ConsequenceMeshes.CharmTie), a little out, at uneven heights.</summary>
        public static Vector3 TreeFruit(Vector2 tree, int stage, int i)
        {
            ConsequenceMeshes.CharmTie(true, 0.16f, out float y, out float r);
            float k = TreeGeo.CrownY(stage) / TreeGeo.CrownY(4);
            float a = (i + 0.37f) / TreeFruits * Mathf.PI * 2f, rr = r * (1.1f + 0.25f * (i % 2));
            return new Vector3(tree.x + Mathf.Cos(a) * rr, (y - 0.015f * (i % 3)) * k, tree.y + Mathf.Sin(a) * rr);
        }

        /// <summary>B75.b's held-high run: both arms x 150.</summary>
        static void HeldHigh(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.25f);
            p.armL = Lerp(p.armL, new Vector3(150f, 0f, -12f), u); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 12f), u);
        }

        /// <summary>Holding the fruit out in both hands (x 90).</summary>
        static void HoldOut(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f);
            p.armL = Lerp(p.armL, new Vector3(90f, 0f, -8f), u); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 8f), u);
        }

        /// <summary>A bite: bends in to the fruit (lean 25) and nods.</summary>
        static CampPoseFn Bite(bool seated) => (ref DollPose p, float t, float len) =>
        {
            if (seated) Seated(ref p);
            p.lean += 25f * Bump(t, 0f, len);
            NodPose(ref p, Mathf.Max(0f, t - len * 0.5f), len * 0.5f);
        };

        /// <summary>
        /// B78.a: the kid stops under the tree and looks up (LookUp, 1 s); goes up on the toes (+0.02 u, arms x 150) and picks the one red fruit
        /// (the hit <see cref="CampScene.a"/>: it leaves the branch); runs to the fire holding it up (both arms x 150, KidRun); there it goes round:
        /// each grown-up at the fire bends and takes a bite (0.6 s each); the last bite goes to the youngest (<paramref name="youngest"/>; Nobody:
        /// the picker is the youngest and eats the last bite). <see cref="CampScene.c"/>: at the fire; <see cref="CampScene.d"/>: the fruit is
        /// eaten. <see cref="CampScene.p0"/>, <see cref="CampScene.p1"/>.x: the low fruit's spot and height (drawn until the hit).
        /// </summary>
        public static CampScene FirstFruit(int kid, Vector2 kidAt, IList<int> grown, IList<Vector2> grownAt, int youngest, Vector2 youngestAt,
            Vector2 tree, int stage, Vector2 dir, Vector2 fire, Vector2 toCam, float t0)
        {
            dir = Unit(dir, Vector2.down); toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var low = LowFruit(tree, stage, dir);
            var under = PickSpot(tree, stage, dir);
            var s = new CampScene(CampUse.FirstFruit, t0, (tree + fire) * 0.5f) { followReach = 2.5f, p0 = new Vector2(low.x, low.z), p1 = FruitLands(under, tree), kind = stage };
            int k = s.Add(kid);
            float t = WalkTime(kidAt, under, KidWalk);
            s.Part(k, 0f, t, under, tree, DollStyle.Stand, null, PropKind.None, KidWalk);
            t = DropAndPick(s, k, under, tree, low, t, 1f); // spots the one red fruit; it falls to their feet
            var atFire = fire + toCam * 0.3f;
            float run = WalkTime(under, atFire, KidRun);
            var r = s.Part(k, t, t + run, atFire, fire, DollStyle.Stand, HeldHigh, PropKind.None, KidRun); r.run = true; r.poseWalking = true;
            t += run;
            s.c = t;
            s.Part(k, t, t + 0.8f, atFire, atFire + toCam, DollStyle.Stand, HeldHigh); t += 0.8f; // look what I found
            // the grown-ups at the fire, sitting round it (never where the kid stands, on the camera's side)
            int n = Mathf.Min(3, grown.Count);
            var angles = new[] { -60f, 60f, 125f };
            var gm = new int[n]; var spots = new Vector2[n]; var gEnd = new float[n];
            for (int i = 0; i < n; i++)
            {
                gm[i] = s.Add(grown[i]);
                spots[i] = fire + Rot(toCam, angles[i]) * 0.3f;
                gEnd[i] = Mathf.Max(WalkTime(grownAt[i], spots[i], Walk), s.c);
                s.Part(gm[i], 0f, gEnd[i], spots[i], fire, DollStyle.Sit, null, PropKind.None, Walk);
            }
            // the round: a bite each
            var at = atFire;
            for (int i = 0; i < n; i++)
            {
                var offer = spots[i] + Dir(spots[i], atFire, toCam) * 0.1f;
                float w = WalkTime(at, offer, KidWalk);
                s.Part(k, t, t + w, offer, spots[i], DollStyle.Stand, HoldOut, PropKind.None, KidWalk).poseWalking = true;
                t += w;
                if (t > gEnd[i]) s.Part(gm[i], gEnd[i], t, spots[i], fire, DollStyle.Sit);
                s.Part(k, t, t + BiteFor, offer, spots[i], DollStyle.Stand, HoldOut);
                s.Part(gm[i], t, t + BiteFor, spots[i], offer, DollStyle.Sit, Bite(true));
                t += BiteFor; gEnd[i] = t;
                at = offer;
            }
            // the last bite to the youngest
            int y = -1;
            if (youngest != Nobody)
            {
                y = s.Add(youngest);
                var meet = at + acr * 0.09f;
                float wy = WalkTime(youngestAt, meet, KidWalk), start = Mathf.Max(s.c, t - wy);
                s.Part(y, start, start + wy, meet, at, DollStyle.Stand, null, PropKind.None, KidWalk);
                float there = start + wy;
                if (there < t) s.Part(y, there, t, meet, at, DollStyle.Stand);
                if (there > t) { s.Part(k, t, there, at, meet, DollStyle.Stand, HoldOut); t = there; }
                s.Part(k, t, t + BiteFor, at, meet, DollStyle.Stand, HoldOut);
                s.Part(y, t, t + BiteFor, meet, at, DollStyle.Stand, Bite(false));
                t += BiteFor;
                s.d = t;
                s.Part(y, t, t + 1f, meet, at, DollStyle.Laugh);
                Go(s, y, t + 1f, 1.2f, meet + toCam * 0.3f + acr * 0.1f, meet + toCam, KidWalk);
            }
            else
            {
                s.Part(k, t, t + 1.4f, at, at + toCam, DollStyle.Taste); // the picker is the youngest: the last bite is theirs
                t += 1.4f;
                s.d = t;
            }
            s.tips.Add(new CampTip { member = k, from = s.a, to = s.d, kind = CampTipKind.Berry, grow = 1f, scale = HeldScale });
            s.Part(k, t, t + 1f, at, at + toCam, DollStyle.Laugh);
            Go(s, k, t + 1f, 1.2f, at + toCam * 0.3f - acr * 0.1f, at + toCam, KidWalk);
            for (int i = 0; i < n; i++) s.Part(gm[i], gEnd[i], t + 1.5f, spots[i], fire, DollStyle.Sit);
            return s;
        }

        /// <summary>#722 (1.1 "Read later", a life use): in summer a kid walks under the Keeper's tree, looks up, goes up on the toes and picks a
        /// fruit (the hit <see cref="CampScene.a"/>), eats it slowly (Taste, the fruit getting smaller), and runs off. Never climbs.</summary>
        public static CampScene FruitPick(int kid, Vector2 kidAt, Vector2 tree, int stage, Vector2 dir, Vector2 toCam, float t0)
        {
            dir = Unit(dir, Vector2.down); toCam = Unit(toCam, Vector2.down);
            var low = LowFruit(tree, stage, dir);
            var under = PickSpot(tree, stage, dir);
            var s = new CampScene(CampUse.FruitPick, t0, under) { holdFocus = true, p0 = new Vector2(low.x, low.z), p1 = FruitLands(under, tree), kind = stage };
            int k = s.Add(kid);
            float t = WalkTime(kidAt, under, KidWalk);
            s.Part(k, 0f, t, under, tree, DollStyle.Stand, null, PropKind.None, KidWalk);
            t = DropAndPick(s, k, under, tree, low, t, 1f);
            const float eat = 2.8f; // two bites' worth of chewing
            s.Part(k, t, t + eat, under, under + toCam, DollStyle.Taste);
            s.tips.Add(new CampTip { member = k, from = s.a, to = t + eat, kind = CampTipKind.Berry, grow = 0.35f, scale = HeldScale });
            t += eat;
            Go(s, k, t, 1.2f, under + toCam * 0.35f + Across(toCam) * 0.15f, under + toCam, KidRun, true);
            return s;
        }

        // ================================================================== B78.b two marks on the tree
        /// <summary>The side of the trunk the marks are cut on (toward the front glass, where the Keeper looks from) [est].</summary>
        public static readonly Vector2 MarkFace = Vector2.down;
        /// <summary>The top pair's height (hand height, arm x 70); each later pair this much under it; the two of a pair this far either side.</summary>
        public const float MarkTop = 0.1f, MarkStep = 0.011f, MarkGap = 0.009f;
        /// <summary>The trunk's radius at hand height, as a share of its foot (TreeGeo.TrunkR has the root flare) [est].</summary>
        public const float TrunkAtHand = 0.75f;
        /// <summary>A notch (B78.b "TreeMark": a pale notch 0.012 x 0.004 u): its height and width.</summary>
        public const float NotchH = 0.012f, NotchW = 0.004f;
        /// <summary>B78.b: three small strokes, each this long, in a cut.</summary>
        public const int Strokes = 3;
        public const float StrokeEvery = 0.5f, CutFor = 0.2f + Strokes * StrokeEvery + 0.2f;

        /// <summary>Notch <paramref name="which"/> (0, 1) of pair <paramref name="pair"/> on the trunk (box x, height, z), on <see cref="MarkFace"/>.</summary>
        public static Vector3 MarkAt(Vector2 tree, int stage, int pair, int which)
        {
            var acr = Across(MarkFace);
            var p = tree + MarkFace * (TreeGeo.TrunkR(stage) * TrunkAtHand) + acr * (which == 0 ? -MarkGap : MarkGap);
            return new Vector3(p.x, MarkTop - Mathf.Max(0, pair) * MarkStep, p.y);
        }

        /// <summary>The cut: the right arm at x 70, three short strokes (down to x 55 and back, the hit at each stroke's middle), the left steadying.</summary>
        static void CutStrokes(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f) * (1f - In(t, len - 0.2f, 0.2f));
            float w = t - 0.2f; int n = Mathf.FloorToInt(w / StrokeEvery);
            float stroke = w >= 0f && n < Strokes ? Bump(w - n * StrokeEvery, 0f, StrokeEvery) : 0f;
            p.armR = Lerp(p.armR, new Vector3(70f - 15f * stroke, 0f, 6f), u); p.armL = Lerp(p.armL, new Vector3(50f, 0f, -14f), u); p.lean += 8f * u;
        }

        /// <summary>
        /// B78.b: the couple meet and walk to the tree side by side; one steps in and cuts a short mark at hand height (arm x 70, the cutting stone
        /// of the name cut, three small strokes, a chip puff each); the other cuts one beside it; they step back together and look at the two marks
        /// (Stand, 1.5 s); kids watch from a step back (Peek). <see cref="CampScene.a"/>, <see cref="CampScene.b"/>: a's notch and b's show;
        /// <see cref="CampScene.kind"/>: the pair; <see cref="CampScene.p0"/>.x: which notch of the pair is a's (0, 1). The knife stays with the
        /// grown-ups: the kids never come nearer than their step back.
        /// </summary>
        public static CampScene TwoMarks(int a, int b, Vector2 aAt, Vector2 bAt, IList<int> kids, IList<Vector2> kidsAt, Vector2 tree, int stage, int pair, float t0)
        {
            var face = MarkFace; var acr = Across(face);
            var visit = TreeGeo.Visit(tree, stage, face);
            var s = new CampScene(CampUse.TwoMarks, t0, visit) { holdFocus = true, kind = pair };
            int ma = s.Add(a), mb = s.Add(b);
            // they meet, then walk to the tree side by side
            var meet = aAt + Dir(aAt, bAt, acr) * 0.08f;
            float t = WalkTime(bAt, meet, Walk);
            s.Part(ma, 0f, t, aAt, meet, DollStyle.Stand);
            s.Part(mb, 0f, t, meet, aAt, DollStyle.Stand, null, PropKind.None, Walk);
            var va = visit + acr * 0.045f; var vb = visit - acr * 0.045f;
            if (Vector2.Dot(va - vb, meet - aAt) > 0f) { var x = va; va = vb; vb = x; } // a keeps its side
            float pace = Walk * 0.85f, wt = Mathf.Max(WalkTime(aAt, va, pace), WalkTime(meet, vb, pace));
            s.Part(ma, t, t + wt, va, tree, DollStyle.Stand, null, PropKind.None, pace);
            s.Part(mb, t, t + wt, vb, tree, DollStyle.Stand, null, PropKind.None, pace);
            float arrive = t + wt;
            t = arrive;
            var ta = TreeGeo.Touch(tree, stage, Rot(face, 18f)); var tb = TreeGeo.Touch(tree, stage, Rot(face, -18f));
            if (Vector2.Dot(ta - tb, va - vb) < 0f) { var x = ta; ta = tb; tb = x; }
            int aw = Vector2.Dot(ta - tb, acr) < 0f ? 0 : 1;
            s.p0 = new Vector2(aw, 0f);
            // a cuts
            s.Part(ma, t, t + 0.5f, ta, tree, DollStyle.Stand, null, PropKind.None, 0.15f);
            s.Part(mb, t, t + 0.5f + CutFor, vb, tree, DollStyle.Stand); // b watches
            t += 0.5f;
            Cut(s, ma, t, ta, tree, MarkAt(tree, stage, pair, aw));
            t += CutFor; s.a = t - 0.2f;
            // a steps back; b steps in and cuts beside it
            s.Part(ma, t, t + 0.5f, va, tree, DollStyle.Stand, null, PropKind.None, 0.15f);
            s.Part(mb, t, t + 0.5f, tb, tree, DollStyle.Stand, null, PropKind.None, 0.15f);
            t += 0.5f;
            s.Part(ma, t, t + CutFor + 0.5f, va, tree, DollStyle.Stand);
            Cut(s, mb, t, tb, tree, MarkAt(tree, stage, pair, 1 - aw));
            t += CutFor; s.b = t - 0.2f;
            s.Part(mb, t, t + 0.5f, vb, tree, DollStyle.Stand, null, PropKind.None, 0.15f);
            t += 0.5f;
            // together, a step back, looking at the two marks
            s.Part(ma, t, t + 1.5f, va, tree, DollStyle.Stand); s.Part(mb, t, t + 1.5f, vb, tree, DollStyle.Stand);
            t += 1.5f;
            var backA = va + face * 0.3f + acr * 0.05f; var backB = vb + face * 0.3f - acr * 0.05f;
            Go(s, ma, t, WalkTime(va, backA, pace), backA, backA + face, pace);
            Go(s, mb, t, WalkTime(vb, backB, pace), backB, backB + face, pace);
            // the kids watch from a step back
            int nk = Mathf.Min(2, kids.Count);
            for (int i = 0; i < nk; i++)
            {
                int m = s.Add(kids[i]);
                var spot = visit + face * 0.2f + acr * (i == 0 ? 0.13f : -0.13f);
                float w = WalkTime(kidsAt[i], spot, KidWalk);
                s.Part(m, 0f, w, spot, visit, DollStyle.Stand, null, PropKind.None, KidWalk);
                if (w < t) s.Part(m, w, t, spot, tree, DollStyle.Peek);
                Go(s, m, Mathf.Max(w, t), 1.2f, spot + face * 0.3f + acr * (i == 0 ? 0.15f : -0.15f), spot + face, KidWalk);
            }
            return s;
        }

        static void Cut(CampScene s, int m, float t, Vector2 at, Vector2 tree, Vector3 mark)
        {
            s.Part(m, t, t + CutFor, at, tree, DollStyle.Stand, CutStrokes);
            s.Tip(m, t, t + CutFor, CampTipKind.Stone, 1.8f); // the name cut's cutting stone in the hand, drawn 1.8x as there
            for (int i = 0; i < Strokes; i++) s.Fx(t + 0.2f + (i + 0.5f) * StrokeEvery, CampFxKind.Chips, mark, 0.3f, 2);
        }

        // ================================================================== B78.c the second stone
        /// <summary>B78.c: the roll's pace (box units a second; the stone turns a quarter each 0.8 s), the pushers' lean, how far behind the stone
        /// they push, how long the heave takes, and how far the kids keep from the roll.</summary>
        public const float QuarterEvery = 0.8f, PushLean = 25f, Behind = 0.125f, HeaveFor = 2.2f, KidsBack = 0.3f;
        /// <summary>How far from the stone's spot the roll starts (the camp's edge) [est].</summary>
        public const float RollFrom = 0.6f;
        /// <summary>The heave: the rollers bend for <see cref="HeaveBend"/> s, then the stone tips up on its foot over <see cref="HeaveLift"/> s.</summary>
        public const float HeaveBend = 0.45f, HeaveLift = 1.3f;
        /// <summary>The longest the rollers take to reach the stone: from further they hurry [est].</summary>
        public const float ApproachFor = 3.5f;

        /// <summary>The rolled stone's section (box units: the runestone's width and depth at the props' scale) and its length.</summary>
        public static float StoneW => 2f * ConsequenceMeshes.StoneHalfX * ConsequenceMeshes.Scale;
        public static float StoneT => 2f * ConsequenceMeshes.StoneHalfZ * ConsequenceMeshes.Scale;
        public static float StoneLen => ConsequenceMeshes.StoneH * ConsequenceMeshes.Scale;
        /// <summary>How far the stone goes with each quarter turn: it tips over its leading edge, flat to on edge and back.</summary>
        public static float RollStep => (StoneW + StoneT) * 0.5f;
        /// <summary>The roll's quarter turns: whole turns (4 quarters each), so it arrives as it lay, flat, the same face down.</summary>
        public static int RollQuarters(float dist) => 4 * Mathf.Max(1, Mathf.RoundToInt(dist / (4f * RollStep)));
        public static float RollFor(int quarters) => quarters * QuarterEvery;

        /// <summary>The stone's long axis (its foot to its top) and its across (local x) in the box for a stone stood at <paramref name="yaw"/>.</summary>
        public static Vector2 StoneUpAxis(float yaw) { float r = yaw * Mathf.Deg2Rad; return new Vector2(Mathf.Sin(r), Mathf.Cos(r)); }
        public static Vector2 StoneAcross(float yaw) { float r = yaw * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), -Mathf.Sin(r)); }

        /// <summary>B78.c's path: the stone lies along its own length (the foot at the spot, the top away along <see cref="StoneUpAxis"/>) and rolls
        /// across it, so the heave is one tip up on its foot into the stone's own standing turn (<paramref name="yaw"/>): no snap at the end. It comes
        /// from the side away from the first stone (<paramref name="flip"/>: the other side, when that one is out of the box). <paramref name="end"/>:
        /// where the foot lies when the roll ends.</summary>
        public static void RollPath(Vector2 spot, Vector2 first, float yaw, bool flip, out Vector2 dir, out Vector2 start, out Vector2 end, out int quarters)
        {
            var across = StoneAcross(yaw);
            dir = Vector2.Dot(first - spot, across) >= 0f ? across : -across;
            if (flip) dir = -dir;
            end = spot + StoneUpAxis(yaw) * (StoneT * 0.5f);
            quarters = RollQuarters(RollFrom);
            start = end - dir * (quarters * RollStep);
        }

        /// <summary>How far the stone has turned (deg about its length) <paramref name="secs"/> into the roll: a quarter each 0.8 s.</summary>
        public static float RollTurn(float secs)
        {
            if (secs <= 0f) return 0f;
            int q = Mathf.FloorToInt(secs / QuarterEvery);
            return 90f * (q + Smooth(secs / QuarterEvery - q));
        }

        /// <summary>The rolled stone <paramref name="u"/> scene seconds in, for the mesh (its origin is the foot of its length, in the middle of its
        /// section): where it is (box x, z; <paramref name="height"/> above the ground), how it is turned, and how far it is stood up (0 lying, 1
        /// upright at <see cref="CampScene.c"/>, in the standing turn <paramref name="yaw"/>). The roll tips it over its leading edge each quarter
        /// (flat, on edge, flat); the heave tips it up on the edge at its foot.</summary>
        public static void StoneNow(CampScene s, float u, float yaw, out Vector2 at, out float height, out Quaternion rot, out float up)
        {
            var stand = Quaternion.Euler(0f, yaw, 0f);
            float h = StoneT * 0.5f;
            float h0 = s.c - HeaveLift;
            if (u < h0)
            {
                var dir = (s.p1 - s.p0).sqrMagnitude > 1e-8f ? (s.p1 - s.p0).normalized : StoneAcross(yaw);
                int n = Mathf.Max(1, Mathf.RoundToInt((s.b - s.a) / QuarterEvery));
                float psi = RollTurn(Mathf.Clamp(u - s.a, 0f, s.b - s.a));
                int q = Mathf.Min(n, Mathf.FloorToInt(psi / 90f + 1e-4f));
                float phi = q >= n ? 0f : (psi - 90f * q) * Mathf.Deg2Rad;
                bool flat = q % 2 == 0;
                float ha = (flat ? StoneW : StoneT) * 0.5f, hb = (flat ? StoneT : StoneW) * 0.5f; // half along the roll, half up, at the quarter's start
                float along = q * RollStep + ha - ha * Mathf.Cos(phi) + hb * Mathf.Sin(phi);
                at = s.p0 + dir * along;
                height = ha * Mathf.Sin(phi) + hb * Mathf.Cos(phi);
                var dir3 = new Vector3(dir.x, 0f, dir.y);
                rot = Quaternion.AngleAxis(psi, Vector3.Cross(Vector3.up, dir3)) * stand * Quaternion.Euler(90f, 0f, 0f);
                up = 0f;
                return;
            }
            up = Smooth((u - h0) / Mathf.Max(1e-3f, s.c - h0));
            float tip = 90f * (1f - up);
            rot = stand * Quaternion.Euler(tip, 0f, 0f);
            var edge = s.p1; // the edge at its foot it tips up on (on the ground; the foot lies there at the roll's end)
            var o = new Vector3(edge.x, 0f, edge.y) + rot * new Vector3(0f, 0f, -h);
            at = new Vector2(o.x, o.z); height = o.y;
        }

        /// <summary>The shortest distance from <paramref name="p"/> to the segment <paramref name="a"/>-<paramref name="b"/>.</summary>
        public static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = b - a; float l = d.sqrMagnitude;
            float k = l < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, d) / l);
            return (p - (a + d * k)).magnitude;
        }

        /// <summary>Where the family stands round the new stone: the camera's side first, each clear of the roll (a kid <see cref="KidsBack"/>, a
        /// grown-up 0.15 u), of where the stone lay before the heave (<paramref name="lieTop"/>: its top), of the first stone (0.2), of the cutter's kneel (0.12) and of each other (0.09).</summary>
        public static List<Vector2> FamilySpots(IList<bool> kid, Vector2 from, Vector2 spot, Vector2 first, Vector2 kneel, Vector2 toCam, Vector2? lieTop = null)
        {
            toCam = Unit(toCam, Vector2.down);
            var got = new List<Vector2>();
            for (int i = 0; i < kid.Count; i++)
            {
                float r0 = kid[i] ? 0.42f : 0.34f, clear = kid[i] ? KidsBack : 0.15f;
                Vector2 best = spot + toCam * (r0 + 0.3f); bool found = false;
                for (int step = 0; step < 25 && !found; step++)
                {
                    float ang = (step % 2 == 0 ? 1f : -1f) * 15f * ((step + 1) / 2);
                    for (float rr = r0; rr <= r0 + 0.25f && !found; rr += 0.08f)
                    {
                        var c = spot + Rot(toCam, ang) * rr;
                        if (ToSegment(c, from, spot) < clear || lieTop.HasValue && ToSegment(c, spot, lieTop.Value) < StoneW * 0.5f + 0.08f || (c - first).magnitude < 0.2f || (c - kneel).magnitude < 0.12f) continue;
                        bool near = false;
                        foreach (var g in got) if ((g - c).magnitude < 0.09f) { near = true; break; }
                        if (near) continue;
                        best = c; found = true;
                    }
                }
                got.Add(best);
            }
            return got;
        }

        /// <summary>Leaning into the stone, both hands on it (arms x 90).</summary>
        static void Brace(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.3f);
            p.lean += PushLean * 0.6f * u; p.armL = Lerp(p.armL, new Vector3(90f, 0f, -6f), u); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), u);
        }

        /// <summary>B78.c's push: lean 25, arms x 90 on the stone, a shove with each quarter turn.</summary>
        static void PushPose(ref DollPose p, float t, float len)
        {
            float shove = Bump(Mathf.Repeat(t, QuarterEvery), 0f, QuarterEvery);
            p.lean += PushLean + 5f * shove; p.armL = new Vector3(90f - 10f * shove, 0f, -6f); p.armR = new Vector3(90f - 10f * shove, 0f, 6f);
        }

        /// <summary>The heave (HuntBeats.Lift's bend and lift): down to the stone (lean 32, arms x 85), then up with it as it tips up (lean −10, arms x 150).</summary>
        static void Heave(ref DollPose p, float t, float len)
        {
            float bend = In(t, 0f, HeaveBend * 0.7f), up = In(t, HeaveBend, HeaveLift);
            p.squash = Mathf.Lerp(1f, 0.9f, bend * (1f - up));
            p.lean += Mathf.Lerp(32f * bend, -10f, up);
            p.armL = Lerp(new Vector3(30f, 0f, -12f), new Vector3(150f, 0f, -10f), up); p.armR = Lerp(new Vector3(85f, 0f, 8f), new Vector3(150f, 0f, 10f), up);
        }

        /// <summary>Copies a one-member scene's timeline (its parts, effects and tips) into member <paramref name="member"/> of <paramref name="into"/>,
        /// <paramref name="at"/> seconds on: B78.c's name is #693's cut as it is.</summary>
        static void Append(CampScene into, int member, CampScene from, float at)
        {
            foreach (var p in from.parts[0])
                into.parts[member].Add(new CampPart
                {
                    from = p.from + at, to = p.to + at, pos = p.pos, face = p.face, keepSpot = p.keepSpot, style = p.style, prop = p.prop, slot = p.slot,
                    speed = p.speed, run = p.run, pose = p.pose, poseWalking = p.poseWalking, carried = p.carried, dipFrom = p.dipFrom,
                });
            foreach (var f in from.fx) { var g = f; g.time += at; if (g.member == 0) g.member = member; into.fx.Add(g); }
            foreach (var tp in from.tips) { var g = tp; g.from += at; g.to += at; g.member = member; into.tips.Add(g); }
        }

        /// <summary>
        /// B78.c: two grown-ups (<paramref name="who"/>, the one named, and <paramref name="helper"/>; Nobody: who alone) walk to a rough stone at
        /// the camp's edge (<paramref name="from"/>) and roll it in (both push, lean 25, a quarter turn each 0.8 s) to its spot beside the first;
        /// they heave it up on its end (the bend and lift; the stone jolts up at <see cref="CampScene.c"/>); <paramref name="who"/> cuts the first
        /// name on it (#693's NameCut, as it is; the new row at its end); the family stands round with heads down (Mourn) from the roll's end
        /// until the name is cut. <see cref="CampScene.a"/>-<see cref="CampScene.b"/>: the roll; <see cref="CampScene.d"/>: the name is cut;
        /// <see cref="CampScene.p0"/>, <see cref="CampScene.p1"/>: the roll's start and end. Only grown-ups roll it; the kids keep 0.3 u clear.
        /// </summary>
        public static CampScene SecondStone(int who, Vector2 whoAt, int helper, Vector2 helperAt, IList<int> family, IList<Vector2> familyAt, IList<bool> kid,
            Vector2 spot, Vector2 first, float yaw, bool flip, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            RollPath(spot, first, yaw, flip, out var dir, out var from, out var end, out int quarters);
            var len = StoneUpAxis(yaw); // the stone's length while it lies: from its foot at the path toward its top
            // the camera holds between the roll and the stone's spot (the cut happens at the spot, on the camera side)
            var s = new CampScene(CampUse.SecondStone, t0, ((from + end) * 0.5f + spot) * 0.5f) { holdFocus = true, p0 = from, p1 = end };
            int mw = s.Add(who), mh = helper != Nobody ? s.Add(helper) : -1;
            var rollers = mh >= 0 ? new[] { mw, mh } : new[] { mw };
            // behind it, both hands on its side, spread along its length
            Vector2 Push(Vector2 foot, int i) => foot - dir * Behind + len * (StoneLen * (rollers.Length == 1 ? 0.5f : i == 0 ? 0.3f : 0.72f));
            // to the stone at the camp's edge
            // from across the camp they hurry, so the stone doesn't wait long in the held camera (the review: 9 s of a still stone)
            var walks = new float[rollers.Length]; var speeds = new float[rollers.Length];
            var ats = mh >= 0 ? new[] { whoAt, helperAt } : new[] { whoAt };
            for (int i = 0; i < rollers.Length; i++)
            {
                speeds[i] = Mathf.Max(Walk, (Push(from, i) - ats[i]).magnitude / ApproachFor);
                walks[i] = WalkTime(ats[i], Push(from, i), speeds[i]);
            }
            float t = 0f;
            foreach (float w in walks) t = Mathf.Max(t, w);
            for (int i = 0; i < rollers.Length; i++)
            {
                s.Part(rollers[i], 0f, walks[i], Push(from, i), Push(from, i) + dir, DollStyle.Stand, null, PropKind.None, speeds[i]);
                if (walks[i] < t) s.Part(rollers[i], walks[i], t, Push(from, i), Push(from, i) + dir, DollStyle.Stand);
                s.Part(rollers[i], t, t + 0.5f, Push(from, i), Push(from, i) + dir, DollStyle.Stand, Brace);
            }
            t += 0.5f;
            s.a = t;
            float roll = RollFor(quarters);
            for (int i = 0; i < rollers.Length; i++)
            {
                var p = s.Part(rollers[i], t, t + roll, Push(end, i), Push(end, i) + dir, DollStyle.Stand, PushPose, PropKind.None, Mathf.Max(0.02f, (end - from).magnitude / roll));
                p.poseWalking = true;
            }
            for (int q = 1; q <= quarters; q++) s.Fx(t + q * QuarterEvery - 0.05f, CampFxKind.Dust, G(from + dir * (q * RollStep) + len * (StoneLen * 0.5f), 0.005f), 0.3f, 2); // a little dust as each edge lands
            t += roll;
            s.b = t;
            // the heave: both at its top half, one each side, bend and lift it up on its foot beside the first
            Vector2 Lift(int i) => end + len * (StoneLen * 0.62f) + dir * ((rollers.Length == 1 ? -1f : i == 0 ? -1f : 1f) * (StoneW * 0.5f + 0.07f));
            float lw = 0f;
            for (int i = 0; i < rollers.Length; i++) lw = Mathf.Max(lw, WalkTime(Push(end, i), Lift(i), Walk));
            for (int i = 0; i < rollers.Length; i++)
            {
                float w = WalkTime(Push(end, i), Lift(i), Walk);
                s.Part(rollers[i], t, t + w, Lift(i), end + len * (StoneLen * 0.62f), DollStyle.Stand, null, PropKind.None, Walk);
                if (w < lw) s.Part(rollers[i], t + w, t + lw, Lift(i), end + len * (StoneLen * 0.62f), DollStyle.Stand);
            }
            t += lw; // the stone waits, lying, until they are at it
            for (int i = 0; i < rollers.Length; i++) s.Part(rollers[i], t, t + HeaveFor, Lift(i), spot, DollStyle.Stand, Heave);
            s.c = t + HeaveBend + HeaveLift;
            s.Fx(s.c, CampFxKind.Prop, Vector3.zero, 0f, (int)PropFx.Jolt); // the stone stands: it jolts (CampLife.Props)
            s.Fx(s.c, CampFxKind.Dust, G(spot, 0.01f), 0.8f, 5);
            t += HeaveFor;
            // the first name: the runestone's cut (#693), by the one who rolled it
            var cut = CampScenes6.NameCut(who, Lift(0), spot, toCam, 0f);
            Append(s, mw, cut, t);
            float rowAt = -1f;
            foreach (var f in cut.fx) if (f.kind == CampFxKind.Prop && f.count == (int)PropFx.NameRow) rowAt = f.time;
            s.d = t + (rowAt >= 0f ? rowAt : cut.Length);
            float cutEnd = s.d + 0.8f; // the cutter's nod after the row
            // the family (and the helper) round the stones, heads down; in only once the stone has stopped rolling
            var kneel = spot + toCam * CampScenes6.NameKneel + acr * 0.09f;
            var flags = new List<bool>();
            if (mh >= 0) flags.Add(false);
            for (int i = 0; i < family.Count; i++) flags.Add(kid[i]);
            var spots = FamilySpots(flags, from, end, first, kneel, toCam, end + len * StoneLen);
            int si = 0;
            if (mh >= 0)
            {
                var hs = spots[si++];
                float w = WalkTime(Lift(1), hs, Walk);
                s.Part(mh, t, t + w, hs, spot, DollStyle.Stand, null, PropKind.None, Walk);
                if (t + w < cutEnd) s.Part(mh, t + w, cutEnd, hs, spot, DollStyle.Mourn);
                Go(s, mh, Mathf.Max(t + w, cutEnd), 1.2f, hs + toCam * 0.3f, hs + toCam, Walk);
            }
            for (int i = 0; i < family.Count; i++)
            {
                int m = s.Add(family[i]);
                var fs = spots[si++];
                float speed = kid[i] ? KidWalk : Walk, w = WalkTime(familyAt[i], fs, speed), start = s.b;
                s.Part(m, start, start + w, fs, spot, DollStyle.Stand, null, PropKind.None, speed);
                if (start + w < cutEnd) s.Part(m, start + w, cutEnd, fs, spot, DollStyle.Mourn);
                Go(s, m, Mathf.Max(start + w, cutEnd), 1.2f, fs + toCam * 0.3f + acr * ((i % 2 == 0 ? 1f : -1f) * 0.1f), fs + toCam, speed);
            }
            return s;
        }

        // ================================================================== B80.a the Keeper's child comes of age
        /// <summary>B80.a: how far apart the two stand for the hand-over (B7.b's <see cref="PairStage.HandGap"/> and a little), how long the tool is
        /// held up, and how far round the fire the watchers sit.</summary>
        public const float GiveGap = 0.22f, ToolUpFor = 1.4f, WatchR = 0.3f;

        /// <summary>The work's tool the elder hands over (the spec: an axe, a basket or a rod): a rod for a fisher, an axe for the work that
        /// cuts or shapes (wood, the hunt, tools, stone, metal), a basket for every other job [est].</summary>
        public static PropKind ToolOf(string job) => job switch
        {
            AquariumCiv.Sim.JobType.Fisher => PropKind.Rod,
            AquariumCiv.Sim.JobType.Woodcutter or AquariumCiv.Sim.JobType.Hunter or AquariumCiv.Sim.JobType.Toolmaker
                or AquariumCiv.Sim.JobType.Mason or AquariumCiv.Sim.JobType.Smith => PropKind.Axe,
            _ => PropKind.Basket,
        };

        /// <summary>The new tool held up: the right arm to x 150, the left out a little, a small hop at the top (0.015 u).</summary>
        static void ToolUp(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f));
            p.armR = Lerp(p.armR, new Vector3(150f, 0f, 10f), u); p.armL = Lerp(p.armL, new Vector3(40f, 0f, -20f), u);
            p.offset.y += 0.015f * Bump(t, 0.25f, 0.35f);
        }

        /// <summary>Seated at the fire, a nod.</summary>
        static void SitNod(ref DollPose p, float t, float len) { Seated(ref p); NodPose(ref p, Mathf.Max(0f, t - 0.2f), len); }

        /// <summary>
        /// B80.a: the one the Keeper named (<paramref name="young"/>, just grown) and an elder (<paramref name="elder"/>, carrying the tool) come to
        /// the fire's camera side and stand face to face across the camera (0.22 u apart); the elder nods; B7.b's hand-over (<see cref="PairStage.HandOver"/>:
        /// held out, the hands meet, the tool changes hands at the contact <see cref="CampScene.a"/>, the taker lifts it to look); the young one holds
        /// it up (right arm x 150, a small hop, 1.4 s) while the elder and the watchers at the fire nod; then the young one walks off with it to
        /// work, and the elder sits down at the fire. <see cref="CampScene.b"/>: the hand-over is over; <see cref="CampScene.c"/>: the tool is
        /// lowered. Up to three watchers sit round the far side of the fire.
        /// </summary>
        public static CampScene ComeOfAge(int young, Vector2 youngAt, int elder, Vector2 elderAt, IList<int> watchers, IList<Vector2> watchersAt,
            Vector2 fire, Vector2 toCam, PropKind tool, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var mid = fire + toCam * 0.3f;
            var ys = mid + acr * (GiveGap * 0.5f); var es = mid - acr * (GiveGap * 0.5f);
            var s = new CampScene(CampUse.KeeperChildGrown, t0, mid) { holdFocus = true };
            int my = s.Add(young), me = s.Add(elder);
            // to the fire: the elder brings the tool
            float wy = WalkTime(youngAt, ys, Walk), we = WalkTime(elderAt, es, Walk);
            float t = Mathf.Max(wy, we);
            s.Part(my, 0f, wy, ys, es, DollStyle.Stand, null, PropKind.None, Walk);
            if (wy < t) s.Part(my, wy, t, ys, es, DollStyle.Stand);
            s.Part(me, 0f, we, es, ys, DollStyle.Stand, null, tool, Walk);
            if (we < t) s.Part(me, we, t, es, ys, DollStyle.Stand, null, tool);
            // face to face; the elder nods
            s.Part(my, t, t + 0.8f, ys, es, DollStyle.Stand);
            s.Part(me, t, t + 0.8f, es, ys, DollStyle.Stand, NodPose, tool);
            t += 0.8f;
            // B7.b: the hand-over (the pair's cue holds both while it plays; the parts under it keep them in the scene)
            var ev = PairStage.HandOver(8011 + young * 31 + elder, t0 + t, elder, young, es, ys, tool);
            float give = ev != null ? ev.approach + ev.duration : PairBeats.HandOverSeconds;
            if (ev != null) s.pairs.Add(ev);
            s.a = t + (ev != null ? ev.approach + ev.giverBeat.ContactAt : PairBeats.HandOverContactAt);
            s.Part(my, t, s.a, ys, es, DollStyle.Stand);
            s.Part(my, s.a, t + give, ys, es, DollStyle.Stand, null, tool);
            s.Part(me, t, s.a, es, ys, DollStyle.Stand, null, tool);
            s.Part(me, s.a, t + give, es, ys, DollStyle.Stand);
            t += give;
            s.b = t;
            // held up for everyone to see
            s.Part(my, t, t + ToolUpFor, ys, ys + toCam, DollStyle.Stand, ToolUp, tool);
            s.Part(me, t, t + ToolUpFor, es, ys, DollStyle.Stand, NodPose);
            float up = t;
            t += ToolUpFor;
            s.c = t;
            // off to work with it; the elder sits down at the fire
            var off = ys + acr * 0.4f + toCam * 0.15f;
            Go(s, my, t, WalkTime(ys, off, Walk), off, off + acr, Walk).prop = tool;
            var seat = fire + Rot(toCam, -70f) * WatchR;
            float ws = WalkTime(es, seat, Walk);
            s.Part(me, t, t + ws, seat, fire, DollStyle.Sit, null, PropKind.None, Walk);
            s.Part(me, t + ws, t + ws + 1.5f, seat, fire, DollStyle.Sit);
            float end = t + ws + 1.5f;
            s.d = end;
            // the watchers round the far side of the fire, facing the two
            int n = Mathf.Min(3, watchers.Count);
            var angles = new[] { 150f, -150f, 180f };
            for (int i = 0; i < n; i++)
            {
                int m = s.Add(watchers[i]);
                var spot = fire + Rot(toCam, angles[i]) * WatchR;
                float w = Mathf.Min(WalkTime(watchersAt[i], spot, Walk), up);
                s.Part(m, 0f, w, spot, mid, DollStyle.Sit, null, PropKind.None, Walk);
                if (w < up) s.Part(m, w, up, spot, mid, DollStyle.Sit);
                s.Part(m, up, up + ToolUpFor, spot, mid, DollStyle.Sit, SitNod);
                s.Part(m, up + ToolUpFor, end, spot, fire, DollStyle.Sit);
            }
            return s;
        }
    }
}
