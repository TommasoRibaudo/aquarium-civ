using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using T = AquariumCiv.View.Shaping.ShapingTuning;

namespace AquariumCiv.View.Shaping
{
    /// <summary>
    /// The four slice tools in the style kit, as curves the view samples every frame (#85; game-definition §6: "everything
    /// you shape follows the art style"). Each look is a pure function of the footprint and the clocks: no dice, no state,
    /// the same frame twice looks the same. Phase 2 turns these numbers into meshes and particles.
    /// <list type="bullet">
    /// <item><b>Rain</b>: a cloud forms, rain falls for a few seconds (a storm in the town, with lightning), the soil
    /// darkens and stays damp, sprouts come up over the next months.</item>
    /// <item><b>Grove</b>: saplings pop up, then grow into trees over three years; woods in the camp, blossom in the
    /// village's spring, an orchard in rows with fruit in the town.</item>
    /// <item><b>Channel</b>: carved cell by cell from the pond to the spot, the water following; irrigation in the village,
    /// a mill race with a little fall in the town.</item>
    /// <item><b>Herd</b>: deer (goats, sheep) run in from the box edge and graze, moving together between grazing spots; a
    /// shoal of fish lands with a splash and swims round the pond. An answered herd drifts to its pasture or jetty.</item>
    /// </list>
    /// A take-back plays each in reverse, quickly.
    /// </summary>
    public static class ShapingLooks
    {
        // ================================================================== rain

        public static RainLook Rain(ShapingFootprint f, ShapingClock c)
        {
            float s = c.sinceSeen;
            float rainLen = T.RainSeconds(f.level);
            float rainEnd = T.RainDelaySeconds + rainLen;
            float cloud = Ramp(s, 0f, T.CloudFormSeconds) * (1f - Ramp(s, rainEnd, rainEnd + T.CloudFadeSeconds));
            float rain = Ramp(s, T.RainDelaySeconds, T.RainDelaySeconds + 0.6f) * (1f - Ramp(s, rainEnd - 0.8f, rainEnd));
            // The soil drinks while it rains, stays dark a month, then dries to damp until the people answer, then dries out.
            float soak = Ramp(s, T.RainDelaySeconds, rainEnd);
            float dry = 1f - (1f - T.DampAfter) * Ramp(c.ageMonths, T.WetMonths, T.WetMonths + T.DryMonths);
            if (c.monthsSinceAnswer >= 0f) dry *= 1f - Ramp(c.monthsSinceAnswer, 0f, T.AnsweredDryMonths);
            float wet = soak * dry;
            float sprouts = Smooth(Ramp(c.ageMonths, T.SproutFromMonths, T.SproutGrownMonths));
            // Once answered with a field, the field's own crop takes over from the sprouts.
            if (c.monthsSinceAnswer >= 0f) sprouts *= 1f - Ramp(c.monthsSinceAnswer, 0f, 1f) * 0.6f;
            bool storm = f.level >= Era.Town;
            float flash = storm && rain > 0.5f ? Flash(f.seed, s) : 0f;
            if (c.Leaving)
            {
                float k = 1f - Ramp(c.sinceLeaving, 0f, T.LeaveSeconds);
                cloud *= k; rain *= k; wet *= k; sprouts *= k; flash = 0f;
            }
            return new RainLook { x = f.x, z = f.z, radius = T.RainRadius, cloud = cloud, rain = rain, wet = wet, sprouts = sprouts, flash = flash, storm = storm, cells = f.cells };
        }

        /// <summary>A storm's lightning: a short flash at two or three hashed moments of the rain.</summary>
        static float Flash(int seed, float s)
        {
            for (int i = 0; i < 3; i++)
            {
                float at = T.RainDelaySeconds + 1f + i * 2.2f + Terrain.Hash3(seed, i, 41) * 1.2f;
                float d = s - at;
                if (d >= 0f && d < 0.25f) return 1f - d / 0.25f;
            }
            return 0f;
        }

