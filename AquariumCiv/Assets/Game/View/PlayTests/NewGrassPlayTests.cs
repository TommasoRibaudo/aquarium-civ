using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P8 #281 phase 2: grass new since the last visit is drawn a lighter, fresher spring green that fades to the normal
    /// colouring over the session; grass already seen keeps the normal colour. Time passes by the same cheats a player's
    /// away does (mark seen, then seasons pass). Screenshots go to Temp/agent/newgrass_*.png.
    /// </summary>
    [Category("NewGrass")]
    public class NewGrassPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.Sim;

        static bool Green(SimCore s, int c)
        {
            byte cv = s.State.eco.cover[c];
            return cv != (byte)EcoCover.Bare && cv != (byte)EcoCover.Water;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator NewGrass_IsFresh_ThenFadesAndCommits()
        {
            UseLivingLand();
            float speedWas = EcoSurface.FadeSpeed;
            try
            {
                yield return OpenSlice(seed: 4242);
                Assert.IsTrue(Sim.EcologyOn);
                var surf = EcoSurface.Instance;
                Assert.IsNotNull(surf, "the ground-look overlays are on the box");
                yield return Frames(20);
                Assert.IsTrue(Sim.HasLandSeenMask, "a fresh box has its mask from the start");
                Assert.AreEqual(0, Sim.LandNewCellCount, "nothing is new on a fresh box");

                EcoDebug.Humidity(0.8f);
                for (int i = 0; i < 30; i++) { int g = 0; for (int c = 0; c < EcoLand.N; c++) if (Green(Sim, c)) g++; if (g >= 30) break; EcoDebug.SkipSeasons(1); }
                Sim.MarkLandSeen();
                var old = new System.Collections.Generic.List<int>();
                for (int c = 0; c < EcoLand.N; c++) if (Green(Sim, c)) old.Add(c);
                Assert.Greater(old.Count, 0, "some grass to start from");
                for (int i = 0; i < 24 && Sim.LandNewCellCount < 3; i++) EcoDebug.SkipSeasons(1);
                if (Sim.LandNewCellCount < 3) Sim.DebugPretendAwayWithNewGrass(12);
                var fresh = new System.Collections.Generic.List<int>();
                for (int c = 0; c < EcoLand.N; c++) if (Green(Sim, c) && Sim.LandFreshness(c, 0f) > 0.99f) fresh.Add(c);
                Assert.GreaterOrEqual(fresh.Count, 3, "grass grew while 'away'");
                surf.RestartFade();
                EcoSurface.FadeSpeed = 1f;
                yield return Frames(30);
                Assert.Greater(surf.NewGrassCells, 0, "new grass is drawn fresh");
                foreach (int c in fresh) Assert.Greater(surf.NewGrassShown(c), 0.9f, "a new cell is fully fresh on return");
                foreach (int c in old) if (!fresh.Contains(c)) Assert.AreEqual(0f, surf.NewGrassShown(c), 0.001f, "seen grass keeps the normal colour");
                Assert.Less(surf.LastBuildMs, 20.0, "the overlay rebuild stays inside a frame");
                Debug.Log($"[newgrass] fresh {fresh.Count} old {old.Count} overlay cells {surf.NewGrassCells} build {surf.LastBuildMs:0.00} ms");
                yield return Shot("newgrass_on_return");

                // the fade: at 45 s in, about half; speed it up to get to the end
                EcoSurface.FadeSpeed = 45f;
                yield return Until(() => surf.LandSeconds >= 80f, 8f);
                float mid = surf.NewGrassShown(fresh[0]);
                Assert.That(mid, Is.GreaterThan(0.05f).And.LessThan(0.95f), "part-way through the fade");
                yield return Shot("newgrass_mid_fade");
                float worst = 0f;
                for (int f = 0; f < 600 && Sim.LandNewCellCount > 0; f++) { worst = Mathf.Max(worst, Time.unscaledDeltaTime); yield return null; }
                Assert.AreEqual(0, Sim.LandNewCellCount, "the fade ended and the mask committed");
                yield return Frames(10);
                foreach (int c in fresh) Assert.AreEqual(0f, surf.NewGrassShown(c), 0.001f, "back to the normal colouring");
                Assert.Less(worst, 0.2f, "no long frame from the tint");
                yield return Shot("newgrass_after_fade");

                // an old save has no mask: nothing is fresh, nothing is shown
                Sim.State.eco.seen = null;
                Assert.IsFalse(Sim.HasLandSeenMask);
                Assert.AreEqual(0, Sim.LandNewCellCount);
                Assert.AreEqual(0f, Sim.LandFreshness(fresh[0], 0f));
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { EcoSurface.FadeSpeed = speedWas; }
        }
    }
}
