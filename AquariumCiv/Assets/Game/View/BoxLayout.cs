using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>What a placed piece is. The view dresses each kind per era (a camp's Home is a tent, a village's a hut or
    /// a house, a town's a jettied house). Kinds are looks, not sim roles: the sim-to-view mapping lives in #33.</summary>
    public enum PieceKind
    {
        Home, Fire, Well, Woodpile, DryingRack, LeanTo, Bench, Lantern, Granary, Crates, LogSeat, Stall,
        Windmill, Lumbermill, Market, Tavern, Church, StandingStones, Watermill, Smithy, Dyer,
    }

    public enum RoofStyle { Auto, Thatch, Terracotta, Slate }
    public enum PathTier { Trail, Path, Road }
    public enum CropKind { Wheat, Sprout, Fallow, Gathering }
    public enum ClearingKind { Dirt, Green, Cobble }

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
    /// Everything the view draws inside the box, as plain data. The sim side (#33) fills it from SimState; until then a
    /// temporary demo layout does (Demo/TemporaryDemoLayout.cs). <see cref="BoxView.Rebuild"/> turns it into meshes.
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
        /// <summary>Scatter trees, bushes, rocks, grass and flowers into the free ground (away from pieces, fields, paths).</summary>
        public bool autoNature = true;
        public float natureDensity = 1f;
        public bool reeds = true;

        public void Clear()
        {
            pieces.Clear(); fields.Clear(); paths.Clear(); clearings.Clear(); trees.Clear(); villagers.Clear();
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
