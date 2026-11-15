namespace AquariumCiv.Sim
{
    /// <summary>#676 (private note 65): what the camp's stew pot holds, from what the camp has. Pure; the view only reads it.</summary>
    public enum StewKind
    {
        /// <summary>Nothing to put in: a thin broth with a few greens.</summary>
        Thin = 0,
        /// <summary>Berries and roots, when that is all there is.</summary>
        Berries = 1,
        /// <summary>Dried fish on the food pile.</summary>
        Fish = 2,
        /// <summary>A rabbit hunt brought meat home today or yesterday.</summary>
        Rabbit = 3,
    }

    public static class StewKinds
    {
        /// <summary>How many view days a rabbit hunt's meat goes in the pot (the day of the catch and the next).</summary>
        public const int RabbitDays = 1;

        /// <summary>The pot's kind. Priority: fresh rabbit meat (a hunt in the last <see cref="RabbitDays"/> + 1 days) first, then fish
        /// on the food pile, then berries and roots (any food in the stores), else a thin broth.
        /// <paramref name="rabbitCatchDay"/> is -1 when no hunt ever caught one.</summary>
        public static StewKind Decide(long today, long rabbitCatchDay, int fishInPile, float food)
        {
            if (rabbitCatchDay >= 0 && today >= rabbitCatchDay && today - rabbitCatchDay <= RabbitDays) return StewKind.Rabbit;
            if (fishInPile > 0) return StewKind.Fish;
            if (food >= 1f) return StewKind.Berries;
            return StewKind.Thin;
        }
    }
}
