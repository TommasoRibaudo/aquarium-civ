using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #423 the life slot (placed-life spec §6): a ready jar wiggles the Kit once and leaves a dot; the slot in the kit takes the
    /// species in hand, its ground glows; the first tap on the glow shows the ghost, a tap on the ghost places it (sim: Placed);
    /// a tap on dark ground does nothing; two ready jars open the chooser row. All by touch, on a portrait phone. Shots go to
    /// Temp/agent/p9-slot-*.png.
    /// </summary>
    [Category("LifeSlot")]
    public class LifeSlotPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        /// <summary>A living box with a meadow, after the first run, at 10 h, with nothing on screen.</summary>
        IEnumerator LivingMeadow(int w = 1179, int h = 2556, bool mist = true, bool tree = true)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            if (mist) foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            // 2026-11-14: seed, then bushes: the thicket's jar opens only once the Keeper's tree is in the ground
            if (mist && tree && Sim.CanPlantTree) Assert.IsTrue(Sim.PlantTree(Sim.EcoGround.campX + 0.9f, Sim.EcoGround.campZ), "the seed goes in by the camp");
            yield return Frames(10);
        }

        /// <summary>A screen point on the land whose cell the species may not have (dark ground), clear of the HUD.</summary>
        static Vector2 DarkSpot(LifeSpecies s, out int cell)
        {
            var lpv = LifePlaceView.Instance; var mist = EcoMistView.Instance;
            for (float fy = 0.3f; fy < 0.75f; fy += 0.05f)
                for (float fx = 0.2f; fx < 0.8f; fx += 0.05f)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (UiInput.Over(p) || !mist.GroundPoint(p, out var at)) continue;
                    cell = AquariumCiv.Sim.Settle.SiteMap.CellAt(at.x, at.z);
                    if (cell >= 0 && !Sim.LifeGroundOk(s, cell)) return p;
                }
            cell = -1;
            return Vector2.zero;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ReadyJar_Wiggle_Choose_Glow_Ghost_Place()
        {
            yield return LivingMeadow();
            var hud = GameHud.Instance;
            Assert.IsFalse(hud.LifeSlotShown, "no ready jar: no life slot");
            Assert.IsFalse(hud.LifeDotShown, "and no dot");
            // the land earns the thicket's jar as time passes
            int wiggles0 = hud.LifeWiggles;
            for (int i = 0; i < 16 && !Sim.JarReady(LifeSpecies.Thicket); i++) EcoDebug.SkipSeasons(1);
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "the thicket's jar opened on a meadow");
            Assert.IsFalse(Sim.JarReady(LifeSpecies.Rabbits), "the rabbits wait for the thicket (the ladder)");
            yield return Until(() => hud.LifeWiggles > wiggles0, 3f);
            Assert.AreEqual(wiggles0 + 1, hud.LifeWiggles, "the Kit wiggles once when the jar opens");
            Assert.IsFalse(Sim.JarWiggleDue(LifeSpecies.Thicket), "and the wiggle is acknowledged");
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Shot("p9-slot-1-wiggle");
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(hud.LifeDotShown, "a small dot stays on the Kit");
            yield return Shot("p9-slot-2-dot");
            yield return Frames(30);
            Assert.AreEqual(wiggles0 + 1, hud.LifeWiggles, "only once");

            // Kit: the life slot is there, with its dot; one ready jar: a tap takes it in hand
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens the kit");
            Assert.IsTrue(hud.LifeSlotShown, "the life slot shows");
            yield return Shot("p9-slot-3-kit");
            // #668 (mock-up A): the jar's contents are tiles in the kit's Ready shelf, beside the shut ones: no jar to open first
            Assert.IsTrue(hud.LifeRowOpen, "the Ready and Still to come shelves show");
            Assert.AreEqual("kit-ready", hud.Root.Q("kit-life-thicket")?.parent?.name, "the thicket's tile sits in Ready");
            Assert.AreEqual(GameHud.ReadyLine(LifeSpecies.Thicket), hud.LifeNoteText.Split('\n')[0], "the note says what to do with it");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-thicket"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "the thicket in hand");
            Assert.AreEqual(LifeSpecies.Thicket, hud.HeldLife);
            Assert.IsNull(hud.OpenSheet, "the kit closes with the tool in hand");
            Assert.IsFalse(hud.LifeDotShown, "no dot on the Kit button while the jar is in hand");
            // #516 (playtest #510): the button says what it does, it doesn't read as "confirm"
            Assert.AreEqual(GameHud.PutAway, hud.Root.Q<Button>("hud-clean").text, "the Kit button puts the jar away");
            Assert.AreEqual("The bushes · " + GameHud.PutAway, hud.KitWord, "#661: the bar shows what is in hand");
            Assert.GreaterOrEqual(hud.Root.Q("hud-clean").worldBound.height / hud.Root.worldBound.height * 852f, 44f, "44 pt or more");

            // the glow
            var lpv = LifePlaceView.Instance;
            Assert.IsNotNull(lpv, "the life tool's view");
            yield return Frames(5);
            var ground = new List<int>(); Sim.LifeGround(LifeSpecies.Thicket, ground);
            Assert.AreEqual(ground.Count, lpv.GlowCells, "the valid ground glows");
            Assert.Greater(lpv.GlowCells, 0);
            yield return new WaitForSecondsRealtime(1.5f); // the hint toast
            yield return Shot("p9-slot-4-glow");

            // a tap on dark ground: nothing but the soft no
            var dark = DarkSpot(LifeSpecies.Thicket, out int darkCell);
            Assert.GreaterOrEqual(darkCell, 0, "dark ground on screen");
            int ref0 = lpv.Refusals;
            yield return TapAt(dark);
            yield return Frames(5);
            Assert.AreEqual(ref0 + 1, lpv.Refusals, "the soft no: " + lpv.Last);
            Assert.AreEqual(Sim.ThicketRefusal(darkCell), lpv.LastRefusal, "#664: the no says why");
            Assert.AreEqual(lpv.LastRefusal + ".", GameUiPanel.Toasts.Current, "#664: and the toast shows it");
            Assert.IsFalse(lpv.GhostShown, "no ghost off the glow");
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "nothing placed");
            Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "still in hand");

            // the first tap on the glow: the ghost
            int cell = GlowingCellOnScreen(Sim, LifeSpecies.Thicket);
            Assert.GreaterOrEqual(cell, 0, "a glowing spot on screen");
            var p = lpv.ScreenOf(cell);
            yield return TapAt(p);
            yield return Frames(5);
            Assert.IsTrue(lpv.GhostShown, "the ghost: " + lpv.Last);
            Assert.AreEqual(cell, lpv.GhostCell);
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "a ghost places nothing");
            // tester 2026-11-08: the "tap again" hint shows while the ghost waits
            yield return Until(() => GameUiPanel.Toasts.Current == LifePlaceView.TapAgainHintFor(LifeSpecies.Thicket), 3f);
            Assert.AreEqual(LifePlaceView.TapAgainHintFor(LifeSpecies.Thicket), GameUiPanel.Toasts.Current, $"the tap-again hint is up (held by: {GameUiPanel.Toasts.Holders})");
            Assert.IsTrue(Shown(hud.Root.Q("hud-toast")), "and on screen");
            yield return new WaitForSecondsRealtime(0.6f);
            var bc = Camera.main.GetComponent<BoxCamera>();
            yield return Until(() => !bc.IsFlying, 8f); // the camera leans in on the ghost (user 2026-11-08): screen spots move
            yield return new WaitForSecondsRealtime(3.5f); // past the old 3 s toast
            Assert.AreEqual(LifePlaceView.TapAgainHintFor(LifeSpecies.Thicket), GameUiPanel.Toasts.Current, "the hint stays while the ghost waits, after the lean");
            yield return Shot("p9-slot-5-ghost");
            // a dark tap with the ghost up: the ghost stays where it was
            dark = DarkSpot(LifeSpecies.Thicket, out darkCell);
            Assert.GreaterOrEqual(darkCell, 0, "dark ground on screen after the lean");
            yield return TapAt(dark);
            yield return Frames(3);
            Assert.AreEqual(cell, lpv.GhostCell, "a tap off the glow leaves the ghost");
            Assert.AreEqual(ref0 + 2, lpv.Refusals);

            // the tap on the ghost places it
            int placed0 = lpv.Placings;
            p = lpv.ScreenOf(lpv.GhostCell);
            yield return TapAt(p);
            yield return Frames(10);
            Assert.AreEqual(JarState.Placed, Sim.JarStateOf(LifeSpecies.Thicket), "placed: " + lpv.Last);
            Assert.AreEqual(cell, Sim.LifePlacedCell(LifeSpecies.Thicket), "where the ghost stood");
            Assert.AreEqual(placed0 + 1, lpv.Placings);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "the tool is put down");
            Assert.IsFalse(lpv.GhostShown, "the ghost goes");
            Assert.AreNotEqual(LifePlaceView.TapAgainHintFor(LifeSpecies.Thicket), GameUiPanel.Toasts.Current, "and its hint with it");
            yield return Frames(5);
            Assert.AreEqual(0, lpv.GlowCells, "and the glow");
            var bushes = new List<int>(); Sim.BushCells(bushes);
            Assert.Greater(bushes.Count, 0, "the thicket's founders stand");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("p9-slot-6-placed");
            if (!Sim.AnyJarReady) Assert.IsFalse(hud.LifeDotShown, "no jar ready: the dot goes");
            AssertNoErrors("life slot");
        }

        /// <summary>#664: with the bushes in hand, a tap on green ground the bushes can't use says why, in a toast: by the fire, in the wet,
        /// on ground that isn't grass. Shots of each.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator RefusedBushTap_SaysWhy()
        {
            yield return LivingMeadow();
            var hud = GameHud.Instance;
            for (int i = 0; i < 16 && !Sim.JarReady(LifeSpecies.Thicket); i++) EcoDebug.SkipSeasons(1);
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "the thicket's jar opened");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-thicket"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(LifeSpecies.Thicket, hud.HeldLife, "the bushes in hand");
            yield return Until(() => GameUiPanel.Toasts.Current == GameHud.LifeHintFor(LifeSpecies.Thicket), 8f); // after the jar's own toast
            Assert.AreEqual(GameHud.LifeHintFor(LifeSpecies.Thicket), GameUiPanel.Toasts.Current, "#664: the first hint says where bushes go");
            StringAssert.Contains("open grass away from the fire", GameUiPanel.Toasts.Current);
            yield return Shot("664-0-hint");
            var lpv = LifePlaceView.Instance;
            // one tap per reason, on a cell on screen that the rule refuses for that reason
            var seen = new HashSet<ThicketRefusalReason>();
            foreach (var want in new[] { ThicketRefusalReason.NearFire, ThicketRefusalReason.Wet, ThicketRefusalReason.NotGrass, ThicketRefusalReason.NoRoom })
            {
                int cell = -1; Vector2 at = default;
                for (int c = 0; c < AquariumCiv.Sim.Eco.EcoLand.N && cell < 0; c++)
                {
                    if (Sim.WhyNotThicket(c) != want) continue;
                    var p = lpv.ScreenOf(c);
                    if (p.x < Screen.width * 0.15f || p.x > Screen.width * 0.85f || p.y < Screen.height * 0.25f || p.y > Screen.height * 0.75f || UiInput.Over(p)) continue;
                    if (!EcoMistView.Instance.GroundPoint(p, out var g) || AquariumCiv.Sim.Settle.SiteMap.CellAt(g.x, g.z) != c) continue;
                    cell = c; at = p;
                }
                if (cell < 0) { Debug.Log($"[664] no {want} cell on screen"); continue; }
                int r0 = lpv.Refusals;
                yield return TapAt(at);
                yield return Frames(5);
                Assert.AreEqual(r0 + 1, lpv.Refusals, $"{want}: refused ({lpv.Last})");
                Assert.AreEqual(Sim.ThicketRefusal(cell), lpv.LastRefusal, $"{want}: the reason");
                Assert.AreEqual(lpv.LastRefusal + ".", GameUiPanel.Toasts.Current, $"{want}: the toast");
                Debug.Log($"[664] {want}: \"{GameUiPanel.Toasts.Current}\"");
                seen.Add(want);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot($"664-{seen.Count}-{want}");
            }
            Assert.IsTrue(seen.Contains(ThicketRefusalReason.NearFire) && seen.Contains(ThicketRefusalReason.Wet), "the fire and the wet are on screen in this box: " + string.Join(", ", seen));
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TwoReadyJars_OpenTheChooserRow()
        {
            yield return LivingMeadow();
            var hud = GameHud.Instance;
            Sim.DebugJarsReady(); // every jar open (the cheat): several are ready at once
            var ready = new List<LifeSpecies>(); Sim.ReadyJars(ready);
            Assert.GreaterOrEqual(ready.Count, 2, "two or more ready jars");
            yield return Frames(10);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet);
            Assert.IsTrue(hud.LifeRowOpen, "two or more ready jars: the Ready shelf shows them");
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "nothing in hand yet");
            foreach (var s in ready) Assert.IsNotNull(hud.Root.Q("kit-life-" + s.ToString().ToLowerInvariant()), "an icon for " + s);
            yield return Shot("p9-slot-7-row");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-rabbits"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "a tap on an icon takes it in hand");
            Assert.AreEqual(LifeSpecies.Rabbits, hud.HeldLife);
            yield return Frames(5);
            Assert.Greater(LifePlaceView.Instance.GlowCells, 0, "the rabbits' ground glows");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("p9-slot-8-rabbits-glow");
            // Put away puts it back
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return Frames(10);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "Put away puts it back");
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Rabbits), "still ready");
            Assert.AreEqual(0, LifePlaceView.Instance.GlowCells, "the glow goes");
            // and the whole real path places the pair
            yield return KeeperPlaces(LifeSpecies.Rabbits);
            Assert.IsTrue(LastPlaced, "the rabbits placed by touch");
            Assert.Greater(Sim.EcoRabbitCount, 0, "a pair in the box");
            AssertNoErrors("life row");
        }

        /// <summary>
        /// #570 what is missing, by touch on a portrait phone. A fresh living box: no jar is ready yet, but the Life slot is in the kit and its
        /// chooser lists every shut jar greyed, in the people's words; a tap on a shut jar says what it waits for (never nothing); the village
        /// line shows under it. Then the thicket opens: the ready jar comes first, the shut ones after. Shots go to Temp/agent/p570-*.png:
        ///  1 (kit): the Kit sheet with the Life slot (no waits-for list before the camp: 2026-11-14);
        ///  2 (chooser): six greyed icons (bushes, rabbits, deer, ducks, fish, wolves), the first one a little stronger, its need and the line below;
        ///  3 (picked): the deer's icon picked, "Deer need the rabbits first" in the note;
        ///  4 (ready): the thicket's icon bright and first, the five others greyed.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator ShutJars_ShowGreyedWithTheirNeed_ATapSaysIt()
        {
            yield return LivingMeadow(mist: false); // bare land: nothing is ready, every need is real
            var hud = GameHud.Instance;
            Sim.DebugLockJars(); // (the cheat; nothing to shut on a bare box, but it keeps the test honest)
            yield return Frames(10);
            var shut = new List<LifeSpecies>(); Sim.LockedJars(shut);
            Assert.AreEqual(LifeJar.All.Length, shut.Count, "every jar is shut");
            Assert.IsFalse(hud.LifeSlotShown, "nothing ready (the slot's ready flag)");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens the kit");
            Assert.IsTrue(Shown(hud.Root.Q("kit-shut")), "the Still to come shelf is there with every jar shut");
            yield return Shot("p570-1-kit");
            Assert.IsTrue(hud.LifeRowOpen, "the shelves show the shut jars");
            if (!Sim.CanPlantAnySeed) Assert.IsFalse(Shown(hud.Root.Q("kit-ready")), "nothing under Ready");
            foreach (var s in shut)
            {
                var icon = hud.Root.Q<KitTile>("kit-life-locked-" + s.ToString().ToLowerInvariant());
                Assert.IsNotNull(icon, "a shut tile for " + s);
                // #662: shut is the spec's unavailable look, never a fade: the lock, the timber edge, full-strength words
                Assert.IsTrue(icon.ClassListContains(KitTile.ShutClass), s + " wears the shut look");
                Assert.AreEqual("kit-shut", icon.parent.name, s + " sits under Still to come");
                Assert.AreEqual(1f, icon.resolvedStyle.opacity, 0.001f, s + " is never faded");
                Assert.AreEqual(DisplayStyle.Flex, icon.Q(className: "kit-tile-lock").resolvedStyle.display, s + " carries the lock");
                Assert.AreEqual(GameHud.ShortNeed(s, Sim), icon.Line, s + " says its need without a tap");
                Assert.IsNotEmpty(icon.Line);
            }
            Assert.IsNull(hud.Root.Q("kit-life-thicket"), "no live icon for a shut jar");
            if (Sim.CanPlantAnySeed)
            {
                // #668: the seed is ready, so the one note says what to do with it; a shut jar's need comes with a tap
                Assert.IsNull(hud.LockedPick, "nothing picked while the seed waits");
                StringAssert.StartsWith("The seed grows the box's one tree", hud.LifeNoteText, "the note speaks of the seed");
            }
            else
            {
                Assert.AreEqual(LifeSpecies.Thicket, hud.LockedPick, "nothing ready: the first shut jar is picked");
                StringAssert.StartsWith(Sim.JarNeed(LifeSpecies.Thicket), hud.LifeNoteText, "the note says what it waits for");
            }
            Assert.IsEmpty(Sim.VillageWaitsLine, "a fresh box: no waits-for list before the camp (2026-11-14: only later upgrades get one)");
            StringAssert.DoesNotContain("waits for:", hud.LifeNoteText);
            yield return Shot("p570-2-chooser");
            // a tap on a shut jar says its need, and picks it; it is never taken in hand
            var rung = Sim.JarNeed(LifeSpecies.Deer);
            Assert.IsNotNull(rung);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-locked-deer"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(LifeSpecies.Deer, hud.LockedPick, "the deer are picked");
            StringAssert.StartsWith(rung, hud.LifeNoteText, "the note says what the deer wait for: " + hud.LifeNoteText);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "a shut jar is never taken in hand");
            Assert.IsTrue(hud.LifeRowOpen, "and the chooser stays open");
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet);
            yield return Shot("p570-3-picked");
            // the land gets its meadow and the thicket opens: it comes first, bright; the rest stay greyed
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            StringAssert.Contains("seed in the ground", Sim.JarNeed(LifeSpecies.Thicket), "2026-11-14: the bushes wait for the seed");
            Assert.IsTrue(Sim.PlantTree(Sim.EcoGround.campX + 0.9f, Sim.EcoGround.campZ), "the seed goes in first");
            for (int i = 0; i < 16 && !Sim.JarReady(LifeSpecies.Thicket); i++) EcoDebug.SkipSeasons(1);
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "the thicket's jar opened on a meadow");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(hud.LifeRowOpen, "the chooser stays open as a jar opens");
            var live = hud.Root.Q("kit-life-thicket");
            Assert.IsNotNull(live, "the thicket's live icon");
            Assert.IsNull(hud.Root.Q("kit-life-locked-thicket"), "and no greyed one");
            Assert.AreEqual("kit-ready", live.parent.name, "the ready jar is under Ready");
            var firstShut = hud.Root.Q("kit-shut").ElementAt(0);
            Assert.IsTrue(firstShut.ClassListContains(KitTile.ShutClass), "the shut ones under Still to come");
            Assert.Less(live.worldBound.yMax, firstShut.worldBound.yMin, "Ready comes first, Still to come after");
            yield return Shot("p570-4-ready");
            AssertNoErrors("shut jars");
        }

        /// <summary>#660 (a new thing went unnoticed): a jar that opens says so once in a toast, the Kit reads "Kit ·" with a pulsing dot
        /// while it waits, and the Kit wiggles once each visit (not once ever). In the kit its tile wears New until the kit is closed.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator JarOpens_OneToast_KitDotPulses_WiggleEachVisit()
        {
            // No tree: with the Keeper's tree in, the land can open the thicket's jar by itself during setup, and then the cheat
            // below found it already open (it failed in some batch runs). Without the tree only the cheat opens it.
            yield return LivingMeadow(tree: false);
            var hud = GameHud.Instance;
            yield return Frames(10);
            int toasts0 = hud.ReadyToasts, wiggles0 = hud.LifeWiggles;
            Assert.IsTrue(Sim.DebugJarReady(LifeSpecies.Thicket), "the thicket's jar opens");
            yield return Until(() => hud.ReadyToasts > toasts0, 3f);
            Assert.AreEqual(toasts0 + 1, hud.ReadyToasts, "one toast");
            yield return Until(() => GameUiPanel.Toasts.Current == GameHud.ReadyToast(LifeSpecies.Thicket), 6f);
            Assert.AreEqual("The bushes are ready in your kit.", GameUiPanel.Toasts.Current, "it says what is ready, and where");
            Assert.AreEqual(wiggles0 + 1, hud.LifeWiggles, "the Kit wiggles");
            Assert.IsTrue(hud.LifeDotShown, "a dot on the Kit");
            Assert.AreEqual("Kit ·", hud.KitWord, "a dot that reads");
            Assert.IsTrue(hud.KitDotPulsing, "and it pulses");
            yield return Shot("660-kit-waits");
            yield return Frames(60);
            Assert.AreEqual(toasts0 + 1, hud.ReadyToasts, "the toast only once");
            Assert.AreEqual(wiggles0 + 1, hud.LifeWiggles, "one wiggle this visit");
            hud.DebugNewVisit();
            yield return Frames(5);
            Assert.AreEqual(wiggles0 + 2, hud.LifeWiggles, "a new visit, the jar still waits: one more wiggle");
            Assert.AreEqual(toasts0 + 1, hud.ReadyToasts, "but no second toast");
            // the kit: the tile wears New until the kit closes
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(hud.KitTileNamed("kit-life-thicket").New, "New on the ready tile");
            hud.CloseSheet();
            yield return Frames(3);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(hud.KitTileNamed("kit-life-thicket").New, "seen once: no New");
            hud.CloseSheet();
            AssertNoErrors("#660");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator SmallPhone_SlotAndRowFit()
        {
            yield return LivingMeadow(750, 1334);
            var hud = GameHud.Instance;
            Sim.DebugJarsReady();
            yield return Frames(10);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(hud.LifeRowOpen);
            var panel = hud.Root.panel.visualTree.worldBound;
            var head = hud.Root.Q("kit-scrim").Q(className: "kg-sheet-head");
            var note = hud.Root.Q("kit-life-note").worldBound;
            // #668: every tile (tools, ready, shut) fits the width, is a full touch target, sits under the title and over the note
            var tiles = hud.Root.Q("kit-shelf").Query<KitTile>().ToList();
            Assert.GreaterOrEqual(tiles.Count, 4 + 6, "the four tools and every jar");
            foreach (var c in tiles)
            {
                var r = c.worldBound;
                Assert.IsTrue(r.xMin >= panel.xMin - 0.5f && r.xMax <= panel.xMax + 0.5f, $"{c.name} fits the screen's width ({r})");
                Assert.GreaterOrEqual(r.height + 32f, GameUiPanel.MinTarget - 0.5f, $"{c.name} is a full touch target (with its reach)");
                Assert.Greater(r.yMin, head.worldBound.yMax - 0.5f, $"{c.name} sits under the title");
                Assert.LessOrEqual(r.yMax, note.yMin + 0.5f, $"{c.name} sits over the note");
            }
            // the landscaping polish: the four tools are one row, never two
            float y0 = hud.Root.Q("kit-cloth").worldBound.yMin;
            foreach (var n in new[] { "kit-mister", "kit-tweezers", "kit-landscape" })
                Assert.AreEqual(y0, hud.Root.Q(n).worldBound.yMin, 0.5f, n + " is in the tools' one row");
            Assert.LessOrEqual(note.yMax, panel.yMax + 0.5f, "the note is on screen");
            yield return Shot("p9-slot-9-small-phone-row");
            AssertNoErrors("small phone");
        }

        /// <summary>Tester 2026-11-08: at 1640x2360 the chooser row ran 26 px into the kit's title and onto the Life slot. #668: at every
        /// phone and tablet size every tile sits wholly under the title and over the note. Shots: p9-slot-row-WxH.png.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator EverySize_ChooserRowClearsTitleAndSlot()
        {
            yield return LivingMeadow(1080, 1920);
            var hud = GameHud.Instance;
            Sim.DebugJarsReady();
            yield return Frames(10);
            foreach (var (w, h) in new[] { (1080, 1920), (1080, 2400), (1179, 2556), (1640, 2360) })
            {
                SetSize(w, h);
                yield return Frames(15);
                if (hud.OpenSheet != GameHud.Kit) { yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean")); yield return new WaitForSecondsRealtime(0.6f); }
                Assert.AreEqual(GameHud.Kit, hud.OpenSheet, $"{w}x{h}: the kit is open");
                Assert.IsTrue(hud.LifeRowOpen, $"{w}x{h}: the shelves show");
                var note = hud.Root.Q("kit-life-note").worldBound;
                var title = hud.Root.Q("kit-scrim").Q(className: "kg-sheet-head").worldBound;
                foreach (var c in hud.Root.Q("kit-shelf").Query<KitTile>().ToList())
                {
                    var r = c.worldBound;
                    Assert.GreaterOrEqual(r.yMin, title.yMax - 0.5f, $"{w}x{h}: {c.name} clears the title ({r} vs {title})");
                    Assert.LessOrEqual(r.yMax, note.yMin + 0.5f, $"{w}x{h}: {c.name} clears the note ({r} vs {note})");
                }
                yield return Shot($"p9-slot-row-{w}x{h}");
            }
            AssertNoErrors("row at every size");
        }
    }
}
