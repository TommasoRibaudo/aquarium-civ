namespace AquariumCiv.Lighting
{
    /// <summary>
    /// #162: how much a pane (or the frame) fades when the camera is close. Pure and smooth in both inputs, so zooming
    /// and orbiting never pop. Panes facing away from the camera (the back of the box) never fade.
    /// </summary>
    public static class NearGlassFade
    {
        /// <summary>Distance ratio (camera distance / its distance at full-box zoom): fade starts at Start, is complete at End.</summary>
        public const float Start = 0.75f, End = 0.40f;
        /// <summary>A pane is "near" once the camera sees its outside face: facing cosine from Edge (edge-on, no fade) to Face (fully fading).</summary>
        public const float Edge = 0.0f, Face = 0.35f;
        /// <summary>Subtle: a pane never fades fully to nothing (the frame is a little more visible than panes).</summary>
        public const float MaxPaneFade = 0.9f, MaxFrameFade = 0.7f;
        /// <summary>Clean mode (grime to wipe) brings the glass back: the transition rate of <see cref="Follow"/>.</summary>
        public const float FollowRate = 6f;

        static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
        static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>0..1 from the camera distance alone (0 = far, 1 = close).</summary>
        public static float Closeness(float distanceRatio) => Smooth((Start - distanceRatio) / (Start - End));

        /// <summary>0..1 from the angle alone: <paramref name="facing"/> = dot(pane outward normal, direction from the box to the camera), flattened to the ground plane.</summary>
        public static float Nearness(float facing) => Smooth((facing - Edge) / (Face - Edge));

        /// <summary>
        /// Fade amount 0 (fully visible) .. max (faded) for one pane. <paramref name="cleanMode"/> forces 0.
        /// <paramref name="forced"/> (debug panel): a value 0..1 overrides the distance part, negative = off.
        /// </summary>
        public static float Amount(float distanceRatio, float facing, bool isFrame = false, bool cleanMode = false, float forced = -1f)
        {
            if (cleanMode) return 0f;
            float close = forced >= 0f ? Clamp01(forced) : Closeness(distanceRatio);
            return close * Nearness(facing) * (isFrame ? MaxFrameFade : MaxPaneFade);
        }

        /// <summary>Opacity multiplier for the pane: 1 - Amount.</summary>
        public static float Opacity(float distanceRatio, float facing, bool isFrame = false, bool cleanMode = false, float forced = -1f)
            => 1f - Amount(distanceRatio, facing, isFrame, cleanMode, forced);

        /// <summary>Per-pane smoothing for the shown value, so a clean-mode toggle or a debug force fades instead of popping.</summary>
        public static float Follow(float shown, float target, float dt)
            => shown + (target - shown) * (1f - (float)System.Math.Exp(-FollowRate * (dt < 0f ? 0f : dt)));
    }
}
