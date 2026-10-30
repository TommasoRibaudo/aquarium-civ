using UnityEngine;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The player's settings (first-session-and-check-in §6): sound and haptics for now, kept in PlayerPrefs. Sound is
    /// applied through the listener volume; haptics is only remembered until something buzzes.
    /// </summary>
    public static class GameSettings
    {
        const string SoundKey = "set_sound", HapticsKey = "set_haptics";

        public static bool Sound
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); Apply(); }
        }

        public static bool Haptics
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set { PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Background music: how often it plays (Off / Rare / Normal) and its volume (0..1).</summary>
        public static MusicFrequency MusicFrequency
        {
            get => (MusicFrequency)PlayerPrefs.GetInt("set_music_freq", (int)MusicFrequency.Normal);
            set { PlayerPrefs.SetInt("set_music_freq", (int)value); PlayerPrefs.Save(); }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("set_music_vol", 0.6f);
            set { PlayerPrefs.SetFloat("set_music_vol", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static void Apply() => AudioListener.volume = Sound ? 1f : 0f;
    }
}
