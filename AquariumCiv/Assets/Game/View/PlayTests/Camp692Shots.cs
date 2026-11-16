// #692 part 1: phone shots (750x1334) of each camp use at the default camera, in a fresh box each, at the moments the spec's read
// depends on. Explicit: run as category "Camp692Shots"; shots land in Temp/agent/692/.
using System;
using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Explicit, Category("Camp692Shots")]
    public class Camp692Shots : PlayTestBase
    {
        IEnumerator Use(string name, float hour, Season? season, Func<string> cheat, params float[] at)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Debug.Log("[692 shots] " + CampCheats.StageCamp(hour, name.Contains("dare") ? 3 : 2));
            if (season.HasValue) { CampCheats.ToSeason(season.Value); yield return Frames(5); PeopleView.DebugSetHour(hour); }
            yield return new WaitForSeconds(2f);
            string r = cheat();
            Debug.Log($"[692 shots] {name}: {r}");
            float t = 0f;
            for (int i = 0; i < at.Length; i++)
            {
                yield return new WaitForSeconds(at[i] - t);
                t = at[i];
                yield return Shot($"692/{name}_{i + 1}_{at[i]:0}s");
            }
            Assert.Greater(PeopleView.Find().Camp.Started.Count, 0, name + ": a use played (" + r + ")");
        }

        [UnityTest, Timeout(300000)] public IEnumerator B29a() => Use("b29a-warm-hands", 17.8f, Season.Winter, CampCheats.WarmHands, 6f, 9f);
        [UnityTest, Timeout(300000)] public IEnumerator B29b() => Use("b29b-kid-poke", 18.3f, null, CampCheats.KidPoke, 4f, 12f);
        [UnityTest, Timeout(300000)] public IEnumerator B29c() => Use("b29c-berry-stick", 18.3f, Season.Autumn, CampCheats.BerryStick, 7f, 10f);
        [UnityTest, Timeout(300000)] public IEnumerator B29d() => Use("b29d-spit", 17.7f, null, () => CampCheats.Spit(false), 9f, 20f, 36f);
        [UnityTest, Timeout(300000)] public IEnumerator B30a() => Use("b30a-pot-fill", 17.7f, null, CampCheats.PotFill, 9f, 16f);
        [UnityTest, Timeout(300000)] public IEnumerator B30b() => Use("b30b-ladle", 17.9f, null, CampCheats.Ladle, 6f, 9f);
        [UnityTest, Timeout(300000)] public IEnumerator B30c() => Use("b30c-kids-peek", 18f, null, CampCheats.PotPeek, 3f, 5f);
        [UnityTest, Timeout(300000)] public IEnumerator B30d() => Use("b30d-sniff", 18f, null, CampCheats.PotSniff, 3f);
        [UnityTest, Timeout(300000)] public IEnumerator B30e() => Use("b30e-pot-off", 18.9f, null, CampCheats.PotOff, 4f, 9f);
        [UnityTest, Timeout(300000)] public IEnumerator B31a() => Use("b31a-feather", 17.4f, null, () => CampCheats.TieFeather(AquariumCiv.Sim.PoleFeatherKind.Newborn), 5f, 9f);
        [UnityTest, Timeout(300000)] public IEnumerator B31b() => Use("b31b-pole-dare", 11f, null, CampCheats.PoleDare, 7f, 10f);
    }
}
