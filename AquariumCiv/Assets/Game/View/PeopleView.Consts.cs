// #786: constants (and pure helpers) the offline beat tests use, split out of PeopleView.cs so Tools/AnimBench compiles them without UnityEngine.
// Same values, same class (a partial): the game is unchanged.
using UnityEngine;

namespace AquariumCiv.View
{
    public partial class PeopleView
    {
        /// <summary>#791: how far a side has to be clear before the pair swaps it (one walking behind the other draws level every few frames).
        /// The hold eases over 0.6 s: at 0.3 s the lean coming in on top of the stride's own sway (and the catch-up) turned the roll at up
        /// to ~70 deg/s, over the 60 the walk test allows (#203); which pairs fall into step depends on where the dolls stand when it starts,
        /// so it failed only after other classes. <see cref="EaseMates"/> keeps it under 60 whatever the stride's phase.</summary>
        public const float MateSwapSide = 0.04f, MateHoldSeconds = 0.6f, MateLeanSeconds = 0.5f;

        /// <summary>#791: one frame of the walking-together hold (0..1) and lean (-1..1) easing toward <paramref name="on"/> and the side.</summary>
        public static void EaseMates(ref float hold, ref float lean, bool on, bool lead, float dt)
        {
            if (hold <= 0f) lean = lead ? -1f : 1f;
            hold = dt > 0f ? Mathf.MoveTowards(hold, on ? 1f : 0f, dt / MateHoldSeconds) : (on ? 1f : 0f);
            lean = dt > 0f ? Mathf.MoveTowards(lean, lead ? -1f : 1f, 2f * dt / MateLeanSeconds) : (lead ? -1f : 1f);
        }
    }
}
