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

        /// <summary>The mesh of a <see cref="ConsequenceKind"/> (an unknown kind: a small cairn, so nothing is ever missing). #693: a
        /// <paramref name="variant"/> is how a use has it now: baskets taken off the stack (1-3), the axe out of the stump or the staff off its
        /// stone (1), new rows cut in the name stone (1-3), the offering's cloth fuller (1) or lifted (2: the food shows; 3: more of it), fish
        /// taken out of the crate (1-3). 0 is the thing as it stands.</summary>
        public static Mesh Build(string kind, int variant = 0)
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
                        if (BasketGone(i, variant)) continue; // #693 (B54.a/b): taken off the stack
                        BerryBasket(mb, BasketAt(i), i);
                    }
                    break;
                case ConsequenceKind.WoodpileRoof: // a tall woodpile under its own little roof
                {
                    Pieces.Woodpile(mb, Vector3.zero, 0f);
                    int laid = Mathf.Clamp(variant & ~FaceLook, 0, WoodSlots);
                    for (int i = 0; i < laid; i++) Pieces.Log(mb, WoodSlot(i), 0.026f, 0.26f, 90f, i % 2 == 0 ? Palette.WoodLight : Palette.Bark); // #693 notes: the logs laid across its top
                    if ((variant & FaceLook) != 0) FaceLog(mb, laid); // #722 (2.4, B80.f): the joker's grin, on top
                }
                    foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.15f, 0.12f, 0f), new Vector3(0.02f, 0.24f, 0.02f), Palette.Timber);
                    foreach (int s in new[] { -1, 1 })
                        mb.Box(new Vector3(0f, 0.26f, s * 0.05f), new Vector3(0.36f, 0.015f, 0.12f), Palette.Thatch, Quaternion.Euler(s * 28f, 0f, 0f), true);
                    break;
                case ConsequenceKind.NameStone: // #693 notes: an archaic runestone: one rough standing slab, sunk in the ground, no shaped base or top
                    RuneStone(mb, variant);
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
                    if (variant == 1) { mb.Box(new Vector3(-0.012f, 0.101f, 0f), new Vector3(0.05f, 0.003f, 0.006f), Palette.Bark); break; } // #693 (B54.j): pulled out, the cut shows
                    mb.Box(new Vector3(0.0f, 0.2f, 0f), new Vector3(0.016f, 0.22f, 0.016f), Palette.WoodLight, Quaternion.Euler(0f, 0f, -12f));
                    mb.Box(new Vector3(-0.03f, 0.13f, 0f), new Vector3(0.05f, 0.03f, 0.01f), Palette.Iron, Quaternion.Euler(0f, 0f, -12f));
                    break;
                case ConsequenceKind.Staff: // a headman's staff with a ribbon, on a flat stone
                    mb.Prism(Vector3.zero, 0.06f, 0.05f, 0.02f, 7, Palette.Rock, topCol: Palette.RockLight);
                    if (variant == 1) break; // #693 (B54.l/m): taken up, the stone is bare
                    mb.Prism(new Vector3(0f, 0.02f, 0f), 0.012f, 0.01f, 0.42f, 6, Palette.WoodLight);
                    mb.Ico(new Vector3(0f, 0.45f, 0f), 0.028f, Vector3.one, Palette.Accents[1], 0.05f);
                    mb.sway = 0.4f;
                    mb.Box(new Vector3(0.03f, 0.38f, 0f), new Vector3(0.06f, 0.05f, 0.003f), Palette.Accents[0]);
                    mb.sway = 0f;
                    break;
                case ConsequenceKind.FlourSacks: // sacks heaped by the mill, one split open (#693 notes: the classic fat sack tied at the neck)
                    for (int i = 0; i < 3; i++) FlourSack(mb, new Vector3((i - 1) * 0.085f, 0f, i == 1 ? 0.04f : 0f), (i * 37f) % 40f - 20f); // three standing
                    FlourSack(mb, new Vector3(-0.02f, 0.03f, -0.07f), 80f, 90f); // one lying in front
                    mb.Ico(new Vector3(0.12f, 0.02f, -0.05f), 0.04f, new Vector3(1.2f, 0.35f, 1f), Palette.Canvas, 0.1f); // the split one, flat
                    mb.Ico(new Vector3(0.13f, 0.03f, -0.06f), 0.035f, new Vector3(1.3f, 0.35f, 1f), new Color(0.97f, 0.96f, 0.92f), 0.1f); // its flour
                    break;
                case ConsequenceKind.RoofBird: // a carved bird on a ridge pole
                    mb.Prism(Vector3.zero, 0.014f, 0.012f, 0.24f, 6, Palette.Timber);
                    mb.Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.2f, 0.02f, 0.02f), Palette.Thatch);
                    mb.Ico(new Vector3(0f, 0.3f, 0f), 0.04f, new Vector3(1.4f, 0.9f, 0.8f), Palette.Accents[2], 0.06f);
                    mb.Ico(new Vector3(-0.05f, 0.335f, 0f), 0.022f, Vector3.one, Palette.Accents[2], 0.05f);
                    mb.Prism(new Vector3(-0.07f, 0.335f, 0f), 0.008f, 0f, 0.03f, 4, Palette.Accents[1], Quaternion.Euler(0f, 0f, 90f));
                    mb.Box(new Vector3(0.06f, 0.31f, 0f), new Vector3(0.05f, 0.012f, 0.03f), Palette.Accents[2]);
                    break;
                case ConsequenceKind.LongTable: // a long table with benches either side (#693 notes: four legs, so kids can crawl under it)
                {
                    // S-ritual (B74): one board longer for every feast after the first (variant = the boards, at most 12), the newest board pale
                    int boards = Mathf.Clamp(variant, 0, MaxBoards);
                    float grow = BoardLength * boards, half = 0.22f + grow * 0.5f;
                    mb.Box(new Vector3(-grow * 0.5f, 0.07f, 0f), new Vector3(0.44f, 0.014f, 0.1f), Palette.WoodLight);
                    for (int k = 0; k < boards; k++)
                        mb.Box(new Vector3(0.22f - grow * 0.5f + BoardLength * (k + 0.5f), 0.0702f, 0f), new Vector3(BoardLength - 0.002f, 0.014f, 0.1f), k == boards - 1 ? NewBoard : k % 2 == 0 ? Palette.Timber : Palette.WoodLight);
                    float legX = TableLegX + grow * 0.5f, benchLegX = 0.17f + grow * 0.5f;
                    foreach (int sx in new[] { -1, 1 })
                        foreach (int sz in new[] { -1, 1 })
                        {
                            mb.Box(new Vector3(sx * legX, 0.032f, sz * 0.04f), new Vector3(0.012f, 0.064f, 0.012f), Palette.Timber); // the table's legs
                            mb.Box(new Vector3(sx * benchLegX, 0.017f, sz * 0.085f), new Vector3(0.01f, 0.034f, 0.01f), Palette.Timber); // the benches' legs
                        }
                    foreach (int sz in new[] { -1, 1 }) mb.Box(new Vector3(0f, 0.04f, sz * 0.085f), new Vector3(0.4f + grow, 0.012f, 0.04f), Palette.Timber);
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
                }
                case ConsequenceKind.OfferingBasket: // #693 notes: a big round woven basket heaped with food, half under a cloth, a flower on top, a handle
                    OfferingBasketMesh(mb, variant);
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
                case ConsequenceKind.FishCrate: // #693 notes: a round woven fish basket at the shore, fish tails sticking out, two fish on top
                    FishBasketMesh(mb, variant);
                    break;
                case ConsequenceKind.GoatPen: // 2026-11-16 "Bring a pen back": a little rail pen, gnawed at one corner (variant 1: mended, a pale new rail)
                    GoatPenMesh(mb, variant);
                    break;
                case ConsequenceKind.NamePeg: // #693 notes: a carved name post by the door: notches and a carved mark, height scratches
                    NamePostMesh(mb, variant);
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
        /// <summary>The goat pen, before <see cref="Scale"/>: 0.2 square (0.3 u drawn), corner posts and side posts, two rails a side; the corner at
        /// +x +z is gnawed: the top rail there stops short with a chewed end, the low rail is gone, a gap a kid can crawl through
        /// (<see cref="PenGap"/>). <paramref name="variant"/> 1: mended, a new pale rail bound on across the gap.</summary>
        public const float PenHalf = 0.1f;
        public static readonly Vector3 PenGap = new Vector3(PenHalf, 0f, PenHalf * 0.55f);
        static void GoatPenMesh(MeshBuilder mb, int variant)
        {
            const float h = PenHalf, top = 0.085f, low = 0.04f;
            var post = Palette.Bark; var rail = Palette.Timber; var fresh = Palette.WoodLight;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    mb.Box(new Vector3(i * h, 0.05f, j * h), new Vector3(0.016f, 0.1f, 0.016f), post);
                }
            void Rail(Vector3 a, Vector3 b, float y, Color c, float th = 0.009f)
            {
                var m = (a + b) * 0.5f; var d = b - a;
                mb.Box(new Vector3(m.x, y, m.z), new Vector3(Mathf.Abs(d.x) + th, th, Mathf.Abs(d.z) + th), c);
            }
            // the four sides; the +x side's far half (toward +z) is the gnawed corner
            Rail(new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h), top, rail); Rail(new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h), low, rail);
            Rail(new Vector3(-h, 0f, h), new Vector3(h, 0f, h), top, rail); Rail(new Vector3(-h, 0f, h), new Vector3(h, 0f, h), low, rail);
            Rail(new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), top, rail); Rail(new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), low, rail);
            Rail(new Vector3(h, 0f, -h), new Vector3(h, 0f, 0f), top, rail); Rail(new Vector3(h, 0f, -h), new Vector3(h, 0f, 0f), low, rail);
            if (variant == 1)
            {
                Rail(new Vector3(h, 0f, 0f), new Vector3(h, 0f, h), top, fresh, 0.011f); Rail(new Vector3(h, 0f, 0f), new Vector3(h, 0f, h), low, fresh, 0.011f);
                foreach (float z in new[] { 0.005f, h - 0.005f }) foreach (float y in new[] { top, low }) mb.Box(new Vector3(h, y, z), new Vector3(0.014f, 0.016f, 0.006f), Palette.Straw); // the bindings
            }
            else
            {
                Rail(new Vector3(h, 0f, 0f), new Vector3(h, 0f, h * 0.45f), top, rail); // stops short: chewed
                for (int k = 0; k < 3; k++) mb.Box(new Vector3(h, top + (k - 1) * 0.003f, h * 0.45f + 0.004f + k * 0.003f), new Vector3(0.008f, 0.004f, 0.006f), Palette.WoodLight); // the pale chewed end
                mb.Box(new Vector3(h, low, h * 0.85f), new Vector3(0.009f, 0.009f, h * 0.3f), rail); // a stub of the low rail at the corner
                for (int k = 0; k < 4; k++) mb.Box(new Vector3(h + 0.012f + (k % 2) * 0.008f, 0.003f, h * (0.3f + 0.15f * k)), new Vector3(0.01f, 0.003f, 0.004f), Palette.WoodLight); // chips on the ground
            }
            mb.Ico(new Vector3(-h * 0.45f, 0.006f, -h * 0.4f), 0.03f, new Vector3(1f, 0.25f, 0.8f), Palette.Straw, 0.1f); // a little hay inside
        }


        // ---------------------------------------------------------------- #693 notes: the reworked things
        /// <summary>One berry basket of the pile (local, before <see cref="Scale"/>): body, bands, rim, berries, the bent handle. The carried
        /// basket (B54.a) is this same piece.</summary>
        public static void BerryBasket(MeshBuilder mb, Vector3 at, int i)
        {
            mb.Prism(at, 0.042f, 0.05f, 0.055f, 8, Palette.Thatch, topCol: Palette.Timber);
            mb.Prism(at + new Vector3(0f, 0.016f, 0f), 0.046f, 0.047f, 0.008f, 8, Palette.WoodLight);
            mb.Prism(at + new Vector3(0f, 0.034f, 0f), 0.049f, 0.05f, 0.008f, 8, Palette.WoodLight);
            mb.Prism(at + new Vector3(0f, 0.052f, 0f), 0.053f, 0.053f, 0.008f, 8, Palette.Straw);
            var berry = i == 1 ? new Color(0.36f, 0.16f, 0.42f) : new Color(0.78f, 0.1f, 0.15f);
            for (int k = 0; k < 9; k++)
            {
                float a = k * 2.4f + i, r = k == 0 ? 0f : 0.012f + 0.022f * ((k % 3) / 2f);
                float y = 0.058f + (k == 0 ? 0.016f : (0.026f - r) * 0.55f);
                mb.Ico(at + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), 0.011f, Vector3.one, Color.Lerp(berry, Color.black, (k % 4) * 0.07f), 0.1f);
            }
            for (int k = 0; k <= 4; k++)
            {
                float t = k / 4f * Mathf.PI, hx = Mathf.Cos(t) * 0.044f, hy = 0.06f + Mathf.Sin(t) * 0.05f;
                mb.Box(at + new Vector3(hx, hy, 0f), new Vector3(0.008f, 0.026f, 0.008f), Palette.WoodLight, Quaternion.Euler(0f, 0f, (t - Mathf.PI * 0.5f) * Mathf.Rad2Deg));
            }
        }

        /// <summary>The runestone's footprint (local half-sizes before <see cref="Scale"/>): people stand outside it (tests: NoOneInsideTheStone).</summary>
        public const float StoneHalfX = 0.07f, StoneHalfZ = 0.035f, StoneH = 0.36f;
        /// <summary>#722 (1.3, B78.c): the second name stone: the same runestone with another seed (its own rough shape), and only the rows cut
        /// on it so far (<paramref name="rows"/>, 0-3: the first name, then the next; the newest pale), not the first stone's four old rows.</summary>
        public static Mesh SecondStone(int rows)
        {
            var mb = new MeshBuilder(3710 + System.Array.IndexOf(ConsequenceKind.All, ConsequenceKind.NameStone) + 7220) { jitter = 0.025f };
            RuneStone(mb, rows, 0);
            var mesh = mb.ToMesh("Consequence_" + ConsequenceKind.NameStone + "_second");
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        static void RuneStone(MeshBuilder mb, int variant, int oldRows = 4)
        {
            var grey = new Color(0.56f, 0.55f, 0.52f); var dark = new Color(0.33f, 0.32f, 0.31f);
            // the slab: one rough piece of stone, an uneven five-sided section squashed thin, narrowing a little to an uneven cut top, leaning a
            // touch and sunk in the ground (the props' jitter roughens it further)
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(new Vector3(0f, -0.02f, 0f), Quaternion.identity, new Vector3(1f, 1f, StoneHalfZ / (StoneHalfX * 0.866f)));
            mb.Prism(Vector3.zero, StoneHalfX, StoneHalfX * 0.96f, StoneH - 0.02f, 6, grey, topCol: Color.Lerp(grey, dark, 0.25f)); // flat faces at +-z
            mb.Prism(new Vector3(-0.02f, StoneH - 0.04f, 0f), StoneHalfX * 0.62f, StoneHalfX * 0.4f, 0.05f, 6, grey, topCol: Color.Lerp(grey, dark, 0.25f)); // an uneven, broken-off top
            mb.M = saved;
            mb.Ico(new Vector3(0.05f, 0.005f, 0.03f), 0.025f, new Vector3(1.2f, 0.4f, 1f), dark, 0.2f); // a stone at its foot
            // the runes on both faces: rows of short strokes and slants (the newest row pale: freshly cut)
            int rows = oldRows + Mathf.Clamp(variant, 0, 3); float gap = rows <= 4 ? 0.045f : 0.17f / (rows - 1);
            foreach (float face in new[] { -1f, 1f })
                for (int i = 0; i < rows; i++)
                {
                    bool fresh = variant > 0 && i == rows - 1;
                    var col = fresh ? new Color(0.88f, 0.86f, 0.80f) : dark;
                    float y = 0.1f + i * gap, z = face * (StoneHalfZ + 0.002f);
                    for (int k = 0; k < 4; k++)
                    {
                        float x = -0.04f + k * 0.027f;
                        mb.Box(new Vector3(x, y, z), new Vector3(0.004f, 0.022f, 0.004f), col);
                        mb.Box(new Vector3(x + 0.007f, y + 0.003f, z), new Vector3(0.004f, 0.016f, 0.004f), col, Quaternion.Euler(0f, 0f, (k % 2 == 0 ? 35f : -35f)));
                    }
                }
        }

        /// <summary>The classic flour sack: a fat cloth body, wider at the foot, pinched and tied at the neck with a cord, an ear of cloth on top
        /// (local foot at <paramref name="at"/>, before <see cref="Scale"/>). The carried sack (B54.n) is this same piece.</summary>
        public static void FlourSack(MeshBuilder mb, Vector3 at, float yaw = 0f, float lie = 0f)
        {
            var cloth = new Color(0.86f, 0.80f, 0.66f); var cord = new Color(0.55f, 0.38f, 0.22f);
            var saved = mb.M; mb.M = saved * Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, lie), Vector3.one);
            mb.Ico(new Vector3(0f, 0.036f, 0f), 0.045f, new Vector3(1.05f, 0.85f, 0.85f), cloth, 0.12f, Color.Lerp(cloth, Color.black, 0.15f)); // the fat body, flat at the foot
            mb.Prism(new Vector3(0f, 0.062f, 0f), 0.034f, 0.01f, 0.026f, 8, cloth); // the shoulders gathered up to the neck
            mb.Prism(new Vector3(0f, 0.086f, 0f), 0.012f, 0.012f, 0.006f, 8, cord); // the cord tied round the neck
            mb.Ico(new Vector3(0f, 0.098f, 0f), 0.013f, new Vector3(1.3f, 0.8f, 0.7f), cloth, 0.2f); // the tuft of cloth above the tie
            mb.M = saved;
        }

        /// <summary>The long table's legs: four posts at +-this x (and +-0.04 z), so a crawling kid passes between them.</summary>
        public const float TableLegX = 0.18f;

        /// <summary>A round woven basket (local, before <see cref="Scale"/>): alternating straw bands, a thick rim. Returns its rim's height.</summary>
        static float WovenBasket(MeshBuilder mb, Vector3 at, float r, float h)
        {
            var a = new Color(0.86f, 0.70f, 0.38f); var b = new Color(0.72f, 0.54f, 0.26f);
            mb.Prism(at, r * 0.82f, r, h, 10, a, topCol: new Color(0.35f, 0.24f, 0.12f));
            for (int k = 1; k < 4; k++) mb.Prism(at + new Vector3(0f, h * k / 4f - 0.004f, 0f), r * (0.82f + 0.18f * k / 4f) + 0.002f, r * (0.82f + 0.18f * k / 4f) + 0.002f, 0.007f, 10, b);
            mb.Prism(at + new Vector3(0f, h - 0.004f, 0f), r + 0.006f, r + 0.006f, 0.012f, 10, new Color(0.62f, 0.45f, 0.22f)); // the rim
            return h;
        }

        /// <summary>#693 notes (the user: "does not read like one AT ALL"): the offering basket from scratch. A big round woven basket with a rim and
        /// a handle arching over, heaped with food (apples, loaves, berries) poking up past the rim, a cloth over half of it, a flower on top.
        /// Variant 1 fuller, 2 the cloth lifted, 3 lifted and fuller.</summary>
        /// <summary>S-ritual (B74): the long table's boards (one a year after the first feast), each this long (mesh units, before the prop scale);
        /// the newest board's paler wood.</summary>
        public const int MaxBoards = 12;
        public const float BoardLength = 0.04f;
        static readonly Color NewBoard = new Color(0.9f, 0.78f, 0.58f);
        /// <summary>S-ritual (B75): the offering basket's looks after the 2026-11-16 note: empty in spring (the cloth lies flat in it, no food at the
        /// rim); gone (in someone's hands: only the flattened grass where it stands).</summary>
        public const int OfferingEmpty = 4, OfferingGone = 5;

        static void OfferingBasketMesh(MeshBuilder mb, int variant)
        {
            const float R = 0.075f, H = 0.065f;
            if (variant == OfferingGone) { mb.Ico(new Vector3(0f, 0.001f, 0f), R * 1.05f, new Vector3(1f, 0.04f, 1f), new Color(0.48f, 0.56f, 0.3f), 0.1f); return; }
            WovenBasket(mb, Vector3.zero, R, H);
            if (variant == OfferingEmpty)
            {
                // empty: the cloth lies flat in it, 0.02 lower than the heap's top, the flower on it
                var flat = new Color(0.93f, 0.9f, 0.82f);
                mb.Ico(new Vector3(0f, H - 0.012f, 0f), R * 0.86f, new Vector3(1f, 0.12f, 1f), flat, 0.06f);
                mb.Ico(new Vector3(0.015f, H - 0.004f, 0.02f), 0.012f, new Vector3(1f, 0.55f, 1f), new Color(0.96f, 0.45f, 0.65f), 0.04f);
                for (int k = 0; k <= 8; k++)
                {
                    float t = k / 8f * Mathf.PI, hx = Mathf.Cos(t) * (R + 0.002f), hy = H + Mathf.Sin(t) * 0.085f;
                    mb.Box(new Vector3(hx, hy, 0f), new Vector3(0.009f, 0.034f, 0.011f), new Color(0.62f, 0.45f, 0.22f), Quaternion.Euler(0f, 0f, t * Mathf.Rad2Deg));
                }
                return;
            }
            bool full = variant == 1 || variant == 3, lifted = variant >= 2;
            var apple = new Color(0.82f, 0.16f, 0.14f); var bread = new Color(0.82f, 0.6f, 0.32f); var berry = new Color(0.36f, 0.14f, 0.42f); var pear = new Color(0.78f, 0.75f, 0.25f);
            float lift = full ? 0.012f : 0f;
            // the heap: a dome of food above the rim
            mb.Ico(new Vector3(0f, H + 0.004f + lift * 0.5f, 0f), R * 0.9f, new Vector3(1f, 0.35f, 1f), new Color(0.6f, 0.42f, 0.24f), 0.1f);
            mb.Ico(new Vector3(-0.03f, H + 0.022f + lift, 0.02f), 0.02f, Vector3.one, apple, 0.06f);
            mb.Ico(new Vector3(0.025f, H + 0.02f + lift, -0.025f), 0.019f, Vector3.one, apple, 0.06f);
            mb.Ico(new Vector3(0.0f, H + 0.028f + lift, -0.005f), 0.026f, new Vector3(1.4f, 0.8f, 0.9f), bread, 0.06f);
            mb.Ico(new Vector3(0.038f, H + 0.016f + lift, 0.025f), 0.017f, new Vector3(0.9f, 1.2f, 0.9f), pear, 0.06f);
            for (int k = 0; k < 6; k++) mb.Ico(new Vector3(-0.045f + k * 0.006f, H + 0.012f + lift + (k % 2) * 0.005f, -0.035f + k * 0.004f), 0.008f, Vector3.one, berry, 0.1f);
            if (full) { mb.Ico(new Vector3(-0.012f, H + 0.045f, 0.03f), 0.018f, Vector3.one, apple, 0.06f); mb.Ico(new Vector3(0.03f, H + 0.04f, 0.0f), 0.02f, new Vector3(1.3f, 0.8f, 0.9f), bread, 0.06f); }
            // the handle, arching over the top across the basket
            for (int k = 0; k <= 8; k++)
            {
                float t = k / 8f * Mathf.PI, hx = Mathf.Cos(t) * (R + 0.002f), hy = H + Mathf.Sin(t) * 0.085f;
                mb.Box(new Vector3(hx, hy, 0f), new Vector3(0.009f, 0.034f, 0.011f), new Color(0.62f, 0.45f, 0.22f), Quaternion.Euler(0f, 0f, t * Mathf.Rad2Deg)); // along the arch
            }
            var cloth = new Color(0.93f, 0.9f, 0.82f);
            if (!lifted)
            {
                // the cloth over half (the +z half), draped over the rim, a flower on its top
                mb.Ico(new Vector3(0f, H + 0.02f + lift, 0.035f), R * 0.75f, new Vector3(1.1f, 0.45f, 0.62f), cloth, 0.08f);
                mb.Box(new Vector3(0f, H - 0.012f, R + 0.004f), new Vector3(R * 1.2f, 0.03f, 0.006f), cloth);
                mb.Box(new Vector3(0.01f, H + 0.05f + lift, 0.035f), new Vector3(0.004f, 0.025f, 0.004f), new Color(0.35f, 0.55f, 0.2f)); // the stem
                mb.Ico(new Vector3(0.01f, H + 0.066f + lift, 0.035f), 0.014f, new Vector3(1f, 0.55f, 1f), new Color(0.96f, 0.45f, 0.65f), 0.04f);
                mb.Ico(new Vector3(0.01f, H + 0.072f + lift, 0.035f), 0.006f, Vector3.one, new Color(1f, 0.85f, 0.25f), 0.04f);
            }
            else mb.Box(new Vector3(R + 0.02f, 0.012f, 0f), new Vector3(0.04f, 0.022f, 0.07f), cloth, Quaternion.Euler(0f, 10f, 0f)); // folded on the ground beside it
        }

        /// <summary>#693 notes: the pond family's fish, in a round woven fish basket at the shore: tails sticking up out of it and two fish lying
        /// on top. Variant 1-3: that many fish taken (the top ones first, then a tail).</summary>
        static void FishBasketMesh(MeshBuilder mb, int variant)
        {
            const float R = 0.055f, H = 0.06f;
            WovenBasket(mb, Vector3.zero, R, H);
            int taken = Mathf.Clamp(variant, 0, 3);
            var fishA = Palette.Water; var fishB = Palette.WaterDeep; var tailCol = new Color(0.35f, 0.5f, 0.6f);
            // the tails sticking up: a stem and a forked fin each
            int tails = taken >= 3 ? 2 : 3;
            for (int i = 0; i < tails; i++)
            {
                float a = i * 2.1f + 0.4f; var p = new Vector3(Mathf.Cos(a) * 0.025f, H, Mathf.Sin(a) * 0.025f);
                var tilt = Quaternion.Euler(Mathf.Sin(a) * 18f, a * 30f, -Mathf.Cos(a) * 18f);
                mb.Box(p + tilt * new Vector3(0f, 0.012f, 0f), new Vector3(0.008f, 0.024f, 0.006f), i % 2 == 0 ? fishA : fishB, tilt);
                mb.Box(p + tilt * new Vector3(-0.007f, 0.03f, 0f), new Vector3(0.006f, 0.016f, 0.003f), tailCol, tilt * Quaternion.Euler(0f, 0f, 30f));
                mb.Box(p + tilt * new Vector3(0.007f, 0.03f, 0f), new Vector3(0.006f, 0.016f, 0.003f), tailCol, tilt * Quaternion.Euler(0f, 0f, -30f));
            }
            // two fish lying on top
            for (int i = 0; i < 2 - Mathf.Min(2, taken); i++)
            {
                var c = new Vector3(-0.012f + i * 0.026f, H + 0.012f, -0.012f + i * 0.02f);
                var r = Quaternion.Euler(0f, 30f + i * 70f, 0f);
                var saved = mb.M; mb.M = saved * Matrix4x4.TRS(c, r, Vector3.one);
                mb.Ico(Vector3.zero, 0.024f, new Vector3(1.7f, 0.5f, 0.65f), i == 0 ? fishA : fishB, 0.05f);
                mb.Box(new Vector3(-0.045f, 0f, 0f), new Vector3(0.012f, 0.006f, 0.022f), tailCol);
                mb.M = saved;
            }
        }

        /// <summary>#693 notes: the carved name post by the family's door: a short square post, notches up its edges, a carved mark (a ring with a
        /// stroke through it) on its face, a rounded top. Variant bits: 1-3 the kids' height scratches (B54.zz), +4 the mark freshly re-cut.</summary>
        public const float PostH = 0.2f, PostHalf = 0.022f;
        static void NamePostMesh(MeshBuilder mb, int variant)
        {
            var wood = new Color(0.55f, 0.38f, 0.22f); var cut = new Color(0.30f, 0.20f, 0.12f); var fresh = new Color(0.88f, 0.76f, 0.55f);
            bool recut = (variant & 4) != 0; int scratches = variant & 3;
            mb.Box(new Vector3(0f, PostH * 0.5f - 0.01f, 0f), new Vector3(PostHalf * 2f, PostH + 0.02f, PostHalf * 2f), wood);
            mb.Ico(new Vector3(0f, PostH + 0.005f, 0f), PostHalf * 1.15f, new Vector3(1f, 0.55f, 1f), Color.Lerp(wood, Color.black, 0.1f), 0.05f); // the rounded top
            for (int i = 0; i < 5; i++) foreach (float sx in new[] { -1f, 1f })
                mb.Box(new Vector3(sx * PostHalf, 0.05f + i * 0.026f, -PostHalf), new Vector3(0.008f, 0.006f, 0.008f), cut, Quaternion.Euler(0f, 45f, 0f)); // the notches
            var mark = recut ? fresh : cut;
            float my = PostH * 0.72f;
            foreach (float z in new[] { -PostHalf - 0.0015f, PostHalf + 0.0015f }) // both faces, so the mark faces the camera whichever way it stands
            {
                for (int k = 0; k < 8; k++)
                {
                    float a = k / 8f * Mathf.PI * 2f;
                    mb.Box(new Vector3(Mathf.Cos(a) * 0.012f, my + Mathf.Sin(a) * 0.012f, z), new Vector3(0.006f, 0.004f, 0.003f), mark, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f));
                }
                mb.Box(new Vector3(0f, my, z), new Vector3(0.004f, 0.034f, 0.003f), mark, Quaternion.Euler(0f, 0f, 25f));
                for (int i = 0; i < scratches; i++) // the kids' heights, scratched across the face (0.145 u drawn = a kid, a little higher each year)
                    mb.Box(new Vector3(0f, NameMarkY(i), z), new Vector3(PostHalf * 1.8f, 0.0035f, 0.003f), fresh);
            }
        }
        /// <summary>The height scratch <paramref name="i"/> (local y before <see cref="Scale"/>): a kid's head height, the next a little higher.</summary>
        public static float NameMarkY(int i) => 0.145f / Scale + i * 0.012f;

        /// <summary>One flour sack on its own (the carried one: the same piece as the heap's), foot at the origin, before <see cref="Scale"/>.</summary>
        public static Mesh OneSack() { var mb = new MeshBuilder(3801); FlourSack(mb, Vector3.zero); var m = mb.ToMesh("Consequence_OneSack"); m.hideFlags = HideFlags.DontSave; return m; }

        /// <summary>#693 notes (B54.c): the woodpile's free slots for a laid log (local, before <see cref="Scale"/>): three side by side, crosswise, on
        /// top of the top row (the pile's logs run along z).</summary>
        public const int WoodSlots = 3;
        /// <summary>#722 (2.4, B80.f): the woodpile's look bit for the joker's face log (the low bits are the logs laid on top).</summary>
        public const int FaceLook = 16;

        /// <summary>#722 (2.4, B80.f "FaceLog"): a grin carved into a log's ends: two dark eyes and a curved mouth on each end, on the top row's
        /// front log (its ends face local ±z), or once logs are laid across the top, on the top one's ends (±x): "it goes back on top".</summary>
        static void FaceLog(MeshBuilder mb, int laid)
        {
            var cut = new Color(0.30f, 0.20f, 0.12f);
            Vector3 c; bool alongX;
            if (laid > 0) { c = WoodSlot(laid - 1); alongX = true; }
            else { c = new Vector3(-0.0275f, 0.124f, 0f); alongX = false; }
            foreach (float e in new[] { -1f, 1f })
            {
                Vector3 At(float across, float up) => alongX ? c + new Vector3(e * 0.131f, up, across) : c + new Vector3(across, up, e * 0.131f);
                var size = alongX ? new Vector3(0.004f, 0.006f, 0.006f) : new Vector3(0.006f, 0.006f, 0.004f);
                mb.Box(At(-0.008f, 0.007f), size, cut); mb.Box(At(0.008f, 0.007f), size, cut); // the eyes
                for (int i = -2; i <= 2; i++) mb.Box(At(i * 0.0045f, -0.007f + 0.0012f * i * i), size, cut); // the grin
            }
        }
        public static Vector3 WoodSlot(int i) => new Vector3(0f, 0.124f + 0.052f, 0.06f * (Mathf.Clamp(i, 0, 2) - 1)); // across the top, carried crosswise so it never pokes into the pile

        /// <summary>#693 (B54.a/b): where basket <paramref name="i"/> of the three stands (local, before <see cref="Scale"/>), and which are gone when
        /// <paramref name="taken"/> have been taken: the right one first, then the left, then the middle (the back one).</summary>
        public static Vector3 BasketAt(int i) => new Vector3((i - 1) * 0.1f, 0f, i == 1 ? 0.05f : 0f);
        public static bool BasketGone(int i, int taken) => taken >= 1 && i == 2 || taken >= 2 && i == 0 || taken >= 3 && i == 1;

        /// <summary>#693 (B54.b): one basket of the stack on its own (local origin at its foot, before <see cref="Scale"/>): the tower's pieces.</summary>
        public static Mesh OneBasket(int i)
        {
            var mb = new MeshBuilder(3790 + i) { jitter = 0.025f };
            BerryBasket(mb, Vector3.zero, i);
            var m = mb.ToMesh("Consequence_OneBasket_" + i); m.hideFlags = HideFlags.DontSave;
            return m;
        }

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
        /// <summary>#692 (B32.d): charms on a tree not drawn yet: the mourner ties this death's charm during the quiet visit (CampLife sets it).</summary>
        public static int HeldCharms;
        /// <summary>Real seconds a pulse lasts.</summary>
        public const float PulseSeconds = 1.6f;
        /// <summary>#722 (B78.c): things not drawn at their spot for now (the second stone while it is rolled in and stood up: CampLife draws it
        /// in the rollers' hands). CampLife.Stories sets it each frame.</summary>
        public static readonly HashSet<int> HeldBack = new();

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

        /// <summary>2026-11-16 "Bring a pen back": the goat pen's room at the fire (its half-width and the rails' reach, SimCore.PenRoom more than a
        /// small thing's): the boat and every later thing keep clear of it.</summary>
        public const float PenRadius = SimCore.PenRoom + CampSpots.PropRadius;

        /// <summary>#693 notes: the offering basket stands at the foot of the Keeper's tree once it is grown (its line: "The best basket went to
        /// the Keeper's tree"), out from the trunk on the camera's side, a little toward the fire [est 0.2 u]; before that where the sim put it.</summary>
        public const float OfferingFromTrunk = 0.2f;
        bool OfferingAtTree(SimCore sim, out float x, out float z)
        {
            x = z = 0f;
            int cell = sim.State.eco.treeCell;
            if (cell < 0 || !sim.KeeperTreeGrown || !TreeSpotOf(cell, AquariumCiv.Sim.Settle.SiteMap.CXc(cell), AquariumCiv.Sim.Settle.SiteMap.CZc(cell), out var at, out _)) return false;
            var dir = new Vector2(0f, -1f);
            var toFire = new Vector2(fx - at.x, fz - at.z);
            if (toFire.sqrMagnitude > 1e-4f) dir = (dir * 0.7f + toFire.normalized * 0.3f).normalized;
            x = at.x + dir.x * OfferingFromTrunk; z = at.z + dir.y * OfferingFromTrunk;
            return true;
        }

        /// <summary>The goat pen stands where the sim put it, moved out of every earlier thing's widened room (as the boat's spot).</summary>
        void PenSpot(ConsequenceProp p, System.Func<float, float, bool> onPath, out float px, out float pz)
        {
            wide.Clear();
            foreach (var d in spots) wide.Add(new CampSpots.Disc(d.x, d.z, d.r + PenRadius - CampSpots.PropRadius));
            CampSpots.Spot(fx, fz, rackAngle, LifePlanner.HideFrameDistance, p.x, p.z, wide, out px, out pz, fishAngle, onPath);
        }

        /// <summary>B3 tester fixes: the room a thing keeps round its middle at the fire (its footprint and half the margin, never less than the
        /// old one size for all, so the small things stand as they did).</summary>
        static float RoomOf(string kind) => Mathf.Max(CampSpots.PropRadius, ConsequenceFootprint.Radius(kind) + ConsequenceFootprint.Margin * 0.5f);

        /// <summary>B3 tester fixes: a thing of this kind at (x, z) would stand on a standing lot (a house, a field, a barn, a yard).</summary>
        static bool UnderLot(SimCore sim, string kind, float x, float z)
        {
            var st = sim.Settlement;
            if (st == null) return false;
            ConsequenceFootprint.Cells(kind, x, z, 0f, lotCells);
            foreach (int c in lotCells)
            {
                int id = st.Map.lot[c];
                if (id == 0) continue;
                foreach (var l in st.Lots) if (l.id == id) { if (!l.retired) return true; break; }
            }
            return false;
        }
        static readonly List<int> lotCells = new();

        /// <summary>B3 tester fixes (4, view): where a thing will stand this frame, as a disc of its room, before the loop places it (r = -1: it
        /// stands nowhere at the fire's ground: not placed, not shown yet, a charm on a tree, a second feather).</summary>
        bool AheadDisc(SimCore sim, ConsequenceProp q, ConsequenceProp pole, bool haveFire, out CampSpots.Disc d)
        {
            d = default;
            if (!haveFire || !q.placed || !ReplayGate.Shows(q.placedMonth)) return false;
            if (q.kind == ConsequenceKind.Feather && q != pole) return false;
            if (q.anchor == PropAnchor.Tree && q.treeCell >= 0) return false;
            float x = q.x, z = q.z;
            if (q.anchor == PropAnchor.Fire && settledSpot.TryGetValue(q.id, out var s)) { x = s.x; z = s.y; }
            else if (q.kind == ConsequenceKind.OfferingBasket && OfferingAtTree(sim, out float ox, out float oz)) { x = ox; z = oz; }
            float r = q.kind == ConsequenceKind.ShoreBoat ? BoatRadius : q.kind == ConsequenceKind.GoatPen ? PenRadius : RoomOf(q.kind);
            d = new CampSpots.Disc(x, z, r);
            return true;
        }

        /// <summary>B3 tester fixes (4, view): would a thing of room <paramref name="own"/> at (x, z) lie inside one already stood this frame or
        /// one stood after it (index past <paramref name="self"/>)?</summary>
        bool InsideAThing(float x, float z, float own, int self)
        {
            foreach (var d in spots) if (new Vector2(x - d.x, z - d.z).magnitude < d.r + own - CampSpots.PropRadius) return true;
            for (int j = self + 1; j < ahead.Count; j++)
                if (ahead[j].r > 0f && new Vector2(x - ahead[j].x, z - ahead[j].z).magnitude < ahead[j].r + own - CampSpots.PropRadius) return true;
            return false;
        }
        readonly List<CampSpots.Disc> ahead = new();

        /// <summary>#714 round 2: the boat lines up with the box's lines (the glass), its long side across the way to the fire, never a random yaw.</summary>
        float BoatYaw(float px, float pz) => Mathf.Abs(px - fx) > Mathf.Abs(pz - fz) ? 90f : 0f;
        readonly List<CampSpots.Disc> fixedScratch = new();
        readonly Dictionary<int, Made> made = new();
        readonly Dictionary<int, float> pulses = new();
        float flutterAt = -99f, flutterAmp = 1f;

        // ---- #693 (B54): how a use has a thing now (view-only, not saved [est]): its look, a jolt, a tilt (the roof bird after a storm)
        static readonly Dictionary<int, int> looks = new();
        static readonly Dictionary<int, float> jolts = new();
        static readonly Dictionary<int, (float from, float to, float at)> tilts = new();
        /// <summary>#693: thing <paramref name="propId"/> is drawn as <see cref="ConsequenceMeshes.Build"/>'s variant <paramref name="variant"/>.</summary>
        public static void SetLook(int propId, int variant) { if (variant == 0) looks.Remove(propId); else looks[propId] = variant; }
        public static int LookOf(int propId) => looks.TryGetValue(propId, out int v) ? v : 0;
        /// <summary>#693: a small wobble and squash (a basket set down, a tag tapped, a drum beat).</summary>
        public static void Jolt(int propId) => jolts[propId] = Time.time;
        /// <summary>#693 (B54.p): tipped over by <paramref name="deg"/> now (the roof bird after a storm).</summary>
        public static void Tip(int propId, float deg) => tilts[propId] = (deg, deg, Time.time);
        /// <summary>#693 (B54.p): eased back upright over <see cref="UprightSeconds"/>.</summary>
        public static void Upright(int propId) { float now = TiltOf(propId); tilts[propId] = (now, 0f, Time.time); }
        public static float TiltOf(int propId)
        {
            if (!tilts.TryGetValue(propId, out var t)) return 0f;
            float k = Mathf.Clamp01((Time.time - t.at) / UprightSeconds);
            return Mathf.Lerp(t.from, t.to, k * k * (3f - 2f * k));
        }
        public const float UprightSeconds = 0.3f;
        static readonly Dictionary<int, float> swings = new();
        /// <summary>#693 retakes (B54.zz): a tapped peg swings, 15 deg dying away over 1.2 s.</summary>
        public static void Swing(int propId) => swings[propId] = Time.time;
        static float SwingOf(int propId)
        {
            if (!swings.TryGetValue(propId, out float st)) return 0f;
            float a = Time.time - st;
            if (a < 0f || a > 1.2f) { swings.Remove(propId); return 0f; }
            return 15f * Mathf.Sin(a * 2.5f * Mathf.PI * 2f) * Mathf.Exp(-a / 0.4f);
        }
        static Quaternion JoltOf(int propId, out float squash)
        {
            squash = 1f;
            if (!jolts.TryGetValue(propId, out float jt)) return Quaternion.identity;
            float ja = Time.time - jt;
            if (ja < 0f || ja > 0.35f) { jolts.Remove(propId); return Quaternion.identity; }
            float e = Mathf.Exp(-ja / 0.12f);
            squash = 1f - 0.05f * e;
            return Quaternion.Euler(Mathf.Sin(ja * 40f) * 4f * e, 0f, Mathf.Sin(ja * 31f) * 3f * e);
        }
        /// <summary>#693 tests: forget every use's look (a new box).</summary>
        public static void ResetLooks() { looks.Clear(); jolts.Clear(); tilts.Clear(); swings.Clear(); }

        /// <summary>#692 (B31.a/b): a new feather tied on, a kid's touch: the pole jolts and its feathers flutter.</summary>
        public static void FlutterPole(float amp = 1f)
        {
            if (Instance == null) return;
            Instance.flutterAt = Time.unscaledTime; Instance.flutterAmp = amp;
        }

        /// <summary>B3 tester fixes: the things standing on the ground now (not the charms hanging on a tree): id, kind, spot (box x, z) and the
        /// yaw drawn, for PeopleView's solids and the tests. No allocation.</summary>
        public void Footprints(List<(int id, string kind, Vector2 at, float yaw)> into)
        {
            into.Clear();
            var list = Sim?.State.consequenceProps;
            if (list == null) return;
            foreach (var p in list)
            {
                if (!p.placed || p.anchor == PropAnchor.Tree && p.treeCell >= 0 || !made.TryGetValue(p.id, out var m) || m.t == null || !m.t.gameObject.activeInHierarchy) continue;
                var lp = m.t.localPosition;
                into.Add((p.id, p.kind, new Vector2(lp.x, lp.z), m.t.localEulerAngles.y));
            }
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
            ResetLooks(); // #693
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
            foreach (var p in list) if (p.kind != ConsequenceKind.ShoreBoat && p.kind != ConsequenceKind.GoatPen) ordered.Add(p);
            foreach (var p in list) if (p.kind == ConsequenceKind.GoatPen) ordered.Add(p); // the goat pen: after the small things, before the boat
            foreach (var p in list) if (p.kind == ConsequenceKind.ShoreBoat) ordered.Add(p);
            // B3 tester fixes (4, view): a thing moved off its sim spot must keep clear of the things stood after it too (the long table moved
            // onto the goat pen and the baskets, which come later in the list): their spots, as they will stand, go in its way
            ahead.Clear();
            foreach (var q in ordered) ahead.Add(AheadDisc(sim, q, pole, haveFire, out var dq) ? dq : new CampSpots.Disc(0f, 0f, -1f));
            for (int oi = 0; oi < ordered.Count; oi++)
            {
                var p = ordered[oi];
                if (!p.placed) continue;
                if (!ReplayGate.Shows(p.placedMonth)) // #669: a replay shows it from the month it was placed
                {
                    if (made.TryGetValue(p.id, out var early) && early.t != null && early.t.gameObject.activeSelf) early.t.gameObject.SetActive(false);
                    continue;
                }
                if (HeldBack.Contains(p.id)) // #722: the second stone is still being rolled in
                {
                    if (made.TryGetValue(p.id, out var held) && held.t != null && held.t.gameObject.activeSelf) held.t.gameObject.SetActive(false);
                    continue;
                }
                if (p.kind == ConsequenceKind.Feather && p != pole)
                {
                    if (made.TryGetValue(p.id, out var gone)) { Kill(gone.mesh); if (gone.t != null) Kill(gone.t.gameObject); made.Remove(p.id); }
                    continue;
                }
                float px = p.x, pz = p.z;
                if (p.kind == ConsequenceKind.OfferingBasket && OfferingAtTree(sim, out float ox, out float oz)) { px = ox; pz = oz; } // #693 notes: "The best basket went to the Keeper's tree"
                float own = RoomOf(p.kind); // B3 tester fixes: its footprint's room, not one size for all
                if (haveFire && p.anchor == PropAnchor.Fire)
                {
                    // #700: never on a path; one that a path grows over (or a moved rack reaches) moves at night, not in front of the player.
                    // B3 tester fixes: never on a lot either (a house, a field, a barn), and one a lot was laid over moves at once
                    var onPath0 = RackSites.PathTest(sim);
                    System.Func<float, float, bool> onPath = (x, z) => onPath0(x, z) || UnderLot(sim, p.kind, x, z);
                    if (p.kind == ConsequenceKind.ShoreBoat) BoatSpot(planner, onPath, out px, out pz);
                    else if (p.kind == ConsequenceKind.GoatPen) PenSpot(p, onPath, out px, out pz);
                    else
                    {
                        wide.Clear();
                        foreach (var d in spots) wide.Add(new CampSpots.Disc(d.x, d.z, d.r + own - CampSpots.PropRadius));
                        for (int j = oi + 1; j < ahead.Count; j++) if (ahead[j].r > 0f) wide.Add(new CampSpots.Disc(ahead[j].x, ahead[j].z, ahead[j].r + own - CampSpots.PropRadius));
                        CampSpots.Spot(fx, fz, rackAngle, LifePlanner.HideFrameDistance, p.x, p.z, wide, out px, out pz, fishAngle, onPath);
                    }
                    if (settledSpot.TryGetValue(p.id, out var was) && (was.x != px || was.y != pz))
                    {
                        fixedScratch.Clear(); CampSpots.FireSide(fx, fz, fixedScratch);
                        bool stillOk = CampSpots.Clear(fx, fz, rackAngle, LifePlanner.HideFrameDistance, was.x, was.y, fixedScratch, spots, fishAngle, onPath);
                        // B3 tester fixes (4, view): a spot that now lies inside another thing is left at once (a path moves it only at night)
                        bool onAThing = InsideAThing(was.x, was.y, own, oi);
                        if (!UnderLot(sim, p.kind, was.x, was.y) && !onAThing && (stillOk || !RackSites.IsNight(sim))) { px = was.x; pz = was.y; }
                    }
                    settledSpot[p.id] = new Vector2(px, pz);
                    spots.Add(new CampSpots.Disc(px, pz, p.kind == ConsequenceKind.ShoreBoat ? BoatRadius : p.kind == ConsequenceKind.GoatPen ? PenRadius : own));
                }
                else if (haveFire) spots.Add(new CampSpots.Disc(px, pz, own));
                // #635: charms on a tree hang from the drawn crown (no post, not scaled up); a tree not drawn yet: wait a frame
                bool onTree = p.anchor == PropAnchor.Tree && p.treeCell >= 0;
                Vector3 treeAt = default; float treeR = 0f;
                if (onTree && !TreeSpotOf(p.treeCell, p.x, p.z, out treeAt, out treeR))
                {
                    if (made.TryGetValue(p.id, out var hide) && hide.t != null) hide.t.gameObject.SetActive(false);
                    continue;
                }
                int charms = p.anchor == PropAnchor.Tree ? Mathf.Max(0, sim.CharmsOn(p) - (onTree ? HeldCharms : 0)) : 0; // #692 B32.d: this death's charm waits to be tied
                string key = KeyOf(p, charms, treeR);
                int[] featherSlots = p == pole ? SimCore.PoleSlots(sim.PoleFeathers) : null; // #692 (B31.a): the pole fills up over the years
                if (featherSlots != null && featherSlots.Length > 0) key += ":f" + string.Join("", featherSlots);
                int look = LookOf(p.id); if (look != 0) key += ":v" + look; // #693
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
                    else if (p.kind == ConsequenceKind.NameStone && p.id == sim.StoryMarks.secondStoneId) mesh = ConsequenceMeshes.SecondStone(look); // #722: its own shape, its own rows
                    else mesh = ConsequenceMeshes.Build(p.kind, look);
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
                var jq = JoltOf(p.id, out float jSquash); // #693 (B54): a jolt (4 deg, 0.35 s) and a little squash
                if (onTree)
                {
                    m.t.localScale = Vector3.one * (s / ConsequenceMeshes.Scale);
                    m.t.SetLocalPositionAndRotation(treeAt + Vector3.up * lift, Quaternion.Euler(0f, p.yaw, 0f) * (jq == Quaternion.identity ? jq : Quaternion.Euler(0f, (1f - jSquash) * 600f, 0f) * jq)); // the charms sway and turn
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
                tilt *= jq; if (jSquash < 1f) m.t.localScale = new Vector3(s, s * jSquash, s); // #693: a use's jolt
                if (swings.ContainsKey(p.id)) tilt *= Quaternion.AngleAxis(SwingOf(p.id), new Vector3(1f, 0f, -1f).normalized);
                if (tilts.ContainsKey(p.id)) tilt *= Quaternion.AngleAxis(TiltOf(p.id), new Vector3(1f, 0f, 1f).normalized); // leaning half across, half along: it shows from any side
                float yaw = p.kind == ConsequenceKind.GoatPen ? Mathf.Round(p.yaw / 90f) * 90f // 2026-11-16 "Bring a pen back": square to the glass, as the boat
                    : p.kind != ConsequenceKind.ShoreBoat ? p.yaw : haveFire && p.anchor == PropAnchor.Fire ? BoatYaw(px, pz) : Mathf.Round(p.yaw / 90f) * 90f; // #714 round 2: square to the glass (old saves too)
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
