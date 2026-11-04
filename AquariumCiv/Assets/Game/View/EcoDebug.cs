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

        public static string CampGate() { var s = Sim; return s != null && s.DebugOpenCampGate() ? Refresh("camp gate open") : "no change"; }
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

        public static string TreeStage(int stage) { var s = Sim; return s != null && s.DebugTreeStage(stage) ? Refresh($"tree stage {stage}") : "no tree"; }
    }
}
