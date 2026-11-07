// #355 Beat kit (pure: Mathf, Vector3 and DollPose only, so Tools/AnimBench compiles it). Vocabulary: docs/design/animation-specs.md §0.2, §0.8.
using System;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>The phases of a told-once action (animation-specs §0.2), in the order a beat plays them.</summary>
    public enum BeatPhase
    {
        /// <summary>Walk or turn into place, facing the target.</summary>
        Approach,
        /// <summary>Anticipation: the wind-up, the opposite way to the act.</summary>
        Antic,
        /// <summary>The fast part: a strike, a throw, a lift.</summary>
        Act,
        /// <summary>The hit window: <see cref="DollPose.hit"/> is 1 here, and <c>PeopleView.WorkHit</c> fires once.</summary>
        Contact,
        /// <summary>The held pose right after contact (≥ 0.1 s, P8).</summary>
        Hold,
        /// <summary>Follow-through: overshoot and recoil while the target answers.</summary>
        Follow,
        /// <summary>Back to the loop underneath.</summary>
        Settle,
        /// <summary>The payoff, once per loop (P5).</summary>
        EndBeat,
        /// <summary>Not a step: the beat is over (or has not started, see <see cref="BeatSample.started"/>).</summary>
        Done,
    }

    /// <summary>Which parts of a <see cref="PoseKey"/> it sets; the others carry on from the key before.</summary>
    [Flags]
    public enum PoseParts
    {
        None = 0, Offset = 1, Yaw = 2, Lean = 4, Roll = 8, Squash = 16, ArmL = 32, ArmR = 64, Extend = 128,
        All = Offset | Yaw | Lean | Roll | Squash | ArmL | ArmR | Extend,
    }

    /// <summary>
    /// The pose a beat's step moves to, part by part, in <see cref="DollPose"/>'s units (arms are absolute local Euler degrees;
    /// offset in adult doll units, −z forward). Parts left unset keep the value of the key before (the first step's "before" is
    /// the pose underneath: the doll's loop). <see cref="Base"/> goes back to the pose underneath (a <see cref="BeatPhase.Settle"/>).
    /// Build with the fluent setters: <c>PoseKey.Hold.Lean(20).ArmR(95, 0, 8)</c>.
    /// </summary>
    public struct PoseKey
    {
        public PoseParts parts;
        /// <summary>Unset parts go back to the pose underneath instead of carrying on.</summary>
        public bool toBase;
        public Vector3 offset, armL, armR;
        public float yaw, lean, roll, squash;
        public bool extend;

        /// <summary>Sets nothing: the step holds the key before (a <see cref="BeatPhase.Hold"/>).</summary>
        public static PoseKey Hold => default;
        /// <summary>Back to the pose underneath (the loop), part by part.</summary>
        public static PoseKey Base => new PoseKey { toBase = true };

        public PoseKey Offset(float x, float y, float z) { offset = new Vector3(x, y, z); parts |= PoseParts.Offset; return this; }
        public PoseKey Yaw(float deg) { yaw = deg; parts |= PoseParts.Yaw; return this; }
        public PoseKey Lean(float deg) { lean = deg; parts |= PoseParts.Lean; return this; }
        public PoseKey Roll(float deg) { roll = deg; parts |= PoseParts.Roll; return this; }
        public PoseKey Squash(float s) { squash = s; parts |= PoseParts.Squash; return this; }
        public PoseKey ArmL(float x, float y, float z) { armL = new Vector3(x, y, z); parts |= PoseParts.ArmL; return this; }
        public PoseKey ArmR(float x, float y, float z) { armR = new Vector3(x, y, z); parts |= PoseParts.ArmR; return this; }
        /// <summary>Both arms, mirrored: forward swing <paramref name="x"/>, out to the side <paramref name="side"/> (right +, left −).</summary>
        public PoseKey Arms(float x, float side) { return ArmL(x, 0f, -side).ArmR(x, 0f, side); }
        public PoseKey Extend(bool on) { extend = on; parts |= PoseParts.Extend; return this; }

        /// <summary>This key over <paramref name="before"/> (the previous step's resolved pose) and <paramref name="under"/> (the loop).</summary>
        public DollPose Resolve(in DollPose before, in DollPose under)
        {
            var p = toBase ? under : before;
            if ((parts & PoseParts.Offset) != 0) p.offset = offset;
            if ((parts & PoseParts.Yaw) != 0) p.yaw = yaw;
            if ((parts & PoseParts.Lean) != 0) p.lean = lean;
            if ((parts & PoseParts.Roll) != 0) p.roll = roll;
            if ((parts & PoseParts.Squash) != 0) p.squash = squash;
            if ((parts & PoseParts.ArmL) != 0) p.armL = armL;
            if ((parts & PoseParts.ArmR) != 0) p.armR = armR;
            if ((parts & PoseParts.Extend) != 0) p.extend = extend;
            return p;
        }
    }

    /// <summary>One step of a beat: its phase, how long it takes, its easing and the pose it moves to.</summary>
    public readonly struct BeatStep
    {
        public readonly BeatPhase phase;
        public readonly float seconds;
        public readonly Ease ease;
        public readonly PoseKey key;

        public BeatStep(BeatPhase phase, float seconds, Ease ease, PoseKey key)
        {
            this.phase = phase; this.seconds = Mathf.Max(0f, seconds); this.ease = ease; this.key = key;
        }
    }

    /// <summary>Where a beat is at a moment.</summary>
    public struct BeatSample
    {
        public BeatPhase phase;
        /// <summary>The step index (−1 before the start; the last step once it is over).</summary>
        public int step;
        /// <summary>0..1 through the step, and that eased (may overshoot for Back, Wobble, Pop).</summary>
        public float local, eased;
        /// <summary>The beat's own clock (seconds), after a hit-stop's freeze; and that over the whole beat, 0..1.</summary>
        public float clock, progress;
        public bool started, done;
        /// <summary>In the hit-stop's freeze (the pose holds the end of the act; the hit comes as it ends).</summary>
        public bool frozen;
        /// <summary>The <see cref="BeatPhase.Contact"/> window: <see cref="DollPose.hit"/> is 1.</summary>
        public bool Hit => phase == BeatPhase.Contact && started && !done;
    }

    /// <summary>The optional variants of the spec sheets (animation-specs §0.8): hit-stop off, hugs on (the user, 2026-11-06).</summary>
    public static class BeatFlags
    {
        /// <summary>A key contact freezes actor, target and arc object for <see cref="HitStopSeconds"/>; the beat's Follow catches up.</summary>
        public static bool HitStop;
        /// <summary>Hugs between peg dolls (B7, C3). On: the user, 2026-11-06 "Hugs are fine. Add them." (View/HugDirector.cs; the panel can turn them off).</summary>
        public static bool Hugs = true;
        public const float HitStopSeconds = 0.1f;
    }

    /// <summary>
    /// A one-shot beat (animation-specs §0.2): phases with durations, easings and pose keys, played on <c>since</c> (real seconds
    /// into the beat) like <c>DollMotion</c>'s <c>Knock</c> and <c>Peer</c>. Immutable: build one per kind of beat once (a static),
    /// and evaluate it for every doll with its own <c>since</c> (and a <see cref="Desync"/>). It rides on top of a loop pose:
    /// <see cref="Apply"/> moves the doll's pose from the loop to the keys and back, and sets <see cref="DollPose.hit"/> in the
    /// <see cref="BeatPhase.Contact"/> window, so <c>PeopleView.WorkHit</c> fires for it like for a loop's stroke.
    /// </summary>
    public sealed class Beat
    {
        /// <summary>A Contact step is at least this long, so a doll's frame (30 fps and slower) always sees its hit.</summary>
        public const float MinContact = 0.06f;

        readonly BeatStep[] steps;
        readonly float[] starts;
        public float Duration { get; }
        /// <summary>When the (first) Contact starts, or −1 if the beat has none.</summary>
        public float ContactAt { get; }
        public int Count => steps.Length;
        public BeatStep this[int i] => steps[i];

        public Beat(params BeatStep[] steps)
        {
            if (steps == null || steps.Length == 0) throw new ArgumentException("a beat needs at least one step");
            this.steps = new BeatStep[steps.Length];
            starts = new float[steps.Length];
            float t = 0f; ContactAt = -1f;
            for (int i = 0; i < steps.Length; i++)
            {
                var s = steps[i];
                if (s.phase == BeatPhase.Done) throw new ArgumentException("Done is not a step");
                if (s.phase == BeatPhase.Contact && s.seconds < MinContact) s = new BeatStep(s.phase, MinContact, s.ease, s.key);
                if (s.phase == BeatPhase.Contact && ContactAt < 0f) ContactAt = t;
                this.steps[i] = s; starts[i] = t; t += s.seconds;
            }
            Duration = t;
        }

        /// <summary>A step: <c>Beat.Step(BeatPhase.Act, 0.15f, Ease.Snap, PoseKey.Hold.ArmR(95, 0, 8))</c>.</summary>
        public static BeatStep Step(BeatPhase phase, float seconds, Ease ease, PoseKey key) => new BeatStep(phase, seconds, ease, key);

        /// <summary>When the first step of <paramref name="phase"/> starts (beat seconds), or −1.</summary>
        public float StartOf(BeatPhase phase)
        {
            for (int i = 0; i < steps.Length; i++) if (steps[i].phase == phase) return starts[i];
            return -1f;
        }

        /// <summary>The same beat <paramref name="k"/> times as long (a member's de-synced period, <see cref="Desync.period"/>).</summary>
        public Beat Scaled(float k)
        {
            k = Mathf.Max(0.05f, k);
            var s = new BeatStep[steps.Length];
            for (int i = 0; i < s.Length; i++) s[i] = new BeatStep(steps[i].phase, steps[i].seconds * k, steps[i].ease, steps[i].key);
            return new Beat(s);
        }

        /// <summary>
        /// The beat's own clock at <paramref name="since"/>. Without a hit-stop it is <paramref name="since"/>. With one, it stops just
        /// before the Contact for <see cref="BeatFlags.HitStopSeconds"/> (the end of the act holds), then the Follow plays that much
        /// faster, so the beat still ends on time (the plan's clock never bends, §0.8). A beat with no Follow catches up in what is left.
        /// </summary>
        public float Clock(float since, bool hitStop, out bool frozen)
        {
            frozen = false;
            if (!hitStop || ContactAt < 0f || since < ContactAt) return since;
            const float h = BeatFlags.HitStopSeconds;
            if (since < ContactAt + h) { frozen = true; return Mathf.Max(0f, ContactAt - 1e-4f); }
            float f0 = -1f, f1 = Duration;
            for (int i = 0; i < steps.Length; i++)
                if (steps[i].phase == BeatPhase.Follow && starts[i] >= ContactAt) { f0 = starts[i]; f1 = starts[i] + steps[i].seconds; break; }
            if (f0 < 0f || f1 - f0 < h * 1.5f) { f0 = ContactAt + h; f1 = Duration; } // no room in a Follow: catch up over the rest
            if (f1 - f0 <= h) return since - h; // no room at all: the beat runs late by the freeze
            if (since < f0 + h) return since - h;
            if (since < f1) return f0 + (since - f0 - h) * (f1 - f0) / (f1 - f0 - h);
            return since;
        }

        /// <summary>Where the beat is <paramref name="since"/> seconds in (negative: not started; past <see cref="Duration"/>: done).</summary>
        public BeatSample Sample(float since, bool hitStop = false)
        {
            float c = Clock(since, hitStop, out bool frozen);
            var s = new BeatSample { clock = c, frozen = frozen, progress = Duration > 0f ? Mathf.Clamp01(c / Duration) : 1f };
            if (c < 0f) { s.phase = steps[0].phase; s.step = -1; return s; }
            s.started = true;
            if (c >= Duration) { s.phase = BeatPhase.Done; s.step = steps.Length - 1; s.local = 1f; s.eased = 1f; s.done = true; return s; }
            int i = steps.Length - 1;
            while (i > 0 && c < starts[i]) i--;
            while (i < steps.Length - 1 && steps[i].seconds <= 0f) i++; // a zero step is already over
            var st = steps[i];
            s.step = i; s.phase = st.phase;
            s.local = st.seconds > 0f ? Mathf.Clamp01((c - starts[i]) / st.seconds) : 1f;
            s.eased = Easing.Apply(st.ease, s.local);
            return s;
        }

        /// <summary>Did a Contact start in (<paramref name="from"/>, <paramref name="to"/>]? Frame-rate proof: a beat's owner (a play group,
        /// the hunt plan) can fire its own events from this, exactly once per contact, without looking at the pose.</summary>
        public bool ContactBetween(float from, float to, bool hitStop = false)
        {
            if (ContactAt < 0f || to <= from) return false;
            float at = ContactAt + (hitStop ? BeatFlags.HitStopSeconds : 0f);
            return from < at && at <= to;
        }

        /// <summary>The pose <paramref name="since"/> seconds in, over the loop pose <paramref name="under"/>. <paramref name="amp"/> scales
        /// how far the beat moves the doll away from the loop (a member's <see cref="Desync.amp"/>). Before the start it is the loop
        /// pose; once done it holds the last key (a Settle to <see cref="PoseKey.Base"/> ends on the loop again).</summary>
        public DollPose Pose(in DollPose under, float since, float amp = 1f, bool hitStop = false)
        {
            var s = Sample(since, hitStop);
            if (!s.started) return under;
            var before = under;
            DollPose from = under, to = under;
            for (int i = 0; i <= s.step; i++)
            {
                from = before;
                to = steps[i].key.Resolve(before, under);
                before = to;
            }
            var p = s.done ? to : Mix(from, to, s.eased);
            if (amp != 1f) p = Mix(under, p, amp);
            p.extend = s.done || s.eased >= 0.5f ? to.extend : from.extend;
            p.hidden = under.hidden; p.lying = under.lying;
            p.hit = s.Hit ? 1f : 0f;
            return p;
        }

        /// <summary><see cref="Pose"/> in place: the doll's loop pose becomes the beat's.</summary>
        public void Apply(ref DollPose pose, float since, float amp = 1f, bool hitStop = false) => pose = Pose(pose, since, amp, hitStop);

        /// <summary>Unclamped, so Back and Pop overshoot (DollMotion.Blend clamps; a beat must not).</summary>
        static DollPose Mix(in DollPose a, in DollPose b, float k)
        {
            var p = b;
            p.offset = Vector3.LerpUnclamped(a.offset, b.offset, k);
            p.yaw = a.yaw + Mathf.DeltaAngle(a.yaw, b.yaw) * k;
            p.lean = Mathf.LerpUnclamped(a.lean, b.lean, k);
            p.roll = Mathf.LerpUnclamped(a.roll, b.roll, k);
            p.squash = Mathf.LerpUnclamped(a.squash, b.squash, k);
            p.armL = Vector3.LerpUnclamped(a.armL, b.armL, k);
            p.armR = Vector3.LerpUnclamped(a.armR, b.armR, k);
            p.hit = k >= 1f ? b.hit : a.hit;
            return p;
        }
    }

    /// <summary>Turns a doll's <see cref="DollPose.hit"/> into a once-per-contact edge (0 → 1). One per doll; <c>PeopleView</c> holds them.</summary>
    public struct HitEdge
    {
        bool high;
        /// <summary>True on the frame the hit goes 0 → 1, never again until it has gone back to 0.</summary>
        public bool Step(float hit)
        {
            bool now = hit > 0.5f;
            bool rose = now && !high;
            high = now;
            return rose;
        }
        public void Reset() => high = false;
    }
}
