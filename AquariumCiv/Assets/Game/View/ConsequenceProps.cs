using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// #371 (visit-loop.md 2.5): the small things a choice left in the box, code-built from the style kit's pieces in the box's palette
    /// (no new art; the real look is the user's call): a feather staked by the chief's door, a woodpile under its own little roof, the
    /// remembering stone's names, a charm pole, a stew pot, a long table, a carved bird on a ridge pole... Box units, standing on their
    /// origin, bigger than life a little (<see cref="Scale"/>) so they read at the default zoom.
    /// </summary>
    public static class ConsequenceMeshes
    {
        /// <summary>How much bigger than life the things stand, so they read at default zoom (as the relics do).</summary>
        public const float Scale = 1.5f;

        /// <summary>The mesh of a <see cref="ConsequenceKind"/> (an unknown kind: a small cairn, so nothing is ever missing).</summary>
        public static Mesh Build(string kind)
        {
            var mb = new MeshBuilder(3710 + System.Array.IndexOf(ConsequenceKind.All, kind)) { jitter = 0.025f };
            switch (kind)
            {
                case ConsequenceKind.Feather: // fb4 #526: a storyteller's stake: a pale stake in a ring of stones, a red cord, three big feathers fanned on top
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f;
                        mb.Ico(new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.05f, 0.022f, new Vector3(1f, 0.7f, 1f), Palette.Rock, 0.2f);
                    }
                    mb.Prism(Vector3.zero, 0.018f, 0.014f, 0.2f, 6, Palette.WoodLight, topCol: Palette.Plaster);
                    mb.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.048f, 0.03f, 0.048f), Palette.Accents[0]);
                    for (int i = -1; i <= 1; i++)
                    {
                        var rot = Quaternion.Euler(0f, 0f, -i * 24f);
                        var col = i == 0 ? Palette.Plaster : i < 0 ? Palette.Accents[1] : Palette.Accents[0];
                        var at = new Vector3(i * 0.04f, 0.31f - Mathf.Abs(i) * 0.015f, 0f);
                        mb.Box(at, new Vector3(0.05f, 0.2f, 0.008f), col, rot);
                        mb.Box(at, new Vector3(0.006f, 0.21f, 0.011f), Palette.Timber, rot);
                    }
                    break;
                case ConsequenceKind.Baskets: // three berry baskets side by side (2026-11-14: the pink domes on flat rims read as odd discs):
                    // a woven basket (body, two darker bands, a rim, a bent handle) heaped with separate small berries, the berry-pick's red
                    for (int i = 0; i < 3; i++)
                    {
                        var at = new Vector3((i - 1) * 0.1f, 0f, i == 1 ? 0.05f : 0f);
                        mb.Prism(at, 0.042f, 0.05f, 0.055f, 8, Palette.Thatch, topCol: Palette.Timber); // the dark inside shows at the top
                        mb.Prism(at + new Vector3(0f, 0.016f, 0f), 0.046f, 0.047f, 0.008f, 8, Palette.WoodLight);
                        mb.Prism(at + new Vector3(0f, 0.034f, 0f), 0.049f, 0.05f, 0.008f, 8, Palette.WoodLight);
                        mb.Prism(at + new Vector3(0f, 0.052f, 0f), 0.053f, 0.053f, 0.008f, 8, Palette.Straw);
                        var berry = i == 1 ? new Color(0.36f, 0.16f, 0.42f) : new Color(0.78f, 0.1f, 0.15f); // the middle one: dark blue-black berries
                        for (int k = 0; k < 9; k++)
                        {
                            float a = k * 2.4f + i, r = k == 0 ? 0f : 0.012f + 0.022f * ((k % 3) / 2f);
                            float y = 0.058f + (k == 0 ? 0.016f : (0.026f - r) * 0.55f);
                            mb.Ico(at + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), 0.011f, Vector3.one, Color.Lerp(berry, Color.black, (k % 4) * 0.07f), 0.1f);
                        }
                        // a bent handle over the berries, across the basket
                        for (int k = 0; k <= 4; k++)
                        {
                            float t = k / 4f * Mathf.PI, hx = Mathf.Cos(t) * 0.044f, hy = 0.06f + Mathf.Sin(t) * 0.05f;
                            mb.Box(at + new Vector3(hx, hy, 0f), new Vector3(0.008f, 0.026f, 0.008f), Palette.WoodLight, Quaternion.Euler(0f, 0f, (t - Mathf.PI * 0.5f) * Mathf.Rad2Deg));
                        }
                    }
                    break;
                case ConsequenceKind.WoodpileRoof: // a tall woodpile under its own little roof
                    Pieces.Woodpile(mb, Vector3.zero, 0f);
                    foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.15f, 0.12f, 0f), new Vector3(0.02f, 0.24f, 0.02f), Palette.Timber);
                    foreach (int s in new[] { -1, 1 })
                        mb.Box(new Vector3(0f, 0.26f, s * 0.05f), new Vector3(0.36f, 0.015f, 0.12f), Palette.Thatch, Quaternion.Euler(s * 28f, 0f, 0f), true);
                    break;
                case ConsequenceKind.NameStone: // a standing stone cut with rows of names
                    mb.Prism(Vector3.zero, 0.11f, 0.08f, 0.1f, 7, Palette.RockDark, topCol: Palette.Rock);
                    mb.Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.13f, 0.22f, 0.07f), Palette.Rock);
                    mb.Box(new Vector3(0f, 0.325f, 0f), new Vector3(0.1f, 0.03f, 0.055f), Palette.RockLight);
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(0f, 0.13f + i * 0.045f, -0.036f), new Vector3(0.08f - (i % 2) * 0.02f, 0.01f, 0.004f), Palette.RockDark);
                    break;
                case ConsequenceKind.CharmPole: // a bough on a post hung with charms
                    mb.Prism(Vector3.zero, 0.02f, 0.014f, 0.36f, 6, Palette.Bark, topCol: Palette.WoodLight);
                    mb.Box(new Vector3(0.06f, 0.32f, 0f), new Vector3(0.14f, 0.014f, 0.014f), Palette.Bark);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -0.0f + i * 0.045f - 0.02f;
                        mb.Box(new Vector3(x + 0.04f, 0.29f, 0f), new Vector3(0.003f, 0.05f, 0.003f), Palette.Plaster);
                        mb.Ico(new Vector3(x + 0.04f, 0.26f, 0f), 0.014f, Vector3.one, Palette.Accents[i % Palette.Accents.Length], 0.05f);
                    }
                    mb.Ico(new Vector3(0f, 0.38f, 0f), 0.07f, new Vector3(1f, 0.7f, 1f), Palette.LeafB, 0.15f);
                    break;
                case ConsequenceKind.StewPot: // a pot on a tripod over a few stones
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        mb.Ico(new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.1f, 0.03f, new Vector3(1f, 0.7f, 1f), Palette.Rock, 0.2f);
                    }
                    for (int i = 0; i < 3; i++)
                        mb.Prism(new Vector3(Mathf.Cos(i * 2.094f), 0f, Mathf.Sin(i * 2.094f)) * 0.1f, 0.008f, 0.006f, 0.2f, 5, Palette.Bark,
                            Quaternion.AngleAxis(14f, new Vector3(-Mathf.Sin(i * 2.094f), 0f, Mathf.Cos(i * 2.094f))));
                    // #492: a pot of stew like the cook's camp pot; #676: the same soot-dark pot and lumpy stew (MealDirector.AddStewPot)
                    MealDirector.AddStewPot(mb, 0.08f, 0.07f, 0.09f, 0.09f, 4920);
                    break;
                case ConsequenceKind.AxeStump: // a stump with an axe stuck in it, a notch in the handle
                    mb.Prism(Vector3.zero, 0.075f, 0.065f, 0.1f, 8, Palette.Bark, topCol: Palette.WoodLight);
                    mb.Box(new Vector3(0.0f, 0.2f, 0f), new Vector3(0.016f, 0.22f, 0.016f), Palette.WoodLight, Quaternion.Euler(0f, 0f, -12f));
                    mb.Box(new Vector3(-0.03f, 0.13f, 0f), new Vector3(0.05f, 0.03f, 0.01f), Palette.Iron, Quaternion.Euler(0f, 0f, -12f));
                    break;
                case ConsequenceKind.Staff: // a headman's staff with a ribbon, on a flat stone
                    mb.Prism(Vector3.zero, 0.06f, 0.05f, 0.02f, 7, Palette.Rock, topCol: Palette.RockLight);
                    mb.Prism(new Vector3(0f, 0.02f, 0f), 0.012f, 0.01f, 0.42f, 6, Palette.WoodLight);
                    mb.Ico(new Vector3(0f, 0.45f, 0f), 0.028f, Vector3.one, Palette.Accents[1], 0.05f);
                    mb.sway = 0.4f;
                    mb.Box(new Vector3(0.03f, 0.38f, 0f), new Vector3(0.06f, 0.05f, 0.003f), Palette.Accents[0]);
                    mb.sway = 0f;
                    break;
                case ConsequenceKind.FlourSacks: // sacks heaped by the mill, one split open
                    for (int i = 0; i < 4; i++)
                    {
                        var at = new Vector3((i % 2 - 0.5f) * 0.1f, (i / 2) * 0.07f, (i / 2) * 0.02f);
                        mb.Ico(at + new Vector3(0f, 0.04f, 0f), 0.055f, new Vector3(1f, 0.75f, 0.8f), Palette.Canvas, 0.1f);
                    }
                    mb.Ico(new Vector3(0.12f, 0.025f, -0.05f), 0.04f, new Vector3(1.2f, 0.4f, 1f), Palette.Plaster, 0.1f);
                    break;
                case ConsequenceKind.RoofBird: // a carved bird on a ridge pole
                    mb.Prism(Vector3.zero, 0.014f, 0.012f, 0.24f, 6, Palette.Timber);
                    mb.Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.2f, 0.02f, 0.02f), Palette.Thatch);
                    mb.Ico(new Vector3(0f, 0.3f, 0f), 0.04f, new Vector3(1.4f, 0.9f, 0.8f), Palette.Accents[2], 0.06f);
                    mb.Ico(new Vector3(-0.05f, 0.335f, 0f), 0.022f, Vector3.one, Palette.Accents[2], 0.05f);
                    mb.Prism(new Vector3(-0.07f, 0.335f, 0f), 0.008f, 0f, 0.03f, 4, Palette.Accents[1], Quaternion.Euler(0f, 0f, 90f));
                    mb.Box(new Vector3(0.06f, 0.31f, 0f), new Vector3(0.05f, 0.012f, 0.03f), Palette.Accents[2]);
                    break;
                case ConsequenceKind.LongTable: // a long table with benches either side
                    mb.Box(new Vector3(0f, 0.07f, 0f), new Vector3(0.44f, 0.014f, 0.1f), Palette.WoodLight);
                    foreach (int s in new[] { -1, 1 })
                    {
                        mb.Box(new Vector3(s * 0.19f, 0.035f, 0f), new Vector3(0.015f, 0.07f, 0.09f), Palette.Timber);
                        mb.Box(new Vector3(0f, 0.04f, s * 0.085f), new Vector3(0.4f, 0.012f, 0.04f), Palette.Timber);
                    }
                    // 2026-11-14: wooden bowls of food (berries, stew, greens), not bright blobs (a pink one read as a gem)
                    var foods = new[] { new Color(0.72f, 0.12f, 0.16f), new Color(0.55f, 0.36f, 0.2f), new Color(0.42f, 0.55f, 0.25f) };
                    for (int i = 0; i < 3; i++)
                    {
                        var at = new Vector3(-0.12f + i * 0.12f, 0.077f, 0f);
                        mb.Prism(at, 0.018f, 0.026f, 0.014f, 8, Palette.Timber, topCol: Palette.WoodLight);
                        for (int k = 0; k < 4; k++)
                        {
                            float a = k * 1.7f + i;
                            mb.Ico(at + new Vector3(Mathf.Cos(a) * 0.009f, 0.017f, Mathf.Sin(a) * 0.009f), 0.009f, Vector3.one, Color.Lerp(foods[i], Color.black, k * 0.06f), 0.1f);
                        }
                    }
                    break;
                case ConsequenceKind.OfferingBasket: // the best basket on a flat stone, a cloth over it
                    mb.Prism(Vector3.zero, 0.09f, 0.075f, 0.04f, 7, Palette.Rock, topCol: Palette.RockLight);
                    mb.Prism(new Vector3(0f, 0.04f, 0f), 0.055f, 0.07f, 0.07f, 8, Palette.Thatch, topCol: Palette.Straw);
                    mb.Ico(new Vector3(0f, 0.12f, 0f), 0.05f, new Vector3(1f, 0.5f, 1f), Palette.Accents[3], 0.08f);
                    break;
                case ConsequenceKind.ShoreBoat: // #714: the pond family's boat, upside down by the fire (the children's house)
                {
                    if (KenneyBoat(mb)) break; // the user's pick: Kenney Watercraft Kit's small rowing boat, recoloured to wood
                    // the code-built stand-in, only if the model is missing:
                    // The Editor pass's close shot read the old squashed lump as a plank: now a planked hull upside down, widest in the middle and
                    // pointed at both ends (a pale gunwale at the ground, darker planks to the keel), its stern propped on a log so the dark
                    // hollow under it shows (the children's house), and its two oars laid beside it.
                    var pivot = new Vector3(-0.16f, 0f, 0f);
                    var tilt = Quaternion.Euler(0f, 0f, 9f);
                    System.Func<Vector3, Vector3> P = v => pivot + tilt * (v - pivot);
                    void Hull(float x, float len, float w, Quaternion? r = null)
                    {
                        var q = tilt * (r ?? Quaternion.identity);
                        mb.Box(P(new Vector3(x, 0.016f, 0f)), new Vector3(len, 0.032f, w), Palette.WoodLight, q);          // the gunwale band
                        mb.Box(P(new Vector3(x, 0.045f, 0f)), new Vector3(len * 0.95f, 0.028f, w * 0.8f), Palette.Timber, q); // the planks
                        mb.Box(P(new Vector3(x, 0.066f, 0f)), new Vector3(len * 0.9f, 0.018f, w * 0.5f), Palette.Bark, q);    // the bottom, turned up
                    }
                    mb.Box(new Vector3(0.06f, 0.004f, 0f), new Vector3(0.2f, 0.008f, 0.1f), new Color(0.16f, 0.11f, 0.07f)); // the dark hollow under it
                    Hull(0f, 0.15f, 0.13f);
                    Hull(-0.1f, 0.07f, 0.1f); Hull(0.1f, 0.07f, 0.1f);
                    Hull(-0.145f, 0.05f, 0.05f, Quaternion.Euler(0f, 45f, 0f)); Hull(0.145f, 0.05f, 0.05f, Quaternion.Euler(0f, 45f, 0f));
                    mb.Box(P(new Vector3(0f, 0.08f, 0f)), new Vector3(0.32f, 0.012f, 0.014f), Palette.Timber, tilt);      // the keel, on top
                    mb.Prism(new Vector3(0.13f, 0f, -0.07f), 0.016f, 0.016f, 0.14f, 6, Palette.Bark, Quaternion.Euler(90f, 0f, 0f), true, true, Palette.WoodLight); // the log under the stern
                    foreach (float z in new[] { 0.1f, 0.125f })                                                         // the oars, laid beside it
                    {
                        var r = Quaternion.Euler(0f, z > 0.11f ? 6f : -4f, 0f);
                        mb.Box(new Vector3(-0.02f, 0.006f, z), new Vector3(0.24f, 0.01f, 0.01f), Palette.WoodLight, r);
                        mb.Box(new Vector3(0.11f, 0.004f, z), new Vector3(0.06f, 0.006f, 0.026f), Palette.WoodLight, r);
                    }
                    break;
                }
                case ConsequenceKind.FishCrate: // a crate with fish on the near shore
                    mb.Box(new Vector3(0f, 0.035f, 0f), new Vector3(0.16f, 0.07f, 0.1f), Palette.WoodLight);
                    for (int i = 0; i < 3; i++)
                        mb.Ico(new Vector3(-0.04f + i * 0.04f, 0.08f, 0f), 0.025f, new Vector3(1.6f, 0.5f, 0.6f), i % 2 == 0 ? Palette.Water : Palette.WaterDeep, 0.05f);
                    break;
                case ConsequenceKind.NamePeg: // a peg with a small wooden tag
                    mb.Prism(Vector3.zero, 0.012f, 0.01f, 0.2f, 5, Palette.Bark);
                    mb.Box(new Vector3(0f, 0.17f, 0f), new Vector3(0.09f, 0.05f, 0.012f), Palette.WoodLight);
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.025f + i * 0.025f, 0.17f, -0.008f), new Vector3(0.012f, 0.025f, 0.003f), Palette.Timber);
                    break;
                default:
                    for (int i = 0; i < 4; i++) mb.Ico(new Vector3(0f, 0.02f + i * 0.04f, 0f) + new Vector3((i % 2) * 0.02f, 0f, 0f), 0.06f - i * 0.01f, new Vector3(1f, 0.7f, 1f), Palette.Rock, 0.2f);
                    break;
            }
            var mesh = mb.ToMesh("Consequence_" + kind);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>#714: the boat's model (Kenney Watercraft Kit 2.1, CC0: boat-row-small), under a Resources folder so a build has it.</summary>
        public const string BoatModel = "KenneyWatercraft/boat-row-small", BoatColormap = "KenneyWatercraft/Textures/colormap";
        /// <summary>#714: the boat's length on the land before <see cref="Scale"/> (0.27 × 1.5 = 0.4 box units: about two people lying down,
        /// a family of three fits in it, and two children crawl under it).</summary>
        public const float BoatLength = 0.27f;
        /// <summary>#714 round 2: the stern's lift on its stone (degrees about the beam).</summary>
        public const float BoatLift = 8f;

        /// <summary>#714: Kenney's small rowing boat, turned upside down, flat and level with its rim on the ground (#714 round 2: no prop,
        /// no roll), its two oars laid beside it; the view stands it apart from the pot (<see cref="ConsequencePropsView"/>'s boat spot). Recoloured to wood in the vertex colours: each corner's colour in Kenney's
        /// colour map is read for its lightness only, then mapped onto the box's timber browns (bark, timber, light wood); the shared texture
        /// is not edited. False when the model can't be read (the code-built stand-in draws instead).</summary>
        static bool KenneyBoat(MeshBuilder mb)
        {
            var go = Resources.Load<GameObject>(BoatModel);
            if (go == null) return false;
            var map = Resources.Load<Texture2D>(BoatColormap);
            var hull = new List<(Mesh mesh, Matrix4x4 m)>();
            var oars = new List<(Mesh mesh, Matrix4x4 m)>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) return false;
                var m = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                (mf.name.ToLowerInvariant().Contains("paddle") ? oars : hull).Add((mf.sharedMesh, m));
            }
            if (hull.Count == 0) return false;
            // the hull's bounds in the model's space: its long side becomes x
            var b = new Bounds();
            bool first = true;
            foreach (var (mesh, m) in hull)
                foreach (var v in mesh.vertices) { var p = m.MultiplyPoint3x4(v); if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
            bool alongZ = b.size.z > b.size.x;
            float k = BoatLength / Mathf.Max(1e-4f, alongZ ? b.size.z : b.size.x);
            var toX = Quaternion.Euler(0f, alongZ ? 90f : 0f, 0f);
            var flip = Quaternion.Euler(180f, 0f, 0f);                       // keel up: rolled over its long axis
            float height = b.size.y * k;
            // centre, turn long side to x, roll over, scale, and sit the gunwale flat and level on the ground (#714 round 2: no tilt,
            // no log, no roll: the user found the propped stern read as skewed, and its open end threw an odd dark blob)
            var place = Matrix4x4.TRS(new Vector3(0f, height * 0.5f - 0.001f, 0f), flip * toX, Vector3.one * k) * Matrix4x4.Translate(-b.center);
            // a small lift at the stern on one stone (about the beam only: no roll), so the gunwale line and the dark inside show and it reads
            // as a boat, not a slab; the bow's rim stays on the ground
            var bow = new Vector3(-BoatLength * 0.5f, 0f, 0f);
            var lift = Matrix4x4.Translate(bow) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, BoatLift)) * Matrix4x4.Translate(-bow);
            var keep = mb.M;
            foreach (var (mesh, m) in hull) AddWood(mb, mesh, m, keep * lift * place, map, 0f);
            float stoneTop = BoatLength * Mathf.Sin(BoatLift * Mathf.Deg2Rad) * 0.9f;
            mb.Ico(new Vector3(BoatLength * 0.42f, stoneTop * 0.45f, 0f), stoneTop * 0.6f, new Vector3(1.3f, 0.85f, 1.1f), Palette.Rock, 0.15f); // the stone under the stern
            // the oars, laid flat on the ground beside it, side by side: the model's paddles are one mesh, an oar out to each side,
            // so each side's half is taken alone, centred, and laid along x (the left one turned round so both blades point one way)
            int i = 0;
            foreach (var (mesh, m) in oars)
                for (int side = -1; side <= 1; side += 2)
                {
                    var ob = new Bounds(); bool f = true;
                    foreach (var v in mesh.vertices) { var p = m.MultiplyPoint3x4(v); if (p.x * side <= 0f) continue; if (f) { ob = new Bounds(p, Vector3.zero); f = false; } else ob.Encapsulate(p); }
                    if (f) continue;
                    var lay = Matrix4x4.TRS(new Vector3(-0.01f + i * 0.03f, ob.size.y * k * 0.5f + 0.002f, BoatLength * 0.34f + i * 0.04f), Quaternion.Euler(0f, (side < 0 ? 180f : 0f) + (i == 0 ? 4f : -6f), 0f), Vector3.one * k)
                              * Matrix4x4.Translate(-ob.center);
                    int s = side;
                    AddWood(mb, mesh, m, keep * lay, map, 0.1f, p => p.x * s > 0f);
                    i++;
                }
            mb.M = keep;
            return true;
        }

        /// <summary>One model mesh into <paramref name="mb"/> in wood colours: lightness from Kenney's colour map at each corner's UV, mapped onto
        /// bark, timber and light wood (<paramref name="lighter"/> moves it up the ramp: the oars are lighter, newer wood). <paramref name="part"/>:
        /// only the triangles whose middle (model space) it keeps.</summary>
        static void AddWood(MeshBuilder mb, Mesh mesh, Matrix4x4 local, Matrix4x4 world, Texture2D map, float lighter, System.Func<Vector3, bool> part = null)
        {
            var vs = mesh.vertices; var uv = mesh.uv; var ts = mesh.triangles;
            var cols = new Color[vs.Length];
            // the height in the model (gunwale light, keel dark, as wooden boats weather), mixed with Kenney's own light and dark
            float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var v in vs) { float y = local.MultiplyPoint3x4(v).y; y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            for (int v = 0; v < vs.Length; v++)
            {
                float lum = 0.6f;
                if (map != null && map.isReadable && uv != null && uv.Length == vs.Length)
                {
                    var c = map.GetPixelBilinear(uv[v].x, uv[v].y);
                    lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                }
                float up = Mathf.InverseLerp(y0, y1, local.MultiplyPoint3x4(vs[v]).y);
                cols[v] = WoodOf(Mathf.Clamp01(0.45f * Mathf.InverseLerp(0.35f, 0.85f, lum) + 0.65f * up - 0.05f + lighter));
            }
            mb.M = world * local;
            for (int t = 0; t + 2 < ts.Length; t += 3)
            {
                if (part != null && !part(local.MultiplyPoint3x4((vs[ts[t]] + vs[ts[t + 1]] + vs[ts[t + 2]]) / 3f))) continue;
                mb.TriC(vs[ts[t]], vs[ts[t + 1]], vs[ts[t + 2]], cols[ts[t]], cols[ts[t + 1]], cols[ts[t + 2]]);
            }
        }

        /// <summary>The box's timber ramp: bark (0), timber (0.5), light wood (1).</summary>
        public static Color WoodOf(float t) => t < 0.5f ? Color.Lerp(Palette.Bark, Palette.Timber, t * 2f) : Color.Lerp(Palette.Timber, Palette.WoodLight, (t - 0.5f) * 2f);

        /// <summary>#635: <paramref name="count"/> charms hanging from a tree's lower crown: each a pale cord and a little coloured charm,
        /// spread round the trunk. Origin = the tree's ground point; box units, not scaled. <paramref name="attachY"/> / <paramref name="ringR"/>:
        /// where the cords tie on (just under the leaves).</summary>
        public static Mesh Charms(int count, float attachY, float ringR)
        {
            var mb = new MeshBuilder(3790 + count) { jitter = 0.01f };
            for (int i = 0; i < count; i++)
            {
                float a = i * 2.39996f + 0.4f;                       // the golden angle: never two on one side
                float r = ringR * (0.8f + 0.2f * ((i * 7) % 3) / 2f);
                float len = 0.055f + 0.025f * (i % 3);
                var top = new Vector3(Mathf.Cos(a) * r, attachY, Mathf.Sin(a) * r);
                var col = Palette.Accents[i % Palette.Accents.Length];
                mb.Box(top - Vector3.up * len * 0.5f, new Vector3(0.006f, len, 0.006f), Palette.Plaster);
                var at = top - Vector3.up * (len + 0.026f);
                mb.Box(at, new Vector3(0.05f, 0.05f, 0.012f), col, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 45f)); // tester: 0.034 was a speck at home zoom
                mb.Ico(at - Vector3.up * 0.04f, 0.015f, Vector3.one, Palette.Plaster, 0.05f);
            }
            var mesh = mb.ToMesh("Consequence_charms_" + count);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>#635: the stand-in pole with <paramref name="count"/> charms on its bough (the sim's count, not always four).</summary>
        /// <summary>#692 (B31.a): the feather pole with its tied feathers: <paramref name="slots"/> are their kinds (PoleFeatherKind) oldest first;
        /// they spiral down from the top, 0.03 u apart (at the props' 1.5 scale: 0.02 here), tilted out 30 deg; white, red or ochre.</summary>
        public static Mesh FeatherPole(int[] slots)
        {
            var mb = new MeshBuilder(3790) { jitter = 0.02f };
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                mb.Ico(new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.05f, 0.022f, new Vector3(1f, 0.7f, 1f), Palette.Rock, 0.2f);
            }
            mb.Prism(Vector3.zero, 0.018f, 0.014f, 0.2f, 6, Palette.WoodLight, topCol: Palette.Plaster);
            mb.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.048f, 0.03f, 0.048f), Palette.Accents[0]);
            for (int i = -1; i <= 1; i++)
            {
                var rot = Quaternion.Euler(0f, 0f, -i * 24f);
                var col = i == 0 ? Palette.Plaster : i < 0 ? Palette.Accents[1] : Palette.Accents[0];
                var at = new Vector3(i * 0.04f, 0.31f - Mathf.Abs(i) * 0.015f, 0f);
                mb.Box(at, new Vector3(0.05f, 0.2f, 0.008f), col, rot);
                mb.Box(at, new Vector3(0.006f, 0.21f, 0.011f), Palette.Timber, rot);
            }
            // the user's review of B31.a ("where's the white feather"): a tied feather is as big as the old fan's (0.04 x 0.17 here), hangs
            // below the fan off the red band, fanned out sideways and up (50 deg), with a dark spine and a dark tip, so a new one shows
            for (int i = 0; i < slots.Length; i++)
            {
                float a = i * 2.39996f + 0.6f, y = 0.125f + (i % 3) * 0.02f;
                var col = slots[i] == PoleFeatherKind.Newborn ? new Color(0.96f, 0.95f, 0.92f) : slots[i] == PoleFeatherKind.Marriage ? new Color(0.80f, 0.18f, 0.16f) : new Color(0.80f, 0.56f, 0.20f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(-40f, 0f, 0f); // out from the pole and up, 50 deg off upright
                var at = new Vector3(0f, y, 0f) + dir * 0.02f;
                mb.Box(at + rot * new Vector3(0f, 0.085f, 0f), new Vector3(0.04f, 0.15f, 0.006f), col, rot);
                mb.Box(at + rot * new Vector3(0f, 0.165f, 0f), new Vector3(0.03f, 0.02f, 0.007f), col * 0.55f, rot);
                mb.Box(at + rot * new Vector3(0f, 0.08f, 0f), new Vector3(0.005f, 0.17f, 0.009f), Palette.Timber, rot);
            }
            var m = mb.ToMesh("Consequence_FeatherPole_" + slots.Length);
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        public static Mesh PoleWithCharms(int count)
        {
            var mb = new MeshBuilder(3780 + count) { jitter = 0.025f };
            mb.Prism(Vector3.zero, 0.02f, 0.014f, 0.36f, 6, Palette.Bark, topCol: Palette.WoodLight);
            mb.Box(new Vector3(0.06f, 0.32f, 0f), new Vector3(0.14f, 0.014f, 0.014f), Palette.Bark);
            int n = Mathf.Clamp(count, 1, 6);
            float step = n <= 4 ? 0.045f : 0.12f / (n - 1);
            for (int i = 0; i < n; i++)
            {
                float x = 0.02f + i * step;
                mb.Box(new Vector3(x, 0.29f, 0f), new Vector3(0.003f, 0.05f, 0.003f), Palette.Plaster);
                mb.Ico(new Vector3(x, 0.26f, 0f), 0.014f, Vector3.one, Palette.Accents[i % Palette.Accents.Length], 0.05f);
            }
            mb.Ico(new Vector3(0f, 0.38f, 0f), 0.07f, new Vector3(1f, 0.7f, 1f), Palette.LeafB, 0.15f);
            var mesh = mb.ToMesh("Consequence_pole_charms_" + count);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>Where the cords tie on for a drawn tree of play radius <paramref name="spotR"/> (BoxView.TreeSpots): the Keeper's grown tree
        /// (KeeperTreeKit stage 4) or a wild one (Pieces.Tree, radius 0.16 x its scale).</summary>
        public static void CharmTie(bool keeperTree, float spotR, out float attachY, out float ringR)
        {
            if (keeperTree) { attachY = 0.4f; ringR = 0.11f; return; }
            float s = spotR / 0.16f;
            attachY = 0.27f * s; ringR = 0.12f * s;
        }
    }

    /// <summary>
    /// #371: draws the things the Keeper's choices left in the box (<see cref="SimCore.ConsequenceProps"/>, placed by the sim at a real
    /// spot, saved with the box). Each stands on the ground at its spot, a little bigger than life. <see cref="Pulse"/> (a callback moment
    /// starting) makes one bob once, so the eye finds it. Everything hangs under the box and goes with it. Auto-created next to the box.
    /// </summary>
    public class ConsequencePropsView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static ConsequencePropsView Instance { get; private set; }
        /// <summary>Real seconds a pulse lasts.</summary>
        public const float PulseSeconds = 1.6f;

        sealed class Made { public Transform t; public Mesh mesh; public string kind; public float yaw; public Vector3 at; }

        /// <summary>#635: the mesh key of a thing: its kind, or for charms on a tree the tree and the count (a new death adds a charm).</summary>
        // #730: and the drawn crown's size, so charms whose tree was lifted re-tie on the next drawn tree (BoxView prunes its spots).
        static string KeyOf(ConsequenceProp p, int charms, float treeR = 0f) => p.anchor != PropAnchor.Tree ? p.kind : p.treeCell >= 0 ? $"charms:{p.treeCell}:{charms}:{treeR:0.000}" : $"pole:{charms}";

        /// <summary>#635: the drawn tree a tree thing hangs on: its cell's spot, else the nearest drawn grown tree (a wild crown is drawn on
        /// some of its cells only). False when none is drawn near.</summary>
        bool TreeSpotOf(int cell, float x, float z, out Vector3 at, out float r)
        {
            at = default; r = 0f;
            lock (box.TreeSpots)
            {
                if (box.TreeSpots.TryGetValue(cell, out var s)) { at = s.pos; r = s.r; return true; }
                float best = 0.35f * 0.35f; bool found = false;
                foreach (var kv in box.TreeSpots)
                {
                    float dx = kv.Value.pos.x - x, dz = kv.Value.pos.z - z, d = dx * dx + dz * dz;
                    if (d < best) { best = d; at = kv.Value.pos; r = kv.Value.r; found = true; }
                }
                return found;
            }
        }

        BoxView box;
        Transform root;
        SimCore drawn;
        float fx, fz, rackAngle, fishAngle = float.NaN;
        PeopleView people;
        readonly List<CampSpots.Disc> spots = new();
        readonly Dictionary<int, Vector2> settledSpot = new(); // #700: where each fire prop stands now
        readonly List<ConsequenceProp> ordered = new();
        readonly List<CampSpots.Disc> wide = new();

        /// <summary>#714 round 2: how far out from the fire the boat wants to lie (toward the pond), and the room it keeps from the other things
        /// (it is 0.4 u long: the pot and the baskets must never hide it).</summary>
        public const float BoatOut = 0.9f, BoatRadius = 0.26f;

        /// <summary>#714 round 2 (the user: "apart from the stew pot, so nothing hides it"): the boat lies out from the fire toward the pond, on
        /// clear ground: every other thing's room is widened by the boat's half-length, so none of them touches it.</summary>
        void BoatSpot(LifePlanner planner, System.Func<float, float, bool> onPath, out float px, out float pz)
        {
            var dir = new Vector2(0f, -1f); // no pond yet: toward the front glass
            if (planner != null && planner.Positions.TryGetPosition(Place.Land(PlaceKind.Pond), out float wx, out float wz))
            {
                var d = new Vector2(wx - fx, wz - fz);
                if (d.sqrMagnitude > 1e-4f) dir = d.normalized;
            }
            wide.Clear();
            foreach (var d in spots) wide.Add(new CampSpots.Disc(d.x, d.z, d.r + BoatRadius - CampSpots.PropRadius));
            CampSpots.Spot(fx, fz, rackAngle, LifePlanner.HideFrameDistance, fx + dir.x * BoatOut, fz + dir.y * BoatOut, wide, out px, out pz, fishAngle, onPath);
        }

        /// <summary>#714 round 2: the boat lines up with the box's lines (the glass), its long side across the way to the fire, never a random yaw.</summary>
        float BoatYaw(float px, float pz) => Mathf.Abs(px - fx) > Mathf.Abs(pz - fz) ? 90f : 0f;
        readonly List<CampSpots.Disc> fixedScratch = new();
        readonly Dictionary<int, Made> made = new();
        readonly Dictionary<int, float> pulses = new();
        float flutterAt = -99f, flutterAmp = 1f;

        /// <summary>#692 (B31.a/b): a new feather tied on, a kid's touch: the pole jolts and its feathers flutter.</summary>
        public static void FlutterPole(float amp = 1f)
        {
            if (Instance == null) return;
            Instance.flutterAt = Time.unscaledTime; Instance.flutterAmp = amp;
        }

        /// <summary>The things standing now, by prop id (tests).</summary>
        public IReadOnlyDictionary<int, Transform> Standing
        {
            get { var d = new Dictionary<int, Transform>(); foreach (var kv in made) d[kv.Key] = kv.Value.t; return d; }
        }

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<ConsequencePropsView>() == null) b.gameObject.AddComponent<ConsequencePropsView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            ClearAll();
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        void ClearAll()
        {
            foreach (var m in made.Values) { Kill(m.mesh); if (m.t != null) Kill(m.t.gameObject); }
            made.Clear(); settledSpot.Clear();
            pulses.Clear();
            if (root != null) Kill(root.gameObject);
            root = null;
            drawn = null;
        }

        /// <summary>A callback moment starts at this thing: it bobs once.</summary>
        public static void Pulse(int propId)
        {
            if (Instance == null) return;
            var sim = Sim; // fb4 #566: every feather answer is the one pole
            var pole = sim?.FeatherPoleProp;
            if (pole != null && sim.FindConsequenceProp(propId)?.kind == ConsequenceKind.Feather) propId = pole.id;
            Instance.pulses[propId] = Time.unscaledTime;
        }

        void LateUpdate()
        {
            var sim = Sim;
            var list = sim?.State.consequenceProps;
            if (box == null || sim == null || list == null || list.Count == 0)
            {
                if (made.Count > 0 || root != null) ClearAll();
                return;
            }
            if (sim != drawn) { ClearAll(); drawn = sim; }
            if (root == null)
            {
                root = new GameObject("ConsequenceProps") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            if (people == null) people = PeopleView.Find(); var planner = people != null ? people.Planner : null;
            bool haveFire = planner != null && planner.FirePoint(out fx, out fz) && planner.HideFrame(out _, out _, out _, out _);
            spots.Clear();
            if (haveFire)
            {
                // fb4 #540: the buildings and the things already placed are in the way
                foreach (var b in sim.State.buildings) if (b.lotId != 0) spots.Add(new CampSpots.Disc(b.x, b.z, 0.22f));
                planner.HideFrame(out float hx, out float hz, out _, out _);
                rackAngle = Mathf.Atan2(hz - fz, hx - fx);
                fishAngle = FishRackSpot.AngleFor(sim, fx, fz); // #543, #700 (where the camp stands it now)
            }
            var pole = sim.FeatherPoleProp; // fb4 #566: one feather pole, however many answers asked for a feather (old saves too)
            ordered.Clear(); // #714: the boat is stood last, so it keeps clear of every other thing at the fire
            foreach (var p in list) if (p.kind != ConsequenceKind.ShoreBoat) ordered.Add(p);
            foreach (var p in list) if (p.kind == ConsequenceKind.ShoreBoat) ordered.Add(p);
            foreach (var p in ordered)
            {
                if (!p.placed) continue;
                if (!ReplayGate.Shows(p.placedMonth)) // #669: a replay shows it from the month it was placed
                {
                    if (made.TryGetValue(p.id, out var early) && early.t != null && early.t.gameObject.activeSelf) early.t.gameObject.SetActive(false);
                    continue;
                }
                if (p.kind == ConsequenceKind.Feather && p != pole)
                {
                    if (made.TryGetValue(p.id, out var gone)) { Kill(gone.mesh); if (gone.t != null) Kill(gone.t.gameObject); made.Remove(p.id); }
                    continue;
                }
                float px = p.x, pz = p.z;
                if (haveFire && p.anchor == PropAnchor.Fire)
                {
                    // #700: never on a path; one that a path grows over (or a moved rack reaches) moves at night, not in front of the player
                    var onPath = RackSites.PathTest(sim);
                    if (p.kind == ConsequenceKind.ShoreBoat) BoatSpot(planner, onPath, out px, out pz);
                    else CampSpots.Spot(fx, fz, rackAngle, LifePlanner.HideFrameDistance, p.x, p.z, spots, out px, out pz, fishAngle, onPath);
                    if (settledSpot.TryGetValue(p.id, out var was) && (was.x != px || was.y != pz))
                    {
                        fixedScratch.Clear(); CampSpots.FireSide(fx, fz, fixedScratch);
                        bool stillOk = CampSpots.Clear(fx, fz, rackAngle, LifePlanner.HideFrameDistance, was.x, was.y, fixedScratch, spots, fishAngle, onPath);
                        if (stillOk || !RackSites.IsNight(sim)) { px = was.x; pz = was.y; }
                    }
                    settledSpot[p.id] = new Vector2(px, pz);
                    spots.Add(new CampSpots.Disc(px, pz, p.kind == ConsequenceKind.ShoreBoat ? BoatRadius : CampSpots.PropRadius));
                }
                else if (haveFire) spots.Add(new CampSpots.Disc(p.x, p.z, CampSpots.PropRadius));
                // #635: charms on a tree hang from the drawn crown (no post, not scaled up); a tree not drawn yet: wait a frame
                bool onTree = p.anchor == PropAnchor.Tree && p.treeCell >= 0;
                Vector3 treeAt = default; float treeR = 0f;
                if (onTree && !TreeSpotOf(p.treeCell, p.x, p.z, out treeAt, out treeR))
                {
                    if (made.TryGetValue(p.id, out var hide) && hide.t != null) hide.t.gameObject.SetActive(false);
                    continue;
                }
                int charms = p.anchor == PropAnchor.Tree ? sim.CharmsOn(p) : 0;
                string key = KeyOf(p, charms, treeR);
                int[] featherSlots = p == pole ? SimCore.PoleSlots(sim.PoleFeathers) : null; // #692 (B31.a): the pole fills up over the years
                if (featherSlots != null && featherSlots.Length > 0) key += ":f" + string.Join("", featherSlots);
                if (!made.TryGetValue(p.id, out var m) || m.t == null || m.kind != key)
                {
                    if (m != null) { Kill(m.mesh); if (m.t != null) Kill(m.t.gameObject); }
                    Mesh mesh;
                    if (onTree)
                    {
                        ConsequenceMeshes.CharmTie(p.treeCell == sim.State.eco.treeCell, treeR, out float ty, out float tr);
                        mesh = ConsequenceMeshes.Charms(charms, ty, tr);
                    }
                    else if (p.anchor == PropAnchor.Tree) mesh = ConsequenceMeshes.PoleWithCharms(charms); // tester: the pole always drew four
                    else if (featherSlots != null && featherSlots.Length > 0) mesh = ConsequenceMeshes.FeatherPole(featherSlots);
                    else mesh = ConsequenceMeshes.Build(p.kind);
                    m = new Made { kind = key, mesh = mesh };
                    var go = new GameObject("Consequence_" + p.kind + "_" + p.id) { hideFlags = HideFlags.DontSave };
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = m.mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = box.SolidMaterial;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    m.t = go.transform;
                    made[p.id] = m;
                }
                float s = ConsequenceMeshes.Scale;
                float lift = 0f;
                if (pulses.TryGetValue(p.id, out float t0))
                {
                    float k = (Time.unscaledTime - t0) / PulseSeconds;
                    if (k >= 1f) pulses.Remove(p.id);
                    else { float w = Mathf.Sin(k * Mathf.PI); s *= 1f + 0.25f * w; lift = 0.04f * w; }
                }
                if (!m.t.gameObject.activeSelf) m.t.gameObject.SetActive(true);
                if (onTree)
                {
                    m.t.localScale = Vector3.one * (s / ConsequenceMeshes.Scale);
                    m.t.SetLocalPositionAndRotation(treeAt + Vector3.up * lift, Quaternion.Euler(0f, p.yaw, 0f));
                    continue;
                }
                m.t.localScale = Vector3.one * s;
                var tilt = Quaternion.identity;
                float flutterFor = flutterAmp > 1.2f ? 2.6f : 1.2f, flutterTau = flutterAmp > 1.2f ? 0.8f : 0.3f; // the user's review: a new feather flutters longer
                if (p == pole && Time.unscaledTime - flutterAt < flutterFor) // #692 (B31.a/b): the feathers flutter (Sway 8 deg, 6 Hz, tau 0.3 s)
                {
                    float ft = Time.unscaledTime - flutterAt;
                    tilt = Quaternion.Euler(Mathf.Sin(ft * 6f * Mathf.PI * 2f) * 8f * Mathf.Min(flutterAmp, 1.3f) * Mathf.Exp(-ft / flutterTau), 0f, Mathf.Sin(ft * 4.3f * Mathf.PI * 2f) * 4f * Mathf.Exp(-ft / flutterTau) * (flutterAmp > 1.2f ? 1f : 0f));
                }
                float yaw = p.kind != ConsequenceKind.ShoreBoat ? p.yaw : haveFire && p.anchor == PropAnchor.Fire ? BoatYaw(px, pz) : Mathf.Round(p.yaw / 90f) * 90f; // #714 round 2: square to the glass (old saves too)
                m.t.SetLocalPositionAndRotation(box.land.OnGround(px, pz) + Vector3.up * lift, Quaternion.Euler(0f, yaw, 0f) * tilt);
            }
        }
    }
}

