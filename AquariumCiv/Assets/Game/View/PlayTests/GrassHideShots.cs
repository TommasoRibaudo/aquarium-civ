using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Tester run 1 #4 follow-up: tall grass planted on the camera side of a stalking hunter flattens (GrassHide); grass far from him does not.
    /// Shots to Temp/agent/ (grasshide_*.png).</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("GrassHide")]
    public class GrassHideShots : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        [UnityTest, Timeout(600000)]
        public IEnumerator TallGrassOnTheCameraSide_Flattens_AndComesBack()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            GameObject patch = null;
            try
            {
                yield return OpenSlice(seed: 4242);
                var pv = PeopleView.Find();
                var rv = RabbitView.Instance;
                PeopleView.DebugSetHour(9f);
                foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
                EcoDebug.SkipSeasons(1);
                EcoDebug.OpenCampGate();
                for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
                EcoDebug.AddRabbits(6);
                for (int calm = 0, f = 0; calm < 120 && f < 1200; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
                PeopleView.DebugSetHour(9f);
                yield return Frames(30);
                yield return Until(() => !rv.SignPlaying && rv.Shown >= 3, 15f);
                var cam = Camera.main;
                cam.GetComponent<BoxCamera>().zoom = 1f;
                EcoDebug.ForceHunt(true);
                Assert.IsTrue(pv.Planner.DebugStartHunt(pv.Now, out var plan));
                var who = plan.hunter;
                pv.timeScale = 4f;
                var box = Object.FindAnyObjectByType<BoxView>();
                bool shot = false;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 120f)
                {
                    yield return null;
                    HuntPhase ph = HuntPhase.None; float prog = 0f;
                    foreach (var a in pv.Activities) if (a.who == who) { ph = a.hunt; prog = a.progress; }
                    if (ph == HuntPhase.Stalk && prog > 0.5f && !shot)
                    {
                        shot = true;
                        pv.timeScale = 0f;
                        var ws = new System.Collections.Generic.List<PersonRef>(); var xs = new System.Collections.Generic.List<float>(); var zs = new System.Collections.Generic.List<float>();
                        pv.StandingNow(ws, xs, zs);
                        var h = new Vector2(float.NaN, float.NaN);
                        for (int i = 0; i < ws.Count; i++) if (ws[i] == who) h = new Vector2(xs[i], zs[i]);
                        yield return Frames(3); var hf = GrassHide.First; var hl = box.Root.InverseTransformPoint(hf); h = new Vector2(hl.x, hl.z); var hw = hf; Debug.Log($"[grasshide] root offset {box.Root.position} scale {box.Root.lossyScale}");
                        var toCam = cam.transform.position - hw; var tc = new Vector2(toCam.x, toCam.z).normalized;
                        var mb = new MeshBuilder(77);
                        var col = new Color(1f, 0.1f, 0.8f);
                        for (int i = 0; i < 14; i++)
                        {   // a clump on the camera side, 0.04..0.2 u from him; and a control clump 0.6 u away, on the same side
                            float ang = (i - 7) * 0.22f; float r = 0.04f + 0.16f * (i % 5) / 4f;
                            var d = Quaternion.Euler(0, ang * Mathf.Rad2Deg, 0) * new Vector3(tc.x, 0, tc.y) * r;
                            var g = box.land.OnGround(h.x + d.x, h.y + d.z);
                            Pieces.GrassTuft(mb, g, 0.13f, col, true);
                            var d2 = d.normalized * (0.6f + 0.02f * i);
                            Pieces.GrassTuft(mb, box.land.OnGround(h.x + d2.x, h.y + d2.z), 0.13f, col, true);
                        }
                        patch = new GameObject("grasshide-patch");
                        patch.transform.SetParent(box.Root, false);
                        patch.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("grasshide");
                        patch.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
                        GrassHide.Off = true; yield return Frames(10);
                        yield return Shot("grasshide_off");
                        GrassHide.Off = false;
                        pv.timeScale = 0.25f;
                        yield return Frames(60);
                        Debug.Log($"[grasshide] active points {GrassHide.Count} {GrassHide.Describe()} hunter {h} world {hw} patch verts {mb.VertexCount}");
                        Assert.GreaterOrEqual(GrassHide.Count, 1, "the net is on while stalking");
                        yield return Shot("grasshide_on");
                        pv.timeScale = 0f; yield return Frames(3); Debug.Log($"[grasshide] at on-shot {GrassHide.Describe()} patch centre local {h}");
                        var rig = cam.GetComponent<BoxCamera>();
                        rig.FlyTo(hw, Mathf.Max(rig.minZoom * 2f, 0.25f), 0.3f);
                        for (int i = 0; i < 200 && rig.IsFlying; i++) yield return null;
                        yield return Frames(10);
                        yield return Shot("grasshide_on_close");
                        GrassHide.Off = true; yield return Frames(10);
                        yield return Shot("grasshide_off_close");
                        GrassHide.Off = false;
                        rig.zoom = 1f; yield return Frames(5);
                        pv.timeScale = 4f;
                    }
                    if (shot && ph == HuntPhase.Return) break;
                }
                Assert.IsTrue(shot, "reached the stalk");
                yield return Frames(120);
                Assert.AreEqual(0, GrassHide.Count, "eased back out after the hunt");
                AssertNoErrors("grasshide");
            }
            finally { GrassHide.Off = false; if (patch != null) Object.Destroy(patch); GameClock.EcologyPref = prefWas; }
        }
    }
}
