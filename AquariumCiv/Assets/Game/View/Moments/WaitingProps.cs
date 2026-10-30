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
            string key = w.waiting ? $"{w.next}:{w.pileLevel}:{sim.Settlement.Map.greenX:0.00}:{sim.Settlement.Map.greenZ:0.00}" : "";
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
            mesh = Build(w, sim.Settlement.Map.greenX, sim.Settlement.Map.greenZ);
            holder.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        Mesh Build(WaitingInfo w, float gx, float gz)
        {
            var mb = new MeshBuilder(17) { jitter = 0.01f };
            int n = EraWaiting.PileCount(w.pileLevel);
            Vector3 G(float dx, float dz) { float x = gx + dx, z = gz + dz; return new Vector3(x, box.GroundHeight(x, z), z); }
            if (w.next == Era.Village)
            {
                // seed baskets stacked by the fire: a pyramid that grows with the wait
                var c = G(0.34f, 0.2f);
                int placed = 0;
                for (int row = 0; placed < n; row++)
                    for (int k = 0; k < 4 - row && placed < n; k++, placed++)
                    {
                        var p = c + new Vector3((k - (3 - row) * 0.5f) * 0.075f, row * 0.055f, (row % 2) * 0.02f);
                        mb.Prism(p, 0.03f, 0.036f, 0.05f, 7, Palette.Wheat);
                        mb.Ico(p + new Vector3(0f, 0.05f, 0f), 0.026f, new Vector3(1f, 0.45f, 1f), Palette.Sprout, 0.1f);
                    }
                // a stake by the fire for every tent (a row of them here) and a cleared strip at the edge
                for (int k = 0; k < 5; k++)
                    mb.Box(G(-0.3f + k * 0.12f, -0.32f) + new Vector3(0f, 0.05f, 0f), new Vector3(0.016f, 0.1f, 0.016f), Palette.WoodLight);
                mb.Box(G(0.75f, -0.05f) + new Vector3(0f, 0.004f, 0f), new Vector3(0.5f, 0.008f, 0.16f), Palette.Dirt);
            }
            else
            {
                // timber and stone on the green, a bell hung on a post
                var t = G(0.36f, 0.22f);
                for (int k = 0; k < n; k++)
                {
                    int row = k / 3, col = k % 3;
                    mb.Box(t + new Vector3(0f, 0.02f + row * 0.035f, (col - 1) * 0.04f), new Vector3(0.26f, 0.032f, 0.034f), k % 2 == 0 ? Palette.WoodLight : Palette.Bark);
                }
                var s = G(-0.34f, 0.24f);
                for (int k = 0; k < n; k++)
                {
                    float a = k * 2.4f;
                    float r = 0.03f + 0.012f * (k % 3);
                    mb.Ico(s + new Vector3(Mathf.Cos(a) * 0.05f, 0.025f + (k / 4) * 0.04f, Mathf.Sin(a) * 0.05f), r, Vector3.one, k % 2 == 0 ? Palette.RockLight : Palette.Rock, 0.2f);
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
