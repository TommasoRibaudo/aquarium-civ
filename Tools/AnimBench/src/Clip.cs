// #352 AnimBench: a clip = a little staged scene (dolls with a timeline, objects) to simulate frame by frame.
// The JSON schema is in Tools/AnimBench/README.md; T2 AnimLab (the Editor) reads the same files, so keep it engine-neutral.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AquariumCiv.Life;
using AquariumCiv.View;
using UnityEngine;
using PropSlot = AquariumCiv.View.PropSlot;

namespace AnimBench
{
    public sealed class Clip
    {
        public string Name = "clip";
        public string Notes = "";
        public float Duration = 6f, Fps = 30f, Warmup = 1f, Clock;
        /// <summary>Real seconds per view hour for "hours" durations (the morning: 80 s for 5 h, LifeClock).</summary>
        public float SecondsPerViewHour = 16f;
        public CameraSpec Camera = new CameraSpec();
        public SheetSpec Sheet = new SheetSpec();
        public readonly List<DollSpec> Dolls = new List<DollSpec>();
        public readonly List<ObjectSpec> Objects = new List<ObjectSpec>();
        public ExpectSpec Expect = new ExpectSpec();
        /// <summary>Principles this clip is known to fail today (research 17 §4): the tests report them as gaps, not failures.</summary>
        public readonly HashSet<string> KnownGaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string SourcePath;
        /// <summary>#358: a kids' play group (src/Play.cs).</summary>
        public PlaySpec Play;
        /// <summary>#363: a pair moment (src/Pair.cs).</summary>
        public PairSpec Pair;
        /// <summary>#365: the meal and the fire (src/Meal.cs).</summary>
        public MealSpec Meal;
        /// <summary>#370: the staged moment the dolls were built from (null for a hand-made clip).</summary>
        public AquariumCiv.Life.MomentStagePlan Moment;

        public int FrameCount => Math.Max(1, (int)Math.Round(Duration * Fps) + 1);
        public float Dt => 1f / Fps;

        public DollSpec Doll(string id) => Dolls.Find(d => d.Id == id);
        public ObjectSpec Object(string id) => Objects.Find(o => o.Id == id);

        // ------------------------------------------------------------------ loading

        static readonly JsonDocumentOptions DocOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        public static Clip Load(string path)
        {
            var c = Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            c.SourcePath = Path.GetFullPath(path);
            return c;
        }

        public static Clip Parse(string json, string fallbackName = "clip")
        {
            var root = JsonNode.Parse(json, null, DocOptions) as JsonObject ?? throw new ClipException("the clip must be a JSON object");
            var c = new Clip { Name = Str(root, "name") ?? fallbackName };
            c.Notes = Str(root, "notes") ?? "";
            c.Duration = Num(root, "duration", 6f);
            c.Fps = Num(root, "fps", 30f);
            c.Warmup = Num(root, "warmup", 1f);
            c.Clock = Num(root, "clock", 0f);
            c.SecondsPerViewHour = Num(root, "secondsPerViewHour", 16f);
            if (c.Duration <= 0f || c.Fps <= 0f) throw new ClipException("duration and fps must be > 0");
            if (root["camera"] is JsonObject cam) c.Camera = CameraSpec.Parse(cam);
            else if (root["camera"] is JsonValue cv) c.Camera.View = cv.GetValue<string>();
            if (root["sheet"] is JsonObject sh) c.Sheet = SheetSpec.Parse(sh, c);
            if (root["expect"] is JsonObject ex) c.Expect = ExpectSpec.Parse(ex);
            if (root["knownGaps"] is JsonArray gaps) foreach (var g in gaps) c.KnownGaps.Add(g.GetValue<string>());
            if (root["objects"] is JsonArray objs) foreach (var o in objs) c.Objects.Add(ObjectSpec.Parse((JsonObject)o, c));
            if (root["dolls"] is JsonArray dolls)
            {
                int i = 0;
                foreach (var d in dolls) c.Dolls.Add(DollSpec.Parse((JsonObject)d, c, i++));
            }
            if (root["play"] is JsonObject pl) c.Play = PlaySpec.Parse(pl); // #358
            if (root["pair"] is JsonObject pr) c.Pair = PairSpec.Parse(pr); // #363
            if (root["meal"] is JsonObject ml) c.Meal = MealSpec.Parse(ml); // #365
            if (root["moment"] is JsonObject mo) c.Moment = MomentClip.Expand(c, mo, root["duration"] == null); // #370: the dolls from the game's staged-moment plan
            if (c.Dolls.Count == 0 && c.Objects.Count == 0) throw new ClipException("a clip needs at least one doll or object");
            var ids = new HashSet<string>();
            foreach (var d in c.Dolls) if (!ids.Add(d.Id)) throw new ClipException($"duplicate id '{d.Id}'");
            foreach (var o in c.Objects) if (!ids.Add(o.Id)) throw new ClipException($"duplicate id '{o.Id}'");
            return c;
        }

