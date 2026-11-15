using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.Cards;
using UnityEngine;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The stops of playtest 1, batch 2 (2026-11-14 notes; the top section of docs/open-questions.md): #707 #704 #701 #676 #706 #703 #669 #700
    /// #690 #671 (look), #705 and #700 (animation: the spec text first, then the beat live), and the decisions (read from the file, nothing
    /// staged). Each stop sets its state up with the existing cheats and frames what its question asks about. A key with a "+" is the
    /// second item of that issue in the file (#700 has a look stop and an animation stop): <see cref="BuildStops"/> falls back to it when the
    /// plain key is taken. Live stops are watched twice with the panel's own "Set up again".
    /// </summary>
    public partial class OpenQuestionsSignOff
    {
        // ---------------------------------------------------------------- the spec text (animation-specs.md B27 / B28)
        /// <summary>One spec section of docs/design/animation-specs.md ("B27"), as plain lines: its tables read "row: text".</summary>
        static string SpecText(string id)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "design", "animation-specs.md"));
            if (!File.Exists(path)) return "(docs/design/animation-specs.md not found)";
            var sb = new StringBuilder();
            bool on = false;
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("## "))
                {
                    if (on) break;
                    if (line.StartsWith("## " + id + ".")) { on = true; sb.AppendLine(Clean(line.Substring(3))); }
                    continue;
                }
                if (!on) continue;
                if (line.StartsWith("|"))
                {
                    var cells = line.Trim().Trim('|').Split('|');
                    if (cells.Length < 2) continue;
                    string a = Clean(cells[0]), b = Clean(string.Join("|", cells.Skip(1)));
                    if ((a.Length == 0 && b.Length == 0) || a.StartsWith("---")) continue;
                    sb.AppendLine("  " + (a.Length > 0 ? a + ": " : "") + b);
                    continue;
                }
                if (line.StartsWith("### ")) { sb.AppendLine(); sb.AppendLine(Clean(line.Substring(4))); continue; }
                if (line.Trim().Length > 0) sb.AppendLine(Clean(line));
            }
            return on ? sb.ToString().TrimEnd() : "(" + id + " not found in docs/design/animation-specs.md)";
        }

        // ---------------------------------------------------------------- the table
        Stop[] Pt1b(string key)
        {
            switch (key)
            {
                case "#707": return new[] { S("#707", "the HUD top: bare icons over sand, night, snow and the pond", "pt1b_hud_top",
                    "The default view at midday with the panel folded so the top row shows (tap the arrow to read this). Good: Settings, Feedback and Photo are bare icons in the " +
                    "title's ink, side by side, readable over the sand with no disc behind them. Night, Winter and Over the pond change what is behind them; judge each.",
                    SetupHudTop, ("Night", HudNightExtra), ("Winter", HudWinterExtra), ("Over the pond", HudPondExtra)) };
                case "#704": return new[] { With(S("#704", "the fire on dry ground and snow", "pt1b_fire_ground",
                    "A fresh camp (its own box) by day, close on the log fire over dry bare ground. Good: no green ring at the fire's base, the ground under it reads as " +
                    "ash and dirt. Grass fire and Log fire swap the stage; Snow puts the camp in winter: still no green ring, the fire sits in the snow.",
                    SetupFireGround, ("Grass fire", () => FireGroundExtra(1)), ("Log fire", () => FireGroundExtra(2)), ("Snow", FireSnowExtra)), box: "camp") };
                case "#701": return new[] { S("#701", "the pond's rim at the glass", "pt1b_pond_rim",
                    "Close on the pond's edge where it reaches the glass (from above, so the water and the rim beside it show). Good: the water ends inside the dug dirt rim, also at the pane, and does not run over " +
                    "the dirt. Whole pond shows all of it; Under ice: the ice shares the same edge; Thawed puts the water back.",
                    SetupPondRim, ("Whole pond", PondWholeExtra), ("Under ice", () => PondRimIceExtra(true)), ("Thawed", () => PondRimIceExtra(false))) };
                case "#676": return new[] { S("#676", "the stew by kind: fish, rabbit, berries, thin", "pt1b_stew_kind",
                    "The camp's stew pot up close at the cook's hour, starting with fish stew. Good: each kind looks like what is in the pot: fish (pale pieces, white flecks), " +
                    "rabbit (dark meat chunks), berries (red and purple), thin (a clear broth with a few greens). Press each; At midday shows it in full daylight.",
                    SetupStewKind, ("Fish", () => StewKindExtra(StewKind.Fish)), ("Rabbit", () => StewKindExtra(StewKind.Rabbit)), ("Berries", () => StewKindExtra(StewKind.Berries)),
                    ("Thin", () => StewKindExtra(StewKind.Thin)), ("At midday", StewMiddayExtra)) };
                case "#706": return new[] { S("#706", "the newcomers: touch choice marks", "pt1b_touch_choice",
                    "The newcomers' choice staged in the box: two figures with a halo and a one to three word tag each, and the bar naming both. Good: each option is marked " +
                    "and says what touching it does, the question line names them, nothing crowds the tags. Touch one to answer (the marks go). Next choice stages " +
                    "the camp's other touch choices.",
                    SetupTouchChoice, ("Next choice", NextChoiceExtra)) };
                case "#703": return new[] { With(S("#703", "the duck jar in winter", "pt1b_duck_jar",
                    "The pond is ice and the Kit is open on Life. Good: the Ducks jar is shut and greyed with \"The pond is ice. Ducks will come when it thaws.\"; " +
                    "press Thaw: the jar opens. Freeze shuts it again.",
                    SetupDuckJar, ("Thaw", DuckThawExtra), ("Freeze", DuckFreezeExtra)), box: "camp") };
                case "#669": return new[] { S("#669", "History before the first house", "pt1b_history_first",
                    "The played box (fish on the rack, props, berries, rabbits) with History open. Good: press Timeline start: scrub back, nothing placed or built later " +
                    "stands there (no life, rack, woodpile, props); Timeline end: today's box is back, with everything. Early years is a point near the start.",
                    SetupHistoryFirst, ("Timeline start", () => HistorySeekExtra(0f)), ("Early years", () => HistorySeekExtra(0.15f)), ("Timeline end", () => HistorySeekExtra(1f))) };
                case "#700": return new[] { S("#700", "a path over the rack: it stays by day, moves off at night", "pt1b_rack_path",
                    "A path laid over the fish rack, fish hanging, at midday, close on the rack. Good: by day the rack stays. Night plays the evening: the rack is taken down and " +
                    "set up off the path. Hide rack lays the path over the hide frame instead.",
                    SetupRackPath, ("Night", RackNightExtra), ("Hide rack", RackHidePathExtra)) };
                case "#700+": return new[] {
                    S("#700", "spec: the rack's take-down and set-up (B27)", "pt1b_spec_b27", SpecText("B27") + "\n\nThe next stop plays the beat in the box.",
                        () => PlainSetup("the spec above is B27 in design/animation-specs.md; the next stop plays it", null, false, false, true)),
                    S("#700", "the rack taken down and set up again (live)", "pt1b_rack_beat",
                        "A path covers the fish rack; two people take it down, carry it and set it up on free ground, the fish back on one by one (about 20 s). Watch it; " +
                        "Set up again plays it twice. Good: it reads \"they took the rack down and put it up somewhere else\", the carry is in step, the old spot is left bare.",
                        SetupRackBeat, ("Hide rack", RackBeatHideExtra)) };
                case "#705": return new[] {
                    S("#705", "spec: hanging out the washing (B28)", "pt1b_spec_b28", SpecText("B28") + "\n\nThe next stop plays the beat in the box.",
                        () => PlainSetup("the spec above is B28 in design/animation-specs.md; the next stop plays it", null, false, false, true)),
                    S("#705", "hanging out the washing (live)", "pt1b_laundry",
                        "The home nearest the middle of the screen hangs its washing now (the cheat; the real chore comes in the dawn window): a cord between two stakes, wet cloths " +
                        "lifted from a basket, shaken, pinned, smoothed. The cloths stay about 16 s, then are taken in. Set up again to watch twice; Take in the washing ends the stay.",
                        SetupLaundry, ("Take in the washing", LaundryTakeInExtra)) };
                case "#690": return new[] { With(S("#690", "the deer jar's need line", "pt1b_deer_jar",
                    "A fresh camp (its own box) with the bushes and rabbits placed, a single wild tree and the Kit open on Life. Good: the Deer jar is shut with " +
                    "\"Deer need a thicker wood (n/12)\" while the trees stand scattered; press A wood stands: the line goes and the jar opens.",
                    SetupDeerJar, ("A wood stands", DeerWoodExtra)), box: "camp") };
            }
            return new Stop[0];
        }

        // ---------------------------------------------------------------- helpers
        /// <summary>The pond's cells as sim points; empty when there is no pond.</summary>
        static System.Collections.Generic.List<Vector2> PondCells()
        {
            var l = new System.Collections.Generic.List<Vector2>();
            for (int c = 0; c < EcoLand.N; c++)
                if (Sim.EcoCoverAt(c) == EcoCover.Water) l.Add(new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)));
            return l;
        }

        static Vector2 PondMiddle(System.Collections.Generic.List<Vector2> cells) => new Vector2(cells.Average(p => p.x), cells.Average(p => p.y));

        /// <summary>A pond cell nearest a pane (the water edge that reaches the glass).</summary>
        static Vector2 PondAtGlass(System.Collections.Generic.List<Vector2> cells) =>
            cells.OrderBy(p => Mathf.Min(Land.HX - Mathf.Abs(p.x), Land.HZ - Mathf.Abs(p.y - Land.CZ))).First();

        /// <summary>The pond's water edge at a pane, moved a little inward (off the glass) so a steeper pitch sees the water surface and the dug rim beside it, not the dirt wall below.</summary>
        static Vector2 PondEdgeInside(System.Collections.Generic.List<Vector2> cells, float inward)
        {
            var p = PondAtGlass(cells);
            if (Land.HX - Mathf.Abs(p.x) <= Land.HZ - Mathf.Abs(p.y - Land.CZ)) p.x -= Mathf.Sign(p.x) * inward; else p.y -= Mathf.Sign(p.y - Land.CZ) * inward;
            return p;
        }

        const float RimZoom = 0.2f, RimPitch = 52f, RimInward = 0.3f;

        /// <summary>The close frame of the rim stops: closer than a player can pinch, so the zoom floor is lowered until the next stop.</summary>
        void FrameRim(Vector2 at, float zoom, float pitch, float yaw = float.NaN)
        {
            zoomFloorChanged = true; Rig.minZoom = 0.02f;
            Frame(World(at.x, at.y), zoom, pitch, yaw, shift: false);
        }

        /// <summary>The orbit angle (of the eight 45 degree ones) whose view runs along <paramref name="dir"/> (world, horizontal).</summary>
        static float YawLooking(Vector3 dir)
        {
            float keep = Rig.yaw, best = keep, bd = -2f;
            dir.y = 0f; dir.Normalize();
            for (int k = 0; k < 8; k++)
            {
                Rig.yaw = k * 45f; Rig.Apply();
                var f = Camera.main.transform.forward; f.y = 0f; f.Normalize();
                float d = Vector3.Dot(f, dir);
                if (d > bd) { bd = d; best = Rig.yaw; }
            }
            Rig.yaw = keep; Rig.Apply();
            return best;
        }

        /// <summary>Frames the pond's edge at its nearest pane from outside the glass, square on to the pane, from above.</summary>
        void FramePondRim(System.Collections.Generic.List<Vector2> cells)
        {
            var p = PondAtGlass(cells);
            bool onX = Land.HX - Mathf.Abs(p.x) <= Land.HZ - Mathf.Abs(p.y - Land.CZ);
            var inward = box.transform.TransformDirection(onX ? new Vector3(-Mathf.Sign(p.x), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(p.y - Land.CZ)));
            FrameRim(PondEdgeInside(cells, RimInward), RimZoom, RimPitch, YawLooking(inward));
        }

        void LeaveTouchChoice()
        {
            ChoiceSceneView.DebugLeave();
            ChoiceSceneView.DebugWatch = false;
            if (Sim != null && Sim.OpenDilemma != null) Sim.DebugLapseDilemma();
        }

        /// <summary>Called from <see cref="CloseLoosePt1"/>: what batch 2's stops leave behind (an ice cheat, a choice on stage).</summary>
        void CloseLoosePt1b()
        {
            PondIce.Force = null;
            SeasonLook.HoldPhase = null;
            if (dollsPinned && pv != null) { pv.ClearPins(); dollsPinned = false; }
            var cv = ChoiceSceneView.Instance;
            if (cv != null && (cv.State != ChoiceSceneView.Phase.Idle || ChoiceSceneView.DebugWatch)) LeaveTouchChoice();
            ChoiceSceneView.DebugWatch = false;
        }

        // ---------------------------------------------------------------- #707 the HUD top
        IEnumerator SetupHudTop()
        {
            Hour(12f, true);
            DefaultView(0f);
            yield return Frames(10);
            panel.collapsed = true;
            Say("midday over the sand: the top row has Settings, Feedback and Photo (the folded panel; Feedback shows in test builds only)");
            yield return Wait(1f);
        }

        IEnumerator HudNightExtra()
        {
            Hour(22.5f, true);
            DefaultView(0f);
            yield return Wait(2.5f);
            panel.collapsed = true;
            Say("night: the icons over the dark ground and sky");
        }

        IEnumerator HudWinterExtra()
        {
            Hour(12f, true);
            for (int k = 0; k < 4 && LifeClock.SeasonOf(Sim.State.month) != Season.Winter; k++) Log(EcoDebug.SkipToNextSeason());
            DefaultView(0f);
            yield return Wait(2f);
            panel.collapsed = true;
            Say("winter: the icons over the snow");
        }

        IEnumerator HudPondExtra()
        {
            yield return EnsurePond();
            Hour(12f, true);
            var cells = PondCells();
            if (cells.Count == 0) { Say("no pond to put under the icons"); yield break; }
            // the pond high on the screen, under the top row: the camera looks short of it
            var m = PondMiddle(cells);
            var mid = World(m.x, m.y);
            Frame(mid, 0.5f, 42f, shift: false);
            var f = Camera.main.transform.forward; f.y = 0f;
            Frame(mid - f.normalized * (ShiftPerZoom * 0.5f * 1.4f), 0.5f, 42f, shift: false);
            yield return Held(1.2f);
            panel.collapsed = true;
            Say($"the pond ({cells.Count} cells) under the top row");
        }

        // ---------------------------------------------------------------- #704 the fire on dry ground and snow
        /// <summary>The hour of the fire stop: the camp is out working (at dusk two people stand in front of the fire and hide its base).</summary>
        const float FireHour = 11f, SnowHour = 15.5f;

        IEnumerator SetupFireGround()
        {
            yield return FreshCamp();
            Sim.DebugFireStage(2);
            box.Rebuild();
            SeasonLook.HoldPhase = null;
            Hour(FireHour, true);
            yield return Wait(2f);
            yield return FrameFireGround();
            Say("dry camp ground, fire stage " + Sim.FireStage + " · zoom " + Rig.zoom.ToString("0.00") + " · fire point " + pv.Planner.FirePoint(out _, out _));
        }

        /// <summary>Everyone stood away from the fire (pinned on a ring 2.6 u out), so nobody hides its base. Cleared by <see cref="CloseLoosePt1b"/>.</summary>
        void ClearFireOfDolls()
        {
            if (pv == null || pv.Planner == null || !pv.Planner.FirePoint(out float fx, out float fz)) return;
            pv.ClearPins();
            int k = 0;
            foreach (var a in pv.Activities)
            {
                float ang = 1.1f * k++ + 0.7f;
                pv.Pin(a.who, new Vector2(fx + 2.6f * Mathf.Cos(ang), fz + 2.6f * Mathf.Sin(ang)));
            }
            dollsPinned = true;
        }

        bool dollsPinned;

        string DollsNearFire()
        {
            if (!pv.Planner.FirePoint(out float fx, out float fz)) return "?";
            var sb = new System.Text.StringBuilder();
            foreach (var act in pv.Activities)
                if (pv.TryGetDollPosition(act.who, out var q) && (q - new Vector2(fx, fz)).magnitude < 0.6f) sb.Append(act.kind + " ");
            return sb.Length == 0 ? "nobody" : sb.ToString();
        }

        float fireZoom = 0.07f;

        IEnumerator FrameFireGround()
        {
            if (pv.Planner != null && pv.Planner.FirePoint(out float fx, out float fz)) { zoomFloorChanged = true; Rig.minZoom = 0.02f; Frame(World(fx, fz), fireZoom, 38f, shift: false); }
            else Frame(CampCentre(), 0.18f, 42f);
            yield return Held(0.5f);
            ClearFireOfDolls();
            yield return Held(4f);
        }

        IEnumerator FireGroundExtra(int stage)
        {
            SeasonLook.HoldPhase = null;
            Sim.DebugFireStage(stage);
            box.Rebuild();
            Hour(FireHour, true);
            yield return Wait(1.2f);
            yield return FrameFireGround();
            Say("fire held at: " + Sim.FireStage);
        }

        IEnumerator FireSnowExtra()
        {
            for (int k = 0; k < 4 && LifeClock.SeasonOf(Sim.State.month) != Season.Winter; k++) Log(EcoDebug.SkipToNextSeason());
            if (UnityEngine.Object.FindAnyObjectByType<SeasonDriver>() == null) box.gameObject.AddComponent<SeasonDriver>(); // a box opened by a stop has none (the driver is added at scene load)
            SeasonLook.HoldPhase = 0.5f; // the middle of winter: full snow cover now (the box's own look can lag, or hold the spring of a first run)
            Hour(SnowHour, true);
            box.Rebuild();
            yield return Wait(3f);
            fireZoom = 0.2f; // wider: the snow lies on the open ground round the fire
            yield return FrameFireGround();
            fireZoom = 0.07f;
            Say("winter: " + SeasonLook.Word(Sim.State.month) + " · fire " + Sim.FireStage + " · snow " + SeasonLook.Snow.ToString("0.00") + " · weights " + SeasonLook.Weights + " · near the fire: " + DollsNearFire());
        }

        // ---------------------------------------------------------------- #701 the pond's rim at the glass
        IEnumerator SetupPondRim()
        {
            Hour(12f, true);
            yield return EnsurePond();
            PondIce.Force = false;
            yield return Wait(1.5f);
            var cells = PondCells();
            if (cells.Count == 0) { Say("setup failed: no pond"); DefaultView(); yield break; }
            FramePondRim(cells);
            yield return Held(1f);
            Say($"{cells.Count} pond cells; the edge at the nearest pane, zoom 0.2, pitch 52 (the zoom floor is lowered for this stop only)");
        }

        IEnumerator PondWholeExtra()
        {
            var cells = PondCells();
            if (cells.Count == 0) { Say("no pond"); yield break; }
            FrameRim(PondMiddle(cells), 0.32f, 50f);
            yield return Held(1f);
            Say("the whole pond, " + (PondIce.Force == true ? "under ice" : "open water"));
        }

        IEnumerator PondRimIceExtra(bool ice)
        {
            PondIce.Force = ice;
            yield return Wait(2f);
            var cells = PondCells();
            if (cells.Count == 0) { Say("no pond"); yield break; }
            FramePondRim(cells);
            yield return Held(1f);
            Say(ice ? "under ice: the ice and the water edge" : "thawed");
        }

        // ---------------------------------------------------------------- #676 the stew by kind
        IEnumerator SetupStewKind()
        {
            yield return SetupStew(); // the pot at the cook's hour, framed close
            Sim.DebugStew(StewKind.Fish);
            yield return Wait(1.5f);
            Say("stew kind: Fish · at " + pv.Now.hour.ToString("0.0") + " h");
        }

        IEnumerator StewKindExtra(StewKind kind)
        {
            Sim.DebugStew(kind);
            yield return Wait(1.5f);
            yield return Held(0.5f);
            Say("stew kind: " + kind + " · at " + pv.Now.hour.ToString("0.0") + " h");
        }

        IEnumerator StewMiddayExtra()
        {
            Hour(12.5f, true);
            yield return Wait(3f);
            yield return Held(0.5f);
            Say("midday: " + pv.Now.hour.ToString("0.0") + " h");
        }

        // ---------------------------------------------------------------- #706 the touch choice
        static readonly string[] TouchChoices = { "camp_travellers", "camp_fire", "camp_remember", "camp_winter" };
        int touchChoice;

        IEnumerator StageTouchChoice(string id)
        {
            var v = ChoiceSceneView.Instance;
            if (v == null) { Say("setup failed: no ChoiceSceneView"); yield break; }
            LeaveTouchChoice();
            yield return Frames(4);
            Hour(10f);
            if (id == "camp_travellers") { Sim.DebugLand = "meadow"; StateCheat("the question's land forced to the meadow", id); }
            string said = DilemmaCheats.Raise(id);
            Sim.DebugLand = "";
            var d = Sim.OpenDilemma;
            if (d == null || d.defId != id) { Say("setup failed: " + said); yield break; }
            var m = CardStackView.Instance != null ? CardStackView.Instance.Model : null;
            if (m != null && m.Mode == CardStackMode.Open) m.Close();
            yield return Frames(6);
            string staged = ChoiceSceneView.DebugStage();
            yield return Until(() => v.State == ChoiceSceneView.Phase.Waiting, 45f);
            yield return Wait(2f);
            // both tags and the bar in view
            panel.collapsed = true; // the sign-off panel would cover the upper tag: folded for this stop (tap the arrow to read the text)
            Say(id + " · " + v.AskLine + (v.State == ChoiceSceneView.Phase.Waiting ? "" : " · NOT WAITING: " + staged));
        }

        IEnumerator SetupTouchChoice()
        {
            touchChoice = 0;
            yield return StageTouchChoice(TouchChoices[0]);
        }

        IEnumerator NextChoiceExtra()
        {
            touchChoice = (touchChoice + 1) % TouchChoices.Length;
            yield return StageTouchChoice(TouchChoices[touchChoice]);
        }

        // ---------------------------------------------------------------- #703 the duck jar in winter
        IEnumerator SetupDuckJar()
        {
            yield return FreshCamp(); // a box where no ducks are placed yet (a placed flock stays, so its jar would read "Placed")
            Hour(10f);
            yield return EnsurePond();
            Sim.DebugJarsReady();
            PondIce.Force = true; // the cheat's own pair: every jar open, then the ice
            DefaultView();
            yield return Frames(10);
            ShowDuckJar();
            yield return Wait(1.2f);
            ShowDuckJar();
            yield return Wait(0.5f);
            Say(DuckJarLine());
        }

        /// <summary>The Kit on its Life chooser with the Ducks jar picked, so its need line shows.</summary>
        void ShowDuckJar()
        {
            OpenLifeRow();
            var f = typeof(GameHud).GetField("lockedPick", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(hud, (LifeSpecies?)LifeSpecies.Ducks);
        }

        string DuckJarLine() => "ducks jar: " + Sim.JarStateOf(LifeSpecies.Ducks) + " · " + (Sim.JarNeed(LifeSpecies.Ducks) ?? "(no line)");

        IEnumerator DuckThawExtra()
        {
            PondIce.Force = false;
            yield return Wait(1.5f);
            ShowDuckJar();
            yield return Wait(0.8f);
            Say("thaw · " + DuckJarLine());
        }

        IEnumerator DuckFreezeExtra()
        {
            PondIce.Force = true;
            yield return Wait(1.5f);
            ShowDuckJar();
            yield return Wait(0.8f);
            Say("ice · " + DuckJarLine());
        }

        // ---------------------------------------------------------------- #669 History before the first house
        IEnumerator SetupHistoryFirst()
        {
            Hour(10f);
            Log("camp props: " + ConsequenceCheats.ShowCampProps());
            Log("fish on the rack: " + FishRackView.DebugRack());
            if (Sim.EcoRabbitsShown() < 3) Log(EcoDebug.AddRabbits(4));
            if (!Sim.BerriesGrow) Log(EcoDebug.AddBushes(3));
            yield return Wait(1.5f);
            Frame(CampCentre(), 0.6f, 45f);
            yield return Held(1.5f);
            yield return OpenHistory();
        }

        IEnumerator HistorySeekExtra(float at)
        {
            var tl = TimeLapseView.Find();
            if (tl == null || !tl.Active) { Say("History is not open: Set up again"); yield break; }
            tl.Seek(tl.Clock.Start + (tl.Clock.End - tl.Clock.Start) * at);
            yield return Held(1.2f);
            Say((at <= 0f ? "timeline start: " : at >= 1f ? "timeline end: " : "early years: ") + tl.LastInfo);
        }

        // ---------------------------------------------------------------- #700 a path over the rack
        IEnumerator SetupRackPath()
        {
            Hour(10f);
            Log("camp props: " + ConsequenceCheats.ShowCampProps());
            yield return Wait(1f);
            Log("fish on the rack: " + FishRackView.DebugRack()); // a rack that holds fish stays by day
            Log(SimDebugPanel.DoPathOverRack(false));
            yield return Wait(1.5f);
            Hour(10f, true);
            yield return FrameRackAndFire(0.3f);
            Say("midday: a path over the fish rack, fish hanging; it moves at night");
        }

        IEnumerator FrameRackAndFire(float zoom)
        {
            if (FireAndRack(out var fire, out var rack)) Frame((fire + rack) * 0.5f, zoom, 45f);
            else Frame(CampCentre(), 0.3f, 45f);
            yield return Held(0.8f);
        }

        /// <summary>Close on the fish rack (about the zoom of a person card): the take-down and the carrying read.</summary>
        IEnumerator FrameRackClose()
        {
            if (FireAndRack(out _, out var rack)) Frame(rack, 0.14f, 45f);
            else Frame(CampCentre(), 0.2f, 45f);
            yield return Held(0.8f);
        }

        IEnumerator RackNightExtra()
        {
            Hour(18.5f, true);
            yield return Wait(8f);
            yield return FrameRackAndFire(0.3f);
            Hour(22.5f, true);
            yield return Wait(6f);
            Say("night: the rack stands where it moved to (hold on the fire and the rack)");
        }

        IEnumerator RackHidePathExtra()
        {
            Hour(10f, true);
            Log(SimDebugPanel.DoPathOverRack(true));
            yield return Wait(1.5f);
            yield return FrameRackAndFire(0.3f);
            Say("a path over the hide frame; press Night");
        }

        // ---------------------------------------------------------------- #700 (B27) the beat live
        IEnumerator SetupRackBeat()
        {
            Hour(10f, true);
            Log("camp props: " + ConsequenceCheats.ShowCampProps());
            yield return Wait(1f);
            Log("fish on the rack: " + FishRackView.DebugRack());
            Log(SimDebugPanel.DoPathOverRack(false));
            yield return Wait(1f);
            yield return FrameRackClose();
            Say(SimDebugPanel.DoRackMoveNow());
        }

        IEnumerator RackBeatHideExtra()
        {
            Hour(10f, true);
            Log(SimDebugPanel.DoPathOverRack(true));
            yield return Wait(1f);
            yield return FrameRackClose();
            Say(SimDebugPanel.DoRackMoveNow());
        }

        // ---------------------------------------------------------------- #705 (B28) the washing live
        IEnumerator SetupLaundry()
        {
            Hour(9f, true);
            Log("camp props: " + ConsequenceCheats.ShowCampProps());
            yield return Wait(1f);
            Frame(CampCentre(), 0.3f, 45f);
            yield return Held(0.8f);
            Say(LaundryCheats.LaundryNow());
            yield return Wait(0.3f);
            // close on the home's washing line, about the zoom of a person card
            var lines = pv.Planner.LaundryOn(pv.Now.day);
            if (lines != null && lines.Count > 0 && pv.TryGetLineAnchor(lines[0].home, out var an)) Frame(World(an.centre.x, an.centre.y), 0.1f, 45f);
            yield return Held(1f);
        }

        IEnumerator LaundryTakeInExtra()
        {
            Say(LaundryCheats.TakeInNow());
            yield return Held(1f);
        }

        // ---------------------------------------------------------------- #690 the deer jar's need line
        IEnumerator SetupDeerJar()
        {
            yield return FreshCamp();
            Hour(10f);
            foreach (var s in new[] { LifeSpecies.Thicket, LifeSpecies.Rabbits })
                if (!Sim.LifeIsPlaced(s)) { if (!Sim.JarReady(s)) Sim.DebugJarsReady(); Log($"{s} placed near the camp ({Sim.DebugPlaceNearCamp(s)})"); }
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 25f);
            Log("jars shut again: " + Sim.DebugLockJars());
            Log(EcoDebug.WildTrees(false)); // one wild tree: scattered, not a wood
            DefaultView();
            yield return Frames(10);
            OpenLifeRow();
            yield return Wait(1f);
            Say(DeerJarLine());
        }

        string DeerJarLine() => "deer jar: " + Sim.JarStateOf(LifeSpecies.Deer) + " · " + (Sim.JarNeed(LifeSpecies.Deer) ?? "(no line)");

        IEnumerator DeerWoodExtra()
        {
            Log("wood: " + Sim.DebugGrowFarWood());
            yield return Wait(2.5f);
            OpenLifeRow();
            var trees = new System.Collections.Generic.List<Vector2>();
            for (int c = 0; c < EcoLand.N; c++)
                if (Sim.EcoCoverAt(c) == EcoCover.Tree) trees.Add(new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)));
            if (trees.Count > 0)
            {
                var m = PondMiddle(trees); // the wood, between the sign-off panel and the kit sheet
                Frame(World(m.x, m.y), 0.45f, 50f);
                yield return Held(1f);
            }
            Say(DeerJarLine() + " (the jar opens at the next sim tick if it still reads shut)");
        }
    }
}
