using System.Collections.Generic;

namespace AquariumCiv.Audio
{
    /// <summary>
    /// Every sound event in the slice as data (#96): tune volumes, jitter and limits here, not in the view. Same event,
    /// same sound (game-design-practices §10). Nothing here is the only way to know something: every cue goes with
    /// something on screen. Clip paths are relative to <c>Assets/Audio/Resources/</c>; sources in docs/assets-log.md.
    /// </summary>
    public static class CueLibrary
    {
        // ---------- ids: UI (the HUD, panels, cards, the book) ----------
        public const string UiTap = "ui_tap", UiToggle = "ui_toggle", UiOpen = "ui_open", UiClose = "ui_close",
            UiBack = "ui_back", UiConfirm = "ui_confirm", UiQuestion = "ui_question", UiScroll = "ui_scroll",
            CardArrive = "card_arrive", CardDismiss = "card_dismiss", BookOpen = "book_open", BookClose = "book_close",
            BookPage = "book_page";

        // ---------- ids: care (the keeper's hands) ----------
        public const string WipeRub = "wipe_rub", WipeStart = "wipe_start", PaneClean = "pane_clean", FullClean = "full_clean",
            MessGrab = "mess_grab", MessDrop = "mess_drop", MessFlick = "mess_flick", Prune = "prune", Rain = "rain_start",
            ShapeChannel = "shape_channel", ShapeOutcrop = "shape_outcrop", ShapeFail = "shape_fail", TapGlass = "tap_glass",
            TapPerson = "tap_person", Discovery = "discovery";

        // ---------- ids: the world and the era moments (MomentBeat.audio maps onto these, see MomentCues) ----------
        public const string Cheer = "cheer", CheerSmall = "cheer_small", Flute = "flute", Drum = "drum", TuneUp = "tune_up",
            Fiddle = "fiddle", Knock = "knock", Hammer = "hammer", Bell = "bell", CobbleTap = "cobble_tap", Chord = "chord",
            Chime = "chime", PauseNote = "pause_note";

        // ---------- ids: ambience loops (see Ambience) ----------
        public const string AmbCalm = "amb_calm", AmbBirds = "amb_birds", AmbCrickets = "amb_crickets", AmbRain = "amb_rain",
            AmbWind = "amb_wind";

        const string Ui = "Sfx/Ui/", Care = "Sfx/Care/", Moment = "Sfx/Moment/", Amb = "Ambience/";

        static Dictionary<string, CueDef> all;

        /// <summary>All cues by id (built once).</summary>
        public static IReadOnlyDictionary<string, CueDef> All => all ??= Build();

        public static CueDef Get(string id) => id != null && All.TryGetValue(id, out var c) ? c : null;

        /// <summary>The cue for a keeper gesture (the same events the people react to, LifeTypes.KeeperGesture); null for
        /// gestures with no sound of their own (Return, Glance, HoldFinger) or a loop the view drives (Wipe → <see cref="WipeRub"/>).</summary>
        public static string ForGesture(AquariumCiv.Life.KeeperGesture g) => g switch
        {
            AquariumCiv.Life.KeeperGesture.FullClean => FullClean,
            AquariumCiv.Life.KeeperGesture.FlickTrash => MessFlick,
            AquariumCiv.Life.KeeperGesture.Prune => Prune,
            AquariumCiv.Life.KeeperGesture.Rain => Rain,
            AquariumCiv.Life.KeeperGesture.TapGlass => TapGlass,
            AquariumCiv.Life.KeeperGesture.TapPerson => TapPerson,
            _ => null,
        };

