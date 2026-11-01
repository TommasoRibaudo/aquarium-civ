using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #77, #80: each close step and each page kind of a 120-year box on the real scene, at 1179x2556 and 750x1334, for the eye. Not a
    /// check: only a thrown exception fails it. PNGs go to Temp/agent/bookclose/&lt;width&gt;/. Run one at a time, by name, with
    /// <c>--include_explicit true --async_tests true</c>.
    /// </summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("BookCloseShots")]
    public class BookCloseShots : PlayTestBase
    {
        string dir;

        IEnumerator Cap(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(dir + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static IEnumerator Seconds(float s) { yield return new WaitForSecondsRealtime(s); }

        static IEnumerator LiveYears(float years)
        {
            for (float y = 0f; y < years; y += 10f) { SimLayoutSource.DebugSkipYears(Mathf.Min(10f, years - y)); yield return null; }
        }

        static IEnumerator Quiet()
        {
            var sim = GameClock.Instance.Sim;
            sim.CleanAll();
            sim.CardsSeenToNow();
            CardStackView.Instance?.Model?.HidePeek();
            GameHud.Instance.ForceStories = false;
            GameHud.Instance.SetHistoryDot(false);
            PeopleView.DebugSetHour(11f);
            yield return Frames(40);
        }

        IEnumerator Old()
        {
            var sim = GameClock.Instance.Sim;
            var cam = Camera.main;
            var rig = cam.GetComponent<BoxCamera>();
            EraStills.DebugClear(sim.State.seed);
            // The box crosses by itself here; each crossing gets its still, as the cheer would take it.
            for (int y = 0; y < 120; y += 2)
            {
                var era = sim.State.era;
                SimLayoutSource.DebugSkipYears(2f);
                yield return null;
                if (sim.State.era == era) continue;
                sim.CleanAll();
                yield return Frames(20);
                EraStills.Take(cam, rig, sim);
            }
            sim.DebugGiveRelic(RelicId.SkyCrumb);
            sim.DebugGiveRelic(RelicId.KeeperDoll);
            sim.DebugGiveRelic(RelicId.FestivalMask);
            yield return Quiet();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Close([Values(1179, 750)] int w)
        {
            dir = Shots + "bookclose/" + w + "/";
            yield return OpenSlice(w, w == 750 ? 1334 : 2556, seed: 7531);
            DebugDock.ForceLocked = true;
            yield return LiveYears(8f);
            yield return Quiet();
            yield return Cap("close-0-before");
            var talk = EveningTalkView.Instance;
            string line = GameClock.Instance.Sim.DebugForceTeaser(ThreadSource.Couple);
            Assert.IsTrue(talk.Begin(line));
            yield return Seconds(0.14f);
            yield return Cap("close-1-leaving");
            yield return Until(() => talk.Step == CloseStep.Gather, 4f);
            yield return Seconds(2f);
            yield return Cap("close-2-gather");
            yield return Until(() => talk.Step == CloseStep.Speak, 4f);
            yield return Seconds(1.2f);
            yield return Cap("close-3-speak");
            yield return Until(() => talk.Step == CloseStep.Shelf, 4f);
            yield return Seconds(1.4f);
            yield return Cap("close-4-shelf-moving");
            yield return Until(() => talk.Step == CloseStep.Rest, 4f);
            yield return Seconds(0.6f);
            yield return Cap("close-5-rest");
            talk.Leave();
            yield return Seconds(1.6f);
            yield return Cap("close-6-back");
            var sim = GameClock.Instance.Sim;
            sim.ForceEraReady();
            yield return Frames(4);
            Assert.IsTrue(talk.Begin());
            yield return Until(() => talk.Step == CloseStep.Rest, 12f);
            yield return Seconds(0.8f);
            yield return Cap("close-7-era-ask");
            yield return TapAt(GameUiPanel.ScreenCenter(MomentPlayer.Instance.GetComponent<UIDocument>().rootVisualElement.Q("moment-notify-no")));
            yield return Seconds(1.2f);
            Assert.IsFalse(talk.Open, "the answer ends the close");
            Assert.IsTrue(talk.Begin());
            yield return Until(() => talk.Step == CloseStep.Rest, 12f);
            yield return Seconds(0.8f);
            yield return Cap("close-8-era-waits");
            talk.Leave();
            AssertNoErrors("close shots");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Book([Values(1179, 750)] int w)
        {
            dir = Shots + "bookclose/" + w + "/";
            yield return OpenSlice(w, w == 750 ? 1334 : 2556, seed: 7531);
            DebugDock.ForceLocked = true;
            yield return Old();
            var sim = GameClock.Instance.Sim;
            Debug.Log($"[Shots] year {sim.State.Year:0} era {sim.State.era} notables {sim.State.notables.Count}");
            var screen = TimeLapseScreenView.Ensure();
            Assert.IsTrue(screen.Open());
            yield return Until(() => screen.Model.Layout.Width > 0f, 3f);
            yield return Frames(20);
            yield return Cap("book-00-ribbon");
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root.Q("tl-book")));
            yield return Frames(10);
            var book = BookView.Instance;
            Assert.IsTrue(book.IsOpen);
            for (int i = 0; i < book.Pages.Count; i++)
            {
                book.GoTo(i);
                yield return Frames(8);
                var p = book.Page;
                string tag = p.kind + (p.kind == BookPageKind.Era ? "-" + p.era : p.kind == BookPageKind.Legend ? "-" + p.person.name : "");
                yield return Cap($"book-{i + 1:00}-{tag}");
            }
            Debug.Log($"[Shots] {book.Pages.Count} pages: " + string.Join(" | ", book.Pages.Select(p => p.title)));
            book.Close();
            yield return Frames(4);
            // a real end: the box is finished
            sim.ForceCompleteBox();
            Assert.IsTrue(book.Open(sim));
            book.GoTo(book.Pages.Count - 1);
            yield return Frames(8);
            yield return Cap("book-98-the-end");
            book.GoTo(book.Pages.FindIndex(p => p.kind == BookPageKind.Legend));
            yield return Frames(6);
            PersonCardView.Instance.Open(book.Page.person.id, false);
            yield return Seconds(0.6f);
            yield return Cap("book-99-person-over-page");
            PersonCardView.Instance.Close();
            yield return Frames(4);
            book.Close();
            AssertNoErrors("book shots");
        }
    }
}
