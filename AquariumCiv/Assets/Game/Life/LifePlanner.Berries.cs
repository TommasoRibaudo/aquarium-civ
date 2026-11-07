using AquariumCiv.Sim;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>#361 debug: "Pickers to the nearest bush now". A short override of the day for a few people (an hour of view time): the
    /// grown-ups gather at the bush nearest the camp, a kid eats from it, so the berry beats can be watched at once. Nothing is planned or saved.</summary>
    public sealed partial class LifePlanner
    {
        readonly Dictionary<int, (int cell, float start, float until, bool eat)> debugPickers = new();

        /// <summary>Sends up to <paramref name="grownUps"/> grown-ups (not the toolmaker) and one kid to the bush nearest the camp for
        /// <paramref name="hours"/> view hours, starting now. Returns the bush cell, or -1 (no living land, no bush, nobody).</summary>
        public int DebugPickersNow(ViewTime now, int grownUps = 3, float hours = 0.8f)
        {
            Sync();
            if (sim == null || !sim.EcologyOn) return -1;
            var g = sim.EcoGround;
            int season = (int)LifeClock.SeasonOf(sim.State.month);
            sim.NearestBushes(g.campX, g.campZ, 1, now.day, season, true, bushScratch);
            if (bushScratch.Count == 0) sim.NearestBushes(g.campX, g.campZ, 1, now.day, season, false, bushScratch);
            if (bushScratch.Count == 0) return -1;
            int cell = bushScratch[0];
            debugPickers.Clear();
            int adults = 0; bool kid = false;
            foreach (var p in people)
            {
                if (p.Adult && p.job != JobType.Toolmaker && adults < grownUps) { debugPickers[p.key] = (cell, now.hour, now.hour + hours, false); adults++; }
                else if (!p.Adult && p.stage == LifeStage.Kid && !kid) { debugPickers[p.key] = (cell, now.hour, now.hour + hours, true); kid = true; }
            }
            return debugPickers.Count > 0 ? cell : -1;
        }

        /// <summary>"Root dig now": up to three grown-ups (not the toolmaker) dig roots at the stream bed for <paramref name="hours"/> view hours. Returns how many.</summary>
        public int DebugRootDigNow(ViewTime now, int grownUps = 3, float hours = 0.8f)
        {
            Sync();
            debugPickers.Clear();
            int adults = 0;
            foreach (var p in people)
                if (p.Adult && p.job != JobType.Toolmaker && adults < grownUps) { debugPickers[p.key] = (-1, now.hour, now.hour + hours, false); adults++; }
            return adults;
        }

        readonly Dictionary<int, (float start, float until)> debugTips = new();

        /// <summary>"Tip at the stash now": up to <paramref name="grownUps"/> grown-ups (not the toolmaker) walk to the camp's stash pile with a basket and tip it
        /// (a TipLoad block of <paramref name="hours"/> view hours: the beat plays once they stand still at the pile). Returns how many, or -1 when there is no stash pile
        /// (it is the camp's: from the village on the load goes to the fire).</summary>
        public int DebugTipNow(ViewTime now, int grownUps = 3, float hours = 0.25f)
        {
            Sync();
            if (stash.kind != PlaceKind.Stash) return -1;
            debugTips.Clear();
            int n = 0;
            foreach (var p in people)
                if (p.Adult && p.job != JobType.Toolmaker && n < grownUps) { debugTips[p.key] = (now.hour, now.hour + hours); n++; }
            return n;
        }

        bool DebugPickerAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (debugTips.Count > 0 && debugTips.TryGetValue(p.key, out var tip))
            {
                if (t.hour >= tip.until) debugTips.Remove(p.key);
                else if (t.hour >= tip.start)
                {
                    a = new PersonActivity
                    {
                        who = p.who, kind = ActivityKind.TipLoad, place = stash, from = stash, prop = Prop.Basket,
                        start = tip.start, end = tip.until, progress = (t.hour - tip.start) / (tip.until - tip.start), pace = 1f, chronicleId = -1,
                    };
                    return true;
                }
            }
            if (debugPickers.Count == 0 || !debugPickers.TryGetValue(p.key, out var d)) return false;
            if (t.hour >= d.until || t.hour < d.start) { if (t.hour >= d.until) debugPickers.Remove(p.key); return false; }
            var place = d.cell < 0 ? Place.Land(PlaceKind.Stream) : Place.Land(PlaceKind.BushCell, d.cell);
            a = new PersonActivity
            {
                who = p.who, kind = d.eat ? ActivityKind.Eat : ActivityKind.Gather, place = place, from = place, prop = d.eat ? Prop.None : Prop.Basket,
                start = d.start, end = d.until, progress = (t.hour - d.start) / (d.until - d.start), pace = 1f, chronicleId = -1,
            };
            return true;
        }
    }
}
