// #363 B7 pair events on a shared clock (pure: Mathf, Vector2, PairGroup and the beat kit only, so Tools/AnimBench compiles it).
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>What a pair event says about one member now: where it stands (the owner moves it there), what it faces, the prop it holds.</summary>
    public struct PairCue
    {
        public PairKind kind;
        public GroupRole role;
        /// <summary>Index in the pair (0 = the giver, the lead, hug A).</summary>
        public int index;
        public Vector2 pos, face;
        /// <summary>The pace the member glides to <see cref="pos"/> at (u/s).</summary>
        public float speed;
        /// <summary>0: the member keeps its own prop; 1: it holds <see cref="prop"/> in <see cref="slot"/> (None: empty-handed).</summary>
        public int propMode;
        public PropKind prop;
        public PropSlot slot;
    }

    /// <summary>
    /// One staged pair moment (hand over, walk together, hug): a <see cref="PairGroup"/> on a shared clock plus the beats that play on it.
    /// Built by <see cref="PairStage"/>; a pure function of its inputs, so the planner's view, the AnimLab and AnimBench play it the same.
    /// </summary>
    public sealed class PairEvent
    {
        public readonly PairKind kind;
        public readonly PairGroup group;
        /// <summary>When it began (the owner's clock), the walk-up before the beat, and the beat's own length.</summary>
        public readonly float start, approach, duration;
        public readonly PropKind thing;
        /// <summary>Stretch of the beats (a hand-over squeezed into a short window).</summary>
        public readonly float k;
        public readonly Beat giverBeat, takerBeat;
        public readonly Vector2 heading;
        public readonly float walkSpeed;
        /// <summary>The smaller of a hug lifts 0.02 u.</summary>
        public bool liftSecond;
        /// <summary>Set to end it early (the step it hangs on ended).</summary>
        public float cutAt = float.MaxValue;

        public PairEvent(PairKind kind, PairGroup group, float start, float approach, float duration, PropKind thing, float k, Vector2 heading, float walkSpeed)
        {
            this.kind = kind; this.group = group; this.start = start; this.approach = approach; this.duration = duration;
            this.thing = thing; this.k = k; this.heading = heading; this.walkSpeed = walkSpeed;
            giverBeat = k == 1f ? PairBeats.Giver : PairBeats.Giver.Scaled(k);
            takerBeat = k == 1f ? PairBeats.Taker : PairBeats.Taker.Scaled(k);
        }

        public float BeatStart => start + approach;
        public float End => Mathf.Min(cutAt, start + approach + duration);
        public bool IsOver(float t) => t >= End;
        public bool Involves(int key) => group.IndexOf(key) >= 0;
    }

    /// <summary>
    /// The pair moments in flight, and how to build them. <c>PeopleView</c> asks <see cref="TryCue"/> for each doll every frame (like
    /// <c>KidPlay.TryCue</c> for kids' play) and lays <see cref="Apply"/> over the pose it drew; AnimBench does the same for a clip's "pair" block.
    /// </summary>
    public static class PairStage
    {
        public const float HandGap = 0.14f, GiverPace = 0.5f, HugFrom = 0.22f;
        static readonly List<PairEvent> events = new List<PairEvent>();

        public static IReadOnlyList<PairEvent> Events => events;
        public static void Clear() => events.Clear();
        public static void Add(PairEvent e) { if (e != null) events.Add(e); }

        // ------------------------------------------------------------------ building
        /// <summary>How near the giver must already be to reach the taker in a hand-over's walk-up (u of centre distance).</summary>
        public const float MaxHandOverReach = 0.62f;

        /// <summary>The giver walks up to <see cref="HandGap"/> from the taker, holds <paramref name="thing"/> out, the taker takes it and looks at it.
        /// <paramref name="window"/>: real seconds available (the beat is squeezed to fit, down to ×0.55); 0 = as long as it takes. Null: the giver is too far.</summary>
        public static PairEvent HandOver(int seed, float start, int giverKey, int takerKey, Vector2 giverPos, Vector2 takerPos, PropKind thing, float window = 0f, float maxReach = MaxHandOverReach)
        {
            var d = takerPos - giverPos;
            float dist = d.magnitude;
            if (dist > maxReach) return null;
            var dir = dist > 1e-4f ? d / dist : new Vector2(1f, 0f);
            float walk = Mathf.Max(0f, dist - HandGap);
            float approach = walk < 0.02f ? 0.25f : Mathf.Max(0.25f, walk / GiverPace);
            float k = window > 0f ? Mathf.Clamp((window - approach) / PairBeats.HandOverSeconds, 0.55f, 1f) : 1f;
            var anchor = takerPos - dir * (HandGap * 0.5f); // the taker stays where it stands
            var g = new PairGroup(seed, start + approach, giverKey, takerKey, PairKind.HandOver, anchor, dir, HandGap);
            return new PairEvent(PairKind.HandOver, g, start, approach, PairBeats.HandOverSeconds * k + 0.05f, thing, k, dir, 0f);
        }

        /// <summary>Two walk side by side along <paramref name="heading"/> from <paramref name="leadPos"/> for <paramref name="seconds"/>; the side walks at the lead's right hand.</summary>
        public static PairEvent WalkTogether(int seed, float start, int leadKey, int sideKey, Vector2 leadPos, Vector2 heading, float seconds, float speed = 0.14f)
        {
            var g = new PairGroup(seed, start, leadKey, sideKey, PairKind.WalkTogether, leadPos, heading, speed: speed, side: PairBeats.WalkGap);
            return new PairEvent(PairKind.WalkTogether, g, start, 0f, seconds, PropKind.None, 1f, g.heading, speed);
        }

        /// <summary>A hug: they close in (arms wide on the way), squeeze, sway, step back. Only with <see cref="BeatFlags.Hugs"/>, unless <paramref name="force"/> (the debug cheat and the clip).</summary>
        public static PairEvent Hug(int seed, float start, int aKey, int bKey, Vector2 aPos, Vector2 bPos, bool force = false, bool liftSecond = false)
        {
            if (!BeatFlags.Hugs && !force) return null;
            var d = bPos - aPos;
            float dist = d.magnitude;
            var dir = dist > 1e-4f ? d / dist : new Vector2(1f, 0f);
            var anchor = (aPos + bPos) * 0.5f;
            float walk = Mathf.Max(0f, dist - HugFrom) * 0.5f;
            float approach = walk < 0.02f ? 0f : Mathf.Clamp(walk / 0.4f, 0.2f, 1.5f);
            var g = new PairGroup(seed, start + approach, aKey, bKey, PairKind.Hug, anchor, dir, HugFrom);
            return new PairEvent(PairKind.Hug, g, start, approach, PairBeats.Hug.Duration, PropKind.None, 1f, dir, 0f) { liftSecond = liftSecond };
        }

        // ------------------------------------------------------------------ playing
        /// <summary>The live event this person is in at <paramref name="t"/>, and what it says about them. Over events are dropped.</summary>
        public static bool TryCue(int key, float t, out PairEvent ev, out PairCue cue)
        {
            for (int n = events.Count - 1; n >= 0; n--)
                if (t > events[n].End + 2f) events.RemoveAt(n);
            for (int n = 0; n < events.Count; n++)
            {
                var e = events[n];
                int i = e.group.IndexOf(key);
                if (i < 0 || t < e.start || e.IsOver(t)) continue;
                ev = e; cue = CueOf(e, i, t);
                return true;
            }
            ev = null; cue = default;
            return false;
        }

        /// <summary>The event covering <paramref name="key"/> at <paramref name="t"/> in a given list (AnimBench keeps its own).</summary>
        public static PairCue CueOf(PairEvent e, int i, float t)
        {
            var mc = e.group.Cue(i, t);
            var c = new PairCue { kind = e.kind, index = i, pos = mc.target, face = mc.face, speed = 0.3f, role = mc.role, slot = PropSlot.Hand };
            float since = t - e.BeatStart;
            switch (e.kind)
            {
                case PairKind.HandOver:
                {
                    c.role = i == 0 ? GroupRole.Giver : GroupRole.Taker;
                    c.speed = since < 0f ? GiverPace : 0.2f;
                    bool given = since >= e.giverBeat.ContactAt;
                    c.propMode = 1; // the thing is in exactly one pair of hands; the giver's own tool comes back when the event is over
                    c.prop = i == 0 ? (given ? PropKind.None : e.thing) : (given ? e.thing : PropKind.None);
                    break;
                }
                case PairKind.WalkTogether:
                    c.speed = e.walkSpeed * 1.5f + 0.1f;
                    break;
                case PairKind.Hug:
                {
                    c.role = i == 0 ? GroupRole.HugA : GroupRole.HugB;
                    c.speed = since < 0f ? 0.4f : 0.5f;
                    float g = HugGap(e, since);
                    var dir = e.heading;
                    var anchor = e.group.anchor;
                    c.pos = anchor + dir * (i == 0 ? -g * 0.5f : g * 0.5f);
                    c.face = anchor + dir * (i == 0 ? g * 0.5f : -g * 0.5f);
                    break;
                }
            }
            return c;
        }

        /// <summary>The distance between the huggers (centre to centre) <paramref name="since"/> seconds into the beat.</summary>
        public static float HugGap(PairEvent e, float since)
        {
            float f = PairBeats.Hug.StartOf(BeatPhase.Follow);
            if (since < 0f) return HugFrom;
            if (since < PairBeats.HugArrive) return Mathf.Lerp(HugFrom, PairBeats.HugGapClose, Easing.Apply(Ease.Smooth, since / PairBeats.HugArrive));
            if (since < f) return PairBeats.HugGapClose;
            return Mathf.Lerp(PairBeats.HugGapClose, PairBeats.HugGapClose + PairBeats.HugStepBack, Easing.Apply(Ease.Smooth, (since - f) / 0.3f));
        }

        /// <summary>Lays the event's beat over <paramref name="pose"/> (the loop pose the doll drew). <paramref name="stride"/>: the walk's phase (for the joined hands).</summary>
        public static void Apply(PairEvent e, int i, float t, ref DollPose pose, float stride)
        {
            float since = t - e.BeatStart;
            switch (e.kind)
            {
                case PairKind.HandOver:
                    if (i == 0) e.giverBeat.Apply(ref pose, since);
                    else { e.takerBeat.Apply(ref pose, since - PairBeats.TakerLag * e.k); pose.hit = 0f; } // one hit per hand-over: the giver's
                    break;
                case PairKind.WalkTogether:
                    PairBeats.WalkTogether(ref pose, i == 0, t, stride, e.group.seed);
                    break;
                case PairKind.Hug:
                    PairBeats.Hug.Apply(ref pose, since);
                    if (i == 1) pose.hit = 0f;
                    PairBeats.HugSway(ref pose, since, i == 1, i == 1 && e.liftSecond);
                    break;
            }
        }
    }
}
