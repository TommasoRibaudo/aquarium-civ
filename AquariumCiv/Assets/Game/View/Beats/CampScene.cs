// #692 people and things (docs/design/animation-specs.md "Batch 3", D rules and parts 1-5): a camp use is a short scene with a cast
// borrowed from their free time. Each member has a timeline of parts (walk to a spot, hold a pose, hold a prop); the scene also
// carries its effects (sparks, steam, droplets) at set times, and the small things it draws (an ember on a twig's tip, a berry).
// Pure: Mathf, Vector2/3, DollPose and the beat kit, so the timelines can be tested in EditMode and AnimBench could compile it.
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>#692: the uses a <see cref="CampScene"/> plays (one per spec row; see the B numbers).</summary>
    public enum CampUse
    {
        None,
        /// <summary>B29.a: a sitter at the fire warms the hands in winter.</summary>
        WarmHands,
        /// <summary>B29.b: a kid pokes a twig in the fire, a grown-up takes it.</summary>
        KidPoke,
        /// <summary>B29.c: a berry on a stick over the fire.</summary>
        BerryStick,
        /// <summary>B29.d: meat on a spit after a hunt (the set-up, the turning, the round of pieces).</summary>
        Spit,
        /// <summary>B30.a: the pot filled and lifted onto the fire.</summary>
        PotFill,
        /// <summary>B30.e: after supper the pot is scraped and lifted off; a kid stacks the bowls.</summary>
        PotOff,
        /// <summary>B30.b: the bowl-giver's dips at the pot between hand-overs.</summary>
        Ladle,
        /// <summary>B30.c: kids peek in the simmering pot.</summary>
        PotPeek,
        /// <summary>B30.d: a grown-up passing the pot sniffs and nods.</summary>
        PotSniff,
        /// <summary>B31.a: a new feather tied on the pole after a good event.</summary>
        FeatherTie,
        /// <summary>B31.b: kids dare each other to touch the pole.</summary>
        PoleDare,
    }

    /// <summary>A pose laid over the doll's pose for one part: <paramref name="into"/> seconds into the part, <paramref name="len"/> its length.</summary>
    public delegate void CampPoseFn(ref DollPose p, float into, float len);

    /// <summary>One stretch of one member's timeline (scene seconds).</summary>
    public sealed class CampPart
    {
        public float from, to;
        /// <summary>Where they stand (box x, z) and what they face. With <see cref="keepSpot"/> the plan's own spot and facing stay.</summary>
        public Vector2 pos, face;
        public bool keepSpot;
        /// <summary>The move drawn while they stand still (walking: the walk, or a run with <see cref="run"/>).</summary>
        public DollStyle style = DollStyle.Stand;
        public PropKind prop;
        public PropSlot slot = PropSlot.Hand;
        /// <summary>Their pace toward <see cref="pos"/> (box units a second).</summary>
        public float speed = 0.3f;
        public bool run;
        /// <summary>The pose over the move (null: the move as it is).</summary>
        public CampPoseFn pose;
        /// <summary>Lay the pose even while walking (a heavy carry, a slow bent walk).</summary>
        public bool poseWalking;
        /// <summary>B30.b (the user's review): this part is a dip at the pot; seconds into the dip at the part's start (−1: not a dip).
        /// CampLife puts the pot's ladle in the right hand while the dip says so.</summary>
        public float dipFrom = -1f;
        public float Len => to - from;
    }

    /// <summary>What an effect is.</summary>
    public enum CampFxKind { Sparks, Flare, Steam, Dust, Droplets, Ring, Grass, Chime, Jolt,
        /// <summary>The user's review of part 1: the pot's own soft steam swells for a moment (several white wisps rising and fading), not a puff disc.</summary>
        PotSteam }

    /// <summary>Where an effect is: on the ground at a box point (<see cref="CampFx.at"/> x, z; y above the ground), or at a member's hand,
    /// head or held twig's tip (plus <see cref="CampFx.at"/> as a world-up offset in box units).</summary>
    public enum CampAnchor { Ground, Hand, Head, TwigTip }

    public struct CampFx
    {
        public float time;
        public CampFxKind kind;
        public CampAnchor anchor;
        public int member;
        public Vector3 at;
        public float scale;
        public int count;
    }

    /// <summary>A small thing the scene draws at a member's hand or twig tip for a while (B29.b the ember, B29.c the berry, B31.a the feather).</summary>
    public enum CampTipKind { Ember, Berry, MeatPiece }

    public struct CampTip
    {
        public int member;
        public float from, to;
        public CampTipKind kind;
        /// <summary>How it changes over its life: grows from 1 to <see cref="grow"/>, and darkens toward <see cref="darken"/> (0..1).</summary>
        public float grow, darken;
    }

    /// <summary>
    /// #692: one camp use playing now: its cast (people keys), each member's parts, its effects and its small drawn things. Times are
    /// scene seconds from <see cref="t0"/> (the PeopleView pose clock). Build it with <see cref="Add"/>, <see cref="Part"/> and <see cref="Fx"/>.
    /// </summary>
    public sealed class CampScene
    {
        public CampUse use;
        public float t0;
        /// <summary>Where the camera should look (AnimLab's "camp" follow, the debug readout).</summary>
        public Vector2 focus;
        /// <summary>The thing it is about (the fire, the pot, the pole), so one use per thing plays at a time.</summary>
        public int thing;
        public readonly List<int> keys = new();
        public readonly List<List<CampPart>> parts = new();
        public readonly List<CampFx> fx = new();
        public readonly List<CampTip> tips = new();
        /// <summary>Extra state a use carries (the spit's meat, the pot's lift), read by the director.</summary>
        public float a, b;
        public int kind;
        /// <summary>The camera keeps to <see cref="focus"/> (the thing), not pulled toward the lead actor (the feather pole: the user's review).</summary>
        public bool holdFocus;

        public CampScene(CampUse use, float t0, Vector2 focus, int thing = 0) { this.use = use; this.t0 = t0; this.focus = focus; this.thing = thing; }

        public int Count => keys.Count;
        /// <summary>The scene's end (scene seconds): the last part's or effect's.</summary>
        public float Length
        {
            get
            {
                float e = 0f;
                foreach (var l in parts) foreach (var p in l) e = Mathf.Max(e, p.to);
                foreach (var f in fx) e = Mathf.Max(e, f.time);
                return e;
            }
        }
        public float End => t0 + Length;

        public int Add(int key) { keys.Add(key); parts.Add(new List<CampPart>()); return keys.Count - 1; }
        public int IndexOf(int key) => keys.IndexOf(key);

        public CampPart Part(int member, float from, float to, Vector2 pos, Vector2 face, DollStyle style, CampPoseFn pose = null, PropKind prop = PropKind.None, float speed = 0.3f)
        {
            var p = new CampPart { from = from, to = to, pos = pos, face = face, style = style, pose = pose, prop = prop, speed = speed };
            parts[member].Add(p);
            return p;
        }

        /// <summary>A part that leaves them where the plan has them (a sitter's overlay).</summary>
        public CampPart Stay(int member, float from, float to, DollStyle style, CampPoseFn pose = null, PropKind prop = PropKind.None)
        {
            var p = new CampPart { from = from, to = to, keepSpot = true, style = style, pose = pose, prop = prop };
            parts[member].Add(p);
            return p;
        }

        public void Fx(float time, CampFxKind kind, Vector3 at, float scale = 1f, int count = 0, CampAnchor anchor = CampAnchor.Ground, int member = -1) =>
            fx.Add(new CampFx { time = time, kind = kind, at = at, scale = scale, count = count, anchor = anchor, member = member });

        public void Tip(int member, float from, float to, CampTipKind kind, float grow = 1f, float darken = 0f) =>
            tips.Add(new CampTip { member = member, from = from, to = to, kind = kind, grow = grow, darken = darken });

        /// <summary>The part member <paramref name="key"/> is in at pose clock <paramref name="t"/>, and the seconds into it.</summary>
        public bool TryPart(int key, float t, out CampPart part, out float into)
        {
            part = null; into = 0f;
            int i = keys.IndexOf(key);
            if (i < 0) return false;
            float s = t - t0;
            foreach (var p in parts[i])
                if (s >= p.from && s < p.to) { part = p; into = s - p.from; return true; }
            return false;
        }

        /// <summary>The member's first part start (scene seconds); before it, the scene doesn't have them yet.</summary>
        public float FirstFrom(int member) { float f = float.MaxValue; foreach (var p in parts[member]) f = Mathf.Min(f, p.from); return f; }
    }

    /// <summary>#692 shared helpers for the scenes' poses.</summary>
    public static class CampPose
    {
        public static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
        /// <summary>0 → 1 over [a, a+len] of <paramref name="t"/>, smoothed.</summary>
        public static float In(float t, float a, float len) => Smooth((t - a) / Mathf.Max(1e-4f, len));
        /// <summary>A bump 0 → 1 → 0 over [a, a+len].</summary>
        public static float Bump(float t, float a, float len) => t < a || t > a + len ? 0f : Mathf.Sin((t - a) / len * Mathf.PI);
        public static Vector3 Lerp(Vector3 x, Vector3 y, float k) => Vector3.LerpUnclamped(x, y, k);
        /// <summary>A seated pose (low, squashed), as the Sit loop and the meal draw it.</summary>
        public static void Seated(ref DollPose p) { p.offset.y = -0.03f; p.squash = 0.78f; }
    }
}
