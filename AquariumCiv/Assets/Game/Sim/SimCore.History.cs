using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    // ---------- what is saved (compact: changes, not grids) ----------
    /// <summary>A building's look from a month on: the era it is dressed in (hut, cottage, house). The first entry is when it was first seen.</summary>
    [Serializable] public class LookChange { public int buildingId; public long month; public int look; }

    /// <summary>The month a building's lot was cleared (the camp hearth at the town's market).</summary>
    [Serializable] public class BuildingEnd { public int buildingId; public long month; }

    /// <summary>A shaping of the land as the time-lapse draws it: a Keeper's shaping (x, z, kind) or a remembering stone.
    /// <see cref="endMonth"/> is set when a take-back removed it.</summary>
    [Serializable]
    public class ShapeRec
    {
        public int nudgeId;      // 0 for a remembering stone
        public string kind;      // a NudgeKind, or "stone"
        public float x, z;
        public long month;
        public long endMonth = -1;
        public int era;
    }

    /// <summary>One year's path change. Each entry of <see cref="packed"/> is cell x 8 + value, value = tier (0-3) + 4 if laid.
    /// A keyframe lists every non-empty cell (every <see cref="SimConfig.historyKeyframeYears"/> years); a delta only the cells that changed.</summary>
    [Serializable]
    public class PathStep
    {
        public int year;
        public bool keyframe;
        public int[] packed;
    }

    /// <summary>Who held a role, from when to when, and how they won it (the rule: Contest, Election, Succession...).</summary>
    [Serializable]
    public class RoleTerm
    {
        public string role;
        public int holderId;
        public long fromMonth;
        public long toMonth = -1;   // -1 = still holds it
        public string how = "";
    }

    /// <summary>Where a chronicle entry happened, in hundredths of a box unit (for fly-to and pins).</summary>
    [Serializable] public class EntryPos { public int id; public int px, pz; }

    /// <summary>How many lines of a kind about a person were pruned, so the wording of repeating events carries on in turn.</summary>
    [Serializable] public class ToldTally { public string kind; public int notableId; public int count; }

    /// <summary>Game hours (real hours at the default pace) a notable has been the player's pin.</summary>
    [Serializable] public class PinTime { public int notableId; public float hours; }

    /// <summary>The history records (#73, chronicle-and-history.md section 6): what the time-lapse and the book need that the
    /// live state no longer shows. Everything here is write-only for the sim: nothing reads it back into the economy.</summary>
    [Serializable]
    public class HistoryState
    {
        public List<LookChange> looks = new();
        public List<BuildingEnd> ends = new();
        public List<ShapeRec> shapes = new();
        public List<PathStep> paths = new();
        public int lastPathYear = -1;
        public List<RoleTerm> roleTerms = new();
        public List<long> eraStarts = new();     // month each era began, by Era index
        public List<string> eraTitles = new();   // by Era index, written when the era ends and never changed
        public List<EntryPos> positions = new();
        public List<PinTime> pins = new();
        public List<ToldTally> told = new();     // pruned lines by kind and person (the story code counts them as told)
        public int prunedEntries;                // chronicle entries dropped so far
        public long prunedThroughMonth = -1;
    }

    // ---------- what the view asks for ----------
    [Serializable]
    public class BuildingAt
    {
        public int id, lotId;
        public string type;
        public float x, z, yaw, w, d;
        public Era look;
        public long foundedMonth;
    }

    [Serializable]
    public struct PathCellAt { public int cell; public int tier; public bool laid; }

    /// <summary>The box at the start of a game year: <see cref="SimCore.BoxAt"/>.</summary>
    [Serializable]
    public class BoxAtYear
    {
        public int year;                // 0-based, as <see cref="YearSnapshot.year"/> (the plate reads year + 1)
        public long month;
        public Era era;
        public int population, notables;
        public float mood;
        public List<BuildingAt> buildings = new();
        public List<PathCellAt> paths = new();
        public List<ShapeRec> shapings = new();
        public int leaderId = -1;
        public string leaderRole;
    }

    /// <summary>
    /// History records (#73; docs/design/chronicle-and-history.md sections 3.5 and 6) for the time-lapse and the book: per-building
    /// looks and the month a lot was cleared, per-year path changes with keyframes, shapings with their spots, role
    /// history, era starts and titles, pin time, positions on chronicle entries, and the pruning of a long chronicle.
    /// Where a building stands (x, z, yaw, size) is already on <see cref="Building"/> and its <see cref="Lot"/> (lots never move).
    /// Observes only: reads the state at the end of each month, draws no dice, and nothing in the economy reads the records
    /// (switch <see cref="SimConfig.recordHistory"/> off and the economy is identical; HistoryTests).
    /// </summary>
    public partial class SimCore
    {
        int[] lastPaths;                          // the path cells as of the last recorded year (rebuilt from the records)
        Dictionary<int, int> lookNow;             // building to its latest recorded look
        HashSet<int> positionIds;                 // chronicle entries with a stored position

        HistoryState Hist => State.history ??= new HistoryState();

        /// <summary>End of every month (and once at founding): records what changed. Prunes once a year.</summary>
        public void HistoryStep()
        {
            if (!Config.recordHistory) return;
            var s = State;
            var h = Hist;
            HistoryEras(h);
            HistoryBuildings(h);
            HistoryShapes(h);
            HistoryRoles(h);
            HistoryPin(h);
            if (s.month % SimConfig.MonthsPerYear == 0 || h.lastPathYear < 0) HistoryPaths(h);
            HistoryPositions(h);
            if (s.month % SimConfig.MonthsPerYear == 0 && s.month > 0) PruneChronicle();
        }

        // ---------- eras ----------
        void HistoryEras(HistoryState h)
        {
            var s = State;
            while (h.eraStarts.Count <= (int)s.era)
            {
                int i = h.eraStarts.Count;
                if (i == 0) h.eraStarts.Add(0);
                else if (i == (int)s.era) h.eraStarts.Add(s.eraStartMonth);
                else h.eraStarts.Add(s.chronicle.Where(e => e.kind == "era").Select(e => e.month).Skip(i - 1).DefaultIfEmpty(s.eraStartMonth).First());
            }
            while (h.eraTitles.Count < (int)s.era || (s.boxComplete && h.eraTitles.Count <= (int)s.era)) h.eraTitles.Add(MakeEraTitle((Era)h.eraTitles.Count));
        }

        /// <summary>The month an era began (the camp: 0); -1 if the box has not reached it.</summary>
        public long EraStartMonth(Era era)
        {
            HistoryEras(Hist);
            return (int)era < Hist.eraStarts.Count ? Hist.eraStarts[(int)era] : -1;
        }

        /// <summary>The era's title for the book ("The Years of the Sky Bread"), written when the era ends (the town's when the
        /// box completes) and never changed after. "" while the era goes on.</summary>
        public string EraTitle(Era era)
        {
            HistoryEras(Hist);
            return (int)era < Hist.eraTitles.Count ? Hist.eraTitles[(int)era] : "";
        }

        /// <summary>The era's top story gives the title: a relic's name, a person's, or the thing that happened.</summary>
        string MakeEraTitle(Era era)
        {
            var s = State;
            long from = (int)era < Hist.eraStarts.Count ? Hist.eraStarts[(int)era] : 0;
            long to = (int)era + 1 < Hist.eraStarts.Count ? Hist.eraStarts[(int)era + 1] : long.MaxValue;
            var top = s.chronicle
                .Where(e => e.month >= from && e.month < to && e.salience >= 0.5f && !ChapterSkips(e.kind) && e.kind != "era_festival" && e.kind != "era_cleared")
                .OrderByDescending(e => e.salience).ThenBy(e => e.month).ThenBy(e => e.id).FirstOrDefault();
            string noun = EraNoun(era);
            string fallback = $"The {char.ToUpperInvariant(noun[0])}{noun.Substring(1)} Years";
            if (top == null) return fallback;
            string relic = top.relicId switch
            {
                RelicId.SkyCrumb => "the Sky Bread", RelicId.FestivalMask => "the Festival Mask", RelicId.KeeperDoll => "the Keeper Doll",
                RelicId.LeftSandal => "the Lost Sandal", RelicId.Lens => "the Great Eye-Glass", _ => null,
            };
            if (relic != null) return $"The Years of {relic}";
            string thing = top.kind switch
            {
                "keeper" => "the Keeper's Hand", "storm" => "the Great Storm", "drought" => "the Dry Summer", "bumper" => "the Full Barns",
                "lean_winter" => "the Hungry Winter", "snow" => "the Deep Snow", "fog" => "the Long Fog", "strange_light" => "the Strange Light",
                "rainbow" => "the Rainbow", "festival" or "anniversary" => "the Feast", "sheep" => "the Wandering Sheep", "ducks" => "the Ducks",
                "peak-ready" => "the Full Glass", "sweepers" => "the Sweepers", "milestone" => "the Growing Crowd",
                _ => null,
            };
            if (thing != null) return $"The Years of {thing}";
            var who = FindNotable(top.notableId);
            if (who != null && top.kind != "newcomer" && top.kind != "rise") return $"The Years of {who.name}";
            return fallback;
        }

        // ---------- buildings: looks and cleared lots ----------
        void HistoryBuildings(HistoryState h)
        {
            var s = State;
            if (lookNow == null)
            {
                lookNow = new Dictionary<int, int>();
                foreach (var c in h.looks) lookNow[c.buildingId] = c.look; // in month order, so the last wins
            }
            foreach (var b in s.buildings)
                if (!lookNow.TryGetValue(b.id, out int look) || look != (int)s.era)
                {
                    h.looks.Add(new LookChange { buildingId = b.id, month = s.month, look = (int)s.era });
                    lookNow[b.id] = (int)s.era;
                }
            var lots = s.settle?.lots;
            if (lots == null) return;
            foreach (var l in lots)
                if (l.retired && l.buildingId > 0 && !h.ends.Any(e => e.buildingId == l.buildingId))
                    h.ends.Add(new BuildingEnd { buildingId = l.buildingId, month = s.month });
        }

        // ---------- shapings and remembering stones ----------
        void HistoryShapes(HistoryState h)
        {
            var s = State;
            foreach (var n in s.nudges)
                if (!h.shapes.Any(r => r.nudgeId == n.id && r.nudgeId != 0))
                    h.shapes.Add(new ShapeRec { nudgeId = n.id, kind = n.kind, x = n.x, z = n.z, month = n.month, era = n.eraAtPlace });
            foreach (var r in h.shapes)
                if (r.nudgeId != 0 && r.endMonth < 0 && !s.nudges.Any(n => n.id == r.nudgeId)) r.endMonth = s.month; // taken back
            var sandal = FindRelic(RelicId.LeftSandal);
            if (sandal != null && sandal.Found && !h.shapes.Any(r => r.kind == "stone"))
                h.shapes.Add(new ShapeRec { kind = "stone", x = sandal.x, z = sandal.z, month = sandal.foundMonth, era = (int)s.era });
        }

        // ---------- roles ----------
        void HistoryRoles(HistoryState h)
        {
            var s = State;
            foreach (var t in h.roleTerms)
            {
                if (t.toMonth >= 0) continue;
                var seat = s.roles.FirstOrDefault(r => r.role == t.role);
                if (seat == null || seat.holderId != t.holderId) t.toMonth = s.month;
            }
            foreach (var seat in s.roles)
            {
                if (seat.holderId < 0) continue;
                if (h.roleTerms.Any(t => t.role == seat.role && t.toMonth < 0 && t.holderId == seat.holderId)) continue;
                string how = RoleTable.Any(r => r.era == s.era && r.role == seat.role) ? RuleOf(s.era, seat.role).ToString() : "";
                h.roleTerms.Add(new RoleTerm { role = seat.role, holderId = seat.holderId, fromMonth = seat.sinceMonth, how = how });
            }
        }

        /// <summary>Every term of a role (or of all roles), oldest first.</summary>
        public IEnumerable<RoleTerm> RoleHistory(string role = null) =>
            Hist.roleTerms.Where(t => role == null || t.role == role);

        /// <summary>Who held the role in this month (-1 = nobody).</summary>
        public int RoleHolderAt(string role, long month)
        {
            var t = Hist.roleTerms.LastOrDefault(r => r.role == role && r.fromMonth <= month && (r.toMonth < 0 || r.toMonth > month));
            return t?.holderId ?? -1;
        }

        /// <summary>Terms of this person in office, with how they won each (their legend page).</summary>
        public IEnumerable<RoleTerm> TermsOf(int notableId) => Hist.roleTerms.Where(t => t.holderId == notableId);

        // ---------- the player's pin ----------
        void HistoryPin(HistoryState h)
        {
            int id = State.pinnedNotableId;
            if (id < 0) return;
            var p = h.pins.FirstOrDefault(x => x.notableId == id);
            if (p == null) h.pins.Add(p = new PinTime { notableId = id });
            p.hours += (float)Config.StepHours;
        }

        /// <summary>Hours this person has been the player's pin (counted per game month, so a long absence counts at the slow pace).</summary>
        public float PinnedHours(int notableId) => Hist.pins.FirstOrDefault(p => p.notableId == notableId)?.hours ?? 0f;

        /// <summary>"Pinned for at least a day" (the legend rule, chronicle-and-history.md section 4.1).</summary>
        public bool PinnedLongEnough(int notableId) => PinnedHours(notableId) >= Config.pinLegendHours;

        // ---------- paths: deltas and keyframes ----------
        int[] CurrentPaths()
        {
            var st = State.settle;
            if (st == null || st.tier == null || st.flags == null) return null;
            var cur = new int[st.tier.Length];
            for (int i = 0; i < cur.Length; i++) cur[i] = (st.tier[i] & 3) | ((st.flags[i] & SiteMap.FLaid) != 0 ? 4 : 0);
            return cur;
        }

        void HistoryPaths(HistoryState h)
        {
            var s = State;
            int year = (int)(s.month / SimConfig.MonthsPerYear);
            if (h.lastPathYear == year) return;
            var cur = CurrentPaths();
            if (cur == null) return;
            var last = lastPaths ?? (h.lastPathYear < 0 ? new int[cur.Length] : ReplayPaths(h, h.lastPathYear, cur.Length));
            bool key = h.paths.Count == 0 || year % Math.Max(1, Config.historyKeyframeYears) == 0;
            var packed = new List<int>();
            for (int i = 0; i < cur.Length; i++)
                if (key ? cur[i] != 0 : cur[i] != last[i]) packed.Add(i * 8 + cur[i]);
            if (key || packed.Count > 0) h.paths.Add(new PathStep { year = year, keyframe = key, packed = packed.ToArray() });
            h.lastPathYear = year;
            lastPaths = cur;
        }

        static int[] ReplayPaths(HistoryState h, int year, int cells)
        {
            var a = new int[cells];
            foreach (var p in h.paths)
            {
                if (p.year > year) break;
                if (p.keyframe) Array.Clear(a, 0, a.Length);
                foreach (int v in p.packed) if (v / 8 < a.Length) a[v / 8] = v % 8;
            }
            return a;
        }

        // ---------- positions of chronicle entries ----------
        void HistoryPositions(HistoryState h)
        {
            var s = State;
            if (positionIds == null) positionIds = new HashSet<int>(h.positions.Select(p => p.id));
            for (int i = s.chronicle.Count - 1; i >= 0; i--)
            {
                var e = s.chronicle[i];
                if (e.month < s.month - 1) break;
                if (e.salience < Config.historyPositionMinSalience || positionIds.Contains(e.id) || IsTermLine(e)) continue;
                if (!ResolveEntryPos(e, out float x, out float z)) continue;
                h.positions.Add(new EntryPos { id = e.id, px = (int)Math.Round(x * 100f), pz = (int)Math.Round(z * 100f) });
                positionIds.Add(e.id);
            }
        }

        /// <summary>Where an entry happened, for fly-to and pins: its stored spot, else worked out now (the building, the relic,
        /// or the person's home). False for box-wide entries (an era, the weather).</summary>
        public bool EntryPosition(ChronicleEntry e, out float x, out float z)
        {
            var p = Hist.positions.FirstOrDefault(q => q.id == e.id);
            if (p != null) { x = p.px / 100f; z = p.pz / 100f; return true; }
            return ResolveEntryPos(e, out x, out z);
        }

        bool ResolveEntryPos(ChronicleEntry e, out float x, out float z)
        {
            x = z = 0f;
            var st = State.settle;
            if (st == null || st.version == 0) return false;
            if (e.buildingId >= 0)
            {
                var b = State.buildings.FirstOrDefault(q => q.id == e.buildingId);
                if (b != null && b.lotId > 0) { x = b.x; z = b.z; return true; }
            }
            if (!string.IsNullOrEmpty(e.relicId))
            {
                var r = FindRelic(e.relicId);
                if (r != null && r.Found) { x = r.x; z = r.z; return true; }
            }
            var who = e.notableId >= 0 ? FindNotable(e.notableId) : null;
            if (who != null && Config.settle)
            {
                var home = Settlement?.HomeOf(who);
                if (home != null) { x = home.x; z = home.z; return true; }
            }
            return false;
        }

        // ---------- the box at a year ----------
        /// <summary>The box at the start of game year <paramref name="year"/> (0-based, month year x 12), as the time-lapse
        /// draws it: buildings with their lot, size and look, the paths, the shapings, the head count. Rebuilt from the
        /// records (a keyframe and the deltas after it). Null for a year that has not happened.</summary>
        public BoxAtYear BoxAt(int year)
        {
            var s = State;
            long month = (long)year * SimConfig.MonthsPerYear;
            if (year < 0 || month > s.month) return null;
            var h = Hist;
            HistoryEras(h);
            var r = new BoxAtYear { year = year, month = month };
            int era = 0;
            for (int i = 1; i < h.eraStarts.Count; i++) if (h.eraStarts[i] < month) era = i; // a snapshot at the change month is taken before the change
            r.era = (Era)era;
            var snap = s.years.LastOrDefault(y => y.year <= year);
            if (snap != null) { r.population = snap.population; r.notables = snap.notables; r.mood = snap.mood; }
            var lots = s.settle?.lots;
            foreach (var b in s.buildings)
            {
                var lot = b.lotId > 0 ? lots?.Find(l => l.id == b.lotId) : null;
                long appeared = lot != null ? lot.foundedMonth : (lots == null || s.settle.version == 0 ? b.foundedMonth : long.MaxValue);
                if (appeared > month) continue;
                var end = h.ends.Find(e => e.buildingId == b.id);
                if (end != null && end.month <= month) continue;
                var look = h.looks.LastOrDefault(l => l.buildingId == b.id && l.month <= month);
                r.buildings.Add(new BuildingAt
                {
                    id = b.id, type = b.type, lotId = b.lotId, x = b.x, z = b.z, yaw = b.yaw, w = b.w, d = b.d,
                    look = (Era)(look?.look ?? era), foundedMonth = appeared,
                });
            }
            int cells = s.settle?.tier?.Length ?? 0;
            if (cells > 0 && h.paths.Count > 0 && h.paths[0].year <= year)
            {
                var a = ReplayPaths(h, year, cells);
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != 0) r.paths.Add(new PathCellAt { cell = i, tier = a[i] & 3, laid = (a[i] & 4) != 0 });
            }
            foreach (var sh in h.shapes)
                if (sh.month <= month && (sh.endMonth < 0 || sh.endMonth > month)) r.shapings.Add(sh);
            string role = LeaderOf((Era)era);
            int holder = RoleHolderAt(role, month);
            r.leaderRole = role; r.leaderId = holder;
            return r;
        }

        // ---------- pruning a long chronicle ----------
        static bool IsTermLine(ChronicleEntry e) => e.id >= TermLineIdBase && e.id < RelicLineIdBase;

        int PrunedTold(string kind, int notableId)
        {
            var told = State.history?.told;
            if (told == null || told.Count == 0) return 0;
            int n = 0;
            foreach (var t in told) if (t.kind == kind && (notableId < 0 || t.notableId == notableId)) n += t.count;
            return n;
        }

        /// <summary>An entry the book (or a card) may need: kept for ever.</summary>
        public bool IsBookWorthy(ChronicleEntry e)
        {
            if (e.salience >= 0.75f || e.keeper || !string.IsNullOrEmpty(e.relicId)) return true;
            switch (e.kind)
            {
                case "founding": case "era": case "era-ready": case "era_festival": case "era_cleared": case "peak-ready":
                case "box-complete": case "founders_left": case "first_building": case "naming":
                    return true;
            }
            return false;
        }

        /// <summary>After <see cref="SimConfig.historyPruneYears"/> years, drops old entries below
        /// <see cref="SimConfig.historyPruneBelow"/> that are not book-worthy, not on anyone's top five (by salience) and not
        /// held by a relic, wish or thread. Returns how many went. Runs once a year from <see cref="HistoryStep"/>.</summary>
        public int PruneChronicle()
        {
            var s = State;
            var h = Hist;
            if (Config.historyPruneYears <= 0) return 0;
            long cutoff = s.month - (long)Config.historyPruneYears * SimConfig.MonthsPerYear;
            if (cutoff <= 0) return 0;
            var keep = new HashSet<int>();
            foreach (var r in s.relics) { keep.Add(r.entryId); keep.Add(r.lostEntryId); }
            foreach (var w in s.wishes) keep.Add(w.entryId);
            foreach (var t in s.threads) { keep.Add(t.payoffEntryId); keep.Add(t.endEntryId); }
            var byPerson = new Dictionary<int, List<ChronicleEntry>>();
            void Add(int who, ChronicleEntry e)
            {
                if (who < 0) return;
                if (!byPerson.TryGetValue(who, out var l)) byPerson[who] = l = new List<ChronicleEntry>();
                l.Add(e);
            }
            foreach (var e in s.chronicle)
            {
                if (IsTermLine(e)) continue; // the flavour never decides which stories stay
                Add(e.notableId, e);
                if (e.otherNotableId != e.notableId) Add(e.otherNotableId, e);
            }
            foreach (var l in byPerson.Values)
                foreach (var e in l.OrderByDescending(e => e.salience).ThenBy(e => e.month).ThenBy(e => e.id).Take(5)) keep.Add(e.id);
            var gone = s.chronicle.Where(e => e.month < cutoff && e.salience < Config.historyPruneBelow && !IsBookWorthy(e) && !keep.Contains(e.id)).ToList();
            foreach (var e in gone)
            {
                var t = h.told.Find(x => x.kind == e.kind && x.notableId == e.notableId);
                if (t == null) h.told.Add(t = new ToldTally { kind = e.kind, notableId = e.notableId });
                t.count++;
            }
            var goneIds = new HashSet<ChronicleEntry>(gone);
            int n = s.chronicle.RemoveAll(goneIds.Contains);
            if (n > 0)
            {
                var live = new HashSet<int>(s.chronicle.Select(e => e.id));
                h.positions.RemoveAll(p => !live.Contains(p.id));
                positionIds = null;
                h.prunedEntries += n;
            }
            h.prunedThroughMonth = cutoff;
            return n;
        }
    }
}
