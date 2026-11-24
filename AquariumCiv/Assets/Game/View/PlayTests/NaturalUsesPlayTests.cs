// 2026-11-24 (the user: "I just want the ones that are not happening to happen naturally and organically"): the seven approved uses that
// never started by themselves now do. With natural uses on and no cheat for the uses themselves:
// - CampNatural (category NaturalUses): a lived-in summer camp with a grown dog, a pup and rabbits near: the dog's hunt, the dog in the
//   sun, the moths at the fire (summer dusk, before bedtime), the path race and the path chat (on the trodden way to the tents) each start.
// - VillageNatural (category NaturalUses): a village: the lamplighter's round of the house lanterns and the doorstep sit each start.
// Both log every use started per view day (the census).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("NaturalUses")]
    public class NaturalUses : PlayTestBase
    {
        static string Census(PeopleView pv) => string.Join(", ", pv.Camp.Started.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} x{kv.Value}"));

        IEnumerator RunUntil(PeopleView pv, string tag, CampUse[] want, int maxDays, float maxReal, bool nextMonth)
        {
            // real play speed by day and evening (x1: a scene's seconds are an hour of view time, so the bedtime/meal rule judges as in play);
            // the night fast (x16: nothing starts then)
            int days = 0; float hourWas = pv.Now.hour; float t0 = Time.realtimeSinceStartup;
            var hours = new System.Text.StringBuilder(); int lastH = -1;
            while (days < maxDays && Time.realtimeSinceStartup - t0 < maxReal && !want.All(u => pv.Camp.Started.ContainsKey(u)))
            {
                yield return null;
                pv.timeScale = pv.Now.hour >= 7.5f && pv.Now.hour < 19.5f ? 1f : 16f;
                int hh = Mathf.FloorToInt(pv.Now.hour);
                if (hh != lastH && hh >= 7 && hh <= 20)
                {   // who could be borrowed this hour: standing, in a free-time kind (CampLife.FreeKind), grown / kid; and how many eat
                    lastH = hh; int g = 0, k = 0, eat = 0;
                    foreach (var a in pv.Activities)
                    {
                        if (a.kind == ActivityKind.Eat) eat++;
                        bool free = !a.moving && !a.enRoute && a.kind is ActivityKind.SitOut or ActivityKind.Chat or ActivityKind.ListenStory or ActivityKind.Listen or ActivityKind.Sing or ActivityKind.SitTogether or ActivityKind.Visit or ActivityKind.Play;
                        if (!free) continue;
                        if (pv.Planner.StageOf(a.who) == LifeStage.Kid) k++; else if (pv.Planner.StageOf(a.who) is LifeStage.Adult or LifeStage.Elder) g++;
                    }
                    hours.Append($" {hh}h:{g}g{k}k{(eat > 0 ? "e" + eat : "")}");
                }
                if (pv.Now.hour < hourWas - 12f)
                {
                    days++;
                    Debug.Log($"[natural {tag}] day {days} (view day {pv.Now.day}, {LifeClock.SeasonOf(pv.Now.day)}) free by hour:{hours}");
                    hours.Clear(); lastH = -1;
                    if (nextMonth) AquariumCiv.Game.GameClock.DebugSkipHours(1.0 / 12.0 + 0.001); // a view day is a sim month (as VillageWorkShare)
                    Debug.Log($"[natural {tag}] day {days}: {Census(pv)}");
                }
                hourWas = pv.Now.hour;
            }
            pv.timeScale = 1f;
            Debug.Log($"[natural {tag}] after {days} view days, {Time.realtimeSinceStartup - t0:0} s: {Census(pv)}");
            Debug.Log($"[natural {tag}] why not: " + string.Join(" | ", pv.Camp.WhyNot.OrderByDescending(kv => kv.Value).Take(30).Select(kv => $"{kv.Key} x{kv.Value}")));
        }

        [UnityTest, Timeout(2400000)]
        public IEnumerator CampNatural_DogHuntDogSunMothsPathRacePathChatStartByThemselves()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[natural camp] " + CampCheats.StageCamp(8f, 4, onlyCheats: false));
            Debug.Log("[natural camp] " + CampCheats.ToSeason(Season.Summer));
            yield return Frames(5);
            PeopleView.DebugSetHour(8f);
            Debug.Log("[natural camp] " + CampCheats.GrownDogNow());
            Debug.Log("[natural camp] " + CampCheats.PupNow());
            var pv = PeopleView.Find();
            Debug.Log("[natural camp] " + pv.Camp.ThicketReady());
            yield return Frames(10);
            var want = new[] { CampUse.DogHunt, CampUse.DogSun, CampUse.ChaseMoths, CampUse.PathRace, CampUse.PathChat };
            yield return RunUntil(pv, "camp", want, 8, 2000f, true); // a year: each view day the next month (summer for the moths)
            var missing = want.Where(u => !pv.Camp.Started.ContainsKey(u)).ToList();
            CloseSlice();
            Assert.IsEmpty(missing, "never started by themselves: " + string.Join(", ", missing));
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator VillageNatural_HouseLanternsAndDoorstepSitStartByThemselves()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[natural village] " + CampCheats.StageVillage(14f, false, 4, onlyCheats: false));
            yield return Frames(10);
            var pv = PeopleView.Find();
            var want = new[] { CampUse.LightLanterns, CampUse.TentDoorSit };
            yield return RunUntil(pv, "village", want, 6, 1500f, true);
            var missing = want.Where(u => !pv.Camp.Started.ContainsKey(u)).ToList();
            CloseSlice();
            Assert.IsEmpty(missing, "never started by themselves: " + string.Join(", ", missing));
        }
    }
}
