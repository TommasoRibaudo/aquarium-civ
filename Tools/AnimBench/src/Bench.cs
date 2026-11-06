// #352 AnimBench: steps a clip frame by frame the way PeopleView.Drive does (walking from the speed, facing, the pose clock and
// phase, loop step, progress and since, the blend on a change of move, the hunt overlay), then places the doll's parts the way
// PegDollView builds them. Objects answer through the real pure models where there are any (rabbits: Sim.Eco.RabbitHerd.Scare).
//
// What is NOT PeopleView (an approximation, say so when you report): the doll stands exactly on its planned point (no crowd
// easing, no catch-up glide, no slot search: the clip gives the spot), the ground is flat (y = 0), and a rabbit's day is a fixed
// grazing spot (no RabbitHerd.Field wandering).
using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View;
using UnityEngine;
using PropSlot = AquariumCiv.View.PropSlot;

namespace AnimBench
{
    /// <summary>An affine frame: an origin and three axis vectors (columns), scale included. World = O + X*x + Y*y + Z*z.</summary>
    public struct Frame
    {
        public Vector3 O, X, Y, Z;
        public Vector3 Point(Vector3 p) => O + X * p.x + Y * p.y + Z * p.z;
        public Vector3 Dir(Vector3 d) => X * d.x + Y * d.y + Z * d.z;
        /// <summary>A child at local position <paramref name="pos"/>, rotation <paramref name="rot"/>, scale <paramref name="scale"/> (Unity's parent/child TRS).</summary>
        public Frame Child(Vector3 pos, Quaternion rot, Vector3 scale) => new Frame
        {
            O = Point(pos),
            X = Dir(rot * new Vector3(scale.x, 0, 0)),
            Y = Dir(rot * new Vector3(0, scale.y, 0)),
            Z = Dir(rot * new Vector3(0, 0, scale.z)),
        };
        public float[] ToArray() => new[] { O.x, O.y, O.z, X.x, X.y, X.z, Y.x, Y.y, Y.z, Z.x, Z.y, Z.z };
    }

    /// <summary>What one doll is at one frame (everything the track, the renderer and the principle checks need).</summary>
    public struct DollFrame
    {
        public DollPose Pose;
        public DollStyle Style;
        public Vector2 Pos;
        /// <summary>d.yaw (the body's facing, PeopleView) and the drawn yaw (d.yaw + pose.yaw).</summary>
        public float BodyYaw, Yaw;
        public bool Walking;
        public float Speed, Progress, Since;
        public int Segment, Step;
        public HuntPhase Hunt;
        public PropKind Prop;
        public PropSlot Slot;
        public Frame Body, ArmL, ArmR;
        public Frame? PropFrame;
        public Vector3 HandL, HandR, Head;
        /// <summary>The working end of a hand tool (spear point, axe head), else the right hand.</summary>
        public Vector3 Tip;
        /// <summary>#358 play groups: whom this frame's hit lands on (a doll or object id; <see cref="HitEvent.ReleaseTarget"/> for a throw), the round's
        /// payoff (P5), moving in step on purpose (the ring dance: exempt from P6), and driven by a play group at all.</summary>
        public string HitTarget;
        public bool EndBeat, InStep, Playing;
    }

    public struct ObjectFrame
    {
        public Vector3 Pos;
        public float Yaw;
        public string Act;
        public bool Visible;
        public float Phase;
        public Dictionary<string, float> State;
    }

    public static class Bench
    {
        // PeopleView's tuning (copied: PeopleView is a MonoBehaviour, these are its private consts).
        public const float BlendSeconds = 0.16f, WalkStops = 0.03f, WalkStarts = 0.05f;
        public const float FaceFrom = 0.2f, SlowTurn = 110f, VelocityRate = 10f, TurnRate = 9f, MostTurn = 300f;

