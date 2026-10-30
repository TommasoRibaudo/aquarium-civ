using UnityEngine;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// Whether the phone is in silent mode. Android: the ringer is silent or vibrate (media volume ignores the ringer
    /// by design, so the music has to ask). iOS: nothing to ask here; Unity's default audio session category (Ambient)
    /// already mutes our audio with the silent switch, as long as Player Settings keep "Prepare iOS for Recording"
    /// off and nobody switches the category to Playback. In the Editor and elsewhere: never silent.
    /// </summary>
    public static class DeviceSilent
    {
        const float RecheckSeconds = 2f;
#if UNITY_ANDROID && !UNITY_EDITOR
        static float checkedAt = -100f;
        static bool cached;
#endif

        public static bool IsSilent
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (Time.unscaledTime - checkedAt >= RecheckSeconds)
                {
                    checkedAt = Time.unscaledTime;
                    cached = QueryAndroid();
                }
                return cached;
#else
                return false;
#endif
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool QueryAndroid()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var audio = activity.Call<AndroidJavaObject>("getSystemService", "audio");
                // AudioManager.RINGER_MODE_SILENT = 0, VIBRATE = 1, NORMAL = 2
                return audio.Call<int>("getRingerMode") != 2;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("DeviceSilent: ringer mode query failed: " + e.Message);
                return false;
            }
        }
#endif
    }
}
