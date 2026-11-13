// #618: the kids on a frozen pond. Pure (Mathf, Vector2, DollMotion and Life types only), on the play kit like the other KidGames.
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #618 the ice slide: 2–4 kids queue on the ice behind the slide's start (<see cref="A"/>), side by side. In turn one runs up to the
    /// start and glides to the far end (<see cref="B"/>), arms out wide, slowing as it goes, with a little wobble; at the end one in three
    /// slides fall on their bottom (the others whoop, arms up), and the slider walks back round the side of the slide to the queue. The
    /// next one runs as the slider reaches the end, so there is always someone on the move. Those queuing watch the slider and cheer a
    /// landing. Every point is on inner ice (PondIce.SlideLine), so nobody stands in open water or on the bank.
    /// </summary>
    public sealed class IceSlideGame : KidGame
    {
        /// <summary>Seconds: the run-up, the glide, the end beat (fall or whoop), the walk back.</summary>
        public const float RunUp = 0.7f, Glide = 1.5f, EndFor = 1.0f, WalkBack = 2.2f;
        /// <summary>The queue: the first stands this far behind the start, the next ones this much further back, a little to the side.</summary>
        public const float QueueBack = 0.09f, QueueStep = 0.07f, QueueSide = 0.04f;
        /// <summary>The walk back keeps this far to the side of the slide.</summary>
        public const float BackSide = 0.09f;
        /// <summary>One slide in this many ends in a fall.</summary>
        public const int FallEvery = 3;

        public readonly Vector2 A, B;
        /// <summary>Seconds between one slider's start and the next one's: the run-up and the glide, or longer with few kids, so a
        /// slider is always back in the queue before their own next turn.</summary>
        public readonly float Turn;
        readonly Vector2 dir, side;
        readonly Vector2[] queue;

        public IceSlideGame(int seed, float t0, IReadOnlyList<int> members, Vector2 a, Vector2 b) : base(PlayGame.IceSlide, seed, t0, members, a)
        {
            A = a; B = b;
            dir = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector2.right;
            side = new Vector2(-dir.y, dir.x);
            Turn = Mathf.Max(TurnSeconds, (AwaySeconds + 0.3f) / Mathf.Max(1, Count));
            queue = new Vector2[Count];
            for (int i = 0; i < Count; i++) queue[i] = QueueSpot(i);
        }

        /// <summary>Where member <paramref name="i"/> waits: behind the start, a short line along the slide.</summary>
        public Vector2 QueueSpot(int i) => A - dir * (QueueBack + QueueStep * i) + side * (i % 2 == 0 ? QueueSide : -QueueSide) * 0.5f;

        /// <summary>The seconds from one slider's start to the next one's (the run-up and the glide).</summary>
        public static float TurnSeconds => RunUp + Glide;
        /// <summary>A slider is away this long (run-up, glide, end beat, walk back).</summary>
        public static float AwaySeconds => RunUp + Glide + EndFor + WalkBack;

        public bool Falls(int turn) => (turn + BeatHash.Pick(FallEvery, seed, 1, 618)) % FallEvery == 0;

        /// <summary>Where the glide is, 0..1 of the way from A to B (eases out: fast off the run-up, slowing to a stop).</summary>
        public static float GlideAt(float u) { u = Mathf.Clamp01(u); return 1f - (1f - u) * (1f - u); }

        public override PlayCue Cue(int i, float t)
        {
            float local = Mathf.Max(0f, t - t0);
            int n = Count;
            var cue = new PlayCue { hitOn = NoHit, pos = queue[i], face = B, under = DollStyle.Stand, style = DollStyle.Stand, role = GroupRole.Watcher };
            // My latest turn: turns go round the members in order (k % n == i), one every TurnSeconds.
            int now = Mathf.FloorToInt(local / Turn);
            int mine = now - ((now - i) % n + n) % n; // the last turn index that is mine, at or before now
            if (mine >= 0)
            {
                float s = local - mine * Turn;
                if (s < AwaySeconds)
                {
                    cue.round = mine; cue.role = GroupRole.Runner;
                    if (s < RunUp)
                    {
                        float u = s / RunUp;
                        cue.pos = Vector2.Lerp(queue[i], A, u); cue.face = B; cue.style = cue.under = DollStyle.Run;
                        cue.speed = (A - queue[i]).magnitude / RunUp; cue.since = s;
                        return cue;
                    }
                    s -= RunUp;
                    if (s < Glide)
                    {
                        cue.pos = Vector2.Lerp(A, B, GlideAt(s / Glide)); cue.face = B; cue.style = DollStyle.Stand; cue.under = DollStyle.Stand;
                        cue.speed = 0f; cue.since = s; cue.stick = false;
                        return cue;
                    }
                    s -= Glide;
                    if (s < EndFor)
                    {
                        cue.pos = B; cue.face = B + dir; cue.since = s; cue.endBeat = true;
                        cue.style = cue.under = Falls(mine) ? DollStyle.Sit : DollStyle.Cheer;
                        return cue;
                    }
                    s -= EndFor;
                    // walk back round the side of the slide
                    float w = s / WalkBack;
                    var off = B + side * BackSide; var offA = A + side * BackSide;
                    Vector2 p = w < 0.15f ? Vector2.Lerp(B, off, w / 0.15f) : w < 0.85f ? Vector2.Lerp(off, offA, (w - 0.15f) / 0.7f) : Vector2.Lerp(offA, queue[i], (w - 0.85f) / 0.15f);
                    cue.pos = p; cue.style = cue.under = DollStyle.Walk; cue.speed = ((B - A).magnitude + 2f * BackSide) / WalkBack; cue.since = s;
                    cue.face = p + (w < 0.15f ? side : w < 0.85f ? -dir : (queue[i] - offA));
                    return cue;
                }
            }
            // queuing: watch the slider; cheer the one who just landed (never all the same move: a wave or a laugh round the line)
            int current = now; // the turn on the move now
            int slider = ((current % n) + n) % n;
            float sinceTurn = local - current * Turn;
            cue.face = slider != i ? Vector2.Lerp(A, B, 0.6f) : B;
            // the newest landing: this turn's slider once it is past its glide, else the last turn's (a slide lands RunUp + Glide after its start)
            int landed = sinceTurn >= RunUp + Glide ? current : current - 1;
            float sinceLanding = local - landed * Turn - (RunUp + Glide);
            if (landed >= 0 && sinceLanding >= 0f && sinceLanding < EndFor && ((landed % n) + n) % n != i)
            {
                cue.style = cue.under = Falls(landed) ? DollStyle.Laugh : (i + landed) % 2 == 0 ? DollStyle.Cheer : DollStyle.Wave;
                cue.since = sinceLanding; cue.endBeat = true; cue.face = B;
            }
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            if (c.role != GroupRole.Runner || c.style != DollStyle.Stand) return;
            // the glide: arms out wide, a slight crouch and lean back, a wobble that settles as it slows
            float u = Mathf.Clamp01(c.since / Glide);
            float wob = Mathf.Sin(c.since * 9f + seed % 7) * (1f - u);
            p.armL = new Vector3(0f, 0f, -85f + wob * 20f);
            p.armR = new Vector3(0f, 0f, 85f + wob * 20f);
            p.roll = wob * 9f;
            p.lean = -6f + u * 4f;
            p.squash = 0.93f;
        }
    }
}
