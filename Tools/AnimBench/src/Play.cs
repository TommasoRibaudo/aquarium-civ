// #358 AnimBench: a kids' play group in a clip. The clip's "play" block names a game (AquariumCiv.Life.PlayGame), its members (doll
// ids), where it plays and its seed; the bench then drives those dolls from the game's own pure code (View/Beats/KidPlay.cs), as
// KidPlayDirector does in PeopleView: the doll stands on the cue's spot, faces the cue's point, shows the cue's move with the game's
// beats on top. The group's ball is an object of kind "ball" ("ball": its id): in the holder's hands (the middle of the two hands),
// else on the game's arc, which starts at the hands where it left them. Every throw is an arc event for the arcs principle.
//
//   "play": { "game": "Tag", "members": ["kidA", "kidB"], "center": [0, 0], "seed": 7, "t0": 0, "ball": "ball" }
//
// The rabbit chase looks for the rabbits (objects of kind "rabbit") within 1.2 u of the centre at the start of each chase.
//
// #359 (B3): the games' real things are clip objects: "water" (a pond or puddle object; the pond's edge is found from its middle
// out towards "center"), "tree" (the trunk), "base" and "covers" (hide and seek), "autumn"; "spray": an object id that follows each
// splash's aimed droplets (an arc event, so `arcs` checks they leave the hands and land on the partner). A splash's or a jump's
// contact is aimed at the water object, a slap on the trunk at the tree (both answer: "ripple"/"sway"). Wading sinks the doll.
using System.Collections.Generic;
using System.Text.Json.Nodes;
using AquariumCiv.Life;
using AquariumCiv.View;
using UnityEngine;

namespace AnimBench
{
    public sealed class PlaySpec
    {
        public PlayGame Game;
        public readonly List<string> Members = new List<string>();
        public Vector2 Centre;
        public int Seed = 1;
        public float T0;
        public string Ball;
        /// <summary>#359: the B3 games' real things (object ids) and the droplets' arc object.</summary>
        public string Water, Tree, Base, Spray;
        public readonly List<string> Covers = new List<string>();
        public bool Autumn;

        public static PlaySpec Parse(JsonObject o)
        {
            var p = new PlaySpec
            {
                Game = Clip.Enum<PlayGame>(Clip.Str(o, "game") ?? "None", "play game"),
                Seed = Clip.Int(o, "seed", 1),
                T0 = Clip.Num(o, "t0", 0f),
                Ball = Clip.Str(o, "ball"),
            };
            if (o["center"] != null || o["centre"] != null) p.Centre = Clip.XZ(o["center"] ?? o["centre"], "play center");
            if (o["members"] is JsonArray m) foreach (var x in m) p.Members.Add(x.GetValue<string>());
            p.Water = Clip.Str(o, "water"); p.Tree = Clip.Str(o, "tree"); p.Base = Clip.Str(o, "base"); p.Spray = Clip.Str(o, "spray"); // #359
            p.Autumn = Clip.Bool(o, "autumn", false);
            if (o["covers"] is JsonArray cv) foreach (var x in cv) p.Covers.Add(x.GetValue<string>());
            if (p.Members.Count == 0) throw new ClipException("play: needs members (doll ids)");
            return p;
        }
    }

    /// <summary>One throw the play group made (the arcs principle): the ball leaves <see cref="By"/>'s hands and lands on <see cref="Target"/> (null: a miss, on the grass).</summary>
    public sealed class ArcEvent
    {
        public string Object, By, Target;
        public float T0, T1;
    }

    public sealed partial class Simulator
    {
        KidGame play;
        readonly Dictionary<string, int> playIndex = new Dictionary<string, int>();
        readonly List<string> playIds = new List<string>();
        public readonly List<ArcEvent> ArcEvents = new List<ArcEvent>();
        int ballShot = -1;
        ArcShot ballFlight;

        void SetUpPlay()
        {
            var ps = clip.Play;
            if (ps == null) return;
            var keys = new List<int>();
            foreach (var id in ps.Members)
            {
                var d = clip.Doll(id) ?? throw new ClipException($"play: member '{id}' is not a doll");
                playIndex[id] = playIds.Count;
                playIds.Add(id);
                keys.Add(d.Seed);
            }
            play = KidGame.Create(ps.Game, ps.Seed, ps.T0, keys, ps.Centre, SiteOf(ps)) ?? throw new ClipException($"play: no game '{ps.Game}'");
            for (int i = 0; i < playIds.Count; i++) play.scales[i] = clip.Doll(playIds[i]).Scale;
            if (play is RabbitChaseGame chase) chase.HerdAt = _ => NearestRabbit(ps.Centre, 1.2f, out string _);
        }

        Vector2? NearestRabbit(Vector2 at, float within, out string id)
        {
            id = null; float best = within * within; Vector2? pos = null;
            foreach (var o in objects)
            {
                if (o.Spec.Kind != "rabbit") continue;
                var p = o.Last.State != null ? o.Last.Pos : o.Spec.Pos;
                var q = new Vector2(p.x, p.z);
                float d = (q - at).sqrMagnitude;
                if (d <= best) { best = d; pos = q; id = o.Spec.Id; }
            }
            return pos;
        }

