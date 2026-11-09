using AquariumCiv.Game;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// P8 debug hooks (CLAUDE.md: the debug panel stays current): the "Living land (P8)" section of
    /// <see cref="SliceDebugPanel"/>, also callable from `unity command script eval`.
    /// </summary>
    public static class EcoDebug
    {
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        static string Refresh(string what)
        {
            var src = SimLayoutSource.Find();
            if (src != null) src.GetComponent<BoxView>().Refresh();
            return what + " · " + Status();
        }

        /// <summary>#285: which jobs are open on this land and why (one line each).</summary>
        public static string Jobs() { var s = Sim; return s == null ? "no sim" : s.EcologyOn ? s.DebugJobsReadout() : "living land off"; }

        public static string Status()
        {
            var s = Sim;
            if (s == null) return "no sim";
            if (!s.EcologyOn) return "living land off" + (GameClock.EcologyPref ? " (on for the next box)" : "");
            var e = s.State.eco;
            var t = s.EcoTree;
            return $"Y{s.State.month / 12f:0.0} bare {s.EcoBareShare:P0} meadow {s.EcoMeadowShare:P0} · rabbits {s.EcoRabbits:0.0} birds {s.EcoBirds:0.0} · " +
                   $"pond {s.EcoPondCells} trees {s.EcoGrownTrees} · humid {s.EcoHumidity:0.00} · gates {(s.CampGateOpen ? "camp" : "-")}/{(s.VillageGateOpen ? "village" : "-")} · " +
                   (t.planted ? $"tree stage {t.stage}" : s.CanPlantTree ? "seed ready" : "no seed");
        }

        public static string NewBox(bool on) => GameClock.DebugEcologyBox(on);

        /// <summary>Mists the middle of the founding flat (or a point) for a few seconds, as a held finger would.</summary>
        public static string MistAt(float x, float z, float seconds = 2f)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var mv = EcoMistView.Instance;
            if (mv != null) mv.Pulse(s, new Vector3(x, 0f, z), seconds);
            else s.Mist(x, z, seconds);
            return Refresh($"misted ({x:0.0}, {z:0.0})");
        }

        /// <summary>"Mist big here": a full-length mist at a point, with the wet area and the puddles it leaves (#266).</summary>
        public static string MistBig(float x, float z)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var n = s.DebugMistBig(x, z).Count;
            return Refresh($"big mist at ({x:0.0}, {z:0.0}), {n} cells green");
        }

        public static string MistBigCamp() { var st = Sim?.Settlement; return st != null ? MistBig(st.Map.greenX + 0.5f, st.Map.greenZ) : "no sim"; }

        /// <summary>"Puddles now": soak the ground round the camp so wet ground and puddles show at once (#266).</summary>
        public static string PuddlesNow()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var st = s.Settlement;
            int n = st != null ? s.DebugPuddlesNow(st.Map.greenX + 0.5f, st.Map.greenZ) : 0;
            return Refresh($"{n} puddle cells");
        }

        /// <summary>"Puddle visit": the people near a puddle come over now, and a rabbit hops up to drink.</summary>
        public static string PuddleVisit() { var p = PuddleLife.Instance; return p != null ? p.VisitNow() : "no puddle life"; }

        /// <summary>"Rabbit tracks now": the tracks show at the meadow's edge at once (#282).</summary>
        public static string RabbitTracksNow()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            return s.DebugRabbitTracksNow() ? Refresh($"rabbit tracks at {s.EcoRabbitTrackCells.Count} cells") : "no change (tracks already there)";
        }

        /// <summary>"+N rabbits": add rabbits to the sim count; the first ones go through the tracks and the first-rabbit sign (#282).</summary>
        public static string AddRabbits(int n)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            int c = s.DebugAddRabbits(n);
            return Refresh($"+{n} rabbits: {c} (shown {s.EcoRabbitsShown()})");
        }

        /// <summary>"Water breaks the surface now" (#279): with no pond, the water table rises to the flood line and seasons pass until
        /// water stands (the moment plays as it would in play); with a pond, the moment replays.</summary>
        public static string WaterBreaksNow()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var fw = FirstWaterView.Instance;
            if (s.EcoPondCells > 0)
            {
                if (fw == null) return "no first-water view";
                fw.Begin(true); return "first water moment replayed";
            }
            s.DebugSetTable(s.EcoFloodLine);
            int n = 0;
            while (s.EcoPondCells == 0 && n < 4) { s.DebugSkipSeasons(1); n++; }
            return Refresh(s.EcoPondCells > 0 ? $"water broke the surface after {n} season(s): {s.EcoPondCells} cells" : "no water after 4 seasons");
        }

        /// <summary>"Frogs now" (#286): the night's frogs show for a minute.</summary>
        public static string FrogsNow() { var p = PondLife.Instance; return p != null ? p.FrogsNow() : "no pond life"; }

        /// <summary>"Ducks now" (#286): the ducks show for a minute.</summary>
        public static string DucksNow() { var p = PondLife.Instance; return p != null ? p.DucksNow() : "no pond life"; }

        /// <summary>"First bush now" (#288): the first bush comes up at once (a bush is made on the grass nearest the camp if none
        /// stands); the bird lands on it and the people notice.</summary>
        public static string FirstBushNow()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            int c = s.DebugFirstBushNow();
            return c < 0 ? "no bush: no ground for it" : Refresh($"first bush at cell {c}");
        }

        /// <summary>"+N bushes" (#288, #385): bushes in the thickets' hearts (before the first thicket, on the grass nearest the camp).</summary>
        public static string AddBushes(int n)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            int c = s.DebugAddBushes(n);
            var all = new System.Collections.Generic.List<int>(); s.BushCells(all);
            return Refresh($"+{n} bushes: {c} came up ({all.Count} of {s.BushCap})");
        }

        /// <summary>"Bush carpet" (#385): the meadow turns to bushes, as an old save grew them; they thin back to the thickets over a few years.</summary>
        public static string CarpetBushes()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            int c = s.DebugCarpetBushes();
            return Refresh($"bush carpet: {c} came up; the land holds {s.BushCap}");
        }

        /// <summary>"Start a hunt now" (#283): a hunter takes a spear and goes after the nearest rabbit, starting now.</summary>
        public static string StartHunt()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            if (!pv.Planner.DebugStartHunt(pv.Now, out var plan)) return "no hunt: no rabbit shown, or nobody free";
            return $"hunt started: rabbit {plan.rabbitId}, {(plan.catches ? "will catch" : "will miss")}, strike in {(plan.strikeStart - pv.Now.hour):0.0} h";
        }

        /// <summary>"Next hunt succeeds / misses" (#283): fixes how the next hunt that has not struck yet ends.</summary>
        public static string ForceHunt(bool catches)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            bool ok = pv.Planner.DebugForceHunt(catches);
            return catches ? (ok ? "next hunt catches" : "refused: the herd is at its floor, it stays a miss") : "next hunt misses";
        }

        /// <summary>"Slow-mo the next hunt" (#362): the view clock runs at half speed from the stalk to the held-up catch (or the pulled spear), once.</summary>
        public static string HuntSlowMoNext()
        {
            HuntSlowMo.Armed = true;
            return "the next hunt plays at half speed (from the stalk on)";
        }

        public static string MistCamp()
        {
            var st = Sim?.Settlement;
            return st != null ? MistAt(st.Map.greenX + 0.5f, st.Map.greenZ) : "no sim";
        }

        public static string MistRandom()
        {
            float x = Random.Range(-2.5f, 2.5f), z = Random.Range(-1.8f, 1.8f);
            return MistAt(x, z, 2f);
        }

        public static string SkipSeasons(int n)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            s.DebugSkipSeasons(n);
            return Refresh($"+{n} seasons");
        }

        /// <summary>"Open the camp gate now" (#284): gives hides if short, opens the gate and plays the camp moment (the first tent goes up from the hides).</summary>
        public static string CampGate()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var b = s.DebugCampMoment();
            return Refresh(b != null ? $"camp gate open: the first tent (building {b.id}) goes up" : "camp gate: no room for a tent");
        }

        /// <summary>Only the gate (no hides, no tent): for tests that bring the hides themselves.</summary>
        public static string OpenCampGate() { var s = Sim; return s != null && s.DebugOpenCampGate() ? Refresh("camp gate open") : "no change"; }

        /// <summary>"+N hides" (#284): hides drying by the fire, on the frame.</summary>
        public static string AddHides(int n)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            int c = s.DebugAddHides(n);
            return Refresh($"+{n} hides: {c} on the frame");
        }
        // #332: "show the drying rack with N hides": sets the hides drying by the fire to exactly N (the rack draws one pelt each, a pile at its foot).
        public static string ShowRack(int n)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var e = s.State.eco;
            e.hidesDrying = Mathf.Max(0, n);
            if (e.hidesTotal < e.hidesDrying) e.hidesTotal = e.hidesDrying;
            return Refresh($"rack with {n} hides");
        }

        public static string VillageGate() { var s = Sim; return s != null && s.DebugOpenVillageGate() ? Refresh("village gate open") : "no change"; }

        public static string Table(float aboveFlood)
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            s.DebugSetTable(s.EcoFloodLine + aboveFlood);
            s.DebugSkipSeasons(1); // the pond surfaces at the next season
            return Refresh($"table at flood line {aboveFlood:+0.00;-0.00}");
        }

        public static string Humidity(float v) { var s = Sim; if (s == null || !s.EcologyOn) return "living land off"; s.DebugSetHumidity(v); s.DebugSkipSeasons(1); s.DebugSetHumidity(v); return Refresh($"humidity {v:0.00} (+1 season, so the glass shows it)"); } // tester P8: droplets only build on a season step

        public static string PlantHere()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            var st = s.Settlement;
            float x = 0f, z = 0f;
            if (st != null) { x = st.Map.greenX + 0.6f; z = st.Map.greenZ + 0.3f; }
            return s.PlantTree(x, z) ? Refresh("the Keeper's tree planted") : "can't plant (no seed yet, or a bad spot)";
        }

        // #378: press the seed on the very corner of the bed; the sim must snap it in from the glass.
        public static string PlantAtEdge()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            float x = -AquariumCiv.Sim.Settle.Terrain.HX + 0.02f, z = AquariumCiv.Sim.Settle.Terrain.HZ - 0.02f;
            int at = AquariumCiv.Sim.Settle.SiteMap.CellAt(x, z), to = s.TreeCellFor(x, z);
            if (!s.PlantTree(x, z)) return "can't plant at the edge (no seed yet, or a tree stands)";
            return Refresh($"edge cell {at} -> tree cell {to}");
        }

        // ---- #518: the seed in hand (it places like the life slot: a ghost, then a tap on it); readies the seed first if the land hasn't ----
        public static string SeedInHand()
        {
            var s = Sim;
            if (s == null || !s.EcologyOn) return "living land off";
            if (s.State.eco.treeCell >= 0) return "a tree stands: one seed per box";
            if (s.State.eco.campGateMonth < 0) s.DebugOpenCampGate();
            if (!s.State.eco.Reached(AquariumCiv.Sim.Eco.EcoMilestones.SeedUnlocked))
            { s.State.eco.milestoneKeys.Add(AquariumCiv.Sim.Eco.EcoMilestones.SeedUnlocked); s.State.eco.milestoneMonths.Add(s.State.month); }
            if (!s.CanPlantTree) return "no seed";
            var hud = AquariumCiv.GameUI.GameHud.Instance;
            if (hud == null) return "no HUD";
            hud.SetHand(AquariumCiv.GameUI.GameHud.KitTool.Seed);
            var sp = SeedPlaceView.Instance;
            return "the seed in hand: tap the land, then the outline" + (sp != null && sp.Last.Length > 0 ? $" (last: {sp.Last})" : "");
        }
        // ---- end #518 ----
        public static string TreeStage(int stage) { var s = Sim; return s != null && s.DebugTreeStage(stage) ? Refresh($"tree stage {stage}") : "no tree"; }
    }
}