        /// <summary>PeopleView.Hash01 (a doll's phase is Hash01(key, 5) * 37).</summary>
        public static float Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>PeopleView.Yaw: the yaw (degrees) that faces from one point to another (the doll's front is local -z).</summary>
        public static float Yaw(Vector2 from, Vector2 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 180f;
        }

        // ---------------------------------------------------------------- the doll's build (PegDoll / PegDollView)
        public const float BodyH = 0.13f, ShoulderX = 0.046f, ArmLen = 0.075f, HeadR = 0.043f, BodyR = 0.055f;
        public static float HeadY => BodyH + 0.04f;
        /// <summary>One doll-width (the tunic's hem across), adult units.</summary>
        public const float DollWidth = 2f * BodyR;
        public const float ToolTilt = 22f;
        static readonly Quaternion AlongArm = Quaternion.Euler(180f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);

        /// <summary>PegDoll.SlotPose (copied; PegDoll.cs builds meshes, so it stays out of the bench).</summary>
        public static (Vector3 pos, Quaternion rot) SlotPose(PropSlot slot) => slot switch
        {
            PropSlot.Head => (new Vector3(0, HeadY + HeadR + 0.008f, 0), Quaternion.identity),
            PropSlot.Back => (new Vector3(0, BodyH * 0.75f, 0.062f), Quaternion.identity),
            PropSlot.Shoulder => (new Vector3(ShoulderX * 0.6f, BodyH + 0.008f, 0), Quaternion.Euler(0, 90f, 0)),
            PropSlot.Arms => (new Vector3(0, BodyH * 0.6f, -0.058f), Quaternion.identity),
            PropSlot.Front => (new Vector3(0, 0f, -0.11f), Quaternion.identity),
            _ => (new Vector3(0, -ArmLen, 0), Quaternion.identity),
        };

        /// <summary>The working end of a hand prop in its own frame (from PegDoll.BuildProp's geometry).</summary>
        public static Vector3 ToolTip(PropKind k) => k switch
        {
            PropKind.Spear => new Vector3(0, 0.205f, 0),
            PropKind.Axe => new Vector3(0, 0.115f, -0.056f),
            PropKind.Hoe => new Vector3(0, 0.17f, -0.045f),
            PropKind.Rod => new Vector3(0, 0.29f, 0),
            PropKind.MeasuringRod => new Vector3(0, 0.29f, 0),
            PropKind.Hammer => new Vector3(0, 0.1f, -0.03f),
            PropKind.Broom => new Vector3(0, -0.09f, 0),
            PropKind.Stick => new Vector3(0, 0.18f, 0),
            PropKind.Fish => new Vector3(0, -0.01f, -0.012f),
            _ => Vector3.zero,
        };

        public static bool IsTool(PropKind k) => k is PropKind.Axe or PropKind.Hoe or PropKind.Spear or PropKind.Fish;

        /// <summary>Places the parts of a doll for a pose (PeopleView.Drive's transform, PegDollView.ApplyArms/SetProp/SetGrip).</summary>
        public static void Place(ref DollFrame f, float s)
        {
            var pose = f.Pose;
            var yawQ = Quaternion.Euler(0, f.BodyYaw + pose.yaw, 0);
            var ground = new Vector3(f.Pos.x, 0f, f.Pos.y);
            var tilt = pose.lying ? Quaternion.Euler(90f, 0, 0) : Quaternion.Euler(-pose.lean, 0, pose.roll);
            var root = new Frame { O = ground + yawQ * (pose.offset * s), X = Vector3.right, Y = Vector3.up, Z = Vector3.forward };
            var body = root.Child(Vector3.zero, yawQ * tilt, new Vector3(1f, pose.squash, 1f));
            f.Body = body;
            float sy = BodyH * 0.92f * s, sx = ShoulderX * s;
            f.ArmL = body.Child(new Vector3(-sx, sy, 0), Quaternion.Euler(pose.armL), Vector3.one);
            f.ArmR = body.Child(new Vector3(sx, sy, 0), Quaternion.Euler(pose.armR), Vector3.one);
            f.HandL = f.ArmL.Point(new Vector3(0, -ArmLen * s, 0));
            f.HandR = f.ArmR.Point(new Vector3(0, -ArmLen * s, 0));
            f.Head = body.Point(new Vector3(0, HeadY * s, 0));
            f.Tip = f.HandR;
            f.PropFrame = null;
            if (f.Prop == PropKind.None) return;
            var (pos, rot) = SlotPose(f.Slot);
            float ps = Mathf.Lerp(1f, s, 0.5f);
            if (f.Slot == PropSlot.Hand)
            {
                var rest = Quaternion.Inverse(Quaternion.Euler(-35f, 0, 10f)) * Quaternion.Euler(0, 0, IsTool(f.Prop) ? -ToolTilt : 0f) * rot;
                var pr = f.ArmR.Child(pos * s, pose.extend ? AlongArm : rest, Vector3.one * ps);
                f.PropFrame = pr;
                if (ToolTip(f.Prop) != Vector3.zero) f.Tip = pr.Point(ToolTip(f.Prop));
            }
            else f.PropFrame = body.Child(pos * s, rot, Vector3.one * ps);
        }
    }

