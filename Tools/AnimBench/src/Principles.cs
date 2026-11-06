// #352 AnimBench: research 17 §4's principles that can be checked without pixels, as reusable checks over a track.
// Each returns a PrincipleResult (pass / fail / n/a, the findings in words, and time marks for the curves). Tests in
// Tools/AnimBench.Tests run them over the committed clips; a clip lists the ones it fails today in "knownGaps".
using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.View;
using UnityEngine;
using PropSlot = AquariumCiv.View.PropSlot;

namespace AnimBench
{
    public sealed class PrincipleMark { public string Doll, Kind; public float T0, T1; }

    public sealed class PrincipleResult
    {
        public string Id, Title;
        public bool Applies = true, Pass = true, KnownGap;
        public readonly List<string> Findings = new List<string>();
        public readonly List<PrincipleMark> Marks = new List<PrincipleMark>();
        public string Status => !Applies ? "n/a" : Pass ? "pass" : "FAIL";
        public PrincipleResult Fail(string why) { Pass = false; Findings.Add(why); return this; }
        public PrincipleResult Note(string what) { Findings.Add(what); return this; }
        public override string ToString() => $"{Status,-4} {Id,-10} {Title}" + (KnownGap && Applies && !Pass ? "  [known gap]" : "") +
                                             string.Concat(Findings.Select(f => "\n       - " + f));
    }

    public static class Principles
    {
        public const string HitNear = "hitNear", React = "react", EndBeat = "endBeat", Neighbours = "neighbours", Arcs = "arcs";
        public static readonly string[] All = { HitNear, React, EndBeat, Neighbours, Arcs };

        /// <summary>Work and play loops: a move that repeats while the activity lasts (research 17 §4.5 wants each to end in something seen once).</summary>
        static readonly HashSet<DollStyle> LoopStyles = new HashSet<DollStyle>
        {
            DollStyle.Chop, DollStyle.Hammer, DollStyle.Hoe, DollStyle.Pick, DollStyle.Scatter, DollStyle.Fish, DollStyle.Stir, DollStyle.Sweep,
            DollStyle.Saw, DollStyle.Lift, DollStyle.SitStrike, DollStyle.SitWeave, DollStyle.Hop, DollStyle.Swim, DollStyle.Stomp, DollStyle.Crouch,
            DollStyle.Hang, DollStyle.Bell, DollStyle.Taste, DollStyle.Write, DollStyle.Lopsided, DollStyle.Talk, DollStyle.Argue, DollStyle.Haggle,
            DollStyle.Speech, DollStyle.PickBerry, DollStyle.DigRoot, DollStyle.TasteBerry,
        };

        /// <summary>Moves that can close a loop: told once or held (a flinch, a held-up catch, a cheer, a bow).</summary>
        static readonly HashSet<DollStyle> EndStyles = new HashSet<DollStyle>
        {
            DollStyle.Flinch, DollStyle.Peer, DollStyle.Knock, DollStyle.HoldUp, DollStyle.Bow, DollStyle.Freeze, DollStyle.Cheer, DollStyle.Wave,
            DollStyle.Laugh, DollStyle.PoseBig, DollStyle.HopBack,
        };

        /// <summary>Rest and getting about: never "the same move" for the neighbour check.</summary>
        static readonly HashSet<DollStyle> Idle = new HashSet<DollStyle>
        {
            DollStyle.Stand, DollStyle.Walk, DollStyle.Run, DollStyle.Hidden, DollStyle.Sit, DollStyle.Lie,
        };

        public static bool IsLoop(Segment s) => s.Hunt is HuntPhase.None or HuntPhase.Stalk && !s.EndBeat && LoopStyles.Contains(s.Style);
        public static bool IsEndBeat(Segment s) => s.EndBeat || s.Hunt is HuntPhase.Strike or HuntPhase.Bolt or HuntPhase.KidCheer || EndStyles.Contains(s.Style);

        public static List<PrincipleResult> CheckAll(Track t)
        {
            var list = new List<PrincipleResult> { CheckHitNear(t), CheckReact(t), CheckEndBeat(t), CheckNeighbours(t), CheckArcs(t) };
            foreach (var r in list) r.KnownGap = t.Clip.KnownGaps.Contains(r.Id);
            return list;
        }

        public static PrincipleResult Check(Track t, string id) => CheckAll(t).First(r => r.Id == id);

