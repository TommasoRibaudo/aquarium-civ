// 2026-11-16 "Bring a pen back" (decisions.md Q7; docs/design/animation-specs.md B54.v, B54.w, B58.d): the pond family's goat pen in the
// view. One goat (QuadDoll, QuadKind.Goat) stands in the pen while SimCore.PenGoat: it grazes, wanders inside the rails and now and then
// gnaws at the gnawed corner. It is drawn here, not by any herd view, so it is in no drawn-animal count and no wolf, dog or hunter list
// (the sim keeps it out of every herd, cap and budget). The uses: the rail mended (work), kids on the rails and one through the gap
// (life), a kid pets the goat over the rail (life). The mended corner holds for a season [est: 3 view days], then it is gnawed again.
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        static bool IsPenUse(CampUse u) => u is CampUse.PenMend or CampUse.PenRails or CampUse.PenPet;
        /// <summary>[est] a mend holds this many view days (a season), then the goat has the corner again.</summary>
        public const int MendHolds = 3;
        /// <summary>The goat's walk inside the pen (box units a second) and how often it goes to gnaw the corner (s, real).</summary>
        public const float GoatWalk = 0.06f, NibbleEvery = 22f, NibbleFor = 3.5f;

        QuadDoll goat;
        Vector2 gPos; float gYaw, gLast = -1f, gSince, gNibbleAt = -1f, gNibbleUntil = -1f, gChipAt;
        Vector2 gTarget; bool gHasTarget; int gPenId = -1;
        long mendedDay = long.MinValue;
        /// <summary>Tests: the goat is drawn now, and how many times it gnawed the corner.</summary>
        public bool GoatDrawn => goat != null && goat.go != null && goat.go.activeSelf;
        public int GoatNibbles { get; private set; }
        public Transform GoatTransform => goat?.go != null ? goat.go.transform : null;
        public Vector2 GoatAt => gPos;
        public bool PenMended => mendedDay != long.MinValue && now.day - mendedDay < MendHolds;

        void ResetGoat() { gLast = -1f; gNibbleAt = -1f; gNibbleUntil = -1f; gHasTarget = false; gPenId = -1; mendedDay = long.MinValue; GoatNibbles = 0; }
        void DestroyGoat() { goat?.Destroy(); goat = null; }

        // ---- the pen's geometry (its own frame: the gnawed corner at +x +z, PenGap)
        Vector2 PenPoint(in PropSpot p, float lx, float lz) => p.at + Local(p, lx, lz);
        Vector2 PenGapAt(in PropSpot p) => PenPoint(p, CampScenes6.PenHalf, ConsequenceMeshes.PenGap.z * PropGeo.S);
        Vector2 PenOut(in PropSpot p) => Local(p, 1f, 0f).normalized;
        /// <summary>The pen's side nearest the camera: its rail's middle, and the way into the pen from it.</summary>
        void PenNearRail(in PropSpot p, out Vector2 rail, out Vector2 inward, out Vector2 along)
        {
            var toCam = ToCam(p.at); float best = -9f; rail = p.at; inward = Vector2.up; along = Vector2.right;
            foreach (var (lx, lz) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            {
                var n = Local(p, lx, lz).normalized; float d = Vector2.Dot(n, toCam);
                if (d > best) { best = d; rail = p.at + n * CampScenes6.PenHalf; inward = -n; along = new Vector2(-n.y, n.x); }
            }
        }

        // ================================================================== each frame: the pen's look, the natural uses
        void StepGoat(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (sim == null || !PropOf(ConsequenceKind.GoatPen, out var pen)) return;
            if (!ThingBusy(ThingOf(pen))) ConsequencePropsView.SetLook(pen.id, PenMended ? 1 : 0);
            if (OnlyCheats || meals == null || meals.Eaters > 0 || planner == null) return;
            float h = Hour;
            if (h < 9f || h >= 16.5f || planner.WeatherOn(now.day) is Weather.KeeperRain or Weather.Storm) return;
            bool Go(CampUse u, float chance)
            {
                if (doneToday.Contains(u) || Roll(u) >= chance || !LifeOk(u) || ThingBusy(ThingOf(pen))) return false;
                if (triedAt.TryGetValue(u, out float tr) && clock - tr < 3f && clock >= tr) return false;
                triedAt[u] = clock; return true;
            }
            if (!PenMended && h < 11.5f && Go(CampUse.PenMend, Sometimes) && TryPenMend(acts, time, false, pen) == null) return;
            if (sim.PenGoat && Go(CampUse.PenPet, Sometimes) && TryPenPet(acts, time, false, pen) == null) return;
            if (Go(CampUse.PenRails, Sometimes) && TryPenRails(acts, time, false, pen) == null) return;
        }

        string TryPenMend(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = Grown1(acts, p.at, force);
            if (who < 0) return "no grown-up free";
            mendedDay = now.day;
            return Start(CampScenes6.PenMend(who, At(who).Value, PenGapAt(p), PenOut(p), ToCam(p.at), time), p, CampUse.PenMend);
        }

        string TryPenRails(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 3, force);
            if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
            PenNearRail(p, out var rail, out _, out var along);
            int c = kids.Count > 2 ? kids[2] : -1;
            return Start(CampScenes6.PenRails(kids[0], kids[1], At(kids[0]).Value, At(kids[1]).Value, c, c >= 0 ? At(c).Value : default, rail, along, PenGapAt(p), PenOut(p), !PenMended, ToCam(p.at), 6f, time), p, CampUse.PenRails);
        }

        string TryPenPet(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            if (!sim.PenGoat) return "no goat in the pen";
            int kid = Kid1(acts, p.at, force);
            if (kid < 0) return "no kid free ('Two kids join the camp' first)";
            PenNearRail(p, out var rail, out var inward, out _);
            return Start(CampScenes6.PenPet(kid, At(kid).Value, rail, inward, ToCam(p.at), time), p, CampUse.PenPet);
        }

        string ForcePen(CampUse use)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            if (!PropOf(ConsequenceKind.GoatPen, out var p)) return "no goat pen in the box ('Goat pen with its goat' first)";
            var acts = pv.Activities; float time = clock;
            switch (use)
            {
                case CampUse.PenMend: mendedDay = long.MinValue; ConsequencePropsView.SetLook(p.id, 0); return TryPenMend(acts, time, true, p) ?? "the goat pen's rail mended";
                case CampUse.PenRails: mendedDay = long.MinValue; return TryPenRails(acts, time, true, p) ?? "kids on the goat pen's rails";
                case CampUse.PenPet: return TryPenPet(acts, time, true, p) ?? "a kid pets the goat";
            }
            return "not a goat pen use: " + use;
        }

        /// <summary>The goat gnaws the corner now (the cheat: it walks there at once).</summary>
        public string GoatNibbleNow()
        {
            if (sim == null || !sim.PenGoat || !PropOf(ConsequenceKind.GoatPen, out var pen)) return "no goat in the pen";
            gNibbleAt = 0f; gNibbleUntil = -1f; gHasTarget = false;
            // a cast-less scene that only holds the camera on the pen for 12 s (AnimLab's "camp" follow, the readout)
            var s = new CampScene(CampUse.None, clock, pen.at) { holdFocus = true, a = 3f };
            s.Fx(12f, CampFxKind.Chime, Vector3.zero);
            Add(s); forced = s;
            return "the goat goes to gnaw the corner";
        }

        // ================================================================== the goat, drawn
        void DrawGoat(Transform root, Material material, float time, System.Func<float, float, float> groundAt)
        {
            if (sim == null || !sim.PenGoat || !PropOf(ConsequenceKind.GoatPen, out var pen)) { goat?.SetActive(false); return; }
            if (goat == null || goat.go == null)
            {
                goat = new QuadDoll(QuadLook.Of(QuadKind.Goat, 7), root, material);
                goat.go.name = "Pen goat";
            }
            if (gPenId != pen.id) { gPenId = pen.id; gPos = pen.at; gYaw = pen.yaw; gLast = -1f; gNibbleAt = time + 8f; }
            float dt = gLast < 0f ? 0f : Mathf.Clamp(time - gLast, 0f, 0.1f); gLast = time;
            float inner = CampScenes6.PenHalf - 0.085f; // kept to the middle: by the rails it hid behind them
            var gapIn = PenGapAt(pen) - PenOut(pen) * 0.065f;
            CampScene pet = null, mend = null;
            foreach (var s in scenes) if (s.thing == ThingOf(pen)) { if (s.use == CampUse.PenPet) pet = s; else if (s.use == CampUse.PenMend) mend = s; }
            // where it wants to be
            Vector2 want; bool nibble = false; Vector2? face = null;
            if (pet != null) { want = pet.p0; face = pet.focus; }
            else if (mend != null) { want = PenPoint(pen, -inner, -inner); face = mend.focus; }
            else
            {
                if (gNibbleAt >= 0f && time >= gNibbleAt && gNibbleUntil < time) { gNibbleUntil = float.MaxValue; gHasTarget = false; }
                if (gNibbleUntil > time) { want = gapIn; nibble = true; face = PenGapAt(pen); }
                else
                {
                    if (!gHasTarget || (gPos - gTarget).magnitude < 0.01f && time - gSince > 5f)
                    {
                        float a = BeatHash.U01((int)(time * 0.2f), pen.id, 6990), b = BeatHash.U01((int)(time * 0.2f), pen.id, 6991);
                        gTarget = PenPoint(pen, Mathf.Lerp(-inner, inner, a), Mathf.Lerp(-inner, inner, b)); gHasTarget = true; gSince = time;
                    }
                    want = gTarget;
                }
            }
            var d = want - gPos; bool moving = d.magnitude > 0.008f;
            if (moving) { gPos += d.normalized * Mathf.Min(d.magnitude, GoatWalk * dt); gSince = time; }
            else if (nibble && gNibbleUntil == float.MaxValue) { gNibbleUntil = time + NibbleFor; GoatNibbles++; }
            if (!moving && nibble && gNibbleUntil < float.MaxValue && time > gNibbleUntil - 0.05f) { gNibbleAt = time + NibbleEvery; nibble = false; }
            var look = moving ? d : face.HasValue ? face.Value - gPos : Vector2.zero;
            if (look.sqrMagnitude > 1e-6f) gYaw = Mathf.MoveTowardsAngle(gYaw, Mathf.Atan2(look.x, look.y) * Mathf.Rad2Deg, 220f * Mathf.Max(dt, 0.016f));
            var pos = gPos;
            float lunge = pet != null ? CampPose.Bump(time - pet.t0, pet.c - 0.1f, 0.4f) : 0f; // the gentle butt
            if (lunge > 0f && face.HasValue) pos += (face.Value - gPos).normalized * 0.03f * lunge;
            var p = QuadPose.Stand(new Vector3(pos.x, groundAt(pos.x, pos.y), pos.y), gYaw);
            if (moving) QuadMotion.Walk(ref p, time * 1.2f);
            else if (pet != null) { p.neck = -10f * lunge; p.bodyPitch = 8f * lunge; } // head up to the hand, then down in the butt
            else QuadMotion.Graze(ref p, time, time - gSince, 7);
            if (nibble && !moving)
            {
                p.bodyPitch += 4f; p.headYaw = 6f * Mathf.Sin(time * 9f); // chewing at the rail
                if (time > gChipAt) { gChipAt = time + 0.9f; HitFx.Puff(PuffKind.Chips, root.TransformPoint(new Vector3(gapIn.x, groundAt(gapIn.x, gapIn.y) + 0.05f, gapIn.y) + (Vector3)(Vector2.zero)), Vector3.up, 0.22f, PuffOptions.Default); }
            }
            goat.Apply(p); goat.SetActive(true);
        }
    }

    public static partial class CampCheats
    {
        /// <summary>"Goat pen with its goat": the pond family's pen stands with its goat (the box keeps only it and the feather pole when
        /// <paramref name="only"/>), as after the "a home here" answer.</summary>
        public static string GoatPen(bool only = false) => only ? OnlyThing(AquariumCiv.Sim.ConsequenceKind.GoatPen) : EveryThing();
        public static string PenMend() => Do(CampUse.PenMend);
        public static string PenRails() => Do(CampUse.PenRails);
        public static string PenPet() => Do(CampUse.PenPet);
        public static string GoatNibble() { var c = Camp; return c == null ? "no PeopleView" : c.GoatNibbleNow(); }
    }
}
