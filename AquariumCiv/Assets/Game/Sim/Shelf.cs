using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>Box 2's setting (era-moment.md Choice 11: the desert oasis is the furthest from box 1's river valley).</summary>
    public static class BoxSetting
    {
        public const string RiverValley = "river_valley", DesertOasis = "desert_oasis", MountainVillage = "mountain_village";
        public static bool IsNextBoxSetting(string s) => s == DesertOasis || s == MountainVillage;
    }

    /// <summary>A founder of a later box and who they were in the box they left (a link back, not a shared object).</summary>
    [Serializable]
    public class FounderLink
    {
        public int notableId;      // in the new box
        public int fromBox;        // the shelf index of the box they came from
        public int fromNotableId;  // their id there
    }

    /// <summary>A relic standing on a plinth in front of a finished box (era-moment.md section 8, "The museum on the shelf").</summary>
    [Serializable]
    public class Plinth
    {
        public int boxIndex;       // the box whose story it belongs to
        public string relicId;
        public long foundMonth;    // orders the plinths by era
        public bool carried;       // it also crossed to the next box
    }

    /// <summary>One box on the shelf. The active box's <see cref="state"/> is the save envelope's own state (not repeated in
    /// the file); every finished box keeps its own.</summary>
    [Serializable]
    public class ShelfBox
    {
        public int index;
        public string setting = BoxSetting.RiverValley;
        public bool museum;                // finished: a museum on the shelf
        public string plate = "";          // "Glasswick - years 1-118 - camp, village, town"
        public string lastSeenUtc = "";    // the museum was last visited (or finished) then: its catch-up counts from here
        public int fromBox = -1;           // the box its founders came from (-1 = the first box)
        public List<FounderLink> founders = new();
        public SimState state;
    }

    /// <summary>
    /// The shelf (#87; era-moment.md sections 8-10): the boxes side by side, which one is being played, and the relics standing
    /// on plinths. Saved in the version-3 envelope; a version-2 save becomes a shelf of one box (<see cref="Single"/>).
    /// </summary>
    [Serializable]
    public class ShelfState
    {
        public int version = 1;
        public int activeIndex;
        public List<ShelfBox> boxes = new();
        public List<Plinth> plinths = new();

        public ShelfBox Active => boxes.FirstOrDefault(b => b.index == activeIndex);
        public ShelfBox Box(int index) => boxes.FirstOrDefault(b => b.index == index);
        public int NextIndex => boxes.Count == 0 ? 0 : boxes.Max(b => b.index) + 1;

        /// <summary>A shelf of one box: what every save from before the shelf becomes.</summary>
        public static ShelfState Single(SimState state) => new()
        {
            boxes = { new ShelfBox { index = 0, state = state } },
        };

        /// <summary>Reads a loaded shelf next to its envelope's active state: puts the state back on the active box (or makes a
        /// shelf of one if the file had none). Returns the shelf to keep.</summary>
        public static ShelfState Attach(ShelfState shelf, SimState active)
        {
            if (shelf == null || shelf.boxes == null || shelf.boxes.Count == 0) return Single(active);
            var box = shelf.Active ?? shelf.boxes[0];
            shelf.activeIndex = box.index;
            box.state = active;
            return shelf;
        }

        /// <summary>The shelf as the file holds it: the active box's state is left out (the envelope carries it).</summary>
        public ShelfState ForSave() => new()
        {
            version = version, activeIndex = activeIndex, plinths = plinths,
            boxes = boxes.Select(b => b.index == activeIndex ? Clone(b, null) : b).ToList(),
        };

        static ShelfBox Clone(ShelfBox b, SimState state) => new()
        {
            index = b.index, setting = b.setting, museum = b.museum, plate = b.plate, lastSeenUtc = b.lastSeenUtc,
            fromBox = b.fromBox, founders = b.founders, state = state,
        };
    }

    /// <summary>The shelf's actions: finish the active box, start the next one, visit a museum.</summary>
    public static class Shelf
    {
        /// <summary>The player has been through <see cref="SimCore.CompleteBox"/>: the active box becomes a museum. Its glass is
        /// left museum-clean (the last wipe) and its relics go on plinths in front. Returns the box.</summary>
        public static ShelfBox FinishActive(ShelfState shelf, SimCore sim, BoxCompletion completion, DateTime nowUtc)
        {
            var box = shelf.Active ?? throw new InvalidOperationException("no active box");
            if (!sim.State.boxComplete) throw new InvalidOperationException("the box is not complete");
            var s = sim.State;
            s.grime = 0; s.overgrowth = 0; s.trashAccum = 0; s.trash.Clear(); // museum glass doesn't get dirty
            box.state = s;
            box.museum = true;
            box.plate = completion?.plate ?? $"{sim.PlaceName} - years 1-{SimCore.YearNo(s.month)}";
            box.lastSeenUtc = Stamp(nowUtc);
            var crossing = new HashSet<string>(sim.RelicsForCrossing());
            shelf.plinths.RemoveAll(p => p.boxIndex == box.index);
            foreach (var r in sim.FoundRelics)
                shelf.plinths.Add(new Plinth { boxIndex = box.index, relicId = r.id, foundMonth = r.foundMonth, carried = r.takenMonth >= 0 || crossing.Contains(r.id) });
            return box;
        }

        /// <summary>Box 2 (or later): a new box in <paramref name="setting"/> with the finished box's founders copied in (a link
        /// back to who they were) and the carried relics. The finished box must be a museum. The new box becomes the active one.
        /// The new box's own play stays out of the slice.</summary>
        public static SimCore StartNextBox(ShelfState shelf, SimConfig config, string setting, int seed, DateTime nowUtc, BoxCompletion completion = null)
        {
            var from = shelf.Active ?? throw new InvalidOperationException("no active box");
            if (!from.museum || from.state == null) throw new InvalidOperationException("finish the box first");
            if (!BoxSetting.IsNextBoxSetting(setting)) throw new ArgumentException("not a setting for the next box: " + setting);
            var finished = new SimCore(config, from.state);
            var next = new ShelfBox { index = shelf.NextIndex, setting = setting, fromBox = from.index };
            var sim = SimCore.SeedNextBox(config, finished, seed, next.index, completion, out var links);
            next.founders = links;
            next.state = sim.State;
            next.lastSeenUtc = Stamp(nowUtc);
            shelf.boxes.Add(next);
            shelf.activeIndex = next.index;
            return sim;
        }

        /// <summary>Opens a finished box: its people live the real time since the last visit, cheaply
        /// (<see cref="SimCore.MuseumAdvance"/>). The shelf's active box does not change.</summary>
        public static SimCore Visit(ShelfState shelf, int index, SimConfig config, DateTime nowUtc, out MuseumResult result)
        {
            var box = shelf.Box(index) ?? throw new ArgumentException("no such box");
            if (!box.museum || box.state == null) throw new InvalidOperationException("only a finished box can be visited");
            double hours = 0;
            if (DateTime.TryParse(box.lastSeenUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last))
                hours = Math.Max(0, (nowUtc - last.ToUniversalTime()).TotalHours);
            var sim = new SimCore(config, box.state);
            result = sim.MuseumAdvance(hours);
            box.lastSeenUtc = Stamp(nowUtc);
            return sim;
        }

        static string Stamp(DateTime t) => t.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }
}
