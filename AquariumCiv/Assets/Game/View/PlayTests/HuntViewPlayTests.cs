using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #283 phase 2: the hunt in the meadow at a phone size. A hunter takes a spear, walks out toward a real rabbit, crouches and
    /// stalks, strikes; a catch takes the rabbit out of the view and the count (a hide dries by the fire, the rabbit is carried
    /// back over the shoulder), a miss makes it bolt. Screenshots go to Temp/agent/hunt_*.png.
    /// </summary>
    [Category("HuntView")]
    public class HuntViewPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        static void Meadow()
        {
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
        }

        static PersonActivity ActOf(PeopleView pv, PersonRef who)
        {
            foreach (var a in pv.Activities) if (a.who == who) return a;
            return default;
        }

        static Vector2 HunterAt(PeopleView pv, PersonRef who)
        {
            var ws = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(ws, xs, zs);
            for (int i = 0; i < ws.Count; i++) if (ws[i] == who) return new Vector2(xs[i], zs[i]);
            return new Vector2(float.NaN, float.NaN);
        }

        /// <summary>Freeze the view clock, fly in on the hunter, shoot, fly back out to the default zoom.</summary>
        IEnumerator Close(PeopleView pv, PersonRef who, string name)
        {
            float was = pv.timeScale; pv.timeScale = 0f;
            var h = HunterAt(pv, who);
            var box = Object.FindAnyObjectByType<BoxView>();
            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.FlyTo(box.transform.TransformPoint(box.land.OnGround(h.x, h.y)), Mathf.Max(rig.minZoom * 2f, 0.25f), 0.3f);
            for (int i = 0; i < 200 && rig.IsFlying; i++) yield return null;
            yield return Frames(8);
            yield return Shot(name);
            rig.zoom = 1f;
            yield return Frames(5);
            pv.timeScale = was;
        }

        /// <summary>Runs one hunt start to end and asserts what a watcher sees.</summary>
        IEnumerator OneHunt(PeopleView pv, RabbitView rv, bool catches, string tag)
        {
            EcoDebug.ForceHunt(catches);
            int simBefore = Sim.EcoRabbitCount; int hidesBefore = Sim.HidesDrying;
            Assert.IsTrue(pv.Planner.DebugStartHunt(pv.Now, out var plan), "a hunt starts now (rabbits shown, a hunter free)");
            Assert.AreEqual(catches, plan.catches, "the cheat fixes how it ends");
            int id = plan.rabbitId;
            Assert.IsTrue(rv.Field.Alive.Contains(id), "the target is a real rabbit of the view's herd");
            var who = plan.hunter;
            pv.timeScale = 4f;
            var seen = new HashSet<HuntPhase>();
            bool spearWalkOut = false, stalkCrouch = false, rabbitShoulder = false, fledAfterStrike = false, calmBeforeStrike = true;
            float closest = 99f; bool shotStalk = false, shotStrike = false, shotCarry = false;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 300f)
            {
                yield return null;
                var a = ActOf(pv, who);
                if (a.hunt == HuntPhase.None && seen.Contains(HuntPhase.Return)) break;
                if (a.hunt != HuntPhase.None) seen.Add(a.hunt);
                if (a.hunt == HuntPhase.WalkOut && a.prop == Prop.Spear) spearWalkOut = true;
                if (a.hunt == HuntPhase.Stalk)
                {
                    stalkCrouch |= a.kind == ActivityKind.Stalk;
                    pv.timeScale = 1f;
                    var h = HunterAt(pv, who);
                    if (rv.TryGetPosition(id, out var rp)) closest = Mathf.Min(closest, Vector2.Distance(h, new Vector2(rp.x, rp.z)));
                    if (rv.TryGetAct(id, out bool fl) && fl) calmBeforeStrike = false;
                    if (!shotStalk && a.progress > 0.5f) { shotStalk = true; yield return Shot($"hunt_{tag}_stalk"); yield return Close(pv, who, $"hunt_{tag}_stalk_close"); }
                }
                if (a.hunt == HuntPhase.Strike)
                {
                    pv.timeScale = 0.5f;
                    if (!shotStrike && a.progress > 0.55f) { shotStrike = true; yield return Shot($"hunt_{tag}_strike"); yield return Close(pv, who, $"hunt_{tag}_strike_close"); }
                }
                if (a.hunt == HuntPhase.Bolt) { pv.timeScale = 1f; if (rv.TryGetAct(id, out bool fl2) && fl2) fledAfterStrike = true; }
                if (a.hunt == HuntPhase.CarryBack)
                {
                    pv.timeScale = 1f;
                    rabbitShoulder |= a.prop == Prop.Rabbit;
                    if (!shotCarry && a.progress > 0.4f) { shotCarry = true; yield return Shot($"hunt_{tag}_carry"); yield return Close(pv, who, $"hunt_{tag}_carry_close"); }
                }
                if (a.hunt is HuntPhase.Skin or HuntPhase.Cook or HuntPhase.Return or HuntPhase.WalkBack) pv.timeScale = 6f;
            }
            pv.timeScale = 1f;
            Debug.Log($"[hunt] {tag}: closest {closest:0.00}, phases {string.Join(",", seen)}");
            Assert.IsTrue(seen.Contains(HuntPhase.WalkOut) && seen.Contains(HuntPhase.Stalk) && seen.Contains(HuntPhase.Strike), "walk out, stalk, strike: " + string.Join(",", seen));
            Assert.IsTrue(spearWalkOut, "the hunter walks out with a spear in hand");
            Assert.IsTrue(stalkCrouch, "and stalks crouched");
            Assert.Less(closest, 0.75f, "the stalk gets close to a real rabbit");
            Assert.IsTrue(calmBeforeStrike, "the rabbit stays put while it is stalked");
            if (catches)
            {
                Assert.IsTrue(plan.caught, "the sim settled a catch");
                Assert.IsFalse(rv.Field.Alive.Contains(id), "the rabbit is gone from the herd");
                Assert.IsFalse(rv.TryGetPosition(id, out _), "and from the view");
                Assert.AreEqual(simBefore - 1, Sim.EcoRabbitCount, "and from the count");
                Assert.AreEqual(hidesBefore + 1, Sim.HidesDrying, "a hide dries by the fire");
                Assert.IsTrue(seen.Contains(HuntPhase.CarryBack) && rabbitShoulder, "the rabbit rides over the shoulder to the fire");
                Assert.IsTrue(seen.Contains(HuntPhase.Skin) && seen.Contains(HuntPhase.Cook), "skinned, then cooked");
            }
            else
            {
                Assert.IsFalse(plan.caught);
                Assert.IsTrue(rv.Field.Alive.Contains(id), "a miss: the rabbit is still there");
                Assert.IsTrue(fledAfterStrike, "and it bolts");
                Assert.AreEqual(simBefore, Sim.EcoRabbitCount, "nothing taken");
                Assert.AreEqual(hidesBefore, Sim.HidesDrying);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Hunter_StalksARealRabbit_CatchAndMiss()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242);
                Assert.IsTrue(Sim.EcologyOn);
                var pv = PeopleView.Find();
                var rv = RabbitView.Instance;
                PeopleView.DebugSetHour(9f);
                Meadow();
                EcoDebug.CampGate();
                for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
                Debug.Log($"[hunt] era {Sim.State.era}, rabbits {Sim.EcoRabbitCount}");
                EcoDebug.AddRabbits(6);
                PeopleView.DebugSetHour(9f);
                yield return Frames(30);
                yield return Until(() => !rv.SignPlaying && rv.Shown >= 3, 15f);
                Assert.GreaterOrEqual(rv.Shown, 3, "rabbits in the meadow");
                Camera.main.GetComponent<BoxCamera>().zoom = 1f;
                yield return OneHunt(pv, rv, true, "catch");
                Assert.AreEqual(Sim.EcoRabbitsShown(), rv.Shown, "the view still follows the sim count");
                PeopleView.DebugSetHour(Mathf.Min(pv.ViewHour + 0.5f, 15f));
                yield return Frames(30);
                yield return OneHunt(pv, rv, false, "miss");
                AssertNoErrors("hunt");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }
    }
}
