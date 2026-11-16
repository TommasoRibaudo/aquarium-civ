// #708 with B39.c (docs/design/animation-specs.md "B39.c Fish: kids peer into the water and try to catch one by hand"): one or two kids
// at the pond's real edge, kneeling, watching the fish shadows (B70.b), a slow hand into the water, a grab, the splash, an empty dripping
// hand, a shake and a laugh. They never catch one. Pure (Mathf, Vector2/3, the beat kit), so Tools/AnimBench compiles it like KidPlay2.cs.
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// B39.c: kneel at the edge (B4's kneel), lean far (45°) over the water, watch; a shadow drifts close (the owner's fish come to
    /// <see cref="Lures"/>); the hand goes down slowly (arm x 45 → 80 in 1.2 s), holds, grabs (a Snap, the Contact: a ring and six droplets,
    /// the fish darts from the ring); the hand comes up empty, two drops fall from it; the kid looks at it, shakes it (z ±20° at 5 Hz) and
    /// laughs. One grab in six splashes its own face (B16.b's miss: a jolt back). Two kids grab in turn, never together.
    /// </summary>
    public sealed class FishGrabGame : KidGame
    {
        public const float Apart = 0.22f, WatchMin = 2.2f, WatchMax = 3.6f, ShakeFor = 0.6f, ShakeHz = 5f, ShakeDeg = 20f, LaughFor = 0.9f;
        /// <summary>How far out over the water the hand goes in (box units from the edge), and where the kid kneels (in from the edge).</summary>
        public const float HandOut = 0.05f, KneelIn = 0.035f;
        /// <summary>One grab in this many splashes the kid's own face.</summary>
        public const int FaceEvery = 6;

        static PoseKey Kneel => PoseKey.Hold.Squash(0.74f).Lean(45f).Offset(0f, -0.008f, -0.02f).ArmL(20f, 0f, -12f).ArmR(45f, 0f, 8f);

        /// <summary>The grab: the slow reach (1.2 s), the hold, the Snap (the Contact), the hand up empty, the look at it.</summary>
        public static readonly Beat Grab = new Beat(
            Beat.Step(BeatPhase.Antic, 1.2f, Ease.Smooth, Kneel.ArmR(80f, 0f, 6f).Lean(48f)),
            Beat.Step(BeatPhase.Hold, 0.5f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Act, 0.1f, Ease.Snap, PoseKey.Hold.ArmR(100f, 0f, 4f).Lean(52f).Squash(0.7f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.4f, Ease.Out, PoseKey.Hold.ArmR(30f, 0f, 10f).Lean(30f).Squash(0.78f)),
            Beat.Step(BeatPhase.Hold, 0.5f, Ease.Smooth, PoseKey.Hold.ArmR(75f, 0f, 22f).Lean(14f).Squash(0.8f)),
            Beat.Step(BeatPhase.EndBeat, ShakeFor, Ease.Linear, PoseKey.Hold));

        /// <summary>B16.b's miss on the kid itself: a jolt back, arms up to the face; held; back.</summary>
        static readonly Beat Face = new Beat(
            Beat.Step(BeatPhase.Act, 0.12f, Ease.Out, PoseKey.Hold.Offset(0f, 0f, 0.035f).Squash(0.82f).Arms(80f, 28f).Lean(-10f)),
            Beat.Step(BeatPhase.Hold, 0.35f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.3f, Ease.Smooth, PoseKey.Hold.Squash(0.8f).Lean(10f)));

        public enum Part { Watch, Grab, Face, Laugh, Settle }

        readonly PlaySite site;
        readonly Vector2[] spot, hand;
        readonly Vector2 along, toWater;

        public FishGrabGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre, PlaySite site) : base(PlayGame.FishGrab, seed, t0, members, centre)
        {
            this.site = site ?? new PlaySite { water = true, edge = centre };
            var land = this.site.toLand.sqrMagnitude > 1e-6f ? this.site.toLand.normalized : new Vector2(0f, -1f);
            toWater = -land;
            along = new Vector2(-land.y, land.x);
            int n = Count;
            spot = new Vector2[n]; hand = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float off = n == 1 ? 0f : (i == 0 ? -Apart * 0.5f : Apart * 0.5f);
                spot[i] = this.site.edge + land * KneelIn + along * off;
                hand[i] = this.site.edge + toWater * HandOut + along * off;
            }
        }

        public PlaySite Site => site;
        /// <summary>Where member <paramref name="i"/>'s hand goes into the water (box x, z).</summary>
        public Vector2 HandAt(int i) => hand[i];

        /// <summary>A round's length for a member (the watch is hashed per round).</summary>
        float Watch(int i, int r) => BeatHash.Range(WatchMin, WatchMax, seed + i * 977, r, 708);
        bool FaceSplash(int i, int r) => BeatHash.Pick(FaceEvery, seed + i * 31, r, 709) == 0;
        float RoundLen(int i, int r) => Watch(i, r) + Grab.Duration + (FaceSplash(i, r) ? Face.Duration : 0f) + LaughFor + 0.4f;
        /// <summary>The second kid starts half a round late, so the two never grab together.</summary>
        float Delay(int i) => i == 0 ? 0.3f : 0.3f + (WatchMin + Grab.ContactAt) * 0.75f;

        /// <summary>Which round member <paramref name="i"/> is in at <paramref name="local"/> s, and the seconds into it.</summary>
        int RoundAt(int i, float local, out float w)
        {
            w = local - Delay(i);
            int r = 0;
            while (w >= RoundLen(i, r) && r < 10000) { w -= RoundLen(i, r); r++; }
            return r;
        }

        /// <summary>The part of the round at <paramref name="w"/> s into round <paramref name="r"/>, and the seconds into that part.</summary>
        public Part PartAt(int i, int r, float w, out float into)
        {
            float watch = Watch(i, r);
            if (w < watch) { into = w; return Part.Watch; }
            w -= watch;
            if (w < Grab.Duration) { into = w; return Part.Grab; }
            w -= Grab.Duration;
            if (FaceSplash(i, r)) { if (w < Face.Duration) { into = w; return Part.Face; } w -= Face.Duration; }
            if (w < LaughFor) { into = w; return Part.Laugh; }
            into = w - LaughFor;
            return Part.Settle;
        }

        public override PlayCue Cue(int i, float t)
        {
            float local = Mathf.Max(0f, t - t0);
            var cue = new PlayCue { role = GroupRole.Splasher, hitOn = NoHit, under = DollStyle.Crouch, style = DollStyle.Crouch, pos = spot[i], face = hand[i] + toWater * 0.2f };
            int r = RoundAt(i, local, out float w);
            cue.round = r;
            if (w < 0f) { cue.since = w; return cue; }
            var part = PartAt(i, r, w, out float into);
            cue.since = into;
            switch (part)
            {
                case Part.Grab:
                    cue.style = DollStyle.Reach; cue.hitOn = Water;
                    cue.endBeat = into >= Grab.ContactAt;
                    break;
                case Part.Face: cue.style = DollStyle.Flinch; cue.endBeat = true; break;
                case Part.Laugh: cue.style = DollStyle.Laugh; cue.under = DollStyle.Laugh; cue.endBeat = true; break;
            }
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            float local = Mathf.Max(0f, t - t0);
            int r = RoundAt(i, local, out float w);
            var basePose = Kneel.Resolve(DollPose.Rest, DollPose.Rest);
            if (w < 0f) { p = basePose; return; }
            var part = PartAt(i, r, w, out float into);
            switch (part)
            {
                case Part.Watch:
                    p = basePose;
                    p.lean += Mathf.Sin(into * 1.3f + i) * 2f; // still, a small sway as it watches
                    break;
                case Part.Grab:
                    p = basePose;
                    Grab.Apply(ref p, into);
                    if (into >= Grab.Duration - ShakeFor)
                    {   // the shake: the wet hand flicked side to side
                        float s = into - (Grab.Duration - ShakeFor);
                        p.armR.z += Mathf.Sin(s * ShakeHz * Mathf.PI * 2f) * ShakeDeg;
                    }
                    break;
                case Part.Face:
                    p = basePose;
                    p.armR = new Vector3(75f, 0f, 22f); p.lean = 14f;
                    Face.Apply(ref p, into);
                    break;
                case Part.Laugh:
                    // DollMotion's laugh (the cue's under) is drawn kneeling: keep the kneel's height and a little of its lean
                    p.squash = Mathf.Min(p.squash, 0.82f);
                    p.lean = Mathf.Max(p.lean, 8f);
                    break;
                default:
                {
                    p = basePose;
                    float u = Mathf.Clamp01(into / 0.4f);
                    p.lean = Mathf.Lerp(12f, 45f, u);
                    break;
                }
            }
        }

        public override void Effects(float from, float to, List<PlayFx> into)
        {
            float a = from - t0, b = to - t0;
            if (b <= 0f) return;
            for (int i = 0; i < Count; i++)
            {
                int r0 = RoundAt(i, Mathf.Max(0f, a), out _), r1 = RoundAt(i, b, out _);
                for (int r = Mathf.Max(0, r0 - 1); r <= r1; r++)
                {
                    float start = Delay(i);
                    for (int k = 0; k < r; k++) start += RoundLen(i, k);
                    float contact = start + Watch(i, r) + Grab.ContactAt;
                    var h = hand[i];
                    if (contact > a && contact <= b)
                    {
                        into.Add(new PlayFx { kind = PlayFxKind.Ring, at = t0 + contact, pos = new Vector3(h.x, 0f, h.y), wet = true, by = i, on = -1, scale = 0.7f, mud = site.mud });
                        if (FaceSplash(i, r))
                        {
                            var face = new Vector3(spot[i].x, KidScale * BodyH * 0.8f, spot[i].y);
                            into.Add(new PlayFx { kind = PlayFxKind.Droplets, at = t0 + contact, pos = new Vector3(h.x, 0.01f, h.y), aim = face, aimed = true, wet = true, count = 6, by = i, on = i, scale = 0.8f, mud = site.mud });
                        }
                        else into.Add(new PlayFx { kind = PlayFxKind.Droplets, at = t0 + contact, pos = new Vector3(h.x, 0.01f, h.y), wet = true, count = 6, by = i, on = -1, scale = 0.7f, mud = site.mud });
                    }
                    float drip = contact + 0.35f; // two drops from the empty hand, over the bank's edge
                    if (drip > a && drip <= b)
                    {
                        var d = Vector2.Lerp(spot[i], h, 0.55f);
                        into.Add(new PlayFx { kind = PlayFxKind.Droplets, at = t0 + drip, pos = new Vector3(d.x, 0.05f, d.y), wet = true, count = 2, by = i, on = -1, scale = 0.35f, mud = site.mud });
                    }
                }
            }
        }

        /// <summary>The hands a fish should come to now: each kid watching or reaching (B39.c: "a shadow drifts close").</summary>
        public void Lures(float t, List<Vector2> into)
        {
            float local = Mathf.Max(0f, t - t0);
            for (int i = 0; i < Count; i++)
            {
                int r = RoundAt(i, local, out float w);
                if (w < 0f) continue;
                var part = PartAt(i, r, w, out float s);
                if (part == Part.Watch || part == Part.Grab && s < Grab.ContactAt) into.Add(hand[i] + toWater * 0.03f);
            }
        }
    }
}
