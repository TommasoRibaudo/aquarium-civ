namespace AquariumCiv.View
{
    // The prop enums of the peg doll (PegDoll.cs builds their meshes). Kept in a file with no UnityEngine, so
    // DollMotion.cs compiles outside Unity too (#352, Tools/AnimBench).
    public enum PropKind { None, Axe, Basket, Sheaf, Log, Lantern, Hoe, Bundle, Fish, Pot, Spear, Rod, Hammer, Broom, Sack, Stick, Bucket, Block, Barrel,
        // #92: the relics and the glass (a chunk of the Sky Crumb, the Festival Mask worn on the face, the Keeper Doll, the Left
        // Sandal, the Lens's folded tripod, the brass Lens, the scholar's slate, the measuring rod)
        Crumb, Mask, Doll, Sandal, Tripod, Lens, Slate, MeasuringRod,
        // #152: a bucket on its way to the water
        BucketEmpty,
        // #283: a caught rabbit over the shoulder
        Rabbit,
        // #267: a bucket of muddy water (scooped from the wet low ground or a puddle while the pond is dry)
        BucketMuddy,
        // #358: the kids' big ball
        Ball,
        // #365 B9: the meal's bowl (rides in the left hand)
        Bowl,
        // #359 (batch 2): the kids' duel stick, a short twig (the 0.24 staff read as a spear in a kid's hand)
        Twig,
        // P9 #426 (B13): the hunter's bow (a bark arc with a string) and the carrying pole
        Bow, Pole }

    /// <summary>Where a prop rides (life-in-the-box §9): in the right hand, on the head, on the back or on the shoulder,
    /// hugged in both arms, or rolled along the ground in front (a barrel).</summary>
    public enum PropSlot { Hand, Head, Back, Shoulder, Arms, Front }
}
