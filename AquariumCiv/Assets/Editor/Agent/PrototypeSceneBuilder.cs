using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes;
using AquariumCiv.Prototypes.P1;
using AquariumCiv.Prototypes.P2;
using AquariumCiv.Prototypes.Hub;
using AquariumCiv.Prototypes.P3;
using AquariumCiv.Prototypes.P4;
using AquariumCiv.Prototypes.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static AquariumCiv.EditorTools.DioramaBuilder;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Generates prototype scenes on top of the shared diorama + P0 systems (sim clock, views, logging, debug, survey).
    /// Each prototype gets its own menu item; scenes are throwaway and safe to rebuild.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string SharedDir = "Assets/Prototypes/_Shared";
        const int Radius = 3;

        public class Built
        {
            public Diorama diorama;
            public GameObject systems;
            public DioramaView village;
            public CrowdView crowd;
            public MessView mess;
            public GlassGrime glass;
            public SimClock clock;
            public NotableView notables;
            public WipeCleaner wipe;
            public string scenePath;
        }

        [MenuItem("AquariumCiv/Agent/Prototypes/Build P0 Sandbox")]
        public static string BuildSandbox()
        {
            var b = BuildBase("P0", $"{SharedDir}/Sandbox.unity", "Cleaning felt satisfying", TestMode.Lab);
            Save(b);
            return Report(b);
        }

        [MenuItem("AquariumCiv/Agent/Prototypes/Build P1 Glass and Mess")]
        public static string BuildP1()
        {
            var b = BuildBase("P1", "Assets/Prototypes/P1_GlassMess/P1_GlassMess.unity", "Cleaning felt satisfying", TestMode.Lab);
            // Lab pacing: the civ runs at x60 (1 real minute = 1 game year, ambient only) and mess grows back within
            // minutes, so testers meet both a dirty tank and a nearly clean one in a 7-minute session.
            b.clock.speed = 60f;
            b.clock.config.messFullHours = 10f;     // x60: full grime in ~10 real minutes
            b.clock.config.trashPerPersonHour = 0.12f;
            b.clock.config.outputPenaltyEnabled = false; // P1 tests the gesture only; consequences are P2
            var gestures = b.systems.AddComponent<CleaningGestures>();
            gestures.glass = b.glass;
            gestures.mess = b.mess;
            Save(b);
            return Report(b);
        }

        [MenuItem("AquariumCiv/Agent/Prototypes/Build P2 Return to Mess")]
        public static string BuildP2()
        {
            // Diary by default: real time at design speed (1 real hour = 1 game year, mess caps at 48 h).
            // A lab session switches the device to Lab (debug panel), which adds "step away" buttons.
            var b = BuildBase("P2", "Assets/Prototypes/P2_ReturnToMess/P2_ReturnToMess.unity", "I felt bad or guilty when I opened it", TestMode.Diary);
            var hud = b.systems.GetComponent<HudToast>();
            hud.welcomeBack = false; // P2 writes its own one-line summary
            hud.bannerTop = 130f;      // below the two-line ledger
            hud.bannerBelowSafe = 96f; // ...also under a notch (ledger is ~75 tall at safeTop + 4)
            var wipe = b.systems.AddComponent<WipeCleaner>();
            wipe.glass = b.glass;
            wipe.mess = b.mess;
            b.mess.tapCleansProps = false;
            b.mess.trashScale = 2.4f; // junk and weeds read at default zoom on a phone (tester run 3: ~8 pt / ~4 pt before)
            b.mess.vineScale = 1.8f;
            var p2 = b.systems.AddComponent<ReturnToMess>();
            p2.wipe = wipe;
            p2.crowd = b.crowd;
            Save(b);
            return Report(b);
        }

        [MenuItem("AquariumCiv/Agent/Prototypes/Build Test Hub")]
        public static string BuildHub()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.10f, 0.13f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var sys = new GameObject("Hub");
            var cfg = sys.AddComponent<PrototypeConfig>();
            cfg.protoId = "Hub";
            cfg.variants = new[] { "-" };
            sys.AddComponent<EventLog>();
            sys.AddComponent<FeedbackSender>();
            sys.AddComponent<TestHub>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(HubScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), HubScenePath);
            AddToBuild(HubScenePath);
            return $"Built {HubScenePath}; build order: {string.Join(", ", EditorBuildSettings.scenes.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)))}";
        }


        [MenuItem("AquariumCiv/Agent/Prototypes/Build P3 While You Were Away")]
        public static string BuildP3()
        {
            // Lab by default (the Test Hub opens it in lab or diary). Diary = real time at design speed.
            var b = BuildLabBase("P3", "Assets/Prototypes/P3_WhileYouWereAway/P3_WhileYouWereAway.unity", "I care about specific people in my tank");
            var cards = b.systems.AddComponent<ChronicleCards>();
            var p3 = b.systems.AddComponent<WhileYouWereAway>();
            WireLab(b, p3);
            p3.cards = cards;
            Save(b);
            return Report(b);
        }

        [MenuItem("AquariumCiv/Agent/Prototypes/Build P4 Book of Ages")]
        public static string BuildP4()
        {
            var b = BuildLabBase("P4", "Assets/Prototypes/P4_BookOfAges/P4_BookOfAges.unity", "I'd show this to a friend");
            var timelapse = b.systems.AddComponent<TimelapseView>();
            timelapse.village = b.village;
            timelapse.crowd = b.crowd;
            timelapse.notables = b.notables;
            var book = b.systems.AddComponent<IllustratedBook>();
            var p4 = b.systems.AddComponent<BookOfAges>();
            WireLab(b, p4);
            p4.timelapse = timelapse;
            p4.book = book;
            p4.prehistoryYears = 100f; // a century to browse
            Save(b);
            return Report(b);
        }

        /// <summary>P3+ base: the shared diorama plus the wipe gesture, named notables walking the village, portraits,
        /// person cards, the A/B lab strip and the lab HUD. The care layer has no consequences (set by PrototypeBase).</summary>
        public static Built BuildLabBase(string protoId, string scenePath, string surveyQ4)
        {
            var b = BuildBase(protoId, scenePath, surveyQ4, TestMode.Lab);
            b.systems.GetComponent<PrototypeConfig>().separateLabSave = true; // a lab test never touches the phone's diary
            var hud = b.systems.GetComponent<HudToast>();
            hud.welcomeBack = false;   // each prototype writes its own welcome
            hud.bannerTop = 100f;      // below the one-line ledger
            hud.bannerBelowSafe = 76f; // ...also under a notch (the lab strip adds its own height on top)
            var wipe = b.systems.AddComponent<WipeCleaner>();
            wipe.glass = b.glass;
            wipe.mess = b.mess;
            wipe.peopleFirst = true; // a tap near a named person meets them, junk or not (P3 tester run 1)
            b.mess.tapCleansProps = false;
            b.mess.trashScale = 2.4f; // as P2 (readable on a phone)
            b.mess.vineScale = 1.8f;
            b.crowd.tagNotables = false;
            var notables = new GameObject("Notables").AddComponent<NotableView>();
            notables.transform.SetParent(b.crowd.transform.parent);
            notables.crowd = b.crowd;
            var studio = b.systems.AddComponent<PortraitStudio>();
            studio.characterPrefabs = b.crowd.characterPrefabs;
            studio.idleClip = b.crowd.idleClip;
            b.systems.AddComponent<NotableCardPanel>();
            b.systems.AddComponent<LabStrip>();
            b.systems.AddComponent<LabHud>();
            b.notables = notables;
            b.wipe = wipe;
            return b;
        }

        static void WireLab(Built b, PrototypeBase p)
        {
            p.wipe = b.wipe;
            p.crowd = b.crowd;
            p.notables = b.notables;
            p.hud = b.systems.GetComponent<LabHud>();
        }

        /// <summary>The shared starting point for every prototype scene (not saved yet: callers add their own bits, then Save).</summary>
        public static Built BuildBase(string protoId, string scenePath, string surveyQ4, TestMode mode)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var b = new Built { scenePath = scenePath };

            var water = new[] { new Vector2Int(2, -1), new(3, -1), new(3, -2), new(2, -2) };
            var d = b.diorama = BuildGround(Radius, water);

            // Static nature on the rim; everything else is free for the sim to build on.
            var nature = new (Vector2Int cell, string path, float yaw)[]
            {
                (new(-3, 3), N("mountain_A_grass_trees"), 0),
                (new(-3, 2), N("hills_A_trees"), 60),
                (new(-2, 3), N("hills_B_trees"), 0),
                (new(0, -3), N("trees_A_large"), 0),
                (new(1, -3), N("trees_B_medium"), 90),
                (new(-3, 0), N("trees_A_small"), 0),
                (new(3, -3), N("trees_B_small"), 0),
            };
            PlaceProps(d, "Nature", nature);

            // Building slots: free cells, center-out.
            var taken = new HashSet<Vector2Int>(water.Concat(nature.Select(n => n.cell)));
            var free = d.cells.Where(c => !taken.Contains(c))
                .OrderBy(c => d.cellPos[c].sqrMagnitude).ThenBy(c => c.x).ThenBy(c => c.y).ToList();
            var rng = new System.Random(11);
            var slots = free.Select(d.Top).ToArray();
            var yaws = free.Select(_ => rng.Next(0, 6) * 60f).ToArray();

            // Invisible ground collider so taps and "look" samples can tell ground from empty space.
            var groundCol = new GameObject("GroundCollider");
            groundCol.transform.SetParent(d.root);
            var radiusWorld = d.w * (Radius + 0.5f);
            groundCol.transform.position = new Vector3(0, d.surfaceY - 0.02f, 0);
            groundCol.AddComponent<BoxCollider>().size = new Vector3(radiusWorld * 2, 0.04f, radiusWorld * 2);
            LogCategory.Tag(groundCol, "ground");

            BuildTerrarium(d);
            var cam = BuildLightingAndCamera(d, $"{SharedDir}/PostFX.asset");
            var orbit = cam.gameObject.AddComponent<OrbitCamera>();
            orbit.pivot = d.cameraTarget;

            // Systems
            var sys = b.systems = new GameObject("Prototype");
            var cfg = sys.AddComponent<PrototypeConfig>();
            cfg.protoId = protoId;
            cfg.surveyQ4 = surveyQ4;
            cfg.testMode = mode;
            var clock = b.clock = sys.AddComponent<SimClock>();
            clock.config.buildingSlots = slots.Length;
            sys.AddComponent<EventLog>();
            sys.AddComponent<HudToast>();
            sys.AddComponent<SurveyPanel>();
            sys.AddComponent<FeedbackSender>();
            sys.AddComponent<FeedbackPanel>();

            // Village
            var village = b.village = new GameObject("Village").AddComponent<DioramaView>();
            village.transform.SetParent(d.root);
            village.buildingRoot = village.transform;
            village.slotPositions = slots;
            village.slotYaw = yaws;
            village.slotRadius = d.w * 0.4f;
            village.prefabs = new[]
            {
                Set(BuildingType.Camp, P("tent")),
                Set(BuildingType.Hut, B("home_A"), B("home_B")),
                Set(BuildingType.Farm, Neutral("building_grain")),
                Set(BuildingType.Lumbermill, B("lumbermill")),
                Set(BuildingType.Well, B("well")),
                Set(BuildingType.Windmill, B("windmill")),
                Set(BuildingType.Market, B("market")),
                Set(BuildingType.Tavern, B("tavern")),
                Set(BuildingType.Church, B("church")),
            };

            // Crowd
            var crowd = b.crowd = new GameObject("Crowd").AddComponent<CrowdView>();
            crowd.transform.SetParent(d.root);
            crowd.diorama = village;
            crowd.characterPrefabs = CharacterNames.Select(n => Load(Character(n))).Where(p => p != null).ToArray();
            var clipSource = Character("Barbarian");
            crowd.walkClip = Clip(clipSource, "Walking_A");
            crowd.idleClip = Clip(clipSource, "Idle");
            crowd.cheerClip = Clip(clipSource, "Cheer");
            float charHeight = MeasureBounds(crowd.characterPrefabs[0]).size.y;
            crowd.characterScale = d.homeHeight * 0.5f / Mathf.Max(charHeight, 0.01f);
            crowd.walkSpeed = d.w * 0.25f;

            // Mess
            var mess = b.mess = new GameObject("Mess").AddComponent<MessView>();
            mess.transform.SetParent(d.root);
            mess.diorama = village;
            mess.crowd = crowd;
            mess.trashPrefabs = new[] { "sack", "crate_open", "bucket_empty", "barrel", "pallet", "crate_B_small" }.Select(n => Load(P(n))).ToArray();
            mess.trashScale = 1.5f;
            mess.vinePrefabs = new[] { "waterplant_A", "waterplant_B", "waterplant_C", "tree_single_A" }.Select(n => Load(N(n))).ToArray();
            mess.vineScale = 1.0f;
            mess.puffMaterial = GetOrCreateMaterial("Puff", m =>
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetFloat("_ZWrite", 0);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetColor("_BaseColor", Color.white);
            }, "Universal Render Pipeline/Particles/Unlit", $"{SharedDir}/Materials");

            // Glass grime (paintable mask) and shared cleaning feedback
            var glass = b.glass = new GameObject("GlassGrime").AddComponent<GlassGrime>();
            glass.transform.SetParent(d.root);
            glass.glassRenderers = d.glass;
            glass.grimeMaterial = GetOrCreateMaterial("GlassGrime", m =>
            {
                m.SetColor("_CleanColor", new Color(0.85f, 0.95f, 1f, 0.10f));
                m.SetColor("_GrimeColor", new Color(0.42f, 0.47f, 0.30f, 1f));
                m.SetFloat("_DirtyAlpha", 0.8f);
            }, "AquariumCiv/GlassGrime", $"{SharedDir}/Materials");
            sys.AddComponent<Juice>().particleMaterial = mess.puffMaterial;

            sys.AddComponent<DebugPanel>().mess = mess;
            return b;
        }

        public static void Save(Built b)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(b.scenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), b.scenePath);
            AddToBuild(b.scenePath);
        }

        /// <summary>Newest build first, except the Test Hub, which always stays first (it opens everything else).</summary>
        public static void AddToBuild(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            var hub = scenes.FirstOrDefault(s => s.path == HubScenePath);
            if (hub != null) { scenes.Remove(hub); scenes.Insert(0, hub); }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public const string HubScenePath = "Assets/Prototypes/_Hub/Hub.unity";

        static string Report(Built b) =>
            $"Built {b.scenePath}: {b.village.slotPositions.Length} slots, glass panes {b.diorama.glass.Length}, " +
            $"crowd scale {b.crowd.characterScale:F2}, clips {(b.crowd.walkClip ? "ok" : "MISSING")}/{(b.crowd.idleClip ? "ok" : "MISSING")}/{(b.crowd.cheerClip ? "ok" : "MISSING")}";

        static DioramaView.BuildingPrefabs Set(string type, params string[] paths) =>
            new() { type = type, prefabs = paths.Select(Load).Where(p => p != null).ToArray() };
    }
}
