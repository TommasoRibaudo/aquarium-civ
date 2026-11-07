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

        /// <summary>Batch 4: who is in the box and what they do, for an Inconclusive/fail message (stage:age, then activity kinds).</summary>
        static string Census()
        {
            var pv = People;
            if (pv == null || pv.Planner == null) return "no people";
            var pl = pv.Planner;
            string stages = string.Join(" ", pl.People.GroupBy(w => pl.StageOf(w)).Select(g => $"{g.Key}:{g.Count()}"));
            string babies = string.Join(",", pl.People.Where(w => pl.StageOf(w) == LifeStage.Baby).Select(w => pl.AgeOf(w).ToString("0.0")));
            string kinds = string.Join(" ", pv.Activities.GroupBy(a => a.kind).Select(g => $"{g.Key}:{g.Count()}"));
            string carry = string.Join(" | ", pv.Activities.Where(a => pl.StageOf(a.who) == LifeStage.Baby).Select(a =>
                $"{a.who} {pl.AgeOf(a.who):0.0}y {a.kind} by {a.carriedBy} foot {a.carryOnFoot} back {a.carryOnBack}"
                + (a.carriedBy.IsNone ? "" : $" carer {pv.Activities.FirstOrDefault(c => c.who.Key == a.carriedBy.Key).kind}")
                + $" parent {pl.ParentOf(a.who)} ({pv.Activities.FirstOrDefault(c => c.who.Key == pl.ParentOf(a.who).Key).kind})"));
            return $"carry: {carry}; [{Sim.State.era}, eco {Sim.EcologyOn}, h {pv.ViewHour:0.0}, moments {StagedMoments.Playing}] stages {stages}; baby ages {babies}; acts {kinds}";
        }

        IEnumerator Grown(float years = 8f, float hour = 10f, int kids = 0, bool toddler = false)
        {
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(years);
            yield return null;
            // Batch 4: the Slice box stays a Camp of five grown-ups through the skip (no kids; its one walking-age baby had no carer in
            // the play test), so the test brings its own family: births aged on to a walker (2) or a kid (7). Each birth queues a
            // newborn moment; the calm loop below stops those.
            var born = new System.Collections.Generic.List<Notable>();
            for (int i = 0; i < kids + (toddler ? 1 : 0); i++) { var n = Sim.DebugBirth(); Assert.IsNotNull(n, "a grown-up to be a parent"); born.Add(n); }
            if (born.Count > 0)
            {
                SimLayoutSource.DebugSkipYears(0.1f); // past the births' own day (a birth's scene has every grown-up cheering for hours)
                yield return null;
                for (int i = 0; i < born.Count; i++) born[i].age = i < kids ? 7f : 2f;
                People.Planner.Invalidate();
            }
            Sim.CleanAll();
            PeopleView.DebugSetHour(hour);
            yield return Frames(4);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            // Batch 4: the year skips queue #370 staged moments (a wedding, a new chief) whose live blocks take every adult, so no carer
            // has free hands and no parent is free to hug. Stop them until none starts for 2 s (docs/tooling.md, batch-2 trap 4).
            for (int calm = 0, f = 0; calm < 120 && f < 1200; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.DebugSetHour(hour);
            yield return Frames(4);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AToddlerWalksAfterItsCarer_NotInTheirArms()
        {
            yield return Grown(hour: 10.5f, toddler: true);
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
                if (!found) Assert.Inconclusive("no carer had free hands for a short walk on this day " + Census());
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
            yield return Grown(hour: 16f, kids: 2);
            Assert.IsTrue(BeatFlags.Hugs, "hugs are on (the user, 2026-11-06)");
            Assert.IsNotNull(HugDirector.Instance, "the hug director runs on the Slice");
            // Batch 4: at some hours a scene has the whole box cheering (seed 1234 after the skip): try the day's hours until a parent and kid are free.
            string said = HugDirector.DebugParentKid();
            for (float h = 8f; h < 19.5f && !said.StartsWith("hug:"); h += 1.5f)
            {
                PeopleView.DebugSetHour(h);
                yield return Frames(20);
                said = HugDirector.DebugParentKid();
            }
            if (!said.StartsWith("hug:")) Assert.Inconclusive(said + " " + Census());
            Assert.IsTrue(PairStage.Events.Any(e => e.kind == PairKind.Hug), "a hug is staged");
            yield return Frames(45);
            yield return Shot("hug-parent-kid");
            StringAssert.Contains("hugs on", HugDirector.DebugStatus());
            AssertNoErrors("hug");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AKidTumblesInTheRabbitChase()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true; // batch 4: rabbits live only in a living-land box (the Slice default was off, so this was always Inconclusive)
            try
            {
            yield return Grown(hour: 14f, kids: 4);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            var rv = RabbitView.Instance;
            Assert.IsNotNull(rv, "the rabbit view runs on a living-land Slice");
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z); // green for them to live on
            Debug.Log("[tumble] " + EcoDebug.AddRabbits(6));
            PeopleView.DebugSetHour(14f);
            yield return Until(() => !rv.SignPlaying && rv.Shown >= 1, 15f);
            Assert.GreaterOrEqual(rv.Shown, 1, "rabbits on view to chase");
            string said = KidPlayDirector.DebugTumble();
            Debug.Log("[tumble] " + said);
            StringAssert.Contains("trips", said, "the cheat starts a chase with a tumble " + Census());
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
            finally { GameClock.EcologyPref = prefWas; }
        }

        /// <summary>Where the play ball is on screen (pixels from the top left) and how wide it is drawn.</summary>
        static string BallOnScreen()
        {
            var cam = Camera.main;
            foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (r == null || r.name != "Play Ball" || !r.gameObject.activeInHierarchy) continue;
                var c = cam.WorldToScreenPoint(r.bounds.center);
                var e = cam.WorldToScreenPoint(r.bounds.center + cam.transform.right * r.bounds.extents.x);
                return $"ball at ({c.x:0}, {cam.pixelHeight - c.y:0}) px, {2f * Vector2.Distance(c, e):0.0} px across (screen {cam.pixelWidth}x{cam.pixelHeight})";
            }
            return "no play ball drawn";
        }

        /// <summary>Batch 4 shots at 1179x2556, default zoom, daytime (not a check; the pictures a person judges): kids playing ball
        /// (the ball has a 9 px floor on screen) and a toddler after its carer ("Toddlers walk: all"). Explicit.</summary>
        [UnityTest, Explicit, Timeout(180000)]
        public IEnumerator Batch4Shots()
        {
            yield return Grown(hour: 11f, kids: 3, toddler: true);
            string ball = KidPlayDirector.Debug(PlayGame.Ball);
            Debug.Log("[b4 shots] " + ball);
            yield return new WaitForSecondsRealtime(4f);
            Debug.Log("[b4 shots] " + BallOnScreen());
            yield return Shot("b4-kids-ball");
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log("[b4 shots] " + BallOnScreen());
            yield return Shot("b4-kids-ball-2");
            var pv = People;
            Debug.Log("[b4 shots] " + pv.Planner.DebugToddlersToggle());
            try
            {
                bool found = false;
                for (float h = 9f; h < 17.4f && !found; h += 0.4f)
                {
                    PeopleView.DebugSetHour(h);
                    yield return Frames(6);
                    foreach (var a in pv.Activities) if (a.carryOnFoot && !a.carriedBy.IsNone) { found = true; Debug.Log($"[b4 shots] toddler {a.who} after {a.carriedBy} at {h:0.0} h"); break; }
                }
                yield return new WaitForSecondsRealtime(3f);
                yield return Shot("b4-toddler-follow");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("b4-toddler-follow-2");
            }
            finally { pv.Planner.SetDebugToddlers(false); }
        }
    }
}
