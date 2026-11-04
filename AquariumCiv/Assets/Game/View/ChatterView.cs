using System;
using System.Collections.Generic;
using System.Diagnostics;
using AquariumCiv.Audio;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Shaping;
using AquariumCiv.View.ShowMe;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// The wordless chatter on screen (#144, research 12): reads who stands together, feeds <see cref="ChatterPlan"/> each frame, draws its
    /// marks and plays their babble, and turns a knock into a <c>!</c> before the body moves (#161). All marks are one mesh, one material and
    /// one draw call, rebuilt in world space each frame from five shapes made once with <see cref="MeshBuilder"/>, facing the camera at about
    /// 20 pt on screen whatever the zoom. <see cref="ShowMeDirector"/> owns it and drives it: this class decides nothing about quiet.
    /// </summary>
    public sealed class ChatterView
    {
        /// <summary>The style in play: the debug panel keeps this until the user picks. Gesture babble has no motion of its own yet: it shows as marks.</summary>
        public static ChatterStyle Style = ChatterStyle.MarksBabble;

        /// <summary>Two people this close (box units) and standing still count as together.</summary>
        public const float GroupRadius = 0.5f;
        /// <summary>A fire this near the group's middle makes it a fireside group.</summary>
        public const float FireRadius = 1f;
        const float GroupSeconds = 0.5f, RapportSeconds = 30f; // ties change seldom, and the graph is the dear part
        /// <summary>The mark's diameter on screen, and panel units (1080 wide) to a point (44 pt is 121 panel units).</summary>
        public const float MarkPoints = 20f, PanelPerPoint = 2.75f;
        const float PopSeconds = 0.12f, FadeSeconds = KnockReaction.FadeSeconds;
        const int Sides = 18;

        public readonly ChatterPlan Plan = new();

        sealed class Live { public ChatterShow show; public double fadeFrom = -1; public int order; public Vector2 spread; }

        readonly List<Live> live = new();
        readonly List<ChatterGroup> groups = new();
        readonly List<(PersonRef who, Vector2 at, ActivityKind kind, bool on)> standing = new();
        readonly List<VoiceMember> lone = new();
        /// <summary>#316: the voices by distance (far: a rare muffled shout; mid: more; near: exchanges). See <see cref="VoicePlan"/>.</summary>
        public readonly VoicePlan Voice = new();
        float hour = 12f;
        readonly Dictionary<long, Rapport> rapport = new();
        readonly Dictionary<ChatterMark, Shape> shapes = new();
        readonly List<Vector3> verts = new();
        readonly List<Color> colors = new();
        readonly List<int> tris = new();
        readonly Stopwatch watch = new();
        double nextGroups, nextRapport;
        long rapportMonth = -1;
        bool clipsMade;
        GameObject go;
        Mesh mesh;
        Material material;
        /// <summary>After the glass, the smoke and the glow.</summary>
        const int MarksQueue = 3010;

        sealed class Shape { public Vector3[] v; public Color[] c; public int[] t; }

        /// <summary>Groups read last, marks drawn last frame and the people over whom (tests, debug).</summary>
        public IReadOnlyList<ChatterGroup> Groups => groups;
        public int MarksDrawn { get; private set; }
        public readonly List<PersonRef> DrawnOver = new();
        public readonly List<ChatterMark> DrawnMarks = new();
        /// <summary>The mark's size on screen last frame, in pixels (tests).</summary>
        public float MarkPixels { get; private set; }
        /// <summary>The renderer, for the draw-call count (tests).</summary>
        public MeshRenderer Renderer { get; private set; }
        /// <summary>Babbles started so far (tests), and the last zoom seen (0 = whole box, 1 = closest).</summary>
        public int Babbles { get; private set; }
        public float Zoom01 { get; private set; }
        /// <summary>Time spent in <see cref="Step"/> and <see cref="Draw"/>, over <see cref="CostFrames"/> frames (reset by <see cref="ResetCost"/>).</summary>
        public double CostMs => watch.Elapsed.TotalMilliseconds;
        public int CostFrames { get; private set; }
        public double WorstMs { get; private set; }
        public void ResetCost() { watch.Reset(); CostFrames = 0; WorstMs = 0; }

        public static ChatterStyle Effective => Style == ChatterStyle.GestureBabble ? ChatterStyle.Marks : Style;

        public static PersonRef RefOf(int id) => id > 0 ? PersonRef.Notable(id) : PersonRef.Crowd(-id - 1);

        static double Now => Time.unscaledTimeAsDouble;

        /// <summary>0 at the whole box (the widest zoom), 1 at the closest. <see cref="BoxCamera"/>'s zoom shrinks as it comes in.</summary>
        public static float Zoom01Of(BoxCamera cam) => cam == null ? 0f : Mathf.InverseLerp(cam.maxZoom, cam.minZoom, cam.zoom);

        // ================================================================== a frame

        /// <summary>One step: the groups (twice a second), the plan, new sounds. <paramref name="quiet"/> and <paramref name="realLine"/> are the director's.</summary>
        public void Step(PeopleView pv, BoxCamera cam, SimCore sim, bool quiet, bool realLine)
        {
            if (pv == null || sim == null || pv.Planner == null) return;
            long t0 = Stopwatch.GetTimestamp();
            watch.Start();
            if (!clipsMade) { clipsMade = true; if (AudioDirector.Instance != null) AudioDirector.Instance.Preload("proc:babble"); }
            double now = Now;
            if (now >= nextGroups)
            {
                nextGroups = now + GroupSeconds;
                ReadRapport(sim, now);
                ReadGroups(pv, sim);
            }
            Zoom01 = Zoom01Of(cam);
            hour = pv.Now.hour;
            var tick = Plan.Tick(now, groups, sim.State.seed, Zoom01, realLine, quiet, Effective);
            foreach (var s in tick.fade)
            {
                var d = live.Find(l => l.show == s);
                if (d != null && d.fadeFrom < 0) d.fadeFrom = now;
            }
            foreach (var s in tick.show)
            {
                live.Add(new Live { show = s, order = ++lastOrder });
                if (Effective != ChatterStyle.MarksBabble || s.reaction || quiet) continue;
                Speak(s, now, quiet);
            }
            if (Effective == ChatterStyle.MarksBabble && !quiet && !realLine && Voice.TickLone(now, VoiceZoom, hour, lone, out var lv))
                Say(lv, now, quiet, true);
            watch.Stop();
            Note(t0);
        }

        /// <summary>The zoom the voices hear: the camera's, or the debug panel's forced one.</summary>
        public float VoiceZoom => AudioDirector.ForcedZoom01 >= 0f ? AudioDirector.ForcedZoom01 : Zoom01;

        /// <summary>#316: a group's turn started: the voice plan says whether it is a line, an exchange or nothing (far, resting).</summary>
        void Speak(ChatterShow s, double now, bool quiet, bool force = false)
        {
            var g = groups.Find(x => x.id == s.groupId);
            if (g == null || (!g.onScreen && !force)) return;
            var vg = new VoiceGroup { id = g.id, nearFire = g.nearFire, working = g.working };
            foreach (var m in g.members) if (!m.asleep) vg.members.Add(new VoiceMember { id = m.id, voice = m.voice, kid = m.kid });
            foreach (var t in Voice.OnTurn(now, VoiceZoom, hour, vg, s.speakerId, s.listenerId, s.mark, force))
                Say(t, now, quiet, false, t.delay > 0.0);
        }

        /// <summary>Plays one voice turn (a delayed one also gets its own mark over the speaker, so a reply shows who answers).</summary>
        void Say(VoiceTurn t, double now, bool quiet, bool markIt, bool reply = false)
        {
            string cue = t.muffled ? CueLibrary.BabbleMuffled(t.voice, t.clip) : CueLibrary.Babble(t.voice, t.clip);
            Sfx.Play(cue, t.semitones, t.volume, (float)t.delay);
            Babbles++;
            if (markIt || reply) Mark(now + t.delay, t.speakerId, t.voice, t.mark, quiet, VoiceMarkSeconds);
        }

        const float VoiceMarkSeconds = 1.6f;

        void Note(long t0)
        {
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (ms > WorstMs) WorstMs = ms;
            CostFrames++;
        }

        void ReadRapport(SimCore sim, double now)
        {
            if (sim.State.month == rapportMonth || now < nextRapport) return;
            rapportMonth = sim.State.month;
            nextRapport = now + RapportSeconds;
            rapport.Clear();
            foreach (var e in sim.RelationshipGraph(true))
            {
                var best = Rapport.Idle;
                foreach (var k in e.kinds)
                {
                    var r = RapportOf(k);
                    if (Rank(r) > Rank(best)) best = r;
                }
                if (best != Rapport.Idle) rapport[Pair(e.a, e.b)] = best;
            }
        }

        /// <summary>What a tie makes of a talk: rivals bristle, ex-rivals aren't sure yet, a master and apprentice are as friends; stories in common are just talk.</summary>
        public static Rapport RapportOf(TieKind k) => k switch
        {
            TieKind.Partner => Rapport.Partner,
            TieKind.Parent or TieKind.Child or TieKind.Sibling => Rapport.Family,
            TieKind.Friend or TieKind.Mentor or TieKind.Apprentice => Rapport.Friend,
            TieKind.Rival => Rapport.Rival,
            TieKind.MadeUp => Rapport.Unsure,
            _ => Rapport.Idle,
        };

        static int Rank(Rapport r) => r switch { Rapport.Rival => 5, Rapport.Partner => 4, Rapport.Family => 3, Rapport.Friend => 2, Rapport.Unsure => 1, _ => 0 };

        static long Pair(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        void ReadGroups(PeopleView pv, SimCore sim)
        {
            groups.Clear();
            standing.Clear();
            var acts = pv.Activities;
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (a.moving || a.kind == ActivityKind.Sleep || a.kind == ActivityKind.None || pv.IsWalking(a.who)) continue;
                if (!pv.TryGetDollTop(a.who, out var top) || !pv.TryGetDollPosition(a.who, out var at)) continue;
                standing.Add((a.who, at, a.kind, OnScreen(top)));
            }
            int n = standing.Count;
            var group = new int[n];
            for (int i = 0; i < n; i++) group[i] = i;
            int Root(int i) { while (group[i] != i) i = group[i] = group[group[i]]; return i; }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if ((standing[i].at - standing[j].at).sqrMagnitude < GroupRadius * GroupRadius) group[Root(i)] = Root(j);
            var byRoot = new Dictionary<int, ChatterGroup>();
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            for (int i = 0; i < n; i++)
            {
                int r = Root(i);
                if (!byRoot.TryGetValue(r, out var g)) byRoot[r] = g = new ChatterGroup { id = int.MaxValue };
                var who = standing[i].who;
                g.id = Mathf.Min(g.id, who.Key + 1000000); // the same people keep their turn order; crowd keys are negative
                g.members.Add(new ChatterPerson { id = who.Key, voice = VoiceOf(pv, sim, who), kid = IsKid(pv, sim, who) });
                g.onScreen |= standing[i].on;
                g.working |= standing[i].kind >= ActivityKind.Gather && standing[i].kind <= ActivityKind.Prepare;
            }
            foreach (var kv in byRoot)
            {
                var g = kv.Value;
                if (g.members.Count < 2) continue;
                foreach (var a in g.members)
                    foreach (var b in g.members)
                        if (a != b && a.id > 0 && b.id > 0 && rapport.TryGetValue(Pair(a.id, b.id), out var r)) a.toward[b.id] = r;
                g.nearFire = NearFire(box, kv.Key);
                groups.Add(g);
            }
            ReadLone(pv, sim);
        }

        /// <summary>#316: people on screen, awake and not in a group: who a far or mid voice may come from.</summary>
        void ReadLone(PeopleView pv, SimCore sim)
        {
            lone.Clear();
            var inGroup = new HashSet<int>();
            foreach (var g in groups) foreach (var m in g.members) inGroup.Add(m.id);
            var acts = pv.Activities;
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (a.kind == ActivityKind.Sleep || a.kind == ActivityKind.Nap || a.kind == ActivityKind.None || inGroup.Contains(a.who.Key)) continue;
                if (!pv.TryGetDollTop(a.who, out var top) || !OnScreen(top)) continue;
                lone.Add(new VoiceMember { id = a.who.Key, voice = VoiceOf(pv, sim, a.who), kid = IsKid(pv, sim, a.who) });
            }
        }

        /// <summary>Inside the camera's view, with a small margin.</summary>
        static bool OnScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x > -0.02f && v.x < 1.02f && v.y > -0.02f && v.y < 1.02f;
        }

        static bool IsKid(PeopleView pv, SimCore sim, PersonRef who)
        {
            var n = who.IsNotable ? sim.FindNotable(who.notableId) : null;
            if (n != null) return n.age < 16f * 0.6f;
            return pv.Planner.StageOf(who) == LifeStage.Kid;
        }

        bool NearFire(BoxView box, int root)
        {
            if (box == null || box.layout == null) return false;
            var at = standing[root].at;
            foreach (var p in box.layout.pieces)
                if (p.kind == PieceKind.Fire && (new Vector2(p.x, p.z) - at).sqrMagnitude < FireRadius * FireRadius) return true;
            return false;
        }

        static int VoiceOf(PeopleView pv, SimCore sim, PersonRef who)
        {
            var n = who.IsNotable ? sim.FindNotable(who.notableId) : null;
            if (n != null) return ChatterBabble.VoiceFor(n.age, who.Key);
            return ChatterBabble.VoiceFor(pv.Planner.StageOf(who) == LifeStage.Kid ? 8f : 30f, who.Key);
        }

        // ================================================================== the knock (#161)

        /// <summary>
        /// The glass is knocked: a <c>!</c> over those who react (the planner says who), nearest the finger first. Returns how many real seconds each
        /// one's body waits (<see cref="PersonRef.Key"/> to seconds), empty if no mark shows: nobody waits for a mark that isn't there.
        /// </summary>
        public Dictionary<int, float> Knock(PeopleView pv, Camera cam, SimCore sim, LifePlanner planner, ViewTime at, Vector2 screen, bool quiet)
        {
            var late = new Dictionary<int, float>();
            if (pv == null || planner == null || sim == null) return late;
            var seen = new List<KnockWitness>();
            foreach (var who in planner.People)
            {
                if (!pv.TryGetDollTop(who, out var top) || cam == null) continue;
                Vector2 s = cam.WorldToScreenPoint(top);
                seen.Add(new KnockWitness { id = who.Key, voice = VoiceOf(pv, sim, who), reacts = planner.WouldReact(who, KeeperGesture.TapGlass, at), distance = (s - screen).magnitude });
            }
            double now = Now;
            foreach (var b in KnockReaction.Plan(now, seen))
            {
                if (!Mark(b.bangAt, b.id, b.voice, ChatterMark.Bang, quiet, KnockReaction.LeadSeconds + KnockReaction.HoldSeconds)) continue;
                late[b.id] = (float)KnockReaction.BodyDelay(b, now);
            }
            LastKnockBeats = late.Count;
            return late;
        }

        /// <summary>Marks the last knock raised (tests).</summary>
        public int LastKnockBeats { get; private set; }

        /// <summary>The stage-3 scholar who answers a knock hears it the same way.</summary>
        public void KnockHeard(PeopleView pv, SimCore sim, PersonRef who, bool quiet)
        {
            if (pv == null || sim == null) return;
            var beats = KnockReaction.Plan(Now, new[] { new KnockWitness { id = who.Key, voice = VoiceOf(pv, sim, who), reacts = true } });
            foreach (var b in beats) Mark(b.bangAt, b.id, b.voice, ChatterMark.Bang, quiet, KnockReaction.LeadSeconds + KnockReaction.HoldSeconds);
        }

        bool Mark(double at, int id, int voice, ChatterMark mark, bool quiet, float seconds)
        {
            var s = Plan.React(at, id, voice, mark, Effective, quiet, seconds);
            if (s == null) return false;
            live.Add(new Live { show = s, order = ++lastOrder });
            return true;
        }

        // ================================================================== drawing

        /// <summary>Lays out the live marks for this frame. Call it after the dolls and the camera have moved.</summary>
        public void Draw(PeopleView pv, Camera cam)
        {
            if (pv == null || cam == null) { Hide(); return; }
            long t0 = Stopwatch.GetTimestamp();
            watch.Start();
            verts.Clear(); colors.Clear(); tris.Clear();
            DrawnOver.Clear(); DrawnMarks.Clear();
            double now = Now;
            float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float px = MarkPoints * PanelPerPoint * Screen.width / GameUiPanel.ReferenceWidth;
            MarkPixels = px;
            var right = cam.transform.right;
            var up = cam.transform.up;
            var forward = cam.transform.forward;
            Spread(pv, cam, px, now);
            DrawnAt.Clear();
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var d = live[i];
                double end = d.fadeFrom >= 0 ? d.fadeFrom + FadeSeconds : d.show.endAt + FadeSeconds;
                if (now >= end) { live.RemoveAt(i); continue; }
                if (now < d.show.startAt) continue;
                float alpha = Mathf.Clamp01((float)((end - now) / FadeSeconds));
                float pop = Mathf.Clamp01((float)((now - d.show.startAt) / PopSeconds));
                pop = 1f + 0.18f * Mathf.Sin(pop * Mathf.PI) - 0.5f * (1f - pop); // a small pop, no bounce to speak of
                var who = RefOf(d.show.speakerId);
                if (!pv.TryGetDollTop(who, out var top)) continue;
                float depth = Vector3.Dot(top - cam.transform.position, forward);
                if (depth <= 0f) continue;
                float perPx = cam.orthographic ? 2f * cam.orthographicSize / Screen.height : 2f * depth * tan / Screen.height;
                float size = px * perPx;
                var at = top + up * (size * 0.72f) + (right * d.spread.x + up * d.spread.y) * perPx;
                Append(Shapes(d.show.mark), at, right * (size * pop), up * (size * pop), alpha);
                DrawnAt.Add(cam.WorldToScreenPoint(at));
                DrawnOver.Add(who);
                DrawnMarks.Add(d.show.mark);
            }
            MarksDrawn = DrawnOver.Count;
            Upload();
            watch.Stop();
            Note(t0);
        }

        /// <summary>Tester run 8: marks over a tight group sat on each other. When the marks up change they're laid out with <see cref="MarkerLayout"/>, oldest first, so a
        /// mark that's up stays where it is and only a newcomer moves to a free spot beside it. <see cref="Live.spread"/> is in pixels.</summary>
        void Spread(PeopleView pv, Camera cam, float px, double now)
        {
            int key = 0;
            foreach (var d in live) if (now >= d.show.startAt) key = key * 31 + d.order;
            if (key == spreadKey) return;
            spreadKey = key;
            spreadInput.Clear();
            foreach (var d in live)
            {
                d.spread = Vector2.zero;
                if (now < d.show.startAt || !pv.TryGetDollTop(RefOf(d.show.speakerId), out var top)) continue;
                var s = cam.WorldToScreenPoint(top);
                spreadInput.Add(new RelMarker(d.order, RelKind.Other, s.x, Screen.height - s.y));
            }
            if (spreadInput.Count < 2) return;
            float w = px * MarkGap;
            var res = MarkerLayout.Layout(spreadInput, w, w, Screen.width, Screen.height, spreadInput.Count, 0.001f, float.MaxValue);
            foreach (var p in res.placed)
            {
                var d = live.Find(l => l.order == p.ids[0]);
                if (d != null) d.spread = new Vector2(p.x - p.anchorX, -(p.y - (p.anchorY - w)));
            }
        }

        const float MarkGap = 1.12f;
        int lastOrder, spreadKey;
        readonly List<RelMarker> spreadInput = new();
        /// <summary>Where each mark was drawn last frame, in pixels (tests).</summary>
        public readonly List<Vector3> DrawnAt = new();

        void Append(Shape s, Vector3 at, Vector3 right, Vector3 up, float alpha)
        {
            int start = verts.Count;
            for (int i = 0; i < s.v.Length; i++)
            {
                verts.Add(at + right * s.v[i].x + up * s.v[i].y);
                var c = s.c[i];
                c.a *= alpha;
                colors.Add(c);
            }
            for (int i = 0; i < s.t.Length; i++) tris.Add(start + s.t[i]);
        }

        void Upload()
        {
            if (verts.Count == 0) { Hide(); return; }
            if (go == null) Make();
            if (!go.activeSelf) go.SetActive(true);
            mesh.Clear(true);
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f); // world space: the camera culls by this, the marks are always near the box
        }

        void Hide() { if (go != null && go.activeSelf) go.SetActive(false); }

        void Make()
        {
            go = new GameObject("ChatterMarks") { hideFlags = HideFlags.DontSave };
            mesh = new Mesh { name = "ChatterMarks", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = go.AddComponent<MeshRenderer>();
            // Tester run 8: a mark says who, so no roof or frame cuts it. Its own copy of the material: everything else in the box keeps its depth test.
            material = new Material(ShapingView.LoadFx()) { name = "ChatterMarks", hideFlags = HideFlags.DontSave, renderQueue = MarksQueue };
            material.SetFloat("_ZTest", (float)CompareFunction.Always);
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
        }

        /// <summary>The director is going: nothing outlives it.</summary>
        public void Dispose()
        {
            if (go != null) UnityEngine.Object.Destroy(go);
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            if (material != null) UnityEngine.Object.Destroy(material);
            go = null; mesh = null; material = null; Renderer = null;
            live.Clear(); Plan.Reset();
        }

        // ================================================================== the five shapes
        Shape Shapes(ChatterMark m)
        {
            if (shapes.TryGetValue(m, out var s)) return s;
            var mb = new MeshBuilder(144) { jitter = 0f };
            var ink = new Color(0.17f, 0.11f, 0.07f, 1f);
            Disc(mb, Vector2.zero, 0.54f, 0.54f, new Color(0.2f, 0.14f, 0.1f, 0.3f));
            Disc(mb, Vector2.zero, 0.5f, 0.5f, Color.white);
            mb.Tri(new Vector3(-0.13f, -0.44f), new Vector3(0.13f, -0.44f), new Vector3(0f, -0.66f), Color.white);
            switch (m)
            {
                case ChatterMark.Dots:
                    for (int i = -1; i <= 1; i++) Disc(mb, new Vector2(i * 0.26f, 0f), 0.09f, 0.09f, ink);
                    break;
                case ChatterMark.Note:
                    Disc(mb, new Vector2(-0.1f, -0.2f), 0.14f, 0.11f, ink);
                    Bar(mb, new Vector2(0.02f, -0.2f), new Vector2(0.02f, 0.28f), 0.05f, ink);
                    Bar(mb, new Vector2(0.02f, 0.28f), new Vector2(0.22f, 0.12f), 0.06f, ink);
                    break;
                case ChatterMark.Heart:
                    Disc(mb, new Vector2(-0.13f, 0.08f), 0.17f, 0.17f, ink);
                    Disc(mb, new Vector2(0.13f, 0.08f), 0.17f, 0.17f, ink);
                    mb.Tri(new Vector3(-0.29f, 0.03f), new Vector3(0.29f, 0.03f), new Vector3(0f, -0.3f), ink);
                    break;
                case ChatterMark.Bang:
                    Bar(mb, new Vector2(0f, 0.3f), new Vector2(0f, -0.08f), 0.09f, ink);
                    Disc(mb, new Vector2(0f, -0.25f), 0.08f, 0.08f, ink);
                    break;
                case ChatterMark.Question:
                    var last = new Vector2(-0.19f, 0.14f);
                    for (int i = 1; i <= 8; i++)
                    {
                        float a = Mathf.Lerp(170f, -80f, i / 8f) * Mathf.Deg2Rad;
                        var p = new Vector2(Mathf.Cos(a) * 0.19f, 0.14f + Mathf.Sin(a) * 0.17f);
                        Bar(mb, last, p, 0.07f, ink);
                        last = p;
                    }
                    Bar(mb, last, new Vector2(0.02f, -0.1f), 0.07f, ink);
                    Disc(mb, new Vector2(0.02f, -0.27f), 0.07f, 0.07f, ink);
                    break;
            }
            s = new Shape { v = mb.verts.ToArray(), c = mb.colors.ToArray(), t = mb.tris.ToArray() };
            shapes[m] = s;
            return s;
        }

        static void Disc(MeshBuilder mb, Vector2 c, float rx, float ry, Color col)
        {
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides, b = (i + 1) * Mathf.PI * 2f / Sides;
                mb.Tri(new Vector3(c.x, c.y), new Vector3(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry), new Vector3(c.x + Mathf.Cos(b) * rx, c.y + Mathf.Sin(b) * ry), col);
            }
        }

        static void Bar(MeshBuilder mb, Vector2 a, Vector2 b, float width, Color col)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * (width * 0.5f);
            mb.Tri(a + n, b + n, b - n, col);
            mb.Tri(a + n, b - n, a - n, col);
        }

        // ================================================================== debug
        /// <summary>Debug: the next style, Off to Gesture babble.</summary>
        public static string DebugNextStyle()
        {
            Style = (ChatterStyle)(((int)Style + 1) % 4);
            return "chatter: " + Style + (Style == ChatterStyle.GestureBabble ? " (no motion yet: shows as marks)" : "");
        }

        /// <summary>Debug: every group takes its turn now.</summary>
        public string DebugForceChat()
        {
            Plan.Hurry(Now);
            nextGroups = 0;
            return groups.Count > 0 ? $"chatter: {groups.Count} groups go now" : "chatter: nobody stands together (try Gather 6)";
        }

        /// <summary>Debug: this mark over the first person on view.</summary>
        public string DebugMark(PeopleView pv, ChatterMark mark)
        {
            if (pv == null) return "no people";
            var acts = pv.Activities;
            for (int i = 0; i < acts.Count; i++)
            {
                if (!pv.TryGetDollTop(acts[i].who, out _)) continue;
                return Mark(Now, acts[i].who.Key, 0, mark, false, 2.2f) ? $"chatter: {mark} over {acts[i].who}" : "chatter is off";
            }
            return "nobody on view";
        }

        // ================================================================== debug (#316)
        /// <summary>Debug: a lone voice now from somebody on view, at the zoom the voices hear (far: muffled, quiet).</summary>
        public string DebugLoneVoice()
        {
            if (lone.Count == 0 && groups.Count > 0) foreach (var m in groups[0].members) lone.Add(new VoiceMember { id = m.id, voice = m.voice, kid = m.kid });
            if (lone.Count == 0) return "voices: nobody on view";
            var who = lone[Voice.Lone % lone.Count];
            var t = Voice.LoneNow(VoiceZoom, who);
            Say(t, Now, false, true);
            return $"voices: lone {(t.muffled ? "muffled " : "")}clip {t.clip} vol {t.volume:0.00} band {Voice.BandOf(VoiceZoom)}";
        }

        /// <summary>Debug: the first group has an exchange now (turns with pauses), whatever the zoom or its rest.</summary>
        public string DebugExchange()
        {
            if (groups.Count == 0) return "voices: nobody stands together (try Gather 6)";
            var g = groups[0];
            var first = g.members[0];
            var show = new ChatterShow { groupId = g.id, speakerId = first.id, listenerId = g.members[1].id, voice = first.voice, mark = ChatterMark.Dots, startAt = Now, endAt = Now + 2.2 };
            int before = Babbles;
            Speak(show, Now, false, true);
            return $"voices: exchange in group {g.id} ({g.members.Count} people), {Babbles - before} turns, band {Voice.BandOf(VoiceZoom)}";
        }

        /// <summary>Debug: the band the voices hear: the camera's own, or forced to far, mid or near.</summary>
        public static string DebugCycleBand()
        {
            float z = AudioDirector.ForcedZoom01;
            AudioDirector.ForcedZoom01 = z < 0f ? 0.1f : z < 0.2f ? 0.4f : z < 0.5f ? 0.9f : -1f;
            return "voice band: " + BandLabel();
        }

        public static string BandLabel() => AudioDirector.ForcedZoom01 < 0f ? "auto" : AudioDirector.ForcedZoom01 < 0.2f ? "far (forced)" : AudioDirector.ForcedZoom01 < 0.5f ? "mid (forced)" : "near (forced)";

        public string VoiceStatus() => $"voices: {Voice.BandOf(VoiceZoom)} (zoom {VoiceZoom:0.00}, {BandLabel()}), vol {Voice.VolumeAt(VoiceZoom):0.00}{(Voice.Muffled(VoiceZoom) ? " muffled" : "")}, lone {Voice.Lone}, exchanges {Voice.Exchanges}, lines {Voice.Singles}, {lone.Count} on view";

        public string Status() => $"chatter: {Style}, {groups.Count} groups, {MarksDrawn} marks, zoom {Zoom01:0.00}, babbles {Babbles}";
    }
}
