using System;
using System.Collections.Generic;

namespace AquariumCiv.Lighting
{
    /// <summary>
    /// The default camera frames the camp (decisions.md 2026-11-06, "The default camera"; tester run 1 of the Visit loop: at the
    /// whole-box default dolls were 15–25 px and the near glass rim crossed the camp). Pure floats, so it runs in dotnet test.
    /// <list type="bullet">
    /// <item><see cref="Frame"/>: the centre and radius of where the people are: the camp's fire, tents and stash plus the dolls
    /// round them, or, when most dolls are together somewhere else at that hour, that group; a tribe with no camp: its group.</item>
    /// <item><see cref="Distance"/>: how far the camera sits so a doll is <see cref="TargetDollPx"/> of a 2556-px-tall screen.</item>
    /// <item>The near glass and its frame are gone at that distance (<see cref="NearGlassFade.CampCloseness"/>), so no rim crosses the camp.</item>
    /// </list>
    /// </summary>
    public static class CampFrame
    {
        /// <summary>The one number to tune: a doll's height on screen at the camp frame, in pixels of a 2556-px-tall portrait screen
        /// (the user's 35–45 px). Other screens get the same share of their height.</summary>
        public const float TargetDollPx = 40f, MinDollPx = 35f, MaxDollPx = 45f, ReferenceScreenH = 2556f;
        /// <summary>A peg doll standing, world units (before <see cref="CameraReach.DollScale"/>).</summary>
        public const float DollHeight = 0.213f;
        /// <summary>The frame's angles: the opening view's (the camera doesn't tilt to dodge the rim: the near glass fades instead).</summary>
        public const float Yaw = 28f, Pitch = 34f;
        /// <summary>How long the ease to the frame takes on an open or a return.</summary>
        public const float EaseSeconds = 1.4f;

        /// <summary>Dolls this close to the camp's middle are at the camp; a group is the dolls this close to its densest doll.</summary>
        public const float CampReach = 2.0f, GroupReach = 1.0f;
        /// <summary>The frame's radius: room round the outermost point, never smaller or larger than these.</summary>
        public const float Pad = 0.3f, MinRadius = 0.6f, MaxRadius = 1.8f;
        /// <summary>The frame's circle takes at most this share of the screen's half width.</summary>
        public const float FitShare = 0.8f;
        /// <summary>The frame's centre stays this far inside the land's edge.</summary>
        public const float EdgeInset = 0.3f;

        public struct Point
        {
            public float x, z, w;
            public Point(float x, float z, float w = 1f) { this.x = x; this.z = z; this.w = w; }
        }

        public enum Source { Box, Camp, Group, CampAndGroup }

        public struct Result
        {
            public float x, z, radius;
            public Source source;
            public override string ToString() => $"{source} ({x:0.00}, {z:0.00}) r {radius:0.00}";
        }

        /// <summary>
        /// The frame for these camp points (the fire first and heaviest, tents, the stash) and dolls (where each stands now), in box
        /// units. Deterministic and order-stable: the same people in the same places give the same frame.
        /// </summary>
        public static Result Frame(IReadOnlyList<Point> camp, IReadOnlyList<Point> dolls, float halfX, float halfZ)
        {
            camp ??= Array.Empty<Point>();
            dolls ??= Array.Empty<Point>();
            var pts = new List<Point>();
            Source src;
            Group(dolls, out int groupCount, out float gx, out float gz);
            bool groupIsMost = dolls.Count > 0 && groupCount * 2 >= dolls.Count;
            if (camp.Count == 0)
            {
                if (dolls.Count == 0) return new Result { x = 0f, z = 0f, radius = MaxRadius, source = Source.Box };
                AddNear(dolls, gx, gz, GroupReach, pts);
                src = Source.Group;
            }
            else
            {
                Mean(camp, out float cx, out float cz);
                int atCamp = 0;
                for (int i = 0; i < dolls.Count; i++) if (Dist(dolls[i].x, dolls[i].z, cx, cz) <= CampReach) atCamp++;
                for (int i = 0; i < camp.Count; i++) if (i == 0 || Dist(camp[i].x, camp[i].z, cx, cz) <= CampReach) pts.Add(camp[i]);
                if (atCamp * 2 >= dolls.Count || !groupIsMost)
                {
                    AddNear(dolls, cx, cz, CampReach, pts);
                    src = Source.Camp;
                }
                else
                {
                    // Most of them are together away from the camp (at the pond, the field): frame them, with the fire when both fit.
                    var both = new List<Point> { camp[0] };
                    AddNear(dolls, gx, gz, GroupReach, both);
                    if (Enclose(both, out _, out _) + Pad <= MaxRadius) { pts = both; src = Source.CampAndGroup; }
                    else { pts.Clear(); AddNear(dolls, gx, gz, GroupReach, pts); src = Source.Group; }
                }
            }
            float r = Enclose(pts, out float fx, out float fz) + Pad;
            fx = Clamp(fx, -(halfX - EdgeInset), halfX - EdgeInset);
            fz = Clamp(fz, -(halfZ - EdgeInset), halfZ - EdgeInset);
            return new Result { x = fx, z = fz, radius = Clamp(r, MinRadius, MaxRadius), source = src };
        }

