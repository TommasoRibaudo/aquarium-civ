// #786: constants (and pure helpers) the offline beat files use, split out of PegDoll.cs / PegDoll.Statue.cs so Tools/AnimBench compiles them without UnityEngine.
// Same values, same class (a partial): the game is unchanged.
namespace AquariumCiv.View
{
    public static partial class PegDoll
    {
        public const float KidScale = 0.68f;
        /// <summary>The plinth's two steps (box units), where the figure's feet stand on it, and the figure's size (1.3 x a person: 0.28 u of doll).</summary>
        public const float StatueLowW = 0.14f, StatueStepH = 0.03f, StatueUpW = 0.09f, StatueFeetY = 0.06f, StatueScale = 1.3f;
    }
}
