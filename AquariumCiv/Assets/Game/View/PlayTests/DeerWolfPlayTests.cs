using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #425 (animation-specs P0, placed-life §5.3 / §5.6): the shared quadruped's everyday deer and wolves. After the arrival they stay
    /// (no walking off into the trees), as many as the sim counts, near their home, out from under the tree crowns; deer look up at a person
    /// and walk off, bolt from a knock; wolves keep every rule (never in the tent ring, never within 0.3 u of a person) day, dusk and night,
    /// and sit to howl at dusk; the first dusk's howl is saved and plays once. Shots: Temp/agent/p9-quad-*.png.
    /// </summary>
    [Category("LifeArrival")]
    public class DeerWolfPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        IEnumerator LivingLand()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.Table(0.3f);
            Sim.DebugGrowFarWood();
            yield return Frames(20);
            Sim.DebugJarsReady();
            yield return new WaitForSecondsRealtime(4f);
        }

        /// <summary>A close look at a box-local point (instant), for the shots.</summary>
        static IEnumerator Look(Vector3 local, float zoom, string shot)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            Cam.FlyTo(box.transform.TransformPoint(local), zoom, 0f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(shot);
        }

        static int Meshes(string holder)
        {
            var h = GameObject.Find(holder);
            return h == null ? 0 : h.GetComponentsInChildren<MeshFilter>(true).Length;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Deer_StayAndLive_NearHome()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance;
            int cell = Sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            Assert.GreaterOrEqual(cell, 0, "the deer placed");
            yield return Until(() => !la.Playing(LifeSpecies.Deer), 30f);
            yield return Frames(5);
            var dv = DeerView.Instance;
            Assert.IsNotNull(dv, "the deer view");
            Assert.AreEqual(Sim.EcoDeerShown(), dv.Shown, $"the shown count follows the sim ({dv.Status()})");
            Assert.AreEqual(3, dv.Shown, "the founding stag and two does");
            // they stay: 20 s later still there, near home, never under a crown
            var at = new List<Vector3>();
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSecondsRealtime(1f);
                Assert.AreEqual(3, dv.Shown, $"still shown after {i + 1} s: they don't walk off into the trees");
            }
            Assert.AreEqual(0, dv.UnderCrownFrames, "never under a tree crown: " + dv.Status());
            float hx = AquariumCiv.Sim.Settle.SiteMap.CXc(Sim.EcoDeerHome), hz = AquariumCiv.Sim.Settle.SiteMap.CZc(Sim.EcoDeerHome);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(dv.TryGetDeer(i, out var p, out _), "deer " + i);
                Assert.Less(new Vector2(p.x - hx, p.z - hz).magnitude, DeerHerd.RangeRadius + 0.6f, $"deer {i} lives near its home");
            }
            int stags = 0; for (int i = 0; i < 3; i++) if (dv.TryGetDeer(i, out _, out bool st) && st) stags++;
            Assert.AreEqual(1, stags, "one stag");
            yield return Shot("p9-quad-deer-default");
            dv.TryGetDeer(0, out var d0, out _);
            yield return Look(d0, 0.3f, "p9-quad-deer-close-day");
            int mesh = Meshes("Deer");
            Debug.Log($"[p9-quad] deer meshes {mesh}: {dv.Status()}");
            Assert.LessOrEqual(mesh, 3 * 8 + DeerView.MaxFawns * 8, "about 8 parts a deer");
            // a person comes near: heads up, then they walk off a little
            dv.Scarers.Add(new Vector2(d0.x + 0.2f, d0.z));
            yield return Until(() => dv.Alerted > 0, 3f);
            Assert.Greater(dv.Alerted, 0, "heads up at a person");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Look(d0, 0.3f, "p9-quad-deer-alert");
            yield return Until(() => dv.WalkedOff > 0, 4f);
            Assert.Greater(dv.WalkedOff, 0, "then they walk off");
            dv.Scarers.Clear();
            yield return new WaitForSecondsRealtime(1f);
            // a knock: they bolt
            int k0 = dv.Knocks;
            Assert.Greater(dv.KnockAtGround(d0.x - 0.1f, d0.z - 0.3f), 0, "a knock reaches them");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.Greater(dv.Bolting, 0, "they bolt");
            yield return Shot("p9-quad-deer-bolt");
            yield return new WaitForSecondsRealtime(4f);
            Assert.AreEqual(3, dv.Shown, "still all there after the bolt");
            Assert.AreEqual(0, dv.UnderCrownFrames, "and never under a crown: " + dv.Status());
            // night: bedded down
            PeopleView.DebugSetHour(23f);
            yield return new WaitForSecondsRealtime(2f);
            dv.TryGetDeer(0, out d0, out _);
            yield return Look(d0, 0.3f, "p9-quad-deer-close-night");
            Assert.AreEqual(3, dv.Shown, "shown at night");
            AssertNoErrors("everyday deer");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Wolves_StayAtTheDen_KeepTheRules_AndHowlOnce()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance;
            PeopleView.DebugSetHour(9f);
            int den = Sim.DebugPlaceWolves();
            Assert.GreaterOrEqual(den, 0, "the wolves placed");
            Assert.IsTrue(Sim.WolfFirstHowlDue, "their first howl waits for dusk");
            yield return Until(() => !la.Playing(LifeSpecies.Wolves), 30f);
            yield return Frames(5);
            var wv = WolfView.Instance;
            Assert.IsNotNull(wv, "the wolf view");
            Assert.AreEqual(Sim.EcoWolfCount, wv.Shown + 0, $"the pack shown ({wv.Status()})");
            var people = new List<float>();
            var pv = PeopleView.Find();
            var who = new List<AquariumCiv.Life.PersonRef>(); var px = new List<float>(); var pz = new List<float>();
            var pos = new List<Vector3>();
            IEnumerator Watch(float seconds)
            {
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < seconds)
                {
                    pv.StandingNow(who, px, pz);
                    people.Clear(); for (int i = 0; i < px.Count; i++) { people.Add(px[i]); people.Add(pz[i]); }
                    wv.LocalPositions(pos);
                    foreach (var p in pos)
                    {
                        var g = Sim.WolfGroundNow();
                        Assert.IsFalse(WolfRules.InTentRing(p.x, p.z, g.ring), $"a wolf in the tent ring at ({p.x:0.00},{p.z:0.00})");
                        Assert.IsFalse(WolfRules.NearPerson(p.x, p.z, people), $"a wolf within 0.3 u of a person at ({p.x:0.00},{p.z:0.00})");
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
            yield return Watch(15f);
            Assert.AreEqual(Sim.EcoWolfCount, wv.Shown, "still at the den after 15 s: they don't vanish into the trees");
            Assert.AreEqual(0, wv.RuleBreaks, wv.Status());
            Assert.AreEqual(0, wv.UnderCrownFrames, "out from under the crowns: " + wv.Status());
            yield return Shot("p9-quad-wolves-default");
            wv.WorldPositions(pos);
            var box = Object.FindAnyObjectByType<BoxView>();
            yield return Look(box.transform.InverseTransformPoint(pos[0]), 0.3f, "p9-quad-wolves-close-day");
            Debug.Log($"[p9-quad] wolf meshes {Meshes("Wolves")}: {wv.Status()}");
            // a person near the den: the wolves keep 0.3 u (the rules are checked above every frame)
            wv.LocalPositions(pos);
            wv.Scarers.Add(new Vector2(pos[0].x + 0.1f, pos[0].z));
            yield return new WaitForSecondsRealtime(1f);
            wv.LocalPositions(pos);
            foreach (var p in pos) Assert.GreaterOrEqual(new Vector2(p.x - wv.Scarers[0].x, p.z - wv.Scarers[0].y).magnitude, WolfRules.PersonGap - 0.01f, "0.3 u from a person");
            wv.Scarers.Clear();
            // dusk: the first howl plays by itself, once, and is saved
            int h0 = la.FirstHowls;
            PeopleView.DebugSetHour(19f);
            yield return Until(() => la.FirstHowls > h0, 5f);
            Assert.AreEqual(h0 + 1, la.FirstHowls, "the first dusk's howl");
            Assert.IsFalse(Sim.WolfFirstHowlDue, "saved: never twice");
            Assert.IsTrue(Sim.State.eco.firstHowlHeard, "in the save");
            yield return new WaitForSecondsRealtime(1.5f);
            wv.WorldPositions(pos);
            yield return Look(box.transform.InverseTransformPoint(pos[0]), 0.3f, "p9-quad-wolves-howl");
            yield return Watch(4f);
            PeopleView.DebugSetHour(19.5f);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(h0 + 1, la.FirstHowls, "the first howl plays only once");
            // night: lying at the den, or watching at the firelight's edge, never among the tents
            PeopleView.DebugSetHour(23f);
            yield return Watch(8f);
            Assert.AreEqual(0, wv.RuleBreaks, wv.Status());
            yield return Shot("p9-quad-wolves-night");
            AssertNoErrors("everyday wolves");
        }

        /// <summary>P0: every look builds, and the dog's drift moves the look by generation (ears droop, patches, a curled tail).</summary>
        [UnityTest]
        public IEnumerator Quadruped_Looks_And_DogDrift()
        {
            var root = new GameObject("QuadLooks").transform;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            foreach (QuadKind k in System.Enum.GetValues(typeof(QuadKind)))
            {
                if (k == QuadKind.None) continue;
                var d = new QuadDoll(QuadLook.Of(k), root, mat);
                var p = QuadPose.Stand(Vector3.zero, 0f);
                QuadMotion.Graze(ref p, 1f, 1f, 0); d.Apply(p);
                QuadMotion.SitHowl(ref p, 1f); d.Apply(p);
                QuadMotion.Bolt(ref p, 0.3f, true); d.Apply(p);
                Assert.Greater(d.go.GetComponentsInChildren<MeshFilter>().Length, 6, k + ": its parts");
            }
            var g0 = QuadLook.Dog(0, 7); var g4 = QuadLook.Dog(4, 7);
            Assert.Less(g0.earDroop, 0.15f, "generation 0 is a wolf: ears up");
            Assert.Greater(g4.earDroop, 0.85f, "generation 4: floppy ears");
            Assert.Greater(g4.tailCurl, 0.85f, "and a curled tail");
            Assert.Less(g4.muzzle, g0.muzzle, "a shorter muzzle");
            Assert.AreNotEqual(QuadLook.Dog(2, 1).earDroop, QuadLook.Dog(2, 2).earDroop, "litter-mates differ");
            Object.Destroy(root.gameObject); Object.Destroy(mat);
            yield return null;
        }
    }
}
