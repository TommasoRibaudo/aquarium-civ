using System;
using AquariumCiv.Life;
using AquariumCiv.Sim;

namespace AquariumCiv.Audio
{
    /// <summary>What the ambience listens to (#96): the era, the view hour (LifeClock), the weather, the season, how
    /// messy the box is, and whether an era moment plays.</summary>
    public struct AmbienceInput
    {
        public Era era;
        public float hour;        // view hour 0..24 (LifeClock)
        public Weather weather;
        public Season season;
        public float mess01;      // 0 clean .. 1 the worst the box gets (grime/overgrowth/trash, whichever is worst)
        public bool inMoment;     // an era moment plays: the ambience thins so the moment's cues carry
        public bool festival;     // a festival evening: the crickets give way to the people (the fiddle cue carries it)
        // P8, the living land (keepers-hands §2.1, #247): the barren bed follows the bare share of the land; the life
        // sounds come in as the land earns them (crickets with the first green, birds with the first bird, frogs once
        // the water surfaces). Off (eco false) the mix is the slice's.
        public bool eco;
        public float bare01;      // the share of the land still bare, 0..1 (the view keeps its lowest: it never comes back)
        public bool ecoGreen, ecoBirds, ecoWater;
        public float water01;     // the water table, 0 (below the lowest ground, the start) .. 1 (at the pond's spill); the soil's trickle follows it (#280)
    }

    /// <summary>The six ambience layers' levels, 0..1 of each layer's cue volume.</summary>
    public struct AmbienceLevels
    {
        public float calm, birds, crickets, rain, wind, flies, barren, frogs, sandHiss, trickle, drips;

        public float this[int i] => i switch { 0 => calm, 1 => birds, 2 => crickets, 3 => rain, 4 => wind, 5 => flies, 6 => barren, 7 => frogs, 8 => sandHiss, 9 => trickle, _ => drips };
        public static readonly string[] CueIds =
            { CueLibrary.AmbCalm, CueLibrary.AmbBirds, CueLibrary.AmbCrickets, CueLibrary.AmbRain, CueLibrary.AmbWind, CueLibrary.AmbFlies,
              CueLibrary.AmbBarren, CueLibrary.AmbFrogs, CueLibrary.AmbSandHiss, CueLibrary.AmbTrickle, CueLibrary.AmbDrips };
        public const int Count = 11;

        public void Set(int i, float v)
        {
            switch (i)
            {
                case 0: calm = v; break; case 1: birds = v; break; case 2: crickets = v; break; case 3: rain = v; break; case 4: wind = v; break;
                case 5: flies = v; break; case 6: barren = v; break; case 7: frogs = v; break;
                case 8: sandHiss = v; break; case 9: trickle = v; break; default: drips = v; break;
            }
        }

        public override string ToString() =>
            $"calm {calm:0.00} birds {birds:0.00} crickets {crickets:0.00} rain {rain:0.00} wind {wind:0.00} flies {flies:0.00} barren {barren:0.00} frogs {frogs:0.00} sandHiss {sandHiss:0.00} trickle {trickle:0.00} drips {drips:0.00}";
    }

    /// <summary>
    /// The ambience mix (#96): a calm loop always (the room the box sits in), birds by day, crickets by night, rain and
    /// wind with the weather. Smooth in the hour, so dawn fades the crickets out as the birds come in. Mess thins the
    /// birds and brings a subtle fly buzz that rises with it and is gone when the box is clean (the user's call,
    /// 2026-10-30; kept quiet so it reads as "the box needs you", not a nag). All numbers [est], to tune by ear.
    /// </summary>
    public static class Ambience
    {
        public static AmbienceLevels Targets(in AmbienceInput a)
        {
            float h = Wrap24(a.hour);
            float day = Curve(h, DayCurve), night = Curve(h, NightCurve);

            float birds = day * BirdsByWeather(a.weather) * BirdsByEra(a.era) * (1f - 0.5f * Clamp01(a.mess01));
            if (a.season == Season.Winter) birds *= 0.35f;

            float crickets = night * CricketsByWeather(a.weather);
            if (a.season == Season.Winter) crickets = 0f;
            else if (a.season == Season.Autumn) crickets *= 0.6f;
            if (a.festival) crickets *= 0.3f;

            float rain = a.weather switch { Weather.KeeperRain => 0.7f, Weather.Storm => 1f, _ => 0f };
            float wind = a.weather switch { Weather.Storm => 0.8f, Weather.Snow => 0.45f, Weather.Fog => 0.2f, _ => 0f };
            float calm = 0.7f + 0.3f * night; // the room tone carries the quiet night
            if (rain > 0f) calm *= 0.6f;

            float flies = Flies(a.mess01);
            if (a.weather == Weather.Storm || a.weather == Weather.Snow) flies *= 0.3f;
            if (a.season == Season.Winter) flies *= 0.5f;
            flies *= 0.6f + 0.4f * day; // a little quieter at night

            float barren = 0f, frogs = 0f, sandHiss = 0f, trickle = 0f, drips = 0f;
            if (a.eco)
            {
                float bare = Clamp01(a.bare01);
                // The barren bed: full on a dead box, eased out as the green takes over; quieter at night, gone below 15 % bare.
                float u = Clamp01((bare - BarrenGoneBelow) / (1f - BarrenGoneBelow));
                barren = u * u * (3f - 2f * u) * (0.75f + 0.25f * day);
                sandHiss = barren;                               // the sand's hiss rides the same share (its own recording, the high band)
                calm *= 1f - 0.5f * barren;                      // the dry wind carries the room on a dead box
                float life = 1f - bare;
                birds = a.ecoBirds ? birds * (0.35f + 0.65f * Clamp01(life * 1.5f)) : 0f;
                // Crickets come with the first green: at night as always, and a faint chirr by day until the birds take the day.
                if (!a.ecoGreen) crickets = 0f;
                else if (!a.ecoBirds && a.season != Season.Winter) crickets = Math.Max(crickets, day * (0.18f + 0.17f * Clamp01(life * 4f))); // audible from the first green (tester: 0 by day)
                if (a.ecoGreen && a.season != Season.Winter) crickets *= 0.9f + 0.1f * Clamp01(life * 2f); // a little fuller as the land greens
                if (a.ecoWater && a.season != Season.Winter) frogs = (0.25f + 0.75f * night) * (a.weather == Weather.Storm ? 0.3f : 1f) * (0.6f + 0.4f * Clamp01(life * 2f));
                // The soil's seep rises with the water table, faint; the drips come in once it is well up (#280).
                float w = Clamp01(a.water01);
                trickle = w * w * (3f - 2f * w);
                drips = Clamp01((w - 0.35f) / 0.65f) * 0.8f;
            }

            var l = new AmbienceLevels { calm = calm, birds = birds, crickets = crickets, rain = rain, wind = wind, flies = flies, barren = barren, frogs = frogs, sandHiss = sandHiss, trickle = trickle, drips = drips };
            if (a.inMoment) { l.calm *= 0.6f; l.birds *= 0.4f; l.crickets *= 0.4f; l.rain *= 0.7f; l.wind *= 0.6f; l.flies *= 0.3f; l.barren *= 0.4f; l.frogs *= 0.4f; l.sandHiss *= 0.4f; l.trickle *= 0.6f; l.drips *= 0.4f; }
            return l;
        }

