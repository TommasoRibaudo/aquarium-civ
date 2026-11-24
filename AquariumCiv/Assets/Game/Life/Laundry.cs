using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #705 (B28): the washing line, pure rules (no engine types, so the sim tests and the planner share them). A dawn chore of hanging out
    /// the washing leaves a line of cloths at the home; they dry for <see cref="StayHours"/> after the doer leaves (never past
    /// <see cref="Latest"/>), then the doer or a housemate takes them in, or else they fade quietly. Nothing hangs in rain, a storm, snow or
    /// winter, and nothing is hung so late that it would stay past 17:00. Spec: docs/design/animation-specs.md B28.
    /// </summary>
    public static class Laundry
    {
        /// <summary>The chore, stretched from the old 0.35-0.55 h so three cloths fit (B28.b), in view hours.</summary>
        public const float ChoreHours = 0.7f;
        /// <summary>How long the cloths hang after she leaves: one view hour, 60 game minutes (about 16 s real at work hours).</summary>
        public const float StayHours = 1f;
        /// <summary>No nobody-free fade before this long after she leaves (the "2 h mark"), view hours.</summary>
        public const float FadeMarkHours = 2f;
        /// <summary>The line is never up past this hour (17:00).</summary>
        public const float Latest = 17f;
        /// <summary>The take-in, in view hours (about 5 s real at work hours: walk out, three plucks, carry in).</summary>
        public const float TakeInHours = 0.3f;
        /// <summary>The least stay worth hanging for: a chore that would leave less than this before 17:00 is another chore.</summary>
        public const float LeastStay = 0.5f;
        /// <summary>Cloths on a line (up to <see cref="BigCapacity"/> in a big home), and one cloth's whole beat in real seconds (B28.b).</summary>
        public const int Capacity = 4, BigCapacity = 5;
        public const float SecondsPerCloth = 2.4f;
        /// <summary>Real seconds into a cloth's beat: the first pin, the second pin (the cloth is flat from here), the beat's end.</summary>
        public const float Pin1 = 1.33f, Pin2 = 1.52f;
        /// <summary>The beat's phases in real seconds: lift, shake, reach, pin, smooth, step (they add up to <see cref="SecondsPerCloth"/>).</summary>
        public const float Lift = 0.6f, Shake = 0.35f, Reach = 0.3f, Pin = 0.35f, Smooth = 0.4f, Step = 0.4f;
        /// <summary>The take-in in real seconds: the walk out, then each cloth plucked, and the stakes popping out after the last.</summary>
        public const float WalkOut = 0.6f, PluckSeconds = 0.45f, StakesOut = 0.25f;
        /// <summary>The quiet fade: the cloths fold up over this long (real seconds).</summary>
        public const float FadeSeconds = 0.8f;
        /// <summary>The wet look: cloths start at this brightness and dry to full over the stay.</summary>
        public const float WetBrightness = 0.7f;

        /// <summary>The line's size (B28.a), box units: a cloth 0.11 x 0.13 u, 0.03 u apart, where the doll stands out from the cord.</summary>
        public const float ClothW = 0.11f, ClothH = 0.13f, Gap = 0.03f, Pitch = ClothW + Gap, StandOut = 0.10f;
        /// <summary>The cord's length for this many cloths (0.55 u for four).</summary>
        public static float Length(int cloths, float scale = 1f) => Math.Max(0.55f, cloths * Pitch - Gap + 0.02f) * scale;
        /// <summary>Where along the cord (from its middle, toward the far stake) cloth <paramref name="j"/> hangs.</summary>
        public static float SlotAlong(int j, int cloths, float scale = 1f) => ((j + 0.5f) * Pitch - cloths * Pitch * 0.5f) * scale;

        public static bool WetDay(Weather w) => w == Weather.KeeperRain || w == Weather.Storm || w == Weather.Snow;

        /// <summary>Nothing hangs on a wet day (rain, a storm, snow) or in winter (cloths freeze stiff). Fog is dry.</summary>
        public static bool CanHang(Season season, Weather w) => season != Season.Winter && !WetDay(w);

        /// <summary>A chore starting at <paramref name="hour"/> may be hanging the washing: dry, not winter, and early enough for a stay before 17:00.</summary>
        public static bool ChoreFits(Season season, Weather w, float hour) => CanHang(season, w) && hour + ChoreHours + LeastStay <= Latest + 1e-4f; // float sums: 15.8 + 0.7 + 0.5 lands a hair over 17

        /// <summary>When the stay ends if she left at <paramref name="leave"/>: one view hour on, never past 17:00.</summary>
        public static float TakeInAt(float leave) => Math.Min(Math.Max(leave, 0f) + StayHours, Math.Max(Latest, leave));

        /// <summary>When the fade comes if nobody took them in: the 2 h mark, never past 17:00.</summary>
        public static float FadeAt(float leave) => Math.Min(leave + FadeMarkHours, Math.Max(Latest, leave));

        /// <summary>How many cloths a chore of <paramref name="realSeconds"/> hangs (3 cloths in 7 s), one to <paramref name="cap"/>.</summary>
        public static int ClothsFor(float realSeconds, int cap = Capacity) => Math.Max(1, Math.Min(cap, (int)((realSeconds + 0.4f) / SecondsPerCloth)));

        /// <summary>A line holds <see cref="BigCapacity"/> in a big home (three or more grown-ups), else <see cref="Capacity"/>.</summary>
        public static int CapacityFor(int grownUps) => grownUps >= 3 ? BigCapacity : Capacity;

        /// <summary>Colour brightness while drying: 70 % wet to 100 % dry, linear over the stay (<paramref name="heldHours"/> since it was pinned).</summary>
        public static float Dryness(float heldHours) => WetBrightness + (1f - WetBrightness) * Clamp01(heldHours / StayHours);

        /// <summary>Real seconds into the chore at which cloth <paramref name="j"/> (0-based) is first pinned / flat.</summary>
        public static float PinnedAt(int j) => j * SecondsPerCloth + Pin1;
        public static float FlatAt(int j) => j * SecondsPerCloth + Pin2;

        /// <summary>How many cloths are pinned after <paramref name="realSeconds"/> of a chore that hangs <paramref name="cloths"/>.</summary>
        public static int PinnedBy(float realSeconds, int cloths)
        {
            int n = 0;
            for (int j = 0; j < cloths; j++) if (realSeconds >= PinnedAt(j)) n++;
            return n;
        }

        /// <summary>How many cloths are still on the line <paramref name="realSeconds"/> into the take-in (plucked one by one after the walk out).</summary>
        public static int LeftAfter(float realSeconds, int cloths)
        {
            int left = cloths;
            for (int j = 0; j < cloths; j++) if (realSeconds >= WalkOut + (j + 1) * PluckSeconds) left--;
            return left;
        }

        /// <summary>The whole take-in in real seconds for this many cloths.</summary>
        public static float TakeInSeconds(int cloths) => WalkOut + cloths * PluckSeconds + StakesOut;

        public enum Phase { None, Going, Hung, TakingIn, Fading, Gone }

        /// <summary>Where a line is on the day's clock. <paramref name="takeAt"/> and <paramref name="taker"/>: the planned take-in (taker false: nobody
        /// free, it fades at <paramref name="fadeAt"/>). Hours are view hours of one day; the take-in and fade lengths are real seconds past them.</summary>
        public static Phase PhaseAt(float hour, float start, float leave, float takeAt, bool taker, float fadeAt, float secondsPastTake, int cloths)
        {
            if (hour < start) return Phase.None;
            if (hour < leave) return Phase.Going;
            if (taker)
            {
                if (hour < takeAt) return Phase.Hung;
                return secondsPastTake < 0f ? Phase.Hung : secondsPastTake < TakeInSeconds(cloths) ? Phase.TakingIn : Phase.Gone;
            }
            if (hour < fadeAt) return Phase.Hung;
            return secondsPastTake < FadeSeconds ? Phase.Fading : Phase.Gone;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
