using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#606: the pinned person's name tag lifts clear of a speech mark over their head and settles back once it is gone.</summary>
    [Category("PinTagLift")]
    public class PinTagLiftPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PersonCardView Cards => PersonCardView.Instance;

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ThePinTagLiftsClearOfASpeechMarkAndSettlesBack()
        {
            yield return OpenSlice(1179, 2556, 4242);
            PeopleView.DebugSetHour(10f);
            var n = Sim.State.notables.First(x => x.alive);
            Assert.IsTrue(Cards.Open(n.id, false));
            yield return Frames(5);
            Cards.TogglePin();
            Cards.Close();
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(Cards.PinTagShown, "the gold tag is over the pinned doll (is the doll on screen?)");
            Assert.That(Cards.PinLift, Is.EqualTo(0f).Within(0.5f), "nothing speaks: the tag rests on their head");
            float restTop = Cards.PinTagRect.yMin;
            yield return Shot("pin-lift-0-rest");

            var chatter = ShowMeDirector.Instance.Chatter;
            ShowMeDirector.DebugSpeechOver(PersonRef.Notable(n.id), 30f);
            yield return Frames(3);
            Assert.IsTrue(chatter.SpeechDrawn, "the speech mark is up");
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(Cards.PinLiftTarget, Is.GreaterThan(1f), "the mark is in the tag's way: it goes up");
            Assert.That(Cards.PinLift, Is.EqualTo(Cards.PinLiftTarget).Within(1f), "and has got there");
            var marks = new System.Collections.Generic.List<ChatterView.ScreenMark>();
            chatter.ScreenMarks(marks);
            var mark = marks.First(m => m.who == PersonRef.Notable(n.id));
            var tag = Cards.PinTagRect;
            float k = Cards.CardRoot.layout.width / Screen.width;
            var m0 = RuntimePanelUtils.ScreenToPanel(Cards.CardRoot.panel, mark.rect.position);
            var markPanel = new Rect(m0.x, m0.y, mark.rect.width * k, mark.rect.height * k);
            Assert.IsFalse(tag.Overlaps(markPanel), $"the pin {tag} and the speech mark {markPanel} no longer overlap");
            Assert.That(tag.yMin, Is.LessThan(restTop), "the tag moved");
            yield return Shot("pin-lift-1-clear");

            // smooth: one frame at a time, no jump bigger than a few px once it has settled
            float speechLift = Cards.PinLift;
            float last = Cards.PinLift;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                Assert.That(Mathf.Abs(Cards.PinLift - last), Is.LessThan(6f), "no jitter while the mark stays");
                last = Cards.PinLift;
            }

            ShowMeDirector.DebugSpeechOver(PersonRef.None, 0f);
            yield return new WaitForSecondsRealtime(1.5f);
            marks.Clear();
            foreach (var c in ChatterView.Drawing) c.ScreenMarks(marks);
            string left = string.Join(", ", marks.Select(m => $"{m.who} {m.rect}")) + $" | bubbles {GlassBubble.OnPanel.Count(b => b.Showing)} | speech {chatter.SpeechDrawn} | target {Cards.PinLiftTarget:0.0} | tag {Cards.PinTagRect}";
            // the people at the camp chat on their own: a natural chatter mark near the head may still hold the tag up a little.
            // The forced mark is gone, so the tag comes down to where the marks left on screen put it (all the way when there are none).
            Assert.That(Cards.PinLift, Is.EqualTo(Cards.PinLiftTarget).Within(0.5f), "the tag has settled (" + left + ")");
            Assert.That(Cards.PinLift, Is.LessThan(speechLift - 1f), "and came down from above the speech mark (" + left + ")");
            if (marks.Count == 0) Assert.That(Cards.PinLift, Is.EqualTo(0f).Within(0.5f), "nothing on screen: the tag rests on their head");
            yield return Shot("pin-lift-2-back");
            CloseSlice();
        }
    }
}
