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
    /// <summary>Bundle 5 by touch on the real scene: tester run 9's findings. A line finishes off screen, every tap on mess and on bare land answers, the mill opens its
    /// card, the speaker's ring draws over the trees, and a winter dawn at the doors stays quiet.</summary>
    [Category("Bundle5")]
    public class Bundle5PlayTests : PlayTestBase
    {
        const int Seed = 1234;
        const string Line = "We will put the new field by the pond.";
        const string Look = "C:/Users/User/AppData/Local/Temp/claude/F--GameDev/0b07151d-6b1f-42ed-8a84-5cd49b334010/scratchpad/bundle5-shots/";
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

        IEnumerator Town(int seed = Seed)
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

        IEnumerator Grown(bool town) => town ? Town() : Village(30f);

        static List<(PersonRef who, Vector2 at)> Shown(bool walking = false)
        {
            var res = new List<(PersonRef, Vector2)>();
            var pv = People;
            foreach (var a in pv.Activities)
            {
                if (walking && !a.moving) continue;
                if (!pv.TryGetDollTop(a.who, out var w)) continue;
                var s = Camera.main.WorldToViewportPoint(w);
                if (s.z <= 0f || s.x < 0.05f || s.x > 0.95f || s.y < 0.3f || s.y > 0.9f) continue;
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

        static bool Unseen(PersonRef who)
        {
            var pv = People;
            if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) return false;
            var from = Camera.main.transform.position;
            return Box.Blocked(from, top) && Box.Blocked(from, (top + feet) * 0.5f);
        }

        // ---------------- item 1: off screen, the line finishes too ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator ASpeakerOffScreenFinishesTheLineAndTheRingComesBackWithThem()
        {
            yield return Village(30f);
            Cam.EaseTo(Vector3.zero, Cam.yaw, 50f, Cam.minZoom, 0.05f);
            yield return new WaitForSecondsRealtime(0.3f);
            var who = Shown().OrderBy(a => a.at.x).First().who;
            yield return Say(who);
            var line = Director.ShownBubble;
            Assert.IsNotNull(line);
            Assert.IsTrue(Director.SpeakerRingUp, "the ring under them");
            Assert.IsTrue(Director.SpeechMarkUp, "the speech mark over them");
            var back = (Cam.target, Cam.yaw, Cam.pitch, Cam.zoom);
            People.TryGetDollFeet(who, out var feet);
            Cam.EaseTo(feet + Vector3.right * 2.4f, Cam.yaw, 50f, Cam.minZoom, 0.05f); // the camera moves off them
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(People.TryGetDollTop(who, out var top) && IsOnScreen(top), "they've left the screen");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreSame(line, Director.ShownBubble, "the line isn't cut: the caption doesn't need them on screen");
            Assert.IsTrue(Director.CaptionUp);
            Cam.EaseTo(back.target, back.yaw, back.pitch, back.zoom, 0.05f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsNotNull(Director.ShownBubble, "still reading");
            Assert.IsTrue(Director.SpeakerRingUp, "the ring is under them");
            Assert.IsTrue(Director.SpeechMarkUp, "and the mark over them");
            AssertNoErrors("off screen");
        }

        static bool IsOnScreen(Vector3 w)
        {
            var s = Camera.main.WorldToScreenPoint(w);
            return s.z > 0f && s.x >= 0f && s.x <= Screen.width && s.y >= 0f && s.y <= Screen.height;
        }

        // ---------------- item 2: every tap on mess answers ----------------

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
        public IEnumerator AfterTheFirstCleanEveryTapOnMessStillPulses()
        {
            yield return Messy();
            CareView.Instance.TouchHint.Cleaned();
            var on = Mess().Where(p => Cards.PickDoll(p).IsNone).Take(3).ToList();
            Assert.Greater(on.Count, 0, "mess on view");
            int taps = 0, hints = CareView.Instance.TouchHint.HintsShown, pulses = Hud.CleanPulses, quiet = 0;
            for (int i = 0; i < 6; i++)
            {
                var at = on[i % on.Count];
                int before = Hud.CleanPulses;
                yield return TapAt(at);
                yield return Frames(3);
                taps++;
                Debug.Log($"[bundle5] tap {i} at {at}: pulses +{Hud.CleanPulses - before}, toast '{GameUiPanel.Toasts.Current}', {PersonCardView.Status()}");
                if (Hud.CleanPulses == before && !Cards.AnyOpen && !Cards.BuildingOpen) quiet++;
                Assert.IsFalse(Cards.AnyOpen, "mess under the finger beats the generous reach");
                Assert.IsFalse(Cards.BuildingOpen, "and the building behind it");
            }
            Assert.AreEqual(0, quiet, $"{quiet} of {taps} taps did nothing");
            Assert.AreEqual(hints, CareView.Instance.TouchHint.HintsShown, "the words stay first-times-only");
            Assert.AreNotEqual(CleanHint.Text, GameUiPanel.Toasts.Current);
            AssertNoErrors("mess after learning");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MessUnderTheFingerBeatsTheGenerousReach([Values(false, true)] bool learned)
        {
            yield return Messy();
            if (learned) CareView.Instance.TouchHint.Cleaned();
            var pv = People;
            var cam = Camera.main;
            var piece = CareView.Instance.Props.AllPieces.First(p => !p.leaving);
            float off = PersonCardView.PickRadius * 0.8f * Screen.width / GameUiPanel.ReferenceWidth;
            foreach (var a in pv.Activities)
            {
                if (!pv.TryGetDollFeet(a.who, out var feet)) continue;
                foreach (var dir in new[] { Vector2.right, Vector2.left })
                {
                    Vector2 s = (Vector2)cam.WorldToScreenPoint(feet) + dir * off;
                    if (s.y < Screen.height * 0.3f || s.y > Screen.height * 0.8f || s.x < 60f || s.x > Screen.width - 60f || UiInput.Over(s)) continue;
                    if (Cards.PickBuilding(s) >= 0 || !Cards.PickDoll(s).IsNone || Cards.PickPerson(s).IsNone) continue;
                    if (!ShapingView.Instance.GroundPoint(s, out float gx, out float gz)) continue;
                    piece.t.localPosition = new Vector3(gx, Box.GroundHeight(gx, gz) + 0.03f, gz);
                    yield return Frames(2);
                    Assert.IsTrue(CareView.Instance.MessSaysSo(s) || !learned, "the finger is on the mess");
                    int pulses = Hud.CleanPulses;
                    yield return TapAt(s);
                    yield return Frames(3);
                    Assert.IsFalse(Cards.AnyOpen, "no person with no doll under the finger");
                    Assert.Greater(Hud.CleanPulses, pulses, "the mess answers");
                    AssertNoErrors("reach");
                    yield break;
                }
            }
            Assert.Inconclusive("nobody on open ground in this run");
        }

        // ---------------- item 3: bare ground, the mill ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator ATapOnBareGroundAnswersWithARipple()
        {
            yield return Village();
            PeopleView.DebugSetHour(23.5f);
            yield return new WaitForSecondsRealtime(0.5f);
            Cam.GoHome(0.02f);
            yield return new WaitForSecondsRealtime(0.15f);
            var points = new List<Vector2>();
            var care = CareView.Instance;
            for (float y = Screen.height * 0.3f; y < Screen.height * 0.8f; y += 30f)
                for (float x = 60f; x < Screen.width - 60f; x += 30f)
                {
                    var p = new Vector2(x, y);
                    if (UiInput.Over(p) || care.GlassAt(p) || Cards.PickBuilding(p) >= 0 || !Cards.PickPerson(p).IsNone || care.MessSaysSo(p)) continue;
                    if (ShapingView.Instance.GroundPoint(p, out _, out _)) points.Add(p);
                }
            Debug.Log($"[bundle5] {points.Count} bare-ground points through the open top");
            Assert.Greater(points.Count, 3, "bare land through the open top");
            var at = points[points.Count / 2];
            int before = GlassVoice.Instance.Ripple.Count, knocks = GlassVoice.Instance.Knocks, pulses = Hud.CleanPulses;
            yield return TapAt(at);
            yield return Frames(4);
            Assert.AreEqual(before + 1, GlassVoice.Instance.Ripple.Count, "the ground answers");
            Assert.IsTrue(GlassVoice.Instance.Ripple.Up, "you can see it");
            yield return new WaitForSecondsRealtime(0.12f);
            Debug.Log($"[bundle5] ripple tap at {at}");
            yield return LookShot("ripple");
            Assert.IsFalse(Cards.AnyOpen || Cards.BuildingOpen, "no card");
            Assert.AreEqual(pulses, Hud.CleanPulses, "no Clean");
            Assert.IsNull(GameUiPanel.Toasts.Current, "no words");
            yield return new WaitForSecondsRealtime(GlassVoice.TapGap + 0.2f);
            Assert.AreEqual(knocks, GlassVoice.Instance.Knocks, "and no knock");
            Assert.IsFalse(GlassVoice.Instance.Ripple.Up, "and it's gone by itself");
            AssertNoErrors("ripple");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheMillOpensItsCardLikeAnyOtherBuilding([Values(false, true)] bool town)
        {
            yield return Grown(town);
            PeopleView.DebugSetHour(23.5f); // everyone in: this is about the mill
            yield return new WaitForSecondsRealtime(0.5f);
            Cam.GoHome(0.02f);
            yield return new WaitForSecondsRealtime(0.15f);
            var mill = Sim.State.buildings.FirstOrDefault(b => b.type == BuildingType.Windmill);
            Assert.IsNotNull(mill, "this box has a mill");
            var lot = Sim.Settlement.Lots.First(l => l.buildingId == mill.id && !l.retired);
            var box = Box;
            var hub = town ? Pieces.TowerHub : Pieces.WindmillHub;
            int asked = 0, opened = 0;
            string misses = "";
            foreach (float up in new[] { 0.1f, 0.25f, 0.4f, 0.55f, 0.7f })
            {
                if (up > hub.y + 0.05f) continue;
                Vector2 at = Camera.main.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(lot.x, box.GroundHeight(lot.x, lot.z) + up, lot.z)));
                if (UiInput.Over(at)) continue;
                Cards.CloseBuilding();
                yield return Frames(2);
                asked++;
                yield return TapAt(at);
                yield return Frames(2);
                if (Cards.BuildingId == mill.id) opened++;
                else misses += $" {up:0.00}->{PersonCardView.Status()}";
            }
            Debug.Log($"[bundle5] {(town ? "Town" : "Village")} mill: {opened} of {asked} body taps, lot {lot.x:0.00},{lot.z:0.00} {lot.w:0.00}x{lot.d:0.00}, misses:{misses}");
            Assert.Greater(asked, 2);
            Assert.AreEqual(asked, opened, "every tap on the mill opens its card:" + misses);
            Assert.IsTrue(Cards.BuildingCard.title.Contains("windmill"), Cards.BuildingCard.title);
            yield return LookShot($"mill-card-{(town ? "town" : "village")}");
            AssertNoErrors("mill");
        }

        // ---------------- item 4: the ring over the trees ----------------

        [UnityTest, Timeout(170000)]
        public IEnumerator TheSpeakersRingDrawsOverEverythingEvenBehindTheTrees()
        {
            yield return Village(30f);
            ShowMeDirector.BubblesOn = true;
            var pv = People;
            PersonRef hidden = PersonRef.None;
            foreach (float hour in new[] { 6.9f, 12.1f, 17.3f, 8f, 16f })
            {
                PeopleView.DebugSetHour(hour);
                for (int f = 0; f < 150 && hidden.IsNone; f++)
                {
                    yield return null;
                    foreach (var s in Shown(true))
                        if (Unseen(s.who)) { hidden = s.who; break; }
                }
                if (!hidden.IsNone) break;
            }
            Assert.IsFalse(hidden.IsNone, "someone walks behind something");
            yield return Say(hidden);
            Assert.IsTrue(Director.SpeakerRingUp, "the ring is up");
            var r = Director.Ring.Renderer;
            Assert.IsNotNull(r);
            Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.Always, r.sharedMaterial.GetFloat("_ZTest"), "no tree cuts the ring");
            Assert.GreaterOrEqual(r.sharedMaterial.renderQueue, 3000, "and it comes after what it draws over");
            Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.LessEqual, ShapingView.LoadFx().GetFloat("_ZTest"), "nothing else in the box lost its depth test");
            Debug.Log($"[bundle5] hidden speaker {hidden}, ring at {Director.Ring.At}, mark {Director.SpeechMarkUp}");
            yield return LookShot("ring-hidden-home");
            People.TryGetDollFeet(hidden, out var feet);
            Cam.EaseTo(feet, Cam.yaw, 45f, Cam.minZoom, 0.05f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return LookShot("ring-hidden-close");
            AssertNoErrors("ring");
        }

        // ---------------- item 5: a winter dawn at the doors ----------------

        /// <summary>Near enough to a door to count as standing at it, box units.</summary>
        const float AtDoor = 0.2f;
        const string Census = Look + "doors.txt";

        struct Dawn { public int worstDoor, doorsOver, axesIdle, stackAxes, chopWalls, chopFacing, chops, worstKnot; public string where; }

        static Dawn CountDawn(PeopleView pv, int frames)
        {
            var d = new Dawn { where = "" };
            for (int f = 0; f < frames; f++) pv.Tick(1f / 30f);
            var was = new Dictionary<int, Vector2>();
            foreach (var a in pv.Activities) if (pv.TryGetDollPosition(a.who, out var w)) was[a.who.Key] = w;
            pv.Tick(1f / 30f);
            var doors = new Dictionary<Place, int>();
            var lots = Sim.Settlement.Lots;
            var home = new List<Vector2>();
            foreach (var a in pv.Activities)
            {
                if (a.moving || !pv.TryGetDollTop(a.who, out _) || !pv.TryGetDollPosition(a.who, out var p)) continue;
                if (a.place.kind == PlaceKind.Home) home.Add(p);
                bool chops = DollMotion.StyleOf(a.kind) == DollStyle.Chop;
                if (a.prop == Prop.Axe && !chops) d.axesIdle++;
                if (a.kind == ActivityKind.StackWood && a.prop == Prop.Axe) d.stackAxes++;
                if (a.place.kind != PlaceKind.Home) continue;
                var lot = lots.FirstOrDefault(l => l.buildingId == a.place.buildingId && !l.retired);
                bool standing = was.TryGetValue(a.who.Key, out var then) && (p - then).magnitude < 0.0005f; // one coming out of the door on their way to the yard isn't standing at it
                if (standing && pv.Planner.Positions.TryGetPosition(a.place, out float dx, out float dz) && (p - new Vector2(dx, dz)).magnitude < AtDoor)
                {
                    doors.TryGetValue(a.place, out int n);
                    doors[a.place] = n + 1;
                    d.where += $" {a.place.buildingId}:{a.who.Key}:{a.kind}:{(p - new Vector2(dx, dz)).magnitude:0.00}";
                }
                if (a.kind != ActivityKind.ChopKindling || lot == null || !standing) continue;
                d.chops++;
                if (pv.Planner.Positions.TryGetPosition(a.place, out float ox, out float oz) && (p - new Vector2(ox, oz)).magnitude < 0.13f) d.chopWalls++; // at the door itself
                pv.TryGetDollFacing(a.who, out var face);
                if (Vector2.Dot(face, p - new Vector2(lot.x, lot.z)) < 0f) d.chopFacing++;
            }
            foreach (var kv in doors)
            {
                d.worstDoor = Mathf.Max(d.worstDoor, kv.Value);
                if (kv.Value > 2) d.doorsOver++;
            }
            foreach (var p in home) d.worstKnot = Mathf.Max(d.worstKnot, home.Count(o => (o - p).magnitude < 0.16f));
            return d;
        }

        [UnityTest, Timeout(170000)]
        public IEnumerator AWinterDawnKeepsTwoAtADoorAndTheAxesOffTheWalls([Values(false, true)] bool town)
        {
            yield return Grown(town);
            var pv = People;
            var planner = pv.Planner;
            pv.HandDriven = true;
            planner.ForcedSeason = Season.Winter;
            string first = null;
            int worst = 0, over = 0, idle = 0, stack = 0, walls = 0, facing = 0, chops = 0, knot = 0, samples = 0;
            try
            {
                for (int day = 0; day < 3; day++)
                {
                    foreach (float h in new[] { 6.0f, 6.3f, 6.6f, 7f, 7.6f, 8.2f, 12.2f, 14.5f })
                    {
                        pv.SetHour(h);
                        var c = CountDawn(pv, 75);
                        if (c.worstDoor > 2) first ??= $"h{h:0.0} day {day}:{c.where}";
                        samples++;
                        worst = Mathf.Max(worst, c.worstDoor); over += c.doorsOver; idle += c.axesIdle; stack += c.stackAxes; walls += c.chopWalls; facing += c.chopFacing; chops += c.chops; knot = Mathf.Max(knot, c.worstKnot);
                    }
                    SimLayoutSource.DebugSkipYears(0.09f);
                    planner.ForcedSeason = Season.Winter;
                    pv.Tick(1f / 30f);
                }
                string say = $"[bundle5] {(town ? "Town" : "Village")} winter: {samples} samples, worst at a door {worst}, doors over 2: {over}, worst knot {knot}, idle axes {idle} (stacking {stack}), {chops} choppers, at the door {walls}, facing the house {facing}; first over: {first}";
                Debug.Log(say);
                File.AppendAllText(Census, say + "\n");
                pv.SetHour(6.3f);
                for (int f = 0; f < 90; f++) pv.Tick(1f / 30f);
                pv.HandDriven = false;
                yield return Frames(2);
                yield return LookShot($"dawn-{(town ? "town" : "village")}");
                var home = pv.Activities.FirstOrDefault(a => a.place.kind == PlaceKind.Home && !a.moving && a.kind == ActivityKind.ChopKindling && pv.TryGetDollTop(a.who, out _));
                if (home.kind == ActivityKind.ChopKindling && pv.TryGetDollFeet(home.who, out var feet))
                {
                    Cam.EaseTo(feet, Cam.yaw, 42f, 0.5f, 0.05f);
                    yield return new WaitForSecondsRealtime(0.25f);
                    yield return LookShot($"dawn-{(town ? "town" : "village")}-close");
                }
                Assert.LessOrEqual(worst, 2, say);
                Assert.AreEqual(0, idle, say);
                Assert.AreEqual(0, walls, say);
                Assert.AreEqual(0, facing, say);
            }
            finally { pv.HandDriven = false; planner.ForcedSeason = null; }
            AssertNoErrors("dawn");
        }

        // ---------------- item 7 ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator TappingCleanTakesItsOwnHintDown()
        {
            yield return Messy();
            var on = Mess().First(p => Cards.PickDoll(p).IsNone);
            yield return TapAt(on);
            yield return Frames(3);
            Assert.AreEqual(CleanHint.Text, GameUiPanel.Toasts.Current, "the words are up");
            yield return TapAt(GameUiPanel.ScreenCenter(Hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(Hud.CleanMode, "clean mode");
            Assert.AreNotEqual(CleanHint.Text, GameUiPanel.Toasts.Current, "Clean was tapped: it stops asking");
            const string wipe = "Drag across the glass to wipe it. Flick junk and weeds out.";
            yield return Until(() => GameUiPanel.Toasts.Current == wipe, 6f); // a cheer from the box's clean-up may finish first
            Assert.AreEqual(wipe, GameUiPanel.Toasts.Current, "and the next hint gets the lane");
            AssertNoErrors("hint swap");
        }

        /// <summary>A wish over a doll under the finger: the bubble moves off the finger and its pill, the tail still reaches. And Osric's caption in the first run sits clear of Skip.</summary>
        [UnityTest, Timeout(170000)]
        public IEnumerator InTheFirstRunTheWordsKeepOffEachOther([Values(1234, 7531, 42)] int seed)
        {
            yield return OpenSlice(1179, 2556, seed, skipFirstRun: false);
            var fr = FirstRunView.Instance;
            Assert.IsTrue(fr.Active, "the first run");
            yield return Until(() => fr.Step == FirstRunStep.Wipe, 6f);
            fr.Run.DebugSkipTo(FirstRunStep.Name);
            yield return Frames(6);
            var skip = fr.Root.Q("firstrun-skip-row");
            ShowMeDirector.BubblesOn = true;
            string said = ShowMeDirector.DebugSayOver(PersonRef.Notable(Sim.State.notables.First(n => n.alive).id), "They asked me and I said yes before I could think. That's how it goes.");
            yield return Frames(4);
            Debug.Log($"[bundle5] caption: {said}, up {Director.CaptionUp}, caption {Director.CaptionBound}, skip {skip.worldBound} shown {skip.resolvedStyle.display}");
            if (Director.CaptionUp && skip.resolvedStyle.display == DisplayStyle.Flex) Assert.IsFalse(Director.CaptionBound.Overlaps(skip.worldBound), "the caption isn't over Skip");
            yield return LookShot($"fr-caption-{seed}");
            yield return TapAt(GameUiPanel.ScreenCenter(fr.Root, "firstrun-plate"));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return TapAt(GameUiPanel.ScreenCenter(fr.Root, "firstrun-name-keep"));
            yield return Frames(10);
            Assert.AreEqual(FirstRunStep.Rain, fr.Step);
            yield return new WaitForSecondsRealtime(2f);
            yield return TapAt(GameUiPanel.ScreenCenter(fr.Root, "firstrun-rain"));
            yield return new WaitForSecondsRealtime(1f);
            yield return Frames(4);
            var bubble = fr.Root.Q("firstrun-bubble");
            var ghost = fr.Root.Q("firstrun-ghost");
            Debug.Log($"[bundle5] rain: hint '{fr.ShownHintId}' bubble {bubble.worldBound} {bubble.resolvedStyle.display} ghost {ghost.worldBound} {ghost.resolvedStyle.display}");
            yield return LookShot($"fr-tap-a-spot-{seed}");
            // Round the box's turn and out to its widest, where the finger comes nearest the wisher.
            int clashes = 0, views = 0;
            foreach (float zoom in new[] { 1f, 1.3f })
                for (float yaw = Cam.minYaw; yaw <= Cam.maxYaw; yaw += (Cam.maxYaw - Cam.minYaw) / 8f)
                {
                    Cam.EaseTo(Vector3.zero, yaw, Cam.pitch, zoom, 0.02f);
                    yield return new WaitForSecondsRealtime(0.1f);
                    yield return Frames(2);
                    views++;
                    if (bubble.resolvedStyle.display == DisplayStyle.Flex && ghost.resolvedStyle.display == DisplayStyle.Flex && bubble.worldBound.Overlaps(ghost.worldBound)) clashes++;
                }
            Debug.Log($"[bundle5] seed {seed}: {views} views, wish stepped off the finger on {fr.WishSteps} frames, clashes left {clashes}");
            Assert.AreEqual(0, clashes, "the wish is off the finger");
            AssertNoErrors("first run words");
        }
    }
}
