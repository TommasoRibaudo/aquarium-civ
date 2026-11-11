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
        /// <summary>Tester run 9: a mill stands far over a house, and its sails turn out past its lot: the top of the tower, and a band of air (a share of the lot) round the hub.</summary>
        const float MillTop = 0.82f, MillSailsFrom = 0.25f, MillSailsTop = 1.1f, MillSailsWide = 2.2f;
        /// <summary>The solid middle of a lot's box: a ray through it beats one that only clips another lot's corner.</summary>
        const float CoreShare = 0.7f;

        /// <summary>#572: the Offices sheet's holder buttons open that person's card here.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookOfficeTaps() => GameHud.OpenPerson = id => Instance?.Open(id);

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
                bool mill = l.kind == LotKind.Windmill;
                float tall = l.kind is LotKind.Farm or LotKind.Pasture or LotKind.Orchard ? LowPiece : mill ? MillTop : TallPiece;
                float y0 = box.GroundHeight(l.x, l.z);
                if (HitsLot(l, o, v, y0, y0 + tall, 1f, out float t) && t < bestT) { bestT = t; best = l.buildingId; }
                if (HitsLot(l, o, v, y0, y0 + tall * CoreShare, CoreShare, out float tc) && tc < coreT) { coreT = tc; core = l.buildingId; }
                if (mill && HitsLot(l, o, v, y0 + MillSailsFrom, y0 + MillSailsTop, MillSailsWide, out float ts) && ts < bestT) { bestT = ts; best = l.buildingId; }
            }
            // #404: the lookout tower the people apart gave the Keeper
            var tv = ApartGroupsView.Instance;
            if (tv != null && tv.TowerForTap(out var tAt, out float tH))
            {
                var tl = new Lot { x = tAt.x, z = tAt.y, w = TowerTapSide, d = TowerTapSide };
                float ty = box.GroundHeight(tAt.x, tAt.y);
                if (HitsLot(tl, o, v, ty, ty + tH, 1f, out float tt) && tt < Mathf.Min(coreT, bestT)) return SimCore.TowerCardId;
            }
            return core >= 0 ? core : best; // the air round a house's eaves doesn't take a tap from the well behind it
        }

        /// <summary>#404: the tower's tap box, a side (box units): its legs and the platform, a little generous.</summary>
        const float TowerTapSide = 0.3f;

        /// <summary>A doll the camera can't see: a roof, a wall or the eaves stand between the lens and both its head and its middle, by the box's real
        /// meshes (tester run 8: a lot's core box let through dolls behind the eaves and just past the lot). A tap there is for what's in front.</summary>
        static bool Unseen(PeopleView pv, Camera cam, PersonRef who)
        {
            var box = FindAnyObjectByType<BoxView>();
            if (box == null || !pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) return false;
            var lens = cam.transform.position;
            return box.Blocked(lens, top) && box.Blocked(lens, (top + feet) * 0.5f);
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
            ShowMark(buildingId);
            return true;
        }

        GameObject markGo;
        Mesh markMesh;
        /// <summary>The soft mark round the open card's building is up (tests).</summary>
        public bool BuildingMarked => markGo != null && markGo.activeSelf;

        /// <summary>Tester run 7: a soft pale band on the ground round the lot, so the card says which building it means.</summary>
        void ShowMark(int buildingId)
        {
            var box = FindAnyObjectByType<BoxView>();
            var lot = Sim?.Settlement?.Lots.FirstOrDefault(l => l.buildingId == buildingId && !l.retired);
            if (buildingId == SimCore.TowerCardId && ApartGroupsView.Instance != null && ApartGroupsView.Instance.TowerForTap(out var tAt, out _))
                lot = new Lot { x = tAt.x, z = tAt.y, w = TowerTapSide, d = TowerTapSide }; // #404: round the tower's legs
            if (lot == null || box == null) { HideMark(); return; }
            if (markGo == null)
            {
                markGo = new GameObject("BuildingMark") { hideFlags = HideFlags.DontSave };
                markGo.AddComponent<MeshFilter>().sharedMesh = markMesh = new Mesh { name = "BuildingMark", hideFlags = HideFlags.DontSave };
                var r = markGo.AddComponent<MeshRenderer>();
                r.sharedMaterial = ShapingView.LoadFx();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            markGo.transform.SetParent(box.transform, false);
            markGo.transform.SetLocalPositionAndRotation(new Vector3(lot.x, box.GroundHeight(lot.x, lot.z) + 0.03f, lot.z), Quaternion.Euler(0f, lot.yaw, 0f));
            FillMark(markMesh, lot.w * 0.5f, lot.d * 0.5f);
            markGo.SetActive(true);
        }

        void HideMark() { if (markGo != null) markGo.SetActive(false); }

        /// <summary>Three nested rectangles, clear, then paper, then clear: a soft line round half-sizes (hw, hd).</summary>
        static void FillMark(Mesh m, float hw, float hd)
        {
            const float In = 0.05f, Out = 0.1f, Peak = 0.62f;
            var v = new Vector3[12];
            var c = new Color[12];
            var paper = new Color(1f, 0.96f, 0.86f, 0f);
            float[] grow = { -In, 0f, Out };
            float[] alpha = { 0f, Peak, 0f };
            var sx = new[] { -1f, 1f, 1f, -1f };
            var sz = new[] { -1f, -1f, 1f, 1f };
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < 4; i++)
                {
                    v[r * 4 + i] = new Vector3(sx[i] * (hw + grow[r]), 0f, sz[i] * (hd + grow[r]));
                    c[r * 4 + i] = new Color(paper.r, paper.g, paper.b, alpha[r]);
                }
            var t = new int[48];
            int n = 0;
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 4; i++)
                {
                    int a = r * 4 + i, b = r * 4 + (i + 1) % 4, d = (r + 1) * 4 + i, e = (r + 1) * 4 + (i + 1) % 4;
                    t[n++] = a; t[n++] = d; t[n++] = b;
                    t[n++] = b; t[n++] = d; t[n++] = e;
                }
            m.Clear();
            m.vertices = v;
            m.colors = c;
            m.triangles = t;
            m.RecalculateBounds();
        }

        public void CloseBuilding() => CloseBuilding(true);

        void CloseBuilding(bool sound)
        {
            if (!BuildingOpen) return;
            if (sound) UiSound.Play(UiRole.Close);
            BuildingId = -1;
            BuildingCard = null;
            HideMark();
            if (GameHud.Instance != null) GameHud.Instance.SetOutsideSheet(null);
            buildingSheet.Close(() => { if (!AnyOpen && !BuildingOpen) cardRoot.style.display = DisplayStyle.None; });
        }

        void FillBuilding(BuildingCardInfo c)
        {
            BuildingCard = c;
            buildingTitle.text = c.title;
            buildingWhat.text = GlassType.Lines(c.whatItIs, 15f, 1.35f);
            buildingYear.text = c.yearLine;
            buildingBy.text = GlassType.Lines(c.builderLine + TowerUpNow(c), 15f, 1.35f);
            buildingBuilder.style.display = c.Together ? DisplayStyle.None : DisplayStyle.Flex;
            if (!c.Together) buildingBuilder.text = c.builderName;
        }

        readonly System.Collections.Generic.List<PersonRef> towerUp = new();

        /// <summary>#404: on the tower's card, who is up there now (the people apart, and any townsfolk on the ladder or the platform).</summary>
        string TowerUpNow(BuildingCardInfo c)
        {
            if (c.buildingId != SimCore.TowerCardId) return "";
            var sim = Sim;
            var names = ApartGroupsView.Instance != null ? ApartGroupsView.Instance.UpTheTower(sim) : new System.Collections.Generic.List<string>();
            var pv = PeopleView.Find();
            if (pv != null && sim != null)
            {
                pv.TowerClimbers(towerUp);
                foreach (var who in towerUp)
                {
                    string n = who.IsNotable ? sim.FindNotable(who.notableId)?.name : who.IsCrowd ? sim.CrowdName(who.crowdIndex) : null;
                    if (!string.IsNullOrEmpty(n)) names.Add(n);
                }
            }
            if (names.Count == 0) return " Nobody is up there now.";
            if (names.Count == 1) return $" {names[0]} is up there now.";
            return $" Up there now: {string.Join(", ", names.GetRange(0, names.Count - 1))} and {names[names.Count - 1]}.";
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