    /// <summary>One doll's running state between frames (PeopleView.Doll).</summary>
    sealed class DollState
    {
        public DollSpec Spec;
        public Vector2 Pos;
        public float Yaw, Speed, Stride;
        public Vector2 Velocity;
        public bool Walking, Placed;
        public DollPose Pose, BlendFrom;
        public DollStyle Drew;
        public float BlendT = Bench.BlendSeconds;
        public DollFrame Last;
    }

    /// <summary>One object's running state (a rabbit's scare, a bush's berries).</summary>
    sealed class ObjectState
    {
        public ObjectSpec Spec;
        public RabbitHerd.Scare Scare;
        public float Berries, DropClock;
        public ObjectFrame Last;
        public Vector3 ArcFrom, ArcTo;
        public bool ArcLaunched;
    }

    public sealed partial class Simulator
    {
        readonly Clip clip;
        readonly List<DollState> dolls = new List<DollState>();
        readonly List<ObjectState> objects = new List<ObjectState>();
        readonly RabbitHerd.Field field;
        readonly List<float> px = new List<float>(), pz = new List<float>(), none = new List<float>();

        public Simulator(Clip clip)
        {
            this.clip = clip;
            foreach (var d in clip.Dolls) dolls.Add(new DollState { Spec = d });
            foreach (var o in clip.Objects)
            {
                var st = new ObjectState { Spec = o };
                if (o.Kind == "rabbit") st.Scare = new RabbitHerd.Scare();
                if (o.State.TryGetValue("berries", out var b)) st.Berries = b;
                objects.Add(st);
            }
            // The rabbits' field: all green, so a scared rabbit may bound anywhere in the box.
            field = new RabbitHerd.Field();
            var cover = new byte[EcoLand.N];
            for (int i = 0; i < cover.Length; i++) cover[i] = (byte)EcoCover.Grass;
            field.Refresh(cover, new bool[EcoLand.N], 352, -1);
            SetUpPlay(); // #358
        }

        /// <summary>Runs the clip: the warm-up (not recorded), then every frame.</summary>
        public Track Run()
        {
            var track = new Track(clip);
            float dt = clip.Dt;
            int warm = (int)Math.Round(clip.Warmup * clip.Fps);
            bool first = true;
            for (int i = -warm; i < clip.FrameCount; i++)
            {
                float t = i * dt;
                Step(t, first ? 0f : dt);
                first = false;
                if (i >= 0) track.Record(i, t, dolls.ConvertAll(d => d.Last), objects.ConvertAll(o => o.Last));
            }
            track.ArcEvents.AddRange(ArcEvents); // #358
            track.Finish();
            return track;
        }

        void Step(float t, float dt)
        {
            float tc = Mathf.Max(0f, t); // during the warm-up the timeline holds its first moment, while the doll's own clock runs
            foreach (var d in dolls) StepDoll(d, t, tc, dt);
            foreach (var o in objects) StepObject(o, tc, dt, t >= 0f);
        }

        DollState DollById(string id) => dolls.Find(d => d.Spec.Id == id);
        ObjectState ObjectById(string id) => objects.Find(o => o.Spec.Id == id);

        Vector2? Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var d = DollById(id);
            if (d != null) return d.Placed ? d.Pos : d.Spec.At(0f).From;
            var o = ObjectById(id);
            if (o != null) { var p = o.Last.State != null ? o.Last.Pos : o.Spec.Pos; return new Vector2(p.x, p.z); }
            throw new ClipException($"face/target '{id}' is neither a doll nor an object");
        }

