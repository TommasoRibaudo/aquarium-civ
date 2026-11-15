using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// Sign-off 2026-11-14 (#651, the user: "mending a bedroll should happen near the fire only and an actual bedroll should appear,
    /// people resting on it. It should last until the end of current session"): every bedroll mended by the fire is laid out on the
    /// ground round it, kept for this session only (never saved: a new session, or another box, starts with none), and the people
    /// sitting, napping, eating or listening at the fire take them (<see cref="PeopleView"/> seats them on one, a nap lies down on it).
    /// Box-local spots on a ring round the fire, away from the grass heap or log stack (<see cref="Pieces.GrassHeapAt"/>).
    /// </summary>
    public static class CampBedrolls
    {
        /// <summary>The most laid out at once, how far from the fire's middle, and the angles tried (degrees from the heap's side).</summary>
        public const int Max = 6;
        public const float Ring = 0.46f;
        static readonly float[] Angles = { 150f, 205f, 100f, 255f, 55f, 300f };

        static readonly List<Vector2> laid = new();
        static readonly List<GameObject> drawn = new();
        static int sessionSim = int.MinValue;
        static Transform drawnUnder;
        static Mesh mesh;

        public static int Count => laid.Count;
        public static IReadOnlyList<Vector2> Spots => laid;

        /// <summary>Forget them all when the box is another one (another sim's seed): they belong to this session and this box.</summary>
        public static void Bind(int simSeed)
        {
            if (simSeed == sessionSim) return;
            sessionSim = simSeed;
            laid.Clear();
            Undraw();
        }

        /// <summary>A mended bedroll is laid out by the fire at <paramref name="fire"/> (box-local). False when the ring is full.</summary>
        public static bool Lay(Vector2 fire)
        {
            if (laid.Count >= Max) return false;
            float heap = Mathf.Atan2(Pieces.GrassHeapAt.z, Pieces.GrassHeapAt.x) * Mathf.Rad2Deg;
            float a = (heap + Angles[laid.Count]) * Mathf.Deg2Rad;
            laid.Add(fire + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Ring);
            return true;
        }

        /// <summary>Debug and tests: none laid.</summary>
        public static void Clear() { laid.Clear(); Undraw(); }

        /// <summary>Draws them (each its own object, lying along the line to the fire, the rolled pillow end away from it) under the box.</summary>
        public static void Draw(Transform under, Land land, Vector2 fire)
        {
            if (under != drawnUnder) { Undraw(); drawnUnder = under; }
            if (under == null || land == null) return;
            if (mesh == null) { var mb = new MeshBuilder(651) { jitter = 0f }; PegDoll.LaidBedroll(mb); mesh = mb.ToMesh("LaidBedroll"); mesh.hideFlags = HideFlags.DontSave; }
            var mat = under.GetComponentInParent<BoxView>() is BoxView bv ? bv.SolidMaterial : null;
            while (drawn.Count < laid.Count)
            {
                var p = laid[drawn.Count];
                var go = new GameObject("Bedroll " + drawn.Count) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(under, false);
                go.transform.localPosition = land.OnGround(p.x, p.y);
                var toFire = fire - p;
                go.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(toFire.x, toFire.y) * Mathf.Rad2Deg, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                drawn.Add(go);
            }
        }

        static void Undraw()
        {
            foreach (var g in drawn) if (g != null) Object.Destroy(g);
            drawn.Clear();
            drawnUnder = null;
        }
    }
}
