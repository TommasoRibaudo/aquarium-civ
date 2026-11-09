using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#437 the hard rule, against the real planner (placed-life spec §5.6; animation-specs "Placed life: hard in code" 5): over 40
    /// game years with the pack at full boldness on the scraps path, no wolf point the sim plans (the pack's poses, the eyes at the
    /// firelight, the grey wolf at the bones, the bone spot, the rack, the kills) is ever inside the tent ring, nearer the fire than its
    /// edge, or within 0.3 u of anyone the planner has standing there at that moment.</summary>
    public class WolfContactTests
    {
        static readonly float[] Hours = { 6f, 11f, 15f, 18.5f, 19.5f, 21f, 23.5f, 2f, 4f };

        [Test]
        public void FortyYears_FullBoldness_NoWolfPlanInsideTheTentRing_OrNearAPerson()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0);
            sim.DebugAddHides(8);
            Assert.IsTrue(sim.DebugOpenCampGate());
            Assert.GreaterOrEqual(sim.DebugPlaceWolves(), 0, "a far wood and a den");
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            sim.State.eco.wolfWay = (int)WolfWay.Scraps; sim.State.eco.wolfWayMonth = sim.State.month;
            var planner = LifePlanner.Wired(sim);
            var field = new WolfPack.Field();
            int kills = 0;
            sim.WolfKilled += k =>
            {
                var kg = sim.WolfGroundNow();
                float kx = SiteMap.CXc(k.cell), kz = SiteMap.CZc(k.cell);
                Assert.IsFalse(WolfRules.InTentRing(kx, kz, kg.ring), "a kill among the tents");
                Assert.GreaterOrEqual(Terrain.Dist(kx, kz, kg.fireX, kg.fireZ), WolfRules.FirelightEdge);
                kills++;
            };
            int checkedPoints = 0, shown = 0, eyes = 0, grey = 0;
            float nearest = float.MaxValue;
            var people = new List<float>();
            for (int year = 0; year < 40; year++)
            {
                sim.DebugAddHides(3); // the camp grows tents
                sim.Advance(1.0);
                sim.DebugWolvesBold();
                planner.Invalidate();
                sim.EcoWolfRefresh(field);
                var g = sim.WolfGroundNow();
                Assert.IsTrue(sim.WolfBoneSpot(out float bx, out float bz));
                Assert.IsFalse(WolfRules.InTentRing(bx, bz, g.ring), "the bone spot");
                Assert.IsTrue(sim.WolfRackSpot(out float rx, out float rz));
                Assert.IsFalse(WolfRules.InTentRing(rx, rz, g.ring), "the rack stands outside the ring");
                long day = 10 + year * 37;
                foreach (float hour in Hours)
                {
                    var t = new ViewTime(day, hour);
                    people.Clear();
                    foreach (var who in planner.People)
                        if (planner.PositionAt(who, t, out float x, out float z)) { people.Add(x); people.Add(z); }
                    Assert.Greater(people.Count, 4, "the planner has people standing somewhere");
                    double secs = (day * 24 + hour) * 10.0;
                    for (int id = 0; id < field.Count; id++)
                    {
                        var p = field.Sample(id, secs, day, hour, people);
                        if (!p.visible) continue;
                        shown++;
                        Check(p.x, p.z, g, people, $"wolf {id} ({p.act}) day {day} hour {hour}", ref checkedPoints, ref nearest);
                        if (p.act == WolfAct.Watch) eyes++;
                    }
                    for (int k = 0; k < 2; k++)
                    {
                        WolfPack.EyesSpot(g, sim.State.eco.seed, day, k, SiteMap.CXc(sim.WolfDen), SiteMap.CZc(sim.WolfDen), sim.WolfBoldness, out float ex, out float ez);
                        if (WolfRules.Keep(ref ex, ref ez, SiteMap.CXc(sim.WolfDen), SiteMap.CZc(sim.WolfDen), g, people, sim.WolfMinFromFire))
                            Check(ex, ez, g, people, "eyes", ref checkedPoints, ref nearest);
                    }
                    if (sim.GreyWolfSpot(people, out float gx, out float gz)) { grey++; Check(gx, gz, g, people, "the grey wolf", ref checkedPoints, ref nearest); }
                }
            }
            TestContext.Out.WriteLine($"{checkedPoints} points, {shown} poses shown, {eyes} at the firelight, grey wolf {grey}; nearest to a person {nearest:0.000} u; tents {sim.WolfGroundNow().ring.Count / 3}");
            Assert.Greater(checkedPoints, 1000);
            Assert.Greater(kills, 10, "the kills were checked where they fell");
            Assert.Greater(eyes, 10, "a bold pack comes to the firelight");
            Assert.Greater(grey, 100, "the grey wolf at the bones");
            Assert.Greater(sim.WolfGroundNow().ring.Count / 3, 2, "a camp of tents");
            Assert.GreaterOrEqual(nearest, WolfRules.PersonGap);
        }

        static void Check(float x, float z, WolfGround g, List<float> people, string what, ref int n, ref float nearest)
        {
            n++;
            Assert.IsFalse(WolfRules.InTentRing(x, z, g.ring), what + ": inside the tent ring");
            Assert.GreaterOrEqual(Terrain.Dist(x, z, g.fireX, g.fireZ), WolfRules.FirelightEdge - 1e-3f, what + ": inside the firelight");
            for (int k = 0; k + 1 < people.Count; k += 2)
            {
                float d = Terrain.Dist(x, z, people[k], people[k + 1]);
                if (d < nearest) nearest = d;
                Assert.GreaterOrEqual(d, WolfRules.PersonGap, what + ": too near a person");
            }
        }
    }
}
