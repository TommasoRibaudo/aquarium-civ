using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#618 editor pass: the frozen pond for the eye. A camp with a big pond (the first water at the flood line), fish and ducks placed;
    /// the ice forced on: the sheet, a fisher at a hole, the kids sliding, the ducks flying off. Shots (1179x2556 PNG) to Temp/agent/fb8/.
    /// Explicit.</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("Fb8Pass")]
    public class Fb8WinterShots : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        const string Dir = "F:/GameDev/AquariumCiv/Temp/agent/fb8/";

        static IEnumerator Snap(string name)
        {
            Directory.CreateDirectory(Dir);
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Dir + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        [TearDown] public void Thaw() => PondIce.Force = null;

        [UnityTest, Timeout(900000)]
        public IEnumerator FrozenPond_FisherAtAHole_KidsSlide_DucksLeave()
        {
            var stage = new Fb5PassShots();
            yield return stage.Stage(hour: 10f, pond: true, grownUps: 5);
            Debug.Log($"[fb8] pond cells {Sim.EcoPondCells}, big enough {PondIce.BigEnough(Sim)}, inner {PondIce.InnerCount(Sim)}");
            Assert.IsTrue(PondIce.BigEnough(Sim), "the staged pond must be big enough for people on the ice");
            Debug.Log("[fb8] ducks at " + Sim.DebugPlaceNearCamp(LifeSpecies.Ducks) + ", fish at " + Sim.DebugPlaceNearCamp(LifeSpecies.Fish));
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            var pv = PeopleView.Find();
            for (int k = 0; k < 3; k++) { var kid = Sim.DebugBirth(); if (kid != null) kid.age = 7f + k; } // kids for the slide
            pv.Planner.Invalidate();
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(4f);
            var g = Sim.EcoGround;
            var pond = Vector2.zero; int n = 0;
            for (int c = 0; c < EcoLand.N; c++) if (Sim.EcoCoverAt(c) == EcoCover.Water) { pond += new Vector2(AquariumCiv.Sim.Settle.SiteMap.CXc(c), AquariumCiv.Sim.Settle.SiteMap.CZc(c)); n++; }
            pond /= Mathf.Max(1, n);
            Fb5PassShots.Look(pond.x, pond.y, 0.55f, 46f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Snap("618_0_pond_before");

            // the freeze: the ducks fly off as the ice comes
            var life = Object.FindAnyObjectByType<PondLife>();
            Debug.Log("[fb8] " + IceView.DebugIce(true));
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Snap("618_1_ducks_fly_off");
            Debug.Log($"[fb8] ducks away {life?.DucksAway}, flying {life?.DucksFlying}");
            yield return new WaitForSecondsRealtime(3f);
            var ice = IceView.Instance;
            Assert.IsNotNull(ice); Assert.IsTrue(ice.Whole, "the cheat freezes it at once");
            Debug.Log($"[fb8] ice cells {ice.CellsIced}, people on ice {ice.PeopleOnIce}");
            yield return Snap("618_2_frozen");

            // ice fishing
            int sent = pv.Planner.DebugFishNow(pv.Now, 2, 3f);
            Debug.Log("[fb8] fishers sent " + sent);
            bool atHole = false;
            for (int i = 0; i < 80 && !atHole; i++)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                atHole = ice.HolesShown > 0 && Fb5PassShots.Find(a => a.kind == ActivityKind.Fish && !a.moving, out _, out var at) && ice.OnIce(at);
            }
            if (Fb5PassShots.Find(a => a.kind == ActivityKind.Fish && !a.moving, out _, out var fat)) { Fb5PassShots.Look(fat.x, fat.y, 0.28f, 42f); Debug.Log("[fb8] fisher at " + fat + " on ice " + ice.OnIce(fat)); }
            yield return new WaitForSecondsRealtime(2f);
            yield return Snap("618_3_ice_fishing");
            Debug.Log($"[fb8] holes {ice.HolesShown}, at a hole {atHole}");

            // the kids' slide
            Debug.Log("[fb8] " + KidPlayDirector.Debug(PlayGame.IceSlide));
            yield return new WaitForSecondsRealtime(4f);
            float sfx = g.campX, sfz = g.campZ;
            if (pv.Planner.Positions.TryGetPosition(pv.Planner.GatherPlace, out float gpx, out float gpz)) { sfx = gpx; sfz = gpz; } // the cheat plays from the gathering place
            if (PondIce.SlideLine(Sim, sfx, sfz, out float ax, out float az, out float bx, out float bz)) Fb5PassShots.Look((ax + bx) * 0.5f, (az + bz) * 0.5f, 0.32f, 42f);
            Debug.Log($"[fb8] slide A ({ax:0.00},{az:0.00}) B ({bx:0.00},{bz:0.00}) from ({sfx:0.00},{sfz:0.00})");
            for (int t = 0; t < 6; t++)
            {
                var who = new System.Collections.Generic.List<PersonRef>(); var xs = new System.Collections.Generic.List<float>(); var zs = new System.Collections.Generic.List<float>();
                pv.StandingNow(who, xs, zs);
                var line = new System.Text.StringBuilder($"[fb8] t{t}:");
                for (int i = 0; i < who.Count; i++) if (pv.Planner.StageOf(who[i]) == LifeStage.Kid) line.Append($" kid{who[i].Key}@({xs[i]:0.00},{zs[i]:0.00}) ice {ice.OnIce(new Vector2(xs[i], zs[i]))}");
                line.Append(" | " + pv.KidPlay.Status());
                Debug.Log(line.ToString());
                yield return new WaitForSecondsRealtime(0.7f);
            }
            yield return Snap("618_4_kids_slide_a");
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Snap("618_5_kids_slide_b");

            // the thaw: back to the season (not winter in the staged box): the cheat thaws it at once
            Debug.Log("[fb8] " + IceView.DebugIce(false));
            yield return new WaitForSecondsRealtime(4f);
            Fb5PassShots.Look(pond.x, pond.y, 0.55f, 46f);
            yield return new WaitForSecondsRealtime(4f);
            yield return Snap("618_6_thawed_ducks_back");
            Debug.Log($"[fb8] after the thaw: ice whole {ice.Whole}, ducks away {life?.DucksAway}, shown {life?.DucksShown}");
            CloseSlice();
        }
    }
}
