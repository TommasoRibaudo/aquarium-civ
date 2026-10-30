using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    public enum PropKind { None, Axe, Basket, Sheaf, Log, Lantern, Hoe, Bundle, Fish, Pot, Spear, Rod, Hammer, Broom, Sack, Stick, Bucket, Block, Barrel }

    /// <summary>Where a prop rides (life-in-the-box §9): in the right hand, on the head, on the back or on the shoulder,
    /// hugged in both arms, or rolled along the ground in front (a barrel).</summary>
    public enum PropSlot { Hand, Head, Back, Shoulder, Arms, Front }

    /// <summary>
    /// The peg-doll villager's meshes (style kit, life-in-the-box §9): body + head as one mesh (tapered tunic, belt,
    /// round head, hair or a straw hat, and an optional bright accent: a headband for camp people, a sash for a role),
    /// one stub-arm mesh used for both arms, and code-built props at 1.5–2× real size so they read at full-box zoom.
    /// No faces, no rigs: every move is a transform of these parts. Adults are ~0.22 u tall; kids are 0.68×.
    /// </summary>
    public static class PegDoll
    {
        public const float KidScale = 0.68f;

        /// <summary>The proportions every part hangs from (adult; multiply by the doll's scale).</summary>
        public const float BodyH = 0.13f, ShoulderX = 0.046f, ArmLen = 0.075f, HeadR = 0.043f;
        public static float HeadY => BodyH + 0.04f;

        public struct Look
        {
            public Color tunic, skin, hair;
            public int hairStyle;
            public bool camp;
            /// <summary>Alpha 0 = no accent.</summary>
            public Color accent;
            /// <summary>A role holder (notable-roles §6): a wide sash and a headband in the role's colour, in every era, so
            /// the chief or the miller can be spotted at the default zoom.</summary>
            public bool role;
        }

        /// <summary>Picks a doll's colours from a seed (stable across rebuilds). Camp people wear hide tones and always
        /// get a bright headband (spike polish note); others get one only when <paramref name="accent"/> ≥ 0.</summary>
        public static Look PickLook(int seed, bool camp, bool kid, int accent)
        {
            var rng = new System.Random(seed * 7919 + 13);
            T Pick<T>(T[] a) => a[rng.Next(a.Length)];
            var look = new Look
            {
                tunic = Pick(Palette.Tunics),
                skin = Pick(Palette.Skins),
                hair = Pick(Palette.Hair),
                hairStyle = rng.Next(4),
                camp = camp,
            };
            if ((kid || camp) && look.hairStyle == 3) look.hairStyle = 0; // straw hats are for village fieldwork; a hat would hide the camp headband
            if (camp) look.tunic = Color.Lerp(look.tunic, Palette.Hide, 0.4f);
            int a = accent >= 0 ? accent : camp ? rng.Next(Palette.Accents.Length) : -1;
            look.accent = a >= 0 ? Palette.Accents[a % Palette.Accents.Length] : Color.clear;
            return look;
        }

        /// <summary>Body + head (+ hair/hat + accent) at scale <paramref name="s"/>, feet at the origin, facing local -z.</summary>
        public static void Body(MeshBuilder mb, Look look, float s)
        {
            var tunic = look.tunic;
            float bodyH = BodyH * s;
            mb.Prism(Vector3.zero, 0.055f * s, 0.038f * s, bodyH, 8, tunic, topCol: tunic);
            mb.Prism(new Vector3(0, bodyH * 0.42f, 0), 0.049f * s, 0.046f * s, 0.012f * s, 8, Color.Lerp(tunic, Palette.Door, 0.6f)); // belt
            bool hasAccent = look.accent.a > 0.5f;
            if (hasAccent && (!look.camp || look.role))
            {
                // A sash over one shoulder: a band tilted 28 degrees that hugs the tunic (nothing sticks out > 0.004 u). A role
                // holder's is twice as wide, the one thing on them that reads as "someone" at full-box zoom.
                Band(mb, s, BodyH * 0.62f * s, 0.046f * s * Mathf.Sin(28f * Mathf.Deg2Rad), (look.role ? 0.014f : 0.007f) * s, 0f, Mathf.PI * 2f, 10, look.accent, 0.003f);
            }
            float headY = HeadY * s;
            mb.Ico(new Vector3(0, headY, 0), HeadR * s, Vector3.one, look.skin, 0.06f);
            if (look.hairStyle == 3)
            {
                mb.Prism(new Vector3(0, headY + 0.022f * s, 0), 0.075f * s, 0.075f * s, 0.008f * s, 10, Palette.Straw);
                mb.Prism(new Vector3(0, headY + 0.03f * s, 0), 0.036f * s, 0.028f * s, 0.03f * s, 8, Palette.Straw);
                if (hasAccent) mb.Prism(new Vector3(0, headY + 0.03f * s, 0), 0.037f * s, 0.035f * s, 0.01f * s, 8, look.accent, capTop: false);
            }
            else
            {
                var cap = look.hairStyle == 2 && look.camp ? Palette.Hide : look.hair;
                mb.Ico(new Vector3(0, headY + 0.012f * s, 0.008f * s), 0.047f * s, new Vector3(1f, 0.8f, 1f), cap, 0.05f);
                if (look.hairStyle == 1) mb.Ico(new Vector3(0, headY + 0.02f * s, 0.045f * s), 0.02f * s, Vector3.one, cap, 0.05f);
                if (hasAccent && look.role && !look.camp)
                {
                    // The role's headband (camp people already wear one, below).
                    mb.Prism(new Vector3(0, headY - 0.002f * s, 0.004f * s), 0.0505f * s, 0.049f * s, 0.022f * s, 9, look.accent, capTop: false);
                }
                if (hasAccent && look.camp)
                {
                    // A collar band in the same colour: with the headband it gives the accent enough pixels to show as a
                    // dot of colour at full-box zoom, even on the trodden dirt round the fire (tester #31).
                    mb.Prism(new Vector3(0, BodyH * 0.8f * s, 0), 0.0445f * s, 0.040f * s, 0.024f * s, 8, look.accent);
                    // Headband: a bright ring round the brow, wide enough to read as a dot of colour at full zoom.
                    mb.Prism(new Vector3(0, headY - 0.002f * s, 0.004f * s), 0.0505f * s, 0.049f * s, 0.022f * s, 9, look.accent, capTop: false);
                }
            }
        }

        /// <summary>The tunic's (circumscribed) radius at height <paramref name="y"/> (already scaled by the doll's scale).</summary>
        public static float TunicRadius(float y, float s) => (0.055f - 0.017f * Mathf.Clamp01(y / (BodyH * s))) * s;

        /// <summary>
        /// A thin band that follows the tunic's taper, <paramref name="standoff"/> proud of it: centre height
        /// <paramref name="yc"/> + <paramref name="amp"/>Â·cos(angle) (a tilted ring when it runs all the way round),
        /// <paramref name="halfH"/> half its width, over the angles a0..a1 (x = cos, z = sin; the front is 3Ï/2).
        /// </summary>
        public static void Band(MeshBuilder mb, float s, float yc, float amp, float halfH, float a0, float a1, int segs, Color col, float standoff)
        {
            Vector3 P(float a, float dy)
            {
                float y = yc + amp * Mathf.Cos(a) + dy;
                float r = TunicRadius(y, s) + standoff * s;
                return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < segs; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / (float)segs), t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)segs);
                mb.Quad(P(t0, -halfH), P(t0, halfH), P(t1, halfH), P(t1, -halfH), col);
            }
        }

        /// <summary>The thin strap across the chest of someone carrying a back bundle (a body part, so it hugs the tunic at any scale).</summary>
        public static void ChestStrap(MeshBuilder mb, float s)
        {
            Band(mb, s, BodyH * 0.72f * s, -0.032f * s, 0.0045f * s, Mathf.PI * 1.17f, Mathf.PI * 1.83f, 6, Palette.Timber, 0.0025f);
        }

        /// <summary>One stub arm: pivot (shoulder) at the origin, hanging down -y, sleeve in the tunic colour, skin hand.</summary>
        public static void Arm(MeshBuilder mb, Look look, float s)
        {
            mb.Prism(Vector3.zero, 0.014f * s, 0.012f * s, ArmLen * s, 5, look.tunic, Quaternion.Euler(0, 0, 180f), true, true, look.skin);
        }

        // ---------------- props ----------------
        static readonly Dictionary<PropKind, Mesh> propCache = new();

        /// <summary>A shared prop mesh (palette colours only), built once. The grip is at the origin.</summary>
        public static Mesh PropMesh(PropKind kind)
        {
            if (kind == PropKind.None) return null;
            if (propCache.TryGetValue(kind, out var m) && m != null) return m;
            var mb = new MeshBuilder(31 + (int)kind) { jitter = 0.02f };
            BuildProp(mb, kind);
            m = mb.ToMesh("Prop_" + kind);
            m.hideFlags = HideFlags.DontSave;
            propCache[kind] = m;
            return m;
        }

        /// <summary>Prop geometry at 1.5–2× real size (life-in-the-box §5), grip at the origin, pointing local +y / -z.</summary>
        public static void BuildProp(MeshBuilder mb, PropKind kind)
        {
            switch (kind)
            {
                case PropKind.Axe:
                {
                    // Handle 0.2 × 0.018; head 0.06 tall × 0.07 long: an Iron poll round the handle and a pale steel wedge
                    // whose edge points forward (-z), so the head reads from the side as a bright triangle.
                    mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.018f, 0.2f, 0.018f), Palette.WoodLight);
                    const float yc = 0.115f, z0 = -0.014f, z1 = -0.056f;
                    mb.Box(new Vector3(0, yc, 0), new Vector3(0.026f, 0.06f, 0.028f), Palette.Iron);
                    Vector3 A = new(-0.012f, yc + 0.03f, z0), B = new(0.012f, yc + 0.03f, z0), C = new(0.012f, yc - 0.03f, z0), D = new(-0.012f, yc - 0.03f, z0);
                    Vector3 E = new(0, yc + 0.036f, z1), F = new(0, yc - 0.036f, z1), inside = new(0, yc, (z0 + z1) * 0.5f);
                    mb.QuadOut(B, E, F, C, inside, Palette.RockLight);
                    mb.QuadOut(A, D, F, E, inside, Palette.RockLight);
                    mb.TriOut(A, B, E, inside, Palette.RockLight);
                    mb.TriOut(D, C, F, inside, Palette.RockLight);
                    break;
                }
                case PropKind.Hoe:
                {
                    // Handle 0.24 × 0.016; blade 0.055 wide × 0.05 deep, angled 70 degrees to the handle (down and forward),
                    // an Iron neck and a pale steel edge.
                    mb.Box(new Vector3(0, 0.07f, 0), new Vector3(0.016f, 0.24f, 0.016f), Palette.WoodLight);
                    var top = new Vector3(0, 0.185f, 0);
                    var d = new Vector3(0, -Mathf.Cos(70f * Mathf.Deg2Rad), -Mathf.Sin(70f * Mathf.Deg2Rad));
                    var q = Quaternion.LookRotation(d, Vector3.up);
                    mb.Box(top + d * 0.015f, new Vector3(0.034f, 0.008f, 0.03f), Palette.Iron, q, true);
                    mb.Box(top + d * 0.041f, new Vector3(0.055f, 0.008f, 0.022f), Palette.RockLight, q, true);
                    break;
                }
                case PropKind.Spear:
                    mb.Box(new Vector3(0, 0.06f, 0), new Vector3(0.008f, 0.22f, 0.008f), Palette.WoodLight);
                    mb.Prism(new Vector3(0, 0.17f, 0), 0.012f, 0f, 0.035f, 4, Palette.RockLight);
                    break;
                case PropKind.Basket:
                    mb.Prism(new Vector3(0, -0.01f, 0), 0.032f, 0.042f, 0.04f, 8, Palette.Thatch, topCol: Palette.Ember);
                    break;
                case PropKind.Sheaf:
                {
                    // Lies crosswise on the head: two cones (0.075 each, 0.15 in all) joined at a tied waist, tilted 8 degrees.
                    // Stalk end in Straw, ear end in darker Wheat (#B8913F); the Bark tie band is 0.012 wide.
                    var ear = new Color(0.722f, 0.569f, 0.247f);
                    var ax = Quaternion.Euler(0, 0, 8f);
                    var lift = new Vector3(0, 0.02f, 0);
                    var toEar = ax * Quaternion.Euler(0, 0, -90f);   // local up -> +x (the ear end)
                    var toStalk = ax * Quaternion.Euler(0, 0, 90f);  // local up -> -x (the stalk end)
                    mb.Prism(lift, 0.012f, 0.03f, 0.075f, 6, ear, toEar, true, false, ear);
                    mb.Prism(lift, 0.012f, 0.025f, 0.075f, 6, Palette.Straw, toStalk, true, false, Palette.Straw);
                    mb.Prism(lift - (ax * Vector3.right) * 0.006f, 0.016f, 0.016f, 0.012f, 6, Palette.Bark, toEar, false);
                    break;
                }
                case PropKind.Log:
                    Pieces.Log(mb, Vector3.zero, 0.02f, 0.14f, 90f, Palette.Bark);
                    break;
                case PropKind.Bundle:
                    // A canvas roll (0.075 × 0.085 × 0.045) with two dark straps; a lighter colour than the tunics so it shows.
                    mb.Box(Vector3.zero, new Vector3(0.075f, 0.085f, 0.045f), Palette.Canvas);
                    mb.Box(new Vector3(0, 0.022f, 0), new Vector3(0.079f, 0.009f, 0.049f), Palette.Timber);
                    mb.Box(new Vector3(0, -0.022f, 0), new Vector3(0.079f, 0.009f, 0.049f), Palette.Timber);
                    break;
                case PropKind.Pot:
                {
                    // 0.075 tall: a straw head-ring (r 0.03, h 0.008), a narrow foot, a wide belly (r 0.04) with a plaster
                    // stripe, a narrow neck and a flared lip.
                    var clay = Palette.Terracotta;
                    mb.Prism(new Vector3(0, -0.004f, 0), 0.03f, 0.03f, 0.008f, 8, Palette.Straw);
                    mb.Prism(new Vector3(0, 0.003f, 0), 0.017f, 0.04f, 0.026f, 8, clay, capTop: false);
                    mb.Prism(new Vector3(0, 0.029f, 0), 0.04f, 0.019f, 0.027f, 8, clay, capTop: false);
                    mb.Prism(new Vector3(0, 0.022f, 0), 0.0353f, 0.0415f, 0.007f, 8, Palette.Plaster, capTop: false);
                    mb.Prism(new Vector3(0, 0.029f, 0), 0.0415f, 0.0361f, 0.007f, 8, Palette.Plaster, capTop: false);
                    mb.Prism(new Vector3(0, 0.054f, 0), 0.019f, 0.0165f, 0.012f, 8, Color.Lerp(clay, Palette.Door, 0.25f), capTop: false);
                    mb.Prism(new Vector3(0, 0.066f, 0), 0.0165f, 0.029f, 0.009f, 8, clay, topCol: Palette.Door);
                    break;
                }
                case PropKind.Fish:
                    mb.Box(new Vector3(0, 0.035f, 0), new Vector3(0.006f, 0.1f, 0.006f), Palette.WoodLight);
                    mb.Ico(new Vector3(0, 0.0f, -0.012f), 0.016f, new Vector3(0.5f, 1.8f, 0.9f), Palette.Water, 0.05f);
                    break;
                case PropKind.Rod:
                    // A long pole (0.32) with a dark line hanging from the tip and a pale float: the fisher's outline.
                    mb.Box(new Vector3(0, 0.13f, 0), new Vector3(0.007f, 0.32f, 0.007f), Palette.WoodLight);
                    mb.Box(new Vector3(0, 0.24f, -0.004f), new Vector3(0.002f, 0.1f, 0.002f), Palette.Iron);
                    mb.Ico(new Vector3(0, 0.19f, -0.004f), 0.008f, Vector3.one, Palette.Plaster, 0.05f);
                    break;
                case PropKind.Hammer:
                    // Handle 0.13; an Iron head 0.055 × 0.03 across the top, so the stroke reads as a hammer.
                    mb.Box(new Vector3(0, 0.035f, 0), new Vector3(0.014f, 0.13f, 0.014f), Palette.WoodLight);
                    mb.Box(new Vector3(0, 0.1f, -0.008f), new Vector3(0.03f, 0.03f, 0.058f), Palette.Iron);
                    break;
                case PropKind.Broom:
                    // Handle up from the hand, a straw head down at the feet (the doll's hand hangs ~0.05 above the ground).
                    mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.012f, 0.2f, 0.012f), Palette.WoodLight);
                    mb.Prism(new Vector3(0, -0.075f, 0), 0.03f, 0.012f, 0.03f, 6, Palette.Straw, topCol: Palette.Thatch);
                    break;
                case PropKind.Sack:
                    // A plump canvas sack (0.08 tall) with a tied neck.
                    mb.Ico(new Vector3(0, 0.0f, 0), 0.042f, new Vector3(1f, 1.1f, 0.85f), Color.Lerp(Palette.Canvas, Palette.Hide, 0.35f), 0.08f);
                    mb.Prism(new Vector3(0, 0.04f, 0), 0.012f, 0.016f, 0.016f, 6, Palette.Timber);
                    break;
                case PropKind.Stick:
                    // A plain staff or stirring paddle (0.24), flat at the low end.
                    mb.Box(new Vector3(0, 0.06f, 0), new Vector3(0.011f, 0.24f, 0.011f), Palette.Bark);
                    mb.Box(new Vector3(0, -0.06f, 0), new Vector3(0.026f, 0.04f, 0.008f), Palette.WoodLight);
                    break;
                case PropKind.Bucket:
                    // A wooden bucket (0.05 tall) with an Iron hoop and a handle the hand holds.
                    mb.Prism(new Vector3(0, -0.06f, 0), 0.026f, 0.032f, 0.05f, 8, Palette.WoodLight, topCol: Palette.Water);
                    mb.Prism(new Vector3(0, -0.04f, 0), 0.031f, 0.032f, 0.008f, 8, Palette.Iron, capTop: false);
                    mb.Box(new Vector3(0, -0.005f, 0), new Vector3(0.06f, 0.006f, 0.006f), Palette.Iron);
                    break;
                case PropKind.Block:
                    mb.Box(Vector3.zero, new Vector3(0.075f, 0.05f, 0.055f), Palette.Stone);
                    break;
                case PropKind.Barrel:
                {
                    // Lying on its side (axis x), 0.07 long, radius 0.036, two Iron hoops; the bottom touches the origin.
                    var side = Quaternion.Euler(0, 0, 90f);
                    mb.Prism(new Vector3(0.035f, 0.036f, 0), 0.032f, 0.032f, 0.07f, 8, Palette.Timber, side, true, true, Palette.WoodLight);
                    mb.Prism(new Vector3(0.024f, 0.036f, 0), 0.034f, 0.034f, 0.008f, 8, Palette.Iron, side, false, false);
                    mb.Prism(new Vector3(-0.016f, 0.036f, 0), 0.034f, 0.034f, 0.008f, 8, Palette.Iron, side, false, false);
                    break;
                }
                case PropKind.Lantern:
                    mb.Box(new Vector3(0, 0.0f, 0), new Vector3(0.006f, 0.03f, 0.006f), Palette.Iron);
                    mb.emission = 0.2f; mb.lamp = 0.05f;
                    mb.Box(new Vector3(0, -0.035f, 0), new Vector3(0.032f, 0.04f, 0.032f), Palette.Lamp);
                    mb.emission = 0f; mb.lamp = -1f;
                    mb.Box(new Vector3(0, -0.012f, 0), new Vector3(0.038f, 0.008f, 0.038f), Palette.Iron);
                    break;
            }
        }

        /// <summary>Where a slot sits on an adult doll (scale 1), relative to the feet, and how the prop is turned there.</summary>
        public static (Vector3 pos, Quaternion rot) SlotPose(PropSlot slot)
        {
            switch (slot)
            {
                case PropSlot.Head: return (new Vector3(0, HeadY + HeadR + 0.008f, 0), Quaternion.identity);
                case PropSlot.Back: return (new Vector3(0, BodyH * 0.75f, 0.062f), Quaternion.identity);
                case PropSlot.Shoulder: return (new Vector3(ShoulderX * 0.6f, BodyH + 0.008f, 0), Quaternion.Euler(0, 90f, 0));
                case PropSlot.Arms: return (new Vector3(0, BodyH * 0.6f, -0.058f), Quaternion.identity);
                case PropSlot.Front: return (new Vector3(0, 0f, -0.11f), Quaternion.identity);
                default: return (new Vector3(0, -ArmLen, 0), Quaternion.identity); // relative to the right arm's pivot
            }
        }
    }
}
