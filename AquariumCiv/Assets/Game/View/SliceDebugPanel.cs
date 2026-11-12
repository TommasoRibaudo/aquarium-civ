using AquariumCiv.Game;
using AquariumCiv.GameUI;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// TEMPORARY on-screen debug for the slice (until the debug panel arrives with the UI): skip years, cross the era
    /// with its rebuild wave, replay the wave, build a hut or a farm, flip day and night, so camp, village and town can
    /// be looked at in play mode. A small "dbg" button opens it (top left). Everything goes through the
    /// <see cref="SimLayoutSource"/> debug hooks, which also work from `script eval` in edit mode.
    /// </summary>
    [AddComponentMenu("AquariumCiv/TEMPORARY Slice Debug Panel")]
    public class SliceDebugPanel : MonoBehaviour
    {
        public bool open { get => DebugDock.Active == DebugDock.Dbg; set => DebugDock.Active = value ? DebugDock.Dbg : DebugDock.None; }
        string last = "";
        float contentH = 800f;
        Vector2 scroll;
        bool soundOpen;
        int cueIndex;
        static string[] cueIds;
        static string[] CueIds => cueIds ??= System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(AquariumCiv.Audio.CueLibrary.All.Keys, k => k));

        void Update() => DebugDock.WatchCorner();

        /// <summary>Light and camera (#113, #175, #146, #162): the hour, the look, how dark the night may be, the near glass, the closest zoom.</summary>
        void DrawLight(PeopleView people)
        {
            var day = DayLightDriver.Instance;
            if (day == null || people == null) return;
            GUILayout.Label(DayLightDriver.Status());
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{people.ViewHour:00.0} h", GUILayout.Width(56));
            float h = GUILayout.HorizontalSlider(people.ViewHour, 0f, 23.99f);
            if (!Mathf.Approximately(h, people.ViewHour)) last = PeopleView.DebugSetHour(h);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (float at in new[] { 6f, 12f, 17.5f, 19.5f, 22f })
                if (GUILayout.Button(at.ToString("0.#"), GUILayout.Width(46))) last = PeopleView.DebugSetHour(at);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(day.ClockPaused ? "Clock: paused" : "Clock: runs")) last = DayLightDriver.DebugPauseClock(!day.ClockPaused);
            GUILayout.BeginHorizontal();
            foreach (AquariumCiv.Lighting.LightLook look in System.Enum.GetValues(typeof(AquariumCiv.Lighting.LightLook)))
            {
                string name = look == AquariumCiv.Lighting.LightLook.SoftBlueDusk ? "Dusk" : look == AquariumCiv.Lighting.LightLook.SoftBlueMoon ? "Moon" : look == AquariumCiv.Lighting.LightLook.Lamplight ? "Lamp" : "Today";
                if (GUILayout.Button((day.Logic.Look == look ? ">" : "") + name, GUILayout.Width(58))) last = DayLightDriver.DebugLook(look);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Floor {day.Logic.NightFloor:0.00}", GUILayout.Width(80));
            float floor = GUILayout.HorizontalSlider(day.Logic.NightFloor, AquariumCiv.Lighting.DayLight.MinNightFloor, AquariumCiv.Lighting.DayLight.MaxNightFloor);
            if (!Mathf.Approximately(floor, day.Logic.NightFloor)) last = DayLightDriver.DebugNightFloor(floor);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool forced = BoxView.NearGlassForce >= 0f;
            if (GUILayout.Button(forced ? "Near glass: forced" : "Near glass: zoom")) BoxView.NearGlassForce = forced ? -1f : 1f;
            if (forced) BoxView.NearGlassForce = GUILayout.HorizontalSlider(BoxView.NearGlassForce, 0f, 1f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Doll +{PeopleView.DollScaleAmount * 100f:0} %", GUILayout.Width(80));
            PeopleView.DollScaleAmount = GUILayout.HorizontalSlider(PeopleView.DollScaleAmount, 0f, 0.4f);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Canal across a path (bridge)")) last = DebugCanalAcrossPath();
            // ---- #588 / #589: the village fire pit and the supper played out (the supper choice itself: DilemmaCheats "Small: supper")
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Village evening dance now")) last = DebugVillageEvening();
            if (GUILayout.Button("Skip to supper")) last = PeopleView.DebugSetHour(17.6f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Supper: one pot")) last = DebugSupper(AquariumCiv.Sim.SupperPick.Pot);
            if (GUILayout.Button("Supper: baskets")) last = DebugSupper(AquariumCiv.Sim.SupperPick.Baskets);
            GUILayout.EndHorizontal();
            // ---- end #588 / #589
            var rig = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (rig == null) return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fly in: fire")) last = DebugFlyFire(people, rig);
            if (GUILayout.Button("Fly in: door")) last = DebugFlyDoor(people, rig);
            if (GUILayout.Button("Go home")) rig.GoHome();
            GUILayout.EndHorizontal();
            // ---- t1-camera (tester run 1, items 1-2): the default camera frames the camp; a pinch out still shows the whole box.
            GUILayout.Label(CampFrameView.Status());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Camera: camp frame")) last = CampFrameView.DebugCampFrame();
            if (GUILayout.Button("Camera: whole box")) last = CampFrameView.DebugWholeBox();
            GUILayout.EndHorizontal();
            // ---- end t1-camera
        }

        /// <summary>#588: a village (the camp crosses over if it still is one) at the evening gathering, 18.6 h: the ring dance round the lit pit.</summary>
        public static string DebugVillageEvening()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            if (sim.State.era == AquariumCiv.Sim.Era.Camp) { GameClock.DebugSkipHours(12f); sim.ForceNextEra(); }
            if (sim.State.era != AquariumCiv.Sim.Era.Village) return "not a village";
            return "village evening: " + PeopleView.DebugSetHour(18.6f);
        }

        /// <summary>#589: settle supper as if the Keeper picked (tonight and tomorrow), then show the hour supper starts.</summary>
        public static string DebugSupper(int pick)
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            sim.DebugSupper(pick);
            return (pick == AquariumCiv.Sim.SupperPick.Pot ? "one big pot" : "a basket each") + ": " + PeopleView.DebugSetHour(17.6f);
        }

        /// <summary>#274: digs a canal across the path nearest the green; a bridge is drawn where they cross.</summary>
        public static string DebugCanalAcrossPath()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            var st = sim?.Settlement;
            if (st == null) return "no sim";
            int c = st.DebugCanalAcrossPath();
            if (c < 0) return "no path to cross yet";
            var src = SimLayoutSource.Find();
            if (src != null) src.GetComponent<BoxView>().Refresh();
            return $"canal dug across the path at cell {c}";
        }

        static string DebugFlyFire(PeopleView people, BoxCamera rig)
        {
            var at = people.Planner != null ? people.Planner.Positions : null;
            if (at == null || !at.TryGetPosition(AquariumCiv.Life.Place.Land(AquariumCiv.Life.PlaceKind.Fire), out float x, out float z)) return "no fire";
            rig.DebugFlyIn(people.transform.TransformPoint(new Vector3(x, 0f, z)), AquariumCiv.Lighting.CameraReach.MinZoom);
            return "closest, at the fire";
        }

        static string DebugFlyDoor(PeopleView people, BoxCamera rig)
        {
            var box = people.GetComponent<BoxView>();
            if (box == null || box.LampCount == 0) return "no doors";
            int k = box.LampCount / 2;
            rig.DebugFlyIn(new Vector3(box.LampX[k], 0f, box.LampZ[k]), AquariumCiv.Lighting.CameraReach.MinZoom);
            return "closest, at a door";
        }

        int rowCount;
        bool inRow;

        void Row() { GUILayout.BeginHorizontal(); inRow = true; rowCount = 0; }
        void EndRow() { GUILayout.EndHorizontal(); inRow = false; }

        /// <summary>A button in the current row, which wraps after three: one long row used to stretch the whole panel past its width, and every other row with it.</summary>
        bool Btn(string text, params GUILayoutOption[] options)
        {
            if (inRow && rowCount == 3) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); rowCount = 0; }
            rowCount++;
            return GUILayout.Button(text, options);
        }

        /// <summary>P8, the living land (#244–#248): a barren box, mists, seasons, the gates, the water and the tree.</summary>
        void DrawEco()
        {
            GUILayout.Label("Living land (P8): " + EcoDebug.Status());
            GUILayout.Label("Jobs: " + EcoDebug.Jobs());
            Row();
            if (Btn("Barren box")) last = EcoDebug.NewBox(true);
            if (Btn("Old box")) last = EcoDebug.NewBox(false);
            if (Btn("Mist")) last = EcoDebug.MistRandom();
            if (Btn("Mist camp")) last = EcoDebug.MistCamp();
            if (Btn("Mist sound A/B")) { var ms = EcoMistView.Instance != null ? EcoMistView.Instance.Sound : null; last = ms != null ? ms.ToggleSource() : "no mister"; }
            EndRow();
            Row();
            if (Btn("Mist big here")) last = EcoDebug.MistBigCamp();
            if (Btn("Mist trees")) last = EcoDebug.MistTrees(); // #569
            if (Btn("Mist one half, skip 3 yr")) last = EcoDebug.MistHalf(12); // #569
            if (Btn("Puddles now")) last = EcoDebug.PuddlesNow();
            if (Btn("Puddle visit")) last = EcoDebug.PuddleVisit();
            EndRow();
            Row(); // #360
            if (Btn("Water run now")) last = PeopleView.DebugWaterRun();
            EndRow();
            Row();
            if (Btn("Water breaks the surface now")) last = EcoDebug.WaterBreaksNow();
            if (Btn("Frogs now")) last = EcoDebug.FrogsNow();
            if (Btn("Ducks now")) last = EcoDebug.DucksNow();
            EndRow();
            Row(); // #364
            if (Btn("Fish bite now")) last = FishingView.DebugBiteNow();
            EndRow();
            Row(); // #361
            if (Btn("Pickers to the nearest bush now")) last = BerryPickView.DebugPickersToBush();
            if (Btn("Root dig now")) last = BerryPickView.DebugRootDigNow();
            if (Btn("Tip at the stash now")) last = BerryPickView.DebugTipNow();
            EndRow();
            Row();
            if (Btn("First bush now")) last = EcoDebug.FirstBushNow();
            if (Btn("+3 bushes")) last = EcoDebug.AddBushes(3);
            if (Btn("Bush carpet (old save)")) last = EcoDebug.CarpetBushes();
            EndRow();
            Row();
            if (Btn("Rabbit tracks now")) last = EcoDebug.RabbitTracksNow();
            if (Btn("+1 rabbit")) last = EcoDebug.AddRabbits(1);
            if (Btn("+5 rabbits")) last = EcoDebug.AddRabbits(5);
            EndRow();
            Row(); // #333
            if (Btn("Spawn rabbits here")) last = RabbitView.Instance != null ? RabbitView.Instance.DebugSpawnHere(4) : "no rabbit view";
            if (Btn("Startle all")) last = RabbitView.Instance != null ? "startled " + RabbitView.Instance.DebugStartleAll() : "no rabbit view";
            EndRow();
            Row(); // #358 kids' play: each game at the gathering place for 45 s
            if (Btn("Kids play: tag")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.Tag);
            if (Btn("Kids play: ball")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.Ball);
            if (Btn("Kids play: chase rabbits")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.ChaseRabbits);
            if (Btn("Toddle")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.Toddle);
            if (Btn("Dance ring now")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.DanceRing);
            if (Btn("Two kids play")) last = KidPlayDirector.DebugTwo(); // fb4 #541
            EndRow();
            Row(); // #365 B9 meals and the fire
            if (Btn("Meal now")) last = PeopleView.DebugMealNow();
            if (Btn("Poke the fire now")) last = PeopleView.DebugPokeFireNow();
            // #367 the background-life director: a story always in progress on screen, as busy as the population says
            GUILayout.Label(StoryDirectorView.DebugStatus());
            Row();
            if (Btn("Story density: off / normal / high (now " + StoryDirectorView.Density + ")")) last = StoryDirectorView.CycleDensity();
            EndRow();
            Row(); // #359 kids' play 2: at the real water, round a real tree, hide and seek round real cover, snowballs
            if (Btn("Kids play: splash")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.Splash);
            if (Btn("Kids play: woods")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.Woods);
            if (Btn("Kids play: hide and seek")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.HideSeek);
            if (Btn("Kids play: snowball")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.Snowball);
            EndRow();
            Row();
            if (Btn("Start a hunt now")) last = EcoDebug.StartHunt();
            if (Btn("Next hunt succeeds")) last = EcoDebug.ForceHunt(true);
            if (Btn("Next hunt misses")) last = EcoDebug.ForceHunt(false);
            if (Btn("Slow-mo next hunt")) last = EcoDebug.HuntSlowMoNext();   // #362
            if (Btn("Make a hunter")) last = EcoDebug.MakeHunter();   // #574
            if (Btn("Gather an egg now")) last = EcoDebug.GatherEggNow();   // #574
            EndRow();
            Row();
            if (Btn("Skip to next season")) last = EcoDebug.SkipToNextSeason(); // #576: the real clock, to the middle of the next season
            if (Btn("+1 season")) last = EcoDebug.SkipSeasons(1);
            if (Btn("+1 yr")) last = EcoDebug.SkipSeasons(4);
            if (Btn("+5 yr")) last = EcoDebug.SkipSeasons(20);
            if (Btn("Camp gate")) last = EcoDebug.CampGate();
            if (Btn("+2 hides")) last = EcoDebug.AddHides(2);
            if (Btn("Rack 1 hide")) last = EcoDebug.ShowRack(1); // #332
            if (Btn("Rack 6 hides")) last = EcoDebug.ShowRack(6); // #332
            if (Btn("Village gate")) last = EcoDebug.VillageGate();
            EndRow();
            Row();
            if (Btn("Water up")) last = EcoDebug.Table(0.004f);
            if (Btn("Pond +1 step")) last = EcoDebug.PondStep(); // fb4 #495
            if (Btn("Settlers: woods")) last = EcoDebug.SettlersArrive(false); // fb4 #539
            if (Btn("Settlers: shore")) last = EcoDebug.SettlersArrive(true); // fb4 #539
            if (Btn("Pond to full")) last = EcoDebug.PondToFull(); // fb4 #539
            if (Btn("Travellers bring wood now")) last = ApartGroupsView.DebugWoodNow(); // fb5 #578
            if (Btn("Travellers join now")) last = ApartGroupsView.DebugJoinNow(); // fb5 #578
            if (Btn("Dry air")) last = EcoDebug.Humidity(0.1f);
            if (Btn("Humid air")) last = EcoDebug.Humidity(0.85f);
            if (Btn("Plant tree")) last = EcoDebug.PlantHere();
            if (Btn("Plant at edge")) last = EcoDebug.PlantAtEdge(); // #378
            if (Btn("Seed in hand")) last = EcoDebug.SeedInHand(); // #518: a ghost first, then a tap on it plants
            for (int i = 0; i <= 4; i++) if (Btn("T" + i)) last = EcoDebug.TreeStage(i);
            EndRow();
            Row(); // fb5 #573: the woods spot
            if (Btn("Wild trees beside Keeper's")) last = EcoDebug.WildTrees(true); // a grown one and a sapling next to it: nobody chops there
            if (Btn("Wild tree away")) last = EcoDebug.WildTrees(false); // a grown one away from it: the woods spot, with chips and logs
            EndRow();
            Row(); // fb4 #543: the fish chain
            if (Btn("Fill fish basket")) last = FishRackView.DebugFillBasket();
            if (Btn("Fish on the rack")) last = FishRackView.DebugRack();
            if (Btn("Dried fish to pile")) last = FishRackView.DebugToPile();
            EndRow();
            Row(); // fb4 #566: the feather pole
            if (Btn("Feather pole: pray now")) last = ConsequenceCheats.PrayNow();
            EndRow();
        }

        void OnGUI()
        {
            var safe = DebugDock.Begin(out float sc);
            if (!DebugDock.DrawBar(safe, sc)) return;
            if (DebugDock.Active != DebugDock.Dbg) { if (DebugDock.Active == DebugDock.None) DebugDock.SetPanel(default); return; }
            var src = SimLayoutSource.Find();
            var area = DebugDock.PanelArea(safe, 300f, contentH + 16f);
            DebugDock.SetPanel(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginVertical();
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(src != null ? src.Status() : "no SimLayoutSource");
            DrawEco();
            DrawLight(PeopleView.Find());
            Row();
            if (Btn("+1 yr")) last = SimLayoutSource.DebugSkipYears(1);
            if (Btn("+10 yr")) last = SimLayoutSource.DebugSkipYears(10);
            if (Btn("+40 yr")) last = SimLayoutSource.DebugSkipYears(40);
            EndRow();
            if (Btn("Next era (wave)")) last = SimLayoutSource.DebugNextEra(true);
            if (Btn("Replay era wave")) last = SimLayoutSource.DebugReplayWave();
            // The next box in either setting (#115): completes this one at once (ClockLogic.StartNextBox) and shows box 2 at its camp.
            Row();
            if (Btn("Box 2: desert")) last = BoxCompleteController.DebugComplete(AquariumCiv.Sim.BoxSetting.DesertOasis);
            if (Btn("Box 2: mountain")) last = BoxCompleteController.DebugComplete(AquariumCiv.Sim.BoxSetting.MountainVillage);
            EndRow();
            Row();
            if (Btn("Build hut")) last = SimLayoutSource.DebugBuild("Hut");
            if (Btn("Build farm")) last = SimLayoutSource.DebugBuild("Farm");
            EndRow();
            if (Btn(NightLamps.Level > 0.5f ? "Day" : "Night")) last = NightLamps.DebugSetLevel(NightLamps.Level > 0.5f ? 0f : 1f);
            if (Btn("Send test feedback")) last = FeedbackSender.DebugSendTest();
            if (Btn(FrostTestPanel.Instance != null ? "Close frost test" : "Frost test (phone)"))
            {
                last = FrostTestPanel.DebugToggle();
                DebugDock.Active = DebugDock.None; // this panel off the screen: it isn't what's being measured
            }
            Row();
            if (Btn(GlassGallery.Instance != null ? "Close gallery" : "UI gallery"))
            {
                last = GlassGallery.DebugToggle();
                DebugDock.Active = DebugDock.None; // the gallery fills the screen
            }
            if (Btn(FrostSwitch.On ? "Sheets: frost" : "Sheets: flat")) { FrostSwitch.On = !FrostSwitch.On; last = FrostSwitch.On ? "sheets frosted" : "sheets flat, no pass"; }
            if (Btn($"Blur {FrostSwitch.Blur:0.#}")) { FrostSwitch.Blur = FrostSwitch.Blur > FrostSwitch.BlurSmall ? FrostSwitch.BlurSmall : FrostSwitch.BlurLarge; last = $"blur {FrostSwitch.Blur:0.#} panel px"; }
            EndRow();
            GUILayout.Label(RecallPromptView.Status());
            Row();
            if (Btn("Day +1")) last = RecallPromptView.DebugAddDays(1);
            if (Btn("Day 3")) last = RecallPromptView.DebugSetDay(3);
            if (Btn("Day 0")) last = RecallPromptView.DebugSetDay(0);
            EndRow();
            Row();
            if (Btn("Ask recall")) last = RecallPromptView.DebugForce();
            if (Btn("Ask recall (when calm)")) last = RecallPromptView.DebugAskWhenCalm(); // #328
            if (Btn("Reset recall")) last = RecallPromptView.DebugReset();
            if (Btn("Week report")) last = RecallPromptView.DebugReport();
            EndRow();
            if (GameHud.Instance != null && Btn(GameHud.Instance.IsVisible ? "Hide HUD" : "Show HUD"))
            {
                GameHud.Instance.SetVisible(!GameHud.Instance.IsVisible);
                last = GameHud.Instance.IsVisible ? "HUD shown" : "HUD hidden";
            }
            if (GameHud.Instance != null)
            {
                Row();
                if (Btn("New tool glow")) { GameHud.Instance.SetShapeNew(!GameHud.Instance.ShapeNew); last = GameHud.Instance.ShapeNew ? "Shape: New tag" : "Shape: tag off"; }
                if (Btn("3 toasts")) { for (int n = 1; n <= 3; n++) GameHud.Instance.Toast($"Toast {n} of 3 in the queue.", 2f); last = "toast queue"; }
                if (Btn("Pin chip")) { GameHud.Instance.SetPinChip(GameHud.Instance.PinChipText == null ? "Piers (remembered)" : null, null); last = "pin chip"; }
                if (Btn("Stories chip")) { GameHud.Instance.ForceStories = !GameHud.Instance.ForceStories; GameHud.Instance.SetHistoryDot(false); last = GameHud.Instance.ForceStories ? "stories chip held up" : "stories chip released"; }
                EndRow();
                Row();
                if (Btn("Camera mode")) { GameHud.Instance.SetCameraMode(true); last = "camera mode on (tap to leave)"; }
                if (Btn("Follow someone")) last = PersonCardView.DebugFollowInCameraMode();
                if (Btn("Auto-orbit: " + (BoxCamera.AutoOrbitInCameraMode ? "on" : "off")))
                    BoxCamera.AutoOrbitInCameraMode = !BoxCamera.AutoOrbitInCameraMode;
                EndRow();
            }
            // The era moment (#88): era ready now, the wait's pile size, watch / replay / leave, hurry speed.
            GUILayout.Label(MomentPlayer.Status());
            Row();
            if (Btn("Grow 12y")) last = MomentPlayer.DebugGrow(12f);
            if (Btn("Village +40y")) last = MomentPlayer.DebugGrowVillage(40f);
            if (Btn("Era ready")) last = MomentPlayer.DebugEraReady();
            if (Btn("Waited 1y")) last = MomentPlayer.DebugWaited(12);
            if (Btn("Waited 1d")) last = MomentPlayer.DebugWaited(288);
            EndRow();
            Row();
            if (Btn("Festival")) last = MomentPlayer.DebugFestival();
            if (Btn("End festival")) last = MomentPlayer.DebugEndFestival();
            EndRow();
            Row();
            if (Btn("Watch")) last = MomentPlayer.DebugWatch(false);
            if (Btn("Replay last")) last = MomentPlayer.DebugReplay();
            if (Btn("Leave")) last = MomentPlayer.DebugLeave();
            EndRow();
            Row();
            if (Btn("Wipe ask")) last = MomentPlayer.DebugWipeAsk();
            if (Btn("Notify ask")) last = MomentPlayer.DebugNotifyAsk();
            if (Btn("New tool + hint")) last = MomentPlayer.DebugNewTool();
            if (Btn("Say a line")) last = MomentPlayer.DebugSay();
            EndRow();
            if (MomentPlayer.Instance != null)
            {
                Row();
                float hf = MomentPlayer.Instance.Director.HurryFactor;
                if (Btn("Hurry ×" + (hf > 2f ? "4" : "2"))) last = MomentPlayer.DebugHurry(hf > 2f ? 2f : 4f);
                if (Btn(MomentPlayer.DebugHoldHurry ? "Hold: on" : "Hold: off")) MomentPlayer.DebugHoldHurry = !MomentPlayer.DebugHoldHurry;
                EndRow();
            }
            // The evening talk (#77): start the close, count the sitting as long, force each teaser kind.
            GUILayout.Label(EveningTalkView.Status());
            Row();
            if (Btn("Start close")) { last = EveningTalkView.DebugStart(); DebugDock.Active = DebugDock.None; }
            if (Btn("Leave close")) last = EveningTalkView.DebugLeave();
            if (Btn("Long sitting")) last = EveningTalkView.DebugLongSitting();
            EndRow();
            Row();
            foreach (var kind in new[] { AquariumCiv.Sim.ThreadSource.Era, AquariumCiv.Sim.ThreadSource.Seat, AquariumCiv.Sim.ThreadSource.Couple, AquariumCiv.Sim.ThreadSource.Gift })
                if (Btn(kind)) { last = EveningTalkView.DebugTeaser(kind); DebugDock.Active = DebugDock.None; }
            EndRow();
            Row();
            foreach (var kind in new[] { AquariumCiv.Sim.ThreadSource.Rivalry, AquariumCiv.Sim.ThreadSource.Office, AquariumCiv.Sim.ThreadSource.Slice })
                if (Btn(kind)) { last = EveningTalkView.DebugTeaser(kind); DebugDock.Active = DebugDock.None; }
            EndRow();
            // The small book (#80): earn every page, open it.
            Row();
            if (Btn("Earn every book page")) last = BookView.DebugEarnAll();
            if (Btn("Open book")) { last = BookView.DebugOpen(); DebugDock.Active = DebugDock.None; }
            EndRow();
            // What the town wants (#181), the said list (#120), a wish granted (#191), a baby (#192), a gathering (#190).
            Row();
            foreach (AquariumCiv.View.ShowMe.WantCase want in System.Enum.GetValues(typeof(AquariumCiv.View.ShowMe.WantCase)))
                if (Btn(want.ToString())) last = ShowMeDirector.DebugWant(want);
            EndRow();
            Row();
            if (Btn("Clear said")) last = ShowMeDirector.DebugClearSaid();
            if (Btn("Clean hint")) last = CareView.DebugCleanHint();
            if (Btn("Mister hint")) last = GameHud.DebugForgetMisterHint();
            if (Btn("Ground tap")) last = GlassVoice.DebugRipple();
            if (Btn("Wind")) last = WindView.DebugNext();
            if (Btn("Line on walker")) last = ShowMeDirector.DebugSayOverWalker();
            if (Btn("Stalls")) last = StallProps.DebugStock();
            if (Btn("Keeper Doll")) last = GlassVoice.DebugPlayMoment(AquariumCiv.Sim.RelicId.KeeperDoll);
            if (Btn("Start a ritual now")) last = GlassVoice.DebugStartRitual();
            if (Btn("Drop crumb")) last = GlassVoice.DebugDropCrumb();
            if (Btn("Crumb eaten")) last = GlassVoice.DebugCrumbEaten();
            if (Btn("Grant a wish")) last = WishPayoffView.DebugGrant();
            if (Btn("Gift glow")) last = WishPayoffView.DebugGlow();
            if (Btn("Baby")) last = PeopleView.DebugBaby();
            if (Btn("Bicker")) last = PeopleView.DebugRunIn(true);
            if (Btn("Chat")) last = PeopleView.DebugRunIn(false);
            if (Btn("Gather 6")) last = GlassVoice.DebugGather();
            if (Btn("Gather 10")) last = GlassVoice.DebugGather(10);
            EndRow();
            // The wordless chatter (#144), the knock's mark (#161).
            Row();
            var chatter = ShowMeDirector.Instance != null ? ShowMeDirector.Instance.Chatter : null;
            if (Btn("Chatter: " + ChatterView.Style)) last = ChatterView.DebugNextStyle();
            if (chatter != null)
            {
                if (Btn("Force chat")) last = chatter.DebugForceChat();
                foreach (var mark in new[] { AquariumCiv.View.ShowMe.ChatterMark.Dots, AquariumCiv.View.ShowMe.ChatterMark.Note, AquariumCiv.View.ShowMe.ChatterMark.Heart, AquariumCiv.View.ShowMe.ChatterMark.Bang, AquariumCiv.View.ShowMe.ChatterMark.Question })
                    if (Btn(mark.ToString())) last = chatter.DebugMark(PeopleView.Find(), mark);
            }
            if (Btn("Force knock")) last = GlassVoice.DebugKnock();
            EndRow();
            // #327 Option B: every spoken line on the caption with the name, the ring and the speech mark.
            Row();
            if (Btn("Line: caption here")) last = ShowMeDirector.DebugLineHere();
            if (Btn("Speech mark")) last = ShowMeDirector.DebugSpeechMark();
            if (Btn("Line: 2nd waits")) last = ShowMeDirector.DebugWaitingLines();
            EndRow();
            // #363: pairs (talk is "Force chat" above; the hug shows the variant even while BeatFlags.Hugs is off)
            Row();
            if (Btn("Hand over now")) last = PeopleView.DebugPair(PairKind.HandOver);
            if (Btn("Walk together now")) last = PeopleView.DebugPair(PairKind.WalkTogether);
            if (Btn("Hug now")) last = PeopleView.DebugPair(PairKind.Hug);
            if (Btn("Hugs: " + (BeatFlags.Hugs ? "on" : "off"))) { BeatFlags.Hugs = !BeatFlags.Hugs; last = "hugs " + (BeatFlags.Hugs ? "on" : "off"); }
            EndRow();
            // hugs/tumble/toddle (user 2026-11-06): the in-game hugs' status, a parent-and-kid hug, a chase with a tumble, toddlers on their feet
            GUILayout.Label(HugDirector.DebugStatus());
            Row();
            if (Btn("Hug: parent and kid now")) last = HugDirector.DebugParentKid();
            if (Btn("Kids chase rabbits: a kid tumbles")) last = KidPlayDirector.DebugTumble();
            if (Btn("Toddlers walk: " + (AquariumCiv.Life.LifePlanner.DebugToddlers ? "all" : "normal"))) { var tpv = PeopleView.Find(); last = tpv != null && tpv.Planner != null ? tpv.Planner.DebugToddlersToggle() : "no people"; }
            EndRow();
            // #315/#316: the frogs and the voices by distance.
            Row();
            if (Btn("Frog now")) last = AudioDirector.DebugFrog();
            if (Btn("Sand gust now")) last = AudioDirector.DebugSandGust(); // #331 (the gust rate: "Audio status" amb line)
            if (chatter != null)
            {
                if (Btn("Voice exchange here")) last = chatter.DebugExchange();
                if (Btn("Far shout now")) last = chatter.DebugLoneVoice();
                if (Btn("Voice band: " + ChatterView.BandLabel())) last = ChatterView.DebugCycleBand();
                if (Btn("Voice status")) last = chatter.VoiceStatus();
            }
            EndRow();
            GUILayout.Label(MusicDirector.Instance != null ? MusicDirector.Instance.Status : "no MusicDirector");
            Row();
            if (Btn("Music now")) last = MusicDirector.DebugPlayNow();
            if (Btn("Stop music")) last = MusicDirector.DebugStop();
            if (Btn("Music: " + GameSettings.MusicFrequency))
            {
                GameSettings.MusicFrequency = (MusicFrequency)(((int)GameSettings.MusicFrequency + 1) % 3);
                last = "music frequency " + GameSettings.MusicFrequency;
            }
            EndRow();
            // Sound (#96): bus levels, play any cue, force ambience layers, the switches, a test buzz.
            soundOpen = GUILayout.Toggle(soundOpen, soundOpen ? "▼ Sound" : "► Sound", "button");
            if (soundOpen)
            {
                GUILayout.Label(AudioDirector.Status());
                var ids = CueIds;
                cueIndex = (cueIndex + ids.Length) % ids.Length;
                Row();
                if (Btn("◀", GUILayout.Width(36))) cueIndex--;
                if (Btn("Play " + ids[(cueIndex + ids.Length) % ids.Length])) last = AudioDirector.DebugPlay(ids[(cueIndex + ids.Length) % ids.Length]);
                if (Btn("▶", GUILayout.Width(36))) cueIndex++;
                EndRow();
                Row();
                for (int i = 0; i < AquariumCiv.Audio.AmbienceLevels.Count; i++)
                {
                    if (i == 6 || i == 9) { EndRow(); Row(); }
                    var ad = AudioDirector.Instance;
                    string n = AquariumCiv.Audio.AmbienceLevels.CueIds[i].Substring(4);
                    if (Btn(n + ":" + (ad != null ? AudioDirector.LayerForceName(ad.LayerForce[i]) : "-"))) last = AudioDirector.DebugCycleLayer(i);
                }
                EndRow();
                Row();
                if (Btn("Sound: " + (GameSettings.Sound ? "on" : "off"))) { GameSettings.Sound = !GameSettings.Sound; last = "sound " + (GameSettings.Sound ? "on" : "off"); }
                if (Btn("Haptics: " + (GameSettings.Haptics ? "on" : "off"))) { GameSettings.Haptics = !GameSettings.Haptics; last = "haptics " + (GameSettings.Haptics ? "on" : "off"); }
                if (Btn("SFX " + GameSettings.SfxVolume.ToString("0.0"))) GameSettings.SfxVolume = GameSettings.SfxVolume > 0.05f ? Mathf.Round((GameSettings.SfxVolume - 0.25f) * 4f) / 4f : 1f;
                if (Btn("Amb " + GameSettings.AmbienceVolume.ToString("0.0"))) GameSettings.AmbienceVolume = GameSettings.AmbienceVolume > 0.05f ? Mathf.Round((GameSettings.AmbienceVolume - 0.25f) * 4f) / 4f : 1f;
                EndRow();
                Row();
                if (Btn("Buzz light")) last = AudioDirector.DebugBuzz(AquariumCiv.Audio.HapticKind.Light);
                if (Btn("Buzz success")) last = AudioDirector.DebugBuzz(AquariumCiv.Audio.HapticKind.Success);
                if (Btn("Check clips")) last = AudioDirector.DebugCheckClips();
                if (Btn("Bird call")) last = AudioDirector.DebugBirdCall();
                EndRow();
            }
            // A building's card (#188): the next one round the box, and its builder's three wordings.
            Row();
            if (Btn("Building card")) last = PersonCardView.DebugBuildingCard();
            if (Btn("Builder ⇄")) last = PersonCardView.DebugBuilder();
            EndRow();
            // The people's view clock (#30): jump to a time of day, speed the day up, list what people are doing.
            var people = PeopleView.Find();
            if (people != null)
            {
                GUILayout.Label(people.Status());
                Row();
                if (Btn("8h")) last = PeopleView.DebugSetHour(8f);
                if (Btn("12h")) last = PeopleView.DebugSetHour(12.2f);
                if (Btn("18h")) last = PeopleView.DebugSetHour(18f);
                if (Btn("22h")) last = PeopleView.DebugSetHour(22f);
                if (Btn("+1h")) last = PeopleView.DebugSkipHours(1f);
                EndRow();
                Row();
                if (Btn(people.timeScale > 1f ? "Day ×1" : "Day ×10")) last = PeopleView.DebugSpeed(people.timeScale > 1f ? 1f : 10f);
                if (Btn("Who's doing what")) last = PeopleView.DebugReport(6);
                EndRow();
            }
            GUILayout.Label(last);
            if (src != null && !string.IsNullOrEmpty(src.lastWaveInfo)) GUILayout.Label(src.lastWaveInfo);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) contentH = GUILayoutUtility.GetLastRect().height;
            GUILayout.EndArea();
        }
    }
}