        // ================================================================== grove

        /// <summary>The grove's look in this era (it levels up with the box: research 10 "Each tool levels up").</summary>
        public static GroveStyle GroveStyleOf(Era era) => era == Era.Camp ? GroveStyle.Woods : era == Era.Village ? GroveStyle.Blossom : GroveStyle.Orchard;

        /// <summary>The grove's trees. <paramref name="map"/> (optional) drops trees whose cell is no longer woods (cut for
        /// timber, taken by a lot).</summary>
        public static GroveLook Grove(ShapingFootprint f, ShapingClock c, SiteMap map = null)
        {
            var style = GroveStyleOf(c.era);
            var trees = new List<TreeLook>();
            var season = LifeClock.SeasonOf(c.day);
            int m = (int)(((c.day % 12) + 12) % 12);
            float leave = c.Leaving ? 1f - Smooth(Ramp(c.sinceLeaving, 0f, T.LeaveSeconds)) : 1f;
            int k = 0;
            foreach (int cell in f.cells)
            {
                int i = SiteMap.I(cell), j = SiteMap.J(cell);
                bool tree = style == GroveStyle.Orchard ? ((i + j) & 1) == 0 : Terrain.Hash3(f.seed, cell, 3) < T.WoodsTreeShare;
                if (!tree) continue;
                if (map != null && !map.Forest(cell)) continue;
                float tx = SiteMap.CXc(cell), tz = SiteMap.CZc(cell);
                if (style != GroveStyle.Orchard)
                {
                    tx += (Terrain.Hash3(f.seed, cell, 4) - 0.5f) * 2f * T.TreeJitter;
                    tz += (Terrain.Hash3(f.seed, cell, 5) - 0.5f) * 2f * T.TreeJitter;
                }
                float pop = Smooth(Ramp(c.sinceSeen, k * T.SaplingStagger, k * T.SaplingStagger + T.SaplingPopSeconds));
                float delay = Terrain.Hash3(f.seed, cell, 6) * T.TreeDelayMonths;
                float grown = Smooth(Ramp(c.ageMonths, delay, delay + T.TreeGrowMonths));
                float growth = pop * (T.SaplingScale + (1f - T.SaplingScale) * grown) * leave;
                float blossom = style != GroveStyle.Woods && season == Season.Spring ? Smooth(grown) : 0f;
                float fruit = style == GroveStyle.Orchard && (m >= 7 && m <= 9) ? Smooth(grown) : 0f;
                trees.Add(new TreeLook { x = tx, z = tz, growth = growth, blossom = blossom, fruit = fruit, seed = Hash(f.seed, cell) });
                k++;
            }
            return new GroveLook { style = style, x = f.x, z = f.z, trees = trees };
        }

        // ================================================================== channel

        public static ChannelStyle ChannelStyleOf(Era era, bool answered) =>
            era >= Era.Town ? ChannelStyle.MillRace : era == Era.Village && answered ? ChannelStyle.Irrigation : ChannelStyle.Ditch;

        /// <summary>Real seconds the carving takes for a channel of <paramref name="cells"/> cells.</summary>
        public static float CarveSeconds(int cells) => Math.Max(T.CarveMinSeconds, Math.Min(T.CarveMaxSeconds, cells * T.CarveSecondsPerCell));

