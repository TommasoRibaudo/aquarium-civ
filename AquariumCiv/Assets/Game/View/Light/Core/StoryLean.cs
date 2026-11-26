using System;

namespace AquariumCiv.Lighting
{
    /// <summary>
    /// #783 (spec docs/design/783-story-camera.md, option A, signed off 2026-11-26): the maths of the lean-in at a held scene. Pure floats, so it
    /// runs in dotnet test. The view side is <c>HeldMomentLean</c>.
    /// </summary>
    public static class StoryLean
    {
        /// <summary>A standing doll is this share of the screen's height once the lean has landed (67 px on 1334, 128 px on 2556: the same share on
        /// every phone, the CampFrame rule).</summary>
        public const float ScenePxShare = 0.05f;
        /// <summary>The cast must fit inside this share of the screen across and up (the middle 80 % and 70 %).</summary>
        public const float FitAcross = 0.8f, FitUp = 0.7f;
        /// <summary>A scene's focus counts as framed (no pan needed) inside this band of the viewport.</summary>
        public const float FramedX0 = 0.25f, FramedX1 = 0.75f, FramedY0 = 0.3f, FramedY1 = 0.7f;
        /// <summary>A zoom this close to the player's own counts as the same one.</summary>
        public const float ZoomSame = 0.02f;
        /// <summary>Seconds between a lean's release (its scene ended) and the next one's start. A scene that starts within
        /// <see cref="ChainSeconds"/> of the release (the next of two queued) chains on the first: no cooldown, one way home.</summary>
        public const float CooldownSeconds = 20f, ChainSeconds = 1.5f;

        /// <summary>The target height of a standing doll on a screen <paramref name="screenH"/> px tall.</summary>
        public static float ScenePx(float screenH) => screenH * ScenePxShare;

        /// <summary>How much of the cast's spread (viewport half-extents <paramref name="ex"/> across and <paramref name="ey"/> up, measured
        /// round the scene's focus at the current zoom) fills the allowed frame: 1 = exactly fits now, 2 = needs twice the zoom value.</summary>
        public static float CastExtent(float ex, float ey) => Math.Max(Math.Max(0f, ex) / FitAcross, Math.Max(0f, ey) / FitUp);

        /// <summary>
        /// The camera zoom (smaller = closer) at which a standing doll is <see cref="ScenePx"/> tall. <paramref name="dollPx"/> is the doll's
        /// height now, at <paramref name="zoomNow"/> and <paramref name="pitchNow"/>; the lean raises the pitch to <paramref name="pitchLean"/>
        /// (a steeper look is a shorter doll), and a doll grows a little as the camera backs off (<see cref="CameraReach.DollScale"/>), both
        /// solved for. Widened when the cast does not fit (<paramref name="castExtent"/>, see <see cref="CastExtent"/>). Never above
        /// <paramref name="zoomNow"/> (a lean never zooms out) and never under <paramref name="minZoom"/> (the player's own floor).
        /// </summary>
        public static float ZoomFor(float dollPx, float screenH, float zoomNow, float castExtent, float pitchNow = 34f, float pitchLean = 52f,
            float minZoom = CameraReach.MinZoom, float dollScaleAmount = CameraReach.DollScaleUp)
        {
            if (dollPx <= 1e-3f || screenH <= 0f || zoomNow <= 0f) return zoomNow;
            double rad = Math.PI / 180.0;
            float steep = (float)(Math.Cos(Math.Max(pitchNow, pitchLean) * rad) / Math.Max(1e-3, Math.Cos(pitchNow * rad)));
            float pxAtLean = dollPx * Math.Min(1f, steep); // looking steeper only shortens the doll
            float want = ScenePx(screenH);
            float s0 = CameraReach.DollScale(zoomNow, dollScaleAmount);
            float z = zoomNow * pxAtLean / want;
            for (int i = 0; i < 5; i++) z = zoomNow * pxAtLean / want * CameraReach.DollScale(z, dollScaleAmount) / s0;
            float fit = zoomNow * Math.Max(0f, castExtent);
            z = Math.Max(z, fit);
            z = Math.Max(z, minZoom);
            return Math.Min(z, zoomNow);
        }

        /// <summary>Does the lean move the camera at all? Not when the zoom stays the player's (<paramref name="zoomTo"/> within
        /// <see cref="ZoomSame"/> of <paramref name="zoomNow"/>) and the scene's focus already sits in the middle of the view
        /// (<paramref name="vx"/>, <paramref name="vy"/>: its viewport point).</summary>
        public static bool NeedsMove(float zoomNow, float zoomTo, float vx, float vy)
        {
            if (zoomTo < zoomNow - ZoomSame) return true;
            return vx < FramedX0 || vx > FramedX1 || vy < FramedY0 || vy > FramedY1;
        }

        /// <summary>May a scene lean now? <paramref name="sinceRelease"/>: seconds since the last lean ended (null = none yet). A new scene may lean
        /// when none has, when the last ended long enough ago, or when it chains on it (starts as the last ends).</summary>
        public static bool MayLean(float? sinceRelease) => sinceRelease == null || sinceRelease.Value >= CooldownSeconds || sinceRelease.Value <= ChainSeconds;

        /// <summary>The focus has left the frame for good: the lean ends early (outside this viewport band, either way).</summary>
        public static bool LeftTheFrame(float vx, float vy) => vx < 0.04f || vx > 0.96f || vy < 0.04f || vy > 0.96f;

        /// <summary>The focus has drifted far enough from the middle that the lean follows it (re-aims).</summary>
        public static bool NeedsFollow(float vx, float vy) => Math.Abs(vx - 0.5f) > 0.22f || Math.Abs(vy - 0.5f) > 0.2f;
    }
}
