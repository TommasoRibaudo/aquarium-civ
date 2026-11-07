using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// t3-welcome (visit-loop.md 2.1-2.2): the small live greeting a return gets when no ceremony waits ("a return should always
    /// greet the Keeper with something seen live"). The values are saved as ints in <see cref="MomentPacing"/>, so None stays 0 and
    /// new kinds are only ever appended.
    /// </summary>
    public enum WelcomeKind
    {
        None = 0,
        /// <summary>The people by the fire look up and wave (always true).</summary>
        WaveFromFire,
        /// <summary>The kids run to the glass and wave, then press their hands to it (only if there are kids).</summary>
        KidsAtGlass,
        /// <summary>A few come up to the glass and wave (always true).</summary>
        GlassWave,
        /// <summary>Someone holds up a hide drying by the fire (only if one is).</summary>
        ShowHide,
        /// <summary>Someone holds up a basket from the pile (only if the pile has one; the planner checks).</summary>
        ShowBasket,
        /// <summary>A pair stands at a building raised since the last visit and shows it (only if one appeared).</summary>
        ShowHut,
    }

    /// <summary>What the box looked like at the last watched step, to tell what is new at the next open. Saved with the box.</summary>
    [Serializable]
    public class WelcomeMemo
    {
        public bool valid;
        public int maxBuildingId;
        public int buildings;
    }

    /// <summary>What is true in the box now that the people could show (the diff since the last visit, and what simply is).</summary>
    public struct WelcomeFacts
    {
        /// <summary>A building raised since the last visit (its id), -1 = none.</summary>
        public int newBuildingId;
        public bool hideDrying;
    }

    /// <summary>The greeting's rules, pure.</summary>
    public static class WelcomeRules
    {
        /// <summary>An open after at least this long away (real hours) gets a greeting when nothing waits.</summary>
        public const double MinAwayHours = 1.0;

        /// <summary>The greetings that fit these facts, best first, never <paramref name="last"/> (the same kind twice in a row), always
        /// ending with the generic ones so something is always there. <paramref name="salt"/> rotates the generics (a hash of the box and visit).</summary>
        public static WelcomeKind[] Options(WelcomeFacts f, WelcomeKind last, int salt)
        {
            var list = new List<WelcomeKind>(6);
            if (f.newBuildingId >= 0) list.Add(WelcomeKind.ShowHut);
            if (f.hideDrying) list.Add(WelcomeKind.ShowHide);
            WelcomeKind[] generic = { WelcomeKind.KidsAtGlass, WelcomeKind.WaveFromFire, WelcomeKind.GlassWave };
            int off = ((salt % 3) + 3) % 3;
            for (int i = 0; i < generic.Length; i++) list.Add(generic[(i + off) % generic.Length]);
            list.RemoveAll(k => k == last);
            return list.ToArray();
        }
    }
}