        public static ChannelLook Channel(ShapingFootprint f, ShapingClock c)
        {
            int n = f.cells.Count;
            var list = new List<ChannelCellLook>(n);
            float carve = CarveSeconds(n);
            float per = n > 0 ? carve / n : 0f;
            float s = c.sinceSeen;
            // A take-back drains the water from the spot back to the pond, then the turf closes over it, the same way.
            float drain = c.Leaving ? Ramp(c.sinceLeaving, 0f, T.LeaveSeconds * 0.5f) : 0f;
            float fill = c.Leaving ? Ramp(c.sinceLeaving, T.LeaveSeconds * 0.5f, T.LeaveSeconds) : 0f;
            float head = 0f;
            for (int i = 0; i < n; i++)
            {
                float carved = Ramp(s, i * per, (i + 1) * per);
                float water = Ramp(s, i * per + T.WaterLagSeconds, (i + 1) * per + T.WaterLagSeconds);
                float back = n <= 1 ? 1f : 1f - (float)i / (n - 1); // 1 at the pond end, 0 at the spot
                if (c.Leaving)
                {
                    // The far end goes first.
                    water *= drain >= 1f ? 0f : Clamp01((back - drain) * n + 1f);
                    carved *= fill >= 1f ? 0f : Clamp01((back - fill) * n + 1f);
                }
                if (water > 0.5f) head = n <= 1 ? 1f : (float)i / (n - 1);
                int cell = f.cells[i];
                list.Add(new ChannelCellLook { cell = cell, x = SiteMap.CXc(cell), z = SiteMap.CZc(cell), carved = carved, water = water });
            }
            var style = ChannelStyleOf(c.era, c.Answered);
            int fall = style == ChannelStyle.MillRace && n >= 3 ? Math.Min(n - 1, (int)Math.Round(T.MillFallAt * (n - 1))) : -1;
            return new ChannelLook { style = style, cells = list, fallAt = fall, flowHead = head };
        }

        // ================================================================== herd

        /// <summary>The herd now. <paramref name="lotX"/>/<paramref name="lotZ"/> is the answer's pasture or jetty (NaN =
        /// none yet): the herd drifts there once answered.</summary>
        public static HerdLook Herd(ShapingFootprint f, ShapingClock c, float lotX = float.NaN, float lotZ = float.NaN)
        {
            var sp = f.SpeciesAt(c.era); // the herd follows the era: deer, then goats, then sheep
            int count = f.spots.Count == 0 ? 0 : T.HerdCount(sp); // not a herd: nothing to draw
            var list = new List<AnimalLook>(count);
            bool fish = sp == HerdSpecies.Fish;
            float splash = fish ? 1f - Ramp(c.sinceSeen, 0f, T.SplashSeconds) : 0f;
            // Drift towards the answer's lot.
            float ox = 0f, oz = 0f;
            if (c.Answered && !float.IsNaN(lotX) && !float.IsNaN(lotZ))
            {
                float k = c.sinceAnswered >= 0f ? Smooth(Ramp(c.sinceAnswered, 0f, T.HerdMoveToLotSeconds)) : 1f;
                ox = (lotX - f.x) * k; oz = (lotZ - f.z) * k;
            }
            for (int i = 0; i < count; i++)
            {
                var a = fish ? Fish(f, c, i, ox, oz) : Grazer(f, c, i, ox, oz);
                a.seed = Hash(f.seed, 100 + i);
                list.Add(a);
            }
            return new HerdLook { species = sp, animals = list, splash = splash };
        }

        /// <summary>Where the herd's focus is at wander time <paramref name="w"/>: it rests at a grazing spot, then walks
        /// on to the next (all together, each a beat apart).</summary>
        static void GrazeFocus(ShapingFootprint f, float w, out float x, out float z, out bool walking, out float hx, out float hz)
        {
            int k = f.spots.Count;
            float cyc = T.GrazeCycle;
            int idx = (int)Math.Floor(w / cyc);
            float into = w - idx * cyc;
            var a = f.spots[Mod(idx, k)];
            var b = f.spots[Mod(idx + 1, k)];
            float t = into < cyc * T.GrazeShare ? 0f : Smooth((into - cyc * T.GrazeShare) / (cyc * (1f - T.GrazeShare)));
            walking = t > 0f && t < 1f;
            x = a.x + (b.x - a.x) * t;
            z = a.z + (b.z - a.z) * t;
            hx = b.x - a.x; hz = b.z - a.z;
        }

