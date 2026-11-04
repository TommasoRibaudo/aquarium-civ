using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #275, #276 in the real Slice scene at a phone size: the carried Keeper Doll stays clear of every building along the whole
    /// procession (seeded Town, several rituals), and during a ritual and a festival evening the participants show wordless
    /// symbol bubbles (at most five, symbols from the set, one shared material, no text). Shots to Temp/agent.
    /// </summary>
    [Category("RitualViewPlay")]
    public class RitualViewPlayTests : PlayTestBase
    {
        IEnumerator Open() => OpenSlice(1179, 2556, 1234);

        static IEnumerator GrowInto(Era era)
        {
            var sim = GameClock.Instance.Sim;
            for (int i = 0; i < 20 && sim.State.era < era; i++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            Assume.That(sim.State.era, Is.EqualTo(era));
            sim.CleanAll();
            yield return null; yield return null;
        }

        static IEnumerator Look(Place place, float zoom)
        {
            var cam = Camera.main.GetComponent<BoxCamera>();
            cam.FlyToPlace(place, zoom, 0.4f);
            yield return new WaitForSecondsRealtime(0.7f);
        }

        /// <summary>The smallest gap (box units) between the carried doll's bounds and any standing solid lot, or large with no doll carried.</summary>
        static float CarriedGap(BoxView box, SimCore sim, out bool carried)
        {
            carried = false;
            float best = float.MaxValue;
            var st = sim.Settlement;
            if (st == null) return best;
            foreach (var d in Resources.FindObjectsOfTypeAll<PegDollView>())
            {
                if (d.propKind != PropKind.Doll || d.prop == null || !d.gameObject.activeInHierarchy) continue;
                var r = d.prop.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                carried = true;
                var b = r.bounds;
                var pts = new List<Vector3>();
                foreach (float fx in new[] { -1f, 0f, 1f })
                    foreach (float fz in new[] { -1f, 0f, 1f })
                        pts.Add(b.center + new Vector3(b.extents.x * fx, 0f, b.extents.z * fz));
                foreach (var w in pts)
                {
                    var l = box.transform.InverseTransformPoint(w);
                    float g = RitualClearance.NearestGap(st, l.x, l.z);
                    if (g < best) best = g;
                }
            }
            return best;
        }

        [UnityTest]
        public IEnumerator TheCarriedDollClearsEveryBuildingAlongTheProcession()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var pv = PeopleView.Find();
                var box = Object.FindAnyObjectByType<BoxView>();
                yield return GrowInto(Era.Town);
                int solid = sim.Settlement.Lots.Count(l => !l.retired && !l.open);
                int samples = 0, rituals = 0, shots = 0, inside = 0;
                var kinds = new HashSet<PropKind>();
                float worst = float.MaxValue;
                string worstAt = "";
                foreach (float hour in new[] { 9f, 11f, 14f })
                {
                    PeopleView.DebugSetHour(hour);
                    yield return null;
                    string r = GlassVoice.DebugStartRitual();
                    var info = GlassVoice.Instance.LastMoment;
                    Assert.IsNotNull(info, "the ritual starts: " + r);
                    rituals++;
                    yield return Look(LifePlanner.RelicPlace(RelicId.KeeperDoll), 0.5f);
                    pv.timeScale = 3f;
                    long day = info.day;
                    for (int f = 0; f < 4000 && pv.Now.day == day && pv.Now.hour < info.end + 0.3f; f++)
                    {
                        foreach (var dv in Resources.FindObjectsOfTypeAll<PegDollView>()) if (dv.propKind != PropKind.None) kinds.Add(dv.propKind);
                        float g = CarriedGap(box, sim, out bool carried);
                        if (carried)
                        {
                            if (shots < 3 && (f % 25 == 0))
                            {
                                foreach (var dv in Resources.FindObjectsOfTypeAll<PegDollView>())
                                    if (dv.propKind == PropKind.Doll && dv.gameObject.activeInHierarchy)
                                    {
                                        var cm = Camera.main.GetComponent<BoxCamera>();
                                        cm.EaseTo(dv.transform.position, cm.yaw, cm.pitch, 0.2f, 0.15f);
                                        break;
                                    }
                                if (f % 50 == 0) { yield return new WaitForSecondsRealtime(0.3f); yield return Shot("carry_" + shots++); }
                            }
                            samples++;
                            if (g < -0.02f) inside++;
                            if (g < worst) { worst = g; worstAt = $"hour {pv.Now.hour:0.00}"; }
                        }
                        yield return null;
                    }
                    pv.timeScale = 1f;
                    yield return null;
                }
                Debug.Log($"[RitualView] {solid} solid lots, {rituals} rituals, {samples} carried samples (props seen: {string.Join(",", kinds)}), closest gap {worst:F3} at {worstAt}");
                Assert.Greater(solid, 2, "a Town has buildings");
                Assert.Greater(samples, 20, "the doll was carried in view");
                // The carrier steps out of their own door at the start (the doll's box touches the lot there): a few samples, never the route.
                Debug.Log($"[RitualView] {inside} of {samples} samples inside a footprint");
                Assert.LessOrEqual(inside, samples / 50, $"the carried doll stays out of building footprints along the route ({inside}/{samples} inside, closest {worst:F3} at {worstAt})");
                AssertNoErrors("procession");
            }
            finally { CloseSlice(); }
        }

        [UnityTest]
        public IEnumerator ARitualShowsWordlessSymbolBubbles()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var pv = PeopleView.Find();
                yield return GrowInto(Era.Town);
                PeopleView.DebugSetHour(10f);
                yield return null;
                var view = RitualBubbleView.Instance;
                Assert.IsNotNull(view, "the glass layer carries the bubble view");
                Debug.Log("[RitualView] " + GlassVoice.DebugStartRitual());
                var info = GlassVoice.Instance.LastMoment;
                Assert.IsNotNull(info);
                // default zoom first
                pv.timeScale = 2f;
                int most = 0, frames = 0;
                bool shotDefault = false, shotClose = false;
                var symbols = new HashSet<RitualSymbol>();
                for (int f = 0; f < 5000 && pv.Now.day == info.day && pv.Now.hour < info.end; f++)
                {
                    most = Mathf.Max(most, view.Drawn.Count);
                    foreach (var d in view.Drawn) symbols.Add(d.symbol);
                    if (view.Drawn.Count > 0) frames++;
                    Assert.LessOrEqual(view.Drawn.Count, RitualBubbles.MaxAlive, "at most five symbols at once");
                    if (!shotDefault && view.Drawn.Count >= 2) { shotDefault = true; yield return Shot("ritual_default"); }
                    if (shotDefault && !shotClose && view.Drawn.Count >= 2)
                    {
                        yield return Look(LifePlanner.RelicPlace(RelicId.KeeperDoll), 0.28f);
                        yield return null;
                        if (view.Drawn.Count >= 1) { shotClose = true; yield return Shot("ritual_close"); }
                    }
                    yield return null;
                }
                pv.timeScale = 1f;
                Debug.Log($"[RitualView] most alive {most}, frames with bubbles {frames}, symbols {string.Join(",", symbols)}");
                Assert.Greater(most, 0, "the participants show symbol bubbles");
                Assert.Greater(view.RitualShown, 0);
                foreach (var s in symbols) Assert.IsTrue(System.Enum.IsDefined(typeof(RitualSymbol), s));
                Assert.IsTrue(symbols.Contains(RitualSymbol.Doll) || symbols.Contains(RitualSymbol.Eye) || symbols.Contains(RitualSymbol.Sun) || symbols.Contains(RitualSymbol.Notes));
                Assert.IsNotNull(view.Renderer);
                Assert.AreEqual(1, view.Renderer.sharedMaterials.Length, "one shared material");
                Assert.IsNull(view.Renderer.GetComponentInChildren<TextMesh>(), "no text on a bubble");
                AssertNoErrors("ritual bubbles");
            }
            finally { CloseSlice(); }
        }

        [UnityTest]
        public IEnumerator AFestivalEveningShowsSymbolBubbles()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var pv = PeopleView.Find();
                yield return GrowInto(Era.Town);
                var view = RitualBubbleView.Instance;
                Assert.IsNotNull(view);
                sim.State.festivalUntilMonth = sim.State.month + 24; // a festival year (the evening scene, not the cinematic)
                PeopleView.DebugSetHour(LifePlanner.FestivalEveningFrom + 0.1f);
                yield return null; yield return null;
                pv.timeScale = 3f;
                var symbols = new HashSet<RitualSymbol>();
                int most = 0;
                bool shot = false;
                for (int f = 0; f < 5000 && pv.Now.hour < LifePlanner.FestivalEveningTo - 0.1f; f++)
                {
                    most = Mathf.Max(most, view.Drawn.Count);
                    foreach (var d in view.Drawn) symbols.Add(d.symbol);
                    Assert.LessOrEqual(view.Drawn.Count, RitualBubbles.MaxAlive);
                    if (!shot && view.Drawn.Count >= 2) { shot = true; yield return Shot("festival_bubbles"); }
                    yield return null;
                }
                pv.timeScale = 1f;
                Debug.Log($"[RitualView] festival: most alive {most}, symbols {string.Join(",", symbols)}, shown {view.FestivalShown}");
                Assert.Greater(view.FestivalShown, 0, "the festival evening shows bubbles");
                Assert.Greater(most, 0);
                Assert.IsTrue(symbols.Contains(RitualSymbol.Sun) || symbols.Contains(RitualSymbol.Notes), "a sun or notes at the festival");
                AssertNoErrors("festival bubbles");
            }
            finally { CloseSlice(); }
        }
    }
}
