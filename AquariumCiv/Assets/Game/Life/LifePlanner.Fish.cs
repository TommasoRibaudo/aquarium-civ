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
            if (sim != null && !sim.FishingWater) return -1; // #421: no fishing of any kind until fish are placed
            debugFishers.Clear();
            debugFishMate.Clear();
            int n = 0;
            foreach (var p in people)
                if (p.Adult && p.job != JobType.Toolmaker && n < grownUps) { debugFishers[p.key] = (now.hour, now.hour + hours); n++; }
            return n == 0 ? -1 : n;
        }

        // #767: the "Fishers chat now" cheat: two fishers who fish with each other (the camp's rest day does the same with a couple)
        readonly Dictionary<int, PersonRef> debugFishMate = new();

        /// <summary>#767: sends two grown-ups (not the toolmaker) to fish the pond side by side, each the other's partner (the view turns them toward each other while they fish).
        /// Returns 2, or -1 with no water / fewer than two free.</summary>
        public int DebugFishersChat(ViewTime now, float hours = 2f)
        {
            int n = DebugFishNow(now, 2, hours);
            debugFishMate.Clear();
            if (n < 2) { debugFishers.Clear(); return -1; }
            Person first = null;
            foreach (var p in people)
            {
                if (!debugFishers.ContainsKey(p.key)) continue;
                if (first == null) first = p;
                else { debugFishMate[first.key] = p.who; debugFishMate[p.key] = first.who; break; }
            }
            return 2;
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
                partner = debugFishMate.TryGetValue(p.key, out var mate) ? mate : PersonRef.None,
                start = f.start, end = f.until, progress = (t.hour - f.start) / (f.until - f.start), pace = 1f, chronicleId = -1,
            };
            return true;
        }
    }
}