        static AnimalLook Grazer(ShapingFootprint f, ShapingClock c, int i, float ox, float oz)
        {
            float lag = Terrain.Hash3(f.seed, i, 11) * 1.5f;
            float wander = Math.Max(0f, c.sinceSeen - lag);
            GrazeFocus(f, wander, out float fx, out float fz, out bool walking, out float hx, out float hz);
            float ang = Terrain.Hash3(f.seed, i, 12) * 6.2831853f;
            float rad = T.HerdSpread * (0.35f + 0.65f * Terrain.Hash3(f.seed, i, 13));
            float gx = ShapingFootprint.ClampX(fx + ox + (float)Math.Cos(ang) * rad);
            float gz = ShapingFootprint.ClampZ(fz + oz + (float)Math.Sin(ang) * rad);
            // The run-in: from the box edge to the first grazing spot, each a stagger apart.
            float start = i * T.RunInStagger;
            float runEnd = start + T.RunInSeconds;
            var pose = walking ? AnimalPose.Walk : AnimalPose.Graze;
            float heading = walking ? Heading(hx, hz) : Heading(hx, hz) + (Terrain.Hash3(f.seed, i, 14) - 0.5f) * 1.6f;
            float x = gx, z = gz, alpha = 1f;
            if (c.sinceSeen < runEnd)
            {
                float t = Smooth(Ramp(c.sinceSeen, start, runEnd));
                float ex = ShapingFootprint.ClampX(f.entryX + (float)Math.Cos(ang) * rad), ez = ShapingFootprint.ClampZ(f.entryZ + (float)Math.Sin(ang) * rad);
                x = ex + (gx - ex) * t; z = ez + (gz - ez) * t;
                pose = c.sinceSeen < start ? AnimalPose.Hidden : AnimalPose.Run;
                heading = Heading(gx - ex, gz - ez);
                alpha = c.sinceSeen < start ? 0f : 1f;
            }
            if (c.Leaving)
            {
                // Back out to the edge they came from.
                float t = Smooth(Ramp(c.sinceLeaving, i * 0.05f, T.HerdLeaveSeconds));
                x += (f.entryX - x) * t; z += (f.entryZ - z) * t;
                pose = t >= 1f ? AnimalPose.Hidden : AnimalPose.Run;
                heading = Heading(f.entryX - gx, f.entryZ - gz);
                alpha = 1f - Ramp(c.sinceLeaving, T.HerdLeaveSeconds * 0.8f, T.HerdLeaveSeconds);
            }
            return new AnimalLook { x = x, z = z, heading = heading, pose = pose, alpha = alpha };
        }

        static AnimalLook Fish(ShapingFootprint f, ShapingClock c, int i, float ox, float oz)
        {
            // The shoal's centre glides round the swimming spots; each fish circles it, a little out of step.
            float w = c.sinceSeen;
            int k = f.spots.Count;
            float cyc = T.SwimCycle;
            float along = w + i * T.ShoalLagSeconds * Smooth(Ramp(w, 0f, T.ShoalFanSeconds)); // strung out along the route
            int idx = (int)Math.Floor(along / cyc);
            float t = Smooth((along - idx * cyc) / cyc);
            var a = f.spots[Mod(idx, k)];
            var b = f.spots[Mod(idx + 1, k)];
            float cx = a.x + (b.x - a.x) * t + ox, cz = a.z + (b.z - a.z) * t + oz;
            float phase = Terrain.Hash3(f.seed, i, 21) * 6.2831853f;
            float spin = 0.9f + 0.4f * Terrain.Hash3(f.seed, i, 22);
            float rad = T.ShoalSpread * (0.5f + 0.5f * Terrain.Hash3(f.seed, i, 23));
            float ang = phase + w * spin;
            // Fanning out from the splash.
            float spread = Smooth(Ramp(w, 0f, T.SplashSeconds * 2f));
            float x = ShapingFootprint.ClampX(cx + (float)Math.Cos(ang) * rad * spread);
            float z = ShapingFootprint.ClampZ(cz + (float)Math.Sin(ang) * rad * spread);
            float heading = Heading(-(float)Math.Sin(ang), (float)Math.Cos(ang)); // along the circle
            float alpha = Ramp(w, 0f, T.SplashSeconds * 0.5f);
            var pose = AnimalPose.Swim;
            if (c.Leaving)
            {
                alpha *= 1f - Ramp(c.sinceLeaving, 0f, T.HerdLeaveSeconds);
                pose = alpha <= 0f ? AnimalPose.Hidden : AnimalPose.Dive;
            }
            return new AnimalLook { x = x, z = z, heading = heading, pose = pose, alpha = alpha };
        }

