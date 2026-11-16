// #708 (B70, docs/design/animation-specs.md "### #708"): the pond's fish as the Keeper sees them, the logic only. A loose school of shadows
// drifting on the water (B70.b) and, where the pond meets a pane, up to two fish seen through the glass (B70.c), each fish drawn once.
// Pure (Mathf, Vector2/3 and delegates for the pond): FishView owns one and draws it; the EditMode tests step it on a made-up pond.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>One side of the box where the pond can stand against the glass (B70.c). Front = −z (the default camera's pane).</summary>
    public enum GlassSide { Left = 0, Right = 1, Front = 2, Back = 3 }

    /// <summary>
    /// The pond's fish: shadows from above (B70.b) and fish at the glass (B70.c), as a pure function of a step (the owner's dt) and what the
    /// pond is. Every fish is one of <see cref="FishSchool.Fish"/> and is drawn once: as a shadow, or on a wall, except during the 0.3 s
    /// hand-off between the two. Numbers are the spec's; [est] ones are marked there.
    /// </summary>
    public sealed class FishSchool
    {
        // ---------------------------------------------------------------- the spec's numbers
        /// <summary>B70.b: the school's drift (u/s), how far the fish keep from the bank, how fast the school turns (all within 0.3 s).</summary>
        public const float DriftMin = 0.03f, DriftMax = 0.05f, BankGap = 0.06f, TurnSeconds = 0.3f;
        /// <summary>B70.b: a tail wag of ±12° at 2.5 Hz.</summary>
        public const float TailDeg = 12f, TailHz = 2.5f;
        /// <summary>B70.b: the dart: 0.2 u in 0.25 s, away from a ring within 0.3 u; regroup after 2 s.</summary>
        public const float StartleRadius = 0.3f, DartDistance = 0.2f, DartSeconds = 0.25f, RegroupSeconds = 2f;
        /// <summary>B16.e: before a bite, one fish darts to the hook in 0.4 s.</summary>
        public const float HookSeconds = 0.4f;
        /// <summary>B70.a: a fish this near a wall goes onto it; at most this many on the walls at once.</summary>
        public const float WallReach = 0.15f;
        public const int MaxOnWalls = 2;
        /// <summary>B70.c: the hand-off between a shadow and a side fish, a wall's least depth for a side fish, how far under the water line and above
        /// the soil cut it keeps, its swim speed, tail (±15° at 3 Hz) and bob (±0.005 u at 0.5 Hz), its depth band, its pause and dart, its turn.</summary>
        public const float HandOff = 0.3f, MinWallDepth = 0.06f, UnderLine = 0.015f, OverCut = 0.01f;
        public const float SideSpeedMin = 0.05f, SideSpeedMax = 0.08f, SideTailDeg = 15f, SideTailHz = 3f, BobAmp = 0.005f, BobHz = 0.5f;
        public const float DepthLow = 0.3f, DepthHigh = 0.8f, PauseMin = 6f, PauseMax = 12f, Hang = 1f, FinDeg = 10f, FinHz = 6f, SideDart = 0.1f, Flip = 0.4f;
        /// <summary>How long a fish stays on the wall before it goes back up, and how long a shadow waits before it may go down again [est].</summary>
        public const float WallStayMin = 10f, WallStayMax = 20f, WallRest = 12f;
        /// <summary>The side fish's half-length (body and tail at the drawn scale) and its top and bottom off its centre line (the dorsal tick and the belly).</summary>
        public const float SideHalfLength = 0.04f, SideTop = 0.0155f, SideBottom = 0.0085f;

        public enum Mode { Shadow, Down, Wall, Up }

        public sealed class Fish
        {
            public int index;
            public Mode mode;
            /// <summary>The shadow's spot (box x, z) and heading (radians, atan2(x, z): 0 = +z), its own speed factor.</summary>
            public Vector2 at; public float heading, speedK = 1f;
            /// <summary>The dart: from, to, the clock from its start (−1 = none), and the regroup hold after it.</summary>
            public Vector2 dartFrom, dartTo; public float dartT = -1f, dartLen = DartSeconds, hold;
            /// <summary>Gone to a hook (B16.e) until this clock.</summary>
            public float hookUntil = -1f; public Vector2 hook;
            /// <summary>Drawn to a lure (a kid's hand at the edge, B39.c).</summary>
            public int lure = -1;
            /// <summary>The hand-off's clock (0..HandOff) and the shadow's and side fish's scale now.</summary>
            public float handT, shadowScale = 1f, sideScale;
            /// <summary>On a wall: which side, where along it, the depth (0 top .. 1 bottom), swim direction (±1), the flip's clock (−1 none), and its timers.</summary>
            public GlassSide side; public float along, depth01, dir = 1f, flipT = -1f, flipFrom = 1f, pauseIn, hangT = -1f, sideDartT = -1f, sideDartFrom, sideDartTo, sideDepthTo, stay, rest;
            public float phase;
            /// <summary>The tail's angle (degrees) and, on a wall, the fin's.</summary>
            public float tail, fin;
            /// <summary>On a wall: the drawn x scale (1 → 0 → −1 over the flip: the cartoon turn) and the bob now.</summary>
            public float flipScale = 1f, bob;
            public bool ShadowDrawn => mode == Mode.Shadow || mode == Mode.Down || mode == Mode.Up;
            public bool SideDrawn => mode == Mode.Down || mode == Mode.Wall || mode == Mode.Up;
        }

        // ---------------------------------------------------------------- the pond, as the owner gives it
        /// <summary>A point is pond water (box x, z).</summary>
        public Func<float, float, bool> Water = (x, z) => false;
        /// <summary>The wall's depth (water line to the drawn foot) at a point along a side; 0 = no wall there.</summary>
        public Func<GlassSide, float, float> WallDepth = (s, a) => 0f;
        /// <summary>The sides a side fish may use (the ones the Keeper can look through).</summary>
        public Func<GlassSide, bool> SideSeen = s => s != GlassSide.Back;
        /// <summary>The box's half sizes: x from −HX to HX, z from ZMin to ZMax (the land's edge, where the walls stand).</summary>
        public float HX = 3f, ZMin = -2.2f, ZMax = 2.2f;
        public float waterY;
        /// <summary>Winter: everyone at half speed, the side fish low (70–90 %).</summary>
        public bool winter;

        public readonly List<Fish> fish = new List<Fish>();
        /// <summary>Lures this frame (box x, z): each pulls the nearest free shadow to it (B39.c: a shadow drifts close to the kid's hand).</summary>
        public readonly List<Vector2> lures = new List<Vector2>();
        Vector2 centre, target;
        float heading, retarget, clock;
        int seed;
        System.Random rng;

        /// <summary>Stats for the tests: fish drawn as shadows / on walls now, darts so far, hand-offs so far, a fish drawn twice outside a hand-off.</summary>
        public int ShadowsNow { get; private set; }
        public int SidesNow { get; private set; }
        public int Darts { get; private set; }
        public int HandOffs { get; private set; }
        public int DrawnTwice { get; private set; }
        public int HookDarts { get; private set; }
        public Vector2 Centre => centre;

        public FishSchool(int seed) { this.seed = seed; rng = new System.Random(seed); }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>A point the shadows may be on: water, and water 0.06 u out every way (they keep off the bank).</summary>
        public bool Inner(Vector2 p)
            => Water(p.x, p.y) && Water(p.x + BankGap, p.y) && Water(p.x - BankGap, p.y) && Water(p.x, p.y + BankGap) && Water(p.x, p.y - BankGap);

        /// <summary>The count to draw (the sim's stock, capped): pooled fish come and go from the end of the list.</summary>
        public void SetCount(int n, Vector2 home, IList<Vector2> inner)
        {
            n = Math.Max(0, n);
            if (fish.Count == 0 && n > 0) { centre = target = home; heading = 0f; retarget = 0f; }
            while (fish.Count > n) fish.RemoveAt(fish.Count - 1);
            while (fish.Count < n)
            {
                var f = new Fish { index = fish.Count, speedK = R(DriftMin, DriftMax) / ((DriftMin + DriftMax) * 0.5f), phase = R(0f, 10f) };
                f.at = centre + Slot(f.index, heading);
                if (!Inner(f.at) && inner != null && inner.Count > 0) f.at = inner[rng.Next(inner.Count)];
                f.heading = heading;
                f.rest = R(0f, WallRest);
                fish.Add(f);
            }
        }

        /// <summary>A fish's place in the loose school (its offset from the centre, turned with the school).</summary>
        static Vector2 Slot(int i, float heading)
        {
            // the founding school's layout (LifeLooks.Group(Fish)) a little looser, along the heading
            Vector2[] o = { new(-0.08f, 0f), new(0f, 0.06f), new(0.07f, -0.04f), new(0.12f, 0.04f), new(-0.02f, -0.07f), new(-0.13f, 0.05f), new(0.05f, 0.1f), new(-0.06f, -0.12f) };
            var v = o[i % o.Length] * 1.3f;
            float c = Mathf.Cos(-heading), s = Mathf.Sin(-heading);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ---------------------------------------------------------------- answers
        /// <summary>B70.b/B70.c: a ring or droplets at (x, z): every shadow within 0.3 u darts 0.2 u away in 0.25 s; a side fish under it darts along
        /// the wall and down to 80 %. Rings at a hook a fish is on are the bite's own (they don't scare it). Returns how many fish moved.</summary>
        public int Startle(Vector2 p, float radius = StartleRadius)
        {
            int n = 0;
            foreach (var f in fish)
            {
                if (f.hookUntil > clock && (p - f.hook).sqrMagnitude < 0.06f * 0.06f) continue; // the nibble's ring: the fish is the one biting
                if (f.mode == Mode.Shadow)
                {
                    if ((f.at - p).sqrMagnitude > radius * radius) continue;
                    Dart(f, f.at - p);
                    n++;
                }
                else if (f.mode == Mode.Wall)
                {
                    if ((WallPoint(f) - p).sqrMagnitude > radius * radius) continue;
                    f.sideDartT = 0f; f.sideDartFrom = f.along; f.sideDartTo = ClampAlong(f, f.along + f.dir * SideDart * 1.5f);
                    f.sideDepthTo = winter ? 0.9f : DepthHigh; f.hangT = -1f;
                    n++;
                }
            }
            Darts += n;
            return n;
        }

        /// <summary>A person standing in the water at (x, z): the shadows near them dart off.</summary>
        public int PersonIn(Vector2 p) => Startle(p, 0.15f);

        /// <summary>B16.e: before a bite, the shadow nearest the hook darts to it in 0.4 s and stays there for <paramref name="stay"/> s (the nibble).
        /// False when no shadow is free or one is already on this hook.</summary>
        public bool ToHook(Vector2 hook, float stay)
        {
            Fish best = null; float bd = float.MaxValue;
            foreach (var f in fish)
            {
                if (f.hookUntil > clock && (f.hook - hook).sqrMagnitude < 0.05f * 0.05f) return false;
                if (f.mode != Mode.Shadow || f.hookUntil > clock) continue;
                float d = (f.at - hook).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            if (best == null) return false;
            var to = hook - (hook - best.at).normalized * 0.02f; // its head at the hook
            best.dartFrom = best.at; best.dartTo = to; best.dartT = 0f; best.dartLen = HookSeconds; best.hold = 0f;
            best.hook = hook; best.hookUntil = clock + HookSeconds + Mathf.Max(0.2f, stay);
            HookDarts++;
            return true;
        }

        void Dart(Fish f, Vector2 away)
        {
            if (away.sqrMagnitude < 1e-6f) away = new Vector2(Mathf.Sin(f.heading), Mathf.Cos(f.heading));
            away.Normalize();
            var to = f.at;
            foreach (float turn in new[] { 0f, 0.5f, -0.5f, 1f, -1f, 1.5f, -1.5f })
            {
                float c = Mathf.Cos(turn), s = Mathf.Sin(turn);
                var d = new Vector2(away.x * c - away.y * s, away.x * s + away.y * c);
                for (float k = 1f; k >= 0.4f; k -= 0.3f)
                    if (Water(f.at.x + d.x * DartDistance * k, f.at.y + d.y * DartDistance * k)) { to = f.at + d * DartDistance * k; goto found; }
            }
            found:
            f.dartFrom = f.at; f.dartTo = to; f.dartT = 0f; f.dartLen = DartSeconds; f.hold = RegroupSeconds; f.hookUntil = -1f;
        }

        // ---------------------------------------------------------------- the walls
        /// <summary>Where a wall's outward face is, on its axis (the land's edge).</summary>
        public float SidePlane(GlassSide s) => s switch { GlassSide.Left => -HX, GlassSide.Right => HX, GlassSide.Front => ZMin, _ => ZMax };
        public static bool AlongX(GlassSide s) => s is GlassSide.Front or GlassSide.Back;

        /// <summary>The (x, z) of a point on a wall.</summary>
        public Vector2 OnSide(GlassSide s, float along) => AlongX(s) ? new Vector2(along, SidePlane(s)) : new Vector2(SidePlane(s), along);
        Vector2 WallPoint(Fish f) => OnSide(f.side, f.along);

        /// <summary>A side fish fits here: the wall is at least 0.06 u deep under its whole length.</summary>
        public bool WallFits(GlassSide s, float along)
        {
            for (int k = -2; k <= 2; k++) if (WallDepth(s, along + k * SideHalfLength * 0.5f) < MinWallDepth) return false;
            return true;
        }

        float ClampAlong(Fish f, float to)
        {
            float step = 0.005f * Mathf.Sign(to - f.along), a = f.along;
            if (Mathf.Abs(to - a) < 1e-5f) return a;
            while (Mathf.Abs(to - a) > Mathf.Abs(step)) { if (!WallFits(f.side, a + step)) return a; a += step; }
            return WallFits(f.side, to) ? to : a;
        }

        /// <summary>The y of a side fish's centre line at depth <paramref name="d01"/> of the wall there, kept 0.015 u under the water line and 0.01 u
        /// above the soil cut (its fin and belly included).</summary>
        public float SideY(GlassSide s, float along, float d01)
        {
            float depth = WallDepth(s, along);
            float top = waterY - UnderLine - SideTop - BobAmp, bottom = waterY - depth + OverCut + SideBottom + BobAmp; // the bob stays inside too
            float y = waterY - depth * d01;
            return bottom > top ? (top + bottom) * 0.5f : Mathf.Clamp(y, bottom, top);
        }

        /// <summary>The nearest wall within <see cref="WallReach"/> of a shadow, where a side fish fits right under it.</summary>
        bool NearWall(Vector2 p, out GlassSide side, out float along)
        {
            side = GlassSide.Front; along = 0f; float best = WallReach;
            bool any = false;
            for (int s = 0; s < 4; s++)
            {
                var gs = (GlassSide)s;
                if (!SideSeen(gs)) continue;
                float d = AlongX(gs) ? Mathf.Abs(p.y - SidePlane(gs)) : Mathf.Abs(p.x - SidePlane(gs));
                float a = AlongX(gs) ? p.x : p.y;
                if (d > best || !WallFits(gs, a)) continue;
                best = d; side = gs; along = a; any = true;
            }
            return any;
        }

        int OnWalls() { int n = 0; foreach (var f in fish) if (f.SideDrawn) n++; return n; }

        void GoDown(Fish f, GlassSide side, float along)
        {
            f.mode = Mode.Down; f.handT = 0f; f.side = side; f.along = along;
            f.depth01 = winter ? R(0.7f, 0.9f) : R(DepthLow, DepthHigh);
            f.dir = Mathf.Sign(Mathf.Sin(f.heading) * (AlongX(side) ? 1f : 0f) + Mathf.Cos(f.heading) * (AlongX(side) ? 0f : 1f));
            if (f.dir == 0f) f.dir = 1f;
            f.flipScale = f.dir; f.flipT = -1f; f.hangT = -1f; f.sideDartT = -1f;
            f.pauseIn = R(PauseMin, PauseMax); f.stay = R(WallStayMin, WallStayMax);
            HandOffs++;
        }

        /// <summary>The panel's "Fish: show them at the glass": up to <paramref name="n"/> fish go onto the walls now, at the deepest spots the panes
        /// show (the camera is the owner's). Returns how many went; <paramref name="focus"/> is the first one's wall point.</summary>
        public int ToGlass(int n, out GlassSide side, out Vector2 focus)
        {
            side = GlassSide.Front; focus = centre;
            var spots = new List<(GlassSide s, float a, float d)>();
            for (int s = 0; s < 4; s++)
            {
                var gs = (GlassSide)s;
                if (!SideSeen(gs)) continue;
                float lim = AlongX(gs) ? HX : Mathf.Max(-ZMin, ZMax);
                for (float a = -lim; a <= lim; a += 0.02f) if (WallFits(gs, a)) spots.Add((gs, a, WallDepth(gs, a) - (gs == GlassSide.Front ? 1f : 0f)));
            }
            if (spots.Count == 0) return 0;
            spots.Sort((x, y) => x.d.CompareTo(y.d)); // the front pane first (the default camera's), then the deepest
            var first = spots[0];
            side = first.s; focus = OnSide(first.s, first.a);
            int went = 0;
            foreach (var f in fish)
            {
                if (went >= n || OnWalls() >= MaxOnWalls) break;
                if (f.mode != Mode.Shadow) continue;
                float a = first.a + (went == 0 ? -0.06f : 0.06f);
                if (!WallFits(first.s, a)) a = first.a;
                var inside = OnSide(first.s, a);
                var inward = AlongX(first.s) ? new Vector2(0f, first.s == GlassSide.Front ? 1f : -1f) : new Vector2(first.s == GlassSide.Left ? 1f : -1f, 0f);
                f.at = inside + inward * 0.08f; f.dartT = -1f; f.hold = 0f; f.hookUntil = -1f;
                GoDown(f, first.s, a);
                went++;
            }
            return went;
        }

        // ---------------------------------------------------------------- the step
        public void Step(float dt, float time)
        {
            clock += dt;
            float k = winter ? 0.5f : 1f;
            // the school's centre drifts toward its target; a new target now and then is the school turning together
            retarget -= dt;
            float drift = (DriftMin + DriftMax) * 0.5f * k;
            if (retarget <= 0f || (target - centre).sqrMagnitude < 0.03f * 0.03f) NewTarget();
            var to = target - centre;
            if (to.sqrMagnitude > 1e-8f)
            {
                var step = to.normalized * Mathf.Min(to.magnitude, drift * dt);
                if (Inner(centre + step) || !Inner(centre)) centre += step; else NewTarget();
                heading = Mathf.Atan2(to.x, to.y);
            }
            // lures: each takes the nearest shadow that isn't darting
            foreach (var f in fish) f.lure = -1;
            for (int l = 0; l < lures.Count; l++)
            {
                Fish best = null; float bd = 0.8f * 0.8f;
                foreach (var f in fish)
                {
                    if (f.mode != Mode.Shadow || f.lure >= 0 || f.dartT >= 0f || f.hold > 0f || f.hookUntil > clock) continue;
                    float d = (f.at - lures[l]).sqrMagnitude;
                    if (d < bd) { bd = d; best = f; }
                }
                if (best != null) best.lure = l;
            }
            int shadows = 0, sides = 0;
            foreach (var f in fish)
            {
                StepFish(f, dt, time, k);
                if (f.ShadowDrawn && f.shadowScale > 0.001f) shadows++;
                if (f.SideDrawn && f.sideScale > 0.001f) sides++;
                if (f.mode == Mode.Shadow && f.sideScale > 0.001f || f.mode == Mode.Wall && f.shadowScale > 0.001f) DrawnTwice++;
            }
            ShadowsNow = shadows; SidesNow = sides;
        }

        void NewTarget()
        {
            retarget = R(6f, 12f);
            for (int i = 0; i < 12; i++)
            {
                var a = R(0f, Mathf.PI * 2f);
                var p = centre + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * R(0.25f, 0.7f);
                if (Inner(p)) { target = p; return; }
            }
            target = centre;
        }

        void StepFish(Fish f, float dt, float time, float k)
        {
            f.tail = Mathf.Sin((time + f.phase) * TailHz * Mathf.PI * 2f) * TailDeg;
            switch (f.mode)
            {
                case Mode.Shadow: Swim(f, dt, k); f.shadowScale = 1f; f.sideScale = 0f; MaybeDown(f, dt); break;
                case Mode.Down:
                    f.handT += dt;
                    f.shadowScale = Mathf.Clamp01(1f - f.handT / HandOff); f.sideScale = 1f - f.shadowScale;
                    SideSwim(f, dt, time, k);
                    if (f.handT >= HandOff) { f.mode = Mode.Wall; f.shadowScale = 0f; f.sideScale = 1f; }
                    break;
                case Mode.Wall:
                    SideSwim(f, dt, time, k);
                    f.stay -= dt;
                    if (f.stay <= 0f && f.hangT < 0f && f.sideDartT < 0f && f.flipT < 0f) GoUp(f);
                    break;
                case Mode.Up:
                    f.handT += dt;
                    f.sideScale = Mathf.Clamp01(1f - f.handT / HandOff); f.shadowScale = 1f - f.sideScale;
                    if (f.handT >= HandOff) { f.mode = Mode.Shadow; f.sideScale = 0f; f.shadowScale = 1f; f.rest = WallRest; }
                    break;
            }
        }

        void GoUp(Fish f)
        {
            // the shadow comes back up at the same x, a little in from the wall, on water a shadow may swim
            var p = OnSide(f.side, f.along);
            var inward = AlongX(f.side) ? new Vector2(0f, f.side == GlassSide.Front ? 1f : -1f) : new Vector2(f.side == GlassSide.Left ? 1f : -1f, 0f);
            var q = p + inward * 0.1f;
            for (int i = 0; i < 10 && !Inner(q); i++) q += inward * 0.03f;
            if (!Inner(q)) q = centre;
            f.at = q; f.heading = Mathf.Atan2(inward.x, inward.y);
            f.mode = Mode.Up; f.handT = 0f;
            HandOffs++;
        }

        void MaybeDown(Fish f, float dt)
        {
            f.rest -= dt;
            if (f.rest > 0f || f.dartT >= 0f || f.hold > 0f || f.lure >= 0 || f.hookUntil > clock || OnWalls() >= MaxOnWalls) return;
            if (NearWall(f.at, out var side, out float along)) GoDown(f, side, along);
        }

        void Swim(Fish f, float dt, float k)
        {
            if (f.dartT >= 0f)
            {
                f.dartT += dt;
                float u = Mathf.Clamp01(f.dartT / f.dartLen), e = 1f - (1f - u) * (1f - u);
                var prev = f.at;
                f.at = Vector2.Lerp(f.dartFrom, f.dartTo, e);
                var d = f.dartTo - f.dartFrom;
                if (d.sqrMagnitude > 1e-8f) f.heading = Mathf.Atan2(d.x, d.y);
                f.tail *= 2f; // a hard kick
                if (u >= 1f) f.dartT = -1f;
                return;
            }
            if (f.hookUntil > clock) { f.heading = Mathf.Atan2(f.hook.x - f.at.x, f.hook.y - f.at.y); return; } // nosing the hook
            if (f.hold > 0f) { f.hold -= dt; f.at += new Vector2(Mathf.Sin(f.heading), Mathf.Cos(f.heading)) * DriftMin * 0.3f * k * dt; ClampToWater(f); return; }
            Vector2 want = f.lure >= 0 && f.lure < lures.Count ? lures[f.lure] : centre + Slot(f.index, heading);
            var to = want - f.at;
            float speed = DriftMax * f.speedK * k * (to.magnitude > 0.15f ? 1.3f : 1f);
            if (to.sqrMagnitude > 0.004f * 0.004f)
            {
                var step = to.normalized * Mathf.Min(to.magnitude, speed * dt);
                var next = f.at + step;
                if (Inner(next) || f.lure >= 0 && Water(next.x, next.y) || !Inner(f.at)) f.at = next;
                float want01 = Mathf.Atan2(to.x, to.y);
                f.heading = TurnTo(f.heading, want01, dt);
            }
            else f.heading = TurnTo(f.heading, heading, dt);
        }

        void ClampToWater(Fish f) { if (!Water(f.at.x, f.at.y)) f.at = Vector2.MoveTowards(f.at, centre, 0.01f); }

        static float TurnTo(float from, float to, float dt)
        {
            float d = Mathf.DeltaAngle(from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return from + d * (1f - Mathf.Exp(-dt * 3f / (TurnSeconds * 0.33f)));
        }

        void SideSwim(Fish f, float dt, float time, float k)
        {
            f.tail = Mathf.Sin((time + f.phase) * SideTailHz * Mathf.PI * 2f) * SideTailDeg;
            f.bob = Mathf.Sin((time + f.phase) * BobHz * Mathf.PI * 2f) * BobAmp;
            f.fin = 0f;
            float lowK = winter ? 0.5f : 1f;
            if (winter) f.depth01 = Mathf.MoveTowards(f.depth01, Mathf.Clamp(f.depth01, 0.7f, 0.9f), 0.2f * dt);
            if (f.flipT >= 0f)
            {
                f.flipT += dt;
                float u = Mathf.Clamp01(f.flipT / Flip);
                f.flipScale = Mathf.Lerp(f.flipFrom, -f.flipFrom, u);
                if (u >= 1f) { f.flipT = -1f; f.dir = -f.flipFrom; f.flipScale = f.dir; }
                return;
            }
            if (f.sideDartT >= 0f)
            {
                f.sideDartT += dt;
                float u = Mathf.Clamp01(f.sideDartT / DartSeconds), e = 1f - (1f - u) * (1f - u);
                f.along = Mathf.Lerp(f.sideDartFrom, f.sideDartTo, e);
                f.depth01 = Mathf.MoveTowards(f.depth01, f.sideDepthTo, dt * 2f);
                f.tail *= 1.8f;
                if (u >= 1f) f.sideDartT = -1f;
                return;
            }
            if (f.hangT >= 0f)
            {
                f.hangT += dt;
                f.tail *= 0.2f;
                f.fin = Mathf.Sin(f.hangT * FinHz * Mathf.PI * 2f) * FinDeg;
                if (f.hangT >= Hang)
                {
                    f.hangT = -1f; f.sideDartT = 0f; f.sideDartFrom = f.along; f.sideDartTo = ClampAlong(f, f.along + f.dir * SideDart); f.sideDepthTo = f.depth01;
                    f.pauseIn = R(PauseMin, PauseMax);
                }
                return;
            }
            f.pauseIn -= dt;
            if (f.pauseIn <= 0f && f.mode == Mode.Wall) { f.hangT = 0f; return; }
            float speed = Mathf.Lerp(SideSpeedMin, SideSpeedMax, (f.speedK - 0.75f) * 2f) * k * lowK / Mathf.Max(0.5f, k); // B70.c: × 0.5 in winter
            float next = f.along + f.dir * speed * dt;
            if (!WallFits(f.side, next + f.dir * 0.01f)) { f.flipT = 0f; f.flipFrom = f.dir; return; } // the wall's end: the flat flip
            f.along = next;
        }

        /// <summary>A side fish's drawn centre (box x, y, z on the wall's plane) now.</summary>
        public Vector3 SidePosition(Fish f)
        {
            var p = OnSide(f.side, f.along);
            return new Vector3(p.x, SideY(f.side, f.along, f.depth01) + f.bob, p.y);
        }
    }
}
