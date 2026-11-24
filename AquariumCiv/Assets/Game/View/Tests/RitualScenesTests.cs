// S-ritual (B72-B77): the pure halves in EditMode, no scene: the camp uses' timelines (B75.b, B77.a-b, RitualScenes) against the spec rows,
// the teller's beats (B76, TellerBeats: each line once at its beat, the kids never nearer the fire), the ritual beats' looks (RitualBeats:
// the right things in the hands at the right beat), and the props' new looks (the long table by boards, the empty offering basket).
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class RitualScenesTests
    {
        static readonly Vector2 Spot = new Vector2(0.4f, 0.9f), ToCam = Vector2.down;
        static readonly Vector2 Far = Spot + new Vector2(0.9f, 0.3f), Far2 = Spot + new Vector2(-0.7f, 0.4f), Far3 = Spot + new Vector2(0.2f, 1.1f);
        static void Ok(CampScene s) { Sane(s); PoseRuns(s); Assert.Greater(s.Length, 1f, s.use + " has a length"); Assert.Greater(s.a, 0f, s.use + " has its hit"); }
        static int PropFxs(CampScene s, PropFx what) => s.fx.Count(f => f.kind == CampFxKind.Prop && f.count == (int)what);

        [Test]
        public void B75b_TheEmptyBasket_AKidRunsToTheFire_TheLeadCarriesItBackToTheTree()
        {
            var fire = Spot + new Vector2(-1.2f, -0.3f);
            var s = RitualScenes.OfferingEmpty(1, Far, new[] { 2, 3, 4 }, new[] { fire + Vector2.left * 0.3f, fire + Vector2.right * 0.3f, fire + Vector2.up * 0.3f }, Spot, fire, ToCam, 0f);
            Ok(s);
            Assert.IsTrue(s.parts[0].Any(p => p.style == DollStyle.Freeze), "the kid freezes: nothing in it");
            Assert.IsTrue(s.parts[0].Any(p => p.run && p.poseWalking), "runs to the fire with it held up");
            Assert.AreEqual(3, s.parts.Skip(1).Count(l => l.Any(p => p.style == DollStyle.LookUp)), "the grown-ups at the fire look up at the glass");
            var looks = s.fx.Where(f => f.kind == CampFxKind.Prop && f.count == (int)PropFx.Look).OrderBy(f => f.time).Select(f => Mathf.RoundToInt(f.scale)).ToArray();
            CollectionAssert.AreEqual(new[] { ConsequenceMeshes.OfferingGone, ConsequenceMeshes.OfferingEmpty }, looks, "it leaves the tree, then is set back down there, empty (the 2026-11-16 note)");
            Assert.Greater(s.d, s.a, "the lead takes it from the kid");
            Assert.Greater(s.b, s.d, "and sets it back down at the tree");
            var set = s.parts[1].Last(p => p.pose != null).pos;
            Assert.Less((set - Spot).magnitude, 0.2f, "at the foot of the tree, not at a stone");
        }

        [Test]
        public void B77a_SortTheCatchOnTheBoat_RodAlongTheHull_SitsOnTheEnd()
        {
            var s = RitualScenes.BoatSort(1, Far, Spot, Vector2.right, ToCam, Far2, 0f);
            Ok(s);
            var sit = s.parts[0].Where(p => p.style == DollStyle.SortBasket).ToList();
            Assert.AreEqual(1, sit.Count); Assert.AreEqual(RitualScenes.SortFor, sit[0].Len, 1e-3f, "sorts the fish basket for 4 s");
            Assert.AreEqual(PropKind.FishBasket, sit[0].prop);
            var d = DollPose.Rest; sit[0].pose(ref d, 1f, sit[0].Len);
            Assert.AreEqual(RitualScenes.HullTop, d.lift + d.offset.y, 1e-4f, "sitting on the hull's end (the seat height in lift since the 2026-11-23 climb)");
            Assert.AreEqual(PropKind.Rod, s.parts[0][0].prop, "comes with the rod");
            Assert.Greater(s.b, s.a, "the rod lies along the hull from the lay to the take");
            Assert.Less((s.parts[0].Last().pos - Far2).magnitude, 0.01f, "goes on to the rack");
        }

        [Test]
        public void B77b_TheKidsHouse_KnockPeekSitSwinging()
        {
            var s = RitualScenes.BoatHouse(new[] { 1, 2, 3 }, new[] { Far, Far2, Far3 }, Spot, Vector2.right, ToCam, 0f);
            Ok(s);
            Assert.IsTrue(s.parts[1].Any(p => p.style == DollStyle.Knock), "a kid knocks on the hull");
            Assert.AreEqual(2, PropFxs(s, PropFx.Jolt), "two taps: the boat jolts twice");
            Assert.IsTrue(s.parts[0].Any(p => p.style == DollStyle.Wave), "the hidden one pops up and waves them round");
            Assert.AreEqual(3, s.parts.Count(l => l.Any(p => p.style == DollStyle.Sit && p.Len >= 6f)), "they sit on it, 6-10 s");
            var sit = s.parts[0].First(p => p.style == DollStyle.Sit);
            Assert.Less((sit.pos - Spot).magnitude, 0.2f, "on the boat");
            float rollMost = 0f;
            for (float u = 0f; u < 1f; u += 0.05f) { var d = DollPose.Rest; sit.pose(ref d, u, sit.Len); rollMost = Mathf.Max(rollMost, Mathf.Abs(d.roll)); }
            Assert.AreEqual(5f, rollMost, 0.3f, "swinging ±5°");
        }

        [Test]
        public void B77b_InRain_TheKidsCrouchInTheBoatsLee()
        {
            var s = RitualScenes.BoatLee(new[] { 1, 2, 3 }, new[] { Far, Far2, Far3 }, Spot, Vector2.right, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(CampUse.BoatHouse, s.use); Assert.AreEqual(1, s.kind, "the rain variant");
            Assert.IsTrue(s.parts.All(l => l[0].run), "they run in");
            var a = s.parts[0].First(p => p.pose != null).pos; var b = s.parts[1].First(p => p.pose != null).pos;
            Assert.AreEqual(0.06f, (a - b).magnitude, 0.005f, "pressed together, 0.06 u apart (B54.d)");
            Assert.Less(Vector2.Dot(a - Spot, ToCam), 0.2f); Assert.Greater(Vector2.Dot(a - Spot, ToCam), 0.1f, "against the hull on the camera's side");
            var d = DollPose.Rest; var hud = s.parts[0].First(p => p.pose != null); hud.pose(ref d, 1f, hud.Len);
            Assert.Less(d.squash, 0.85f, "crouched");
            Assert.GreaterOrEqual(s.fx.Count(f => f.kind == CampFxKind.Droplets), 6, "drops off the hull");
        }

        static bool Up(CampPart p, float u) { if (p.pose == null) return false; var d = DollPose.Rest; p.pose(ref d, u, p.Len); return d.lift + d.offset.y > 0.03f; }

        /// <summary>2026-11-24 (tester: "the fisher stands inside the hull", "the kids walk in through it"): from every side, no walk on the
        /// ground crosses the hull (the climbs on and off, lifted, may).</summary>
        static void NeverThroughTheHull(CampScene s, Vector2[] starts, string what)
        {
            for (int m = 0; m < s.parts.Count; m++)
            {
                var at = starts[Mathf.Min(m, starts.Length - 1)];
                foreach (var p in s.parts[m])
                {
                    bool lifted = Up(p, 0f) || Up(p, p.Len);
                    if (!lifted)
                        for (float k = 0f; k <= 1f; k += 0.02f)
                        {
                            var q = Vector2.Lerp(at, p.pos, k) - Spot; // the hull lies along x (Vector2.right)
                            Assert.IsFalse(Mathf.Abs(q.x) < 0.15f && Mathf.Abs(q.y) < 0.13f, $"{what}: member {m} walks through the hull at {q} (to {p.pos - Spot}, {p.style})");
                        }
                    at = p.pos;
                }
            }
        }

        [Test]
        public void B77_TheBoatScenes_NobodyWalksThroughTheHull()
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 Dir(float deg) => Spot + (Vector2)(Quaternion.Euler(0f, 0f, deg) * Vector3.right) * 0.8f;
                var a = Dir(i * 45f); var b = Dir(i * 45f + 120f); var c = Dir(i * 45f + 240f);
                NeverThroughTheHull(RitualScenes.BoatSort(1, a, Spot, Vector2.right, ToCam, b, 0f), new[] { a }, $"sort from {i * 45}°");
                NeverThroughTheHull(RitualScenes.BoatHouse(new[] { 1, 2, 3 }, new[] { a, b, c }, Spot, Vector2.right, ToCam, 0f), new[] { a, b, c }, $"house from {i * 45}°");
                NeverThroughTheHull(RitualScenes.BoatLee(new[] { 1, 2, 3 }, new[] { a, b, c }, Spot, Vector2.right, ToCam, 0f), new[] { a, b, c }, $"lee from {i * 45}°");
            }
        }

        [Test]
        public void B76_TheTellersLines_EachOnceAtItsBeat()
        {
            int Count(bool slow, bool wood, int line) { int n = 0; float last = -1f; for (float s = 0f; s < 30f; s += 1f / 30f) { if (TellerBeats.LineBetween(slow, wood, last, s) == line) n++; last = s; } return n; }
            Assert.AreEqual(1, Count(true, false, 0), "the wind, once"); Assert.AreEqual(1, Count(true, false, 1), "the scary part, once");
            Assert.AreEqual(1, Count(false, true, 0), "the woodpile, once"); Assert.AreEqual(1, Count(false, true, 1), "the end, once");
            Assert.AreEqual(1, Count(false, false, 0), "no woodpile: only the end line");
            Assert.AreEqual(0, Count(false, false, 1));
        }

        [Test]
        public void B76_TheSlowTelling_VoicesWindJump_TheKidsCopyFlinchAndScoot()
        {
            DollPose At(float s) { var p = DollPose.Rest; TellerBeats.Slow(ref p, s); return p; }
            Assert.Greater(At(1f).armL.x, 100f, "the hush: hands up");
            Assert.Greater(At(TellerBeats.SlowVoices + 1.5f).squash, 1.03f, "the big voice is tall");
            Assert.Less(At(TellerBeats.SlowVoices + 4.5f).squash, 0.9f, "the small voice is low");
            Assert.Greater(Mathf.Abs(At(TellerBeats.SlowWind + 2.5f).armR.z), 100f, "the wind: arms over the head");
            Assert.Less(At(TellerBeats.SlowJump - 0.5f).squash, 0.85f, "the crouch");
            Assert.Greater(At(TellerBeats.SlowJump + 0.1f).offset.y, 0.02f, "the jump");
            bool hit = false; for (float s = TellerBeats.SlowJump; s < TellerBeats.SlowJump + 0.2f; s += 0.01f) hit |= At(s).hit > 0f;
            Assert.IsTrue(hit, "the jump is the hit");
            float most = 0f;
            for (float s = 0f; s < 30f; s += 0.1f) { var p = DollPose.Rest; most = Mathf.Max(most, TellerBeats.SlowListener(ref p, s, true, false, false)); }
            Assert.LessOrEqual(most, 0.0701f, "a kid scoots in 0.03 + 0.04 u at most");
            Assert.GreaterOrEqual(CampLife.KidRing - most, CampLife.KidsNearest - 1e-3f, "kids safe: never nearer the fire than 0.33 u");
            var flinch = DollPose.Rest; TellerBeats.SlowListener(ref flinch, TellerBeats.SlowJump + 0.25f, true, false, false);
            Assert.Less(flinch.lean, -5f, "the kids flinch back at the jump");
            var copy = DollPose.Rest; TellerBeats.SlowListener(ref copy, TellerBeats.SlowWind + 2.5f, true, false, false);
            Assert.Greater(Mathf.Abs(copy.armL.z), 100f, "the kids copy the wind");
        }

        [Test]
        public void B76_TheShortTelling_TheHeadsTurnToTheWoodpile()
        {
            var p = DollPose.Rest; TellerBeats.Short(ref p, TellerBeats.ShortWood + 1.8f, 50f);
            Assert.AreEqual(50f, p.yaw, 1f, "the teller points at the woodpile");
            var a = DollPose.Rest; TellerBeats.ShortListener(ref a, TellerBeats.ShortWood + 1.15f, false, 0, 50f);
            var b = DollPose.Rest; TellerBeats.ShortListener(ref b, TellerBeats.ShortWood + 1.15f, false, 6, 50f);
            Assert.Greater(a.yaw, 5f, "heads turn to it");
            Assert.Greater(a.yaw, b.yaw, "in a wave, 0.05 s apart");
            var none = DollPose.Rest; TellerBeats.ShortListener(ref none, TellerBeats.ShortWood + 1.6f, false, 0, float.NaN);
            Assert.AreEqual(0f, none.yaw, 1e-4f, "no woodpile: no turn");
        }

        [Test]
        public void RitualBeats_WhatIsInTheHands()
        {
            var g = new MomentGeometry { fireX = 0f, fireZ = 0.6f, glassX = 0f, glassZ = -2.1f, nx = 0f, nz = -1f, halfX = 3f, halfZ = 3f, centreZ = 0.8f };
            g.spots = new RitualSpots { ritual = RitualKind.Woodpile, hasPile = true, pileX = -0.6f, pileZ = 1f, pileFX = 0f, pileFZ = -1f };
            var c = new MomentCast { officiant = MomentPerson.Of(1, false, 0.3f, 0.9f, 2f), hasOfficiant = true };
            for (int i = 0; i < 6; i++) c.grown.Add(MomentPerson.Of(10 + i, false, -1f + 0.3f * i, 0.4f, 3f));
            for (int i = 0; i < 3; i++) c.kids.Add(MomentPerson.Of(-1 - i, true, 0.5f - 0.3f * i, 1.2f, 3f));
            var plan = MomentStagePlan.Build(MomentKind.Ritual, c, g, 7);
            Assert.AreEqual(RitualKind.Woodpile, plan.RitualStaged);
            var first = plan.members.First(m => m.steps.Any(s => s.beat == StageBeat.PassLog && s.arg == 0));
            int i0 = first.steps.FindIndex(s => s.beat == StageBeat.PassLog);
            var style = DollStyle.Stand; var kind = PropKind.None; var slot = PropSlot.Hand;
            RitualBeats.Look(plan, first, i0, plan.LogReaches(0, 0) + 0.4f, false, ref style, ref kind, ref slot);
            Assert.AreEqual(PropKind.Log, kind, "the first passer holds the first log");
            RitualBeats.Look(plan, first, i0, plan.LogReaches(0, 0) + 0.9f, false, ref style, ref kind, ref slot);
            Assert.AreEqual(PropKind.None, kind, "and has passed it on");
            foreach (var k in plan.members.Where(m => m.kid))
                for (int i = 0; i < k.steps.Count; i++)
                    for (float t = k.steps[i].t0; t < k.steps[i].t1; t += 0.25f)
                    {
                        var ks = DollStyle.Stand; var kk = PropKind.None; var ksl = PropSlot.Hand;
                        RitualBeats.Look(plan, k, i, t, false, ref ks, ref kk, ref ksl);
                        Assert.AreNotEqual(PropKind.Log, kk, "kids never hold a log");
                    }
        }

        [Test]
        public void TheLongTableByBoards_AndTheEmptyBasket()
        {
            var b0 = ConsequenceMeshes.Build(ConsequenceKind.LongTable, 0).bounds.size.x;
            var b6 = ConsequenceMeshes.Build(ConsequenceKind.LongTable, 6).bounds.size.x;
            var b20 = ConsequenceMeshes.Build(ConsequenceKind.LongTable, 20).bounds.size.x;
            Assert.AreEqual(6 * ConsequenceMeshes.BoardLength, b6 - b0, 0.01f, "one board longer a year");
            Assert.AreEqual(ConsequenceMeshes.MaxBoards * ConsequenceMeshes.BoardLength, b20 - b0, 0.01f, "at most 12 boards");
            var full = ConsequenceMeshes.Build(ConsequenceKind.OfferingBasket, 1).bounds.max.y;
            var empty = ConsequenceMeshes.Build(ConsequenceKind.OfferingBasket, ConsequenceMeshes.OfferingEmpty).bounds;
            Assert.Greater(empty.size.y, 0.05f, "the basket still stands");
            var gone = ConsequenceMeshes.Build(ConsequenceKind.OfferingBasket, ConsequenceMeshes.OfferingGone).bounds;
            Assert.Less(gone.size.y, 0.01f, "carried away: only the flattened grass");
            Assert.Greater(full, 0.1f);
        }
    }
}
