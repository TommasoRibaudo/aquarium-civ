namespace AquariumCiv.View
{
    /// <summary>
    /// The camp fire's looks under test (#65): the user picks one from screenshots, then the losers go. Flip with
    /// <see cref="SimDebugPanel.DoFireLook"/> (or the "Fire (#65)" section of the sim panel), which rebuilds the box.
    /// <list type="bullet">
    /// <item><b>Current:</b> the #63 fire (lit cones, red tongues). Kept for before/after shots.</item>
    /// <item><b>Soft:</b> A. Cel-banded teardrops on camera-facing quads (Glow shader kind 5): round bellies, a three-tone
    /// ramp, soft tips, licks running up them. No flame geometry at all.</item>
    /// <item><b>Embers:</b> B. A low hot coal heap and stubby flames; the show is the light: streaked sparks (kind 6), a big
    /// warm pool, a wash over whoever sits close (kind 7), logs and stones warmed in the mesh.</item>
    /// <item><b>Paper:</b> C. Six big folded tongues, faceted like the rest of the kit, two tones per tongue, unlit.</item>
    /// </list>
    /// </summary>
    public enum FireLook { Current, Soft, Embers, Paper }

    /// <summary>What one look asks of the view, per fire. Sizes are world units.</summary>
    public sealed class FireSpec
    {
        public float haloRadius, poolRadius;
        /// <summary>Extra strength of the warm pool (0 = the #63 pool), full at night, 40 % by day.</summary>
        public float poolBoost;
        /// <summary>The warm wash in front of the fire's neighbours (0 = none).</summary>
        public float washRadius;
        public int embers, sparks, smokePuffs;
        /// <summary>Billboard flames (see <see cref="FireLooks.SoftTongue"/>).</summary>
        public int softTongues;
        /// <summary>Where the embers start, above the ground.</summary>
        public float emberY;
        /// <summary>Most vertices the look's flame body may add to the box mesh (the stone ring and logs are extra).</summary>
        public int bodyVertexBudget;

        /// <summary>Quads this look adds to the one glow mesh, per fire.</summary>
        public int GlowQuads => 2 + (washRadius > 0f ? 1 : 0) + embers + sparks + softTongues;
    }

    /// <summary>The look in use and the table behind it. Pure: read from worker threads by the mesh builders.</summary>
    public static class FireLooks
    {
        /// <summary>The look a fresh game starts with: Embers, the user's pick (#65).</summary>
        public const FireLook Default = FireLook.Embers;

        /// <summary>The look the box builds. A debug flip outlives leaving play mode.</summary>
        public static FireLook Active = Default;

        /// <summary>Quads of glow per fire, whatever the look (each is one more quad in one small mesh, no extra draw).</summary>
        public const int GlowQuadBudget = 24;

        static readonly FireSpec[] Specs =
        {
            new FireSpec { haloRadius = 0.42f, poolRadius = 0.55f, embers = 8, emberY = 0.14f, smokePuffs = 4, bodyVertexBudget = 130 },
            new FireSpec { haloRadius = 0.5f, poolRadius = 0.6f, poolBoost = 0.6f, embers = 8, emberY = 0.28f, smokePuffs = 3, softTongues = 4, bodyVertexBudget = 45 },
            new FireSpec { haloRadius = 0.34f, poolRadius = 0.8f, poolBoost = 1.6f, washRadius = 0.6f, embers = 4, emberY = 0.08f, sparks = 14, smokePuffs = 2, bodyVertexBudget = 90 },
            new FireSpec { haloRadius = 0.46f, poolRadius = 0.6f, poolBoost = 0.6f, embers = 6, emberY = 0.26f, smokePuffs = 3, bodyVertexBudget = 150 },
        };

        public static FireSpec Spec(FireLook look) => Specs[(int)look];

        /// <summary>All looks, in panel order.</summary>
        public static readonly FireLook[] All = { FireLook.Current, FireLook.Soft, FireLook.Embers, FireLook.Paper };

        public static FireLook Next(FireLook look) => All[((int)look + 1) % All.Length];

        /// <summary>A look by name, any case; also "A"/"B"/"C" as the issue calls them.</summary>
        public static bool TryParse(string name, out FireLook look)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "current": look = FireLook.Current; return true;
                case "soft": case "a": look = FireLook.Soft; return true;
                case "embers": case "b": look = FireLook.Embers; return true;
                case "paper": case "c": look = FireLook.Paper; return true;
            }
            look = FireLook.Current;
            return false;
        }

        /// <summary>
        /// Billboard flame <paramref name="i"/> of the Soft look: index 0.. are the small licks round the middle (written
        /// first, so the big one draws over them), the last is the big one. <paramref name="dx"/>/<paramref name="dz"/> is its
        /// foot's offset from the pit's centre.
        /// </summary>
        public static void SoftTongue(int i, int count, float yaw, out float dx, out float dz, out float halfWidth, out float height)
        {
            if (i >= count - 1) { dx = dz = 0f; halfWidth = 0.11f; height = 0.36f; return; }
            float a = yaw + i * 6.2832f / (count - 1);
            dx = (float)System.Math.Cos(a) * 0.075f;
            dz = (float)System.Math.Sin(a) * 0.075f;
            halfWidth = 0.075f;
            height = 0.23f + 0.03f * i;
        }
    }
}
