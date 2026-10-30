using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// The shelf around the box on screen (#93; era-moment.md section 8 "The museum on the shelf", section 9): code-built props
    /// in the box's palette, nothing saved with the scene.
    /// Every other box on the shelf stands beside the one on screen as a glass-box proxy (a terrain slab in its setting's
    /// colour); the finished ones are museums: a lit lamp over the box, a brass plate in front (place, years, epithet or
    /// title) and the relics on plinths in front, ordered by era. The box on screen, if it is a museum, wears the same
    /// dressing at the origin. The last moment stages the new box being set down, the ruler and the founders walking it.
    /// </summary>
    public class ShelfProps : MonoBehaviour
    {
        public const float SlotStep = 7.6f;

        BoxView box;
        Transform root, staged, ruler, dressRoot;
        readonly List<Transform> proxies = new();
        readonly List<PegDollView> founders = new();
        string signature = "";
        TextMesh plateText;
        Light lampLight;
        Transform lampBulb;
        string plateFull = "";
        /// <summary>A box the shelf does not draw as a proxy (the new box is staged there during the last moment).</summary>
        public int HideIndex = -1;

        public Transform StagedBox => staged;
        public TextMesh PlateText => plateText;
        public string PlateFull => plateFull;
        public bool LampLit => lampLight != null && lampLight.enabled;
        public int ProxyCount => proxies.Count;
        public bool Dressed => dressRoot != null;
        public IReadOnlyList<PegDollView> FounderDolls => founders;

        public static ShelfProps Ensure(BoxView b)
        {
            if (!b.TryGetComponent(out ShelfProps p)) p = b.gameObject.AddComponent<ShelfProps>();
            p.box = b;
            return p;
        }

        void OnDisable() => Clear();

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        public void Clear()
        {
            if (root != null) Kill(root.gameObject);
            root = staged = ruler = dressRoot = null;
            proxies.Clear(); founders.Clear();
            plateText = null; lampLight = null; lampBulb = null;
            signature = "";
        }

        Transform Root()
        {
            if (root == null)
            {
                root = new GameObject("Shelf") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(box.transform, false);
            }
            return root;
        }

        /// <summary>The x of the n-th other box (0 = the nearest, on the left of the box on screen).</summary>
        public static float SlotX(int n) => -SlotStep * (n + 1);

        // ---------------- sync ----------------
        /// <summary>Puts the props in line with the shelf: proxies of every box but the one on screen, and the dressing of the
        /// box on screen if it is a museum. Rebuilds only when something changed.</summary>
        public void Sync(ShelfState shelf, int shownIndex, bool lampOn = true, bool force = false)
        {
            if (box == null || box.Root == null || shelf == null) return;
            string sig = Signature(shelf, shownIndex, lampOn, HideIndex);
            if (!force && sig == signature && root != null) return;
            signature = sig;
            if (root != null)
            {
                var keep = new HashSet<Transform>();
                if (staged != null) keep.Add(staged);
                if (ruler != null) keep.Add(ruler);
                foreach (var f in founders) if (f != null) keep.Add(f.transform);
                var kill = new List<Transform>();
                foreach (Transform c in root) if (!keep.Contains(c)) kill.Add(c);
                foreach (var c in kill) Kill(c.gameObject);
                proxies.Clear();
            }
            Root();
            dressRoot = null; plateText = null; lampLight = null; lampBulb = null;
            var others = shelf.boxes.Where(b => b.index != shownIndex && b.index != HideIndex).OrderBy(b => b.index).ToList();
            for (int i = 0; i < others.Count; i++) proxies.Add(Proxy(shelf, others[i], SlotX(i), lampOn));
            var shown = shelf.Box(shownIndex);
            if (shown != null && shown.museum) dressRoot = Dress(shelf, shown, 0f, lampOn, true);
        }

        static string Signature(ShelfState s, int shown, bool lamp, int hide)
            => shown + "|" + hide + "|" + lamp + "|" + string.Join(";", s.boxes.Select(b => b.index + ":" + b.setting + ":" + b.museum + ":" + b.plate))
               + "|" + string.Join(";", s.plinths.Select(p => p.boxIndex + p.relicId));

        // ---------------- a box proxy ----------------
        Transform Proxy(ShelfState shelf, ShelfBox b, float x, bool lampOn)
        {
            var t = new GameObject("ShelfBox" + b.index) { hideFlags = HideFlags.DontSave }.transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(x, 0f, 0f);
            BoxBody(t, b.setting, 1f);
            if (b.museum) Dress(shelf, b, 0f, lampOn, false, t);
            return t;
        }

        /// <summary>The empty box body: a dark base, a terrain slab in the setting's colours and the glass frame.</summary>
        public Transform BoxBody(Transform parent, string setting, float fill)
        {
            var mb = new MeshBuilder(11) { jitter = 0.02f };
            float hx = Land.HX, hz = Land.HZ;
            mb.Box(new Vector3(0, 0.06f, 0), new Vector3(hx * 2 + 0.3f, 0.12f, hz * 2 + 0.3f), Palette.Timber);
            Color top, under;
            switch (setting)
            {
                case BoxSetting.DesertOasis: top = Palette.Sand; under = Palette.SandWet; break;
                case BoxSetting.MountainVillage: top = Palette.RockLight; under = Palette.RockDark; break;
                default: top = Palette.GrassA; under = Palette.Loam; break;
            }
            float slab = 0.9f * fill;
            mb.Box(new Vector3(0, 0.12f + slab * 0.5f, 0), new Vector3(hx * 2, slab, hz * 2), under);
            mb.Box(new Vector3(0, 0.12f + slab, 0), new Vector3(hx * 2 - 0.02f, 0.06f, hz * 2 - 0.02f), top);
            float gy0 = 0.12f + slab;
            if (setting == BoxSetting.DesertOasis)
            {
                mb.Prism(new Vector3(-1.0f, gy0, 0.4f), 0.55f, 0.5f, 0.02f, 12, Palette.Water);
                mb.Prism(new Vector3(-1.0f, gy0, 0.4f), 0.04f, 0.03f, 0.5f, 6, Palette.Bark);
                mb.Ico(new Vector3(-1.0f, gy0 + 0.55f, 0.4f), 0.2f, new Vector3(1.4f, 0.5f, 1.4f), Palette.LeafA, 0.2f);
            }
            else if (setting == BoxSetting.MountainVillage)
            {
                mb.Prism(new Vector3(1.0f, gy0, -0.8f), 1.1f, 0.15f, 1.5f, 6, Palette.Rock, topCol: Palette.Plaster);
                mb.Prism(new Vector3(-1.2f, gy0, -1.0f), 0.8f, 0.12f, 1.0f, 6, Palette.RockDark);
            }
            else mb.Ico(new Vector3(1.2f, gy0 + 0.12f, -0.6f), 0.3f, new Vector3(1f, 0.8f, 1f), Palette.LeafB, 0.2f);
            float gy = Land.GlassTop;
            foreach (var sx in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    mb.Box(new Vector3(sx * hx, gy * 0.5f, sz * hz), new Vector3(0.05f, gy, 0.05f), Palette.Frame);
            mb.Box(new Vector3(0, gy, hz), new Vector3(hx * 2, 0.05f, 0.05f), Palette.Frame);
            mb.Box(new Vector3(0, gy, -hz), new Vector3(hx * 2, 0.05f, 0.05f), Palette.Frame);
            mb.Box(new Vector3(hx, gy, 0), new Vector3(0.05f, 0.05f, hz * 2), Palette.Frame);
            mb.Box(new Vector3(-hx, gy, 0), new Vector3(0.05f, 0.05f, hz * 2), Palette.Frame);
            var go = new GameObject("Body") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("ShelfBoxBody");
            go.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
            return go.transform;
        }

        // ---------------- the museum dressing ----------------
        /// <summary>The lamp, the plate and the plinths of a finished box. Returns the dressing's root.</summary>
        public Transform Dress(ShelfState shelf, ShelfBox b, float x, bool lampOn, bool main, Transform parent = null)
        {
            var d = new GameObject("Museum" + b.index) { hideFlags = HideFlags.DontSave }.transform;
            d.SetParent(parent != null ? parent : root, false);
            d.localPosition = new Vector3(x, 0f, 0f);
            float hz = Land.HZ;
            var mb = new MeshBuilder(21) { jitter = 0.01f };
            mb.Box(new Vector3(0, 1.8f, hz + 0.35f), new Vector3(0.07f, 3.6f, 0.07f), PegDoll.Brass);
            mb.Box(new Vector3(0, 3.55f, hz + 0.1f), new Vector3(0.07f, 0.07f, 0.6f), PegDoll.Brass);
            Part(d, "LampArm", mb.ToMesh("MuseumLamp"), Vector3.zero);
            var bulbMb = new MeshBuilder(22) { jitter = 0f };
            bulbMb.Ico(Vector3.zero, 0.13f, Vector3.one, Palette.Lamp, 0f);
            var bulb = Part(d, "LampBulb", bulbMb.ToMesh("MuseumBulb"), new Vector3(0, 3.42f, hz - 0.1f));
            var light = new GameObject("LampLight") { hideFlags = HideFlags.DontSave };
            light.transform.SetParent(bulb, false);
            var l = light.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.5f); l.range = 7f; l.intensity = 6f; l.enabled = lampOn;
            bulb.GetComponent<MeshRenderer>().enabled = lampOn;
            if (main) { lampLight = l; lampBulb = bulb; }
            // the brass plate on the base's front edge
            var pm = new MeshBuilder(23) { jitter = 0.005f };
            pm.Box(Vector3.zero, new Vector3(PlateW, PlateH, 0.06f), PegDoll.Brass);
            var plate = Part(d, "Plate", pm.ToMesh("MuseumPlate"), new Vector3(0, 0.05f + PlateH * 0.5f, -hz - 0.3f));
            plate.localRotation = Quaternion.Euler(14f, 0, 0);
            string text = PlateLines(b);
            var tm = new GameObject("PlateText") { hideFlags = HideFlags.DontSave };
            tm.transform.SetParent(plate, false);
            tm.transform.localPosition = new Vector3(0, 0f, -0.04f);
            tm.transform.localScale = Vector3.one * PlateTextScale(text);
            var t = tm.AddComponent<TextMesh>();
            t.text = text; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
            t.fontSize = 48; t.characterSize = 0.6f; t.color = new Color(0.22f, 0.15f, 0.07f);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
            if (main) { plateText = t; plateFull = text; }
            // the plinths: relics in front, ordered by era
            var pl = shelf.plinths.Where(p => p.boxIndex == b.index).OrderBy(p => p.foundMonth).ToList();
            for (int i = 0; i < pl.Count; i++)
            {
                float px = (i - (pl.Count - 1) * 0.5f) * 1.1f;
                var m2 = new MeshBuilder(30 + i) { jitter = 0.01f };
                m2.Prism(Vector3.zero, 0.3f, 0.26f, 0.42f, 8, Palette.Plinth, topCol: Palette.PlinthTop);
                Relic(m2, pl[i].relicId, 0.42f);
                Part(d, "Plinth_" + pl[i].relicId, m2.ToMesh("Plinth"), new Vector3(px, 0.02f, -hz - 1.5f));
            }
            return d;
        }

        /// <summary>The brass plate's size (box units): three lines fit on it.</summary>
        public const float PlateW = 3.1f, PlateH = 0.8f;

        /// <summary>The plate text's scale so every line fits inside the plate with a margin (tester batch 2: the third line ran
        /// off the bottom). Measured on the built-in font at fontSize 48, characterSize 0.6: a line is ~4.3 units tall and a
        /// character ~2.6 wide per unit of scale.</summary>
        public static float PlateTextScale(string text)
        {
            var lines = string.IsNullOrEmpty(text) ? new[] { "" } : text.Split('\n');
            int longest = 1;
            foreach (var l in lines) longest = Mathf.Max(longest, l.Length);
            float byHeight = (PlateH - 0.12f) / (lines.Length * 4.3f);
            float byWidth = (PlateW - 0.3f) / (longest * 2.6f);
            return Mathf.Min(0.05f, byHeight, byWidth);
        }

        public static string PlateLines(ShelfBox b) => string.IsNullOrEmpty(b.plate) ? "" : b.plate.Replace(" - ", "\n").Replace(" · ", "\n");

        static void Relic(MeshBuilder mb, string id, float y)
        {
            switch (id)
            {
                case RelicId.SkyCrumb: mb.Ico(new Vector3(0, y + 0.1f, 0), 0.13f, new Vector3(1.25f, 0.8f, 1f), Palette.Wheat, 0.2f, Palette.Thatch); break;
                case RelicId.KeeperDoll:
                    mb.Prism(new Vector3(0, y, 0), 0.07f, 0.05f, 0.2f, 6, Palette.Tunics[1]);
                    mb.Ico(new Vector3(0, y + 0.26f, 0), 0.06f, Vector3.one, Palette.Skins[1], 0f); break;
                case RelicId.Lens:
                    mb.Prism(new Vector3(0, y + 0.06f, 0), 0.04f, 0.04f, 0.3f, 8, PegDoll.Brass, Quaternion.Euler(-70f, 0, 0), true, true, Palette.GlassEdge); break;
                case RelicId.LeftSandal:
                    mb.Box(new Vector3(0, y + 0.02f, 0), new Vector3(0.14f, 0.04f, 0.22f), Palette.Accents[0]); break;
                case RelicId.FestivalMask:
                    mb.Prism(new Vector3(0, y + 0.02f, 0), 0.13f, 0.13f, 0.03f, 10, Palette.Accents[1], Quaternion.Euler(-80f, 0, 0), true, true, Palette.Accents[4]); break;
                default: mb.Ico(new Vector3(0, y + 0.1f, 0), 0.1f, Vector3.one, Palette.Stone, 0.2f); break;
            }
        }

        Transform Part(Transform parent, string name, Mesh mesh, Vector3 pos)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial; r.shadowCastingMode = ShadowCastingMode.On;
            return go.transform;
        }

        // ---------------- the lamp and the plate (the last moment) ----------------
        public void SetLamp(bool on)
        {
            if (lampLight != null) lampLight.enabled = on;
            if (lampBulb != null) lampBulb.GetComponent<MeshRenderer>().enabled = on;
        }

        /// <summary>Engraves the plate: the first t (0..1) of its letters.</summary>
        public void EngravePlate(float t)
        {
            if (plateText == null) return;
            int n = Mathf.Clamp(Mathf.RoundToInt(plateFull.Length * Mathf.Clamp01(t)), 0, plateFull.Length);
            plateText.text = plateFull.Substring(0, n);
        }

        // ---------------- the new box and the crossing ----------------
        /// <summary>Sets a new empty box down on the shelf to the right of the box on screen; drop 0..1 lowers it from above.</summary>
        public Transform StageNewBox(string setting, float drop = 1f)
        {
            Root();
            if (staged == null)
            {
                staged = new GameObject("NewBox") { hideFlags = HideFlags.DontSave }.transform;
                staged.SetParent(root, false);
                BoxBody(staged, setting, 0.9f);
            }
            float k = 1f - Mathf.Clamp01(drop);
            staged.localPosition = new Vector3(SlotStep, 2.2f * k * k, 0f);
            return staged;
        }

        /// <summary>The ruler laid from the old box to the new one, t 0..1 of its length.</summary>
        public void LayRuler(float t)
        {
            Root();
            float len = SlotStep - 2 * Land.HX + 0.4f;
            if (ruler == null)
            {
                var mb = new MeshBuilder(41) { jitter = 0.005f };
                mb.Box(Vector3.zero, new Vector3(1f, 0.06f, 0.5f), Palette.WoodLight);
                for (int i = 0; i <= 12; i++) mb.Box(new Vector3(-0.5f + i / 12f, 0.035f, 0.2f), new Vector3(0.02f, 0.02f, i % 4 == 0 ? 0.18f : 0.1f), Palette.Timber);
                ruler = Part(root, "Ruler", mb.ToMesh("Ruler"), Vector3.zero);
            }
            t = Mathf.Max(0.001f, Mathf.Clamp01(t));
            ruler.localScale = new Vector3(len * t, 1f, 1f);
            ruler.localPosition = new Vector3(Land.HX + 0.1f + len * t * 0.5f, 0.95f, 0f);
        }

        /// <summary>The founders as small peg dolls with bundles, standing at the old box's right edge.</summary>
        public void MakeFounders(IList<(int seed, bool kid)> who)
        {
            Root();
            foreach (var f in founders) if (f != null) Kill(f.gameObject);
            founders.Clear();
            for (int i = 0; i < who.Count; i++)
            {
                var look = PegDoll.PickLook(who[i].seed, false, who[i].kid, i % Palette.Accents.Length);
                var d = PegDollView.Create(root, box.SolidMaterial, look, who[i].kid, who[i].seed);
                d.SetProp(PropKind.Bundle, PropSlot.Back);
                d.transform.localScale = Vector3.one * 1.6f;
                founders.Add(d);
                PlaceFounder(i, 0f);
            }
        }

        /// <summary>Puts founder i: u = 0 waiting at the old box's edge, 1 arrived in the new box. The leader (0) goes first.</summary>
        public void PlaceFounder(int i, float u)
        {
            if (i < 0 || i >= founders.Count || founders[i] == null) return;
            float x0 = Land.HX + 0.45f, x1 = SlotStep - 1.4f;
            float delay = i * 0.07f;
            float k = Mathf.Clamp01((u - delay) / (1f - delay));
            float mid = (i - (founders.Count - 1) * 0.5f);
            float z = mid * Mathf.Lerp(0.3f, 0.5f, k);
            founders[i].transform.localPosition = new Vector3(Mathf.Lerp(x0, x1, k), 0.95f, z);
            founders[i].transform.localRotation = Quaternion.Euler(0, k > 0f && k < 1f ? 90f : 180f, 0);
        }

        public void ClearStage()
        {
            foreach (var f in founders) if (f != null) Kill(f.gameObject);
            founders.Clear();
            if (staged != null) Kill(staged.gameObject);
            if (ruler != null) Kill(ruler.gameObject);
            staged = ruler = null;
        }
    }
}
