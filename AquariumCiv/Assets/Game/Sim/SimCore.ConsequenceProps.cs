using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>#371: the small things a choice leaves in the box (visit-loop.md 2.5). Names the view draws (ConsequenceProps).</summary>
    public static class ConsequenceKind
    {
        public const string Feather = "feather", Baskets = "baskets", WoodpileRoof = "woodpile_roof", NameStone = "name_stone",
            CharmPole = "charm_pole", StewPot = "stew_pot", AxeStump = "axe_stump", Staff = "staff", FlourSacks = "flour_sacks",
            RoofBird = "roof_bird", LongTable = "long_table", OfferingBasket = "offering_basket", ShoreBoat = "shore_boat",
            FishCrate = "fish_crate", NamePeg = "name_peg";

        /// <summary>2026-11-16 "Bring a pen back": the pond family's little goat pen by the fire, beside their boat, with one goat in it
        /// (<see cref="SimCore.PenGoat"/>). The goat is outside <see cref="AnimalBudget"/> and every herd, cap, food and hunting list (Q7).</summary>
        public const string GoatPen = "goat_pen";

        /// <summary>#714: the pond family's goat pen of old saves; it loads as <see cref="ShoreBoat"/> plus a <see cref="GoatPen"/>
        /// (2026-11-16, EnsureDilemmaState).</summary>
        public const string LegacyGoatFence = "goat_fence";

        public static readonly string[] All =
        {
            Feather, Baskets, WoodpileRoof, NameStone, CharmPole, StewPot, AxeStump, Staff, FlourSacks, RoofBird, LongTable,
            OfferingBasket, ShoreBoat, FishCrate, NamePeg, GoatPen,
        };
    }

    /// <summary>B3 tester fixes: the ground each choice's thing covers as drawn (ConsequenceMeshes at its 1.5 scale; measured from the meshes'
    /// bounds 2026-11-17, only what stands on the ground: a charm pole's bough or the roof bird's wings are overhead). Half-extents in the
    /// thing's own frame (x along its yaw), box units. Props keep this plus <see cref="Margin"/> apart, no lot, field or path covers it, and
    /// people walk round it (PeopleView).</summary>
    public static class ConsequenceFootprint
    {
        /// <summary>The gap kept between two things' footprints, and between a thing and a lot.</summary>
        public const float Margin = 0.06f;

        public static void Half(string kind, out float hx, out float hz)
        {
            switch (kind)
            {
                case ConsequenceKind.Feather: hx = hz = 0.11f; return;
                case ConsequenceKind.Baskets: hx = 0.24f; hz = 0.12f; return;
                case ConsequenceKind.WoodpileRoof: hx = 0.27f; hz = 0.2f; return;
                case ConsequenceKind.NameStone: hx = 0.11f; hz = 0.07f; return;
                case ConsequenceKind.CharmPole: hx = hz = 0.045f; return;
                case ConsequenceKind.StewPot: hx = 0.19f; hz = 0.17f; return;
                case ConsequenceKind.AxeStump: hx = hz = 0.11f; return;
                case ConsequenceKind.Staff: hx = hz = 0.09f; return;
                case ConsequenceKind.FlourSacks: hx = 0.22f; hz = 0.12f; return;
                case ConsequenceKind.RoofBird: hx = hz = 0.05f; return;
                case ConsequenceKind.LongTable: hx = 0.33f; hz = 0.16f; return;
                case ConsequenceKind.OfferingBasket: hx = hz = 0.12f; return;
                case ConsequenceKind.ShoreBoat: hx = 0.2f; hz = 0.18f; return;
                case ConsequenceKind.FishCrate: hx = hz = 0.09f; return;
                case ConsequenceKind.NamePeg: hx = hz = 0.04f; return;
                case ConsequenceKind.GoatPen: hx = 0.18f; hz = 0.16f; return;
                default: hx = hz = 0.1f; return;
            }
        }

        /// <summary>The circle round the footprint at any yaw (its half diagonal is too much for a long thing: the longer half is enough, as
        /// two things are kept <see cref="Margin"/> apart on top).</summary>
        public static float Radius(string kind) { Half(kind, out float hx, out float hz); return Math.Max(hx, hz); }

        /// <summary>A thing about as wide as it is long (a stump, a pot, a pen) covers a circle of <see cref="Radius"/>; the long ones (a table,
        /// the woodpile, the baskets, the sacks, the name stone) a rectangle along their yaw.</summary>
        public static bool Round(string kind) { Half(kind, out float hx, out float hz); return hx - hz < 0.045f; }

        /// <summary>The point (x, z) lies on the thing's footprint (box units; <paramref name="pad"/> widens it).</summary>
        public static bool Covers(ConsequenceProp p, float x, float z, float pad = 0f) => Covers(p.kind, p.x, p.z, p.yaw, x, z, pad);

        public static bool Covers(string kind, float px, float pz, float yawDeg, float x, float z, float pad = 0f)
        {
            Half(kind, out float hx, out float hz);
            float dx = x - px, dz = z - pz;
            if (Round(kind)) { float rr = Math.Max(hx, hz) + pad; return dx * dx + dz * dz <= rr * rr; }
            double r = yawDeg * Math.PI / 180.0;
            // Unity's yaw turns local +x toward -z: local x = dx cos + dz sin... inverse of (x cos + z sin, -x sin + z cos)
            float lx = (float)(dx * Math.Cos(r) - dz * Math.Sin(r)), lz = (float)(dx * Math.Sin(r) + dz * Math.Cos(r));
            return Math.Abs(lx) <= hx + pad && Math.Abs(lz) <= hz + pad;
        }

        /// <summary>The circles that cover the footprint, for the people to walk round (PeopleView): one for a round thing, a row along the long
        /// side for a long one, each wide enough to cover the rectangle's corners; <paramref name="pad"/> is added to each radius (half a doll).</summary>
        public static void Circles(string kind, float px, float pz, float yawDeg, float pad, List<(float x, float z, float r)> into)
        {
            Half(kind, out float hx, out float hz);
            if (Round(kind)) { into.Add((px, pz, Math.Max(hx, hz) + pad)); return; }
            int n = (int)Math.Ceiling(hx / hz - 0.01f);
            float step = 2f * hx / n, r = (float)Math.Sqrt(step * step * 0.25f + hz * hz) + pad;
            double a = yawDeg * Math.PI / 180.0;
            float ux = (float)Math.Cos(a), uz = -(float)Math.Sin(a); // local +x in the box (Unity's yaw)
            for (int i = 0; i < n; i++)
            {
                float l = -hx + step * (i + 0.5f);
                into.Add((px + ux * l, pz + uz * l, r));
            }
        }

        /// <summary>The cells (SiteMap) under a thing at (x, z): every cell whose middle lies within its footprint circle plus <paramref name="pad"/>.</summary>
        public static void Cells(string kind, float x, float z, float pad, List<int> into)
        {
            into.Clear();
            float r = Radius(kind) + pad;
            int reach = (int)Math.Ceiling(r / SiteMap.Cell) + 1, c0 = SiteMap.CellAt(x, z);
            if (c0 < 0) return;
            int i0 = SiteMap.I(c0), j0 = SiteMap.J(c0);
            for (int j = Math.Max(0, j0 - reach); j <= Math.Min(SiteMap.NZ - 1, j0 + reach); j++)
                for (int i = Math.Max(0, i0 - reach); i <= Math.Min(SiteMap.NX - 1, i0 + reach); i++)
                {
                    int c = SiteMap.Index(i, j);
                    float dx = SiteMap.CXc(c) - x, dz = SiteMap.CZc(c) - z;
                    if (dx * dx + dz * dz <= r * r) into.Add(c);
                }
            if (into.Count == 0) into.Add(c0);
        }
    }

    /// <summary>Where a consequence prop goes (#371). Each falls back to the fire if its anchor is not there.</summary>
    public enum PropAnchor
    {
        /// <summary>By the home of the option's person (a small choice: of its subject).</summary>
        Home,
        /// <summary>By the option's building, else the first standing building of <see cref="DilemmaOptionDef.buildingType"/>.</summary>
        Building,
        /// <summary>Near the fire or the green.</summary>
        Fire,
        /// <summary>Out at the edge of the settlement (an offering on the hill, a charm pole at the edge of the woods).</summary>
        Edge,
        /// <summary>By the remembering stone / the pond side.</summary>
        Pond,
        /// <summary>#635: on a grown tree: the Keeper's tree first, else the grown tree nearest the fire. With no grown tree the thing
        /// stands at the <see cref="Edge"/> as its own pole until a tree grows, and it moves to the Keeper's tree once that is grown.</summary>
        Tree,
    }

    /// <summary>One thing an answered (or lapsed) choice left in the box. Saved with the box. Placed by the sim at a real, open spot
    /// (box coordinates, as Building.x/z) at most <see cref="SimCore.PropWithinMonths"/> after the choice ended.</summary>
    [Serializable]
    public class ConsequenceProp
    {
        public int id;
        public int dilemmaId;
        public int option;
        public string kind = "";            // ConsequenceKind
        public PropAnchor anchor;
        public bool placed;
        public float x, z, yaw;             // the spot (valid once placed), yaw in degrees
        public int personId = -1;           // the person it is about (-1 = none)
        public int buildingId = -1;         // the building it stands by (-1 = none)
        public string buildingType = "";    // else the first standing building of this type (PropAnchor.Building)
        public long dueMonth;               // the choice ended this month; it must stand within PropWithinMonths
        public long placedMonth = -1;
        public bool keeper;                 // the Keeper's choice (answered or "let them decide"), not the people's
        public int treeCell = -1;           // #635 (PropAnchor.Tree): the grown tree it hangs on (an eco cell); -1 = it stands on its own
    }

    public partial class SimCore
    {
        /// <summary>A choice's thing stands within this many game months of its end (one game year).</summary>
        public const int PropWithinMonths = SimConfig.MonthsPerYear;

        /// <summary>The things the choices left, in the order they were made.</summary>
        public IReadOnlyList<ConsequenceProp> ConsequenceProps => State.consequenceProps ??= new List<ConsequenceProp>();

        public ConsequenceProp FindConsequenceProp(int id) => ConsequenceProps.FirstOrDefault(p => p.id == id);

        /// <summary>The thing the option left (null if none).</summary>
        public ConsequenceProp ConsequencePropFor(int dilemmaId, int option) =>
            ConsequenceProps.FirstOrDefault(p => p.dilemmaId == dilemmaId && p.option == option);

        /// <summary>The thing a callback line is about, from its chronicle entry (null if none or not a callback).</summary>
        public ConsequenceProp ConsequencePropOfEntry(ChronicleEntry e)
        {
            var info = CallbackInfoOf(e);
            if (info == null) return null;
            // 2026-11-16: a callback about the option's second thing (needsProp: the goat at the pen) is about that thing, not the first
            string kind = info.callback == null ? null : CallbackDefOf(info.callback)?.needsProp;
            var own = string.IsNullOrEmpty(kind) ? null : ConsequenceProps.FirstOrDefault(p => p.dilemmaId == info.dilemma.id && p.option == info.option && p.kind == kind);
            return own ?? ConsequencePropFor(info.dilemma.id, info.option);
        }

        // ---------- 2026-11-16 "Bring a pen back": the pond family's goat pen ----------
        /// <summary>The goat pen (the first one; null when the pond family never brought one). It may wait a month to be placed.</summary>
        public ConsequenceProp GoatPenProp => ConsequenceProps.FirstOrDefault(p => p.kind == ConsequenceKind.GoatPen);

        /// <summary>The box has the goat pen (placed or about to be): the words "goat" and "goats" may be said (ContentRules.Gates).</summary>
        public bool HasGoatPen => GoatPenProp != null;

        /// <summary>The one goat stands in its pen: true once the pen is placed. Read-only for the view (draw the goat inside the pen at
        /// <see cref="GoatPenProp"/>'s x, z, yaw). The goat is in no herd, cap, budget, food or hunting list (Q7): it lives only in the pen.</summary>
        public bool PenGoat => GoatPenProp?.placed ?? false;

        /// <summary>How many goats the pen holds: one, or none without a placed pen. Never grows.</summary>
        public int PenGoats => PenGoat ? 1 : 0;

        /// <summary>The option made a thing (and its second thing, <see cref="DilemmaOptionDef.prop2"/>): queue them (placed now if
        /// the anchor is there, else on a later month, within the year).</summary>
        void LeaveConsequence(Dilemma d, DilemmaDef def, int option)
        {
            QueueConsequence(d, def, option);
            PlaceQueuedConsequence(d, option);
        }

        /// <summary>2026-11-16: the option's things join the box's list, not placed yet. An answer queues them before its line is written,
        /// so a line that names them ("penned their goat") is said in a box that has them (ContentRules.Gates); placing waits for the
        /// option's effects (<see cref="PlaceQueuedConsequence"/>), as before.</summary>
        void QueueConsequence(Dilemma d, DilemmaDef def, int option)
        {
            var o = def.options[option];
            if (string.IsNullOrEmpty(o.prop)) return;
            var list = (State.consequenceProps ??= new List<ConsequenceProp>());
            int person = def.smallKind != null && d.subjectId >= 0 ? d.subjectId : d.PersonOf(option); // t4-fixes: a small choice with no subject (the story) is about its person
            string type = string.IsNullOrEmpty(o.propBuilding) ? o.buildingType ?? "" : o.propBuilding;
            foreach (var (kind, at) in new[] { (o.prop, o.propAt), (o.prop2, o.prop2At) })
            {
                if (string.IsNullOrEmpty(kind) || list.Any(p => p.dilemmaId == d.id && p.option == option && p.kind == kind)) continue;
                list.Add(new ConsequenceProp
                {
                    id = list.Count == 0 ? 1 : list.Max(x => x.id) + 1, dilemmaId = d.id, option = option, kind = kind, anchor = at,
                    personId = person, buildingId = d.BuildingOf(option), buildingType = type, dueMonth = State.month,
                    keeper = d.state == DilemmaState.Answered,
                });
            }
        }

        /// <summary>The option's queued things are placed now if their anchor is there (the first thing first).</summary>
        void PlaceQueuedConsequence(Dilemma d, int option)
        {
            var list = State.consequenceProps;
            if (list == null) return;
            foreach (var p in list) if (!p.placed && p.dilemmaId == d.id && p.option == option) TryPlaceProp(p, false);
        }

        /// <summary>Every month: a thing still waiting for its anchor is placed; at the end of the year it goes by the fire.</summary>
        void ConsequencePropStep()
        {
            var list = State.consequenceProps;
            if (list == null || list.Count == 0) return;
            foreach (var p in list)
            {
                if (!p.placed) TryPlaceProp(p, State.month >= p.dueMonth + PropWithinMonths - 1);
                else if (p.anchor == PropAnchor.Tree) KeepOnTree(p);
            }
            MoveCoveredProps(); // B3 tester fixes: a lot, a field or a path laid over a thing moves it (lots now keep clear: old saves, the camp's last lots)
            Settlement?.MarkProps();
        }

        // ---------- #635: charms on a tree ----------
        /// <summary>The grown tree a <see cref="PropAnchor.Tree"/> thing hangs on: the Keeper's tree when it is grown, else the grown tree
        /// nearest the fire; -1 when no tree has grown (or the ecology is off).</summary>
        public int CharmTreeCell()
        {
            if (!EcologyOn) return -1;
            var e = State.eco;
            if (KeeperTreeGrown) return e.treeCell;
            float fx = 0f, fz = 0f;
            Settlement?.Fire(out fx, out fz, out _);
            int best = -1; float bestD = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if ((EcoCover)e.cover[c] != EcoCover.Tree) continue;
                float dx = SiteMap.CXc(c) - fx, dz = SiteMap.CZc(c) - fz, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>The Keeper's tree stands grown.</summary>
        public bool KeeperTreeGrown
        {
            get
            {
                var e = State.eco;
                return EcologyOn && e.treeCell >= 0 && e.treeCell < EcoLand.N && e.treeStage >= 4 && (EcoCover)e.cover[e.treeCell] == EcoCover.Tree;
            }
        }

        /// <summary>The words for where the charms go: "the Keeper's tree", "the old tree", or "a pole till a tree grows".</summary>
        public string CharmTreeWords => KeeperTreeGrown ? "the Keeper's tree" : CharmTreeCell() >= 0 ? "the old tree" : "a pole till a tree grows";

        /// <summary>Charms hanging for the dead: one for the death the choice was about, and one for each death since (at most
        /// <see cref="MaxCharms"/>).</summary>
        public int CharmsOn(ConsequenceProp p)
        {
            if (p == null) return 0;
            int n = 1;
            foreach (var x in State.notables) if (!x.alive && x.diedMonth > p.dueMonth) n++;
            return Math.Min(MaxCharms, n);
        }

        public const int MaxCharms = 9;

        /// <summary>Hangs a tree thing on its tree; with none grown, it stands at the edge as a pole.</summary>
        void HangOnTree(ConsequenceProp p)
        {
            int c = CharmTreeCell();
            p.treeCell = c;
            if (c >= 0) { p.x = SiteMap.CXc(c); p.z = SiteMap.CZc(c); }
            else
            {
                float ax = 0f, az = 0f, minR = 0.05f, maxR = 0.5f;
                var keep = p.anchor;
                p.anchor = PropAnchor.Edge;
                AnchorOf(p, ref ax, ref az, ref minR, ref maxR);
                p.anchor = keep;
                PropSpot(p, ax, az, minR, maxR, out p.x, out p.z);
            }
        }

        /// <summary>Monthly: a tree thing whose tree is gone moves (to another tree, or back to a pole); a pole or a wild tree's charms move
        /// to the Keeper's tree once it is grown, and a pole moves to the first tree that grows.</summary>
        void KeepOnTree(ConsequenceProp p)
        {
            bool onTree = p.treeCell >= 0 && EcologyOn && (EcoCover)State.eco.cover[p.treeCell] == EcoCover.Tree;
            int want = CharmTreeCell();
            if (onTree && (want == p.treeCell || !KeeperTreeGrown)) return;
            if (!onTree && p.treeCell < 0 && want < 0) return; // a pole, and still no tree
            HangOnTree(p);
        }

        void TryPlaceProp(ConsequenceProp p, bool force)
        {
            if (p.anchor == PropAnchor.Tree)
            {
                HangOnTree(p); // #635: never waits: a pole stands in until a tree grows
                p.yaw = (int)(DilemmaRoll(110, p.dilemmaId * 8 + p.option, p.id) * 360f);
                p.placed = true;
                p.placedMonth = State.month;
                return;
            }
            float ax = 0f, az = 0f, minR = 0.12f, maxR = 0.4f;
            bool found = AnchorOf(p, ref ax, ref az, ref minR, ref maxR);
            if (!found)
            {
                if (!force) return;
                p.anchor = PropAnchor.Fire;
                AnchorOf(p, ref ax, ref az, ref minR, ref maxR);
            }
            if (p.kind == ConsequenceKind.ShoreBoat && p.anchor == PropAnchor.Fire) { minR = Math.Max(minR, BoatFromFire); maxR = Math.Max(maxR, BoatFromFire + 1.2f); } // #714 round 2: out past the pot and the seats
            if (p.kind == ConsequenceKind.GoatPen && p.anchor == PropAnchor.Fire) { minR = Math.Max(minR, PenFromFire); maxR = Math.Max(maxR, PenFromFire + 0.9f); } // 2026-11-16: out past the pot and the seats, beside the boat
            PropSpot(p, ax, az, minR, maxR, out p.x, out p.z);
            p.yaw = (int)(DilemmaRoll(110, p.dilemmaId * 8 + p.option, p.id) * 360f);
            if (p.kind == ConsequenceKind.ShoreBoat || p.kind == ConsequenceKind.GoatPen) p.yaw = p.yaw < 180 ? 0 : 90; // #714 round 2: lined up with the box's lines, never a random yaw
            p.placed = true;
            p.placedMonth = State.month;
        }

        /// <summary>#714 round 2: the extra room the boat keeps from every other thing (it is 0.4 u long; the stew pot must never hide it).</summary>
        public const float BoatRoom = 0.35f, BoatFromFire = 0.75f;

        /// <summary>2026-11-16 [est]: the goat pen is about 0.3 u square: the extra room it keeps from every other thing, its ring from the
        /// fire, how far it stays from the water's edge, and the extra margin it keeps from the glass (on top of Terrain.GlassMargin).</summary>
        public const float PenRoom = 0.2f, PenFromFire = 0.6f, PenFromWater = 0.3f, PenFromGlass = 0.2f;

        /// <summary>2026-11-16 [est]: a tent's yard or a house lot within this many box units of a cell: the pen does not stand there.</summary>
        public const float PenFromLot = 0.25f;

        static bool NearLot(SiteMap map, int c, float within)
        {
            int reach = (int)Math.Ceiling(within / SiteMap.Cell), i0 = SiteMap.I(c), j0 = SiteMap.J(c);
            for (int j = Math.Max(0, j0 - reach); j <= Math.Min(SiteMap.NZ - 1, j0 + reach); j++)
                for (int i = Math.Max(0, i0 - reach); i <= Math.Min(SiteMap.NX - 1, i0 + reach); i++)
                    if (map.lot[SiteMap.Index(i, j)] != 0) return true;
            return false;
        }

        /// <summary>#714 round 2: a lot (a house, a yard) within 0.3 u of a cell, or in front of it toward the default camera within 0.8 u: the boat lying there would be hidden.</summary>
        static bool BoatNearLot(SiteMap map, int c)
        {
            int reach = (int)Math.Ceiling(0.3f / SiteMap.Cell), i0 = SiteMap.I(c), j0 = SiteMap.J(c);
            for (int j = Math.Max(0, j0 - reach); j <= Math.Min(SiteMap.NZ - 1, j0 + reach); j++)
                for (int i = Math.Max(0, i0 - reach); i <= Math.Min(SiteMap.NX - 1, i0 + reach); i++)
                    if (map.lot[SiteMap.Index(i, j)] != 0) return true;
            // and between it and the default camera (yaw 28: the camera stands off -x, -z): a house there hides it
            float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c), dx = -0.47f, dz = -0.88f;
            for (float d = 0.1f; d <= 0.8f; d += 0.05f)
            {
                int q = SiteMap.CellAt(cx + dx * d, cz + dz * d);
                if (q >= 0 && q < SiteMap.N && map.lot[q] != 0) return true;
            }
            return false;
        }

        bool AnchorOf(ConsequenceProp p, ref float ax, ref float az, ref float minR, ref float maxR)
        {
            var st = Settlement;
            switch (p.anchor)
            {
                case PropAnchor.Home:
                {
                    var n = Alive(p.personId);
                    var lot = n == null || st == null ? null : st.HomeOf(n);
                    if (lot == null) return false;
                    ax = lot.doorX; az = lot.doorZ; minR = 0.1f; maxR = 0.3f;
                    return true;
                }
                case PropAnchor.Building:
                {
                    var b = State.buildings.FirstOrDefault(x => x.id == p.buildingId);
                    if (b == null)
                    {
                        string type = p.buildingType;
                        b = string.IsNullOrEmpty(type) ? null : State.buildings.FirstOrDefault(x => x.type == type && x.lotId != 0);
                    }
                    if (b == null) return false;
                    ax = b.doorX != 0f || b.doorZ != 0f ? b.doorX : b.x; az = b.doorX != 0f || b.doorZ != 0f ? b.doorZ : b.z;
                    minR = 0.12f; maxR = 0.35f;
                    return true;
                }
                case PropAnchor.Fire:
                    if (st != null) st.Fire(out ax, out az, out _); else { ax = 0f; az = 0f; }
                    minR = 0.45f; maxR = 0.85f;
                    return true;
                case PropAnchor.Edge:
                {
                    var (x, z) = ApartSpotPreview(ApartKindFor(FindDilemma(p.dilemmaId), ApartKind.WoodsCamp));
                    ax = x * 0.8f; az = z * 0.8f; minR = 0.05f; maxR = 0.5f;
                    return true;
                }
                case PropAnchor.Pond:
                {
                    var map = st?.Map;
                    if (map == null) { ax = -Terrain.HX * 0.5f; az = 0f; minR = 0.1f; maxR = 0.6f; return true; }
                    // The open ground nearest the pond's edge (the remembering stone stands by the water).
                    float bestD = float.MaxValue;
                    for (int c = 0; c < SiteMap.N; c++)
                    {
                        if (map.pond[c] || map.waterDist[c] > 0.35f || map.lot[c] != 0) continue;
                        float dx = SiteMap.CXc(c) - map.greenX, dz = SiteMap.CZc(c) - map.greenZ, d = dx * dx + dz * dz;
                        if (d < bestD) { bestD = d; ax = SiteMap.CXc(c); az = SiteMap.CZc(c); }
                    }
                    if (bestD == float.MaxValue) return false;
                    minR = 0.0f; maxR = 0.4f;
                    return true;
                }
            }
            return false;
        }

        /// <summary>An open spot in a ring round the anchor, clear of lots, paths, water, trees, the green and the other things: the
        /// candidate with the lowest hash. With no map, a point on the ring.</summary>
        void PropSpot(ConsequenceProp p, float ax, float az, float minR, float maxR, out float x, out float z)
        {
            var map = Settlement?.Map;
            int salt = p.dilemmaId * 8 + p.option;
            float mx = Terrain.HX - Terrain.GlassMargin, mz = Terrain.HZ - Terrain.GlassMargin;
            bool pen = p.kind == ConsequenceKind.GoatPen;
            if (pen) { mx -= PenFromGlass; mz -= PenFromGlass; } // 2026-11-16: the pen is wide; it keeps clear of the glass
            if (map == null)
            {
                double a = DilemmaRoll(111, salt, p.id) * Math.PI * 2.0;
                float r = (minR + maxR) * 0.5f + p.id * 0.07f;
                x = Math.Max(-mx, Math.Min(mx, ax + (float)Math.Cos(a) * r)); z = Terrain.CZ + Math.Max(-mz, Math.Min(mz, az + (float)Math.Sin(a) * r - Terrain.CZ));
                return;
            }
            var future = pen ? FutureWaterMask(PenFromWater) : null; // 2026-11-16: where the living land's pond will reach, too
            bool poleSet = State.featherPoleSet && State.consequenceProps.Any(q => q.placed && q.kind == ConsequenceKind.Feather);
            float poleX = State.featherPoleX, poleZ = State.featherPoleZ, poleRoom = PropRoom(p.kind, ConsequenceKind.Feather);
            const byte block = SiteMap.FCanal | SiteMap.FGreen | SiteMap.FCommons | SiteMap.FYard | SiteMap.FRock;
            for (int pass = 0; pass < 5; pass++) // B3 tester fixes: passes 3 and 4 look further out before giving up (a thing's whole footprint must be clear)
            {
                float lo = pass == 0 ? minR : 0f, hi = maxR + pass * 0.3f;
                int best = -1; float bestRoll = float.MaxValue;
                for (int c = 0; c < SiteMap.N; c++)
                {
                    float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
                    if (Math.Abs(cx) > mx || Math.Abs(cz - Terrain.CZ) > mz) continue;
                    float dx = cx - ax, dz = cz - az, d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d < lo || d > hi) continue;
                    if (map.pond[c] || map.lot[c] != 0 || map.ring[c] != 0 || (map.flags[c] & block) != 0 || map.Forest(c) || map.ThicketReserved(c)) continue;
                    if (pass < 2 && Settlement.WalkCost(c) == 0) continue;
                    if (!GroundClear(map, p.kind, cx, cz, block)) continue; // B3 tester fixes: the whole footprint, not only its middle cell
                    bool crowded = poleSet && p.kind != ConsequenceKind.Feather && (poleX - cx) * (poleX - cx) + (poleZ - cz) * (poleZ - cz) < poleRoom * poleRoom; // fb4 #566: the one feather pole
                    if (!crowded) foreach (var q in State.consequenceProps)
                    {
                        if (q == p || !q.placed || q.anchor == PropAnchor.Tree && q.treeCell >= 0) continue; // a charm on a tree hangs overhead
                        if (q.kind == ConsequenceKind.Feather) continue; // every feather stands on the one pole (above)
                        float room = 0.22f + (p.kind == ConsequenceKind.ShoreBoat || q.kind == ConsequenceKind.ShoreBoat ? BoatRoom : 0f); // #714 round 2: nothing hides the boat
                        if (pen || q.kind == ConsequenceKind.GoatPen) room += PenRoom; // 2026-11-16: the pen is wide
                        room = Math.Max(room, PropRoom(p.kind, q.kind)); // B3 tester fixes: two things keep their footprints apart
                        if ((q.x - cx) * (q.x - cx) + (q.z - cz) * (q.z - cz) < room * room) { crowded = true; break; }
                    }
                    if (crowded) continue;
                    if (pen && (map.waterDist[c] < PenFromWater || future != null && c < future.Length && future[c] || pass < 2 && NearLot(map, c, PenFromLot))) continue; // 2026-11-16: dry ground, clear of the tents (a cramped camp: by a tent before on the fire)
                    float roll = DilemmaRoll(112, salt * 4096 + c, p.id);
                    if (p.kind == ConsequenceKind.ShoreBoat) // #714 round 2: the boat lies on clear ground nearest the pond's edge (not on the mud), not behind the pot
                    {
                        if (map.waterDist[c] < 0.15f) continue;
                        roll = map.waterDist[c] + (BoatNearLot(map, c) ? 2f : 0f) + Math.Max(0f, cz - az) * 0.15f + roll * 0.01f; // not by or behind a house, on the camera's side when it can
                    }
                    if (roll < bestRoll) { bestRoll = roll; best = c; }
                }
                if (best >= 0) { x = SiteMap.CXc(best); z = SiteMap.CZc(best); return; }
            }
            x = Math.Max(-mx, Math.Min(mx, ax)); z = Terrain.CZ + Math.Max(-mz, Math.Min(mz, az - Terrain.CZ));
        }

        /// <summary>B3 tester fixes: how far apart two things' middles stand (their footprints' circles and a margin between).</summary>
        public static float PropRoom(string a, string b) => ConsequenceFootprint.Radius(a) + ConsequenceFootprint.Radius(b) + ConsequenceFootprint.Margin;

        readonly List<int> footCells = new();

        /// <summary>B3 tester fixes: no cell under the thing's footprint (and its margin) is water, a lot, a family ring, a yard or the green.</summary>
        bool GroundClear(SiteMap map, string kind, float x, float z, byte block)
        {
            ConsequenceFootprint.Cells(kind, x, z, ConsequenceFootprint.Margin, footCells);
            foreach (int c in footCells)
                if (map.pond[c] || map.lot[c] != 0 && !LotRetired(map.lot[c]) || map.ring[c] != 0 || (map.flags[c] & block) != 0) return false;
            return true;
        }

        bool LotRetired(int lotId)
        {
            var st = Settlement;
            if (st == null) return false;
            foreach (var l in st.Lots) if (l.id == lotId) return l.retired;
            return false;
        }

        /// <summary>B3 tester fixes: the thing's ground is now under a lot, a field or a path (laid before the thing stood, or an old save): it is
        /// moved to a clear spot by its anchor. A thing on a tree hangs overhead and never moves for this.</summary>
        public bool PropCovered(ConsequenceProp p)
        {
            var map = Settlement?.Map;
            if (map == null || !p.placed || p.anchor == PropAnchor.Tree && p.treeCell >= 0) return false;
            ConsequenceFootprint.Cells(p.kind, p.x, p.z, 0f, footCells);
            foreach (int c in footCells)
                if (map.lot[c] != 0 && !LotRetired(map.lot[c]) || map.Laid(c) || map.tier[c] >= PathNet.PathTier) return true;
            return false;
        }

        /// <summary>Moves every covered thing (<see cref="PropCovered"/>) to a clear spot by its anchor. Returns how many moved.</summary>
        public int MoveCoveredProps()
        {
            var list = State.consequenceProps;
            if (list == null) return 0;
            int n = 0;
            foreach (var p in list)
            {
                if (!PropCovered(p)) continue;
                float ax = 0f, az = 0f, minR = 0.12f, maxR = 0.4f;
                if (!AnchorOf(p, ref ax, ref az, ref minR, ref maxR)) { var keep = p.anchor; p.anchor = PropAnchor.Fire; AnchorOf(p, ref ax, ref az, ref minR, ref maxR); p.anchor = keep; }
                if (p.kind == ConsequenceKind.ShoreBoat && p.anchor == PropAnchor.Fire) { minR = Math.Max(minR, BoatFromFire); maxR = Math.Max(maxR, BoatFromFire + 1.2f); }
                if (p.kind == ConsequenceKind.GoatPen && p.anchor == PropAnchor.Fire) { minR = Math.Max(minR, PenFromFire); maxR = Math.Max(maxR, PenFromFire + 0.9f); }
                PropSpot(p, ax, az, minR, maxR, out p.x, out p.z);
                n++;
            }
            if (n > 0) Settlement?.MarkProps();
            return n;
        }

        /// <summary>Debug: every option of every choice that has a thing leaves it now (one per option, as if each had been taken),
        /// placed at once. Returns how many stand.</summary>
        public int DebugPlaceEveryProp()
        {
            var list = (State.consequenceProps ??= new List<ConsequenceProp>());
            int fake = 90000;
            foreach (var def in DilemmaCatalog.Concat(SmallCatalog))
                for (int i = 0; i < def.options.Length; i++)
                {
                    if (string.IsNullOrEmpty(def.options[i].prop)) continue;
                    var d = new Dilemma { id = fake++, defId = def.id, month = State.month, endMonth = State.month, state = DilemmaState.Answered, end = DilemmaEnd.Answered, option = i, personA = Pick(0), personB = Pick(1), subjectId = Pick(0) };
                    LeaveConsequence(d, def, i);
                }
            foreach (var p in list) if (!p.placed) TryPlaceProp(p, true);
            Settlement?.MarkProps();
            return list.Count(p => p.placed);
        }

        int Pick(int skip)
        {
            var alive = State.notables.Where(n => n.alive).ToList();
            return alive.Count == 0 ? -1 : alive[Math.Min(skip, alive.Count - 1)].id;
        }
    }
}
