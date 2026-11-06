// #352 AnimBench: what a clip did, frame by frame (track.json: the channels; render.json: the parts' frames for the renderer).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using AquariumCiv.View;
using UnityEngine;
using PropSlot = AquariumCiv.View.PropSlot;

namespace AnimBench
{
    public sealed class DollTrack
    {
        public DollSpec Spec;
        public DollFrame[] Frames;
        public float Scale => Spec.Scale;
        public float Width => Bench.DollWidth * Spec.Scale;
    }

    public sealed class ObjectTrack
    {
        public ObjectSpec Spec;
        public ObjectFrame[] Frames;
    }

    /// <summary>A run of frames with <see cref="DollPose.hit"/> on: one contact.</summary>
    public sealed class HitEvent
    {
        public string Doll, Target;
        public int F0, F1, Segment;
        public float T0, T1;
        /// <summary>#358: a throw's release (the ball leaves the hands): the arcs principle checks where it goes.</summary>
        public const string ReleaseTarget = "!release";
        public bool IsRelease => Target == ReleaseTarget;
    }

    public sealed class Track
    {
        public readonly Clip Clip;
        public readonly float[] Times;
        public readonly List<DollTrack> Dolls = new List<DollTrack>();
        public readonly List<ObjectTrack> Objects = new List<ObjectTrack>();
        public readonly List<HitEvent> Hits = new List<HitEvent>();
        /// <summary>#358: the play group's throws.</summary>
        public readonly List<ArcEvent> ArcEvents = new List<ArcEvent>();

        public int Count => Times.Length;

        public Track(Clip clip)
        {
            Clip = clip;
            Times = new float[clip.FrameCount];
            foreach (var d in clip.Dolls) Dolls.Add(new DollTrack { Spec = d, Frames = new DollFrame[clip.FrameCount] });
            foreach (var o in clip.Objects) Objects.Add(new ObjectTrack { Spec = o, Frames = new ObjectFrame[clip.FrameCount] });
        }

        public DollTrack Doll(string id) => Dolls.Find(d => d.Spec.Id == id);
        public ObjectTrack Object(string id) => Objects.Find(o => o.Spec.Id == id);
        public int FrameAt(float t) => Mathf.Clamp((int)Math.Round(t * Clip.Fps), 0, Count - 1);

        public void Record(int i, float t, List<DollFrame> dolls, List<ObjectFrame> objects)
        {
            Times[i] = t;
            for (int k = 0; k < dolls.Count; k++) Dolls[k].Frames[i] = dolls[k];
            for (int k = 0; k < objects.Count; k++) Objects[k].Frames[i] = objects[k];
        }

        public void Finish()
        {
            Hits.Clear();
            foreach (var d in Dolls)
            {
                int start = -1;
                for (int i = 0; i <= Count; i++)
                {
                    bool on = i < Count && d.Frames[i].Pose.hit > 0.5f;
                    if (on && start < 0) start = i;
                    if (!on && start >= 0)
                    {
                        int seg = d.Frames[start].Segment;
                        Hits.Add(new HitEvent { Doll = d.Spec.Id, F0 = start, F1 = i - 1, T0 = Times[start], T1 = Times[i - 1], Segment = seg, Target = d.Frames[start].HitTarget ?? d.Spec.Segments[seg].Target });
                        start = -1;
                    }
                }
            }
        }

        /// <summary>Where a target (a doll or an object) is at a frame, and how big it is (its radius).</summary>
        public bool TargetAt(string id, int f, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero; radius = 0f;
            var d = Doll(id);
            if (d != null)
            {
                var fr = d.Frames[f];
                centre = fr.Body.Point(new Vector3(0, Bench.BodyH * 0.5f * d.Scale, 0));
                radius = Bench.BodyR * d.Scale;
                return true;
            }
            var o = Object(id);
            if (o == null) return false;
            centre = o.Frames[f].Pos + new Vector3(0, o.Spec.Kind == "rabbit" ? 0.03f : 0f, 0);
            radius = o.Spec.Radius;
            return true;
        }

        // ---------------------------------------------------------------- JSON

