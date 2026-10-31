using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>What a placed piece is. The view dresses each kind per era (a camp's Home is a tent, a village's a hut or
    /// a house, a town's a jettied house). Kinds are looks, not sim roles: the sim-to-view mapping is <see cref="SimLayout"/>.</summary>
    public enum PieceKind
    {
        Home, Fire, Well, Woodpile, DryingRack, LeanTo, Bench, Lantern, Granary, Crates, LogSeat, Stall,
        Windmill, Lumbermill, Market, Tavern, Church, StandingStones, Watermill, Smithy, Dyer,
        // #33: the rest of the sim's lot kinds and the Keeper's shapings (town ones stay placeholders until M6).
        Shop, Workshop, Guildhall, Barn, Gathering, Orchard, Herd, Jetty, Quarry, Kiln, Outcrop,
        /// <summary>The town's bakery (#112): chimney, loaf sign. Appended last (saved as an int).</summary>
        Bakery,
    }

    public enum RoofStyle { Auto, Thatch, Terracotta, Slate }
    public enum PathTier { Trail, Path, Road }
    public enum CropKind { Wheat, Sprout, Fallow, Gathering, Pasture }
    /// <summary>Dirt: the camp's trodden circle. Green, Cobble: the village green, the town square. Yard: a family's
    /// shared yard (trodden grass). Meadow: commons kept open. Flowers: a dye meadow in bloom. Meadow and Flowers leave
    /// room for grass and flowers.</summary>
    public enum ClearingKind { Dirt, Green, Cobble, Yard, Meadow, Flowers }

    /// <summary>
    /// A piece on a lot: centre (x, z) on the ground, yaw in degrees (local -z = the front, the door side, faces the
    /// path), lot size w × d in box units. <see cref="seed"/> keeps a piece's random details stable across rebuilds (use
    /// the building id).
    /// </summary>
    [Serializable]
    public class PieceData
    {
        public PieceKind kind;
        public float x, z, yaw, w = 0.5f, d = 0.45f;
        public Era era = Era.Village;
        public RoofStyle roof = RoofStyle.Auto;
        /// <summary>Homes: 1 or 2 (town houses). 0 = pick from the era.</summary>
        public int storeys;
        public bool chimney;
        public int seed;
        /// <summary>A per-kind look switch (barn or shed; an outcrop's deposit: 0 stone, 1 clay, 2 ore; a timber yard).</summary>
        public int variant;
        /// <summary>0..1: how much of the piece is there (the time-lapse grows a new building in and shrinks a cleared one away, #78).</summary>
        public float grow = 1f;

        public Vector2 Pos => new(x, z);
    }

    /// <summary>A field as a ground polygon (counter- or clockwise), crop rows running along <see cref="rowYaw"/>.</summary>
    [Serializable]
    public class FieldData
    {
        public Vector2[] polygon;
        public CropKind crop = CropKind.Wheat;
        public float rowYaw;
        public bool fence = true;
        /// <summary>Index of the polygon edge left open as the gate (-1 = fully fenced).</summary>
        public int gateEdge = 0;
        public int seed;
    }

    /// <summary>A worn or laid path: a polyline painted into the ground; the tier sets its width and surface.</summary>
    [Serializable]
    public class PathData
    {
        public Vector2[] points;
        public PathTier tier = PathTier.Path;
        /// <summary>Laid with cobbles whatever its tier (#34; #90 spreads cobbles along the main path). A town road is
        /// always cobbled; this cobbles a trail or path too, at its own width.</summary>
        public bool cobbled;
    }

    /// <summary>An open patch of ground: the camp's trodden circle, a village green, a town square.</summary>
    [Serializable]
    public class ClearingData
    {
        public Vector2 centre;
        public float radius = 0.5f;
        public ClearingKind kind = ClearingKind.Dirt;
    }

    [Serializable]
    public class TreeData
    {
        public Vector2 pos;
        public float scale = 1f;
        public bool pine;
    }

    /// <summary>A peg doll to place (until the activity planner, #29/#30, moves them).</summary>
    [Serializable]
    public class VillagerData
    {
        public Vector2 pos;
        public float yaw;
        public bool kid;
        /// <summary>Camp people wear hide tones with one bright accent (headband or sash).</summary>
        public Era era = Era.Village;
        public int seed;
        /// <summary>-1 = none; otherwise an index into <see cref="Palette.Accents"/> (role accent or camp headband).</summary>
        public int accent = -1;
        public PropKind prop = PropKind.None;
        public PropSlot slot = PropSlot.Hand;
    }

    /// <summary>
    /// Everything the view draws inside the box, as plain data. <see cref="SimLayout"/> fills it from the live sim (#33);
    /// the demo layout (Demo/TemporaryDemoLayout.cs) is kept for tests. <see cref="BoxView.Rebuild"/> turns it into meshes.
    /// </summary>
    [Serializable]
    public class BoxLayout
    {
        public List<PieceData> pieces = new();
        public List<FieldData> fields = new();
        public List<PathData> paths = new();
        public List<ClearingData> clearings = new();
        public List<TreeData> trees = new();
        public List<VillagerData> villagers = new();
        /// <summary>Dug channels: short polylines (cell centre to cell centre) carved into the ground as water strips.</summary>
        public List<Vector2[]> canals = new();
        /// <summary>Scatter trees, bushes, rocks, grass and flowers into the free ground (away from pieces, fields, paths).</summary>
        public bool autoNature = true;
        /// <summary>With <see cref="autoNature"/>: also scatter trees. Off when the sim's forest gives the trees.</summary>
        public bool autoTrees = true;
        public float natureDensity = 1f;
        public bool reeds = true;

        public void Clear()
        {
            pieces.Clear(); fields.Clear(); paths.Clear(); clearings.Clear(); trees.Clear(); villagers.Clear(); canals.Clear();
            autoNature = true; autoTrees = true;
        }

        // Builders, so callers can write layout.House(...).Fire(...) without object initialisers.
        public PieceData Add(PieceKind kind, float x, float z, float yaw = 0f, float w = 0.5f, float d = 0.45f, Era era = Era.Village, int seed = 0)
        {
            var p = new PieceData { kind = kind, x = x, z = z, yaw = yaw, w = w, d = d, era = era, seed = seed == 0 ? pieces.Count + 1 : seed };
            pieces.Add(p);
            return p;
        }

        public PieceData House(float x, float z, float yaw, float w, float d, Era era, RoofStyle roof = RoofStyle.Auto, int storeys = 0, bool chimney = false, int seed = 0)
        {
            var p = Add(PieceKind.Home, x, z, yaw, w, d, era, seed);
            p.roof = roof; p.storeys = storeys; p.chimney = chimney;
            return p;
        }

        public FieldData Field(Vector2[] polygon, CropKind crop, float rowYaw = 0f, bool fence = true, int gateEdge = 0)
        {
            var f = new FieldData { polygon = polygon, crop = crop, rowYaw = rowYaw, fence = fence, gateEdge = gateEdge, seed = fields.Count + 11 };
            fields.Add(f);
            return f;
        }

        public PathData Path(PathTier tier, params Vector2[] points)
        {
            var p = new PathData { points = points, tier = tier };
            paths.Add(p);
            return p;
        }

        /// <summary>A cobbled path (any tier): see <see cref="PathData.cobbled"/>.</summary>
        public PathData CobbledPath(PathTier tier, params Vector2[] points)
        {
            var p = Path(tier, points);
            p.cobbled = true;
            return p;
        }

        public ClearingData Clearing(Vector2 centre, float radius, ClearingKind kind)
        {
            var c = new ClearingData { centre = centre, radius = radius, kind = kind };
            clearings.Add(c);
            return c;
        }

        public VillagerData Villager(Vector2 pos, float yaw, Era era, int seed, bool kid = false, int accent = -1, PropKind prop = PropKind.None, PropSlot slot = PropSlot.Hand)
        {
            var v = new VillagerData { pos = pos, yaw = yaw, era = era, seed = seed, kid = kid, accent = accent, prop = prop, slot = slot };
            villagers.Add(v);
            return v;
        }
    }
}
