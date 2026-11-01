using System.Collections;
using System.Linq;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#144, #161, #194 on the real scene: who chats, what silences it, the knock's order, the cost, the labels.</summary>
    [Category("Chatter"), Category("Bundle3")]
    public class ChatterPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static ShowMeDirector Sm => ShowMeDirector.Instance;
        static ChatterView View => Sm.Chatter;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        /// <summary>A camp at the evening fire, everyone together; the people may speak, but no real line gets in the way.</summary>
        IEnumerator AtTheFire(float zoom = 0.6f)
        {
            yield return OpenSlice(seed: Seed);
            ChatterView.Style = ChatterStyle.MarksBabble;
            ShowMeDirector.BubblesOn = true;
            Sm.Bubbles.rules.minGapSeconds = 1000f;
            Sm.Bubbles.rules.momentGapSeconds = 1000f;
            MomentPlayer.DebugGrowVillage(8f);
            yield return Frames(3);
            Sim.CleanAll();
            PeopleView.DebugSetHour(11f);
            GlassVoice.DebugGather(10);
            Cam.EaseTo(Vector3.up, Cam.yaw, 45f, zoom, 0.05f);
            yield return new WaitForSecondsRealtime(3f);
            yield return Until(() => View.Groups.Count > 0, 6f);
            Assert.IsNotEmpty(View.Groups, "people stand together");
        }

        IEnumerator UntilMarks(float seconds = 7f)
        {
            View.DebugForceChat();
            yield return Until(() => View.MarksDrawn > 0, seconds);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AGroupTalksInMarksAtAboutTwentyPoints()
        {
            yield return AtTheFire();
            yield return UntilMarks();
            Assert.Greater(View.MarksDrawn, 0, View.Status());
            Assert.AreEqual(20f * ChatterView.PanelPerPoint * Screen.width / GameUiPanel.ReferenceWidth, View.MarkPixels, 0.01f);
            Assert.IsTrue(View.Renderer.enabled && View.Renderer.gameObject.activeInHierarchy, "drawn");
            Assert.AreEqual(1, View.Renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount, "one draw");
            yield return Shot("bundle3-chat");
            AssertNoErrors("marks");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ARealLineWinsACardQuietsAndZoomedOutShowsOne()
        {
            yield return AtTheFire();
            yield return UntilMarks();
            Assert.Greater(View.MarksDrawn, 0, "chatting");

            // a real line
            var who = View.DrawnOver[0];
            Assert.IsTrue(Sm.SayOverHead("Hush.", who, 2f));
            yield return Frames(3);
            Assert.AreEqual(0, View.Plan.Live.Count(s => !s.reaction), "a real line fades the talk");
            yield return Frames(10);
            Assert.AreEqual(0, View.Plan.Live.Count(s => !s.reaction), "and no new marks under it");
            yield return new WaitForSecondsRealtime(2.5f);

            // a card
            yield return UntilMarks();
            Assert.IsTrue(PersonCardView.Instance.Open(Sim.State.notables.First(n => n.alive).id, false));
            yield return Frames(3);
            Assert.AreEqual(0, View.Plan.Live.Count, "a card quiets it");
            View.DebugForceChat();
            yield return Frames(10);
            Assert.AreEqual(0, View.Plan.Live.Count, "and stays quiet");
            PersonCardView.Instance.Close();
            yield return Frames(3);

            // clean mode
            GameHud.Instance.SetCleanMode(true);
            yield return Frames(3);
            Assert.AreEqual(0, View.Plan.Live.Count, "clean mode");
            GameHud.Instance.SetCleanMode(false);

            // zoomed out
            Cam.EaseTo(Vector3.up, Cam.yaw, 45f, Cam.maxZoom, 0.05f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Less(View.Zoom01, View.Plan.rules.zoomedOut, "the whole box");
            int most = 0;
            float until = Time.realtimeSinceStartup + 3f;
            View.DebugForceChat();
            while (Time.realtimeSinceStartup < until) { most = Mathf.Max(most, View.Plan.Live.Count(s => !s.reaction)); yield return null; }
            Assert.LessOrEqual(most, View.Plan.rules.maxLiveZoomedOut, "one mark");
            AssertNoErrors("gating");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AtNightNobodyTalks()
        {
            yield return AtTheFire();
            PeopleView.DebugSetHour(1f);
            yield return new WaitForSecondsRealtime(1.2f);
            View.Plan.Reset();
            View.DebugForceChat();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(0, View.MarksDrawn, View.Status());
            AssertNoErrors("night");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheBabbleIsHeardCloseAndNotAtDefaultZoom()
        {
            yield return AtTheFire(1f);
            Assert.That(View.Zoom01, Is.GreaterThan(View.Plan.rules.zoomedOut).And.LessThan(View.Plan.rules.babbleFrom), "default zoom shows marks and stays silent");
            int before = View.Babbles;
            yield return UntilMarks();
            Assert.Greater(View.MarksDrawn, 0, "marks at default zoom");
            Assert.AreEqual(before, View.Babbles, "no babble at default zoom");
            yield return new WaitForSecondsRealtime(2.6f);
            Cam.EaseTo(Vector3.up, Cam.yaw, 45f, BoxCameraMath.FlyPersonZoom, 0.05f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(View.Plan.BabbleVolume(View.Zoom01), 0.9f, "a person's fly-to is close enough");
            View.DebugForceChat();
            yield return Until(() => View.Babbles > before, 8f);
            Assert.Greater(View.Babbles, before, "heard close up");
            Assert.IsEmpty(AudioDirector.Instance.CheckClips().Where(c => c.StartsWith("proc:babble")), "the 24 clips");
            AssertNoErrors("babble");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheMarkComesBeforeTheBody()
        {
            yield return AtTheFire(1f);
            yield return new WaitForSecondsRealtime(2.6f);
            var pv = PeopleView.Find();
            GlassVoice.DebugKnock(1);
            float t0 = Time.realtimeSinceStartup;
            Assert.Greater(View.LastKnockBeats, 0, "someone reacts");
            Assert.LessOrEqual(View.LastKnockBeats, KnockReaction.MaxMarks);
            var bang = View.Plan.Live.First(s => s.reaction);
            var who = ChatterView.RefOf(bang.speakerId);
            float seenMark = -1f, seenBody = -1f;
            while (Time.realtimeSinceStartup - t0 < 1.2f && (seenMark < 0f || seenBody < 0f))
            {
                if (seenMark < 0f && View.DrawnOver.Contains(who) && View.DrawnMarks.Contains(ChatterMark.Bang)) seenMark = Time.realtimeSinceStartup - t0;
                if (seenBody < 0f && pv.Planner.Plan(who, pv.Now).source == ActivitySource.Reaction) seenBody = Time.realtimeSinceStartup - t0;
                yield return null;
            }
            Debug.Log($"[knock] mark at {seenMark:0.000}, body at {seenBody:0.000}, {View.LastKnockBeats} marks");
            Assert.GreaterOrEqual(seenMark, 0f, "the mark shows");
            Assert.GreaterOrEqual(seenBody, 0f, "the body follows");
            Assert.Less(seenMark, seenBody, "the mark first");
            yield return Shot("bundle3-knock");
            AssertNoErrors("knock");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheCostAtTwentyEightPeople()
        {
            yield return OpenSlice(seed: Seed);
            ChatterView.Style = ChatterStyle.MarksBabble;
            ShowMeDirector.BubblesOn = true;
            Sm.Bubbles.rules.minGapSeconds = 1000f;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Sim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(15f);
            Sim.State.population = Mathf.Max(Sim.State.population, 34); // the crowd the cost is asked at
            SimLayoutSource.DebugSkipYears(0.2f);
            yield return Frames(6);
            Sim.CleanAll();
            PeopleView.DebugSetHour(12f);
            GlassVoice.DebugGather(10);
            var pv = PeopleView.Find();
            yield return new WaitForSecondsRealtime(2f);
            View.ResetCost();
            View.DebugForceChat();
            float until = Time.realtimeSinceStartup + 6f;
            int most = 0, frames = 0;
            while (Time.realtimeSinceStartup < until) { most = Mathf.Max(most, View.MarksDrawn); frames++; yield return null; }
            double perFrame = View.CostMs / frames;
            Debug.Log($"[chatter] {pv.Activities.Count} people, {View.Groups.Count} groups, up to {most} marks: {perFrame:0.000} ms a frame over {frames} frames, worst call {View.WorstMs:0.00} ms");
            Assert.GreaterOrEqual(pv.Activities.Count, 28, "the people asked for");
            Assert.Less(perFrame, 1.5, "cheap");
            AssertNoErrors("cost");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TiesStayReadableByTheFire()
        {
            yield return OpenSlice(seed: Seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(25f);
            yield return Frames(4);
            Sim.CleanAll();
            PeopleView.DebugSetHour(19.5f);
            GlassVoice.DebugGather(10);
            yield return new WaitForSecondsRealtime(3f);
            var cards = PersonCardView.Instance;
            var n = Sim.State.notables.Where(x => x.alive).OrderByDescending(x => Sim.RelationshipGraph(true, x.id).Count).First();
            Assert.IsTrue(cards.Open(n.id, true));
            yield return new WaitForSecondsRealtime(1.4f);
            var root = cards.CardRoot.panel.visualTree;
            var shown = root.Query<GlassTag>().ToList().Where(t => t.name.StartsWith("pc-tie-") && t.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex).ToList();
            Debug.Log($"[ties] {n.name}: {Sim.RelationshipGraph(true, n.id).Count} ties, {cards.LinesDrawn} lines, labels: {string.Join(" | ", shown.Select(l => l.Word))}, overflow {cards.OverflowShown}, leaders {cards.LeadersDrawn}");
            for (int i = 0; i < shown.Count; i++)
                for (int j = i + 1; j < shown.Count; j++)
                    Assert.IsFalse(shown[i].worldBound.Overlaps(shown[j].worldBound), $"{shown[i].Word} on {shown[j].Word}");
            yield return Shot("bundle3-ties");
            AssertNoErrors("ties");
        }
    }
}
