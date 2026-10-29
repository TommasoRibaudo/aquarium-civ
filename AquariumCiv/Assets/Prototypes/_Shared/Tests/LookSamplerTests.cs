using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Prototypes.Tests
{
    /// <summary>Look-sampler spike: weighted shares instead of one centre ray.</summary>
    public class LookSamplerTests
    {
        static LookSampler.Target T(string cat, float x, float y, float r = 0.05f, float z = 5f) =>
            new() { category = cat, viewport = new Vector3(x, y, z), radius = r };

        static readonly Vector2 Centre = new(0.5f, 0.5f);

        [Test]
        public void SharesSumToOneAndTheCentreWins()
        {
            var s = LookSampler.Weigh(new[] { T("building", 0.5f, 0.5f), T("nudge", 0.85f, 0.5f) }, Centre);
            Assert.AreEqual(1f, s.Values.Sum(), 1e-5f);
            Assert.Greater(s["building"], s["nudge"]);
            Assert.Greater(s["nudge"], 0f, "an off-centre nudge still gets some look-time (the centre ray gave it none)");
        }

        [Test]
        public void TwoBoxesSeenFromAfarAreNotEmpty()
        {
            // P6's shelf view: the screen centre falls between the boxes, so the centre ray logged "empty".
            var s = LookSampler.Weigh(new[] { T("building", 0.3f, 0.5f), T("relic", 0.7f, 0.5f) }, Centre);
            Assert.IsFalse(s.ContainsKey("empty"));
            Assert.AreEqual(s["building"], s["relic"], 1e-5f);
        }

        [Test]
        public void OffScreenAndBehindTheCameraDontCount()
        {
            var s = LookSampler.Weigh(new[] { T("trash", 1.4f, 0.5f), T("vine", 0.5f, 0.5f, z: -2f) }, Centre);
            Assert.AreEqual(new Dictionary<string, float> { ["empty"] = 1f }, s);
        }

        [Test]
        public void BiggerThingsOnScreenGetMore()
        {
            var s = LookSampler.Weigh(new[] { T("building", 0.4f, 0.5f, 0.1f), T("citizen", 0.6f, 0.5f, 0.01f) }, Centre);
            Assert.Greater(s["building"], s["citizen"] * 5);
        }

        [Test]
        public void TheFocusFollowsATouchThenDriftsBack()
        {
            var touch = new Vector2(0.9f, 0.2f);
            Assert.AreEqual(touch, LookSampler.Focus(touch, 0f));
            var mid = LookSampler.Focus(touch, 1.5f);
            Assert.AreEqual(0.7f, mid.x, 1e-4f);
            Assert.AreEqual(Centre, LookSampler.Focus(touch, 3f));
            Assert.AreEqual(Centre, LookSampler.Focus(null, 0f));
            var s = LookSampler.Weigh(new[] { T("building", 0.5f, 0.5f), T("nudge", 0.9f, 0.2f) }, touch);
            Assert.Greater(s["nudge"], s["building"], "right after tapping near a nudge, the player is looking at it");
        }
    }
}
