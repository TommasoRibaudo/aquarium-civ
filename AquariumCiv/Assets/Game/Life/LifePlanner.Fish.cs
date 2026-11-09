using AquariumCiv.Sim;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>#364 debug: "Fish bite now". A short override of the day for a few people: the grown-ups fish at the pond for a while, so the bites,
    /// the yank and the end beat (View/FishingView.cs, View/Beats/FishBeats.cs) can be watched at once. Nothing is planned or saved.</summary>
    public sealed partial class LifePlanner
    {
        readonly Dictionary<int, (float start, float until)> debugFishers = new();

        /// <summary>Sends up to <paramref name="grownUps"/> grown-ups (not the toolmaker) to fish the pond from now for <paramref name="hours"/> view hours.
        /// Returns who, or -1 when the pond has no water yet (the ecology layer's dry hollow), no fish were placed (#421), or nobody is free.</summary>
        public int DebugFishNow(ViewTime now, int grownUps = 2, float hours = 2f)
        {
            Sync();
            if (sim != null && !sim.FishingOpen) return -1; // #421: no fishing of any kind until fish are placed
            debugFishers.Clear();
            int n = 0;
            foreach (var p in people)
                if (p.Adult && p.job != JobType.Toolmaker && n < grownUps) { debugFishers[p.key] = (now.hour, now.hour + hours); n++; }
            return n == 0 ? -1 : n;
        }

        /// <summary>Keys of the people the cheat sent fishing (so the view can bring their first bite forward).</summary>
        public IEnumerable<int> DebugFisherKeys => debugFishers.Keys;

        bool DebugFishAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (debugFishers.Count == 0 || !debugFishers.TryGetValue(p.key, out var f)) return false;
            if (t.hour >= f.until) { debugFishers.Remove(p.key); return false; }
            if (t.hour < f.start) return false;
            var pond = Place.Land(PlaceKind.Pond);
            a = new PersonActivity
            {
                who = p.who, kind = ActivityKind.Fish, place = pond, from = pond, prop = Prop.Rod, slot = p.key % 3,
                start = f.start, end = f.until, progress = (t.hour - f.start) / (f.until - f.start), pace = 1f, chronicleId = -1,
            };
            return true;
        }
    }
}
