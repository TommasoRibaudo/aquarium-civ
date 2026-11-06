// #365 B9 meals and the fire: pure tests (NUnit 3 classic asserts; UnityEngine maths only), so Tools/AnimBench runs them too.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class MealBeatsTests
    {
        static DollPose Eaten(float t, float left)
        {
            var p = DollPose.Rest;
            MealBeats.Eat(ref p, t, left);
            return p;
        }

        [Test]
        public void Eater_BitesEvery_TwoPointSevenToFiveTwoSeconds_AndTheHandReachesTheMouth()
        {
            // walk the clock: collect every bite start and the arm's highest swing
            var starts = new List<float>();
            float prev = -1f, top = 0f;
            for (float t = 0f; t < 120f; t += 0.02f)
            {
                float x = MealBeats.BiteAt(t);
                if (x >= 0f && prev < 0f) starts.Add(t);
                prev = x;
                top = Mathf.Max(top, Eaten(t, 99f).armR.x);
            }
            Assert.That(starts.Count, Is.GreaterThan(20));
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i] - starts[i - 1], Is.InRange(2.6f, 5.3f), "bite gap " + i);
            Assert.That(top, Is.GreaterThanOrEqualTo(130f), "the right hand goes up to the mouth");
        }

        [Test]
        public void Eater_BowlStaysInTheLeftHand_AndTheChewBobs()
        {
            for (float t = 0f; t < 20f; t += 0.1f) Assert.AreEqual(60f, Eaten(t, 99f).armL.x, 0.01f);
            float x0 = 0f; while (MealBeats.BiteAt(x0) < 0f) x0 += 0.01f;
            float baseY = -0.03f, maxY = baseY;
            for (float x = 0f; x < MealBeats.BiteSeconds; x += 0.01f) maxY = Mathf.Max(maxY, Eaten(x0 + x, 99f).offset.y);
            Assert.That(maxY - baseY, Is.GreaterThanOrEqualTo(0.01f), "a chew bob you can see");
        }

        [Test]
        public void Eater_LeansBackFullInTheMealsLastSecond()
        {
            var mid = Eaten(7f, 8f);
            Assert.That(mid.lean, Is.GreaterThan(0f), "leaning in to the food mid-meal");
            var end = Eaten(7f, 0.3f);
            Assert.That(end.lean, Is.LessThanOrEqualTo(-9f), "leaning back");
            Assert.That(end.armR.z, Is.GreaterThanOrEqualTo(70f), "arms out wide");
            Assert.That(end.armL.z, Is.LessThanOrEqualTo(-70f));
        }

        [Test]
        public void Poke_ContactsOnceAndMovesTheDollTowardTheFire()
        {
            var under = DollPose.Rest; under.squash = 0.78f; under.offset.y = -0.03f;
            Assert.Greater(MealBeats.Poke.ContactAt, 0.3f, "a wind-up before the thrust");
            int hits = 0; float prevHit = 0f, forward = 0f;
            for (float s = 0f; s < MealBeats.Poke.Duration + 0.2f; s += 0.01f)
            {
                var p = MealBeats.Poke.Pose(under, s);
                if (p.hit > 0.5f && prevHit < 0.5f) hits++;
                prevHit = p.hit;
                forward = Mathf.Min(forward, p.offset.z);
            }
            Assert.AreEqual(1, hits);
            Assert.That(forward, Is.LessThanOrEqualTo(-0.09f), "a step in toward the fire");
            var end = MealBeats.Poke.Pose(under, MealBeats.Poke.Duration + 0.5f);
            Assert.AreEqual(under.squash, end.squash, 0.001f, "sits down again");
        }

        [Test]
        public void PokeSlots_AreFiveToEightSecondsApart_AndEveryThirdIsBig()
        {
            float prev = -1f; int bigs = 0, n = 0;
            for (int slot = 0; slot < 60; slot++)
            {
                MealBeats.PokeSlotAt(77, slot * MealBeats.PokeSlot + 0.01f, out float start, out bool big);
                if (prev >= 0f) Assert.That(start - prev, Is.InRange(5f, 8f));
                prev = start; n++; if (big) bigs++;
            }
            Assert.That(bigs, Is.InRange(n / 6, n / 2), "about a third");
        }

        [Test]
        public void Stir_CountsStirs_AndTastesEveryFourth()
        {
            int last = -1, tastes = 0; bool wasTasting = false;
            for (float t = 0f; t < 60f; t += 0.02f)
            {
                int idx = MealBeats.StirIndex(t, 1f, out _, out float taste);
                Assert.That(idx, Is.GreaterThanOrEqualTo(last), "never goes back");
                last = idx;
                if (taste >= 0f && !wasTasting) tastes++;
                wasTasting = taste >= 0f;
            }
            Assert.That(tastes, Is.InRange(7, 8), "60 s / 8.3 s");
            Assert.That(last, Is.InRange(28, 32), "a stir every 1.6 s, four to a taste");
        }

        [Test]
        public void Stir_TasteRaisesTheHandToTheMouth()
        {
            float cycleStart = 0f, tasteAt = MealBeats.StirsPerTaste * MealBeats.StirSeconds + 0.7f;
            var p = DollPose.Rest; MealBeats.Stir(ref p, cycleStart + tasteAt, 1f);
            Assert.That(p.armR.x, Is.GreaterThanOrEqualTo(120f));
            var q = DollPose.Rest; MealBeats.Stir(ref q, 0.4f, 1f);
            Assert.That(q.armR.x, Is.LessThan(70f), "stirring, the hand low at the pot");
        }

        [Test]
        public void Pass_ServesRoundTheRing_NearestToThePotFirst_AndTheGiverLast()
        {
            var fire = Vector2.zero; var pot = new Vector2(0.1f, -0.09f);
            var keys = new List<int>(); var pos = new List<Vector2>();
            for (int i = 0; i < 7; i++) { float a = (20f + i * 45f) * Mathf.Deg2Rad; keys.Add(100 + i); pos.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f); }
            var served = new Dictionary<int, float>();
            var events = MealStage.Pass(5, 2f, 99, pot, fire, pot, keys, pos, served);
            Assert.AreEqual(MealStage.MaxServed, events.Count);
            Assert.AreEqual(7 + 1, served.Count, "every taker and the giver has a serve time");
            float prev = 0f;
            foreach (var e in events)
            {
                Assert.That(e.takerSeated);
                Assert.That(e.start, Is.GreaterThanOrEqualTo(prev - 1e-3f), "one after the other");
                prev = e.End;
                Assert.That(e.End - e.start, Is.InRange(1.0f, 1.8f), "about 1.5 s a person");
            }
            Assert.That(served[99], Is.GreaterThan(events[events.Count - 1].BeatStart), "the giver eats last");
            Assert.That(served[keys[6]], Is.EqualTo(2f), "the one past the sixth took its own at the start");
        }
    }
}
