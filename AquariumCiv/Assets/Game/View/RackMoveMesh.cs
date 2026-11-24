using System;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #700 (B27): the rack as the take-down and set-up beat draws it: the whole rack standing (the camp's own pieces, with the cargo coming off or
    /// going back one at a time), or its three parts (the ridge pole and two A-frames) lifted, carried, planted and dropped in
    /// (<see cref="RackLook"/>), and the four holes the frames leave. Used by <see cref="RackMoveView"/> in the game and by AnimLab.
    /// </summary>
    public static class RackMoveMesh
    {
        static readonly Color Wood = new Color(0.40f, 0.28f, 0.16f), WoodDark = new Color(0.28f, 0.19f, 0.11f), Cord = new Color(0.93f, 0.86f, 0.68f), Hole = new Color(0.12f, 0.08f, 0.05f);

        /// <summary>Everything the beat shows of the rack at <paramref name="look"/>. <paramref name="ground"/> gives the ground height at (x, z).</summary>
        public static void Build(MeshBuilder mb, RackMovePlan plan, in RackLook look, Func<float, float, float> ground, int seed)
        {
            float sj = mb.jitter, ss = mb.sway; mb.jitter = 0f; mb.sway = 0f;
            if (look.oldWhole >= 0) Whole(mb, plan, false, look.oldWhole, ground, seed);
            if (look.newWhole >= 0) Whole(mb, plan, true, look.newWhole, ground, seed);
            if (look.oldWhole < 0 && look.newWhole < 0)
            {
                Ridge(mb, plan, look.ridge, ground);
                Frame(mb, plan, look.frameA, ground);
                Frame(mb, plan, look.frameB, ground);
            }
            mb.M = Matrix4x4.identity;
            mb.jitter = sj; mb.sway = ss;
        }

        /// <summary>The four holes at the old spot (small dark squares on the ground).</summary>
        public static void Holes(MeshBuilder mb, System.Collections.Generic.List<(float x, float z)> holes, Func<float, float, float> ground)
        {
            float sj = mb.jitter; mb.jitter = 0f;
            mb.M = Matrix4x4.identity;
            foreach (var (x, z) in holes) mb.Box(new Vector3(x, ground(x, z) + 0.002f, z), new Vector3(0.026f, 0.006f, 0.026f), Hole);
            mb.jitter = sj;
        }

        static Matrix4x4 Frame4(float x, float y, float z, float dirX, float dirZ, float tiltX, float tiltZ, Func<float, float, float> ground)
        {
            float yaw = Mathf.Atan2(dirX, dirZ) * Mathf.Rad2Deg;
            return Matrix4x4.TRS(new Vector3(x, ground(x, z) + y, z), Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tiltX, 0f, tiltZ), Vector3.one);
        }

        static void Whole(MeshBuilder mb, RackMovePlan plan, bool atNew, int n, Func<float, float, float> ground, int seed)
        {
            plan.Centre(atNew, out float cx, out float cz); plan.Axes(atNew, out float dx, out float dz, out _, out _);
            mb.M = Frame4(cx, 0f, cz, dx, dz, 0f, 0f, ground);
            if (plan.kind == RackKind.Fish)
            {
                FishRackPieces.Rack(mb, n);
                int dry = Mathf.Max(0, plan.cargo - plan.fresh);
                for (int i = 0; i < n; i++) FishRackPieces.HangingFish(mb, FishRackPieces.HangPoint(i), i < dry, i + 1);
            }
            else Pieces.HideRack(mb, n, 0, seed);
        }

        static void Ridge(MeshBuilder mb, RackMovePlan plan, in RackPart r, Func<float, float, float> ground)
        {
            if (!r.shown) return;
            mb.M = Frame4(r.x, r.y + r.jolt, r.z, r.dirX, r.dirZ, r.tilt, 0f, ground);
            bool fish = plan.kind == RackKind.Fish;
            mb.Box(Vector3.zero, new Vector3(fish ? 0.016f : 0.020f, fish ? 0.016f : 0.020f, plan.half * 2f + (fish ? 0.06f : 0.07f)), Wood);
        }

        static void Frame(MeshBuilder mb, RackMovePlan plan, in RackPart f, Func<float, float, float> ground)
        {
            if (!f.shown) return;
            mb.M = Frame4(f.x, f.y + f.jolt, f.z, f.dirX, f.dirZ, 0f, f.tilt + f.wobble, ground);
            bool fish = plan.kind == RackKind.Fish;
            float h = plan.height, baseW = fish ? 0.05f : 0.07f, topW = fish ? 0.012f : 0.035f, over = fish ? 0.03f : 0.045f, thick = fish ? 0.014f : 0.016f;
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                Bar(mb, new Vector3(side * baseW, -0.004f, 0f), new Vector3(-side * topW, h + over, 0f), thick, k == 0 ? Wood : WoodDark);
            }
            mb.Box(new Vector3(0f, h - (fish ? 0.012f : 0.03f), 0f), new Vector3(0.022f, 0.010f, 0.016f), Cord); // the lashing where they cross
        }

        static void Bar(MeshBuilder mb, Vector3 a, Vector3 b, float thick, Color col)
        {
            var d = b - a; float len = d.magnitude;
            if (len < 1e-5f) return;
            mb.Box((a + b) * 0.5f, new Vector3(thick, len, thick), col, Quaternion.FromToRotation(Vector3.up, d / len));
        }
    }
}
