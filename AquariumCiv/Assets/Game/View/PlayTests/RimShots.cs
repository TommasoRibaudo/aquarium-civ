using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#701 (the pond's water must end inside the dug dirt rim) and #704 (the fire's ring must come from the ground under it):
    /// close phone-portrait shots, plus the water-vertex rule. Shots to Temp/agent/701/ and Temp/agent/704/.</summary>
    [Explicit("Screenshots for the eye: run by hand")]
    public class RimShots : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        readonly StringBuilder log = new();
        void Log(string s) { log.AppendLine(s); Debug.Log("[rim] " + s); }

        IEnumerator Stage()
        {
            GameClock.EcologyPref = true;
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(12f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>World-space shot point on a pond shore: the water cell with the most water neighbours missing, nearest a wanted direction.</summary>
        List<Vector2> WaterCells(BoxView box)
        {
            var l = new List<Vector2>();
            for (int c = 0; c < SiteMap.N; c++) { var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)); if (box.InPond(p)) l.Add(p); }
            return l;
        }

        /// <summary>#701: every vertex of the water sheet that is not on the glass has ground under the level by the margin (it ends inside the bank).</summary>
        void AssertWaterInsideBank(BoxView box, string tag)
        {
            var m = box.DebugWaterMesh;
            Assert.IsNotNull(m, tag + ": a water mesh");
            var v = m.vertices;
            Assert.Greater(v.Length, 0, tag + ": the sheet has vertices");
            float y = v.Max(q => q.y), worst = float.MinValue; int checkedN = 0;
            foreach (var q in v)
            {
                if (Mathf.Abs(q.x) >= Land.HX - 0.005f || Mathf.Abs(q.z) >= Land.HZ - 0.005f) continue; // on the glass: it runs on to the pane
                if (q.y < y - 0.001f) continue; // a wall's foot
                checkedN++;
                worst = Mathf.Max(worst, box.land.Height(q.x, q.z));
            }
            Log($"{tag}: {checkedN} water vertices off the glass, highest ground under one {worst:0.000} vs level {y:0.000} (margin {BoxView.RimMargin})");
            Assert.Less(worst, y - BoxView.RimMargin + 0.001f, tag + ": water ends inside the bank");
        }

        IEnumerator Frame(BoxView box, Vector2 at, float yaw, float pitch, float zoom)
        {
            Rig.minZoom = 0.02f;
            for (int i = 0; i < 40; i++)
            {
                var world = box.transform.TransformPoint(box.land.OnGround(at.x, at.y));
                Rig.EaseTo(world, yaw, pitch, zoom, 0.01f);
                yield return null;
            }
        }

        IEnumerator Pond(string dir, string tag, BoxView box, bool wantGlass)
        {
            var cells = WaterCells(box);
            Log($"{tag}: {Sim.EcoPondCells} pond cells, {cells.Count} sampled");
            if (Sim.EcoPondCells > 0) AssertWaterInsideBank(box, tag);
            if (cells.Count == 0) yield break;
            // the rim shots: the shore cell furthest toward the camera (low z), furthest right, and (if asked) the one on the glass
            Vector2 front = cells.OrderBy(p => p.y).First();
            Vector2 side = cells.OrderByDescending(p => p.x).First();
            Vector2 centre = new Vector2(cells.Average(p => p.x), cells.Average(p => p.y));
            Vector2 glass = cells.OrderBy(p => Mathf.Min(Land.HX - Mathf.Abs(p.x), Land.HZ - Mathf.Abs(p.y))).First();
            var spots = new List<(string, Vector2, float, float, float)> { ("a_whole", centre, Rig.Home.yaw, 50f, 0.32f), ("b_front", front, Rig.Home.yaw, 35f, 0.14f), ("c_side", side, Rig.Home.yaw + 40f, 35f, 0.14f) };
            if (wantGlass) spots.Add(("d_glass", glass, Rig.Home.yaw, 35f, 0.14f));
            foreach (var (n, at, yaw, pitch, zoom) in spots)
            {
                yield return Frame(box, at, yaw, pitch, zoom);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(dir + tag + "_" + n);
            }
        }

        [UnityTest, Timeout(900000), Category("Rim701")]
        public IEnumerator PondRims()
        {
            const string Dir = "701/";
            Directory.CreateDirectory(Shots + Dir);
            yield return Stage();
            var box = Object.FindAnyObjectByType<BoxView>();
            Log("first water: " + EcoDebug.WaterBreaksNow());
            yield return new WaitForSecondsRealtime(12f); // the first-water moment plays
            // this seed's basin floods whole (the table steps jump straight to every pond cell), so the sizes reachable are: dry, the full pond,
            // the full pond under ice (#618: ice must meet the new water edge), and after a thaw
            Log("dries: " + EcoDebug.PondDries());
            Assert.AreEqual(0, Sim.EcoPondCells);
            Log("floods: " + EcoDebug.WaterBreaksNow());
            yield return new WaitForSecondsRealtime(12f);
            PondIce.Force = false;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Pond(Dir, "1water", box, true);
            PondIce.Force = true;
            yield return new WaitForSecondsRealtime(4f);
            yield return Pond(Dir, "2ice", box, true);
            PondIce.Force = null;
            File.WriteAllText(Shots + Dir + "log.txt", log.ToString());
        }

        [UnityTest, Timeout(900000), Category("Rim704")]
        public IEnumerator FireRing()
        {
            const string Dir = "704/";
            Directory.CreateDirectory(Shots + Dir);
            yield return Stage();
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            yield return new WaitForSecondsRealtime(2f);
            var box = Object.FindAnyObjectByType<BoxView>();
            Sim.DebugFireStage(2); // #704: the log fire (the grass fire is shot as well, below)
            box.Rebuild();
            yield return new WaitForSecondsRealtime(1f);
            var fire = box.layout.pieces.FirstOrDefault(q => q.kind == PieceKind.Fire);
            Log("era " + Sim.State.era + ", fire piece: " + (fire != null ? $"({fire.x:0.00},{fire.z:0.00}) variant {fire.variant}" : "none"));
            Assert.IsNotNull(fire, "the camp has a fire");
            var at = new Vector2(fire.x, fire.z);
            IEnumerator Close(string name)
            {
                yield return Frame(box, at, Rig.Home.yaw, 38f, 0.06f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot(Dir + name + "_close");
                yield return Frame(box, at, Rig.Home.yaw, 45f, 0.14f);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(Dir + name + "_wide");
            }
            Log("bare/dry: " + EcoDebug.SkipToNextSeason() + " bare " + Sim.State.eco.cover.Count(c => c == (byte)EcoCover.Bare));
            yield return Close("1dry");
            Sim.DebugFireStage(1); box.Rebuild(); yield return new WaitForSecondsRealtime(1f);
            yield return Close("1dry_grassfire");
            Sim.DebugFireStage(2); box.Rebuild(); yield return new WaitForSecondsRealtime(1f);
            foreach (var (x, z) in new[] { (fire.x, fire.z), (fire.x + 0.5f, fire.z), (fire.x - 0.5f, fire.z + 0.3f), (fire.x, fire.z - 0.5f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(2);
            yield return new WaitForSecondsRealtime(2f);
            Log("greener: " + EcoDebug.Status());
            yield return Close("2grass");
            for (int i = 0; i < 4 && SeasonLook.Word(Sim.State.month) != "Winter"; i++) EcoDebug.SkipToNextSeason();
            Log("season " + SeasonLook.Word(Sim.State.month));
            yield return new WaitForSecondsRealtime(2f);
            yield return Close("3snow");
            File.WriteAllText(Shots + Dir + "log.txt", log.ToString());
        }
    }
}
