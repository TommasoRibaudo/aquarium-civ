using AquariumCiv.Sim.Eco;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #282: one rabbit doll in the peg-doll style (low poly, flat colours, no outline): a fur body with a white tail, a head with
    /// two long ears (dark tips so the silhouette reads at a distance) and a dark shade blob on the ground for contrast on green.
    /// The doll faces local +z; the owner sets position and yaw, <see cref="Show"/> sets the pose (graze, sit up, hop, flee).
    /// Meshes are shared per fur variant.
    /// </summary>
    public sealed class RabbitDoll : MonoBehaviour
    {
        public const int Variants = 3;
        /// <summary>Rabbits are drawn this much bigger than life next to the peg dolls (0.13 tall): they have to read on a phone.</summary>
        public const float Scale = 2.2f;

        static readonly Color[] Fur = { new Color(0.66f, 0.47f, 0.30f), new Color(0.56f, 0.52f, 0.47f), new Color(0.80f, 0.66f, 0.46f) };
        static readonly Color Dark = new Color(0.20f, 0.14f, 0.12f), Pink = new Color(0.92f, 0.58f, 0.58f), White = new Color(0.98f, 0.97f, 0.94f);
        static Mesh[] bodies = new Mesh[Variants], heads = new Mesh[Variants];
        static Mesh shade;

        public int Variant { get; private set; }
        Transform body, head;
        float phase0;

        public static RabbitDoll Create(BoxView box, Transform parent, int variant)
        {
            variant = ((variant % Variants) + Variants) % Variants;
            BuildMeshes(variant);
            var go = new GameObject("Rabbit") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<RabbitDoll>();
            d.Variant = variant;
            d.phase0 = variant * 1.7f + (go.GetInstanceID() & 15) * 0.4f;
            d.body = Part(go.transform, "Body", bodies[variant], box, Vector3.zero);
            d.head = Part(d.body, "Head", heads[variant], box, new Vector3(0f, 0.044f, 0.056f));
            var sh = Part(go.transform, "Shade", shade, box, new Vector3(0f, 0.002f, 0.01f));
            sh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * Scale;
            return d;
        }

        static Transform Part(Transform parent, string name, Mesh m, BoxView box, Vector3 at)
        {
            var g = new GameObject(name) { hideFlags = HideFlags.DontSave };
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        static void BuildMeshes(int v)
        {
            if (shade == null)
            {
                var sb = new MeshBuilder(5) { jitter = 0f };
                sb.Ico(Vector3.zero, 0.04f, new Vector3(0.9f, 0.1f, 1.6f), new Color(0.10f, 0.20f, 0.08f), 0.05f);
                shade = sb.ToMesh("RabbitShade");
            }
            if (bodies[v] != null && heads[v] != null) return;
            var fur = Fur[v];
            var belly = Color.Lerp(fur, White, 0.55f);
            var mb = new MeshBuilder(30 + v) { jitter = 0.02f };
            mb.Ico(new Vector3(0f, 0.030f, -0.012f), 0.031f, new Vector3(0.95f, 0.95f, 1.2f), fur, 0.06f, belly);   // haunch
            mb.Ico(new Vector3(0f, 0.032f, 0.022f), 0.027f, new Vector3(0.85f, 0.9f, 1.3f), fur, 0.06f, belly);     // chest
            mb.Ico(new Vector3(0f, 0.042f, -0.046f), 0.0155f, Vector3.one, White, 0.05f);                           // the white tail
            mb.Ico(new Vector3(0.011f, 0.006f, 0.040f), 0.0075f, new Vector3(0.8f, 0.6f, 1.5f), belly, 0.05f);      // forepaws
            mb.Ico(new Vector3(-0.011f, 0.006f, 0.040f), 0.0075f, new Vector3(0.8f, 0.6f, 1.5f), belly, 0.05f);
            bodies[v] = mb.ToMesh("RabbitBody" + v);

            var hb = new MeshBuilder(40 + v) { jitter = 0.02f };
            hb.Ico(Vector3.zero, 0.0215f, new Vector3(0.85f, 0.9f, 1.2f), fur, 0.05f, belly);                       // head
            hb.Ico(new Vector3(0f, -0.003f, 0.027f), 0.0065f, Vector3.one, Pink, 0.04f);                             // nose
            hb.Ico(new Vector3(0.0125f, 0.007f, 0.012f), 0.0042f, Vector3.one, Dark, 0.03f);                         // eyes
            hb.Ico(new Vector3(-0.0125f, 0.007f, 0.012f), 0.0042f, Vector3.one, Dark, 0.03f);
            foreach (float s in new[] { -1f, 1f })
            {
                hb.M = Matrix4x4.TRS(new Vector3(s * 0.010f, 0.018f, -0.006f), Quaternion.Euler(-10f, 0f, s * -7f), Vector3.one);
                hb.Ico(new Vector3(0f, 0.026f, 0f), 0.0125f, new Vector3(0.42f, 2.5f, 0.55f), fur, 0.04f);             // the ear
                hb.Ico(new Vector3(0f, 0.052f, 0f), 0.0075f, new Vector3(0.45f, 1.3f, 0.55f), Dark, 0.03f);            // dark tip: reads against the sky and the green
                hb.Ico(new Vector3(0f, 0.022f, 0.0045f), 0.007f, new Vector3(0.3f, 2.2f, 0.3f), Pink, 0.03f);          // pink inside
                hb.M = Matrix4x4.identity;
            }
            heads[v] = hb.ToMesh("RabbitHead" + v);
        }

        /// <summary>Pose the doll: the act and where it is in the hop (0..1), <paramref name="time"/> in seconds for the nibble.</summary>
        public void Show(RabbitAct act, float phase, float time, float grow = 1f)
        {
            float bodyPitch, headPitch;
            float t = time + phase0;
            switch (act)
            {
                case RabbitAct.Graze:
                    bodyPitch = 14f; headPitch = 34f + Mathf.Max(0f, Mathf.Sin(t * 4.2f)) * 14f; // head down, nibbling
                    break;
                case RabbitAct.SitUp:
                    bodyPitch = -52f; headPitch = 40f + Mathf.Sin(t * 1.7f) * 4f; // up on the haunches, head level, looking about
                    break;
                case RabbitAct.Hop:
                    bodyPitch = Mathf.Lerp(-24f, 20f, phase); headPitch = Mathf.Lerp(8f, -6f, phase);
                    break;
                default: // flee: stretched out, head low and forward
                    bodyPitch = 12f + Mathf.Sin(t * 20f) * 10f; headPitch = -10f;
                    break;
            }
            body.localRotation = Quaternion.Euler(bodyPitch, 0f, 0f);
            head.localRotation = Quaternion.Euler(headPitch, act == RabbitAct.SitUp ? Mathf.Sin(t * 0.9f) * 25f : 0f, 0f);
            transform.localScale = Vector3.one * (Scale * Mathf.Max(0.001f, grow));
        }

        /// <summary>The yaw (degrees) that faces a heading of (dx, dz).</summary>
        public static float YawOf(float dx, float dz) => Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
    }
}
