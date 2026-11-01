using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The motion tokens of spec §1 as code, using only what limits.md allows: transitions on opacity, translate, scale and colour,
    /// no keyframes, no loops, no filter transition. <see cref="Show"/> puts an element up, waits a frame and then removes the
    /// off class, because done in one frame it jumps; <see cref="Hide"/> adds the off class and takes the element down after the
    /// token's time. Each off class goes with a class in Glass.uss that holds the timing.
    /// </summary>
    public static class GlassMotion
    {
        public const string FadeClass = "kg-fade", FadeOff = "kg-fade--off";
        public const string SlideClass = "kg-slide", SlideOff = "kg-slide--off";
        public const string AwayClass = "kg-away", AwayOff = "kg-away--off";
        public const string CaptionClass = "kg-caption-fade", CaptionOff = "kg-caption-fade--off";
        public const string TurnClass = "kg-turn", TurnOff = "kg-turn--off";

        public const int PressMs = 80, FadeMs = 160, SlideMs = 280, AwayMs = 320, CaptionInMs = 400, CaptionOutMs = 600, TurnMs = 240, SwitchMs = 160, GlowStepMs = 600;
        /// <summary>The glow's 3 steps, chained in C#: the only loop-like thing on screen, and it stops by itself.</summary>
        public const int GlowSteps = 3;

        /// <summary>
        /// On: everything lands at once. Play tests turn this on with <see cref="GameUiPanel.AcceptInputUnfocused"/>: they tap controls
        /// by where they are, and a sheet on its way up would take the tap at the wrong place. Never on in a build.
        /// </summary>
        public static bool Instant { get; set; }

        /// <summary>Makes <paramref name="e"/> visible, then lets it transition in from <paramref name="offClass"/> a frame later.</summary>
        public static void Show(VisualElement e, string offClass, Action done = null, int ms = 0)
        {
            e.style.display = DisplayStyle.Flex;
            if (Instant) { Land(e, offClass); done?.Invoke(); return; }
            e.AddToClassList(offClass);
            e.schedule.Execute(() => e.RemoveFromClassList(offClass)).ExecuteLater(16);
            if (done != null) e.schedule.Execute(done).ExecuteLater(ms + 40);
        }

        /// <summary>Plays the off transition, then takes <paramref name="e"/> down (display none) after <paramref name="ms"/>.</summary>
        public static void Hide(VisualElement e, string offClass, int ms, Action done = null)
        {
            if (Instant || e.panel == null)
            {
                e.AddToClassList(offClass);
                e.style.display = DisplayStyle.None;
                done?.Invoke();
                return;
            }
            e.AddToClassList(offClass);
            e.schedule.Execute(() =>
            {
                if (!e.ClassListContains(offClass)) return; // shown again meanwhile
                e.style.display = DisplayStyle.None;
                done?.Invoke();
            }).ExecuteLater(ms + 20);
        }

        static void Land(VisualElement e, string offClass) => e.RemoveFromClassList(offClass);

        /// <summary>The new-tool glow: <paramref name="glowClass"/> on and off, <see cref="GlowSteps"/> steps of 600 ms, then still.</summary>
        public static void Glow(VisualElement e, string glowClass, Action done = null)
        {
            if (Instant) { done?.Invoke(); return; }
            for (int i = 0; i < GlowSteps; i++)
            {
                bool on = i % 2 == 0;
                e.schedule.Execute(() => e.EnableInClassList(glowClass, on)).ExecuteLater(i * GlowStepMs);
            }
            e.schedule.Execute(() =>
            {
                e.RemoveFromClassList(glowClass);
                done?.Invoke();
            }).ExecuteLater(GlowSteps * GlowStepMs);
        }
    }

    /// <summary>The spec's type rules that USS can't hold.</summary>
    public static class GlassType
    {
        /// <summary>USS px per pt on the 393 pt phone the boards are drawn for.</summary>
        public const float Pt = 2.75f;
        /// <summary>Body line pitch: 1.35 of the 15 pt size, in panel px.</summary>
        public const float BodyPitch = 15f * 1.35f * Pt;

        /// <summary>A px line-height tag: USS has no such property, and a percent tag is a percent of the font's own line, which gives 1.79.</summary>
        public static string Lines(string text, float sizePt = 15f, float ratio = 1.35f)
            => $"<line-height={(sizePt * ratio * Pt).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}px>{text}";
    }
}
