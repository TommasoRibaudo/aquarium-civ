// #352: the harness itself. If these fail, no AnimBench sheet or principle result can be trusted.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimBench;
using AquariumCiv.Life;
using AquariumCiv.View;
using NUnit.Framework;
using UnityEngine;

namespace AnimBenchTests
{
    static class Near
    {
        public static void V(Vector3 want, Vector3 got, float tol = 1e-4f, string what = "")
        {
            Assert.That((want - got).magnitude, Is.LessThan(tol), $"{what}: want {want}, got {got}");
        }
    }

    public class ShimTests
    {
        [Test]
        public void Euler_MatchesUnityConventions()
        {
            Near.V(new Vector3(0, -1, 0), Quaternion.Euler(90, 0, 0) * Vector3.forward, what: "pitch 90 looks down");
            Near.V(new Vector3(0, 0, 1), Quaternion.Euler(90, 0, 0) * Vector3.up);
            Near.V(new Vector3(1, 0, 0), Quaternion.Euler(0, 90, 0) * Vector3.forward, what: "yaw 90 turns forward to +x");
            Near.V(new Vector3(0, 1, 0), Quaternion.Euler(0, 0, 90) * Vector3.right, what: "roll 90 turns right to up");
        }

        [Test]
        public void Euler_AppliesZThenXThenY()
        {
            var v = new Vector3(0.3f, -0.7f, 0.2f);
            var a = Quaternion.Euler(30, 40, 50) * v;
            var b = Quaternion.Euler(0, 40, 0) * (Quaternion.Euler(30, 0, 0) * (Quaternion.Euler(0, 0, 50) * v));
            Near.V(b, a);
            Near.V(v, Quaternion.Inverse(Quaternion.Euler(30, 40, 50)) * a, what: "inverse undoes it");
        }

        [Test]
        public void Mathf_MatchesUnity()
        {
            Assert.That(Mathf.SmoothStep(0f, 1f, 0.5f), Is.EqualTo(0.5f).Within(1e-6));
            Assert.That(Mathf.SmoothStep(10f, 20f, 0.25f), Is.EqualTo(10f + 10f * 0.15625f).Within(1e-5));
            Assert.That(Mathf.SmoothStep(0f, 1f, 2f), Is.EqualTo(1f));
            Assert.That(Mathf.DeltaAngle(350f, 10f), Is.EqualTo(20f).Within(1e-4));
            Assert.That(Mathf.LerpAngle(350f, 10f, 0.5f), Is.EqualTo(360f).Within(1e-4));
            Assert.That(Mathf.Repeat(-1f, 3f), Is.EqualTo(2f).Within(1e-6));
            Assert.That(Mathf.Lerp(0f, 10f, 1.5f), Is.EqualTo(10f), "Lerp clamps");
            Assert.That(Mathf.FloorToInt(-0.5f), Is.EqualTo(-1));
        }
    }

    public class ClipTests
    {
        const string Minimal = @"{ ""name"": ""t"", ""duration"": 2, ""fps"": 10, ""dolls"": [ { ""id"": ""a"", ""activity"": ""Chop"", ""prop"": ""Axe"", ""pos"": [0.1, 0.2] } ] }";

        [Test]
        public void Parse_MinimalClip()
        {
            var c = Clip.Parse(Minimal);
            Assert.That(c.Name, Is.EqualTo("t"));
            Assert.That(c.FrameCount, Is.EqualTo(21));
            var d = c.Dolls.Single();
            Assert.That(d.Segments.Count, Is.EqualTo(1));
            var s = d.Segments[0];
            Assert.That(s.Style, Is.EqualTo(DollStyle.Chop), "activity -> DollMotion.StyleOf");
            Assert.That(s.Prop, Is.EqualTo(PropKind.Axe));
            Assert.That(s.From.x, Is.EqualTo(0.1f).Within(1e-6));
            Assert.That(s.From.y, Is.EqualTo(0.2f).Within(1e-6));
            Assert.That(d.Phase, Is.EqualTo(Bench.Hash01(d.Seed, 5) * 37f).Within(1e-5), "PeopleView's phase: Hash01(key, 5) * 37");
        }

