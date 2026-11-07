using System.Collections.Generic;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The user's 2026-11-06 calls in the pure beats: a kid tumbles now and then in the rabbit chase (RabbitChaseGame.Tumble), a
    /// toddler follows its carer (ToddleFollow). Also compiled by Tools/AnimBench.Tests.</summary>
    public class TumbleToddleTests
    {
        static RabbitChaseGame Chase(int seed, bool force)
        {
            var g = new RabbitChaseGame(seed, 0f, new[] { 11, 12, 13 }, Vector2.zero) { TumbleFirst = force };
            g.HerdAt = _ => new Vector2(0.9f, 0.2f);
            return g;
        }

        [Test]
        public void AboutOneChaseInFour_HasATumble_NeverTheKidWhoScaresTheHerd()
        {
            int tumbles = 0, n = 0;
            for (int seed = 1; seed < 200; seed++)
            {
                var g = Chase(seed, false);
                for (int c = 0; c < 3; c++)
                {
                    var ch = g.ChaseOf(c);
                    n++;
                    if (ch.tumbler < 0) continue;
                    tumbles++;
                    Assert.AreNotEqual(ch.contactBy, ch.tumbler, "the kid whose charge scares the herd never trips");
                    Assert.Greater(ch.tripAt, ch.chargeAt[ch.tumbler], "it trips in the charge");
                    Assert.Greater(ch.laughAt[ch.tumbler], ch.upAt + RabbitChaseGame.BrushFor, "it laughs once it has brushed off");
                }
            }
            Assert.That(tumbles / (float)n, Is.InRange(0.17f, 0.33f), $"{tumbles} of {n}");
            Assert.AreEqual(Chase(2, false).ChaseOf(0).tumbler, Chase(2, false).ChaseOf(0).tumbler, "the kit's hash: the same every time");
        }

        [Test]
        public void TheTumble_DivesFlat_PuffsOnce_GetsUp_AndTheOthersLaughAtIt()
        {
            var g = Chase(9, true);
            var ch = g.ChaseOf(0);
            Assert.GreaterOrEqual(ch.tumbler, 0, "the cheat: the first chase has a tumble");
            int k = ch.tumbler;
            var lying = g.PoseOf(k, ch.landAt + 0.4f, 0f, 0f, 0f, out var cue);
            Assert.Greater(lying.lean, 70f, "flat on its front");
            Assert.AreEqual(0f, lying.hit, "no hit on anyone: the grass puff is the game's effect");
            Assert.AreEqual(DollStyle.Stand, cue.under);
            var up = g.PoseOf(k, ch.upAt + RabbitChaseGame.BrushFor - 0.05f, 0f, 0f, 0f, out _);
            Assert.Less(up.lean, 20f, "up again");
            // Where it fell: it slides on a little and stays.
            var a = g.Cue(k, ch.landAt).pos; var b = g.Cue(k, ch.upAt).pos;
            Assert.Less((a - b).magnitude, RabbitChaseGame.Slide, "it stays where it fell");
            var fx = new List<PlayFx>();
            g.Effects(0f, g.Period, fx);
            Assert.AreEqual(1, fx.Count, "one grass puff");
            Assert.AreEqual(PlayFxKind.Grass, fx[0].kind);
            Assert.AreEqual(ch.landAt, fx[0].at, 1e-4f, "on the landing");
            fx.Clear();
            g.Effects(0f, ch.landAt - 0.01f, fx); g.Effects(ch.landAt - 0.01f, ch.landAt + 0.01f, fx); g.Effects(ch.landAt + 0.01f, g.Period, fx);
            Assert.AreEqual(1, fx.Count, "once, however the frames fall");
            for (int i = 0; i < g.Count; i++)
            {
                if (i == k) continue;
                var c = g.Cue(i, ch.laughAt[i] + 0.3f);
                Assert.AreEqual(DollStyle.Laugh, c.style);
                var faceDir = (c.face - c.pos).normalized; var toTumbler = (g.Cue(k, ch.laughAt[i] + 0.3f).pos - c.pos).normalized;
                Assert.Greater(Vector2.Dot(faceDir, toTumbler), 0.99f, "they laugh at the one who fell (Laugh points)");
            }
        }

        [Test]
        public void ToddleFollow_BehindTheWalkingCarer_RoundTheStandingOne_PlopsNowAndThen()
        {
            int key = 402;
            var carer = new Vector2(0.3f, 0.1f);
            int plops = 0, frames = 0;
            for (float t = 0f; t < 60f; t += 0.1f)
            {
                frames++;
                if (ToddleFollow.PlopSince(key, t) >= 0f) { plops++; continue; }
                var walking = ToddleFollow.Target(key, t, carer, Vector2.up, true, Vector2.right, Vector2.zero);
                Assert.Less(Vector2.Dot(walking - carer, Vector2.right), -ToddleFollow.Behind * 0.9f, "behind the walking carer");
                var standing = ToddleFollow.Target(key, t, carer, Vector2.up, false, Vector2.zero, Vector2.zero);
                Assert.AreEqual(ToddleFollow.Potter, (standing - carer).magnitude, 1e-4f, "pottering near the standing carer");
                Assert.Greater(Vector2.Dot(standing - carer, Vector2.up), -0.05f, "on its front half, where the carer sees it");
            }
            float share = plops / (float)frames;
            Assert.That(share, Is.InRange(ToddleFollow.PlopFor / (ToddleFollow.WalkHi + ToddleFollow.PlopFor) - 0.05f, ToddleFollow.PlopFor / (ToddleFollow.WalkLo + ToddleFollow.PlopFor) + 0.05f));
            var sat = new Vector2(0.11f, 0.22f);
            float plopAt = 0f;
            for (float t = 0f; t < 20f; t += 0.05f) if (ToddleFollow.PlopSince(key, t) >= 0f) { plopAt = t + 0.4f; break; }
            Assert.AreEqual(sat, ToddleFollow.Target(key, plopAt, carer, Vector2.up, true, Vector2.right, sat), "plopped: it sits where it is");
            var p = DollPose.Rest;
            ToddleFollow.ToddlerPose(ref p, key, plopAt);
            Assert.Less(p.squash, 0.85f, "sat down hard");
            var c = DollPose.Rest;
            Assert.IsTrue(ToddleFollow.CarerPose(ref c, key, plopAt + 0.5f));
            Assert.Greater(c.lean, 15f, "the carer bends to it");
            Assert.AreNotEqual(ToddleFollow.Cycle(402), ToddleFollow.Cycle(403), "two toddlers have their own clocks");
            Assert.LessOrEqual(ToddleFollow.Speed(5f), ToddleFollow.MaxSpeed);
        }
    }
}