namespace AquariumCiv.View
{
    /// <summary>#371 cheats (debug panel "Choices leave things (#371)").</summary>
    public static class ConsequenceCheats
    {
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;

        /// <summary>Every option that leaves a thing leaves it now, placed (one of each kind stands in the box).</summary>
        public static string PlaceEvery()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            int n = sim.DebugPlaceEveryProp();
            return $"{n} things stand in the box";
        }

        /// <summary>#635: raises "where do we remember the ones we lose" (a death first if nobody has died) and answers "the tree": the
        /// charms hang on the Keeper's tree if grown, else the nearest grown tree, else a pole stands in.</summary>
        public static string RememberOnTree()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            var d = sim.DebugRaiseDilemma("camp_remember");
            if (d == null) return "camp_remember can't be raised here (Camp only): " + sim.DilemmaWhyNot("camp_remember");
            if (!sim.AnswerDilemma(d.id, 1, DilemmaPath.Card)) return "could not answer it";
            var p = sim.ConsequencePropFor(d.id, 1);
            return p == null ? "answered, but no thing was left"
                : p.treeCell < 0 ? $"answered: no grown tree, a pole stands in ({sim.CharmsOn(p)} charm)"
                : $"answered: {sim.CharmsOn(p)} charm(s) on {(p.treeCell == sim.State.eco.treeCell ? "the Keeper's tree" : "a wild tree")}";
        }

        /// <summary>#635: the Keeper's tree grown now (planted first if need be); charms on a pole or a wild tree move to it next month.</summary>
        public static string GrowKeeperTree()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            return sim.DebugTreeStage(4) ? "the Keeper's tree is grown" : "no Keeper's tree can grow here";
        }

        /// <summary>#635: the oldest dies: one more charm on the tree.</summary>
        public static string OneMoreDeath()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            sim.DebugKillOldest();
            return "the oldest died";
        }

        /// <summary>fb4 #566: "Feather pole: pray now": the one pole stands (made if need be) and two grown-ups and a kid walk to it and pray.</summary>
        public static string PrayNow()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no PeopleView";
            int n = pv.Planner.DebugPrayNow(pv.Now);
            return n < 0 ? "nobody free to pray (or no box)" : $"{n} walk to the feather pole and pray";
        }

        /// <summary>fb4 #540: every fire-side thing at once: the drying rack with 6 hides, every Keeper's prop (the stew pot, the feathers...),
        /// so the overlaps can be looked at.</summary>
        public static string ShowCampProps()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            string a = EcoDebug.ShowRack(6);
            return a + "; " + PlaceEvery();
        }

        /// <summary>Fires the next callback (a choice is raised and answered first when none is waiting) and plays its moment now.</summary>
        public static string PlayCallbackNow()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            if (sim.PendingCallbacks().Count == 0)
            {
                string id = System.Array.Find(DilemmaCheats.Ids, x => sim.DilemmaWhyNot(x) == null);
                if (id == null) return "no callback waits and no choice can be raised here";
                var d = sim.DebugRaiseDilemma(id);
                if (d == null || !sim.AnswerDilemma(d.id, 0, DilemmaPath.Card)) return "could not answer a choice for its callback";
            }
            var e = sim.DebugFireCallbackNow();
            if (e == null) return "no callback had anything to tell";
            return "callback told: " + HeldMomentStage.DebugPlayNextNow();
        }
    }
}
