// #370: a clip's "moment" block builds its dolls from the game's staged-moment plan, and the bench plays the plan's spots and beats.
using System.IO;
using System.Linq;
using AnimBench;
using AquariumCiv.Life;
using AquariumCiv.View;
using NUnit.Framework;
using UnityEngine;

namespace AnimBenchTests
{
    public class MomentClipTests
    {
        static Clip Load(string name) => Clip.Load(Path.Combine(ClipPrincipleTests.ClipsDir(), name + ".json"));

        [TestCase("370-wedding")]
        [TestCase("370-newborn")]
        [TestCase("370-new-chief")]
        [TestCase("370-festival")]
        public void AMomentClip_HasADollPerMember_AndRunsThePlansLength(string name)
        {
            var c = Load(name);
            Assert.IsNotNull(c.Moment);
            Assert.AreEqual(c.Moment.members.Count, c.Dolls.Count);
            Assert.AreEqual(c.Moment.Duration + 0.6f, c.Duration, 1e-4f);
            Assert.IsTrue(c.Dolls.All(d => d.Segments.Any(s => s.Plan == c.Moment)), "every doll plays the plan");
        }

        [Test]
        public void TheBench_StandsTheDancersOnTheKitsRing()
        {
            var c = Load("370-festival");
            var track = new Simulator(c).Run();
            var plan = c.Moment;
            int f = (int)((plan.ringT0 + 3.1f) * c.Fps);
            float t = f / c.Fps;
            for (int k = 0; k < c.Dolls.Count; k++)
            {
                var spec = c.Dolls[k];
                var m = plan.members[k];
                Assert.IsTrue(plan.StepAt(m.key, t, out _, out int i));
                var want = MomentBeats.Spot(plan, m, i, t, out _);
                var got = track.Dolls[k].Frames[f].Pos;
                Assert.Less((want - got).magnitude, 0.03f, $"{spec.Id} on its ring spot");
            }
        }
    }
}
