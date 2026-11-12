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
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The stops of feedback batch 5 (issues #569-#578) for the sign-off walk: each sets its state up with the cheats of that issue and frames the
    /// camera, then asks one question. The live ones (the chopping, the hunt, the egg gather, the log carry, the joining) are re-run with
    /// "Set up again". The text of the section is in docs/open-questions.md ("Feedback 5 (build 25 notes)").
    /// </summary>
    public partial class OpenQuestionsSignOff
    {
        /// <summary>The stops of one issue of the batch (the key is "#569" ... "#578").</summary>
        Stop[] Fb5(string key)
        {
            switch (key)
            {
                case "#569": return new[] {
                    S("#569", "pacing: before and after", "pacing_table",
                        "The mister's water now makes trees grow and seed where it falls. The village used to come at year 35 for the active player; now year 25.\n" +
                        "Active player (six visits a day): 35.0 years before, 25.1 after.   The user's habit (every 4.5 h): 40.8 before, 31.0 after.   Never mists: not in 60 years, unchanged.\n" +
                        "Below: the counts of young and grown trees per half on a young box, left half misted for 3 years, right half the control. Is that the pace you wanted?", SetupPacingTable),
                    S("#569", "the misted half against the control", "pacing_halves",
                        "Left half: misted every season for 3 years. Right half: nothing. Does the misted half look greener and denser, enough to see at a glance?", SetupPacingHalves) };
                case "#570": return new[] { S("#570", "locked seeds and the village line", "locked_seeds",
                    "Every seed jar is shut (cheat) and the Kit is open. Tap Life: the seeds that are not ready show greyed. Tap a greyed one: it says what it needs. The line under it says what the camp waits for. Does it tell you what to do next?", SetupLockedSeeds) };
                case "#571": return new[] { S("#571", "population on the title", "title_line",
                    "The title line at the top. It should read \"Year N · Season · P people\" and fit beside the round buttons. Is it clear and in the right place?", SetupTitleLine) };
                case "#572": return new[] { S("#572", "the chief's office", "chief_office",
                    "The Offices sheet is open. Tap the chief's name: the chief's card should open. Does that feel like \"tap the office, get the chief\"?", SetupOffices) };
                case "#573": return new[] {
                    S("#573", "no chopping by the Keeper's tree", "chop_beside",
                        "A grown wild tree stands right beside the Keeper's tree, and everyone has an axe. The view runs fast (x4) for about a minute. Nobody should chop at the Keeper's tree. Is it left alone? Use Set up again to watch twice.", () => SetupChop(0)),
                    S("#573", "chopping with chips and logs", "chop_logs",
                        "A grown wild tree stands away from the Keeper's tree. A chopper swings, chips fly, and a log lies on the ground every few hits. Does it read as chopping wood? (Set up again to watch twice.)", () => SetupChop(1)),
                    S("#573", "the log carried to the stash", "chop_carry",
                        "The same woodcutting, followed to the end: a chopper lifts a log and walks it to the stash pile. Does the wood get from the tree to the pile where you can see it? (Set up again.)", () => SetupChop(2)) };
                case "#574": return new[] {
                    S("#574", "a rabbit hunt", "hunt",
                        "Five rabbits and a hunter (cheat), the hunt started now. Watch the stalk, the throw and the catch. Is it a hunt you can follow? (Set up again to watch twice.)", SetupHunt),
                    S("#574", "the egg gather", "egg_gather",
                        "Ducks on the pond with eggs in the nests (cheat). A grown-up walks to the pond edge with a basket and takes an egg to the pile. Does it read, and is it clear what they are doing? (Set up again.)", SetupEggGather) };
                case "#575": return new[] { S("#575", "rename on the card", "rename",
                    "A person's card is open on the About tab with the rename field showing. Type a new first name and tap Keep. The name should change on the card at once and stay. Does it feel natural?", SetupRename) };
                case "#576": return new[] {
                    S("#576", "spring", "season_spring", "Spring. Does the box read as spring at a glance, apart from summer? The title shows the season word.", () => SetupSeason(0)),
                    S("#576", "summer", "season_summer", "Summer. Does it read as summer, deeper green than spring?", () => SetupSeason(1)),
                    S("#576", "autumn", "season_autumn", "Autumn. Do the leaves and the grass read as autumn?", () => SetupSeason(2)),
                    S("#576", "winter", "season_winter", "Winter: light snow on the flat ground. Does it read as winter without hiding the land?", () => SetupSeason(3)) };
                case "#577": return new[] {
                    S("#577", "glass: dry land close", "glass_dry", "Close on dry land at midday, the near panes hidden. There should be no dark outline or line across the ground. Is the glass line gone?", () => SetupGlass(12f, 0.4f)),
                    S("#577", "glass: the lit camp at night", "glass_night", "The lit camp at night, close. No outline from the hidden panes around the firelight. Is it clean?", () => SetupGlass(21.5f, 0.55f)),
                    S("#577", "the lid at the default view", "lid_default", "The whole box from the default camera, with the glass lid on top. A faint haze over the land, nothing in the way of your finger. Do you like the lid's look?", () => SetupLid(30f)),
                    S("#577", "the lid, top-down", "lid_topdown", "The camera looks straight down (pitch about 68 degrees): the lid should fade out. Does it get out of the way?", () => SetupLid(68f)) };
                case "#578": return new[] {
                    S("#578", "the travellers' words (no woods)", "trav_words",
                        "No woods in this box (cheat), the travellers come. Read the card: the words should name the meadow, not the woods. Answer with the second way (full packs) if you like. Do the words fit the land?", SetupTravellerWords),
                    S("#578", "the travellers' wood is carried", "trav_wood",
                        "Their camp stands apart. Their cutter walks a log to their pile. Is the wood reward something you can see? (Set up again.)", SetupTravellerWood),
                    S("#578", "the travellers join", "trav_join",
                        "The village is made: they pack up, walk in to the green and join, and their camp is gone. It waits for a calm moment. Does it read, and is the camp cleared? (Set up again.)", SetupTravellerJoin) };
            }
            return new Stop[0];
        }

        // ---------------------------------------------------------------- small helpers
        void StateCheat(string what, string result) => Log("[f5] STATE CHEAT " + what + ": " + result);

        static int TreesIn(bool left)
        {
            var e = Sim.State.eco; int n = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                var cv = (EcoCover)e.cover[c];
                if ((SiteMap.CXc(c) < 0f) == left && (cv == EcoCover.Young || cv == EcoCover.Tree)) n++;
            }
            return n;
        }

        bool youngBox;      // the world is the young pacing box, not the played one
        bool mistedHalf;    // the young box has had its half misted
        string halfCounts = "";

        /// <summary>Before every stop: the person card, a sheet and a Kit tool left over from the stop before must not cover this one's frame.</summary>
        IEnumerator CloseLooseUi()
        {
            var h = GameHud.Instance;
            if (PersonCardView.Instance != null && PersonCardView.Instance.IsOpen) PersonCardView.Instance.Close();
            if (h != null)
            {
                if (h.SheetOpen) h.CloseSheet();
                if (h.Hand != GameHud.KitTool.None) h.SetHand(GameHud.KitTool.None);
            }
            yield return Frames(8);
        }

        /// <summary>Every stop but the season ones and the pacing pair shows the land in summer (no snow on dry land, leaves on the trees); the season stops set their own.</summary>
        IEnumerator ResetSeason()
        {
            for (int k = 0; k < 4 && LifeClock.SeasonOf(Sim.State.month) != Season.Summer; k++) EcoDebug.SkipToNextSeason();
            yield return Frames(4);
        }

        /// <summary>The camera on a subject with no shift (it sits mid-screen: below the sign-off panel, above the Kit button), following the first activity that matches.</summary>
        IEnumerator Follow(Func<PersonActivity, bool> match, float zoom, float pitch, float seconds)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < seconds)
            {
                if (Seen(match, out var at)) { var w = World(at.x, at.y); Rig.CancelFlight(); Rig.pitch = pitch; Rig.zoom = zoom; Rig.target = w; Rig.Apply(); }
                yield return null;
            }
        }

        /// <summary>#574: the nearest open-water cell to a sim point, in units (the egg gatherer must kneel at the pond's edge).</summary>
        static float WaterDist(Vector2 p)
        {
            float wd = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
                if (Sim.EcoCoverAt(c) == EcoCover.Water) wd = Mathf.Min(wd, Vector2.Distance(new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)), p));
            return wd;
        }

        /// <summary>A person kneeling with a basket within 0.8 u of the water: the egg gatherer (a berry gatherer kneels with a basket too, in the meadow).</summary>
        bool EggKneel(out Vector2 at)
        {
            at = default;
            foreach (var a in pv.Activities)
                if (a.kind == ActivityKind.Gather && a.prop == Prop.Basket && !a.moving && pv.TryGetDollPosition(a.who, out var p) && WaterDist(p) < 0.8f) { at = p; return true; }
            return false;
        }

        void Rebind()
        {
            hud = GameHud.Instance;
            box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            pv = PeopleView.Find();
        }

        /// <summary>The played box every other stop expects: a fresh box with 50 seasons gone and the camp gate open (what the walk starts with).</summary>
        IEnumerator RebuildPlayedBox()
        {
            Say("putting the played box back...");
            yield return Open(1179, 2556, 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Rebind();
            Log("skipped seasons: " + EcoDebug.SkipSeasons(50));
            if (Sim.State.era != Era.Camp) Log("camp gate: " + EcoDebug.CampGate());
            youngBox = false; mistedHalf = false;
            Hour(10f);
            yield return Wait(2f);
            DefaultView();
            yield return Frames(10);
        }

        /// <summary>#569: a YOUNG box (fresh, the Keeper's tree planted, a few seedlings), then the left half misted for 12 seasons: the control is visibly sparser.</summary>
        IEnumerator MistTheHalfOnce()
        {
            if (mistedHalf) yield break;
            Say("a young box for the comparison...");
            yield return Open(1179, 2556, 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Rebind();
            youngBox = true;
            // the setup that works (Fb5PassShots.MistHalf_GreenerWithMoreYoungTrees): a seed first (big mists both sides), a grown Keeper's tree as the
            // seed source, seedling pairs mirrored across the middle; then the half run and the half-year on to summer (snow would hide the green)
            foreach (var (mx, mz) in new[] { (-1.6f, 0.6f), (1.6f, 0.9f) }) EcoDebug.MistBig(mx, mz);
            string planted = EcoDebug.PlantHere();
            StateCheat("young box", "fresh box; Keeper's tree: " + planted + "; " + EcoDebug.TreeStage(4) + "; seedling cells " + Fb5PassShots.PlantSeedlingPairs(8));
            Hour(10f);
            yield return Frames(20);
            int bl = TreesIn(true), br = TreesIn(false);
            StateCheat("mist one half, skip 3 yr", EcoDebug.MistHalf(12));
            mistedHalf = true;
            yield return Fb5SeasonShots.ToSeason(1, 2f);
            yield return Frames(30);
            halfCounts = $"misted half: {TreesIn(true)} young/grown trees (was {bl}) · control half: {TreesIn(false)} (was {br})";
            Log("pacing: " + halfCounts);
        }

        IEnumerator SetupPacingTable()
        {
            yield return MistTheHalfOnce();
            Hour(10f);
            DefaultView();
            Say(halfCounts);
            yield return Wait(1f);
        }

        IEnumerator SetupPacingHalves()
        {
            yield return MistTheHalfOnce();
            Hour(10f);
            Look(box.transform.position, 1f, 58f);
            Say(halfCounts);
            yield return Wait(1f);
        }

        IEnumerator SetupLockedSeeds()
        {
            Hour(10f);
            StateCheat("lock all jars", Sim.DebugLockJars() + " jars shut");
            DefaultView();
            yield return Frames(10);
            hud.Open(GameHud.Kit);
            yield return Wait(0.5f);
            Say("Kit is open: tap Life, then a greyed seed. Village line: " + (string.IsNullOrEmpty(Sim.VillageWaitsLine) ? "(none)" : Sim.VillageWaitsLine));
        }

        IEnumerator SetupTitleLine()
        {
            Hour(10f);
            DefaultView(0f);
            yield return Frames(10);
            var year = hud.Root.panel.visualTree.Q<UnityEngine.UIElements.Label>(className: "hud-title-year");
            Say("the title reads: " + (year != null ? year.text : "(not found)"));
            yield return Wait(0.5f);
        }

        IEnumerator SetupOffices()
        {
            Hour(10f);
            DefaultView();
            yield return Frames(10);
            hud.Open(GameHud.Offices);
            yield return Wait(0.6f);
            var chief = Sim.HolderOf(RoleType.Chief);
            Say("the chief: " + (chief != null ? chief.FullName : "(vacant)"));
        }

        // ---------------------------------------------------------------- #573 chopping
        bool Seen(Func<PersonActivity, bool> match, out Vector2 at)
        {
            at = default;
            foreach (var a in pv.Activities)
                if (match(a) && pv.TryGetDollPosition(a.who, out at)) return true;
            return false;
        }

        IEnumerator SetupChop(int mode)
        {
            Hour(8f);
            foreach (var n in Sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < Sim.Config.retireAge)) Sim.DebugSetJob(n, JobType.Woodcutter);
            StateCheat("woodcutters", "every grown-up takes the axe");
            if (Sim.State.eco.treeCell < 0) StateCheat("plant tree", EcoDebug.PlantHere());
            StateCheat(mode == 0 ? "wild trees beside Keeper's" : "wild tree away", EcoDebug.WildTrees(mode == 0));
            pv.Planner.Invalidate();
            int tc = Sim.State.eco.treeCell;
            if (tc < 0) { Say("setup failed: no Keeper's tree"); DefaultView(); yield break; }
            var keeper = World(SiteMap.CXc(tc), SiteMap.CZc(tc));
            pv.timeScale = 4f;
            if (mode == 0)
            {
                Look(keeper, 0.3f);
                Say("fast forward x4: watch the Keeper's tree for about a minute");
                yield return Wait(1f);
                yield break;
            }
            Say("fast forward x4: waiting for a chopper");
            yield return Until(() => Seen(a => a.kind == ActivityKind.Chop && !a.moving, out _), 90f);
            if (!Seen(a => a.kind == ActivityKind.Chop && !a.moving, out var at)) { Say("setup failed: nobody chopped in time; press Set up again"); DefaultView(); yield break; }
            if (mode == 1)
            {
                pv.timeScale = 0.5f; // slow enough that the swing, the chips and the log on the ground read, and the chopper is still at the tree when the shot is taken
                Say("chopping at the wild tree: chips, then logs on the ground");
                yield return Follow(a => a.kind == ActivityKind.Chop, 0.13f, 38f, 3f);
                yield break;
            }
            Look(World(at.x, at.y), 0.2f, 45f);
            Say("waiting for a log to be carried");
            yield return Until(() => Seen(a => a.moving && a.prop == Prop.Log, out _), 90f);
            if (Seen(a => a.moving && a.prop == Prop.Log, out var carry)) { pv.timeScale = 0.5f; Say("a log on its way to the stash"); yield return Follow(a => a.moving && a.prop == Prop.Log, 0.2f, 42f, 3f); yield break; }
            else Say("no log carried yet; press Set up again");
            yield return Wait(1f);
        }

        // ---------------------------------------------------------------- #574 the hunt and the egg
        IEnumerator SetupHunt()
        {
            Hour(10f);
            StateCheat("rabbits", EcoDebug.AddRabbits(5));
            StateCheat("make a hunter", EcoDebug.MakeHunter());
            Hour(10f);
            yield return Frames(10);
            StateCheat("start a hunt", EcoDebug.StartHunt());
            Say("waiting for the stalk");
            yield return Until(() => Seen(a => a.kind == ActivityKind.Stalk, out _), 90f);
            if (Seen(a => a.kind == ActivityKind.Stalk, out var at)) { Look(World(at.x, at.y), 0.22f, 45f); Say("the hunter stalks a rabbit"); }
            else { Say("setup failed: no stalk seen; press Set up again"); DefaultView(); }
            yield return Wait(1f);
        }

        IEnumerator SetupEggGather()
        {
            Hour(10f);
            yield return EnsurePond();
            if (!Sim.LifeIsPlaced(LifeSpecies.Ducks))
            {
                if (!Sim.JarReady(LifeSpecies.Ducks)) Sim.DebugJarsReady();
                StateCheat("ducks placed", $"near the camp ({Sim.DebugPlaceNearCamp(LifeSpecies.Ducks)})");
            }
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            // ducks above their floor, or the cheat refuses (no egg is taken at the floor): let the flock grow 8 seasons (the season comes round), staged moments stopped
            StateCheat("grow the flock", EcoDebug.SkipSeasons(8));
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            Hour(10f);
            yield return Frames(10);
            StateCheat("gather an egg now", EcoDebug.GatherEggNow());
            yield return Until(() => EggKneel(out _), 90f);
            if (EggKneel(out var at))
            {
                pv.timeScale = 0.6f; Say("a gatherer kneels at the pond edge with a basket");
                for (float tt = 0f; tt < 2.5f; tt += Time.unscaledDeltaTime)
                {
                    if (EggKneel(out at)) { var w = World(at.x, at.y); Rig.CancelFlight(); Rig.pitch = 40f; Rig.zoom = 0.22f; Rig.target = w; Rig.Apply(); }
                    yield return null;
                }
                yield break;
            }
            else { Say("setup failed: no gatherer seen; press Set up again"); DefaultView(); }
            yield return Wait(1f);
        }

        // ---------------------------------------------------------------- #575 rename
        IEnumerator SetupRename()
        {
            Hour(10f);
            DefaultView();
            yield return Frames(10);
            Say(PersonCardView.DebugRenameField());
            yield return Wait(0.8f);
        }

        // ---------------------------------------------------------------- #576 seasons
        IEnumerator SetupSeason(int season)
        {
            Hour(11f);
            for (int i = 0; i < 4 && (int)LifeClock.SeasonOf(Sim.State.month) != season; i++) StateCheat("skip to next season", EcoDebug.SkipToNextSeason());
            Hour(11f);
            DefaultView();
            yield return Wait(1.5f);
            Say("the title: " + GameHud.TitleLine(Mathf.FloorToInt(Sim.State.Year), GameHud.SeasonWord(Sim.State), Sim.State.population));
        }

        // ---------------------------------------------------------------- #577 glass and lid
        IEnumerator SetupGlass(float hour, float zoom)
        {
            Hour(hour, true);
            yield return Wait(2f);
            if (FireAndRack(out var fire, out _)) { Look(fire, zoom, 40f); }
            else { Say("setup failed: no camp fire"); DefaultView(); yield break; }
            yield return Wait(1f);
        }

        IEnumerator SetupLid(float pitch)
        {
            Hour(10f);
            Rig.CancelFlight();
            Rig.target = box.transform.position; Rig.zoom = 1f; Rig.pitch = pitch; Rig.Apply();
            yield return Wait(1.6f);
            Say($"lid opacity {box.LidOpacity:0.00} at pitch {pitch:0}");
        }

        // ---------------------------------------------------------------- #578 the travellers
        IEnumerator ClearTheKeeper()
        {
            if (Sim.OpenDilemma != null) StateCheat("answer the open question", DilemmaCheats.AnswerOnCard(1));
            var m = CardStackView.Instance != null ? CardStackView.Instance.Model : null;
            if (m != null && m.Mode == CardStackMode.Open) m.Close();
            yield return Frames(6);
        }

        IEnumerator SetupTravellerWords()
        {
            Hour(10f);
            DefaultView();
            Sim.DebugLand = "meadow";
            StateCheat("no woods", "the question's land forced to the meadow");
            string said = DilemmaCheats.Raise("camp_travellers");
            Sim.DebugLand = "";
            var d = Sim.OpenDilemma;
            if (d == null || d.defId != "camp_travellers") { Say("setup failed: " + said); yield break; }
            yield return Wait(1f);
            Say(Sim.QuestionOf(d));
        }

        IEnumerator SetupTravellerWood()
        {
            Hour(10f);
            yield return ClearTheKeeper();
            Rig.GoHome(0.01f, true);
            StateCheat("travellers bring wood now", ApartGroupsView.DebugWoodNow());
            yield return Wait(0.5f);
            var g = Sim.ApartGroups.LastOrDefault();
            if (g == null) { Say("setup failed: no travellers' camp"); yield break; }
            ApartGroupsView.DebugFly(g.id, 0.4f);
            Say("their cutter carries a log to their pile");
            yield return Wait(1f);
        }

        IEnumerator SetupTravellerJoin()
        {
            Hour(10f);
            yield return ClearTheKeeper();
            Rig.GoHome(0.01f, true);
            StateCheat("travellers join now", ApartGroupsView.DebugJoinNow());
            yield return Wait(1f);
            var g = Sim.ApartGroups.LastOrDefault();
            if (g != null) ApartGroupsView.DebugFly(g.id, 0.4f);
            Say("they pack up at the next calm moment and walk in");
            yield return Wait(1f);
        }
    }
}
