using AquariumCiv.Sim.Eco;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #282: one rabbit doll in the peg-doll style (low poly, flat colours, no outline). The body, head, ears, eyes, hind feet
    /// and tail are separate parts, so the rabbit can live on its own between the herd's moves: it blinks, twitches an ear,
    /// breathes, nibbles in bouts and looks up to chew, wiggles its nose when it sits up, squashes and stretches through a hop,
    /// and bolts with its ears flat and its white tail up.
    /// The doll faces local +z; the owner sets position and calls <see cref="Face"/> for the heading (it turns, it doesn't snap);
    /// <see cref="Show"/> sets the pose (graze, sit up, hop, flee). Meshes are shared per fur variant.
    /// </summary>
    public sealed class RabbitDoll : MonoBehaviour
    {
        public const int Variants = 3;
        /// <summary>Bigger than life next to the peg dolls (~0.21 tall) so it reads on a phone, but small beside a person:
        /// the back about a third of a person's height, the ear tips under the shoulder. 2.2 (the first pass) stood taller than the people.</summary>
        public const float Scale = 1.2f;

        static readonly Color[] Fur = { new Color(0.62f, 0.45f, 0.31f), new Color(0.58f, 0.55f, 0.51f), new Color(0.82f, 0.68f, 0.50f) };
        static readonly Color Dark = new Color(0.10f, 0.07f, 0.06f), Pink = new Color(0.93f, 0.60f, 0.62f), White = new Color(0.98f, 0.97f, 0.94f);
        /// <summary>The body turns about the haunches (sitting up), not the middle.</summary>
        static readonly Vector3 Hip = new Vector3(0f, 0.008f, -0.028f);
        static readonly Mesh[] bodies = new Mesh[Variants], heads = new Mesh[Variants], ears = new Mesh[Variants], feet = new Mesh[Variants];
        static Mesh shade, tail, eyeL, eyeR;

        public int Variant { get; private set; }
        Transform body, head, earL, earR, eyeLt, eyeRt, feetT, tailT;
        float seed;
        bool faced;
        float yaw, lastTime = float.NaN;
        // the eased pose
        float bodyPitch, headPitch, headYaw, earBack, tailUp, feetBack;

        public static RabbitDoll Create(BoxView box, Transform parent, int variant)
        {
            variant = ((variant % Variants) + Variants) % Variants;
            BuildMeshes(variant);
            var go = new GameObject("Rabbit") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<RabbitDoll>();
            d.Variant = variant;
            d.seed = variant * 1.7f + (go.GetInstanceID() & 63) * 0.37f;
            d.body = Part(go.transform, "Body", bodies[variant], box, Hip);
            d.feetT = Part(d.body, "Feet", feet[variant], box, new Vector3(0f, 0.004f, -0.012f) - Hip);
            d.tailT = Part(d.body, "Tail", tail, box, new Vector3(0f, 0.036f, -0.044f) - Hip);
            d.head = Part(d.body, "Head", heads[variant], box, new Vector3(0f, 0.046f, 0.024f) - Hip);
            d.earL = Part(d.head, "EarL", ears[variant], box, new Vector3(-0.0078f, 0.026f, 0.004f));
            d.earR = Part(d.head, "EarR", ears[variant], box, new Vector3(0.0078f, 0.026f, 0.004f));
            d.eyeLt = Part(d.head, "EyeL", eyeL, box, new Vector3(-0.0165f, 0.012f, 0.022f));
            d.eyeRt = Part(d.head, "EyeR", eyeR, box, new Vector3(0.0165f, 0.012f, 0.022f));
            Part(go.transform, "Shade", shade, box, new Vector3(0f, 0.002f, 0.004f));
            go.transform.localScale = Vector3.one * Scale;
            return d;
        }

        void OnEnable() { faced = false; lastTime = float.NaN; } // a pooled doll coming back: no turn or ease from where it was

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

        // ---------------------------------------------------------------- meshes
        static Vector3[] sphV; static int[] sphF;

        /// <summary>A once-subdivided icosahedron (80 faces): still faceted, but round enough for a soft animal.</summary>
        static void Sphere()
        {
            if (sphV != null) return;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new System.Collections.Generic.List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            int[] f0 =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var mid = new System.Collections.Generic.Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(k, out int m)) return m;
                v.Add(((v[a] + v[b]) * 0.5f).normalized); mid[k] = v.Count - 1; return v.Count - 1;
            }
            var f = new System.Collections.Generic.List<int>();
            for (int i = 0; i < f0.Length; i += 3)
            {
                int a = f0[i], b = f0[i + 1], c = f0[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                f.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            sphV = v.ToArray(); sphF = f.ToArray();
        }

        static void Ball(MeshBuilder mb, Vector3 c, float r, Vector3 s, Color col, Color? under = null, float rough = 0.025f)
        {
            Sphere();
            var p = new Vector3[sphV.Length];
            for (int i = 0; i < p.Length; i++) p[i] = c + Vector3.Scale(sphV[i] * r * (1f + mb.Range(-rough, rough)), s);
            for (int i = 0; i < sphF.Length; i += 3)
            {
                Vector3 a = p[sphF[i]], b = p[sphF[i + 1]], d = p[sphF[i + 2]];
                bool low = under.HasValue && (a.y + b.y + d.y) / 3f < c.y - r * s.y * 0.3f;
                mb.Tri(a, b, d, low ? under.Value : col);
            }
        }

        static void BuildMeshes(int v)
        {
            if (shade == null)
            {
                var sb = new MeshBuilder(5) { jitter = 0f };
                Ball(sb, Vector3.zero, 0.026f, new Vector3(1f, 0.06f, 1.45f), new Color(0.24f, 0.34f, 0.12f), null, 0f); // a soft contact shade, a little darker than the grass
                shade = sb.ToMesh("RabbitShade");
                var tb = new MeshBuilder(6) { jitter = 0.03f };
                Ball(tb, Vector3.zero, 0.011f, new Vector3(1f, 1f, 0.9f), White, null, 0.08f); // a puff, a little ragged
                tail = tb.ToMesh("RabbitTail");
                eyeL = Eye(-1f); eyeR = Eye(1f);
            }
            if (bodies[v] != null) return;
            var fur = Fur[v];
            var belly = Color.Lerp(fur, White, 0.6f);
            var cheek = Color.Lerp(fur, White, 0.3f);

            var mb = new MeshBuilder(30 + v) { jitter = 0.03f, M = Matrix4x4.Translate(-Hip) };
            Ball(mb, new Vector3(0f, 0.030f, -0.008f), 0.030f, new Vector3(0.92f, 0.86f, 1.15f), fur, belly);   // the loaf
            foreach (float s in new[] { -1f, 1f })
            {
                Ball(mb, new Vector3(s * 0.016f, 0.022f, -0.019f), 0.017f, new Vector3(0.8f, 1f, 1.15f), fur, belly); // a haunch
                Ball(mb, new Vector3(s * 0.009f, 0.006f, 0.034f), 0.0062f, new Vector3(0.8f, 0.6f, 1.4f), belly);    // a forepaw
            }
            Ball(mb, new Vector3(0f, 0.029f, 0.019f), 0.022f, new Vector3(0.9f, 1f, 1f), fur, belly);            // the chest
            mb.M = Matrix4x4.identity;
            bodies[v] = mb.ToMesh("RabbitBody" + v);

            var fb = new MeshBuilder(35 + v) { jitter = 0.03f };
            foreach (float s in new[] { -1f, 1f })
                Ball(fb, new Vector3(s * 0.016f, 0f, 0.008f), 0.0085f, new Vector3(0.75f, 0.45f, 2f), belly); // the long hind feet
            feet[v] = fb.ToMesh("RabbitFeet" + v);

            var hb = new MeshBuilder(40 + v) { jitter = 0.03f };
            Ball(hb, new Vector3(0f, 0.008f, 0.012f), 0.024f, new Vector3(0.95f, 0.92f, 1.08f), fur, cheek);     // the head
            foreach (float s in new[] { -1f, 1f })
                Ball(hb, new Vector3(s * 0.0105f, 0.0005f, 0.024f), 0.011f, new Vector3(1f, 0.85f, 1f), cheek); // the cheeks
            Ball(hb, new Vector3(0f, -0.001f, 0.032f), 0.0085f, new Vector3(1.1f, 0.8f, 0.8f), belly);           // the muzzle
            Ball(hb, new Vector3(0f, 0.0045f, 0.0395f), 0.0034f, new Vector3(1.25f, 0.8f, 0.8f), Pink);          // the nose
            heads[v] = hb.ToMesh("RabbitHead" + v);

            var eb = new MeshBuilder(45 + v) { jitter = 0.02f };
            Ball(eb, new Vector3(0f, 0.022f, 0f), 0.011f, new Vector3(0.5f, 2.1f, 0.32f), fur);                        // the ear
            Ball(eb, new Vector3(0f, 0.021f, 0.0028f), 0.009f, new Vector3(0.3f, 1.8f, 0.18f), Pink);                   // pink inside
            Ball(eb, new Vector3(0f, 0.042f, 0f), 0.0056f, new Vector3(0.44f, 1f, 0.34f), Color.Lerp(fur, Dark, 0.45f)); // darker tip
            ears[v] = eb.ToMesh("RabbitEar" + v);
        }

        /// <summary>A dark eye with a white glint up and forward: the glint is what makes it look back at you.</summary>
        static Mesh Eye(float side)
        {
            var b = new MeshBuilder(50) { jitter = 0f };
            Ball(b, Vector3.zero, 0.0058f, new Vector3(0.55f, 1f, 1f), Dark, null, 0f);
            Ball(b, new Vector3(side * 0.0024f, 0.0022f, 0.0024f), 0.0017f, Vector3.one, White, null, 0f);
            return b.ToMesh(side < 0f ? "RabbitEyeL" : "RabbitEyeR");
        }

        // ---------------------------------------------------------------- the pose
        static float Hash(float a, float b)
        {
            float h = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.547f;
            return h - Mathf.Floor(h);
        }

        /// <summary>A bump 0..1..0 when an event recurs about every <paramref name="period"/> seconds (with probability <paramref name="chance"/>),
        /// lasting <paramref name="len"/> seconds, at a random place in each period.</summary>
        float Event(float t, float period, float len, float chance, float salt)
        {
            float k = Mathf.Floor(t / period), u = t - k * period;
            if (Hash(k + salt, seed) > chance) return 0f;
            float at = Hash(k + salt * 3.1f, seed + 9f) * (period - len);
            float w = (u - at) / len;
            return w <= 0f || w >= 1f ? 0f : Mathf.Sin(w * Mathf.PI);
        }

        static float Ease(float cur, float to, float rate, float dt) => Mathf.Lerp(cur, to, 1f - Mathf.Exp(-rate * dt));

        /// <summary>Turn toward a heading (degrees). The first call after the doll appears snaps; after that it turns at a rabbit's speed
        /// (quicker when it bolts), so a new direction reads as the rabbit deciding to go there.</summary>
        public void Face(float yawDeg, float dt, bool quick = false)
        {
            if (!faced) { yaw = yawDeg; faced = true; }
            else yaw = Mathf.MoveTowardsAngle(yaw, yawDeg, (quick ? 900f : 420f) * dt);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Pose the doll: the act and where it is in the hop or stride (0..1), <paramref name="time"/> in seconds (the life clock).</summary>
        public void Show(RabbitAct act, float phase, float time, float grow = 1f)
        {
            bool snap = float.IsNaN(lastTime);
            float dt = snap ? 1f : Mathf.Clamp(time - lastTime, 0f, 0.1f);
            lastTime = time;
            float t = time + seed * 10f;

            float bp, hp, hy = 0f, eb, tu = 0f, fb = 0f, rate;
            float sy = 1f, sz = 1f, chew = 0f;
            float twitchL = Event(t, 2.6f, 0.32f, 0.55f, 1f), twitchR = Event(t + 0.9f, 3.1f, 0.32f, 0.5f, 2f);
            switch (act)
            {
                case RabbitAct.Graze:
                {   // bouts: head down nibbling, then up to chew and look about
                    float cyc = 4.5f + Hash(seed, 3f) * 2f, u = Mathf.Repeat(t, cyc) / cyc;
                    bool down = u < 0.62f;
                    bp = down ? 10f : 2f;
                    hp = down ? 40f : 4f;
                    hy = down ? 0f : Mathf.Sin(t * 1.3f) * 30f * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0.62f, 1f, u));
                    chew = down ? Mathf.Max(0f, Mathf.Sin(t * 11f)) * 5f : Mathf.Sin(t * 14f) * 1.5f;
                    eb = 12f;
                    sy = 1f + Mathf.Sin(t * 5.5f) * 0.018f; // breathing
                    rate = 8f;
                    break;
                }
                case RabbitAct.SitUp:
                    bp = -48f; hp = 40f + Mathf.Sin(t * 1.4f) * 4f;
                    hy = Mathf.Sin(t * 0.8f) * 35f;
                    chew = Mathf.Sin(t * 26f) * 1.2f * (Mathf.Sin(t * 0.9f) > 0f ? 1f : 0.2f); // the nose going
                    eb = -4f; // ears up, listening
                    sy = 1f + Mathf.Sin(t * 6f) * 0.02f;
                    rate = 9f;
                    break;
                case RabbitAct.Hop:
                {
                    float s = Mathf.Sin(Mathf.PI * phase);
                    bp = phase < 0.5f ? Mathf.Lerp(-16f, 0f, phase * 2f) : Mathf.Lerp(0f, 16f, (phase - 0.5f) * 2f); // nose up off, nose down in
                    hp = -bp * 0.6f;
                    eb = 12f + 24f * s;
                    tu = 0.4f * s;
                    fb = 55f * s;
                    sz = 1f + 0.16f * s; sy = 1f - 0.08f * s;
                    float land = phase < 0.1f ? 1f - phase / 0.1f : phase > 0.9f ? (phase - 0.9f) / 0.1f : 0f;
                    sy *= 1f - 0.12f * land; sz *= 1f + 0.05f * land; // squash at takeoff and landing
                    rate = 30f;
                    break;
                }
                default: // flee: long bounds, ears flat, the white tail up
                {
                    float s = Mathf.Sin(Mathf.PI * phase);
                    bp = Mathf.Lerp(-12f, 14f, phase);
                    hp = -6f;
                    eb = 62f;
                    tu = 1f;
                    fb = 70f * s;
                    sz = 1f + 0.26f * s; sy = 1f - 0.12f * s;
                    rate = 30f;
                    break;
                }
            }
            if (snap) { bodyPitch = bp; headPitch = hp; headYaw = hy; earBack = eb; tailUp = tu; feetBack = fb; }
            bodyPitch = Ease(bodyPitch, bp, rate, dt);
            headPitch = Ease(headPitch, hp, rate, dt);
            headYaw = Ease(headYaw, hy, 6f, dt);
            earBack = Ease(earBack, eb, act == RabbitAct.Flee ? 25f : 12f, dt);
            tailUp = Ease(tailUp, tu, 18f, dt);
            feetBack = Ease(feetBack, fb, 35f, dt);

            body.localRotation = Quaternion.Euler(bodyPitch, 0f, 0f);
            body.localScale = new Vector3(1f, sy, sz);
            head.localRotation = Quaternion.Euler(headPitch + chew, headYaw, 0f);
            earL.localRotation = Quaternion.Euler(-earBack - twitchL * 18f, -6f, 12f + twitchL * 22f);
            earR.localRotation = Quaternion.Euler(-earBack - twitchR * 18f, 6f, -12f - twitchR * 22f);
            float blink = Event(t, 3.4f, 0.14f, 0.9f, 5f);
            var eye = new Vector3(1f, 1f - 0.9f * blink, 1f);
            eyeLt.localScale = eye; eyeRt.localScale = eye;
            float kick = feetBack / 70f; // the hind feet kick out behind and down, toes pointing
            feetT.localPosition = new Vector3(0f, 0.004f - 0.008f * kick, -0.012f - 0.022f * kick) - Hip;
            feetT.localRotation = Quaternion.Euler(feetBack * 0.5f, 0f, 0f);
            float flick = Event(t, 3.7f, 0.25f, 0.4f, 7f);
            tailT.localPosition = new Vector3(0f, 0.036f + 0.008f * tailUp, -0.044f - 0.003f * tailUp) - Hip;
            tailT.localScale = Vector3.one * (1f + 0.3f * tailUp + 0.15f * flick);
            transform.localScale = Vector3.one * (Scale * Mathf.Max(0.001f, grow));
        }

        /// <summary>The yaw (degrees) that faces a heading of (dx, dz).</summary>
        public static float YawOf(float dx, float dz) => Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
    }
}
