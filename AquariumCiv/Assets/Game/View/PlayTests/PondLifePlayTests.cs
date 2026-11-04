using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P8 #279 and #286 at a phone size: the pond's first water is a moment (sparkles, a soft sound, people walk over, a line, the
    /// camera leans in; a short version when it came while the player was away), and the pond has frogs (more at night, they
    /// hop away from people) and, from 20 cells, ducks. The water comes by the debug cheat that raises the water table and
    /// passes seasons, the same path as play. Screenshots go to Temp/agent/pond_*.png.
    /// </summary>
    [Category("PondLife")]
    public class PondLifePlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.Sim;

        IEnumerator Fly(BoxView box, float x, float z, float zoom)
        {
            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.FlyTo(box.transform.TransformPoint(box.land.OnGround(x, z)), Mathf.Max(rig.minZoom * zoom, 0.25f), 0.4f);
            for (int i = 0; i < 200 && rig.IsFlying; i++) yield return null;
            yield return Frames(10);
        }

        static int PeopleNear(PeopleView pv, float x, float z, float r)
        {
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            int n = 0;
            for (int i = 0; i < who.Count; i++) if ((xs[i] - x) * (xs[i] - x) + (zs[i] - z) * (zs[i] - z) <= r * r) n++;
            return n;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator FirstWater_IsAMoment_OnceOnly()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            var box = Object.FindAnyObjectByType<BoxView>();
            var fw = FirstWaterView.Instance;
            var pv = PeopleView.Find();
            Assert.IsNotNull(fw, "the first-water view is on the box");
            Assert.AreEqual(0, Sim.EcoPondCells, "a dry hollow to start");
            PeopleView.DebugSetHour(10f);
            yield return Frames(20);
            yield return Fly(box, 0f, 0f, 6f); // far out, so the lean has somewhere to go
            Sfx.LastAsked.Clear();
            StringAssert.Contains("broke the surface", EcoDebug.WaterBreaksNow());
            Assert.Greater(Sim.EcoPondCells, 0, "water stands");
            yield return Until(() => fw.Playing && fw.SparklesShown > 0, 10f);
            Assert.AreEqual(1, fw.FullPlays, "the moment began");
            Assert.Greater(fw.SparklesShown, 3, "sparkles on the new water");
            CollectionAssert.Contains(Sfx.LastAsked, CueLibrary.FirstWaterDrop, "a soft water sound");
            Assert.IsTrue(fw.PondSpot(out float px, out float pz));
            int before = PeopleNear(pv, px, pz, 0.7f);
            yield return Shot("pond_first_water_start");
            pv.timeScale = 3f;
            yield return Until(() => fw.PeopleSent > 0, 5f);
            yield return Until(() => PeopleNear(pv, px, pz, 0.7f) > before, 14f);
            Assert.Greater(PeopleNear(pv, px, pz, 0.7f), before, "people walked over to look");
            yield return Shot("pond_first_water_people");
            yield return Until(() => !string.IsNullOrEmpty(fw.LastLine), 12f);
            Assert.IsNotEmpty(fw.LastLine, "the glass voice gave a line");
            Assert.GreaterOrEqual(fw.Leans, 1, "the camera leaned in");
            yield return Until(() => !fw.Playing, 40f);
            Assert.AreEqual(0, fw.SparklesShown, "the sparkles are gone");
            pv.timeScale = 1f;
            EcoDebug.SkipSeasons(3);
            yield return Frames(30);
            Assert.AreEqual(1, fw.FullPlays, "once per land");
            Assert.AreEqual(0, fw.AwayPlays);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator FirstWater_WhileAway_ShortVersionOnReturn()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            var fw = FirstWaterView.Instance;
            Assert.AreEqual(0, Sim.EcoPondCells);
            Sim.DebugSetTable(Sim.EcoFloodLine);
            int n = 0;
            while (Sim.EcoPondCells == 0 && n++ < 40) Sim.CatchUp(48);
            Assert.Greater(Sim.EcoPondCells, 0, $"water came while away (after {n} catch-ups of 48 h)");
            yield return Until(() => fw.AwayPlays > 0, 5f);
            Assert.AreEqual(1, fw.AwayPlays, "the short version on return");
            Assert.AreEqual(0, fw.FullPlays, "not the full one");
            yield return Until(() => fw.SparklesShown > 0, 5f);
            Assert.Greater(fw.SparklesShown, 0);
            yield return Until(() => !string.IsNullOrEmpty(fw.LastLine), 12f);
            CollectionAssert.Contains(FirstWaterView.AwayLines, fw.LastLine);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Frogs_MoreAtNight_HopFromPeople_Ducks()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            var box = Object.FindAnyObjectByType<BoxView>();
            var pl = PondLife.Instance;
            Assert.IsNotNull(pl, "pond life is on the box");
            EcoDebug.WaterBreaksNow();
            int cells = Sim.EcoPondCells;
            for (int i = 0; i < 8 && LifeClock.SeasonOf(Sim.State.month) == Season.Winter; i++) EcoDebug.SkipSeasons(1); // frogs sleep in winter
            PeopleView.DebugSetHour(12f);
            yield return Frames(40);
            yield return Until(() => pl.FrogsSitting > 0, 15f);
            int day = pl.FrogsSitting;
            Assert.Greater(day, 0, "a frog by day");
            PeopleView.DebugSetHour(21f);
            yield return Frames(30);
            yield return Until(() => pl.FrogsSitting > day, 30f);
            int night = pl.FrogsSitting;
            Assert.Greater(night, day, $"more frogs at night ({night}) than by day ({day}); pond {cells} cells");
            Assert.Greater(pl.PadsShown, 0, "lily pads on the pond");
            var spots = new List<Vector3>();
            pl.FrogSpots(spots);
            var s0 = spots[0];
            yield return Fly(box, s0.x, s0.z, 1.2f);
            yield return Shot("pond_frogs_night");
            int hops = pl.FrogHops;
            pl.Scarers.Add(new Vector2(s0.x + 0.05f, s0.z)); // someone comes close
            yield return Until(() => pl.FrogHops > hops, 5f);
            Assert.Greater(pl.FrogHops, hops, "a frog hopped into the water when someone came near");
            pl.Scarers.Clear();

            int natural = pl.WantedDucks;
            Assert.AreEqual(cells >= PondLife.TwoDucksPondCells ? 2 : cells >= PondLife.DuckPondCells ? 1 : 0, natural, $"ducks by pond size ({cells} cells)");
            string msg = EcoDebug.DucksNow();
            yield return Until(() => pl.DucksShown > 0, 5f);
            Assert.Greater(pl.DucksShown, 0, msg);
            var ds = new List<Vector2>();
            pl.DuckSpots(ds);
            PeopleView.DebugSetHour(11f);
            yield return Fly(box, ds[0].x, ds[0].y, 1.2f);
            yield return Shot("pond_ducks");
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Debug.Log($"[PondLife] pond {cells} cells, frogs day {day} night {night}, natural ducks {natural}, ducks shown {pl.DucksShown}");
        }
    }
}
