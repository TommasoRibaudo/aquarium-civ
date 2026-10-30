using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The piles the people make while an era waits (era-moment.md §2, #88): seed baskets and a row of stakes by the fire
    /// (waiting for the village), timber and stone on the green with a bell on a post (waiting for the town). The growing
    /// piles get bigger with the wait (<see cref="EraWaiting.PileLevel"/>): a comic stack, never anything sad. Placeholder
    /// looks in the style kit's flat colours, one mesh, rebuilt only when the state changes; #89/#90 replace them.
    /// Lives on the BoxView (added by <see cref="MomentPlayer"/>).
    /// </summary>
    public class WaitingProps : MonoBehaviour
    {
        BoxView box;
        GameObject holder;
        Mesh mesh;
        string shown = "";

        public bool Showing => holder != null && holder.activeSelf;
        public int Level { get; private set; }

        void Awake() => box = GetComponent<BoxView>();

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (holder != null) Destroy(holder);
        }

        public void Sync(WaitingInfo w, SimCore sim)
        {
            if (box == null || sim?.Settlement == null) return;
            string key = w.waiting ? $"{w.next}:{w.pileLevel}:{sim.Settlement.Lots.Count}:{sim.Settlement.Map.greenX:0.00}:{sim.Settlement.Map.greenZ:0.00}" : "";
            if (key == shown) return;
            shown = key;
            Level = w.waiting ? w.pileLevel : 0;
            if (!w.waiting) { if (holder != null) holder.SetActive(false); return; }
            if (holder == null)
            {
                holder = new GameObject("WaitingProps") { hideFlags = HideFlags.DontSave };
                holder.transform.SetParent(transform, false);
                holder.AddComponent<MeshFilter>();
                var r = holder.AddComponent<MeshRenderer>();
                r.sharedMaterial = box.SolidMaterial;
            }
            holder.SetActive(true);
            if (mesh != null) Destroy(mesh);
            mesh = Build(w, sim, sim.Settlement.Map.greenX, sim.Settlement.Map.greenZ);
            holder.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        /// <summary>Pile size: a tent is about 0.25 high; a basket about a third of that, so a stack reads at default zoom.</summary>
        public const float K = 1.7f;
        /// <summary>Where the piles stand, from the green's centre: front-left of the fire, where the camp keeps open ground.</summary>
        public static readonly Vector2 PileSpot = new(-0.42f, -0.2f), SecondSpot = new(-0.1f, -0.42f);

        Mesh Build(WaitingInfo w, SimCore sim, float gx, float gz)
        {
            var mb = new MeshBuilder(17) { jitter = 0.01f };
            int n = EraWaiting.PileCount(w.pileLevel);
            Vector3 G(float dx, float dz) { float x = gx + dx, z = gz + dz; return new Vector3(x, box.GroundHeight(x, z), z); }
            Vector3 At(float x, float z) => new(x, box.GroundHeight(x, z), z);
            if (w.next == Era.Village)
            {
                // seed baskets stacked by the fire: a pyramid that grows with the wait
                var c = G(PileSpot.x, PileSpot.y);
                int placed = 0;
                for (int row = 0; placed < n; row++)
                    for (int k = 0; k < 4 - row && placed < n; k++, placed++)
                    {
                        var p = c + new Vector3((k - (3 - row) * 0.5f) * 0.075f * K, row * 0.052f * K, (row % 2) * 0.02f * K);
                        mb.Prism(p, 0.03f * K, 0.036f * K, 0.05f * K, 7, Palette.Thatch);
                        mb.Ico(p + new Vector3(0f, 0.05f * K, 0f), 0.026f * K, new Vector3(1f, 0.45f, 1f), Palette.Wheat, 0.1f);
                    }
                // a stake by every tent's door
                foreach (var l in sim.Settlement.Lots)
                {
                    if (l.retired || l.kind != AquariumCiv.Sim.Settle.LotKind.Hut) continue;
                    float sx = l.door >= 0 ? l.doorX : l.x, sz = l.door >= 0 ? l.doorZ : l.z - 0.15f;
                    mb.Box(At(sx + 0.06f, sz) + new Vector3(0f, 0.06f * K, 0f), new Vector3(0.014f * K, 0.12f * K, 0.014f * K), Palette.WoodLight);
                    mb.Box(At(sx + 0.06f, sz) + new Vector3(0f, 0.115f * K, 0f), new Vector3(0.03f * K, 0.012f * K, 0.006f * K), Palette.Terracotta);
                }
                // a cleared strip of ground at the edge of the camp: a rake of bare soil
                var strip = G(SecondSpot.x, SecondSpot.y);
                mb.Box(strip + new Vector3(0f, 0.004f, 0f), new Vector3(0.42f, 0.008f, 0.14f), Palette.FieldSoil);
                for (int k = 0; k < 3; k++)
                    mb.Box(strip + new Vector3(0f, 0.01f, (k - 1) * 0.045f), new Vector3(0.4f, 0.006f, 0.012f), Palette.SoilLight);
            }
            else
            {
                // timber and stone on the green, a bell hung on a post
                var t = G(PileSpot.x, PileSpot.y);
                for (int k = 0; k < n; k++)
                {
                    int row = k / 3, col = k % 3;
                    mb.Box(t + new Vector3(0f, (0.02f + row * 0.035f) * K, (col - 1) * 0.04f * K), new Vector3(0.26f * K, 0.032f * K, 0.034f * K), k % 2 == 0 ? Palette.WoodLight : Palette.Bark);
                }
                var s = G(SecondSpot.x, SecondSpot.y);
                for (int k = 0; k < n; k++)
                {
                    float a = k * 2.4f;
                    float r = (0.03f + 0.012f * (k % 3)) * K;
                    mb.Ico(s + new Vector3(Mathf.Cos(a) * 0.05f * K, (0.025f + (k / 4) * 0.04f) * K, Mathf.Sin(a) * 0.05f * K), r, Vector3.one, k % 2 == 0 ? Palette.RockLight : Palette.Rock, 0.2f);
                }
                var b = G(0f, -0.36f);
                mb.Box(b + new Vector3(0f, 0.12f, 0f), new Vector3(0.022f, 0.24f, 0.022f), Palette.Timber);
                mb.Box(b + new Vector3(0.05f, 0.23f, 0f), new Vector3(0.11f, 0.016f, 0.016f), Palette.Timber);
                mb.Prism(b + new Vector3(0.09f, 0.16f, 0f), 0.035f, 0.018f, 0.06f, 8, Palette.Thatch);
            }
            return mb.ToMesh("WaitingProps");
        }
    }
}