        /// <summary>The densest doll (most others within <see cref="GroupReach"/>; the first on a tie) and the mean of its group.</summary>
        static void Group(IReadOnlyList<Point> dolls, out int count, out float x, out float z)
        {
            count = 0; x = z = 0f;
            int best = -1;
            for (int i = 0; i < dolls.Count; i++)
            {
                int n = 0;
                for (int j = 0; j < dolls.Count; j++) if (Dist(dolls[i].x, dolls[i].z, dolls[j].x, dolls[j].z) <= GroupReach) n++;
                if (n > count) { count = n; best = i; }
            }
            if (best < 0) return;
            float sx = 0f, sz = 0f;
            for (int j = 0; j < dolls.Count; j++)
                if (Dist(dolls[best].x, dolls[best].z, dolls[j].x, dolls[j].z) <= GroupReach) { sx += dolls[j].x; sz += dolls[j].z; }
            x = sx / count; z = sz / count;
        }

        static void AddNear(IReadOnlyList<Point> from, float x, float z, float reach, List<Point> into)
        {
            for (int i = 0; i < from.Count; i++) if (Dist(from[i].x, from[i].z, x, z) <= reach) into.Add(from[i]);
        }

        static void Mean(IReadOnlyList<Point> p, out float x, out float z)
        {
            float sx = 0f, sz = 0f, sw = 0f;
            for (int i = 0; i < p.Count; i++) { float w = p[i].w > 0f ? p[i].w : 1f; sx += p[i].x * w; sz += p[i].z * w; sw += w; }
            x = sw > 0f ? sx / sw : 0f; z = sw > 0f ? sz / sw : 0f;
        }

        /// <summary>The middle of the points' bounds and the farthest point from it (0 for none).</summary>
        static float Enclose(List<Point> p, out float x, out float z)
        {
            x = z = 0f;
            if (p.Count == 0) return 0f;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var q in p) { x0 = Math.Min(x0, q.x); x1 = Math.Max(x1, q.x); z0 = Math.Min(z0, q.z); z1 = Math.Max(z1, q.z); }
            x = (x0 + x1) * 0.5f; z = (z0 + z1) * 0.5f;
            float r = 0f;
            foreach (var q in p) r = Math.Max(r, Dist(q.x, q.z, x, z));
            return r;
        }

        // ---------------------------------------------------------------- the distance

        /// <summary>A standing doll's height on screen, pixels, at the middle of the view: an upright stick of height h seen
        /// <paramref name="pitchDeg"/> down from <paramref name="distance"/> is h·cos(pitch) tall, over a view 2·d·tan(fov/2) tall.</summary>
        public static float DollPx(float distance, float pitchDeg, float screenH, float tanHalfV, float dollScale = 1f)
            => screenH * DollHeight * dollScale * (float)Math.Cos(pitchDeg * Math.PI / 180.0) / (2f * Math.Max(1e-4f, distance) * tanHalfV);

        /// <summary>The distance at which a doll is <paramref name="px"/> of a <see cref="ReferenceScreenH"/>-tall screen (the share is what counts).</summary>
        public static float DistanceForDollPx(float px, float pitchDeg, float tanHalfV, float dollScale = 1f)
            => ReferenceScreenH * DollHeight * dollScale * (float)Math.Cos(pitchDeg * Math.PI / 180.0) / (2f * Math.Max(1f, px) * tanHalfV);

        /// <summary>
        /// The camera distance for a frame of <paramref name="radius"/>: dolls at <see cref="TargetDollPx"/>, backed off (down to
        /// <see cref="MinDollPx"/>) only when the frame wouldn't fit across the screen. <paramref name="fit"/> is the whole-box distance
        /// (zoom 1), so the zoom is distance / fit; the doll's own growth with zoom (<see cref="CameraReach.DollScale"/>) is solved for.
        /// </summary>
        public static float Distance(float radius, float fit, float tanHalfV, float aspect, float pitchDeg = Pitch, float dollScaleAmount = CameraReach.DollScaleUp)
        {
            float d = DistanceForDollPx(TargetDollPx, pitchDeg, tanHalfV);
            float need = radius / (FitShare * tanHalfV * Math.Max(0.1f, Math.Min(1f, aspect)));
            for (int i = 0; i < 4; i++)
            {
                float s = CameraReach.DollScale(d / Math.Max(1e-3f, fit), dollScaleAmount);
                float d40 = DistanceForDollPx(TargetDollPx, pitchDeg, tanHalfV, s), d35 = DistanceForDollPx(MinDollPx, pitchDeg, tanHalfV, s);
                d = Math.Max(d40, Math.Min(need, d35));
            }
            float lo = CameraReach.MinZoom * fit, hi = fit;
            return Clamp(d, lo, hi);
        }

        /// <summary>The zoom for <see cref="Distance"/>.</summary>
        public static float Zoom(float radius, float fit, float tanHalfV, float aspect, float pitchDeg = Pitch, float dollScaleAmount = CameraReach.DollScaleUp)
            => Distance(radius, fit, tanHalfV, aspect, pitchDeg, dollScaleAmount) / Math.Max(1e-3f, fit);

        static float Dist(float ax, float az, float bx, float bz) { float dx = ax - bx, dz = az - bz; return (float)Math.Sqrt(dx * dx + dz * dz); }
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
