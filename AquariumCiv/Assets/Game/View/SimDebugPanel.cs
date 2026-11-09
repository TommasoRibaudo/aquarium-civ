using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Replay;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// TEMPORARY sim cheats panel (#40): every sim debug hook in collapsible sections, so a tester or the user can reach any
    /// state without waiting. A "sim" button (top right, inside the safe area) opens it; <see cref="SliceDebugPanel"/> keeps
    /// the camp/era/box/music tools. IMGUI, drawn on the safe area, scrollable by drag. Every action also goes through the
    /// static <c>Do*</c> methods, so `script eval` can call them (SimDebugPanel.DoRelic("lens")).
    /// Sections: Time, Dilemmas (P7, <see cref="DilemmaCheats"/>), Fire, Era and box, Notables (selected one), Roles, Buildings, Shaping, Wishes, Relics and belief, Care.
    /// </summary>
    [AddComponentMenu("AquariumCiv/TEMPORARY Sim Debug Panel")]
    public class SimDebugPanel : MonoBehaviour
    {
        public bool open { get => DebugDock.Active == DebugDock.Sim; set => DebugDock.Active = value ? DebugDock.Sim : DebugDock.None; }
        float contentH = 600f;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        readonly Dictionary<string, bool> fold = new();
        Vector2 scroll;
        string last = "";
        int hintStage = 1; // #424: the "Hint X" cheats play this stage
        int sel;                 // selected notable id (-1 = none)
        float shapeX, shapeZ = 0.8f;
        int shapeKind;
        readonly List<RelicMomentInfo> reactions = new();
        string runAhead = "run ahead: not asked yet";
        static readonly string[] Types = { BuildingType.Hut, BuildingType.Farm, BuildingType.Lumbermill, BuildingType.Well,
            BuildingType.Windmill, BuildingType.Market, BuildingType.Tavern, BuildingType.Church, BuildingType.Workshop, BuildingType.Guildhall, BuildingType.Bakery };
        static readonly string[] Jobs = { JobType.Gatherer, JobType.Hunter, JobType.Fisher, JobType.Woodcutter, JobType.Toolmaker,
            JobType.Farmer, JobType.Shepherd, JobType.Beekeeper, JobType.Potter, JobType.Brewer, JobType.Mason, JobType.Smith,
            JobType.Weaver, JobType.Baker, JobType.Stallholder, JobType.Sweeper, JobType.Elder };
        static readonly string[] Roles = { RoleType.Chief, RoleType.Headman, RoleType.Lord, RoleType.Miller, RoleType.Priest,
            RoleType.Innkeeper, RoleType.Guildmaster, RoleType.MasterBuilder, RoleType.Merchant, RoleType.Captain };

        // ---------- shared helpers (also for `script eval`) ----------
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : SimLayoutSource.Find()?.Sim;

        static string Done(string text)
        {
            if (GameClock.Instance != null && GameClock.Instance.Sim != null) GameClock.Instance.Save();
            var box = SimLayoutSource.Find();
            if (box != null) box.GetComponent<BoxView>()?.Refresh();
            return text;
        }

        static string Need(out SimCore sim)
        {
            sim = Sim;
            return sim == null ? "no sim" : null;
        }

        public static string DoRelic(string id) { var e = Need(out var s); if (e != null) return e; var r = s.DebugGiveRelic(id); return Done(r == null ? "no " + id : "relic " + id); }
        public static string DoLoseSandal() { var e = Need(out var s); if (e != null) return e; return Done(s.DebugLoseSandal() != null ? "sandal lost" : "nobody to lose it"); }
        public static string DoSandalYears() { var e = Need(out var s); if (e != null) return e; return Done(s.DebugSkipSandalYears(20) ? "sandal lost 20 years longer" : "no lost sandal"); }
        public static string DoBelief(int stage, bool later) { var e = Need(out var s); if (e != null) return e; s.DebugSetBelief(stage, later); return Done($"belief stage {stage} {(later ? "later" : "early")}"); }
        public static string DoTapBack() { var e = Need(out var s); if (e != null) return e; int id = s.DebugTapBack(3); return Done(id < 0 ? "nobody tapped back" : "tapped back: " + Name(s, id)); }
        public static string DoShape(string kind, float x, float z)
        {
            var e = Need(out var s); if (e != null) return e;
            var n = s.DebugShape(kind, x, z);
            return Done(n == null ? "no such tool" : $"{kind} #{n.id} at ({x:0.0}, {z:0.0})");
        }
        public static string DoSkipToAnswer()
        {
            var e = Need(out var s); if (e != null) return e;
            var n = s.State.nudges.LastOrDefault(x => !x.credited);
            return Done(n != null && s.DebugSkipToAnswer(n.id) ? $"people answered {n.kind} #{n.id}" : "nothing waiting for an answer");
        }
        public static string DoWish(string kind, int notableId)
        {
            var e = Need(out var s); if (e != null) return e;
            var w = s.DebugForceWish(kind, notableId);
            return Done(w == null ? "nobody can wish" : $"{Name(s, w.notableId)} wishes for {w.kind}");
        }

        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();

        /// <summary>Camera fly-to cheats (#38): "person" (notable id, -1 = a random one alive), "fire", "building" (sim id, -1 = the first), "point" (x, z).</summary>
        public static string DoFly(string what, int id = -1, float x = 0f, float z = 0f)
        {
            var cam = Cam; if (cam == null) return "no BoxCamera";
            var sim = Sim;
            switch (what)
            {
                case "person":
                {
                    if (sim == null) return "no sim";
                    var alive = sim.State.notables.Where(n => n.alive).ToList();
                    if (alive.Count == 0) return "nobody alive";
                    var n = id >= 0 ? sim.FindNotable(id) : alive[Random.Range(0, alive.Count)];
                    return n != null && cam.FlyToPerson(AquariumCiv.Life.PersonRef.Notable(n.id)) ? "flying to " + n.FullName : "no doll for " + (n?.FullName ?? "#" + id);
                }
                case "fire":
                    return cam.FlyToPlace(new AquariumCiv.Life.Place(AquariumCiv.Life.PlaceKind.Fire)) ? "flying to the fire" : "no fire spot";
                case "building":
                {
                    if (sim == null) return "no sim";
                    int bid = id >= 0 ? id : (sim.State.buildings.FirstOrDefault()?.id ?? -1);
                    return cam.FlyToBuilding(bid) ? "flying to building #" + bid : "no spot for building #" + bid;
                }
                case "point": cam.FlyTo(new Vector3(x, 0f, z), 0.6f); return $"flying to ({x:0.0}, {z:0.0})";
                case "overview": cam.FlyTo(Vector3.zero, 1f); return "flying to the overview";
            }
            return "unknown " + what;
        }

        /// <summary>Flips the camp fire's look (#65; "current", "soft"/"a", "embers"/"b", "paper"/"c") and rebuilds the box.</summary>
        public static string DoFireLook(string name)
        {
            if (!FireLooks.TryParse(name, out var look)) return "unknown fire look " + name;
            FireLooks.Active = look;
            BoxView.DebugRebuild();
            return "fire look: " + look;
        }

        static string Name(SimCore s, int id) => s.FindNotable(id)?.FullName ?? ("#" + id);

        void TimeLapseGui()
        {
            var tl = TimeLapseView.Find();
            if (tl == null || !tl.Active)
            {
                if (GUILayout.Button("Open replay")) last = TimeLapseView.DebugOpen();
                return;
            }
            float end = tl.Lapse.EndPosition, p = tl.Position;
            GUILayout.Label($"year {p + 1f:0.0} of {end + 1f:0.0} · {tl.Lapse.EraAt(p)} · {tl.LastInfo}");
            float np = GUILayout.HorizontalSlider(p, 0f, end);
            if (Mathf.Abs(np - p) > 1e-4f) tl.Seek(np);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(tl.Clock.Playing ? "pause" : "play")) tl.Clock.Toggle();
            for (int i = 0; i < ReplayClock.Speeds.Length; i++)
                if (GUILayout.Toggle(tl.Clock.SpeedIndex == i, ReplayClock.Speeds[i] + "x", GUI.skin.button)) tl.SetSpeed(i);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Back to now")) last = TimeLapseView.DebugClose();
        }

        void FirstRunGui()
        {
            GUILayout.Label(FirstRunView.DebugStatus());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Replay")) last = FirstRunView.DebugReplay();
            if (GUILayout.Button("Skip all")) last = FirstRunView.DebugSkipAll();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (var step in new[] { FirstRunStep.Patch, FirstRunStep.Name, FirstRunStep.Rain, FirstRunStep.Year, FirstRunStep.Card })
                if (GUILayout.Button(step.ToString())) last = FirstRunView.DebugSkipTo(step);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            FirstRunView.DebugKeyboard = GUILayout.Toggle(FirstRunView.DebugKeyboard, "Keyboard up", GUI.skin.button);
            if (GUILayout.Button("Refused name")) last = FirstRunView.DebugRefuseName();
            if (GUILayout.Button("Reward line")) last = FirstRunView.DebugReward();
            GUILayout.EndHorizontal();
        }

        // ---------- IMGUI ----------
        void OnGUI()
        {
            if (DebugDock.Active != DebugDock.Sim || DebugDock.Hidden) return;
            var safe = DebugDock.Begin(out float sc);
            var area = DebugDock.PanelArea(safe, 340f, contentH + 60f);
            DebugDock.SetPanel(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            var sim = Sim;
            if (sim == null)
            {
                GUILayout.Label("no sim yet");
                if (GUILayout.Button("close")) open = false;
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(Status(sim));
            if (GUILayout.Button("close", GUILayout.Width(52))) open = false;
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(last)) GUILayout.Label(last);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginVertical();

            if (Section("Time"))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+1 yr")) last = SimLayoutSource.DebugSkipYears(1);
                if (GUILayout.Button("+10 yr")) last = SimLayoutSource.DebugSkipYears(10);
                if (GUILayout.Button("+40 yr")) last = SimLayoutSource.DebugSkipYears(40);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Live +", GUILayout.Width(44));
                if (GUILayout.Button("1h")) last = GameClock.DebugSkipHours(1);
                if (GUILayout.Button("8h")) last = GameClock.DebugSkipHours(8);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Away", GUILayout.Width(44));
                if (GUILayout.Button("+1h")) last = GameClock.DebugAway(1);
                if (GUILayout.Button("+8h")) last = GameClock.DebugAway(8);
                if (GUILayout.Button("+48h")) last = GameClock.DebugAway(48);
                GUILayout.EndHorizontal();
                if (sim.EcologyOn && GUILayout.Button("Pretend I was away, with new grass")) last = GameClock.DebugPretendAwayNewGrass();
                if (GUILayout.Button("One story event")) { sim.ForceStory(); last = Done("story forced"); }
            }

            if (Section("Time-lapse"))
            {
                TimeLapseGui();
                TimeLapseScreenView.BookRibbon = GUILayout.Toggle(TimeLapseScreenView.BookRibbon, "Book: new pages (ribbon)", GUI.skin.button);
            }
            if (Section("First run")) FirstRunGui();
            if (Section("Cards (#75)"))
            {
                GUILayout.Label(CardStackView.Status());
                GUILayout.BeginHorizontal();
                foreach (int n in new[] { 1, 3, 5, 7 }) if (GUILayout.Button("Return " + n)) last = CardStackView.DebugReturn(n);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Clear read cards")) last = CardStackView.DebugClearRead();
                GUILayout.BeginHorizontal();
                foreach (CardKind kind in System.Enum.GetValues(typeof(CardKind))) if (GUILayout.Button(kind.ToString())) last = CardStackView.DebugCard(kind);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                foreach (var what in new[] { "payoff", "end", "long", "two" }) if (GUILayout.Button("Text: " + what)) last = CardStackView.DebugText(what);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Show me on this card (the bar)")) last = CardStackView.DebugShowMe();
            }
            if (Section("Dilemmas (P7)")) { var line = DilemmaCheats.Gui(); if (line != null) last = line; }
            if (Section("Hit puffs (#357)")) { var line = HitFxCheats.Gui(); if (line != null) last = line; } // #357
            // ---- #369: the moment queue and director ----
            if (Section("Moments (#369)"))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Queue one moment of each kind")) last = HeldMomentStage.DebugQueueOneOfEach();
                if (GUILayout.Button("Play next moment now")) last = HeldMomentStage.DebugPlayNextNow();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Busy day")) last = HeldMomentStage.DebugPlayNextNow(true);
                if (GUILayout.Button("Show queue + next due time")) last = HeldMomentStage.DebugStatus();
                if (GUILayout.Button("Moments on/off")) last = HeldMomentStage.DebugToggle();
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Stage the held choice now (t1-moments)")) last = HeldMomentStage.DebugStageHeldChoice(); // visit-loop 2.4
                // ---- t3-choice: the box-staged choice's hold ----
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Box choice: status")) last = ChoiceSceneView.DebugStatus();
                if (GUILayout.Button("Box choice: let go now")) last = ChoiceSceneView.DebugRelax();
                GUILayout.EndHorizontal();
                // ---- end t3-choice ----
            }
            // ---- t3-welcome: the greeting when nothing waits ----
            if (GUILayout.Button("Welcome now (nothing queued)")) last = HeldMomentStage.DebugWelcomeNow();
            // ---- end t3-welcome ----
            // ---- end #369 ----
            // ---- #374: this visit's telemetry numbers ----
            if (Section("Visit telemetry (#374)"))
            {
                var vt = GameClock.Instance != null ? GameClock.Instance.VisitTracker() : null;
                GUILayout.Label(vt != null ? vt.Line() : "no visit telemetry (no box or telemetry off)");
            }
            // ---- end #374 ----
            // ---- #371: the things a choice leaves in the box ----
            if (Section("Choices leave things (#371)"))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Place every consequence prop")) last = ConsequenceCheats.PlaceEvery();
                if (GUILayout.Button("Play a callback moment now")) last = ConsequenceCheats.PlayCallbackNow();
                GUILayout.EndHorizontal();
            }
            // ---- end #371 ----
            if (Section("Staged moments (#370)")) { var line = StagedMoments.Gui(); if (line != null) last = line; } // #370
            // ---- #373: the Keeper's choices page and the hand pins ----
            if (Section("Keeper's choices (#373)"))
            {
                if (GUILayout.Button("Fill the Keeper's choices (3 eras of sample choices)"))
                {
                    int n = sim.DebugFillKeeperChoices();
                    last = Done(n > 0 ? $"{n} sample choices in the book and on the time-lapse" : "already filled, or no era reached yet");
                }
            }
            // ---- end #373 ----
            // ---- #417: the life jar (placed-life spec §10); "place" opens a shut jar and places by the camp, no ghost ----
            if (sim.EcologyOn && Section("Life jar (#417)"))
            {
                var all = AquariumCiv.Sim.Eco.LifeJar.All;
                GUILayout.Label(string.Join(" · ", all.Select(s => s + " " + sim.JarStateOf(s))));
                if (GUILayout.Button("Ready every species")) last = Done($"{sim.DebugJarsReady()} jars opened");
                for (int i = 0; i < all.Length; i += 3)
                {
                    GUILayout.BeginHorizontal();
                    for (int k = i; k < i + 3 && k < all.Length; k++)
                        if (GUILayout.Button("Place " + all[k])) { int c = sim.DebugPlaceNearCamp(all[k]); last = Done(c < 0 ? $"{all[k]}: no valid ground" : $"{all[k]} placed at cell {c}"); }
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                foreach (var s in all)
                    if (AquariumCiv.Sim.Eco.LifeJar.Restocks(s) && GUILayout.Button("Again " + s)) last = Done(sim.DebugJarAgain(s) ? $"{s} jar open again" : $"{s}: not placed");
                GUILayout.EndHorizontal();
                // ---- #519: the ducks and the fish keep to the water you can see ----
                if (GUILayout.Button("Pond dries now (ducks/fish leave, #519)")) { int n = sim.DebugPondDries(); last = Done($"{n} pond cells dried; ducks {sim.EcoDucks}, fish {sim.EcoFish}"); }
                // ---- end #519 ----
                // #423: the life tool in hand (glow, ghost, tap to place), for a ready jar
                var hudL = GameHud.Instance;
                GUILayout.BeginHorizontal();
                foreach (var s in all)
                    if (sim.JarReady(s) && hudL != null && GUILayout.Button("Hold " + s)) { hudL.TakeLife(s); last = Done($"{s} in hand: tap the glow"); }
                GUILayout.EndHorizontal();
                var lpv = LifePlaceView.Instance;
                if (lpv != null && lpv.Last.Length > 0) GUILayout.Label($"life tool: {lpv.Last} (glow {lpv.GlowCells})");
                // #424: the arrivals (replay any placed species'), the wolves' first howl, the waiting hint at each stage
                var la = LifeArrivalView.Instance;
                if (la != null)
                {
                    GUILayout.Label(la.Status());
                    for (int i = 0; i < all.Length; i += 3)
                    {
                        GUILayout.BeginHorizontal();
                        for (int k = i; k < i + 3 && k < all.Length; k++)
                            if (GUILayout.Button("Replay " + all[k])) last = Done(la.Replay(all[k]));
                        GUILayout.EndHorizontal();
                    }
                    if (GUILayout.Button("Wolves howl now")) last = Done(la.DebugHowl());
                    // #425: the everyday deer and wolves (P0's quadruped): status, a knock, a person near, the hour they show off at
                    var dv = DeerView.Instance; var wv = WolfView.Instance;
                    if (dv != null) GUILayout.Label(dv.Status());
                    if (wv != null) GUILayout.Label(wv.Status());
                    GUILayout.BeginHorizontal();
                    if (dv != null && GUILayout.Button("Knock by the deer"))
                    {
                        var ps = new System.Collections.Generic.List<Vector3>(); dv.WorldPositions(ps);
                        if (ps.Count == 0) last = Done("no deer shown");
                        else { var loc = dv.transform.InverseTransformPoint(ps[0]); last = Done($"{dv.KnockAtGround(loc.x + 0.2f, loc.z + 0.2f)} deer bolt"); }
                    }
                    if (dv != null && GUILayout.Button(dv.Scarers.Count > 0 ? "Person by the deer: off" : "Person by the deer"))
                    {
                        if (dv.Scarers.Count > 0) { dv.Scarers.Clear(); last = Done("nobody by the deer"); }
                        else if (dv.TryGetDeer(0, out var d0, out _)) { dv.Scarers.Add(new Vector2(d0.x + 0.25f, d0.z)); last = Done("someone stands by the deer: heads up, then they walk off"); }
                    }
                    if (sim.WolvesPlaced && GUILayout.Button("Wolves: first howl again")) { sim.State.eco.firstHowlHeard = false; last = Done("the first howl plays at the next dusk"); }
                    GUILayout.EndHorizontal();
                    var sdv = StoryDirectorView.Instance;
                    GUILayout.BeginHorizontal();
                    foreach (var s in all)
                        if (s != AquariumCiv.Sim.Eco.LifeSpecies.Wolves && sdv != null && GUILayout.Button("Hint " + s)) last = Done(sdv.HintNow(s, hintStage));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    for (int st = 1; st <= 3; st++) if (GUILayout.Toggle(hintStage == st, $"stage {st}", "Button")) hintStage = st;
                    GUILayout.EndHorizontal();
                }
            }
            // ---- end #417 ----
            // ---- #420-#422: deer, ducks, fish; floors, shy, restraint (placed-life spec §10) ----
            if (sim.EcologyOn && Section("Placed life (#420-#422)"))
            {
                GUILayout.Label($"deer {sim.EcoDeerCount}/{sim.EcoDeerCap} · ducks {sim.EcoDucks} eggs {sim.EcoDuckEggs} · fish {sim.EcoFish} · white stag {sim.WhiteStagState}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Settle a deer hit (sim)")) last = Done(sim.DebugDeerHuntNow() ? $"a deer taken, {sim.EcoDeerCount} left" : "no deer to spare");
                if (GUILayout.Button("Deer to capacity")) last = Done($"{sim.DebugDeerToCap()} deer");
                if (GUILayout.Button("White stag now")) last = Done(DeerScenesView.Instance != null ? DeerScenesView.Instance.DebugWhiteStagNow() : sim.DebugWhiteStagNow() ? "the white stag shows" : "it already has");
                if (GUILayout.Button("Eggs now")) last = Done($"{sim.DebugEggsNow()} eggs in the nests");
                GUILayout.EndHorizontal();
                // #426: the deer hunt you watch (bows, the stalk, the arrow, the pole carry), the feast, the antlers
                var dhs = DeerHuntStage.Instance;
                if (dhs != null) GUILayout.Label(dhs.Status());
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Deer hunt now (hit)")) last = Done(DeerHuntStage.DebugHuntNow(true));
                if (GUILayout.Button("Deer hunt now (miss)")) last = Done(DeerHuntStage.DebugHuntNow(false));
                if (dhs != null && GUILayout.Button("Deer feast now")) last = Done(dhs.DebugFeastNow());
                if (dhs != null && GUILayout.Button("Antlers up now")) last = Done(dhs.DebugAntlersNow());
                GUILayout.EndHorizontal();
                // #427: dawn at the pond, the white stag (its choice; after "let it be" it shows at dawn)
                var dsv = DeerScenesView.Instance;
                if (dsv != null)
                {
                    GUILayout.Label(dsv.Status());
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Dawn at the pond now")) last = Done(dsv.DebugDawnNow());
                    if (GUILayout.Button("White stag at dawn now")) last = Done(dsv.DebugWhiteDawnNow());
                    GUILayout.EndHorizontal();
                }
                var takers = new[] { AquariumCiv.Sim.Eco.LifeSpecies.Rabbits, AquariumCiv.Sim.Eco.LifeSpecies.Deer, AquariumCiv.Sim.Eco.LifeSpecies.Ducks, AquariumCiv.Sim.Eco.LifeSpecies.Fish };
                GUILayout.BeginHorizontal();
                foreach (var s in takers)
                    if (GUILayout.Button("Floor " + s)) last = Done(sim.DebugToFloor(s) ? $"{s} at its floor (shy)" : $"{s}: not placed");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                foreach (var s in takers)
                    if (GUILayout.Button("Restraint " + s)) last = Done(sim.DebugRestraintNow(s) ? $"{s}: the ruling" : $"{s}: ruled already");
                GUILayout.EndHorizontal();
            }
            // ---- end #420-#422 ----
            // ---- #437/#438: the wolves and the dogs (placed-life spec §10) ----
            if (sim.EcologyOn && Section("Wolves and dogs (#437-#438)"))
            {
                GUILayout.Label($"wolves {sim.EcoWolfCount} (pups {sim.EcoWolfPups}) · bold {sim.WolfBoldness:0.00} · {sim.WolfWayTaken} · kills {sim.WolfKillCount} · thefts {sim.WolfThefts} · dogs {sim.DogCount} · pen {sim.DuckPenGuard}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ready wolves")) last = Done(sim.DebugReadyWolves() ? "the wolves' jar is open" : "no den spot, or placed already");
                if (GUILayout.Button("Place wolves (far wood)")) { int c = sim.DebugPlaceWolves(); last = Done(c < 0 ? "no den spot, or placed already" : $"the pair at den cell {c}"); }
                if (GUILayout.Button("Wolves bold (1)")) last = Done(sim.DebugWolvesBold() ? "boldness 1" : "no wolves");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Meat thief now")) last = Done(sim.DebugMeatThiefNow() ? "a wolf took from the rack" : "no wolves, or the thief is stopped");
                if (GUILayout.Button("Wolves' kill now")) { var k = sim.DebugWolfKillNow(); last = Done(k == null ? "no prey to spare" : $"{k.Prey} at cell {k.cell}: the ravens circle"); }
                if (GUILayout.Button("White wolf now")) last = Done(sim.DebugWhiteWolfNow() ? "the white wolf howls" : "no wolves, or it already has");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Lost pup now")) last = Done(sim.DebugLostPupNow() ? "a child found a pup" : "a dog came home already");
                if (GUILayout.Button("Next dog generation")) last = Done($"{sim.DebugNextDogGeneration()} dogs");
                if (GUILayout.Button("Wolves at the pen now")) last = Done(sim.DebugWolvesAtPenNow() ? "the pack circled the pen" : "no wolves, or it has already");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ask: thief")) last = Done(sim.DebugRaiseLifeChoice(LifeChoices.Thief) != null ? "the thief choice is open" : "no theft yet (meat thief now)");
                if (GUILayout.Button("Ask: kill")) last = Done(sim.DebugRaiseLifeChoice(LifeChoices.Kill) != null ? "the kill choice is open" : "no fresh kill (wolves' kill now)");
                if (GUILayout.Button("Ask: pen")) last = Done(sim.DebugRaiseLifeChoice(LifeChoices.Pen) != null ? "the pen choice is open" : "no circling, or no grown dog");
                GUILayout.EndHorizontal();
            }
            // ---- end #437/#438 ----
            if (Section("Fire (#65)"))
            {
                GUILayout.BeginHorizontal();
                foreach (var look in FireLooks.All)
                    if (GUILayout.Toggle(FireLooks.Active == look, look.ToString(), GUI.skin.button) && FireLooks.Active != look) last = DoFireLook(look.ToString());
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(NightLamps.Level > 0.5f ? "Day" : "Night")) last = NightLamps.DebugSetLevel(NightLamps.Level > 0.5f ? 0f : 1f);
                if (GUILayout.Button("Fly to the fire")) last = DoFly("fire");
                GUILayout.EndHorizontal();
            }
            if (Section("Era and box"))
            {
                var st = sim.State;
                GUILayout.Label($"era {st.era} · ready {st.eraReady} · peak {st.peakReady} · complete {st.boxComplete} · glass {sim.GlassStage}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Era ready")) last = Done(sim.ForceEraReady() ? "era ready" : "already ready / last era");
                if (GUILayout.Button("Next era")) last = SimLayoutSource.DebugNextEra(true);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Peak ready")) last = Done(BoxCompleteController.DebugPeak());
                if (GUILayout.Button("Last moment")) last = BoxCompleteController.DebugMoment();
                GUILayout.EndHorizontal();
                // #97: what a close would schedule: the run ahead on a copy, and what it took here.
                GUILayout.Label(runAhead);
                if (GUILayout.Button("Run ahead now")) runAhead = RunAheadLine(sim);
                // #97: the notification by hand: what a close schedules, without leaving.
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Close now")) runAhead = GameClock.DebugClose();
                if (GUILayout.Button("Notifications: " + (GameSettings.Notifications ? "on" : "off"))) GameSettings.Notifications = !GameSettings.Notifications;
                if (GUILayout.Button("Ask")) last = MomentPlayer.DebugNotifyAsk();
                GUILayout.EndHorizontal();
                if (GUILayout.Button("End festival")) { sim.DebugEndFestival(); last = Done("festival ended"); }
                if (GUILayout.Button("Open teaser")) last = Done(sim.DebugOpenTeaser() ?? "no teaser");
            }

            if (Section("Settings (#95)"))
            {
                GUILayout.BeginHorizontal();
                DeviceSilent.DebugForce = GUILayout.Toggle(DeviceSilent.DebugForce, "Phone silent", GUI.skin.button);
                GlassKeyboard.Debug = GUILayout.Toggle(GlassKeyboard.Debug, "Keyboard up", GUI.skin.button);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                foreach (var id in new[] { SettingsSheets.Rename, SettingsSheets.Reset, SettingsSheets.Licences })
                    if (GUILayout.Button(id) && GameHud.Instance != null) GameHud.Instance.Open(id);
                GUILayout.EndHorizontal();
            }

            if (Section("Shelf (#93)"))
            {
                GUILayout.Label(BoxCompleteController.Status());
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Complete now: oasis")) last = BoxCompleteController.DebugComplete(BoxSetting.DesertOasis);
                if (GUILayout.Button("Complete now: mountain")) last = BoxCompleteController.DebugComplete(BoxSetting.MountainVillage);
                GUILayout.EndHorizontal();
                var shelf = GameClock.Instance?.Shelf;
                if (shelf != null)
                    foreach (var sb in shelf.boxes.Where(b => b.museum))
                        if (GUILayout.Button("Visit box " + sb.index + " (" + sb.setting + ")")) last = BoxCompleteController.DebugVisit(sb.index);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Back to active")) last = BoxCompleteController.DebugBack();
                if (GUILayout.Button("Shelf view")) last = BoxCompleteController.DebugShelf();
                if (GUILayout.Button("Setting picker")) last = BoxCompleteController.DebugPicker();
                GUILayout.EndHorizontal();
            }

            if (Section("Stash (#150)"))
            {
                var stash = PeopleView.Find()?.Planner;
                GUILayout.Label(stash != null && stash.TryGetStash(out var pile) ? $"logs {pile.stock.logs}, baskets {pile.stock.baskets}, hides {pile.stock.hides}" : "no stash after the camp");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Stock low")) last = Done(StashProps.DebugStock(sim, false));
                if (GUILayout.Button("Stock high")) last = Done(StashProps.DebugStock(sim, true));
                GUILayout.EndHorizontal();
            }
            if (Section("Crowd (#148)"))
            {
                GUILayout.Label($"in the crowd: {sim.CrowdCount}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Open a crowd card")) last = PersonCardView.DebugOpenCrowd();
                if (GUILayout.Button("One rises")) { var risen = sim.ForceCrowdRise(); last = Done(risen != null ? risen.FullName + " stepped out of the crowd" : "no room for a notable"); }
                GUILayout.EndHorizontal();
            }

            if (Section("Notables")) NotablesGui(sim);
            if (Section("Roles")) RolesGui(sim);

            if (Section("Camera"))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Fly: random person")) last = DoFly("person");
                if (GUILayout.Button("Fly: selected")) last = DoFly("person", sel);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Fly: fire")) last = DoFly("fire");
                if (GUILayout.Button("Fly: building")) last = DoFly("building");
                if (GUILayout.Button("Fly: overview")) last = DoFly("overview");
                GUILayout.EndHorizontal();
            }

            if (Section("Show me & bubbles"))
            {
                // #76: Show me on any of the latest cards' entries, a bubble now.
                GUILayout.Label(ShowMeDirector.Status());
                foreach (var e in ShowMeDirector.Latest(8))
                {
                    string h = sim.Headline(e);
                    if (h.Length > 34) h = h.Substring(0, 33) + "…";
                    if (GUILayout.Button($"Show me: {e.kind} · {h}")) last = ShowMeDirector.DebugShow(e.id);
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Force a line")) last = ShowMeDirector.DebugBubble();
                if (GUILayout.Button("Tree line")) last = ShowMeDirector.DebugTreeLine(); // #318
                if (GUILayout.Button("End show me")) last = ShowMeDirector.DebugEnd();
                GUILayout.EndHorizontal();
            }

            if (Section("Buildings"))
            {
                GUILayout.Label(string.Join(" ", sim.State.buildings.GroupBy(b => b.type).Select(g => g.Key + "×" + g.Count())));
                for (int i = 0; i < Types.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < i + 2 && j < Types.Length; j++)
                    {
                        if (GUILayout.Button("+" + Types[j])) last = SimLayoutSource.DebugBuild(Types[j]);
                        if (GUILayout.Button("-", GUILayout.Width(28))) last = SimLayoutSource.DebugRemoveBuilding(Types[j]);
                    }
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button(SimLayoutSource.CobbleAllPaths ? "Cobbles on every path: on (tap = off)" : "Cobbles on every path: off (tap = on)"))
                    last = SimLayoutSource.DebugCobblePaths(!SimLayoutSource.CobbleAllPaths);
            }

            if (Section("Shaping"))
            {
                GUILayout.Label($"charges {sim.ShapingCharges:0.0}/{sim.ShapingMaxCharges:0}");
                var tools = SimCore.SliceTools;
                shapeKind = Mathf.Clamp(shapeKind, 0, tools.Length - 1);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < tools.Length; i++) if (GUILayout.Toggle(shapeKind == i, tools[i], GUI.skin.button)) shapeKind = i;
                GUILayout.EndHorizontal();
                shapeX = Slider("x", shapeX, -2.6f, 2.6f);
                shapeZ = Slider("z", shapeZ, -1.9f, 1.9f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Shape here (free)")) last = ShapingView.Instance != null ? ShapingView.DebugPlace(tools[shapeKind], shapeX, shapeZ) : DoShape(tools[shapeKind], shapeX, shapeZ);
                if (GUILayout.Button("Refill")) { sim.DebugRefillShaping(); last = Done("charges full"); }
                GUILayout.EndHorizontal();
                var sv = ShapingView.Instance;
                if (sv != null)
                {
                    // #85: pick a free tool here and tap the ground (a drag moves the camera).
                    GUILayout.Label(sv.ShapeMode ? $"shape mode: tap the ground to place {sv.Tool}" : "tap to place: pick a tool");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Toggle(!sv.ShapeMode, "off", GUI.skin.button)) { if (sv.ShapeMode) sv.SetTool(null); }
                    foreach (var k in tools) if (GUILayout.Toggle(sv.Tool == k, k, GUI.skin.button) && sv.Tool != k) sv.SetTool(k);
                    GUILayout.EndHorizontal();
                    sv.Free = GUILayout.Toggle(sv.Free, sv.Free ? "free (no charge, lock or cap)" : "real (spends a charge)");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Take back last")) last = ShapingView.DebugTakeBack();
                    if (GUILayout.Button("Answer last")) last = ShapingView.DebugSkip();
                    GUILayout.EndHorizontal();
                    // #167, #149, #145: a real tool in hand, the refused mark, the Take back chip's timer.
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("In hand:");
                    foreach (var k in tools) if (GUILayout.Button(k)) last = ShapingView.DebugHand(k);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Refused spot")) last = ShapingView.DebugRefused();
                    if (GUILayout.Button("Chip: fading")) last = ShapingView.DebugChipLeft(3f);
                    if (GUILayout.Button("Chip: gone")) last = ShapingView.DebugChipLeft(0f);
                    GUILayout.EndHorizontal();
                }
                // #142: the first reaction to a gift, and who's playing it now.
                var planner = PeopleView.Find()?.Planner;
                int reacting = planner != null ? planner.ActiveShapingReactions(PeopleView.Find().Now, reactions) : 0;
                GUILayout.Label(reacting == 0 ? "reactions: none playing" : $"reactions: {reacting} playing to gift #{reactions[0].nudgeId}");
                if (GUILayout.Button("People react to the last gift")) last = ShapingView.DebugReact();
                var waiting = sim.State.nudges.Count(n => !n.credited);
                if (GUILayout.Button($"People answer the last one ({waiting} waiting)")) last = sv != null ? ShapingView.DebugSkip() : DoSkipToAnswer();
            }

            if (Section("Wishes"))
            {
                var ow = sim.OpenWish;
                GUILayout.Label(ow == null ? "no open wish" : $"{Name(sim, ow.notableId)} wishes for {ow.kind}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Wish (selected)")) last = DoWish(null, sel);
                if (GUILayout.Button("Wish (anyone)")) last = DoWish(null, -1);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                foreach (var k in SimCore.SliceTools) if (GUILayout.Button(k)) last = DoWish(k, sel);
                GUILayout.EndHorizontal();
            }

            if (Section("Relics and belief"))
            {
                GUILayout.Label("relics: " + (sim.State.relics.Count == 0 ? "none" : string.Join(", ", sim.State.relics.Select(r => r.id + (r.Found ? "" : " (lost)")))));
                for (int i = 0; i < RelicId.All.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < i + 2 && j < RelicId.All.Length; j++)
                        if (GUILayout.Button("Give " + RelicId.All[j])) last = DoRelic(RelicId.All[j]);
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Lose sandal")) last = DoLoseSandal();
                if (GUILayout.Button("Sandal +20 yr")) last = DoSandalYears();
                GUILayout.EndHorizontal();
                for (int stage = 1; stage <= 3; stage++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Belief " + stage, GUILayout.Width(60));
                    if (GUILayout.Button("early")) last = DoBelief(stage, false);
                    if (GUILayout.Button("later")) last = DoBelief(stage, true);
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Glass: tap back x3 (sim only)")) last = DoTapBack();
                // #92: the moments and the people at the glass, as the view plays them.
                GUILayout.Label(GlassVoice.Status());
                for (int i = 0; i < RelicId.All.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < i + 2 && j < RelicId.All.Length; j++)
                        if (GUILayout.Button("Play " + RelicId.All[j])) last = GlassVoice.DebugPlayMoment(RelicId.All[j]);
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Start a ritual now (Keeper Doll)")) last = GlassVoice.DebugStartRitual();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Gather at glass")) last = GlassVoice.DebugGather();
                if (GUILayout.Button("Knock x3")) last = GlassVoice.DebugKnock(3);
                if (GUILayout.Button("Tap back x3")) last = GlassVoice.DebugTapBack(3);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Front: camera")) last = GlassVoice.DebugGlassSide(-1);
                for (int side = 0; side < 4; side++) { int sd = side; if (GUILayout.Button("side " + sd)) last = GlassVoice.DebugGlassSide(sd); }
                GUILayout.EndHorizontal();
            }

            if (Section("Care"))
            {
                GUILayout.Label($"mess {sim.MessLevel:0.00} · grime {sim.State.grime:0.00} · overgrowth {sim.State.overgrowth:0.00}");
                if (GUILayout.Button("Sweepers trim weeds")) last = Done(sim.DebugSweep() ? "trimmed" : "no sweepers (town)");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+grime .3")) { sim.AddMess(0.3f, 0f); last = Done("grime +0.3"); }
                if (GUILayout.Button("+weeds .3")) { sim.AddMess(0f, 0.3f); last = Done("overgrowth +0.3"); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Full mess")) { sim.AddMess(1f, 1f); last = Done("full mess"); }
                if (GUILayout.Button("Clean all")) { sim.CleanAll(); last = Done("cleaned all"); }
                GUILayout.EndHorizontal();
                var hud = GameHud.Instance;
                if (hud != null && GUILayout.Button(hud.CleanMode ? "Clean mode: on (tap = off)" : "Clean mode: off (tap = on)")) { hud.SetCleanMode(!hud.CleanMode); last = Done("clean mode " + (hud.CleanMode ? "on" : "off")); }
                var care = CareView.Instance;
                if (care != null) GUILayout.Label($"sun x{care.SunFactor:0.00} · glass drawn {care.GlassMean:0.00} · weeds {care.Props.WeedCount} · strokes {care.StrokeCount}");
                if (sim.EcologyOn)
                {
                    // #311 / #314: what the tweezers take
                    GUILayout.Label($"vine {sim.EcoVine.Count} cells · tree weeds {sim.EcoTreeWeedPieces}/3 · plucks {(care != null ? care.Plucks : 0)}");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Weeds round tree")) last = Done(sim.DebugGrowTreeWeeds() ? "weeds round the tree" : "no tree planted yet");
                    if (GUILayout.Button("Creep forest edge")) last = Done(sim.DebugCreepForestEdge(6) + " saplings crept in");
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Grow vines here"))
                    {
                        var at = new Vector3(sim.EcoGround.campX + 0.8f, 0f, sim.EcoGround.campZ);
                        var mist = EcoMistView.Instance;
                        if (mist != null && mist.GroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out var g)) at = g;
                        last = Done(sim.DebugGrowVine(at.x, at.z, 8) + " vine cells");
                    }
                    if (hud != null && GUILayout.Button(hud.Hand == GameHud.KitTool.Tweezers ? "Tweezers: in hand" : "Give tweezers")) { hud.SetHand(GameHud.KitTool.Tweezers); last = Done("tweezers in hand"); }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) contentH = GUILayoutUtility.GetLastRect().height;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string RunAheadLine(SimCore sim)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = sim.RunAhead(s => JsonUtility.FromJson<SimState>(JsonUtility.ToJson(s)));
            sw.Stop();
            string found = r.Found ? $"{r.target} {r.era} in {r.realHours:0.0} real h" : r.miss.ToString();
            return $"run ahead: {found} · {r.monthsRun} months in {sw.ElapsedMilliseconds} ms";
        }

        bool Section(string title)
        {
            fold.TryGetValue(title, out bool on);
            bool now = GUILayout.Toggle(on, (on ? "v " : "> ") + title, GUI.skin.button);
            fold[title] = now;
            return now;
        }

        static float Slider(string label, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + " " + v.ToString("0.0", Ci), GUILayout.Width(52));
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.EndHorizontal();
            return v;
        }

        static string Status(SimCore s)
        {
            var st = s.State;
            return string.Format(Ci, "year {0:0.0} · {1} · pop {2} · {3} notables", st.Year, st.era, st.population, st.notables.Count(n => n.alive));
        }

        void NotablesGui(SimCore sim)
        {
            var alive = sim.State.notables.Where(n => n.alive).OrderBy(n => n.id).ToList();
            if (alive.Count == 0) { GUILayout.Label("nobody alive"); return; }
            int idx = alive.FindIndex(n => n.id == sel);
            if (idx < 0) { idx = 0; sel = alive[0].id; }
            var cur = alive[idx];
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(32))) sel = alive[(idx + alive.Count - 1) % alive.Count].id;
            GUILayout.Label($"{cur.FullName} · {cur.trait} · age {cur.age:0} · job {(string.IsNullOrEmpty(cur.job) ? "-" : cur.job)} · {sim.RoleOf(cur) ?? "no role"}");
            if (GUILayout.Button(">", GUILayout.Width(32))) sel = alive[(idx + 1) % alive.Count].id;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Set job: " + (string.IsNullOrEmpty(cur.job) ? "-" : cur.job) + " (next)"))
            {
                int j = System.Array.IndexOf(Jobs, cur.job);
                sim.DebugSetJob(cur, Jobs[(j + 1) % Jobs.Length]);
                last = Done($"{cur.FullName} → {cur.job}");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Redraw all jobs")) { sim.DebugRedrawJobs(); last = Done("jobs redrawn"); }
            GUILayout.Label("open: " + string.Join(",", sim.OpenJobs()));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Trait: " + cur.trait + " (next)"))
            {
                int t = System.Array.IndexOf(NameData.Traits, cur.trait);
                cur.trait = NameData.Traits[(t + 1) % NameData.Traits.Length];
                last = Done($"{cur.FullName} is now {cur.trait}");
            }
            // #39: the person card and the pin
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Card: selected")) last = PersonCardView.DebugOpen(cur.id);
            if (GUILayout.Button(sim.State.pinnedNotableId == cur.id ? "Unpin" : "Pin selected"))
                last = PersonCardView.DebugPin(sim.State.pinnedNotableId == cur.id ? -1 : cur.id);
            if (GUILayout.Button("Card: a dead one"))
            {
                var dead = sim.State.notables.LastOrDefault(n => !n.alive);
                last = dead != null ? PersonCardView.DebugOpen(dead.id) : "nobody has died yet";
            }
            if (GUILayout.Button("Card: next tab")) last = PersonCardView.DebugNextTab();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Birth")) last = Done(sim.DebugBirth() is Notable c ? "born: " + c.FullName : "no adult to bear");
            if (GUILayout.Button("Oldest dies")) last = Done(sim.DebugKillOldest() is Notable k ? "died: " + k.FullName : "nobody");
            GUILayout.EndHorizontal();
            // readout of everyone: name, trait, age, job, role
            var sb = new StringBuilder();
            foreach (var n in alive)
                sb.AppendLine($"{(n.id == sel ? "> " : "  ")}{n.FullName}, {n.trait}, {n.age:0}, {(string.IsNullOrEmpty(n.job) ? "-" : n.job)}{(sim.RoleOf(n) is string r ? ", " + r : "")}");
            GUILayout.Label(sb.ToString());
        }

        void RolesGui(SimCore sim)
        {
            sim.TermFlavourOn = GUILayout.Toggle(sim.TermFlavourOn, "Term flavour (trait stories) on");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill / hold elections")) { sim.DebugFillRoles(); last = Done("contests held"); }
            GUILayout.EndHorizontal();
            foreach (var role in Roles.Where(sim.RoleOpen))
            {
                var seat = sim.SeatOf(role);
                string holder = seat == null || seat.holderId < 0 ? "vacant" : Name(sim, seat.holderId);
                GUILayout.Label($"{role}: {holder}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Vacate")) last = Done(sim.DebugVacateRole(role) ? role + " vacated" : "not held");
                if (GUILayout.Button("Set = selected")) last = Done(sim.DebugSetRoleHolder(role, sim.FindNotable(sel)) ? role + " → " + Name(sim, sel) : "can't");
                if (GUILayout.Button("End term")) last = Done(sim.DebugEndTerm(role) ? "term ended" : "no term");
                if (GUILayout.Button("Story")) last = Done(sim.DebugTermStory(role)?.text ?? "no fresh term line");
                GUILayout.EndHorizontal();
            }
        }
    }
}
