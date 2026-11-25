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
                if (Mathf.Abs(q.x) >= Land.HX - 0.005f || !Land.InZ(q.z, 0.005f)) continue; // on the glass: it runs on to the pane
                if (q.y < y - 0.001f) continue; // a wall's foot
                checkedN++;
                worst = Mathf.Max(worst, box.land.Height(q.x, q.z));
            }
            foreach (var q in v)
            {
                Assert.LessOrEqual(Mathf.Abs(q.x), Land.HX + BoxView.WaterFace + 1e-4f, tag + ": no water past the land's edge (x)");
                Assert.That(q.z, Is.InRange(Land.ZMin - BoxView.WaterFace - 1e-4f, Land.ZMax + BoxView.WaterFace + 1e-4f), tag + ": no water past the land's edge (z)");
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
            Vector2 glass = cells.OrderBy(p => Mathf.Min(Land.HX - Mathf.Abs(p.x), Land.GlassGapZ(p.y))).First();
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

        /// <summary>#708 (the user 2026-11-25: "the water level just stops horizontally on the left"): the pond's section through the glass
        /// at a small phone, from the default camera and closer, by day in summer and in winter. The section must slope down from the bank,
        /// no straight cut at its end: at the bank the foot is within 1 cm of the water line. Shots to Temp/agent/708b/.</summary>
        [UnityTest, Timeout(900000), Category("Basin708")]
        public IEnumerator BasinSection()
        {
            const string Dir = "708b/";
            Directory.CreateDirectory(Shots + Dir);
            GameClock.EcologyPref = true;
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(12f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            yield return new WaitForSecondsRealtime(1f);
            var box = Object.FindAnyObjectByType<BoxView>();
            EcoDebug.WaterBreaksNow();
            yield return new WaitForSecondsRealtime(12f);
            PondIce.Force = false;
            float y = box.DebugWaterMesh.vertices.Max(v => v.y); // the water line (the sheet's top)
            Assert.Greater(BoxView.WallFoot(y - 0.002f, y, 0f), y - 0.01f, "at the bank the section is a sliver, not a cut");
            Assert.Less(BoxView.WallFoot(y - 0.002f, y, BoxView.BankRamp), y - BoxView.BasinBelow + 0.001f, "away from the bank it is the full basin");
            var cells = WaterCells(box);
            var glass = cells.OrderBy(p => Mathf.Min(Land.HX - Mathf.Abs(p.x), Land.GlassGapZ(p.y))).First();
            var edge = glass;
            if (Land.HX - Mathf.Abs(glass.x) < Land.GlassGapZ(glass.y)) edge.x = Mathf.Sign(glass.x) * (Land.HX - 0.02f); else edge.y = Land.CZ + Mathf.Sign(glass.y - Land.CZ) * (Land.HZ - 0.02f);
            float yaw = Rig.Home.yaw;
            for (int season = 0; season < 2; season++)
            {
                string tag = season == 0 ? "summer" : "winter";
                if (season == 1) { for (int i = 0; i < 4 && SeasonLook.Word(Sim.State.month) != "Winter"; i++) EcoDebug.SkipToNextSeason(); PondIce.Force = false; yield return new WaitForSecondsRealtime(2f); }
                else { for (int i = 0; i < 4 && SeasonLook.Word(Sim.State.month) != "Summer"; i++) EcoDebug.SkipToNextSeason(); yield return new WaitForSecondsRealtime(2f); }
                PeopleView.DebugSetHour(12f);
                Rig.GoHome(0.01f); yield return new WaitForSecondsRealtime(1.5f); yield return Shot(Dir + tag + "_default");
                yield return Frame(box, edge, yaw, 30f, 0.22f); yield return new WaitForSecondsRealtime(0.5f); yield return Shot(Dir + tag + "_side");
                yield return Frame(box, edge, yaw, 18f, 0.12f); yield return new WaitForSecondsRealtime(0.5f); yield return Shot(Dir + tag + "_low");
            }
            PondIce.Force = null;
        }

        /// <summary>#701 round 2: the pond at the nearest pane from the walkthrough's camera and the default view, with each renderer toggled off in turn.</summary>
        [UnityTest, Timeout(900000), Category("Rim701b")]
        public IEnumerator GlassShots()
        {
            string Dir = "701b/" + (System.Environment.GetEnvironmentVariable("RIM_TAG") ?? "x") + "/";
            Directory.CreateDirectory(Shots + Dir);
            yield return Stage();
            var box = Object.FindAnyObjectByType<BoxView>();
            EcoDebug.WaterBreaksNow();
            yield return new WaitForSecondsRealtime(12f);
            EcoDebug.PondDries();
            yield return new WaitForSecondsRealtime(1f);
            EcoDebug.WaterBreaksNow();
            yield return new WaitForSecondsRealtime(12f);
            PondIce.Force = false;
            yield return new WaitForSecondsRealtime(1.5f);
            var cells = WaterCells(box);
            Log($"{Sim.EcoPondCells} pond cells");
            var glass = cells.OrderBy(p => Mathf.Min(Land.HX - Mathf.Abs(p.x), Land.GlassGapZ(p.y))).First();
            Log("glass cell " + glass);
            var edge = glass;
            if (Land.HX - Mathf.Abs(glass.x) < Land.GlassGapZ(glass.y)) edge.x = Mathf.Sign(glass.x) * (Land.HX - 0.02f); else edge.y = Land.CZ + Mathf.Sign(glass.y - Land.CZ) * (Land.HZ - 0.02f);
            foreach (var r in box.GetComponentsInChildren<MeshRenderer>(true))
                if (r.enabled && r.gameObject.activeInHierarchy && !r.name.StartsWith("Chunk") && !r.name.StartsWith("Live")) Log("renderer " + r.name + " " + (r.sharedMaterial != null ? r.sharedMaterial.name + " q" + r.sharedMaterial.renderQueue : "-") + " b=" + r.bounds.center + r.bounds.size);
            float yawSide = Rig.Home.yaw;
            Rig.minZoom = 0.02f;
            // the walkthrough's view (yaw square to the pane)
            float yaw = Mathf.Abs(glass.x) > Mathf.Abs(glass.y - Land.CZ) * 1.0f && Land.HX - Mathf.Abs(glass.x) < Land.GlassGapZ(glass.y) ? (glass.x > 0 ? 90f : -90f) : (glass.y < Land.CZ ? 0f : 180f);
            Log("yaw " + yaw + " home " + yawSide);
            IEnumerator Both(string tag)
            {
                yield return Frame(box, edge, yawSide, 52f, 0.2f); yield return new WaitForSecondsRealtime(0.4f); yield return Shot(Dir + tag + "_walk_home");
                yield return Frame(box, edge, yaw, 52f, 0.2f); yield return new WaitForSecondsRealtime(0.4f); yield return Shot(Dir + tag + "_walk_square");
                yield return Frame(box, edge, yaw, 35f, 0.08f); yield return new WaitForSecondsRealtime(0.4f); yield return Shot(Dir + tag + "_low_square");
                Rig.GoHome(0.01f); yield return new WaitForSecondsRealtime(1.5f); yield return Shot(Dir + tag + "_default");
            }
            yield return Both("a");
            foreach (var nm in (System.Environment.GetEnvironmentVariable("RIM_OFF") ?? "Water,DampEarth,DampSheen,GlassFront,GlassBack,GlassLeft,GlassRight,Lid,Frame,Base").Split(','))
            {
                var rs = box.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == nm).ToList();
                Log("off " + nm + ": " + rs.Count);
                foreach (var r in rs) r.enabled = false;
                yield return Frame(box, edge, yaw, 52f, 0.2f); yield return new WaitForSecondsRealtime(0.4f); yield return Shot(Dir + "off_" + nm);
                foreach (var r in rs) r.enabled = true;
            }
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