        // ---------------------------------------------------------------- 1. the verb touches its real target
        /// <summary>§4.1: on a hit, the tool's working end (or the hand) is within one doll-width of the target's surface.</summary>
        public static PrincipleResult CheckHitNear(Track t)
        {
            var r = new PrincipleResult { Id = HitNear, Title = "on a hit the tool/hand is within one doll-width of its target (§4.1)" };
            if (t.Hits.Count == 0) { r.Applies = false; return r.Note("no hit frames in the clip"); }
            foreach (var h in t.Hits)
            {
                var d = t.Doll(h.Doll);
                if (h.IsRelease) { r.Note($"{h.Doll} lets go of the ball at {h.T0:0.00}s (a release: the arcs check says where it lands)"); continue; }
                r.Marks.Add(new PrincipleMark { Doll = h.Doll, Kind = "contact", T0 = h.T0, T1 = h.T1 });
                // #362: a throw's hit is the thrown thing coming down: then it is the spear that has to touch its target
                var landing = t.Objects.FirstOrDefault(o => o.Spec.Arc != null && o.Spec.Arc.To != null && Math.Abs(o.Spec.Arc.T1 - h.T0) <= 0.15f && (o.Spec.Arc.From ?? "").StartsWith(h.Doll + "."));
                string aim = landing != null ? landing.Spec.Arc.To : h.Target;
                if (aim == null) { r.Fail($"{h.Doll} hits at {h.T0:0.00}s with no target (the verb has no object)"); continue; }
                float best = float.MaxValue; int bestF = h.F0;
                for (int f = h.F0; f <= h.F1; f++)
                {
                    if (!t.TargetAt(aim, f, out var c, out var rad)) { r.Fail($"{h.Doll}'s target '{aim}' is not in the clip"); best = -1f; break; }
                    var toolAt = landing != null ? landing.Frames[f].Pos : d.Frames[f].Tip;
                    float dist = Mathf.Max(0f, (toolAt - c).magnitude - rad);
                    if (dist < best) { best = dist; bestF = f; }
                }
                if (best < 0f) continue;
                float reach = t.Clip.Expect.HitReach ?? d.Width; // #358: the chase's contact is the alert radius
                if (best > reach) r.Fail($"{h.Doll} hits at {h.T0:0.00}s but its tool is {best:0.000} u from {aim} (> {(t.Clip.Expect.HitReach.HasValue ? "the clip's hitReach" : "one doll-width")} {reach:0.000}; tool tip at height {d.Frames[bestF].Tip.y:0.00})");
                else r.Note($"{h.Doll} hits {aim} at {h.T0:0.00}s, {(landing != null ? "the spear" : "tool")} {best:0.000} u off it (frame {bestF})");
            }
            return r;
        }

        // ---------------------------------------------------------------- 2. the target answers on the contact frame
        /// <summary>§4.2: the target visibly changes within <see cref="ExpectSpec.ReactSeconds"/> of the hit, and not before it.</summary>
        public static PrincipleResult CheckReact(Track t)
        {
            float window = t.Clip.Expect.ReactSeconds;
            var r = new PrincipleResult { Id = React, Title = $"the target answers within {window:0.00}s of the hit, not before (§4.2)" };
            var targeted = t.Hits.Where(h => h.Target != null && !h.IsRelease).ToList();
            if (targeted.Count == 0) { r.Applies = false; return r.Note("no hit with a target"); }
            foreach (var h in targeted)
            {
                int before = Math.Max(0, h.F0 - 1);
                int early = Math.Max(0, before - (int)Math.Round(window * t.Clip.Fps));
                string was = Changed(t, h.Target, early, before);
                if (was != null) r.Fail($"{h.Target} was already answering before {h.Doll}'s hit at {h.T0:0.00}s ({was})");
                int answer = -1; string how = null;
                for (int f = h.F0; f < t.Count; f++)
                {
                    how = Changed(t, h.Target, before, f);
                    if (how != null) { answer = f; break; }
                }
                if (answer < 0) { r.Fail($"{h.Target} never answers {h.Doll}'s hit at {h.T0:0.00}s"); continue; }
                float delay = t.Times[answer] - h.T0;
                r.Marks.Add(new PrincipleMark { Doll = h.Doll, Kind = "answer", T0 = t.Times[answer], T1 = t.Times[answer] });
                if (delay > window + 0.5f / t.Clip.Fps) r.Fail($"{h.Target} answers {h.Doll}'s hit {delay:0.00}s late ({how}; want <= {window:0.00}s)");
                else r.Note($"{h.Target} answers {h.Doll}'s hit after {delay:0.00}s ({how})");
            }
            return r;
        }

        /// <summary>What changed about a target between two frames (null: nothing you'd see).</summary>
        public static string Changed(Track t, string id, int a, int b)
        {
            var d = t.Doll(id);
            if (d != null)
            {
                var A = d.Frames[a]; var B = d.Frames[b];
                if (A.Style != B.Style) return $"{A.Style} -> {B.Style}";
                if ((A.Pos - B.Pos).magnitude > 0.01f) return "moved";
                return null;
            }
            var o = t.Object(id);
            if (o == null) return null;
            var p = o.Frames[a]; var q = o.Frames[b];
            if (p.Visible != q.Visible) return q.Visible ? "appears" : "disappears";
            if (p.Act != q.Act) return $"{p.Act} -> {q.Act}";
            if ((p.Pos - q.Pos).magnitude > 0.004f) return $"moved {(p.Pos - q.Pos).magnitude:0.000} u";
            if (Mathf.Abs(Mathf.DeltaAngle(p.Yaw, q.Yaw)) > 8f) return "turned";
            if (p.State != null && q.State != null)
                foreach (var kv in q.State)
                    if (p.State.TryGetValue(kv.Key, out var v) && Mathf.Abs(v - kv.Value) > 1e-3f) return $"{kv.Key} {v:0.##} -> {kv.Value:0.##}";
            return null;
        }

        // ---------------------------------------------------------------- 5. a clear end beat
        /// <summary>§4.5: every work/play loop in the clip is followed by an end beat (a move told once or held: the strike, the held-up catch, a cheer).</summary>
        public static PrincipleResult CheckEndBeat(Track t)
        {
            var r = new PrincipleResult { Id = EndBeat, Title = "every loop ends in a beat you see once (§4.5)" };
            bool any = false;
            foreach (var d in t.Dolls)
            {
                var segs = d.Spec.Segments;
                for (int i = 0; i < segs.Count; i++)
                {
                    var s = segs[i];
                    if (!IsLoop(s) || s.T0 >= t.Clip.Duration) continue;
                    any = true;
                    var next = i + 1 < segs.Count ? segs[i + 1] : null;
                    if (s.Plan != null && next != null && next.Plan == s.Plan && next.Style == s.Style && !next.EndBeat && next.T0 < t.Clip.Duration) continue; // #370: a staged moment's step is cut in short stretches: one loop
                    float end = Math.Min(s.T1, t.Clip.Duration);
                    r.Marks.Add(new PrincipleMark { Doll = d.Spec.Id, Kind = "loop", T0 = s.T0, T1 = end });
                    if (next == null || next.T0 >= t.Clip.Duration)
                        r.Fail($"{d.Spec.Id}: {s.Style} loops {s.T0:0.0}-{end:0.0}s to the end of the clip with no end beat");
                    else if (!IsEndBeat(next))
                        r.Fail($"{d.Spec.Id}: {s.Style} loop ends at {s.T1:0.0}s into {next.Style}{(next.Hunt != HuntPhase.None ? "/" + next.Hunt : "")}, not an end beat");
                    else
                    {
                        r.Note($"{d.Spec.Id}: {s.Style} loop ends in {(next.Hunt != HuntPhase.None ? next.Hunt.ToString() : next.Style.ToString())} at {next.T0:0.0}s");
                        r.Marks.Add(new PrincipleMark { Doll = d.Spec.Id, Kind = "end", T0 = next.T0, T1 = Math.Min(next.T1, t.Clip.Duration) });
                    }
                }
            }
            any |= CheckPlayEndBeats(t, r); // #358
            if (!any) { r.Applies = false; r.Note("no work or play loop in the clip"); }
            return r;
        }

        // ---------------------------------------------------------------- 6. no two neighbours in step
        /// <summary>§4.6: at no moment do 3+ dolls within <see cref="ExpectSpec.NeighbourRadius"/> of each other share one move (rest and walking don't count).</summary>
        public static PrincipleResult CheckNeighbours(Track t)
        {
            float radius = t.Clip.Expect.NeighbourRadius;
            var r = new PrincipleResult { Id = Neighbours, Title = $"no 3+ neighbours (within {radius:0.##} u) share one move (§4.6)" };
            if (t.Dolls.Count < 3) { r.Applies = false; return r.Note("fewer than 3 dolls"); }
            int stride = Math.Max(1, (int)Math.Round(t.Clip.Fps * 0.25f));
            float badFrom = -1f; string badWhat = null; int badFrames = 0, samples = 0;
            for (int f = 0; f < t.Count; f += stride)
            {
                samples++;
                string group = LargestGroup(t, f, radius);
                if (group != null)
                {
                    badFrames++;
                    if (badFrom < 0f) { badFrom = t.Times[f]; badWhat = group; }
                }
            }
            if (badFrames > 0)
            {
                r.Fail($"{badWhat} from {badFrom:0.00}s ({badFrames * 100 / Math.Max(1, samples)}% of the clip)");
                r.Marks.Add(new PrincipleMark { Doll = "", Kind = "inStep", T0 = badFrom, T1 = badFrom });
            }
            else r.Note("no 3 neighbours ever share a move");
            return r;
        }

        static string LargestGroup(Track t, int f, float radius)
        {
            var byStyle = new Dictionary<DollStyle, List<DollTrack>>();
            foreach (var d in t.Dolls)
            {
                var s = d.Frames[f].Style;
                if (Idle.Contains(s) || d.Frames[f].InStep) continue; // #358: the ring dance and its cheer move together on purpose (§0.8)
                if (!byStyle.TryGetValue(s, out var l)) byStyle[s] = l = new List<DollTrack>();
                l.Add(d);
            }
            foreach (var kv in byStyle)
            {
                var l = kv.Value;
                if (l.Count < 3) continue;
                // connected groups: neighbours of neighbours count (a ring of kids)
                var seen = new bool[l.Count];
                for (int i = 0; i < l.Count; i++)
                {
                    if (seen[i]) continue;
                    var comp = new List<int> { i }; seen[i] = true;
                    for (int k = 0; k < comp.Count; k++)
                        for (int j = 0; j < l.Count; j++)
                            if (!seen[j] && (l[comp[k]].Frames[f].Pos - l[j].Frames[f].Pos).magnitude <= radius) { seen[j] = true; comp.Add(j); }
                    if (comp.Count >= 3) return $"{comp.Count} dolls ({string.Join(", ", comp.Select(c => l[c].Spec.Id))}) all {kv.Key}";
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- arcs
        /// <summary>§4 / §6.4: every thrown thing the clip expects (ball, spear, berry, fish) leaves the thrower's hand and lands on its target.</summary>
        public static PrincipleResult CheckArcs(Track t)
        {
            var r = new PrincipleResult { Id = Arcs, Title = "arcs (ball, spear, berry, fish) start at the hand and land on the target" };
            var arcs = t.Objects.Where(o => o.Spec.Arc != null).ToList();
            if (t.Clip.Expect.Arcs.Count == 0 && arcs.Count == 0 && t.ArcEvents.Count == 0) { r.Applies = false; return r.Note("no arcs expected or present"); }
            CheckPlayArcs(t, r); // #358
            var checkedArcs = new HashSet<ObjectTrack>();
            foreach (var e in t.Clip.Expect.Arcs)
            {
                var o = arcs.FirstOrDefault(a => a.Spec.Kind == e.Kind && (e.By == null || (a.Spec.Arc.From ?? "").Split('.')[0] == e.By));
                if (o == null) { r.Fail($"no {e.Kind} arc from {e.By} to {e.Target}: the {e.Kind} never leaves the hand"); continue; }
                checkedArcs.Add(o);
                CheckOneArc(t, o, e.By, e.Target ?? o.Spec.Arc.To, r);
            }
            foreach (var o in arcs) if (!checkedArcs.Contains(o)) CheckOneArc(t, o, (o.Spec.Arc.From ?? "").Split('.')[0], o.Spec.Arc.To, r);
            return r;
        }

        /// <summary>#358: a play group's dolls have no segments: each run of play frames that isn't a payoff (a chase, a toss, a creep) must be
        /// followed by a payoff frame (<see cref="DollFrame.EndBeat"/>: the touch held, the catch, the laugh) before the clip ends.</summary>
        static bool CheckPlayEndBeats(Track t, PrincipleResult r)
        {
            bool any = false;
            foreach (var d in t.Dolls)
            {
                int owing = -1, loops = 0, ends = 0;
                for (int f = 0; f < t.Count; f++)
                {
                    var fr = d.Frames[f];
                    if (!fr.Playing) continue;
                    any = true;
                    if (fr.EndBeat) { if (owing >= 0) { ends++; owing = -1; } }
                    else if (!Idle.Contains(fr.Style) && owing < 0) { owing = f; loops++; }
                }
                if (owing >= 0) r.Fail($"{d.Spec.Id}: plays {d.Frames[owing].Style} from {t.Times[owing]:0.0}s to the end of the clip with no payoff");
                else if (loops > 0) r.Note($"{d.Spec.Id}: {loops} play rounds, each ends in a payoff");
            }
            return any;
        }

        /// <summary>#358: every throw of a play group's ball starts at the thrower's hands and (unless it was a miss) lands in the catcher's arms.</summary>
        static void CheckPlayArcs(Track t, PrincipleResult r)
        {
            foreach (var e in t.ArcEvents)
            {
                var o = t.Object(e.Object); var d = t.Doll(e.By);
                if (o == null || d == null) continue;
                int f0 = t.FrameAt(e.T0), f1 = Math.Min(t.Count - 1, t.FrameAt(e.T1));
                r.Marks.Add(new PrincipleMark { Doll = e.By, Kind = "arc", T0 = e.T0, T1 = e.T1 });
                float dh = Mathf.Min((o.Frames[f0].Pos - d.Frames[f0].HandR).magnitude, (o.Frames[f0].Pos - (d.Frames[f0].HandL + d.Frames[f0].HandR) * 0.5f).magnitude);
                if (dh > d.Width) r.Fail($"{e.Object} leaves {e.By} {dh:0.000} u from its hands at {e.T0:0.00}s (> {d.Width:0.000})");
                if (e.T1 > t.Times[t.Count - 1]) { r.Note($"{e.Object}: {e.By} throws at {e.T0:0.00}s, lands after the clip"); continue; }
                if (e.Target == null) { r.Note($"{e.Object}: {e.By} throws at {e.T0:0.00}s, a miss: it bounces and rolls"); continue; }
                if (t.TargetAt(e.Target, f1, out var c, out var rad))
                {
                    float dl = Mathf.Max(0f, (o.Frames[f1].Pos - c).magnitude - rad);
                    var td = t.Doll(e.Target);
                    float w = td != null ? td.Width : Bench.DollWidth;
                    if (dl > w) r.Fail($"{e.Object} lands {dl:0.000} u from {e.Target} (> {w:0.000})");
                    else r.Note($"{e.Object}: {e.By} -> {e.Target}, from {dh:0.000} u off the hands, lands {dl:0.000} u off it at {e.T1:0.00}s");
                }
            }
        }

        static void CheckOneArc(Track t, ObjectTrack o, string by, string target, PrincipleResult r)
        {
            var a = o.Spec.Arc;
            int f0 = t.FrameAt(a.T0), f1 = t.FrameAt(a.T1);
            r.Marks.Add(new PrincipleMark { Doll = by ?? "", Kind = "arc", T0 = a.T0, T1 = a.T1 });
            var d = by != null ? t.Doll(by) : null;
            float width = d != null ? d.Width : Bench.DollWidth;
            if (d != null)
            {
                var hand = d.Frames[f0];
                float dh = Mathf.Min((o.Frames[f0].Pos - hand.HandR).magnitude, (o.Frames[f0].Pos - hand.Tip).magnitude);
                if (dh > width) r.Fail($"{o.Spec.Id} starts {dh:0.000} u from {by}'s hand (> {width:0.000})");
            }
            Vector3 c = default; float rad = 0f; bool found = false;
            int dot = target != null ? target.LastIndexOf('.') : -1;
            if (dot > 0 && t.Doll(target.Substring(0, dot)) is DollTrack td) // #361: "picker.handL": the basket in a doll's hand
            {
                var fr = td.Frames[f1];
                string part = target.Substring(dot + 1);
                c = part == "handL" ? fr.HandL : part == "head" ? fr.Head : part == "tip" ? fr.Tip : fr.HandR; rad = 0.03f; found = true;
            }
            else if (target != null) found = t.TargetAt(target, f1, out c, out rad);
            if (target != null && found)
            {
                float dl = Mathf.Max(0f, (o.Frames[f1].Pos - c).magnitude - rad);
                if (dl > width) r.Fail($"{o.Spec.Id} lands {dl:0.000} u from {target} (> {width:0.000})");
                else r.Note($"{o.Spec.Id}: {by} -> {target}, lands {dl:0.000} u off it at {a.T1:0.00}s");
            }
            else if (target != null) r.Fail($"{o.Spec.Id}'s target '{target}' is not in the clip");
        }
    }
}