        // ------------------------------------------------------------------ JSON helpers

        internal static string Str(JsonObject o, string key) => o[key] is JsonValue v ? v.ToString() : null;
        internal static float Num(JsonObject o, string key, float def)
        {
            if (o[key] is not JsonValue v) return def;
            try { return (float)v.GetValue<double>(); }
            catch (Exception) { throw new ClipException($"'{key}' must be a number, got {v.ToJsonString()}"); }
        }
        internal static float? NumOpt(JsonObject o, string key) => o[key] is JsonValue ? Num(o, key, 0f) : (float?)null;
        internal static bool Bool(JsonObject o, string key, bool def) => o[key] is JsonValue v ? v.GetValue<bool>() : def;
        internal static int Int(JsonObject o, string key, int def) => o[key] is JsonValue ? (int)Math.Round(Num(o, key, def)) : def;

        internal static Vector2 XZ(JsonNode n, string what)
        {
            if (n is JsonArray a && a.Count >= 2) return new Vector2((float)a[0].GetValue<double>(), (float)a[a.Count == 3 ? 2 : 1].GetValue<double>());
            throw new ClipException($"{what} must be [x, z] (or [x, y, z])");
        }

        internal static Vector3 XYZ(JsonNode n, string what)
        {
            if (n is JsonArray a && a.Count == 3) return new Vector3((float)a[0].GetValue<double>(), (float)a[1].GetValue<double>(), (float)a[2].GetValue<double>());
            if (n is JsonArray b && b.Count == 2) return new Vector3((float)b[0].GetValue<double>(), 0f, (float)b[1].GetValue<double>());
            throw new ClipException($"{what} must be [x, y, z] or [x, z]");
        }

        internal static T Enum<T>(string s, string what) where T : struct
        {
            if (System.Enum.TryParse<T>(s, true, out var v) && System.Enum.IsDefined(typeof(T), v)) return v;
            throw new ClipException($"{what}: '{s}' is not a {typeof(T).Name} (one of: {string.Join(", ", System.Enum.GetNames(typeof(T)))})");
        }

        internal static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public sealed class ClipException : Exception { public ClipException(string m) : base(m) { } }

    public sealed class CameraSpec
    {
        /// <summary>default (BoxCamera's yaw 28, pitch 34), side (level, from the side), close (default angles, tight on the dolls).</summary>
        public string View = "default";
        /// <summary>Optional: the world point the frame centres on, and the world width it shows (else fitted to the whole clip).</summary>
        public Vector2? Focus;
        public float Span;
        public float Yaw = 28f, Pitch = 34f;

        public static CameraSpec Parse(JsonObject o)
        {
            var c = new CameraSpec { View = Clip.Str(o, "view") ?? "default" };
            if (o["focus"] != null) c.Focus = Clip.XZ(o["focus"], "camera.focus");
            c.Span = Clip.Num(o, "span", 0f);
            c.Yaw = Clip.Num(o, "yaw", 28f);
            c.Pitch = Clip.Num(o, "pitch", 34f);
            return c;
        }
    }

    public sealed class SheetSpec
    {
        public float From, To = -1f;
        public int Frames = 24, Columns = 6;

        public static SheetSpec Parse(JsonObject o, Clip c) => new SheetSpec
        {
            From = Clip.Num(o, "from", 0f), To = Clip.Num(o, "to", -1f), Frames = Clip.Int(o, "frames", 24), Columns = Clip.Int(o, "columns", 6),
        };
    }

