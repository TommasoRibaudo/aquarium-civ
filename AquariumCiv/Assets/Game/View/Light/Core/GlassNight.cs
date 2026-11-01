namespace AquariumCiv.Lighting
{
    /// <summary>The glass at night (research 13 section 6/7): frost gets more opaque, the clean tint dims with the day.</summary>
    public static class GlassNight
    {
        /// <summary>Frost opacity at full night = base x this (capped at 1 by <see cref="FrostOpacity"/>).</summary>
        public const float NightFrostBoost = 1.5f;

        /// <summary>0 by day .. 1 at night, from the table's daylight at this hour.</summary>
        public static float Night(float hour) => 1f - DayLight.Table(hour).Daylight;

        /// <summary>Multiplier on the frost / condensation opacity: 1 by day, <see cref="NightFrostBoost"/> at night.</summary>
        public static float FrostScale(float hour) => 1f + (NightFrostBoost - 1f) * Night(hour);

        public static float FrostOpacity(float baseOpacity, float hour)
        {
            float v = baseOpacity * FrostScale(hour);
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>Multiplier on the clean glass tint: 0.35 + 0.65 x day brightness (1 by day, 0.35 at deep night).</summary>
        public static float TintFactor(float hour) => 0.35f + 0.65f * DayLight.Table(hour).Daylight;
    }
}
