using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// #188: tap a building to see what it is, when it went up and who built it, in the crowd card's shape (a title, Close and three
    /// lines). People win: <see cref="WorldTap"/> asks for a doll first. The builder's name opens their card, also when they've died.
    /// It's a part of this view because the two share the sheet layer, the tap and the rules that close a card.
    /// </summary>
    public partial class PersonCardView
    {
        /// <summary>How tall a lot's building is, for a tap: a roof takes the tap, and a field or a yard only near the ground.</summary>
        const float TallPiece = 0.55f, LowPiece = 0.12f;
        /// <summary>The solid middle of a lot's box: a ray through it beats one that only clips another lot's corner.</summary>
        const float CoreShare = 0.7f;

        GlassSheet buildingSheet;
        Label buildingTitle, buildingWhat, buildingYear, buildingBy;
        GlassButton buildingBuilder;

        /// <summary>The building whose card is open, or -1.</summary>
        public int BuildingId { get; private set; } = -1;
        public bool BuildingOpen => BuildingId >= 0;
        public BuildingCardInfo BuildingCard { get; private set; }

        /// <summary>The building under a screen point (the nearest lot the ray enters), or -1. A flat box on the lot, so a tap needn't find the wall.</summary>
        public int PickBuilding(Vector2 screenPos)
        {
            var sim = Sim;
            var cam = Camera.main;
            var box = FindAnyObjectByType<BoxView>();
            if (sim?.Settlement == null || cam == null || box == null) return -1;
            var ray = cam.ScreenPointToRay(screenPos);
            var o = box.transform.InverseTransformPoint(ray.origin);
            var v = box.transform.InverseTransformDirection(ray.direction);
            int best = -1, core = -1;
            float bestT = float.PositiveInfinity, coreT = float.PositiveInfinity;
            foreach (var l in sim.Settlement.Lots)
            {
                if (l.retired || l.buildingId <= 0 || !sim.State.buildings.Exists(b => b.id == l.buildingId)) continue;
                float tall = l.kind is LotKind.Farm or LotKind.Pasture or LotKind.Orchard ? LowPiece : TallPiece;
                float y0 = box.GroundHeight(l.x, l.z);
                if (HitsLot(l, o, v, y0, y0 + tall, 1f, out float t) && t < bestT) { bestT = t; best = l.buildingId; }
                if (HitsLot(l, o, v, y0, y0 + tall * CoreShare, CoreShare, out float tc) && tc < coreT) { coreT = tc; core = l.buildingId; }
            }
            return core >= 0 ? core : best; // the air round a house's eaves doesn't take a tap from the well behind it
        }

        /// <summary>A doll the camera can't see for a house: a tap on that roof is for the house. A doll in the yard in front stands outside
        /// the lot's solid middle, so it stays a doll.</summary>
        bool HiddenByABuilding(PeopleView pv, Camera cam, PersonRef who)
        {
            var sim = Sim;
            var box = FindAnyObjectByType<BoxView>();
            if (sim?.Settlement == null || box == null || !pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) return false;
            var o = box.transform.InverseTransformPoint(cam.transform.position);
            var v = box.transform.InverseTransformPoint((top + feet) * 0.5f) - o; // the doll is at t = 1
            foreach (var l in sim.Settlement.Lots)
            {
                if (l.retired || l.buildingId <= 0 || l.kind is LotKind.Farm or LotKind.Pasture or LotKind.Orchard) continue;
                float y0 = box.GroundHeight(l.x, l.z);
                if (HitsLot(l, o, v, y0, y0 + TallPiece, CoreShare, out float t) && t < 0.98f) return true;
            }
            return false;
        }

        /// <summary>The ray (box space) against the lot's upright box. Pure slab test, in the lot's own axes (<see cref="SimLayout.Corners"/>).</summary>
        static bool HitsLot(Lot l, Vector3 o, Vector3 v, float y0, float y1, float share, out float t)
        {
            float r = l.yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            float dx = o.x - l.x, dz = o.z - l.z;
            var po = new Vector3(dx * c - dz * s, o.y, dx * s + dz * c);
            var pv = new Vector3(v.x * c - v.z * s, v.y, v.x * s + v.z * c);
            float hw = l.w / 2f * share, hd = l.d / 2f * share, lo = 0f, hi = float.PositiveInfinity;
            t = 0f;
            bool Slab(float start, float dir, float a, float b, ref float n, ref float f)
            {
                if (Mathf.Abs(dir) < 1e-6f) return start >= a && start <= b;
                float t0 = (a - start) / dir, t1 = (b - start) / dir;
                if (t0 > t1) (t0, t1) = (t1, t0);
                n = Mathf.Max(n, t0);
                f = Mathf.Min(f, t1);
                return n <= f;
            }
            if (!Slab(po.x, pv.x, -hw, hw, ref lo, ref hi) || !Slab(po.y, pv.y, y0, y1, ref lo, ref hi) || !Slab(po.z, pv.z, -hd, hd, ref lo, ref hi)) return false;
            t = lo;
            return true;
        }

        /// <summary>Opens a building's card. False if it isn't there. A person's card gives way to it, and it to them.</summary>
        public bool OpenBuilding(int buildingId)
        {
            var card = Sim?.BuildingCardOf(buildingId);
            if (card == null) return false;
            EnsureUi();
            if (IsOpen) Close();
            if (CrowdOpen) CloseCrowd();
            var hud = GameHud.Instance;
            if (hud != null) { hud.CloseSheet(); hud.SetCleanMode(false); }
            if (BuildingId != buildingId) GameClock.Tel.CardOpened("building", buildingId);
            BuildingId = buildingId;
            FillBuilding(card);
            if (!buildingSheet.IsOpen)
            {
                UiSound.Play(UiRole.Open);
                cardRoot.style.display = DisplayStyle.Flex;
                buildingSheet.Open();
            }
            if (hud != null) hud.SetOutsideSheet(buildingSheet);
            return true;
        }

        public void CloseBuilding() => CloseBuilding(true);

        void CloseBuilding(bool sound)
        {
            if (!BuildingOpen) return;
            if (sound) UiSound.Play(UiRole.Close);
            BuildingId = -1;
            BuildingCard = null;
            if (GameHud.Instance != null) GameHud.Instance.SetOutsideSheet(null);
            buildingSheet.Close(() => { if (!AnyOpen && !BuildingOpen) cardRoot.style.display = DisplayStyle.None; });
        }

        void FillBuilding(BuildingCardInfo c)
        {
            BuildingCard = c;
            buildingTitle.text = c.title;
            buildingWhat.text = GlassType.Lines(c.whatItIs, 15f, 1.35f);
            buildingYear.text = c.yearLine;
            buildingBy.text = GlassType.Lines(c.builderLine, 15f, 1.35f);
            buildingBuilder.style.display = c.Together ? DisplayStyle.None : DisplayStyle.Flex;
            if (!c.Together) buildingBuilder.text = c.builderName;
        }

        void BuilderTapped()
        {
            var card = BuildingCard;
            if (card == null || card.Together) return;
            CloseBuilding(false);
            Open(card.builderId, card.builderAlive);
        }

        void BuildBuildingUi()
        {
            buildingSheet = new GlassSheet(SheetHeight.Content) { name = "building-card" };
            buildingSheet.AddToClassList("pc-crowd");
            buildingSheet.style.display = DisplayStyle.None;
            cardRoot.Add(buildingSheet);
            var head = new VisualElement();
            head.AddToClassList("pc-head");
            buildingTitle = new Label { name = "building-card-title", pickingMode = PickingMode.Ignore };
            buildingTitle.AddToClassList("type-sheet");
            buildingTitle.AddToClassList("pc-name");
            head.Add(buildingTitle);
            head.Add(new GlassButton("", CloseBuilding, ButtonKind.Icon, UiRole.None) { name = "building-card-close", Icon = "close" });
            buildingSheet.Add(head);
            Label Line(string name)
            {
                var l = new Label { name = name, pickingMode = PickingMode.Ignore, enableRichText = true };
                l.AddToClassList("type-body");
                l.AddToClassList("pc-line");
                buildingSheet.Add(l);
                return l;
            }
            buildingWhat = Line("building-card-what");
            buildingYear = Line("building-card-year");
            buildingBy = Line("building-card-by");
            buildingBuilder = new GlassButton("", BuilderTapped, ButtonKind.Secondary, UiRole.Open) { name = "building-card-builder", Compact = true };
            buildingBuilder.style.alignSelf = Align.FlexStart;
            buildingBuilder.style.marginTop = 8f;
            buildingSheet.Add(buildingBuilder);
        }

        /// <summary>Debug (#188): the next building's card, round the box.</summary>
        public static string DebugBuildingCard()
        {
            var v = Instance;
            var sim = Sim;
            if (v == null || sim == null || sim.State.buildings.Count == 0) return "no buildings";
            var ids = sim.State.buildings.Select(b => b.id).OrderBy(i => i).ToList();
            int at = ids.IndexOf(v.BuildingId);
            int id = ids[(at + 1) % ids.Count];
            v.OpenBuilding(id);
            return $"building {id}: {v.BuildingCard?.title} · {v.BuildingCard?.builderLine}";
        }

        /// <summary>Debug (#188): the open building's builder, round the three wordings: together, someone living, someone dead.</summary>
        public static string DebugBuilder()
        {
            var v = Instance;
            var sim = Sim;
            if (v == null || sim == null || !v.BuildingOpen) return "open a building card first";
            var now = v.BuildingCard;
            Notable next;
            if (now.Together) next = sim.State.notables.Find(n => n.alive && n.age >= 16f);
            else if (now.builderAlive) next = sim.State.notables.Find(n => !n.alive);
            else next = null;
            sim.DebugSetBuilder(v.BuildingId, next?.id ?? 0);
            v.OpenBuilding(v.BuildingId);
            return v.BuildingCard.builderLine;
        }
    }
}
