using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.View
{
    /// <summary>
    /// A past year (or the moment between two) to draw instead of the live sim (#78): <see cref="SimLayout.Fill"/> keeps every
    /// lot, path and dressing rule but shows only the lots standing then, the paths worn then, and each lot's look of then.
    /// Lots never move, so the live settlement gives their spots. Yards, shapings and standing people are left out.
    /// While two years blend, the lots of both show and <see cref="grow"/> says how far each one's piece has come (0 = not
    /// there, 1 = whole): a lot only in the later year grows in, one only in the earlier shrinks away, one that changed
    /// look dips as it changes.
    /// </summary>
    public sealed class ReplayFrame
    {
        public int year;
        public Era era;
        public long month;
        public byte[] tiers;                              // site cells -> path tier (0..3)
        public readonly HashSet<int> lotIds = new();
        readonly Dictionary<int, Era> lookOfLot = new();
        /// <summary>Piece seed (<see cref="SimLayout.SeedOf"/>) to 0..1 growth, for lots that are mid-change.</summary>
        public readonly Dictionary<int, float> grow = new();

        /// <summary>The year to draw (<paramref name="shown"/>), blending towards <paramref name="other"/> by
        /// <paramref name="blend"/> (null/0 = settled).</summary>
        public ReplayFrame(BoxAtYear shown, int cells, BoxAtYear other = null, float blend = 0f, int steps = 6)
        {
            year = shown.year; era = shown.era; month = shown.month;
            tiers = new byte[cells];
            foreach (var p in shown.paths) if (p.cell < cells) tiers[p.cell] = (byte)p.tier;
            foreach (var b in shown.buildings) if (b.lotId > 0) { lotIds.Add(b.lotId); lookOfLot[b.lotId] = b.look; }
            if (other == null || other == shown) return;
            // blend is measured from the earlier year to the later; "shown" is whichever is nearer, so work in a -> b terms.
            var earlier = shown.year < other.year ? shown : other;
            var later = earlier == shown ? other : shown;
            float t = earlier == shown ? blend : 1f - blend;       // 0 at the earlier year, 1 at the later
            t = System.Math.Min(1f, System.Math.Max(0f, t));
            float q = (float)System.Math.Round(t * steps) / steps;
            var inEarlier = new Dictionary<int, BuildingAt>();
            foreach (var b in earlier.buildings) if (b.lotId > 0) inEarlier[b.lotId] = b;
            var inLater = new HashSet<int>();
            foreach (var b in later.buildings)
            {
                if (b.lotId <= 0) continue;
                inLater.Add(b.lotId);
                if (!inEarlier.TryGetValue(b.lotId, out var was)) { if (q > 0f) { lotIds.Add(b.lotId); grow[SimLayout.SeedOfLot(b.lotId)] = q; } continue; }
                lotIds.Add(b.lotId);
                if (was.look != b.look) grow[SimLayout.SeedOfLot(b.lotId)] = 1f - 0.18f * (1f - System.Math.Abs(2f * q - 1f)); // a dip as it changes
            }
            foreach (var kv in inEarlier)
                if (!inLater.Contains(kv.Key) && q < 1f) { lotIds.Add(kv.Key); if (!lookOfLot.ContainsKey(kv.Key)) lookOfLot[kv.Key] = kv.Value.look; grow[SimLayout.SeedOfLot(kv.Key)] = 1f - q; }
        }

        /// <summary>A lot shows: it had a building then, or (no building: a yard barn, a shaping's lot) it was founded by then and is not cleared now.</summary>
        public bool Shows(Lot l) => lotIds.Contains(l.id) || (l.buildingId == 0 && !l.retired && l.foundedMonth <= month);

        /// <summary>The era a lot is dressed in (null = the green: the year's era).</summary>
        public Era EraOf(Lot l) => l != null && lookOfLot.TryGetValue(l.id, out var e) ? e : era;
    }
}
