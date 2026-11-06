// #355 Beat kit (pure: Mathf, Vector2 only, so Tools/AnimBench compiles it). Groups and pairs: docs/design/animation-specs.md §0.7, §0.8.
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>The roles of the spec sheets (animation-specs §0.7).</summary>
    public enum GroupRole
    {
        None,
        // play groups
        Chaser, Runner, Thrower, Catcher, Fetcher, Charger, Splasher, Seeker, Hider, Circler, Dancer, Centre, Carer, Toddler,
        // pairs
        Talker, Listener, Giver, Taker, Lead, Side, HugA, HugB,
        // onlookers
        Watcher, Cheerer,
    }

    /// <summary>
    /// What one member of a group does at a moment: its role, where it should stand (box x, z: <c>PeopleView</c>'s ground
    /// coordinates), the point it faces, and its own beat clock. <see cref="since"/> is seconds into the member's current beat,
    /// after its de-sync delay (negative: not yet; feed it to <c>beat.Pose(under, since, desync.amp)</c>).
    /// </summary>
    public struct MemberCue
    {
        public GroupRole role;
        public Vector2 target, face;
        /// <summary>The round (a tag, a throw, a talk turn, a ring beat) the group is in.</summary>
        public int round;
        /// <summary>Seconds into this member's beat this round (its delay and period applied).</summary>
        public float since;
        public Desync desync;
        /// <summary>The member walks or runs to <see cref="target"/> now (else it stands there).</summary>
        public bool moving;
    }

    /// <summary>
    /// A group (play group, dance ring, staged moment) or pair (talk, hand-over, walk together, hug) with one shared clock
    /// (<see cref="t0"/>), roles and a cue per member at any time: a pure function of (seed, members, t), so the planner, the view
    /// and AnimBench agree, and replaying a moment replays it exactly. Members never run their own loop while in it (§0.7).
    /// The seed is the group's own (a hash of its place and day, say), never the sim's random stream.
    /// </summary>
    public abstract class BeatGroup
    {
        protected readonly int[] keys;
        protected readonly Desync[] desyncs;
        public readonly int seed;
        /// <summary>When the group started (the owner's clock, seconds).</summary>
        public readonly float t0;

        protected BeatGroup(int seed, float t0, IReadOnlyList<int> memberKeys, bool inStep = false)
        {
            this.seed = seed; this.t0 = t0;
            int n = memberKeys?.Count ?? 0;
            keys = new int[n]; desyncs = new Desync[n];
            for (int i = 0; i < n; i++)
            {
                keys[i] = memberKeys[i];
                desyncs[i] = inStep ? Desync.InStep : Desync.InGroup(seed, i, n);
            }
        }

        public int Count => keys.Length;
        /// <summary>The member's key (a person's <c>PersonRef.Key</c>).</summary>
        public int KeyOf(int i) => keys[i];
        public int IndexOf(int key) { for (int i = 0; i < keys.Length; i++) if (keys[i] == key) return i; return -1; }
        public Desync DesyncOf(int i) => desyncs[i];

        /// <summary>Member <paramref name="i"/>'s cue at <paramref name="t"/> (the owner's clock).</summary>
        public abstract MemberCue Cue(int i, float t);

        /// <summary>A point on a ring: angle 0 is +x, counter-clockwise seen from above (x right, z up the screen).</summary>
        public static Vector2 OnRing(Vector2 centre, float radius, float angleRad) => centre + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;

        protected static Vector2 Clamp(Vector2 p, Vector2 centre, float radius)
        {
            var d = p - centre;
            float m = d.magnitude;
            return m > radius && m > 1e-5f ? centre + d * (radius / m) : p;
        }
    }

    /// <summary>
    /// A ring round a centre (§0.7 Dancer / Centre, or Watcher / Cheerer round an event): the members spread evenly on a ring of
    /// <see cref="radius"/>, which turns <see cref="degPerSecond"/> and reverses every <see cref="reverseEvery"/> beats; everyone faces
    /// the centre. One optional leader stands in the middle (role <see cref="centreRole"/>). The ring dance moves in step on purpose
    /// (exempt from P6); onlookers don't (<c>inStep</c> false).
    /// </summary>
    public sealed class RingGroup : BeatGroup
    {
        public readonly Vector2 centre;
        public readonly float radius, degPerSecond, beatSeconds;
        public readonly int reverseEvery, leader;
        public readonly GroupRole ringRole, centreRole;
        readonly float angle0;

        /// <param name="leader">Index of the member in the middle, or −1.</param>
        public RingGroup(int seed, float t0, IReadOnlyList<int> members, Vector2 centre, float radius, float degPerSecond = 25f, float beatSeconds = 0.5f,
                         int reverseEvery = 4, int leader = -1, GroupRole ringRole = GroupRole.Dancer, GroupRole centreRole = GroupRole.Centre, bool inStep = true)
            : base(seed, t0, members, inStep)
        {
            this.centre = centre; this.radius = radius; this.degPerSecond = degPerSecond; this.beatSeconds = Mathf.Max(0.05f, beatSeconds);
            this.reverseEvery = reverseEvery; this.leader = leader; this.ringRole = ringRole; this.centreRole = centreRole;
            angle0 = BeatHash.U01(seed, 1, 355) * Mathf.PI * 2f;
        }

        /// <summary>The spec's ring size: r = 0.3 + 0.03 × max(0, n − 6) u (B2.e).</summary>
        public static float RadiusFor(int dancers) => 0.3f + 0.03f * Mathf.Max(0, dancers - 6);

        /// <summary>How far the ring has turned (degrees) at <paramref name="since"/>: back and forth, continuous at each reversal.</summary>
        public float TurnAt(float since)
        {
            if (since <= 0f) return 0f;
            if (reverseEvery <= 0) return degPerSecond * since;
            float leg = reverseEvery * beatSeconds;
            int k = Mathf.FloorToInt(since / leg);
            float rem = since - k * leg;
            return (k & 1) == 0 ? degPerSecond * rem : degPerSecond * (leg - rem);
        }

        public override MemberCue Cue(int i, float t)
        {
            float since = t - t0;
            var d = desyncs[i];
            int beat = Mathf.FloorToInt(Mathf.Max(0f, d.Clock(since)) / beatSeconds);
            var cue = new MemberCue { desync = d, round = beat, since = d.Clock(since) - beat * beatSeconds, face = centre };
            if (i == leader)
            {
                cue.role = centreRole;
                cue.target = centre;
                cue.face = OnRing(centre, 1f, angle0); // the leader faces one way, not its own spot
                return cue;
            }
            int n = Count - (leader >= 0 && leader < Count ? 1 : 0);
            int slot = leader >= 0 && i > leader ? i - 1 : i;
            float a = angle0 + (n > 0 ? slot * Mathf.PI * 2f / n : 0f) + TurnAt(since) * Mathf.Deg2Rad;
            cue.role = ringRole;
            cue.target = OnRing(centre, radius, a);
            cue.moving = degPerSecond != 0f;
            return cue;
        }
    }

    /// <summary>The kinds of pair (§0.7): their roles and how they stand.</summary>
    public enum PairKind { Talk, HandOver, WalkTogether, Hug }

    /// <summary>
    /// Two people (§0.7 pairs). <see cref="PairKind.Talk"/>: face to face <see cref="gap"/> apart, Talker and Listener swap every
    /// <see cref="turnSeconds"/> (today's chat turn-taking, <c>PeopleView.cs:429</c>, with roles instead of a phase offset).
    /// <see cref="PairKind.HandOver"/>: the Giver (member 0) and the Taker face each other; one beat from t0. <see cref="PairKind.WalkTogether"/>:
    /// the Lead walks along <see cref="heading"/> at <see cref="speed"/>, the Side a shoulder to its right. <see cref="PairKind.Hug"/>:
    /// they close from <see cref="gap"/> to <see cref="HugGap"/> over the first 0.6 s (only with <see cref="BeatFlags.Hugs"/>; else a hand-over stance).
    /// </summary>
    public sealed class PairGroup : BeatGroup
    {
        public const float HugGap = 0.06f, SideBySide = 0.09f, HugClose = 0.6f;
        public readonly PairKind kind;
        /// <summary>The middle between them (the start point for a walk).</summary>
        public readonly Vector2 anchor;
        /// <summary>The way from member 0 to member 1 (talk, hand-over, hug), or the way they walk (unit).</summary>
        public readonly Vector2 heading;
        public readonly float gap, turnSeconds, speed;
        readonly int firstTalker;

        public PairGroup(int seed, float t0, int a, int b, PairKind kind, Vector2 anchor, Vector2 heading, float gap = 0.16f, float turnSeconds = 2.6f, float speed = 0.12f)
            : base(seed, t0, new[] { a, b }, false)
        {
            this.kind = kind; this.anchor = anchor; this.gap = gap; this.turnSeconds = Mathf.Max(0.2f, turnSeconds); this.speed = speed;
            this.heading = heading.sqrMagnitude > 1e-8f ? heading.normalized : new Vector2(1f, 0f);
            firstTalker = BeatHash.Pick(2, seed, a ^ b, 363);
        }

        public override MemberCue Cue(int i, float t)
        {
            float since = Mathf.Max(0f, t - t0);
            var d = desyncs[i];
            var other = 1 - i;
            var cue = new MemberCue { desync = d };
            switch (kind == PairKind.Hug && !BeatFlags.Hugs ? PairKind.HandOver : kind)
            {
                case PairKind.Talk:
                {
                    int turn = Mathf.FloorToInt(since / turnSeconds);
                    bool talks = ((turn + firstTalker) & 1) == i;
                    cue.role = talks ? GroupRole.Talker : GroupRole.Listener;
                    cue.round = turn;
                    cue.since = d.Clock(since - turn * turnSeconds);
                    cue.target = Spot(i, gap);
                    cue.face = Spot(other, gap);
                    break;
                }
                case PairKind.HandOver:
                    cue.role = i == 0 ? GroupRole.Giver : GroupRole.Taker;
                    cue.since = i == 0 ? since : since - 0.1f; // the taker reaches a breath after the giver offers (no hash delay: they answer each other)
                    cue.target = Spot(i, gap);
                    cue.face = Spot(other, gap);
                    break;
                case PairKind.WalkTogether:
                {
                    var lead = anchor + heading * (speed * since);
                    var right = new Vector2(heading.y, -heading.x);
                    cue.role = i == 0 ? GroupRole.Lead : GroupRole.Side;
                    cue.since = d.Clock(since);
                    cue.target = i == 0 ? lead : lead + right * SideBySide;
                    cue.face = cue.target + heading;
                    cue.moving = speed > 0f;
                    break;
                }
                case PairKind.Hug:
                {
                    float k = Easing.Apply(Ease.Smooth, since / HugClose);
                    float g = Mathf.Lerp(gap, HugGap, k);
                    cue.role = i == 0 ? GroupRole.HugA : GroupRole.HugB;
                    cue.since = since;
                    cue.target = Spot(i, g);
                    cue.face = Spot(other, g);
                    cue.moving = k < 1f;
                    break;
                }
            }
            return cue;
        }

        Vector2 Spot(int i, float g) => anchor + heading * (i == 0 ? -g * 0.5f : g * 0.5f);
    }

    /// <summary>
    /// Tag (§0.7 Chaser / Runner, B2.a), as a pure function of time: rounds of <see cref="roundSeconds"/>. Each round the chaser runs at
    /// the nearest runner (not the one who just tagged it), the runners scatter away from it, and at <see cref="ContactShare"/> of the
    /// round the chaser is <see cref="TouchDistance"/> from its target: the touch (the Contact). Both hold, facing each other, to the
    /// round's end; the touched runner is the next round's chaser. Everyone stays within <see cref="radius"/> of the centre.
    /// B2 adds the zigzag and the taunts on top; this gives every member a spot, a facing and its beat clock.
    /// </summary>
    public sealed class ChaseGroup : BeatGroup
    {
        public const float TouchDistance = 0.09f, ContactShare = 0.8f, FleeDistance = 0.28f;
        public readonly Vector2 centre;
        public readonly float radius, roundSeconds;
        // Per round: everyone's start spot, the chaser and its target (computed forward from round 0, kept).
        readonly List<Vector2[]> starts = new();
        readonly List<int> chasers = new(), targets = new();

        /// <summary>How far a runner flees in a round (#358: tag uses more of the green than the default).</summary>
        public readonly float flee;

        public ChaseGroup(int seed, float t0, IReadOnlyList<int> members, Vector2 centre, float radius = 0.6f, float roundSeconds = 5f, float flee = FleeDistance)
            : base(seed, t0, members, false)
        {
            this.centre = centre; this.radius = radius; this.roundSeconds = Mathf.Max(0.5f, roundSeconds); this.flee = flee;
            int n = Count;
            var s0 = new Vector2[n];
            float a0 = BeatHash.U01(seed, 0, 358) * Mathf.PI * 2f;
            for (int i = 0; i < n; i++) s0[i] = OnRing(centre, radius * BeatHash.Range(0.25f, 0.7f, seed, i, 1), a0 + i * Mathf.PI * 2f / Mathf.Max(1, n));
            starts.Add(s0);
            chasers.Add(BeatHash.Pick(n, seed, 2, 358));
        }

        public int RoundAt(float t) => Mathf.Max(0, Mathf.FloorToInt((t - t0) / roundSeconds));
        /// <summary>Who chases in round <paramref name="r"/>, and whom.</summary>
        public int ChaserOf(int r) { Grow(r); return chasers[r]; }
        public int TargetOf(int r) { Grow(r); return targets[r]; }
        /// <summary>The touch of round <paramref name="r"/> (the owner's clock).</summary>
        public float ContactAt(int r) => t0 + (r + ContactShare) * roundSeconds;

        void Grow(int r)
        {
            int n = Count;
            while (targets.Count <= r)
            {
                int k = targets.Count;
                var s = starts[k];
                int c = chasers[k], before = k > 0 ? chasers[k - 1] : -1;
                int g = -1; float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (i == c || (i == before && n > 2)) continue; // no tag-backs
                    float dd = (s[i] - s[c]).sqrMagnitude;
                    if (dd < best) { best = dd; g = i; }
                }
                if (g < 0) g = c; // a group of one: nothing to chase
                targets.Add(g);
                var e = new Vector2[n];
                for (int i = 0; i < n; i++) e[i] = FleeEnd(s, c, i, k);
                if (g != c)
                {
                    var back = s[c] - e[g];
                    e[c] = e[g] + (back.sqrMagnitude > 1e-8f ? back.normalized : new Vector2(1f, 0f)) * TouchDistance;
                }
                starts.Add(e);
                chasers.Add(g);
            }
        }

        Vector2 FleeEnd(Vector2[] s, int c, int i, int round)
        {
            if (i == c) return s[i];
            var away = s[i] - s[c];
            if (away.sqrMagnitude < 1e-6f) away = new Vector2(1f, 0f);
            float turn = BeatHash.Range(-0.6f, 0.6f, seed, i, round * 31 + 5); // not straight away: they scatter
            var dir = away.normalized;
            float cs = Mathf.Cos(turn), sn = Mathf.Sin(turn);
            dir = new Vector2(dir.x * cs - dir.y * sn, dir.x * sn + dir.y * cs);
            var end = s[i] + dir * flee * desyncs[i].period;
            if ((end - centre).magnitude > radius) end = Clamp(s[i] - dir * (flee * 0.5f), centre, radius); // at the edge: duck back the other way
            return Clamp(end, centre, radius);
        }

        public override MemberCue Cue(int i, float t)
        {
            int r = RoundAt(t);
            Grow(r);
            var s = starts[r]; var e = starts[r + 1];
            int c = chasers[r], g = targets[r];
            float local = t - t0 - r * roundSeconds;
            float run = ContactShare * roundSeconds;
            var d = desyncs[i];
            var cue = new MemberCue { round = r, desync = d };
            if (i == c)
            {
                // The chaser starts at once (it's "it") and arrives exactly at the touch.
                float u = Easing.Apply(Ease.Smooth, local / run);
                cue.role = GroupRole.Chaser;
                cue.since = local - run; // 0 at the touch: the chaser's beat runs on the contact
                cue.target = Vector2.Lerp(s[i], e[i], u);
                cue.face = Vector2.Lerp(s[g], e[g], RunnerU(g, local, run));
                cue.moving = local < run;
            }
            else
            {
                float u = RunnerU(i, local, run);
                cue.role = GroupRole.Runner;
                cue.since = d.Clock(local);
                cue.target = Vector2.Lerp(s[i], e[i], u);
                cue.moving = local < run && u < 1f;
                var chaserNow = Vector2.Lerp(s[c], e[c], Easing.Apply(Ease.Smooth, local / run));
                cue.face = local < run && i != g ? cue.target + (e[i] - s[i]) : chaserNow; // running away; after the touch everyone looks
                if (i == g && local < run) cue.face = cue.target + (e[i] - s[i]);
            }
            return cue;
        }

        /// <summary>A runner's way along its flight this round: it reacts after its delay, runs, and is there by the touch.</summary>
        float RunnerU(int i, float local, float run)
        {
            float delay = Mathf.Min(desyncs[i].delay * 0.5f, run * 0.3f);
            return Easing.Apply(Ease.Smooth, (local - delay) / Mathf.Max(0.05f, run - delay));
        }
    }
}
