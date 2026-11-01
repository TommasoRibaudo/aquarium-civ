using System;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#190: the people at the glass stand in a loose group, not a line.</summary>
    public class GlassSpotsTests
    {
        static GlassSpots.Stand[] Group(int seed, int n)
        {
            var g = new GlassSpots.Stand[n];
            for (int i = 0; i < n; i++) g[i] = GlassSpots.Spot(seed, i, 1000 + i * 7 - (seed & 3));
            return g;
        }

        /// <summary>The widest any person strays from the best straight line through the group's middle (a line fit over both axes).</summary>
        static float WidthOffTheLine(GlassSpots.Stand[] g)
        {
            float mx = 0, my = 0;
            foreach (var s in g) { mx += s.along; my += s.depth; }
            mx /= g.Length; my /= g.Length;
            float sxx = 0, syy = 0, sxy = 0;
            foreach (var s in g) { float dx = s.along - mx, dy = s.depth - my; sxx += dx * dx; syy += dy * dy; sxy += dx * dy; }
            float ang = 0.5f * (float)Math.Atan2(2 * sxy, sxx - syy); // the line's direction
            float nx = -(float)Math.Sin(ang), ny = (float)Math.Cos(ang);
            float worst = 0;
            foreach (var s in g) worst = Math.Max(worst, Math.Abs((s.along - mx) * nx + (s.depth - my) * ny));
            return worst;
        }

        [TestCase(4)]
        [TestCase(6)]
        [TestCase(10)]
        public void ThereIsNoStraightLine(int n)
        {
            for (int seed = 1; seed <= 200; seed++)
                Assert.Greater(WidthOffTheLine(Group(seed, n)), 0.07f, $"seed {seed}, {n} people");
        }

        [Test]
        public void SomeStandCloserAndSomeBehind()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var g = Group(seed, 6);
                float min = float.MaxValue, max = float.MinValue;
                foreach (var s in g) { min = Math.Min(min, s.depth); max = Math.Max(max, s.depth); }
                Assert.GreaterOrEqual(max - min, GlassSpots.RankStep * 0.9f, $"seed {seed}: at least two ranks");
                Assert.GreaterOrEqual(min, 0f, "nobody reaches into the glass");
            }
        }

        [TestCase(6)]
        [TestCase(10)]
        public void NobodyStandsCloserThanTheGap(int n)
        {
            for (int seed = 1; seed <= 300; seed++)
            {
                var g = Group(seed, n);
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        float d = (float)Math.Sqrt(Math.Pow(g[i].along - g[j].along, 2) + Math.Pow(g[i].depth - g[j].depth, 2));
                        Assert.GreaterOrEqual(d, SlotSpots.Gap, $"seed {seed}: {i} and {j}");
                    }
            }
        }

        [Test]
        public void ASpotIsStableAndComesFromTheGroupAndThePerson()
        {
            var a = GlassSpots.Spot(5, 2, 40);
            var b = GlassSpots.Spot(5, 2, 40);
            Assert.AreEqual(a, b, "the same group, slot and person: the same spot");
            // arrivals never move anyone standing: the spot doesn't depend on how many came
            Assert.AreEqual(Group(5, 3)[2].along, Group(5, 8)[2].along);
            // another group looks different
            int differ = 0;
            for (int seed = 1; seed <= 30; seed++) if (Math.Abs(GlassSpots.Spot(seed, 0, 1).along - GlassSpots.Spot(seed + 1, 0, 1).along) > 0.01f) differ++;
            Assert.Greater(differ, 15);
            // another person in the slot is nudged, but only a little
            var c = GlassSpots.Spot(5, 2, 41);
            Assert.LessOrEqual(Math.Abs(a.along - c.along), 2 * GlassSpots.Nudge + 1e-4f);
        }

        [Test]
        public void SomeTurnToEachOtherButNobodyTurnsAway()
        {
            int turned = 0, total = 0;
            for (int seed = 1; seed <= 50; seed++)
                foreach (var s in Group(seed, 6))
                {
                    total++;
                    Assert.LessOrEqual(Math.Abs(s.turn), GlassSpots.MaxTurn + 1e-4f);
                    if (Math.Abs(s.turn) > 0.2f) turned++;
                }
            Assert.Greater(turned, total / 5, "some turn to a neighbour");
            Assert.Less(turned, total * 3 / 4, "most still face the glass");
        }

        [Test]
        public void TheGroupStaysCompact()
        {
            for (int seed = 1; seed <= 100; seed++)
                foreach (var s in Group(seed, 10)) Assert.LessOrEqual(Math.Abs(s.along), 1.1f, $"seed {seed}");
        }
    }
}
