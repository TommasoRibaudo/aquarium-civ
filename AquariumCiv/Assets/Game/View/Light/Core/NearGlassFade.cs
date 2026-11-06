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
        /// <summary>#319: a pane close to the camera goes fully clear (the frame stays a little visible).</summary>
        public const float MaxPaneFade = 1f, MaxFrameFade = 0.7f;
        /// <summary>Clean mode (grime to wipe) brings the glass back: the transition rate of <see cref="Follow"/>.</summary>
        public const float FollowRate = 6f;

        /// <summary>#319: camera distance to the pane's plane, over the full-box fit distance: fade starts at PlaneStart, complete at PlaneEnd.</summary>
        public const float PlaneStart = 0.55f, PlaneEnd = 0.25f;

        static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
        static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>0..1 from the camera distance alone (0 = far, 1 = close).</summary>
        public static float Closeness(float distanceRatio) => Smooth((Start - distanceRatio) / (Start - End));

        /// <summary>#319: 0..1 from how near the camera is to the pane itself (plane distance / fit distance); NaN or huge = 0.</summary>
        public static float PlaneCloseness(float planeRatio) => Smooth((PlaneStart - planeRatio) / (PlaneStart - PlaneEnd));

        /// <summary>0..1 from the angle alone: <paramref name="facing"/> = dot(pane outward normal, direction from the box to the camera), flattened to the ground plane.</summary>
        public static float Nearness(float facing) => Smooth((facing - Edge) / (Face - Edge));

        /// <summary>The camp frame (decisions 2026-11-06; the user: no glass edge in a zoomed-in view). From this camera distance in
        /// (world units) the camera-side panes and the whole camera-side frame are gone. The fade starts at <see cref="CampFadeStart"/>,
        /// or just inside the whole-box distance when that is nearer (a wider screen fits the box from closer), so the whole box never fades.</summary>
        public const float CampFadeEnd = 34f, CampFadeStart = 46f, CampFadeStartShare = 0.95f;
        /// <summary>As close as a camp frame, a part counts as near from this facing (the corner post beside the near pane is 0.17-0.3 at the opening's yaw, wherever the camp).</summary>
        public const float CampFace = 0.12f;

        /// <summary>0..1 from the camera's distance to its pivot (<paramref name="distance"/>) and the whole-box distance (<paramref name="fit"/>):
        /// 1 from <see cref="CampFadeEnd"/> in. A camp frame sits there (dolls of 35-45 px are 26-33 units away at the opening's pitch, <see cref="CampFrame"/>).</summary>
        public static float CampCloseness(float distance, float fit)
        {
            float start = System.Math.Min(CampFadeStart, CampFadeStartShare * fit);
            float end = System.Math.Min(CampFadeEnd, start - 2f); // a wide screen (a tablet) fits the whole box from nearer still
            return Smooth((start - distance) / (start - end));
        }

        /// <summary>
        /// Fade amount 0 (fully visible) .. max (faded) for one pane. <paramref name="cleanMode"/> forces 0.
        /// <paramref name="forced"/> (debug panel): a value 0..1 overrides the distance part, negative = off.
        /// <paramref name="campCloseness"/> (<see cref="CampCloseness"/>, 0 = off): as close as a camp frame the near frame goes entirely,
        /// not only to <see cref="MaxFrameFade"/>, and a part counts as near from <see cref="CampFace"/>. The back of the box never fades.
        /// </summary>
        public static float Amount(float distanceRatio, float facing, bool isFrame = false, bool cleanMode = false, float forced = -1f, float planeRatio = float.MaxValue, float campCloseness = 0f)
        {
            if (cleanMode) return 0f;
            float camp = forced >= 0f ? 0f : Clamp01(campCloseness);
            float close = forced >= 0f ? Clamp01(forced) : System.Math.Max(System.Math.Max(Closeness(distanceRatio), PlaneCloseness(planeRatio)), camp);
            float face = Face + (CampFace - Face) * camp;
            float near = Smooth((facing - Edge) / (face - Edge));
            float max = isFrame ? MaxFrameFade + (1f - MaxFrameFade) * camp : MaxPaneFade;
            return close * near * max;
        }

        /// <summary>Opacity multiplier for the pane: 1 - Amount.</summary>
        public static float Opacity(float distanceRatio, float facing, bool isFrame = false, bool cleanMode = false, float forced = -1f, float planeRatio = float.MaxValue, float campCloseness = 0f)
            => 1f - Amount(distanceRatio, facing, isFrame, cleanMode, forced, planeRatio, campCloseness);

        /// <summary>Per-pane smoothing for the shown value, so a clean-mode toggle or a debug force fades instead of popping.</summary>
        public static float Follow(float shown, float target, float dt)
            => shown + (target - shown) * (1f - (float)System.Math.Exp(-FollowRate * (dt < 0f ? 0f : dt)));
    }
}
