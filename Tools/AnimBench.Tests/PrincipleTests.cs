// #352: research 17 §4's principles. Part 1 checks the checks (clips built to pass and to fail each one). Part 2 runs every
// clip in Tools/AnimBench/clips: a principle the clip lists in "knownGaps" is reported as Skipped with "KNOWN GAP" (today's
// animation doesn't meet it yet); any other principle must pass. When a gap closes, the test fails and asks you to take it off
// the clip's knownGaps, so the list stays true.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimBench;
using NUnit.Framework;

namespace AnimBenchTests
{
    public class PrincipleCheckTests
    {
        static Track Run(string json) => new Simulator(Clip.Parse(json)).Run();
        static PrincipleResult Check(string json, string id) => Principles.Check(Run(json), id);

        // A hammer at a fixed spot hits every 0.75 s: put the target where the hammer head lands, or far away.
        const string HammerAt = @"{{ ""duration"": 3, ""objects"": [ {{ ""id"": ""stone"", ""kind"": ""thing"", ""pos"": [{0}, 0.0, {1}], ""radius"": 0.03 }} ],
            ""dolls"": [ {{ ""id"": ""w"", ""activity"": ""Hammer"", ""prop"": ""Hammer"", ""facing"": 180, ""target"": ""stone"" }} ] }}";

        [Test]
        public void HitNear_PassesWhenTheToolReachesTheTarget_FailsWhenNot()
        {
            // find where the hammer's head is on the hit frames, then put the stone there
            var probe = Run(string.Format(HammerAt, "5", "5"));
            var h = probe.Hits.First();
            var tip = probe.Dolls[0].Frames[h.F0].Tip;
            var near = Check(string.Format(HammerAt, tip.x.ToString(System.Globalization.CultureInfo.InvariantCulture), tip.z.ToString(System.Globalization.CultureInfo.InvariantCulture)), Principles.HitNear);
            Assert.That(near.Pass, Is.True, string.Join("; ", near.Findings));
            var far = Check(string.Format(HammerAt, "1.5", "1.5"), Principles.HitNear);
            Assert.That(far.Pass, Is.False);
            StringAssert.Contains("doll-width", far.Findings[0]);
        }

        [Test]
        public void HitNear_FailsAHitWithNoTarget()
        {
            var r = Check(@"{ ""duration"": 2, ""dolls"": [ { ""id"": ""w"", ""activity"": ""Chop"", ""prop"": ""Axe"" } ] }", Principles.HitNear);
            Assert.That(r.Applies && !r.Pass, Is.True);
            StringAssert.Contains("no target", r.Findings[0]);
            Assert.That(Check(@"{ ""duration"": 2, ""dolls"": [ { ""id"": ""w"", ""style"": ""Stand"" } ] }", Principles.HitNear).Applies, Is.False, "no hits: n/a");
        }

        [Test]
        public void React_NeedsAnAnswerWithinTheWindow()
        {
            // a bush that drops a berry on its own clock never answers the hammer in time; a rabbit by the hunter does after the scare's freeze
            var late = Check(@"{ ""duration"": 3, ""objects"": [ { ""id"": ""b"", ""kind"": ""bush"", ""pos"": [0, 0, -0.2], ""berries"": 5 } ],
                ""dolls"": [ { ""id"": ""w"", ""activity"": ""Hammer"", ""prop"": ""Hammer"", ""target"": ""b"" } ] }", Principles.React);
            Assert.That(late.Pass, Is.False);
            StringAssert.Contains("never answers", late.Findings[0]);
        }

        [Test]
        public void React_PassesAQuickAnswer_AndFlagsOneBeforeTheHit()
        {
            // the target is another doll who starts walking 0.1 s after the first hit (a flinch away): quick enough
            var probe = Run(string.Format(HammerAt, "5", "5"));
            float hit = probe.Hits.First().T0;
            string Clip(float startsAt) => @"{ ""duration"": 3, ""dolls"": [
                { ""id"": ""w"", ""activity"": ""Hammer"", ""prop"": ""Hammer"", ""target"": ""v"" },
                { ""id"": ""v"", ""timeline"": [ { ""style"": ""Stand"", ""pos"": [0, -0.3], ""until"": " + startsAt.ToString(System.Globalization.CultureInfo.InvariantCulture) + @" },
                                                { ""style"": ""Flinch"", ""dur"": 9 } ] } ] }";
            var quick = Check(Clip(hit + 0.1f), Principles.React);
            Assert.That(quick.Findings.First(), Does.Contain("after 0.1"), string.Join("; ", quick.Findings));
            var early = Check(Clip(hit - 0.1f), Principles.React);
            Assert.That(early.Pass, Is.False);
            StringAssert.Contains("before", early.Findings[0]);
        }

        [Test]
        public void EndBeat_LoopMustEndInABeatSeenOnce()
        {
            string Clip(string after) => @"{ ""duration"": 4, ""dolls"": [ { ""id"": ""p"", ""timeline"": [
                { ""activity"": ""Gather"", ""prop"": ""Basket"", ""dur"": 2 }, { " + after + @", ""dur"": 3 } ] } ] }";
            Assert.That(Check(Clip(@"""style"": ""HoldUp"""), Principles.EndBeat).Pass, Is.True, "held up: an end beat");
            Assert.That(Check(Clip(@"""style"": ""Walk"", ""endBeat"": true"), Principles.EndBeat).Pass, Is.True, "the clip can mark its own end beat");
            Assert.That(Check(Clip(@"""style"": ""Stand"""), Principles.EndBeat).Pass, Is.False, "just stopping is not an end beat");
            var forever = Check(@"{ ""duration"": 4, ""dolls"": [ { ""id"": ""p"", ""activity"": ""Gather"" } ] }", Principles.EndBeat);
            Assert.That(forever.Pass, Is.False);
            StringAssert.Contains("to the end of the clip", forever.Findings[0]);
        }

        [Test]
        public void Neighbours_ThreeCloseInOneMoveFail_SpreadOrMixedPass()
        {
            string Kids(float gap, string third) => @"{ ""duration"": 2, ""dolls"": [
                { ""id"": ""a"", ""kid"": true, ""activity"": ""Play"", ""pos"": [0, 0] },
                { ""id"": ""b"", ""kid"": true, ""activity"": ""Play"", ""pos"": [" + gap.ToString(System.Globalization.CultureInfo.InvariantCulture) + @", 0] },
                { ""id"": ""c"", ""kid"": true, ""activity"": """ + third + @""", ""pos"": [" + (2 * gap).ToString(System.Globalization.CultureInfo.InvariantCulture) + @", 0] } ] }";
            Assert.That(Check(Kids(0.3f, "Play"), Principles.Neighbours).Pass, Is.False, "a chain of three in a ring of 0.3");
            Assert.That(Check(Kids(0.9f, "Play"), Principles.Neighbours).Pass, Is.True, "too far apart to be neighbours");
            Assert.That(Check(Kids(0.3f, "Chop"), Principles.Neighbours).Pass, Is.True, "two in one move is fine");
            Assert.That(Check(Kids(0.3f, "SitOut"), Principles.Neighbours).Pass, Is.True, "rest doesn't count");
        }

        [Test]
        public void Arcs_ExpectedThrowMustLeaveTheHandAndLand()
        {
            const string Scene = @"{{ ""duration"": 2, ""expect"": {{ ""arcs"": [ {{ ""kind"": ""ball"", ""by"": ""k"", ""target"": ""m"" }} ] }},
                ""objects"": [ {0} ],
                ""dolls"": [ {{ ""id"": ""k"", ""kid"": true, ""style"": ""Stand"" }}, {{ ""id"": ""m"", ""kid"": true, ""style"": ""Stand"", ""pos"": [0.6, 0] }} ] }}";
            var none = Principles.Check(Run(string.Format(Scene, @"{ ""id"": ""rock"", ""kind"": ""thing"", ""pos"": [3, 0] }")), Principles.Arcs);
            Assert.That(none.Pass, Is.False);
            StringAssert.Contains("never leaves the hand", none.Findings[0]);
            var good = Principles.Check(Run(string.Format(Scene, @"{ ""id"": ""ball"", ""kind"": ""ball"", ""arc"": { ""from"": ""k.hand"", ""to"": ""m"", ""t0"": 0.5, ""t1"": 1.2 } }")), Principles.Arcs);
            Assert.That(good.Pass, Is.True, string.Join("; ", good.Findings));
            var wide = Principles.Check(Run(string.Format(Scene, @"{ ""id"": ""ball"", ""kind"": ""ball"", ""arc"": { ""from"": ""k.hand"", ""to"": [0.6, 0, 0.7], ""t0"": 0.5, ""t1"": 1.2 } }")), Principles.Arcs);
            Assert.That(wide.Pass, Is.False, "lands a long way off its target");
        }
    }

    /// <summary>Every committed clip against every principle. Known gaps are Skipped with the finding, so they stay visible.</summary>
    [Category("Principles")]
    public class ClipPrincipleTests
    {
        public static string ClipsDir()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Tools", "AnimBench", "clips"))) d = d.Parent;
            if (d == null) throw new DirectoryNotFoundException("Tools/AnimBench/clips not found above " + AppContext.BaseDirectory);
            return Path.Combine(d.FullName, "Tools", "AnimBench", "clips");
        }

        static readonly Dictionary<string, Track> Cache = new Dictionary<string, Track>();

        static Track TrackOf(string clip)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(clip, out var t)) Cache[clip] = t = new Simulator(Clip.Load(Path.Combine(ClipsDir(), clip + ".json"))).Run();
                return t;
            }
        }

        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (var f in Directory.GetFiles(ClipsDir(), "*.json").OrderBy(x => x))
                foreach (var p in Principles.All)
                    yield return new TestCaseData(Path.GetFileNameWithoutExtension(f), p).SetName($"{Path.GetFileNameWithoutExtension(f)}.{p}");
        }

        [TestCaseSource(nameof(Cases))]
        public void Clip_MeetsPrinciple(string clip, string principle)
        {
            var t = TrackOf(clip);
            var r = Principles.CheckAll(t).First(x => x.Id == principle);
            string detail = string.Join(" | ", r.Findings);
            if (!r.Applies) Assert.Pass("n/a: " + detail);
            if (r.KnownGap)
            {
                if (!r.Pass) Assert.Ignore($"KNOWN GAP ({clip}, research 17 §4): {detail}");
                Assert.Fail($"{clip} now meets '{principle}' ({detail}): take it off knownGaps in clips/{clip}.json");
            }
            Assert.That(r.Pass, Is.True, $"{clip} breaks '{principle}': {detail}");
        }

        [Test]
        public void EveryClip_RunsAndListsOnlyRealPrinciples()
        {
            foreach (var f in Directory.GetFiles(ClipsDir(), "*.json"))
            {
                var c = Clip.Load(f);
                foreach (var g in c.KnownGaps) CollectionAssert.Contains(Principles.All, g, $"{Path.GetFileName(f)}: unknown principle '{g}' in knownGaps");
                Assert.That(TrackOf(Path.GetFileNameWithoutExtension(f)).Count, Is.EqualTo(c.FrameCount));
            }
        }
    }
}
