using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#763: panning the camera made the feather pole's white and red feathers flicker. The pole is drawn in small steps of a pan and
    /// the feather pixels counted in every frame: a steady feather keeps (about) the same number of pixels; a flickering one does not.
    /// The mesh and the pole's transform must also not change from frame to frame.</summary>
    [Category("FeatherPoleSteady")]
    public class FeatherPoleSteadyPlayTests : PlayTestBase
    {
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        struct Counts { public int white, red, ochre, dark; }

        static Counts Count(Texture2D tex, int cx, int cy, int half)
        {
            var c = new Counts();
            int x0 = Mathf.Clamp(cx - half, 0, tex.width - 1), y0 = Mathf.Clamp(cy - half, 0, tex.height - 1);
            int w = Mathf.Min(half * 2, tex.width - x0), h = Mathf.Min(half * 2, tex.height - y0);
            var px = tex.GetPixels(x0, y0, w, h);
            foreach (var p in px)
            {
                if (Mathf.Min(p.r, p.g, p.b) > 0.70f && Mathf.Max(p.r, p.g, p.b) - Mathf.Min(p.r, p.g, p.b) < 0.14f) c.white++;
                else if (p.r > 0.50f && p.g < 0.34f && p.b < 0.34f && p.r > p.g * 1.8f) c.red++;
                else if (p.r > 0.62f && p.g > 0.42f && p.g < 0.62f && p.b < 0.34f) c.ochre++;
            }
            return c;
        }

        static IEnumerator Grab(System.Action<Texture2D> got)
        {
            yield return new WaitForEndOfFrame();
            got(ScreenCapture.CaptureScreenshotAsTexture());
        }

        static float Cv(IEnumerable<int> v) { var a = v.Select(x => (float)x).ToArray(); float m = a.Average(); return m < 1f ? 0f : Mathf.Sqrt(a.Select(x => (x - m) * (x - m)).Average()) / m; }

        /// <summary>The pole stands, three tied feathers (white, red, ochre).</summary>
        IEnumerator StandPole(System.Action<Transform, Vector3> got)
        {
            yield return OpenSlice(1179, 2556, seed: 4242);
            var sim = GameClock.Instance.ActiveSim;
            var box = Object.FindAnyObjectByType<BoxView>();
            foreach (int kind in new[] { PoleFeatherKind.Newborn, PoleFeatherKind.Marriage, PoleFeatherKind.Village, PoleFeatherKind.Newborn, PoleFeatherKind.Marriage })
            {
                int i = sim.DebugPoleFeather(kind);
                Assert.GreaterOrEqual(i, 0, "a pole could stand");
                sim.MarkPoleFeatherTied(i);
            }
            PeopleView.DebugSetHour(13f);
            yield return Frames(30);
            Assert.IsTrue(sim.FeatherPoleSpot(out float px, out float pz), "the pole has a spot");
            var t = ConsequencePropsView.Instance.Standing.First(kv => kv.Value != null && kv.Value.name.StartsWith("Consequence_feather")).Value;
            got(t, box.transform.TransformPoint(box.land.OnGround(px, pz)));
        }

        static IEnumerator PanSeries(Vector3 pole, float zoom, float pitch, int steps, float stepSize, string tag, List<Counts> into, Transform t, string shotDir)
        {
            var rig = Cam;
            for (int k = 0; k < steps; k++)
            {
                rig.CancelFlight();
                rig.target = pole + Vector3.up * 0.05f + rig.transform.right * (k * stepSize);
                rig.zoom = zoom; rig.pitch = pitch;
                rig.Apply();
                yield return null;
                Texture2D tex = null;
                yield return Grab(x => tex = x);
                var sp = Camera.main.WorldToScreenPoint(t.position + Vector3.up * 0.42f);
                var c = Count(tex, (int)sp.x, (int)sp.y, 130);
                into.Add(c);
                if (shotDir != null) { Directory.CreateDirectory(shotDir); File.AppendAllText($"{shotDir}counts_{tag}.txt", $"{k} white {c.white} red {c.red} ochre {c.ochre} sp {sp} tex {tex.width}x{tex.height} cam {Camera.main.pixelWidth}x{Camera.main.pixelHeight}\n"); }
                if (shotDir != null && (k == 0 || k == steps / 2))
                {
                    Directory.CreateDirectory(shotDir);
                    File.WriteAllBytes($"{shotDir}{tag}_{k:00}.png", tex.EncodeToPNG());
                }
                Object.Destroy(tex);
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator FeathersStayPutWhilePanning()
        {
            Transform pt = null; Vector3 pole = default;
            yield return StandPole((t, p) => { pt = t; pole = p; });
            var mf = pt.GetComponent<MeshFilter>();
            var mesh0 = mf.sharedMesh; var verts0 = mesh0.vertices;
            var pos0 = pt.position; var rot0 = pt.rotation; var scl0 = pt.localScale;

            var shotDir = Shots + "763/"; File.Delete(shotDir + "counts_pan.txt");
            var counts = new List<Counts>();
            yield return PanSeries(pole, 0.1f, 45f, 24, 0.002f, "pan", counts, pt, shotDir);
            TestContext.WriteLine($"white cv {Cv(counts.Select(c => c.white)):0.000} red cv {Cv(counts.Select(c => c.red)):0.000} ochre cv {Cv(counts.Select(c => c.ochre)):0.000}");
            TestContext.WriteLine("white " + string.Join(",", counts.Select(c => c.white)));
            TestContext.WriteLine("red " + string.Join(",", counts.Select(c => c.red)));
            TestContext.WriteLine("ochre " + string.Join(",", counts.Select(c => c.ochre)));

            // nothing about the pole moved or was rebuilt by the camera
            Assert.AreSame(mesh0, mf.sharedMesh, "the pole's mesh was not rebuilt while panning");
            CollectionAssert.AreEqual(verts0, mf.sharedMesh.vertices);
            Assert.AreEqual(pos0, pt.position); Assert.AreEqual(rot0, pt.rotation); Assert.AreEqual(scl0, pt.localScale);
            Assert.Greater(counts.Min(c => c.white), 0, "the white feather shows");
            Assert.Greater(counts.Min(c => c.red), 0, "the red feather shows");
            AssertNoErrors("feather pole pan");
        }


        /// <summary>The camera creeps a hair at a time (about a tenth of a pixel); on steady geometry the picture of the pole barely changes between
        /// two frames. Z-fighting faces swap whole pixels, so the share of strongly changed pixels shows it.</summary>
        static int Class(Color p)
        {
            if (Mathf.Min(p.r, p.g, p.b) > 0.70f && Mathf.Max(p.r, p.g, p.b) - Mathf.Min(p.r, p.g, p.b) < 0.14f) return 1;
            if (p.r > 0.50f && p.g < 0.34f && p.b < 0.34f && p.r > p.g * 1.8f) return 2;
            if (p.r > 0.62f && p.g > 0.42f && p.g < 0.62f && p.b < 0.34f) return 3;
            return 0;
        }

        /// <summary>Of the pixels that are a feather colour in either frame, the share that is a different colour class in the other.</summary>
        static float Speckle(Color[] a, Color[] b)
        {
            int n = 0, diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int ca = Class(a[i]), cb = Class(b[i]);
                if (ca == 0 && cb == 0) continue;
                n++; if (ca != cb) diff++;
            }
            return n == 0 ? 0f : diff / (float)n;
        }

        IEnumerator CreepSeries(Vector3 pole, Transform t, int steps, float stepSize, List<float> into, string tag)
        {
            var rig = Cam; Color[] prev = null; Vector3 prevSp = default;
            for (int k = 0; k < steps; k++)
            {
                rig.CancelFlight();
                rig.target = pole + Vector3.up * 0.05f + rig.transform.right * (k * stepSize);
                rig.zoom = 0.1f; rig.pitch = 45f; rig.Apply();
                yield return null; yield return null;
                Texture2D tex = null;
                yield return Grab(x => tex = x);
                var sp = Camera.main.WorldToScreenPoint(t.position + Vector3.up * 0.42f);
                int half = 130;
                var px = tex.GetPixels(Mathf.Clamp((int)sp.x - half, 0, tex.width - 2 * half), Mathf.Clamp((int)sp.y - half, 0, tex.height - 2 * half), 2 * half, 2 * half);
                if (k == 0 || k == steps - 1) { var dir = Shots + "763/"; Directory.CreateDirectory(dir); File.WriteAllBytes($"{dir}{tag}_creep_{k:00}.png", tex.EncodeToPNG()); }
                Object.Destroy(tex);
                if (prev != null && (sp - prevSp).magnitude < 6f) into.Add(Speckle(prev, px)); // a frame where the camera hopped is not a frame-pair
                prev = px; prevSp = sp;
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator FeathersDoNotSpeckleWhileCreeping()
        {
            Transform pt = null; Vector3 pole = default;
            yield return StandPole((t, p) => { pt = t; pole = p; });
            var speck = new List<float>();
            yield return CreepSeries(pole, pt, 24, 0.0002f, speck, "creep");
            speck = speck.Skip(4).ToList(); // the first frames still carry the camera settling
            File.WriteAllText(Shots + "763/speckle_creep.txt", "max " + speck.Max().ToString("0.0000") + " mean " + speck.Average().ToString("0.0000") + " all " + string.Join(",", speck.Select(x => x.ToString("0.0000"))));
            Assert.Less(speck.Average(), 0.15f, // before the fix 0.21 (shadows on or off), after 0.10; the rest is the blades' own moving edges
                 "between two frames a hair apart almost no pixel of the pole's picture changes");
        }

        [Explicit("Evidence for #763; run by hand"), UnityTest, Timeout(300000)]
        public IEnumerator Evidence_CreepShadowsOff()
        {
            Transform pt = null; Vector3 pole = default;
            yield return StandPole((t, p) => { pt = t; pole = p; });
            var mr = pt.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            var speck = new List<float>();
            yield return CreepSeries(pole, pt, 24, 0.0002f, speck, "creepnoshadow");
            speck = speck.Skip(4).ToList();
            File.WriteAllText(Shots + "763/speckle_noshadow.txt", "n " + speck.Count + " mean " + speck.Average().ToString("0.0000") + " all " + string.Join(",", speck.Select(x => x.ToString("0.0000"))));
        }

        /// <summary>Evidence run: the same pan with shadows off on the pole.</summary>
        [Explicit("Evidence for #763; run by hand"), UnityTest, Timeout(300000)]
        public IEnumerator Evidence_ShadowsOff()
        {
            Transform pt = null; Vector3 pole = default;
            yield return StandPole((t, p) => { pt = t; pole = p; });
            var counts = new List<Counts>();
            File.Delete(Shots + "763/counts_noshadow.txt");
            pt.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            pt.GetComponent<MeshRenderer>().receiveShadows = false;
            yield return PanSeries(pole, 0.1f, 45f, 24, 0.002f, "noshadow", counts, pt, Shots + "763/");
            TestContext.WriteLine($"NOSHADOW white cv {Cv(counts.Select(c => c.white)):0.000} red cv {Cv(counts.Select(c => c.red)):0.000}");
            TestContext.WriteLine("white " + string.Join(",", counts.Select(c => c.white)));
            TestContext.WriteLine("red " + string.Join(",", counts.Select(c => c.red)));
        }
    }
}
