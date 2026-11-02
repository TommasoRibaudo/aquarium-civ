using System;

namespace AquariumCiv.Lighting
{
    /// <summary>Engine-free colour (the driver converts to UnityEngine.Color).</summary>
    public readonly struct Rgb
    {
        public readonly float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
        public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        public Rgb Scaled(float k) => new Rgb(R * k, G * k, B * k);
        public float Luma => 0.2126f * R + 0.7152f * G + 0.0722f * B;
    }

    /// <summary>The look switch (research 13 section 8). SoftBlueDusk is the default; Today leaves the current light alone.</summary>
    public enum LightLook { Today, SoftBlueDusk, SoftBlueMoon, Lamplight }

    /// <summary>Everything the light driver writes in one frame. Plain data, no allocation.</summary>
    public struct LightValues
    {
        public Rgb SunColor;
        public float SunIntensity, SunPitch, SunYaw;
        public Rgb AmbientSky, AmbientEquator, AmbientGround;
        public Rgb FogColor;
        public float ShadowStrength;
        public float Lamp;              // _AC_LampLevel, 0..1
        public float PostExposure, PostSaturation, Vignette, Bloom;
        public float ShaderAmbient;     // Stylized _Ambient
        public float Daylight;          // 0 night .. 1 full day, for the glass and anything else that wants "how bright is it"

        public static LightValues Lerp(in LightValues a, in LightValues b, float t)
        {
            LightValues v;
            v.SunColor = Rgb.Lerp(a.SunColor, b.SunColor, t);
            v.SunIntensity = L(a.SunIntensity, b.SunIntensity, t);
            v.SunPitch = L(a.SunPitch, b.SunPitch, t);
            v.SunYaw = L(a.SunYaw, b.SunYaw, t);
            v.AmbientSky = Rgb.Lerp(a.AmbientSky, b.AmbientSky, t);
            v.AmbientEquator = Rgb.Lerp(a.AmbientEquator, b.AmbientEquator, t);
            v.AmbientGround = Rgb.Lerp(a.AmbientGround, b.AmbientGround, t);
            v.FogColor = Rgb.Lerp(a.FogColor, b.FogColor, t);
            v.ShadowStrength = L(a.ShadowStrength, b.ShadowStrength, t);
            v.Lamp = L(a.Lamp, b.Lamp, t);
            v.PostExposure = L(a.PostExposure, b.PostExposure, t);
            v.PostSaturation = L(a.PostSaturation, b.PostSaturation, t);
            v.Vignette = L(a.Vignette, b.Vignette, t);
            v.Bloom = L(a.Bloom, b.Bloom, t);
            v.ShaderAmbient = L(a.ShaderAmbient, b.ShaderAmbient, t);
            v.Daylight = L(a.Daylight, b.Daylight, t);
            return v;
        }

