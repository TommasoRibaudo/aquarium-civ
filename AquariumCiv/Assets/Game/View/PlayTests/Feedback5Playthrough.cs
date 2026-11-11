using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Playtest-feedback batch 5 played the way a phone plays it (1179x2556, living-land box, seed 4242). Real HUD taps and touches for every
    /// player action; cheats only reach a state and each logs "[f5] STATE CHEAT". Beats: a) the misted half against the control (#569);
    /// b) Kit, Life: locked seeds with their needs, the waits-for line (#570); c) the title "Year N · Season · P people", the Offices sheet, the
    /// chief's card (#571/#572/#576); d) no chopping beside the Keeper's tree, chopping elsewhere with logs carried to the stash (#573);
    /// e) a rabbit hunt seen and an egg gathered at the pond (#574); f) the travellers with no woods: words that name the land, wood carried,
    /// joining at a calm moment, the camp cleared (#578); g) rename a notable on the card, save and reload (#575); h) the four seasons (#576);
    /// i) the glass: dry land and the lit camp at night, the lid at two angles, a mist stroke, a tweezers pick and a Kit placement through
    /// the lid (#577). Each beat is in its own try/catch: a failure is a soft failure and the run goes on; the test asserts at the end that there
    /// are none. Shots: Temp/agent/fb5/play/*.png. Explicit.
    /// </summary>
    [Explicit, Category("Feedback5Playthrough")]
    public class Feedback5Playthrough : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static GameHud Hud => GameHud.Instance;
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();
        static PeopleView Pv => PeopleView.Find();
        readonly List<string> soft = new List<string>();
        readonly List<string> cheats = new List<string>();

        static void Log(string s) => Debug.Log("[f5] " + s);
        void Cheat(string what, string r) { cheats.Add(what); Debug.LogWarning($"[f5] STATE CHEAT {what}: {r}"); }
        void Soft(string beat, string why) { soft.Add($"{beat}: {why}"); Debug.LogWarning($"[f5] SOFT FAIL {beat}: {why}"); }
        void Check(string beat, bool ok, string what) { Log($"{(ok ? "OK  " : "FAIL")} {beat}: {what}"); if (!ok) soft.Add($"{beat}: {what}"); }

        /// <summary>A shot into Temp/agent/fb5/play/ (the folder is made first).</summary>
        IEnumerator Snap(string name)
        {
            System.IO.Directory.CreateDirectory(Shots + "fb5/play");
            yield return Shot("fb5/play/" + name);
        }

        /// <summary>Runs a beat, catching anything it throws as a soft failure so the next beat still runs.</summary>
        IEnumerator Beat(string name, IEnumerator body)
        {
            Log($"---- beat {name}");
            while (true)
            {
                object cur;
                try { if (!body.MoveNext()) break; cur = body.Current; }
                catch (Exception e) { Soft(name, e.GetType().Name + ": " + e.Message); break; }
                yield return cur;
            }
            var hud = Hud;
            if (Pv != null) Pv.timeScale = 1f;
            if (hud != null)
            {
                if (PersonCardView.Instance != null && PersonCardView.Instance.IsOpen) PersonCardView.Instance.Close();
                if (hud.SheetOpen) hud.CloseSheet();
                if (hud.Hand != GameHud.KitTool.None) hud.SetHand(GameHud.KitTool.None);
            }
            yield return Frames(5);
        }

        // ---------------------------------------------------------------- shared helpers
        static void DefaultView()
        {
            Rig.CancelFlight(); Rig.target = Box.transform.position; Rig.zoom = 1f; Rig.Apply();
        }

        static void Look(Vector3 world, float zoom, float pitch = 52f)
        {
            Rig.CancelFlight(); Rig.target = world; Rig.zoom = zoom; Rig.pitch = pitch; Rig.Apply();
        }

        static Vector3 World(float x, float z) => Box.transform.TransformPoint(Box.land.OnGround(x, z));

        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return e == null;
        }

        IEnumerator CalmHud()
        {
            var hud = Hud;
            yield return Until(() => hud.IsVisible && !(PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)), 30f);
            if (PersonCardView.Instance != null && PersonCardView.Instance.AnyOpen) { yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.93f)); yield return Frames(10); }
            if (hud.SheetOpen) { hud.CloseSheet(); yield return Frames(10); }
            if (hud.Hand != GameHud.KitTool.None || hud.CleanMode) { yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean")); yield return Frames(10); }
        }

        /// <summary>A tap on an element of the person card (its root has its own scale).</summary>
        IEnumerator TapEl(VisualElement e)
        {
            Assert.IsNotNull(e, "element exists");
            var c = e.worldBound.center;
            float k = Screen.width / GameUiPanel.ReferenceWidth;
            yield return TapAt(new Vector2(c.x * k, Screen.height - c.y * k));
        }

        static IEnumerator Settle(float s) { yield return new WaitForSecondsRealtime(s); }

        /// <summary>Lets the view clock run fast from <paramref name="from"/> for <paramref name="hours"/> view hours (at most <paramref name="maxSeconds"/>
        /// of real time), calling <paramref name="scan"/> every frame.</summary>
        IEnumerator RunHours(float from, float hours, float maxSeconds, Action scan)
        {
            var pv = Pv;
            pv.Planner.ClearLive();
            PeopleView.DebugSetHour(from);
            yield return Frames(3);
            pv.timeScale = 6f;
            var t0 = pv.Now; float real = 0f;
            while (real < maxSeconds)
            {
                real += Time.unscaledDeltaTime;
                float el = (pv.Now.day - t0.day) * 24f + pv.Now.hour - t0.hour;
                if (el >= hours) break;
                scan();
                yield return null;
            }
            pv.timeScale = 1f;
        }

        static int Count(Func<EcoCover, bool> pick, bool left)
        {
            var e = Sim.State.eco; int n = 0;
            for (int c = 0; c < EcoLand.N; c++)
                if ((SiteMap.CXc(c) < 0f) == left && pick((EcoCover)e.cover[c])) n++;
            return n;
        }

        // ---------------------------------------------------------------- a) pacing: the misted half against the control (#569)
        IEnumerator BeatPacing()
        {
            // the setup of Fb5PassShots.MistHalf_GreenerWithMoreYoungTrees: big mists (a seed) on both sides, a grown Keeper's tree as the seed source,
            // seedling pairs mirrored across the middle; the half-year after the run goes on to summer, since winter snow hides the green
            foreach (var (mx, mz) in new[] { (-1.6f, 0.6f), (1.6f, 0.9f) }) EcoDebug.MistBig(mx, mz);
            string planted = EcoDebug.PlantHere();
            Cheat("young box", "Keeper's tree: " + planted + "; " + EcoDebug.TreeStage(4) + "; seedling cells " + Fb5PassShots.PlantSeedlingPairs(8));
            PeopleView.DebugSetHour(10f);
            DefaultView();
            yield return Settle(1f);
            int y0 = Count(c => c == EcoCover.Young || c == EcoCover.Tree, true), c0 = Count(c => c == EcoCover.Young || c == EcoCover.Tree, false);
            Log($"a before: young+grown trees left half {y0}, right half {c0}");
            yield return Snap("a_0_before");
            Cheat("mist one half, skip 3 yr", EcoDebug.MistHalf(12)); // SliceDebugPanel's #569 button: the left half (x < 0) misted each season, the right is the control
            yield return Fb5SeasonShots.ToSeason(1, 2f);
            yield return Frames(30);
            DefaultView();
            yield return Settle(1.5f);
            int misted = Count(c => c == EcoCover.Young || c == EcoCover.Tree, true), control = Count(c => c == EcoCover.Young || c == EcoCover.Tree, false);
            int mGrass = Count(c => c == EcoCover.Grass || c == EcoCover.Tall, true), cGrass = Count(c => c == EcoCover.Grass || c == EcoCover.Tall, false);
            Log($"a after 3 yr: young+grown trees misted half {misted} vs control {control}; grass cells {mGrass} vs {cGrass}");
            Check("a", misted > control && mGrass > cGrass, $"the misted half holds at least as many young and grown trees ({misted} vs {control})");
            yield return Snap("a_1_misted_vs_control");
        }

        // ---------------------------------------------------------------- b) Kit, Life: locked seeds and the waits-for line (#570)
        IEnumerator BeatLockedSeeds()
        {
            var hud = Hud;
            Cheat("lock all jars", $"{Sim.DebugLockJars()} jars shut"); // SimDebugPanel's #570 button: a played box has opened them by now
            yield return CalmHud();
            yield return Until(() => !Rig.Leaning && !Rig.Gliding && !Rig.GlideArmed && !Rig.IsFlying, 30f);
            var shut = new List<LifeSpecies>(); Sim.LockedJars(shut);
            Check("b", shut.Count > 0, $"{shut.Count} jars are shut");
            if (shut.Count == 0) yield break;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean")); // Kit
            yield return Settle(0.6f);
            Check("b", hud.OpenSheet == GameHud.Kit, "Kit opens the kit");
            Check("b", Shown(hud.Root.Q("kit-life")), "the Life slot is there with every jar shut");
            yield return Snap("b_1_kit");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life"));
            yield return Settle(0.5f);
            Check("b", hud.LifeRowOpen, "the chooser opens on the shut jars");
            foreach (var s in shut)
            {
                var icon = hud.Root.Q("kit-life-locked-" + s.ToString().ToLowerInvariant());
                Check("b", icon != null && icon.ClassListContains("kit-life-choice--locked"), $"a greyed icon for {s}");
            }
            Log("b note: " + hud.LifeNoteText);
            yield return Snap("b_2_chooser");
            var pick = shut[shut.Count - 1];
            string need = Sim.JarNeed(pick);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-locked-" + pick.ToString().ToLowerInvariant()));
            yield return Settle(0.5f);
            Check("b", hud.LockedPick == pick, $"{pick} is picked");
            Check("b", need != null && hud.LifeNoteText.StartsWith(need), $"tapping a shut seed says its need: '{hud.LifeNoteText}'");
            Check("b", hud.Hand == GameHud.KitTool.None, "a shut jar is never taken in hand");
            string waits = Sim.VillageWaitsLine;
            Log("b village line: " + (string.IsNullOrEmpty(waits) ? "(none)" : waits));
            Check("b", string.IsNullOrEmpty(waits) || hud.LifeNoteText.Contains(waits), "the waits-for line names what is missing");
            yield return Snap("b_3_need");
            hud.CloseSheet();
            yield return Frames(10);
        }

        // ---------------------------------------------------------------- c) the title line, the Offices sheet, the chief's card (#571/#572/#576)
        IEnumerator BeatTitleAndChief()
        {
            var hud = Hud;
            yield return CalmHud();
            var year = hud.Root.panel.visualTree.Q<Label>(className: "hud-title-year");
            Check("c", year != null, "the title has a year line");
            string want = GameHud.TitleLine(Mathf.FloorToInt(Sim.State.Year), GameHud.SeasonWord(Sim.State), Sim.State.population);
            Log($"c title '{year?.text}' (expected '{want}')");
            Check("c", year != null && year.text == want, "the title reads Year N · Season · P people");
            Check("c", year != null && year.text.Contains("Year") && (year.text.Contains("people") || year.text.Contains("person")), "year, season and people are all there");
            yield return Snap("c_1_title");
            var chief = Sim.HolderOf(RoleType.Chief);
            Check("c", chief != null, "a camp has a chief");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-title"));
            yield return Settle(0.6f);
            Check("c", hud.OpenSheet == GameHud.Offices, "tapping the title opens the Offices sheet");
            yield return Snap("c_2_offices");
            if (chief == null) yield break;
            Check("c", hud.Root.Q("office-chief-holder") != null, "the chief's row has a tappable name");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "office-chief-holder"));
            yield return Settle(1.2f);
            Check("c", PersonCardView.Instance.IsOpen && PersonCardView.Instance.OpenId == chief.id, "the chief's card opens");
            yield return Snap("c_3_chief_card");
            PersonCardView.Instance.Close();
            yield return Frames(10);
        }

        // ---------------------------------------------------------------- d) the woods spot: no chopping beside the Keeper's tree (#573)
        IEnumerator BeatChopping()
        {
            var sim = Sim; var pv = Pv;
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge)) sim.DebugSetJob(n, JobType.Woodcutter);
            Cheat("woodcutters", "every grown-up takes the axe (DebugSetJob)");
            pv.Planner.Invalidate();
            if (sim.State.eco.treeCell < 0) Cheat("plant tree", EcoDebug.PlantHere());
            Cheat("wild trees beside Keeper's", EcoDebug.WildTrees(true)); // SliceDebugPanel's #573 button: a grown wild tree and a sapling next to the Keeper's tree
            int tc = sim.State.eco.treeCell;
            Check("d", tc >= 0, "the Keeper's tree stands");
            float kx = SiteMap.CXc(tc), kz = SiteMap.CZc(tc);
            float clear = (sim.Config.eco ?? new EcoTuning()).woodsClearRadius;
            int chopsNear = 0, chopsSeen = 0; float nearest = float.MaxValue;
            void Scan()
            {
                foreach (var a in pv.Activities)
                {
                    if (a.kind != ActivityKind.Chop || a.moving || !pv.TryGetDollPosition(a.who, out var p)) continue;
                    chopsSeen++;
                    float d = Vector2.Distance(p, new Vector2(kx, kz));
                    nearest = Mathf.Min(nearest, d);
                    if (d < clear - 0.25f) chopsNear++; // the rule clears the TREE cell by woodsClearRadius; the chopper stands about a quarter unit off the tree, toward the camp
                }
            }
            Look(World(kx, kz), 0.35f);
            yield return RunHours(8f, 8f, 70f, Scan);
            Log($"d beside: {chopsSeen} chop frames seen, {chopsNear} within {clear:0.0} u of the Keeper's tree (nearest {(nearest == float.MaxValue ? "none" : nearest.ToString("0.00"))})");
            Check("d", chopsNear == 0, "nobody chops beside the Keeper's tree");
            yield return Snap("d_1_keeper_tree_quiet");
            // a grown wild tree away from it: chopping with chips and logs, then the carry to the stash
            Cheat("wild tree away", EcoDebug.WildTrees(false)); // the #573 button: a grown wild tree away from the Keeper's tree
            pv.Planner.Invalidate();
            chopsSeen = chopsNear = 0; nearest = float.MaxValue;
            bool carried = false, shotChop = false, shotLogs = false, shotCarry = false;
            int logs = 0;
            var logsView = ChopLogsView.Ensure();
            IEnumerator Watch()
            {
                // runs the clock in short slices so a shot can be taken the moment each thing is seen
                for (int slice = 0; slice < 8 && !(shotChop && shotLogs && shotCarry); slice++)
                {
                    yield return RunHours(7f + slice * 1.5f, 1.5f, 25f, () =>
                    {
                        Scan();
                        logs = Mathf.Max(logs, logsView != null ? logsView.LogsOnGround : 0);
                        foreach (var a in pv.Activities) if (a.moving && a.prop == Prop.Log) carried = true;
                    });
                    foreach (var a in pv.Activities)
                    {
                        if (!pv.TryGetDollPosition(a.who, out var p)) continue;
                        if (a.kind == ActivityKind.Chop && !a.moving && !shotChop) { Look(World(p.x, p.y), 0.18f, 45f); yield return Settle(0.4f); yield return Snap("d_2_chopping_chips"); shotChop = true; break; }
                        if (a.moving && a.prop == Prop.Log && !shotCarry) { Look(World(p.x, p.y), 0.25f, 45f); yield return Settle(0.4f); yield return Snap("d_4_log_carried"); shotCarry = true; break; }
                    }
                    if (!shotLogs && logsView != null && logsView.LogsOnGround > 0)
                    {
                        var t = pv.Activities.Where(a => a.kind == ActivityKind.Chop).Select(a => a.who).FirstOrDefault();
                        if (pv.TryGetDollPosition(t, out var p)) { Look(World(p.x, p.y), 0.18f, 45f); yield return Settle(0.3f); yield return Snap("d_3_logs_on_ground"); shotLogs = true; }
                    }
                }
            }
            yield return Watch();
            Log($"d away: {chopsSeen} chop frames seen, most logs on the ground at once {logs}, hits seen {(logsView != null ? logsView.HitsSeen : -1)}, a log carried {carried}");
            Check("d", chopsSeen > 0, "chopping happens at the grown wild tree away from the Keeper's");
            Check("d", logs > 0 || (logsView != null && logsView.HitsSeen > 0), "chips and logs show");
            Check("d", carried, "a log is carried to the stash");
            DefaultView();
        }

        // ---------------------------------------------------------------- e) a hunt seen, an egg gathered (#574)
        IEnumerator BeatHuntAndEgg()
        {
            var sim = Sim; var pv = Pv;
            Cheat("rabbits", EcoDebug.AddRabbits(5));
            Cheat("make a hunter", EcoDebug.MakeHunter()); // SliceDebugPanel's #574 button
            PeopleView.DebugSetHour(10f);
            pv.Planner.ClearLive();
            yield return Frames(10);
            Cheat("start a hunt", EcoDebug.StartHunt());
            bool stalk = false; Vector2 at = default;
            yield return Until(() =>
            {
                foreach (var a in pv.Activities)
                    if (a.kind == ActivityKind.Stalk && pv.TryGetDollPosition(a.who, out at)) { stalk = true; return true; }
                return false;
            }, 90f);
            Check("e", stalk, "a hunter stalks a rabbit, seen");
            if (stalk)
            {
                Look(World(at.x, at.y), 0.22f, 45f);
                yield return Settle(1.5f);
                yield return Snap("e_1_hunt_stalk");
                yield return Settle(4f);
                foreach (var a in pv.Activities) if (a.who.Key != 0 && pv.TryGetDollPosition(a.who, out var p) && Vector2.Distance(p, at) < 1.5f) { at = p; break; }
                Look(World(at.x, at.y), 0.22f, 45f);
                yield return Snap("e_2_hunt_later");
            }
            // the egg gather: ducks on the pond first
            if (sim.EcoPondCells == 0)
            {
                foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
                Cheat("pond", "five big mists; " + EcoDebug.Table(0.3f));
                sim.DebugGrowFarWood();
                yield return Frames(20);
            }
            if (!sim.LifeIsPlaced(LifeSpecies.Ducks))
            {
                if (!sim.JarReady(LifeSpecies.Ducks)) sim.DebugJarsReady();
                Cheat("ducks placed", $"near the camp ({sim.DebugPlaceNearCamp(LifeSpecies.Ducks)})");
            }
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            PeopleView.DebugSetHour(10f);
            pv.Planner.ClearLive();
            yield return Frames(10);
            Cheat("gather an egg now", EcoDebug.GatherEggNow()); // SliceDebugPanel's #574 button
            bool basket = false; Vector2 gp = default;
            yield return Until(() =>
            {
                foreach (var a in pv.Activities)
                    if (a.kind == ActivityKind.Gather && a.prop == Prop.Basket && !a.moving && pv.TryGetDollPosition(a.who, out gp)) { basket = true; return true; }
                return false;
            }, 90f);
            Check("e", basket, "a gatherer stands at the pond edge with a basket");
            if (basket)
            {
                Look(World(gp.x, gp.y), 0.2f, 45f);
                yield return Settle(1.2f);
                yield return Snap("e_3_egg_gatherer");
            }
            DefaultView();
        }

        // ---------------------------------------------------------------- f) the travellers with no woods (#578)
        static int ApartRoots() => Resources.FindObjectsOfTypeAll<Transform>().Count(t => t != null && t.name.StartsWith("ApartCamp") && t.gameObject.scene.IsValid());

        static string CardText(CardStackView v)
        {
            var sheet = v.Root.Q("cards-sheet");
            return string.Join(" | ", sheet.Query<Label>().ToList().Where(l => Shown(l) && !string.IsNullOrEmpty(l.text)).Select(l => l.text));
        }

        IEnumerator BeatTravellers()
        {
            var sim = Sim;
            yield return CalmHud();
            sim.DebugLand = "meadow";
            Cheat("no woods", "the question's land forced to the meadow (SimCore.DebugLand)");
            Dilemma d = null;
            for (int i = 0; i < 8 && d == null; i++)
            {
                Log("f raise: " + DilemmaCheats.Raise("camp_travellers"));
                d = sim.OpenDilemma != null && sim.OpenDilemma.defId == "camp_travellers" ? sim.OpenDilemma : null;
                if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; }
            }
            sim.DebugLand = "";
            Check("f", d != null, "camp_travellers is asked");
            if (d == null) yield break;
            Cheat("travellers question raised", $"{d.defId}, land '{d.land}'");
            var view = CardStackView.Instance;
            // open the card by touch: the peek, then next until the question
            if (view.Model.Mode == CardStackMode.Peek) { yield return TapAt(GameUiPanel.ScreenCenter(view.Root.Q("cards-peek"))); yield return Frames(6); }
            for (int g = 0; g < 8 && !(view.Model.Mode == CardStackMode.Open && view.Model.Face.kind == CardKind.Dilemma); g++)
            {
                if (view.Model.Mode != CardStackMode.Open) break;
                yield return TapAt(GameUiPanel.ScreenCenter(view.Root.Q("cards-next")));
                yield return Frames(6);
            }
            yield return Settle(0.8f);
            string text = view.Model.Mode == CardStackMode.Open ? CardText(view) : "";
            Log("f card text: " + text);
            string word = d.land == "pond" ? "pond" : d.land == "meadow" ? "meadow" : "woods";
            Check("f", text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0, $"the question's words name the {word}");
            Check("f", d.land != "woods" && d.land != "", $"with no woods the land is not the woods ('{d.land}')");
            yield return Snap("f_1_question");
            // "full packs" by touch
            yield return TapAt(GameUiPanel.ScreenCenter(view.Root.Q("cards-dil-answer-1")));
            yield return Frames(10);
            var g1 = sim.ApartOf(d);
            Check("f", g1 != null && g1.joins, "full packs keeps them apart, and they will join at the village");
            yield return Settle(0.8f);
            yield return Snap("f_2_answered");
            if (view.Model.Mode == CardStackMode.Open) view.Model.Close();
            yield return Frames(10);
            if (g1 == null) yield break;
            int id = g1.id;
            PeopleView.DebugSetHour(10f);
            Rig.GoHome(0.01f, true);
            Cheat("travellers bring wood now", ApartGroupsView.DebugWoodNow()); // SliceDebugPanel's #578 button
            ApartGroupsView.DebugFly(id, 0.4f);
            int loads0 = g1.woodLoads;
            yield return Settle(1.0f);
            yield return Snap("f_3_carry_lift");
            yield return Until(() => g1.woodLoads > loads0, 25f);
            Check("f", g1.woodLoads > loads0, "the load reached their pile");
            yield return Snap("f_4_carry_pile");
            int roots0 = ApartRoots();
            Check("f", roots0 > 0, "their camp is drawn before they go");
            Cheat("travellers join now", ApartGroupsView.DebugJoinNow()); // SliceDebugPanel's #578 button
            Rig.GoHome(0.01f, true);
            yield return Until(() => g1.joinState == 2, 30f);
            Check("f", g1.joinState == 2, "at a calm moment they pack up and walk");
            yield return Settle(1.5f);
            yield return Snap("f_5_walk");
            yield return Until(() => !sim.ApartGroups.Any(x => x.id == id), 60f);
            Check("f", !sim.ApartGroups.Any(x => x.id == id), "they have walked in: the camp group is gone");
            yield return Settle(1.0f);
            int left = ApartRoots();
            Log($"f camp roots named ApartCamp*: {roots0} before, {left} after");
            Check("f", left == 0, "no ApartCamp* root left");
            yield return Snap("f_6_cleared");
        }

        // ---------------------------------------------------------------- g) rename on the card, then save and reload (#575)
        IEnumerator BeatRename()
        {
            var sim = Sim; var cards = PersonCardView.Instance;
            var n = sim.State.notables.First(x => x.alive && x.age >= 8f);
            string was = n.name; int id = n.id; string family = n.family;
            Check("g", cards.Open(id, false), "the card opens");
            yield return Frames(10);
            var btn = cards.CardRoot.Q("person-card-rename");
            Check("g", btn != null, "the About tab has the rename button");
            yield return TapEl(btn);
            yield return Frames(10);
            Check("g", cards.RenameOpen, "the field opens");
            yield return Snap("g_1_field");
            cards.RenameField.value = "Wren";
            yield return TapEl(cards.CardRoot.Q("person-card-rename-keep"));
            yield return Frames(10);
            Check("g", sim.FindNotable(id).name == "Wren", $"the sim holds the new name (was {was})");
            var label = cards.CardRoot.Q<Label>("person-card-name");
            Check("g", label != null && label.text.Contains("Wren"), "the card's label reads the new name");
            Cheat("a new chronicle line", "written with SimCore.Chronicle naming the person"); // the line a later event would write
            int before = sim.State.chronicle.Count;
            sim.Chronicle("fb5_rename", $"{sim.FindNotable(id).FullName} mended the roof.", 0.5f, id);
            Check("g", sim.State.chronicle.Count == before + 1 && sim.State.chronicle.Last().text.Contains("Wren"), "a new chronicle line names Wren");
            yield return Snap("g_2_renamed");
            cards.Close();
            yield return Frames(5);
            // save and reload (the way PersonCardPlayTests does): the name holds
            Check("g", GameClock.Instance.Save(), "the box saves");
            yield return OpenSlice(1179, 2556, freshSave: false);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return Settle(0.8f);
            PeopleView.DebugSetHour(10f);
            var n2 = Sim.FindNotable(id);
            Check("g", n2 != null && n2.name == "Wren", $"after the reload the name holds ('{n2?.name}')");
            Check("g", Sim.State.chronicle.Any(e => e.text.Contains("Wren " + family)), "and the chronicle line");
            Check("g", PersonCardView.Instance.Open(id, false), "the card opens again");
            yield return Frames(8);
            Check("g", PersonCardView.Instance.CardRoot.Q<Label>("person-card-name").text.Contains("Wren"), "and reads Wren");
            yield return Snap("g_3_after_reload");
            PersonCardView.Instance.Close();
        }

        // ---------------------------------------------------------------- h) the four seasons (#576)
        IEnumerator BeatSeasons()
        {
            PeopleView.DebugSetHour(11f);
            DefaultView();
            var year = Hud.Root.panel.visualTree.Q<Label>(className: "hud-title-year");
            var seen = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                Cheat("skip to next season", EcoDebug.SkipToNextSeason()); // SliceDebugPanel's #576 button
                PeopleView.DebugSetHour(11f);
                DefaultView();
                yield return Settle(1.5f);
                string w = GameHud.SeasonWord(Sim.State);
                seen.Add(w);
                Check("h", year != null && year.text.Contains(w), $"the title shows {w} ('{year?.text}')");
                yield return Snap($"h_{i + 1}_{w.ToLowerInvariant()}");
            }
            Check("h", seen.Distinct().Count() == 4, "four seasons in four skips: " + string.Join(", ", seen));
        }

        // ---------------------------------------------------------------- i) the glass and the lid (#577)
        static int PaneRenderers() => Resources.FindObjectsOfTypeAll<MeshRenderer>()
            .Count(r => r != null && r.gameObject.scene.IsValid() && r.gameObject.activeInHierarchy && (r.name.StartsWith("Glass") || r.name == "Lid"));

        static bool LidInTheWay(Vector2 p) => Box.Lid != null && Box.Lid.bounds.IntersectRay(Camera.main.ScreenPointToRay(p));

        IEnumerator BeatGlass()
        {
            var rig = Rig; var hud = Hud;
            int panes = PaneRenderers();
            Check("i", panes == 5, $"four sides and a lid ({panes})");
            // dry land close up, midday, hidden near panes
            PeopleView.DebugSetHour(12f);
            rig.EaseTo(rig.target, 28f, 34f, 1f, 0.01f);
            yield return Settle(0.6f);
            rig.FlyToPlace(Place.Land(PlaceKind.Fire), 0.4f, 0.01f);
            yield return Settle(1.4f);
            yield return Snap("i_1_dry_close");
            // the lit camp at night
            PeopleView.DebugSetHour(21.5f);
            yield return Settle(2.5f);
            yield return Snap("i_2_night_camp");
            rig.FlyToPlace(Place.Land(PlaceKind.Fire), 0.7f, 0.01f);
            yield return Settle(1.4f);
            yield return Snap("i_3_night_mid");
            PeopleView.DebugSetHour(10f);
            // the lid at the default view, then top-down
            rig.CancelFlight(); rig.EaseTo(Box.transform.position, 28f, 30f, 1f, 0.01f);
            yield return Settle(1.6f);
            Check("i", Box.LidOpacity > 0.9f, $"at the default view the lid is there ({Box.LidOpacity:0.00})");
            yield return Snap("i_4_lid_default");
            rig.EaseTo(rig.target, rig.yaw, 68f, 1f, 0.01f);
            yield return Settle(1.6f);
            Check("i", Box.LidOpacity < 0.35f, $"top-down the lid has faded ({Box.LidOpacity:0.00})");
            yield return Snap("i_5_lid_topdown");
            rig.EaseTo(rig.target, rig.yaw, 30f, 1f, 0.01f);
            yield return Settle(1.6f);
            // a mist stroke through the lid
            yield return CalmHud();
            var mist = EcoMistView.Instance;
            Vector2 at = default; bool found = false;
            for (float fy = 0.25f; fy < 0.75f && !found; fy += 0.05f)
                for (float fx = 0.15f; fx < 0.85f && !found; fx += 0.05f)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (!UiInput.Over(p) && mist.GroundPoint(p, out _) && LidInTheWay(p)) { at = p; found = true; }
                }
            Check("i", found, "ground to mist under the lid on screen");
            if (found)
            {
                int pulses0 = mist.MistPulses;
                int id = touchId++;
                BeginTouch(id, at, queueEventOnly: true);
                yield return null;
                float t0 = Time.unscaledTime; bool sawMisting = false;
                while (Time.unscaledTime - t0 < 2.2f)
                {
                    MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true);
                    sawMisting |= mist.Misting;
                    yield return null;
                }
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                yield return Frames(10);
                Check("i", sawMisting && mist.MistPulses > pulses0, "a still finger under the lid turned into a mist and the sim was told");
                yield return Snap("i_6_mist_through_lid");
            }
            // a tweezers pick through the lid
            var care = CareView.Instance;
            Sim.CleanAll();
            Cheat("a weed", "SimCore.AddMess puts a weed in view");
            for (int wi = 0; wi < 6; wi++) Sim.AddMess(0f, 0.4f); // weeds land at random spots: one far enough back has its ray cross the lid
            yield return Frames(60);
            DefaultView();
            yield return Frames(10);
            var weeds = care.Props.AllPieces.Where(p => !p.leaving && p.kind == MessProps.Kind.Weed && OnScreen(Camera.main.WorldToScreenPoint(p.Center))).ToList();
            var weed = weeds.FirstOrDefault(p => LidInTheWay(Camera.main.WorldToScreenPoint(p.Center))) ?? weeds.FirstOrDefault();
            Check("i", weed != null, "a weed in view");
            if (weed != null)
            {
                var s = Camera.main.WorldToScreenPoint(weed.Center);
                Check("i", LidInTheWay(s), "the lid is between the finger and the weed");
                hud.SetHand(GameHud.KitTool.Tweezers);
                yield return null;
                float og0 = Sim.State.overgrowth;
                yield return TapAt(s);
                Check("i", care.LastPluck == "weed" && Sim.State.overgrowth < og0 - 0.05f, "the tweezers took the weed out through the lid");
                hud.SetHand(GameHud.KitTool.None);
                yield return Snap("i_7_tweezers_through_lid");
            }
            // a Kit placement through the lid
            Cheat("jars ready", $"{Sim.DebugJarsReady()}");
            yield return Frames(10);
            LifeSpecies? spec = null;
            foreach (var s in new[] { LifeSpecies.Thicket, LifeSpecies.Deer, LifeSpecies.Rabbits, LifeSpecies.Fish })
                if (Sim.JarReady(s) && !Sim.LifeIsPlaced(s)) { spec = s; break; }
            Check("i", spec.HasValue, "a ready jar to place");
            if (spec.HasValue)
            {
                DefaultView();
                yield return Settle(0.8f);
                Check("i", Box.LidOpacity > 0.9f, "the lid is on while placing");
                yield return KeeperPlaces(spec.Value, "fb5/play/i_8_place");
                Check("i", LastPlaced, $"{spec.Value} placed by touch through the lid");
            }
            yield return Frames(20);
            int after = PaneRenderers();
            Check("i", after == panes, $"the pane count is stable ({panes} before, {after} after)");
            DefaultView();
        }

        static bool OnScreen(Vector3 s) => s.x > 40 && s.y > 200 && s.x < Screen.width - 40 && s.y < Screen.height - 300 && s.z > 0;

        // ---------------------------------------------------------------- the run
        [UnityTest, Timeout(3600000)]
        public IEnumerator Feedback5_PlayedByTouch()
        {
            UseLivingLand(true);
            ShowMeDirector.BubblesOn = false;
            try
            {
                yield return OpenSlice(1179, 2556, seed: 4242);
                GameUiPanel.AcceptInputUnfocused(true);
                Assert.IsTrue(Sim.EcologyOn, "a living-land box");
                PeopleView.DebugSetHour(10f);
                yield return Settle(2f);
                DefaultView();
                yield return Frames(10);

                // a) first, on the fresh meadow, so the two halves start level
                yield return Beat("a pacing", BeatPacing());
                Cheat("first run skipped + 50 seasons", EcoDebug.SkipSeasons(50)); // a pass-sized run can't reach a camp by time alone (tooling 2026-11-09)
                if (Sim.State.era != Era.Camp) Cheat("camp gate", EcoDebug.CampGate());
                Log($"era {Sim.State.era}, year {Sim.State.Year:0.0}, buildings {Sim.State.buildings.Count}");
                PeopleView.DebugSetHour(10f);
                yield return Settle(2f);
                DefaultView();
                yield return Frames(10);

                yield return Beat("b locked seeds", BeatLockedSeeds());
                yield return Beat("c title and chief", BeatTitleAndChief());
                yield return Beat("d chopping", BeatChopping());
                yield return Beat("e hunt and egg", BeatHuntAndEgg());
                yield return Beat("f travellers", BeatTravellers());
                yield return Beat("g rename", BeatRename());
                yield return Beat("h seasons", BeatSeasons());
                yield return Beat("i glass and lid", BeatGlass());

                Log("RESULT state cheats: " + string.Join("; ", cheats));
                Log("RESULT soft failures (" + soft.Count + "): " + string.Join(" || ", soft));
                Assert.AreEqual(0, soft.Count, "soft failures: " + string.Join(" || ", soft));
            }
            finally
            {
                ShowMeDirector.BubblesOn = true;
                var p = PeopleView.Find();
                if (p != null) p.timeScale = 1f;
                if (GameClock.Instance != null && GameClock.Instance.ActiveSim != null) GameClock.Instance.ActiveSim.DebugLand = "";
                UseLivingLand(false);
            }
        }
    }
}
