using System;
using UnityEngine;

namespace AquariumCiv.Spikes.StyleKit
{
    /// <summary>
    /// A peg-doll villager: a tapered tunic, two stub arms, a round head with hair or a hat. No faces, no weapons: they
    /// read as ordinary people from the default zoom, and a tunic colour is enough to tell them apart. In play mode they
    /// amble between spots near home with a little walking bob.
    /// </summary>
    public class Villager : MonoBehaviour
    {
        public Vector2 home;
        public float range = 1.5f, speed = 0.16f;
        public int seed;
        public Func<Vector2, bool> isBlocked;

        System.Random rng;
        Vector2 target;
        float wait;

        public static void BuildMesh(MeshBuilder mb, System.Random rng, bool kid, bool camp)
        {
            T Pick<T>(T[] a) => a[rng.Next(a.Length)];
            float s = kid ? 0.68f : 1f;
            var tunic = camp ? Color.Lerp(Pick(Palette.Tunics), Palette.Hide, 0.55f) : Pick(Palette.Tunics);
            var skin = Pick(Palette.Skins);
            float bodyH = 0.13f * s;
            mb.Prism(Vector3.zero, 0.055f * s, 0.038f * s, bodyH, 8, tunic, topCol: tunic);
            mb.Prism(new Vector3(0, bodyH * 0.42f, 0), 0.049f * s, 0.046f * s, 0.012f * s, 8, Color.Lerp(tunic, Palette.Door, 0.6f)); // belt
            foreach (int side in new[] { -1, 1 })
                mb.Prism(new Vector3(side * 0.046f * s, bodyH * 0.92f, 0), 0.014f * s, 0.012f * s, 0.075f * s, 5, tunic,
                    Quaternion.Euler(0, 0, 180f + side * 14f), true, true, skin);
            float headY = bodyH + 0.04f * s;
            mb.Ico(new Vector3(0, headY, 0), 0.043f * s, Vector3.one, skin, 0.06f);
            int style = rng.Next(4);
            var hair = Pick(Palette.Hair);
            if (style == 3 && !kid)
            {
                // Straw hat: brim and crown.
                mb.Prism(new Vector3(0, headY + 0.022f * s, 0), 0.075f * s, 0.075f * s, 0.008f * s, 10, Palette.Straw);
                mb.Prism(new Vector3(0, headY + 0.03f * s, 0), 0.036f * s, 0.028f * s, 0.03f * s, 8, Palette.Straw);
            }
            else
            {
                // Hair: a cap over the top and back of the head; style 1 adds a bun, style 2 a hood in the tunic colour.
                var cap = style == 2 && camp ? Palette.Hide : hair;
                mb.Ico(new Vector3(0, headY + 0.012f * s, 0.008f * s), 0.047f * s, new Vector3(1f, 0.8f, 1f), cap, 0.05f);
                if (style == 1) mb.Ico(new Vector3(0, headY + 0.02f * s, 0.045f * s), 0.02f * s, Vector3.one, cap, 0.05f);
            }
        }

        void Start()
        {
            rng = new System.Random(seed);
            PickTarget();
            wait = (float)rng.NextDouble() * 3f;
        }

        void PickTarget()
        {
            for (int i = 0; i < 20; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = (float)rng.NextDouble() * range;
                var p = home + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (Mathf.Abs(p.x) > StyleBox.HX - 0.2f || Mathf.Abs(p.y) > StyleBox.HZ - 0.2f) continue;
                if (isBlocked != null && isBlocked(p)) continue;
                target = p;
                return;
            }
            target = new Vector2(transform.localPosition.x, transform.localPosition.z);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            var pos = new Vector2(transform.localPosition.x, transform.localPosition.z);
            if (wait > 0) { wait -= Time.deltaTime; SetPos(pos, 0); return; }
            var to = target - pos;
            if (to.magnitude < 0.03f) { wait = 1f + (float)rng.NextDouble() * 4f; PickTarget(); return; }
            var step = to.normalized * Mathf.Min(to.magnitude, speed * Time.deltaTime);
            var next = pos + step;
            if (isBlocked != null && isBlocked(next)) { PickTarget(); return; }
            var look = Quaternion.LookRotation(new Vector3(step.x, 0, step.y));
            transform.localRotation = Quaternion.Slerp(transform.localRotation, look, Time.deltaTime * 8f);
            SetPos(next, Mathf.Abs(Mathf.Sin(Time.time * 14f + seed)) * 0.012f);
        }

        void SetPos(Vector2 p, float bob) => transform.localPosition = new Vector3(p.x, StyleBox.Height(p.x, p.y) + bob, p.y);
    }
}
