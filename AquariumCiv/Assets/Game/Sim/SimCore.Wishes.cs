using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    public enum WishState { Open, Given, Lapsed, Met }

    /// <summary>One notable's wish for a shaping (#83): a tool, a spot, and a chronicle entry that becomes a Wish card.</summary>
    [Serializable]
    public class Wish
    {
        public int id;
        public int notableId;
        public string kind = "";        // SimCore.NudgeKind
        public float x, z;              // where it points
        public long month;              // raised
        public long endMonth = -1;      // given, lapsed or met
        public int entryId = -1;        // its chronicle entry (kind "wish")
        public int nudgeId = -1;        // the shaping that answered it (Given)
        public WishState state;
    }

    /// <summary>
    /// Notables wish for a gift in a card (#83; decisions.md "Shaping the land", research 10 "Wishes in cards"). By job
    /// (a farmer wants rain, a woodcutter a grove, a hunter deer, a fisher fish) a notable sometimes dreams of a shaping on a
    /// spot: a chronicle entry of kind "wish" (the card). At most one wish is open at a time (so at most one per return),
    /// a gap of <see cref="WishGapMonths"/> follows each. <see cref="GiveWish"/> places the shaping at the wish's spot,
    /// charges permitting, and the people's answer is credited to the wisher. There is never a timer or a penalty: an
    /// ignored wish lapses quietly after <see cref="WishLapseMonths"/> (or earlier if the wisher dies, or the Keeper gives
    /// that gift on their own), and its card stops being shown. No dice stream is drawn (a hash of the seed, person and
    /// month), and nothing in the economy or mood reads a wish.
    /// </summary>
    public partial class SimCore
    {
        public const int WishGapMonths = 8, WishLapseMonths = 24;
        /// <summary>Wish entries take their own ids (above the term lines'), so the shared ids never shift.</summary>
        public const int WishIdBase = 1_100_000_000;
        public const float WishChancePerMonth = 0.12f;

        /// <summary>Debug switch: false stops new wishes (the tests run the box both ways to show the economy is the same).</summary>
        public bool WishesOn { get; set; } = true;

        /// <summary>What each job dreams of, in order of liking.</summary>
        static readonly Dictionary<string, string[]> WishTools = new()
        {
            [JobType.Farmer] = new[] { NudgeKind.Rain, NudgeKind.Channel },
            [JobType.Gatherer] = new[] { NudgeKind.Grove, NudgeKind.Rain },
            [JobType.Hunter] = new[] { NudgeKind.Herd },
            [JobType.Fisher] = new[] { NudgeKind.Herd },
            [JobType.Woodcutter] = new[] { NudgeKind.Grove },
            [JobType.Toolmaker] = new[] { NudgeKind.Grove },
            [JobType.Shepherd] = new[] { NudgeKind.Herd },
            [JobType.Beekeeper] = new[] { NudgeKind.Grove },
            [JobType.Potter] = new[] { NudgeKind.Channel },
            [JobType.Brewer] = new[] { NudgeKind.Channel, NudgeKind.Rain },
            [JobType.Mason] = new[] { NudgeKind.Channel },
            [JobType.Smith] = new[] { NudgeKind.Channel },
            [JobType.Weaver] = new[] { NudgeKind.Herd },
            [JobType.Baker] = new[] { NudgeKind.Rain, NudgeKind.Channel },
            [JobType.Stallholder] = new[] { NudgeKind.Grove },
            [JobType.Sweeper] = new[] { NudgeKind.Rain },
        };

        public Wish FindWish(int id) => State.wishes.FirstOrDefault(w => w.id == id);

        /// <summary>The wish a chronicle entry (a Wish card's lead) belongs to, or null.</summary>
        public Wish WishOfEntry(int entryId) => State.wishes.FirstOrDefault(w => w.entryId == entryId);

        /// <summary>The wish that is open now (at most one), or null.</summary>
        public Wish OpenWish => State.wishes.FirstOrDefault(w => w.state == WishState.Open);

        /// <summary>A wish entry is shown as a card only while its wish is open (a given or lapsed wish stops).</summary>
        bool WishCardLive(ChronicleEntry e)
        {
            if (e.kind != "wish") return true;
            var w = WishOfEntry(e.id);
            return w == null || w.state == WishState.Open;
        }

        float WishRoll(int salt, int id)
        {
            uint h = (uint)State.seed * 0x9E3779B1u ^ (uint)id * 0x85EBCA77u ^ (uint)State.month * 0xC2B2AE3Du ^ (uint)salt * 0x27D4EB2Fu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        /// <summary>Why the Keeper can't give this wish now (None = can). A wish that isn't open: Unknown.</summary>
        public ShapeFail CanGiveWish(int wishId)
        {
            var w = FindWish(wishId);
            if (w == null || w.state != WishState.Open) return ShapeFail.Unknown;
            return CanShape(w.kind, w.kind == NudgeKind.Herd ? HerdVariantAt(w.x, w.z) : null);
        }

        /// <summary>Give the wish: the shaping goes where the wish points, spending a charge, and the answer is credited to the
        /// wisher. On a failure (no charge, at the cap...) the wish stays open.</summary>
        public ShapeResult GiveWish(int wishId)
        {
            var w = FindWish(wishId);
            if (w == null || w.state != WishState.Open) return new ShapeResult { fail = ShapeFail.Unknown };
            var r = PlaceShaping(w.kind, w.x, w.z, w.notableId);
            if (!r.Ok) return r;
            w.state = WishState.Given;
            w.endMonth = State.month;
            w.nudgeId = r.nudge.id;
            return r;
        }

        /// <summary>Every month: lapse what's done, and now and then a notable makes a new wish. No dice, no economy.</summary>
        void WishStep()
        {
            var open = OpenWish;
            if (open != null)
            {
                var who = FindNotable(open.notableId);
                if (State.nudges.Any(n => n.kind == open.kind && n.month >= open.month)) { CloseWish(open, WishState.Met); return; }
                if (who == null || !who.alive || State.month - open.month >= WishLapseMonths) CloseWish(open, WishState.Lapsed);
                return;
            }
            if (!WishesOn) return;
            long last = State.wishes.Count == 0 ? long.MinValue / 2 : State.wishes.Max(w => w.endMonth >= 0 ? w.endMonth : w.month);
            if (State.month - last < WishGapMonths) return;
            if (WishRoll(1, 0) >= WishChancePerMonth) return;
            TryMakeWish(null, null, false);
        }

        void CloseWish(Wish w, WishState s) { w.state = s; w.endMonth = State.month; }

        bool ToolOpenForWish(string kind) =>
            ShapingUnlocked(kind) && ShapingStanding(kind) < ShapingCap(kind);

        Wish TryMakeWish(Notable only, string forceKind, bool force)
        {
            var pool = State.notables.Where(n => n.alive && n.age >= 16f && !string.IsNullOrEmpty(n.job) && n.job != JobType.Elder && WishTools.ContainsKey(n.job));
            if (only != null) pool = new[] { only };
            var people = pool.OrderBy(n => n.id).ToList();
            if (people.Count == 0) return null;
            int start = (int)(WishRoll(2, 0) * people.Count);
            for (int i = 0; i < people.Count; i++)
            {
                var n = people[(start + i) % people.Count];
                var tools = forceKind != null ? new[] { forceKind } : WishTools[n.job];
                var open = tools.Where(k => SliceTools.Contains(k) && (force || ToolOpenForWish(k))).ToArray();
                if (open.Length == 0) continue;
                string kind = open[(int)(WishRoll(3, n.id) * open.Length) % open.Length];
                PickWishSpot(kind, n, out float x, out float z);
                return RaiseWish(n, kind, x, z);
            }
            return null;
        }

        void PickWishSpot(string kind, Notable n, out float x, out float z)
        {
            var spec = TerrainSpec.Preset(TerrainPreset);
            const float mx = Terrain.HX - Terrain.GlassMargin - 0.3f, mz = Terrain.HZ - Terrain.GlassMargin - 0.3f;
            x = z = 0f;
            bool wantWater = kind == NudgeKind.Herd && n.job == JobType.Fisher;
            for (int i = 0; i < 40; i++)
            {
                x = (WishRoll(10 + i * 2, n.id) * 2f - 1f) * mx;
                z = (WishRoll(11 + i * 2, n.id) * 2f - 1f) * mz;
                bool wet = WetSpot(spec, x, z);
                if (wantWater) { if (wet) return; continue; }
                if (wet) continue;
                if (Settlement != null && !Settlement.OpenSpotNear(ref x, ref z, 0.5f)) continue;
                return;
            }
        }

        Wish RaiseWish(Notable n, string kind, float x, float z)
        {
            string dir = Math.Abs(x) >= Math.Abs(z) ? (x >= 0 ? "east" : "west") : (z >= 0 ? "north" : "south");
            string thing = kind switch
            {
                NudgeKind.Rain => $"rain on the {dir} fields",
                NudgeKind.Grove => $"a grove on the {dir} side",
                NudgeKind.Herd => HerdVariantAt(x, z) == "fish" ? "fish in the pond" : $"deer on the {dir} meadow",
                NudgeKind.Channel => $"a channel on the {dir} side",
                _ => kind,
            };
            string who = string.IsNullOrEmpty(n.job) ? n.name : $"{n.name} the {n.job}";
            string[] forms = { "{0} dreams of {1}.", "{0} keeps wishing for {1}.", "{0} would love {1}.", "{0} hopes for {1} one day." };
            string last = State.chronicle.LastOrDefault(c => c.kind == "wish" && c.notableId == n.id)?.text;
            int f = (int)(WishRoll(4, n.id) * forms.Length) % forms.Length;
            string line = string.Format(forms[f], who, thing);
            for (int i = 1; line == last && i < forms.Length; i++) line = string.Format(forms[(f + i) % forms.Length], who, thing);
            int wid = State.wishNextId++;
            var e = new ChronicleEntry { id = WishIdBase + wid, month = State.month, kind = "wish", text = line, salience = 0.55f, notableId = n.id };
            State.chronicle.Add(e);
            Chronicled?.Invoke(e);
            var w = new Wish { id = wid, notableId = n.id, kind = kind, x = x, z = z, month = State.month, entryId = e.id };
            State.wishes.Add(w);
            return w;
        }

        /// <summary>Debug: a wish now (closes the open one first). <paramref name="kind"/> null = what the notable's job dreams
        /// of; <paramref name="notableId"/> −1 = anyone. Ignores caps and ages. Returns the wish, or null if nobody can.</summary>
        public Wish DebugForceWish(string kind = null, int notableId = -1)
        {
            var open = OpenWish;
            if (open != null) CloseWish(open, WishState.Lapsed);
            Notable only = notableId >= 0 ? FindNotable(notableId) : null;
            if (notableId >= 0 && only == null) return null;
            if (only != null && (kind != null || string.IsNullOrEmpty(only.job) || !WishTools.ContainsKey(only.job)))
            {
                kind ??= SliceTools.First(ShapingUnlocked);
                PickWishSpot(kind, only, out float x, out float z);
                return RaiseWish(only, kind, x, z);
            }
            return TryMakeWish(only, kind, true) ?? TryMakeWish(null, kind, true);
        }
    }
}
