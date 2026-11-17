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
            // ---- #717: tonight's story, told by the Keeper's pick (the choice itself: DilemmaCheats "Small: who tells the story")
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Story tonight: slow")) last = DebugStoryTonight(0);
            if (GUILayout.Button("Story tonight: short")) last = DebugStoryTonight(1);
            GUILayout.EndHorizontal();
            // ---- end #717
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

        /// <summary>#717: the story choice raised and answered now (0: slow, with all the voices; 1: short, for a laugh), then the evening:
        /// the picked teller tells it at the fire.</summary>
        public static string DebugStoryTonight(int option)
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            if (sim.DebugTellStoryTonight(option) == null) return "fewer than two adults";
            PeopleView.Find()?.Planner?.Invalidate();
            return $"{sim.FindNotable(sim.State.storyTellerId)?.name} tells it {(option == 0 ? "slow" : "short")}: " + PeopleView.DebugSetHour(18.2f);
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
            Row(); // #708 (B70): the fish seen from above and through the glass; B39.c the kids' try by hand
            if (Btn("Fish: the shadows")) last = FishView.DebugShadows();
            if (Btn("Fish: show them at the glass")) last = FishView.DebugGlass();
            if (Btn("Fish: startle")) last = FishView.DebugStartle();
            if (Btn("Kids try to catch a fish by hand")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.FishGrab);
            if (Btn("Two kids join the camp")) { var gc = AquariumCiv.Game.GameClock.Instance; last = gc == null ? "no box" : $"{gc.Sim.DebugAddKids(2)} kids came"; }
            EndRow();
            Row(); // #361
            if (Btn("Pickers to the nearest bush now")) last = BerryPickView.DebugPickersToBush();
            if (Btn("Tip at the stash now")) last = BerryPickView.DebugTipNow();
            EndRow();
            Row(); // #656/#677: the new start (the root dig is gone)
            if (Btn("Arrival again")) last = NewStartView.DebugArrivalAgain();
            if (Btn("First wild wood now")) last = NewStartView.DebugFirstWildWoodNow();
            if (Btn("Empty the basket")) { var ns = GameClock.Instance != null ? GameClock.Instance.Sim : null; if (ns != null) { ns.DebugEmptyBasket(); last = "the basket is empty: hungry (slower, looking at the sky), nobody harmed"; } }
            EndRow();
            Row(); // #658: the deeper box (an old 44-row save grows a bare strip at the back)
            if (Btn("Grow a 44-row box")) last = GameClock.DebugGrowFrom44();
            EndRow();
            Row();
            if (Btn("Fire: none")) last = NewStartFire(0);
            if (Btn("Fire: grass and twigs")) last = NewStartFire(1);
            if (Btn("Fire: logs")) last = NewStartFire(2);
            if (Btn("Fire: as the land")) last = NewStartFire(-1);
            if (Btn("Lay a bedroll")) { var pv0 = PeopleView.Find(); last = pv0 != null && pv0.Planner != null && pv0.Planner.FirePoint(out float lbx, out float lbz) && CampBedrolls.Lay(new Vector2(lbx, lbz)) ? $"bedrolls by the fire: {CampBedrolls.Count} (this session only)" : "no fire, or the ring is full"; }
            EndRow();
            Row();
            if (Btn("First bush now")) last = EcoDebug.FirstBushNow();
            if (Btn("+3 bushes")) last = EcoDebug.AddBushes(3);
            if (Btn("Bush carpet (old save)")) last = EcoDebug.CarpetBushes();
            EndRow();
            Row();
            if (Btn("Rabbit tracks now")) last = EcoDebug.RabbitTracksNow();
            if (Btn("Rabbit trails: count")) last = EcoDebug.LiveTrailsNow();   // #665
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
            Row(); // #618 winter on the pond: the freeze (and the ducks' leave), and the kids' slide
            if (Btn("Ice: on")) last = IceView.DebugIce(true);
            if (Btn("Ice: off")) last = IceView.DebugIce(false);
            if (Btn("Ice: as the season")) last = IceView.DebugIce(null);
            if (Btn("Kids play: slide on ice")) last = KidPlayDirector.Debug(AquariumCiv.Life.PlayGame.IceSlide);
            EndRow();
            Row();
            if (Btn("Start a hunt now")) last = EcoDebug.StartHunt();
            if (Btn("Next hunt succeeds")) last = EcoDebug.ForceHunt(true);
            if (Btn("Next hunt misses")) last = EcoDebug.ForceHunt(false);
            if (Btn("Slow-mo next hunt")) last = EcoDebug.HuntSlowMoNext();   // #362
            if (Btn("Hunt: miss, try again")) last = EcoDebug.HuntMissThenRetry();   // #680
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
            Row(); // #705: the washing line
            if (Btn("Laundry now")) last = LaundryCheats.LaundryNow();
            if (Btn("Take in the washing")) last = LaundryCheats.TakeInNow();
            EndRow();
            Row(); // fb4 #566: the feather pole
            if (Btn("Feather pole: pray now")) last = ConsequenceCheats.PrayNow();
            EndRow();
            // #692 people and things, part 1: the fire, the stew pot, the feather pole
            Row(); // #692
            if (Btn("Winter fire: warm hands")) last = CampCheats.WarmHands(); // #692 B29.a
            if (Btn("Kids poke the fire")) last = CampCheats.KidPoke(); // #692 B29.b
            if (Btn("Berry on a stick")) last = CampCheats.BerryStick(); // #692 B29.c
            EndRow();
            Row(); // #692
            if (Btn("Roast on the spit (rabbit)")) last = CampCheats.Spit(false); // #692 B29.d
            if (Btn("Roast on the spit (deer)")) last = CampCheats.Spit(true); // #692 B29.d
            EndRow();
            Row(); // #692
            if (Btn("Pot: fill it now")) last = CampCheats.PotFill(); // #692 B30.a
            if (Btn("Pot: scrape and lift off")) last = CampCheats.PotOff(); // #692 B30.e
            if (Btn("Pot: cold / cooking")) last = CampCheats.PotToggle(); // #692 B30.e
            EndRow();
            Row(); // #692
            if (Btn("Kids peek in the pot")) last = CampCheats.PotPeek(); // #692 B30.c
            if (Btn("Someone sniffs the pot")) last = CampCheats.PotSniff(); // #692 B30.d
            if (Btn("Bowl round: ladle")) last = CampCheats.Ladle(); // #692 B30.b (with "Meal now" at supper)
            EndRow();
            Row(); // #692
            if (Btn("Feather: newborn")) last = CampCheats.TieFeather(AquariumCiv.Sim.PoleFeatherKind.Newborn); // #692 B31.a
            if (Btn("Feather: marriage")) last = CampCheats.TieFeather(AquariumCiv.Sim.PoleFeatherKind.Marriage); // #692 B31.a
            if (Btn("Feather: village")) last = CampCheats.TieFeather(AquariumCiv.Sim.PoleFeatherKind.Village); // #692 B31.a
            if (Btn("Kids dare at the feather pole")) last = CampCheats.PoleDare(); // #692 B31.b
            EndRow();
            // #692 part 2: the Keeper's tree visits (each grows the tree first if it must)
            Row(); // #692
            if (Btn("Keeper's tree: sit under it")) last = CampCheats.TreeSit(); // #692 B32.a
            if (Btn("Keeper's tree: touch the bark")) last = CampCheats.TreeTouch(); // #692 B32.b
            if (Btn("Kids circle the Keeper's tree")) last = CampCheats.TreeCircle(); // #692 B32.c
            EndRow();
            Row(); // #692
            if (Btn("Keeper's tree: quiet visit (after a death)")) last = CampCheats.TreeQuiet(); // #692 B32.d
            if (Btn("Keeper's tree: newlyweds' visit")) last = CampCheats.TreeWed(); // #692 B32.e
            EndRow();
            Row(); // #692
            if (Btn("Keeper's tree: newborn visit")) last = CampCheats.TreeNewborn(); // #692 B32.f
            if (Btn("Keeper's tree: the old one sits there")) last = CampCheats.TreeOld(); // #692 B32.g
            if (Btn("Keeper's tree: the eldest tells the kids")) last = CampCheats.TreeTell(); // #692 B32.h
            EndRow();
            // #692 part 3: grass, saplings, rocks, fallen branches ("Land: a wild tree and saplings" first for B34 and B36)
            Row(); // #692
            if (Btn("Kids roll in the grass")) last = CampCheats.GrassRoll(); // #692 B33.a
            if (Btn("Lie back in the grass")) last = CampCheats.LieBack(); // #692 B33.b
            if (Btn("Pick flowers")) last = CampCheats.PickFlowers(); // #692 B33.c
            if (Btn("Grass whistle")) last = CampCheats.GrassWhistle(); // #692 B33.d
            EndRow();
            Row(); // #692
            if (Btn("Land: a wild tree and saplings")) last = CampCheats.LandReady(); // #692 part 3
            if (Btn("Water a sapling")) last = CampCheats.WaterSapling(); // #692 B34.a
            if (Btn("Ring a sapling with stones")) last = CampCheats.RingSapling(); // #692 B34.b
            if (Btn("A kid plants a twig")) last = CampCheats.PlantTwig(); // #692 B34.c
            EndRow();
            Row(); // #692
            if (Btn("Sit on a rock / kid on the rock")) last = CampCheats.SitRock(); // #692 B35.a
            if (Btn("Stack pebbles")) last = CampCheats.StackPebbles(); // #692 B35.b (alternates: stands / topples)
            if (Btn("Kids sword fight with sticks")) last = CampCheats.StickFight(); // #692 B36.a
            if (Btn("Stick horse")) last = CampCheats.StickHorse(); // #692 B36.b
            EndRow();
            // #692 part 4: animals (each places its animals first if the box has none; B39.c is "Kids try to catch a fish by hand" above)
            Row(); // #692
            if (Btn("Kids catch a frog")) last = CampCheats.FrogCatch(); // #692 B37 (alternates: caught / it gets away)
            if (Btn("Watch the deer")) last = CampCheats.DeerWatch(); // #692 B38.a
            if (Btn("Kids creep up on the deer")) last = CampCheats.DeerCreep(); // #692 B38.b
            EndRow();
            Row(); // #692
            if (Btn("A kid holds a rabbit kit")) last = CampCheats.KitHold(); // #692 B39.a
            if (Btn("Kids feed the ducks")) last = CampCheats.FeedDucks(); // #692 B15.d (B39.b)
            EndRow();
            // #692 part 5: the rest of the camp (each finds its thing; "Land: a wild tree and saplings" first for B44 and B53.d)
            Row(); // #692
            if (Btn("Kids peek out of the tents")) last = CampCheats.TentPeek(); // #692 B40.a
            if (Btn("Hang a toy on a tent")) last = CampCheats.TentToy(); // #692 B40.b
            if (Btn("Sit in the tent doors (dusk)")) last = CampCheats.TentDoorSit(); // #692 B40.c
            if (Btn("Kids count the stash")) last = CampCheats.CountStash(); // #692 B41.a
            if (Btn("Snack sneak at the stash")) last = CampCheats.SnackSneak(); // #692 B41.b
            EndRow();
            Row(); // #692
            if (Btn("Tend the racks now")) last = CampCheats.TendRacks(); // #692 B42.a
            if (Btn("Hang meat on the rack")) last = CampCheats.HangMeat(); // #692 B42.a
            if (Btn("Fish on the rack (they stay)")) last = CampCheats.FishStay(); // #692 B42 retakes
            if (Btn("Kids run under the hide rack")) last = CampCheats.DuckUnderHides(); // #692 B42.b
            if (Btn("Kids count the fish")) last = CampCheats.CountFish(); // #692 B42.c
            if (Btn("A kid sneaks a fish")) last = CampCheats.SneakFish(); // #692 B42.d
            EndRow();
            Row(); // #692
            if (Btn("Peek at rabbits from the thicket")) last = CampCheats.ThicketPeek(); // #692 B43.b
            if (Btn("Berry kid now")) last = CampCheats.BerryKid(); // #692 B43.c (B17.a's purple mouth)
            if (Btn("Kid climbs a tree")) last = CampCheats.ClimbTree(); // #692 B44.a
            if (Btn("Nap under a tree")) last = CampCheats.TreeNap(); // #692 B44.b
            if (Btn("Chat leaning on a tree")) last = CampCheats.TrunkLean(); // #692 B44.c
            EndRow();
            Row(); // #692
            if (Btn("Skip stones")) last = CampCheats.SkipStones(); // #692 B45.a (alternates: three skips / a kid's plop)
            if (Btn("Leaf boat")) last = CampCheats.LeafBoat(); // #692 B45.b
            if (Btn("Feet in the water")) last = CampCheats.FeetInWater(); // #692 B45.c
            if (Btn("Kids look in a puddle")) last = CampCheats.PuddleLook(); // #692 B46.a
            if (Btn("Kids race the puddles")) last = CampCheats.PuddleRace(); // #692 B46.b ("Puddles now" first)
            EndRow();
            Row(); // #692
            if (Btn("Cut reeds")) last = CampCheats.CutReeds(); // #692 B47.a
            if (Btn("Reed whistle")) last = CampCheats.ReedWhistle(); // #692 B47.b
            if (Btn("Kids follow the stream")) last = CampCheats.FollowStream(); // #692 B48.a
            if (Btn("Kids build a dam")) last = CampCheats.BuildDam(); // #692 B48.b
            EndRow();
            Row(); // #692
            if (Btn("Sit on the hill at dusk")) last = CampCheats.HillDusk(); // #692 B49.a
            if (Btn("Kids roll down the hill")) last = CampCheats.HillRoll(); // #692 B49.b
            if (Btn("Kids race on the path")) last = CampCheats.PathRace(); // #692 B50.a
            if (Btn("Chat on a path")) last = CampCheats.PathChat(); // #692 B50.b
            if (Btn("A gift at the remembering stone")) last = CampCheats.StoneGift(); // #692 B51
            EndRow();
            Row(); // #692
            if (Btn("Light the lanterns now")) last = CampCheats.LightLanterns(); // #692 B52.a
            if (Btn("Kids chase moths")) last = CampCheats.ChaseMoths(); // #692 B52.b
            if (Btn("Pull a weed in the camp")) last = CampCheats.PullWeed(); // #692 B53.a
            if (Btn("Kids make a crown")) last = CampCheats.Crown(); // #692 B53.c
            if (Btn("Hang the swing")) last = CampCheats.HangSwing(); // #692 B53.d
            if (Btn("Kids on the swing")) last = CampCheats.Swing(); // #692 B53.d (hangs it first if none)
            EndRow();
            // #693 the choice props, a work use and a life use each ("#693: every choice's thing" first: one of each stands in the box)
            Row(); // #693
            if (Btn("#693: every choice's thing")) last = CampCheats.EveryThing(); // #693
            if (Btn("Baskets: take and bring back")) last = CampCheats.BasketTake(); // #693 B54.a
            if (Btn("Woodpile: stack / fetch")) last = CampCheats.WoodStack(); // #693 B54.c (alternates)
            if (Btn("Rain at the woodpile")) last = CampCheats.WoodShelter(); // #693 B54.d (alternates: rain / kids sit on the logs)
            EndRow();
            Row(); // #693
            if (Btn("Name stone: cut a name")) last = CampCheats.NameCut(); // #693 B54.e
            if (Btn("Kids trace the names")) last = CampCheats.NameTrace(); // #693 B54.f
            if (Btn("Kids flick the charms")) last = CampCheats.CharmFlick(); // #693 B54.g
            if (Btn("Big pot: the together supper")) last = CampCheats.BigPot(); // #693 B54.h (then "Bowl round: ladle")
            if (Btn("Kids drum on the pot")) last = CampCheats.PotDrum(); // #693 B54.i
            EndRow();
            Row(); // #693
            if (Btn("Split wood at the stump")) last = CampCheats.StumpSplit(); // #693 B54.j
            if (Btn("Kid sits on the stump")) last = CampCheats.StumpSit(); // #693 B54.k
            if (Btn("Headman takes the staff")) last = CampCheats.StaffWalk(); // #693 B54.l
            if (Btn("Kids play headman")) last = CampCheats.StaffPlay(); // #693 B54.m
            if (Btn("Carry flour")) last = CampCheats.FlourCarry(); // #693 B54.n
            if (Btn("Flour fight")) last = CampCheats.FlourFight(); // #693 B54.o
            EndRow();
            Row(); // #693
            if (Btn("Storm: straighten the roof bird")) last = CampCheats.BirdStraighten(); // #693 B54.p
            if (Btn("Kids fly under the bird")) last = CampCheats.BirdFly(); // #693 B54.q
            if (Btn("Long table meal now")) last = CampCheats.TableMeal(); // #693 B54.r
            if (Btn("Kids under the long table")) last = CampCheats.TableUnder(); // #693 B54.s
            if (Btn("Fill the offering basket")) last = CampCheats.OfferingFill(); // #693 B54.t
            if (Btn("Kids wait by the offering")) last = CampCheats.OfferingWait(); // #693 B54.u
            if (Btn("Kids wait, then the Keeper knocks")) last = CampCheats.OfferingWaitKnock(); // #693 B54.u (the knock)
            EndRow();
            Row(); // #693
            if (Btn("Fish crate to the rack")) last = CampCheats.CrateFish(); // #693 B54.x
            if (Btn("Kids wave across the pond")) last = CampCheats.CrateWave(); // #693 B54.y
            if (Btn("Re-carve the name post")) last = CampCheats.PostCarve(); // #693 B54.z (notes: the carved name post)
            if (Btn("Kids measure their height")) last = CampCheats.PostMeasure(); // #693 B54.zz (notes)
            EndRow();
            Row(); // #693 goat pen (2026-11-16 "Bring a pen back")
            if (Btn("Goat pen with its goat")) last = CampCheats.GoatPen(); // stands every choice's thing, the pen and its goat among them
            if (Btn("Goat: gnaw the corner")) last = CampCheats.GoatNibble();
            if (Btn("Mend the goat pen")) last = CampCheats.PenMend(); // #693 B54.v
            if (Btn("Kids at the goat pen")) last = CampCheats.PenRails(); // #693 B54.w
            if (Btn("Kid pets the goat")) last = CampCheats.PenPet(); // B58.d at the pen
            EndRow();
            // #694 dogs and wolves in the people's day (B24.b, B25, B55, B56); "Pup now" / "Grown dog now" first. "Lost pup now" is in the sim panel.
            Row(); // #694
            if (Btn("Pup now")) last = CampCheats.PupNow(); // #694
            if (Btn("Grown dog now")) last = CampCheats.GrownDogNow(); // #694
            if (Btn("Big dog now")) last = CampCheats.BigDogNow(); // #694 (gen 3: big enough to ride)
            if (Btn("Old dog now")) last = CampCheats.OldDogNow(); // #694 B25.c (grey muzzle)
            if (Btn("Dogs: status")) last = CampCheats.DogStatus(); // #694
            EndRow();
            Row(); // #694 the pup at the fire (B24.b)
            if (Btn("Pup: chew a hide")) last = CampCheats.PupChew(); // #694 B24.b
            if (Btn("Pup: play with the kids")) last = CampCheats.PupPlay(); // #694 B24.b
            if (Btn("Pup: sleep by the embers")) last = CampCheats.PupSleep(); // #694 B24.b
            if (Btn("Pup: steal a fish")) last = CampCheats.PupSteal(); // #694 B24.b
            if (Btn("Pup: shooed off the meat")) last = CampCheats.PupShoo(); // #694 B24.b
            if (Btn("Pup: run along in a game")) last = CampCheats.PupRunAlong(); // #694 B24.b
            EndRow();
            Row(); // #694 the grown dog (B25, B56)
            if (Btn("Dog: hunt now")) last = CampCheats.DogHunt(); // #694 B25.a + B56.a
            if (Btn("Wolves come near (guard)")) last = CampCheats.DogGuard(); // #694 B25.b + B56.b
            if (Btn("Dog: fetch")) last = CampCheats.DogFetch(); // #694 B56.c
            if (Btn("Dog: a kid rides")) last = CampCheats.DogRide(); // #694 B56.d
            if (Btn("Dog by the fire")) last = CampCheats.DogFire(); // #694 B56.e
            if (Btn("Dog: lie in the sun")) last = CampCheats.DogSun(); // #694 B25
            EndRow();
            Row(); // #694 the people and the wolves (B55)
            if (Btn("Wolves come near: drive them off")) last = CampCheats.DriveOff(); // #694 B55.a
            if (Btn("Wolves in view: hold the kids close")) last = CampCheats.KidsClose(); // #694 B55.b
            EndRow();
            // #695 the village and the town (B57, B58.c/f/g/j/k); "Jump: a village box" / "Jump: a town box" first
            Row(); // #695
            if (Btn("Jump: a village box")) last = CampCheats.StageVillage(10f, false, 4, false); // #695
            if (Btn("Jump: a town box")) last = CampCheats.StageVillage(8f, true, 4, false); // #695 (a bakery stands)
            if (Btn("Village: status")) last = CampCheats.VillageStatus(); // #695
            EndRow();
            Row(); // #695 fields, homes, the well (B57.a-f)
            if (Btn("Kids run in the fields")) last = CampCheats.FieldTag(); // #695 B57.a
            if (Btn("Make a scarecrow")) last = CampCheats.Scarecrow(); // #695 B57.b
            if (Btn("Harvest dance")) last = CampCheats.HarvestDance(); // #695 B57.c
            if (Btn("Mend and dress a home")) last = CampCheats.HomeMend(); // #695 B57.d
            if (Btn("Gossip at the well")) last = CampCheats.WellGossip(); // #695 B57.e
            if (Btn("Kid drops a pebble down the well")) last = CampCheats.WellPebble(); // #695 B57.f
            EndRow();
            Row(); // #695 the windmill, the lumber mill (B57.g-j)
            if (Btn("Kids watch the sails")) last = CampCheats.SailWatch(); // #695 B57.g
            if (Btn("Kids race the sails' shadow")) last = CampCheats.SailShadow(); // #695 B57.h
            if (Btn("Break on the log pile")) last = CampCheats.LogPileSit(); // #695 B57.i
            if (Btn("Kid balances on a log")) last = CampCheats.LogBalance(); // #695 B57.j
            EndRow();
            Row(); // #695 the bakery, streets, fences (B58.c/f/g/j/k)
            if (Btn("Kids wait for a crust")) last = CampCheats.CrustWait(); // #695 B58.c (a town)
            if (Btn("Kids play hopscotch")) last = CampCheats.Hopscotch(); // #695 B58.f
            if (Btn("Stop to talk in a street")) last = CampCheats.StreetChat(); // #695 B58.g
            if (Btn("Fix a fence")) last = CampCheats.FenceFix(); // #695 B58.j
            if (Btn("Kids on the fence")) last = CampCheats.FenceLean(); // #695 B58.k
            EndRow();
            Row(); // #695 B59 outbuildings (the jetty, orchard, quarry, dyer and watermill can't stand in a living-land box)
            if (Btn("Barn chores")) last = CampCheats.BarnChores(); // #695 B59.k
            if (Btn("Kids jump in the hay")) last = CampCheats.HayJump(); // #695 B59.l
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
            // #636: the playtest's quick questions (one per session)
            GUILayout.Label(PulseQuestionView.Status());
            Row();
            foreach (var q in PulseQuestions.All)
                if (Btn("Ask " + q.id)) last = PulseQuestionView.DebugAsk(q.id);
            EndRow();
            Row();
            if (Btn("Reset questions")) last = PulseQuestionView.DebugReset();
            if (Btn(PulseQuestionView.On ? "Questions: on" : "Questions: off")) { PulseQuestionView.On = !PulseQuestionView.On; last = PulseQuestionView.On ? "quick questions on" : "quick questions off (the old recall asks)"; }
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
            foreach (var kind in new[] { AquariumCiv.Sim.ThreadSource.Era, AquariumCiv.Sim.ThreadSource.Seat, AquariumCiv.Sim.ThreadSource.Couple }) // #718: no gift teaser
                if (Btn(kind)) { last = EveningTalkView.DebugTeaser(kind); DebugDock.Active = DebugDock.None; }
            EndRow();
            Row();
            foreach (var kind in new[] { AquariumCiv.Sim.ThreadSource.Rivalry, AquariumCiv.Sim.ThreadSource.Office }) // #715: no slice-of-life teaser
                if (Btn(kind)) { last = EveningTalkView.DebugTeaser(kind); DebugDock.Active = DebugDock.None; }
            EndRow();
            // The small book (#80): earn every page, open it.
            Row();
            if (Btn("Earn every book page")) last = BookView.DebugEarnAll();
            if (Btn("Open book")) { last = BookView.DebugOpen(); DebugDock.Active = DebugDock.None; }
            EndRow();
            // What the town wants (#181), the said list (#120), a baby (#192), a gathering (#190).
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
            if (Btn("Speech over pin")) last = PersonCardView.DebugSpeechOverPin();
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

        /// <summary>#656/#677: the panel's "fire" cheats (0 none, 1 grass and twigs, 2 logs, -1 as the land lets it burn).</summary>
        static string NewStartFire(int stage)
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null || !sim.NewStartOn) return "not a new-start box (living land off, or an old box)";
            sim.DebugFireStage(stage);
            var src = SimLayoutSource.Find(); if (src != null) src.GetComponent<BoxView>().Refresh();
            return "fire: " + sim.FireStage;
        }
    }
}