    /// <summary>What a clip promises to show (research 17 §4), for the principle checks.</summary>
    public sealed class ExpectSpec
    {
        /// <summary>A thrown or flying thing: object <see cref="Object"/> (kind ball, spear, berry, fish) leaves <see cref="By"/>'s hand and lands on <see cref="Target"/>.</summary>
        public sealed class Arc { public string Kind, By, Target; }
        public readonly List<Arc> Arcs = new List<Arc>();
        /// <summary>Neighbours for "no 3+ neighbours in the same move": dolls closer than this (box units).</summary>
        public float NeighbourRadius = 0.6f;
        /// <summary>How soon the target must answer a hit (seconds).</summary>
        public float ReactSeconds = 0.2f;
        /// <summary>#358: how close a hit's hand must be to its target's surface (default one doll-width). The rabbit chase's contact
        /// is the kid coming inside the rabbits' alert radius (0.55 u), not a touch.</summary>
        public float? HitReach;

        public static ExpectSpec Parse(JsonObject o)
        {
            var e = new ExpectSpec
            {
                NeighbourRadius = Clip.Num(o, "neighbourRadius", 0.6f),
                ReactSeconds = Clip.Num(o, "reactSeconds", 0.2f),
                HitReach = Clip.NumOpt(o, "hitReach"),
            };
            if (o["arcs"] is JsonArray arcs)
                foreach (JsonObject a in arcs)
                    e.Arcs.Add(new Arc { Kind = Clip.Str(a, "kind"), By = Clip.Str(a, "by"), Target = Clip.Str(a, "target") });
            return e;
        }
    }

    public enum FaceKind { None, Point, Angle, Ref }

    public struct FaceSpec
    {
        public FaceKind Kind;
        public Vector2 Point;
        public float Angle;
        public string Ref;

        public static FaceSpec Parse(JsonNode n, string what)
        {
            if (n == null) return default;
            if (n is JsonArray) return new FaceSpec { Kind = FaceKind.Point, Point = Clip.XZ(n, what) };
            var v = (JsonValue)n;
            if (v.TryGetValue<double>(out var deg)) return new FaceSpec { Kind = FaceKind.Angle, Angle = (float)deg };
            return new FaceSpec { Kind = FaceKind.Ref, Ref = v.GetValue<string>() };
        }
    }

    /// <summary>One stretch of a doll's life: an activity (or a raw style) at a place or along a walk, from <see cref="T0"/> to <see cref="T1"/> (clip seconds).</summary>
    public sealed class Segment
    {
        public float T0, T1;
        public ActivityKind? Activity;
        public DollStyle Style = DollStyle.Stand;
        public HuntPhase Hunt;
        public Vector2 From, To;
        public FaceSpec Face;
        public PropKind Prop;
        /// <summary>#365: the eater holds the meal's bowl once it is served (PeopleView gives every served eater one).</summary>
        public bool Bowl;
        public PropSlot Slot = PropSlot.Hand;
        public float Pace = 1f;
        public int? Step;
        public int Steps;
        public int Count;
        public float StepSeconds;
        public bool CopiesPartner, Hidden, EndBeat;
        /// <summary>#360: the bucket is drawn from muddy water (the full bucket is the brown one).</summary>
        public bool Muddy;
        /// <summary>#360: the place in the queue at the bank (PersonActivity.slot): sets the draw's start delay.</summary>
        public int Queue;
        /// <summary>#362: a Stalk segment's two freezes (seconds; the rabbit sits up and the hunter stops), as the hunt plan has them.</summary>
        public float FreezeA, FreezeB;
        public string Partner, Target, Label;
        /// <summary>#370: this stretch is step <see cref="StepIndex"/> of a staged moment (MomentBeats lays its beat on the pose).</summary>
        public AquariumCiv.Life.MomentStagePlan Plan;
        public AquariumCiv.Life.MomentMember Member;
        public int StepIndex = -1;

        public float Length => Math.Max(1e-4f, T1 - T0);
        public bool Moves => (To - From).sqrMagnitude > 1e-10f;
    }

