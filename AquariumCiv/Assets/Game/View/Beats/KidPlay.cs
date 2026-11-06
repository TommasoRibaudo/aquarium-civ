// #358 Kids' play 1 (docs/design/animation-specs.md B2): the play group's games as pure functions of (seed, members, time), on the beat
// kit (#355). Pure (Mathf, Vector2/3, Quaternion, DollMotion and Life types only), so Tools/AnimBench steps exactly this code.
// The owner (View/KidPlayDirector.cs in the game, the bench's Simulator) asks each member's cue, draws the pose over the cue's loop,
// stands the doll on cue.pos facing cue.face, and draws the ball from Ball(t).
// B3 (#359) adds its games (splash, woods, hide and seek) as more KidGame subclasses and a case in KidGame.Create.
using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>What one member of a play group does now: role, where it stands, what it faces, the move it shows and its beat clock.</summary>
    public struct PlayCue
    {
        public GroupRole role;
        /// <summary>The move the doll is seen doing (principle 6, HitFx, the blend), and the loop pose under the beats (Run, Walk, Stand, Sit, ...).</summary>
        public DollStyle style, under;
        /// <summary>Where it stands (box x, z) and the point it faces.</summary>
        public Vector2 pos, face;
        /// <summary>The planned ground speed (u/s).</summary>
        public float speed;
        public int round;
        /// <summary>Seconds into the member's beat (negative: not yet).</summary>
        public float since;
        /// <summary>Whom this member's contact lands on: a member index, <see cref="KidGame.Herd"/>, <see cref="KidGame.Release"/> or <see cref="KidGame.NoHit"/>.</summary>
        public int hitOn;
        /// <summary>The ball is in its arms (the ball game).</summary>
        public bool holdsBall;
        /// <summary>The payoff of the round (P5): a touch held, a catch, a pick-up, the laugh, the plop, the cheer.</summary>
        public bool endBeat;
        /// <summary>Moves in step with the others on purpose (the ring dance, its cheer: exempt from P6).</summary>
        public bool inStep;
        /// <summary>#359: the feet this far (box units) below the water's surface (wading at the pond, jumped in); 0 on the ground.</summary>
        public float sink;
        /// <summary>#359: a stick in the hand (the stick duel in the woods).</summary>
        public bool stick;
    }

    /// <summary>The group's ball now: in someone's arms, or free (flying, bouncing, rolling, lying).</summary>
    public struct BallState
    {
        /// <summary>The member holding it, or −1 when it is free.</summary>
        public int holder;
        /// <summary>The throw it is on (free) or was last on.</summary>
        public int shotIndex;
        public ArcShot shot;
        /// <summary>Free: where it is (box x, z; y above the flat ground) and how it is turned.</summary>
        public Vector3 position;
        public Quaternion rotation;
        public ArcStage stage;
        public bool Free => holder < 0;
    }

    /// <summary>Loop poses for the play styles when they play outside a group (DollMotion.Evaluate's Toddle and RingDance).</summary>
    public static class KidPlayPoses
    {
        public static void Loop(DollStyle style, ref DollPose p, float t)
        {
            if (style == DollStyle.Toddle) Toddle(ref p, t);
            else Ring(ref p, t, false);
        }

        /// <summary>B2.d: roll ±10° at 2.5 Hz, a bounce, arms out for balance. (The spec's 0.01 u bounce is under a pixel: 0.02.)</summary>
        public static void Toddle(ref DollPose p, float t)
        {
            float w = Mathf.Sin(t * 2.5f * Mathf.PI * 2f);
            p.roll = w * 10f;
            p.offset.y = Mathf.Abs(w) * 0.02f;
            p.lean = 4f;
            p.armL = new Vector3(10f, 0f, -70f); p.armR = new Vector3(10f, 0f, 70f);
        }

        /// <summary>B2.e: arms out to the side so hands meet (z ∓95), a hop on each 0.5 s beat; <paramref name="up"/>: the step in, arms up and a cheer bounce.</summary>
        public static void Ring(ref DollPose p, float beatClock, bool up)
        {
            float hop = Mathf.Abs(Mathf.Sin(beatClock * Mathf.PI / DanceRingGame.BeatSeconds));
            p.offset.y = hop * (up ? 0.035f : 0.025f);
            p.armL = new Vector3(0f, 0f, up ? -160f : -95f); p.armR = new Vector3(0f, 0f, up ? 160f : 95f);
            p.lean = up ? -6f : 0f;
            p.roll = 0f;
        }
    }

    /// <summary>
    /// One play group's game (§0.7): a shared clock from <see cref="t0"/>, roles, and a cue per member at any time. A pure function of
    /// (seed, members, t) and, for the rabbit chase, where the owner says the rabbits are: replaying a moment replays it exactly.
    /// </summary>
    public abstract class KidGame
    {
        public const int NoHit = -1, Herd = -2, Release = -3;
        /// <summary>#359: the contact lands on the water (a splash, a jump in) or on the game's tree (a hand slapped on the trunk).</summary>
        public const int Water = -4, TreeHit = -5;
        /// <summary>The peg doll's build (PegDoll; copied: PegDoll builds meshes, so it stays out of the pure kit).</summary>
        public const float KidScale = 0.68f, BodyH = 0.13f, ShoulderX = 0.046f, ArmLen = 0.075f;

        public readonly PlayGame game;
        public readonly int seed;
        /// <summary>When the group started (the owner's clock, seconds).</summary>
        public readonly float t0;
        public readonly Vector2 centre;
        protected readonly int[] keys;
        /// <summary>Per member: how big the doll is drawn (doll scale × zoom), for where its hands are. The owner keeps it current.</summary>
        public readonly float[] scales;

        protected KidGame(PlayGame game, int seed, float t0, IReadOnlyList<int> members, Vector2 centre)
        {
            this.game = game; this.seed = seed; this.t0 = t0; this.centre = centre;
            int n = members?.Count ?? 0;
            keys = new int[n]; scales = new float[n];
            for (int i = 0; i < n; i++) { keys[i] = members[i]; scales[i] = KidScale; }
        }

        public int Count => keys.Length;
        public int KeyOf(int i) => keys[i];
        public int IndexOf(int key) { for (int i = 0; i < keys.Length; i++) if (keys[i] == key) return i; return -1; }

        /// <summary>Member <paramref name="i"/>'s cue at <paramref name="t"/> (the owner's clock).</summary>
        public abstract PlayCue Cue(int i, float t);
        /// <summary>The beats over the cue's loop pose.</summary>
        public virtual void Pose(int i, float t, in PlayCue c, ref DollPose p) { }
        public virtual bool HasBall => false;
        /// <summary>#359: the puffs and shakes due in (<paramref name="from"/>, <paramref name="to"/>] (the owner's clock), each once (KidPlay2.cs).</summary>
        public virtual void Effects(float from, float to, List<PlayFx> into) { }
        public virtual BallState Ball(float t) => new BallState { holder = -1, stage = ArcStage.Gone };

        /// <summary>The whole pose: the cue's loop (<see cref="DollMotion.Evaluate"/>, its run cycle on the doll's own stride) and the beats on top.</summary>
        public DollPose PoseOf(int i, float t, float phase, float stride, float speed, out PlayCue c)
        {
            c = Cue(i, t);
            if (c.inStep) phase = t - t0; // the ring and its cheer move together (exempt from P6)
            var p = DollMotion.Evaluate(c.under, phase, 1f, 0, 1, c.holdsBall ? PropSlot.Arms : PropSlot.Hand, c.holdsBall, speed, 0f, Mathf.Max(0f, c.since), 0, stride);
            Pose(i, t, c, ref p);
            if (c.hitOn == NoHit) p.hit = 0f;
            return p;
        }

        /// <summary>The game for a plan; the B3 games (#359) play on the real things in <paramref name="site"/>.</summary>
        public static KidGame Create(PlayGame g, int seed, float t0, IReadOnlyList<int> members, Vector2 centre, PlaySite site = null) => g switch
        {
            PlayGame.Splash => new SplashGame(seed, t0, members, centre, site),
            PlayGame.Woods => new WoodsGame(seed, t0, members, centre, site),
            PlayGame.HideSeek => new HideSeekGame(seed, t0, members, centre, site),
            PlayGame.Snowball => new SnowballGame(seed, t0, members, centre),
            PlayGame.Tag => new TagGame(seed, t0, members, centre),
            PlayGame.Ball => new BallGame(seed, t0, members, centre),
            PlayGame.ChaseRabbits => new RabbitChaseGame(seed, t0, members, centre),
            PlayGame.Toddle => new ToddleGame(seed, t0, members, centre),
            PlayGame.DanceRing => new DanceRingGame(seed, t0, members, centre),
            _ => null,
        };

        // ---------------------------------------------------------------- helpers

        /// <summary>PeopleView.Yaw: the yaw (degrees) that faces from one point to another (the doll's front is local −z).</summary>
        public static float YawTo(Vector2 from, Vector2 to) { var d = to - from; return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 180f; }

        /// <summary>The point between a doll's two hands (box x, z; y above its feet), as PegDollView places the arms (zoom included in <paramref name="s"/>).</summary>
        public static Vector3 HandsMid(Vector2 pos, float yawDeg, in DollPose p, float s)
        {
            var yawQ = Quaternion.Euler(0f, yawDeg + p.yaw, 0f);
            var rot = yawQ * Quaternion.Euler(-p.lean, 0f, p.roll);
            var root = new Vector3(pos.x, 0f, pos.y) + yawQ * (p.offset * s);
            return (Hand(root, rot, -1f, p.armL, p.squash, s) + Hand(root, rot, 1f, p.armR, p.squash, s)) * 0.5f;
        }

        static Vector3 Hand(Vector3 root, Quaternion rot, float side, Vector3 arm, float squash, float s)
        {
            var local = new Vector3(side * ShoulderX * s, BodyH * 0.92f * s, 0f) + Quaternion.Euler(arm) * new Vector3(0f, -ArmLen * s, 0f);
            return root + rot * new Vector3(local.x, local.y * squash, local.z);
        }

        protected static Vector2 Dir(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        protected static Vector2 Rotate(Vector2 v, float rad) { float c = Mathf.Cos(rad), s = Mathf.Sin(rad); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        protected static float Local01(float x, float a, float b) => b > a ? Mathf.Clamp01((x - a) / (b - a)) : (x >= b ? 1f : 0f);

        /// <summary>A planned speed from a path: how far it goes in the next 0.05 s.</summary>
        protected static float SpeedOf(Func<float, Vector2> at, float t) => (at(t + 0.05f) - at(t)).magnitude / 0.05f;
    }

    // ==================================================================== B2.a Tag
    /// <summary>
    /// Tag (B2.a) on the kit's <see cref="ChaseGroup"/>: rounds of <see cref="RoundSeconds"/>. The new chaser spins once and bounces twice
    /// (the wind-up), runs at its runner with its arms out, and its right arm shoots forward on the touch (the Contact: <c>hit</c>, the
    /// runner jolts back as in a Flinch); both hold, looking at each other. The others run off waving and taunt; the old chaser runs off
    /// arms up, laughing. After 3–5 tags everyone drops onto the grass and sits panting (the end beat), then a new set starts.
    /// </summary>
    public sealed class TagGame : KidGame
    {
        public const float WindUp = 0.9f, Spin = 0.4f, Rest = 2.6f, SitFor = 1.9f, Radius = 0.75f, Flee = 0.6f, Zig = 0.05f, ZigPeriod = 1f;
        public readonly float RoundSeconds;
        public readonly int TagsPerSet;
        readonly ChaseGroup chase;

        /// <summary>The chaser's touch: the arm out (0.1 s, Snap), the hit, a held look (0.25 s), back.</summary>
        static readonly Beat Touch = new Beat(
            Beat.Step(BeatPhase.Act, 0.1f, Ease.Snap, PoseKey.Hold.ArmR(95f, 0f, 8f).ArmL(40f, 0f, -14f).Lean(20f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.25f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Base));

        /// <summary>The touched runner's answer: jolt back 0.04 u, squash 0.9, arms up to the chest (the Flinch keys), held, back.</summary>
        static readonly Beat Tagged = new Beat(
            Beat.Step(BeatPhase.Act, 0.12f, Ease.Out, PoseKey.Hold.Offset(0f, 0f, 0.04f).Squash(0.9f).Arms(70f, 30f).Lean(-10f)),
            Beat.Step(BeatPhase.Hold, 0.45f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.3f, Ease.Smooth, PoseKey.Base));

        public TagGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre) : base(PlayGame.Tag, seed, t0, members, centre)
        {
            RoundSeconds = BeatHash.Range(4.6f, 5.6f, seed, 1, 358);
            TagsPerSet = 3 + BeatHash.Pick(3, seed, 2, 358);
            chase = new ChaseGroup(seed, t0, members, centre, Radius, RoundSeconds, Flee);
        }

        public float SetSeconds => TagsPerSet * RoundSeconds + Rest;
        public float RunSeconds => ChaseGroup.ContactShare * RoundSeconds;
        public int ChaserOf(int round) => chase.ChaserOf(round);
        public int TargetOf(int round) => chase.TargetOf(round);

        /// <summary>The round at <paramref name="t"/>, the seconds into it, and whether the set's rest is on (then: seconds into the rest).</summary>
        public void Where(float t, out int round, out float local, out bool resting)
        {
            float s = Mathf.Max(0f, t - t0);
            int set = Mathf.FloorToInt(s / SetSeconds);
            float w = s - set * SetSeconds;
            float play = TagsPerSet * RoundSeconds;
            if (w < play)
            {
                int k = Mathf.Min(TagsPerSet - 1, Mathf.FloorToInt(w / RoundSeconds));
                round = set * TagsPerSet + k; local = w - k * RoundSeconds; resting = false;
            }
            else { round = set * TagsPerSet + TagsPerSet - 1; local = w - play; resting = true; }
        }

        /// <summary>When the touch of round <paramref name="r"/> is (the owner's clock).</summary>
        public float ContactAt(int r)
        {
            int set = r / TagsPerSet, k = r % TagsPerSet;
            return t0 + set * SetSeconds + k * RoundSeconds + RunSeconds;
        }

        float ChaseTime(int r, float local) => t0 + r * RoundSeconds + local;

        /// <summary>The chaser waits out its wind-up, then runs the kit's path in what is left of the run, arriving on the touch.</summary>
        float ChaserLocal(float local)
        {
            float run = RunSeconds;
            if (local < WindUp) return 0f;
            if (local >= run) return local;
            return (local - WindUp) * run / (run - WindUp);
        }

        Vector2 PosAt(int i, int r, float local)
        {
            int c = chase.ChaserOf(r);
            var p = chase.Cue(i, ChaseTime(r, i == c ? ChaserLocal(local) : local)).target;
            float run = RunSeconds;
            if (i != c && local > 0f && local < run)
            {
                // Runners zigzag (about ±25°, every half second), never off their path at the start or on the touch.
                var d = chase.Cue(i, ChaseTime(r, run)).target - chase.Cue(i, ChaseTime(r, 0f)).target;
                if (d.sqrMagnitude > 1e-4f)
                {
                    var perp = new Vector2(-d.y, d.x).normalized;
                    p += perp * (Zig * Mathf.Sin(local / run * Mathf.PI) * Mathf.Sin(local * Mathf.PI * 2f / ZigPeriod + i * 1.7f));
                }
            }
            return p;
        }

        public override PlayCue Cue(int i, float t)
        {
            Where(t, out int r, out float local, out bool resting);
            var cue = new PlayCue { round = r, hitOn = NoHit, under = DollStyle.Stand };
            if (resting)
            {
                // The end beat: everyone flops down where the last tag left them, panting, then gets up.
                cue.pos = chase.Cue(i, ChaseTime(r, RoundSeconds - 1e-3f)).target;
                cue.face = centre;
                cue.role = GroupRole.Runner;
                bool sits = local < SitFor;
                cue.under = sits ? DollStyle.Sit : DollStyle.Stand;
                cue.style = sits ? DollStyle.Sit : DollStyle.Stand;
                cue.since = local;
                cue.endBeat = true;
                return cue;
            }
            int ch = chase.ChaserOf(r), g = chase.TargetOf(r), old = r % TagsPerSet > 0 ? chase.ChaserOf(r - 1) : -1;
            float run = RunSeconds;
            var raw = chase.Cue(i, ChaseTime(r, i == ch ? ChaserLocal(local) : local));
            cue.pos = PosAt(i, r, local);
            cue.face = raw.face;
            cue.speed = SpeedOf(x => PosAt(i, r, x), local);
            bool moving = local < run && cue.speed > 0.02f;
            cue.under = moving ? DollStyle.Run : DollStyle.Stand;
            cue.since = local - run; // 0 on the touch
            cue.endBeat = local >= run; // everyone sees the touch: the round's payoff
            if (i == ch)
            {
                cue.role = GroupRole.Chaser;
                cue.style = DollStyle.PlayChaser;
                cue.hitOn = g != ch ? g : NoHit;
                cue.face = PosAt(g, r, local);
                if (local < WindUp) cue.under = DollStyle.Stand;
            }
            else if (i == g)
            {
                cue.role = GroupRole.Runner;
                cue.style = local >= run && local < run + 0.6f ? DollStyle.Flinch : DollStyle.PlayRunner;
                if (local >= run) cue.face = PosAt(ch, r, local);
            }
            else
            {
                cue.role = GroupRole.Runner;
                cue.style = DollStyle.PlayTaunt;
                if (local >= run) cue.face = PosAt(ch, r, local);
            }
            if (i == old && local < 1.6f) cue.round = -r - 1; // marks the old chaser for Pose: running off, arms up, laughing
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            if (c.endBeat && c.under == DollStyle.Sit && c.role == GroupRole.Runner && c.style == DollStyle.Sit)
            {
                // Panting on the grass (B2.a end beat): a quick small heave, leaning back on the arms.
                p.offset.y += Mathf.Abs(Mathf.Sin(c.since * 3f * Mathf.PI)) * 0.012f;
                p.armL = new Vector3(-30f, 0f, -30f); p.armR = new Vector3(-30f, 0f, 30f);
                p.lean = -8f;
                return;
            }
            if (c.endBeat && c.under == DollStyle.Stand && c.style == DollStyle.Stand) return; // getting up after the rest
            Where(t, out int r, out float local, out _);
            float tau = Mathf.PI * 2f;
            switch (c.role)
            {
                case GroupRole.Chaser:
                    if (local < WindUp)
                    {
                        // The new "it": spins once, bounces twice on the spot.
                        p.yaw += 360f * Easing.Apply(Ease.Smooth, local / Spin);
                        if (local >= Spin) p.offset.y += Mathf.Abs(Mathf.Sin((local - Spin) / 0.25f * Mathf.PI)) * 0.03f;
                        p.armL = new Vector3(20f, 0f, -60f); p.armR = new Vector3(20f, 0f, 60f);
                    }
                    else if (c.since < -Touch[0].seconds)
                    {
                        // Running with both arms out in front, grabbing.
                        float w = Mathf.Sin(t * 9f);
                        p.armL = new Vector3(75f + w * 14f, 0f, -12f); p.armR = new Vector3(75f - w * 14f, 0f, 12f);
                        p.lean = 16f;
                    }
                    else Touch.Apply(ref p, c.since + Touch[0].seconds);
                    break;
                case GroupRole.Runner:
                    if (TargetIs(i, r) && c.since >= 0f)
                        Tagged.Apply(ref p, c.since);
                    else if (c.round < 0)
                    {
                        // The old chaser runs off, arms up, laughing.
                        p.armL = new Vector3(0f, 0f, -150f); p.armR = new Vector3(0f, 0f, 150f);
                        p.offset.y += Mathf.Abs(Mathf.Sin(t * 9f * tau * 0.5f)) * 0.015f;
                        p.lean = 6f;
                    }
                    else if (c.style == DollStyle.PlayTaunt)
                    {
                        // Not chased: one arm up, waving (6 Hz), and a little hop once it has stopped.
                        float w = Mathf.Sin(t * 6f * tau);
                        p.armL = new Vector3(0f, 0f, -155f + w * 20f);
                        if (c.under == DollStyle.Stand) p.offset.y += Mathf.Abs(Mathf.Sin(t * 3f * tau * 0.5f)) * 0.02f;
                    }
                    else
                    {
                        // The one being chased: arms flung high and wide, looking back over its shoulder (a lean back).
                        float w = Mathf.Sin(t * 9f);
                        p.armL = new Vector3(30f + w * 30f, 0f, -70f); p.armR = new Vector3(30f - w * 30f, 0f, 70f);
                        p.lean = 4f;
                    }
                    break;
            }
        }

        bool TargetIs(int i, int r) => chase.TargetOf(r) == i;
    }

    // ==================================================================== B2.b Ball
    /// <summary>
    /// The ball (B2.b): 2–3 kids stand in a triangle (or a pair) and toss a big ball. The thrower squashes, swings both arms back, then
    /// forward (a two-handed underhand toss: peg dolls have no legs), the ball flies on a kit arc from its hands to the catcher's arms; the
    /// catcher lifts its arms as it comes, catches (squash, a step back) and holds it. One in four lands past the catcher, bounces twice
    /// and rolls to a stop; the nearest kid runs, bends, picks it up and runs back. Every 6–9 throws the holder holds the ball up high
    /// (the end beat). Alone, a kid tosses it ahead and runs after it. The ball is the group's: the owner draws it from <see cref="Ball"/>.
    /// </summary>
    public sealed class BallGame : KidGame
    {
        public const float Antic = 0.3f, Act = 0.12f, Speed = 0.6f, BallR = 0.035f, RunSpeed = 0.5f, Pick = 0.45f, Turn = 0.45f,
            HoldUp = 0.9f, CatchHold = 0.25f, Side = 0.42f, SoloThrow = 0.4f, FirstReady = 0.6f, MissShare = 0.25f;

        /// <summary>The toss: wind up (arms back, squash, lean back), swing through (a hop), release (the Contact), hold, back.</summary>
        static readonly Beat Toss = new Beat(
            Beat.Step(BeatPhase.Antic, Antic, Ease.Out, PoseKey.Hold.Squash(0.85f).Lean(-8f).Arms(-30f, 10f)),
            Beat.Step(BeatPhase.Act, Act, Ease.Snap, PoseKey.Hold.Squash(1f).Lean(12f).Offset(0f, 0.03f, 0f).Arms(80f, 10f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold.Arms(110f, 10f)),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold.Offset(0f, 0f, 0f)),
            Beat.Step(BeatPhase.Settle, 0.4f, Ease.Smooth, PoseKey.Base));

        /// <summary>The catch, from the moment it lands: squash, a step back, the arms round it; held; up again.</summary>
        static readonly Beat Catch = new Beat(
            Beat.Step(BeatPhase.Act, 0.1f, Ease.Out, PoseKey.Hold.Squash(0.85f).Offset(0f, 0f, 0.03f).Arms(62f, 4f).Lean(-4f)),
            Beat.Step(BeatPhase.Hold, CatchHold, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.3f, Ease.Smooth, PoseKey.Base));

        /// <summary>The release pose (for where the ball leaves the hands).</summary>
        static DollPose ReleasePose
        {
            get { var p = DollPose.Rest; p.lean = 12f; p.offset = new Vector3(0f, 0.03f, 0f); p.armL = new Vector3(80f, 0f, -10f); p.armR = new Vector3(80f, 0f, 10f); return p; }
        }

        /// <summary>The hug pose (where the catcher's arms close round it).</summary>
        static DollPose HugPose
        {
            get { var p = DollPose.Rest; p.armL = new Vector3(62f, 0f, -4f); p.armR = new Vector3(62f, 0f, 4f); return p; }
        }

        public struct Throw
        {
            public int from, to, fetcher, holderAfter;
            public bool miss, holdUp;
            public Vector2 fromPos, aim, fetchTo, restPos;
            public float ready, launch, land, runStart, arrive, pickStart, pickEnd, backAt, holdUpAt, next;
            public ArcShot shot;
            public Vector2[] spots;
        }

        readonly List<Throw> throws = new();
        readonly Vector2[] home;
        readonly int holdUpEvery;
        int firstHolder;

        public BallGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre) : base(PlayGame.Ball, seed, t0, members, centre)
        {
            int n = Count;
            home = new Vector2[n];
            float a0 = BeatHash.U01(seed, 3, 358) * Mathf.PI * 2f;
            float side = BeatHash.Range(0.36f, 0.48f, seed, 4, 358);
            float ring = n == 3 ? side / Mathf.Sqrt(3f) : n == 2 ? side * 0.5f : 0f;
            for (int i = 0; i < n; i++) home[i] = centre + Dir(a0 + i * Mathf.PI * 2f / Mathf.Max(1, n)) * ring;
            holdUpEvery = 6 + BeatHash.Pick(4, seed, 5, 358);
            firstHolder = BeatHash.Pick(n, seed, 6, 358);
        }

        public override bool HasBall => true;
        public Vector2 HomeOf(int i) => home[i];

        /// <summary>Throw <paramref name="k"/> (grown on demand, kept).</summary>
        public Throw ThrowAt(int k) { Grow(k); return throws[k]; }

        /// <summary>The throw on at game-local seconds <paramref name="local"/> (the last one whose ready has come; the first before it starts).</summary>
        public int ThrowIndexAt(float local)
        {
            int k = 0;
            Grow(0);
            while (true)
            {
                Grow(k + 1);
                if (throws[k + 1].ready > local) return k;
                k++;
            }
        }

        void Grow(int k)
        {
            int n = Count;
            while (throws.Count <= k)
            {
                int j = throws.Count;
                var prev = j > 0 ? throws[j - 1] : default;
                var spots = j > 0 ? (Vector2[])prev.spots.Clone() : (Vector2[])home.Clone();
                if (j > 0 && prev.miss && n == 1) spots[prev.fetcher] = prev.fetchTo; // alone: throws again from where it picked the ball up
                var th = new Throw { spots = spots, ready = j > 0 ? prev.next : FirstReady, fetcher = -1 };
                th.from = j > 0 ? prev.holderAfter : firstHolder;
                th.fromPos = spots[th.from];
                if (n == 1)
                {
                    // Alone: toss it ahead, turned back toward the middle when it has wandered off.
                    var off = th.fromPos - centre;
                    float a = off.magnitude > 0.25f ? Mathf.Atan2(-off.y, -off.x) : BeatHash.U01(seed, j, 21) * Mathf.PI * 2f;
                    a += BeatHash.Range(-1.1f, 1.1f, seed, j, 22);
                    th.to = -1;
                    th.aim = th.fromPos + Dir(a) * SoloThrow;
                    th.miss = true;
                }
                else
                {
                    if (n == 2) th.to = 1 - th.from;
                    else
                    {
                        int a = (th.from + 1) % n, b = (th.from + 2) % n;
                        int lastFrom = j > 0 ? prev.from : -1;
                        bool preferA = a != lastFrom; // throw on to the one who didn't just throw, most of the time
                        th.to = BeatHash.U01(seed, j, 23) < 0.7f ? (preferA ? a : b) : (preferA ? b : a);
                    }
                    th.aim = spots[th.to];
                    th.miss = BeatHash.U01(seed, j, 24) < MissShare;
                }
                var dir = (th.aim - th.fromPos).normalized;
                if (dir.sqrMagnitude < 1e-6f) dir = new Vector2(1f, 0f);
                th.launch = th.ready + Antic + Act;
                var start = HandsMid(th.fromPos, YawTo(th.fromPos, th.aim), ReleasePose, scales[th.from]);
                Vector3 end;
                if (th.miss)
                {
                    var past = th.to >= 0 ? th.aim + dir * BeatHash.Range(0.15f, 0.3f, seed, j, 25) : th.aim;
                    end = new Vector3(past.x, BallR, past.y);
                }
                else end = HandsMid(th.aim, YawTo(th.aim, th.fromPos), HugPose, scales[th.to]);
                float dist = new Vector2(end.x - start.x, end.z - start.z).magnitude;
                float apex = Mathf.Clamp(0.25f * dist / 0.45f, 0.12f, 0.3f);
                th.shot = ArcShot.Throw(start, end, apex, Arc.FlightFor(dist, Speed), th.miss ? Landing.BounceRoll : Landing.Catch, th.launch, 0f, BallR);
                th.land = th.shot.LandAt;
                if (!th.miss)
                {
                    th.holderAfter = th.to;
                    th.next = Mathf.Max(th.land + CatchHold + 0.1f + Turn, th.ready + BeatHash.Range(2.2f, 3.0f, seed, j, 26));
                }
                else
                {
                    float restAt = th.land + Arc.SettleSeconds(th.shot);
                    var rest = Arc.Sample(th.shot, restAt).position;
                    th.restPos = new Vector2(rest.x, rest.z);
                    // The nearest kid fetches it (alone: the thrower).
                    int f = 0; float best = float.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        if (n > 1 && i == th.from) continue;
                        float d = (spots[i] - th.restPos).sqrMagnitude;
                        if (d < best) { best = d; f = i; }
                    }
                    th.fetcher = f;
                    var toBall = th.restPos - spots[f];
                    float m = toBall.magnitude;
                    th.fetchTo = m > 0.07f ? th.restPos - toBall / m * 0.07f : spots[f];
                    th.runStart = th.land + 0.25f;
                    th.arrive = th.runStart + (th.fetchTo - spots[f]).magnitude / RunSpeed;
                    th.pickStart = Mathf.Max(th.arrive, restAt - 0.1f);
                    th.pickEnd = th.pickStart + Pick;
                    th.backAt = n == 1 ? th.pickEnd : th.pickEnd + (th.fetchTo - spots[f]).magnitude / RunSpeed;
                    th.holderAfter = f;
                    th.next = th.backAt + Turn;
                }
                th.holdUp = (j + 1) % holdUpEvery == 0;
                if (th.holdUp) { th.holdUpAt = th.next; th.next += HoldUp + 0.3f; }
                throws.Add(th);
            }
        }

        /// <summary>Where member <paramref name="i"/> stands during throw <paramref name="th"/> at <paramref name="local"/> (the fetch is a run there and back).</summary>
        Vector2 PosIn(in Throw th, int i, float local)
        {
            var spot = th.spots[i];
            if (!th.miss || i != th.fetcher) return spot;
            if (local < th.runStart) return spot;
            if (local < th.arrive) return Vector2.Lerp(spot, th.fetchTo, Local01(local, th.runStart, th.arrive));
            if (local < th.pickEnd || Count == 1) return th.fetchTo;
            return Vector2.Lerp(th.fetchTo, spot, Local01(local, th.pickEnd, th.backAt));
        }

        public override PlayCue Cue(int i, float t)
        {
            float local = Mathf.Max(0f, t - t0);
            int k = ThrowIndexAt(local);
            var th = throws[k];
            var ball = Ball(t);
            var cue = new PlayCue { round = k, hitOn = NoHit, under = DollStyle.Stand, style = DollStyle.Stand, role = GroupRole.Watcher, since = local - th.ready };
            cue.pos = PosIn(th, i, local);
            int kk = k;
            cue.speed = SpeedOf(x => PosIn(throws[kk], i, x), local);
            cue.face = ball.Free ? new Vector2(ball.position.x, ball.position.z) : ball.holder != i ? PosIn(th, ball.holder, local) : th.aim;
            cue.holdsBall = ball.holder == i;
            if (local < th.ready) { cue.face = th.aim; return cue; } // before the first throw: the first holder, ready
            if (i == th.from && local < th.launch + 0.66f)
            {
                cue.role = GroupRole.Thrower;
                cue.style = DollStyle.PlayThrow;
                cue.face = th.aim;
                cue.hitOn = Release;
                cue.endBeat = local >= th.launch; // let go: it watches the ball fly
            }
            else if (!th.miss && i == th.to && local >= th.launch)
            {
                cue.role = GroupRole.Catcher;
                cue.style = local < th.land + CatchHold + 0.4f ? DollStyle.PlayCatch : DollStyle.Stand;
                cue.face = th.fromPos;
                cue.since = local - th.land;
                cue.endBeat = local >= th.land;
                if (local >= th.land + CatchHold + 0.1f) cue.face = NextAim(k); // turns to whom it throws to next
            }
            else if (th.miss && th.to >= 0 && i == th.to && i != th.fetcher && local >= th.launch && local < th.land + 0.5f)
            {
                cue.role = GroupRole.Catcher; // reached for it and missed
                cue.style = DollStyle.PlayCatch;
                cue.face = th.fromPos;
                cue.since = local - th.land;
            }
            if (th.miss && i == th.fetcher && local >= th.launch)
            {
                cue.role = GroupRole.Fetcher;
                cue.since = local - th.land;
                if (local < th.runStart) { cue.style = i == th.to ? DollStyle.PlayCatch : DollStyle.Stand; cue.face = i == th.to ? th.fromPos : cue.face; }
                else if (local < th.arrive) { cue.style = DollStyle.PlayCatch; cue.under = DollStyle.Run; cue.face = th.restPos; }
                else if (local < th.pickEnd) { cue.style = DollStyle.PlayCatch; cue.face = th.restPos; cue.endBeat = true; }
                else if (local < th.backAt) { cue.style = DollStyle.Run; cue.under = DollStyle.Run; cue.face = th.spots[i]; }
                else { cue.style = DollStyle.Stand; cue.face = NextAim(k); }
                if (i == th.from && local < th.launch + 0.66f && local < th.runStart) { cue.role = GroupRole.Thrower; cue.style = DollStyle.PlayThrow; cue.hitOn = Release; cue.endBeat = local >= th.launch; cue.face = th.aim; cue.since = local - th.ready; }
            }
            if (th.holdUp && i == th.holderAfter && local >= th.holdUpAt && local < th.holdUpAt + HoldUp)
            {
                cue.role = GroupRole.Cheerer;
                cue.style = DollStyle.HoldUp;
                cue.under = DollStyle.Stand;
                cue.since = local - th.holdUpAt;
                cue.endBeat = true;
            }
            return cue;
        }

        Vector2 NextAim(int k) { Grow(k + 1); return throws[k + 1].aim; }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            float local = Mathf.Max(0f, t - t0);
            var th = throws[Mathf.Clamp(c.round, 0, throws.Count - 1)];
            switch (c.style)
            {
                case DollStyle.PlayThrow:
                    Toss.Apply(ref p, c.since);
                    break;
                case DollStyle.PlayCatch:
                    if (c.role == GroupRole.Fetcher && local >= th.arrive && local < th.pickEnd)
                    {
                        // Bends to pick it up (Lift's bend), straightens with it.
                        float u = Local01(local, th.pickStart - 0.1f, th.pickEnd);
                        float down = Mathf.Sin(u * Mathf.PI);
                        p.lean = Mathf.Lerp(p.lean, 34f, down);
                        p.squash = Mathf.Lerp(1f, 0.9f, down);
                        p.armL = Vector3.Lerp(p.armL, new Vector3(40f, 0f, -8f), down);
                        p.armR = Vector3.Lerp(p.armR, new Vector3(40f, 0f, 8f), down);
                    }
                    else if (c.role == GroupRole.Fetcher && local < th.arrive && local >= th.runStart) { p.lean = 14f; }
                    else if (c.since < 0f)
                    {
                        // Arms up as it comes (from the top of its flight): anticipation.
                        float up = Easing.Apply(Ease.Smooth, Local01(local, th.launch + (th.land - th.launch) * 0.5f - 0.15f, th.land - 0.05f));
                        p.armL = Vector3.Lerp(p.armL, new Vector3(120f, 0f, -12f), up);
                        p.armR = Vector3.Lerp(p.armR, new Vector3(120f, 0f, 12f), up);
                        p.lean = Mathf.Lerp(p.lean, -6f, up);
                    }
                    else if (!th.miss) Catch.Apply(ref p, c.since);
                    else
                    {
                        // Missed: arms drop, a little slump.
                        float u = Easing.Apply(Ease.Out, Mathf.Clamp01(c.since / 0.3f));
                        p.armL = Vector3.Lerp(new Vector3(120f, 0f, -12f), new Vector3(20f, 0f, -30f), u);
                        p.armR = Vector3.Lerp(new Vector3(120f, 0f, 12f), new Vector3(20f, 0f, 30f), u);
                    }
                    break;
                case DollStyle.HoldUp:
                {
                    // The end beat: the ball held up high in both hands.
                    float u = Easing.Apply(Ease.Back, Mathf.Clamp01(c.since / 0.25f));
                    p.armL = Vector3.LerpUnclamped(new Vector3(62f, 0f, -4f), new Vector3(170f, 0f, -10f), u);
                    p.armR = Vector3.LerpUnclamped(new Vector3(62f, 0f, 4f), new Vector3(170f, 0f, 10f), u);
                    p.lean = -10f;
                    p.offset.y = Mathf.Abs(Mathf.Sin(c.since * 3f * Mathf.PI * 2f)) * 0.015f;
                    break;
                }
            }
        }

        public override BallState Ball(float t)
        {
            float local = Mathf.Max(0f, t - t0);
            int k = ThrowIndexAt(local);
            var th = throws[k];
            var b = new BallState { shotIndex = k, shot = th.shot, holder = th.from, stage = ArcStage.Resting };
            if (local < th.launch) return b;
            if (local < th.land || (th.miss && local < th.pickEnd - Pick * 0.4f))
            {
                var s = Arc.Sample(th.shot, local);
                b.holder = -1; b.position = s.position; b.rotation = s.rotation; b.stage = s.stage;
                return b;
            }
            b.holder = th.holderAfter;
            return b;
        }
    }

    // ==================================================================== B2.c Chase the rabbits
    /// <summary>
    /// Chase the rabbits (B2.c): the kids walk round to the herd's side, one points, all creep up crouched (a pincer, 50° apart, so
    /// they come from different sides), then charge, arms up; the first one inside the rabbits' alert radius is the Contact, and the
    /// real herd's <c>RabbitHerd.Scare</c> answers (the owner stands the dolls where the cue says; the rabbits see them). The kids run on,
    /// slow, stop, hands on knees, then laugh one after the other (the end beat), and sit until the next chase (one a view hour).
    /// <see cref="HerdAt"/> is the owner's: where the rabbits are when a chase starts (null: none near, the owner picks another game).
    /// </summary>
    public sealed class RabbitChaseGame : KidGame
    {
        public const float Approach = 2.2f, PointFor = 0.6f, Creep = 1.2f, StartAt = 1.0f, CreepBy = 0.15f, StopAt = 0.22f,
            Charge = 1.75f, Pant = 1.0f, Laugh = 1.2f, LaughStagger = 0.7f, Spread = 50f, AlertRadius = 0.55f;
        public readonly float Period;
        public Func<int, Vector2?> HerdAt;

        public sealed class Chase
        {
            public bool herd;
            public Vector2 H;
            public Vector2[] from, S, C, E;
            public float[] chargeAt, laughAt;
            public int leader, contactBy;
            public float contactAt;
            /// <summary>The 2026-11-06 tumble: the kid who trips in this chase (−1: nobody), when it trips (cycle seconds), lands, is up again.</summary>
            public int tumbler = -1;
            public float tripAt, landAt, upAt;
        }

        // ---------------------------------------------------------------- the tumble (user 2026-11-06: "Kid tumble sometimes")
        /// <summary>About one chase in four, one kid (never the one whose charge scares the herd) trips while charging. By the kit's hash.</summary>
        public const float TumbleShare = 0.25f, TripAfter = 0.7f, Slide = 0.08f, SlideFor = 0.45f, BrushFor = 0.8f;
        /// <summary>The cheat: the next chase built has a tumble (the first chase of a forced game).</summary>
        public bool TumbleFirst;

        /// <summary>The trip: a stumble forward (arms out to catch it), a dive onto the belly with a little bounce (the Contact: the grass puff),
        /// a beat lying there, up onto the knees, up. Never hurt-looking: the arms reach out the whole way, no clutching, no limp.</summary>
        public static readonly Beat Tumble = new Beat(
            Beat.Step(BeatPhase.Act, 0.22f, Ease.Out, PoseKey.Hold.Lean(34f).Roll(0f).Squash(0.95f).Offset(0f, 0.02f, 0f).Arms(95f, 30f)),
            Beat.Step(BeatPhase.Act, 0.16f, Ease.In, PoseKey.Hold.Lean(80f).Squash(0.92f).Offset(0f, 0.012f, 0f).Arms(165f, 22f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.18f, Ease.Back, PoseKey.Hold.Lean(84f).Squash(0.86f)),
            Beat.Step(BeatPhase.Hold, 0.62f, Ease.Smooth, PoseKey.Hold.Lean(82f).Squash(0.94f).Arms(150f, 40f)),
            Beat.Step(BeatPhase.Follow, 0.40f, Ease.Out, PoseKey.Hold.Lean(22f).Squash(0.8f).Offset(0f, 0f, 0f).Arms(40f, 30f)),
            Beat.Step(BeatPhase.Settle, 0.30f, Ease.Back, PoseKey.Hold.Lean(6f).Squash(1f).Arms(25f, 18f)));

        /// <summary>Seconds from the trip to the landing (the grass puff) and to standing again.</summary>
        public static float LandAfter => Tumble.ContactAt;
        public static float UpAfter => Tumble.Duration;

        /// <summary>Who trips in chase <paramref name="j"/> of a game with <paramref name="n"/> kids (−1: nobody): the kit's hash, never the sim's dice.</summary>
        public static int TumblerOf(int seed, int j, int n, int contactBy, bool force)
        {
            if (n < 2 || (!force && BeatHash.U01(seed, j, 3583) >= TumbleShare)) return -1;
            int k = BeatHash.Pick(n - 1, seed, j, 3584);
            return k >= contactBy && contactBy >= 0 ? k + 1 : k;
        }

        readonly List<Chase> chases = new();

        public RabbitChaseGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre) : base(PlayGame.ChaseRabbits, seed, t0, members, centre)
        {
            Period = BeatHash.Range(16f, 18f, seed, 7, 358);
        }

        public int CycleAt(float t) => Mathf.Max(0, Mathf.FloorToInt((t - t0) / Period));
        /// <summary>No rabbits near at the start of the chase on at <paramref name="t"/>: the owner should pick another game.</summary>
        public bool NoHerd(float t) => !ChaseOf(CycleAt(t)).herd;

        public Chase ChaseOf(int c)
        {
            int n = Count;
            while (chases.Count <= c)
            {
                int j = chases.Count;
                var ch = new Chase { from = new Vector2[n], S = new Vector2[n], C = new Vector2[n], E = new Vector2[n], chargeAt = new float[n], laughAt = new float[n] };
                for (int i = 0; i < n; i++)
                    ch.from[i] = j == 0 ? centre + Dir(i * Mathf.PI * 2f / Mathf.Max(1, n) + 0.4f) * (n > 1 ? 0.14f : 0f) : chases[j - 1].E[i];
                var h = HerdAt?.Invoke(j);
                ch.herd = h.HasValue;
                ch.leader = BeatHash.Pick(n, seed, j, 31);
                if (!ch.herd)
                {
                    for (int i = 0; i < n; i++) ch.S[i] = ch.C[i] = ch.E[i] = ch.from[i];
                    ch.contactAt = -1f; ch.contactBy = -1;
                    chases.Add(ch);
                    continue;
                }
                ch.H = h.Value;
                var mid = Vector2.zero;
                for (int i = 0; i < n; i++) mid += ch.from[i];
                mid /= Mathf.Max(1, n);
                var side = mid - ch.H;
                float a0 = side.sqrMagnitude > 1e-6f ? Mathf.Atan2(side.y, side.x) : BeatHash.U01(seed, j, 32) * Mathf.PI * 2f;
                // Who goes where: the kids keep their left-to-right order round the herd, so nobody crosses another's way.
                var order = new int[n];
                for (int i = 0; i < n; i++) order[i] = i;
                Array.Sort(order, (x, y) => Side(ch.from[x], ch.H, a0).CompareTo(Side(ch.from[y], ch.H, a0)));
                float first = 0f, t1 = 0f;
                ch.contactAt = float.MaxValue;
                for (int o = 0; o < n; o++)
                {
                    int i = order[o];
                    float a = a0 + (o - (n - 1) * 0.5f) * Spread * Mathf.Deg2Rad;
                    var d = Dir(a);
                    ch.S[i] = ch.H + d * StartAt;
                    ch.C[i] = ch.H + d * (StartAt - CreepBy);
                    ch.E[i] = ch.H + d * StopAt;
                    float delay = Desync.InGroup(seed + j, i, n).delay * (0.4f / Desync.MaxDelay);
                    ch.chargeAt[i] = Approach + PointFor + Creep + delay;
                    // The Contact: when it comes inside the alert radius (Out: the charge bursts out and slows).
                    float total = StartAt - CreepBy - StopAt;
                    float need = (StartAt - CreepBy - AlertRadius) / total;
                    float u = 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - need));
                    float at = ch.chargeAt[i] + u * Charge;
                    if (at < ch.contactAt) { ch.contactAt = at; ch.contactBy = i; }
                    t1 = Mathf.Max(t1, ch.chargeAt[i] + Charge);
                }
                first = t1 + Pant;
                for (int o = 0; o < n; o++) ch.laughAt[order[(o + j) % n]] = first + o * LaughStagger; // the laugh goes round them
                ch.tumbler = TumblerOf(seed, j, n, ch.contactBy, j == 0 && TumbleFirst);
                if (ch.tumbler >= 0)
                {
                    ch.tripAt = ch.chargeAt[ch.tumbler] + TripAfter;
                    ch.landAt = ch.tripAt + LandAfter;
                    ch.upAt = ch.tripAt + UpAfter;
                    // It laughs too, once it has brushed itself off (sits out laughing where it fell).
                    ch.laughAt[ch.tumbler] = Mathf.Max(ch.laughAt[ch.tumbler], ch.upAt + BrushFor + 0.1f);
                }
                chases.Add(ch);
            }
            return chases[c];
        }

        static float Side(Vector2 p, Vector2 h, float a0)
        {
            var d = p - h;
            return Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        Vector2 PosIn(Chase ch, int i, float w)
        {
            if (!ch.herd) return ch.from[i];
            if (i == ch.tumbler && w >= ch.tripAt)
            {
                // The trip: it slides on a little the way it ran, and stays where it fell.
                var at = PosIn(ch, i, ch.tripAt - 1e-4f);
                var run = ch.E[i] - ch.C[i];
                var dir = run.sqrMagnitude > 1e-6f ? run.normalized : Vector2.right;
                return at + dir * (Slide * Easing.Apply(Ease.Out, (w - ch.tripAt) / SlideFor));
            }
            if (w < Approach) return Vector2.Lerp(ch.from[i], ch.S[i], Easing.Apply(Ease.Smooth, w / Approach));
            float creep0 = Approach + PointFor;
            if (w < creep0) return ch.S[i];
            if (w < ch.chargeAt[i])
            {
                // Three small steps (0.05 u each): a step, a stop.
                float s = Mathf.Clamp01((w - creep0) / Creep) * 3f;
                int k = Mathf.Min(2, Mathf.FloorToInt(s));
                float f = Mathf.Clamp01((s - k) / 0.5f);
                float along = Mathf.Min(1f, (k + Easing.Apply(Ease.Smooth, f)) / 3f);
                return Vector2.Lerp(ch.S[i], ch.C[i], along);
            }
            return Vector2.Lerp(ch.C[i], ch.E[i], Easing.Apply(Ease.Out, (w - ch.chargeAt[i]) / Charge));
        }

        public override PlayCue Cue(int i, float t)
        {
            int c = CycleAt(t);
            var ch = ChaseOf(c);
            float w = t - t0 - c * Period;
            var cue = new PlayCue { round = c, hitOn = NoHit, role = GroupRole.Charger, since = w, under = DollStyle.Stand, style = DollStyle.Stand };
            cue.pos = PosIn(ch, i, w);
            cue.speed = SpeedOf(x => PosIn(ch, i, x), w);
            if (!ch.herd) { cue.under = cue.style = DollStyle.Sit; cue.face = centre; return cue; }
            cue.face = ch.H;
            float creep0 = Approach + PointFor;
            if (i == ch.tumbler && w >= ch.tripAt && w < ch.laughAt[i])
            {
                // Tripped: the beat is the whole pose (on Stand, so no run cycle under it), then brushing off where it fell.
                var run = ch.E[i] - ch.C[i];
                cue.face = cue.pos + (run.sqrMagnitude > 1e-6f ? run.normalized : Vector2.right);
                cue.under = cue.style = DollStyle.Stand;
                cue.since = w - ch.tripAt;
                return cue;
            }
            // The others turn to the one who fell once it is down, and laugh at it (Laugh points).
            bool atTumble = ch.tumbler >= 0 && i != ch.tumbler && w >= ch.landAt;
            if (w < Approach) { cue.under = cue.style = DollStyle.Walk; }
            else if (w < creep0) cue.style = i == ch.leader ? DollStyle.Point : DollStyle.Crouch;
            else if (w < ch.chargeAt[i]) cue.style = DollStyle.Crouch;
            else if (w < ch.chargeAt[i] + Charge)
            {
                cue.under = DollStyle.Run;
                cue.style = (i == ch.leader || (i + ch.leader) % 2 == 0) ? DollStyle.PlayCharge : DollStyle.PlayRunner;
                cue.since = w - ch.contactAt;
                if (i == ch.contactBy) cue.hitOn = Herd;
                cue.face = ch.H + (ch.H - ch.S[i]);
            }
            else if (w < ch.laughAt[i]) { cue.style = DollStyle.Stand; cue.face = atTumble ? PosIn(ch, ch.tumbler, w) : ch.H + (ch.H - ch.S[i]); cue.since = w - ch.chargeAt[i] - Charge; }
            else if (w < ch.laughAt[i] + Laugh) { cue.under = cue.style = DollStyle.Laugh; cue.endBeat = true; cue.face = atTumble ? PosIn(ch, ch.tumbler, w) : ch.H + (ch.H - ch.S[i]); }
            else { cue.under = cue.style = DollStyle.Sit; cue.face = centre; }
            if (cue.style == DollStyle.Crouch) cue.under = DollStyle.Crouch;
            if (cue.style == DollStyle.Point) cue.under = DollStyle.Point;
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            int cyc = CycleAt(t);
            var ch = ChaseOf(cyc);
            float w = t - t0 - cyc * Period;
            if (!ch.herd) return;
            float tau = Mathf.PI * 2f;
            if (i == ch.tumbler && w >= ch.tripAt && w < ch.laughAt[i]) { TumblePose(ref p, c.since); return; }
            switch (c.style)
            {
                case DollStyle.Crouch:
                case DollStyle.Point:
                    // Sneaking: crouched low, leaning in (Crouch's own creep is too small to read; the cue moves it in steps).
                    p.squash = 0.72f; p.lean = 18f; p.offset.z = 0f;
                    if (c.style == DollStyle.Point) { p.armR = new Vector3(88f, 0f, 12f); p.squash = 0.8f; }
                    else { p.armL = new Vector3(40f, 0f, -20f); p.armR = new Vector3(40f, 0f, 20f); }
                    break;
                case DollStyle.PlayCharge:
                {
                    // Arms up, waving, whooping.
                    float wv = Mathf.Sin(t * 6f * tau) * 20f;
                    p.armL = new Vector3(0f, 0f, -150f + wv); p.armR = new Vector3(0f, 0f, 150f - wv);
                    p.lean = 14f;
                    if (c.hitOn == Herd) p.hit = c.since >= 0f && c.since < Beat.MinContact ? 1f : 0f;
                    break;
                }
                case DollStyle.PlayRunner:
                {
                    float wv = Mathf.Sin(t * 9f);
                    p.armL = new Vector3(60f + wv * 30f, 0f, -40f); p.armR = new Vector3(60f - wv * 30f, 0f, 40f);
                    p.lean = 18f;
                    if (c.hitOn == Herd) p.hit = c.since >= 0f && c.since < Beat.MinContact ? 1f : 0f;
                    break;
                }
                case DollStyle.Stand:
                    if (w >= Approach + PointFor + Creep)
                    {
                        // Out of breath: hands on knees, panting.
                        float u = Easing.Apply(Ease.Smooth, Mathf.Clamp01(c.since / 0.3f));
                        p.lean = Mathf.Lerp(p.lean, 30f, u);
                        p.armL = Vector3.Lerp(p.armL, new Vector3(30f, 0f, -10f), u); p.armR = Vector3.Lerp(p.armR, new Vector3(30f, 0f, 10f), u);
                        p.offset.y = Mathf.Abs(Mathf.Sin(t * 3f * tau)) * 0.012f * u;
                    }
                    break;
            }
        }

        /// <summary>The tripped kid <paramref name="since"/> seconds after the trip: the <see cref="Tumble"/> beat over a stand, then brushing
        /// itself off (both hands swat the tunic in turn, a look down, a little wiggle) for <see cref="BrushFor"/> s.</summary>
        public static void TumblePose(ref DollPose p, float since)
        {
            if (since < UpAfter) { Tumble.Apply(ref p, since); return; }
            float b = since - UpAfter;
            float k = Easing.Apply(Ease.Smooth, b / 0.15f) * (1f - Easing.Apply(Ease.Smooth, (b - (BrushFor - 0.2f)) / 0.2f));
            float sw = Mathf.Sin(b * 5f * Mathf.PI * 2f);
            p.lean = Mathf.Lerp(6f, 14f, k);
            p.squash = 1f;
            p.offset = new Vector3(0f, Mathf.Abs(sw) * 0.006f * k, 0f);
            p.roll = sw * 4f * k;
            p.armL = Vector3.Lerp(new Vector3(25f, 0f, -18f), new Vector3(42f + sw * 26f, 0f, -12f), k);
            p.armR = Vector3.Lerp(new Vector3(25f, 0f, 18f), new Vector3(42f - sw * 26f, 0f, 12f), k);
        }

        /// <summary>The grass puff where a tumbling kid lands (once per tumble, through HitFx: <c>KidPlayDirector.FireEffects</c>).</summary>
        public override void Effects(float from, float to, List<PlayFx> into)
        {
            if (to <= from) return;
            for (int c = CycleAt(from); c <= CycleAt(to); c++)
            {
                var ch = ChaseOf(c);
                if (ch.tumbler < 0) continue;
                float at = t0 + c * Period + ch.landAt;
                if (at <= from || at > to) continue;
                var pos = PosIn(ch, ch.tumbler, ch.landAt);
                var run = ch.E[ch.tumbler] - ch.C[ch.tumbler];
                var dir = run.sqrMagnitude > 1e-6f ? run.normalized : Vector2.right;
                var chest = pos + dir * (BodyH * KidScale * 0.6f); // it lands on its front: the puff is under the chest, ahead of the feet
                into.Add(new PlayFx { kind = PlayFxKind.Grass, at = at, pos = new Vector3(chest.x, 0.005f, chest.y), aim = Vector3.up, count = 7, by = ch.tumbler, on = -1, scale = 0.8f });
            }
        }
    }

    // ==================================================================== B2.d Toddle
    /// <summary>
    /// Toddle (B2.d): member 0 is the carer, member 1 the toddler. The carer strolls round a small circle (0.12 u/s); the toddler wobbles
    /// after it, 0.15 u behind, arms out. Every 6–10 s the toddler plops down (squash, a drop, Back); the carer stops, turns, bends to it,
    /// and the little one gets up (the end beat), and on they go.
    /// </summary>
    public sealed class ToddleGame : KidGame
    {
        public const float Walk = 0.12f, Behind = 0.15f, Radius = 0.3f, PauseFor = 1.7f;

        static readonly Beat Plop = new Beat(
            Beat.Step(BeatPhase.Act, 0.25f, Ease.Back, PoseKey.Hold.Squash(0.75f).Offset(0f, -0.03f, 0f).Arms(30f, 40f).Roll(0f)),
            Beat.Step(BeatPhase.Hold, 0.8f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.45f, Ease.Back, PoseKey.Base));

        static readonly Beat Bend = new Beat(
            Beat.Step(BeatPhase.Approach, 0.3f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Smooth, PoseKey.Hold.Lean(30f).Arms(60f, 10f)),
            Beat.Step(BeatPhase.Hold, 0.45f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.4f, Ease.Smooth, PoseKey.Base));

        readonly float angle0;
        readonly List<float> walks = new();

        public ToddleGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre) : base(PlayGame.Toddle, seed, t0, members, centre)
        {
            angle0 = BeatHash.U01(seed, 8, 358) * Mathf.PI * 2f;
            if (Count > 0) scales[0] = 1f;
        }

        float WalkOf(int j) { while (walks.Count <= j) walks.Add(BeatHash.Range(6f, 10f, seed, walks.Count, 41)); return walks[j]; }

        /// <summary>Seconds walked so far (the path's clock: it stops in a plop), and the plop's clock (−1 while walking).</summary>
        public void Where(float t, out float walked, out float plopSince)
        {
            float local = Mathf.Max(0f, t - t0), start = 0f;
            walked = 0f;
            for (int j = 0; ; j++)
            {
                float w = WalkOf(j);
                if (local < start + w) { walked += local - start; plopSince = -1f; return; }
                walked += w;
                if (local < start + w + PauseFor) { plopSince = local - start - w; return; }
                start += w + PauseFor;
            }
        }

        Vector2 OnPath(float walked) => centre + Dir(angle0 + walked * Walk / Radius) * Radius;

        public override PlayCue Cue(int i, float t)
        {
            Where(t, out float walked, out float plop);
            var cue = new PlayCue { hitOn = NoHit, since = plop };
            bool toddler = i == 1;
            float along = toddler ? walked - Behind / Walk : walked;
            cue.pos = OnPath(along);
            cue.face = OnPath(along + 1f);
            cue.speed = plop < 0f ? Walk : 0f;
            if (toddler)
            {
                cue.role = GroupRole.Toddler;
                cue.under = cue.style = DollStyle.Toddle;
                cue.endBeat = plop >= 0f;
            }
            else
            {
                cue.role = GroupRole.Carer;
                cue.under = plop < 0f ? DollStyle.Walk : DollStyle.Stand;
                cue.style = cue.under;
                if (plop >= 0f) cue.face = OnPath(walked - Behind / Walk);
            }
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            if (c.since < 0f) return;
            if (c.role == GroupRole.Toddler) Plop.Apply(ref p, c.since);
            else Bend.Apply(ref p, c.since);
        }
    }

    // ==================================================================== B2.e Dance ring
    /// <summary>
    /// The ring dance (B2.e) on the kit's <see cref="RingGroup"/>: everyone on a ring, arms out so the hands meet, the ring turns 25°/s
    /// and reverses every 4 beats, a hop on each 0.5 s beat, in step on purpose; every 8 beats all step in to 0.6 r with arms up and a
    /// cheer bounce, and out again. After 24 beats the ring stops and everyone claps (the end beat), then dances again.
    /// </summary>
    public sealed class DanceRingGame : KidGame
    {
        public const float BeatSeconds = 0.5f, DanceSeconds = 12f, CheerSeconds = 1.5f, StepIn = 0.4f;
        readonly RingGroup ring;
        public readonly float Radius;

        public DanceRingGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre) : base(PlayGame.DanceRing, seed, t0, members, centre)
        {
            Radius = RingGroup.RadiusFor(Count);
            ring = new RingGroup(seed, t0, members, centre, Radius, 25f, BeatSeconds, 4, -1, GroupRole.Dancer, GroupRole.Centre, true);
            for (int i = 0; i < Count; i++) scales[i] = 1f;
        }

        public float CycleSeconds => DanceSeconds + CheerSeconds;

        void Where(float t, out float ringTime, out float cheer)
        {
            float s = Mathf.Max(0f, t - t0);
            int k = Mathf.FloorToInt(s / CycleSeconds);
            float w = s - k * CycleSeconds;
            ringTime = k * DanceSeconds + Mathf.Min(w, DanceSeconds);
            cheer = w >= DanceSeconds ? w - DanceSeconds : -1f;
        }

        /// <summary>How far in the ring is (1: on the ring, 0.6: stepped in), from the ring's own time.</summary>
        public static float InOut(float ringTime, out bool up)
        {
            float b = ringTime % (8f * BeatSeconds);
            up = b >= 6f * BeatSeconds;
            return up ? 1f - StepIn * Mathf.Sin(Mathf.PI * (b - 6f * BeatSeconds) / (2f * BeatSeconds)) : 1f;
        }

        public override PlayCue Cue(int i, float t)
        {
            Where(t, out float rt, out float cheer);
            var r = ring.Cue(i, t0 + rt);
            float k = InOut(rt, out _);
            var cue = new PlayCue
            {
                role = GroupRole.Dancer, hitOn = NoHit, inStep = true, round = r.round,
                pos = centre + (r.target - centre) * k, face = centre, since = rt,
                under = cheer >= 0f ? DollStyle.Cheer : DollStyle.Stand,
                style = cheer >= 0f ? DollStyle.Cheer : DollStyle.RingDance,
                endBeat = cheer >= 0f,
            };
            cue.speed = cheer >= 0f ? 0f : Radius * 25f * Mathf.Deg2Rad;
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            if (c.style != DollStyle.RingDance) return;
            InOut(c.since, out bool up);
            KidPlayPoses.Ring(ref p, c.since, up);
        }
    }
}
