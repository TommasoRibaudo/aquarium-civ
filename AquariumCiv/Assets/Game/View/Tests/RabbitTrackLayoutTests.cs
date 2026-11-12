using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#608 rabbit tracks and #609 seed pad: the pure math.</summary>
    public class RabbitTrackLayoutTests
    {
        static List<RabbitTrackLayout.Print> Trail(uint cell, float keep = 1f)
        {
            var l = new List<RabbitTrackLayout.Print>();
            RabbitTrackLayout.Build(0.5f, -0.3f, 4242u, cell, keep, l);
            return l;
        }

        [Test]
        public void Trail_IsShort_AndMadeOfHopsOfTwoHindAndTwoFore()
        {
            for (uint c = 1; c < 30; c++)
            {
                var l = Trail(c);
                Assert.That(l.Count % 4, Is.EqualTo(0));
                Assert.That(l.Count / 4, Is.GreaterThanOrEqualTo(3).And.LessThanOrEqualTo(6), "cell " + c);
                int hind = 0; foreach (var p in l) if (p.hind) hind++;
                Assert.That(hind, Is.EqualTo(l.Count / 2));
            }
        }

        [Test]
        public void Prints_NeverOverlap_AndStayNearTheCell()
        {
            for (uint c = 1; c < 40; c++)
            {
                var l = Trail(c);
                for (int i = 0; i < l.Count; i++)
                {
                    Assert.That((l[i].at - new Vector2(0.5f, -0.3f)).magnitude, Is.LessThan(0.35f), "cell " + c);
                    for (int j = i + 1; j < l.Count; j++)
                        Assert.That((l[i].at - l[j].at).magnitude, Is.GreaterThan(Mathf.Min(l[i].rx, l[j].rx) * 1.5f), $"cell {c} prints {i}/{j}");
                }
            }
        }

        [Test]
        public void Hops_AreSpaced_AndHindPrintsAreLonger()
        {
            var l = Trail(7);
            for (int h = 1; h < l.Count / 4; h++)
            {
                float d = (l[h * 4 + 3].at - l[(h - 1) * 4 + 3].at).magnitude;
                Assert.That(d, Is.GreaterThan(0.05f).And.LessThan(0.2f));
            }
            Assert.That(RabbitTrackLayout.HindRz, Is.GreaterThan(RabbitTrackLayout.ForeRz));
        }

        [Test]
        public void CellsDiffer_AndTheSameCellRepeats()
        {
            var a = Trail(3); var b = Trail(4); var a2 = Trail(3);
            Assert.That(a.Count, Is.EqualTo(a2.Count));
            for (int i = 0; i < a.Count; i++) Assert.That(a[i].at, Is.EqualTo(a2[i].at));
            bool differ = a.Count != b.Count || Mathf.Abs(a[0].yaw - b[0].yaw) > 0.01f;
            Assert.That(differ, Is.True);
        }

        [Test]
        public void OldTrails_AreThinner()
        {
            for (uint c = 1; c < 20; c++)
                Assert.That(Trail(c, 0f).Count, Is.LessThanOrEqualTo(Trail(c, 1f).Count));
        }

        [Test]
        public void TrailsShown_ScaleWithRabbits_AndAgeRises()
        {
            Assert.That(RabbitView.TrailsShown(2, 3), Is.EqualTo(2));
            Assert.That(RabbitView.TrailsShown(8, 3), Is.EqualTo(3));
            Assert.That(RabbitView.TrailsShown(8, 1), Is.EqualTo(1));
            Assert.That(RabbitView.TrackAge(0), Is.EqualTo(0f));
            Assert.That(RabbitView.TrackAge(20), Is.EqualTo(1f));
        }

        [Test]
        public void PadNormal_MatchesAPlane_AndTiltCarriesUpToIt()
        {
            // plane rising 0.5 per unit towards +x: h = 0.5 x, span 0.2
            var n = KeeperTreeKit.PadNormal(0.1f, -0.1f, 0f, 0f, 0.2f);
            Assert.That(Vector3.Angle(n, new Vector3(-0.5f, 1f, 0f)), Is.LessThan(0.01f));
            KeeperTreeKit.PadFit(0f, 0.1f, -0.1f, 0f, 0f, 0.2f, out var tilt, out var lift);
            Assert.That(Vector3.Angle(tilt * Vector3.up, n), Is.LessThan(0.01f));
            Assert.That(lift, Is.EqualTo(0f).Within(1e-6f), "a plane needs no lift");
            // rim of the pad lies on the plane: no floating or buried edge
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var rim = tilt * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.2f;
                Assert.That(rim.y, Is.EqualTo(0.5f * rim.x).Within(0.03f));
            }
        }

        [Test]
        public void PadFit_FlatGroundIsUnchanged_SteepIsCapped_HollowLiftsTheRim()
        {
            KeeperTreeKit.PadFit(1f, 1f, 1f, 1f, 1f, 0.2f, out var t0, out var l0);
            Assert.That(Quaternion.Angle(t0, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(l0, Is.EqualTo(0f));
            KeeperTreeKit.PadFit(0f, 2f, -2f, 0f, 0f, 0.2f, out var t1, out _);
            Assert.That(Quaternion.Angle(t1, Quaternion.identity), Is.LessThanOrEqualTo(KeeperTreeKit.MaxTiltDeg + 0.01f));
            KeeperTreeKit.PadFit(0f, 0.01f, 0.01f, 0.01f, 0.01f, 0.2f, out _, out var l2);
            Assert.That(l2, Is.EqualTo(0.01f).Within(1e-6f), "the rim is above the middle in a dip: lift to meet it");
        }

        [Test]
        public void Build_OnALand_StillBuilds_AndKeepsThePlantUpright()
        {
            var land = Land.StyleKit();
            var mb = new MeshBuilder(5);
            KeeperTreeKit.Build(mb, land.OnGround(1f, 1f), 0, 0, LandColors.River, land);
            Assert.That(mb.VertexCount, Is.GreaterThan(100));
        }
    }
}
