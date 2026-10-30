using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    public enum PropKind { None, Axe, Basket, Sheaf, Log, Lantern, Hoe, Bundle, Fish, Pot, Spear }

    /// <summary>Where a prop rides (life-in-the-box §9): in the right hand, on the head, on the back or on the shoulder.</summary>
    public enum PropSlot { Hand, Head, Back, Shoulder }

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
            if (camp) look.tunic = Color.Lerp(look.tunic, Palette.Hide, 0.55f);
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
            if (hasAccent && !look.camp)
            {
                // A sash over one shoulder: a thin slanted band round the tunic.
                mb.Box(new Vector3(0, bodyH * 0.62f, 0), new Vector3(0.1f * s, 0.016f * s, 0.098f * s), look.accent, Quaternion.Euler(0, 0, 32f));
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
                if (hasAccent && look.camp)
                {
                    // Headband: a bright ring round the brow, wide enough to read as a dot of colour at full zoom.
                    mb.Prism(new Vector3(0, headY - 0.002f * s, 0.004f * s), 0.0505f * s, 0.049f * s, 0.022f * s, 9, look.accent, capTop: false);
                }
            }
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
                    mb.Box(new Vector3(0, 0.03f, 0), new Vector3(0.012f, 0.13f, 0.012f), Palette.WoodLight);
                    mb.Box(new Vector3(0, 0.085f, -0.022f), new Vector3(0.01f, 0.035f, 0.04f), Palette.Iron);
                    break;
                case PropKind.Hoe:
                    mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.01f, 0.18f, 0.01f), Palette.WoodLight);
                    mb.Box(new Vector3(0, 0.135f, -0.02f), new Vector3(0.03f, 0.01f, 0.035f), Palette.Iron);
                    break;
                case PropKind.Spear:
                    mb.Box(new Vector3(0, 0.06f, 0), new Vector3(0.008f, 0.22f, 0.008f), Palette.WoodLight);
                    mb.Prism(new Vector3(0, 0.17f, 0), 0.012f, 0f, 0.035f, 4, Palette.RockLight);
                    break;
                case PropKind.Basket:
                    mb.Prism(new Vector3(0, -0.01f, 0), 0.032f, 0.042f, 0.04f, 8, Palette.Thatch, topCol: Palette.Ember);
                    break;
                case PropKind.Sheaf:
                    mb.Prism(new Vector3(0, 0f, 0), 0.022f, 0.04f, 0.075f, 6, Palette.Wheat, topCol: Palette.Straw);
                    mb.Prism(new Vector3(0, 0.03f, 0), 0.026f, 0.026f, 0.01f, 6, Palette.Bark, capTop: false);
                    break;
                case PropKind.Log:
                    Pieces.Log(mb, Vector3.zero, 0.02f, 0.14f, 90f, Palette.Bark);
                    break;
                case PropKind.Bundle:
                    mb.Box(Vector3.zero, new Vector3(0.06f, 0.07f, 0.035f), Palette.Hide);
                    mb.Box(new Vector3(0, 0.0f, 0), new Vector3(0.062f, 0.01f, 0.037f), Palette.Timber);
                    break;
                case PropKind.Pot:
                    mb.Prism(new Vector3(0, 0, 0), 0.025f, 0.034f, 0.025f, 8, Palette.Terracotta, capTop: false);
                    mb.Prism(new Vector3(0, 0.025f, 0), 0.034f, 0.02f, 0.02f, 8, Palette.Terracotta, topCol: Palette.Door);
                    break;
                case PropKind.Fish:
                    mb.Box(new Vector3(0, 0.035f, 0), new Vector3(0.006f, 0.1f, 0.006f), Palette.WoodLight);
                    mb.Ico(new Vector3(0, 0.0f, -0.012f), 0.016f, new Vector3(0.5f, 1.8f, 0.9f), Palette.Water, 0.05f);
                    break;
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
                case PropSlot.Back: return (new Vector3(0, BodyH * 0.65f, 0.05f), Quaternion.identity);
                case PropSlot.Shoulder: return (new Vector3(ShoulderX * 0.6f, BodyH + 0.008f, 0), Quaternion.Euler(0, 90f, 0));
                default: return (new Vector3(0, -ArmLen, 0), Quaternion.identity); // relative to the right arm's pivot
            }
        }
    }
}
