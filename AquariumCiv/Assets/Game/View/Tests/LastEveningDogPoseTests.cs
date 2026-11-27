using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>
    /// #800 (the #789 v2 shoot): the dog at the elder's feet stood up during its tail wag, about 3 s after the hand on the shoulder. The scene's dog
    /// parts are posed frame by frame here through the box's own quad pose (<see cref="CampLife.PoseQuad"/>, with the act clock CampLife keeps): from
    /// the moment it lies at the feet until the elder rises it stays down, and its tail moves in the thumps. Not in AnimBench (CampLife is the box's).
    /// </summary>
    public class LastEveningDogPoseTests
    {
        static readonly Vector2 Fire = new Vector2(0.1f, 0.8f), ToCam = new Vector2(-0.47f, -0.88f), Door = new Vector2(1.1f, 0.3f);

        [Test]
        public void TheDog_StaysLyingThroughTheTailThumps_AndTheTailMoves()
        {
            var c = new LastEveningCast { elder = 1, elderAt = Fire + new Vector2(0.6f, -0.2f), fire = Fire, toCam = ToCam, door = Door, dog = 5, dogAt = Fire + new Vector2(0.3f, 0.3f) };
            for (int i = 0; i < 3; i++) { c.kin.Add(10 + i); c.kinAt.Add(Fire + new Vector2(-1f + 0.25f * i, -0.6f)); c.kinKid.Add(i == 2); }
            c.partner = 0;
            var s = LastEveningScenes.Evening(c, 0f);
            float tRise = s.b + LastEveningScenes.LookFor + LastEveningScenes.NodFor + LastEveningScenes.StillFor;
            const float dt = 1f / 30f;
            var was = s.QuadAt(0, 0f).act; float since = 0f;
            float lowest = 1f, tailLo = float.MaxValue, tailHi = float.MinValue; int thumpFrames = 0;
            for (float t = 0f; t < tRise; t += dt)
            {
                var act = s.QuadAt(0, t).act;
                // CampLife.StepDog's act clock: it starts again on a new act, except from one lying act into the other
                since = act != was && !(CampLife.Lying(act) && CampLife.Lying(was)) ? 0f : since + dt;
                was = act;
                var p = QuadPose.Stand(Vector3.zero, 0f);
                CampLife.PoseQuad(ref p, false, 0f, act, since, t, true);
                if (t >= s.a && t < tRise) lowest = Mathf.Min(lowest, p.lie);
                if (act == QuadAct.LieThump) { thumpFrames++; tailLo = Mathf.Min(tailLo, p.tail); tailHi = Mathf.Max(tailHi, p.tail); }
            }
            Assert.Greater(lowest, 0.99f, "lying at the elder's feet from the hand to the rising (it never gets up for the thumps)");
            Assert.Greater(thumpFrames, 20, "the thumps play (about 1.3 s)");
            Assert.Greater(tailHi - tailLo, 20f, "the tail lifts and drops in the thumps");
        }
    }
}
