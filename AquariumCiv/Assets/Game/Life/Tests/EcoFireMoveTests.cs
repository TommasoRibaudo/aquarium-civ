using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#246 (keepers-hands §2.1): with the Keeper's first green the tribe carries its fire to the green's edge, once:
    /// a legal spot (dry, above the flood line, clear of the tent, inside the glass), the fire's places follow it, the people
    /// carry it over as a scene, and the ecology off and an older save keep their fire where it is.</summary>
    public class EcoFireMoveTests
    {
        static readonly int[] Seeds = { 1, 7, 33 };

        static SimCore Barren(int seed, bool ecology = true)
        {
            var cfg = new SimConfig { ecology = ecology };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>The first run's first-mist spot (by the camp, out past the people).</summary>
        static (float x, float z) FirstMistSpot(SimCore sim) => FirstRun.Begin(sim).SuggestedRainSpot;

        static (float x, float z) Fire(SimCore sim) { sim.Settlement.Fire(out float x, out float z, out _); return (x, z); }

        static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        static float PointToLot(float x, float z, Lot l)
        {
            float r = l.yaw * ((float)Math.PI / 180f);
            float cs = (float)Math.Cos(r), sn = (float)Math.Sin(r);
            float dx = x - l.x, dz = z - l.z;
            float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
            float ox = Math.Max(0f, Math.Abs(lx) - l.w / 2f), oz = Math.Max(0f, Math.Abs(lz) - l.d / 2f);
            return (float)Math.Sqrt(ox * ox + oz * oz);
        }

        static Building CampB(SimCore sim) => sim.State.buildings.First(b => b.type == BuildingType.Camp);

        [Test]
        public void TheFirstMist_CarriesTheFireOnce_ToALegalSpotAtTheGreensEdge([ValueSource(nameof(Seeds))] int seed)
        {
            var sim = Barren(seed);
            var st = sim.Settlement;
            var (ox, oz) = Fire(sim);
            Assert.AreEqual(st.GreenX, ox, 1e-4f, "the fire starts on the green's middle");
            Assert.IsFalse(sim.EcoFireMove.moved);
            int told = 0;
            sim.EcoFireMoved += _ => told++;

            var (mx, mz) = FirstMistSpot(sim);
            var greened = sim.Mist(mx, mz, 2f);
            Assert.IsNotEmpty(greened, "the first mist greens");
            var m = sim.EcoFireMove;
            Assert.IsTrue(m.moved, "the tribe carried its fire");
            Assert.AreEqual(1, m.count);
            Assert.AreEqual(1, told);
            Assert.AreEqual(sim.State.month, m.month);
            Assert.AreEqual(ox, m.fromX, 1e-4f); Assert.AreEqual(oz, m.fromZ, 1e-4f);
            var (fx, fz) = Fire(sim);
            Assert.AreEqual(m.toX, fx, 1e-4f); Assert.AreEqual(m.toZ, fz, 1e-4f);
            st.Fire(out _, out _, out bool lit);
            Assert.IsTrue(lit, "it still burns");
            TestContext.WriteLine($"seed {seed}: fire ({ox:0.00},{oz:0.00}) -> ({fx:0.00},{fz:0.00}), {m.Distance:0.00} u; mist at ({mx:0.00},{mz:0.00})");

            // Next to the green: its edge, not on it.
            float edge = float.MaxValue;
            for (int c = 0; c < SiteMap.N; c++)
                if (sim.EcoCoverAt(c) != EcoCover.Bare && sim.EcoCoverAt(c) != EcoCover.Water) edge = Math.Min(edge, Dist(fx, fz, SiteMap.CXc(c), SiteMap.CZc(c)));
            Assert.That(edge, Is.InRange(SimCore.FireEdgeMin - 1e-3f, SimCore.FireEdgeMax + 1e-3f), "at the green's edge");
            Assert.Less(Dist(fx, fz, mx, mz), 1.6f, "by the mist that greened it");
            // Legal: dry, above the flood line, off the water, inside the glass, clear of the tent.
            var at = sim.EcoAt(fx, fz);
            Assert.IsFalse(at.belowFloodLine, "not in the hollow");
            Assert.AreNotEqual(EcoCover.Water, at.cover);
            int cell = SiteMap.CellAt(fx, fz);
            Assert.IsFalse(st.Map.Water(cell) || st.Map.Rock(cell), "dry ground");
            Assert.Greater(st.WalkCost(cell), 0, "they can walk to it");
            Assert.Less(Math.Abs(fx), Terrain.HX - Terrain.GlassMargin - 0.4f);
            Assert.Less(Math.Abs(fz), Terrain.HZ - Terrain.GlassMargin - 0.4f);
            foreach (var l in st.Lots.Where(l => !l.open && !l.retired))
                Assert.GreaterOrEqual(PointToLot(fx, fz, l), SimCore.FireTentClearance - 1e-3f, $"clear of the {l.kind}");

            // Once: another mist elsewhere, and a year of play, leave it where it is.
            sim.Mist(-mx * 0.5f, mz + 0.8f, 3f);
            for (int i = 0; i < 12; i++) sim.Step();
            Assert.AreEqual(1, sim.EcoFireMove.count, "once");
            var (fx2, fz2) = Fire(sim);
            Assert.AreEqual(fx, fx2, 1e-4f); Assert.AreEqual(fz, fz2, 1e-4f);
        }

        [Test]
        public void TheFiresPlaces_FollowIt()
        {
            var sim = Barren(7);
            var planner = LifePlanner.Wired(sim);
            var fire = new Place(PlaceKind.Fire, CampB(sim).id);
            Assert.IsTrue(planner.Positions.TryGetPosition(fire, out float bx, out float bz));      // cached before the move
            Assert.IsTrue(planner.Positions.TryGetPosition(CampStash.Stand, out float sx0, out float sz0));
            Assert.IsFalse(planner.Positions.TryGetPosition(LifePlanner.OldFirePlace, out _, out _), "no old fire before the carry");

            var (mx, mz) = FirstMistSpot(sim);
            sim.Mist(mx, mz, 2f);
            var m = sim.EcoFireMove;
            Assert.IsTrue(m.moved);
            Assert.IsTrue(planner.Positions.TryGetPosition(fire, out float x, out float z));
            Assert.AreEqual(m.toX, x, 1e-4f, "the fire's place is the new fire"); Assert.AreEqual(m.toZ, z, 1e-4f);
            Assert.AreEqual(bx, m.fromX, 1e-4f);
            Assert.IsTrue(planner.Positions.TryGetPosition(LifePlanner.OldFirePlace, out float ox, out float oz));
            Assert.AreEqual(m.fromX, ox, 1e-4f); Assert.AreEqual(m.fromZ, oz, 1e-4f);
            // The stash (where the roots and the wood go) sits by the new fire.
            Assert.IsTrue(planner.Positions.TryGetPosition(CampStash.Stand, out float sx, out float sz));
            Assert.AreEqual(CampStash.StandRadius, Dist(sx, sz, x, z), 1e-3f);
            Assert.Greater(Dist(sx, sz, sx0, sz0), 0.1f, "the stash moved with it");
            Assert.IsTrue(planner.TryGetStash(out var look));
            Assert.Less(Dist(look.x, look.z, x, z), CampStash.PileRadius + 0.01f);

            // The day: the gathering, the fire-side work and the roots carried home go to the new fire, never the old one.
            var acts = new List<PersonActivity>();
            int atFire = 0;
            for (long day = sim.State.month; day < sim.State.month + 3; day++)
                for (float h = 6f; h < 23f; h += 0.5f)
                {
                    planner.PlanAll(new ViewTime(day, h), acts);
                    foreach (var a in acts)
                    {
                        Assert.AreNotEqual(PlaceKind.OldFire, a.place.kind, "the day never sends anyone to the old fire");
                        if (a.place == fire || a.place == CampStash.Stand) atFire++;
                    }
                }
            Assert.Greater(atFire, 0, "they still come to the fire");
            Assert.AreEqual(0, planner.FallbackCount);
        }

        [Test]
        public void ThePeopleCarryItOver_AsAScene()
        {
            var sim = Barren(7);
            var planner = LifePlanner.Wired(sim);
            var (mx, mz) = FirstMistSpot(sim);
            Assert.IsFalse(planner.FireMovePending);
            sim.Mist(mx, mz, 2f);
            Assert.IsTrue(planner.FireMovePending, "a carry to play");

            Assert.IsNull(planner.StartFireMove(new ViewTime(sim.State.month, 22.5f)), "not in the dark");
            Assert.IsTrue(planner.FireMovePending, "still to play, in the morning");
            var t0 = new ViewTime(sim.State.month, 9f);
            var info = planner.StartFireMove(t0);
            Assert.IsNotNull(info, "staged by day");
            Assert.IsFalse(planner.FireMovePending, "staged once");
            var again = planner.StartFireMove(t0.AddHours(0.1f));
            Assert.IsNotNull(again, "asking again returns the one playing");
            Assert.AreEqual(info.start, again.start, 1e-5f);
            var owner = info.cast.Where(c => c.role == RelicCast.Owner).Select(c => c.who).ToList();
            Assert.AreEqual(1, owner.Count, "someone carries the embers");
            TestContext.WriteLine($"cast: {string.Join(", ", info.cast.Select(c => c.role))}; {info.start:0.00}h-{info.end:0.00}h");

            Assert.IsTrue(planner.FireMoveAt(t0, out var look));
            Assert.AreEqual(0, look.phase, "the old fire burns as they come to it");
            Assert.IsTrue(planner.FireMoveAt(new ViewTime(t0.day, (look.liftHour + look.setHour) / 2f), out var mid));
            Assert.AreEqual(1, mid.phase, "carried");
            Assert.IsTrue(planner.FireMoveAt(new ViewTime(t0.day, (look.setHour + look.end) / 2f), out var late));
            Assert.AreEqual(2, late.phase, "the new fire burns");

            // Someone walks over with the embers, and others with the stones.
            bool embers = false, stones = false;
            for (float h = look.liftHour; h < look.setHour; h += 0.01f)
                foreach (var who in info.cast.Select(c => c.who))
                {
                    var a = planner.Plan(who, new ViewTime(t0.day, h));
                    if (a.kind == ActivityKind.Carry && a.prop == Prop.Embers) embers = true;
                    if (a.kind == ActivityKind.Carry && a.prop == Prop.Block) stones = true;
                }
            Assert.IsTrue(embers, "the embers are carried");
            if (info.cast.Any(c => c.role == RelicCast.Grown)) Assert.IsTrue(stones, "the stones are carried");
            // Before the lift the carrier is at the old fire; at the end at the new one.
            Assert.IsTrue(planner.PositionAt(owner[0], new ViewTime(t0.day, look.liftHour - 0.01f), out float px, out float pz));
            Assert.Less(Dist(px, pz, look.fromX, look.fromZ), 0.6f, "picks up at the old fire");
            Assert.IsTrue(planner.PositionAt(owner[0], new ViewTime(t0.day, look.end - 0.01f), out px, out pz));
            Assert.Less(Dist(px, pz, look.toX, look.toZ), 0.6f, "ends at the new fire");

            Assert.IsNull(planner.StartFireMove(new ViewTime(t0.day, look.end + 0.2f)), "played once");
            Assert.IsNotNull(planner.StartFireMove(new ViewTime(t0.day, look.end + 0.2f), force: true), "the debug panel can replay it");
        }

        [Test]
        public void TheEcologyOff_KeepsItsFire()
        {
            var sim = Barren(7, ecology: false);
            var (ox, oz) = Fire(sim);
            Assert.IsEmpty(sim.Mist(1f, 0.5f, 2f));
            Assert.IsFalse(sim.DebugMoveFireNow());
            for (int i = 0; i < 12; i++) sim.Step();
            Assert.IsFalse(sim.EcoFireMove.moved);
            var (fx, fz) = Fire(sim);
            Assert.AreEqual(ox, fx, 1e-6f); Assert.AreEqual(oz, fz, 1e-6f);
            var planner = LifePlanner.Wired(sim);
            Assert.IsFalse(planner.FireMovePending);
            Assert.IsNull(planner.StartFireMove(new ViewTime(sim.State.month, 9f)));
        }

        [Test]
        public void AnOlderSave_KeepsItsFire_AndTheMoveRoundTrips()
        {
            Assert.IsFalse(new EcoState().fireMoveArmed, "a save without the field loads unarmed");
            Assert.AreEqual(-1, new EcoState().fireMovedMonth);

            var sim = Barren(7);
            string json = UnityEngine.JsonUtility.ToJson(sim.State);
            Assert.IsTrue(json.Contains("\"fireMoveArmed\":true"), "a new box is armed");
            var oldState = UnityEngine.JsonUtility.FromJson<SimState>(json.Replace("\"fireMoveArmed\":true,", "").Replace(",\"fireMoveArmed\":true", ""));
            var old = new SimCore(sim.Config, oldState);
            Assert.IsFalse(old.State.eco.fireMoveArmed);
            var (ox, oz) = Fire(old);
            var (mx, mz) = FirstMistSpot(old);
            Assert.IsNotEmpty(old.Mist(mx, mz, 2f));
            Assert.IsFalse(old.EcoFireMove.moved, "an older box keeps its fire where it is");
            var (fx, fz) = Fire(old);
            Assert.AreEqual(ox, fx, 1e-6f); Assert.AreEqual(oz, fz, 1e-6f);

            // A carried fire stays carried through a save.
            sim.Mist(mx, mz, 2f);
            Assert.IsTrue(sim.EcoFireMove.moved);
            var back = new SimCore(sim.Config, UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(sim.EcoFireMove.count, back.EcoFireMove.count);
            var (bx, bz) = Fire(back);
            Assert.AreEqual(sim.EcoFireMove.toX, bx, 1e-4f); Assert.AreEqual(sim.EcoFireMove.toZ, bz, 1e-4f);
            Assert.IsFalse(back.State.eco.fireMoveArmed, "and doesn't move again");
        }

        [Test]
        public void AnOddSproutOnItsOwn_DoesntMoveIt_TheKeepersFirstGreenDoes()
        {
            var sim = Barren(7);
            for (int i = 0; i < 12 * 6 && sim.EcoMilestoneMonth(EcoMilestones.FirstGreen) < 0; i++) sim.Step();
            Assume.That(sim.EcoMilestoneMonth(EcoMilestones.FirstGreen), Is.GreaterThanOrEqualTo(0), "an odd sprout came up");
            Assert.IsFalse(sim.EcoFireMove.moved, "not for an odd sprout");
            if (!sim.BeforeCamp) return;
            var (mx, mz) = FirstMistSpot(sim);
            sim.Mist(mx, mz, 2f);
            Assert.IsTrue(sim.EcoFireMove.moved, "the Keeper's first green");
        }

        [Test]
        public void TheCheat_MovesTheFireNow_EvenOnABoxWithNoGreen()
        {
            var sim = Barren(1);
            Assert.IsTrue(sim.DebugMoveFireNow(), "mists the first spot and carries the fire");
            Assert.AreEqual(1, sim.EcoFireMove.count);
            Assert.Greater(sim.EcoMeadowShare, 0f);
            Assert.IsFalse(sim.DebugMoveFireNow(), "by the green already");
            Assert.AreEqual(1, sim.EcoFireMove.count);
        }

        [Test]
        public void TheTentsTheCampRaisesLater_LeaveTheFireClear()
        {
            var sim = Barren(7);
            var (mx, mz) = FirstMistSpot(sim);
            sim.Mist(mx, mz, 2f);
            Assert.IsTrue(sim.EcoFireMove.moved);
            sim.DebugOpenCampGate();
            int lots0 = sim.Settlement.Lots.Count;
            for (int i = 0; i < 12 * 4; i++) sim.Step();
            var (fx, fz) = Fire(sim);
            TestContext.WriteLine($"lots {lots0} -> {sim.Settlement.Lots.Count}");
            foreach (var l in sim.Settlement.Lots.Where(l => !l.open && !l.retired))
                Assert.GreaterOrEqual(PointToLot(fx, fz, l), 0.3f, $"the {l.kind} (lot {l.id}) leaves the fire's seats clear");
            var sanity = SettleChecks.Check(sim.Settlement, sim.State.buildings);
            Assert.AreEqual(0, sanity.overlaps, sanity.ToString());
        }
    }
}
