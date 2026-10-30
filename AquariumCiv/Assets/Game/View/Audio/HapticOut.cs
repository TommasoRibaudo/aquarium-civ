using AquariumCiv.Audio;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// Sends one buzz to the phone (#96), plugin-free where it can be:
    /// <list type="bullet">
    /// <item>Android: the system Vibrator through JNI, <c>VibrationEffect.createWaveform</c> on API 26+ (soft amplitudes),
    /// a plain <c>vibrate(ms)</c> below that; <see cref="Handheld.Vibrate"/> if both fail (and its reference makes Unity add
    /// the VIBRATE permission).</item>
    /// <item>iOS: <c>UIImpactFeedbackGenerator</c> / <c>UINotificationFeedbackGenerator</c> through the tiny
    /// <c>Plugins/iOS/AqHaptics.mm</c>; nothing heavier.</item>
    /// <item>Editor and desktop: a log line only.</item>
    /// </list>
    /// </summary>
    public static class HapticOut
    {
        /// <summary>The last buzz sent, for the debug panel ("Light (ios ImpactLight, android 12 ms)").</summary>
        public static string Last { get; private set; } = "";

        public static void Fire(HapticKind kind, HapticPattern p)
        {
            Last = $"{kind} (ios {p.ios}, android {p.AndroidTotalMs} ms)";
#if UNITY_EDITOR
            if (AudioDirector.LogPlays) Debug.Log("[Haptic] " + Last);
#elif UNITY_ANDROID
            Android(p);
#elif UNITY_IOS
            AqHaptic((int)p.ios);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk = -1;
        static bool failed;

        static void Android(HapticPattern p)
        {
            if (failed) { if (p.AndroidTotalMs >= 18) Handheld.Vibrate(); return; }
            try
            {
                if (vibrator == null)
                {
                    using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var act = up.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using var ver = new AndroidJavaClass("android.os.Build$VERSION");
                    sdk = ver.GetStatic<int>("SDK_INT");
                }
                if (vibrator == null) { failed = true; return; }
                if (sdk >= 26)
                {
                    using var ve = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = ve.CallStatic<AndroidJavaObject>("createWaveform", p.androidTimings, p.androidAmplitudes, -1);
                    vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", p.AndroidTotalMs);
            }
            catch (System.Exception e)
            {
                failed = true;
                Debug.LogWarning("[Haptic] Android vibrator unavailable, falling back to Handheld.Vibrate: " + e.Message);
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void AqHaptic(int kind);
#endif
    }
}
