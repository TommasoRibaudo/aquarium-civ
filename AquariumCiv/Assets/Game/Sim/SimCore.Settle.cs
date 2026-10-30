using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// The settlement layer's hooks into the sim (M2, #32; Sim/Settle). SimCore decides <i>what</i> is built; at the end
    /// of every month <see cref="Settlement"/> decides <i>where</i>, and stores it in <see cref="SimState.settle"/>.
    /// Placement draws no dice and the economy never reads placement results, so turning it off
    /// (<see cref="SimConfig.settle"/>) leaves the economy exactly as it is (SettleTests).
    /// </summary>
    public partial class SimCore
    {
        Settlement settlement;

        /// <summary>Where everything stands, and the queries for the view and the planner. Null when settling is off.
        /// Built from the save on first use; a save from before placement is migrated from its slots once.</summary>
        public Settlement Settlement
        {
            get
            {
                if (!Config.settle) return null;
                return settlement ??= new Settlement(this);
            }
        }

        /// <summary>The box's land preset: the save's once settled, else the config's.</summary>
        public int TerrainPreset => State.settle != null && State.settle.version > 0 ? State.settle.terrain : Config.terrainPreset;

        void SettleStep()
        {
            if (Config.settle) Settlement.Step();
        }

        /// <summary>Is the spot by (or in) the pond? From the terrain alone, so the economy may read it (a herd flicked
        /// into the water is a shoal of fish; research 10).</summary>
        public static bool WetSpot(TerrainSpec t, float x, float z)
        {
            Terrain.Land(t, x, z, out _, out _, out float pondDist);
            return pondDist < 0.35f;
        }

        /// <summary>What an outcrop pushes up, by the land (research 10): clay by the water, ore deep in a slope, stone
        /// on a hill (and anywhere else). From the terrain alone.</summary>
        public static string Deposit(TerrainSpec t, float x, float z)
        {
            Terrain.Land(t, x, z, out float h, out float slope, out float pondDist);
            if (pondDist < 0.5f) return "clay";
            if (slope > 0.35f && h < Terrain.G + 0.25f) return "ore";
            return "stone";
        }

        bool WetSpot(Nudge n) => WetSpot(TerrainSpec.Preset(TerrainPreset), n.x, n.z);
        string Deposit(Nudge n) => Deposit(TerrainSpec.Preset(TerrainPreset), n.x, n.z);

        // ---------- debug (for the debug panel; CLAUDE.md "debug panel stays current") ----------
        /// <summary>Debug: build a building of this type now (no wood, no cap), so the layout can be filled or tested.</summary>
        public Building DebugBuild(string type)
        {
            var s = State;
            var b = new Building { id = s.NextId(), type = type, slot = -1, foundedMonth = s.month };
            s.buildings.Add(b);
            if (!s.foundedTypes.Contains(type)) s.foundedTypes.Add(type);
            Settlement?.PlacePending();
            return b;
        }
    }
}
