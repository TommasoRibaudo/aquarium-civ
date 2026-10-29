using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>Floating world labels (tapped names) and the welcome-back banner.</summary>
    public class HudToast : MonoBehaviour
    {
        struct Toast { public string text; public Vector3 world; public float until; }

        static HudToast instance;
        readonly List<Toast> toasts = new();
        string banner;
        float bannerUntil;

        void Awake()
        {
            instance = this;
            SimClock.Instance.Returned += OnReturned; // SimClock wakes first (execution order) and returns in Start
        }

        void OnReturned(double hours, float years)
        {
            if (hours < 0.05) return;
            string away = hours < 1 ? $"{hours * 60:0} minutes" : $"{hours:0.#} hours";
            ShowBanner($"Welcome back, Keeper. You were away {away}: {years:0.#} years passed in the glass.", 5f);
        }

        public static void Show(string text, Vector3 world, float seconds = 2f)
        {
            if (instance == null) return;
            instance.toasts.RemoveAll(t => t.world == world);
            instance.toasts.Add(new Toast { text = text, world = world, until = Time.unscaledTime + seconds });
        }

        public static void ShowBanner(string text, float seconds = 3f)
        {
            if (instance == null) return;
            instance.banner = text;
            instance.bannerUntil = Time.unscaledTime + seconds;
        }

        void OnGUI()
        {
            ProtoGui.Begin();
            toasts.RemoveAll(t => Time.unscaledTime > t.until);
            foreach (var t in toasts)
            {
                var p = ProtoGui.WorldToCanvas(t.world);
                var size = GUI.skin.box.CalcSize(new GUIContent(t.text));
                float x = Mathf.Clamp(p.x - size.x / 2 - 6, 8, ProtoGui.Width - size.x - 20); // never clipped at the edges
                GUI.Box(new Rect(x, p.y - size.y - 6, size.x + 12, size.y + 4), t.text);
            }
            if (banner != null && Time.unscaledTime < bannerUntil)
            {
                float safeTop = (Screen.height - Screen.safeArea.yMax) / ProtoGui.Scale; // notch / status bar
                var r = new Rect(30, Mathf.Max(90, safeTop + 24), ProtoGui.Width - 60, 90);
                GUI.Box(r, banner, ProtoGui.Box);
            }
        }
    }
}
