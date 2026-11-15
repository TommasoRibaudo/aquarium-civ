// #365 B9: the meal director's staging of the bowls and the poke (EditMode; no scene: it takes the plan's activities and positions as data).
using System.Collections.Generic;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class MealDirectorTests
    {
        static readonly Place Fire = new Place(PlaceKind.Fire, 1);

        static PersonActivity Act(int i, ActivityKind kind, bool moving = false) => new PersonActivity { who = PersonRef.Crowd(i), kind = kind, place = Fire, from = Fire, moving = moving };

        static (List<PersonActivity> acts, System.Func<int, Vector2?> pos) Ring(int n, ActivityKind kind)
        {
            var acts = new List<PersonActivity>();
            for (int i = 0; i < n; i++) acts.Add(Act(i, kind));
            return (acts, key =>
            {
                int i = -key - 1;
                float a = (20f + i * 45f) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f;
            });
        }

        static void Step(MealDirector d, List<PersonActivity> acts, float time, System.Func<int, Vector2?> pos, LifeStage stage = LifeStage.Adult, string job = null)
            => d.Step(acts, time, p => Vector2.zero, who => stage, who => job, pos);

        [SetUp] public void Clear() => PairStage.Clear();
        [TearDown] public void ClearAfter() => PairStage.Clear();

        [Test]
        public void ThreeSeatedEaters_GetTheirBowlsInTurn_AndWaitUntilThen()
        {
            var d = new MealDirector();
            var (acts, pos) = Ring(5, ActivityKind.Eat);
            Step(d, acts, 10f, pos);
            Assert.IsTrue(PairStage.Events.Count > 0, "the bowls are going round");
            // the first taker has its bowl at the contact; the last still waits before its turn
            var first = PairStage.Events[0];
            int firstKey = first.group.KeyOf(1);
            Assert.IsTrue(d.Waiting(firstKey, 10.5f), "waits for its bowl");
            Assert.IsFalse(d.Waiting(firstKey, first.BeatStart + first.giverBeat.ContactAt + 0.05f), "has it at the contact");
            var last = PairStage.Events[PairStage.Events.Count - 1];
            Assert.IsTrue(d.Waiting(last.group.KeyOf(1), last.BeatStart - 0.1f));
        }

        [Test]
        public void TwoEaters_NoPass_TheyJustEat()
        {
            var d = new MealDirector();
            var (acts, pos) = Ring(2, ActivityKind.Eat);
            Step(d, acts, 10f, pos);
            Assert.AreEqual(0, PairStage.Events.Count);
            Assert.IsFalse(d.Waiting(acts[0].who.Key, 10.5f));
        }

        [Test]
        public void Poke_PicksOneGrownUpSittingAtTheFire_OncePerSlot_AndNeverAnEater()
        {
            var d = new MealDirector();
            var (acts, pos) = Ring(4, ActivityKind.SitOut);
            acts.Add(Act(9, ActivityKind.Eat)); // eating: never the poker
            int pokes = 0; int who = -1;
            for (float t = 0f; t < 40f; t += 1f / 30f)
            {
                Step(d, acts, t, pos);
                bool any = false;
                foreach (var a in acts)
                    if (d.Poking(a.who.Key, t, out _, out _)) { any = true; Assert.AreNotEqual(PersonRef.Crowd(9).Key, a.who.Key); if (who != a.who.Key) { pokes++; who = a.who.Key; } }
                if (!any) who = -1;
            }
            Assert.That(pokes, Is.InRange(4, 8), "one poke every 5 to 8 s");
        }

        [Test]
        public void CookStandsAtThePot_AndStirBeginsFireOnceEach()
        {
            var d = new MealDirector();
            var (acts, pos) = Ring(1, ActivityKind.Cook);
            Step(d, acts, 0f, pos, job: "cook");
            var spot = d.CookSpot(new Vector2(0.3f, 0f));
            Assert.AreEqual(MealDirector.CookStandOff, (spot - d.Pot).magnitude, 0.001f);
            int began = 0;
            for (float t = 0f; t < 16f; t += 1f / 30f) if (d.StirBegan(7, t, 1f)) began++;
            Assert.That(began, Is.InRange(6, 8), "16 s: 1.6 s stirs, four to a taste (1.9 s): seven new ones");
        }

        [Test]
        public void StewBubbles_StayOnTheStew_SwellPopAndRest()
        {
            // #676: each bubble sits on the stew inside the lip, never grows much past full size, and rests (size 0) part of each cycle
            for (int i = 0; i < 3; i++)
            {
                int resting = 0, n = 0;
                for (float t = 0f; t < 30f; t += 1f / 30f, n++)
                {
                    float k = MealDirector.BubbleAt(t, i, out Vector3 at);
                    Assert.That(k, Is.InRange(0f, 1.31f));
                    Assert.Less(new Vector2(at.x, at.z).magnitude, 0.052f * 0.92f, "inside the pot's lip");
                    if (k == 0f) resting++;
                }
                Assert.That(resting / (float)n, Is.InRange(0.3f, 0.5f), "popped and resting about 40 % of the time");
            }
        }

        [Test]
        public void Steam_IsSoftSmallAndSpread_NeverAColumn()
        {
            // #676 (tester): crisp white discs stacked over the pot read as a lid. Steam is translucent, small, widens as it fades, and the
            // puffs start apart and out of step
            Assert.Less(MealDirector.SteamAlpha, 0.5f, "well under half opaque (the shader fades it to 0 at the top)");
            Assert.Greater(MealDirector.SteamSizeTo, MealDirector.SteamSizeFrom * 2f, "widens as it rises");
            Assert.Less(MealDirector.SteamSizeTo, 0.052f, "a puff stays smaller than the pot's mouth");
            Assert.Greater(MealDirector.SteamDrift, 0f, "drifts aside");
            foreach (int n in new[] { MealDirector.SteamPuffs, MealDirector.SteamPuffsWinter })
            {
                var spots = new List<Vector3>(); var phases = new List<float>();
                for (int i = 0; i < n; i++) { spots.Add(MealDirector.SteamStart(i, n, out float ph)); phases.Add(ph); }
                for (int i = 0; i < n; i++)
                {
                    Assert.Less(new Vector2(spots[i].x, spots[i].z).magnitude, 0.052f * 0.92f, "starts over the stew");
                    for (int j = i + 1; j < n; j++)
                    {
                        bool samePlace = Vector2.Distance(new Vector2(spots[i].x, spots[i].z), new Vector2(spots[j].x, spots[j].z)) < 0.006f;
                        float dp = Mathf.Abs(Mathf.Repeat(phases[i] - phases[j] + 0.5f, 1f) - 0.5f);
                        Assert.IsFalse(samePlace && dp < 0.25f, $"puffs {i} and {j} would stack");
                        Assert.Greater(dp, 0.02f, $"puffs {i} and {j} rise out of step");
                    }
                }
            }
            Assert.Greater(MealDirector.SteamPuffsWinter, MealDirector.SteamPuffs, "more in winter");
        }
    }
}
