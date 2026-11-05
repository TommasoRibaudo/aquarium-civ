using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>#316: the voices by distance: rare and muffled far, more in the middle, exchanges up close; varied and never the same clip twice.</summary>
    public class VoicePlanTests
    {
        static VoiceMember M(int id, int voice = 0, bool kid = false) => new VoiceMember { id = id, voice = voice, kid = kid };

        static VoiceGroup G(int id, int n, bool fire = false, bool work = false)
        {
            var g = new VoiceGroup { id = id, nearFire = fire, working = work };
            for (int i = 0; i < n; i++) g.members.Add(M(id * 10 + i + 1, i % 4));
            return g;
        }

        static List<VoiceMember> Crowd(int n) { var l = new List<VoiceMember>(); for (int i = 1; i <= n; i++) l.Add(M(i, i % 4, i % 5 == 0)); return l; }

        [Test]
        public void TheBandsFollowTheZoomAndTheVolumeRisesWithIt()
        {
            var p = new VoicePlan();
            Assert.AreEqual(VoiceBand.Far, p.BandOf(0f));
            Assert.AreEqual(VoiceBand.Far, p.BandOf(0.2f));
            Assert.AreEqual(VoiceBand.Mid, p.BandOf(0.4f));
            Assert.AreEqual(VoiceBand.Near, p.BandOf(0.7f));
            float last = -1f;
            for (float z = 0f; z <= 1f; z += 0.02f)
            {
                float v = p.VolumeAt(z);
                Assert.GreaterOrEqual(v, last - 1e-5f, $"zoom {z}: never quieter when closer");
                last = v;
            }
            Assert.Less(p.VolumeAt(0.1f), 0.6f);
            Assert.AreEqual(1f, p.VolumeAt(1f), 1e-5);
            Assert.IsTrue(p.Muffled(0.1f)); Assert.IsTrue(p.Muffled(0.3f)); Assert.IsFalse(p.Muffled(0.7f));
        }

        [Test]
        public void FarAwayOnlyARareQuietMuffledShoutCallOrLaugh()
        {
            var p = new VoicePlan(5);
            var crowd = Crowd(8);
            var at = new List<double>(); var clips = new HashSet<int>();
            for (double t = 0; t < 1200; t += 0.25)
                if (p.TickLone(t, 0.1f, 12f, crowd, out var v))
                {
                    at.Add(t); clips.Add(v.clip);
                    Assert.IsTrue(v.muffled, "far voices are muffled");
                    Assert.LessOrEqual(v.volume, p.rules.farVolume + 1e-4f, "and quiet");
                    CollectionAssert.Contains(new[] { ChatterBabble.Shout, ChatterBabble.Call, ChatterBabble.Laugh, ChatterBabble.Giggle }, v.clip, "calls, shouts, laughs: no chatter");
                }
            Assert.That(at.Count, Is.InRange(25, 70), "one every 20 to 40 s over 20 minutes");
            for (int i = 1; i < at.Count; i++) Assert.GreaterOrEqual(at[i] - at[i - 1], p.rules.farGapMin - 0.3, "never closer than the gap");
            Assert.GreaterOrEqual(clips.Count, 3, "varied");
            Assert.AreEqual(0, p.Exchanges + p.Singles, "no group talk from afar");
        }

        [Test]
        public void MidIsBusierThanFarAndNearHasNoLoneVoices()
        {
            int Count(float zoom) { var p = new VoicePlan(6); int n = 0; var c = Crowd(6); for (double t = 0; t < 1200; t += 0.25) if (p.TickLone(t, zoom, 12f, c, out _)) n++; return n; }
            Assert.Greater(Count(0.4f), Count(0.1f) * 1.5);
            Assert.AreEqual(0, Count(0.8f), "up close the groups do the talking");
        }

        [Test]
        public void NightHushesAndNobodyOnViewMeansNothing()
        {
            var p = new VoicePlan(7);
            for (double t = 0; t < 600; t += 0.25) Assert.IsFalse(p.TickLone(t, 0.1f, 23f, Crowd(5), out _), "no shouts at night");
            for (double t = 0; t < 600; t += 0.25) Assert.IsFalse(p.TickLone(t, 0.1f, 12f, new List<VoiceMember>(), out _), "nobody there");
            Assert.IsTrue(p.IsNight(22f)); Assert.IsTrue(p.IsNight(3f)); Assert.IsFalse(p.IsNight(12f));
            var turns = new VoicePlan(8).OnTurn(0, 0.9f, 23f, G(1, 3), 11, 12, ChatterMark.Bang);
            Assert.IsNotEmpty(turns);
            foreach (var t in turns)
            {
                CollectionAssert.Contains(new[] { ChatterBabble.Hum, 2, 0 }, t.clip, "a hush: soft kinds only");
                Assert.LessOrEqual(t.volume, 0.5f + 1e-4f);
            }
        }

        [Test]
        public void UpCloseAGroupHasAnExchangeOfTwoOrThreeTakingTurnsWithPauses()
        {
            int twos = 0, threes = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var p = new VoicePlan(seed);
                var turns = p.OnTurn(100, 0.9f, 12f, G(1, 3), 11, 12, ChatterMark.Heart);
                Assert.That(turns.Count, Is.InRange(2, 3));
                if (turns.Count == 2) twos++; else threes++;
                Assert.AreEqual(0.0, turns[0].delay);
                Assert.AreEqual(11, turns[0].speakerId, "the plan's speaker opens");
                for (int i = 1; i < turns.Count; i++)
                {
                    Assert.AreNotEqual(turns[i - 1].speakerId, turns[i].speakerId, "somebody else answers");
                    Assert.GreaterOrEqual(turns[i].delay - turns[i - 1].delay, p.rules.turnSeconds * 0.8 + p.rules.replyLatencyMin - 1e-6, "a pause between turns");
                    Assert.IsFalse(turns[i].muffled, "close: clear");
                }
            }
            Assert.Greater(twos, 0); Assert.Greater(threes, 0);
            // two people: two turns
            Assert.AreEqual(2, new VoicePlan(1).OnTurn(0, 0.9f, 12f, G(2, 2), 21, 22, ChatterMark.Dots).Count);
        }

        [Test]
        public void AGroupRestsAfterAnExchangeAndOnlyTwoExchangesOverlap()
        {
            var p = new VoicePlan(9);
            var g = G(1, 3);
            Assert.IsNotEmpty(p.OnTurn(0, 0.9f, 12f, g, 11, 12, ChatterMark.Dots));
            Assert.IsEmpty(p.OnTurn(4, 0.9f, 12f, g, 12, 13, ChatterMark.Dots), "still resting");
            Assert.IsEmpty(p.OnTurn(9, 0.9f, 12f, g, 12, 13, ChatterMark.Dots), "still resting");
            Assert.IsNotEmpty(p.OnTurn(60, 0.9f, 12f, g, 12, 13, ChatterMark.Dots), "after the rest, again");
            var q = new VoicePlan(10);
            int started = 0;
            for (int i = 1; i <= 4; i++) if (q.OnTurn(0, 0.9f, 12f, G(i, 2), i * 10 + 1, i * 10 + 2, ChatterMark.Dots).Count > 0) started++;
            Assert.AreEqual(q.rules.maxExchangesAtOnce, started, "never a crowd talking at once");
            Assert.IsNotEmpty(new VoicePlan(11).OnTurn(0, 0.9f, 12f, g, 11, 12, ChatterMark.Dots), "sanity");
        }

        [Test]
        public void MidIsOneRareLineAndFarIsNoGroupTalk()
        {
            var p = new VoicePlan(12);
            var g = G(1, 3);
            Assert.IsEmpty(p.OnTurn(0, 0.4f, 12f, g, 11, 12, ChatterMark.Dots), "the first line waits a while");
            Assert.AreEqual(1, p.OnTurn(30, 0.4f, 12f, g, 11, 12, ChatterMark.Dots).Count, "one line, no exchange");
            Assert.IsEmpty(p.OnTurn(32, 0.4f, 12f, g, 12, 13, ChatterMark.Dots), "then a rest");
            Assert.IsEmpty(new VoicePlan(13).OnTurn(0, 0.1f, 12f, g, 11, 12, ChatterMark.Dots), "far: nothing");
            Assert.IsNotEmpty(new VoicePlan(13).OnTurn(0, 0.1f, 12f, g, 11, 12, ChatterMark.Dots, force: true), "unless the debug cheat forces it");
        }

        [Test]
        public void NoClipRepeatsBackToBackAndTheKindsFitTheMood()
        {
            var p = new VoicePlan(14);
            var pool = new[] { 0, 5, 11, 7 };
            int last = -1;
            for (int i = 0; i < 300; i++)
            {
                int c = p.Pick(pool, 1 + i % 3, (i * 0.37f) % 1f);
                Assert.AreNotEqual(last, c, "no immediate repeat"); last = c;
            }
            var seen = new HashSet<int>();
            for (int i = 0; i < 200; i++) seen.Add(p.Pick(pool, 9, (i * 0.618f) % 1f));
            Assert.AreEqual(pool.Length, seen.Count, "all of them get their turn");

            var fire = G(1, 3, fire: true); var work = G(2, 3, work: true);
            Assert.Contains(ChatterBabble.Laugh, VoicePlan.PoolFor(ChatterMark.Heart, fire, false, false), "laughter at the fire");
            Assert.Contains(ChatterBabble.Giggle, VoicePlan.PoolFor(ChatterMark.Heart, fire, true, false), "a child giggles");
            Assert.Contains(ChatterBabble.Call, VoicePlan.PoolFor(ChatterMark.Dots, work, false, false), "a call out at work");
            Assert.IsFalse(VoicePlan.PoolFor(ChatterMark.Dots, fire, false, false).Contains(ChatterBabble.Call), "not by the fire");
            Assert.Contains(4, VoicePlan.PoolFor(ChatterMark.Question, fire, false, false), "a question rises");
            Assert.IsFalse(VoicePlan.PoolFor(ChatterMark.Bang, fire, false, false, reply: true).Contains(ChatterBabble.Call), "a reply is gentler");
            CollectionAssert.AreEquivalent(new[] { ChatterBabble.Hum, 2, 0 }, VoicePlan.PoolFor(ChatterMark.Bang, work, false, true), "a hush at night");
        }

        [Test]
        public void EachPersonHasTheirOwnSteadyPitchAndKidsSitHigher()
        {
            var p = new VoicePlan(15);
            Assert.AreEqual(p.SemitonesOf(7, false), p.SemitonesOf(7, false), "steady");
            var pitches = Enumerable.Range(1, 30).Select(i => p.SemitonesOf(i, false)).ToList();
            Assert.Greater(pitches.Distinct().Count(), 25, "people differ");
            Assert.That(pitches.All(x => Math.Abs(x) <= p.rules.personSemitones + 1e-4f));
            Assert.AreEqual(p.rules.kidSemitones, p.SemitonesOf(3, true) - p.SemitonesOf(3, false), 1e-5);
        }

        [Test]
        public void SameSeedSameVoicesAndTheMuffledTakeIsDuller()
        {
            List<int> Run(int seed)
            {
                var p = new VoicePlan(seed); var l = new List<int>(); var c = Crowd(6);
                for (double t = 0; t < 600; t += 0.25) if (p.TickLone(t, 0.3f, 12f, c, out var v)) { l.Add(v.clip); l.Add(v.speakerId); }
                return l;
            }
            CollectionAssert.AreEqual(Run(3), Run(3));
            for (int clip = 0; clip < ChatterBabble.Clips; clip++)
            {
                var clear = ChatterBabble.Generate(1, clip, 144);
                var dull = ChatterBabble.Muffle(clear);
                Assert.AreEqual(clear.Length, dull.Length);
                Assert.AreEqual(ChatterBabble.Peak, dull.Max(Math.Abs), 1e-4);
                double Rough(float[] d) { double n = 0, e = 0; for (int i = 1; i < d.Length; i++) { n += Math.Abs(d[i] - d[i - 1]); e += d[i] * d[i]; } return n / Math.Sqrt(e / d.Length); }
                Assert.Less(Rough(dull), Rough(clear), $"clip {clip}: less fast wiggle once muffled");
            }
        }

        [Test]
        public void TheNewClipKindsAreDistinctAndTheMarksMatch()
        {
            for (int a = 6; a < ChatterBabble.Clips; a++)
                for (int b = a + 1; b < ChatterBabble.Clips; b++)
                    CollectionAssert.AreNotEqual(ChatterBabble.Generate(0, a, 144), ChatterBabble.Generate(0, b, 144), $"{a} vs {b}");
            Assert.AreEqual(ChatterMark.Bang, VoicePlan.MarkForClip(ChatterBabble.Shout));
            Assert.AreEqual(ChatterMark.Note, VoicePlan.MarkForClip(ChatterBabble.Laugh));
            Assert.AreEqual(ChatterMark.Dots, VoicePlan.MarkForClip(ChatterBabble.Call));
        }
    }
}