        void StepDoll(DollState d, float t, float tc, float dt)
        {
            if (StepPlayDoll(d, t, dt)) return; // #358: a play group drives this doll
            var spec = d.Spec;
            int si = spec.IndexAt(tc);
            var seg = spec.Segments[si];
            float since = Mathf.Max(0f, tc - seg.T0);
            float progress = Mathf.Clamp01(since / seg.Length);
            var plan = seg.Moves ? Vector2.Lerp(seg.From, seg.To, progress) : seg.From;
            bool hunting = seg.Hunt != HuntPhase.None;

            var prev = d.Pos;
            bool jump = !d.Placed || dt <= 0f;
            d.Pos = plan;
            float inst = !jump ? (d.Pos - prev).magnitude / dt : 0f;
            d.Speed = Mathf.Lerp(d.Speed, inst, dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 1f);
            d.Placed = true;
            if (d.Speed > Bench.WalkStarts) d.Walking = true; else if (d.Speed < Bench.WalkStops) d.Walking = false;
            bool walking = d.Walking;

            var style = seg.Style;
            var kind = seg.Activity ?? ActivityKind.None;
            if (walking && style != DollStyle.Hop) style = kind == ActivityKind.RunOver || (spec.Kid && kind == ActivityKind.Play) ? DollStyle.Run : DollStyle.Walk;
            if (seg.Hidden) style = DollStyle.Hidden;

            // Facing: along the walk, else the partner, else what the clip says to face.
            Vector2? face = null;
            float? faceAngle = null;
            switch (seg.Face.Kind)
            {
                case FaceKind.Point: face = seg.Face.Point; break;
                case FaceKind.Angle: faceAngle = seg.Face.Angle; break;
                case FaceKind.Ref: face = Resolve(seg.Face.Ref); break;
            }
            float want = d.Yaw;
            if (jump) d.Velocity = Vector2.zero;
            else d.Velocity = Vector2.Lerp(d.Velocity, (d.Pos - prev) / dt, 1f - Mathf.Exp(-dt * Bench.VelocityRate));
            if (walking && !(hunting && face.HasValue)) { if (d.Velocity.sqrMagnitude > 0.05f * 0.05f) want = Bench.Yaw(Vector2.zero, d.Velocity); }
            else
            {
                var partner = seg.Partner != null ? DollById(seg.Partner) : null;
                if (partner != null && partner.Placed && (partner.Pos - d.Pos).sqrMagnitude < 0.6f * 0.6f && partner.Pos != d.Pos) want = Bench.Yaw(d.Pos, partner.Pos);
                else if (face.HasValue && (face.Value - d.Pos).sqrMagnitude > 1e-5f) want = Bench.Yaw(d.Pos, face.Value);
                else if (faceAngle.HasValue) want = faceAngle.Value;
            }
            float most = walking || seg.Moves ? Mathf.Lerp(Bench.SlowTurn, Bench.MostTurn, d.Speed / Bench.FaceFrom) : Bench.MostTurn;
            d.Yaw = dt > 0f ? d.Yaw + Mathf.Clamp(Mathf.DeltaAngle(d.Yaw, want) * (1f - Mathf.Exp(-dt * Bench.TurnRate)), -most * dt, most * dt) : want;

            // Pose (PeopleView.Drive: phase, loop step, since, stride).
            float time = clip.Clock + t;
            float phase = time + spec.Phase - (seg.CopiesPartner ? 0.35f : 0f);
            if (seg.Partner != null && !seg.CopiesPartner && kind is ActivityKind.Chat or ActivityKind.Argue)
            {
                var p = clip.Doll(seg.Partner);
                if (p != null) phase += spec.Seed < p.Seed ? 0f : 0.65f;
            }
            int steps = seg.Steps;
            int step = seg.Step ?? (steps <= 1 ? 0 : (int)(since / Mathf.Max(1e-3f, seg.StepSeconds)) % steps);
            bool holds = seg.Prop != PropKind.None && seg.Prop != PropKind.Mask;
            d.Stride = Mathf.Repeat(d.Stride + DollMotion.Cadence(style, d.Speed) * Mathf.PI * dt, Mathf.PI * 2f);
            var pose = DollMotion.Evaluate(style, phase, seg.Pace, step, steps, seg.Slot, holds, d.Speed, progress, since, seg.Count, d.Stride);
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
            if (hunting) DollMotion.Hunt(ref pose, seg.Hunt, progress);
            d.Pose = pose;

            var f = new DollFrame
            {
                Pose = pose, Style = style, Pos = d.Pos, BodyYaw = d.Yaw, Yaw = d.Yaw + pose.yaw, Walking = walking, Speed = d.Speed,
                Progress = progress, Since = since, Segment = si, Step = step, Hunt = seg.Hunt, Prop = seg.Prop, Slot = seg.Slot,
            };
            Bench.Place(ref f, spec.Scale);
            d.Last = f;
        }

