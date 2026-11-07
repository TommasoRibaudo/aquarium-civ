using System.Collections.Generic;

namespace AquariumCiv.Life
{
    public sealed partial class LifePlanner
    {
        // #360 debug: people sent to draw water at dawn whatever the dice said ("Water run now").
        readonly HashSet<int> forcedWater = new HashSet<int>();

        /// <summary>Debug (#360): the first <paramref name="count"/> grown people draw water at the next dawn too (their day is replanned). Returns who.</summary>
        public string ForceWaterRun(int count)
        {
            Sync();
            var names = new List<string>();
            foreach (var p in people)
            {
                if (names.Count >= count) break;
                if (p == null || p.stage != LifeStage.Adult) continue;
                forcedWater.Add(p.key);
                names.Add(p.who.ToString());
            }
            ClearDays();
            return names.Count == 0 ? "no adults to send" : "water run: " + string.Join(", ", names);
        }
    }
}
