using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #357: principle 2 (the target answers within 0.2 s) in the real box: when a woodcutter or hammerer stroke lands (<see cref="PeopleView.WorkHit"/>),
    /// <see cref="HitFx"/> has a live puff within 0.2 s, in one draw call; and the cheats work. Needs the default camera (the cost guard only makes puffs in view).
    /// </summary>
    [Category("HitFx")]
    public class HitFxPlayTests : PlayTestBase
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator ChopAndHammerStrokes_MakeAPuffWithin200ms()
        {
            yield return OpenSlice(seed: 7);
            SimLayoutSource.DebugSkipYears(30);
            yield return Frames(30);
            var pv = PeopleView.Find();
            var fx = HitFx.Instance;
            Assert.IsNotNull(pv, "people");
            Assert.IsNotNull(fx, "HitFx is up with the people (its bootstrap)");
            HitFx.Enabled = true;
            PeopleView.DebugSetHour(8.5f);
            pv.timeScale = 3f;

            int hits = 0, spawnedBefore = 0;
            DollStyle style = default;
            float hitAt = -1f;
            pv.WorkHit += (who, place, st, at) =>
            {
                if (hitAt >= 0f || st is not (DollStyle.Chop or DollStyle.Hammer) || !HitFx.InView(at)) return;
                hits++; style = st; hitAt = Time.unscaledTime; spawnedBefore = fx.PuffsSpawned;
            };
            yield return Until(() => hitAt >= 0f, 120f);
            Assert.GreaterOrEqual(hits, 1, "a Chop or Hammer stroke landed in view within 2 minutes (seed 7, 30 years): pick another seed if the box changed");
            // HitFx handles the same event; its puff is made in that frame (the subscription is older than ours, so it ran first).
            yield return null;
            Assert.Less(Time.unscaledTime - hitAt, 0.2f, "looked within 0.2 s");
            Assert.GreaterOrEqual(fx.PuffsSpawned, spawnedBefore, "HitFx counted the puff");
            Assert.GreaterOrEqual(fx.LivePuffs, 1, $"a {style} stroke has a live puff within 0.2 s");
            Assert.LessOrEqual(fx.LivePuffs, PuffPool.MaxPuffs);
            Assert.IsNotNull(fx.Renderer, "the one mesh is up");
            Assert.Greater(fx.VertsDrawn, 0);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Cheats_PuffRow_ShakeAll_AndOff()
        {
            yield return OpenSlice(seed: 7);
            yield return Frames(20);
            var fx = HitFx.Ensure();
            HitFx.Enabled = true;
            StringAssert.Contains("puffs in a row", HitFxCheats.PuffRow());
            yield return Frames(3);
            Assert.GreaterOrEqual(fx.LivePuffs, 5, "a row of every kind");
            Assert.Greater(fx.VertsDrawn, 0);
            HitFxCheats.ShakeAll();
            yield return Frames(3);
            Assert.AreEqual("puffs off", HitFxCheats.Toggle());
            Assert.AreEqual(0, fx.LivePuffs, "off clears what is up");
            Assert.IsFalse(HitFx.Puff(PuffKind.Dust, Vector3.zero, Vector3.forward, 1f, true), "and makes none");
            HitFxCheats.Toggle();
            Assert.IsTrue(HitFx.Enabled);
        }
    }
}
