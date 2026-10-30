using AquariumCiv.Sim;
using AquariumCiv.View;
using UnityEngine;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Look checks for the town kit (#34), from `unity command script eval`: <see cref="Sheet"/> lays every town piece
    /// out in rows on the slice's box (edit mode, nothing saved) and renders it at phone size; <see cref="Restore"/> puts
    /// the sim's layout back. Pair with <see cref="SliceSceneBuilder.Shot"/> and <see cref="NightLamps.DebugSetLevel"/>.
    /// </summary>
    public static class TownKitShots
    {
        /// <summary>Fills the box with a kit sheet of <paramref name="era"/>'s pieces (default: the town) and rebuilds.</summary>
        public static string Sheet(Era era = Era.Town)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            if (box == null) return "no BoxView";
            var L = box.layout;
            L.Clear();
            L.autoNature = false;
            L.Clearing(new Vector2(0.2f, 0.0f), 0.55f, era == Era.Town ? ClearingKind.Cobble : ClearingKind.Green);
            int s = 1;
            PieceData P(PieceKind k, float x, float z, float w = 0.5f, float d = 0.45f, int variant = 0)
            {
                var p = L.Add(k, x, z, 0f, w, d, era, 101 * s++ + 7);
                p.variant = variant;
                return p;
            }
            // Back row: the big silhouettes.
            P(PieceKind.Windmill, -1.9f, 1.2f, 0.36f, 0.36f);
            P(PieceKind.Guildhall, -1.05f, 1.15f, 0.52f, 0.48f);
            P(PieceKind.Church, -0.2f, 1.2f, 0.44f, 0.72f);
            P(PieceKind.Market, 0.65f, 1.15f, 0.56f, 0.42f);
            P(PieceKind.Watermill, 1.5f, 1.2f, 0.46f, 0.4f);
            // Middle row: workshops and trades.
            P(PieceKind.Workshop, -2.0f, 0.45f, 0.42f, 0.4f, 1);
            P(PieceKind.Workshop, -1.4f, 0.45f, 0.42f, 0.4f, 2);
            P(PieceKind.Workshop, -0.8f, 0.45f, 0.42f, 0.4f, 3);
            P(PieceKind.Smithy, 1.0f, 0.45f, 0.42f, 0.4f);
            P(PieceKind.Dyer, 1.6f, 0.45f, 0.44f, 0.44f);
            // The square: fountain and stalls, then homes, shop, tavern, lumber mill, kiln.
            P(PieceKind.Well, 0.2f, 0.0f, 0.3f, 0.3f);
            for (int k = 0; k < 3; k++) P(PieceKind.Stall, -0.15f + k * 0.35f, -0.35f, 0.3f, 0.22f);
            P(PieceKind.Home, -2.0f, -0.4f, 0.42f, 0.46f).storeys = 2;
            P(PieceKind.Home, -1.45f, -0.4f, 0.38f, 0.42f).storeys = 1;
            P(PieceKind.Tavern, -0.85f, -0.4f, 0.5f, 0.42f);
            P(PieceKind.Shop, 1.0f, -0.4f, 0.38f, 0.36f);
            P(PieceKind.Lumbermill, 1.6f, -0.45f, 0.46f, 0.42f);
            P(PieceKind.Kiln, -1.6f, -1.15f, 0.4f, 0.4f);
            P(PieceKind.Quarry, -0.9f, -1.15f, 0.42f, 0.42f);
            P(PieceKind.Lantern, 0.6f, 0.1f, 0.1f, 0.1f);
            L.CobbledPath(PathTier.Path, new Vector2(-2.4f, 0.05f), new Vector2(-0.4f, 0.05f));
            L.Path(PathTier.Road, new Vector2(0.75f, 0.05f), new Vector2(2.0f, 0.05f));
            L.Path(PathTier.Path, new Vector2(-0.3f, -0.75f), new Vector2(1.9f, -0.75f));
            box.Rebuild();
            return box.lastBuildInfo;
        }

        /// <summary>Gives the box its sim layout back.</summary>
        public static string Restore()
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            if (box == null) return "no BoxView";
            var src = box.GetComponent<IBoxLayoutSource>();
            src?.FillLayout(box.layout);
            box.Rebuild();
            return box.lastBuildInfo;
        }
    }
}