    public sealed class DollSpec
    {
        public string Id, Role;
        public bool Kid, Camp = true;
        public int Seed;
        /// <summary>The doll's own pose-clock offset (PeopleView: Hash01(key, 5) * 37 seconds), unless the clip sets it.</summary>
        public float Phase;
        public readonly List<Segment> Segments = new List<Segment>();

        public float Scale => Kid ? 0.68f : 1f;

        public Segment At(float t)
        {
            for (int i = Segments.Count - 1; i >= 0; i--) if (t >= Segments[i].T0) return Segments[i];
            return Segments[0];
        }

        public int IndexAt(float t)
        {
            for (int i = Segments.Count - 1; i >= 0; i--) if (t >= Segments[i].T0) return i;
            return 0;
        }

        public static DollSpec Parse(JsonObject o, Clip clip, int index)
        {
            var d = new DollSpec
            {
                Id = Clip.Str(o, "id") ?? $"doll{index}",
                Kid = Clip.Bool(o, "kid", false),
                Camp = Clip.Bool(o, "camp", true),
                Role = Clip.Str(o, "role") ?? "",
            };
            d.Seed = Clip.Int(o, "seed", StableHash(d.Id));
            d.Phase = Clip.NumOpt(o, "phase") ?? Bench.Hash01(d.Seed, 5) * 37f;
            string where = $"doll '{d.Id}'";

            // A ring slot as PeopleView.SlotSpot places it at the fire / green / market: angle h + slot * 2pi / max(count, 8), facing the middle.
            Vector2? ringPos = null; Vector2? ringCentre = null;
            if (o["ring"] is JsonObject ring)
            {
                var centre = Clip.XZ(ring["center"] ?? ring["centre"], where + " ring.center");
                float r = Clip.Num(ring, "r", 0.3f);
                int slot = Clip.Int(ring, "slot", 0), count = Clip.Int(ring, "count", 1);
                float h = Clip.Num(ring, "h", 0f) * Mathf.Deg2Rad;
                r += Math.Max(0, count - 8) * 0.015f;
                float ang = h + slot * Mathf.PI * 2f / Math.Max(count, 8);
                ringPos = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                ringCentre = centre;
            }

            var defaults = new JsonObject();
            foreach (var kv in o) if (kv.Key != "timeline" && kv.Key != "ring") defaults[kv.Key] = kv.Value?.DeepClone();
            if (ringPos.HasValue)
            {
                if (defaults["pos"] == null) defaults["pos"] = new JsonArray((double)ringPos.Value.x, (double)ringPos.Value.y);
                if (defaults["face"] == null && defaults["facing"] == null) defaults["face"] = new JsonArray((double)ringCentre.Value.x, (double)ringCentre.Value.y);
            }

            var list = o["timeline"] as JsonArray;
            if (list == null || list.Count == 0) list = new JsonArray(new JsonObject());
            float t = 0f;
            Vector2 at = defaults["pos"] != null ? Clip.XZ(defaults["pos"], where + " pos") : Vector2.zero;
            for (int i = 0; i < list.Count; i++)
            {
                var so = (JsonObject)list[i];
                string sw = $"{where} timeline[{i}]";
                JsonNode Get(string key) => so[key] ?? defaults[key];
                var seg = new Segment();
                seg.T0 = so["t"] != null ? Clip.Num(so, "t", t) : t;
                float? dur = Clip.NumOpt(so, "dur");
                float? hours = Clip.NumOpt(so, "hours");
                float? until = Clip.NumOpt(so, "until");
                seg.T1 = until ?? (dur.HasValue ? seg.T0 + dur.Value : hours.HasValue ? seg.T0 + hours.Value * clip.SecondsPerViewHour
                    : i + 1 < list.Count && list[i + 1]["t"] != null ? Clip.Num((JsonObject)list[i + 1], "t", 0f) : clip.Duration + 1e3f);
                if (seg.T1 < seg.T0) throw new ClipException($"{sw}: ends before it starts");

                string act = Get("activity")?.ToString();
                string style = Get("style")?.ToString();
                if (act != null)
                {
                    seg.Activity = Clip.Enum<ActivityKind>(act, sw + " activity");
                    seg.Style = DollMotion.StyleOf(seg.Activity.Value);
                    seg.Steps = ActivityInfo.LoopSteps(seg.Activity.Value);
                }
                if (style != null) seg.Style = Clip.Enum<DollStyle>(style, sw + " style");
                if (so["steps"] != null) seg.Steps = Clip.Int(so, "steps", 0);
                if (Get("hunt") != null) seg.Hunt = Clip.Enum<HuntPhase>(Get("hunt").ToString(), sw + " hunt");
                if (so["freezeA"] != null) seg.FreezeA = Clip.Num(so, "freezeA", 0f);
                if (so["freezeB"] != null) seg.FreezeB = Clip.Num(so, "freezeB", 0f);
                seg.From = so["from"] != null ? Clip.XZ(so["from"], sw + " from") : so["pos"] != null ? Clip.XZ(so["pos"], sw + " pos") : at;
                seg.To = so["to"] != null ? Clip.XZ(so["to"], sw + " to") : seg.From;
                at = seg.To;
                if (Get("face") != null) seg.Face = FaceSpec.Parse(Get("face"), sw + " face");
                else if (Get("facing") != null) seg.Face = FaceSpec.Parse(Get("facing"), sw + " facing");
                if (Get("prop") != null) seg.Prop = Clip.Enum<PropKind>(Get("prop").ToString(), sw + " prop");
                if (Get("bowl") != null) seg.Bowl = bool.Parse(Get("bowl").ToString().ToLowerInvariant()); // #365
                if (Get("slot") != null) seg.Slot = Clip.Enum<PropSlot>(Get("slot").ToString(), sw + " slot");
                seg.Pace = Get("pace") is JsonValue pv ? (float)pv.GetValue<double>() : 1f;
                if (so["step"] != null) seg.Step = Clip.Int(so, "step", 0);
                seg.Count = Get("count") is JsonValue cv ? (int)cv.GetValue<double>() : 0;
                seg.StepSeconds = Get("stepSeconds") is JsonValue ss ? (float)ss.GetValue<double>() : 0.25f * clip.SecondsPerViewHour; // LifePlanner.StepHours
                seg.CopiesPartner = Get("copiesPartner") is JsonValue cp && cp.GetValue<bool>();
                seg.Hidden = Get("hidden") is JsonValue hv && hv.GetValue<bool>();
                seg.EndBeat = so["endBeat"] is JsonValue eb && eb.GetValue<bool>();
                seg.Queue = Clip.Int(so, "queue", 0);
                seg.Muddy = Get("water")?.ToString() == "muddy";
                seg.Partner = Get("partner")?.ToString();
                seg.Target = Get("target")?.ToString();
                seg.Label = Clip.Str(so, "label");
                d.Segments.Add(seg);
                t = seg.T1;
            }
            return d;
        }

