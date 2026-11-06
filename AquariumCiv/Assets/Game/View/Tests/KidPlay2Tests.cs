// #359 Kids' play 2: pure tests of the splash, the woods games, hide and seek and the snowball fight (NUnit 3 classic asserts; UnityEngine
// maths only), so Tools/AnimBench compiles and runs them too (Tools/AnimBench.Tests), and Unity runs them in the View EditMode tests.
using System.Collections.Generic;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class KidPlay2Tests
    {
        static readonly int[] Two = { 11, 22 }, Three = { 11, 22, 33 }, Four = { 11, 22, 33, 44 };

        static PlaySite Pond() => new PlaySite { water = true, edge = new Vector2(0f, 0.4f), toLand = new Vector2(0f, -1f) };

        [Test]
        public void Splash_EachSplashHitsTheWater_TheDropletsLandOnThePartner_WhoIsSoaked()
        {
            foreach (var members in new[] { Two, Three, Four })
            {
                var g = new SplashGame(359, 1f, members, Vector2.zero, Pond());
                var fx = new List<PlayFx>();
                g.Effects(0f, 60f, fx);
                int aimed = 0;
                foreach (var f in fx)
                {
                    if (!(f.kind == PlayFxKind.Droplets && f.aimed)) continue;
                    aimed++;
                    var by = g.Cue(f.by, f.at - 0.01f);
                    Assert.AreEqual(KidGame.Water, by.hitOn, "the splasher's contact is on the water");
                    Assert.AreEqual(DollStyle.PlaySplash, by.style);
                    var on = g.Cue(f.on, f.at + PlayFx.DropletFlight + 0.05f);
                    Assert.AreEqual(new Vector2(f.aim.x, f.aim.z).x, on.pos.x, 0.05f, "the droplets are aimed at the partner (x)");
                    Assert.AreEqual(new Vector2(f.aim.x, f.aim.z).y, on.pos.y, 0.05f, "the droplets are aimed at the partner (z)");
                    Assert.AreEqual(DollStyle.Flinch, on.style, "the partner is soaked when they land");
                    Assert.IsTrue(on.endBeat);
                    Assert.Greater(by.sink, 0f, "at the pond they stand in the water");
                }
                Assert.GreaterOrEqual(aimed, 10, $"{members.Length} kids: splashes in a minute");
            }
        }

        [Test]
        public void Splash_EverySetHasAJumpIn_ThenTheShakeOff()
        {
            var g = new SplashGame(7, 0f, Two, Vector2.zero, Pond());
            int jumps = 0, shakes = 0;
            for (int j = 0; j < 20; j++)
            {
                var e = g.ExOf(0, j);
                if (e.kind == SplashGame.ExKind.Jump)
                {
                    jumps++;
                    var c = g.Cue(e.by, e.contact + 0.3f);
                    Assert.AreEqual(SplashGame.Deep, c.sink, 1e-4f, "lands sunk deeper");
                    Assert.AreEqual(SplashGame.ExKind.Shake, g.ExOf(0, j + 1).kind, "the jump is followed by the shake-off");
                }
                if (e.kind == SplashGame.ExKind.Shake)
                {
                    shakes++;
                    var c = g.Cue(0, e.start + SplashGame.WadeBack + 0.2f);
                    Assert.AreEqual(DollStyle.PlayShake, c.style);
                    Assert.IsTrue(c.endBeat, "the shake-off is the end beat");
                    Assert.AreEqual(0f, c.sink, "on dry ground to shake off");
                }
                Assert.Greater(e.end, e.start);
                Assert.AreEqual(e.end, g.ExOf(0, j + 1).start, 1e-4f, "one after another");
            }
            Assert.GreaterOrEqual(jumps, 3);
            Assert.AreEqual(jumps, shakes);
        }

        [Test]
        public void Splash_AtAPuddle_TheyStandOnIt_NotSunk_MuddyDrops()
        {
            var site = new PlaySite { water = true, puddle = true, mud = true, edge = Vector2.zero, puddleR = 0.07f, toLand = Vector2.right };
            var g = new SplashGame(3, 0f, Two, Vector2.zero, site);
            for (float t = 0f; t < 30f; t += 0.1f) for (int i = 0; i < 2; i++) Assert.AreEqual(0f, g.Cue(i, t).sink, "a puddle has no depth");
            var fx = new List<PlayFx>();
            g.Effects(0f, 30f, fx);
            foreach (var f in fx) if (f.kind is PlayFxKind.Droplets or PlayFxKind.Ring) Assert.IsTrue(f.mud, "puddle water is muddy");
        }

        [Test]
        public void Woods_TheTagTouchIsWithinReach_TheSlapIsAtTheTrunk_TheTreeShakesOnce()
        {
            var trunk = new Vector2(0.5f, -0.3f);
            var g = new WoodsGame(77, 0f, Two, trunk, new PlaySite { tree = true, trunk = trunk });
            var fx = new List<PlayFx>();
            g.Effects(0f, g.CycleSeconds, fx);
            int shakes = 0;
            foreach (var f in fx) if (f.kind == PlayFxKind.TreeShake) { shakes++; Assert.AreEqual(trunk.x, f.pos.x, 1e-4f); }
            Assert.AreEqual(WoodsGame.Rounds, shakes, "one slap on the trunk a round");
            int tags = 0, slaps = 0, strikes = 0;
            DollStyle lastB = DollStyle.Stand;
            for (float t = 0f; t < g.CycleSeconds; t += 1f / 60f)
                for (int i = 0; i < 2; i++)
                {
                    var c = g.Cue(i, t);
                    var p = g.PoseOf(i, t, t, 0f, c.speed, out _);
                    if (p.hit < 0.5f) continue;
                    if (c.hitOn == KidGame.TreeHit) { slaps++; Assert.LessOrEqual((c.pos - trunk).magnitude, WoodsGame.R + 0.01f, "at the trunk"); }
                    else if (c.hitOn == 1 - i)
                    {
                        var o = g.Cue(1 - i, t);
                        if (c.stick) { strikes++; Assert.AreEqual(DollStyle.Flinch, o.style, "the block jolts on the cross"); }
                        else { tags++; Assert.LessOrEqual((c.pos - o.pos).magnitude, WoodsGame.TagReach + 0.005f, "the tag touch (P1)"); Assert.AreEqual(DollStyle.Flinch, o.style); }
                    }
                    lastB = c.style;
                }
            Assert.Greater(tags, 0); Assert.Greater(slaps, 0); Assert.Greater(strikes, 0);
            _ = lastB;
        }

        [Test]
        public void HideSeek_HidersCrouchBehindTheirOwnCover_FarSide_EachFoundOnce_TheFirstFoundCountsNext()
        {
            var site = new PlaySite { hasBase = true, home = new PlayCover(Vector2.zero, 0.12f, CoverKind.Tent) };
            site.covers.Add(new PlayCover(new Vector2(-0.65f, 0.35f), 0.09f, CoverKind.Bush));
            site.covers.Add(new PlayCover(new Vector2(0.6f, 0.45f), 0.08f, CoverKind.Bush));
            site.covers.Add(new PlayCover(new Vector2(0.75f, -0.25f), 0.04f, CoverKind.Tree));
            site.covers.Add(new PlayCover(new Vector2(-0.55f, -0.6f), 0.12f, CoverKind.Tent));
            Assert.IsTrue(HideSeekGame.Enough(site, 4));
            var g = new HideSeekGame(31, 0f, Four, new Vector2(0f, -0.3f), site);
            for (int k = 0; k < 3; k++)
            {
                var s = g.SeekOf(k);
                var used = new HashSet<Vector2>();
                Assert.AreEqual(3, s.order.Length, "three hiders, each found");
                foreach (int h in s.order)
                {
                    Assert.AreNotEqual(s.seeker, h);
                    // Its cover: the nearest; within 0.1 u of its surface, on the far side from the base (P1).
                    PlayCover c = default; float bd = float.MaxValue;
                    foreach (var cc in site.covers) { float d = (cc.pos - s.hide[h]).magnitude - cc.r; if (d < bd) { bd = d; c = cc; } }
                    Assert.LessOrEqual(bd, 0.1f, "crouched right behind it");
                    Assert.Greater(Vector2.Dot(s.hide[h] - c.pos, c.pos - site.home.pos), 0f, "on the far side from the base");
                    Assert.IsTrue(used.Add(c.pos), "one hider a cover");
                    var cue = g.Cue(h, s.foundAt[h] - 0.3f);
                    Assert.AreEqual(DollStyle.PlayHide, cue.style, "hidden until found");
                    var seek = g.Cue(s.seeker, s.foundAt[h] + 0.01f);
                    Assert.AreEqual(h, seek.hitOn, "the point lands on that hider");
                    Assert.LessOrEqual((seek.pos - s.hide[h]).magnitude, 0.15f, "beside it");
                    Assert.AreEqual(DollStyle.PlayFound, g.Cue(h, s.foundAt[h] + 0.05f).style, "it pops up on the contact");
                }
                Assert.LessOrEqual(s.end - (k == 0 ? 0f : g.SeekOf(k - 1).end), 22f, "a round in about 15-20 s");
                Assert.AreEqual(s.order[0], g.SeekOf(k + 1).seeker, "the first found counts next");
                // The end beat: everyone at the base.
                bool joy = false;
                for (float t = s.cheer; t < s.end; t += 0.05f) for (int i = 0; i < 4; i++) { var c = g.Cue(i, t); if (c.endBeat && c.role == GroupRole.Cheerer) joy = true; Assert.LessOrEqual((c.pos - site.home.pos).magnitude, site.home.r + 0.2f, "at the base"); }
                Assert.IsTrue(joy);
            }
        }

        [Test]
        public void Snowball_EachThrowLeavesTheHand_AndBurstsOnItsTarget_WithAPuff()
        {
            var g = new SnowballGame(12, 0f, Three, Vector2.zero);
            var fx = new List<PlayFx>();
            g.Effects(0f, 40f, fx);
            for (int k = 0; k < 8; k++)
            {
                var s = g.ShotAt(k);
                Assert.AreNotEqual(s.from, s.to);
                var to = g.SpotOf(s.to);
                Assert.AreEqual(to.x, s.arc.end.x, 1e-3f); Assert.AreEqual(to.y, s.arc.end.z, 1e-3f);
                Assert.AreEqual(DollStyle.Flinch, g.Cue(s.to, s.land + 0.05f).style, "it flinches as it bursts");
                bool puff = false;
                foreach (var f in fx) if (f.kind == PlayFxKind.Snow && Mathf.Abs(f.at - s.land) < 1e-3f && f.on == s.to) puff = true;
                if (s.land < 40f) Assert.IsTrue(puff, $"throw {k}: a snow puff on the target");
                Assert.AreEqual(s.to, g.ShotAt(k + 1).from, "it throws back");
            }
        }
    }
}
