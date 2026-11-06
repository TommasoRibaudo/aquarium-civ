// #370 AnimBench: a clip-level "moment" block builds the dolls from the game's own staged-moment plan (Life/MomentStagePlan.cs)
// and the view's spots and beats (View/Beats/MomentBeats.cs), so the bench plays exactly what the box plays.
//
//   "moment": { "kind": "Wedding", "seed": 7, "fire": [0, 0], "glass": [0, -1.9], "normal": [0, -1],
//               "a": [x, z], "b": [x, z], "officiant": [x, z], "guests": [[x, z], ...], "kids": [[x, z], ...] }
//
// a, b: the couple (wedding), the parent (newborn: a), the chief and the old chief (new chief). officiant: who blesses or speaks.
// The planner's walks to the place become straight walks at the walking pace (an approximation: the game walks the paths).
// The clip's duration defaults to the plan's. A "bundle" object that "follows" the newborn's parent is drawn between its hands.
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View;
using UnityEngine;
using PropSlot = AquariumCiv.View.PropSlot;

namespace AnimBench
{
    public static class MomentClip
    {
        /// <summary>The game's walking pace (LifePlanner.RealWalkSpeed).</summary>
        public static float WalkSpeed => LifePlanner.RealWalkSpeed;
        const float Sample = 0.1f;

        public static MomentStagePlan Expand(Clip c, JsonObject mo, bool setDuration)
        {
            var kind = Clip.Enum<MomentKind>(Clip.Str(mo, "kind") ?? "Wedding", "moment.kind");
            int seed = Clip.Int(mo, "seed", 370);
            var fire = mo["fire"] != null ? Clip.XZ(mo["fire"], "moment.fire") : Vector2.zero;
            var glass = mo["glass"] != null ? Clip.XZ(mo["glass"], "moment.glass") : new Vector2(0f, -1.9f);
            var normal = mo["normal"] != null ? Clip.XZ(mo["normal"], "moment.normal") : new Vector2(0f, -1f);
            var place = kind == MomentKind.Newborn ? glass : fire;
            var geo = new MomentGeometry { fireX = fire.x, fireZ = fire.y, glassX = glass.x, glassZ = glass.y, nx = normal.x, nz = normal.y, halfX = 3f, halfZ = 2.2f };
            int next = 1;
            MomentPerson P(JsonNode n, bool kid, string what)
            {
                var at = Clip.XZ(n, what);
                return MomentPerson.Of(next++, kid, at.x, at.y, (at - place).magnitude / WalkSpeed);
            }
            var cast = new MomentCast { babyKey = kind == MomentKind.Newborn ? 999 : 0 };
            if (mo["a"] != null) { cast.a = P(mo["a"], false, "moment.a"); cast.hasA = true; }
            if (mo["b"] != null) { cast.b = P(mo["b"], false, "moment.b"); cast.hasB = true; }
            if (mo["officiant"] != null) { cast.officiant = P(mo["officiant"], false, "moment.officiant"); cast.hasOfficiant = true; }
            if (mo["guests"] is JsonArray gs) foreach (var g in gs) cast.grown.Add(P(g, false, "moment.guests[]"));
            if (mo["kids"] is JsonArray ks) foreach (var k in ks) cast.kids.Add(P(k, true, "moment.kids[]"));
            var plan = MomentStagePlan.Build(kind, cast, geo, seed) ?? throw new ClipException("moment: " + MomentStagePlan.LastWhyNot);
            if (setDuration) c.Duration = plan.Duration + 0.6f;
            var roleCount = new Dictionary<MomentRole, int>();
            foreach (var m in plan.members)
            {
                roleCount.TryGetValue(m.role, out int k);
                roleCount[m.role] = k + 1;
                string id = m.role.ToString().ToLowerInvariant() + (k + 1);
                var d = new DollSpec { Id = id, Role = m.role.ToString(), Kid = m.kid, Seed = seed * 31 + m.key * 7 };
                d.Phase = Bench.Hash01(d.Seed, 5) * 37f;
                Fill(c, plan, m, d);
                c.Dolls.Add(d);
            }
            return plan;
        }