        /// <summary>A stable small hash of an id (FNV-1a), so a doll without a seed keeps the same look and phase.</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char ch in s) { h ^= ch; h *= 16777619u; }
                return (int)(h & 0x7FFFFFFF) % 100000 + 1;
            }
        }
    }

    public sealed class ArcSpec
    {
        /// <summary>Where it leaves from: "dollId.hand", "dollId.tip" (the hand tool's tip), an object id, or a point [x, y, z].</summary>
        public string From, To;
        public Vector3? FromPoint, ToPoint;
        public float T0, T1, Height = 0.15f;
        /// <summary>Before the throw it rides on its "from" anchor (true) or is not there yet (false).</summary>
        public bool HeldBefore = true;
        /// <summary>#362: from this clip time the object is gone (a spear pulled out of the ground, a berry eaten).</summary>
        public float HideAt = float.MaxValue;
    }

    public sealed class ObjectSpec
    {
        public string Id, Kind;
        public Vector3 Pos;
        public float Radius, Yaw;
        public Vector2 Size;
        public readonly Dictionary<string, float> State = new Dictionary<string, float>();
        /// <summary>Rabbit: the stalked one minds nobody until <see cref="HuntedUntil"/> (RabbitView: the hunt's resolve hour), only sits up when the hunter is within 0.8.</summary>
        public string HuntedBy;
        public float HuntedUntil;
        /// <summary>#362 rabbit: windows (clip seconds) in which the hunted rabbit sits up (the plan's stalk freezes).</summary>
        public readonly List<Vector2> SitUp = new List<Vector2>();
        /// <summary>#362 rabbit: at <see cref="StartleAt"/> it freezes (flinch, ears up, 0.45 s) and bolts to <see cref="StartleTo"/>, away from <see cref="StartleFrom"/>
        /// (RabbitView's Scare.Hold, as the knock and the hunt use it). Negative = never.</summary>
        public float StartleAt = -1f;
        public Vector2 StartleFrom, StartleTo;
        /// <summary>#362 rabbit: hit by the spear at this time: jumps 0.05 u and falls on its side (0.25 s). Negative = never.</summary>
        public float FallAt = -1f;
        /// <summary>Rabbit: false = never scared (a prop rabbit).</summary>
        public bool Scared = true;
        public ArcSpec Arc;
        /// <summary>Bush: who picks it (a berry drops every <see cref="DropEvery"/> s while they work it, BushView).</summary>
        public string PickedBy;
        public float DropEvery = 5f;
        /// <summary>#370: a "bundle" (the newborn) rides between this doll's hands.</summary>
        public string Follows;

        static float DefaultRadius(string kind) => kind switch
        {
            "rabbit" => 0.04f, "bush" => 0.13f, "tree" => 0.12f, "fire" => 0.1f, "ball" => 0.025f, "basket" => 0.04f,
            "spear" => 0.02f, "berry" => 0.012f, "fish" => 0.025f, "tent" => 0.18f, "stash" => 0.1f, "pot" => 0.06f, _ => 0.05f,
        };

        public static ObjectSpec Parse(JsonObject o, Clip clip)
        {
            var s = new ObjectSpec { Kind = (Clip.Str(o, "kind") ?? "thing").ToLowerInvariant() };
            s.Id = Clip.Str(o, "id") ?? s.Kind + clip.Objects.Count;
            string w = $"object '{s.Id}'";
            s.Pos = o["pos"] != null ? Clip.XYZ(o["pos"], w + " pos") : Vector3.zero;
            s.Radius = Clip.Num(o, "radius", DefaultRadius(s.Kind));
            s.Yaw = Clip.Num(o, "yaw", 0f);
            if (o["size"] != null) s.Size = Clip.XZ(o["size"], w + " size");
            if (o["state"] is JsonObject st) foreach (var kv in st) s.State[kv.Key] = (float)kv.Value.GetValue<double>();
            if (o["berries"] != null) s.State["berries"] = Clip.Num(o, "berries", 0f);
            if (o["hunted"] is JsonObject h) { s.HuntedBy = Clip.Str(h, "by"); s.HuntedUntil = Clip.Num(h, "until", 0f); }
            if (o["sitUp"] is JsonArray su) foreach (var w2 in su) s.SitUp.Add(Clip.XZ(w2, w + " sitUp"));
            if (o["startle"] is JsonObject sl) { s.StartleAt = Clip.Num(sl, "at", 0f); s.StartleFrom = Clip.XZ(sl["from"], w + " startle.from"); s.StartleTo = Clip.XZ(sl["to"], w + " startle.to"); }
            s.FallAt = Clip.Num(o, "fallAt", -1f);
            s.Scared = Clip.Bool(o, "scared", true);
            s.PickedBy = Clip.Str(o, "pickedBy");
            s.Follows = Clip.Str(o, "follows");
            s.DropEvery = Clip.Num(o, "dropEvery", 5f);
            if (o["arc"] is JsonObject a)
            {
                var arc = new ArcSpec { T0 = Clip.Num(a, "t0", 0f), T1 = Clip.Num(a, "t1", 1f), Height = Clip.Num(a, "height", 0.15f), HeldBefore = Clip.Bool(a, "heldBefore", true), HideAt = Clip.Num(a, "hideAt", float.MaxValue) };
                if (a["from"] is JsonArray) arc.FromPoint = Clip.XYZ(a["from"], w + " arc.from"); else arc.From = Clip.Str(a, "from");
                if (a["to"] is JsonArray) arc.ToPoint = Clip.XYZ(a["to"], w + " arc.to"); else arc.To = Clip.Str(a, "to");
                if (arc.T1 <= arc.T0) throw new ClipException($"{w} arc: t1 must be after t0");
                s.Arc = arc;
            }
            return s;
        }
    }
}