        // ================================================================== the answer

        /// <summary>The world label for an answered gift: "The Keeper's rain" (storm, grove, orchard, herd, shoal, goats,
        /// sheep, channel, mill race), by the tool's level when it was used.</summary>
        public static string LabelOf(string kind, string variant, Era level)
        {
            string noun = kind switch
            {
                SimCore.NudgeKind.Rain => level >= Era.Town ? "storm" : "rain",
                SimCore.NudgeKind.Grove => level >= Era.Town ? "orchard" : "grove",
                SimCore.NudgeKind.Herd => variant == "fish" ? "shoal" : level == Era.Camp ? "herd" : level == Era.Village ? "goats" : "flock",
                SimCore.NudgeKind.Channel => level >= Era.Town ? "mill race" : "channel",
                _ => kind,
            };
            return $"The Keeper's {noun}";
        }

        /// <summary>Real seconds the chronicle line stays up: long enough to read, never long.</summary>
        public static float BubbleSeconds(string text) =>
            Math.Max(T.BubbleMin, Math.Min(T.BubbleMax, (text?.Length ?? 0) / T.BubbleCharsPerSecond + 2f));

        /// <summary>Real seconds the whole answer show lasts for this line.</summary>
        public static float AnswerSeconds(string text) =>
            Math.Max(T.LabelSeconds, Math.Max(T.HighlightIn + T.HighlightHold + T.HighlightOut, T.BubbleDelay + BubbleSeconds(text) + T.FadeSeconds));

        /// <summary>The answer show at <paramref name="since"/> real seconds after the view saw the answer.</summary>
        public static AnswerLook Answer(int nudgeId, int buildingId, int chronicleId, int notableId, float x, float z, string label, string line, float since)
        {
            float total = AnswerSeconds(line);
            if (since < 0f || since >= total) return new AnswerLook { nudgeId = nudgeId, progress = since < 0f ? 0f : 1f };
            float hi = Ramp(since, 0f, T.HighlightIn) * (1f - Ramp(since, T.HighlightIn + T.HighlightHold, T.HighlightIn + T.HighlightHold + T.HighlightOut));
            float bs = BubbleSeconds(line);
            float bub = string.IsNullOrEmpty(line) ? 0f
                : Ramp(since, T.BubbleDelay, T.BubbleDelay + T.FadeSeconds) * (1f - Ramp(since, T.BubbleDelay + bs, T.BubbleDelay + bs + T.FadeSeconds));
            float lab = Ramp(since, 0f, T.FadeSeconds) * (1f - Ramp(since, T.LabelSeconds - T.FadeSeconds, T.LabelSeconds));
            return new AnswerLook
            {
                active = true, nudgeId = nudgeId, buildingId = buildingId, chronicleId = chronicleId, notableId = notableId, x = x, z = z,
                highlight = buildingId >= 0 ? hi : 0f, label = label, labelAlpha = lab, bubble = line, bubbleAlpha = bub, progress = since / total,
            };
        }

        // ================================================================== helpers

        public static float Ramp(float v, float a, float b) => b <= a ? (v >= b ? 1f : 0f) : Clamp01((v - a) / (b - a));
        public static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static int Mod(int a, int k) => ((a % k) + k) % k;
        static float Heading(float dx, float dz) => (float)Math.Atan2(dx, dz);
        static int Hash(int a, int b) => (int)(Terrain.Hash3(a, b, 97) * 16777216f);
    }
}
