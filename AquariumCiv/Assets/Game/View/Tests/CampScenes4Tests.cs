// #692 part 4: the animal uses' timelines (pure builders, CampScenes4) against the spec rows B37-B39 and B15.d, and B39.b's rate check
// (EditMode, no scene).
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class CampScenes4Tests
    {
        static readonly Vector2 Frog = new Vector2(0.4f, 0.9f), ToWater = Vector2.up, Camp = new Vector2(0.2f, -0.4f);

        static bool Gapless(CampScene s, int m, float from, float to)
        {
            for (float t = from; t < to; t += 0.05f) if (!s.parts[m].Any(p => t >= p.from && t < p.to)) return false;
            return true;
        }

        [Test]
        public void B37_FrogCatch_CreepPounceCuppedHandsAndLetGo()
        {
            var s = CampScenes4.FrogCatch(1, 2, Frog + new Vector2(0.6f, -0.5f), Frog + new Vector2(-0.4f, -0.6f), Frog, ToWater, false, 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(2, s.Count, "the kid and the friend");
            Assert.Greater(s.a, 0f, "the grab");
            Assert.Greater(s.b, s.a + 2f, "held up and peeked at before the release");
            Assert.That(s.c, Is.InRange(s.a, s.b), "the wriggle while held");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Droplets && f.count == 3), "3 muddy drops at the pounce");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Ring), "a ring where it hops in");
            Assert.IsTrue(s.parts[0].Any(p => p.style == DollStyle.WaveSmall), "the kid waves it off");
            var p0 = DollPose.Rest; CampScenes4.PouncePose(ref p0, CampScenes4.PounceHit, 0.5f);
            Assert.AreEqual(40f, p0.lean, 0.5f, "the pounce: lean 40");
            Assert.AreEqual(60f, p0.armR.x, 0.5f, "both arms down x 60");
            Assert.IsTrue(Gapless(s, 0, 0f, s.b + 1.5f), "the kid is in the scene throughout");
            Assert.IsTrue(Gapless(s, 1, 0f, s.b + 1.5f), "the friend too");
        }

        [Test]
        public void B37_FrogCatch_TheMiss_TheFrogGoesFirstTheKidFlopsAndShakesOff()
        {
            var s = CampScenes4.FrogCatch(1, 2, Frog + new Vector2(0.6f, -0.5f), Frog + new Vector2(-0.4f, -0.6f), Frog, ToWater, true, 0f);
            Sane(s); PoseRuns(s);
            Assert.Less(s.a, 0f, "nothing in the hands");
            Assert.Greater(s.b, 0f, "the frog hops in at the pounce");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Droplets && f.count == 6), "the flop: 6 drops");
            Assert.IsTrue(s.parts[1].Any(p => p.style == DollStyle.Laugh), "the friend laughs");
            var sh = DollPose.Rest; float most = 0f;
            for (float t = 0f; t < 0.5f; t += 0.01f) { sh = DollPose.Rest; CampScenes4.ShakeOffPose(ref sh, t, 0.9f); most = Mathf.Max(most, Mathf.Abs(sh.roll)); }
            Assert.AreEqual(12f, most, 0.5f, "the shake: roll ±12°");
        }

        [Test]
        public void B38a_DeerWatch_StandOutsideTheAlertRadiusPointFreezeAndStepBack()
        {
            var deer = new Vector2(-1.2f, 1.4f);
            var s = CampScenes4.DeerWatch(new[] { 10, 11, 12 }, new[] { Camp, Camp + Vector2.right * 0.2f, Camp + Vector2.left * 0.2f }, 11, deer, Camp, 8f, 0f);
            Sane(s); PoseRuns(s);
            for (int m = 0; m < s.Count; m++)
            {
                var stand = s.parts[m][1].pos;
                Assert.That((stand - deer).magnitude, Is.InRange(AnimalGeo.WatchFrom, AnimalGeo.WatchTo), "outside the deer's alert radius (0.6)");
                Assert.Greater((stand - deer).magnitude, DeerView.AlertRadius);
            }
            Assert.Greater(s.a, 0f, "a doe looks up");
            Assert.IsTrue(s.parts.All(l => l.Any(p => p.style == DollStyle.Freeze && p.from >= s.a - 1e-3f)), "everyone freezes at her look");
            Assert.IsTrue(s.parts[1].Any(p => p.pose != null && p.from < s.a), "the grown-up points");
        }

        [Test]
        public void B38b_DeerCreep_ALineOfKidsFreezesThenTheHerdBoltsAndTheyRunAfterIt()
        {
            var deer = new Vector2(-1.2f, 1.4f);
            var s = CampScenes4.DeerCreep(new[] { 1, 2, 3 }, new[] { Camp, Camp, Camp }, 9, Camp + Vector2.right * 0.3f, deer, Camp, true, 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(4, s.Count, "three kids and the grown-up");
            Assert.That(s.c, Is.InRange(0f, s.a), "a deer lifts its head mid-creep");
            for (int m = 0; m < 3; m++)
            {
                float closest = s.parts[m].Min(p => (p.pos - deer).magnitude);
                Assert.AreEqual(AnimalGeo.CreepTo - 0.3f, closest, 0.05f, "creep to 0.62 u, then run 0.3 u after them");
            }
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Chips), "a twig underfoot");
            Assert.IsTrue(s.parts.Take(3).All(l => l.Any(p => p.style == DollStyle.Laugh)), "they all laugh");
            Assert.IsTrue(s.parts[3].Any(p => p.pose != null), "the grown-up shakes the head");
        }

        [Test]
        public void B39a_KitHold_TheKitHopsUpIsHeldStrokedShownAndHopsBack()
        {
            var mother = new Vector2(1.0f, 1.2f); var spot = mother + new Vector2(0f, -0.35f);
            var s = CampScenes4.KitHold(1, 2, Camp, Camp + Vector2.right * 0.3f, spot, mother, 0f);
            Sane(s); PoseRuns(s);
            Assert.That(s.c, Is.LessThan(s.a)); Assert.That(s.a, Is.LessThan(s.b)); Assert.That(s.b, Is.LessThan(s.d));
            Assert.IsTrue(CampScenes4.KitAt(s, (s.a + s.b) * 0.5f, out _, out bool held, out _, out _) && held, "held between the scoop and the setting down");
            Assert.IsTrue(CampScenes4.KitAt(s, s.c + 0.1f, out var first, out _, out _, out _));
            Assert.Less((first - mother).magnitude, 0.15f, "it sets off from its mother");
            Assert.IsTrue(CampScenes4.KitAt(s, s.d - 0.01f, out var last, out _, out _, out _));
            Assert.Less((last - mother).magnitude, 0.1f, "and hops back to her");
            Assert.IsFalse(CampScenes4.KitAt(s, s.d + 1f, out _, out _, out _, out _), "then it is gone among the rabbits");
            Assert.IsTrue(s.parts[0].Any(p => p.style == DollStyle.WaveSmall), "the kid waves after it");
            Assert.IsTrue(Gapless(s, 1, 0f, s.d), "the other kid stays in the scene");
        }

        [Test]
        public void B15d_FeedDucks_ThreeRoundsOfCrumbsAndADuckClimbsOut()
        {
            var bank = new Vector2(0.4f, 0.9f);
            var s = CampScenes4.FeedDucks(new[] { 1, 2, 3 }, new[] { Camp, Camp, Camp }, bank, ToWater, 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(9, s.arcs.Count, "three throws each");
            Assert.IsTrue(s.arcs.All(a => Mathf.Approximately(a.h, AnimalGeo.CrumbH) && Mathf.Approximately(a.seconds, AnimalGeo.CrumbT)), "h 0.08, T 0.35");
            Assert.AreEqual(9, s.fx.Count(f => f.kind == CampFxKind.Ring), "a ring where each lands");
            Assert.IsTrue(s.arcs.All(a => Vector2.Dot(new Vector2(a.to.x, a.to.z) - bank, ToWater) > 0.1f), "onto the water");
            Assert.Greater(s.a, s.arcs.Max(a => a.from), "the duck climbs out after the crumbs");
            Assert.IsTrue(s.parts[0].Any(p => p.run), "a kid runs from it");
            for (int m = 0; m < 3; m++) Assert.IsTrue(Gapless(s, m, 0f, s.a), $"kid {m} throws without a gap");
        }

        [Test]
        public void B39b_DuckUsesPlanned_NeverNoneOverTenDays_TwoOnMostDays()
        {
            int two = 0, days = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                for (long day = 100; day < 110; day++, days++)
                {
                    bool egg = BeatHash.U01((int)day, seed, 7) < 0.55f; // LifePlanner.EggDayChance
                    int n = AnimalGeo.DuckUsesPlanned(day, seed * 977, egg, out _, out _);
                    Assert.GreaterOrEqual(n, 1, $"seed {seed} day {day}: a duck use every view day");
                    if (n >= 2) two++;
                }
            }
            Assert.Greater(two / (float)days, 0.6f, $"two duck uses on most days ({two} of {days})");
        }
    }
}