        bool StepPlayDoll(DollState d, float t, float dt)
        {
            if (play == null || !playIndex.TryGetValue(d.Spec.Id, out int pi)) return false;
            var spec = d.Spec;
            var prev = d.Pos;
            bool jump = !d.Placed || dt <= 0f;
            var first = play.Cue(pi, t);
            d.Pos = first.pos;
            float inst = !jump ? (d.Pos - prev).magnitude / dt : 0f;
            d.Speed = Mathf.Lerp(d.Speed, inst, dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 1f);
            d.Placed = true;
            if (d.Speed > Bench.WalkStarts) d.Walking = true; else if (d.Speed < Bench.WalkStops) d.Walking = false;

            float phase = clip.Clock + t + spec.Phase;
            d.Stride = Mathf.Repeat(d.Stride + DollMotion.Cadence(first.under, d.Speed) * Mathf.PI * dt, Mathf.PI * 2f);
            var pose = play.PoseOf(pi, t, phase, d.Stride, d.Speed, out var cue);
            var style = cue.style;
            if (style != d.Drew)
            {
                d.BlendFrom = d.Pose;
                d.BlendT = pose.hidden || pose.lying || d.Pose.hidden || d.Pose.lying || jump ? Bench.BlendSeconds : 0f;
                d.Drew = style;
            }
            if (d.BlendT < Bench.BlendSeconds)
            {
                d.BlendT += dt;
                pose = DollMotion.Blend(d.BlendFrom, pose, Mathf.SmoothStep(0f, 1f, d.BlendT / Bench.BlendSeconds));
            }
            d.Pose = pose;

            // KidPlayDirector: the game says what a member faces, also while it runs (a ring dancer side-steps facing the middle).
            float want = (cue.face - d.Pos).sqrMagnitude > 1e-5f ? Bench.Yaw(d.Pos, cue.face) : d.Yaw;
            if (jump) d.Velocity = Vector2.zero;
            else d.Velocity = Vector2.Lerp(d.Velocity, (d.Pos - prev) / dt, 1f - Mathf.Exp(-dt * Bench.VelocityRate));
            float most = d.Walking ? Mathf.Lerp(Bench.SlowTurn, Bench.MostTurn, d.Speed / Bench.FaceFrom) : Bench.MostTurn;
            d.Yaw = dt > 0f ? d.Yaw + Mathf.Clamp(Mathf.DeltaAngle(d.Yaw, want) * (1f - Mathf.Exp(-dt * Bench.TurnRate)), -most * dt, most * dt) : want;

            if (cue.sink > 0f) { pose.offset.y -= cue.sink / spec.Scale; d.Pose = pose; } // #359: wading (the ground is flat at the water's surface here)
            string target = null;
            if (cue.hitOn == KidGame.Water) target = clip.Play.Water; // #359
            else if (cue.hitOn == KidGame.TreeHit) target = clip.Play.Tree;
            else if (cue.hitOn >= 0) target = playIds[cue.hitOn];
            else if (cue.hitOn == KidGame.Herd) { NearestRabbit(d.Pos, 3f, out target); }
            else if (cue.hitOn == KidGame.Release) target = HitEvent.ReleaseTarget;
            var f = new DollFrame
            {
                Pose = pose, Style = style, Pos = d.Pos, BodyYaw = d.Yaw, Yaw = d.Yaw + pose.yaw, Walking = d.Walking, Speed = d.Speed,
                Progress = 0f, Since = cue.since, Segment = 0, Step = cue.round, Hunt = HuntPhase.None, Prop = cue.stick ? PropKind.Twig : PropKind.None, Slot = AquariumCiv.View.PropSlot.Hand,
                HitTarget = target, EndBeat = cue.endBeat, InStep = cue.inStep, Playing = true,
            };
            Bench.Place(ref f, spec.Scale);
            d.Last = f;
            return true;
        }

        bool StepBall(ObjectState o, ref ObjectFrame f, float t)
        {
            if (play == null || !play.HasBall || o.Spec.Id != clip.Play.Ball) return false;
            var b = play.Ball(t);
            if (!b.Free)
            {
                var h = DollById(playIds[b.holder]).Last;
                f.Pos = (h.HandL + h.HandR) * 0.5f;
                f.Act = "held";
                return true;
            }
            if (play is SnowballGame && b.stage == ArcStage.Gone) { f.Visible = false; f.Act = "gone"; if (o.Last.State != null) f.Pos = o.Last.Pos; return true; } // #359: not made yet / burst (where it burst)
            if (b.shotIndex != ballShot)
            {
                // It leaves the hands where they are on this frame (the game's start is where it expects them).
                ballShot = b.shotIndex;
                ballFlight = b.shot;
                if (o.Last.State != null) ballFlight.start = o.Last.Pos;
                if (play is SnowballGame sg && t >= 0f)
                {
                    var sh = sg.ShotAt(b.shotIndex);
                    ArcEvents.Add(new ArcEvent { Object = o.Spec.Id, By = playIds[sh.from], Target = playIds[sh.to], T0 = play.t0 + sh.launch, T1 = play.t0 + sh.land });
                }
                if (play is BallGame bg && t >= 0f)
                {
                    var th = bg.ThrowAt(b.shotIndex);
                    ArcEvents.Add(new ArcEvent
                    {
                        Object = o.Spec.Id, By = playIds[th.from], Target = th.miss ? null : playIds[th.to],
                        T0 = play.t0 + th.launch, T1 = play.t0 + th.land,
                    });
                }
            }
            var s = Arc.Sample(ballFlight, t - play.t0);
            f.Pos = s.position;
            f.Yaw = 0f;
            f.Act = s.stage.ToString();
            return true;
        }
    }
}