        static Dictionary<string, CueDef> Build()
        {
            var d = new Dictionary<string, CueDef>();
            void Add(CueDef c) => d.Add(c.id, c);

            // UI: short, dry, quiet; never louder than the world. Kenney Interface Sounds + RPG Audio (book).
            Add(new CueDef(UiTap, SoundBus.Ui, 0.45f, Ui + "click_001", Ui + "click_002").Jitter(0.6f).Limit(0.05f, 2, true).Prio(40).Len(0.15f).Buzz(HapticKind.Selection));
            Add(new CueDef(UiToggle, SoundBus.Ui, 0.45f, Ui + "toggle_001", Ui + "toggle_002").Limit(0.08f, 1, true).Prio(40).Len(0.2f).Buzz(HapticKind.Selection));
            Add(new CueDef(UiOpen, SoundBus.Ui, 0.4f, Ui + "open_001").Limit(0.1f, 1, true).Prio(45).Len(0.3f));
            Add(new CueDef(UiClose, SoundBus.Ui, 0.4f, Ui + "close_001").Limit(0.1f, 1, true).Prio(45).Len(0.3f));
            Add(new CueDef(UiBack, SoundBus.Ui, 0.4f, Ui + "back_001").Limit(0.1f, 1, true).Prio(40).Len(0.3f));
            Add(new CueDef(UiConfirm, SoundBus.Ui, 0.5f, Ui + "confirmation_001").Limit(0.2f, 1, true).Prio(55).Len(0.5f).Buzz(HapticKind.Light));
            Add(new CueDef(UiQuestion, SoundBus.Ui, 0.45f, Ui + "question_001").Limit(0.5f, 1).Prio(55).Len(0.5f));
            Add(new CueDef(UiScroll, SoundBus.Ui, 0.25f, Ui + "scroll_001").Jitter(1f).Limit(0.08f, 1, true).Prio(20).Len(0.15f));
            Add(new CueDef(CardArrive, SoundBus.Ui, 0.5f, Ui + "bookPlace1", Ui + "bookPlace2").Jitter(0.5f).Limit(0.3f, 1).Prio(60).Len(0.4f).Buzz(HapticKind.Light));
            Add(new CueDef(CardDismiss, SoundBus.Ui, 0.35f, Ui + "minimize_001").Limit(0.1f, 1, true).Prio(40).Len(0.3f));
            Add(new CueDef(BookOpen, SoundBus.Ui, 0.55f, Ui + "bookOpen").Limit(0.3f, 1).Prio(55).Len(0.6f));
            Add(new CueDef(BookClose, SoundBus.Ui, 0.5f, Ui + "bookClose").Limit(0.3f, 1).Prio(55).Len(0.5f));
            Add(new CueDef(BookPage, SoundBus.Ui, 0.45f, Ui + "bookFlip1", Ui + "bookFlip2", Ui + "bookFlip3").Jitter(0.8f).Limit(0.12f, 2, true).Prio(45).Len(0.5f));

            // Care: the keeper's touch on the glass (#37 placeholders stay for the rub and the swish: proc:*).
            Add(new CueDef(WipeRub, SoundBus.Care, 0.5f, "proc:rub").Looped().Limit(0f, 1).Prio(60).Len(1f));
            Add(new CueDef(WipeStart, SoundBus.Care, 0.3f, Care + "cloth1", Care + "cloth2", Care + "cloth3").Jitter(1f, 0.15f).Limit(0.35f, 1).Prio(45).Len(0.4f));
            Add(new CueDef(PaneClean, SoundBus.Care, 0.5f, Care + "glass_001", Care + "glass_002", Care + "glass_003").Jitter(0f).Limit(0.25f, 2).Prio(70).Len(1f).Buzz(HapticKind.Light));
            Add(new CueDef(FullClean, SoundBus.Care, 0.7f, Care + "confirmation_002").Limit(2f, 1).Prio(85).Len(1.2f).Buzz(HapticKind.Success).Ducks());
            Add(new CueDef(MessGrab, SoundBus.Care, 0.45f, Care + "handleSmallLeather", Care + "handleSmallLeather2").Jitter(1f).Limit(0.1f, 2).Prio(55).Len(0.4f).Buzz(HapticKind.Light));
            Add(new CueDef(MessDrop, SoundBus.Care, 0.45f, Care + "dropLeather").Jitter(1.5f).Limit(0.1f, 2).Prio(50).Len(0.4f));
            Add(new CueDef(MessFlick, SoundBus.Care, 0.6f, "proc:swish").Jitter(1f).Limit(0.08f, 3).Prio(60).Len(0.4f).Buzz(HapticKind.Medium));
            Add(new CueDef(Prune, SoundBus.Care, 0.45f, Care + "knifeSlice", Care + "knifeSlice2").Jitter(1.5f).Limit(0.12f, 2).Prio(55).Len(0.4f).Buzz(HapticKind.Selection));
            Add(new CueDef(Rain, SoundBus.Care, 0.35f, Care + "drop_002", Care + "drop_003").Jitter(2f).Limit(0.15f, 3).Prio(45).Len(0.3f));
            Add(new CueDef(ShapeChannel, SoundBus.Care, 0.5f, Care + "footstep_grass_000", Care + "footstep_grass_001", Care + "footstep_grass_002").Jitter(1.5f, 0.12f).Limit(0.09f, 3).Prio(55).Len(0.3f).Buzz(HapticKind.Selection));
            Add(new CueDef(ShapeOutcrop, SoundBus.Care, 0.55f, Care + "impactMining_000", Care + "impactMining_001", Care + "impactMining_002").Jitter(1f).Limit(0.2f, 2).Prio(60).Len(0.5f).Buzz(HapticKind.Medium));
            Add(new CueDef(ShapeFail, SoundBus.Care, 0.3f, Care + "tick_002").Limit(0.6f, 1).Prio(40).Len(0.2f).Buzz(HapticKind.Warning));
            Add(new CueDef(TapGlass, SoundBus.Care, 0.45f, Care + "impactGlass_light_000", Care + "impactGlass_light_001", Care + "impactGlass_light_002").Jitter(1f).Limit(0.12f, 2, true).Prio(50).Len(0.4f).Buzz(HapticKind.Light));
            Add(new CueDef(TapPerson, SoundBus.Care, 0.4f, Care + "pluck_001", Care + "pluck_002").Jitter(2f).Limit(0.15f, 1, true).Prio(50).Len(0.3f).Buzz(HapticKind.Light));
            Add(new CueDef(Discovery, SoundBus.Care, 0.55f, Care + "maximize_003").Limit(1f, 1).Prio(80).Len(0.8f).Buzz(HapticKind.Success));

            // World and era moments. The moment's own cues duck the music (era-moment.md §10).
            Add(new CueDef(Cheer, SoundBus.World, 0.7f, "proc:cheer").Limit(1.5f, 1).Prio(90).Len(1.5f).Buzz(HapticKind.Success).Ducks());
            Add(new CueDef(CheerSmall, SoundBus.World, 0.45f, "proc:cheer").Jitter(1f).Limit(1f, 1).Prio(70).Len(1f).Ducks());
            Add(new CueDef(Flute, SoundBus.Moment, 0.6f, Moment + "flute_drum_tag").Limit(5f, 1).Prio(90).Len(15.5f).Ducks());
            Add(new CueDef(Drum, SoundBus.Moment, 0.65f, Moment + "impactSoft_heavy_000", Moment + "impactSoft_heavy_001").Jitter(0.5f).Limit(0.25f, 2).Prio(75).Len(0.6f).Buzz(HapticKind.Light).Ducks());
            Add(new CueDef(TuneUp, SoundBus.Moment, 0.55f, Moment + "jingles_PIZZI01").Limit(2f, 1).Prio(80).Len(1f).Ducks());
            Add(new CueDef(Fiddle, SoundBus.Moment, 0.6f, Moment + "jingles_PIZZI07", Moment + "jingles_PIZZI03").Limit(1.5f, 1).Prio(85).Len(1.4f).Ducks());
            // The wave's knocks: one clip pitched up the pentatonic scale per lot (MomentCues adds the semitone), so no jitter.
            Add(new CueDef(Knock, SoundBus.Moment, 0.6f, Moment + "impactWood_light_000").Limit(0f, 4, true).Prio(80).Len(0.4f).Buzz(HapticKind.Selection).Ducks());
            Add(new CueDef(Hammer, SoundBus.Moment, 0.55f, Moment + "impactPlank_medium_000").Limit(0f, 4, true).Prio(80).Len(0.4f).Buzz(HapticKind.Selection).Ducks());
            Add(new CueDef(Bell, SoundBus.Moment, 0.7f, Moment + "impactBell_heavy_000").Limit(2f, 1).Prio(90).Len(2.5f).Buzz(HapticKind.Medium).Ducks());
            Add(new CueDef(CobbleTap, SoundBus.Moment, 0.4f, Moment + "footstep_concrete_000", Moment + "footstep_concrete_001", Moment + "footstep_concrete_002").Jitter(2f, 0.15f).Limit(0.07f, 3).Prio(60).Len(0.3f).Ducks());
            Add(new CueDef(Chord, SoundBus.Moment, 0.75f, Moment + "guitar_chord").Limit(3f, 1).Prio(95).Len(2f).Buzz(HapticKind.Success).Ducks());
            Add(new CueDef(Chime, SoundBus.Moment, 0.55f, Moment + "glass_005").Limit(1f, 1).Prio(80).Len(1f).Ducks());
            Add(new CueDef(PauseNote, SoundBus.Moment, 0.45f, Moment + "pan_flute_d5").Limit(3f, 1).Prio(80).Len(6f).Ducks());

            // Ambience loops (levels come from Ambience.Targets; the cue volume is the layer's full level).
            Add(new CueDef(AmbCalm, SoundBus.Ambience, 0.35f, Amb + "calm_wind_loop").Looped().Prio(10).Len(40f));
            Add(new CueDef(AmbBirds, SoundBus.Ambience, 0.4f, Amb + "birds_day_loop").Looped().Prio(10).Len(28.5f));
            Add(new CueDef(AmbCrickets, SoundBus.Ambience, 0.35f, Amb + "crickets_night_loop").Looped().Prio(10).Len(11f));
            Add(new CueDef(AmbRain, SoundBus.Ambience, 0.5f, Amb + "rain_loop").Looped().Prio(10).Len(88f));
            Add(new CueDef(AmbWind, SoundBus.Ambience, 0.5f, Amb + "calm_wind_loop").Looped().Prio(10).Len(40f));
            return d;
        }
    }
}
