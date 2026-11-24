// #694 (B24.b, B25, B55, B56): the dogs' and the wolves' scenes as pure timelines, and the drive-off planner's hard rules: every wolf
// point outside the tent ring and on the land, at least 0.6 u from the people driving it off (0.3 from everyone else), within 1.2 u of
// the camp's edge at its stop.
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class DogScenesTests
    {
        static readonly Vector2 Fire = new Vector2(0.2f, 0.8f);

        /// <summary>A camp: the fire and a ring of tents round it (x, z, r as WolfRules.TentRing makes them).</summary>
        static WolfGround Camp(int tents = 5, float ringR = 0.6f)
        {
            var g = new WolfGround { fireX = Fire.x, fireZ = Fire.y };
            for (int i = 0; i < tents; i++)
            {
                float a = i * Mathf.PI * 2f / tents + 0.3f;
                g.ring.Add(Fire.x + Mathf.Cos(a) * ringR); g.ring.Add(Fire.y + Mathf.Sin(a) * ringR); g.ring.Add(0.15f + WolfRules.TentPad);
            }
            return g;
        }

        static List<Vector2> Drivers() => new() { Fire + new Vector2(0.18f, -0.05f), Fire + new Vector2(-0.12f, 0.14f), Fire + new Vector2(0.02f, -0.2f) };

        [Test]
        public void DriveOff_EveryWolfPoint_OutsideTheTentRing_AndAtLeast06FromTheDrivers()
        {
            foreach (int tents in new[] { 0, 3, 5, 7 })
            {
                var g = Camp(tents);
                float edge = WolfDriveOff.CampEdge(g);
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    var toward = Fire + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f;
                    var drivers = Drivers();
                    var others = new List<float> { Fire.x - 0.3f, Fire.y + 0.1f, Fire.x + 0.25f, Fire.y + 0.25f };
                    var plan = WolfDriveOff.Make(g, toward, drivers, others);
                    string at = $"{tents} tents, from {a * Mathf.Rad2Deg:0} deg";
                    Assert.IsTrue(plan.ok, "a plan: " + at);
                    float stopR = (plan.wolfStop - Fire).magnitude;
                    Assert.LessOrEqual(stopR, edge + WolfDriveOff.NearEdge + 1e-3f, "it comes within 1.2 u of the camp's edge: " + at);
                    Assert.GreaterOrEqual(stopR, WolfRules.FirelightEdge, "never past the firelight's edge: " + at);
                    foreach (var p in WolfDriveOff.Points(plan))
                    {
                        Assert.IsFalse(WolfRules.InTentRing(p.x, p.y, g.ring), $"a wolf point among the tents ({p}): " + at);
                        Assert.IsTrue(WolfRules.OnLand(p.x, p.y), $"a wolf point off the land ({p}): " + at);
                        for (int i = 0; i < drivers.Count; i++)
                        {
                            Assert.GreaterOrEqual((p - drivers[i]).magnitude, WolfDriveOff.MinGap, $"0.6 u from driver {i} where they start: " + at);
                            Assert.GreaterOrEqual((p - plan.driverStop[i]).magnitude, WolfDriveOff.MinGap, $"0.6 u from driver {i} where they stop: " + at);
                        }
                        for (int i = 0; i + 1 < others.Count; i += 2)
                            Assert.GreaterOrEqual((p - new Vector2(others[i], others[i + 1])).magnitude, WolfRules.PersonGap, "0.3 u from everyone: " + at);
                    }
                    for (int i = 0; i < drivers.Count; i++)
                    {
                        Assert.LessOrEqual((plan.driverStop[i] - drivers[i]).magnitude, WolfDriveOff.Step + 1e-3f, "they walk at most 0.3 u toward it");
                        Assert.Greater((plan.wolfStop - plan.driverStop[i]).magnitude, WolfDriveOff.MinGap, "and stop short of 0.6 u");
                    }
                    Assert.Greater((plan.wolfAway - Fire).magnitude, (plan.wolfStop - Fire).magnitude, "it goes away into the dark");
                }
            }
        }

        [Test]
        public void DriveOff_Scene_TheWolfKeepsToThePlan_TheTorchSparks_NobodyTouchesIt()
        {
            var g = Camp(5);
            var drivers = Drivers();
            var plan = WolfDriveOff.Make(g, Fire + new Vector2(0f, 2.5f), drivers);
            Assert.IsTrue(plan.ok);
            var s = DogScenes.DriveOff(new[] { 11, 12, 13 }, drivers, plan, Fire, Vector2.down, 0f);
            Assert.AreEqual(CampUse.WolfDriveOff, s.use);
            var wolf = s.quads.Where(q => q.actor == 1).ToList();
            Assert.IsNotEmpty(wolf);
            var pts = WolfDriveOff.Points(plan);
            foreach (var q in wolf) Assert.Less(pts.Min(p => (p - q.pos).magnitude), 1e-3f, "every wolf part is a point of the plan");
            Assert.IsTrue(wolf.Any(q => q.act == QuadAct.Alert), "the wolf Alerts");
            Assert.IsTrue(s.tips.Any(t => t.kind == CampTipKind.Ember && t.grow > 1.5f), "a torch (the ember tip, bigger)");
            int sparks = s.fx.Count(f => f.kind == CampFxKind.Sparks && f.anchor == CampAnchor.TwigTip);
            Assert.GreaterOrEqual(sparks, 5, "sparks float off the torch");
            for (int m = 0; m < s.Count; m++)
                foreach (var part in s.parts[m])
                    if (!part.keepSpot) Assert.GreaterOrEqual(WolfDriveOff.MinTo(plan, part.pos), WolfDriveOff.MinGap, "no one goes nearer than 0.6 u");
            Assert.Greater(s.a, 0f); Assert.Less(s.a, s.Length);
        }

        [Test]
        public void Guard_TheDogBarks_TheWolfTurnsBack_AGrownUpScratchesItsHead()
        {
            var g = Camp(4);
            var plan = WolfDriveOff.Make(g, Fire + new Vector2(2f, 1f), null);
            Assert.IsTrue(plan.ok);
            var s = DogScenes.DogGuard(3, Fire + new Vector2(0.25f, 0f), plan, Fire, 7, Fire + new Vector2(-0.2f, -0.1f), Vector2.down, 0f);
            Assert.AreEqual(3, s.dog);
            Assert.IsTrue(s.quads.Any(q => q.actor == 0 && q.act == QuadAct.Bark && q.Len >= 3f * DogGeo.BarkEvery - 1e-3f), "three barks");
            Assert.IsTrue(s.quads.Any(q => q.actor == 1 && q.act == QuadAct.Alert && q.from >= s.a), "the wolf Alerts at the bark");
            Assert.IsTrue(s.quads.Any(q => q.actor == 1 && (q.pos - plan.wolfAway).magnitude < 1e-3f), "and goes back into the dark");
            Assert.Greater(s.c, s.a, "the scratch after the bark");
            Assert.AreEqual(WolfDriveOff.Answer.Guard, WolfDriveOff.AnswerFor(true, 3));
            Assert.AreEqual(WolfDriveOff.Answer.DriveOff, WolfDriveOff.AnswerFor(false, 2));
            Assert.AreEqual(WolfDriveOff.Answer.HoldKids, WolfDriveOff.AnswerFor(false, 1));
            var last = s.quads.Where(q => q.actor == 0).OrderBy(q => q.to).Last();
            Assert.AreEqual(QuadAct.Sleep, last.act, "the dog curls up by the fire after");
            Assert.AreEqual(DogGeo.FireSleep, (last.pos - Fire).magnitude, 1e-3f, "at the fire ring's edge, never in it");
        }

        [Test]
        public void KidsClose_PairsDrawnTogether_AllFacingTheWolf()
        {
            var g = Camp(3);
            var plan = WolfDriveOff.Make(g, Fire + new Vector2(-2f, 0.5f), null);
            var kids = new List<Vector2> { Fire + new Vector2(0.3f, 0.1f), Fire + new Vector2(-0.2f, -0.3f) };
            var grown = new List<Vector2> { Fire + new Vector2(0.5f, 0.2f), Fire + new Vector2(-0.1f, -0.6f) };
            var s = DogScenes.KidsClose(new[] { 1, 2 }, grown, new[] { 5, 6 }, kids, plan.wolfStop, plan, 6f, Vector2.down, 0f);
            for (int i = 0; i < 2; i++)
            {
                var gHold = s.parts[2 * i].First(p => p.from >= s.a - 1e-3f);
                var kHold = s.parts[2 * i + 1].First(p => p.from >= s.a - 1e-3f);
                Assert.AreEqual(DogGeo.PressIn, (gHold.pos - kHold.pos).magnitude, 1e-3f, "the kid presses in (0.04 u)");
                Assert.AreEqual(plan.wolfStop, gHold.face); Assert.AreEqual(plan.wolfStop, kHold.face);
                Assert.GreaterOrEqual(gHold.Len, 6f, "held until the wolves are gone, plus 2 s");
            }
            Assert.IsTrue(s.quads.Any(q => q.actor == 1), "a wolf in view");
        }

        [Test]
        public void ThePupsScenes_FollowTheSpec()
        {
            var at = new Vector2(0.5f, 0.5f);
            var chew = DogScenes.PupChew(1, at, 4, at + Vector2.right * 0.2f, at + Vector2.up * 0.1f, Vector2.down, 0f);
            Assert.IsTrue(chew.quads.Any(q => q.act == QuadAct.Chew && q.held == QuadHeld.Hide), "chews a hide");
            var play = DogScenes.PupPlay(1, at, new[] { 4, 5 }, new[] { at + Vector2.right * 0.3f, at + Vector2.left * 0.3f }, at, Vector2.down, 0f);
            Assert.AreEqual(3, play.quads.Count(q => q.act == QuadAct.PlayBow), "two bows and one at the end");
            var sleep = DogScenes.PupSleep(1, at, Fire, Vector2.down, 10f, 0f);
            var curl = sleep.quads.Last();
            Assert.AreEqual(QuadAct.Sleep, curl.act);
            Assert.AreEqual(DogGeo.EmberSleep, (curl.pos - Fire).magnitude, 1e-3f, "by the embers, not in them");
            var steal = DogScenes.PupSteal(1, at, Fire + Vector2.right * 0.4f, 9, Fire, Vector2.right, Vector2.down, 0f);
            Assert.IsTrue(steal.quads.Any(q => q.held == QuadHeld.Fish && q.speed >= DogGeo.DogRun - 1e-3f), "runs with the fish in its mouth");
            var chase = steal.parts[0].First(p => p.run);
            Assert.AreEqual(DogGeo.ChaseFor, (chase.pos - steal.parts[0][0].pos).magnitude, 0.02f, "chases 0.5 u and gives up");
            var shoo = DogScenes.PupShoo(1, at, Fire + Vector2.right * 0.4f, 9, Fire, Vector2.down, 0f);
            Assert.IsTrue(shoo.quads.Any(q => q.act == QuadAct.Growl && q.Len >= 0.5f - 1e-3f), "growls, head low, 0.5 s");
            var run = DogScenes.PupRunAlong(1, at, new[] { 4, 5 }, new[] { at, at + Vector2.right * 0.1f }, at, Vector2.right, Vector2.down, 0f);
            Assert.IsTrue(run.quads.Any(q => q.speed >= DogGeo.DogRun - 1e-3f), "bounds along with them");
            foreach (var s in new[] { chew, play, sleep, steal, shoo, run }) { Assert.AreEqual(1, s.dog); Assert.Greater(s.Length, 4f, s.use.ToString()); Assert.Less(s.Length, 40f, s.use.ToString()); }
        }

        [Test]
        public void TheGrownDogsScenes_FollowTheSpec()
        {
            var at = new Vector2(0.5f, 0.5f);
            var hunt = DogScenes.DogHunt(2, at, 8, Fire, Fire + new Vector2(1.2f, 0.3f), Vector2.down, 0f);
            Assert.Less(hunt.a, hunt.b, "the flush, then the throw");
            Assert.Greater(hunt.c, hunt.b, "the pat after the catch");
            Assert.IsTrue(hunt.parts[0].Any(p => p.prop == PropKind.Rabbit), "the catch held up");
            var fetch = DogScenes.Fetch(4, at, 2, at + Vector2.right * 0.2f, at, Vector2.down, 3, 0f);
            Assert.AreEqual(3, fetch.arcs.Count, "three throws");
            foreach (var arc in fetch.arcs) { Assert.AreEqual(DogGeo.FetchH, arc.h, 1e-4f); Assert.AreEqual(DogGeo.FetchT, arc.seconds, 1e-4f); }
            Assert.AreEqual(3, fetch.quads.Count(q => q.held == QuadHeld.Stick), "the stick back in its mouth each time");
            Assert.AreEqual(3, fetch.quads.Count(q => q.act == QuadAct.PlayBow), "a play bow after each drop");
            var ride = DogScenes.Ride(4, at, 5, at + Vector2.left * 0.2f, 2, at + Vector2.right * 0.2f, at, Vector2.right, Vector2.down, 0f);
            var walk = ride.quads.First(q => q.speed == DogGeo.RideSpeed);
            Assert.AreEqual(DogGeo.RideLen / DogGeo.RideSpeed, walk.Len, 1e-3f, "0.4 u slowly");
            Assert.IsTrue(ride.quads.Any(q => q.act == QuadAct.Lie && q.from >= walk.to - 1e-3f), "then it lies down");
            Assert.Greater(ride.b, ride.a, "the kid slides off after the ride");
            var fire = DogScenes.DogFire(2, at, Fire, 4, at, Vector2.down, 0f);
            var curl = fire.quads.First(q => q.act == QuadAct.Sleep);
            Assert.AreEqual(DogGeo.FireSleep, (curl.pos - Fire).magnitude, 1e-3f, "0.25 u from the fire");
            Assert.AreEqual(1, fire.Count, "a kid lies with the head on it");
            var sun = DogScenes.DogSun(2, at, at + Vector2.up * 0.3f, Vector2.down, 8f, 0f);
            Assert.IsTrue(sun.quads.Any(q => q.act == QuadAct.Lie && q.Len >= 8f - 1e-3f));
        }
    }
}
