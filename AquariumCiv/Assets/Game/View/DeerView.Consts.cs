// #786: constants (and pure helpers) the offline beat files use, split out of DeerView.cs so Tools/AnimBench compiles them without UnityEngine.
// Same values, same class (a partial): the game is unchanged.
namespace AquariumCiv.View
{
    public partial class DeerView
    {
        /// <summary>A person this near makes a deer look up (AlertRadius); the look lasts this long; a knock's look before the bolt.</summary>
        public const float AlertSeconds = 1.0f, KnockLook = 0.45f;
    }
}
