using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #362 B6: the thrown spear. When the hunter's throw passes <see cref="HuntTiming.ReleaseAt"/> the spear leaves his hand on a kit arc
    /// (<see cref="ArcShot"/>, <see cref="Landing.Stick"/>) and comes down on the plan's <c>landX/landZ</c> (the rabbit on a catch, past or
    /// beside it on a miss), where it stands in the ground at 30 to 50 degrees and quivers. It stays until the hunter pulls it out (a miss) or
    /// lifts the catch (<c>plan.takeHour</c>), with a puff of dust when it comes out of the ground. The clock is the view day's seconds
    /// (<see cref="HuntClock.FieldSeconds"/>), the same as the pose's, so slow-mo and fast-forward keep the spear and the doll together.
    /// Meshes are box-local children of the people root; one mesh per live spear, pooled.
    /// </summary>
    public sealed class HuntStage
    {
        sealed class Spear
        {
            public HuntPlan plan;
            public ArcShot shot;
            public Transform go;
            public bool landed, taken;
        }

        readonly Dictionary<HuntPlan, Spear> spears = new();
        readonly List<Transform> pool = new();
        readonly List<HuntPlan> drop = new();
        Mesh mesh;

        /// <summary>The spear came down (plan, box-local point), once per throw. HitFx's grass puff comes with the hunter's Contact (PeopleView).</summary>
        public event Action<HuntPlan, Vector3> Landed;

        public int Live => spears.Count;
        /// <summary>Spears standing in the ground now (landed, not yet pulled).</summary>
        public int Stuck { get { int n = 0; foreach (var s in spears.Values) if (s.landed && !s.taken) n++; return n; } }
        /// <summary>The newest spear's state, for tests: flying or stuck, and its tip (box-local).</summary>
        public bool TryGetSpear(HuntPlan plan, out Vector3 tip, out ArcStage stage)
        {
            tip = default; stage = ArcStage.Gone;
            if (!spears.TryGetValue(plan, out var s) || s.go == null) return false;
            tip = s.go.localPosition; stage = s.landed ? ArcStage.Stuck : ArcStage.Flying;
            return !s.taken;
        }

        /// <summary>Where the spear of this plan comes down (box-local), on the ground.</summary>
        public static Vector3 LandPoint(HuntPlan plan, Func<float, float, float> ground) =>
            new Vector3(plan.landX, ground(plan.landX, plan.landZ) + (plan.catches ? 0.02f : 0f), plan.landZ);

        public void Tick(LifePlanner planner, ViewTime now, Transform root, Material material, Func<PersonRef, Transform> propOf, Func<float, float, float> ground)
        {
            if (planner == null || root == null) { Clear(); return; }
            var plans = planner.HuntsOn(now.day);
            drop.Clear();
            foreach (var kv in spears) if (!Active(kv.Key, now) || kv.Key.day != now.day) drop.Add(kv.Key);
            foreach (var p in drop) Remove(p);
            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                if (!Active(plan, now)) continue;
                var strike = plan.steps.Find(s => s.phase == HuntPhase.Strike);
                if (strike.phase != HuntPhase.Strike) continue;
                double strikeField = HuntClock.FieldSeconds(plan.day, strike.start);
                float rel = (float)(HuntClock.FieldSeconds(now.day, now.hour) - strikeField);
                if (rel < HuntTiming.ReleaseAt) continue;
                if (!spears.TryGetValue(plan, out var sp))
                {
                    var end = LandPoint(plan, ground);
                    Vector3 start;
                    var prop = propOf != null ? propOf(plan.hunter) : null;
                    if (prop != null && rel < HuntTiming.ReleaseAt + 0.3f) start = root.InverseTransformPoint(prop.TransformPoint(new Vector3(0f, 0.205f, 0f)));
                    else start = new Vector3(plan.lungeX, ground(plan.lungeX, plan.lungeZ) + 0.2f, plan.lungeZ);
                    var shot = ArcShot.Throw(start, end, HuntTiming.FlightApex, HuntTiming.FlightSeconds, Landing.Stick, HuntTiming.ReleaseAt);
                    sp = new Spear { plan = plan, shot = shot, go = Rent(root, material) };
                    spears[plan] = sp;
                }
                if (sp.taken) continue;
                if (now.hour >= plan.takeHour)
                {
                    sp.taken = true;
                    if (!plan.catches && HitFx.Enabled)
                        HitFx.Puff(PuffKind.Dust, root.TransformPoint(sp.shot.end), Vector3.up, 0.8f);   // out of the ground
                    sp.go.gameObject.SetActive(false);
                    continue;
                }
                var s = Arc.Sample(sp.shot, rel);
                sp.go.gameObject.SetActive(s.Visible);
                sp.go.SetLocalPositionAndRotation(s.position, s.rotation);
                if (!sp.landed && rel >= sp.shot.LandAt)
                {
                    sp.landed = true;
                    Landed?.Invoke(plan, sp.shot.end);
                }
            }
        }

        static bool Active(HuntPlan plan, ViewTime now)
        {
            if (!plan.TryStep(HuntPhase.Strike, out var strike)) return false;   // #680: a second try has fewer steps before it
            return now.day == plan.day && now.hour < plan.takeHour + 0.05f && now.hour >= strike.start;
        }

        void Remove(HuntPlan plan)
        {
            if (!spears.TryGetValue(plan, out var s)) return;
            if (s.go != null) { s.go.gameObject.SetActive(false); pool.Add(s.go); }
            spears.Remove(plan);
        }

        public void Clear()
        {
            drop.Clear();
            foreach (var k in spears.Keys) drop.Add(k);
            foreach (var p in drop) Remove(p);
        }

        /// <summary>A parent whose +z is the spear's long axis and whose origin is the tip; the mesh hangs behind it.</summary>
        Transform Rent(Transform root, Material material)
        {
            while (pool.Count > 0)
            {
                var t = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1);
                if (t != null) { t.SetParent(root, false); return t; }
            }
            var go = new GameObject("ThrownSpear") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            var body = new GameObject("Mesh") { hideFlags = HideFlags.DontSave };
            body.transform.SetParent(go.transform, false);
            // the prop mesh stands along +y with its tip at 0.205: turn it onto +z and hang it back so the tip is the origin
            float s = 1.35f;   // a little over life, as the doll's own spear is drawn (it has to read at phone size)
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = Vector3.one * s;
            body.transform.localPosition = new Vector3(0f, 0f, -0.205f * s);
            mesh ??= PegDoll.PropMesh(PropKind.Spear);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = body.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}

