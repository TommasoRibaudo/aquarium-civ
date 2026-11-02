using System.Collections.Generic;
using AquariumCiv.Audio;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// The relics in the world (#92; relics-and-glass-voice.md §1), code-built in the box's palette:
    /// <list type="bullet">
    /// <item><b>Sky Crumb:</b> a heel of bread the size of a hut. When its moment starts it drops from the top of the box, bounces off a
    /// wall (<see cref="CrumbFall"/>: a thud and a smudge on the glass) and lands at its spot, then is eaten away over the next game
    /// year; only then the last piece shows, on a flat stone by the fire, in the gap between the seat logs.</item>
    /// <item><b>Keeper Doll:</b> on a post by the shrine (or the green), facing the glass.</item>
    /// <item><b>Lens:</b> a brass tube on a tripod; it slowly turns to follow the camera.</item>
    /// <item><b>Left Sandal:</b> on the remembering stone when an heir got it back (the owner wears it).</item>
    /// </list>
    /// The Festival Mask has no place in the world (it's worn at the festival, then kept on the shelf).
    /// </summary>
    public class RelicProps : MonoBehaviour
    {
        /// <summary>Game months the crumb takes to be eaten down to its last piece.</summary>
        public const int CrumbMonths = 12;
        public const float DropSeconds = 0.6f;
        /// <summary>How much bigger than life the relics stand, so they read at default zoom (user 2026-10-30, #92).</summary>
        public const float CrumbScale = 1.5f, DollScale = 2.2f, LensScale = 1.9f, SandalScale = 2.2f;

        BoxView box;
        Transform root;
        readonly Dictionary<string, Transform> made = new();
        Transform crumbPiece;
        float crumbDrop = -1f, crumbArrival, lensYaw;
        bool crumbPlanned, crumbHit;
        CrumbFall.Plan crumbPlan;
        /// <summary>Tester-free clock of the drop: seconds in, or -1 when nothing's falling (tests, the debug panel).</summary>
        public float CrumbDropSeconds => crumbDrop;
        public CrumbFall.Plan CrumbPlan => crumbPlan;
        /// <summary>#147: found, but its moment hasn't started: no lump yet. <see cref="GlassVoice"/> sets it.</summary>
        public bool CrumbHeld { get; set; }
        /// <summary>From the fire to the stone: the gap between the seat logs (angle 5.64 rad, <see cref="Pieces"/>), clear of them all.</summary>
        const float StoneAngle = 5.64f, StoneReach = 0.42f;

        /// <summary>The relic props standing now (tests).</summary>
        public IReadOnlyDictionary<string, Transform> Made => made;

        void OnEnable() => box = GetComponent<BoxView>();

        void OnDisable()
        {
            if (root != null) Kill(root.gameObject);
            root = null;
            made.Clear();
            crumbPiece = null;
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        /// <summary>A relic's moment starts: the crumb drops in from the top of the box.</summary>
        public void MomentStarted(string id, float arrivalSeconds = 0f)
        {
            if (id != RelicId.SkyCrumb) return;
            crumbDrop = 0f;
            crumbArrival = arrivalSeconds;
            crumbPlanned = crumbHit = false;
        }

        public void Sync(SimCore sim, LifePlanner planner, float dt)
        {
            if (box == null || box.Root == null || sim == null || planner == null) return;
            if (root == null)
            {
                root = new GameObject("Relics") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            var pos = planner.Positions;

            // Sky Crumb: the big lump where it fell, eaten away; the last piece by the fire.
            var crumb = sim.FindRelic(RelicId.SkyCrumb);
            if (crumb != null && crumb.Found && pos.TryGetPosition(LifePlanner.RelicPlace(RelicId.SkyCrumb), out float cx, out float cz))
            {
                float left = CrumbFall.Left(crumb.foundMonth, sim.State.month);
                var big = Ensure(RelicId.SkyCrumb, BuildCrumb);
                big.gameObject.SetActive(left > 0.02f && !CrumbHeld);
                float s = Mathf.Lerp(0.3f, 1f, left);
                var at = box.land.OnGround(cx, cz);
                bool falling = crumbDrop >= 0f;
                if (falling) Fall(sim, crumb, cx, cz, dt, ref at);
                big.localPosition = at;
                big.localScale = new Vector3(s, s * (falling ? 1f : 0.92f), s) * CrumbScale;
                if (crumbPiece == null) crumbPiece = Part("SkyCrumb_LastPiece", BuildCrumbPiece);
                bool last = CrumbFall.ShowLastPiece(crumb.Found, crumb.foundMonth, sim.State.month); // #138: only once the big lump is eaten
                crumbPiece.gameObject.SetActive(last);
                var st = sim.Settlement;
                if (st != null && last)
                {
                    st.Fire(out float fx, out float fz, out _);
                    crumbPiece.localPosition = box.land.OnGround(fx + Mathf.Cos(StoneAngle) * StoneReach, fz + Mathf.Sin(StoneAngle) * StoneReach);
                    crumbPiece.localScale = Vector3.one * CrumbScale;
                }
            }

            // Keeper Doll: on its post, facing the glass.
            if (sim.HasRelic(RelicId.KeeperDoll) && pos.TryGetPosition(LifePlanner.RelicPlace(RelicId.KeeperDoll), out float dx, out float dz))
            {
                var doll = Ensure(RelicId.KeeperDoll, BuildDollPost);
                var n = PeopleView.GlassNormal(planner.GlassSide);
                doll.localPosition = box.land.OnGround(dx + n.x * 0.08f, dz + n.y * 0.08f);
                doll.localScale = Vector3.one * DollScale;
                doll.localRotation = Quaternion.Euler(0f, Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg + 180f, 0f); // its front (-z) faces out
            }

            // Lens: on its tripod, slowly following the camera.
            if (sim.HasRelic(RelicId.Lens) && pos.TryGetPosition(LifePlanner.RelicPlace(RelicId.Lens), out float lx, out float lz))
            {
                var lens = Ensure(RelicId.Lens, BuildLens);
                lens.localPosition = box.land.OnGround(lx + 0.12f, lz);
                lens.localScale = Vector3.one * LensScale;
                var cam = Camera.main;
                if (cam != null)
                {
                    var c = transform.InverseTransformPoint(cam.transform.position);
                    float want = Mathf.Atan2(c.x - lens.localPosition.x, c.z - lens.localPosition.z) * Mathf.Rad2Deg + 180f;
                    lensYaw = Mathf.MoveTowardsAngle(lensYaw, want, 20f * dt);
                }
                lens.localRotation = Quaternion.Euler(0f, lensYaw, 0f);
            }

            // Left Sandal: an heir set it on the remembering stone.
            var sandal = sim.FindRelic(RelicId.LeftSandal);
            bool onStone = sandal != null && sandal.Found && sandal.holderId != sandal.ownerId;
            if (onStone)
            {
                bool church = sim.State.buildings.Exists(b => b.type == BuildingType.Church);
                if (pos.TryGetPosition(Place.Land(PlaceKind.Remembering, church ? 1 : 0), out float rx, out float rz))
                {
                    var stone = Ensure(RelicId.LeftSandal, BuildSandalStone);
                    stone.localPosition = box.land.OnGround(rx, rz);
                    stone.localScale = Vector3.one * SandalScale;
                }
            }
            else if (made.TryGetValue(RelicId.LeftSandal, out var gone)) { Kill(gone.gameObject); made.Remove(RelicId.LeftSandal); }
        }

        /// <summary>#147: one frame of the drop: from the top of the box onto a wall (the thud and the smudge), back to the spot.</summary>
        void Fall(SimCore sim, Relic crumb, float cx, float cz, float dt, ref Vector3 at)
        {
            var care = CareView.Instance;
            float halfW = Land.HX, halfD = Land.HZ, top = 1.6f;
            if (care != null) care.Walls(out halfW, out halfD, out top);
            float ground = at.y;
            if (!crumbPlanned)
            {
                crumbPlan = CrumbFall.For(crumb.cause, crumbArrival, cx, cz, halfW, halfD, top - ground, sim.State.seed);
                crumbPlanned = true;
            }
            crumbDrop += dt;
            CrumbFall.At(crumbPlan, crumbDrop, out float x, out float y, out float z);
            // The lump is wide: its middle stops that far inside the glass, while the mark stays where the plan's point is.
            at = new Vector3(Mathf.Clamp(x, -halfW + CrumbClear, halfW - CrumbClear), ground + y, Mathf.Clamp(z, -halfD + CrumbClear, halfD - CrumbClear));
            if (!crumbHit && crumbDrop >= crumbPlan.hitAt)
            {
                crumbHit = true;
                Sfx.Play(CueLibrary.Thump);
                care?.Smudge(crumbPlan.wall, new Vector3(crumbPlan.hitX, ground + crumbPlan.hitY, crumbPlan.hitZ), crumbPlan.smudgeRadius, crumbPlan.smudgeStrength);
            }
            if (crumbDrop >= crumbPlan.landAt)
            {
                crumbDrop = -1f;
                Sfx.Play(CueLibrary.Thump);
                GlassVoice.Instance?.CrumbLanded(sim); // tester run 7: the fall needed a voice
            }
        }

        /// <summary>How far the falling lump's middle keeps from the glass.</summary>
        const float CrumbClear = 0.3f;

        Transform Ensure(string id, System.Action<MeshBuilder> build)
        {
            if (made.TryGetValue(id, out var t) && t != null) return t;
            t = Part("Relic_" + id, build);
            made[id] = t;
            return t;
        }

        Transform Part(string name, System.Action<MeshBuilder> build)
        {
            var mb = new MeshBuilder(name.GetHashCode()) { jitter = 0.02f };
            build(mb);
            var mesh = mb.ToMesh(name);
            mesh.hideFlags = HideFlags.DontSave;
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go.transform;
        }

        // ---------------- meshes ----------------
        /// <summary>The bread's colours: a baked crust, its darker top, and the pale torn inside.</summary>
        static readonly Color Crust = new(0.73f, 0.45f, 0.2f), CrustTop = new(0.58f, 0.33f, 0.14f);

        /// <summary>A heel of bread the size of a hut (#147): a domed crust over a flat foot, three slashes in the top, and the torn pale end where it broke off, with loose crumbs round it.</summary>
        static void BuildCrumb(MeshBuilder mb)
        {
            mb.Ico(new Vector3(0f, 0.095f, 0f), 0.16f, new Vector3(1.4f, 0.75f, 1f), CrustTop, 0.1f, Crust);
            mb.Prism(new Vector3(-0.215f, 0f, 0f), 0.15f, 0.15f, 0.43f, 9, Crust, Quaternion.Euler(0f, 0f, -90f), true, true, Palette.Canvas); // the loaf's body, lying along x
            mb.Prism(new Vector3(0.21f, 0.09f, 0f), 0.135f, 0.105f, 0.022f, 9, Palette.Canvas, Quaternion.Euler(0f, 0f, -90f), true, true, Palette.Canvas); // the torn end
            var slash = Quaternion.Euler(0f, 24f, 0f);
            foreach (float x in new[] { -0.12f, 0f, 0.12f })
                mb.Box(new Vector3(x, 0.214f, 0f), new Vector3(0.022f, 0.012f, 0.17f), Palette.Canvas, slash, true);
            mb.Ico(new Vector3(0.3f, 0.015f, 0.08f), 0.025f, Vector3.one, Palette.Canvas, 0.3f);
            mb.Ico(new Vector3(-0.26f, 0.012f, -0.15f), 0.02f, Vector3.one, Crust, 0.3f);
            mb.Ico(new Vector3(0.28f, 0.01f, -0.11f), 0.016f, Vector3.one, Crust, 0.3f);
        }

        /// <summary>The last piece of sky bread on a flat stone by the fire.</summary>
        static void BuildCrumbPiece(MeshBuilder mb)
        {
            mb.Prism(Vector3.zero, 0.075f, 0.065f, 0.022f, 7, Palette.Stone, topCol: Palette.RockLight);
            mb.Ico(new Vector3(0f, 0.044f, 0f), 0.03f, new Vector3(1.25f, 0.8f, 1f), CrustTop, 0.2f, Palette.Canvas);
        }

        /// <summary>The Keeper Doll on a post, about as tall as a person, facing -z.</summary>
        static void BuildDollPost(MeshBuilder mb)
        {
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.022f, 0.1f, 0.022f), Palette.Timber);
            PegDoll.KeeperDoll(mb, new Vector3(0, 0.1f, 0), 1.1f);
        }

        /// <summary>The Lens: three tripod legs and a brass tube on top, pointing -z.</summary>
        static void BuildLens(MeshBuilder mb)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f;
                var foot = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 0.06f);
                var top = new Vector3(0, 0.16f, 0);
                var mid = (foot + top) * 0.5f;
                var dir = (top - foot).normalized;
                mb.Box(mid, new Vector3(0.008f, (top - foot).magnitude, 0.008f), Palette.WoodLight, Quaternion.FromToRotation(Vector3.up, dir));
            }
            mb.Prism(new Vector3(0, 0.17f, 0.05f), 0.02f, 0.016f, 0.15f, 8, PegDoll.Brass, Quaternion.Euler(-80f, 0, 0), true, true, Palette.GlassEdge);
        }

        /// <summary>The remembering stone's sandal, on a cushion.</summary>
        static void BuildSandalStone(MeshBuilder mb)
        {
            mb.Box(new Vector3(0.09f, 0.01f, 0), new Vector3(0.05f, 0.02f, 0.06f), Palette.Accents[0]);
            mb.Box(new Vector3(0.09f, 0.024f, 0), new Vector3(0.026f, 0.008f, 0.05f), Palette.Hide);
        }
    }
}