        // (hour, level) points, linear between, wrapping at 24. Birds: up at dawn (a chorus), quieter at midday, gone
        // after the evening. Crickets: the mirror, starting in the evening.
        static readonly float[] DayCurve = { 0f, 0f, 4.5f, 0f, 6f, 1f, 8f, 0.8f, 12f, 0.5f, 16f, 0.65f, 19f, 0.3f, 20.5f, 0f, 24f, 0f };
        static readonly float[] NightCurve = { 0f, 1f, 4.5f, 1f, 6f, 0f, 18f, 0f, 20.5f, 1f, 24f, 1f };

        /// <summary>P8: below this share of bare land the barren bed is silent [est].</summary>
        public const float BarrenGoneBelow = 0.15f;

        /// <summary>Below this much mess there are no flies at all [est].</summary>
        public const float FliesFrom = 0.15f;
        /// <summary>The fly layer by mess: silent below <see cref="FliesFrom"/>, full at 0.8 (eased in, so the first flies are faint).</summary>
        public static float Flies(float mess01)
        {
            float u = (Clamp01(mess01) - FliesFrom) / (0.8f - FliesFrom);
            u = Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        static float BirdsByWeather(Weather w) => w switch
        {
            Weather.KeeperRain => 0.3f, Weather.Storm => 0f, Weather.Snow => 0.2f, Weather.Fog => 0.5f, Weather.Rainbow => 1f, _ => 1f,
        };

        static float CricketsByWeather(Weather w) => w switch
        {
            Weather.KeeperRain => 0.3f, Weather.Storm => 0f, Weather.Snow => 0f, Weather.Fog => 0.7f, _ => 1f,
        };

        /// <summary>Fewer birds as the place grows (the camp is wild; the town has roofs) [est].</summary>
        static float BirdsByEra(Era e) => e switch { Era.Camp => 1f, Era.Village => 0.85f, _ => 0.7f };

        static float Curve(float h, float[] pts)
        {
            for (int i = 2; i < pts.Length; i += 2)
            {
                if (h > pts[i]) continue;
                float h0 = pts[i - 2], v0 = pts[i - 1], h1 = pts[i], v1 = pts[i + 1];
                return h1 <= h0 ? v1 : v0 + (v1 - v0) * (h - h0) / (h1 - h0);
            }
            return pts[pts.Length - 1];
        }

        static float Wrap24(float h) { h %= 24f; return h < 0f ? h + 24f : h; }
        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }

    /// <summary>Keeps the lowest bare share seen: the barren bed follows the land greening and never comes back (nothing dies back, #280).</summary>
    public sealed class BareLatch
    {
        float low = 1f;
        public float Low => low;
        public float Update(float bare01) { if (bare01 < low) low = bare01 < 0f ? 0f : bare01; return low; }
        public void Reset() => low = 1f;
    }

    /// <summary>Moves the playing levels toward the targets at a set speed, so a weather change or the hour fades
    /// rather than cuts. A layer that reaches 0 can be paused by the view (<see cref="Audible"/>).</summary>
    public class AmbienceFader
    {
        /// <summary>Seconds a layer takes to go from silent to full [est].</summary>
        public const float FadeSeconds = 4f;
        readonly float[] now = new float[AmbienceLevels.Count];

        public float Level(int layer) => now[layer];
        public bool Audible(int layer) => now[layer] > 0.001f;

        public void Snap(in AmbienceLevels target) { for (int i = 0; i < now.Length; i++) now[i] = target[i]; }

        public void Tick(float dt, in AmbienceLevels target)
        {
            float step = dt / FadeSeconds;
            for (int i = 0; i < now.Length; i++)
            {
                float t = target[i];
                now[i] = now[i] < t ? Math.Min(t, now[i] + step) : Math.Max(t, now[i] - step);
            }
        }
    }
}
