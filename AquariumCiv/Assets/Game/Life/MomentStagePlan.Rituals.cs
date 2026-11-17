// S-ritual (B72-B75, docs/design/animation-specs.md "Rituals and the teller"): each yearly ritual's own staged plan, in place of the
// festival's gathering. Pure (System and the Life/Sim types), so Tools/SimTests and Tools/AnimBench compile it. The view poses each beat
// (View/Beats/RitualBeats.cs) and draws the things in the hands and on the ground (View/Moments/RitualProps.cs).
using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>S-ritual: what a member has in the hands in a ritual's step (the view picks the mesh and the grip).</summary>
    public enum StageHold : byte { None, Basket, FullBasket, SmallBasket, Log, Twig, Bowl, Board, Offering, Bundle }

    public sealed partial class MomentStagePlan
    {
        // ---- the numbers (animation-specs B72-B75; all [est] until AnimLab) ----
        /// <summary>The walking pace the plan's own walks use (u/s): the planner's real walk in the day.</summary>
        public const float Pace = 0.3f;
        /// <summary>B72: a bush farther than this from the fire is skipped.</summary>
        public const float BushReach = 2.5f;
        /// <summary>B72: at most this many bushes, this many baskets handed out (0.8 s each), the line's gap, the pick's length.</summary>
        public const int MaxBushes = 6, MaxHandOut = 4;
        public const float HandOutSeconds = 0.8f, LineGap = 0.14f, PickSeconds = 12f, PickCycle = 2.5f, TipGap = 0.4f, TipLength = 1.6f, HeavyPace = 0.85f;
        /// <summary>B73: the line's gap, the zigzag, a hop, a new log every, the twigs' line.</summary>
        public const float PassGap = 0.15f, Zig = 0.03f, HopSeconds = 0.65f, LogEvery = 1.2f, TwigGap = 0.1f, TwigHop = 0.5f, TwigEvery = 1f;
        public const int MaxLogLine = 8, MaxTwigKids = 4;
        /// <summary>B74: seats along a bench, a bench's distance out from the table's middle, a hop of the bowls down a side.</summary>
        public const float SeatGap = 0.07f, BenchOut = 0.14f, BowlHop = 0.5f, EatSeconds = 10f, SongSeconds = 6f;
        /// <summary>B75.a: the careful walk's pace (× the walk), its longest (a far tree starts part way), the kids' and the pairs' gaps.</summary>
        public const float CarefulPace = 0.75f, MaxCarry = 14f, KidGap = 0.2f, PairGap = 0.2f;
        public const int MaxOfferingGrown = 4, MaxOfferingKids = 2;

        /// <summary>S-ritual: the ritual this plan stages (null = the festival's stand-in).</summary>
        public string RitualStaged { get; private set; }

        bool Ritual(MomentCast c)
        {
            var sp = geo.spots;
            bool ok = false;
            if (sp != null)
            {
                switch (sp.ritual)
                {
                    case RitualKind.Berrying: ok = Berrying(c, sp); break;
                    case RitualKind.Woodpile: ok = Woodpile(c, sp); break;
                    case RitualKind.Feast: ok = Feast(c, sp); break;
                    case RitualKind.Offering: ok = Offering(c, sp); break;
                }
            }
            if (ok) { RitualStaged = sp.ritual; return true; }
            string why = LastWhyNot;
            members.Clear(); ringKeys.Clear();
            bool fest = Festival(c); // no target (no bushes in reach, no woodpile, no table): the festival's gathering stands in
            if (fest && !string.IsNullOrEmpty(why)) LastWhyNot = "stand-in: " + why;
            return fest;
        }

        // ---------------------------------------------------------------- helpers (every step starts where the last one ended)

        /// <summary>When the last of them gets to their spot at a walk (each from where they last are), at least <paramref name="least"/>.</summary>
        static float ArriveBy(float least, IEnumerable<(MomentMember m, float x, float z)> to)
        {
            float t = least;
            foreach (var (m, x, z) in to) { var (x0, z0) = Last(m); t = Math.Max(t, End(m) + Dist(x0, z0, x, z) / Pace); }
            return t;
        }

        static float End(MomentMember m) => m.steps.Count == 0 ? 0f : m.steps[m.steps.Count - 1].t1;

        /// <summary>Stand where they are until <paramref name="t1"/>.</summary>
        static MomentStep Stay(MomentMember m, StageBeat b, ActivityKind k, float t1)
        {
            var (x, z) = Last(m);
            return Step(b, k, End(m), t1, x, z, x, z);
        }

        /// <summary>A straight move from where they are to (x, z), arriving at <paramref name="t1"/>.</summary>
        static MomentStep Move(MomentMember m, StageBeat b, ActivityKind k, float t1, float x, float z)
        {
            var (x0, z0) = Last(m);
            return Step(b, k, End(m), t1, x0, z0, x, z);
        }

        static MomentStep Hold(MomentStep s, StageHold h) { s.hold = h; return s; }
        static MomentStep Said(MomentStep s, string line) { s.line = line; return s; }
        static MomentStep Arg(MomentStep s, int a) { s.arg = a; return s; }
        static MomentStep Cue(MomentStep s, float c) { s.cue = c; return s; }
        static MomentStep EndBeat(MomentStep s) { s.endBeat = true; return s; }

        static (float x, float z) Unit(float x, float z, float dx = 0f, float dz = -1f)
        {
            float l = (float)Math.Sqrt(x * x + z * z);
            return l < 1e-5f ? (dx, dz) : (x / l, z / l);
        }

        /// <summary>(x, z) turned by <paramref name="deg"/>.</summary>
        static (float x, float z) Turn(float x, float z, float deg)
        {
            double a = deg * Math.PI / 180.0; float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return (x * c - z * s, x * s + z * c);
        }

        /// <summary>Nobody stands in the fire: a ritual's spots keep this far from its middle (u; the fire is drawn ~0.3 across) [est: clip
        /// 721-berrying-first-walk, a grown-up at the basket stack stood in the flames].</summary>
        public const float FireClear = 0.3f;

        (float x, float z) In(float x, float z, float margin = 0.12f)
        {
            float dx = x - geo.fireX, dz = z - geo.fireZ, d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d < FireClear) { if (d < 1e-4f) { dx = Gx; dz = Gz; d = 1f; } x = geo.fireX + dx / d * FireClear; z = geo.fireZ + dz / d * FireClear; }
            ClampIn(ref x, ref z, margin);
            return (x, z);
        }

        /// <summary>The crowd for a ritual: the lead first (if any), then the grown and the kids, nearest first, who can get there in
        /// <see cref="MaxGather"/>; at most <paramref name="grown"/> grown-ups (the lead not counted) and <paramref name="kids"/> kids.</summary>
        List<(MomentPerson p, bool lead)> RitualCrowd(MomentCast c, int grown, int kids, float spread)
        {
            var all = new List<(MomentPerson p, bool lead)>();
            if (c.hasOfficiant) all.Add((c.officiant, true));
            int g = 0, k = 0;
            foreach (var p in c.grown) if (g < grown && all.Count < MaxFestival && GoAt(p.key, 0f, spread) + p.walk + 0.5f <= MaxGather) { all.Add((p, false)); g++; }
            foreach (var p in c.kids) if (k < kids && all.Count < MaxFestival && GoAt(p.key, 0f, spread) + p.walk + 0.5f <= MaxGather) { all.Add((p, false)); k++; }
            return all;
        }

        float GatherOf(List<(MomentPerson p, bool lead)> all, float least, float spread)
        {
            float g = least;
            foreach (var e in all) g = Math.Max(g, GoAt(e.p.key, 0f, spread) + e.p.walk + 0.5f);
            return g;
        }

        /// <summary>Kids after grown-ups in turn (a kid is never first in a line, kids stay near a grown-up).</summary>
        static List<MomentMember> Interleave(List<MomentMember> grown, List<MomentMember> kids)
        {
            var r = new List<MomentMember>();
            int gi = 0, ki = 0;
            while (gi < grown.Count || ki < kids.Count)
            {
                if (gi < grown.Count) r.Add(grown[gi++]);
                if (ki < kids.Count && (gi > 0 || grown.Count == 0)) r.Add(kids[ki++]);
            }
            return r;
        }

        // ======================================================================== B72 the berrying walk

        /// <summary>B72: the kid who runs 0.3 u ahead of the lead on the walk out and back (0 = none), when it sets off and turns back.</summary>
        public int RunAhead;
        public float RunAheadFrom, RunAheadBack;

        bool Berrying(MomentCast c, RitualSpots sp)
        {
            float fx = geo.fireX, fz = geo.fireZ;
            var bush = new List<(float x, float z)>();
            if (sp.bushes != null)
                for (int i = 0; i + 1 < sp.bushes.Length && bush.Count < MaxBushes; i += 2)
                    if (Dist(sp.bushes[i], sp.bushes[i + 1], fx, fz) <= BushReach) bush.Add((sp.bushes[i], sp.bushes[i + 1]));
            if (bush.Count == 0) return Fail("no berry bush in reach");
            if (!c.hasOfficiant) return Fail("nobody to lead the berrying walk");
            var all = RitualCrowd(c, MaxFestival, MaxFestival, 1.5f);
            if (all.Count < 3) return Fail("too few people for the berrying walk");
            float gather = GatherOf(all, 6f, 1.5f);
            if (gather > MaxGather) return Fail("the lead can't get there in time");

            // Where they gather: the basket stack (else in front of the fire); the lead behind it, the rest in a half ring in front.
            float sx = sp.hasStack ? sp.stackX : fx + Gx * 0.3f, sz = sp.hasStack ? sp.stackZ : fz + Gz * 0.3f;
            (sx, sz) = In(sx, sz);
            // The bushes they pick: as many as there are pairs, nearest first.
            int nb = Math.Max(1, Math.Min(bush.Count, (all.Count + 1) / 2));
            float cx = 0f, cz = 0f;
            for (int i = 0; i < nb; i++) { cx += bush[i].x / nb; cz += bush[i].z / nb; }
            var (dx, dz) = Unit(cx - sx, cz - sz, Gx, Gz);
            float px = -dz, pz = dx; // across the line
            bool hasHome = sp.toTable && sp.hasTable || sp.hasStash;
            float hx = sp.toTable && sp.hasTable ? sp.tableX : sp.hasStash ? sp.stashX : sx, hz = sp.toTable && sp.hasTable ? sp.tableZ : sp.hasStash ? sp.stashZ : sz;
            bool toTable = sp.toTable && sp.hasTable;

            var lead = Add(all[0].p, MomentRole.Leader);
            var grownM = new List<MomentMember>(); var kidM = new List<MomentMember>();
            MomentMember firstKid = null, firstParent = null;
            for (int i = 1; i < all.Count; i++)
            {
                var m = Add(all[i].p, all[i].p.kid ? MomentRole.Kid : MomentRole.Bringer);
                if (m.kid) kidM.Add(m); else grownM.Add(m);
                if (sp.firstWalk != 0 && m.key == sp.firstWalk && m.kid) firstKid = m;
                if (sp.firstWalkParent != 0 && m.key == sp.firstWalkParent && !m.kid) firstParent = m;
            }
            if (firstKid != null && firstParent == null) firstParent = lead.key == sp.firstWalkParent ? lead : grownM.Count > 0 ? grownM[0] : lead;
            if (firstKid != null) kidM.Remove(firstKid);
            // The line: the lead, then grown-ups and kids in turn; the first-walk kid right after its parent, hand in hand.
            var line = new List<MomentMember> { lead };
            foreach (var m in Interleave(grownM, kidM)) { line.Add(m); if (m == firstParent && firstKid != null) line.Add(firstKid); }
            if (firstKid != null && !line.Contains(firstKid)) line.Insert(1, firstKid);
            int n = line.Count;

            // ---- 1 Baskets: the lead stands behind the stack and hands out up to 4; the others brought their own.
            float lx = sx - Gx * 0.1f, lz = sz - Gz * 0.1f;
            Put(lead, Glass(Travel(lead, GoAt(lead.key, 0f, 1.5f), gather, lx, lz)));
            var takers = new List<MomentMember>();
            int slot = 0;
            foreach (var m in line)
            {
                if (m == lead) continue;
                bool takes = takers.Count < MaxHandOut && sp.hasStack && m != firstKid;
                if (takes) takers.Add(m);
                int k = slot++;
                float r = takes ? 0.22f : 0.38f + 0.1f * (k / 7);
                float a = -75f + 150f * ((k % 7) + 0.5f) / 7f;
                var (ox, oz) = Turn(Gx, Gz, a);
                var (tx, tz) = In(sx + ox * r, sz + oz * r);
                var tr = Facing(Travel(m, GoAt(m.key, 0f, 1.5f), gather, tx, tz, takes ? Prop.None : Prop.Basket), lx, lz);
                Put(m, Hold(tr, takes ? StageHold.None : m == firstKid ? StageHold.SmallBasket : StageHold.Basket));
            }
            float tH = gather;
            for (int i = 0; i < takers.Count; i++)
            {
                var (tx, tz) = Last(takers[i]);
                Put(lead, Hold(Arg(Facing(Stay(lead, StageBeat.HandOut, ActivityKind.Watch, tH + HandOutSeconds * (i + 1)), tx, tz), i), StageHold.Basket));
                Put(takers[i], Facing(Stay(takers[i], StageBeat.Stand, ActivityKind.Watch, tH + HandOutSeconds * i + 0.35f), lx, lz));
                Put(takers[i], Hold(Facing(Stay(takers[i], StageBeat.TakeBasket, ActivityKind.Watch, tH + HandOutSeconds * (i + 1)), lx, lz), StageHold.Basket));
            }
            float tUp = tH + HandOutSeconds * takers.Count;
            Put(lead, Hold(Glass(Stay(lead, StageBeat.HoldBasketUp, ActivityKind.Watch, tUp + 1.2f)), StageHold.Basket));
            float tB = tUp + 1.2f;
            foreach (var m in line) if (m != lead) Put(m, Hold(Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tB), lx, lz), HoldOf(m)));

            // ---- 2 Walk out: fall in behind the lead, then the line walks out together at one pace and each goes on to a bush.
            float headX = sx + dx * 0.12f, headZ = sz + dz * 0.12f;
            var qx = new float[n]; var qz = new float[n];
            float fall = 0.8f;
            for (int i = 0; i < n; i++)
            {
                var m = line[i];
                float side = m == firstKid ? 0.06f : (Hash01(seed, m.key, 3801) - 0.5f) * 0.08f;
                int at = m == firstKid ? Math.Max(0, i - 1) : i;
                (qx[i], qz[i]) = In(headX - dx * LineGap * at + px * side, headZ - dz * LineGap * at + pz * side);
                var (x0, z0) = Last(m);
                fall = Math.Max(fall, Dist(x0, z0, qx[i], qz[i]) / Pace);
            }
            float tF = tB + Math.Min(4f, fall);
            for (int i = 0; i < n; i++) Put(line[i], Hold(Arg(Move(line[i], StageBeat.WalkOut, ActivityKind.Walk, tF, qx[i], qz[i]), HandOf(line[i])), HoldOf(line[i])));
            // the bushes: 2-3 to a bush, round its side they come from; the first-walk kid at its parent's bush
            var bushOf = new int[n]; var inBush = new int[n]; var perBush = new int[nb];
            for (int i = 0; i < n; i++) { int b = line[i] == firstKid && firstParent != null ? bushOf[line.IndexOf(firstParent)] : i % nb; bushOf[i] = b; inBush[i] = perBush[b]++; }
            var bx = new float[n]; var bz = new float[n]; var arrive = new float[n];
            float tP = tF;
            for (int i = 0; i < n; i++)
            {
                var (ux, uz) = Unit(qx[i] - bush[bushOf[i]].x, qz[i] - bush[bushOf[i]].z, Gx, Gz); // the side they come from
                float a = (inBush[i] - (perBush[bushOf[i]] - 1) * 0.5f) * 50f;
                var (ox, oz) = Turn(ux, uz, a);
                (bx[i], bz[i]) = In(bush[bushOf[i]].x + ox * 0.13f, bush[bushOf[i]].z + oz * 0.13f);
                arrive[i] = tF + Math.Max(1f, Math.Min(9f, Dist(qx[i], qz[i], bx[i], bz[i]) / Pace));
                tP = Math.Max(tP, arrive[i]);
            }
            if (firstKid != null && firstParent != null) { int ki = line.IndexOf(firstKid), pi = line.IndexOf(firstParent); arrive[ki] = arrive[pi] = Math.Max(arrive[ki], arrive[pi]); } // hand in hand: they arrive together
            // a kid runs 0.3 u ahead of the lead and back once (the first kid in the line who isn't on its first walk)
            RunAhead = 0; var runX = new float[2]; var runZ = new float[2];
            int ra = line.FindIndex(m => m.kid && m != firstKid);
            float runEnd = 0f;
            if (ra > 0)
            {
                RunAhead = line[ra].key;
                float leadAt = Math.Min(1f, 2.5f * Pace / Math.Max(0.01f, Dist(qx[0], qz[0], bx[0], bz[0]))); // where the lead is 2.5 s in
                float lx2 = qx[0] + (bx[0] - qx[0]) * leadAt, lz2 = qz[0] + (bz[0] - qz[0]) * leadAt;
                (runX[0], runZ[0]) = In(lx2 + dx * 0.3f, lz2 + dz * 0.3f);
                (runX[1], runZ[1]) = In(qx[ra] + (bx[ra] - qx[ra]) * 0.4f, qz[ra] + (bz[ra] - qz[ra]) * 0.4f);
                float r1 = Math.Max(0.8f, Dist(qx[ra], qz[ra], runX[0], runZ[0]) / RunSpeed), r2 = Math.Max(0.8f, Dist(runX[0], runZ[0], runX[1], runZ[1]) / RunSpeed);
                runEnd = tF + r1 + r2;
                arrive[ra] = Math.Max(arrive[ra], runEnd + Math.Max(0.8f, Dist(runX[1], runZ[1], bx[ra], bz[ra]) / Pace));
                tP = Math.Max(tP, arrive[ra]);
                RunAheadFrom = tF; RunAheadBack = tF + r1;
            }
            // ---- 3 Pick: from arriving (0.8 s apart by slot) for at least 10 s; kids eat every other berry; the lead points a picker on.
            float tP1 = tP + PickSeconds - 2f;
            for (int i = 0; i < n; i++)
            {
                var m = line[i];
                if (i == ra && ra > 0)
                {
                    Put(m, Hold(Move(m, StageBeat.Run, ActivityKind.RunOver, RunAheadBack, runX[0], runZ[0]), HoldOf(m)));
                    Put(m, Hold(Move(m, StageBeat.Run, ActivityKind.RunOver, runEnd, runX[1], runZ[1]), HoldOf(m)));
                }
                Put(m, Hold(Arg(Move(m, StageBeat.WalkOut, ActivityKind.Walk, arrive[i], bx[i], bz[i]), HandOf(m)), HoldOf(m)));
                float cue = 0.8f * inBush[i];
                int eat = m == firstKid ? 2 : m.kid ? 1 : 0; // 2: eats every berry (a first walk), 1: every other
                if (m == lead && nb > 1)
                {
                    int next = (bushOf[i] + 1) % nb;
                    float pt = Math.Max(arrive[i] + 3f, tP);
                    Put(m, Hold(Arg(Cue(Facing(Stay(m, StageBeat.Pick, ActivityKind.Gather, pt), bush[bushOf[i]].x, bush[bushOf[i]].z), cue), eat), StageHold.Basket));
                    Put(m, Hold(Said(Facing(Stay(m, StageBeat.PointBush, ActivityKind.Point, pt + 1f), bush[next].x, bush[next].z), "Not that bush, that bush."), StageHold.Basket));
                    Put(m, Hold(Arg(Facing(Stay(m, StageBeat.Pick, ActivityKind.Gather, tP1), bush[bushOf[i]].x, bush[bushOf[i]].z), eat), StageHold.Basket));
                }
                else
                {
                    var pick = Arg(Cue(Facing(Stay(m, StageBeat.Pick, ActivityKind.Gather, tP1), bush[bushOf[i]].x, bush[bushOf[i]].z), cue), eat);
                    if (m.kid && kidM.Count > 0 && m == kidM[0]) pick = Said(pick, "One for the basket, one for me.");
                    Put(m, Hold(pick, HoldOf(m)));
                }
            }

            // ---- 5 Walk home: the line forms again with the lead at the back, full baskets held in front, the heavy walk.
            var home = new List<MomentMember>(line); home.Remove(lead); home.Add(lead);
            var (ex, ez) = Unit(hx - cx, hz - cz, -dx, -dz);
            float rx0 = cx + ex * 0.3f, rz0 = cz + ez * 0.3f;
            var hxs = new float[n]; var hzs = new float[n];
            float form = 0.8f;
            for (int i = 0; i < n; i++)
            {
                var m = home[i];
                float side = m == firstKid ? 0.06f : (Hash01(seed, m.key, 3802) - 0.5f) * 0.08f;
                int at = m == firstKid ? Math.Max(0, i - 1) : i;
                (hxs[i], hzs[i]) = In(rx0 - ex * LineGap * at - ez * side, rz0 - ez * LineGap * at + ex * side);
                var (x0, z0) = Last(m);
                form = Math.Max(form, Dist(x0, z0, hxs[i], hzs[i]) / Pace);
            }
            float tR = tP1 + Math.Min(4f, form);
            // ---- 6 Tip: each walks on to their place round the stash (or the table) and they tip one after another down the line.
            var (bkx, bkz) = (-ex, -ez); // the side they arrive from
            var fanX = new float[n]; var fanZ = new float[n];
            float tC = tR + 1f;
            for (int i = 0; i < n; i++)
            {
                var m = home[i];
                Put(m, Hold(Arg(Move(m, StageBeat.WalkHome, ActivityKind.Carry, tR, hxs[i], hzs[i]), HandOf(m)), FullOf(m)));
                int row = i / 7, k = i % 7, inRow = Math.Min(7, n - row * 7);
                float a = (k - (inRow - 1) * 0.5f) * (toTable ? 18f : 26f);
                var (ox, oz) = Turn(bkx, bkz, a);
                float r = (toTable ? 0.3f : 0.22f) + 0.13f * row;
                (fanX[i], fanZ[i]) = In(hx + ox * r, hz + oz * r);
                tC = Math.Max(tC, tR + Math.Min(8f, Dist(hxs[i], hzs[i], fanX[i], fanZ[i]) / (Pace * HeavyPace)));
            }
            for (int i = 0; i < n; i++)
            {
                var m = home[i];
                float d = Dist(hxs[i], hzs[i], fanX[i], fanZ[i]);
                float there = Math.Min(tC, tR + Math.Max(1f, d / (Pace * HeavyPace)));
                if (m == firstKid && firstParent != null) there = tC; // with the parent, hand in hand
                if (m == firstParent && firstKid != null) there = tC;
                Put(m, Hold(Arg(Move(m, StageBeat.WalkHome, ActivityKind.Carry, there, fanX[i], fanZ[i]), HandOf(m)), FullOf(m)));
                Put(m, Hold(Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tC), hx, hz), FullOf(m)));
            }
            float tTip = tC + TipGap * (n - 1) + TipLength;
            for (int i = 0; i < n; i++)
            {
                var m = home[i];
                var tip = Arg(Cue(Facing(Stay(m, toTable ? StageBeat.TableSet : StageBeat.Tip, ActivityKind.Watch, tTip), hx, hz), TipGap * i), m == firstKid ? 1 : 0);
                if (m == firstParent && firstKid != null) tip.arg = 2; // the parent drops two berries into the kid's basket first
                Put(m, Hold(tip, FullOf(m)));
            }
            // ---- 7 End beat: both hands up to the glass; two kids put theirs side by side and laugh.
            float tEnd = tTip + 2f;
            var kids2 = new List<MomentMember>();
            foreach (var m in home) if (m.kid && kids2.Count < 2) kids2.Add(m);
            foreach (var m in home)
            {
                if (kids2.Count == 2 && m == kids2[1])
                {
                    var (ax, az) = Last(kids2[0]);
                    Put(m, EndBeat(Glass(Arg(Move(m, StageBeat.KidHands, ActivityKind.Watch, tEnd, ax + Px * 0.045f, az + Pz * 0.045f), 1))));
                }
                else if (kids2.Count == 2 && m == kids2[0]) Put(m, EndBeat(Glass(Said(Arg(Stay(m, StageBeat.KidHands, ActivityKind.Watch, tEnd), 0), "Look at my hands!"))));
                else Put(m, EndBeat(Glass(Stay(m, StageBeat.ShowHands, ActivityKind.Watch, tEnd))));
            }
            centreX = cx; centreZ = cz;
            return true;

            StageHold HoldOf(MomentMember m) => m == firstKid ? StageHold.SmallBasket : StageHold.Basket;
            int HandOf(MomentMember m) => m == firstKid ? 1 : m == firstParent && firstKid != null ? 2 : 0; // 1: the kid's hand up in the parent's, 2: the parent's down
            StageHold FullOf(MomentMember m) => m == firstKid ? StageHold.SmallBasket : StageHold.FullBasket;
        }

        // ======================================================================== B73 the log line

        /// <summary>B73: the log line's own numbers (tests, the view): logs in it, passers, the first log's start, the dropped log (-1 none) and who drops it.</summary>
        public int LogCount, Passers, DropLogAt = -1, DropBy = -1, TwigCount, TwigKids;
        /// <summary>B73: the pile stands by the front glass, so the line runs into the box from its side.</summary>
        public bool ByGlass;
        /// <summary>B73: across the line, on its open side (the kids' line, the zigzag).</summary>
        public float SideX, SideZ;
        public float LineStart, LastLogAt;
        /// <summary>B73: the heap, the pile's foot (the lead), the line's way (unit, from the pile out to the heap).</summary>
        public float HeapX, HeapZ, LineX, LineZ;

        /// <summary>B73: when log <paramref name="k"/> reaches passer <paramref name="j"/> (0 = at the heap; <see cref="Passers"/> = the lead).</summary>
        public float LogReaches(int k, int j) => LineStart + LogEvery * k + HopSeconds * j;

        bool Woodpile(MomentCast c, RitualSpots sp)
        {
            if (!sp.hasPile) return Fail("no woodpile");
            if (!c.hasOfficiant) return Fail("nobody to lead the log line");
            var all = RitualCrowd(c, MaxLogLine + 1, MaxTwigKids, 1.5f);
            int grownN = 0; foreach (var e in all) if (!e.lead && !e.p.kid) grownN++;
            if (grownN < 1) return Fail("too few grown-ups for a log line"); // [est] a small camp: the lead and one passer
            float gather = GatherOf(all, 6f, 1.5f);
            if (gather > MaxGather) return Fail("the lead can't get there in time");
            var (fx, fz) = Unit(sp.pileFX, sp.pileFZ, Gx, Gz);
            // [est] the line stands in front of the pile (its camera side), not beside its roof: clip 721-log-line try 1 hid it behind the roof.
            // A pile by the front glass (less than 0.9 u in from it): the line runs from the pile's side into the box instead, the heap on the
            // open side, so the zigzag shows and nobody stands at the glass (clip try 2).
            float inFromGlass = (sp.pileX - geo.glassX) * -Gx + (sp.pileZ - geo.glassZ) * -Gz;
            bool byGlass = inFromGlass < 0.9f;
            float wx = sp.pileX + Gx * 0.16f, wz = sp.pileZ + Gz * 0.16f;
            ByGlass = byGlass;
            // The line runs across the screen, out from the pile toward the chopping spot's side.
            float ax = Px, az = Pz;
            if (sp.hasChop && (sp.chopX - wx) * ax + (sp.chopZ - wz) * az < 0f) { ax = -ax; az = -az; }
            if (byGlass)
            {
                float side = sp.hasChop && (sp.chopX - sp.pileX) * Px + (sp.chopZ - sp.pileZ) * Pz < 0f ? -1f : 1f;
                wx = sp.pileX + Px * 0.2f * side; wz = sp.pileZ + Pz * 0.2f * side; // beside the pile
                ax = -Gx; az = -Gz; // into the box, away from the glass
                fx = -Gx; fz = -Gz; // and its "front" (the kids' sit, the lead's step) is the open side
            }
            // the line's own "toward the camera": across it, on the camera's side (by the glass: away from the pile)
            float sx2 = -az, sz2 = ax;
            if (byGlass ? sx2 * (wx - sp.pileX) + sz2 * (wz - sp.pileZ) < 0f : sx2 * Gx + sz2 * Gz < 0f) { sx2 = -sx2; sz2 = -sz2; }
            SideX = sx2; SideZ = sz2;
            bool mends = grownN >= 4;
            int passers = Math.Min(MaxLogLine, grownN - (mends ? 1 : 0));
            Passers = passers;
            LineX = ax; LineZ = az;
            float Along(int j) => 0.16f + PassGap * (passers - j); // passer j's distance from the pile (0 = at the heap end)
            var (hx, hz) = In(wx + ax * (Along(0) + 0.2f), wz + az * (Along(0) + 0.2f));
            HeapX = hx; HeapZ = hz;

            var lead = Add(all[0].p, MomentRole.Leader);
            var grownM = new List<MomentMember>(); var kidM = new List<MomentMember>();
            for (int i = 1; i < all.Count; i++)
            {
                var m = Add(all[i].p, all[i].p.kid ? MomentRole.Kid : MomentRole.Bringer);
                if (m.kid) kidM.Add(m); else grownM.Add(m);
            }
            MomentMember mender = mends ? grownM[grownM.Count - 1] : null;
            if (mender != null) { mender.role = MomentRole.Mender; grownM.Remove(mender); }
            int nLogs = Math.Max(4, Math.Min(MaxLogLine, grownN + 1)); // every grown-up brings one, the lead too [est: at least 4]
            LogCount = nLogs;

            // ---- 1 The heap: each grown-up brings a log from the chopping spot and drops it on the heap, one by one.
            var bringers = new List<MomentMember> { lead }; bringers.AddRange(grownM); if (mender != null) bringers.Add(mender);
            for (int i = 0; i < bringers.Count; i++)
            {
                var m = bringers[i];
                var (ox, oz) = Turn(ax, az, 180f + (i - (bringers.Count - 1) * 0.5f) * 22f);
                var (tx, tz) = In(hx + ox * 0.3f + Gx * 0.08f, hz + oz * 0.3f + Gz * 0.08f);
                Put(m, Hold(Facing(Travel(m, GoAt(m.key, 0f, 1.5f), gather, tx, tz, Prop.Log), hx, hz), StageHold.Log));
            }
            foreach (var m in kidM)
            {
                var (tx, tz) = In(hx + sx2 * 0.3f + ax * (Hash01(seed, m.key, 3811) - 0.5f) * 0.3f, hz + sz2 * 0.3f + az * (Hash01(seed, m.key, 3811) - 0.5f) * 0.3f);
                Put(m, Facing(Travel(m, GoAt(m.key, 0f, 1.5f), gather, tx, tz), hx, hz));
            }
            float tD = gather;
            for (int i = 0; i < bringers.Count; i++)
            {
                var m = bringers[i];
                float go = tD + 0.45f * i;
                var (x0, z0) = Last(m);
                var (ux, uz) = Unit(x0 - hx, z0 - hz, Gx, Gz);
                Put(m, Hold(Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, go), hx, hz), StageHold.Log));
                Put(m, Hold(Move(m, StageBeat.Stand, ActivityKind.Walk, go + 0.6f, hx + ux * 0.1f, hz + uz * 0.1f), StageHold.Log));
                Put(m, Arg(Facing(Stay(m, StageBeat.DropLog, ActivityKind.Watch, go + 1.6f), hx, hz), i));
            }
            float tL0 = tD + 0.45f * bringers.Count + 1.6f;
            // ---- 2 The line: a zigzag from the heap to the pile, facing across it (the camera); the lead last, at the pile.
            var to = new List<(MomentMember m, float x, float z)> { (lead, wx + ax * 0.16f + fx * 0.03f, wz + az * 0.16f + fz * 0.03f) };
            for (int j = 0; j < grownM.Count; j++)
            {
                float zig = (j & 1) == 1 ? Zig : 0f;
                var (tx, tz) = In(wx + ax * Along(j) + sx2 * zig, wz + az * Along(j) + sz2 * zig);
                to.Add((grownM[j], tx, tz));
            }
            // the kids' line beside the big one (0.15 off it, toward the camera), into a basket at the pile's foot
            int twigKids = Math.Min(MaxTwigKids, kidM.Count);
            TwigKids = twigKids;
            for (int q = 0; q < kidM.Count; q++)
            {
                float along = q < twigKids ? 0.22f + TwigGap * (twigKids - 1 - q) : 0.3f + 0.1f * q;
                var (tx, tz) = In(wx + ax * along + sx2 * (0.15f + Zig), wz + az * along + sz2 * (0.15f + Zig));
                to.Add((kidM[q], tx, tz));
            }
            // the mender: to the pile's other side, the short ladder on the roof
            if (mender != null) { var (tx, tz) = byGlass ? In(2f * sp.pileX - wx, 2f * sp.pileZ - wz) : In(wx - ax * 0.2f + fx * 0.05f, wz - az * 0.2f + fz * 0.05f); to.Add((mender, tx, tz)); } // by the glass: the pile's other side
            float lineAt = ArriveBy(tL0 + 1.2f, to);
            LineStart = lineAt + 0.3f;
            foreach (var (m, tx, tz) in to) Put(m, Move(m, StageBeat.Stand, ActivityKind.Walk, lineAt, tx, tz));
            // ---- 3-4 Pass it along: a log every 1.2 s, 0.65 s a hop; the lead slides each onto the pile.
            LastLogAt = LogReaches(nLogs - 1, passers) + 0.6f;
            bool drop = sp.dropLog && passers >= 2;
            float tf = 0f;
            if (drop)
            {
                DropBy = (passers - 2) / 2; // the next one down the line hops back
                DropLogAt = Math.Min(3, nLogs - 2);
                tf = LogReaches(DropLogAt, DropBy) + 0.2f;
            }
            for (int j = 0; j < grownM.Count; j++)
            {
                var m = grownM[j];
                if (drop && (j == DropBy || j == DropBy + 1))
                {
                    var pass = Arg(Glass(Stay(m, StageBeat.PassLog, ActivityKind.Watch, j == DropBy ? tf : tf + 0.15f)), j);
                    if (j == 0) pass = Said(pass, "Pass it along!");
                    Put(m, pass);
                    if (j == DropBy) Put(m, Arg(Glass(Stay(m, StageBeat.Fumble, ActivityKind.Watch, tf + 2f)), j));
                    else
                    {
                        var (x0, z0) = Last(m);
                        Put(m, Said(Arg(Glass(Move(m, StageBeat.HopBack, ActivityKind.Watch, tf + 2f, x0 + ax * 0.04f, z0 + az * 0.04f)), j), "Mind your toes."));
                    }
                    Put(m, Arg(Glass(Stay(m, StageBeat.PassLog, ActivityKind.Watch, LastLogAt)), j));
                }
                else
                {
                    var pass = Arg(Glass(Stay(m, StageBeat.PassLog, ActivityKind.Watch, LastLogAt)), j);
                    if (j == 0) pass = Said(pass, "Pass it along!");
                    Put(m, pass);
                }
            }
            Put(lead, Arg(Facing(Stay(lead, StageBeat.PileLog, ActivityKind.Watch, LastLogAt - 0.6f), sp.pileX, sp.pileZ), passers));
            // ---- 8 End beat: the lead sets the last log on top and pats it; everyone claps; the kids sit on the low logs.
            float tClap = LastLogAt + 1.6f, tEnd = tClap + 1.2f, tKids = tClap + 2f;
            Put(lead, Facing(Stay(lead, StageBeat.LastLog, ActivityKind.Watch, tClap), sp.pileX, sp.pileZ));
            Put(lead, EndBeat(Glass(Stay(lead, StageBeat.Clap, ActivityKind.Clap, tEnd))));
            foreach (var m in grownM)
            {
                Put(m, Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tClap), wx, wz));
                Put(m, EndBeat(Facing(Stay(m, StageBeat.Clap, ActivityKind.Clap, tEnd), wx, wz)));
            }
            if (mender != null)
            {
                Put(mender, Facing(Stay(mender, StageBeat.MendRoof, ActivityKind.Hammer, LineStart + 6.5f), sp.pileX, sp.pileZ));
                Put(mender, Facing(Stay(mender, StageBeat.Stand, ActivityKind.Watch, tClap), wx, wz));
                Put(mender, EndBeat(Facing(Stay(mender, StageBeat.Clap, ActivityKind.Clap, tEnd), wx, wz)));
            }
            // ---- 6 The kids' twig line; one hugs the biggest twig and won't pass it; the next laughs.
            TwigCount = Math.Max(3, (int)((LastLogAt - LineStart) / TwigEvery));
            float keepAt = LineStart + 3f;
            for (int q = 0; q < kidM.Count; q++)
            {
                var m = kidM[q];
                if (q < twigKids)
                {
                    if (q == 0 && twigKids >= 1)
                    {
                        Put(m, Arg(Glass(Stay(m, StageBeat.PassTwig, ActivityKind.Watch, keepAt)), q));
                        Put(m, Said(Arg(Glass(Stay(m, StageBeat.KeepTwig, ActivityKind.Watch, keepAt + 1.2f)), q), "That one's mine. I'm keeping it."));
                    }
                    else if (q == 1)
                    {
                        Put(m, Arg(Glass(Stay(m, StageBeat.PassTwig, ActivityKind.Watch, keepAt + 0.6f)), q));
                        Put(m, Arg(Glass(Stay(m, StageBeat.Laugh, ActivityKind.Watch, keepAt + 1.2f)), q));
                    }
                    Put(m, Arg(Glass(Stay(m, StageBeat.PassTwig, ActivityKind.Watch, LastLogAt)), q));
                }
                else Put(m, Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, LastLogAt), wx, wz));
                Put(m, Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tClap), wx, wz));
                var (sx, sz) = In(wx + fx * 0.14f + ax * (0.05f * q - 0.04f), wz + fz * 0.14f + az * (0.05f * q - 0.04f));
                var (kx0, kz0) = Last(m);
                float sat = tClap + Math.Max(0.6f, Dist(kx0, kz0, sx, sz) / Pace);
                Put(m, Move(m, StageBeat.SitLogs, ActivityKind.Walk, sat, sx, sz));
                Put(m, EndBeat(Glass(Stay(m, StageBeat.SitLogs, ActivityKind.SitOut, Math.Max(tKids + 0.6f, sat + 1.5f)))));
            }
            centreX = wx; centreZ = wz;
            return true;
        }

        // ======================================================================== B74 the long table

        /// <summary>B74: who sits where (seat 0 at the head, side 1 the camera's), the late one, the eldest, the board's carriers.</summary>
        public readonly Dictionary<int, (int side, int seat)> Seats = new Dictionary<int, (int side, int seat)>();
        public int LateKey, EldestKey, BoardA, BoardB;
        /// <summary>B74's end beat: from here the bowls are stacked at the head, one per sitter standing up.</summary>
        public float BowlsStackFrom = -1f;
        public float BoardLaidAt = -1f, HeadX, HeadZ, AxisX, AxisZ, NearX, NearZ, TableLen;

        /// <summary>B74: a seat's spot (box x, z) and the way it faces (the table's middle line).</summary>
        public (float x, float z, float fx, float fz) SeatSpot(int side, int seat)
        {
            float along = -TableLen * 0.5f + 0.07f + SeatGap * seat;
            float s = side == 1 ? 1f : -1f;
            float cx = centreX + AxisX * along, cz = centreZ + AxisZ * along;
            return (cx + NearX * BenchOut * s, cz + NearZ * BenchOut * s, cx, cz);
        }

        bool Feast(MomentCast c, RitualSpots sp)
        {
            if (!sp.hasTable) return Fail("no long table");
            if (!c.hasOfficiant) return Fail("nobody to serve the first bowl");
            var all = RitualCrowd(c, MaxFestival, MaxFestival, 1.5f);
            if (all.Count < 3) return Fail("too few people for the feast");
            float gather = GatherOf(all, 6f, 1.5f);
            if (gather > MaxGather) return Fail("the lead can't get there in time");
            centreX = sp.tableX; centreZ = sp.tableZ;
            var (ax, az) = Unit(sp.tableAX, sp.tableAZ, Px, Pz);
            float len = sp.tableLen > 0.1f ? sp.tableLen : 0.66f;
            // the head: the end nearer the fire
            if ((geo.fireX - centreX) * ax + (geo.fireZ - centreZ) * az > 0f) { ax = -ax; az = -az; }
            float nx = -az, nz = ax;
            if (nx * Gx + nz * Gz < 0f) { nx = -nx; nz = -nz; }
            AxisX = ax; AxisZ = az; NearX = nx; NearZ = nz; TableLen = len;
            HeadX = centreX - ax * (len * 0.5f + 0.1f); HeadZ = centreZ - az * (len * 0.5f + 0.1f);
            int perSide = Math.Max(1, (int)((len - 0.1f) / SeatGap) + 1);

            var lead = Add(all[0].p, MomentRole.Leader);
            var grownM = new List<MomentMember>(); var kidM = new List<MomentMember>();
            for (int i = 1; i < all.Count; i++)
            {
                var m = Add(all[i].p, all[i].p.kid ? MomentRole.Kid : MomentRole.Bringer);
                if (m.kid) kidM.Add(m); else grownM.Add(m);
            }
            // the eldest (gets the first bowl), the late one (the latest to set off: budge up), the board's two carriers
            MomentMember eldest = null;
            foreach (var m in grownM) { var p = PersonOf(all, m.key); if (eldest == null || p.age > PersonOf(all, eldest.key).age) eldest = m; }
            eldest ??= lead;
            EldestKey = eldest.key;
            MomentMember late = null; float lateGo = -1f;
            foreach (var m in members) { if (m == lead || m == eldest) continue; float go = GoAt(m.key, 0f, 1.5f); if (go > lateGo) { lateGo = go; late = m; } }
            if (members.Count < 4) late = null;
            LateKey = late?.key ?? 0;
            MomentMember ca = null, cb = null;
            if (sp.addBoard) foreach (var m in grownM) { if (m == eldest || m == late) continue; if (ca == null) ca = m; else if (cb == null) cb = m; }
            if (cb == null) ca = null;
            BoardA = ca?.key ?? 0; BoardB = cb?.key ?? 0;

            // seats: the lead at the head (near side, seat 0), the eldest across from it; then from the head down, kids beside grown-ups
            Seats[lead.key] = (1, 0);
            Seats[eldest.key] = (eldest == lead ? 1 : 0, 0);
            var order = Interleave(grownM, kidM);
            order.Remove(eldest); if (late != null) order.Remove(late);
            int[] next = { eldest == lead ? 0 : 1, 1 };
            var ground = new List<MomentMember>();
            foreach (var m in order)
            {
                int side = next[0] <= next[1] ? 0 : 1;
                if (next[side] >= perSide) side = 1 - side;
                if (next[side] >= perSide) { ground.Add(m); continue; }
                Seats[m.key] = (side, next[side]++);
            }
            if (late != null)
            {
                if (next[1] < perSide) Seats[late.key] = (1, next[1]++);
                else if (next[0] < perSide) Seats[late.key] = (0, next[0]++);
                else { ground.Add(late); late = null; LateKey = 0; }
            }

            // ---- 1 Gather: everyone walks to the table with a bowl or a basket; baskets go on the table.
            var standAt = new Dictionary<int, (float x, float z)>();
            int g = 0;
            foreach (var m in members)
            {
                float x, z;
                if (Seats.TryGetValue(m.key, out var st)) { var s = SeatSpot(st.side, st.seat); float o = st.side == 1 ? 1f : -1f; x = s.x + nx * o * 0.12f; z = s.z + nz * o * 0.12f; }
                else { int q = ground.IndexOf(m); x = centreX + ax * (len * 0.5f + 0.12f) + nx * (0.07f * q - 0.03f); z = centreZ + az * (len * 0.5f + 0.12f) + nz * (0.07f * q - 0.03f); }
                if (m == late) { x = centreX + ax * (len * 0.5f + 0.35f) + nx * 0.3f; z = centreZ + az * (len * 0.5f + 0.35f) + nz * 0.3f; }
                (x, z) = In(x, z);
                standAt[m.key] = (x, z);
                var hold = m.kid || (g++ & 1) == 1 ? StageHold.Basket : StageHold.Bowl;
                Put(m, Hold(Facing(Travel(m, GoAt(m.key, 0f, 1.5f), gather, x, z, hold == StageHold.Basket ? Prop.Basket : Prop.None), centreX, centreZ), hold));
                if (m != late) Put(m, Arg(Hold(Cue(Facing(Stay(m, StageBeat.SetDown, ActivityKind.Watch, gather + 1.6f), centreX, centreZ), 0.5f * Hash01(seed, m.key, 3821)), hold), (int)hold));
            }
            float t = gather + 1.6f;
            // ---- 2 One more board: two carry it to the table's foot, lay it on, one taps it on.
            float footX = centreX + ax * (len * 0.5f), footZ = centreZ + az * (len * 0.5f);
            if (ca != null)
            {
                float px0 = footX + ax * 0.5f, pz0 = footZ + az * 0.5f;
                float tPick = ArriveBy(t + 1.2f, new[] { (ca, px0 + nx * 0.06f, pz0 + nz * 0.06f), (cb, px0 - nx * 0.06f, pz0 - nz * 0.06f) });
                float tCarry = tPick + 1.8f, tLay = tCarry + 0.7f, tTap = tLay + 1.5f;
                foreach (var (m, o) in new[] { (ca, 1f), (cb, -1f) })
                {
                    Put(m, Move(m, StageBeat.Stand, ActivityKind.Walk, tPick, px0 + nx * 0.06f * o, pz0 + nz * 0.06f * o));
                    Put(m, Hold(Move(m, StageBeat.CarryBoard, ActivityKind.Carry, tCarry, footX + ax * 0.08f + nx * 0.06f * o, footZ + az * 0.08f + nz * 0.06f * o), StageHold.Board));
                    Put(m, Hold(Facing(Stay(m, StageBeat.CarryBoard, ActivityKind.Watch, tLay), footX, footZ), StageHold.Board));
                    var tap = Arg(Facing(Stay(m, StageBeat.TapBoard, ActivityKind.Watch, tTap), footX, footZ), m == ca ? 1 : 0);
                    if (m == ca) tap = Said(tap, "One more seat this year.");
                    Put(m, tap);
                }
                BoardLaidAt = tLay;
                t = tTap;
            }
            foreach (var m in members) if (End(m) < t) Put(m, Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, t), footX, footZ));
            // ---- 3 Sit: from the head down, both sides, 0.1 s apart; more than seats: the last kids on the ground at the foot.
            var sitTo = new List<(MomentMember m, float x, float z)>();
            foreach (var m in members) if (m != late && Seats.TryGetValue(m.key, out var stq)) { var s = SeatSpot(stq.side, stq.seat); sitTo.Add((m, s.x, s.z)); }
            float tSit = ArriveBy(t + 1.2f, sitTo) + 0.8f;
            foreach (var m in members)
            {
                if (m == late) { Put(m, Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tSit), centreX, centreZ)); continue; }
                if (Seats.TryGetValue(m.key, out var st))
                {
                    var s = SeatSpot(st.side, st.seat);
                    var (x0, z0) = Last(m);
                    Put(m, Move(m, StageBeat.Stand, ActivityKind.Walk, Math.Min(tSit - 0.2f, End(m) + Math.Max(0.8f, Dist(x0, z0, s.x, s.z) / Pace) + 0.08f * st.seat), s.x, s.z));
                    Put(m, Arg(Facing(Stay(m, StageBeat.TableSit, ActivityKind.SitOut, tSit), s.fx, s.fz), st.seat));
                }
                else Put(m, Arg(Facing(Stay(m, StageBeat.SitLogs, ActivityKind.SitOut, tSit), centreX, centreZ), -1));
            }
            // ---- 4 The first bowl: the lead ladles at the head and gives it to the eldest, who lifts it.
            float tLadle = tSit + 0.6f + 1.2f, tGive = tLadle + 0.8f, tFirst = tGive + 0.7f;
            var es = SeatSpot(Seats[eldest.key].side, 0);
            if (eldest != lead)
            {
                var (hx, hz) = In(HeadX, HeadZ);
                Put(lead, Move(lead, StageBeat.Stand, ActivityKind.Walk, tSit + 0.6f, hx, hz));
                Put(lead, Hold(Facing(Stay(lead, StageBeat.Ladle, ActivityKind.Watch, tLadle), centreX, centreZ), StageHold.Bowl));
                Put(lead, Hold(Facing(Stay(lead, StageBeat.Give, ActivityKind.Give, tGive), es.x, es.z), StageHold.Bowl));
                var ls = SeatSpot(1, 0);
                Put(lead, Move(lead, StageBeat.Stand, ActivityKind.Walk, tFirst, ls.x, ls.z));
                Put(eldest, Arg(Facing(Stay(eldest, StageBeat.TableSit, ActivityKind.SitOut, tGive - 0.2f), es.fx, es.fz), 0));
                Put(eldest, Hold(Arg(Facing(Stay(eldest, StageBeat.BowlsUp, ActivityKind.SitOut, tFirst), es.fx, es.fz), 1), StageHold.Bowl));
            }
            // ---- 5 Down the sides: bowls go hand to hand down both benches.
            float tPass = tFirst, tSides = tPass + BowlHop * perSide + 0.3f;
            foreach (var m in members)
            {
                if (m == late || m == eldest && eldest != lead) continue;
                if (!Seats.TryGetValue(m.key, out var st)) { Put(m, Facing(Stay(m, StageBeat.SitLogs, ActivityKind.SitOut, tSides), centreX, centreZ)); continue; }
                var s = SeatSpot(st.side, st.seat);
                if (End(m) < tPass) Put(m, Arg(Facing(Stay(m, StageBeat.TableSit, ActivityKind.SitOut, tPass), s.fx, s.fz), st.seat));
                Put(m, Hold(Arg(Cue(Facing(Stay(m, StageBeat.PassBowl, ActivityKind.SitOut, tSides), s.fx, s.fz), BowlHop * st.seat), st.seat), StageHold.Bowl));
            }
            if (eldest != lead) Put(eldest, Hold(Arg(Facing(Stay(eldest, StageBeat.PassBowl, ActivityKind.SitOut, tSides), es.fx, es.fz), 0), StageHold.Bowl));
            // ---- 6 Budge up: the late one comes to the bench; its sitters shuffle along in a wave from the head.
            float tBudge = tSides + 2f;
            foreach (var m in members)
            {
                if (m == late)
                {
                    var st = Seats[m.key]; var s = SeatSpot(st.side, st.seat);
                    var (lx0, lz0) = Last(m);
                    float walkIn = Math.Max(1.2f, Dist(lx0, lz0, s.x, s.z) / Pace);
                    Put(m, Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tSides + 1.2f - walkIn), centreX, centreZ));
                    Put(m, Move(m, StageBeat.Stand, ActivityKind.Walk, tSides + 1.2f, s.x, s.z));
                    Put(m, Hold(Arg(Facing(Stay(m, StageBeat.TableSit, ActivityKind.SitOut, tBudge), s.fx, s.fz), st.seat), StageHold.Bowl));
                    continue;
                }
                if (Seats.TryGetValue(m.key, out var st2))
                {
                    var s = SeatSpot(st2.side, st2.seat);
                    bool budges = late != null && st2.side == Seats[late.key].side;
                    var b = Hold(Arg(Cue(Facing(Stay(m, budges ? StageBeat.Budge : StageBeat.TableSit, ActivityKind.SitOut, tBudge), s.fx, s.fz), 0.1f * st2.seat), st2.seat), StageHold.Bowl);
                    if (budges && st2.seat == 1) b = Said(b, "Budge up, there's room.");
                    Put(m, b);
                }
                else Put(m, Facing(Stay(m, StageBeat.SitLogs, ActivityKind.SitOut, tBudge), centreX, centreZ));
            }
            // ---- 7 Eat: out of step; two kids crawl under the table end to end.
            float tEat = tBudge + EatSeconds;
            var under = new List<MomentMember>();
            foreach (var m in kidM) if (under.Count < 2 && m != late) under.Add(m);
            if (under.Count < 2) under.Clear();
            bool said = false;
            foreach (var m in members)
            {
                int ui = under.IndexOf(m);
                if (ui >= 0)
                {
                    float o = ui == 0 ? 0.02f : -0.02f;
                    var (ix, iz) = In(footX + ax * 0.06f + nx * o, footZ + az * 0.06f + nz * o);
                    var (ox, oz) = In(centreX - ax * (len * 0.5f + 0.06f) + nx * o, centreZ - az * (len * 0.5f + 0.06f) + nz * o);
                    var (ux0, uz0) = Last(m);
                    float tIn = tBudge + Math.Max(1.2f, Dist(ux0, uz0, ix, iz) / Pace), tOut = tEat - 1.6f;
                    Put(m, Move(m, StageBeat.Stand, ActivityKind.Walk, tIn, ix, iz));
                    Put(m, Arg(Move(m, StageBeat.UnderTable, ActivityKind.Watch, tOut, centreX + ax * (ui == 0 ? 0.05f : -0.05f) + nx * o, centreZ + az * (ui == 0 ? 0.05f : -0.05f) + nz * o), ui));
                    Put(m, Arg(Move(m, StageBeat.UnderTable, ActivityKind.Watch, tEat - 0.6f, ox, oz), ui));
                    var st = Seats.TryGetValue(m.key, out var su) ? SeatSpot(su.side, su.seat) : (ox, oz, centreX, centreZ);
                    Put(m, Move(m, StageBeat.Stand, ActivityKind.Walk, tEat, st.Item1, st.Item2));
                    continue;
                }
                if (Seats.TryGetValue(m.key, out var st3))
                {
                    var s = SeatSpot(st3.side, st3.seat);
                    var e = Hold(Arg(Cue(Facing(Stay(m, StageBeat.TableEat, ActivityKind.SitOut, tEat), s.fx, s.fz), 0.7f * Hash01(seed, m.key, 3822)), st3.seat), StageHold.Bowl);
                    if (!said && !m.kid && m != lead) { e = Said(e, "Who made this? It's good."); said = true; }
                    Put(m, e);
                }
                else Put(m, Hold(Facing(Stay(m, StageBeat.SitLogs, ActivityKind.SitOut, tEat), centreX, centreZ), StageHold.Bowl));
            }
            // ---- 8 Bowls up (in step on purpose), 9 the song down the benches, 10 they stand from the foot end; the eldest naps.
            float tUp = tEat + 1.5f, tSong = tUp + SongSeconds, tEnd = tSong + 3f;
            BowlsStackFrom = tSong; // 10: as they stand from the foot end, the bowls go onto a stack at the head (B30.e's stack, drawn by the view)
            foreach (var m in members)
            {
                bool seated = Seats.TryGetValue(m.key, out var st4);
                var s = seated ? SeatSpot(st4.side, st4.seat) : (Last(m).x, Last(m).z, centreX, centreZ);
                int seat = seated ? st4.seat : perSide;
                Put(m, Hold(Arg(Facing(Stay(m, StageBeat.BowlsUp, ActivityKind.SitOut, tUp), s.Item3, s.Item4), 0), StageHold.Bowl));
                Put(m, Arg(Cue(Facing(Stay(m, StageBeat.SongSway, ActivityKind.SitOut, tSong), s.Item3, s.Item4), 0.15f * seat), seat));
                if (m == eldest && eldest != lead && seated) { Put(m, EndBeat(Arg(Facing(Stay(m, StageBeat.BenchNap, ActivityKind.Nap, tEnd), s.Item3, s.Item4), st4.side))); continue; }
                float up = tSong + 0.12f * Math.Max(0, perSide - 1 - seat); // from the foot end
                var (ox, oz) = seated ? (st4.side == 1 ? (nx, nz) : (-nx, -nz)) : (ax, az);
                Put(m, Arg(Facing(Stay(m, seated ? StageBeat.TableSit : StageBeat.SitLogs, ActivityKind.SitOut, up), s.Item3, s.Item4), seat));
                Put(m, Move(m, StageBeat.Stand, ActivityKind.Walk, up + 0.6f, s.Item1 + ox * 0.12f, s.Item2 + oz * 0.12f));
                Put(m, EndBeat(Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tEnd), centreX, centreZ)));
            }
            return true;
        }

        static MomentPerson PersonOf(List<(MomentPerson p, bool lead)> all, int key)
        {
            foreach (var e in all) if (e.p.key == key) return e.p;
            return default;
        }

        // ======================================================================== B75.a the offering

        /// <summary>B75.a: when the basket is set down at the tree (the kids' small basket beside it), plan seconds.</summary>
        public float OfferingSetAt = -1f, StillAt = -1f;
        public float OfferingX, OfferingZ;

        bool Offering(MomentCast c, RitualSpots sp)
        {
            if (!c.hasOfficiant) return Fail("nobody to carry the offering");
            var all = RitualCrowd(c, sp.town ? MaxFestival : MaxOfferingGrown, MaxFestival, 1.2f);
            // the small basket's two kids: the year's kid first, then the nearest; the town's other kids walk behind, elsewhere they stay
            var carriers = new List<int>();
            foreach (var e in all) if (e.p.kid && e.p.key == sp.ritualKid) carriers.Add(e.p.key);
            foreach (var e in all) if (e.p.kid && carriers.Count < MaxOfferingKids && !carriers.Contains(e.p.key)) carriers.Add(e.p.key);
            if (!sp.town) all.RemoveAll(e => e.p.kid && !carriers.Contains(e.p.key));
            float gather = GatherOf(all, 5f, 1.2f);
            if (gather > MaxGather) return Fail("the lead can't get there in time");
            float fx = geo.fireX, fz = geo.fireZ;
            float sx = sp.hasStash ? sp.stashX : sp.hasStack ? sp.stackX : fx + Gx * 0.3f, sz = sp.hasStash ? sp.stashZ : sp.hasStack ? sp.stackZ : fz + Gz * 0.3f;
            (sx, sz) = In(sx, sz);
            bool tree = sp.hasTree;
            float vx = tree ? sp.spotX : sx + Gx * 0.25f, vz = tree ? sp.spotZ : sz + Gz * 0.25f;
            (vx, vz) = In(vx, vz, 0.1f);
            var (dx, dz) = Unit(vx - sx, vz - sz, Gx, Gz);
            float px = -dz, pz = dx;
            float far = Dist(sx, sz, vx, vz);
            float most = MaxCarry * Pace * CarefulPace;
            if (tree && far > most) { sx = vx - dx * most; sz = vz - dz * most; far = most; } // a far tree: they start part way
            OfferingX = vx; OfferingZ = vz;

            var lead = Add(all[0].p, MomentRole.Leader);
            var kidM = new List<MomentMember>(); var grownM = new List<MomentMember>();
            foreach (int key in carriers) kidM.Add(Add(all.Find(e => e.p.key == key).p, MomentRole.Kid));
            for (int i = 1; i < all.Count; i++)
                if (!carriers.Contains(all[i].p.key)) grownM.Add(Add(all[i].p, all[i].p.kid ? MomentRole.Kid : MomentRole.Bringer)); // the walkers behind, in pairs

            // The formation: the lead at the start, the two kids 0.2 behind, the rest in pairs behind them.
            var slot = new Dictionary<MomentMember, (float x, float z)>();
            slot[lead] = (sx, sz);
            for (int i = 0; i < kidM.Count; i++) slot[kidM[i]] = (sx - dx * KidGap + px * (i == 0 ? 0.035f : -0.035f), sz - dz * KidGap + pz * (i == 0 ? 0.035f : -0.035f));
            for (int i = 0; i < grownM.Count; i++)
            {
                int row = i / 2; float o = (i & 1) == 0 ? 0.055f : -0.055f;
                float back = KidGap + PairGap * (row + 1);
                slot[grownM[i]] = (sx - dx * back + px * o, sz - dz * back + pz * o);
            }
            foreach (var m in members)
            {
                var (x, z) = In(slot[m].x, slot[m].z);
                slot[m] = (x, z);
                Put(m, Facing(Travel(m, GoAt(m.key, 0f, 1.2f), gather, x, z), vx, vz));
            }
            // ---- 1-2 Lift: the lead squats and lifts the best basket in both arms; the kids lift the small one.
            float tLift = gather + 1.2f;
            Put(lead, Hold(Said(Facing(Stay(lead, StageBeat.LiftOffering, ActivityKind.Watch, tLift), vx, vz), "Gently. It's for {keeper}."), StageHold.Offering));
            foreach (var k in kidM) Put(k, Hold(Arg(Facing(Stay(k, StageBeat.KidCarry, ActivityKind.Watch, tLift), vx, vz), kidM.IndexOf(k)), StageHold.SmallBasket));
            foreach (var m in grownM) Put(m, Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tLift), vx, vz));
            // ---- 3 The walk: the careful walk, the kids behind with the small basket between them, the rest in pairs.
            float walk = tree ? Math.Max(1.5f, far / (Pace * CarefulPace)) : 1.2f;
            float tArrive = tLift + walk;
            float shiftX = tree ? vx - sx : dx * 0.18f, shiftZ = tree ? vz - sz : dz * 0.18f;
            foreach (var m in members)
            {
                var (x, z) = In(slot[m].x + shiftX, slot[m].z + shiftZ);
                bool carries = kidM.Contains(m);
                var hold = m == lead ? StageHold.Offering : carries ? StageHold.SmallBasket : StageHold.None;
                var beat = m == lead ? StageBeat.CarryCareful : carries ? StageBeat.KidCarry : StageBeat.WalkOut;
                Put(m, Hold(Arg(Move(m, beat, ActivityKind.Walk, tArrive, x, z), carries ? kidM.IndexOf(m) : Math.Max(0, grownM.IndexOf(m)) & 1), hold));
            }
            // ---- 4 Set down: the lead kneels and tips it into the basket at the tree (else sets it down); the kids set theirs beside it.
            float tSet = tArrive + 3f;
            OfferingSetAt = tArrive + 2f;
            Put(lead, Hold(Arg(Facing(Stay(lead, StageBeat.KneelSet, ActivityKind.Watch, tSet), vx + dx, vz + dz), tree && sp.keeperTree ? 1 : 0), StageHold.Offering));
            for (int i = 0; i < kidM.Count; i++)
            {
                var k = kidM[i];
                float o = i == 0 ? 0.075f : 0.115f;
                var (x, z) = In(vx + px * o - dx * 0.04f, vz + pz * o - dz * 0.04f);
                Put(k, Hold(Arg(Move(k, StageBeat.KidCarry, ActivityKind.Walk, tArrive + 0.8f, x, z), i), StageHold.SmallBasket));
                Put(k, Hold(Arg(Facing(Stay(k, StageBeat.KneelSet, ActivityKind.Watch, tSet), vx + dx, vz + dz), 2 + i), StageHold.SmallBasket));
            }
            foreach (var m in grownM) Put(m, Facing(Stay(m, StageBeat.Stand, ActivityKind.Watch, tSet), vx, vz));
            // ---- 5 To the Keeper: everyone looks up at the glass, then bows in a wave, nearest the basket first, the lead last.
            float tLook = tSet + 1f, tBow = tLook + 2f;
            var bowers = new List<MomentMember>(members); bowers.Remove(lead);
            bowers.Sort((a, b) => { var (ax0, az0) = Last(a); var (bx0, bz0) = Last(b); float da = Dist(ax0, az0, vx, vz), db = Dist(bx0, bz0, vx, vz); return da != db ? da.CompareTo(db) : a.key.CompareTo(b.key); });
            bowers.Add(lead);
            for (int i = 0; i < bowers.Count; i++)
            {
                var m = bowers[i];
                Put(m, Glass(Stay(m, StageBeat.LookUp, ActivityKind.LookUp, tLook)));
                Put(m, Cue(Glass(Stay(m, StageBeat.Bow, ActivityKind.Watch, tBow + BowGap * bowers.Count)), BowGap * i));
            }
            // ---- 6 Still: two seconds, quiet (a leaf falls from the crown).
            float tStill = tBow + BowGap * bowers.Count + 2f;
            StillAt = tStill - 2f; // one leaf falls from the crown (drawn by the view)
            foreach (var m in members) Put(m, Facing(Stay(m, StageBeat.Still, ActivityKind.Watch, tStill), vx, vz));
            // ---- 7 Walk back: they turn back; one kid looks back, a grown-up turns its head forward again. Released 2 s in.
            float tBack = tStill + 2f;
            MomentMember looker = kidM.Count > 0 ? kidM[kidM.Count - 1] : null, turner = grownM.Find(g => !g.kid) ?? lead;
            foreach (var m in members)
            {
                var (x0, z0) = Last(m);
                if (m == looker && turner != null)
                {
                    Put(m, Arg(Facing(Stay(m, StageBeat.LookBack, ActivityKind.Watch, tStill + 1.4f), vx, vz), 0));
                    Put(m, EndBeat(Move(m, StageBeat.WalkOut, ActivityKind.Walk, tBack + 0.6f, x0 - dx * 0.25f, z0 - dz * 0.25f)));
                    continue;
                }
                if (m == turner && looker != null)
                {
                    var (kx, kz) = Last(looker);
                    Put(m, Move(m, StageBeat.WalkOut, ActivityKind.Walk, tStill + 0.6f, kx - dx * 0.05f + px * 0.06f, kz - dz * 0.05f + pz * 0.06f));
                    Put(m, Said(Facing(Stay(m, StageBeat.TurnHead, ActivityKind.Watch, tStill + 1.4f), kx, kz), "Don't look back, it's rude."));
                    var (tx, tz) = Last(m);
                    Put(m, EndBeat(Move(m, StageBeat.WalkOut, ActivityKind.Walk, tBack + 0.6f, tx - dx * 0.25f, tz - dz * 0.25f)));
                    continue;
                }
                Put(m, EndBeat(Hold(Move(m, StageBeat.WalkOut, ActivityKind.Walk, tBack, x0 - dx * 0.5f, z0 - dz * 0.5f), StageHold.None)));
            }
            centreX = vx; centreZ = vz;
            return true;
        }
    }
}
