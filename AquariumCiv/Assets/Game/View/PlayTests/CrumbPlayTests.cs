using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Shaping;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#147 on the real scene: the fall, the wall, the smudge wiped by touch, the wait after a shaping, the last piece.</summary>
    [Category("Crumb")]
    public class CrumbPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static RelicProps Props => GlassVoice.Instance.GetComponent<RelicProps>();
        static CareView Care => CareView.Instance;

        IEnumerator Grown()
        {
            yield return OpenSlice(seed: Seed);
            ShowMeDirector.BubblesOn = false;
            SimLayoutSource.DebugSkipYears(3f);
            yield return null;
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return Frames(6);
        }

        [UnityTest, Timeout(60000), Category("Bundle3")]
        public IEnumerator SomeoneSaysSomethingWhenItLands()
        {
            yield return Grown();
            yield return Drop();
            yield return Until(() => GlassVoice.Instance.LastCrumbLine != null, 8f);
            Assert.IsNotNull(GlassVoice.Instance.LastCrumbLine, "a line as it lands");
            Assert.AreEqual(GlassVoice.Instance.LastCrumbLine, ShowMeDirector.Instance.LastLine);
            AssertNoErrors("crumb line");
        }

        static IEnumerator Drop()
        {
            StringAssert.Contains("people until", GlassVoice.DebugDropCrumb());
            yield return Until(() => Props.CrumbDropSeconds > 0.02f, 2f);
            Assert.GreaterOrEqual(Props.CrumbDropSeconds, 0f, "it's falling");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheCrumbFallsHitsAWallSmudgesItAndLands()
        {
            yield return Grown();
            yield return Drop();
            var plan = Props.CrumbPlan;
            yield return Shot("bundle2-147-fall");
            yield return Until(() => Props.CrumbDropSeconds < 0f || Props.CrumbDropSeconds >= plan.hitAt + 0.04f, 2f);
            yield return Shot("bundle2-147-hit");
            Assert.GreaterOrEqual(Care.LastSmudgePane, 0, "the smudge is on a pane");
            var mask = Care.Masks[Care.LastSmudgePane];
            float g = mask.GrimeAt(Care.LastSmudgeUv, Care.LastSmudgeRadius * 0.5f);
            Debug.Log($"[crumb] wall {plan.wall} pane {Care.LastSmudgePane} uv {Care.LastSmudgeUv} r {Care.LastSmudgeRadius:0.00} grime {g:0.00}");
            Assert.Greater(g, 0.3f, "a mark that shows");
            yield return Until(() => Props.CrumbDropSeconds < 0f, 2f);
            var lump = Props.Made[RelicId.SkyCrumb];
            var box = Object.FindAnyObjectByType<BoxView>();
            Assert.AreEqual(plan.landX, lump.localPosition.x, 0.02f, "it lands at its spot");
            Assert.AreEqual(plan.landZ, lump.localPosition.z, 0.02f);
            Assert.AreEqual(box.GroundHeight(plan.landX, plan.landZ), lump.localPosition.y, 0.05f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("bundle2-147-landed");
            StringAssert.Contains("Bright One", Sim.RelicLabel(RelicId.SkyCrumb));
            Assert.IsFalse(Sim.FindRelic(RelicId.SkyCrumb).cause == "rain" && Sim.RelicLabel(RelicId.SkyCrumb).Contains("trees"), "the label names the real shaping");
            AssertNoErrors("crumb drop");
        }

        /// <summary>#210 #211: small square bits of cookie (dough, a few choc chips) break off where the lump hits the glass, fall off it
        /// speeding up, scatter on the ground, flatten a little, rest, and are gone. Shots at default zoom and close.</summary>
        [UnityTest, Timeout(90000)]
        public IEnumerator TheBitsFallOffTheGlassScatterAndGo()
        {
            yield return Grown();
            var cam = Camera.main.GetComponent<BoxCamera>();
            yield return Drop();
            yield return Until(() => Props.CrumbBitsSeconds >= 0f, 3f);
            Assert.GreaterOrEqual(Props.CrumbBitsSeconds, 0f, "bits break off at the hit");
            var bits = Props.CrumbBits;
            var parts = Props.CrumbBitParts;
            Assert.AreEqual(CrumbFall.BitCount, parts.Length);
            int chips = bits.Count(b => b.kind == CrumbFall.BitKind.Chip);
            Assert.That(chips, Is.InRange(1, bits.Length / 2 - 1), "a few choc chips, mostly dough");
            // Every chip's mesh is darker than every dough bit's: two kinds you can tell apart.
            float Dark(int i) { var c = parts[i].GetComponent<MeshFilter>().sharedMesh.colors; return c.Length == 0 ? 1f : c.Average(x => x.r + x.g + x.b); }
            float chipMax = Enumerable.Range(0, bits.Length).Where(i => bits[i].kind == CrumbFall.BitKind.Chip).Max(Dark);
            float doughMin = Enumerable.Range(0, bits.Length).Where(i => bits[i].kind == CrumbFall.BitKind.Dough).Min(Dark);
            Assert.Less(chipMax, doughMin * 0.5f, "choc chips read dark against the dough");
            yield return Shot("210-bits-on-glass-default");
            // The fall: sample one bit's height while it drops; it drops faster at the end than at the start.
            int k = Enumerable.Range(0, bits.Length).OrderByDescending(i => bits[i].cling).First();
            var heights = new List<(float t, float y)>();
            float until = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < until && Props.CrumbBitsSeconds >= 0f && Props.CrumbBitsSeconds < bits[k].landAt + 0.05f)
            {
                heights.Add((Props.CrumbBitsSeconds, parts[k].localPosition.y));
                yield return null;
            }
            var falling = heights.Where(h => h.t > bits[k].cling && h.t < bits[k].landAt).ToList();
            Debug.Log($"[crumb bits] bit {k}: {falling.Count} frames falling, {string.Join(" ", falling.Select(h => $"{h.t:0.00}:{h.y:0.000}"))}");
            Assert.GreaterOrEqual(falling.Count, 4, "seen falling over a few frames");
            float v0 = (falling[0].y - falling[1].y) / Mathf.Max(1e-4f, falling[1].t - falling[0].t);
            float v1 = (falling[falling.Count - 2].y - falling[falling.Count - 1].y) / Mathf.Max(1e-4f, falling[falling.Count - 1].t - falling[falling.Count - 2].t);
            Assert.Greater(v1, v0 * 1.5f, "it speeds up as it falls: gravity, not a constant drift");
            // Down: they scatter from where they landed, and sit flattened on the ground.
            float last = bits.Max(b => b.landAt);
            yield return Until(() => Props.CrumbBitsSeconds > last + 0.1f, 3f);
            var landed = parts.Select(p => new Vector2(p.localPosition.x, p.localPosition.z)).ToList();
            yield return Shot("210-bits-scatter-default");
            yield return Until(() => Props.CrumbBitsSeconds > last + 1f, 3f);
            var rest = parts.Select(p => new Vector2(p.localPosition.x, p.localPosition.z)).ToList();
            var box = Object.FindAnyObjectByType<BoxView>();
            int moved = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if ((rest[i] - landed[i]).magnitude > 0.01f) moved++;
                Assert.Less(parts[i].localScale.y, parts[i].localScale.x * 0.8f, "flattened where it landed: its smear");
                float ground = box.land.OnGround(parts[i].localPosition.x, parts[i].localPosition.z).y;
                Assert.AreEqual(ground + parts[i].localScale.y * 0.5f, parts[i].localPosition.y, 0.01f, "on the ground");
            }
            Assert.GreaterOrEqual(moved, parts.Length / 2, "they scatter after landing");
            yield return Shot("210-bits-rest-default");
            var mid = parts.Aggregate(Vector3.zero, (s, p) => s + p.localPosition) / parts.Length;
            cam.EaseTo(box.transform.TransformPoint(mid), cam.yaw, 40f, cam.minZoom, 0.05f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("210-bits-rest-close");
            // Rest, fade, gone: nothing left over.
            yield return Until(() => Props.CrumbBitsSeconds < 0f, CrumbFall.RestSeconds + CrumbFall.FadeSeconds + 3f);
            Assert.Less(Props.CrumbBitsSeconds, 0f, "the bits are done");
            Assert.IsTrue(parts.All(p => !p.gameObject.activeSelf), "and gone");
            AssertNoErrors("crumb bits");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheSmudgeWipesOffByTouch()
        {
            yield return Grown();
            yield return Drop();
            yield return Until(() => Props.CrumbDropSeconds < 0f, 3f);
            var cam = Camera.main.GetComponent<BoxCamera>();
            yield return Until(() => !cam.IsFlying, 4f); // the moment's nudge moves the camera: the finger goes where the mark is now
            int pane = Care.LastSmudgePane;
            var mask = Care.Masks[pane];
            var uv = Care.LastSmudgeUv;
            float r = Care.LastSmudgeRadius * 0.5f, before = mask.GrimeAt(uv, r);
            var t = Care.PaneOf(pane);
            bool zz = t.localScale.x < t.localScale.z;
            var os = new Vector3(zz ? 0f : uv.x - 0.5f, uv.y - 0.5f, zz ? uv.x - 0.5f : 0f);
            Vector2 at = Camera.main.WorldToScreenPoint(t.TransformPoint(os));
            GameHud.Instance.SetCleanMode(true);
            yield return Frames(2);
            float reach = 0.03f * Screen.height;
            var path = new[] { at + new Vector2(0f, -reach), at + new Vector2(0f, reach), at + new Vector2(-reach * 0.4f, -reach), at + new Vector2(-reach * 0.4f, reach), at };
            yield return Drag(path, 16f);
            yield return Frames(3);
            float after = mask.GrimeAt(uv, r);
            Debug.Log($"[crumb] wipe at {at}: {before:0.00} to {after:0.00}");
            Assert.Less(after, before * 0.5f, "a wipe takes it off");
            AssertNoErrors("smudge wipe");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheCrumbWaitsForTheShapingAndNoLumpShowsBeforeIt()
        {
            yield return Grown();
            var g = GlassVoice.Instance;
            var tools = SimCore.NudgeKind.Rain;
            StringAssert.DoesNotContain("fail", ShapingView.DebugPlace(tools, 0.8f, 0.4f).ToLowerInvariant());
            yield return Frames(3);
            StringAssert.DoesNotContain("fail", ShapingView.DebugPlace(SimCore.NudgeKind.Herd, -0.8f, 0.5f).ToLowerInvariant());
            yield return Until(() => g.Pending.Contains(RelicId.SkyCrumb), 3f);
            Assert.IsTrue(Sim.HasRelic(RelicId.SkyCrumb), "the second shaping made it");
            float start = Time.realtimeSinceStartup;
            yield return Frames(4);
            Assert.IsFalse(Props.Made.TryGetValue(RelicId.SkyCrumb, out var lump) && lump.gameObject.activeSelf, "no lump before it falls");
            yield return Until(() => !g.Pending.Contains(RelicId.SkyCrumb), 30f);
            float waited = Time.realtimeSinceStartup - start;
            Debug.Log($"[crumb] fell {waited:0.0} s after the herd");
            Assert.GreaterOrEqual(waited, 3f, "a herd's run-in, then a beat");
            AssertNoErrors("crumb wait");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheLastPieceShowsOnceTheLumpIsEaten()
        {
            yield return Grown();
            yield return Drop();
            yield return Frames(4);
            var piece = Object.FindAnyObjectByType<RelicProps>().transform.GetComponentsInChildren<Transform>(true).First(t => t.name == "SkyCrumb_LastPiece");
            Assert.IsFalse(piece.gameObject.activeSelf, "not while the lump is there");
            yield return Until(() => Props.CrumbDropSeconds < 0f, 3f);
            GlassVoice.DebugCrumbEaten();
            yield return Frames(4);
            Assert.IsTrue(piece.gameObject.activeSelf, "and then it does");
            Assert.IsFalse(Props.Made[RelicId.SkyCrumb].gameObject.activeSelf, "the lump is gone");
            var cam = Camera.main.GetComponent<BoxCamera>();
            cam.EaseTo(Object.FindAnyObjectByType<BoxView>().transform.TransformPoint(piece.localPosition), cam.yaw, 52f, 0.3f, 0.2f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("bundle2-147-last-piece");
            AssertNoErrors("last piece");
        }
    }
}
