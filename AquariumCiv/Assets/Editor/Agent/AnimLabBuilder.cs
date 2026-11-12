// #353 AnimLab: the real look of an animation, rendered in EDIT mode (no play mode, no domain reload per clip).
//
// A lab stage built in code: one cell per clip (a grass patch in the box's palette, the box's day light and post grade), the
// game's own peg dolls (PegDollView.Create with the style kit's material, PegDoll.PickLook), rabbits (RabbitDoll), bushes, the
// fire pit, tents and trees from Pieces. Each frame poses every cell from AnimBench's track (the same clip JSON run through
// the same DollMotion / beat kit code, frame for frame; Tools/animlab/prep.py flattens it to lab.json) the way
// PeopleView.Drive and RabbitView apply a pose, and renders a default 3/4 camera and a close one through URP post
// (cam.targetTexture + cam.Render + ReadPixels, as GrowthLabBuilder.Render).
//
// Driven by Tools/animlab.sh: it preps the queue into Temp/agent/animlab/batch.json, then calls RenderChunk(seconds) through
// `unity command script eval` until it says done (each call well under 60 s; state on disk, so it resumes after a timeout or
// a reload), then Finish() puts the previous scene back. Tools/animlab/post.py makes the videos, gifs and sheets.
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AquariumCiv.Sim.Eco;
using AquariumCiv.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AquariumCiv.EditorTools
{
    public static class AnimLabBuilder
    {
        const string ViewDir = "Assets/Game/View";
        /// <summary>Cells sit this far apart in x, so one cell never shows in (or shades) another.</summary>
        const float CellGap = 60f;

        sealed class DollRig
        {
            public LabDoll data;
            public PegDollView view;
            public string propKey = "";
        }

        sealed class ObjectRig
        {
            public LabObject data;
            public GameObject go;
            public RabbitDoll rabbit;
            public MeshFilter berries;
            public int berryCount = -1;
        }

        sealed class Cell
        {
            public LabClip clip;
            public string outDir;
            public Vector3 origin;
            public readonly List<DollRig> dolls = new List<DollRig>();
            public readonly List<ObjectRig> objects = new List<ObjectRig>();
            public StringBuilder log = new StringBuilder();
        }

        sealed class Stage
        {
            public string batchId;
            public GameObject root;
            public Camera cam;
            public readonly List<Cell> cells = new List<Cell>();
            public readonly List<Object> owned = new List<Object>();
            public int posedUpTo = -1;
        }

        static Stage stage;
        static string PrevScenePath => Path.Combine(LabBatch.Root, "prev-scene.txt");
        static Texture2D readback;

        // ------------------------------------------------------------------ entry points

        /// <summary>Renders the batch on disk (Temp/agent/animlab/batch.json) for about <paramref name="seconds"/>, then saves where it got to.</summary>
        public static string RenderChunk(float seconds = 40f)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "ERROR: in play mode (AnimLab renders in edit mode)";
            var b = LabBatch.Load();
            if (b == null) return "ERROR: no batch (run Tools/animlab.sh render)";
            if (b.done || b.next >= b.total) { b.done = true; b.Save(); return $"DONE {b.next}/{b.total}"; }
            var sw = Stopwatch.StartNew();
            string err = EnsureStage(b);
            if (err != null) return "ERROR: " + err;
            float build = (float)sw.Elapsed.TotalSeconds;
            // Rabbits ease their turn and pose: replay a few frames unrendered when resuming on a fresh stage.
            if (stage.posedUpTo != b.next - 1)
                for (int f = Mathf.Max(0, b.next - 12); f < b.next; f++) PoseAll(f);
            int startFrame = b.next;
            var rsw = Stopwatch.StartNew();
            while (b.next < b.total && sw.Elapsed.TotalSeconds < seconds)
            {
                RenderFrame(b, b.next);
                b.next++;
            }
            float spent = (float)rsw.Elapsed.TotalSeconds;
            b.renderSeconds += spent;
            b.rendered += b.next - startFrame;
            b.calls++;
            foreach (var c in stage.cells) FlushLog(c);
            if (b.next >= b.total) b.done = true;
            b.Save();
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}/{2} frames ({3} this call in {4:0.0} s, {5:0.00} frames/s, build {6:0.0} s)",
                b.done ? "DONE" : "MORE", b.next, b.total, b.next - startFrame, spent, (b.next - startFrame) / Mathf.Max(0.01f, spent), build);
        }

        /// <summary>Takes the lab stage down and reopens the scene that was open before the batch.</summary>
        public static string Finish()
        {
            Teardown();
            string prev = File.Exists(PrevScenePath) ? File.ReadAllText(PrevScenePath).Trim() : null;
            if (File.Exists(PrevScenePath)) File.Delete(PrevScenePath);
            if (!string.IsNullOrEmpty(prev) && File.Exists(prev) && SceneManager.GetActiveScene().path != prev)
            {
                EditorSceneManager.OpenScene(prev, OpenSceneMode.Single);
                return "reopened " + prev;
            }
            return "stage down" + (string.IsNullOrEmpty(prev) ? " (no previous scene)" : "");
        }

        /// <summary>One still of a lab.json at a frame (both cameras), for a quick look without the queue.</summary>
        public static string Still(string labPath, int frame, string outDir)
        {
            var b = new LabBatch { id = "still-" + System.DateTime.Now.Ticks, labs = new[] { labPath }, outs = new[] { outDir }, total = frame + 1, next = frame };
            string err = EnsureStage(b);
            if (err != null) return "ERROR: " + err;
            for (int f = Mathf.Max(0, frame - 12); f < frame; f++) PoseAll(f);
            RenderFrame(b, frame);
            FlushLog(stage.cells[0]);
            return outDir;
        }

        // ------------------------------------------------------------------ the stage

        static string EnsureStage(LabBatch b)
        {
            if (stage != null && stage.batchId == b.id && stage.root != null) return null;
            Teardown();
            var active = SceneManager.GetActiveScene();
            if (active.name != "AnimLab")
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) return $"scene '{SceneManager.GetSceneAt(i).name}' has unsaved changes: save or discard them first";
                File.WriteAllText(PrevScenePath, active.path); // Finish() reopens it
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene.name = "AnimLab";
            }
            stage = new Stage { batchId = b.id };
            BuildCommon(stage, b);
            for (int i = 0; i < b.labs.Length; i++)
            {
                LabClip clip;
                try { clip = LabClip.Load(b.labs[i]); }
                catch (System.Exception e) { return $"bad lab file {b.labs[i]}: {e.Message}"; }
                var cell = new Cell { clip = clip, outDir = b.outs[i], origin = new Vector3(i * CellGap, 0f, 0f) };
                Directory.CreateDirectory(Path.Combine(cell.outDir, "frames"));
                BuildCell(stage, cell, i);
                stage.cells.Add(cell);
            }
            return null;
        }

        static void Teardown()
        {
            if (stage != null)
            {
                if (stage.root != null) Object.DestroyImmediate(stage.root);
                foreach (var o in stage.owned) if (o != null) Object.DestroyImmediate(o);
                stage = null;
            }
            // A domain reload forgets the stage but not the scene: clear any root a lost stage left behind.
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "AnimLab")
                foreach (var go in scene.GetRootGameObjects()) if (go.name == "AnimLab") Object.DestroyImmediate(go);
        }

        static void BuildCommon(Stage s, LabBatch b)
        {
            s.root = new GameObject("AnimLab");
            // The box at day (SliceSceneBuilder: the Village box's midday sun, trilight ambient, the slice's post grade).
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(s.root.transform, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(42, -40, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.74f, 0.8f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.65f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.3f);
            RenderSettings.fog = false;

            var camGo = new GameObject("LabCamera");
            camGo.transform.SetParent(s.root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false; // renders only on request
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.9f, 0.86f, 0.8f);
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            s.cam = cam;

            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.transform.SetParent(s.root.transform, false);
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{ViewDir}/Post_Slice.asset"); // shared, not modified
        }

        static Material solidMat, waterMat;
        static Material Solid => solidMat != null ? solidMat : solidMat = AssetDatabase.LoadAssetAtPath<Material>($"{ViewDir}/Materials/Stylized.mat");
        static Material Water => waterMat != null ? waterMat : waterMat = AssetDatabase.LoadAssetAtPath<Material>($"{ViewDir}/Materials/Water.mat") ?? Solid;

        static BoxView boxStub;
        /// <summary>RabbitDoll.Create takes a BoxView for its material: an inactive one that never builds a box.</summary>
        static BoxView BoxStub(Stage s)
        {
            if (boxStub != null) return boxStub;
            var go = new GameObject("BoxStub (material only)");
            go.SetActive(false);
            go.transform.SetParent(s.root.transform, false);
            boxStub = go.AddComponent<BoxView>();
            boxStub.solid = Solid;
            return boxStub;
        }

        static GameObject MeshObject(Stage s, Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, bool shadows = true)
        {
            s.owned.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static void BuildCell(Stage s, Cell c, int index)
        {
            boxStub = null;
            var root = new GameObject($"Cell {index} {c.clip.clip}").transform;
            root.SetParent(s.root.transform, false);
            root.localPosition = c.origin;

            // Ground: a faceted grass patch in the river valley's colours, wide enough for the default camera.
            var land = LandColors.River;
            float half = Mathf.Max(1.2f, c.clip.cam.span * 1.6f);
            var mb = new MeshBuilder(353 + index) { jitter = 0f };
            const float tile = 0.08f;
            int n = Mathf.CeilToInt(half / tile);
            var rng = new System.Random(353 + index);
            for (int i = -n; i < n; i++)
                for (int j = -n; j < n; j++)
                {
                    float x0 = c.clip.cam.fx + i * tile, z0 = c.clip.cam.fz + j * tile;
                    // the box's grass: soft cell-to-cell variation, never a checkerboard
                    var col = Color.Lerp(land.grassA, land.grassB, (float)rng.NextDouble() * 0.6f);
                    if (rng.NextDouble() < 0.12) col = Color.Lerp(col, land.grassC, 0.35f);
                    mb.Quad(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z0 + tile), new Vector3(x0 + tile, 0f, z0 + tile), new Vector3(x0 + tile, 0f, z0), col);
                }
            MeshObject(s, root, "Ground", mb.ToMesh("LabGround"), Solid, new Vector3(0f, -0.002f, 0f), false);

            foreach (var d in c.clip.dolls ?? new LabDoll[0])
            {
                // PeopleView.LookOf for a plain person: PickLook, and gold stays the leader's.
                var look = PegDoll.PickLook(d.seed, d.camp, d.kid, -1);
                if (!look.role && look.accent == Palette.Accents[1]) look.accent = Palette.Accents[(d.seed & 1) == 0 ? 0 : 3];
                var v = PegDollView.Create(root, Solid, look, d.kid, d.seed);
                v.name = d.id;
                s.owned.Add(v.body.GetComponent<MeshFilter>().sharedMesh); // per-doll meshes (the props' are shared by kind)
                s.owned.Add(v.armL.GetComponent<MeshFilter>().sharedMesh);
                c.dolls.Add(new DollRig { data = d, view = v });
            }
            int rabbits = 0;
            foreach (var o in c.clip.objects ?? new LabObject[0])
            {
                var rig = new ObjectRig { data = o };
                var p0 = new Vector3(o.f[0], o.f[1], o.f[2]);
                switch (o.kind)
                {
                    case "rabbit":
                        rig.rabbit = RabbitDoll.Create(BoxStub(s), root, rabbits++);
                        rig.go = rig.rabbit.gameObject;
                        break;
                    case "bush":
                    {
                        var b = new MeshBuilder(index * 31 + 7);
                        Pieces.Bush(b, Vector3.zero, o.radius, false, land);
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabBush"), Solid, p0);
                        var bm = new GameObject("Berries");
                        bm.transform.SetParent(rig.go.transform, false);
                        rig.berries = bm.AddComponent<MeshFilter>();
                        bm.AddComponent<MeshRenderer>().sharedMaterial = Solid;
                        break;
                    }
                    case "fire":
                    {
                        var b = new MeshBuilder(index * 31 + 9);
                        Pieces.Firepit(b, Vector3.zero, false, 0.37f);
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabFire"), Solid, p0);
                        break;
                    }
                    case "tree":
                    {
                        var b = new MeshBuilder(index * 31 + 11);
                        Pieces.FruitTree(b, Vector3.zero, Mathf.Max(0.4f, o.radius / 0.12f * 0.75f));
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabTree"), Solid, p0);
                        break;
                    }
                    case "tent":
                    {
                        var b = new MeshBuilder(index * 31 + 13);
                        Pieces.Teepee(b, Vector3.zero, o.radius, o.radius * 1.5f, new Color(0.83f, 0.74f, 0.58f), 180f, index);
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabTent"), Solid, p0);
                        break;
                    }
                    case "water": case "pond": case "puddle":
                    {
                        var b = new MeshBuilder(index * 31 + 15) { jitter = 0f };
                        float w = o.sx > 0f ? o.sx * 0.5f : Mathf.Max(0.2f, o.radius), dd = o.sz > 0f ? o.sz * 0.5f : w;
                        const int seg = 28;
                        for (int k = 0; k < seg; k++)
                        {
                            float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                            b.Tri(Vector3.zero, new Vector3(Mathf.Cos(a1) * w, 0f, Mathf.Sin(a1) * dd), new Vector3(Mathf.Cos(a0) * w, 0f, Mathf.Sin(a0) * dd), land.water);
                        }
                        // look-fix: the lab's ground is flat at y 0, so water set at the real surface (-0.08) was hidden under the grass: lift it to the ground
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabWater"), Water, new Vector3(p0.x, Mathf.Max(p0.y, 0f) + 0.004f, p0.z), false);
                        break;
                    }
                    case "spear": case "fish": case "basket": case "pot": // look-fix: the stew pot is the game's clay pot, not a grey ball
                    {
                        var kind = o.kind == "spear" ? PropKind.Spear : o.kind == "fish" ? PropKind.Fish : o.kind == "pot" ? PropKind.Pot : PropKind.Basket;
                        rig.go = new GameObject(o.id);
                        rig.go.transform.SetParent(root, false);
                        var part = new GameObject("Mesh");
                        part.transform.SetParent(rig.go.transform, false);
                        part.AddComponent<MeshFilter>().sharedMesh = PegDoll.PropMesh(kind);
                        part.AddComponent<MeshRenderer>().sharedMaterial = Solid;
                        if (kind == PropKind.Spear) part.transform.localRotation = Quaternion.Euler(80f, 0f, 0f); // in flight: point first
                        break;
                    }
                    case "bundle":
                    {
                        // batch 2: the newborn is the game's own baby (PegDoll.BabyMesh, as PegDollView.SetBaby/LiftBaby draw it), not a grey ball
                        rig.go = new GameObject(o.id);
                        rig.go.transform.SetParent(root, false);
                        var part = new GameObject("Baby");
                        part.transform.SetParent(rig.go.transform, false);
                        part.AddComponent<MeshFilter>().sharedMesh = PegDoll.BabyMesh();
                        part.AddComponent<MeshRenderer>().sharedMaterial = Solid;
                        break;
                    }
                    case "rack":
                    {
                        // #543: the camp's fish rack (the game's frame, bar along z, a cord at each of the six places the clip hangs fish), not a grey boulder
                        var b = new MeshBuilder(index * 31 + 23) { jitter = 0f };
                        FishRackPieces.Rack(b, 6);
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabRack"), Solid, p0);
                        break;
                    }
                    case "feather":
                    {
                        // #566: the feather pole, as the game draws it (ConsequenceMeshes at its Scale)
                        rig.go = MeshObject(s, root, o.id, ConsequenceMeshes.Build(AquariumCiv.Sim.ConsequenceKind.Feather), Solid, p0);
                        rig.go.transform.localScale = Vector3.one * ConsequenceMeshes.Scale;
                        break;
                    }
                    case "stash":
                    {
                        // look-fix: the camp's stash is a low heap of berries on a mat, not a grey boulder
                        var b = new MeshBuilder(index * 31 + 21) { jitter = 0f };
                        b.Prism(Vector3.zero, o.radius, o.radius * 0.95f, 0.006f, 10, Palette.Thatch);
                        b.Ico(new Vector3(0f, 0.004f, 0f), o.radius * 0.7f, new Vector3(1f, 0.45f, 1f), Palette.Ember, 0.15f);
                        for (int k = 0; k < 9; k++)
                        {
                            float a = k * 2.39996f, r = o.radius * 0.55f * Mathf.Sqrt((k + 0.5f) / 9f);
                            b.Ico(new Vector3(Mathf.Cos(a) * r, 0.018f + 0.01f * (1f - r / o.radius), Mathf.Sin(a) * r), 0.012f, Vector3.one, new Color(0.78f, 0.1f, 0.15f), 0.1f);
                        }
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabStash"), Solid, p0);
                        break;
                    }
                    case "root":
                    {
                        // look-fix: the dig spot is a patch of turned dirt; a root on an arc is the game's own dug root (BerryPickView.BuildRoot)
                        var b = new MeshBuilder(index * 31 + 19) { jitter = 0f };
                        if (o.act != null && o.act.Length > 0 && o.act[0] == "held") BerryPickView.BuildRoot(b); // an arc object (Bench.StepArc)
                        else b.Prism(Vector3.zero, o.radius, o.radius * 0.8f, 0.006f, 9, Palette.Dirt);
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabRoot"), Solid, p0);
                        break;
                    }
                    default:
                    {
                        // ball, berry, stash and anything else: a small round thing (a berry red, a ball hide-coloured, else grey).
                        var b = new MeshBuilder(index * 31 + 17);
                        var col = o.kind == "berry" ? Palette.Ember : o.kind == "ball" ? new Color(0.78f, 0.6f, 0.42f) : new Color(0.6f, 0.6f, 0.6f);
                        b.Ico(Vector3.up * o.radius, o.radius, Vector3.one, col, 0.1f);
                        rig.go = MeshObject(s, root, o.id, b.ToMesh("LabThing"), Solid, p0);
                        break;
                    }
                }
                rig.go.name = o.id;
                c.objects.Add(rig);
            }
        }

        // ------------------------------------------------------------------ posing (PeopleView.Drive, RabbitView)

        static void PoseAll(int f)
        {
            foreach (var c in stage.cells) if (f < c.clip.frames) PoseCell(c, f);
            stage.posedUpTo = f;
        }

        static void PoseCell(Cell c, int f)
        {
            float dt = 1f / c.clip.fps, time = c.clip.clock + f * dt;
            foreach (var r in c.dolls)
            {
                var a = r.data.f;
                int k = f * LabDoll.Stride;
                var v = r.view;
                bool hidden = a[k + 18] > 0.5f;
                v.gameObject.SetActive(!hidden);
                if (hidden) continue;
                string key = r.data.prop[f];
                if (key != r.propKey)
                {
                    int bar = key.IndexOf('/');
                    var kind = (PropKind)System.Enum.Parse(typeof(PropKind), key.Substring(0, bar));
                    var slot = (PropSlot)System.Enum.Parse(typeof(PropSlot), key.Substring(bar + 1));
                    v.SetProp(kind, slot);
                    r.propKey = key;
                }
                var pose = new DollPose
                {
                    offset = new Vector3(a[k + 4], a[k + 5], a[k + 6]), lean = a[k + 7], roll = a[k + 8], squash = a[k + 9],
                    armL = new Vector3(a[k + 10], a[k + 11], a[k + 12]), armR = new Vector3(a[k + 13], a[k + 14], a[k + 15]),
                    extend = a[k + 16] > 0.5f, hit = a[k + 17], lying = a[k + 19] > 0.5f,
                };
                float s = v.scale;
                var yawQ = Quaternion.Euler(0, a[k + 3], 0); // the drawn yaw: d.yaw + pose.yaw
                var ground = new Vector3(a[k], 0f, a[k + 1]);
                var tilt = pose.lying ? Quaternion.Euler(90f, 0, 0) : Quaternion.Euler(-pose.lean, 0, pose.roll);
                v.transform.SetLocalPositionAndRotation(ground + yawQ * (pose.offset * s), yawQ * tilt);
                v.transform.localScale = new Vector3(1f, pose.squash, 1f);
                v.ApplyArms(pose);
            }
            foreach (var r in c.objects)
            {
                var a = r.data.f;
                int k = f * LabObject.Stride;
                bool visible = a[k + 4] > 0.5f;
                if (r.go.activeSelf != visible) r.go.SetActive(visible);
                if (!visible) continue;
                var pos = new Vector3(a[k], a[k + 1], a[k + 2]);
                float yaw = a[k + 3];
                if (r.rabbit != null)
                {
                    var act = RabbitAct.Graze;
                    System.Enum.TryParse(r.data.act[f], out act);
                    r.go.transform.localPosition = pos;
                    r.rabbit.Face(yaw, dt, act == RabbitAct.Flee);
                    r.rabbit.Show(act, a[k + 5], time);
                    continue;
                }
                if (r.data.kind is "water" or "pond" or "puddle") pos.y = Mathf.Max(pos.y, 0f) + 0.004f; // look-fix: on the flat lab ground, not under it
                r.go.transform.localPosition = pos;
                if (r.data.kind == "bundle")
                {
                    // batch 2: held across the chest (-68 deg, as SetBaby), upright once lifted over the head (LiftBaby), turned with its holder
                    DollRig near = null; float best = float.MaxValue;
                    foreach (var d in c.dolls)
                    {
                        var dp = d.view.transform.localPosition;
                        float dd = (dp.x - pos.x) * (dp.x - pos.x) + (dp.z - pos.z) * (dp.z - pos.z);
                        if (dd < best) { best = dd; near = d; }
                    }
                    float lift = Mathf.InverseLerp(0.12f, 0.22f, pos.y);
                    var turn = near != null ? Quaternion.Euler(0f, near.view.transform.localEulerAngles.y, 0f) : Quaternion.identity;
                    r.go.transform.localRotation = turn * Quaternion.Euler(0f, 0f, -68f * (1f - lift));
                }
                if (r.data.kind is "spear" or "fish" or "ball" or "berry") r.go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                if (r.berries != null)
                {
                    int n = Mathf.Clamp(Mathf.RoundToInt(a[k + 6]), 0, 12);
                    if (n != r.berryCount) { SetBerries(r, n); r.berryCount = n; }
                }
            }
        }

        static void SetBerries(ObjectRig r, int n)
        {
            var old = r.berries.sharedMesh;
            var mb = new MeshBuilder(r.data.id.GetHashCode());
            float rad = r.data.radius;
            // On the leaf ball's skin (Pieces.Bush: an ico at y = r/2, radii r * (1.2, 0.8, 1.1)), spread round its upper half.
            for (int i = 0; i < n; i++)
            {
                float a = i * 2.39996f, up = 0.25f + 0.5f * ((i * 53) % 10) / 10f, flat = Mathf.Sqrt(1f - up * up);
                var dir = new Vector3(Mathf.Cos(a) * flat * 1.2f, up * 0.8f, Mathf.Sin(a) * flat * 1.1f);
                mb.Ico(new Vector3(0f, rad * 0.5f, 0f) + dir * rad * 1.02f, 0.018f, Vector3.one, Palette.Ember, 0.1f);
            }
            r.berries.sharedMesh = n > 0 ? mb.ToMesh("LabBerries") : null;
            if (old != null) Object.DestroyImmediate(old);
            if (r.berries.sharedMesh != null) stage.owned.Add(r.berries.sharedMesh);
        }

        // ------------------------------------------------------------------ rendering

        /// <summary>Bench.ToolTip (Tools/AnimBench/src/Bench.cs): the working end of a hand prop in its own frame.</summary>
        static Vector3 ToolTip(PropKind k) => k switch
        {
            PropKind.Spear => new Vector3(0, 0.205f, 0),
            PropKind.Axe => new Vector3(0, 0.115f, -0.056f),
            PropKind.Hoe => new Vector3(0, 0.17f, -0.045f),
            PropKind.Rod => new Vector3(0, 0.29f, 0),
            PropKind.MeasuringRod => new Vector3(0, 0.29f, 0),
            PropKind.Hammer => new Vector3(0, 0.1f, -0.03f),
            PropKind.Broom => new Vector3(0, -0.09f, 0),
            PropKind.Stick => new Vector3(0, 0.18f, 0),
            PropKind.Twig => new Vector3(0, 0.13f, 0), // #359 batch 2: the duel's twig (PegDoll.PropMesh)
            PropKind.Fish => new Vector3(0, -0.01f, -0.012f),
            _ => Vector3.zero,
        };

        static void Aim(Camera cam, Vector3 origin, LabCam v, float aspect)
        {
            var target = origin + new Vector3(v.fx, 0.07f, v.fz);
            float vfov = cam.fieldOfView * Mathf.Deg2Rad;
            float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * 0.5f) * aspect);
            float dist = v.span * 0.5f / Mathf.Tan(hfov * 0.5f);
            cam.transform.position = target + Quaternion.Euler(v.pitch, v.yaw, 0) * Vector3.back * dist;
            cam.transform.LookAt(target);
            cam.aspect = aspect;
        }

        static void Shoot(Camera cam, int w, int h, string path)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var keep = RenderTexture.active;
            RenderTexture.active = rt;
            if (readback == null || readback.width != w || readback.height != h)
            {
                if (readback != null) Object.DestroyImmediate(readback);
                readback = new Texture2D(w, h, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave };
            }
            readback.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readback.Apply(false);
            RenderTexture.active = keep;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, readback.EncodeToJPG(92));
        }

        static void RenderFrame(LabBatch b, int f)
        {
            PoseAll(f);
            var cam = stage.cam;
            foreach (var c in stage.cells)
            {
                if (f >= c.clip.frames) continue;
                string dir = Path.Combine(c.outDir, "frames");
                Aim(cam, c.origin, c.clip.cam, b.dw / (float)b.dh);
                var vpD = cam.projectionMatrix * cam.worldToCameraMatrix;
                Shoot(cam, b.dw, b.dh, Path.Combine(dir, $"d_{f:00000}.jpg"));
                Aim(cam, c.origin, c.clip.close, b.cw / (float)b.ch);
                var vpC = cam.projectionMatrix * cam.worldToCameraMatrix;
                Shoot(cam, b.cw, b.ch, Path.Combine(dir, $"c_{f:00000}.jpg"));
                // The check: the real doll's hand and tool tip against AnimBench's (Bench.Place copies PegDollView's build),
                // and where the tip lands on screen in each camera (post.py rings it on hit frames).
                foreach (var r in c.dolls)
                {
                    var a = r.data.f;
                    int k = f * LabDoll.Stride;
                    if (a[k + 18] > 0.5f) continue;
                    var v = r.view;
                    float s = v.scale;
                    var hand = v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * s, 0f)) - c.origin;
                    var tip = hand;
                    if (v.prop != null && v.propSlot == PropSlot.Hand && ToolTip(v.propKind) != Vector3.zero)
                        tip = v.prop.TransformPoint(ToolTip(v.propKind)) - c.origin;
                    var benchHand = new Vector3(a[k + 23], a[k + 24], a[k + 25]);
                    var benchTip = new Vector3(a[k + 20], a[k + 21], a[k + 22]);
                    var wTip = tip + c.origin;
                    var sd = Viewport(vpD, wTip);
                    var sc = Viewport(vpC, wTip);
                    c.log.AppendFormat(CultureInfo.InvariantCulture, "{0}\t{1}\t{2:0.##}\t{3:0.0000}\t{4:0.0000}\t{5:0.0000}\t{6:0.0000}\t{7:0.0000}\t{8:0.0000}\n",
                        f, r.data.id, a[k + 17], (hand - benchHand).magnitude, (tip - benchTip).magnitude, sd.x, sd.y, sc.x, sc.y);
                }
            }
        }

        static Vector2 Viewport(Matrix4x4 vp, Vector3 w)
        {
            var p = vp * new Vector4(w.x, w.y, w.z, 1f);
            if (p.w <= 1e-5f) return new Vector2(-1f, -1f);
            return new Vector2(p.x / p.w * 0.5f + 0.5f, p.y / p.w * 0.5f + 0.5f);
        }

        static void FlushLog(Cell c)
        {
            if (c.log.Length == 0) return;
            File.AppendAllText(Path.Combine(c.outDir, "lab-check.tsv"), c.log.ToString());
            c.log.Clear();
        }
    }
}