        void StepObject(ObjectState o, float t, float dt, bool live)
        {
            var s = o.Spec;
            var f = new ObjectFrame { Pos = s.Pos, Yaw = s.Yaw, Act = "", Visible = true, State = new Dictionary<string, float>(s.State) };
            switch (s.Kind)
            {
                case "rabbit": StepRabbit(o, ref f, t, dt); break;
                case "bush":
                {
                    var picker = s.PickedBy != null ? DollById(s.PickedBy) : null;
                    if (live && picker != null && picker.Last.Style == DollStyle.Pick && o.Berries > 0f)
                    {
                        o.DropClock += dt;
                        if (o.DropClock >= s.DropEvery) { o.DropClock -= s.DropEvery; o.Berries -= 1f; }
                    }
                    f.State["berries"] = o.Berries;
                    break;
                }
            }
            if (s.Arc != null) StepArc(o, ref f, t);
            StepBall(o, ref f, t); // #358
            o.Last = f;
        }

        void StepRabbit(ObjectState o, ref ObjectFrame f, float t, float dt)
        {
            var s = o.Spec;
            var basePose = new RabbitPose { x = s.Pos.x, z = s.Pos.z, yaw = s.Yaw * Mathf.Deg2Rad, act = RabbitAct.Graze };
            px.Clear(); pz.Clear();
            foreach (var d in dolls) if (d.Placed && d.Last.Style != DollStyle.Hidden) { px.Add(d.Pos.x); pz.Add(d.Pos.y); }
            RabbitPose p;
            if (!s.Scared) p = basePose;
            else if (s.HuntedBy != null && t < s.HuntedUntil)
            {
                // RabbitView #283: the stalked rabbit minds nobody until the strike is over; with the hunter within 0.8 it sits up.
                p = o.Scare.Step(dt, basePose, none, none, field, t);
                var h = DollById(s.HuntedBy);
                if (p.act == RabbitAct.Graze && h != null && h.Placed && (h.Pos - new Vector2(p.x, p.z)).sqrMagnitude < 0.8f * 0.8f) p.act = RabbitAct.SitUp;
            }
            else p = o.Scare.Step(dt, basePose, px, pz, field, t);
            f.Pos = new Vector3(p.x, p.y, p.z);
            f.Yaw = p.yaw * Mathf.Rad2Deg;
            f.Act = p.act.ToString();
            f.Phase = p.phase;
        }

        Vector3 Anchor(string a, Vector3? point)
        {
            if (point.HasValue) return point.Value;
            if (string.IsNullOrEmpty(a)) return Vector3.zero;
            int dot = a.LastIndexOf('.');
            string id = dot > 0 ? a.Substring(0, dot) : a, part = dot > 0 ? a.Substring(dot + 1) : "";
            var d = DollById(id);
            if (d != null)
                return part switch { "tip" => d.Last.Tip, "handL" => d.Last.HandL, "head" => d.Last.Head, _ => d.Last.HandR };
            var o = ObjectById(id) ?? throw new ClipException($"arc anchor '{a}' is neither a doll nor an object");
            return o.Last.State != null ? o.Last.Pos + new Vector3(0, o.Spec.Radius * 0.5f, 0) : o.Spec.Pos;
        }

        /// <summary>A pooled thing on a parametric arc (research 17 §6.4): start, end, height, time; fixed when it leaves the hand.</summary>
        void StepArc(ObjectState o, ref ObjectFrame f, float t)
        {
            var a = o.Spec.Arc;
            if (t < a.T0)
            {
                f.Visible = a.HeldBefore;
                f.Pos = Anchor(a.From, a.FromPoint);
                f.Act = "held";
                return;
            }
            if (!o.ArcLaunched) { o.ArcFrom = Anchor(a.From, a.FromPoint); o.ArcTo = Anchor(a.To, a.ToPoint); o.ArcLaunched = true; }
            float u = Mathf.Clamp01((t - a.T0) / (a.T1 - a.T0));
            var p = Vector3.LerpUnclamped(o.ArcFrom, o.ArcTo, u) + Vector3.up * (a.Height * 4f * u * (1f - u));
            f.Pos = p;
            var v = (o.ArcTo - o.ArcFrom) + Vector3.up * (a.Height * 4f * (1f - 2f * u));
            f.Yaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            f.Phase = u;
            f.Act = u < 1f ? "flying" : "landed";
        }
    }
}
