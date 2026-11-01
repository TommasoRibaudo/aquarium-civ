using AquariumCiv.Audio;

namespace AquariumCiv.GameUI
{
    /// <summary>What a control means to the ear (spec §1): components name a role, never a clip.</summary>
    public enum UiRole
    {
        None, Tap, Toggle, Open, Close, Back, Confirm, Ask, ScrubTick, CardIn, CardOut, BookOpen, BookClose, BookPage, ShowMe, Refused, NewTool, Found,
    }

    /// <summary>
    /// The spec's sound and haptics table in one place: a role plays a <see cref="CueLibrary"/> cue through <see cref="Sfx"/>.
    /// Haptics live on the cues, so the one role whose cue has none in the moments (New tool: Chime) adds its own buzz here.
    /// Nothing is carried by sound alone, and the Sound and Haptics settings apply downstream.
    /// </summary>
    public static class UiSound
    {
        public static string Cue(UiRole role) => role switch
        {
            UiRole.Tap => CueLibrary.UiTap,
            UiRole.Toggle => CueLibrary.UiToggle,
            UiRole.Open => CueLibrary.UiOpen,
            UiRole.Close => CueLibrary.UiClose,
            UiRole.Back => CueLibrary.UiBack,
            UiRole.Confirm => CueLibrary.UiConfirm,
            UiRole.Ask => CueLibrary.UiQuestion,
            UiRole.ScrubTick => CueLibrary.UiScroll,
            UiRole.CardIn => CueLibrary.CardArrive,
            UiRole.CardOut => CueLibrary.CardDismiss,
            UiRole.BookOpen => CueLibrary.BookOpen,
            UiRole.BookClose => CueLibrary.BookClose,
            UiRole.BookPage => CueLibrary.BookPage,
            UiRole.ShowMe => CueLibrary.ShowMe,
            UiRole.Refused => CueLibrary.ShapeFail,
            UiRole.NewTool => CueLibrary.Chime,
            UiRole.Found => CueLibrary.Discovery,
            _ => null,
        };

        /// <summary>The buzz the spec gives a role. All but New tool come with the cue itself.</summary>
        public static HapticKind Haptic(UiRole role) => role switch
        {
            UiRole.Tap or UiRole.Toggle => HapticKind.Selection,
            UiRole.Confirm or UiRole.CardIn => HapticKind.Light,
            UiRole.Refused => HapticKind.Warning,
            UiRole.NewTool or UiRole.Found => HapticKind.Success,
            _ => HapticKind.None,
        };

        public static void Play(UiRole role)
        {
            Sfx.Play(Cue(role));
            if (role == UiRole.NewTool) Sfx.Haptic(HapticKind.Success); // the moments' chime stays silent to the hand
        }
    }
}
