using System.IO;
using AquariumCiv.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>
    /// #504: not an assertion test, a camera. Run it (category "Shots504") to write close side views of the quadrupeds to
    /// <c>Temp/agent/quad504/</c>: the stag, doe, fawn and wolf standing, and a contact sheet of the stag's graze (rest, head going
    /// down, chewing, lifting, looking round with an ear flick, going down again). Run it on the old commit and the new one and compare.
    /// </summary>
    [Category("Shots504")]
    public class QuadShots504
    {
        const int W = 480, H = 360;

        [Test]
        public void WriteTheShots()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "agent", "quad504"));
            Directory.CreateDirectory(dir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/View/Materials/Stylized.mat");
            Assume.That(mat != null, "Stylized.mat");
            var root = new GameObject("QuadShots504");
            try
            {
                var mb = new MeshBuilder(504);
                mb.Box(new Vector3(0f, -0.01f, 0f), new Vector3(1.2f, 0.02f, 1.2f), new Color(0.36f, 0.58f, 0.25f));
                var ground = new GameObject("ground", typeof(MeshFilter), typeof(MeshRenderer));
                ground.transform.SetParent(root.transform, false);
                ground.GetComponent<MeshFilter>().sharedMesh = mb.ToMesh("ground504");
                ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var sun = new GameObject("sun", typeof(Light)).GetComponent<Light>();
                sun.transform.SetParent(root.transform, false);
                sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50f, 40f, 0f); sun.intensity = 1.1f;
                var camGo = new GameObject("cam", typeof(Camera));
                camGo.transform.SetParent(root.transform, false);
                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 0.15f; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.72f, 0.84f, 0.92f); cam.nearClipPlane = 0.01f; cam.farClipPlane = 5f;
                cam.transform.position = new Vector3(1f, 0.09f, 0f); cam.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // side view, deer faces +z (screen left)
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;

                foreach (var kind in new[] { QuadKind.Stag, QuadKind.Doe, QuadKind.Fawn, QuadKind.Wolf })
                {
                    var d = new QuadDoll(kind, root.transform, mat);
                    d.Apply(QuadPose.Stand(Vector3.zero, 0f));
                    File.WriteAllBytes(Path.Combine(dir, kind.ToString().ToLower() + "-stand.png"), Shoot(cam, rt).EncodeToPNG());
                    d.Destroy();
                }

                var stag = new QuadDoll(QuadKind.Stag, root.transform, mat);
                float[] times = { 0f, 0.3f, 0.6f, 2.0f, 2.2f, 6.2f, 7.0f, 7.55f, 8.4f, 9.0f };
                const int cols = 5;
                var sheet = new Texture2D(W * cols, H * 2, TextureFormat.RGB24, false);
                for (int i = 0; i < times.Length; i++)
                {
                    var p = QuadPose.Stand(Vector3.zero, 0f);
                    QuadMotion.Graze(ref p, times[i], times[i], 0);
                    stag.Apply(p);
                    var shot = Shoot(cam, rt);
                    sheet.SetPixels((i % cols) * W, (1 - i / cols) * H, W, H, shot.GetPixels());
                    Object.DestroyImmediate(shot);
                }
                File.WriteAllBytes(Path.Combine(dir, "stag-graze-sheet.png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
                TestContext.Out.WriteLine("quad504 shots in " + dir);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Texture2D Shoot(Camera cam, RenderTexture rt)
        {
            cam.Render();
            var keep = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = keep;
            return tex;
        }
    }
}
