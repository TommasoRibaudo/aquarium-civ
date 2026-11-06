using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>The user's 2026-11-06 calls on the real Slice: hugs on (the director is there, a parent-and-kid hug plays B7's beat), a kid
    /// tumbles in the rabbit chase (the cheat), toddlers walk after their carer (the cheat adds a two-year-old if the box has none).</summary>
    public class HugsToddlersPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();

        IEnumerator Grown(float years = 8f, float hour = 10f)
        {
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(years);
            yield return null;
            Sim.CleanAll();
            PeopleView.DebugSetHour(hour);
            yield return Frames(4);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AToddlerWalksAfterItsCarer_NotInTheirArms()
        {
            yield return Grown(hour: 10.5f);
            var pv = People;
            string said = pv.Planner.DebugToddlersToggle();
            StringAssert.Contains("toddlers walk", said);
            try
            {
                PersonActivity baby = default; bool found = false;
                for (float h = 8.5f; h < 17.4f && !found; h += 0.4f)
                {
                    PeopleView.DebugSetHour(h);
                    yield return Frames(6);
                    foreach (var a in pv.Activities) if (a.carryOnFoot && !a.carriedBy.IsNone) { baby = a; found = true; break; }
                }
                if (!found) Assert.Inconclusive("no carer had free hands for a short walk on this day");
                yield return Frames(30);
                Assert.IsTrue(pv.TryGetDollPose(baby.who, out var pose) && !pose.hidden, "the toddler is drawn");
                Assert.IsTrue(pv.TryGetDollPosition(baby.who, out var bp));
                Assert.IsTrue(pv.TryGetDollPosition(baby.carriedBy, out var cp));
                Assert.Less((bp - cp).magnitude, 0.7f, "it is near its carer");
                var carer = pv.Activities.First(a => a.who.Key == baby.carriedBy.Key);
                Assert.IsTrue(carer.carryOnFoot, "the carer's plan agrees");
                StringAssert.Contains("toddling after them", pv.Planner.Describe(carer));
                yield return Shot("toddler-walks");
            }
            finally { pv.Planner.SetDebugToddlers(false); }
            AssertNoErrors("toddler");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HugsAreOn_AndAParentAndKidHug()
        {
            yield return Grown(hour: 18.5f);
            Assert.IsTrue(BeatFlags.Hugs, "hugs are on (the user, 2026-11-06)");
            Assert.IsNotNull(HugDirector.Instance, "the hug director runs on the Slice");
            string said = HugDirector.DebugParentKid();
            if (!said.StartsWith("hug:")) Assert.Inconclusive(said);
            Assert.IsTrue(PairStage.Events.Any(e => e.kind == PairKind.Hug), "a hug is staged");
            yield return Frames(45);
            yield return Shot("hug-parent-kid");
            StringAssert.Contains("hugs on", HugDirector.DebugStatus());
            AssertNoErrors("hug");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AKidTumblesInTheRabbitChase()
        {
            yield return Grown(hour: 14f);
            var rv = RabbitView.Instance;
            if (rv == null) Assert.Inconclusive("no rabbit view (the living land is off)");
            rv.DebugSpawnHere(4);
            yield return Frames(10);
            string said = KidPlayDirector.DebugTumble();
            if (!said.Contains("trips")) Assert.Inconclusive(said);
            var pv = People;
            float flat = 0f, end = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < end && flat < 70f)
            {
                yield return null;
                foreach (var a in pv.Activities)
                    if (pv.Planner.StageOf(a.who) == LifeStage.Kid && pv.TryGetDollPose(a.who, out var p) && !p.hidden) flat = Mathf.Max(flat, p.lean);
            }
            Assert.Greater(flat, 70f, "a kid went flat on its front");
            yield return Shot("kid-tumble");
            AssertNoErrors("tumble");
        }
    }
}
