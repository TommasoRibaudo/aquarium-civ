using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#385: the berry thickets at year 5, 16 and 30 of a cared-for living land, at the default camera, phone portrait.
    /// A scripted Keeper mists round the camp every season and plants the tree when it may (as EcoTests' Keeper). Shots in Temp/agent/bushes/.</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("BushShots")]
    public class BushShots : PlayTestBase
    {
        const string Dir = "bushes/";
        public override void Setup() { base.Setup(); System.IO.Directory.CreateDirectory(Shots + Dir); }

        static void KeeperSeason(SimCore s, int season)
        {
            var map = s.Settlement.Map;
            float cx = map.greenX, cz = map.greenZ;
            if (s.CanPlantTree) s.PlantTree(cx + 0.9f, cz + 0.3f);
            for (int k = 0; k < 3; k++)
            {
                float a = (season * 3 + k) * 2.39996f, r = 0.5f + 0.35f * ((season + k) % 4);
                float x = Mathf.Clamp(cx + Mathf.Cos(a) * r, -2.7f, 2.7f), z = Mathf.Clamp(cz + Mathf.Sin(a) * r, -1.9f, 1.9f);
                s.Mist(x, z, 2f);
            }
            s.CleanGrime(1f);
            s.Prune(1f);
        }

        static IEnumerator Calm()
        {
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Years_5_16_30()
        {
            GameClock.EcologyPref = true;
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            PeopleView.DebugSetHour(10f);
            var s = GameClock.Instance.ActiveSim;
            int season = 0;
            foreach (int year in new[] { 5, 16, 30 })
            {
                while (s.State.month < year * 12) { KeeperSeason(s, season++); s.DebugSkipSeasons(1); }
                EcoDebug.SkipSeasons(0);
                yield return Calm();
                PeopleView.DebugSetHour(10f);
                yield return new WaitForSecondsRealtime(4f);
                var cells = new List<int>(); s.BushCells(cells);
                Debug.Log($"[bushes] year {s.State.month / 12f:0.0} era {s.State.era}: {cells.Count} bushes (cap {s.BushCap}), thickets {s.State.eco.thicketA}/{s.State.eco.thicketB}, trees {s.EcoGrownTrees}");
                yield return Shot(Dir + $"y{year:00}_default");
                Debug.Log("[bushes] " + CampFrameView.DebugCampFrame());
                var rig = Camera.main.GetComponent<BoxCamera>();
                yield return new WaitForSecondsRealtime(0.3f);
                for (float t = 0f; t < 6f && rig.IsFlying; t += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot(Dir + $"y{year:00}_camp");
                CampFrameView.DebugWholeBox();
                yield return new WaitForSecondsRealtime(2f);
            }
        }
    }
}
