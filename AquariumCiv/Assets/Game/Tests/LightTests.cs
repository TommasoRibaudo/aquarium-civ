using System;
using AquariumCiv.Lighting;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#113/#175/#146/#162 logic: the day light table and driver, lamps, glass at night, camera reach, near-glass fade.</summary>
    public class LightTests
    {
        // ---- key table ----

        [Test]
        public void Table_HitsTheResearchKeys()
        {
            var dusk = DayLight.Table(19.5f);
            Assert.AreEqual(0.45f, dusk.SunIntensity, 1e-4f);
            Assert.AreEqual(7f, dusk.SunPitch, 1e-3f);
            Assert.AreEqual(0.82f, dusk.SunColor.R, 1e-4f);
            Assert.AreEqual(0.72f, dusk.AmbientEquator.B, 1e-4f);
            var day = DayLight.Table(12f);
            Assert.AreEqual(1.3f, day.SunIntensity, 1e-4f);
            Assert.AreEqual(42f, day.SunPitch, 1e-3f);
            var night = DayLight.Table(22f);
            Assert.AreEqual(0.30f, night.SunIntensity, 1e-4f);
            Assert.AreEqual(30f, night.SunYaw, 1e-3f);
            Assert.AreEqual(0.55f, night.ShadowStrength, 1e-4f);
        }

        [Test]
        public void Table_DayIsFlatBetween9And15()
        {
            Assert.AreEqual(DayLight.Table(9f).SunIntensity, DayLight.Table(15f).SunIntensity, 1e-5f);
            Assert.AreEqual(DayLight.Table(10.3f).SunIntensity, DayLight.Table(13.9f).SunIntensity, 1e-5f);
        }

        [Test]
        public void Table_SunIntensityFallsFrom15UntilTheSwapAndRisesFrom5To9()
        {
            float prev = float.MaxValue;
            for (float h = 15f; h <= 20.7f; h += 0.1f) { float v = DayLight.Table(h).SunIntensity; Assert.LessOrEqual(v, prev + 1e-5f, "at " + h); prev = v; }
            prev = 0f;
            for (float h = 20.9f; h <= 22f; h += 0.1f) { float v = DayLight.Table(h).SunIntensity; Assert.GreaterOrEqual(v, prev - 1e-5f, "the moon comes up, at " + h); prev = v; }
            prev = 0f;
            for (float h = 5f; h <= 9f; h += 0.1f) { float v = DayLight.Table(h).SunIntensity; Assert.GreaterOrEqual(v, prev - 1e-5f, "at " + h); prev = v; }
        }

        [Test]
        public void Table_IsContinuousAcrossTheWholeClockIncludingMidnight()
        {
            var a = DayLight.Table(0f);
            for (float h = 0f; h < 48f; h += 0.01f)
            {
                var b = DayLight.Table(h);
                Assert.Less(Math.Abs(b.SunIntensity - a.SunIntensity), 0.05f, "jump at " + h);
                Assert.Less(Math.Abs(b.AmbientEquator.B - a.AmbientEquator.B), 0.05f, "jump at " + h);
                a = b;
            }
            Assert.AreEqual(DayLight.Table(3f).SunIntensity, DayLight.Table(27f).SunIntensity, 1e-5f, "24 h periodic");
            Assert.AreEqual(DayLight.Table(-2f).SunIntensity, DayLight.Table(22f).SunIntensity, 1e-5f, "negative hours wrap");
        }

        [Test]
        public void SunAndMoon_SwapOnlyWhileNeitherShows()
        {
            // Wherever the light's direction moves fast, it must be too dim and shadowless to read as a sweep.
            for (float h = 19.5f; h < 29f; h += 0.02f)
            {
                var a = DayLight.Table(h);
                var b = DayLight.Table(h + 0.02f);
                if (Math.Abs(b.SunYaw - a.SunYaw) <= 0.1f) continue;
                Assert.LessOrEqual(a.SunIntensity, DayLight.OutSun + 1e-3f, "at " + h);
                Assert.AreEqual(0f, a.ShadowStrength, 1e-3f, "at " + h);
            }
            Assert.AreEqual(-40f, DayLight.Table(20.6f).SunYaw, 1e-3f, "dusk: still the sun");
            Assert.AreEqual(30f, DayLight.Table(21f).SunYaw, 1e-3f, "the moon by 21 h");
            Assert.AreEqual(0f, DayLight.Evaluate(20.8f, LightLook.SoftBlueMoon, 0.9f).ShadowStrength, 1e-3f, "no look puts a shadow on the swap");
        }

        [Test]
        public void Restore_PutsTheLightBackAfterASnap()
        {
            var d = new DayLight();
            d.Snap(12f);
            d.Update(0.05f, 12.5f);
            var was = d.Current; float shown = d.ShownHour;
            d.Snap(17.5f);
            Assert.AreNotEqual(was.SunIntensity, d.Current.SunIntensity);
            d.Restore(was, shown);
            Assert.AreEqual(was.SunIntensity, d.Current.SunIntensity);
            Assert.AreEqual(shown, d.ShownHour);
        }

        [Test]
        public void SunPitch_StaysAboveFiveDegrees()
        {
            for (float h = 0f; h < 24f; h += 0.05f) Assert.GreaterOrEqual(DayLight.Table(h).SunPitch, 5f, "at " + h);
        }

        [Test]
        public void LampLevel_FollowsTheIssueRamp()
        {
            Assert.AreEqual(0f, DayLight.LampLevel(12f));
            Assert.AreEqual(0f, DayLight.LampLevel(17f), 1e-5f);
            Assert.AreEqual(1f, DayLight.LampLevel(20f), 1e-5f);
            Assert.AreEqual(1f, DayLight.LampLevel(23f));
            Assert.AreEqual(1f, DayLight.LampLevel(2f));
            Assert.AreEqual(1f, DayLight.LampLevel(5f), 1e-5f);
            Assert.AreEqual(0f, DayLight.LampLevel(7f), 1e-5f);
            Assert.AreEqual(0.5f, DayLight.LampLevel(6f), 1e-5f);
            Assert.AreEqual(DayLight.LampLevel(19.5f), DayLight.Table(19.5f).Lamp, 1e-6f);
        }

        // ---- looks and the night floor ----

        [Test]
        public void Look_DuskIsTheDefault_TodayDrivesNothing()
        {
            var d = new DayLight();
            Assert.AreEqual(LightLook.SoftBlueDusk, d.Look);
            Assert.IsTrue(d.Drives);
            d.Look = LightLook.Today;
            Assert.IsFalse(d.Drives, "Today leaves the scene's own light alone");
            Assert.IsTrue(LookTuning.For(LightLook.SoftBlueMoon).Drives);
            Assert.IsTrue(LookTuning.For(LightLook.Lamplight).Drives);
        }

        [Test]
        public void Looks_DifferAtNightAndAgreeByDay()
        {
            var dusk = DayLight.Evaluate(22f, LightLook.SoftBlueDusk, 0.9f);
            var moon = DayLight.Evaluate(22f, LightLook.SoftBlueMoon, 0.9f);
            var lamp = DayLight.Evaluate(22f, LightLook.Lamplight, 0.9f);
            Assert.Greater(moon.ShadowStrength, dusk.ShadowStrength, "moon shadow is stronger");
            Assert.Less(lamp.AmbientEquator.Luma, dusk.AmbientEquator.Luma, "lamplight has a darker night");
            var a = DayLight.Evaluate(12f, LightLook.SoftBlueDusk, 0.9f);
            var b = DayLight.Evaluate(12f, LightLook.Lamplight, 0.9f);
            Assert.AreEqual(a.AmbientEquator.Luma, b.AmbientEquator.Luma, 1e-5f);
            Assert.AreEqual(0.85f, a.ShadowStrength, 1e-4f);
        }

        [Test]
        public void NightFloor_ClampsAndKeepsTheBoxReadable()
        {
            var lo = DayLight.Evaluate(23f, LightLook.SoftBlueDusk, 0f);
            var min = DayLight.Evaluate(23f, LightLook.SoftBlueDusk, 0.45f);
            var hi = DayLight.Evaluate(23f, LightLook.SoftBlueDusk, 5f);
            var top = DayLight.Evaluate(23f, LightLook.SoftBlueDusk, 0.9f);
            Assert.AreEqual(min.AmbientEquator.Luma, lo.AmbientEquator.Luma, 1e-6f, "below the range clamps to 0.45");
            Assert.AreEqual(top.AmbientEquator.Luma, hi.AmbientEquator.Luma, 1e-6f, "above clamps to 0.9");
            Assert.AreEqual(0.45f, min.ShaderAmbient, 1e-5f);
            Assert.AreEqual(0.9f, top.ShaderAmbient, 1e-5f);
            Assert.Less(min.AmbientEquator.Luma, top.AmbientEquator.Luma);
            Assert.Greater(min.AmbientEquator.Luma, 0.1f, "even the darkest floor keeps the ambient above a readable minimum");
            // by day the floor does nothing
            Assert.AreEqual(DayLight.Evaluate(12f, LightLook.SoftBlueDusk, 0.45f).AmbientSky.Luma, DayLight.Evaluate(12f, LightLook.SoftBlueDusk, 0.9f).AmbientSky.Luma, 1e-6f);
            Assert.AreEqual(0.75f, DayLight.Evaluate(12f, LightLook.SoftBlueDusk, 0.45f).ShaderAmbient, 1e-5f);
        }

        [Test]
        public void MessAmbientScale_IsCapped()
        {
            Assert.AreEqual(1f, DayLight.MessAmbientScale(1f), 1e-6f);
            Assert.AreEqual(0.85f, DayLight.MessAmbientScale(0.75f), 1e-5f);
            Assert.AreEqual(0.7f, DayLight.MessAmbientScale(0f), 1e-6f);
        }

        // ---- driver ----

        [Test]
        public void Driver_FirstFrameSnaps_ThenConvergesWithoutOvershoot()
        {
            var d = new DayLight();
            d.Update(0.016f, 12f);
            Assert.AreEqual(1.3f, d.Current.SunIntensity, 1e-4f);
            d.Snap(12f);
            float target = DayLight.Evaluate(13f, d.Look, d.NightFloor).SunIntensity;
            float prev = d.Current.SunIntensity;
            for (int i = 0; i < 600; i++)
            {
                d.Update(0.016f, 13f);
                Assert.LessOrEqual(Math.Abs(d.Current.SunIntensity - target), Math.Abs(prev - target) + 1e-6f);
                prev = d.Current.SunIntensity;
            }
            Assert.AreEqual(target, d.Current.SunIntensity, 1e-3f);
            Assert.AreEqual(13f, d.ShownHour, 1e-3f);
            Assert.IsFalse(d.FlickerHeld);
        }

        [Test]
        public void Driver_SmoothingIsOneMinusExpMinus3Dt()
        {
            var d = new DayLight();
            d.Snap(12f);
            float start = d.Current.SunIntensity;
            d.Update(0.1f, 12f);   // same hour: target = current, nothing moves
            Assert.AreEqual(start, d.Current.SunIntensity, 1e-6f);
            d.Look = LightLook.SoftBlueMoon;                         // target changes at the same hour? night only
            d.Snap(22f);
            var from = d.Current;
            d.Look = LightLook.Lamplight;                            // the target now has a lower night ambient
            var target = DayLight.Evaluate(22f, d.Look, d.NightFloor);
            d.Update(0.1f, 22f);
            float k = 1f - (float)Math.Exp(-0.3);
            Assert.AreEqual(from.AmbientEquator.R + (target.AmbientEquator.R - from.AmbientEquator.R) * k, d.Current.AmbientEquator.R, 1e-5f);
        }

        [Test]
        public void Driver_FastClockIsRateClampedSoItNeverStrobes()
        {
            var d = new DayLight();
            d.Snap(0f);
            float hour = 0f, prevShown = 0f, prevI = d.Current.SunIntensity, maxI = 0f;
            bool held = false;
            for (int i = 0; i < 600; i++)   // 10 s at 60 fps with a clock racing 24 hours per second
            {
                hour += 24f * 0.016f;
                d.Update(0.016f, hour);
                float step = Math.Abs(DayLight.HourDelta(prevShown, d.ShownHour));
                Assert.LessOrEqual(step, DayLight.MaxHourRate * 0.016f + 1e-4f, "the shown hour is clamped");
                maxI = Math.Max(maxI, Math.Abs(d.Current.SunIntensity - prevI));
                prevShown = d.ShownHour; prevI = d.Current.SunIntensity;
                held |= d.FlickerHeld;
            }
            Assert.Less(maxI, 0.02f, "no frame changes the sun by more than a sliver");
            Assert.IsTrue(held, "the flicker is held while the clock races");
        }

        [Test]
        public void Driver_ChasesThroughMidnightTheShortWay()
        {
            var d = new DayLight();
            d.Snap(23.5f);
            for (int i = 0; i < 300; i++) d.Update(0.016f, 0.5f);
            Assert.AreEqual(0.5f, d.ShownHour, 1e-3f);
            Assert.AreEqual(1f, DayLight.HourDelta(23.5f, 0.5f), 1e-5f);
        }

        [Test]
        public void Driver_HugeDtIsClampedAndZeroDtIsIgnored()
        {
            var d = new DayLight();
            d.Snap(12f);
            d.Update(0f, 20f);
            Assert.AreEqual(12f, d.ShownHour, 1e-6f);
            d.Update(60f, 20f);   // a frame hitch: no more than 0.1 s of movement
            Assert.AreEqual(12f + DayLight.MaxHourRate * 0.1f, d.ShownHour, 1e-4f);
        }

        // ---- lamps ----

        [Test]
        public void Lamps_ThresholdsAreStaggeredAndDeterministic()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 8; i++)
            {
                float t = Lamps.Threshold(7, i);
                Assert.That(t, Is.InRange(0.05f, 0.95f));
                Assert.AreEqual(t, Lamps.Threshold(7, i), "same seed, same lamp, same threshold");
                seen.Add((int)(t * 1000));
            }
            Assert.AreEqual(8, seen.Count, "eight lamps, eight different thresholds");
            Assert.AreNotEqual(Lamps.Threshold(7, 3), Lamps.Threshold(8, 3), "a different seed shuffles the order");
        }

        [Test]
        public void Lamps_ComeOnOneByOneAsTheLevelRises()
        {
            int prevOn = 0;
            for (float level = 0f; level <= 1f; level += 0.01f)
            {
                int on = 0;
                for (int i = 0; i < 8; i++) if (Lamps.Brightness(level, 3, i, 100f, steady: true) > 0.5f) on++;
                Assert.GreaterOrEqual(on, prevOn, "lamps never switch back off while the level rises");
                Assert.LessOrEqual(on - prevOn, 2, "one or two at a time");
                prevOn = on;
            }
            Assert.AreEqual(8, prevOn, "all on at level 1");
            for (int i = 0; i < 8; i++) Assert.AreEqual(0f, Lamps.Brightness(0f, 3, i, 5f));
        }

        [Test]
        public void Lamps_FadeOnThenBreatheSlowly()
        {
            int seed = 5, id = 2;
            float th = Lamps.Threshold(seed, id);
            // No flicker (#214): a lamp just past its threshold is on, steady but for the slow breathing, never dropping out.
            float prev = Lamps.Brightness(th + 0.06f, seed, id, 0f), jump = 0f, low = 1f;
            for (int i = 1; i < 2000; i++)
            {
                float b = Lamps.Brightness(th + 0.06f, seed, id, i * 0.005f);
                jump = Math.Max(jump, Math.Abs(b - prev)); prev = b; low = Math.Min(low, b);
            }
            Assert.Less(jump, 0.01f, "5 ms apart it barely moves: no strobing, no pulses");
            Assert.Greater(low, 0.5f, "and it never drops out once it has come on");
            float f0 = Lamps.Brightness(th + 0.005f, seed, id, 3f), f1 = Lamps.Brightness(th + 0.05f, seed, id, 3f), f2 = Lamps.Brightness(th + 0.09f, seed, id, 3f);
            Assert.Less(f0, f1, "it fades on as the level rises");
            Assert.Less(f1, f2);
            float lo = 1f, hi = 0f;
            for (int i = 0; i < 400; i++)
            {
                float b = Lamps.Brightness(th + 0.5f, seed, id, i * 0.05f);
                lo = Math.Min(lo, b); hi = Math.Max(hi, b);
            }
            Assert.Greater(lo, 0.88f, "burning steady it only breathes a little");
            Assert.LessOrEqual(hi, 1f);
            Assert.Greater(hi - lo, 0.03f, "but it does breathe");
        }

        [Test]
        public void Lamps_SteadyModeHoldsNoFlicker()
        {
            float th = Lamps.Threshold(1, 1);
            float a = Lamps.Brightness(th + 0.3f, 1, 1, 1.23f, steady: true);
            float b = Lamps.Brightness(th + 0.3f, 1, 1, 9.87f, steady: true);
            Assert.AreEqual(a, b);
            Assert.AreEqual(1f, a, 1e-6f);
        }

        [Test]
        public void PickNearest_TakesAtMostEight_NearestFirst()
        {
            var xs = new float[20]; var zs = new float[20];
            for (int i = 0; i < 20; i++) { xs[i] = i; zs[i] = 0f; }
            var pick = new LampPick();
            Lamps.PickNearest(xs, zs, 20, 0f, 0f, pick);
            Assert.AreEqual(8, pick.Count);
            for (int i = 0; i < 8; i++) Assert.AreEqual(i, pick.Indices[i]);
            Lamps.PickNearest(xs, zs, 20, 19f, 0f, pick);
            Assert.AreEqual(8, pick.Count);
            Assert.AreEqual(19, pick.Indices[0]);
            Assert.AreEqual(12, pick.Indices[7]);
        }

        [Test]
        public void PickNearest_FewLampsAreAllTaken_AndNoneIsFine()
        {
            var xs = new float[] { 5f, 1f, 3f }; var zs = new float[] { 0f, 0f, 0f };
            var pick = new LampPick();
            Lamps.PickNearest(xs, zs, 3, 0f, 0f, pick);
            Assert.AreEqual(3, pick.Count);
            Assert.AreEqual(1, pick.Indices[0]); Assert.AreEqual(2, pick.Indices[1]); Assert.AreEqual(0, pick.Indices[2]);
            Lamps.PickNearest(xs, zs, 0, 0f, 0f, pick);
            Assert.AreEqual(0, pick.Count);
        }

        [Test]
        public void PickNearest_KeepsTheOldSetWhileTheCameraDriftsOnTheEdge()
        {
            var xs = new float[9]; var zs = new float[9];
            for (int i = 0; i < 9; i++) xs[i] = i;
            var pick = new LampPick();
            Lamps.PickNearest(xs, zs, 9, 3.9f, 0f, pick);   // lamps 0..8, focus 3.9: lamp 8 is farthest (4.1), lamp 0 next (3.9)
            Assert.IsFalse(Array.IndexOf(pick.Indices, 8, 0, pick.Count) >= 0);
            // the focus moves a hair so 0 and 8 are nearly tied; the old set (without 8) wins
            Lamps.PickNearest(xs, zs, 9, 4.05f, 0f, pick);
            Assert.IsFalse(Array.IndexOf(pick.Indices, 8, 0, pick.Count) >= 0, "no swap on a near-tie");
            Lamps.PickNearest(xs, zs, 9, 7f, 0f, pick);
            Assert.IsTrue(Array.IndexOf(pick.Indices, 8, 0, pick.Count) >= 0, "a real move does swap");
        }

        [Test]
        public void PickNearest_DoesNotAllocate()
        {
            var xs = new float[30]; var zs = new float[30];
            for (int i = 0; i < 30; i++) { xs[i] = i * 0.3f; zs[i] = (i % 5) * 0.4f; }
            var pick = new LampPick();
            Lamps.PickNearest(xs, zs, 30, 1f, 1f, pick);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) Lamps.PickNearest(xs, zs, 30, i * 0.01f, 1f, pick);
            long mid = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, mid - before, "picking allocates nothing per frame");
            var d = new DayLight(); d.Snap(10f);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) d.Update(0.016f, 10f + i * 0.01f);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before, "driving allocates nothing per frame");
        }

        // ---- glass at night ----

        [Test]
        public void Glass_FrostRisesAtNight_TintDims()
        {
            Assert.AreEqual(1f, GlassNight.FrostScale(12f), 1e-5f);
            Assert.AreEqual(GlassNight.NightFrostBoost, GlassNight.FrostScale(23f), 1e-5f);
            Assert.AreEqual(1f, GlassNight.TintFactor(12f), 1e-5f);
            Assert.AreEqual(0.35f, GlassNight.TintFactor(23f), 1e-5f);
            Assert.Greater(GlassNight.FrostScale(19.5f), GlassNight.FrostScale(18f));
            Assert.Greater(GlassNight.FrostScale(22f), GlassNight.FrostScale(19.5f));
            Assert.AreEqual(1f, GlassNight.FrostOpacity(0.9f, 23f), "capped at 1");
            Assert.AreEqual(0.3f, GlassNight.FrostOpacity(0.2f, 23f), 1e-5f);
        }

        // ---- #146 camera reach ----

        [Test]
        public void Reach_ZoomsCloserThanBefore()
        {
            Assert.Less(CameraReach.MinZoom, 0.3f);
            Assert.AreEqual(1.3f, CameraReach.MaxZoom);
        }

        [Test]
        public void Reach_PanOpensAtFullBoxAndGrowsSoonerThanBefore()
        {
            CameraReach.PanBounds(1f, 3f, 2.2f, out float x1, out float z1);
            Assert.Greater(x1, 0.2f, "a little pan at full-box view (was zero)");
            Assert.Less(x1, 0.6f);
            CameraReach.PanBounds(0.5f, 3f, 2.2f, out float xh, out float zh);
            Assert.GreaterOrEqual(xh, 3f - CameraReach.Margin - 1e-3f, "fully open by zoom 0.5 (was by 0.3)");
            CameraReach.PanBounds(CameraReach.MinZoom, 3f, 2.2f, out float xc, out float zc);
            Assert.Less(xc, 3f - CameraReach.Margin, "tester run 10: close in the focus stays well on the land");
            Assert.Greater(xc, 2f, "and the land's edge is still in reach of the view");
            Assert.Less(zc, 2.2f - CameraReach.Margin);
            Assert.Greater(zc, 1.3f);
        }

        [Test]
        public void Reach_PanIsMonotonicAndContinuousInZoom()
        {
            float prev = -1f, prevK = CameraReach.PanK(0.2f);
            for (float z = 1f; z >= CameraReach.FullPanZoom; z -= 0.005f)
            {
                CameraReach.PanBounds(z, 3f, 2.2f, out float bx, out _);
                Assert.GreaterOrEqual(bx, prev - 1e-5f, "closer opens the pan, at " + z);
                prev = bx;
            }
            for (float z = CameraReach.FullPanZoom; z >= 0.2f; z -= 0.005f)
            {
                CameraReach.PanBounds(z, 3f, 2.2f, out float bx, out _);
                Assert.LessOrEqual(bx, prev + 1e-5f, "past 0.5 the view is so small the focus draws in, at " + z);
                Assert.Less(prev - bx, 0.02f, "with no jump");
                prev = bx;
            }
            float last = CameraReach.PanK(1.3f);
            for (float z = 1.3f; z >= 0.2f; z -= 0.002f)
            {
                float k = CameraReach.PanK(z);
                Assert.Less(Math.Abs(k - last), 0.02f, "no jump at " + z);
                last = k;
            }
            Assert.Less(CameraReach.PanK(1.3f), CameraReach.PanK(1f), "zoomed out past the full box, a bit less");
        }

        [Test]
        public void Reach_PitchFloorKeepsItsEndsOnTheNewRange()
        {
            Assert.AreEqual(24f, CameraReach.PitchFloor(1f), 1e-4f);
            Assert.AreEqual(24f, CameraReach.PitchFloor(1.3f), 1e-4f);
            Assert.AreEqual(14f, CameraReach.PitchFloor(CameraReach.MinZoom), 1e-4f);
            Assert.AreEqual(14f, CameraReach.PitchFloor(0.1f), 1e-4f);
        }

        [Test]
        public void DollScale_GrowsAsTheCameraBacksOffAndNeverPops()
        {
            Assert.AreEqual(1f, CameraReach.DollScale(CameraReach.MinZoom), 1e-5f, "close in the doll is its size");
            Assert.AreEqual(1f + CameraReach.DollScaleUp, CameraReach.DollScale(1f), 1e-5f);
            Assert.AreEqual(1f + CameraReach.DollScaleUp, CameraReach.DollScale(1.3f), 1e-5f);
            Assert.AreEqual(1f, CameraReach.DollScale(1f, 0f), 1e-6f, "amount 0 = off");
            float prev = 1f;
            for (float z = 0.2f; z <= 1.3f; z += 0.01f) { float s = CameraReach.DollScale(z); Assert.GreaterOrEqual(s, prev); Assert.Less(s - prev, 0.02f, "no pop"); prev = s; }
        }

        [Test]
        public void GlideBack_BringsTheAnglesHomeToo()
        {
            var g = new GlideBack();
            g.Remember(1f, 2f, 1f, 28f, 34f);
            float x = 0f, z = 0f, zoom = 0.7f, yaw = 28f, pitch = 50f;
            for (float t = 0f; t < GlideBack.Hold + GlideBack.Duration + 0.2f; t += 0.02f) g.Step(0.02f, ref x, ref z, ref zoom, ref yaw, ref pitch);
            Assert.AreEqual(34f, pitch, 1e-3f);
            Assert.AreEqual(28f, yaw, 1e-3f);
            Assert.AreEqual(1f, zoom, 1e-3f);
            Assert.IsFalse(g.Armed);
        }

        [Test]
        public void GlideBack_WaitsThenEasesHomeAndATouchStopsIt()
        {
            var g = new GlideBack();
            float x = 2f, z = 1f, zoom = 0.45f;
            Assert.IsFalse(g.Step(0.1f, ref x, ref z, ref zoom), "nothing remembered, nothing to do");
            g.Remember(0f, 0f, 1f);
            for (int i = 0; i < 10; i++) Assert.IsFalse(g.Step(0.1f, ref x, ref z, ref zoom), "holds first");
            Assert.AreEqual(2f, x);
            float prevX = x;
            bool done = false;
            for (int i = 0; i < 40; i++)
            {
                g.Step(0.05f, ref x, ref z, ref zoom);
                Assert.LessOrEqual(x, prevX + 1e-6f, "moves steadily home");
                prevX = x;
                if (!g.Armed) { done = true; break; }
            }
            Assert.IsTrue(done);
            Assert.AreEqual(0f, x, 1e-4f); Assert.AreEqual(1f, zoom, 1e-4f);

            g.Remember(0f, 0f, 1f); x = 2f; zoom = 0.45f;
            for (int i = 0; i < 20; i++) g.Step(0.1f, ref x, ref z, ref zoom);   // glide under way
            float mid = x;
            g.Cancel();
            Assert.IsFalse(g.Step(0.1f, ref x, ref z, ref zoom));
            Assert.AreEqual(mid, x, "a touch cancels it where it stands");
        }

        [Test]
        public void GlideBack_StartsAndEndsWithoutAJerk()
        {
            var g = new GlideBack();
            float x = 3f, z = 0f, zoom = 0.4f;
            g.Remember(0f, 0f, 1f);
            while (!g.Gliding) g.Step(0.016f, ref x, ref z, ref zoom);
            float x0 = x;
            g.Step(0.016f, ref x, ref z, ref zoom);
            Assert.Less(Math.Abs(x - x0), 0.01f, "eases in");
            float prev = x;
            float lastStep = 0f;
            while (g.Armed) { prev = x; g.Step(0.016f, ref x, ref z, ref zoom); lastStep = Math.Abs(x - prev); }
            Assert.Less(lastStep, 0.01f, "eases out");
        }

        // ---- #162 near-glass fade ----

        [Test]
        public void Fade_NoneFarOrOnTheBackPanes_FullClose()
        {
            Assert.AreEqual(0f, NearGlassFade.Amount(1f, 1f), "full-box view: nothing fades");
            Assert.AreEqual(0f, NearGlassFade.Amount(0.2f, -1f), "the back pane never fades, however close");
            Assert.AreEqual(0f, NearGlassFade.Amount(0.2f, 0f), "an edge-on pane has no fade");
            Assert.AreEqual(NearGlassFade.MaxPaneFade, NearGlassFade.Amount(0.2f, 1f), 1e-6f);
            Assert.AreEqual(NearGlassFade.MaxFrameFade, NearGlassFade.Amount(0.2f, 1f, isFrame: true), 1e-6f);
            Assert.AreEqual(1f, NearGlassFade.MaxPaneFade, "#319: a close pane goes fully clear");
        }

        [Test]
        public void Fade_IsSmoothInDistanceAndAngle_NoPop()
        {
            float prev = NearGlassFade.Amount(1.2f, 0.8f);
            for (float r = 1.2f; r >= 0.1f; r -= 0.002f)
            {
                float a = NearGlassFade.Amount(r, 0.8f);
                Assert.GreaterOrEqual(a, prev - 1e-6f, "closer never fades less, at " + r);
                Assert.Less(a - prev, 0.02f, "no pop zooming, at " + r);
                prev = a;
            }
            prev = NearGlassFade.Amount(0.3f, -1f);
            for (float f = -1f; f <= 1f; f += 0.002f)
            {
                float a = NearGlassFade.Amount(0.3f, f);
                Assert.GreaterOrEqual(a, prev - 1e-6f);
                Assert.Less(a - prev, 0.02f, "no pop orbiting, at " + f);
                prev = a;
            }
        }

        [Test]
        public void Fade_OrbitingAFullCircleKeepsTheBackHalfAndFadesTheFrontQuarterSmoothly()
        {
            // four panes with normals +-x, +-z; the camera orbits; the opposite pane of the one fading never fades
            for (int deg = 0; deg < 360; deg += 3)
            {
                double a = deg * Math.PI / 180.0;
                float cx = (float)Math.Cos(a), cz = (float)Math.Sin(a);
                float[] facing = { cx, -cx, cz, -cz };
                int visible = 0;
                for (int p = 0; p < 4; p++) if (NearGlassFade.Opacity(0.3f, facing[p]) > 0.99f) visible++;
                Assert.GreaterOrEqual(visible, 2, "at yaw " + deg + " the back panes stay in view");
            }
        }

        [Test]
        public void Fade_PlaneDistance_FadesAtFullZoomWhenTheCameraIsNearThePane()
        {
            Assert.AreEqual(0f, NearGlassFade.Amount(1f, 1f, planeRatio: 1f), 1e-6f, "far from the pane: nothing");
            Assert.AreEqual(1f, NearGlassFade.Amount(1f, 1f, planeRatio: 0.1f), 1e-6f, "right at the pane: gone");
            Assert.AreEqual(0f, NearGlassFade.Amount(1f, -1f, planeRatio: 0f), "the back pane never fades");
            Assert.AreEqual(0f, NearGlassFade.Amount(1f, 1f, cleanMode: true, planeRatio: 0f), "cloth in hand: stays");
            float prev = 0f;
            for (float r = 1f; r >= 0f; r -= 0.002f)
            {
                float a = NearGlassFade.Amount(1f, 1f, planeRatio: r);
                Assert.GreaterOrEqual(a, prev - 1e-6f);
                Assert.Less(a - prev, 0.02f, "no pop at " + r);
                prev = a;
            }
        }

        [Test]
        public void Fade_CleanModeAndForce()
        {
            Assert.AreEqual(0f, NearGlassFade.Amount(0.2f, 1f, cleanMode: true));
            Assert.AreEqual(1f, NearGlassFade.Opacity(0.2f, 1f, cleanMode: true));
            Assert.AreEqual(NearGlassFade.MaxPaneFade * 0.5f, NearGlassFade.Amount(1f, 1f, forced: 0.5f), 1e-6f, "the debug panel can force it at any zoom");
            float s = 0.9f;
            for (int i = 0; i < 120; i++) s = NearGlassFade.Follow(s, 0f, 0.016f);
            Assert.Less(s, 0.01f);
            float s1 = NearGlassFade.Follow(0.9f, 0f, 0.016f);
            Assert.Less(0.9f - s1, 0.1f, "a toggle moves it a step, not all at once");
        }
    }
}
