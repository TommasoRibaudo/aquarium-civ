using System.Collections.Generic;
using AquariumCiv.Sim.Eco;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>#608: where the rabbit prints of one track cell lie. A short trail of hops, each hop two long hind prints side by side
    /// ahead and two small fore prints one behind the other behind them, as a rabbit lands. Length, bend, spacing and heading are seeded
    /// per cell, so no two cells show the same trail. Pure math (the view turns each print into a flat, pressed-in oval).</summary>
    public static class RabbitTrackLayout
    {
        public struct Print
        {
            public Vector2 at;
            public float yaw;   // radians, direction of travel (sin, cos) as RabbitView uses it
            public float rx, rz; // half width, half length
            public bool hind;
            public int hop;
        }

        public const float HindRx = 0.0055f, HindRz = 0.013f, ForeRx = 0.004f, ForeRz = 0.0075f;
        public const float HindSide = 0.016f;     // each hind print this far off the trail's line
        public const float HopMin = 0.075f, HopMax = 0.115f;

        /// <summary><paramref name="keep"/> (0..1) thins an old trail: fewer hops are left of it.</summary>
        public static void Build(float cx, float cz, uint worldSeed, uint cell, float keep, List<Print> into)
        {
            uint r = RabbitHerd.H(worldSeed, cell, 77);
            float F(uint k) => RabbitHerd.F(r, k, 3);
            int hops = Mathf.Max(2, Mathf.RoundToInt((3f + F(1) * 3f) * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(keep)))); // 3..6 hops, fewer when old
            float heading = F(2) * Mathf.PI * 2f;
            float bend = (F(3) - 0.5f) * 0.7f;                // radians turned per hop: a curve to either side
            float spacing = Mathf.Lerp(HopMin, HopMax, F(4));
            // laid from the cell's middle, then the whole trail is moved so its prints centre on it (a bent trail's chord is not
            // its first heading: centring on that let a long curved one reach past the cell)
            var pos = new Vector2(cx, cz);
            int first = into.Count;
            for (int h = 0; h < hops; h++)
            {
                var dir = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
                var side = new Vector2(dir.y, -dir.x);
                float wob = (RabbitHerd.F(r, (uint)(10 + h), 5) - 0.5f) * 0.012f; // no hop quite on the line
                var c = pos + side * wob;
                // fore prints trail behind, one behind the other, a little off the line
                into.Add(new Print { at = c - dir * 0.026f + side * 0.004f, yaw = heading, rx = ForeRx, rz = ForeRz, hind = false, hop = h });
                into.Add(new Print { at = c - dir * 0.008f - side * 0.004f, yaw = heading, rx = ForeRx, rz = ForeRz, hind = false, hop = h });
                // hind prints land ahead of the fore, side by side
                for (int s = -1; s <= 1; s += 2)
                    into.Add(new Print { at = c + dir * 0.022f + side * (s * HindSide), yaw = heading + s * 0.12f, rx = HindRx, rz = HindRz, hind = true, hop = h });
                pos += dir * spacing;
                heading += bend + (RabbitHerd.F(r, (uint)(30 + h), 5) - 0.5f) * 0.25f;
            }
            var mid = Vector2.zero;
            for (int i = first; i < into.Count; i++) mid += into[i].at;
            mid /= Mathf.Max(1, into.Count - first);
            var shift = new Vector2(cx, cz) - mid;
            for (int i = first; i < into.Count; i++) { var p = into[i]; p.at += shift; into[i] = p; }
        }
    }
}
