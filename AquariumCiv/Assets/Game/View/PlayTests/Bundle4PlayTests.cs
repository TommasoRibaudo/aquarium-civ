using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.Shaping;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Bundle 4 by touch on the real scene: a line's ring and speech mark are on its speaker (#204, #327), a roof tap can't open someone the camera can't
    /// see, and every tap on mess gets an answer without taking a building's card (tester run 8).</summary>
    [Category("Bundle4")]
    public class Bundle4PlayTests : PlayTestBase
    {
        const int Seed = 1234;
        const string Line = "We will put the new field by the pond.";
        const string Look = "C:/Users/User/AppData/Local/Temp/claude/F--GameDev/0b07151d-6b1f-42ed-8a84-5cd49b334010/scratchpad/bundle4-shots/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static GameHud Hud => GameHud.Instance;
        static PersonCardView Cards => PersonCardView.Instance;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => Object.FindAnyObjectByType<BoxView>();
        static PeopleView People => PeopleView.Find();
        static ShowMeDirector Director => ShowMeDirector.Instance;

        static IEnumerator LookShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Look);
            File.WriteAllBytes(Look + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        IEnumerator Village(float years = 14f, int seed = Seed)
        {
            yield return OpenSlice(1080, 1920, seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(years);
            yield return Frames(3);
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        IEnumerator Town(int seed)
        {
            yield return Village(14f, seed);
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Sim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(10f);
            yield return Frames(6);
            Sim.CleanAll();
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        /// <summary>#327: the ring sits at the speaker's feet (box units off), false with no doll.</summary>
        static bool RingOn(PersonRef who, out float off)
        {
            off = float.MaxValue;
            if (!People.TryGetDollFeet(who, out var feet) || !Director.SpeakerRingUp) return false;
            off = (Director.Ring.At - feet).magnitude;
            return true;
        }

        /// <summary>Everyone shown now, by where their head is on screen.</summary>
        static List<(PersonRef who, Vector2 at)> Shown(bool walking = false)
        {
            var res = new List<(PersonRef, Vector2)>();
            var pv = People;
            foreach (var a in pv.Activities)
            {
                if (walking && !a.moving) continue;
                if (!pv.TryGetDollTop(a.who, out var w)) continue;
                var s = Camera.main.WorldToViewportPoint(w);
                if (s.z <= 0f || s.x < 0f || s.x > 1f || s.y < 0.3f || s.y > 0.9f) continue;
                res.Add((a.who, new Vector2(s.x, s.y)));
            }
            return res;
        }

        static IEnumerator Say(PersonRef who)
        {
            ShowMeDirector.BubblesOn = true; // off, the director takes a line down at once
            StringAssert.StartsWith("line", ShowMeDirector.DebugSayOver(who, Line));
            yield return new WaitForSecondsRealtime(0.25f);
        }

        // ---------------- #204, since #327 Option B: the ring and the speech mark tie the caption to its speaker ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator TwoSpeakersCloseTogetherEachGetTheirOwnRingAndMark()
        {
            yield return Village(30f);
            PeopleView.DebugSetHour(18.8f);
            yield return new WaitForSecondsRealtime(1.2f);
            var all = Shown();
            Assert.Greater(all.Count, 3, "people about");
            var pair = (from a in all from b in all where a.who.Key < b.who.Key orderby (a.at - b.at).sqrMagnitude select (a, b)).First();
            Debug.Log($"[#204] pair {pair.a.who} {pair.b.who} {(pair.a.at - pair.b.at).magnitude * Screen.width:0} px apart");
            foreach (var (who, other) in new[] { (pair.a.who, pair.b.who), (pair.b.who, pair.a.who) })
            {
                yield return Say(who);
                Assert.IsTrue(Director.CaptionUp, "the line is on the caption");
                Assert.AreEqual(who, Director.LineSpeaker);
                Assert.IsTrue(RingOn(who, out float off), "the speaker has the ring");
                Assert.Less(off, 0.02f, $"the ring is at {who}'s feet");
                Assert.IsTrue(Director.SpeechMarkUp, "and the speech mark");
                Assert.AreEqual(who, Director.Chatter.SpeechDrawnOver, "over them, not the neighbour");
                yield return LookShot($"ring-pair-{who.Key}");
            }
            AssertNoErrors("pair");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheRingAndTheMarkFollowAWalker()
        {
            yield return Village(30f);
            PeopleView.DebugSetHour(6.9f);
            yield return new WaitForSecondsRealtime(1f);
            var walkers = Shown(true);
            Assume.That(walkers.Count, Is.GreaterThan(0), "someone walking on view");
            var who = walkers[0].who;
            yield return Say(who);
            Assert.AreEqual(BubbleSource.Ambient, Director.ShownBubble.source);
            float worst = 0f, end = Time.realtimeSinceStartup + 2.5f;
            int frames = 0;
            while (Time.realtimeSinceStartup < end && Director.ShownBubble != null)
            {
                yield return null;
                if (!RingOn(who, out float off)) break;
                frames++;
                worst = Mathf.Max(worst, off);
                Assert.AreEqual(who, Director.Chatter.SpeechDrawnOver, "the mark stays over the walker");
            }
            Debug.Log($"[#204] walker {who}: {frames} frames, worst ring off {worst:0.000}");
            Assert.Greater(frames, 5);
            Assert.Less(worst, 0.05f, "the ring stays under the walker");
            yield return LookShot("ring-walker");
            AssertNoErrors("walker");
        }

        /// <summary>#143 over #327: the line finishes its read time. With nobody to stand under or over, the ring and the mark go.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator ASpeakerGoneIndoorsFinishesTheLineAndNothingPointsAtNothing()
        {
            yield return Village(30f);
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(1f);
            var who = Shown()[0].who;
            yield return Say(who);
            var line = Director.ShownBubble;
            Assert.IsNotNull(line);
            Assert.IsTrue(Director.SpeakerRingUp, "the speaker has their ring while the line is up");
            Assert.IsTrue(Director.SpeechMarkUp, "and the speech mark");
            float start = Time.realtimeSinceStartup;
            PeopleView.DebugSetHour(23.5f); // everyone goes in at once
            yield return Frames(3);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreSame(line, Director.ShownBubble, "the line isn't cut");
            Assert.IsFalse(Director.SpeakerRingUp, "no ring under nobody");
            Assert.IsFalse(Director.Chatter.SpeechDrawn, "and no mark over nobody");
            yield return Until(() => Director.ShownBubble == null, line.seconds + 2f);
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, line.seconds - 1.2f, "it finishes at its own time");
            Assert.IsTrue(GameUiPanel.Bubbles.Free(BubbleHost.Rank.World), "the word is free again");
            AssertNoErrors("indoors");
        }

        // ---------------- tester run 8: a roof tap and who's behind it ----------------

        /// <summary>Roofs' tops, a few points each.</summary>
        static List<Vector2> RoofPoints()
        {
            var res = new List<Vector2>();
            var box = Box;
            var cam = Camera.main;
            foreach (var l in Sim.Settlement.Lots.Where(x => !x.retired && x.buildingId > 0 && x.kind != LotKind.Farm && x.kind != LotKind.Pasture && x.kind != LotKind.Orchard))
                foreach (var f in new[] { 0.2f, 0.42f, 0.55f })
                    foreach (var side in new[] { -0.3f, 0f, 0.3f })
                    {
                        Vector2 at = cam.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(l.x + side * l.w, box.GroundHeight(l.x, l.z) + f, l.z)));
                        if (at.x > 40f && at.x < Screen.width - 40f && at.y > Screen.height * 0.25f && at.y < Screen.height * 0.8f && !UiInput.Over(at)) res.Add(at);
                    }
            return res;
        }

        static bool Unseen(PersonRef who)
        {
            var pv = People;
            if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) return false;
            var from = Camera.main.transform.position;
            return Box.Blocked(from, top) && Box.Blocked(from, (top + feet) * 0.5f);
        }

        [UnityTest, Timeout(170000)]
        public IEnumerator ARoofTapNeverOpensSomeoneTheCameraCannotSee([Values(1234, 7531)] int seed)
        {
            yield return Town(seed);
            var pv = People;
            int asked = 0, bad = 0, seenBehind = 0;
            string first = null;
            var badPoint = default(Vector2);
            foreach (float hour in new[] { 8f, 10f, 12f, 14f, 16f, 18.5f })
            {
                PeopleView.DebugSetHour(hour);
                yield return new WaitForSecondsRealtime(0.5f);
                Cam.GoHome(0.02f);
                yield return new WaitForSecondsRealtime(0.1f);
                foreach (var at in RoofPoints())
                {
                    asked++;
                    var who = Cards.PickDoll(at);
                    if (who.IsNone && Cards.PickBuilding(at) < 0) who = Cards.PickPerson(at); // the generous reach, past the lot's box
                    if (who.IsNone) continue;
                    seenBehind++;
                    if (Unseen(who)) { bad++; if (first == null) { first = $"{who} at {at} hour {hour}"; badPoint = at; } }
                }
            }
            Debug.Log($"[tester8] seed {seed}: {asked} roof points, doll picked at {seenBehind}, of them hidden {bad} ({first})");
            if (bad > 0)
            {
                yield return LookShot($"roof-hidden-{seed}");
                yield return TapAt(badPoint);
                yield return Frames(2);
            }
            Assert.AreEqual(0, bad, "a doll nobody can see never takes a roof's tap: " + first);
            // And the real thing, by touch: a roof with nobody visible on it opens the building.
            Cam.GoHome(0.02f);
            yield return new WaitForSecondsRealtime(0.1f);
            var roofs = RoofPoints().Where(p => Cards.PickDoll(p).IsNone && Cards.PickBuilding(p) >= 0).Take(4).ToList();
            Assert.Greater(roofs.Count, 0, "a bare roof on view");
            foreach (var roof in roofs)
            {
                yield return TapAt(roof);
                yield return Frames(2);
                if (Cards.BuildingOpen) break;
                Debug.Log($"[tester8] roof tap at {roof}: person open {Cards.AnyOpen}, doll now {Cards.PickDoll(roof)}, mess {CareView.Instance.MessSaysSo(roof)}"); // someone walked under the finger
                Cards.Close(); Cards.CloseCrowd();
                yield return Frames(2);
            }
            Assert.IsTrue(Cards.BuildingOpen, "a bare roof opens its building");
            AssertNoErrors("roofs");
        }

        // ---------------- tester run 8: taps on mess ----------------

        IEnumerator Messy()
        {
            yield return Village();
            Sim.State.overgrowth = 0.5f;
            CareView.DebugCleanHint();
            yield return Frames(12);
            Cam.GoHome(0.02f);
            yield return new WaitForSecondsRealtime(0.15f);
        }

        static List<Vector2> Mess()
        {
            var l = new List<Vector2>();
            CareView.Instance.MessOnScreen(l);
            return l.Where(p => p.x > 60f && p.x < Screen.width - 60f && p.y > Screen.height * 0.3f && p.y < Screen.height * 0.8f && !UiInput.Over(p)).ToList();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EveryTapOnMessGetsAnAnswerInTheOpen()
        {
            yield return Messy();
            var on = Mess().First(p => Cards.PickDoll(p).IsNone);
            int pulses = Hud.CleanPulses;
            yield return TapAt(on);
            yield return Frames(3);
            Assert.AreEqual(CleanHint.Text, GameUiPanel.Toasts.Current, "the first tap says it in words");
            Assert.Greater(Hud.CleanPulses, pulses, "and the button pulses");
            pulses = Hud.CleanPulses;
            yield return TapAt(on); // well inside the pulse gap
            yield return Frames(3);
            Assert.Greater(Hud.CleanPulses, pulses, "a second tap straight after still gets an answer");
            Assert.IsFalse(Cards.BuildingOpen || Cards.AnyOpen, "and opens nothing");
            var toast = Hud.GetComponentsInChildren<UIDocument>(true).Select(d => d.rootVisualElement.Q("hud-toast")).FirstOrDefault(e => e != null);
            Assert.IsNotNull(toast);
            var pill = MomentPlayer.Instance.WatchPillRect;
            Assert.Greater(pill.height, 1f, "this box's Watch pill is up");
            Assert.IsFalse(toast.worldBound.Overlaps(pill), $"the pill doesn't cover the words: toast {toast.worldBound} pill {pill}");
            yield return LookShot("mess-words-over-pill");
            AssertNoErrors("mess tap");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ABuildingNearMessKeepsItsCardUnlessTheFingerIsOnTheMess()
        {
            yield return Messy();
            var mess = Mess();
            Assert.Greater(mess.Count, 0, "mess on view");
            float tight = CareView.Instance.TapReachPixels;
            Vector2? near = null;
            float loose = 0.035f * Screen.height;
            foreach (var l in Sim.Settlement.Lots.Where(x => !x.retired && x.buildingId > 0 && x.kind != LotKind.Farm && x.kind != LotKind.Pasture && x.kind != LotKind.Orchard))
                for (float a = 0f; a < 6.28f && near == null; a += 0.5f)
                    for (float r = 0.05f; r < 0.3f && near == null; r += 0.05f)
                    {
                        Vector2 at = Camera.main.WorldToScreenPoint(Box.transform.TransformPoint(new Vector3(l.x + Mathf.Cos(a) * r, Box.GroundHeight(l.x, l.z) + 0.4f, l.z + Mathf.Sin(a) * r)));
                        float d = mess.Min(m => (m - at).magnitude);
                        if (d > tight * 1.1f && d < loose * 0.95f && !UiInput.Over(at) && Cards.PickBuilding(at) >= 0 && Cards.PickDoll(at).IsNone) near = at;
                    }
            Assume.That(near.HasValue, "a roof inside the old reach of some mess");
            Debug.Log($"[tester8] roof point {near} tight reach {tight:0}px old {loose:0}px");
            yield return TapAt(near.Value);
            yield return Frames(3);
            Assert.IsTrue(Cards.BuildingOpen, "the finger isn't on the mess: the building opens");
            AssertNoErrors("near mess");
        }

        // ---------------- #202 ----------------

        [UnityTest, Timeout(120000)]
        [Category("Mills")]
        public IEnumerator TheMillsSailsGoWithTheWind()
        {
            yield return Village(30f);
            var wind = WindView.Instance;
            Assert.IsNotNull(wind, "the box made it");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.GreaterOrEqual(wind.Mills, 1, "this box has a mill with sails");
            try
            {
                WindView.Held = 0f;
                yield return Frames(2);
                float a = wind.Angle;
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(a, wind.Angle, 0.01f, "still air: they stand");
                WindView.Held = WindDrift.Gentle;
                a = wind.Angle;
                yield return new WaitForSecondsRealtime(0.5f);
                float gentle = Mathf.Abs(Mathf.DeltaAngle(a, wind.Angle));
                WindView.Held = WindDrift.Brisk;
                a = wind.Angle;
                yield return new WaitForSecondsRealtime(0.5f);
                float brisk = Mathf.Abs(Mathf.DeltaAngle(a, wind.Angle));
                Debug.Log($"[#202] half a second: gentle {gentle:0.0} deg, brisk {brisk:0.0} deg, {WindView.Status()}");
                Assert.Greater(gentle, 2f, "a breeze: they drift round");
                Assert.Greater(brisk, gentle * 2f, "a wind: they go");
                var sails = wind.transform.Find("Sails").GetComponentsInChildren<MeshRenderer>().ToList(); // DontSave: FindObjectsByType skips them
                Assert.AreEqual(wind.Mills, sails.Count, "a set a mill");
                var tip = sails[0].transform.position;
                Vector2 on = Camera.main.WorldToViewportPoint(tip);
                Debug.Log($"[#202] hub at viewport {on}");
                Assert.IsTrue(on.x > 0f && on.x < 1f && on.y > 0f && on.y < 1f, "on view");
                var hubAt = sails[0].transform.position;
                Assert.AreEqual(WindView.DebugNext().Contains("wind"), true);
                WindView.Held = 0f;
                Cam.EaseTo(hubAt, Cam.yaw, 35f, Cam.minZoom, 0.05f);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return LookShot("wind-a");
                WindView.Held = WindDrift.Brisk;
                yield return new WaitForSecondsRealtime(0.45f);
                WindView.Held = 0f;
                yield return LookShot("wind-b");
            }
            finally { WindView.Held = -1f; }
            AssertNoErrors("wind");
        }

        /// <summary>A sail's tip is over this height from the mill's foot, and both mills' caps stop short of it.</summary>
        const float SailTop = 0.85f;

        /// <summary>The chunks hold something at sail height over this spot: the sails were built in.</summary>
        static bool SailsInChunks(BoxView box, float x, float z)
        {
            float ground = box.GroundHeight(x, z);
            foreach (var f in box.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || !f.sharedMesh.name.StartsWith("BoxChunk")) continue;
                foreach (var v in f.sharedMesh.vertices)
                    if ((new Vector2(v.x - x, v.z - z)).magnitude < 0.5f && v.y - ground > SailTop) return true;
            }
            return false;
        }

        static List<MeshRenderer> Turning(BoxView box)
        {
            var root = box.transform.Find("Sails");
            return root == null ? new List<MeshRenderer>() : root.GetComponentsInChildren<MeshRenderer>().ToList();
        }

        /// <summary>Each mill in the layout has a real set of sails: in the chunks, or a mesh of its own that draws, at full size, hanging on that mill's hub.</summary>
        static string SailProblem(BoxView box)
        {
            var mills = box.layout.pieces.Where(p => p.kind == PieceKind.Windmill && p.era != Era.Camp).ToList();
            var live = Turning(box);
            foreach (var m in mills)
            {
                if (SailsInChunks(box, m.x, m.z)) { if (live.Count > 0) return $"{m.era} mill has two sets"; continue; }
                var hub = box.transform.TransformPoint(box.land.OnGround(m.x, m.z) + Quaternion.Euler(0f, m.yaw, 0f) * (m.era == Era.Town ? Pieces.TowerHub : Pieces.WindmillHub));
                var on = live.FirstOrDefault(r => (r.transform.position - hub).magnitude < 0.01f);
                if (on == null) return $"{m.era} mill at {m.x:0.00},{m.z:0.00} has no sails: {live.Count} sets live, wind {(box.GetComponent<WindView>() != null ? "there" : "none")}";
                var mesh = on.GetComponent<MeshFilter>().sharedMesh;
                if (mesh == null || mesh.vertexCount == 0 || on.sharedMaterial == null || !on.enabled || !on.gameObject.activeInHierarchy) return $"{m.era} sails don't draw";
                if (Mathf.Abs(on.transform.lossyScale.y - box.transform.lossyScale.y) > 0.01f) return $"{m.era} sails scaled {on.transform.lossyScale.y}";
                if (on.bounds.extents.magnitude < 0.3f) return $"{m.era} sails too small: {on.bounds.extents}";
            }
            return null;
        }

        static BoxView MillBox(string name, Era era)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var box = go.AddComponent<BoxView>();
            box.bakeAO = false;
            box.logRebuilds = false;
            box.layout.autoNature = false;
            box.layout.pieces.Add(new PieceData { kind = PieceKind.Windmill, era = era, x = 0.3f, z = 0.2f, seed = 3 });
            go.SetActive(true);
            return box;
        }

        /// <summary>The user's bare mills: a box nothing bootstrapped still has sails, on the post mill and the tower mill, a new mill gets them in the frame it stands
        /// in, and a second box, which can't have the wind, keeps them in its chunks.</summary>
        [UnityTest, Timeout(60000)]
        [Category("Mills")]
        public IEnumerator AMillInABoxMadeAfterTheBootstrapHasItsSails([Values(Era.Village, Era.Town)] Era era)
        {
            if (WindView.Instance != null) { Object.DestroyImmediate(WindView.Instance.gameObject); yield return null; } // the class's last test leaves its scene loaded
            Assert.IsNull(WindView.Instance, "no scene has a wind here");
            var first = MillBox("MillBoxA", era);
            var second = MillBox("MillBoxB", era);
            try
            {
                yield return null;
                yield return null;
                Debug.Log($"[#202] {era} first: wind {first.GetComponent<WindView>() != null}, chunks {SailsInChunks(first, 0.3f, 0.2f)}, turning {Turning(first).Count}; second: wind on {second.GetComponent<WindView>()?.enabled}, chunks {SailsInChunks(second, 0.3f, 0.2f)}, turning {Turning(second).Count}");
                Assert.IsNull(SailProblem(first), "the first box");
                Assert.IsNull(SailProblem(second), "a second box");
                first.layout.pieces.Add(new PieceData { kind = PieceKind.Windmill, era = era, x = -0.8f, z = -0.3f, seed = 5 });
                first.Rebuild();
                yield return null; // a new mill's sails come with the frame it stands in, not at the next half-second sync
                Assert.IsNull(SailProblem(first), "a new mill isn't left bare");
            }
            finally
            {
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
            }
        }

        /// <summary>The path the user saw: the real scene loaded by a test that doesn't run the bootstraps, grown through both mills, with a look at each.</summary>
        [UnityTest, Timeout(170000)]
        [Category("Mills")]
        public IEnumerator BothMillsHaveSailsOnTheirHubsInTheRealScene([Values(true, false)] bool bootstraps)
        {
            yield return OpenSlice(1080, 1920, Seed, bootstraps: bootstraps);
            ShowMeDirector.BubblesOn = false;
            var clock = GameClock.Instance;
            var box = Box;
            WindView.Held = 0f;
            try
            {
                foreach (var era in new[] { Era.Village, Era.Town })
                {
                    for (int i = 0; i < 12 && clock.Sim.State.era < era; i++) { clock.Sim.ForceNextEra(); yield return Frames(3); yield return Until(() => { var src = SimLayoutSource.Find(); return (MomentPlayer.Instance == null || !MomentPlayer.Instance.Playing) && (src == null || (!src.MomentLook && !src.WaveRunning)); }, 90f); // the era moment shows the old look until it ends: a mill built meanwhile is in the old era
                         SimLayoutSource.DebugSkipYears(10f); yield return Frames(3); }
                    for (int i = 0; i < 20 && !box.layout.pieces.Any(p => p.kind == PieceKind.Windmill && p.era == era); i++) { if (i % 5 == 0) SimLayoutSource.DebugSkipYears(15f); yield return Frames(12); }
                    yield return Frames(3);
                    var mill = box.layout.pieces.FirstOrDefault(p => p.kind == PieceKind.Windmill && p.era == era);
                    Debug.Log($"[#202] scene, bootstraps {bootstraps}, {era}: era now {clock.Sim.State.era}, mill {(mill != null)}, wind {box.GetComponent<WindView>() != null}, instance {WindView.Instance != null}, turning {Turning(box).Count}, chunks {(mill != null && SailsInChunks(box, mill.x, mill.z))}");
                    Assume.That(mill != null, $"a {era} mill in this box");
                    Assert.IsNull(SailProblem(box), era.ToString());
                    if (!bootstraps || !Application.isPlaying) continue;
                    Camera.main.GetComponent<BoxCamera>().EaseTo(box.transform.TransformPoint(box.land.OnGround(mill.x, mill.z)), Cam.yaw, 35f, Cam.minZoom, 0.05f);
                    yield return new WaitForSecondsRealtime(0.4f);
                    yield return LookShot("mill-" + era);
                    Cam.GoHome(0.02f);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }
            finally { WindView.Held = -1f; }
            AssertNoErrors("mills");
        }

        // ---------------- tester run 8: the marks ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator KnockMarksDrawOverEverythingAndSpreadOverATightGroup()
        {
            yield return Village(30f);
            ShowMeDirector.BubblesOn = true;
            yield return new WaitForSecondsRealtime(0.2f);
            GlassVoice.DebugGather(10);
            yield return new WaitForSecondsRealtime(4f);
            var chatter = Director.Chatter;
            var pv = People;
            var at = Shown().OrderBy(a => a.at.y).ToList();
            Assert.Greater(at.Count, 2);
            int n = 0;
            foreach (var a in pv.Activities)
            {
                if (!pv.TryGetDollTop(a.who, out _) || n >= 6) continue;
                n++;
                chatter.DebugMark(pv, ChatterMark.Bang);
            }
            string said = "";
            yield return Until(() => { said = GlassVoice.DebugKnock(1); return true; }, 0.1f);
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log($"[tester8] knock: {said}, marks {chatter.MarksDrawn}, {chatter.Status()}");
            Assert.IsNotNull(chatter.Renderer, "marks drawn");
            Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.Always, chatter.Renderer.sharedMaterial.GetFloat("_ZTest"), "no roof or frame cuts a mark");
            Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.LessEqual, ShapingView.LoadFx().GetFloat("_ZTest"), "and nothing else in the box lost its depth test");
            yield return LookShot("marks-knock");
            int close = 0;
            float least = chatter.MarkPixels * 0.9f;
            for (int i = 0; i < chatter.DrawnAt.Count; i++)
                for (int j = i + 1; j < chatter.DrawnAt.Count; j++)
                    if (Vector2.Distance(chatter.DrawnAt[i], chatter.DrawnAt[j]) < least) close++;
            Debug.Log($"[tester8] {chatter.DrawnAt.Count} marks, {close} pairs on top of each other");
            Assert.Greater(chatter.DrawnAt.Count, 1, "several marks up");
            Assert.AreEqual(0, close, "none on top of another");
            AssertNoErrors("marks");
        }
    }
}
