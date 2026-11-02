using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#150 on the real Slice scene: the pile by the fire follows the stock, nobody stands in it, and it goes with the camp.</summary>
    [Category("CampStashPlay")]
    public class CampStashPlayTests : PlayTestBase
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ThePileFollowsTheStockAndGoesWithTheCamp()
        {
            yield return OpenSlice(seed: 7531);
            var sim = GameClock.Instance.ActiveSim;
            Assert.AreEqual(Era.Camp, sim.State.era);
            var props = GlassVoice.Instance.GetComponent<StashProps>();
            var pv = PeopleView.Find();
            yield return Frames(4);
            Assert.IsTrue(props.Visible, "a camp has a stash");
            Assert.IsTrue(pv.Planner.TryGetStash(out var pile));

            StashProps.DebugStock(sim, false);
            yield return Frames(3);
            int lowLogs = props.LogsShown, lowBaskets = props.BasketsShown;
            Assert.Greater(lowLogs, 0, "a low stock still shows");
            Assert.Greater(lowBaskets, 0);
            Assert.AreEqual(0, props.HidesShown, "hides join in the upper half of the food");

            StashProps.DebugStock(sim, true);
            yield return Frames(3);
            Assert.AreEqual(CampStash.MaxLogs, props.LogsShown);
            Assert.AreEqual(CampStash.MaxBaskets, props.BasketsShown);
            Assert.AreEqual(CampStash.MaxHides, props.HidesShown);
            foreach (Transform t in props.Root)
            {
                if (!t.gameObject.activeSelf) continue;
                Assert.LessOrEqual(new Vector2(t.localPosition.x - pile.x, t.localPosition.z - pile.z).magnitude, CampStash.PileExtent, t.name + " stays on the pile");
            }

            // Carriers come and go through a few days. A carry is a walk that ends at the stand, so someone stopped there is rare:
            // those stand round the pile, not in it. The walk in is measured too.
            pv.timeScale = 20f;
            int stopped = 0, walking = 0, overPile = 0;
            var centre = new Vector2(pile.x, pile.z);
            for (int i = 0; i < 1500; i++)
            {
                foreach (var a in pv.Activities.Where(a => a.place.kind == PlaceKind.Stash))
                {
                    if (!pv.TryGetDollPosition(a.who, out var at)) continue;
                    float d = (at - centre).magnitude;
                    if (a.moving || pv.IsWalking(a.who)) { walking++; if (d < CampStash.PileExtent) overPile++; continue; }
                    stopped++;
                    Assert.GreaterOrEqual(d, CampStash.PileExtent, a.who + " stands off the pile");
                }
                yield return null;
            }
            pv.timeScale = 1f;
            TestContext.WriteLine($"at the stash: {stopped} doll frames stopped; on the way {walking}, of them {overPile} over the pile");
            Assert.Greater(stopped + walking, 0, "carriers came");

            SimLayoutSource.DebugNextEra(false);
            yield return Frames(8);
            Assert.IsFalse(props.Visible, "no stash after the camp");
            AssertNoErrors("stash");
        }
    }

    /// <summary>The pictures for the bundle of 2026-10-31: stash, buckets, gift reactions, crowd card and the Show me bar, to Temp/agent/bundle,
    /// at both phone sizes. Run by hand: <c>--filter BundleShots --filter_type category --include_explicit true --async_tests true</c>.</summary>
    [Explicit("Writes images; run by hand"), Category("BundleShots")]
    public class BundleShots : PlayTestBase
    {
        const string Dir = "bundle/";

        static IEnumerator Fly(System.Func<BoxCamera, bool> go)
        {
            var cam = Camera.main.GetComponent<BoxCamera>();
            Assert.IsTrue(go(cam), "a place to fly to");
            yield return new WaitForSecondsRealtime(0.9f);
        }

        /// <summary>The view clock runs until someone has this prop, then nearly stops with the camera on them.</summary>
        static IEnumerator CloseUpOf(Prop prop, string name)
        {
            var pv = PeopleView.Find();
            pv.timeScale = 8f;
            PersonRef who = PersonRef.None;
            yield return Until(() =>
            {
                foreach (var a in pv.Activities) if (a.prop == prop && pv.TryGetDollPosition(a.who, out _)) { who = a.who; return true; }
                return false;
            }, 60f);
            pv.timeScale = 0.02f;
            Assert.IsFalse(who.IsNone, "somebody carries " + prop);
            yield return Fly(c => c.FlyToPerson(who, 0.3f, 0.5f));
            yield return Shot(Dir + name);
            pv.timeScale = 1f;
        }

        static IEnumerator Gift(string kind, float x, float z, string name, float wait = 16f)
        {
            var pv = PeopleView.Find();
            PeopleView.DebugSetHour(10f);
            yield return null;
            string r = ShapingView.DebugPlace(kind, x, z);
            TestContext.WriteLine($"{kind}: {r}");
            var sim = GameClock.Instance.ActiveSim;
            int id = sim.State.nudges[sim.State.nudges.Count - 1].id;
            var reactions = new System.Collections.Generic.List<RelicMomentInfo>();
            yield return Until(() => pv.Planner.ActiveShapingReactions(pv.Now, reactions) > 0, 8f);
            float left = reactions.Count > 0 ? LifeClock.RealSecondsBetween(pv.Now.hour, reactions[0].end) : wait;
            TestContext.WriteLine($"{kind}: the reaction has {left:0.0} s left, cast {reactions.FirstOrDefault()?.cast.Count}");
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, left - 2.5f)); // near its end: whoever walks over is there
            TestContext.WriteLine($"{kind}: {pv.Planner.ActiveShapingReactions(pv.Now, reactions)} reaction(s), cast {(reactions.Count > 0 ? reactions[0].cast.Count : 0)}");
            var land = pv.GetComponent<BoxView>().land;
            PersonRef nearest = PersonRef.None;
            if (reactions.Count > 0)
            {
                pv.Planner.Positions.TryGetPosition(reactions[0].spot, out float gx, out float gz);
                float best = float.MaxValue;
                foreach (var (who, role) in reactions[0].cast)
                {
                    if (!pv.TryGetDollPosition(who, out var at)) continue;
                    float d = (at - new Vector2(gx, gz)).magnitude;
                    if (d < best) { best = d; nearest = who; }
                    TestContext.WriteLine($"  {who} {role}: {d:0.00} from the gift, in water {land.IsWater(at.x, at.y)}, {pv.Planner.Plan(who, pv.Now).kind}");
                }
            }
            if (nearest.IsNone) yield return Fly(c => c.FlyToPlace(Place.Land(PlaceKind.Nudge, id), 0.45f, 0.5f));
            else { pv.timeScale = 0.02f; yield return Fly(c => c.FlyToPerson(nearest, 0.3f, 0.5f)); }
            yield return Shot(Dir + name);
            pv.timeScale = 1f;
        }

        IEnumerator Camp(int w, int h)
        {
            System.IO.Directory.CreateDirectory(Shots + Dir);
            yield return OpenSlice(w, h, seed: 7531);
            string size = "_" + w;
            ShowMeDirector.BubblesOn = false;
            var sim = GameClock.Instance.ActiveSim;
            sim.CleanAll();
            PeopleView.DebugSetHour(11f);
            yield return Frames(10);

            yield return Fly(c => c.FlyToPlace(Place.Land(PlaceKind.Fire), 0.3f, 0.5f));
            StashProps.DebugStock(sim, false);
            yield return Frames(4);
            yield return Shot(Dir + "stash_low" + size);
            StashProps.DebugStock(sim, true);
            yield return Frames(4);
            yield return Shot(Dir + "stash_high" + size);

            yield return CloseUpOf(Prop.EmptyBucket, "bucket_empty" + size);
            yield return CloseUpOf(Prop.Buckets, "bucket_full" + size);

            sim.DebugRefillShaping();
            var open = ShapingLand(sim, 0.9f, 0.5f);
            yield return Gift(SimCore.NudgeKind.Grove, open.x, open.y, "gift_grove" + size);
            AssertNoErrors("camp " + w);
        }

        /// <summary>A box of its own for each gift: a five-person camp held in one reaction has nobody left for the next.</summary>
        IEnumerator OneGift(string kind, System.Func<SimCore, Vector2> where, string name, float wait)
        {
            System.IO.Directory.CreateDirectory(Shots + Dir);
            yield return OpenSlice(seed: 7531);
            ShowMeDirector.BubblesOn = false;
            var sim = GameClock.Instance.ActiveSim;
            sim.CleanAll();
            yield return Frames(10);
            var at = where(sim);
            yield return Gift(kind, at.x, at.y, name, wait);
            AssertNoErrors(name);
        }

        IEnumerator Later(int w, int h)
        {
            System.IO.Directory.CreateDirectory(Shots + Dir);
            yield return OpenSlice(w, h, seed: 7531);
            string size = "_" + w;
            ShowMeDirector.BubblesOn = false;
            var sim = GameClock.Instance.ActiveSim;
            for (int y = 0; y < 3 && sim.CrowdCount < 6; y++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            sim.CleanAll();
            PeopleView.DebugSetHour(11f);
            yield return new WaitForSecondsRealtime(1.5f);
            var cards = PersonCardView.Instance;
            TestContext.WriteLine($"crowd {sim.CrowdCount}: " + PersonCardView.DebugOpenCrowd());
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsTrue(cards.CrowdOpen, "a crowd card");
            yield return Shot(Dir + "crowd_card" + size);
            cards.CloseCrowd();
            yield return Frames(30);

            Assert.That(CardStackView.DebugReturn(5), Does.StartWith("cards"));
            yield return Frames(4);
            Assert.IsTrue(CardStackView.Instance.Model.TapPeek());
            yield return Frames(30);
            yield return Shot(Dir + "cards_open" + size);
            Assert.AreEqual("card text: two", CardStackView.DebugText("two"));
            yield return Frames(6);
            yield return Shot(Dir + "cards_two" + size);
            Assert.That(CardStackView.DebugShowMe(), Does.Contain("the bar is up"));
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Shot(Dir + "showme_bar" + size);
            AssertNoErrors("later " + w);
        }

        static Vector2 ShapingLand(SimCore sim, float x, float z)
        {
            // Open ground near a point: the tray's own check says where a tool may go.
            for (float r = 0f; r < 2f; r += 0.1f)
                for (int a = 0; a < 12; a++)
                {
                    float px = x + Mathf.Cos(a * 0.52f) * r, pz = z + Mathf.Sin(a * 0.52f) * r;
                    if (sim.HerdVariantAt(px, pz) == "deer" && sim.Settlement != null && sim.Settlement.Map != null) return new Vector2(px, pz);
                }
            return new Vector2(x, z);
        }

        [UnityTest, Timeout(200000)] public IEnumerator CampTall() => Camp(1179, 2556);
        [UnityTest, Timeout(200000)] public IEnumerator CampShort() => Camp(750, 1334);
        [UnityTest, Timeout(200000)] public IEnumerator LaterTall() => Later(1179, 2556);
        [UnityTest, Timeout(200000)] public IEnumerator LaterShort() => Later(750, 1334);

        [UnityTest, Timeout(120000)]
        public IEnumerator GiftHerd() => OneGift(SimCore.NudgeKind.Herd, s => ShapingLand(s, 0.9f, 0.5f), "gift_herd_1179", 16f);

        [UnityTest, Timeout(120000)]
        public IEnumerator GiftRain() => OneGift(SimCore.NudgeKind.Rain, s => ShapingLand(s, 0.9f, 0.5f), "gift_rain_1179", 16f);

        [UnityTest, Timeout(120000)]
        public IEnumerator GiftFish() => OneGift(SimCore.NudgeKind.Herd, s => PeopleView.Find().GetComponent<BoxView>().land.pond, "gift_fish_1179", 34f);
    }
}
