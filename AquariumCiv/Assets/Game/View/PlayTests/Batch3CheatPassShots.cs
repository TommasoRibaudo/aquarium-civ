// Batch 3 cheat pass: every #692 (parts 1-3), #708 and #709 debug-panel cheat at the default phone camera (1080x1920, the camp
// frame), one shot each, with a doll-jump watch (a doll that moves more than PopStep in one frame "pops"). Explicit: run as category
// "Batch3CheatPass"; shots land in Temp/agent/b3cheats/ and a summary line per cheat in the log ("[b3cheats]").
using System;
using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Explicit, Category("Batch3CheatPass")]
    public class Batch3CheatPassShots : PlayTestBase
    {
        const float PopStep = 0.12f;
        static SimCore Sim => GameClock.Instance.ActiveSim;

        IEnumerator Box(float hour, int kids = 2, Season? season = null)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1080, 1920, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Debug.Log("[b3cheats] " + CampCheats.StageCamp(hour, kids));
            if (season.HasValue) { CampCheats.ToSeason(season.Value); yield return Frames(5); PeopleView.DebugSetHour(hour); }
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log("[b3cheats] " + CampFrameView.DebugCampFrame());
            yield return new WaitForSecondsRealtime(1.5f);
        }

        /// <summary>Runs a cheat, watches the dolls for <paramref name="seconds"/>, shoots at <paramref name="shotAt"/>.</summary>
        IEnumerator Cheat(string name, Func<string> cheat, float shotAt = 6f, float seconds = 9f)
        {
            var pv = PeopleView.Find();
            int started = pv != null && pv.Camp != null ? pv.Camp.Started.Count : 0;
            string r;
            try { r = cheat(); } catch (Exception e) { r = "THREW " + e.Message; }
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            var last = new Dictionary<long, Vector2>();
            int pops = 0; float worst = 0f; bool shot = false; float t = 0f;
            while (t < seconds)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                if (pv != null)
                {
                    pv.StandingNow(who, xs, zs);
                    for (int i = 0; i < who.Count; i++)
                    {
                        var p = new Vector2(xs[i], zs[i]);
                        if (last.TryGetValue(who[i].Key, out var q))
                        {
                            float d = Vector2.Distance(p, q);
                            if (d > worst) worst = d;
                            if (d > PopStep) pops++;
                        }
                        last[who[i].Key] = p;
                    }
                }
                if (!shot && t >= shotAt) { shot = true; yield return Shot($"b3cheats/{name}"); }
            }
            int played = pv != null && pv.Camp != null ? pv.Camp.Started.Count - started : -1;
            Debug.Log($"[b3cheats] {name}: \"{r}\" · camp uses started {played} · doll pops {pops} (worst step {worst:0.000} u)");
        }

        // ---------------- #692 part 1: the fire, the pot, the feather pole ----------------
        [UnityTest, Timeout(900000)]
        public IEnumerator Part1()
        {
            yield return Box(17.8f, 2, Season.Winter);
            yield return Cheat("692-b29a-warm-hands", CampCheats.WarmHands, 6f);
            yield return Box(18.3f);
            yield return Cheat("692-b29b-kids-poke", CampCheats.KidPoke, 5f);
            yield return Box(18.3f, 2, Season.Autumn);
            yield return Cheat("692-b29c-berry-stick", CampCheats.BerryStick, 7f);
            yield return Box(17.7f);
            yield return Cheat("692-b29d-spit-rabbit", () => CampCheats.Spit(false), 9f, 12f);
            yield return Box(17.7f);
            yield return Cheat("692-b29d-spit-deer", () => CampCheats.Spit(true), 9f, 12f);
            yield return Box(17.7f);
            yield return Cheat("692-b30a-pot-fill", CampCheats.PotFill, 9f, 12f);
            yield return Box(18.9f);
            yield return Cheat("692-b30e-pot-off", CampCheats.PotOff, 6f);
            yield return Box(18f);
            yield return Cheat("692-b30e-pot-toggle", CampCheats.PotToggle, 3f, 4f);
            yield return Box(18f);
            yield return Cheat("692-b30c-kids-peek", CampCheats.PotPeek, 4f);
            yield return Box(18f);
            yield return Cheat("692-b30d-sniff", CampCheats.PotSniff, 3f, 5f);
            yield return Box(17.9f);
            yield return Cheat("692-b30b-ladle", CampCheats.Ladle, 6f);
            yield return Box(17.4f);
            yield return Cheat("692-b31a-feather-newborn", () => CampCheats.TieFeather(PoleFeatherKind.Newborn), 6f);
            yield return Box(17.4f);
            yield return Cheat("692-b31a-feather-marriage", () => CampCheats.TieFeather(PoleFeatherKind.Marriage), 6f);
            yield return Box(17.4f);
            yield return Cheat("692-b31a-feather-village", () => CampCheats.TieFeather(PoleFeatherKind.Village), 6f);
            yield return Box(11f, 3);
            yield return Cheat("692-b31b-pole-dare", CampCheats.PoleDare, 8f, 11f);
        }

        // ---------------- #692 part 2: the Keeper's tree ----------------
        [UnityTest, Timeout(900000)]
        public IEnumerator Part2()
        {
            yield return Box(11f); yield return Cheat("692-b32a-tree-sit", CampCheats.TreeSit, 8f, 11f);
            yield return Box(11f); yield return Cheat("692-b32b-tree-touch", CampCheats.TreeTouch, 8f, 11f);
            yield return Box(11f); yield return Cheat("692-b32c-kids-circle", CampCheats.TreeCircle, 8f, 11f);
            yield return Box(11f); yield return Cheat("692-b32d-quiet-visit", CampCheats.TreeQuiet, 8f, 11f);
            yield return Box(11f); yield return Cheat("692-b32e-newlyweds", CampCheats.TreeWed, 8f, 11f);
            yield return Box(11f); yield return Cheat("692-b32f-newborn", CampCheats.TreeNewborn, 8f, 11f);
            yield return Box(11f); yield return Cheat("692-b32g-old-one-sits", CampCheats.TreeOld, 8f, 11f);
            yield return Box(11f); yield return Cheat("692-b32h-eldest-tells", CampCheats.TreeTell, 8f, 11f);
        }

        // ---------------- #692 part 3: grass, saplings, rocks, branches ----------------
        [UnityTest, Timeout(900000)]
        public IEnumerator Part3()
        {
            yield return Box(11f); yield return Cheat("692-b33a-grass-roll", CampCheats.GrassRoll, 6f);
            yield return Box(11f); yield return Cheat("692-b33b-lie-back", CampCheats.LieBack, 7f);
            yield return Box(11f); yield return Cheat("692-b33c-pick-flowers", CampCheats.PickFlowers, 7f);
            yield return Box(11f); yield return Cheat("692-b33d-grass-whistle", CampCheats.GrassWhistle, 6f);
            yield return Box(11f); yield return Cheat("692-land-ready", CampCheats.LandReady, 3f, 4f);
            yield return Box(11f); Debug.Log("[b3cheats] " + CampCheats.LandReady()); yield return Frames(30); yield return Cheat("692-b34a-water-sapling", CampCheats.WaterSapling, 8f, 11f);
            yield return Box(11f); Debug.Log("[b3cheats] " + CampCheats.LandReady()); yield return Frames(30); yield return Cheat("692-b34b-ring-sapling", CampCheats.RingSapling, 8f, 11f);
            yield return Box(11f); Debug.Log("[b3cheats] " + CampCheats.LandReady()); yield return Frames(30); yield return Cheat("692-b34c-plant-twig", CampCheats.PlantTwig, 8f, 11f);
            yield return Box(11f); Debug.Log("[b3cheats] " + CampCheats.LandReady()); yield return Frames(30); yield return Cheat("692-b35a-sit-rock", CampCheats.SitRock, 8f, 11f);
            yield return Box(11f); Debug.Log("[b3cheats] " + CampCheats.LandReady()); yield return Frames(30); yield return Cheat("692-b35b-stack-pebbles", CampCheats.StackPebbles, 8f, 11f);
            yield return Box(11f, 3); Debug.Log("[b3cheats] " + CampCheats.LandReady()); yield return Frames(30); yield return Cheat("692-b36a-stick-fight", CampCheats.StickFight, 7f, 10f);
            yield return Box(11f, 3); Debug.Log("[b3cheats] " + CampCheats.LandReady()); yield return Frames(30); yield return Cheat("692-b36b-stick-horse", CampCheats.StickHorse, 7f, 10f);
        }

        // ---------------- #708: the fish (B70) and the kids' try by hand (B39.c) ----------------
        IEnumerator FishBox()
        {
            yield return Box(11f, 2);
            EcoDebug.Table(0.3f);
            yield return Frames(20);
            Sim.DebugJarReady(LifeSpecies.Ducks); Sim.DebugPlaceNearCamp(LifeSpecies.Ducks);
            yield return Frames(10);
            Sim.DebugJarReady(LifeSpecies.Fish); Sim.DebugPlaceNearCamp(LifeSpecies.Fish);
            yield return new WaitForSecondsRealtime(6f);
            Debug.Log("[b3cheats] " + CampFrameView.DebugCampFrame());
            yield return new WaitForSecondsRealtime(1.5f);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Fish708()
        {
            yield return FishBox();
            yield return Cheat("708-fish-shadows", FishView.DebugShadows, 4f, 6f);
            yield return Cheat("708-fish-at-the-glass", FishView.DebugGlass, 4f, 6f);
            yield return Cheat("708-fish-startle", FishView.DebugStartle, 1f, 4f);
            yield return Cheat("708-two-kids-join", () => $"{Sim.DebugAddKids(2)} kids came", 3f, 4f);
            yield return Cheat("708-b39c-kids-catch-by-hand", () => KidPlayDirector.Debug(PlayGame.FishGrab), 7f, 12f);
        }

        // ---------------- #709: the nest in the thicket (B71) ----------------
        [UnityTest, Timeout(900000)]
        public IEnumerator Nest709()
        {
            yield return Box(11f, 2);
            yield return Cheat("709-place-the-nest", () => { int c = Sim.DebugNestPlace(); return c >= 0 ? $"nest at bush {c}" : "no ground"; }, 4f, 5f);
            yield return Cheat("709-nest-show-me", NestView.DebugLook, 3f, 4f);
            yield return Cheat("709-skip-the-building", NestView.DebugSkipBuild, 3f, 4f);
            yield return Cheat("709-lay-eggs", () => $"{Sim.DebugNestLay()} eggs", 3f, 4f);
            yield return Cheat("709-take-an-egg-sim", () => Sim.DebugNestTakeEgg() ? $"taken, {Sim.NestEggs} left" : "refused", 3f, 4f);
            yield return Cheat("709-gatherer-takes-an-egg", () => { Sim.DebugNestLay(); var pv = PeopleView.Find(); return pv.Planner.DebugStartNestVisit(pv.Now, true, out _) ? "a gatherer walks to the nest" : "nobody free, or no nest"; }, 8f, 12f);
            yield return Cheat("709-kids-look-at-the-nest", () => { Sim.DebugNestLay(); var pv = PeopleView.Find(); return pv.Planner.DebugStartNestVisit(pv.Now, false, out var np) ? $"{np.lookerKeys.Count} kids creep up" : "no kids, or no nest"; }, 8f, 12f);
            yield return Cheat("709-hatch", () => $"{Sim.DebugNestHatch()} chicks", 3f, 4f);
            yield return Cheat("709-nest-next-stage", () => "nest: " + Sim.DebugNestNextStage(), 3f, 4f);
            yield return Cheat("709-chicks-fly-off", () => Sim.DebugNestFlyOff() ? "they leave" : "no parent or chicks", 3f, 6f);
            yield return Box(11f, 2);
            yield return Cheat("709-skip-to-spring", () => { int c = Sim.DebugNestSpring(); return c >= 0 ? $"spring: the parent builds at bush {c}" : "no ground"; }, 6f, 8f);
        }
    }
}
