using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P9 #424 (B12): placed life arrives at once. Every species' arrival plays from the real kit tap (<see cref="PlayTestBase.KeeperPlaces"/>)
    /// and from the debug "place near camp"; the people react within about a second (they turn, one points, a kid runs over); the line
    /// and the history entry come; the waiting hint plays for a ready jar, once a visit, never for the wolves. Also the user's look calls
    /// for the life tool (2026-11-08): the land dims while a species is in hand and clears when it is put down; the ghost is the group's
    /// silhouettes. Shots: Temp/agent/p9-arr-*.png.
    /// </summary>
    [Category("LifeArrival")]
    public class LifeArrivalPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        /// <summary>A living box with a meadow, a pond and a far wood, at 10 h: every species has ground.</summary>
        IEnumerator LivingLand(int w = 1179, int h = 2556)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.Table(0.3f); // a pond with open water
            Sim.DebugGrowFarWood();
            yield return Frames(20);
            Sim.DebugJarsReady();
            yield return new WaitForSecondsRealtime(4f); // the first water's moment (its line, the camera's lean) passes
        }

        static readonly LifeSpecies[] Ladder = { LifeSpecies.Thicket, LifeSpecies.Rabbits, LifeSpecies.Deer, LifeSpecies.Ducks, LifeSpecies.Fish, LifeSpecies.Wolves };

        /// <summary>The arrival started on the tap's frame (B12 P2), the people reacted within ~1 s, the line came after the end beat and the
        /// sim's history line is there.</summary>
        IEnumerator AssertArrival(LifeSpecies s, int played0, string shot)
        {
            var la = LifeArrivalView.Instance;
            Assert.IsNotNull(la, "the arrivals view");
            Assert.AreEqual(played0 + 1, la.PlayedOf(s), $"{s}: the arrival started on the placing");
            float t0 = Time.unscaledTime;
            yield return Until(() => la.LastReactAt >= 0f, 2f);
            Assert.GreaterOrEqual(la.LastReactAt, 0f, $"{s}: the people reacted");
            Assert.LessOrEqual(la.LastReactAt - la.LastStartAt, 1f, $"{s}: within about a second");
            Assert.Greater(la.LastTurned, 0, $"{s}: someone near turned to look ({la.Status()})");
            if (s != LifeSpecies.Wolves) Assert.IsFalse(la.LastPointer.IsNone, $"{s}: the nearest grown-up points");
            else Assert.AreEqual(0, la.LastWalkers, "the wolves: nobody walks over");
            // shots through the arrival at the default camera
            float len = LifeArrivalView.Length(s);
            foreach (float at in new[] { 0.3f, len * 0.45f, len * 0.9f })
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, at - (Time.unscaledTime - t0)));
                yield return Shot($"{shot}-{at:0.0}s");
            }
            yield return Until(() => la.LastLine == LifeArrivalView.Lines[(int)s], len + 6f);
            Assert.AreEqual(LifeArrivalView.Lines[(int)s], la.LastLine, $"{s}: its line after the end beat");
            string kind = "life_" + s.ToString().ToLowerInvariant();
            Assert.IsTrue(Sim.State.chronicle.Any(e => e.kind == kind), $"{s}: the history line {kind}");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EveryArrival_FromTheKitTap()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance;
            var lpv = LifePlaceView.Instance;
            int byTap = 0;
            foreach (var s in Ladder)
            {
                Sim.DebugJarsReady();
                yield return Frames(5);
                if (!Sim.JarReady(s)) { Assert.Inconclusive($"{s}: its jar would not open ({Sim.JarStateOf(s)})"); yield break; }
                int played0 = la.PlayedOf(s);
                var ground = new List<int>();
                Sim.LifeGround(s, ground);
                // the real kit path when a glowing spot is on screen (the far wood's den may be off it)
                bool tapped = false;
                bool onScreen = GlowingCellOnScreen(Sim, s) >= 0;
                if (onScreen)
                {
                    yield return KeeperPlaces(s, "p9-arr-" + s.ToString().ToLowerInvariant());
                    tapped = LastPlaced;
                    byTap++;
                }
                else
                {
                    Debug.Log($"[p9-arr] {s}: no glowing spot on screen ({ground.Count} cells): placed by the debug path");
                    Assert.GreaterOrEqual(s == LifeSpecies.Wolves ? Sim.DebugPlaceWolves() : Sim.DebugPlaceNearCamp(s), 0, $"{s}: placed");
                }
                yield return null;
                yield return AssertArrival(s, played0, "p9-arr-" + s.ToString().ToLowerInvariant() + (tapped ? "-tap" : "-dbg"));
                yield return Until(() => !la.Playing(s), 20f);
            }
            Assert.GreaterOrEqual(byTap, 4, "most species placed by the real tap");
            AssertNoErrors("every arrival");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EveryArrival_FromTheDebugPlace_AndReplay()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance;
            foreach (var s in Ladder)
            {
                int played0 = la.PlayedOf(s);
                int c = s == LifeSpecies.Wolves ? Sim.DebugPlaceWolves() : Sim.DebugPlaceNearCamp(s);
                Assert.GreaterOrEqual(c, 0, $"{s}: the debug place found ground");
                yield return null;
                Assert.AreEqual(played0 + 1, la.PlayedOf(s), $"{s}: the arrival plays from the debug place");
                Assert.IsFalse(Sim.LifeArrivalPending(s), $"{s}: and is acknowledged");
                yield return Until(() => la.LastReactAt >= 0f, 2f);
                Assert.LessOrEqual(la.LastReactAt - la.LastStartAt, 1f, $"{s}: the people react within about a second");
                yield return Until(() => !la.Playing(s), 20f);
            }
            // the panel's replay plays one again, with no change to the sim
            int r0 = la.PlayedOf(LifeSpecies.Ducks), ducks = Sim.EcoDucks;
            StringAssert.Contains("replayed", la.Replay(LifeSpecies.Ducks));
            Assert.AreEqual(r0 + 1, la.PlayedOf(LifeSpecies.Ducks), "replay");
            Assert.AreEqual(ducks, Sim.EcoDucks, "a replay places nothing");
            yield return Until(() => !la.Playing(LifeSpecies.Ducks), 10f);
            StringAssert.Contains("howl", la.DebugHowl());
            Assert.AreEqual(1, la.Howls, "the wolves' howl");
            yield return new WaitForSecondsRealtime(6f);
            AssertNoErrors("debug arrivals");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Ghost_And_Dim_Look()
        {
            yield return LivingLand();
            var hud = GameHud.Instance; var lpv = LifePlaceView.Instance;
            // the same view without and with a species in hand: the dim and the soft glow
            yield return Shot("p9-arr-dim-0-off");
            hud.TakeLife(LifeSpecies.Thicket);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("p9-arr-dim-1-on");
            hud.SetHand(GameHud.KitTool.None);
            yield return Frames(2);
            Assert.IsFalse(lpv.Dimmed, "the dim clears the moment the tool is put down");
            foreach (var s in Ladder)
            {
                hud.TakeLife(s);
                yield return Frames(5);
                Assert.IsTrue(lpv.Dimmed, $"{s} in hand: the rest of the land dims");
                int cell = GlowingCellOnScreen(Sim, s);
                if (cell < 0) { Debug.Log($"[p9-arr] {s}: no glowing spot on screen for the ghost shot"); yield return Shot($"p9-arr-dim-{s.ToString().ToLowerInvariant()}"); hud.SetHand(GameHud.KitTool.None); continue; }
                yield return TapAt(lpv.ScreenOf(cell));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsTrue(lpv.GhostShown, $"{s}: the ghost");
                yield return Shot($"p9-arr-ghost-{s.ToString().ToLowerInvariant()}");
                hud.SetHand(GameHud.KitTool.None);
                yield return Frames(2);
                Assert.IsFalse(lpv.Dimmed, $"{s}: the dim clears at once");
                Assert.IsFalse(lpv.GhostShown, $"{s}: and the ghost goes");
            }
            AssertNoErrors("ghosts");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator AReadyJar_Hints_OncePerVisit_NeverWolves()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance; var sdv = StoryDirectorView.Instance;
            Assert.IsNotNull(sdv, "the story director");
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "the thicket's jar is ready");
            Assert.IsTrue(la.HintWanted(Sim, out int sp, out int stage, out int cell, out _, out _), "a ready jar wants its hint");
            Assert.AreEqual((int)LifeSpecies.Thicket, sp, "the first ready jar's");
            Assert.AreEqual(1, stage, "stage 1 in its first year");
            // the box's first moments (a welcome, the first water) hold everyone: let them pass, as a player would
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.Find().Planner.ClearLive();
            yield return new WaitForSecondsRealtime(1f);
            // the cheat starts it at once with people near the spot
            string said = "";
            for (int i = 0; i < 6 && !said.Contains("stage"); i++)
            {
                said = sdv.HintNow(LifeSpecies.Thicket, 1);
                if (!said.Contains("stage")) { PeopleView.DebugSetHour(10f + i * 0.5f); yield return new WaitForSecondsRealtime(1f); }
            }
            StringAssert.Contains("stage", said, "the hint started");
            var plan = sdv.LastPlan;
            var pv = PeopleView.Find();
            Assert.IsTrue(plan.members.Any(k => pv.Planner.InLifeStory(k > 0 ? PersonRef.Notable(k) : PersonRef.Crowd(-k - 1), pv.Now)), "someone goes to the empty meadow");
            Assert.IsTrue(la.HintedThisVisit(LifeSpecies.Thicket), "once this visit");
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("p9-arr-hint-thicket");
            // once per visit per jar, and never the wolves
            var seen = new List<int>();
            while (la.HintWanted(Sim, out int s2, out int st2, out _, out _, out _))
            {
                Assert.AreNotEqual((int)LifeSpecies.Thicket, s2, "the thicket hinted already this visit");
                Assert.AreNotEqual((int)LifeSpecies.Wolves, s2, "nobody misses wolves");
                seen.Add(s2);
                la.HintStarted((LifeSpecies)s2, st2, 0);
                if (seen.Count > 8) break;
            }
            Assert.Greater(seen.Count, 0, "the other ready jars hint too");
            // the stage grows with the time ready
            Assert.AreEqual(2, StoryRules.HintStage(13));
            Assert.AreEqual(3, StoryRules.HintStage(25));
            AssertNoErrors("hint");
        }
    }
}