        /// <summary>The member's whole clip as short straight segments: before they join they stand where they are; the planner's walk
        /// is a straight walk at the walking pace; then the plan's spots (the ring and the orbit, sampled every 0.1 s).</summary>
        static void Fill(Clip c, MomentStagePlan plan, MomentMember m, DollSpec d)
        {
            float end = Math.Max(c.Duration, plan.Duration) + 1f;
            var start = new Vector2(m.x, m.z);
            if (m.JoinAt > 0f) d.Segments.Add(new Segment { T0 = 0f, T1 = m.JoinAt, Activity = ActivityKind.Watch, Style = DollStyle.Stand, From = start, To = start, Steps = 1, StepSeconds = 4f, Label = "own day" });
            for (int i = 0; i < m.steps.Count; i++)
            {
                var st = m.steps[i];
                var cuts = new List<float> { st.t0 };
                float arrive = -1f;
                if (st.travel)
                {
                    var from = i == 0 ? start : MomentBeats.Spot(plan, m, i - 1, m.steps[i - 1].t1, out _); // batch 2: from where the last step left them (a ring spot, the toss spot), not its plan point: no pop
                    arrive = Math.Min(st.t1, st.t0 + (new Vector2(st.x0, st.z0) - from).magnitude / WalkSpeed);
                    if (arrive > st.t0 + 1e-3f && arrive < st.t1 - 1e-3f) cuts.Add(arrive);
                }
                else if (st.beat is StageBeat.Ring or StageBeat.Centre || st.Moves || st.ring >= 0)
                    for (float t = st.t0 + Sample; t < st.t1 - 1e-3f; t += Sample) cuts.Add(t);
                cuts.Add(st.t1);
                for (int k = 0; k + 1 < cuts.Count; k++)
                {
                    float a = cuts[k], b = cuts[k + 1];
                    var seg = new Segment { T0 = a, T1 = b, Activity = st.kind, Style = DollMotion.StyleOf(st.kind), Steps = ActivityInfo.LoopSteps(st.kind), StepSeconds = 4f };
                    seg.Plan = plan; seg.Member = m; seg.StepIndex = i;
                    if (!st.travel) { seg.ActT0 = st.t0; seg.ActT1 = st.t1; } // batch 2: one activity over the step
                    Vector2? face;
                    if (st.travel)
                    {
                        var from = i == 0 ? start : MomentBeats.Spot(plan, m, i - 1, m.steps[i - 1].t1, out _); // batch 2: from where the last step left them (a ring spot, the toss spot), not its plan point: no pop
                        var to = new Vector2(st.x0, st.z0);
                        bool walking = arrive > a + 1e-4f && k == 0 && arrive < st.t1 - 1e-3f;
                        seg.From = walking ? from : to; seg.To = to;
                        if (walking) { seg.Activity = st.carries ? ActivityKind.Carry : ActivityKind.Walk; seg.Style = DollMotion.StyleOf(seg.Activity.Value); }
                        MomentBeats.Spot(plan, m, i, b, out face);
                        seg.Label = walking ? "walks there" : "there";
                    }
                    else
                    {
                        seg.From = MomentBeats.Spot(plan, m, i, a, out face);
                        seg.To = MomentBeats.Spot(plan, m, i, b, out _);
                        if (k == 0) seg.Label = st.beat.ToString();
                    }
                    if (face.HasValue) seg.Face = new FaceSpec { Kind = FaceKind.Point, Point = face.Value };
                    var (pk, slot) = PropOf(st.prop);
                    seg.Prop = pk; seg.Slot = slot;
                    seg.EndBeat = st.endBeat && k == 0;
                    if (st.beat == StageBeat.Toss || st.beat == StageBeat.TossWind) seg.Target = "fire";
                    if (st.beat == StageBeat.GoldPop) seg.Target = "goldmark"; // the clip's "mark" object over the chief's head
                    d.Segments.Add(seg);
                }
            }
            var last = d.Segments.Count > 0 ? d.Segments[d.Segments.Count - 1].To : start;
            d.Segments.Add(new Segment { T0 = m.ReleaseAt, T1 = end, Activity = ActivityKind.Watch, Style = DollStyle.Stand, From = last, To = last, Steps = 1, StepSeconds = 4f, Label = "back to the day" });
        }

        static (PropKind, PropSlot) PropOf(Prop p) => p switch
        {
            Prop.Log => (PropKind.Log, PropSlot.Shoulder),
            Prop.Basket => (PropKind.Basket, PropSlot.Arms),
            _ => (PropKind.None, PropSlot.Hand),
        };
    }
}
