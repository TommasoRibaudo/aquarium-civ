// #786: constants (and pure helpers) the offline beat files use, split out of ConsequenceProps.cs so Tools/AnimBench compiles them without UnityEngine.
// Same values, same class (a partial): the game is unchanged.
namespace AquariumCiv.View
{
    public static partial class ConsequenceMeshes
    {
        /// <summary>How much bigger than life the things stand, so they read at default zoom (as the relics do).</summary>
        public const float Scale = 1.5f;
        public const float PenHalf = 0.1f;
        /// <summary>The runestone's footprint (local half-sizes before <see cref="Scale"/>): people stand outside it (tests: NoOneInsideTheStone).</summary>
        public const float StoneHalfX = 0.07f, StoneHalfZ = 0.035f, StoneH = 0.36f;
        public const float PostH = 0.2f, PostHalf = 0.022f;

        /// <summary>Where the cords tie on for a drawn tree of play radius <paramref name="spotR"/> (BoxView.TreeSpots): the Keeper's grown tree
        /// (KeeperTreeKit stage 4) or a wild one (Pieces.Tree, radius 0.16 x its scale).</summary>
        public static void CharmTie(bool keeperTree, float spotR, out float attachY, out float ringR)
        {
            if (keeperTree) { attachY = 0.4f; ringR = 0.11f; return; }
            float s = spotR / 0.16f;
            attachY = 0.27f * s; ringR = 0.12f * s;
        }
    }
}
