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
            DollStyle.Speech,
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
                r.Marks.Add(new PrincipleMark { Doll = h.Doll, Kind = "contact", T0 = h.T0, T1 = h.T1 });
                if (h.Target == null) { r.Fail($"{h.Doll} hits at {h.T0:0.00}s with no target (the verb has no object)"); continue; }
                float best = float.MaxValue; int bestF = h.F0;
                for (int f = h.F0; f <= h.F1; f++)
                {
                    if (!t.TargetAt(h.Target, f, out var c, out var rad)) { r.Fail($"{h.Doll}'s target '{h.Target}' is not in the clip"); best = -1f; break; }
                    float dist = Mathf.Max(0f, (d.Frames[f].Tip - c).magnitude - rad);
                    if (dist < best) { best = dist; bestF = f; }
                }
                if (best < 0f) continue;
                if (best > d.Width) r.Fail($"{h.Doll} hits at {h.T0:0.00}s but its tool is {best:0.000} u from {h.Target} (> one doll-width {d.Width:0.000}; tool tip at height {d.Frames[bestF].Tip.y:0.00})");
                else r.Note($"{h.Doll} hits {h.Target} at {h.T0:0.00}s, tool {best:0.000} u off it (frame {bestF})");
            }
            return r;
        }

        // ---------------------------------------------------------------- 2. the target answers on the contact frame
        /// <summary>§4.2: the target visibly changes within <see cref="ExpectSpec.ReactSeconds"/> of the hit, and not before it.</summary>
        public static PrincipleResult CheckReact(Track t)
        {
            float window = t.Clip.Expect.ReactSeconds;
            var r = new PrincipleResult { Id = React, Title = $"the target answers within {window:0.00}s of the hit, not before (§4.2)" };
            var targeted = t.Hits.Where(h => h.Target != null).ToList();
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
                if (Idle.Contains(s)) continue;
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
            if (t.Clip.Expect.Arcs.Count == 0 && arcs.Count == 0) { r.Applies = false; return r.Note("no arcs expected or present"); }
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
            if (target != null && t.TargetAt(target, f1, out var c, out var rad))
            {
                float dl = Mathf.Max(0f, (o.Frames[f1].Pos - c).magnitude - rad);
                if (dl > width) r.Fail($"{o.Spec.Id} lands {dl:0.000} u from {target} (> {width:0.000})");
                else r.Note($"{o.Spec.Id}: {by} -> {target}, lands {dl:0.000} u off it at {a.T1:0.00}s");
            }
            else if (target != null) r.Fail($"{o.Spec.Id}'s target '{target}' is not in the clip");
        }
    }
}