namespace AquariumCiv.View
{
    /// <summary>#362 cheat: "Slow-mo the next hunt". Armed, the view clock (<see cref="PeopleView.timeScale"/>) runs at half speed from the start of
    /// the next stalk to the end of the held-up catch (or the pulled-out spear), then goes back to what it was and disarms. The planner, the
    /// spear and the rabbits all read the view clock, so they stay together.</summary>
    public static class HuntSlowMo
    {
        public static bool Armed;
        public const float Scale = 0.5f;
        static float saved = -1f;
        public static bool Active => saved >= 0f;

        public static void Tick(PeopleView pv, ViewTime now)
        {
            if (pv == null) return;
            var planner = pv.Planner;
            bool inside = false;
            if (planner != null && (Armed || Active))
                foreach (var plan in planner.HuntsOn(now.day))
                {
                    if (plan.steps.Count < 6) continue;
                    float end = plan.steps[plan.steps.Count - 1].start;
                    foreach (var st in plan.steps) if (st.phase is HuntPhase.HoldUp or HuntPhase.PullSpear) end = st.end;
                    if (now.hour >= plan.stalkStart && now.hour < end) { inside = true; break; }
                }
            if (inside && Armed && saved < 0f) { saved = pv.timeScale; pv.timeScale = Scale; }
            else if (!inside && saved >= 0f) { pv.timeScale = saved; saved = -1f; Armed = false; }
        }
    }
}
