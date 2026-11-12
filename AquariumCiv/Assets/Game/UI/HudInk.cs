using UnityEngine;

namespace AquariumCiv.GameUI
{
    /// <summary>#604: the box name and the year line have no pill and no outline. Their ink eases between dark (bright day) and
    /// light with a soft shadow (dusk, night, dark winter) as the daylight changes. Pure maths; <see cref="GameHud"/> applies it.</summary>
    public static class HudInk
    {
        /// <summary>The scene's daylight (0 night .. 1 full day), written by DayLightDriver each frame; 1 when nothing drives the light.</summary>
        public static float Daylight = 1f;

        // The ink is either dark or light, never a mid-tone that vanishes on a mid-tone dusk sky: a running dusk sat at 0.71-0.75
        // daylight for a long stretch and drew grey-brown on terracotta (tester, fb7). The switch has hysteresis, so daylight
        // hovering at the edge doesn't flip it back and forth; only the switch itself eases (EaseSeconds).
        public const float ToLightBelow = 0.70f, ToDarkAbove = 0.74f, EaseSeconds = 1f;
        public static readonly Color Dark = new(0.239f, 0.165f, 0.118f, 1f);   // #3D2A1E, var(--ink)
        public static readonly Color Light = new(0.953f, 0.914f, 0.839f, 1f);  // #F3E9D6
        public const float ShadowAlpha = 0.6f;

        /// <summary>0 = dark ink (bright day), 1 = light ink (dusk to dark), with no history: light below the middle of the band.</summary>
        public static float Target(float daylight) => daylight < (ToLightBelow + ToDarkAbove) * 0.5f ? 1f : 0f;

        /// <summary>Like <see cref="Target(float)"/>, but inside the band it keeps the side the ink is on (or heading to):
        /// <paramref name="current"/> is the eased lightness now (0..1).</summary>
        public static float Target(float daylight, float current)
        {
            if (daylight < ToLightBelow) return 1f;
            if (daylight > ToDarkAbove) return 0f;
            return current >= 0.5f ? 1f : 0f;
        }

        /// <summary>One frame of the ease: the full swing takes <see cref="EaseSeconds"/>, never a snap.</summary>
        public static float Step(float current, float target, float dt) => Mathf.MoveTowards(current, target, dt / EaseSeconds);

        public static Color Ink(float lightness) => Color.Lerp(Dark, Light, Mathf.Clamp01(lightness));

        /// <summary>The soft shadow only grows in as the ink goes light (dark ink on a bright day needs none).</summary>
        public static Color Shadow(float lightness) => new(0f, 0f, 0f, ShadowAlpha * Mathf.Clamp01(lightness));

        /// <summary>The shadow with an off-home halo: dark ink gets a soft pale glow (<paramref name="halo"/> 0..1) so it reads over the glass rails
        /// and the room; light ink keeps its dark shadow.</summary>
        public static Color Shadow(float lightness, float halo)
        {
            float l = Mathf.Clamp01(lightness), h = Mathf.Clamp01(halo) * (1f - l);
            if (h <= l) return Shadow(lightness);
            return new Color(Light.r, Light.g, Light.b, HaloAlpha * h);
        }

        public const float HaloAlpha = 0.85f;
    }
}
