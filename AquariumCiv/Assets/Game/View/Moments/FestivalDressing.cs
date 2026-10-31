using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The era festival's look (era-moment.md §6, #91): while the sim's festival year runs (<see cref="SimCore.FestivalOn"/>)
    /// bunting hangs on poles round the green (village) or the square (town) and two long tables with benches stand on it.
    /// The ring dance and the evenings are the people's (the life planner's festival scenes). One mesh in the style kit's
    /// flat colours, built when the festival starts and dropped when it ends. Lives on the BoxView (added by
    /// <see cref="MomentPlayer"/>).
    /// </summary>
    public class FestivalDressing : MonoBehaviour
    {
        public const int Poles = 7;
        public const float RingR = SiteMap.GreenR + 0.06f, PoleH = 0.42f;

        BoxView box;
        GameObject holder;
        Mesh mesh;
        string shown = "";

        public bool Showing => holder != null && holder.activeSelf;
        public int Flags { get; private set; }
        public int Tables { get; private set; }

        void Awake() => box = GetComponent<BoxView>();

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (holder != null) Destroy(holder);
        }

        public void Sync(SimCore sim)
        {
            if (box == null || sim?.Settlement == null) return;
            var map = sim.Settlement.Map;
            string key = sim.FestivalOn ? $"{sim.State.era}:{map.greenX:0.00}:{map.greenZ:0.00}:{sim.State.buildings.Count}" : "";
            if (key == shown) return;
            shown = key;
            if (!sim.FestivalOn) { if (holder != null) holder.SetActive(false); Flags = Tables = 0; return; }
            if (holder == null)
            {
                holder = new GameObject("FestivalDressing") { hideFlags = HideFlags.DontSave };
                holder.transform.SetParent(transform, false);
                holder.AddComponent<MeshFilter>();
                holder.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
            }
            holder.SetActive(true);
            if (mesh != null) Destroy(mesh);
            mesh = Build(sim, map.greenX, map.greenZ);
            holder.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        Mesh Build(SimCore sim, float gx, float gz)
        {
            var mb = new MeshBuilder(91) { jitter = 0f };
            Vector3 G(float x, float z) => new(x, box.GroundHeight(x, z), z);
            Color[] flagCols = { Palette.Terracotta, Palette.FlameCoreA, Palette.Plaster, Palette.Flowers[3], Palette.Water };
            Flags = 0;

            // Poles in a ring (an arc left open towards the front, so the camera looks in), bunting strung between them.
            var tops = new Vector3[Poles];
            for (int i = 0; i < Poles; i++)
            {
                float a = Mathf.Lerp(-20f, 200f, i / (float)(Poles - 1)) * Mathf.Deg2Rad; // the back (+z) and the sides; the front stays open to the camera
                var foot = G(gx + Mathf.Cos(a) * RingR, gz + Mathf.Sin(a) * RingR);
                mb.Box(foot + new Vector3(0f, PoleH * 0.5f, 0f), new Vector3(0.022f, PoleH, 0.022f), Palette.Timber);
                tops[i] = foot + new Vector3(0f, PoleH, 0f);
            }
            for (int i = 0; i + 1 < Poles; i++)
            {
                var a = tops[i]; var b = tops[i + 1];
                const int n = 4; // big flags: they have to read at default zoom
                for (int k = 0; k < n; k++)
                {
                    float u0 = (k + 0.15f) / n, u1 = (k + 0.85f) / n, um = (k + 0.5f) / n;
                    Vector3 P(float u) => Vector3.Lerp(a, b, u) + Vector3.down * (0.07f * 4f * u * (1f - u)); // the string sags
                    var p0 = P(u0); var p1 = P(u1); var tip = P(um) + Vector3.down * 0.1f;
                    var col = flagCols[(i * n + k) % flagCols.Length];
                    mb.Tri(p0, p1, tip, col);
                    mb.Tri(p1, p0, tip, col); // both sides
                    Flags++;
                }
            }

            // Two long tables with benches, on the open side of the green, the well and the stalls left free.
            Tables = 0;
            for (int t = 0; t < 2; t++)
            {
                var c = G(gx - 0.2f, gz + (t == 0 ? 0.12f : -0.2f));
                const float len = 0.34f, w = 0.07f, h = 0.05f;
                mb.Box(c + new Vector3(0f, h, 0f), new Vector3(len, 0.012f, w), Palette.WoodLight);
                for (int leg = -1; leg <= 1; leg += 2)
                    mb.Box(c + new Vector3(leg * len * 0.42f, h * 0.5f, 0f), new Vector3(0.012f, h, w * 0.8f), Palette.Timber);
                for (int side = -1; side <= 1; side += 2)
                    mb.Box(c + new Vector3(0f, h * 0.55f, side * (w * 0.5f + 0.03f)), new Vector3(len * 0.9f, 0.01f, 0.025f), Palette.Timber);
                // bread, jugs and fruit on the table
                for (int k = 0; k < 4; k++)
                    mb.Ico(c + new Vector3((k - 1.5f) * 0.075f, h + 0.016f, 0f), 0.014f, Vector3.one, k % 2 == 0 ? Palette.Thatch : Palette.Terracotta, 0.1f);
                Tables++;
            }
            return mb.ToMesh("FestivalDressing");
        }
    }
}
