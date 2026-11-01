using System;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// Settings and the sheets behind it (#95, board 3.8, first-session-and-check-in §6). Settings stands at 59 % and scrolls, snapped to whole
    /// rows: sound with its three volumes (a live sample on release), how often music plays, haptics, notifications, then the box (rename,
    /// reset, the shelf while Stories has its chip), then licences, feedback in test builds and the version. Everything is written to
    /// <see cref="GameSettings"/> as it changes, so it stands after a restart.
    /// Names: <c>settings-sound</c>, <c>settings-silent</c>, <c>settings-music</c>, <c>settings-effects</c>, <c>settings-ambience</c>,
    /// <c>settings-music-plays</c> (its tabs <c>tab-off</c>, <c>tab-rare</c>, <c>tab-normal</c>), <c>settings-haptics</c>, <c>settings-notifications</c>,
    /// <c>settings-rename</c>, <c>settings-reset</c>, <c>settings-shelf</c>, <c>settings-licences</c>, <c>settings-feedback</c>, <c>settings-version</c>,
    /// <c>licences-back</c>, <c>licences-list</c>, <c>reset-sheet</c>, <c>reset-keep</c>, <c>reset-confirm</c>, and <c>rename-*</c> (<see cref="GlassNameSheet"/>).
    /// </summary>
    public class SettingsSheets
    {
        public const string Licences = "licences", Rename = "rename", Reset = "reset";

        /// <summary>From docs/assets-log.md, grouped by source. Attribution is asked for on the music, so it stands by name.</summary>
        public static readonly string[] LicenceRows =
        {
            "KayKit assets, Kay Lousberg, CC0",
            "Kenney assets, CC0",
            "Calm Piano 1, The Cynic Project, CC0",
            "Forget Me Not, Kistol, CC0",
            "Rain, birds, crickets and wind: kresiek-the-furry, isaiah658, wolfgang, thimras, CC0",
            "Flute, guitar and pan flute: nicholasjudy567, antumdeluge, pattingthestar, CC0",
            "Lora, SIL OFL 1.1",
            "PT Serif, SIL OFL 1.1",
        };

        readonly GameHud hud;
        readonly Action<GlassSheet> tightened;
        GlassToggle sound, haptics, notifications;
        GlassSlider music, effects, ambience;
        GlassTabs plays;
        VisualElement silent;
        GlassRowButton renameRow, resetRow;
        Label resetBody, resetTitle;
        GlassNameSheet rename;
        int rerolls;

        /// <summary>The Shelf row (<see cref="GameHud.SetShelfRow"/>).</summary>
        public GlassRowButton ShelfRow { get; private set; }
        public GlassScroll Scroll { get; private set; }

        public SettingsSheets(GameHud hud, Action<GlassSheet> tightened)
        {
            this.hud = hud;
            this.tightened = tightened;
            BuildSettings();
            BuildLicences();
            BuildRename();
            BuildReset();
        }

        static SimCore Box => GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;
        static string Place(string unnamed)
        {
            var name = Box?.State.settlementName;
            return string.IsNullOrEmpty(name) ? unnamed : name;
        }

        // ---------------- settings ----------------
        void BuildSettings()
        {
            var sheet = hud.Sheet(GameHud.Settings, "Settings", SheetHeight.Tall);
            Scroll = new GlassScroll { name = "settings-scroll", SnapToRows = true };
            Scroll.View.contentContainer.AddToClassList("kg-rows");
            sheet.Add(Scroll);

            var soundRow = Row(Scroll);
            soundRow.Add(new GlassIcon("sound", "kg-row-icon"));
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.AddToClassList("kg-row-label");
            words.Add(Text("Sound", "type-body"));
            silent = new VisualElement { name = "settings-silent", pickingMode = PickingMode.Ignore };
            silent.AddToClassList("kg-row-notice");
            silent.Add(new GlassIcon("alert", "kg-icon--small", "kg-row-notice-mark"));
            var why = Text("Your phone is on silent, so you won't hear this.", "type-note", "kg-row-notice-words");
            silent.Add(why);
            silent.style.display = DisplayStyle.None;
            words.Add(silent);
            soundRow.Add(words);
            sound = new GlassToggle(GameSettings.Sound, v => GameSettings.Sound = v) { name = "settings-sound" };
            soundRow.Add(sound);

            music = Volume("settings-music", "Music", v => GameSettings.MusicVolume = v, () => Sample(CueLibrary.Chime, GameSettings.MusicVolume));
            effects = Volume("settings-effects", "Effects", v => GameSettings.SfxVolume = v, () => Sample(CueLibrary.UiConfirm, 1f));
            ambience = Volume("settings-ambience", "Ambience", v => GameSettings.AmbienceVolume = v, () => Sample(CueLibrary.UiToggle, GameSettings.AmbienceVolume));

            var playsRow = Row(Scroll);
            playsRow.Add(Text("Music plays", "type-body", "kg-row-tag"));
            plays = new GlassTabs("Off", "Rare", "Normal") { name = "settings-music-plays" };
            plays.Changed += i => GameSettings.MusicFrequency = (MusicFrequency)i;
            playsRow.Add(plays);

            haptics = Switch("settings-haptics", "haptic", "Haptics", GameSettings.Haptics, v =>
            {
                GameSettings.Haptics = v;
                if (v) Sfx.Haptic(HapticKind.Light); // the toggle's own tick came while they were off
            });
            notifications = Switch("settings-notifications", "bell", "Notifications", GameSettings.Notifications, OnNotifications);

            Scroll.Add(Heading("Your box"));
            renameRow = Link("settings-rename", "pencil", "Rename your box", () => hud.Open(Rename));
            resetRow = Link("settings-reset", "reset", "Reset your box", () => hud.Open(Reset));
            ShelfRow = Link("settings-shelf", "shelf", "The shelf", () => { hud.CloseSheet(); hud.ShelfTapped?.Invoke(); });
            ShelfRow.style.display = DisplayStyle.None;

            Scroll.Add(Heading("About"));
            Link("settings-licences", "book", "Licences", () => hud.Open(Licences), UiRole.Open);
            if (FeedbackUI.InTestBuild) Link("settings-feedback", "feedback", "Send feedback", hud.OpenFeedback, UiRole.Tap);
            var version = Text($"Aquarium Civ {Application.version}", "type-note", "kg-row-end", GlassScroll.RowClass);
            version.name = "settings-version";
            Scroll.Add(version);
        }

        VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("kg-row");
            row.AddToClassList(GlassScroll.RowClass);
            parent.Add(row);
            return row;
        }

        GlassSlider Volume(string name, string label, Action<float> set, Action sample)
        {
            var row = Row(Scroll);
            row.Add(Text(label, "type-body", "kg-row-tag"));
            var slider = new GlassSlider(0f, set) { name = name };
            slider.Released += () => { GameSettings.Commit(); sample(); };
            row.Add(slider);
            return slider;
        }

        GlassToggle Switch(string name, string icon, string label, bool value, Action<bool> set)
        {
            var row = Row(Scroll);
            row.Add(new GlassIcon(icon, "kg-row-icon"));
            row.Add(Text(label, "type-body", "kg-row-label"));
            var toggle = new GlassToggle(value, set) { name = name };
            row.Add(toggle);
            return toggle;
        }

        GlassRowButton Link(string name, string icon, string label, Action tapped, UiRole role = UiRole.Open)
        {
            var row = new GlassRowButton(icon, label, tapped, role) { name = name };
            row.AddToClassList(GlassScroll.RowClass);
            Scroll.Add(row);
            return row;
        }

        static Label Heading(string words) => Text(words, "type-small-head", "kg-row-head", GlassScroll.RowClass);

        static Label Text(string words, params string[] classes)
        {
            var l = new Label(words) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        /// <summary>A short sound at what the slider was just left on. The Effects bus is the slider's own; the other two have nothing of
        /// their own to play for sure (music plays now and then), so a cue stands in at their level.</summary>
        static void Sample(string cue, float level) => Sfx.Play(cue, 0f, level);

        void OnNotifications(bool on)
        {
            if (!on) { GameSettings.Notifications = false; return; }
            var clock = GameClock.Instance;
            if (clock == null) { notifications.On = false; return; }
            clock.AskNotifications(granted =>
            {
                notifications.On = granted;
                if (!granted) hud.Toast("Turn them on in your phone's settings, and tap here again.");
            });
        }

        // ---------------- licences ----------------
        void BuildLicences()
        {
            var sheet = hud.Sheet(Licences, "Licences", SheetHeight.Licences, false);
            var back = new GlassButton("", () => hud.Open(GameHud.Settings), ButtonKind.Icon, UiRole.Back) { name = "licences-back", Icon = "back" };
            sheet.Q(className: "kg-sheet-head").Add(back);
            var scroll = new GlassScroll { name = "licences-list", SnapToRows = true };
            scroll.View.contentContainer.AddToClassList("kg-rows");
            foreach (var line in LicenceRows)
            {
                var row = Text(line, "type-body", "kg-list-row", GlassScroll.RowClass);
                scroll.Add(row);
            }
            sheet.Add(scroll);
        }

        // ---------------- rename ----------------
        void BuildRename()
        {
            rename = new GlassNameSheet(Rename, "Rename your box", null, hud.CloseSheet, OnAnother, OnKeep);
            hud.AddSheet(Rename, rename.Scrim);
            tightened(rename.Sheet);
        }

        void OnAnother()
        {
            if (Box != null) rename.Field.value = SettlementNaming.Suggest(Box.State.seed, ++rerolls);
        }

        void OnKeep()
        {
            var p = SettlementNaming.Check(rename.Field.value, out string clean);
            if (p != NameProblem.None) { rename.Refuse(p); return; }
            var box = Box;
            if (box == null) return;
            box.SetSettlementName(clean);
            GameClock.Instance.Save();
            hud.CloseSheet();
            UiSound.Play(UiRole.Confirm);
            hud.RefreshNow();
            hud.Toast($"Welcome to {clean}.");
        }

        // ---------------- reset ----------------
        void BuildReset()
        {
            var sheet = hud.Sheet(Reset, "Reset your box?", SheetHeight.Standard, false);
            sheet.Surface.KeepFlat = true; // question 32: frost would come out lighter over the scrim, the copy doesn't hold it
            hud.ScrimOf(Reset).Dim = true;
            resetTitle = sheet.Q<Label>("kg-sheet-title");
            resetBody = Text("", "type-body", "kg-reset-body");
            resetBody.name = "reset-body";
            sheet.Add(resetBody);
            var acts = new VisualElement();
            acts.AddToClassList("kg-card-acts");
            var confirm = new GlassButton("Reset box", DoReset, ButtonKind.Quiet) { name = "reset-confirm" };
            confirm.Marked("alert", true);
            acts.Add(confirm);
            acts.Add(new GlassButton("Keep playing", hud.CloseSheet, ButtonKind.Primary, UiRole.Confirm) { name = "reset-keep" });
            sheet.Add(acts);
            tightened(sheet);
        }

        /// <summary>The body says what goes, in the box's own words.</summary>
        public static string ResetWords(string place, int years) => years < 1
            ? $"{place} and its people will be gone. This can't be undone."
            : $"{place}, its {years} {(years == 1 ? "year" : "years")} and its people will be gone. This can't be undone.";

        void DoReset()
        {
            hud.CloseSheet();
            GameClock.Instance?.ResetBox();
            hud.RefreshNow();
        }

        // ---------------- the HUD's calls ----------------
        /// <summary>A sheet of ours opens: it shows what's true now.</summary>
        public void Opening(string id)
        {
            if (id == GameHud.Settings) ShowValues();
            if (id == Rename)
            {
                rerolls = 0;
                rename.Field.value = Place("");
                rename.Say("");
            }
            if (id == Reset)
            {
                string place = Place("Your box");
                resetTitle.text = $"Reset {place}?";
                resetBody.text = GlassType.Lines(ResetWords(place, Box != null ? Mathf.FloorToInt(Box.State.Year) : 0));
            }
        }

        void ShowValues()
        {
            sound.On = GameSettings.Sound;
            haptics.On = GameSettings.Haptics;
            notifications.On = GameSettings.Notifications;
            music.Value = GameSettings.MusicVolume;
            effects.Value = GameSettings.SfxVolume;
            ambience.Value = GameSettings.AmbienceVolume;
            plays.Select((int)GameSettings.MusicFrequency, false);
            string place = Place("your box");
            renameRow.Label = "Rename " + place;
            resetRow.Label = "Reset " + place;
            FollowSilent();
        }

        void FollowSilent()
        {
            var d = DeviceSilent.IsSilent ? DisplayStyle.Flex : DisplayStyle.None;
            if (silent.style.display != d) silent.style.display = d;
        }

        /// <summary>Each frame.</summary>
        public void Tick(Vector4 insets, float pad, bool settingsOpen)
        {
            rename.Ride(insets, pad);
            if (settingsOpen) FollowSilent();
        }

        /// <summary>A sheet closed: rename lands at once.</summary>
        public void Closing(Vector4 insets, float pad)
        {
            if (rename.Riding) rename.Land(insets, pad);
        }

        /// <summary>The refusal showing on Rename (tests).</summary>
        public string RenameProblem => rename.Problem;
        public bool RenameRiding => rename.Riding;
    }
}
