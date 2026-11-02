using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Tester run 7's polish: what a test can hold, and the shots the eye has to judge.</summary>
    [Category("Polish"), Category("Bundle3")]
    public class PolishPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;

        [UnityTest, Timeout(60000)]
        public IEnumerator TheRingSitsOnTheRainButtonsMiddle()
        {
            yield return OpenSlice(seed: Seed, skipFirstRun: false);
            var first = FirstRunView.Instance;
            first.Run.DebugSkipTo(FirstRunStep.Rain);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(FirstRunStep.Rain, first.Step);
            var ring = first.Root.Q("firstrun-ghost-ring");
            var button = first.Root.Q("firstrun-rain");
            Assert.AreNotEqual(0f, button.worldBound.width, "the button is up");
            yield return Shot("bundle3-ring");
            Assert.AreEqual(button.worldBound.center.x, ring.worldBound.center.x, 14f, "centred across");
            Assert.AreEqual(button.worldBound.center.y, ring.worldBound.center.y, 14f, "and up and down");
            AssertNoErrors("ring");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ThePillStaysOffABuildingsCardAndTheStallsShowAtDefaultZoom()
        {
            yield return OpenSlice(seed: Seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Sim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(10f);
            yield return Frames(6);
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(0.7f);
            var cam = Camera.main.GetComponent<BoxCamera>();
            var cards = PersonCardView.Instance;
            var b = Sim.State.buildings.Last(x => Sim.Settlement.Lots.Any(l => l.buildingId == x.id && !l.retired && x.type == BuildingType.Hut && l.w > 0f));
            Assert.IsTrue(cards.OpenBuilding(b.id));
            yield return Frames(3);
            Assert.IsTrue(MomentPlayer.PromptsBlocked(), "no prompt under a building's card");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("bundle3-mark");
            cards.CloseBuilding();
            var box = Object.FindAnyObjectByType<BoxView>();
            var stall = box.layout.pieces.First(p => p.kind == PieceKind.Stall && p.era >= Era.Town);
            cam.GoHome(0.05f);
            yield return new WaitForSecondsRealtime(0.4f);
            foreach (var p in box.layout.pieces.Where(p => p.kind == PieceKind.Stall && p.era >= Era.Town))
                Debug.Log($"[stalls] at {Camera.main.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(p.x, 1f, p.z)))} yaw {p.yaw}");
            foreach (var how in new[] { "empty", "half", "full" })
            {
                Debug.Log("[stalls] " + StallProps.DebugStock(how));
                yield return Frames(3);
                yield return Shot("bundle3-stalls-" + how);
            }
            cam.EaseTo(box.transform.TransformPoint(new Vector3(stall.x, 1f, stall.z)), stall.yaw, cam.pitch, 0.5f, 0.05f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("bundle3-stalls-close");
            AssertNoErrors("pill and stalls");
        }

        /// <summary>#197, for the eye and the numbers: nobody piles up or stands in a wall at village midday, work day and rest day.</summary>
        [UnityTest, Timeout(120000), Category("Midday")]
        public IEnumerator VillageMiddayByTheNumbers([Values(1234, 7531)] int seed)
        {
            yield return OpenSlice(seed: seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(14f);
            yield return Frames(3);
            Sim.CleanAll();
            var pv = PeopleView.Find();
            var cam = Camera.main.GetComponent<BoxCamera>();
            cam.EaseTo(Vector3.zero + Vector3.up * 1f, cam.yaw, 45f, 0.6f, 0.05f);
            if (LifeClock.IsRestDay(Sim.State.month)) Sim.State.month++;
            PeopleView.DebugSetHour(12.5f);
            yield return new WaitForSecondsRealtime(0.7f);
            Audit(pv, seed, "work day 12.5");
            yield return Shot($"bundle3-197-{seed}-workday");
            while (!LifeClock.IsRestDay(Sim.State.month)) Sim.State.month++; // the view's day is the month
            Sim.CleanAll();
            PeopleView.DebugSetHour(12.9f);
            yield return new WaitForSecondsRealtime(0.7f);
            Audit(pv, seed, $"rest day {LifeClock.IsRestDay(pv.Now.day)} 12.9");
            yield return Shot($"bundle3-197-{seed}-restday");
            AssertNoErrors("midday");
        }

        static void Audit(PeopleView pv, int seed, string when)
        {
            var at = new System.Collections.Generic.List<(string who, string what, Vector2 p)>();
            foreach (var a in pv.Activities)
                if (pv.TryGetDollPosition(a.who, out var p) && pv.TryGetDollFeet(a.who, out _)) at.Add((a.who.ToString(), pv.Describe(a), p));
            int crowd = 0, close = 0, inWall = 0;
            var wall = new System.Text.StringBuilder();
            foreach (var a in at)
            {
                int near = at.Count(b => (b.p - a.p).magnitude < 0.3f);
                crowd = Mathf.Max(crowd, near);
                foreach (var b in at) if (string.CompareOrdinal(a.who, b.who) < 0 && (b.p - a.p).magnitude < 0.07f) close++;
                foreach (var l in Sim.Settlement.Lots.Where(l => !l.retired && l.buildingId > 0 && l.kind != AquariumCiv.Sim.Settle.LotKind.Farm && l.kind != AquariumCiv.Sim.Settle.LotKind.Pasture && l.kind != AquariumCiv.Sim.Settle.LotKind.Orchard))
                {
                    float r = l.yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
                    float dx = a.p.x - l.x, dz = a.p.y - l.z, lx = dx * c - dz * s, lz = dx * s + dz * c;
                    if (Mathf.Abs(lx) < l.w * 0.3f && Mathf.Abs(lz) < l.d * 0.3f) { inWall++; wall.Append($" [{a.who} {a.what} in {l.kind}]"); }
                }
            }
            string doing = string.Join(", ", at.GroupBy(a => a.what.Split(' ')[0]).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key} {g.Count()}"));
            Debug.Log($"[midday] seed {seed} {when} day {pv.Now.day}: {at.Count} shown, biggest group within 0.3 = {crowd}, pairs under 0.07 = {close}, in a wall = {inWall}{wall}; {doing}");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheMuseumKeepsItsWoodsAndPond()
        {
            yield return OpenSlice(1080, 1920, Seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Sim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(30f);
            yield return Frames(3);
            foreach (var id in RelicId.All) Sim.DebugGiveRelic(id);
            yield return Frames(3);
            Assert.That(BoxCompleteController.DebugComplete(), Does.StartWith("box completed"));
            yield return Frames(8);
            var first = FirstRunView.Instance;
            for (int i = 0; i < 12 && first.Active; i++) { first.Run.SkipStep(); yield return Frames(2); }
            var c = BoxCompleteController.Instance;
            Assert.IsTrue(c.OpenShelf(), "the shelf");
            yield return new WaitForSecondsRealtime(1.5f);
            var shelf = Object.FindAnyObjectByType<ShelfProps>();
            var mesh = shelf.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == "Town");
            Debug.Log($"[shelf] the museum's town has {mesh.sharedMesh.vertexCount} vertices");
            yield return Shot("bundle3-shelf");
            ShowMeDirector.BubblesOn = true;
            Assert.AreEqual("the shelf", ShowMeDirector.Instance.QuietReason(), "no bubble over the shelf");
            AssertNoErrors("shelf");
        }
    }
}