        static void Num(Utf8JsonWriter w, float v) => w.WriteRawValue(Math.Round(v, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
        static void Vec(Utf8JsonWriter w, Vector3 v) { w.WriteStartArray(); Num(w, v.x); Num(w, v.y); Num(w, v.z); w.WriteEndArray(); }
        static void Arr(Utf8JsonWriter w, float[] a) { w.WriteStartArray(); foreach (var v in a) Num(w, v); w.WriteEndArray(); }

        void Series(Utf8JsonWriter w, string name, Func<int, float> get)
        {
            w.WritePropertyName(name); w.WriteStartArray();
            for (int i = 0; i < Count; i++) Num(w, get(i));
            w.WriteEndArray();
        }

        void SeriesV(Utf8JsonWriter w, string name, Func<int, Vector3> get)
        {
            w.WritePropertyName(name); w.WriteStartArray();
            for (int i = 0; i < Count; i++) Vec(w, get(i));
            w.WriteEndArray();
        }

        void SeriesS(Utf8JsonWriter w, string name, Func<int, string> get)
        {
            w.WritePropertyName(name); w.WriteStartArray();
            for (int i = 0; i < Count; i++) w.WriteStringValue(get(i));
            w.WriteEndArray();
        }

        /// <summary>track.json: every channel per frame, per doll and per object (columns: one array per channel), and the hits.</summary>
        public void WriteTrack(string path, IReadOnlyList<PrincipleResult> principles)
        {
            using var fs = File.Create(path);
            using var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = false });
            w.WriteStartObject();
            w.WriteString("format", "animbench-track/1");
            w.WriteString("clip", Clip.Name);
            w.WriteNumber("fps", Clip.Fps);
            w.WriteNumber("duration", Clip.Duration);
            w.WriteNumber("frames", Count);
            w.WritePropertyName("camera"); w.WriteStartObject();
            w.WriteString("view", Clip.Camera.View); w.WriteNumber("yaw", Clip.Camera.Yaw); w.WriteNumber("pitch", Clip.Camera.Pitch);
            if (Clip.Camera.Focus.HasValue) { w.WritePropertyName("focus"); w.WriteStartArray(); Num(w, Clip.Camera.Focus.Value.x); Num(w, Clip.Camera.Focus.Value.y); w.WriteEndArray(); }
            if (Clip.Camera.Span > 0f) w.WriteNumber("span", Clip.Camera.Span);
            w.WriteEndObject();
            w.WritePropertyName("sheet"); w.WriteStartObject();
            w.WriteNumber("from", Clip.Sheet.From); w.WriteNumber("to", Clip.Sheet.To < 0f ? Clip.Duration : Clip.Sheet.To);
            w.WriteNumber("frames", Clip.Sheet.Frames); w.WriteNumber("columns", Clip.Sheet.Columns);
            w.WriteEndObject();
            w.WritePropertyName("t"); Arr(w, Times);

            w.WritePropertyName("dolls"); w.WriteStartArray();
            foreach (var d in Dolls)
            {
                var F = d.Frames;
                w.WriteStartObject();
                w.WriteString("id", d.Spec.Id);
                w.WriteString("role", d.Spec.Role);
                w.WriteBoolean("kid", d.Spec.Kid);
                w.WriteBoolean("camp", d.Spec.Camp);
                w.WriteNumber("seed", d.Spec.Seed);
                w.WriteNumber("scale", d.Scale);
                w.WriteNumber("width", Math.Round(d.Width, 4));
                w.WritePropertyName("segments"); w.WriteStartArray();
                foreach (var s in d.Spec.Segments)
                {
                    w.WriteStartObject();
                    w.WriteNumber("t0", Math.Round(s.T0, 4)); w.WriteNumber("t1", Math.Round(Math.Min(s.T1, 1e4f), 4));
                    if (s.Activity.HasValue) w.WriteString("activity", s.Activity.Value.ToString());
                    w.WriteString("style", s.Style.ToString());
                    if (s.Hunt != AquariumCiv.Life.HuntPhase.None) w.WriteString("hunt", s.Hunt.ToString());
                    if (s.Target != null) w.WriteString("target", s.Target);
                    if (s.Label != null) w.WriteString("label", s.Label);
                    w.WriteBoolean("loop", Principles.IsLoop(s));
                    w.WriteBoolean("endBeat", Principles.IsEndBeat(s));
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WritePropertyName("ch"); w.WriteStartObject();
                SeriesS(w, "style", i => F[i].Style.ToString());
                Series(w, "seg", i => F[i].Segment);
                Series(w, "x", i => F[i].Pos.x);
                Series(w, "z", i => F[i].Pos.y);
                Series(w, "yaw", i => F[i].Yaw);
                Series(w, "bodyYaw", i => F[i].BodyYaw);
                SeriesV(w, "offset", i => F[i].Pose.offset);
                Series(w, "lean", i => F[i].Pose.lean);
                Series(w, "roll", i => F[i].Pose.roll);
                Series(w, "squash", i => F[i].Pose.squash);
                SeriesV(w, "armL", i => F[i].Pose.armL);
                SeriesV(w, "armR", i => F[i].Pose.armR);
                Series(w, "extend", i => F[i].Pose.extend ? 1f : 0f);
                Series(w, "hit", i => F[i].Pose.hit);
                Series(w, "walking", i => F[i].Walking ? 1f : 0f);
                Series(w, "speed", i => F[i].Speed);
                Series(w, "progress", i => F[i].Progress);
                Series(w, "since", i => F[i].Since);
                Series(w, "step", i => F[i].Step);
                SeriesS(w, "hunt", i => F[i].Hunt.ToString());
                SeriesS(w, "prop", i => F[i].Prop.ToString());
                SeriesV(w, "handR", i => F[i].HandR);
                SeriesV(w, "handL", i => F[i].HandL);
                SeriesV(w, "tip", i => F[i].Tip);
                SeriesV(w, "head", i => F[i].Head);
                w.WriteEndObject();
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WritePropertyName("objects"); w.WriteStartArray();
            foreach (var o in Objects)
            {
                var F = o.Frames;
                w.WriteStartObject();
                w.WriteString("id", o.Spec.Id);
                w.WriteString("kind", o.Spec.Kind);
                w.WriteNumber("radius", o.Spec.Radius);
                if (o.Spec.Size != Vector2.zero) { w.WritePropertyName("size"); w.WriteStartArray(); Num(w, o.Spec.Size.x); Num(w, o.Spec.Size.y); w.WriteEndArray(); }
                w.WritePropertyName("ch"); w.WriteStartObject();
                SeriesV(w, "pos", i => F[i].Pos);
                Series(w, "yaw", i => F[i].Yaw);
                SeriesS(w, "act", i => F[i].Act ?? "");
                Series(w, "visible", i => F[i].Visible ? 1f : 0f);
                Series(w, "phase", i => F[i].Phase);
                var keys = new SortedSet<string>();
                foreach (var f in F) if (f.State != null) foreach (var k in f.State.Keys) keys.Add(k);
                foreach (var k in keys) Series(w, k, i => F[i].State != null && F[i].State.TryGetValue(k, out var v) ? v : 0f);
                w.WriteEndObject();
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WritePropertyName("hits"); w.WriteStartArray();
            foreach (var h in Hits)
            {
                w.WriteStartObject();
                w.WriteString("doll", h.Doll); w.WriteNumber("f0", h.F0); w.WriteNumber("f1", h.F1);
                w.WriteNumber("t0", Math.Round(h.T0, 4)); w.WriteNumber("t1", Math.Round(h.T1, 4));
                if (h.Target != null) w.WriteString("target", h.Target);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            if (principles != null)
            {
                w.WritePropertyName("principles"); w.WriteStartArray();
                foreach (var p in principles)
                {
                    w.WriteStartObject();
                    w.WriteString("id", p.Id); w.WriteString("status", p.Status); w.WriteBoolean("knownGap", p.KnownGap);
                    w.WritePropertyName("findings"); w.WriteStartArray(); foreach (var s in p.Findings) w.WriteStringValue(s); w.WriteEndArray();
                    w.WritePropertyName("marks"); w.WriteStartArray();
                    foreach (var m in p.Marks) { w.WriteStartObject(); w.WriteString("doll", m.Doll); w.WriteString("kind", m.Kind); w.WriteNumber("t0", Math.Round(m.T0, 4)); w.WriteNumber("t1", Math.Round(m.T1, 4)); w.WriteEndObject(); }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }

        /// <summary>render.json: the parts' world frames per frame (body, arms, prop), for render.py. Not meant for reading.</summary>
        public void WriteRender(string path)
        {
            using var fs = File.Create(path);
            using var w = new Utf8JsonWriter(fs);
            w.WriteStartObject();
            w.WriteString("format", "animbench-render/1");
            w.WritePropertyName("dolls"); w.WriteStartArray();
            foreach (var d in Dolls)
            {
                w.WriteStartObject();
                w.WriteString("id", d.Spec.Id);
                w.WritePropertyName("body"); w.WriteStartArray(); foreach (var f in d.Frames) Arr(w, f.Body.ToArray()); w.WriteEndArray();
                w.WritePropertyName("armL"); w.WriteStartArray(); foreach (var f in d.Frames) Arr(w, f.ArmL.ToArray()); w.WriteEndArray();
                w.WritePropertyName("armR"); w.WriteStartArray(); foreach (var f in d.Frames) Arr(w, f.ArmR.ToArray()); w.WriteEndArray();
                w.WritePropertyName("prop"); w.WriteStartArray();
                foreach (var f in d.Frames)
                {
                    if (f.PropFrame.HasValue) { w.WriteStartObject(); w.WriteString("k", f.Prop.ToString()); w.WriteString("s", f.Slot.ToString()); w.WritePropertyName("f"); Arr(w, f.PropFrame.Value.ToArray()); w.WriteEndObject(); }
                    else w.WriteNullValue();
                }
                w.WriteEndArray();
                w.WritePropertyName("hidden"); w.WriteStartArray(); foreach (var f in d.Frames) w.WriteBooleanValue(f.Pose.hidden); w.WriteEndArray();
                w.WritePropertyName("ground"); w.WriteStartArray(); foreach (var f in d.Frames) { w.WriteStartArray(); Num(w, f.Pos.x); Num(w, f.Pos.y); w.WriteEndArray(); } w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
    }
}
