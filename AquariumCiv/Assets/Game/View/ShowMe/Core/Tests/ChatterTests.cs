using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>#144/#161/#194 phase 1: the wordless chatter (turn-taking, caps, gating, mood to mark), the babble samples,
    /// the knock's `!` timing and the relationship markers' layout. Pure: no Editor.</summary>
    public class ChatterTests
    {
        static ChatterPerson P(int id, bool asleep = false, Rapport r = Rapport.Idle, params int[] toward)
        {
            var p = new ChatterPerson { id = id, asleep = asleep, voice = id % 4 };
            foreach (var t in toward) p.toward[t] = r;
            return p;
        }

        static ChatterGroup G(int id, params ChatterPerson[] m) => new ChatterGroup { id = id, members = m.ToList() };

        static List<ChatterGroup> Camp(int groups = 1, int size = 3)
        {
            var l = new List<ChatterGroup>();
            for (int g = 0; g < groups; g++) l.Add(G(g, Enumerable.Range(0, size).Select(i => P(g * 10 + i)).ToArray()));
            return l;
        }

        /// <summary>Runs the plan in 0.25 s steps and returns every mark shown.</summary>
        static List<ChatterShow> Run(ChatterPlan plan, IList<ChatterGroup> g, double secs, float zoom = 0.6f, bool real = false, bool quiet = false, ChatterStyle st = ChatterStyle.MarksBabble, int seed = 5, double from = 0)
        {
            var all = new List<ChatterShow>();
            for (double t = from; t < from + secs; t += 0.25) all.AddRange(plan.Tick(t, g, seed, zoom, real, quiet, st).show);
            return all;
        }

        [Test]
        public void ALabelFarFromItsPersonIsTheOneThatGetsALine()
        {
            var label = new LabelBox(0f, 0f, 200f, 60f);
            Assert.AreEqual(0f, MarkerLayout.GapTo(label, 50f, 10f), 1e-3f, "on it");
            Assert.AreEqual(50f, MarkerLayout.GapTo(label, 0f, 80f), 1e-3f, "from its edge, not its middle");
            Assert.Greater(MarkerLayout.GapTo(label, 0f, 150f), MarkerLayout.LeaderGap, "Gwen, 150 px off");
            Assert.Less(MarkerLayout.GapTo(label, 90f, 110f), MarkerLayout.LeaderGap, "a label at its usual height");
        }

        [Test]
        public void OneSpeakerPerGroupEveryThreeToSixSeconds()
        {
            var plan = new ChatterPlan();
            var shows = Run(plan, Camp(), 120);
            Assert.GreaterOrEqual(shows.Count, 120 / 6 - 3, "a chat, not silence");
            Assert.LessOrEqual(shows.Count, 120 / 3 + 1);
            for (int i = 1; i < shows.Count; i++)
                Assert.GreaterOrEqual(shows[i].startAt - shows[i - 1].startAt, 3f - 1e-6, "never faster than every 3 s");
            for (int i = 1; i < shows.Count; i++)
                Assert.LessOrEqual(shows[i].startAt - shows[i - 1].startAt, 6.25f + 1e-6, "and every 6 s at most (a step of 0.25)");
            Assert.AreEqual(shows.Select(s => s.groupId).Distinct().Count(), 1);
            foreach (var s in shows) Assert.AreNotEqual(s.speakerId, s.listenerId);
        }

        [Test]
        public void NeverTheSameSpeakerTwiceInARowWhenThereAreThree()
        {
            var shows = Run(new ChatterPlan(), Camp(1, 3), 200);
            for (int i = 1; i < shows.Count; i++) Assert.AreNotEqual(shows[i - 1].speakerId, shows[i].speakerId);
            Assert.Greater(shows.Select(s => s.speakerId).Distinct().Count(), 1, "everyone gets a word");
        }

        [Test]
        public void AtMostFourAreAliveInTheBox()
        {
            var plan = new ChatterPlan();
            var g = Camp(8, 3);
            int maxLive = 0;
            for (double t = 0; t < 120; t += 0.25)
            {
                plan.Tick(t, g, 9, 0.6f, false);
                maxLive = Math.Max(maxLive, plan.Live.Count);
            }
            Assert.AreEqual(4, maxLive, "busy, and capped");
        }

        [Test]
        public void ZoomedOutShowsAtMostOneMark()
        {
            var plan = new ChatterPlan();
            var g = Camp(6, 3);
            int maxLive = 0;
            for (double t = 0; t < 60; t += 0.25) { plan.Tick(t, g, 9, 0.05f, false); maxLive = Math.Max(maxLive, plan.Live.Count); }
            Assert.AreEqual(1, maxLive);
            // zooming out with several up lets the extras go
            var plan2 = new ChatterPlan();
            for (double t = 0; t < 20; t += 0.25) plan2.Tick(t, g, 9, 0.6f, false);
            Assert.Greater(plan2.Live.Count, 1);
            var tick = plan2.Tick(20, g, 9, 0.05f, false);
            Assert.AreEqual(1, plan2.Live.Count);
            Assert.Greater(tick.fade.Count, 0);
        }

        [Test]
        public void ARealLineWinsAndTheChatterFadesAndWaits()
        {
            var plan = new ChatterPlan();
            var g = Camp(3, 3);
            Run(plan, g, 15);
            Assert.Greater(plan.Live.Count, 0);
            var live = plan.Live.ToList();
            var tick = plan.Tick(15.25, g, 5, 0.6f, realLineUp: true);
            Assert.AreEqual(0, tick.show.Count);
            CollectionAssert.AreEquivalent(live, tick.fade, "every live mark gives way");
            Assert.AreEqual(0, plan.Live.Count);
            Assert.AreEqual(0, Run(plan, g, 10, real: true, from: 15.5).Count, "nothing new while the line is up");
            // afterwards: no burst, a staggered restart
            var after = Run(plan, g, 1.0, from: 30);
            Assert.LessOrEqual(after.Count, 3);
        }

        [Test]
        public void QuietCasesAndOffSayNothing()
        {
            foreach (var st in new[] { ChatterStyle.Off, ChatterStyle.GestureBabble })
                Assert.AreEqual(0, Run(new ChatterPlan(), Camp(2), 60, st: st).Count, st.ToString());
            Assert.AreEqual(0, Run(new ChatterPlan(), Camp(2), 60, quiet: true).Count, "a card is open or a moment plays");
            var plan = new ChatterPlan();
            double now = 0;
            while (plan.Live.Count == 0 && now < 30) plan.Tick(now += 0.25, Camp(2), 5, 0.6f, false);
            Assert.AreEqual(1, plan.Live.Count > 0 ? 1 : 0, "a mark is up");
            var t = plan.Tick(now + 0.25, Camp(2), 5, 0.6f, false, quiet: true);
            Assert.Greater(t.fade.Count, 0, "a card opening takes them down");
            Assert.AreEqual(0, plan.Live.Count);
        }

        [Test]
        public void SleepersDontChatAndALoneAwakeDoesNotEither()
        {
            var g = new List<ChatterGroup> { G(1, P(1, true), P(2, true), P(3, true)), G(2, P(4), P(5, true)) };
            Assert.AreEqual(0, Run(new ChatterPlan(), g, 120).Count, "night asleep: quiet");
            var g2 = new List<ChatterGroup> { G(1, P(1, true), P(2), P(3)) };
            var shows = Run(new ChatterPlan(), g2, 120);
            Assert.Greater(shows.Count, 5);
            Assert.IsFalse(shows.Any(s => s.speakerId == 1 || s.listenerId == 1), "nobody speaks to or as a sleeper");
        }

        [Test]
        public void BabbleOnlyWhenZoomedInAndOnlyWithTheStyle()
        {
            var plan = new ChatterPlan();
            Assert.AreEqual(0f, plan.BabbleVolume(0f));
            Assert.AreEqual(0f, plan.BabbleVolume(0.4f), "default zoom: no babble");
            Assert.AreEqual(1f, plan.BabbleVolume(0.95f), 1e-6);
            Assert.Greater(plan.BabbleVolume(0.65f), 0f);
            Assert.Less(plan.BabbleVolume(0.65f), 1f, "a fade over a range, not a switch");
            Assert.IsTrue(Run(new ChatterPlan(), Camp(), 30, zoom: 0.95f).All(s => s.babble > 0.99f));
            Assert.IsTrue(Run(new ChatterPlan(), Camp(), 30, zoom: 0.4f).All(s => s.babble == 0f));
            Assert.IsTrue(Run(new ChatterPlan(), Camp(), 30, zoom: 0.95f, st: ChatterStyle.Marks).All(s => s.babble == 0f), "Marks is silent");
        }

        [Test]
        public void MoodPicksTheMark()
        {
            for (int i = 0; i < 100; i++)
            {
                float r = i / 100f;
                Assert.Contains(ChatterPlan.MarkFor(Rapport.Friend, false, r), new[] { ChatterMark.Heart, ChatterMark.Note });
                Assert.Contains(ChatterPlan.MarkFor(Rapport.Partner, true, r), new[] { ChatterMark.Heart, ChatterMark.Note });
                Assert.Contains(ChatterPlan.MarkFor(Rapport.Rival, true, r), new[] { ChatterMark.Bang, ChatterMark.Dots });
                Assert.Contains(ChatterPlan.MarkFor(Rapport.Unsure, true, r), new[] { ChatterMark.Question, ChatterMark.Dots });
                Assert.Contains(ChatterPlan.MarkFor(Rapport.Idle, true, r), new[] { ChatterMark.Dots, ChatterMark.Note });
            }
            Assert.AreEqual(ChatterMark.Dots, ChatterPlan.MarkFor(Rapport.Idle, false, 0.5f));
            Assert.AreEqual(ChatterMark.Bang, ChatterPlan.MarkFor(Rapport.Rival, false, 0.1f));
            Assert.AreEqual(ChatterMark.Question, ChatterPlan.MarkFor(Rapport.Unsure, false, 0.1f));
            Assert.AreEqual(ChatterMark.Heart, ChatterPlan.MarkFor(Rapport.Partner, false, 0.1f));
        }

        [Test]
        public void TheRelationshipDecidesTheMarkInAGroup()
        {
            var a = P(1, false, Rapport.Rival, 2); var b = P(2, false, Rapport.Rival, 1);
            var shows = Run(new ChatterPlan(), new List<ChatterGroup> { G(1, a, b) }, 200);
            var share = shows.Count(s => s.mark == ChatterMark.Bang) / (float)shows.Count;
            Assert.Greater(share, 0.6f, "two rivals mostly bark at each other");
            Assert.IsFalse(shows.Any(s => s.mark == ChatterMark.Heart));
        }

        [Test]
        public void SameSeedSamePlanDifferentSeedDifferent()
        {
            string Sig(int seed) => string.Join(",", Run(new ChatterPlan(), Camp(3), 100, seed: seed).Select(s => $"{s.groupId}:{s.speakerId}:{s.mark}:{s.startAt}"));
            Assert.AreEqual(Sig(3), Sig(3));
            Assert.AreNotEqual(Sig(3), Sig(4));
        }

        [Test]
        public void ChatterRateIsNotTheRealLinesRate()
        {
            // The real lines come once per 20 s at most (BubbleRules.minGapSeconds); the chatter does not wait for them.
            var plan = new ChatterPlan();
            Assert.Less(plan.rules.maxGap, new BubbleRules().minGapSeconds);
            Assert.Greater(Run(plan, Camp(), 20).Count, 2);
        }

        [Test]
        public void AKnockMarkSkipsTheTurnsAndTheCapButNotQuiet()
        {
            var plan = new ChatterPlan();
            var g = Camp(8, 3);
            Run(plan, g, 30);
            int before = plan.Live.Count;
            for (int i = 0; i < 4; i++) Assert.IsNotNull(plan.React(30, 100 + i, 0, ChatterMark.Bang, ChatterStyle.MarksBabble, false, 1.2f));
            Assert.AreEqual(before + 4, plan.Live.Count);
            Assert.IsNull(plan.React(30, 1, 0, ChatterMark.Bang, ChatterStyle.MarksBabble, true, 1.2f));
            Assert.IsNull(plan.React(30, 1, 0, ChatterMark.Bang, ChatterStyle.Off, false, 1.2f));
            // a real line keeps the reaction's mark
            var tick = plan.Tick(30.25, g, 5, 0.6f, realLineUp: true);
            Assert.IsFalse(tick.fade.Any(s => s.reaction));
            Assert.AreEqual(4, plan.Live.Count(s => s.reaction));
        }

        // ---------- babble ----------

        [Test]
        public void BabbleIsDeterministicBoundedAndShort()
        {
            for (int v = 0; v < ChatterBabble.Voices; v++)
                for (int c = 0; c < ChatterBabble.Clips; c++)
                {
                    var a = ChatterBabble.Generate(v, c, 7);
                    var b = ChatterBabble.Generate(v, c, 7);
                    CollectionAssert.AreEqual(a, b, $"voice {v} clip {c}");
                    float max = a.Max(Math.Abs);
                    Assert.AreEqual(ChatterBabble.Peak, max, 1e-4, "normalised");
                    Assert.IsTrue(a.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
                    float secs = a.Length / (float)ChatterBabble.SampleRate;
                    Assert.That(secs, Is.InRange(0.08f, 2.2f), $"voice {v} clip {c}: a short phrase");
                    Assert.Less(Math.Abs(a[0]), 0.02f, "no click at the start");
                    Assert.Less(Math.Abs(a[^1]), 0.02f, "or the end");
                }
            Assert.AreEqual(4, ChatterBabble.Voices);
            Assert.AreEqual(6, ChatterBabble.Clips);
            Assert.AreEqual(22050, ChatterBabble.SampleRate);
        }

        [Test]
        public void BabbleDiffersBySeedVoiceAndClip()
        {
            Assert.IsFalse(ChatterBabble.Generate(0, 0, 1).SequenceEqual(ChatterBabble.Generate(0, 0, 2)));
            Assert.IsFalse(ChatterBabble.Generate(0, 0, 1).SequenceEqual(ChatterBabble.Generate(1, 0, 1)));
            Assert.IsFalse(ChatterBabble.Generate(0, 0, 1).SequenceEqual(ChatterBabble.Generate(0, 1, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChatterBabble.Generate(4, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChatterBabble.Generate(0, 6, 1));
        }

        [Test]
        public void KidsAreHigherThanAdults()
        {
            // zero-crossing rate is a pitch proxy: an adult low voice vs the kid voice, the same clip and seed
            float Zc(float[] s) { int z = 0; for (int i = 1; i < s.Length; i++) if ((s[i - 1] < 0) != (s[i] < 0)) z++; return z / (s.Length / (float)ChatterBabble.SampleRate); }
            float low = 0, high = 0;
            for (int seed = 0; seed < 8; seed++) { low += Zc(ChatterBabble.Generate(0, 0, seed)); high += Zc(ChatterBabble.Generate(3, 0, seed)); }
            Assert.Greater(high, low * 1.2f);
            Assert.AreEqual(3, ChatterBabble.VoiceFor(4f, 10));
            Assert.AreEqual(2, ChatterBabble.VoiceFor(14f, 10));
            Assert.That(ChatterBabble.VoiceFor(30f, 10), Is.InRange(0, 1));
            Assert.AreEqual(1, ChatterBabble.ClipFor(ChatterMark.Note, 0.3f));
            Assert.AreEqual(3, ChatterBabble.ClipFor(ChatterMark.Bang, 0.3f));
            Assert.AreEqual(4, ChatterBabble.ClipFor(ChatterMark.Question, 0.3f));
        }

        [Test]
        public void ABangIsShorterThanAMurmur()
        {
            double Avg(int clip) => Enumerable.Range(0, 20).Average(s => ChatterBabble.Generate(1, clip, s).Length);
            Assert.Less(Avg(3), Avg(0));
        }

        // ---------- the knock ----------

        [Test]
        public void TheKnockBangPopsBeforeTheBodyTurns()
        {
            var w = new[]
            {
                new KnockWitness { id = 1, reacts = true, distance = 3 },
                new KnockWitness { id = 2, reacts = false, distance = 1 }, // a sleeper or a baby: the planner said no
                new KnockWitness { id = 3, reacts = true, distance = 2 },
            };
            var plan = KnockReaction.Plan(10.0, w);
            Assert.AreEqual(new[] { 3, 1 }, plan.Select(b => b.id).ToArray(), "nearest first, only those who react");
            var b0 = plan[0];
            Assert.AreEqual(10.0, b0.bangAt, 1e-9);
            Assert.AreEqual(0.15, b0.bodyAt - b0.bangAt, 1e-6, "0.15 s before the body turns");
            Assert.AreEqual(1.2, b0.holdUntil - b0.bodyAt, 1e-6, "held 1.2 s");
            Assert.Greater(b0.fadeUntil, b0.holdUntil);
            foreach (var b in plan) Assert.AreEqual(KnockReaction.LeadSeconds, b.bodyAt - b.bangAt, 1e-6);
            Assert.AreEqual(0.15, KnockReaction.BodyDelay(b0, 10.0), 1e-6);
            Assert.AreEqual(0.0, KnockReaction.BodyDelay(b0, 11.0));
        }

        [Test]
        public void ACrowdedKnockShowsAtMostFourBangsNearestFirstWithTiesById()
        {
            var w = Enumerable.Range(0, 9).Select(i => new KnockWitness { id = 20 - i, reacts = true, distance = i < 6 ? 1 : 5 }).ToList();
            var plan = KnockReaction.Plan(0, w);
            Assert.AreEqual(4, plan.Count);
            Assert.AreEqual(plan.Select(b => b.id).OrderBy(i => i).ToList(), plan.Select(b => b.id).OrderBy(i => i).ToList());
            Assert.AreEqual(new[] { 15, 16, 17, 18 }, plan.Select(b => b.id).ToArray());
            Assert.AreEqual(0, KnockReaction.Plan(0, new KnockWitness[0]).Count);
            for (int i = 1; i < plan.Count; i++) Assert.Greater(plan[i].bangAt, plan[i - 1].bangAt, "a hair apart, not one flash");
        }

        // ---------- #194 markers ----------

        static List<RelMarker> FireFamily() => new List<RelMarker>
        {
            new RelMarker(1, RelKind.Partner, 200, 300),
            new RelMarker(2, RelKind.Child, 215, 305), new RelMarker(3, RelKind.Child, 190, 310), new RelMarker(4, RelKind.Child, 205, 295),
            new RelMarker(5, RelKind.Friend, 225, 300),
        };

        [Test]
        public void KidsByTheFireMergeIntoOneLabelWithACount()
        {
            var r = MarkerLayout.Layout(FireFamily(), 60, 24, 400, 700);
            var kids = r.placed.Single(p => p.kind == RelKind.Child);
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, kids.ids);
            Assert.AreEqual(3, kids.Count);
            Assert.AreEqual(3, r.placed.Count, "partner, kids, friend");
            Assert.AreEqual(0, r.overflow);
        }

        [Test]
        public void NoLabelsOverlapAndAllStayOnScreen()
        {
            const float W = 60, H = 24;
            var rng = new Random(3);
            for (int run = 0; run < 200; run++)
            {
                var ms = new List<RelMarker>();
                for (int i = 0; i < 2 + rng.Next(10); i++)
                    ms.Add(new RelMarker(i, (RelKind)rng.Next(8), 100 + rng.Next(80), 200 + rng.Next(60)));
                var r = MarkerLayout.Layout(ms, W, H, 400, 700);
                Assert.LessOrEqual(r.placed.Count, 4);
                Assert.AreEqual(ms.Count, r.placed.Sum(p => p.Count) + r.overflow, "everyone is drawn or counted");
                foreach (var p in r.placed)
                {
                    Assert.GreaterOrEqual(p.x - W / 2, 0); Assert.LessOrEqual(p.x + W / 2, 400);
                    Assert.GreaterOrEqual(p.y - H / 2, 0); Assert.LessOrEqual(p.y + H / 2, 700);
                }
                for (int i = 0; i < r.placed.Count; i++)
                    for (int j = i + 1; j < r.placed.Count; j++)
                        Assert.IsFalse(MarkerLayout.Overlaps(r.placed[i], r.placed[j], W, H), $"run {run}: {i} and {j}");
            }
        }

        [Test]
        public void PriorityDecidesWhoSurvivesWhenTooManyAndTheRestAreCounted()
        {
            var ms = new List<RelMarker>
            {
                new RelMarker(9, RelKind.Other, 100, 300), new RelMarker(8, RelKind.Rival, 400, 300), new RelMarker(7, RelKind.Mentor, 700, 300),
                new RelMarker(6, RelKind.Friend, 1000, 300), new RelMarker(5, RelKind.Sibling, 1300, 300),
                new RelMarker(4, RelKind.Parent, 1600, 300), new RelMarker(1, RelKind.Partner, 1900, 300),
            };
            var r = MarkerLayout.Layout(ms, 60, 24, 2200, 700);
            CollectionAssert.AreEqual(new[] { RelKind.Partner, RelKind.Parent, RelKind.Sibling, RelKind.Friend }, r.placed.Select(p => p.kind).ToArray());
            Assert.AreEqual(3, r.overflow);
            CollectionAssert.AreEquivalent(new[] { 9, 8, 7 }, r.overflowIds);
        }

        [Test]
        public void AMarkerThatFitsStaysAboveItsPersonWithoutALeader()
        {
            var r = MarkerLayout.Layout(new[] { new RelMarker(1, RelKind.Partner, 200, 300) }, 60, 24, 400, 700);
            Assert.AreEqual(200, r.placed[0].x, 1e-4);
            Assert.AreEqual(276, r.placed[0].y, 1e-4);
            Assert.IsFalse(r.placed[0].leader);
        }

        [Test]
        public void ADisplacedMarkerGetsALeaderLine()
        {
            // two partners-class labels on the same spot: the second must move and say who it belongs to
            var ms = new[] { new RelMarker(1, RelKind.Partner, 200, 300), new RelMarker(2, RelKind.Rival, 200, 300) };
            var r = MarkerLayout.Layout(ms, 60, 24, 400, 700);
            Assert.IsFalse(r.placed[0].leader);
            Assert.IsTrue(r.placed[1].leader || Math.Abs(r.placed[1].y - 276) > 0);
            Assert.IsFalse(MarkerLayout.Overlaps(r.placed[0], r.placed[1], 60, 24));
        }

        [Test]
        public void LayoutIsDeterministicAndIgnoresInputOrder()
        {
            var a = MarkerLayout.Layout(FireFamily(), 60, 24, 400, 700);
            var b = MarkerLayout.Layout(Enumerable.Reverse(FireFamily()), 60, 24, 400, 700);
            Assert.AreEqual(a.placed.Select(p => (p.kind, p.x, p.y)).ToList(), b.placed.Select(p => (p.kind, p.x, p.y)).ToList());
        }

        // ---------------- tester run 8: leaders

        static bool Behind(LeaderRoute r, LabelBox o)
        {
            if (!r.bends) return MarkerLayout.Crosses(r.ax, r.ay, r.bx, r.by, o);
            if (!r.twice) return MarkerLayout.Crosses(r.ax, r.ay, r.vx, r.vy, o) || MarkerLayout.Crosses(r.vx, r.vy, r.bx, r.by, o);
            return MarkerLayout.Crosses(r.ax, r.ay, r.vx, r.vy, o) || MarkerLayout.Crosses(r.vx, r.vy, r.wx, r.wy, o) || MarkerLayout.Crosses(r.wx, r.wy, r.bx, r.by, o);
        }

        [Test]
        public void ALeaderWithAClearWayGoesStraight()
        {
            var r = MarkerLayout.RouteLeader(new LabelBox(100f, 100f, 80f, 30f), 100f, 300f, new[] { new LabelBox(400f, 100f, 80f, 30f) });
            Assert.IsFalse(r.bends);
            Assert.AreEqual(115f, r.ay, 0.01f, "from the edge facing the person");
        }

        [Test]
        public void ALeaderDoesNotRunBehindAnotherLabel()
        {
            var other = new LabelBox(100f, 200f, 200f, 40f);
            var r = MarkerLayout.RouteLeader(new LabelBox(100f, 100f, 80f, 30f), 100f, 300f, new[] { other });
            Assert.IsFalse(Behind(r, other), "it goes round the label in the way");
            Assert.IsTrue(r.bends);
        }

        [Test]
        public void ManyLabelsInTheWayStillFindAWayRound()
        {
            // Labels dropped anywhere can wall a person in: real layouts have at most four, so one in fifty may stay straight.
            var rng = new System.Random(3);
            int stuck = 0;
            for (int k = 0; k < 200; k++)
            {
                var others = new List<LabelBox>();
                for (int i = 0; i < 3; i++) others.Add(new LabelBox(rng.Next(100, 900), rng.Next(300, 900), 180f, 40f));
                var me = new LabelBox(500f, 200f, 180f, 40f);
                float toX = rng.Next(100, 900), toY = 1000f;
                var r = MarkerLayout.RouteLeader(me, toX, toY, others);
                foreach (var o in others) if (Behind(r, o)) { stuck++; break; }
            }
            Assert.LessOrEqual(stuck, 4);
        }
    }
}
