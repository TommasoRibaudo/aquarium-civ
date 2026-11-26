// #780: the story asks (#722 4.1-4.4) and the supper choice fit their card at phone sizes: the question, both hints and the buttons are all
// on the screen and the buttons never cover a hint. Screenshots: Temp/agent/780/<ask>-<w>x<h>.png.
using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    [Category("AskFit")]
    public class AskFitPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static CardStackView View => CardStackView.Instance;

        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        /// <summary>A lived-in camp enough for every ask: the camp era, grown-ups and kids, the Keeper's tree grown, a woodpile.</summary>
        IEnumerator Camp(int w, int h)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            var sim = Sim;
            Assert.IsNotNull(sim, "a living box");
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            for (int k = 0; k < 6; k++)
                foreach (var (x, z) in new[] { (-2.4f, 1.2f), (0f, 1.4f), (2.4f, -1f), (0.5f, -1.6f), (-0.5f, 0.2f), (2f, 1.5f) }) EcoDebug.MistBig(x, z);
            sim.DebugAddKids(7, 21f);
            EcoDebug.SkipSeasons(4);
            sim.DebugGrowFarWood();
            sim.MarkFirstWildWoodSeen();
            if (!sim.StockSetDown) sim.SetDownStock();
            sim.DebugFireStage(2);
            Debug.Log("[780] " + CampCheats.GrowTree());
            sim.DebugAddKids(4, 7f);
            HeldMomentStage.DebugEndPlaying();
            yield return Frames(30);
            Assert.AreEqual(Era.Camp, sim.State.era);
        }

        /// <summary>Opens the card of whatever is open, and checks it.</summary>
        IEnumerator Check(string name, int w, int h)
        {
            yield return Frames(20);
            var root = View.Root;
            var sheet = root.Q("cards-sheet");
            Assert.IsTrue(Shown(sheet), name + ": the card is up");
            var screen = root.worldBound;
            var hints = new[] { root.Q("cards-dil-hint-0"), root.Q("cards-dil-hint-1") };
            var buttons = new[] { root.Q("cards-next"), root.Q("cards-showme"), root.Q("cards-dil-let"), root.Q("cards-dil-answer-0"), root.Q("cards-dil-answer-1"), root.Q("cards-back") };
            var all = new System.Collections.Generic.List<VisualElement>(hints); all.AddRange(buttons); all.Add(root.Q("cards-headline"));
            yield return Shot($"780/{name}-{w}x{h}");
            foreach (var e in all)
            {
                if (e == null || !Shown(e)) continue;
                var r = e.worldBound;
                Assert.IsTrue(r.xMin >= screen.xMin - 1f && r.xMax <= screen.xMax + 1f && r.yMin >= screen.yMin - 1f && r.yMax <= screen.yMax + 1f,
                    $"{name}: {e.name} {r} is inside the screen {screen}");
                Assert.LessOrEqual(r.yMax, sheet.worldBound.yMax + 1f, $"{name}: {e.name} is on the card");
            }
            foreach (var hint in hints)
            {
                if (hint == null || !Shown(hint) || string.IsNullOrEmpty((hint as Label)?.text)) continue;
                foreach (var b in buttons)
                    if (b != null && Shown(b))
                        Assert.IsFalse(hint.worldBound.Overlaps(b.worldBound), $"{name}: {hint.name} {hint.worldBound} is covered by {b.name} {b.worldBound}");
            }
            var a0 = root.Q("cards-dil-answer-0").worldBound; var a1 = root.Q("cards-dil-answer-1").worldBound;
            Assert.LessOrEqual(a0.xMax, a1.xMin, name + ": the two answers do not overlap");
            Assert.Less(root.Q("cards-headline").worldBound.yMax, a0.yMin + 1f, name + ": the question sits over the options");
        }

        IEnumerator AllAsks(int w, int h)
        {
            yield return Camp(w, h);
            foreach (var (name, kind) in new[] { ("4.1-offering", StoryChoices.Offering), ("4.2-first-log", StoryChoices.FirstLog), ("4.3-song-name", StoryChoices.SongName), ("4.4-pup", StoryChoices.PupHolder) })
            {
                string r = StoryMoment.DebugAskNow(kind);
                Debug.Log($"[780] {name}: {r}");
                Assert.IsTrue(r.StartsWith("raised"), name + ": " + r);
                yield return Check(name, w, h);
            }
            var d = Sim.DebugRaiseSmall(SmallChoices.Supper);
            Assert.IsNotNull(d, "the supper choice raises");
            Debug.Log("[780] supper: " + CardStackView.DebugOpenEntry(d.entryId));
            yield return Check("supper", w, h);
            AssertNoErrors("ask fit");
            CloseSlice();
        }

        [UnityTest, Timeout(900000)] public IEnumerator EveryAskFitsItsCard_750x1334() => AllAsks(750, 1334);
        [UnityTest, Timeout(900000)] public IEnumerator EveryAskFitsItsCard_1179x2556() => AllAsks(1179, 2556);
    }
}
