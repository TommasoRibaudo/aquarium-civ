// #786: constants (and pure helpers) the offline beat files use, split out of CampLife.cs so Tools/AnimBench compiles them without UnityEngine.
// Same values, same class (a partial): the game is unchanged.
namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        /// <summary>How often a use plays (D rules): <b>sometimes</b> about one view day in three; <b>often</b> most days.</summary>
        public const float Sometimes = 0.35f, Often = 0.85f, RabbitSpit = 0.5f;
    }
}