        [Test]
        public void Ring_IsPeopleViewSlotSpot()
        {
            var c = Clip.Parse(@"{ ""dolls"": [ { ""id"": ""k"", ""kid"": true, ""activity"": ""Play"", ""ring"": { ""center"": [1, 2], ""r"": 0.3, ""slot"": 2, ""count"": 4, ""h"": 30 } } ] }");
            var s = c.Dolls[0].Segments[0];
            float ang = 30f * Mathf.Deg2Rad + 2 * Mathf.PI * 2f / 8f; // max(count, 8) slots round the ring
            Assert.That(s.From.x, Is.EqualTo(1f + Mathf.Cos(ang) * 0.3f).Within(1e-4));
            Assert.That(s.From.y, Is.EqualTo(2f + Mathf.Sin(ang) * 0.3f).Within(1e-4));
            Assert.That(s.Face.Kind, Is.EqualTo(FaceKind.Point), "faces the middle");
            Assert.That(c.Dolls[0].Scale, Is.EqualTo(0.68f));
        }

        [Test]
        public void Timeline_HoursAndSequence()
        {
            var c = Clip.Parse(@"{ ""secondsPerViewHour"": 16, ""dolls"": [ { ""id"": ""h"", ""timeline"": [
                { ""activity"": ""Walk"", ""from"": [0, 0], ""to"": [1, 0], ""dur"": 2 },
                { ""activity"": ""Stalk"", ""hunt"": ""Strike"", ""hours"": 0.06 },
                { ""activity"": ""Stand"" } ] } ] }".Replace("\"Stand\"", "\"Watch\""));
            var segs = c.Dolls[0].Segments;
            Assert.That(segs[1].T0, Is.EqualTo(2f));
            Assert.That(segs[1].T1, Is.EqualTo(2f + 0.96f).Within(1e-4), "0.06 view hours at 16 s/h");
            Assert.That(segs[1].From.x, Is.EqualTo(1f), "starts where the walk ended");
            Assert.That(segs[1].Hunt, Is.EqualTo(HuntPhase.Strike));
            Assert.That(segs[2].T0, Is.EqualTo(segs[1].T1).Within(1e-5));
        }

        [Test]
        public void BadInput_SaysWhatIsWrong()
        {
            var e = Assert.Throws<ClipException>(() => Clip.Parse(@"{ ""dolls"": [ { ""id"": ""x"", ""style"": ""Moonwalk"" } ] }"));
            StringAssert.Contains("Moonwalk", e.Message);
            StringAssert.Contains("doll 'x'", e.Message);
            Assert.Throws<ClipException>(() => Clip.Parse(@"{ ""dolls"": [ { ""id"": ""x"" }, { ""id"": ""x"" } ] }"), "duplicate ids");
            Assert.Throws<ClipException>(() => Clip.Parse(@"{ ""dolls"": [] }"), "nothing in it");
        }
    }

    public class SimulatorTests
    {
        static Track Run(string json) => new Simulator(Clip.Parse(json)).Run();

        [Test]
        public void SameClip_SameTrack()
        {
            const string j = @"{ ""duration"": 3, ""dolls"": [ { ""id"": ""a"", ""activity"": ""Chop"", ""prop"": ""Axe"" }, { ""id"": ""b"", ""kid"": true, ""activity"": ""Play"", ""pos"": [0.4, 0] } ] }";
            var a = Run(j); var b = Run(j);
            for (int f = 0; f < a.Count; f++)
                for (int d = 0; d < 2; d++)
                {
                    Assert.That(a.Dolls[d].Frames[f].Pose.armR, Is.EqualTo(b.Dolls[d].Frames[f].Pose.armR));
                    Assert.That(a.Dolls[d].Frames[f].Tip, Is.EqualTo(b.Dolls[d].Frames[f].Tip));
                }
        }

        [Test]
        public void Pose_IsDollMotionAtThePoseClock()
        {
            var t = Run(@"{ ""duration"": 1, ""clock"": 5, ""dolls"": [ { ""id"": ""a"", ""phase"": 2.5, ""activity"": ""Chop"", ""prop"": ""Axe"" } ] }");
            int f = 12;
            var want = DollMotion.Evaluate(DollStyle.Chop, 5f + t.Times[f] + 2.5f, 1f, 0, ActivityInfo.LoopSteps(ActivityKind.Chop), AquariumCiv.View.PropSlot.Hand, true, 0f, t.Times[f] / 1e3f, t.Times[f], 0, float.NaN);
            var got = t.Dolls[0].Frames[f].Pose;
            Assert.That(got.armR.x, Is.EqualTo(want.armR.x).Within(1e-3));
            Assert.That(got.lean, Is.EqualTo(want.lean).Within(1e-3));
            Assert.That(got.hit, Is.EqualTo(want.hit));
        }

        [Test]
        public void Chop_HitsOncePerStroke()
        {
            var t = Run(@"{ ""duration"": 6, ""fps"": 60, ""dolls"": [ { ""id"": ""a"", ""activity"": ""Chop"", ""prop"": ""Axe"" } ] }");
            var hits = t.Hits.Where(h => h.Doll == "a").ToList();
            Assert.That(hits.Count, Is.InRange(4, 6), "a stroke every 1.15 s");
            for (int i = 1; i < hits.Count; i++) Assert.That(hits[i].T0 - hits[i - 1].T0, Is.EqualTo(1.15f).Within(0.04f));
        }

        [Test]
        public void Walk_UsesTheWalkAndFacesTheWayItGoes()
        {
            var t = Run(@"{ ""duration"": 3, ""dolls"": [ { ""id"": ""a"", ""timeline"": [ { ""activity"": ""Walk"", ""from"": [0, 0], ""to"": [0.9, 0], ""dur"": 3 } ] } ] }");
            var fr = t.Dolls[0].Frames[45];
            Assert.That(fr.Style, Is.EqualTo(DollStyle.Walk));
            Assert.That(fr.Walking, Is.True);
            // the front is local -z: walking +x means yaw 270 (PeopleView.Yaw)
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(fr.BodyYaw, 270f)), Is.LessThan(5f));
            Assert.That(fr.Pos.x, Is.EqualTo(0.45f).Within(0.01f));
        }

        [Test]
        public void SlowCreep_DrawsAsAWalk_LikePeopleView()
        {
            // 0.08 u/s is above PeopleView's WalkStarts (0.05): the stalk's crouch is replaced by the walk.
            var t = Run(@"{ ""duration"": 3, ""dolls"": [ { ""id"": ""a"", ""timeline"": [ { ""activity"": ""Stalk"", ""from"": [0, 0], ""to"": [0.24, 0], ""dur"": 3 } ] } ] }");
            Assert.That(t.Dolls[0].Frames[60].Style, Is.EqualTo(DollStyle.Walk));
            var still = Run(@"{ ""duration"": 3, ""dolls"": [ { ""id"": ""a"", ""activity"": ""Stalk"" } ] }");
            Assert.That(still.Dolls[0].Frames[60].Style, Is.EqualTo(DollStyle.Crouch));
        }

        [Test]
        public void Hop_CirclesTheSpot_AtTheKidsScale()
        {
            var t = Run(@"{ ""duration"": 3, ""dolls"": [ { ""id"": ""k"", ""kid"": true, ""activity"": ""Play"", ""pos"": [0.5, 0.5] } ] }");
            foreach (int f in new[] { 0, 20, 45, 70 })
            {
                var o = t.Dolls[0].Frames[f].Body.O;
                float r = new Vector2(o.x - 0.5f, o.z - 0.5f).magnitude;
                Assert.That(r, Is.EqualTo(0.1f * 0.68f).Within(0.002f), "DollMotion's 0.1 circle, scaled by the kid");
            }
        }

        [Test]
        public void Doll_IsBuiltLikeThePegDoll()
        {
            var t = Run(@"{ ""duration"": 0.1, ""dolls"": [ { ""id"": ""a"", ""style"": ""Freeze"", ""facing"": 180 } ] }");
            var fr = t.Dolls[0].Frames[0];
            // Freeze: lean -4 (tilt Euler(4, 0, 0)), arms (10, 0, +-45); facing 180, so the doll's right (+x) is world -x
            var body = Quaternion.Euler(0, 180, 0) * Quaternion.Euler(4, 0, 0);
            Near.V(body * new Vector3(0, Bench.HeadY, 0), fr.Head, 1e-4f, "head");
            var hand = body * (new Vector3(Bench.ShoulderX, Bench.BodyH * 0.92f, 0) + Quaternion.Euler(10, 0, 45) * new Vector3(0, -Bench.ArmLen, 0));
            Near.V(hand, fr.HandR, 1e-4f, "right hand");
            Assert.That(fr.HandR.x, Is.LessThan(0f), "the right hand is on world -x when facing +z");
        }

        [Test]
        public void ExtendedSpear_PointsAlongTheArm()
        {
            var t = Run(@"{ ""duration"": 1.2, ""warmup"": 0, ""dolls"": [ { ""id"": ""h"", ""prop"": ""Spear"", ""timeline"": [ { ""activity"": ""Stalk"", ""hunt"": ""Strike"", ""dur"": 3 } ] } ] }");
            var fr = t.Dolls[0].Frames[t.FrameAt(1.0f)];   // #362: the throw's follow-through (the spear is out along the arm; at 0.4 s it is still in the wind-up)
            Assert.That(fr.Pose.extend, Is.True, "the arm is out after the throw");
            Assert.That(fr.Prop, Is.EqualTo(PropKind.None), "the spear has left the hand (#362)");
            var held = t.Dolls[0].Frames[t.FrameAt(0.7f)];
            Assert.That(held.Prop, Is.EqualTo(PropKind.Spear), "and is in it before the release");
            var arm = (held.HandR - held.ArmR.O).normalized;
            Assert.That(held.HandR.y, Is.GreaterThan(held.Head.y), "the spear hand is up over the head in the wind-up");
        }

        [Test]
        public void HuntedRabbit_IgnoresTheHunterUntilTheStrikeIsOver()
        {
            var t = Run(@"{ ""duration"": 4, ""objects"": [ { ""id"": ""r"", ""kind"": ""rabbit"", ""pos"": [0.2, 0], ""hunted"": { ""by"": ""h"", ""until"": 2 }, ""sitUp"": [[1, 2]] } ],
                            ""dolls"": [ { ""id"": ""h"", ""activity"": ""Stalk"", ""pos"": [0, 0] } ] }");
            var r = t.Objects[0];
            Assert.That(r.Frames[t.FrameAt(1.9f)].Act, Is.EqualTo("SitUp"), "sits up when the plan says so (#362), the hunter near or not");
            Assert.That((r.Frames[t.FrameAt(1.9f)].Pos - new Vector3(0.2f, 0, 0)).magnitude, Is.LessThan(1e-3f), "but stays");
            Assert.That((r.Frames[t.FrameAt(3.5f)].Pos - new Vector3(0.2f, 0, 0)).magnitude, Is.GreaterThan(0.1f), "after: RabbitHerd.Scare bolts");
        }

        [Test]
        public void Arc_LeavesTheHandAndLands()
        {
            var t = Run(@"{ ""duration"": 2, ""objects"": [ { ""id"": ""b"", ""kind"": ""ball"", ""arc"": { ""from"": ""k.hand"", ""to"": [1, 0, 0], ""t0"": 0.5, ""t1"": 1.5, ""height"": 0.2 } } ],
                            ""dolls"": [ { ""id"": ""k"", ""kid"": true, ""style"": ""Stand"" } ] }");
            var b = t.Objects[0];
            int f0 = t.FrameAt(0.5f), mid = t.FrameAt(1.0f), f1 = t.FrameAt(1.5f);
            Near.V(t.Dolls[0].Frames[f0].HandR, b.Frames[f0].Pos, 1e-3f, "starts at the hand");
            Near.V(new Vector3(1, 0, 0), b.Frames[f1].Pos, 1e-3f, "lands");
            Assert.That(b.Frames[mid].Pos.y, Is.GreaterThan(0.2f), "arcs over");
            Assert.That(b.Frames[f1].Act, Is.EqualTo("landed"));
        }

        [Test]
        public void Cli_WritesTrackRenderAndReport()
        {
            string dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cli-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            string clip = Path.Combine(dir, "c.json");
            File.WriteAllText(clip, @"{ ""name"": ""c"", ""duration"": 1, ""dolls"": [ { ""id"": ""a"", ""activity"": ""Hoe"", ""prop"": ""Hoe"" } ] }");
            try
            {
                Assert.That(AnimBench.Program.Main(new[] { clip, "--out", dir }), Is.EqualTo(0));
                var track = File.ReadAllText(Path.Combine(dir, "track.json"));
                StringAssert.Contains("\"format\":\"animbench-track/1\"", track);
                StringAssert.Contains("\"armR\"", track);
                Assert.That(File.Exists(Path.Combine(dir, "render.json")));
                StringAssert.Contains("endBeat", File.ReadAllText(Path.Combine(dir, "principles.txt")));
                Assert.That(AnimBench.Program.Main(new[] { Path.Combine(dir, "missing.json") }), Is.EqualTo(2));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
