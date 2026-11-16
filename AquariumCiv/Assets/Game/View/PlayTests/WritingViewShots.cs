using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The writing pass's Editor side (#718, #721, #714, #717, #719) for the eye, at 1179x2556 on the real scene: a ritual held moment
    /// (the harvest feast, stacking the woodpile), the book's relic page (three relics), the pond family's boat close, a story told by
    /// the Keeper's pick, the new headman's moment, and the debug block. Not a check: only a thrown exception or a missing state fails
    /// it. PNGs go to Temp/agent/718/. Run by hand: <c>--filter WritingShots --filter_type category --include_explicit true --async_tests true</c>.
    /// </summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("WritingShots")]
    public class WritingViewShots : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.Sim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        [TearDown]
        public void Close() => CloseSlice();

        static IEnumerator GrowInto(Era era)
        {
            var sim = Sim;
            for (int i = 0; i < 20 && sim.State.era < era; i++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            Assume.That(sim.State.era, Is.EqualTo(era), "the box grows into the " + era);
            sim.CleanAll();
            sim.CardsSeenToNow();
            CardStackView.Instance?.Model?.HidePeek();
            yield return Frames(4);
        }

        static IEnumerator AtTheFire(float zoom = 0.32f)
        {
            var pv = PeopleView.Find();
            Cam.FlyToPlace(pv.Planner.GatherPlace, zoom, 0.3f);
            yield return new WaitForSecondsRealtime(0.8f);
        }

        IEnumerator Ritual(string ritual, Era era, string name)
        {
            yield return OpenSlice(seed: Seed);
            yield return GrowInto(era);
            string r = StagedMoments.DebugPlayRitualNow(ritual);
            Debug.Log($"[WritingShots] {ritual}: {r}");
            Assert.IsNotNull(StagedMoments.LastScene, r);
            Assert.AreEqual(MomentKind.Ritual, StagedMoments.LastScene.kind, r);
            yield return AtTheFire();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot($"718/ritual-{name}-caption");
            var pv = PeopleView.Find();
            pv.timeScale = 3f;
            var sc = StagedMoments.LastScene;
            yield return Until(() => pv.Now.hour >= (sc.start + sc.end) * 0.5f, 25f);
            pv.timeScale = 1f;
            ShowMeDirector.DebugBubble(); // the ritual's own line on the caption
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot($"718/ritual-{name}-mid");
            Assert.IsEmpty(errors, string.Join(" | ", errors));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator RitualHarvestFeast() => Ritual(RitualKind.Feast, Era.Village, "harvest-feast");

        [UnityTest, Timeout(300000)]
        public IEnumerator RitualWinterWoodpile() => Ritual(RitualKind.Woodpile, Era.Camp, "winter-woodpile");

        [UnityTest, Timeout(300000)]
        public IEnumerator BookRelicPage()
        {
            yield return OpenSlice(seed: Seed);
            yield return GrowInto(Era.Village);
            foreach (var id in RelicId.All) Sim.DebugGiveRelic(id);
            Sim.CleanAll();
            yield return Frames(10);
            var book = BookView.Instance;
            Assert.IsNotNull(book);
            Assert.IsTrue(book.Open());
            yield return Frames(6);
            int at = book.Pages.FindIndex(p => p.kind == BookPageKind.Relics);
            Assert.GreaterOrEqual(at, 0, "a relic page");
            Assert.AreEqual(3, book.Pages[at].relics.Count, "#718: the Mask, the Doll and the Sandal");
            book.GoTo(at);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("718/book-relics");
            Assert.IsEmpty(errors, string.Join(" | ", errors));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PondFamilysBoat()
        {
            yield return OpenSlice(seed: Seed);
            yield return GrowInto(Era.Village);
            PeopleView.DebugSetHour(11f);
            Debug.Log("[WritingShots] " + ConsequenceCheats.PlaceEvery());
            yield return Frames(20);
            var boat = Sim.ConsequenceProps.FirstOrDefault(p => p.kind == ConsequenceKind.ShoreBoat);
            Assert.IsNotNull(boat, "the boat is placed");
            Assert.IsTrue(ConsequencePropsView.Instance.Standing.TryGetValue(boat.id, out var t) && t != null, "and drawn");
            foreach (var kv in ConsequencePropsView.Instance.Standing) if (kv.Key != boat.id && kv.Value != null) kv.Value.gameObject.SetActive(false); // the boat alone: the cheat stacks every prop at the fire
            Cam.FlyTo(t.position, 0.03f, 0.3f); // #714: close (0 is read as no zoom given: the home view)
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("718/boat-close");
            Cam.FlyTo(t.position, 0.3f, 0.3f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("718/boat-mid");
            // #714: the boat in the scene, with people about at their size (the other props stay hidden: the cheat stacks them at the fire)
            Cam.FlyTo(t.position, 0.6f, 0.3f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("718/boat-scene");
            Assert.IsEmpty(errors, string.Join(" | ", errors));
        }

        IEnumerator Teller(int option, string name)
        {
            yield return OpenSlice(seed: Seed);
            yield return GrowInto(Era.Camp);
            string r = SliceDebugPanel.DebugStoryTonight(option);
            Debug.Log($"[WritingShots] story {name}: {r}");
            var pv = PeopleView.Find();
            int teller = Sim.State.storyTellerId;
            Assert.GreaterOrEqual(teller, 0, r);
            yield return Until(() => pv.Now.hour >= LifePlanner.TellFrom + 0.05f, 20f);
            yield return Until(() => pv.Planner.Plan(PersonRef.Notable(teller), pv.Now).kind == ActivityKind.StoryTell, 20f);
            Assert.AreEqual(ActivityKind.StoryTell, pv.Planner.Plan(PersonRef.Notable(teller), pv.Now).kind, "the pick tells it");
            Cam.FlyToPerson(PersonRef.Notable(teller), 0.28f, 0.3f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Frames(30); // the director's next look sets the teller
            string said = ShowMeDirector.DebugBubble();
            Debug.Log($"[WritingShots] story {name}: {said}");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot($"718/teller-{name}");
            Assert.IsEmpty(errors, string.Join(" | ", errors));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TellerSlow() => Teller(0, "slow");

        [UnityTest, Timeout(300000)]
        public IEnumerator TellerShort() => Teller(1, "short");

        [UnityTest, Timeout(300000)]
        public IEnumerator NewHeadman()
        {
            yield return OpenSlice(seed: Seed);
            yield return GrowInto(Era.Village);
            string r = StagedMoments.DebugPlayNow(MomentKind.NewLeader);
            Debug.Log("[WritingShots] new leader: " + r);
            Assume.That(StagedMoments.LastScene?.kind, Is.EqualTo(MomentKind.NewLeader), r);
            yield return AtTheFire();
            var d = ShowMeDirector.Instance;
            for (int i = 0; i < 4 && !(d.ShownCaption ?? "").Contains("Long live"); i++)
            {
                ShowMeDirector.DebugBubble();
                yield return new WaitForSecondsRealtime(0.8f);
            }
            Debug.Log("[WritingShots] said: " + d.ShownCaption);
            yield return Shot("718/new-headman");
            Assert.IsEmpty(errors, string.Join(" | ", errors));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator DebugBlock()
        {
            yield return OpenSlice(seed: Seed);
            var panel = Object.FindAnyObjectByType<SimDebugPanel>();
            Assume.That(panel, Is.Not.Null, "the sim debug panel is in the scene");
            panel.open = true;
            var fold = (Dictionary<string, bool>)typeof(SimDebugPanel).GetField("fold", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            fold["Rituals (#721)"] = true;
            yield return Frames(10);
            yield return Shot("718/debug-rituals");
            panel.open = false;
            Assert.IsEmpty(errors, string.Join(" | ", errors));
        }
    }
}
