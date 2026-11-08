// #370 Staged moments: the pure plan (System and the Life/Sim types only, so Tools/SimTests and Tools/AnimBench compile it).
using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>#370: who someone is in a staged moment (animation-specs C3).</summary>
    public enum MomentRole { Spotter, Couple, Officiant, Guest, Parent, Family, Kid, Chief, OldChief, Elder, Leader, Bringer }

    /// <summary>#370: what one step of a staged moment shows. The planner plays <see cref="MomentStep.kind"/>; the view lays the
    /// beat's one-shot pose on top (View/Beats/MomentBeats.cs).</summary>
    public enum StageBeat
    {
        /// <summary>The planner walks them to the moment's place (its route); they stand at their spot when there.</summary>
        Travel,
        Stand, LookUp, Point, Run, AisleCheer, WalkIn, Vow, Bless, Join, Ring, Centre, Wave,
        Settle, LiftBaby, Cradle, Cheer, PressHands, WalkThrough, Give, Take, Seat, Speech, Listen, Bow, GoldPop,
        SetDown, TossWind, Toss, Eat, Jump, Clap,
    }

    /// <summary>#370: one step of one member: what, when (real seconds from the moment's start), where (box x, z; a straight move
    /// from 0 to 1 over the step), what they face.</summary>
    public struct MomentStep
    {
        public StageBeat beat;
        public ActivityKind kind;
        public float t0, t1;
        public float x0, z0, x1, z1;
        public bool hasFace, faceGlass;
        public float faceX, faceZ;
        /// <summary>Seconds into the step when its one-shot starts (a cheer as the couple passes, a bow in the wave). Negative: the
        /// one-shot began that long before this step (the toss's second half).</summary>
        public float cue;
        public Prop prop;
        /// <summary>The planner walks them to the place (<see cref="StageBeat.Travel"/>), carrying <see cref="prop"/> if set.</summary>
        public bool travel, carries;
        /// <summary>Their slot in the moment's ring (−1 = not in it). Steps after the dance keep it: they stay where the ring stopped,
        /// at <see cref="ringScale"/> of its radius.</summary>
        public int ring;
        public float ringScale;
        /// <summary>The other one (the couple; the giver and the taker), by person key; 0 = none.</summary>
        public int partner;
        /// <summary>The loop this ends (P5): the moment's end beat.</summary>
        public bool endBeat;

        public float Length => Math.Max(1e-4f, t1 - t0);
        public bool Moves => Math.Abs(x1 - x0) + Math.Abs(z1 - z0) > 1e-5f;
    }

    /// <summary>#370: one person in a staged moment and their steps, in time order, back to back.</summary>
    public sealed class MomentMember
    {
        public int key;
        public MomentRole role;
        public bool kid;
        /// <summary>Where they stood when the moment began.</summary>
        public float x, z;
        public readonly List<MomentStep> steps = new List<MomentStep>();
        /// <summary>When they are free again (real seconds from the moment's start).</summary>
        public float ReleaseAt => steps.Count == 0 ? 0f : steps[steps.Count - 1].t1;
        public float JoinAt => steps.Count == 0 ? 0f : steps[0].t0;
    }

    /// <summary>#370: a person the planner can cast: their key, kid or not, where they stand, and how long the planner's walk to
    /// the moment's place takes them (real seconds at the walking pace).</summary>
    public struct MomentPerson
    {
        public int key;
        public bool kid;
        public float x, z, walk;
        public static MomentPerson Of(int key, bool kid, float x, float z, float walk) => new MomentPerson { key = key, kid = kid, x = x, z = z, walk = walk };
    }

    /// <summary>#370: the moment's cast as the planner found it. Wedding: <see cref="a"/>, <see cref="b"/> the couple; newborn:
    /// <see cref="a"/> the parent (the baby rides in the arms, <see cref="babyKey"/>); new chief: <see cref="a"/> the chief,
    /// <see cref="b"/> the old chief if alive. <see cref="officiant"/>: who blesses (the leader or the eldest), the festival's
    /// speaker. The crowd nearest first.</summary>
    public sealed class MomentCast
    {
        public MomentPerson a, b, officiant;
        public bool hasA, hasB, hasOfficiant;
        public int babyKey;
        public readonly List<MomentPerson> grown = new List<MomentPerson>();
        public readonly List<MomentPerson> kids = new List<MomentPerson>();
    }

    /// <summary>#370: the real places: the fire, the front glass's spot and its outward normal (toward the Keeper), the box.</summary>
    public struct MomentGeometry
    {
        public float fireX, fireZ, glassX, glassZ, nx, nz, halfX, halfZ;
        /// <summary>t4-fixes: what hides people standing behind it (tents, buildings): x, z, radius triples in box units; null = none.</summary>
        public float[] blockers;
    }

    /// <summary>
    /// #370: a staged moment as a pure plan (animation-specs C3; visit-loop.md §2.1, §4.3): the people organise the ceremony
    /// <b>because the Keeper showed up</b>, so every scene starts now, from where everyone stands. One function of (kind, cast,
    /// places, seed): the planner lays its steps as live segments, the view poses and places the dolls from the same steps, and
    /// AnimBench runs the same plan. Every member ends by <see cref="Duration"/> (they go back to their day then); nothing
    /// reads the sim's random stream (a hash of the seed).
    /// <list type="bullet">
    /// <item><b>Wedding:</b> a kid looks up at the glass, points, runs to tell; everyone gathers into an aisle; the couple walks in
    /// hand in hand while the rows cheer as they pass; the vow and the blessing; hands joined and a hop (a hug behind the
    /// view's <c>BeatFlags.Hugs</c>); a ring dances round them; they all wave to the glass.</item>
    /// <item><b>Newborn:</b> a parent carries the baby to the glass and lifts it high; the family cheers behind; the kids press
    /// their hands to the glass; cradle and bow.</item>
    /// <item><b>New chief:</b> a horseshoe at the fire; the chief walks through its gap to the seat; the old chief hands over (or
    /// the elder blesses); sit, stand, speak; everyone bows in a wave, nearest first; the gold mark pops on.</item>
    /// <item><b>Festival:</b> everyone brings a log or a basket and sets it down; the leader tosses a log on the fire (it
    /// flares); the speech; the camp dances in a ring round the fire, eats, and jumps together once at the end.</item>
    /// </list>
    /// All numbers [est], tuned on AnimBench (clips 370-*.json).
    /// </summary>
    public sealed class MomentStagePlan
    {
        // ---- the numbers (animation-specs C3, B2.e; [est]) ----
        /// <summary>A kid runs to tell at this pace (u/s), about twice the walk.</summary>
        public const float RunSpeed = 0.62f;
        /// <summary>The longest the gathering may take (s); whoever can't arrive by then isn't cast.</summary>
        public const float MaxGather = 16f;
        public const int MaxWeddingGuests = 12, MaxFamily = 4, MaxGlassKids = 3, MaxChiefRing = 12, MaxFestival = 16;
        /// <summary>The wedding: the aisle walk, the vow, the join, the dance, the wave (s).</summary>
        public const float AisleSeconds = 4.5f, VowSeconds = 2.2f, JoinSeconds = 2f, WeddingDance = 14f, WeddingWave = 2.5f;
        /// <summary>The ring (B2.e): its beat, a hop each beat, a step in every 8 beats (1 s), the turn, reversing every 4 beats.</summary>
        public const float RingBeat = 0.5f, RingDegPerSecond = 25f;
        public const int RingReverseEvery = 4;
        /// <summary>The newborn: the settle, the lift (with the wind-up), the cradle and bow (s).</summary>
        public const float SettleSeconds = 0.4f, LiftSeconds = 3.8f, CradleSeconds = 1.2f;
        /// <summary>The new chief: through the gap, the hand-over, sit, speech, the bow wave (0.12 s apart), the pop (s).</summary>
        public const float WalkThroughSeconds = 2f, HandOverSeconds = 1.6f, SitSeconds = 1f, ChiefSpeech = 2.2f, BowWave = 2.2f, BowGap = 0.12f, PopSeconds = 1.3f;
        /// <summary>The festival: set down, the toss (wind, throw), the speech, the dance, the meal, the jump (s).</summary>
        public const float SetDownSeconds = 1f, TossWindSeconds = 0.55f, TossSeconds = 1.05f, FestivalSpeech = 3.2f, SpeechCheer = 0.8f, FestivalDance = 16f, ClapSeconds = 1f, MealSeconds = 10f, JumpSeconds = 1.2f;
        public const float SeatOffset = 0.27f, TossReach = 0.24f;
        /// <summary>The aisle: the rows stand this far either side of its line, this far apart along it (u).</summary>
        public const float AisleHalf = 0.19f, AisleSlot = 0.16f;

        public readonly MomentKind kind;
        public readonly int seed;
        public readonly MomentGeometry geo;
        public readonly List<MomentMember> members = new List<MomentMember>();
        /// <summary>The ring (wedding, festival): its centre, radius, when it dances, and who is in it in slot order.</summary>
        public float centreX, centreZ, ringRadius, ringT0, ringT1;
        public readonly List<int> ringKeys = new List<int>();
        /// <summary>The newborn's baby (a person key, 0 = none) and the parent who carries it.</summary>
        public int babyKey, carrierKey;
        public float Duration { get; private set; }
        /// <summary>Why <see cref="Build"/> returned null (tests, the panel).</summary>
        public static string LastWhyNot { get; private set; } = "";

        MomentStagePlan(MomentKind kind, int seed, MomentGeometry geo) { this.kind = kind; this.seed = seed; this.geo = geo; }

        public MomentMember Member(int key)
        {
            foreach (var m in members) if (m.key == key) return m;
            return null;
        }

        /// <summary>The step <paramref name="key"/> is in at <paramref name="t"/> (seconds from the start). False before they join,
        /// after they are released, or when they aren't in it.</summary>
        public bool StepAt(int key, float t, out MomentMember member, out int index)
        {
            member = Member(key); index = -1;
            if (member == null || member.steps.Count == 0 || t < member.JoinAt || t >= member.ReleaseAt) return false;
            var s = member.steps;
            for (int i = s.Count - 1; i >= 0; i--) if (t >= s[i].t0) { index = i; return true; }
            return false;
        }

        /// <summary>The ring's radius for <paramref name="dancers"/> (B2.e: 0.3 + 0.03 × max(0, n − 6); the kit's RingGroup.RadiusFor).</summary>
        public static float RingRadiusFor(int dancers) => 0.3f + 0.03f * Math.Max(0, dancers - 6);

        /// <summary>A hash in [0, 1) of (seed, key, salt): the moment's own stream, never the sim's.</summary>
        public static float Hash01(int seed, int key, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)key * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du ^ 0x27D4EB2Fu;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h >> 8) * (1f / 16777216f);
            }
        }

        // ======================================================================== building

        /// <summary>The plan for a moment of <paramref name="kind"/>, or null when it can't be staged (a principal missing or too
        /// far to arrive in <see cref="MaxGather"/> s; <see cref="LastWhyNot"/> says why). Callbacks aren't staged here.</summary>
        public static MomentStagePlan Build(MomentKind kind, MomentCast cast, MomentGeometry geo, int seed)
        {
            LastWhyNot = "";
            if (cast == null) { LastWhyNot = "no cast"; return null; }
            if (geo.halfX <= 0f) geo.halfX = 3f;
            if (geo.halfZ <= 0f) geo.halfZ = 2.2f;
            float nl = (float)Math.Sqrt(geo.nx * geo.nx + geo.nz * geo.nz);
            if (nl < 1e-4f) { geo.nx = 0f; geo.nz = -1f; } else { geo.nx /= nl; geo.nz /= nl; }
            var plan = new MomentStagePlan(kind, seed, geo);
            bool ok = kind switch
            {
                MomentKind.Wedding => plan.Wedding(cast),
                MomentKind.Newborn => plan.Newborn(cast),
                MomentKind.NewLeader => plan.Chief(cast),
                MomentKind.Festival => plan.Festival(cast),
                MomentKind.Ruling => plan.Festival(cast), // #422: a gathering at the fire, the ruling notable speaking (its own beats: a view ticket)
                MomentKind.WhiteWolf => plan.Festival(cast), // #437: the camp gathers to watch the white wolf (B21.d; its own beats: #441)
                _ => plan.Fail("not a staged kind"),
            };
            if (!ok) return null;
            float end = 0f;
            foreach (var m in plan.members) end = Math.Max(end, m.ReleaseAt);
            plan.Duration = end;
            return plan;
        }

        bool Fail(string why) { LastWhyNot = why; return false; }

        /// <summary>t4-fixes: the wedding's turns round the fire, in the order tried (radians from straight toward the glass).</summary>
        static readonly float[] VowTurns = { 0f, 0.4f, -0.4f, 0.8f, -0.8f, 1.2f, -1.2f, 1.57f, -1.57f };

        /// <summary>
        /// t4-fixes (tester run 3: the arch beside the teepee hid the couple and the guests at the default camera): the vow point is in
        /// front of the fire toward the glass (the Keeper's side), as before; when a blocker (<see cref="MomentGeometry.blockers"/>) stands in
        /// the ring or between it and the glass, the point turns round the fire (up to a quarter either way) to the first spot in the open,
        /// else the least hidden one. No blockers: the old spot.
        /// </summary>
        void VowPoint(float R, out float x, out float z)
        {
            float dist = R + 0.34f, best = float.MaxValue;
            x = geo.fireX + Gx * dist; z = geo.fireZ + Gz * dist;
            ClampIn(ref x, ref z, R + 0.12f);
            foreach (float a in VowTurns)
                for (int far = 0; far < 2; far++) // the usual distance, then a little farther out from the fire
                {
                    float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a), d = dist + far * 0.25f;
                    float dx = Gx * ca - Gz * sa, dz = Gx * sa + Gz * ca;
                    float cx = geo.fireX + dx * d, cz = geo.fireZ + dz * d;
                    ClampIn(ref cx, ref cz, R + 0.12f);
                    float hidden = Hidden(cx, cz, R);
                    if (hidden <= 0f) { x = cx; z = cz; return; } // the first spot in the open, in the order tried
                    float score = hidden + Math.Abs(a) * 0.02f + far * 0.01f; // none in the open: the least hidden, a little turn preferred
                    if (score < best) { best = score; x = cx; z = cz; }
                }
        }

        /// <summary>t4-fixes: how much the blockers hide a ring of radius <paramref name="R"/> at (cx, cz): overlap with the ring, and any in
        /// front of it (toward the glass, within a box unit) as wide as the ring. 0 = in the open.</summary>
        public float Hidden(float cx, float cz, float R)
        {
            var b = geo.blockers;
            if (b == null) return 0f;
            float s = 0f;
            for (int i = 0; i + 2 < b.Length; i += 3)
            {
                float bx = b[i], bz = b[i + 1], br = b[i + 2];
                float inRing = R + 0.12f + br - Dist(cx, cz, bx, bz);
                if (inRing > 0f) s += inRing;
                float ahead = (bx - cx) * Gx + (bz - cz) * Gz, side = Math.Abs((bx - cx) * Px + (bz - cz) * Pz);
                if (ahead > 0f && ahead < 1f && side < R + br) s += (R + br - side) * 0.5f;
            }
            return s;
        }

        // ---- geometry helpers: g points out of the glass (toward the Keeper), p across it (left to right on screen) ----
        float Gx => geo.nx; float Gz => geo.nz;
        float Px => -geo.nz; float Pz => geo.nx;

        void ClampIn(ref float x, ref float z, float margin)
        {
            float mx = Math.Max(0.05f, geo.halfX - margin), mz = Math.Max(0.05f, geo.halfZ - margin);
            x = Math.Max(-mx, Math.Min(mx, x));
            z = Math.Max(-mz, Math.Min(mz, z));
        }

        static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        MomentMember Add(MomentPerson p, MomentRole role)
        {
            var m = new MomentMember { key = p.key, role = role, kid = p.kid, x = p.x, z = p.z };
            members.Add(m);
            return m;
        }

        /// <summary>A step from where the member last was to (x1, z1); a stand when it's the same spot.</summary>
        static MomentStep Step(StageBeat beat, ActivityKind kind, float t0, float t1, float x0, float z0, float x1, float z1)
            => new MomentStep { beat = beat, kind = kind, t0 = t0, t1 = t1, x0 = x0, z0 = z0, x1 = x1, z1 = z1, ring = -1, ringScale = 1f };

        static void Put(MomentMember m, MomentStep s)
        {
            if (s.t1 <= s.t0 + 1e-4f) return;
            m.steps.Add(s);
        }

        static (float x, float z) Last(MomentMember m) => m.steps.Count == 0 ? (m.x, m.z) : (m.steps[m.steps.Count - 1].x1, m.steps[m.steps.Count - 1].z1);

        static MomentStep Facing(MomentStep s, float fx, float fz) { s.hasFace = true; s.faceX = fx; s.faceZ = fz; return s; }
        static MomentStep Glass(MomentStep s) { s.faceGlass = true; return s; }

        /// <summary>Stand at the member's last spot from t0 to t1.</summary>
        static MomentStep Here(MomentMember m, StageBeat beat, ActivityKind kind, float t0, float t1)
        {
            var (x, z) = Last(m);
            return Step(beat, kind, t0, t1, x, z, x, z);
        }

        /// <summary>The gathering: from <paramref name="go"/> the planner walks them to the place; they stand at (x, z) till <paramref name="until"/>.</summary>
        static MomentStep Travel(MomentMember m, float go, float until, float x, float z, Prop carry = Prop.None)
        {
            var s = Step(StageBeat.Travel, ActivityKind.Watch, go, until, x, z, x, z);
            s.travel = true;
            if (carry != Prop.None) { s.prop = carry; s.carries = true; }
            return s;
        }

        /// <summary>When the crowd sets off: after the news reaches them, a little apart (0..<paramref name="spread"/> s).</summary>
        float GoAt(int key, float from, float spread) => from + spread * Hash01(seed, key, 3701);

        // ======================================================================== the wedding (C3.a)

        bool Wedding(MomentCast c)
        {
            if (!c.hasA || !c.hasB) return Fail("the wedding needs both of the couple");
            // The crowd: the officiant, the kids, the grown, nearest first, up to the cap.
            var crowd = new List<MomentPerson>();
            if (c.hasOfficiant) crowd.Add(c.officiant);
            foreach (var k in c.kids) crowd.Add(k);
            foreach (var g in c.grown) crowd.Add(g);
            // The spotter: the kid nearest the glass's middle line of the camp (else the nearest grown-up). The Keeper is the reason.
            int spotterAt = -1;
            for (int i = 0; i < crowd.Count; i++) if (crowd[i].kid && (!c.hasOfficiant || i > 0)) { spotterAt = i; break; }
            if (spotterAt < 0) for (int i = 0; i < crowd.Count; i++) if (!c.hasOfficiant || i > 0) { spotterAt = i; break; }

            int ringN = 0; // dancers: everyone but the couple
            float RFor(int n) => RingRadiusFor(n);
            // Who comes: those who can arrive in time.
            const float spotEnd = 2f;
            var cast = new List<MomentPerson>();
            float gather = 9.5f;
            float spotRun = 0f;
            MomentPerson spotter = default; bool hasSpotter = false;
            if (spotterAt >= 0)
            {
                spotter = crowd[spotterAt];
                hasSpotter = true;
            }
            foreach (var p in new[] { c.a, c.b })
                gather = Math.Max(gather, GoAt(p.key, spotEnd, 1.2f) + p.walk + 0.6f);
            if (gather > MaxGather) return Fail("the couple can't get there in time");
            for (int i = 0; i < crowd.Count && cast.Count < MaxWeddingGuests; i++)
            {
                var p = crowd[i];
                if (hasSpotter && p.key == spotter.key) continue;
                if (GoAt(p.key, spotEnd, 1.2f) + p.walk + 0.6f > MaxGather) continue;
                cast.Add(p);
            }
            foreach (var p in cast) gather = Math.Max(gather, GoAt(p.key, spotEnd, 1.2f) + p.walk + 0.6f);
            ringN = cast.Count + (hasSpotter ? 1 : 0);
            float R = RFor(Math.Max(1, ringN));

            // The vow point: in front of the fire, toward the glass, clear of the fire by the ring (t4-fixes: turned round the fire away
            // from a tent that would stand in the ring or hide it from the glass).
            VowPoint(R, out centreX, out centreZ);
            float cx = centreX, cz = centreZ;
            // The aisle runs across the screen (p), the couple walks it left to right; two rows face each other 0.32 apart.
            int rowMembers = cast.Count + (hasSpotter ? 1 : 0);
            int rows = Math.Max(1, (rowMembers + 1) / 2);
            float aisle = 0.34f + AisleSlot * rows;
            float ex = cx - Px * aisle, ez = cz - Pz * aisle;
            if (hasSpotter)
            {
                float d = Dist(spotter.x, spotter.z, ex, ez);
                spotRun = Math.Max(1.2f, d / RunSpeed);
                gather = Math.Max(gather, spotEnd + spotRun + 0.6f);
                if (gather > MaxGather) { gather = MaxGather; spotRun = Math.Max(1.2f, gather - spotEnd - 0.6f); } // a far spotter runs faster
            }

            float tA = gather, tV = tA + AisleSeconds, tJ = tV + VowSeconds, tD = tJ + JoinSeconds, tE = tD + WeddingDance, tEnd = tE + WeddingWave;
            ringT0 = tD; ringT1 = tE; ringRadius = R;

            // The couple: side by side at the aisle's start, then the walk in, the vow facing each other, the join, the middle of the ring, the wave.
            var A = Add(c.a, MomentRole.Couple);
            var B = Add(c.b, MomentRole.Couple);
            const float side = 0.045f, face = 0.07f;
            float axL = ex + Gx * side, azL = ez + Gz * side, bxL = ex - Gx * side, bzL = ez - Gz * side;
            float axC = cx + Gx * side, azC = cz + Gz * side, bxC = cx - Gx * side, bzC = cz - Gz * side;
            float axV = cx - Px * face, azV = cz - Pz * face, bxV = cx + Px * face, bzV = cz + Pz * face;
            foreach (var (m, other, sx, sz, wx, wz, vx, vz) in new[] { (A, B, axL, azL, axC, azC, axV, azV), (B, A, bxL, bzL, bxC, bzC, bxV, bzV) })
            {
                Put(m, Travel(m, GoAt(m.key, spotEnd, 1.2f), tA, sx, sz));
                var walk = Step(StageBeat.WalkIn, ActivityKind.Court, tA, tV, sx, sz, wx, wz); walk.partner = other.key; Put(m, walk);
                var turn = Step(StageBeat.Vow, ActivityKind.Court, tV, tV + 0.6f, wx, wz, vx, vz); turn.partner = other.key; Put(m, Facing(turn, cx, cz)); // a step round to face each other
                var vow = Step(StageBeat.Vow, ActivityKind.Court, tV + 0.6f, tJ, vx, vz, vx, vz); vow.partner = other.key; Put(m, Facing(vow, cx, cz));
                var join = Step(StageBeat.Join, ActivityKind.Court, tJ, tD, vx, vz, vx, vz); join.partner = other.key; join.endBeat = true; Put(m, Facing(join, cx, cz)); // the vow's payoff
                var ctr = Step(StageBeat.Centre, ActivityKind.Dance, tD, tE, vx, vz, vx, vz); ctr.partner = other.key; Put(m, ctr);
                var wave = Glass(Step(StageBeat.Wave, ActivityKind.Wave, tE, tEnd, vx, vz, vx, vz)); wave.endBeat = true; Put(m, wave);
            }

            // The rows: the spotter first (it told them), then the rest alternating sides; each cheers as the couple passes.
            var rowCast = new List<(MomentPerson p, MomentRole role)>();
            if (hasSpotter) rowCast.Add((spotter, MomentRole.Spotter));
            foreach (var p in cast) rowCast.Add((p, c.hasOfficiant && p.key == c.officiant.key ? MomentRole.Officiant : p.kid ? MomentRole.Kid : MomentRole.Guest));
            int slot = 0;
            float aisleLen = Dist(ex, ez, cx, cz);
            foreach (var (p, role) in rowCast)
            {
                var m = Add(p, role);
                float sx, sz, along;
                if (role == MomentRole.Officiant)
                {
                    // Behind the vow point, facing the couple and the glass.
                    sx = cx - Gx * 0.17f; sz = cz - Gz * 0.17f; along = aisleLen;
                }
                else
                {
                    int j = slot / 2, s = (slot & 1) == 0 ? 1 : -1;
                    slot++;
                    along = 0.22f + AisleSlot * j;
                    float lx = ex + Px * along, lz = ez + Pz * along;
                    sx = lx + Gx * AisleHalf * s; sz = lz + Gz * AisleHalf * s;
                }
                float lineX = ex + Px * Math.Min(along, aisleLen), lineZ = ez + Pz * Math.Min(along, aisleLen);
                if (role == MomentRole.Spotter)
                {
                    // Spotted: look up at the glass, point at the Keeper (a "!" pops), run to the fire to tell.
                    Put(m, Glass(Step(StageBeat.LookUp, ActivityKind.LookUp, 0f, 0.9f, p.x, p.z, p.x, p.z)));
                    Put(m, Glass(Step(StageBeat.Point, ActivityKind.JumpAndPoint, 0.9f, spotEnd, p.x, p.z, p.x, p.z)));
                    Put(m, Step(StageBeat.Run, ActivityKind.RunOver, spotEnd, spotEnd + spotRun, p.x, p.z, sx, sz));
                    Put(m, Facing(Here(m, StageBeat.Stand, ActivityKind.Watch, spotEnd + spotRun, tA), lineX, lineZ));
                }
                else Put(m, Travel(m, GoAt(m.key, spotEnd, 1.2f), tA, sx, sz));
                if (role == MomentRole.Officiant)
                {
                    Put(m, Facing(Here(m, StageBeat.Stand, ActivityKind.Watch, tA, tV), cx, cz));
                    Put(m, Facing(Here(m, StageBeat.Bless, ActivityKind.Bless, tV, tJ), cx, cz));
                }
                else
                {
                    var cheer = Facing(Here(m, StageBeat.AisleCheer, ActivityKind.Watch, tA, tJ), lineX, lineZ);
                    cheer.cue = AisleSeconds * Math.Max(0f, Math.Min(1f, (along - 0.1f) / Math.Max(0.1f, aisleLen))); // the wave runs down the aisle with them
                    Put(m, cheer);
                }
                var j2 = Facing(Here(m, StageBeat.Cheer, ActivityKind.Watch, tJ, tD), cx, cz);
                j2.cue = 0.15f + 0.4f * Hash01(seed, m.key, 3702);
                Put(m, j2);
                ringKeys.Add(m.key);
            }
            // The ring: slot order by where they stand round the vow point, so nobody crosses the middle to get there.
            SortRingByAngle(cx, cz);
            foreach (var m in members)
            {
                int r = ringKeys.IndexOf(m.key);
                if (r < 0) continue;
                var ring = Step(StageBeat.Ring, ActivityKind.Dance, tD, tE, cx, cz, cx, cz); ring.ring = r; Put(m, ring);
                var wave = Glass(Step(StageBeat.Wave, ActivityKind.Wave, tE, tEnd, cx, cz, cx, cz)); wave.ring = r; wave.cue = 0.1f + 0.3f * Hash01(seed, m.key, 3703); wave.endBeat = true;
                Put(m, wave);
            }
            return true;
        }

        void SortRingByAngle(float cx, float cz)
        {
            var at = new Dictionary<int, double>();
            foreach (var m in members)
            {
                if (!ringKeys.Contains(m.key)) continue;
                var (x, z) = Last(m);
                at[m.key] = Math.Atan2(z - cz, x - cx);
            }
            ringKeys.Sort((a, b) => at[a] != at[b] ? at[a].CompareTo(at[b]) : a.CompareTo(b));
        }

        // ======================================================================== the newborn (C3.b)

        bool Newborn(MomentCast c)
        {
            if (!c.hasA) return Fail("nobody to hold the baby");
            babyKey = c.babyKey; carrierKey = c.a.key;
            // At the glass, a step in from it, facing the Keeper.
            float px = geo.glassX - Gx * 0.12f, pz = geo.glassZ - Gz * 0.12f;
            ClampIn(ref px, ref pz, 0.06f);
            centreX = px; centreZ = pz;
            float gather = Math.Max(4f, c.a.walk + 0.6f);
            if (gather > MaxGather) return Fail("the parent can't get to the glass in time");
            var fam = new List<MomentPerson>();
            foreach (var g in c.grown) if (fam.Count < MaxFamily && GoAt(g.key, 0.3f, 0.8f) + g.walk + 0.5f <= MaxGather) fam.Add(g);
            var kids = new List<MomentPerson>();
            foreach (var k in c.kids) if (kids.Count < MaxGlassKids && GoAt(k.key, 0.3f, 0.8f) + k.walk + 0.5f <= MaxGather) kids.Add(k);
            foreach (var p in fam) gather = Math.Max(gather, GoAt(p.key, 0.3f, 0.8f) + p.walk + 0.5f);
            foreach (var p in kids) gather = Math.Max(gather, GoAt(p.key, 0.3f, 0.8f) + p.walk + 0.5f);
            float tS = gather, tL = tS + SettleSeconds, tC = tL + LiftSeconds, tEnd = tC + CradleSeconds;

            var par = Add(c.a, MomentRole.Parent);
            Put(par, Glass(Travel(par, 0f, tS, px, pz)));
            Put(par, Glass(Here(par, StageBeat.Settle, ActivityKind.Watch, tS, tL)));
            Put(par, Glass(Here(par, StageBeat.LiftBaby, ActivityKind.Watch, tL, tC)));
            var cradle = Glass(Here(par, StageBeat.Cradle, ActivityKind.Watch, tC, tEnd)); cradle.endBeat = true;
            Put(par, cradle);

            for (int i = 0; i < fam.Count; i++)
            {
                var m = Add(fam[i], MomentRole.Family);
                int row = i / 3, col = i % 3;
                // batch 2: 0.3 across, not 0.17: at the default camera (yaw 28) the +0.17 one stood right behind the parent and read as inside it
                float across = (col == 0 ? -1f : col == 1 ? 1f : 0f) * 0.3f;
                float x = px - Gx * (0.3f + 0.12f * row) + Px * across, z = pz - Gz * (0.3f + 0.12f * row) + Pz * across;
                Put(m, Facing(Travel(m, GoAt(m.key, 0.3f, 0.8f), tL, x, z), px, pz));
                var cheer = Facing(Here(m, StageBeat.Cheer, ActivityKind.Cheer, tL + 0.45f, tC), px, pz); // the baby goes up: they cheer
                Put(m, Facing(Here(m, StageBeat.Stand, ActivityKind.Watch, tL, tL + 0.45f), px, pz));
                Put(m, cheer);
                var wave = Glass(Here(m, StageBeat.Wave, ActivityKind.Wave, tC, tEnd)); wave.cue = 0.1f + 0.3f * Hash01(seed, m.key, 3711); wave.endBeat = true;
                Put(m, wave);
            }
            for (int i = 0; i < kids.Count; i++)
            {
                var m = Add(kids[i], MomentRole.Kid);
                float across = (i % 2 == 0 ? -1f : 1f) * (0.17f + 0.13f * (i / 2));
                float x = geo.glassX - Gx * 0.04f + Px * across, z = geo.glassZ - Gz * 0.04f + Pz * across;
                ClampIn(ref x, ref z, 0.05f);
                Put(m, Glass(Travel(m, GoAt(m.key, 0.3f, 0.8f), tS, x, z)));
                var press = Glass(Here(m, StageBeat.PressHands, ActivityKind.PressHands, tS, tEnd)); press.endBeat = true;
                Put(m, press);
            }
            return true;
        }

        // ======================================================================== the new chief (C3.c)

        bool Chief(MomentCast c)
        {
            if (!c.hasA) return Fail("no chief");
            float gather = Math.Max(6f, GoAt(c.a.key, 0f, 1f) + c.a.walk + 0.5f);
            if (gather > MaxGather) return Fail("the chief can't get to the fire in time");
            bool old = c.hasB && GoAt(c.b.key, 0f, 1f) + c.b.walk + 0.5f <= MaxGather;
            if (old) gather = Math.Max(gather, GoAt(c.b.key, 0f, 1f) + c.b.walk + 0.5f);
            var ring = new List<MomentPerson>();
            if (c.hasOfficiant && !old && GoAt(c.officiant.key, 0f, 1f) + c.officiant.walk + 0.5f <= MaxGather) ring.Add(c.officiant); // no old chief: the elder blesses
            foreach (var g in c.grown) if (ring.Count < MaxChiefRing) ring.Add(g);
            foreach (var k in c.kids) if (ring.Count < MaxChiefRing) ring.Add(k);
            ring.RemoveAll(p => GoAt(p.key, 0f, 1f) + p.walk + 0.5f > MaxGather);
            foreach (var p in ring) gather = Math.Max(gather, GoAt(p.key, 0f, 1f) + p.walk + 0.5f);
            float fx = geo.fireX, fz = geo.fireZ;
            int n = ring.Count;
            float R = Math.Max(0.48f, RingRadiusFor(n + 2));
            ringRadius = R; centreX = fx; centreZ = fz;
            double tg = Math.Atan2(Gz, Gx);
            (float x, float z) OnRing(double deg) => (fx + R * (float)Math.Cos(tg + deg * Math.PI / 180.0), fz + R * (float)Math.Sin(tg + deg * Math.PI / 180.0));
            // The seat: between the fire and the glass, inside the horseshoe that opens toward the Keeper.
            float sx = fx + Gx * SeatOffset, sz = fz + Gz * SeatOffset;
            float hx = sx + Px * 0.14f, hz = sz + Pz * 0.14f; // who hands over (or blesses) stands beside the seat

            float tW = gather, tH = tW + WalkThroughSeconds, tS = tH + HandOverSeconds, tSp = tS + SitSeconds, tB = tSp + ChiefSpeech, tP = tB + BowWave, tEnd = tP + PopSeconds;

            // The chief waits at the gap's edge, walks through it to the seat, takes the band (or the blessing), sits, stands, speaks, pops.
            var ch = Add(c.a, MomentRole.Chief);
            var (gx0, gz0) = OnRing(-48);
            Put(ch, Facing(Travel(ch, GoAt(ch.key, 0f, 1f), tW, gx0, gz0), sx, sz));
            Put(ch, Step(StageBeat.WalkThrough, ActivityKind.Watch, tW, tH, gx0, gz0, sx, sz));
            int giver = 0;
            MomentMember oldM = null;
            if (old)
            {
                oldM = Add(c.b, MomentRole.OldChief);
                var (ox, oz) = OnRing(48);
                Put(oldM, Facing(Travel(oldM, GoAt(oldM.key, 0f, 1f), tW, ox, oz), sx, sz));
                Put(oldM, Facing(Step(StageBeat.Stand, ActivityKind.Watch, tW, tH, ox, oz, hx, hz), sx, sz));
                var give = Facing(Here(oldM, StageBeat.Give, ActivityKind.Give, tH, tS), sx, sz); give.partner = ch.key;
                Put(oldM, give);
                giver = oldM.key;
            }
            var take = Here(ch, StageBeat.Take, ActivityKind.Watch, tH, tS); take.partner = giver;
            Put(ch, giver != 0 ? Facing(take, hx, hz) : Glass(take));
            Put(ch, Glass(Here(ch, StageBeat.Seat, ActivityKind.SitOut, tS, tSp)));
            Put(ch, Glass(Here(ch, StageBeat.Speech, ActivityKind.Speech, tSp, tB)));
            var back = Glass(Here(ch, StageBeat.Bow, ActivityKind.Watch, tB, tP)); back.cue = BowWave - 1.2f; back.endBeat = true; // the chief bows back once the wave has gone round
            Put(ch, back);
            var pop = Glass(Here(ch, StageBeat.GoldPop, ActivityKind.Cheer, tP, tEnd)); pop.endBeat = true;
            Put(ch, pop);

            // The horseshoe: from 64° to 296° off the glass side, nearest first in the bow wave.
            var ringMembers = new List<MomentMember>();
            for (int i = 0; i < n; i++)
            {
                var p = ring[i];
                var role = c.hasOfficiant && !old && p.key == c.officiant.key ? MomentRole.Elder : p.kid ? MomentRole.Kid : MomentRole.Guest;
                var m = Add(p, role);
                var (rx, rz) = OnRing(64 + 232 * (i + 0.5) / Math.Max(1, n));
                Put(m, Facing(Travel(m, GoAt(m.key, 0f, 1f), tW, rx, rz), fx, fz));
                if (role == MomentRole.Elder)
                {
                    // No old chief: the elder steps to the seat's side and blesses the new one.
                    Put(m, Facing(Step(StageBeat.Stand, ActivityKind.Watch, tW, tH, rx, rz, hx, hz), sx, sz));
                    Put(m, Facing(Here(m, StageBeat.Bless, ActivityKind.Bless, tH, tS), sx, sz));
                    Put(m, Facing(Here(m, StageBeat.Stand, ActivityKind.Watch, tS, tB), sx, sz));
                }
                else Put(m, Facing(Here(m, StageBeat.Stand, ActivityKind.Watch, tW, tB), sx, sz));
                ringMembers.Add(m);
            }
            if (oldM != null) { Put(oldM, Facing(Here(oldM, StageBeat.Stand, ActivityKind.Watch, tS, tB), sx, sz)); ringMembers.Add(oldM); }
            // The bow wave: nearest the seat first, 0.12 s apart.
            ringMembers.Sort((a, b) =>
            {
                var (ax, az) = Last(a); var (bx, bz) = Last(b);
                float da = Dist(ax, az, sx, sz), db = Dist(bx, bz, sx, sz);
                return da != db ? da.CompareTo(db) : a.key.CompareTo(b.key);
            });
            for (int i = 0; i < ringMembers.Count; i++)
            {
                var m = ringMembers[i];
                var bow = Facing(Here(m, StageBeat.Bow, ActivityKind.Watch, tB, tP), sx, sz); bow.cue = BowGap * i;
                Put(m, bow);
                var cheer = Facing(Here(m, StageBeat.Cheer, ActivityKind.Watch, tP, tEnd), sx, sz); cheer.cue = 0.12f + 0.05f * i; cheer.endBeat = true; // the pop: a cheer runs round
                Put(m, cheer);
            }
            return true;
        }

        // ======================================================================== the festival (C3.d)

        bool Festival(MomentCast c)
        {
            float fx = geo.fireX, fz = geo.fireZ;
            var all = new List<(MomentPerson p, bool lead)>();
            if (c.hasOfficiant) all.Add((c.officiant, true));
            foreach (var g in c.grown) if (all.Count < MaxFestival) all.Add((g, false));
            foreach (var k in c.kids) if (all.Count < MaxFestival) all.Add((k, false));
            all.RemoveAll(e => !e.lead && GoAt(e.p.key, 0f, 1.5f) + e.p.walk + 0.5f > MaxGather);
            if (all.Count < 2) return Fail("too few people for a festival");
            float gather = 10f;
            foreach (var e in all) gather = Math.Max(gather, GoAt(e.p.key, 0f, 1.5f) + e.p.walk + 0.5f);
            if (gather > MaxGather) return Fail("the leader can't get to the fire in time");
            int n = all.Count;
            float R = Math.Max(0.5f, RingRadiusFor(n));
            ringRadius = R; centreX = fx; centreZ = fz;
            double tg = Math.Atan2(Gz, Gx);
            // Behind the fire, facing the Keeper and the camp, close enough to throw a log on it without a step (the hit stays a Poke's).
            float spx = fx - Gx * TossReach, spz = fz - Gz * TossReach, tox = spx, toz = spz;
            float tT = gather, tT2 = tT + TossWindSeconds, tSp = tT2 + TossSeconds, tD = tSp + FestivalSpeech, tC = tD + FestivalDance, tM = tC + ClapSeconds, tJ = tM + MealSeconds, tEnd = tJ + JumpSeconds;
            ringT0 = tD; ringT1 = tC;

            int bring = 0;
            for (int i = 0; i < n; i++)
            {
                var (p, lead) = all[i];
                var m = Add(p, lead ? MomentRole.Leader : p.kid ? MomentRole.Kid : MomentRole.Bringer);
                double a = tg + Math.PI + (i + 0.5) * 2 * Math.PI / n;
                float rx = fx + R * (float)Math.Cos(a), rz = fz + R * (float)Math.Sin(a);
                if (lead)
                {
                    // The leader brings a log for the fire, throws it on (the fire flares), and speaks from behind it.
                    Put(m, Facing(Travel(m, GoAt(m.key, 0f, 1.5f), tT, spx, spz, Prop.Log), fx, fz));
                    var wind = Facing(Step(StageBeat.TossWind, ActivityKind.Poke, tT, tT2, spx, spz, tox, toz), fx, fz); wind.prop = Prop.Log;
                    Put(m, wind);
                    var toss = Facing(Here(m, StageBeat.Toss, ActivityKind.Poke, tT2, tSp), fx, fz); toss.cue = -TossWindSeconds;
                    Put(m, toss);
                    Put(m, Glass(Step(StageBeat.Speech, ActivityKind.Speech, tSp, tD - SpeechCheer, tox, toz, spx, spz)));
                    var call = Glass(Here(m, StageBeat.Cheer, ActivityKind.Cheer, tD - SpeechCheer, tD)); call.endBeat = true; // "dance!": arms up, the speech's end beat
                    Put(m, call);
                }
                else
                {
                    // Everyone brings something: a log or a basket (the kids a basket), and sets it down at their place in the ring.
                    var prop = p.kid || (bring++ & 1) == 1 ? Prop.Basket : Prop.Log;
                    Put(m, Facing(Travel(m, GoAt(m.key, 0f, 1.5f), tT, rx, rz, prop), fx, fz));
                    var down = Facing(Here(m, StageBeat.SetDown, ActivityKind.Watch, tT, tT + SetDownSeconds + 0.6f), fx, fz); down.prop = prop; down.cue = 0.6f * Hash01(seed, m.key, 3721); // one by one, not in step down.endBeat = true; // what they brought, set down
                    Put(m, down);
                    Put(m, Facing(Here(m, StageBeat.Stand, ActivityKind.Watch, tT + SetDownSeconds + 0.6f, tSp), fx, fz));
                    Put(m, Facing(Here(m, StageBeat.Listen, ActivityKind.Listen, tSp, tD), spx, spz));
                }
                ringKeys.Add(m.key);
            }
            SortRingByAngle(fx, fz);
            foreach (var m in members)
            {
                int r = ringKeys.IndexOf(m.key);
                var dance = Step(StageBeat.Ring, ActivityKind.Dance, tD, tC, fx, fz, fx, fz); dance.ring = r; Put(m, dance);
                var clap = Facing(Step(StageBeat.Clap, ActivityKind.Clap, tC, tM, fx, fz, fx, fz), fx, fz); clap.ring = r; clap.endBeat = true; Put(m, clap); // the ring stops: everyone claps (B2.e)
                var eat = Facing(Step(StageBeat.Eat, ActivityKind.Eat, tM, tJ, fx, fz, fx, fz), fx, fz); eat.ring = r; eat.ringScale = 0.85f; Put(m, eat);
                var jump = Facing(Step(StageBeat.Jump, ActivityKind.Cheer, tJ, tEnd, fx, fz, fx, fz), fx, fz); jump.ring = r; jump.ringScale = 0.85f; jump.cue = 0.25f; jump.endBeat = true;
                Put(m, jump); // in step on purpose: the whole camp jumps once (P6 exempt)
            }
            return true;
        }
    }
}
