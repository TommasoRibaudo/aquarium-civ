// #655 (the phone runs hot) and #667 (scrolling isn't smooth): the frame rate follows what the player does. At rest the game asks for
// 30 fps (iOS's default was implicit: target -1); while a finger is down, a sheet is open, or for a moment after either, it asks for 60,
// so a flick and a scroll feel smooth, then it drops back to 30 to keep the phone cool. Only on the device: the Editor and the tests keep
// their own pace (a cap there would slow every play test).
using AquariumCiv.GameUI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.View
{
    public sealed class FrameRateDirector : MonoBehaviour
    {
        /// <summary>The frame rates: at rest and while the player touches or reads a sheet.</summary>
        public const int Rest = 30, Active = 60;
        /// <summary>How long 60 fps holds after the last touch or sheet (a flick's glide, a sheet's close).</summary>
        public const float Tail = 1f;

        /// <summary>The rate asked for now (0 before the director runs).</summary>
        public static int Target { get; private set; }
        /// <summary>The frost test sets the rate itself (its 30/60 toggle): while it is open the director leaves the rate alone.</summary>
        static bool Hold => FrostTestPanel.Instance != null;

        float lastActive = -99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor) return;
            var go = new GameObject("FrameRateDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<FrameRateDirector>();
        }

        void Awake()
        {
            QualitySettings.vSyncCount = 0; // targetFrameRate only counts with vSync off
            Set(Rest);
        }

        void Update()
        {
            if (Hold) { Target = Application.targetFrameRate; return; }
            if (Wants()) lastActive = Time.unscaledTime;
            Set(Time.unscaledTime - lastActive < Tail ? Active : Rest);
        }

        /// <summary>A finger (or the mouse) is down, or a sheet is open.</summary>
        static bool Wants()
        {
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed) return true;
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed) return true;
            var hud = GameHud.Instance;
            if (hud != null && hud.SheetOpen) return true;
            var fb = FeedbackUI.Instance;
            return fb != null && fb.IsOpen;
        }

        static void Set(int fps)
        {
            if (Target == fps && Application.targetFrameRate == fps) return;
            Target = fps;
            Application.targetFrameRate = fps;
        }
    }
}