        static float L(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>Per-look constants layered on the key table (research 13 sections 7 and 8).</summary>
    public readonly struct LookTuning
    {
        public readonly bool Drives;               // false = Today: the driver writes nothing
        public readonly float NightShadow;         // sun shadow strength at night
        public readonly float LampStrength;        // x the vertex lamp term and the pool alpha
        public readonly float PoolRadius;          // x lamp pool radius
        public readonly float NightAmbientScale;   // x the night ambient colours
        public LookTuning(bool drives, float nightShadow, float lampStrength, float poolRadius, float nightAmbientScale)
        { Drives = drives; NightShadow = nightShadow; LampStrength = lampStrength; PoolRadius = poolRadius; NightAmbientScale = nightAmbientScale; }

        public static LookTuning For(LightLook look)
        {
            switch (look)
            {
                case LightLook.Today: return new LookTuning(false, 0.85f, 1f, 1f, 1f);
                case LightLook.SoftBlueMoon: return new LookTuning(true, 0.9f, 1f, 0.85f, 1f);    // a visible moon shadow, pools a little lower
                case LightLook.Lamplight: return new LookTuning(true, 0.55f, 1.4f, 1.3f, 0.75f);  // brighter lamps on a darker night
                default: return new LookTuning(true, 0.55f, 1f, 1f, 1f);                          // SoftBlueDusk
            }
        }
    }

    /// <summary>
    /// "Soft Blue Dusk" (research 13 section 7): the key table by view hour, sampled linearly (circular over midnight), the
    /// lamp ramp, and a smoothing driver. Pure: the Editor-side driver reads <see cref="Current"/> and writes it to the
    /// sun, RenderSettings, the post overrides and the shader globals.
    /// </summary>
    public sealed class DayLight
    {
        public const float SmoothRate = 3f;            // 1 - exp(-3 dt)
        public const float MaxHourRate = 1.5f;         // view hours per real second the light may chase; faster = time-lapse
        public const float FlickerHoldRate = 0.5f;     // above this many hours/s the lamp flicker is held steady
        public const float DefaultNightFloor = 0.9f, MinNightFloor = 0.45f, MaxNightFloor = 0.9f;
        public const float DayShaderAmbient = 0.75f;
        const float MaxDt = 0.1f;
        /// <summary>The sun's intensity while it swaps places with the moon: too little to see it move.</summary>
        public const float OutSun = 0.06f;

        // Key hours; the last key is the first + 24 (the night holds until 02:30, then dawn warms up to 05:00).
        // The sun never sweeps to the moon: it goes down to almost nothing with no shadow where it is (20.7 h), turns over in
        // a moment while it is out (20.9 h), and the moon comes up from there; the same on the way back (03:00 to 03:12).
        static readonly float[] Hours = { 5f, 7f, 9f, 15f, 17f, 18.5f, 19.5f, 20.7f, 20.9f, 22f, 26.5f, 27f, 27.2f, 29f };
        static readonly LightValues[] Keys = BuildKeys();

        static LightValues Key(Rgb sun, float inten, float pitch, float yaw, Rgb sky, Rgb eq, Rgb gr, float exp, float sat, float vig, float bloom, float shadow, float daylight)
        {
            var v = new LightValues
            {
                SunColor = sun, SunIntensity = inten, SunPitch = pitch, SunYaw = yaw,
                AmbientSky = sky, AmbientEquator = eq, AmbientGround = gr,
                FogColor = Rgb.Lerp(eq, sky, 0.5f),
                ShadowStrength = shadow, PostExposure = exp, PostSaturation = sat, Vignette = vig, Bloom = bloom,
                ShaderAmbient = DayShaderAmbient, Daylight = daylight,
            };
            return v;
        }

        static LightValues[] BuildKeys()
        {
            var dawn = Key(new Rgb(.95f, .66f, .55f), .55f, 10, -40, new Rgb(.55f, .60f, .82f), new Rgb(.58f, .52f, .55f), new Rgb(.30f, .27f, .30f), -.15f, 0, .30f, .55f, .75f, .45f);
            var morn = Key(new Rgb(1, .85f, .70f), 1.0f, 25, -40, new Rgb(.70f, .76f, .90f), new Rgb(.66f, .62f, .58f), new Rgb(.40f, .35f, .30f), 0, 3, .27f, .5f, .85f, .8f);
            var day = Key(new Rgb(1, .95f, .85f), 1.3f, 42, -40, new Rgb(.74f, .80f, .92f), new Rgb(.68f, .65f, .60f), new Rgb(.42f, .36f, .30f), 0, 5, .26f, .5f, .85f, 1f);
            var h17 = Key(new Rgb(1, .78f, .52f), 1.1f, 24, -40, new Rgb(.68f, .72f, .90f), new Rgb(.66f, .58f, .55f), new Rgb(.38f, .32f, .30f), -.05f, 6, .28f, .5f, .85f, .85f);
            var h185 = Key(new Rgb(1, .62f, .45f), .75f, 12, -40, new Rgb(.55f, .60f, .88f), new Rgb(.55f, .50f, .62f), new Rgb(.30f, .27f, .36f), -.15f, 6, .30f, .55f, .8f, .6f);
            var dusk = Key(new Rgb(.82f, .68f, .88f), .45f, 7, -40, new Rgb(.48f, .56f, .90f), new Rgb(.44f, .47f, .72f), new Rgb(.25f, .26f, .40f), -.25f, 4, .33f, .6f, .7f, .35f);
            var night = Key(new Rgb(.55f, .66f, 1.0f), .30f, 35, 30, new Rgb(.36f, .44f, .76f), new Rgb(.32f, .36f, .58f), new Rgb(.20f, .22f, .34f), -.35f, 2, .36f, .7f, .55f, 0f);
            LightValues Out(in LightValues a, in LightValues b, float t, in LightValues pose)
            {
                var v = LightValues.Lerp(a, b, t);
                v.SunIntensity = OutSun; v.ShadowStrength = 0f; v.SunColor = night.SunColor;
                v.SunPitch = pose.SunPitch; v.SunYaw = pose.SunYaw;
                return v;
            }
            var sunOut = Out(dusk, night, .48f, dusk);
            var moonIn = Out(dusk, night, .56f, night);
            var moonOut = Out(night, dawn, .2f, night);
            var sunIn = Out(night, dawn, .28f, dawn);
            return new[] { dawn, morn, day, day, h17, h185, dusk, sunOut, moonIn, night, night, moonOut, sunIn, dawn };
        }

        public static float Wrap24(float h) { h %= 24f; return h < 0f ? h + 24f : h; }

        /// <summary>Signed hours from one clock time to another along the shortest way round (-12..12).</summary>
        public static float HourDelta(float from, float to)
        {
            float d = Wrap24(to - from);
            return d > 12f ? d - 24f : d;
        }

        /// <summary>The raw table at an hour: linear between keys, circular over midnight, no look, no night floor.</summary>
        public static LightValues Table(float hour)
        {
            float h = Wrap24(hour);
            if (h < Hours[0]) h += 24f;
            int i = 0;
            while (i < Hours.Length - 2 && h > Hours[i + 1]) i++;
            float t = (h - Hours[i]) / (Hours[i + 1] - Hours[i]);
            var v = LightValues.Lerp(Keys[i], Keys[i + 1], t < 0f ? 0f : t > 1f ? 1f : t);
            v.Lamp = LampLevel(hour);
            return v;
        }

        /// <summary>Lamp level (#113): 0 until 17 h, 1 at 20 h, 1 through the night, down from 5 h to 7 h.</summary>
        public static float LampLevel(float hour)
        {
            float h = Wrap24(hour);
            if (h >= 20f || h < 5f) return 1f;
            if (h >= 17f) return (h - 17f) / 3f;
            if (h >= 7f) return 0f;
            return 1f - (h - 5f) / 2f;
        }

        /// <summary>The table with the look and the night floor applied. Night means 1 - Daylight.</summary>
        public static LightValues Evaluate(float hour, LightLook look, float nightFloor)
        {
            var tune = LookTuning.For(look);
            var v = Table(hour);
            float night = 1f - v.Daylight;
            float nf = Clamp(nightFloor, MinNightFloor, MaxNightFloor);
            float amb = 1f + (nf / MaxNightFloor * tune.NightAmbientScale - 1f) * night;   // 1 by day, down to the floor at night
            v.AmbientSky = v.AmbientSky.Scaled(amb);
            v.AmbientEquator = v.AmbientEquator.Scaled(amb);
            v.AmbientGround = v.AmbientGround.Scaled(amb);
            v.FogColor = v.FogColor.Scaled(amb);
            v.ShaderAmbient = DayShaderAmbient + (nf - DayShaderAmbient) * night;        // 0.75 by day, the floor at night
            v.ShadowStrength *= 1f + (tune.NightShadow / 0.55f - 1f) * night;            // research: 0.85 day, 0.55 night; the moon look differs (and 0 stays 0 at the swap)
            return v;
        }

        /// <summary>Mess dimming also dims the ambient (research 13 section 6): 60 % of the sun's loss, never below 0.7.</summary>
        public static float MessAmbientScale(float sunScale) => Clamp(1f - (1f - sunScale) * 0.6f, 0.7f, 1f);

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        // ----- the driver state -----

        public LightLook Look = LightLook.SoftBlueDusk;
        public float NightFloor = DefaultNightFloor;
        public LightValues Current { get; private set; }
        /// <summary>The hour the light is actually showing (follows the clock at most <see cref="MaxHourRate"/> per second).</summary>
        public float ShownHour { get; private set; }
        /// <summary>True while the clock runs fast (time-lapse): hold the lamp flicker steady.</summary>
        public bool FlickerHeld { get; private set; }
        /// <summary>False for <see cref="LightLook.Today"/>: the caller leaves the scene's own light alone.</summary>
        public bool Drives => LookTuning.For(Look).Drives;
        bool _started;

        /// <summary>Jump straight to an hour (first frame, era stills, a scrubber drag): no smoothing, no rate clamp.</summary>
        public void Snap(float hour)
        {
            ShownHour = Wrap24(hour);
            Current = Evaluate(ShownHour, Look, NightFloor);
            FlickerHeld = false;
            _started = true;
        }

        /// <summary>Puts back what a <see cref="Snap"/> replaced (an era still is taken, the light carries on as it was).</summary>
        public void Restore(in LightValues current, float shownHour)
        {
            Current = current;
            ShownHour = shownHour;
        }

        /// <summary>One frame. <paramref name="hour"/> is the view hour; the light chases it, never faster than the clamp.</summary>
        public void Update(float dt, float hour)
        {
            if (!_started) { Snap(hour); return; }
            if (dt > MaxDt) dt = MaxDt;
            if (dt <= 0f) return;
            float d = HourDelta(ShownHour, hour);
            float maxStep = MaxHourRate * dt;
            float step = d > maxStep ? maxStep : d < -maxStep ? -maxStep : d;
            ShownHour = Wrap24(ShownHour + step);
            FlickerHeld = Math.Abs(d) > maxStep;   // the clock is running away from the light: time-lapse
            var target = Evaluate(ShownHour, Look, NightFloor);
            float k = 1f - (float)Math.Exp(-SmoothRate * dt);
            Current = LightValues.Lerp(Current, target, k);
        }
    }
}
