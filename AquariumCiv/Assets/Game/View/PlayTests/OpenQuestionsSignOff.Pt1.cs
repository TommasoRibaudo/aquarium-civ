using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Lighting;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The stops of the first outside playtest (#643-#680) and the new start (#656, #677) for the sign-off walk. The text of the section is
    /// in docs/open-questions.md ("Playtest 1"). Each stop stages its state with the existing cheats (EcoDebug, SimCore.Debug*, the views'
    /// Debug* helpers) and says in its judge text what to look at and what good is. A few stops open a box of their own (<see cref="Stop.Box"/>:
    /// a plain village with fields, a box in its first run, a fresh camp with no wild wood, a young box, a new-start box); the walk puts the
    /// played box back before the next stop that needs it. #658 sets three candidate default framings on the rig directly (the game's
    /// default is unchanged). Every setup keeps the walk's contract: Setup and Extras are IEnumerators, abortable by a verdict, so auto
    /// mode (Temp/agent/oqsignoff/AUTO, "from,to") shoots them like any other look stop.
    /// </summary>
    public partial class OpenQuestionsSignOff
    {
        string ownBox = "";        // the key of the box a stop opened itself ("" = the played box or the young pacing box)
        bool pinnedHere;           // #673 pinned someone: unpinned before the next stop
        bool zoomFloorChanged;     // #676 lowered the rig's zoom floor for the close look at the pot
        float sfxWas = -1f, ambWas = -1f;
        int framing;               // #658: the framing Set up again shows next
        (Vector3 target, float yaw, float pitch, float zoom)? framingA;

        static Stop With(Stop s, int w = 0, int h = 0, string box = null, string notStaged = null)
        {
            if (w > 0) { s.W = w; s.H = h; }
            if (box != null) s.Box = box;
            if (notStaged != null) s.NotStaged = notStaged;
            return s;
        }

        /// <summary>The stops of one issue of playtest 1 (the key is the issue number); an empty list: shown by another issue's stop.</summary>
        Stop[] Pt1(string key)
        {
            switch (key)
            {
                case "#643": return new[] { S("#643", "the wolf jar from the chooser", "pt1_wolves",
                    "Every other group is placed and the wolf jar is ready; the Kit is open on Life. Good: the chooser shows the wolves (it opens for one jar too), " +
                    "and a tap on them puts them in hand with a hint that names the wolves; the camera brings the den ground into view if the den has room " +
                    "(if not, the jar says it waits for room). Tap the wolves and judge the hint and the framing.", SetupWolves) };
                case "#644": return new[] { With(S("#644", "fields through the seasons", "pt1_fields",
                    "A grown village (a plain box, its own) framed on a farm field in spring. Good: spring green shoots, summer standing wheat, autumn stubble, " +
                    "winter bare soil (no green or wheat in winter). Press Next season three times and judge each.", SetupFields, ("Next season", NextFieldSeason)), box: "fields") };
                case "#645": return new[] { With(S("#645", "name fields and the iPhone keyboard", "pt1_keyboard",
                    "Save on a name field must close the iPhone keyboard, with no second input bar. Judge on the next phone build: Skip here.", () => PlainSetup("", null, false, false, false)),
                    notStaged: "not staged: phone only (the Editor has no iPhone keyboard); judge it on the next build, or Skip") };
                case "#647": return new[] { With(S("#647", "the first run without the Photo button", "pt1_firstrun",
                    "A fresh box in its first run (its own box; the panel is folded: tap the arrow to read this). Good: the top-right corner has Settings " +
                    "(and Feedback) but NO Photo button while the first-run steps go on; press Skip first run and the Photo button comes back.", SetupFirstRun,
                    ("Skip first run", SkipFirstRunExtra)), box: "firstrun") };
                case "#648": return new[] { S("#648", "quieter sound at the default view", "pt1_sound",
                    "Sound on, the default view, midday. Good: the effects and the ambience sit under the music, nothing harsh or loud. The line below shows " +
                    "your current slider values; press Use the 0.6 defaults to hear what a new player gets (your values come back at the end of the walk).", SetupSound,
                    ("Use the 0.6 defaults", SoundDefaultsExtra)) };
                case "#649": return new[] { S("#649", "the HUD icons: gear and Feedback", "pt1_hud_icons",
                    "The default view with the panel folded so the top row shows (tap the arrow to read this). Good: the Settings icon is a gear, not a sun; " +
                    "the Feedback button is full ink, as dark (or light) as its neighbours, not faded like a disabled button.", SetupHudIcons) };
                case "#650": return new Stop[0]; // in #649's stop
                case "#651": return new[] { With(S("#651", "camp chores with no wild wood", "pt1_kindling",
                    "A fresh camp (its own box) with no wild tree grown, at dawn: the morning chores. Watch about a minute. Good: sweeping, washing, " +
                    "sitting out, water; nobody chops kindling or swings an axe at a block. (Winter farmers hoeing need a village: covered by the planner tests.)",
                    SetupKindling), box: "camp") };
                case "#652": return new[] { With(S("#652", "the gatherers' place word", "pt1_place_word",
                    "A camp with berries and a gatherer; their card is open. Read the card's doing line and the tag over the doll (and the line below, the " +
                    "planner's own words). Good: no pond: \"the dry hollow\"; press Add a pond: \"the water's edge\". Never \"the stream\".", SetupPlaceWord,
                    ("Add a pond", PlaceWordPondExtra)), box: "camp") };
                case "#654": return new[] { S("#654", "the Settings sliders", "pt1_sliders",
                    "The Settings sheet is open. Drag Music, Effects and Ambience sideways; scroll the sheet and drag again. Good: each slider follows the " +
                    "finger from anywhere on its row, and the list does not move while you drag.", SetupSliders) };
                case "#657": return new[] { S("#657", "grass tufts up close", "pt1_grass",
                    "Grass by the camp at the closest zoom, low pitch. Good: every tuft's base sits in the soil; no tuft floats or shows a gap under it. Orbit a little to check.", SetupGrass) };
                case "#659": return new[] { With(S("#659", "the caption under the title (iPhone 16 Pro)", "pt1_caption",
                    "1206x2622 (iPhone 16 Pro). Someone near the middle speaks a line; the panel is folded so the top shows. Good: the caption plate sits " +
                    "fully below the title line (year, season, people), never over it. Set up again says another line.", SetupCaption), w: 1206, h: 2622) };
                case "#669": return new[] { S("#669", "History on the played box", "pt1_history_old",
                    "The played box (rabbits and berries today) with History open. Drag the bar back to the early years. Good: no berries, rabbits, deer or " +
                    "wolves of today in the old years; they are back after Close.", SetupHistoryOld) };
                case "#670": return new[] { With(S("#670", "History on a young box", "pt1_history_young",
                    "A young box (its own, about a year and a half) with History open. Press play. Good: the whole span plays in about 12 s, not in a blink; " +
                    "press Slowest speed: the speed reads like \"9 d/s\" and Close stays on screen.", SetupHistoryYoung, ("Slowest speed", SlowestSpeedExtra)), box: "young") };
                case "#672": return new[] { S("#672", "the evening talk and your view", "pt1_evening",
                    "The camera sits at a far corner, then the evening talk starts (cheat): the camera goes to the fire. Wait for the talk to end. Good: " +
                    "the camera eases back to this corner view, not to the camp frame. Zoomed in watching: the talk must not start by itself (wait 30 s).",
                    SetupEvening, ("Zoomed in watching", EveningZoomedExtra)) };
                case "#673": return new[] { S("#673", "photo mode with someone pinned", "pt1_photo",
                    "Someone is pinned and the camera sits away from them; photo mode is on (the panel folded). Good: the view stays where it was (no jump " +
                    "to the pinned person), the pill shows about 4 s and then fades out fully. A tap on the screen brings the UI back.", SetupPhoto,
                    ("Photo mode again", PhotoAgainExtra)) };
                case "#674": return new[] { With(S("#674", "the person card's first view (750x1334)", "pt1_card_short",
                    "750x1334. A notable with a long two-line subtitle (a woodcutter who is also headman); their card is open at its first height. Good: " +
                    "the first view shows a row of family and friend chips under the subtitle, before the quote; nothing cut at the card's edge.", SetupCardShort), w: 750, h: 1334) };
                case "#680": return new[] { S("#680", "the hunter tries again", "pt1_hunt_retry",
                    "A hunt that misses (cheat), close. Good: after the miss the hunter stalks once more and throws again (that one misses too here), " +
                    "then walks home; one retry, never more. Set up again to watch twice.", SetupHuntRetry) };
                case "#675": return new[] { S("#675", "the seed in the life jar", "pt1_seed",
                    "The Kit is open on Life with the seed ready beside the groups. Good: the seed is a sprout icon with the word Seed; tap it: the hint " +
                    "says one seed grows into the box's tree, and where to tap.", SetupSeedJar) };
                case "#676": return new[] { S("#676", "the stew pot at dusk", "pt1_stew",
                    "The camp's stew pot up close at the cook's hour. Good: a dark sooty pot, a lumpy brown stew that reads as food, soft white steam rising " +
                    "and a few bubbles on the surface.", SetupStew) };
                case "#665": return new[] { S("#665", "rabbit tracks keep coming", "pt1_tracks",
                    "Rabbits in the meadow, framed on their tracks. Good: fresh hop prints show near them; press Skip a season (more than once): new prints keep " +
                    "appearing while rabbits live (the line below counts the living trails).", SetupTracks, ("Skip a season", TracksSeasonExtra)) };
                case "#671": return new[] { S("#671", "faded near glass up close", "pt1_near_glass",
                    "Misted wet ground along the near glass, zoomed in so the near panes and frame fade. Good: the wet darkening runs right up to the glass " +
                    "with no straight cut or pale band where the faded frame is; press Winter: the snow does the same.", SetupNearGlass, ("Winter", NearGlassWinterExtra)) };
                case "#656": return new[] { With(S("#656", "the arrival with the basket and the bundle", "pt1_arrival",
                    "A new-start box (its own) as the camp is founded, with the stock carried in again (cheat). Good: the people walk in carrying a basket " +
                    "of food and a bundle of sticks and set them down where they settle; the HUD's food and wood numbers show only after that. Arrival again repeats it.",
                    SetupArrival, ("Arrival again", ArrivalAgainExtra)), box: "newstart") };
                case "#677": return new[] { With(S("#677", "the fire stages and the first wild wood", "pt1_fire",
                    "The new-start camp at dusk, on its fire. Good: No fire: the people sit together, nothing burns; Grass fire: a small low fire of grass and " +
                    "twigs, no logs; Log fire: a bigger log fire with warmer light. First wild wood now: the first logs are carried home from a wild tree and the fire becomes a log fire.",
                    SetupFire, ("No fire", () => FireExtra(0)), ("Grass fire", () => FireExtra(1)), ("Log fire", () => FireExtra(2)), ("First wild wood now", FirstWildWoodExtra)), box: "newstart") };
                case "#658": return new[] { S("#658", "three default framings", "pt1_framing",
                    "Three candidate default views of the camp on a tall phone (the panel folded; tap the arrow to read this). A: today's default (the camp frame). " +
                    "B: closer on the camp's fire with a lower pitch. C: turned to the orbit's limit (55 degrees) so the land's long side runs into the screen, and closer. " +
                    "Good: the camp and the people fill the screen, the action reads, the glass does not crowd it. Write the letter you like in the note (the decision asks again).",
                    SetupFraming, ("Framing A", () => FramingExtra(0)), ("Framing B", () => FramingExtra(1)), ("Framing C", () => FramingExtra(2))) };
            }
            return new Stop[0];
        }

        // ---------------------------------------------------------------- the walk's housekeeping
        void CloseLoosePt1()
        {
            var tls = TimeLapseScreenView.Instance;
            if (tls != null && tls.IsOpen) tls.Close();
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) TimeLapseView.DebugClose();
            var h = GameHud.Instance;
            if (h != null && h.CameraMode) h.SetCameraMode(false);
            var talk = EveningTalkView.Instance;
            if (talk != null && talk.Open) EveningTalkView.DebugLeave();
            EveningTalkView.AutoStart = false;
            if (zoomFloorChanged && Camera.main != null) { Rig.minZoom = CameraReach.MinZoom; zoomFloorChanged = false; }
            if (pinnedHere && GameClock.Instance != null && Sim != null) { Sim.Unpin(); pinnedHere = false; }
            if (pv != null) pv.timeScale = 1f;
        }

        /// <summary>At the end of the walk: the player's own sound sliders come back (#648 may have set the defaults).</summary>
        void RestorePt1()
        {
            if (sfxWas >= 0f) { GameSettings.SfxVolume = sfxWas; sfxWas = -1f; }
            if (ambWas >= 0f) { GameSettings.AmbienceVolume = ambWas; ambWas = -1f; }
            if (Camera.main != null && Camera.main.GetComponent<BoxCamera>() != null) Rig.minZoom = CameraReach.MinZoom;
            EveningTalkView.AutoStart = true;
        }

        /// <summary>A box of the stop's own (a fresh box at the current Game view size; the first run skipped unless asked), and the walk's handles on it.</summary>
        IEnumerator OpenOwn(string key, int seed, bool skipFirstRun = true)
        {
            Say("a box of its own for this stop...");
            yield return Open(Screen.width, Screen.height, seed, skipFirstRun);
            GameUiPanel.AcceptInputUnfocused(true);
            Rebind();
            ownBox = key; youngBox = false; mistedHalf = false; framingA = null;
            Hour(10f);
            yield return Frames(10);
        }

        /// <summary>The Kit open on its Life chooser (the slot's own handler, as a tap on it would run it).</summary>
        bool OpenLifeRow()
        {
            hud.Open(GameHud.Kit);
            var m = typeof(GameHud).GetMethod("OnLifeSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null && !hud.LifeRowOpen) m.Invoke(hud, null);
            return hud.LifeRowOpen;
        }

        Notable Grown(Func<Notable, bool> also = null) =>
            Sim.State.notables.FirstOrDefault(n => n.alive && n.age >= 16f && n.age < Sim.Config.retireAge && (also == null || also(n)));

        IEnumerator LookAtCamp(float zoom = 0.35f)
        {
            if (FireAndRack(out var fire, out var rack)) Look((fire + rack) * 0.5f, zoom);
            else DefaultView();
            yield return Wait(0.6f);
        }

        // ---------------------------------------------------------------- #643 the wolves
        IEnumerator SetupWolves()
        {
            Hour(10f);
            yield return EnsureFish(); // a pond with ducks and fish placed
            foreach (var s in new[] { LifeSpecies.Thicket, LifeSpecies.Rabbits, LifeSpecies.Deer })
                if (!Sim.LifeIsPlaced(s)) { if (!Sim.JarReady(s)) Sim.DebugJarsReady(); Log($"{s} placed near the camp ({Sim.DebugPlaceNearCamp(s)})"); }
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            Log("jars ready: " + Sim.DebugJarsReady());
            DefaultView();
            yield return Frames(10);
            var ready = new List<LifeSpecies>(); Sim.ReadyJars(ready);
            bool open = OpenLifeRow();
            yield return Wait(0.6f);
            Say($"ready jars: {(ready.Count > 0 ? string.Join(", ", ready) : "none")}; chooser {(open ? "open" : "not open: tap Kit, then Life")}");
        }

        // ---------------------------------------------------------------- #644 fields (a plain village)
        int fieldSeason;

        IEnumerator SetupFields()
        {
            if (ownBox != "fields")
            {
                // the FeedbackShotPlayTests / Fb10Shots recipe on a plain slice (tooling.md 2026-11-13: the living-land village gate gives no farm lots)
                GameClock.EcologyPref = false;
                try { yield return OpenOwn("fields", 1234); }
                finally { GameClock.EcologyPref = true; }
                PeopleView.DebugSetHour(12f);
                Log("grow village: " + MomentPlayer.DebugGrowVillage(30f));
                yield return Frames(3);
                Sim.ForceNextEra();
                yield return Frames(3);
                Log("skip years: " + SimLayoutSource.DebugSkipYears(30f));
                yield return Frames(4);
                Sim.CleanAll();
            }
            fieldSeason = 0;
            yield return ShowField();
        }

        IEnumerator ShowField()
        {
            yield return Fb5SeasonShots.ToSeason(fieldSeason, 1f);
            Hour(12f);
            var farm = Sim.Settlement != null ? Sim.Settlement.Lots.FirstOrDefault(l => l.kind == LotKind.Farm) : null;
            if (farm == null) { Say("setup failed: no farm lot in the village; press Set up again"); DefaultView(); yield break; }
            Look(World(farm.x, farm.z), 0.35f, 45f);
            yield return Wait(1f);
            Say("now: " + LifeClock.SeasonOf(Sim.State.month) + " (farm " + farm.id + ")");
        }

        IEnumerator NextFieldSeason()
        {
            fieldSeason = (fieldSeason + 1) % 4;
            yield return ShowField();
        }

        // ---------------------------------------------------------------- #647 the first run
        IEnumerator SetupFirstRun()
        {
            if (ownBox != "firstrun" || FirstRunView.Instance == null || !FirstRunView.Instance.Active)
                yield return OpenOwn("firstrun", 4242, skipFirstRun: false);
            yield return Wait(2f);
            panel.collapsed = true;
            Say(PhotoButtonLine() + " · " + FirstRunView.DebugStatus());
        }

        string PhotoButtonLine()
        {
            var b = hud != null && hud.Root != null ? hud.Root.Q("hud-photo") : null;
            bool first = FirstRunView.Instance != null && FirstRunView.Instance.Active;
            return $"first run {(first ? "on" : "off")}, Photo button {(b == null ? "not found" : b.resolvedStyle.display == DisplayStyle.None ? "hidden" : "shown")}";
        }

        IEnumerator SkipFirstRunExtra()
        {
            Log(FirstRunView.DebugSkipAll());
            yield return Wait(1.5f);
            ownBox = "firstrun_done"; // Set up again opens a box in its first run again
            panel.collapsed = true; // the top row stays in sight (an auto run unfolds the panel between the shot and the extras)
            Say(PhotoButtonLine());
        }

        // ---------------------------------------------------------------- #648 sound, #649/#650 the HUD icons, #654 sliders
        IEnumerator SetupSound()
        {
            Hour(12f);
            GameSettings.Sound = true;
            GameSettings.Apply();
            DefaultView();
            yield return Wait(1f);
            Say($"sound on · Music {GameSettings.MusicVolume:0.00}, Effects {GameSettings.SfxVolume:0.00}, Ambience {GameSettings.AmbienceVolume:0.00}");
        }

        IEnumerator SoundDefaultsExtra()
        {
            if (sfxWas < 0f) { sfxWas = GameSettings.SfxVolume; ambWas = GameSettings.AmbienceVolume; }
            GameSettings.SfxVolume = 0.6f; GameSettings.AmbienceVolume = 0.6f;
            yield return Wait(0.5f);
            Say($"the defaults: Effects 0.60, Ambience 0.60 (yours were {sfxWas:0.00} and {ambWas:0.00}; they come back at the end)");
        }

        IEnumerator SetupHudIcons()
        {
            Hour(10f);
            DefaultView(0f);
            yield return Frames(10);
            panel.collapsed = true;
            bool fb = hud.Root != null && hud.Root.Q("hud-feedback") != null;
            Say(fb ? "top row: the title, Feedback, Photo, Settings" : "no Feedback button in this build (it shows in test builds only)");
        }

        IEnumerator SetupSliders()
        {
            Hour(10f);
            DefaultView();
            yield return Frames(6);
            hud.Open(GameHud.Settings);
            yield return Wait(0.6f);
            Say("the Settings sheet: drag the sliders, scroll, drag again");
        }

        // ---------------------------------------------------------------- #651 / #652 a fresh camp
        IEnumerator FreshCamp()
        {
            if (ownBox == "camp") yield break;
            yield return OpenOwn("camp", 4242);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            Log("camp: " + EcoDebug.CampGate());
            yield return Wait(2f);
            for (int calm = 0, f = 0; calm < 90 && f < 1200; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
        }

        IEnumerator SetupKindling()
        {
            yield return FreshCamp();
            Hour(6.8f, true);
            yield return Wait(1f);
            yield return LookAtCamp(0.3f);
            Say($"wild trees grown: {Sim.WildWoodTreeCount} (woods {Sim.WoodsTreeCount}) · watch the dawn chores");
        }

        IEnumerator SetupPlaceWord()
        {
            yield return FreshCamp();
            if (!Sim.BerriesGrow) Log("bushes: " + EcoDebug.AddBushes(3));
            yield return PlaceWordCard();
        }

        IEnumerator PlaceWordCard()
        {
            var g = Grown(n => n.job == JobType.Gatherer) ?? Grown(n => n.job != JobType.Toolmaker);
            if (g == null) { Say("setup failed: nobody grown up"); yield break; }
            if (g.job != JobType.Gatherer) { Sim.DebugSetJob(g, JobType.Gatherer); Log(g.FullName + " gathers now"); }
            pv.Planner.Invalidate();
            Hour(10f, true);
            yield return Wait(1.5f);
            var cards = PersonCardView.Instance;
            if (cards != null) { if (cards.IsOpen) cards.Close(); yield return null; cards.Open(g.id, true); }
            yield return Wait(1.2f);
            var words = new List<string>();
            foreach (var a in pv.Activities)
            {
                string d = pv.Planner.Describe(a) ?? "";
                if ((d.Contains("hollow") || d.Contains("water's edge") || d.Contains("stream")) && !words.Contains(d)) words.Add(d);
            }
            Say($"pond cells {Sim.EcoPondCells} · {g.FullName}: \"{(cards != null ? cards.DoingNow : "")}\" · lines naming the place now: " + (words.Count > 0 ? string.Join(" / ", words.Take(3)) : "none at this hour"));
        }

        IEnumerator PlaceWordPondExtra()
        {
            yield return EnsurePond();
            ownBox = "camp_pond"; // the next stop that wants the fresh camp opens it again (this one has a pond and far wood now)
            yield return PlaceWordCard();
        }

        // ---------------------------------------------------------------- #657 grass
        IEnumerator SetupGrass()
        {
            Hour(11f);
            if (!FireAndRack(out var fire, out _)) fire = box.transform.position;
            var local = box.transform.InverseTransformPoint(fire);
            int best = -1; float bestD = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
            {
                var cv = Sim.EcoCoverAt(c);
                if (cv != EcoCover.Grass && cv != EcoCover.Tall) continue;
                float d = Vector2.Distance(new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)), new Vector2(local.x, local.z));
                if (d > 0.7f && d < bestD) { bestD = d; best = c; }
            }
            if (best < 0) { Say("no grass cell found: misting the camp"); Log(EcoDebug.MistBigCamp()); yield return Frames(20); DefaultView(); yield break; }
            var at = World(SiteMap.CXc(best), SiteMap.CZc(best));
            Rig.CancelFlight(); Rig.pitch = 26f; Rig.zoom = 0.2f; Rig.target = at; Rig.Apply();
            yield return Wait(1f);
            Say($"grass cell {best}, {bestD:0.0} u from the fire, zoom 0.20, pitch 26");
        }

        // ---------------------------------------------------------------- #659 the caption on an iPhone 16 Pro
        IEnumerator SetupCaption()
        {
            Hour(10f);
            yield return LookAtCamp(0.4f);
            yield return Wait(0.5f);
            panel.collapsed = true;
            Say(ShowMeDirector.DebugLineHere() + $" · {Screen.width}x{Screen.height}");
            yield return Wait(1.2f); // the plate and the ring fade in (tooling 2026-11-08)
        }

        // ---------------------------------------------------------------- #669 / #670 History
        IEnumerator OpenHistory()
        {
            var s = TimeLapseScreenView.Ensure();
            bool ok = s != null && s.Open();
            yield return Wait(0.8f);
            var tl = TimeLapseView.Find();
            Say(ok ? "History open · " + (tl != null ? tl.LastInfo : "") : "History did not open (a moment playing?); tap History at the bottom");
        }

        IEnumerator SetupHistoryOld()
        {
            Hour(10f);
            if (Sim.EcoRabbitsShown() < 3) Log(EcoDebug.AddRabbits(4));
            if (!Sim.BerriesGrow) Log(EcoDebug.AddBushes(3));
            DefaultView();
            yield return Wait(1.5f);
            yield return OpenHistory();
        }

        IEnumerator SetupHistoryYoung()
        {
            if (ownBox != "young")
            {
                yield return OpenOwn("young", 4242);
                foreach (var (x, z) in new[] { (-1.6f, 0.6f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
                Log(EcoDebug.SkipSeasons(6));
                yield return Frames(20);
            }
            Hour(10f);
            DefaultView();
            yield return Wait(1f);
            yield return OpenHistory();
        }

        IEnumerator SlowestSpeedExtra()
        {
            var tl = TimeLapseView.Find();
            if (tl == null || !tl.Active) { Say("History is not open: Set up again"); yield break; }
            tl.SetSpeed(0);
            yield return Wait(0.5f);
            Say("slowest speed: " + AquariumCiv.Sim.Replay.ReplayClock.Speeds[0] + "x · " + tl.LastInfo);
        }

        // ---------------------------------------------------------------- #672 the evening talk
        Vector3 Corner() => World(Sim.Settlement != null && Sim.Settlement.Map.greenX > 0f ? -2.2f : 2.2f, 1.3f);

        IEnumerator SetupEvening()
        {
            Hour(19.3f, true);
            yield return Wait(1.5f);
            Rig.CancelFlight(); Rig.pitch = 40f; Rig.zoom = 0.6f; Rig.target = Corner(); Rig.Apply();
            yield return Wait(1f);
            var before = Rig.target;
            Say(EveningTalkView.DebugStart() + $" · your view: target {before.x:0.0},{before.z:0.0} zoom 0.60 · wait for the talk to end");
        }

        IEnumerator EveningZoomedExtra()
        {
            var talk = EveningTalkView.Instance;
            if (talk != null && talk.Open) EveningTalkView.DebugLeave();
            yield return Wait(1f);
            var g = Grown();
            if (g != null && pv.TryGetDollPosition(PersonRef.Notable(g.id), out var p)) { Rig.CancelFlight(); Rig.pitch = 40f; Rig.zoom = 0.22f; Rig.target = World(p.x, p.y); Rig.Apply(); }
            Say(EveningTalkView.DebugLongSitting() + ": zoomed in on someone; the talk must NOT start while you watch (20 s idle would start it when zoomed out)");
        }

        // ---------------------------------------------------------------- #673 photo mode
        IEnumerator SetupPhoto()
        {
            Hour(10f);
            var g = Grown();
            if (g == null || !Sim.Pin(g.id)) { Say("setup failed: nobody to pin"); yield break; }
            pinnedHere = true;
            yield return Frames(4);
            Vector3 far = Corner();
            if (pv.TryGetDollPosition(PersonRef.Notable(g.id), out var p)) { var w = World(p.x, p.y); far = World(w.x > box.transform.position.x ? -2.2f : 2.2f, w.z > box.transform.position.z ? -1.3f : 1.3f); }
            Rig.CancelFlight(); Rig.pitch = 40f; Rig.zoom = 0.45f; Rig.target = far; Rig.Apply();
            yield return Wait(1f);
            yield return PhotoAgainExtra();
        }

        IEnumerator PhotoAgainExtra()
        {
            if (hud.CameraMode) { hud.SetCameraMode(false); yield return Wait(0.5f); }
            var at = Rig.target;
            hud.SetCameraMode(true);
            panel.collapsed = true;
            yield return Wait(0.5f);
            Say($"photo mode on · pinned {(Sim.Pinned != null ? Sim.Pinned.FullName : "nobody")} · following {(Rig.IsFollowing ? "someone (wrong)" : "nobody")} · moved {(Rig.target - at).magnitude:0.00} u");
        }

        // ---------------------------------------------------------------- #674 the person card at 750x1334
        IEnumerator SetupCardShort()
        {
            Hour(10f);
            DefaultView();
            var n = Grown(x => x.id != (Sim.Pinned != null ? Sim.Pinned.id : -1));
            if (n == null) { Say("setup failed: nobody grown up"); yield break; }
            Sim.DebugSetJob(n, JobType.Woodcutter);
            Log("headman: " + Sim.DebugSetRoleHolder(RoleType.Headman, n));
            pv.Planner.Invalidate();
            yield return Frames(4);
            var cards = PersonCardView.Instance;
            if (cards == null) { Say("setup failed: no person card"); yield break; }
            if (cards.IsOpen) cards.Close();
            yield return null;
            cards.Open(n.id, true);
            yield return Wait(1.2f);
            var sub = cards.CardRoot != null ? cards.CardRoot.Q<Label>("person-card-sub") : null;
            Say($"{n.FullName}: \"{(sub != null ? sub.text : "?")}\" ({(sub != null && sub.worldBound.height > sub.resolvedStyle.fontSize * 1.8f ? "two lines" : "one line")})");
        }

        // ---------------------------------------------------------------- #680 the retry
        IEnumerator SetupHuntRetry()
        {
            Hour(9f);
            if (Sim.EcoRabbitsShown() < 4) StateCheat("rabbits", EcoDebug.AddRabbits(5));
            StateCheat("make a hunter", EcoDebug.MakeHunter());
            Hour(9f);
            yield return Frames(10);
            StateCheat("hunt: miss, then try again", EcoDebug.HuntMissThenRetry());
            Say("waiting for the stalk");
            yield return Until(() => Seen(a => a.kind == ActivityKind.Stalk, out _), 90f);
            if (!Seen(a => a.kind == ActivityKind.Stalk, out var at)) { Say("setup failed: no stalk seen; press Set up again"); DefaultView(); yield break; }
            Look(World(at.x, at.y), 0.25f, 45f);
            Say("the hunter stalks: watch the miss, the second stalk and throw, the walk home");
            yield return Follow(a => a.kind == ActivityKind.Stalk, 0.25f, 45f, 2f);
        }

        // ---------------------------------------------------------------- #675 the seed in the jar
        IEnumerator SetupSeedJar()
        {
            if (Sim.State.eco.treeCell >= 0) yield return OpenOwn("seed", 4242); // one seed per box: a tree stands on the played box
            Hour(11f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            yield return Frames(10);
            Log(EcoDebug.SeedInHand());
            Log("jars ready: " + Sim.DebugJarsReady()); // a full row: the groups beside the seed
            hud.SetHand(GameHud.KitTool.None);
            DefaultView();
            yield return Wait(1f);
            if (hud.SheetOpen) hud.CloseSheet();
            bool open = OpenLifeRow();
            yield return Wait(0.6f);
            Say(open ? "the chooser is open: find the seed, tap it, read the hint" : "the chooser did not open: tap Kit, then Life");
        }

        // ---------------------------------------------------------------- #676 the stew pot
        IEnumerator SetupStew()
        {
            Log("camp props: " + ConsequenceCheats.ShowCampProps());
            GameObject pot = null;
            foreach (float hr in new[] { 17.8f, 12.5f, 16.5f, 8f })
            {
                Hour(hr, true);
                yield return Wait(3f);
                pot = GameObject.Find("Camp stew pot");
                if (pot != null && pot.activeInHierarchy) { Log("pot at " + hr); break; }
                pot = null;
            }
            if (pot == null) { Say("setup failed: no stew pot (nobody at the fire); press Set up again"); yield return LookAtCamp(); yield break; }
            zoomFloorChanged = true;
            for (float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 1.2f;)
            {
                Rig.CancelFlight(); Rig.minZoom = 0.02f;
                Rig.target = pot.transform.position + Vector3.up * 0.05f; Rig.zoom = 0.06f; Rig.pitch = 40f;
                Rig.Apply();
                yield return null;
            }
            Say($"the pot at {pv.Now.hour:0.0} h, zoom 0.06 (the zoom floor is lowered for this stop only)");
        }

        // ---------------------------------------------------------------- #665 rabbit tracks
        IEnumerator SetupTracks()
        {
            Hour(10f);
            if (Sim.EcoRabbitsShown() < 4) Log(EcoDebug.AddRabbits(6));
            Log(EcoDebug.RabbitTracksNow());
            yield return Wait(1.5f);
            yield return FrameTracks();
        }

        IEnumerator FrameTracks()
        {
            var cells = Sim.EcoRabbitTrackCells;
            if (cells == null || cells.Count == 0) { DefaultView(); Say("no track cells yet · " + EcoDebug.LiveTrailsNow()); yield break; }
            int c = cells[cells.Count / 2];
            Look(World(SiteMap.CXc(c), SiteMap.CZc(c)), 0.3f, 50f);
            yield return Wait(0.8f);
            Say(SeasonLook.Word(Sim.State.month) + " · " + EcoDebug.LiveTrailsNow());
        }

        IEnumerator TracksSeasonExtra()
        {
            Log(EcoDebug.SkipToNextSeason());
            Hour(10f);
            yield return Wait(2f);
            yield return FrameTracks();
        }

        // ---------------------------------------------------------------- #671 the near glass
        Vector3 nearFocus;

        IEnumerator SetupNearGlass()
        {
            Hour(11f);
            DefaultView(0f);
            yield return Frames(4);
            // the NightShots.NearGlass671 recipe: wet the ground along the two near panes, then zoom in on the near corner
            var camLocal = box.transform.InverseTransformPoint(Camera.main.transform.position);
            float nx = Mathf.Clamp(camLocal.x, -Land.HX + 0.5f, Land.HX - 0.5f), nz = Mathf.Clamp(camLocal.z, -Land.HZ + 0.5f, Land.HZ - 0.5f);
            for (float t = -1f; t <= 1f; t += 0.5f)
            {
                EcoDebug.MistBig(nx, nz + t * 1.2f * -Mathf.Sign(nz));
                EcoDebug.MistBig(nx + t * 1.6f * -Mathf.Sign(nx), nz);
            }
            yield return Wait(1.5f);
            nearFocus = World(nx, nz);
            yield return HoldNearGlass();
        }

        IEnumerator HoldNearGlass()
        {
            for (float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 1.5f;)
            {
                Rig.CancelFlight(); Rig.target = nearFocus; Rig.zoom = 0.22f; Rig.pitch = 40f; Rig.Apply();
                yield return null;
            }
            Say($"{SeasonLook.Word(Sim.State.month)} · near panes {box.PaneOpacity(0):0.00} {box.PaneOpacity(1):0.00} {box.PaneOpacity(2):0.00} {box.PaneOpacity(3):0.00} at zoom 0.22");
        }

        IEnumerator NearGlassWinterExtra()
        {
            for (int k = 0; k < 4 && LifeClock.SeasonOf(Sim.State.month) != Season.Winter; k++) Log(EcoDebug.SkipToNextSeason());
            Hour(11f);
            yield return Wait(2f);
            yield return HoldNearGlass();
        }

        // ---------------------------------------------------------------- #656 / #677 the new start
        IEnumerator NewStartBox()
        {
            if (ownBox == "newstart") yield break;
            yield return OpenOwn("newstart", 4242);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            Log(EcoDebug.SkipSeasons(2));
            yield return Frames(10);
            if (!Sim.NewStartOn) Say("note: not a new-start box (living land off, or an old box)");
        }

        IEnumerator SetupArrival()
        {
            yield return NewStartBox();
            Hour(10f);
            bool campStood = Sim.State.eco.campGateMonth >= 0;
            Sim.DebugArrivalAgain();
            StateCheat("arrival again", "the basket and the bundle are carried in again");
            if (!campStood) StateCheat("found the camp", EcoDebug.CampGate());
            yield return Wait(1.5f);
            yield return LookAtCamp(0.35f);
            Say($"{(campStood ? "the camp stood already" : "the camp is founded now")} · stock set down: {Sim.StockSetDown} · food {Sim.State.food:0.0}, wood {Sim.State.wood:0.0}");
        }

        IEnumerator ArrivalAgainExtra()
        {
            Sim.DebugArrivalAgain();
            yield return Wait(0.5f);
            yield return LookAtCamp(0.35f);
            Say("carried in again · stock set down: " + Sim.StockSetDown);
        }

        IEnumerator SetupFire()
        {
            yield return NewStartBox();
            if (Sim.State.eco.campGateMonth < 0) StateCheat("found the camp", EcoDebug.CampGate());
            Sim.DebugFireStage(-1);
            Hour(18.6f, true);
            yield return Wait(2f);
            yield return LookAtCamp(0.3f);
            Say("the land's fire now: " + Sim.FireStage + " (first wild wood " + (Sim.FirstWildWoodReached ? "came" : "not yet") + ")");
        }

        IEnumerator FireExtra(int stage)
        {
            Sim.DebugFireStage(stage);
            yield return Wait(1.5f);
            yield return LookAtCamp(0.3f);
            Say("fire held at: " + Sim.FireStage);
        }

        IEnumerator FirstWildWoodExtra()
        {
            Sim.DebugFireStage(-1);
            var bv = box;
            StateCheat("first wild wood now", Sim.DebugFirstWildWoodNow(bv == null ? null : (x, z, r) => bv.FreeForProp(new Vector2(x, z), r)));
            ownBox = "newstart_wood"; // the first wood comes once: Set up again opens a fresh new-start box
            Hour(16.5f, true);
            yield return Wait(1f);
            yield return LookAtCamp(0.35f);
            Say("the first logs: watch them carried home; the fire: " + Sim.FireStage);
        }

        // ---------------------------------------------------------------- #658 three default framings (the rig only; the game's default is unchanged)
        static readonly string[] FramingNames = { "A: today's default (the camp frame)", "B: closer on the fire, lower pitch", "C: turned to the orbit's limit, closer" };

        IEnumerator SetupFraming()
        {
            Hour(10f);
            yield return Frames(4);
            framingA = null;
            int show = framing; framing = (framing + 1) % 3; // Set up again shows the next one
            yield return FramingExtra(show);
        }

        IEnumerator FramingExtra(int which)
        {
            if (framingA == null)
            {
                var cf = CampFrameView.Instance;
                if (cf != null) { Log(cf.Go(0.01f)); yield return Wait(0.3f); }
                Rig.CancelFlight();
                var h = Rig.Home;
                framingA = cf != null && cf.Framed > 0 ? h : (box.transform.position, CampFrame.Yaw, CampFrame.Pitch, 0.45f);
            }
            var a = framingA.Value;
            Vector3 target = a.target; float yaw = a.yaw, pitch = a.pitch, zoom = a.zoom;
            if (which == 1)
            {
                if (FireAndRack(out var fire, out _)) target = new Vector3(fire.x, a.target.y, fire.z);
                zoom = a.zoom * 0.72f;
                pitch = Mathf.Max(28f, CameraReach.PitchFloor(zoom));
            }
            else if (which == 2)
            {
                yaw = Rig.maxYaw; // 55: the orbit's limit (the shelf and the wall behind stay in view, tester #31)
                zoom = a.zoom * 0.8f;
                pitch = Mathf.Max(40f, CameraReach.PitchFloor(zoom));
            }
            Rig.CancelFlight(); Rig.target = target; Rig.yaw = yaw; Rig.pitch = pitch; Rig.zoom = zoom; Rig.Apply();
            panel.collapsed = true;
            yield return Wait(0.8f);
            Say($"Framing {FramingNames[which]} · target {target.x:0.00},{target.z:0.00} yaw {yaw:0} pitch {pitch:0} zoom {zoom:0.00}");
        }
    }
}
