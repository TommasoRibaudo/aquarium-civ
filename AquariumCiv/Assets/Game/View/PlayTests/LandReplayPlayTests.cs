using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#287 phase 2: History shows the land as it was. A seeded living-land box grows (time passing and the Keeper's mist), the
    /// replay scrubs from year 0 to now and the drawn cover greens, the live land comes back on closing, and a box without land
    /// records (or with the ecology off) replays as before. Shots at 1179x2556 in Temp/agent/land_*.png.</summary>
    [Category("TimeLapsePlay")]
    public class LandReplayPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static float Green(byte[] cover) => cover.Count(c => c >= EcoLayer.Grass && c <= EcoLayer.Reeds) / (float)cover.Length;

        IEnumerator GrowLiving(int years)
        {
            var map = Sim.Settlement.Map;
            for (int i = 0; i < years; i++)
            {
                Sim.Mist(map.greenX + 0.5f, map.greenZ, 6f);
                SimLayoutSource.DebugSkipYears(1f);
                if (i % 4 == 0) yield return null;
            }
            yield return null; yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ScrubbingYearZeroToNowShowsTheCoverGrowingAndTheLiveLandComesBack()
        {
            bool prefWas = GameClock.EcologyPref;
            UseLivingLand(true);
            try
            {
                yield return OpenSlice(seed: 4242);
                Assert.IsTrue(Sim.EcologyOn, "a living-land box");
                yield return GrowLiving(14);
                var box = Object.FindAnyObjectByType<BoxView>();
                box.Refresh();
                var liveCover = (byte[])box.layout.eco.cover.Clone();
                float liveGreen = Green(liveCover);
                Assert.Greater(liveGreen, 0.02f, "the box grew some green");

                // Open History by touch, as a player does.
                var hud = GameHud.Instance;
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
                var tl = TimeLapseView.Find();
                yield return Until(() => tl != null && tl.Active, 3f);
                Assert.IsTrue(tl.Active, "History opened the replay");
                yield return Frames(4);

                float end = tl.Lapse.EndPosition;
                float Drawn() => Green(box.layout.eco.cover);
                tl.Seek(0f); yield return Frames(8);
                float g0 = tl.DrawnGreenShare, d0 = Drawn();
                yield return Shot("land_year0");
                tl.Seek(end * 0.5f); yield return Frames(8);
                float gm = tl.DrawnGreenShare, dm = Drawn();
                yield return Shot("land_mid");
                tl.Seek(end); yield return Frames(12);
                float gn = tl.DrawnGreenShare, dn = Drawn();
                yield return Shot("land_now");
                Debug.Log($"[land] green share year0 {g0:0.00}/{d0:0.00}, mid {gm:0.00}/{dm:0.00}, now {gn:0.00}/{dn:0.00}, live {liveGreen:0.00}");
                Assert.GreaterOrEqual(g0, 0f, "the replay carries the land");
                Assert.Less(g0, 0.05f, "year 0 is barren");
                Assert.Greater(gm, g0 + 0.01f, "mid way it has greened");
                Assert.Greater(gn, gm - 0.001f, "and more by now");
                Assert.Greater(dn, d0 + 0.02f, "the drawn cover (what the box was built from) grew from year 0 to now");
                Assert.AreEqual(liveGreen, dn, 0.03f, "now is the live land");

                // A sweep stays cheap in chunks (the cap holds with the land changing too).
                int widest = 0;
                for (float y = 0f; y <= end; y += 0.5f) { tl.Seek(y); widest = Mathf.Max(widest, tl.LastChunks); yield return null; }
                Assert.LessOrEqual(widest, BoxView.ScrubChunksPerFrame);

                tl.Close();
                yield return Frames(6);
                box.Refresh();
                Assert.IsNull(box.layout.eco.greenMix, "no replay tint left");
                CollectionAssert.AreEqual(liveCover, box.layout.eco.cover, "the live land came back");

                // A box whose history has no land records plays as before: no replayed land, the old look.
                Sim.State.history.land.Clear();
                Assert.IsTrue(tl.Open(), "an old box still opens");
                yield return Frames(4);
                tl.Seek(0f); yield return Frames(4);
                Assert.AreEqual(-1f, tl.DrawnGreenShare, "no land to draw");
                tl.Seek(end * 0.5f); yield return Frames(4);
                tl.Close();
                yield return Frames(6);
                AssertNoErrors("land replay");
            }
            finally { GameClock.EcologyPref = prefWas; CloseSlice(); }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator EcologyOffReplaysAsBefore()
        {
            UseLivingLand(false);
            yield return OpenSlice(seed: 7903);
            for (int i = 0; i < 12; i++) { SimLayoutSource.DebugSkipYears(1f); if (i % 4 == 0) yield return null; }
            Sim.CleanAll();
            var tl = TimeLapseView.Ensure();
            Assert.IsTrue(tl.Open());
            tl.Seek(0f); yield return Frames(4);
            Assert.AreEqual(-1f, tl.DrawnGreenShare);
            Assert.IsFalse(Object.FindAnyObjectByType<BoxView>().layout.eco.on);
            tl.Seek(6f); yield return Frames(4);
            tl.Close(); yield return Frames(4);
            AssertNoErrors("ecology off");
            CloseSlice();
        }
    }
}
