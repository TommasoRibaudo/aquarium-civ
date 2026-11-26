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

        /// <summary>#687 (the thirst signs, the user 2026-11-24): a green camp, then a dry spell: the grass pales to straw and its tufts wilt; a
        /// grown-up looks up at the glass for rain and shakes their head. Shots and frames for the user's look (Explicit).</summary>
        [UnityTest, Explicit("Shots for the user's sign-off (#687)"), Timeout(600000)]
        public IEnumerator ThirstSigns_Shots()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            Debug.Log("[687 thirst] " + CampCheats.StageCamp(11f, 3));
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            // a real meadow first: mist wide, let three seasons grow it, then mist again so the 'before' is green and watered
            for (float x = -2.4f; x <= 2.4f; x += 0.8f) for (float z = Land.ZMin + 0.6f; z <= Land.ZMax - 0.6f; z += 0.8f) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(3);
            for (float x = -2.4f; x <= 2.4f; x += 0.8f) for (float z = Land.ZMin + 0.6f; z <= Land.ZMax - 0.6f; z += 0.8f) EcoDebug.MistBig(x, z);
            PeopleView.DebugSetHour(11f);
            yield return new WaitForSeconds(3f);
            Debug.Log($"[687 thirst] meadow: {EcoDebug.Status()}");
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            var rig = Camera.main.GetComponent<BoxCamera>();
            var fire = new Vector2(sim.EcoGround.campX, sim.EcoGround.campZ);
            rig.FlyTo(box.transform.TransformPoint(box.land.OnGround(fire.x + 0.4f, fire.y)), Mathf.Max(rig.minZoom * 1.6f, 0.45f), 0.4f);
            for (int i = 0; i < 200 && rig.IsFlying; i++) yield return null;
            yield return new WaitForSeconds(1f);
            System.IO.Directory.CreateDirectory(Shots + "687-thirst/");
            Debug.Log($"[687 thirst] before: thirst {sim.EcoThirst:0.00}");
            yield return Shot("687-thirst/0-green");
            Debug.Log("[687 thirst] " + EcoDebug.DrySpell());
            yield return new WaitForSeconds(2f);
            yield return Shot("687-thirst/1-dry");
            Assert.GreaterOrEqual(sim.EcoThirst, CampLife.ThirstyBox, "a dry spell: most of the grass thirsty");
            // the look up for rain, framed on the one who does it
            int n0 = pv.Camp.Started.TryGetValue(CampUse.SkyLook, out int a0) ? a0 : 0;
            Debug.Log("[687 thirst] " + CampCheats.SkyLook());
            Assert.Greater(pv.Camp.Started.TryGetValue(CampUse.SkyLook, out int a1) ? a1 : 0, n0, "the look up started");
            var sc = pv.Camp.Scenes.Last(x => x.use == CampUse.SkyLook);
            var lookAct = pv.Activities.FirstOrDefault(a => a.who.Key == sc.keys[0]);
            if (lookAct.who.Key == sc.keys[0] && pv.TryGetDollPosition(lookAct.who, out var who))
            {
                rig.FlyTo(box.transform.TransformPoint(box.land.OnGround(who.x, who.y)), Mathf.Max(rig.minZoom * 1.05f, 0.2f), 0.25f);
                for (int i = 0; i < 120 && rig.IsFlying; i++) yield return null;
            }
            float t0 = Time.realtimeSinceStartup; int f = 0;
            while (Time.realtimeSinceStartup - t0 < 4.4f)
            {
                yield return null;
                if ((f++ % 3) == 0) ScreenCapture.CaptureScreenshot(Shots + $"687-thirst/look_{f:000}.png");
            }
            CloseSlice();
        }

        /// <summary>2026-11-24 (tester: two village kids at play fused into one body; two story listeners 0.008 u apart): through a village evening
        /// at play speed, no two settled people stay closer than 0.03 u for half a second.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Village_NobodyStandsInsideAnybody()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[apart] " + CampCheats.StageVillage(15.5f, false, 6, onlyCheats: false));
            yield return Frames(10);
            var pv = PeopleView.Find();
            pv.timeScale = 1f;
            var close = new Dictionary<(int, int), float>();
            var worst = new List<string>();
            float t0 = Time.realtimeSinceStartup;
            while (pv.Now.hour < 19f && Time.realtimeSinceStartup - t0 < 600f)
            {
                yield return null;
                var pts = new List<(int k, Vector2 p, string what)>();
                foreach (var a in pv.Activities)
                {
                    if (a.moving || pv.IsWalking(a.who) || a.kind == ActivityKind.Sleep || pv.Planner.StageOf(a.who) == LifeStage.Baby) continue;
                    if (pv.TryGetDollPosition(a.who, out var q)) pts.Add((a.who.Key, q, pv.Describe(a)));
                }
                var seenNow = new HashSet<(int, int)>();
                for (int i = 0; i < pts.Count; i++)
                    for (int j = i + 1; j < pts.Count; j++)
                    {
                        if ((pts[i].p - pts[j].p).magnitude >= 0.03f) continue;
                        var key = (Mathf.Min(pts[i].k, pts[j].k), Mathf.Max(pts[i].k, pts[j].k));
                        seenNow.Add(key);
                        close[key] = (close.TryGetValue(key, out float f) ? f : 0f) + Time.deltaTime;
                        if (close[key] >= 0.5f && worst.Count < 8) { worst.Add($"{pv.Now} {pts[i].what} | {pts[j].what}"); close[key] = -999f; }
                    }
                foreach (var k in new List<(int, int)>(close.Keys)) if (!seenNow.Contains(k) && close[k] > 0f) close[k] = 0f;
            }
            Debug.Log("[apart] " + (worst.Count == 0 ? "nobody inside anybody" : string.Join(" || ", worst)));
            CloseSlice();
            Assert.IsEmpty(worst, "two people inside each other for half a second: " + string.Join(" || ", worst));
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

        /// <summary>#744 (the user, 2026-11-24, options 3 and 4): frames of a ball game at the pond's shore whose missed throw lands in the water
        /// (the fetcher wades in, shakes it, runs back), then the same spot with the pond dried (the ball rolls down into the hollow). Explicit:
        /// the clip for the user's sign-off.</summary>
        [UnityTest, Explicit("Frames for the user's sign-off (#744)"), Timeout(900000)]
        public IEnumerator BallInThePond_AndTheDryHollow_Frames()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            Debug.Log("[744] " + CampCheats.StageCamp(11f, 4));
            Debug.Log("[744] " + EcoDebug.WaterBreaksNow());
            yield return new WaitForSeconds(12f);
            var pv = PeopleView.Find();
            var box = Object.FindAnyObjectByType<BoxView>();
            var rig = Camera.main.GetComponent<BoxCamera>();
            System.IO.Directory.CreateDirectory(Shots + "744/");
            // a shore point: dry land with drawn water 0.25-0.35 u away
            Vector2 shore = default, water = default; bool found = false;
            for (int c = 0; c < AquariumCiv.Sim.Settle.SiteMap.N && !found; c++)
            {
                float x = AquariumCiv.Sim.Settle.SiteMap.CXc(c), z = AquariumCiv.Sim.Settle.SiteMap.CZc(c);
                if (!box.WaterDrawnAt(x, z)) continue;
                for (int k = 0; k < 8 && !found; k++)
                {
                    var d = new Vector2(Mathf.Cos(k * Mathf.PI / 4f), Mathf.Sin(k * Mathf.PI / 4f));
                    var land = new Vector2(x, z) - d * 0.3f;
                    if (box.WaterDrawnAt(land.x, land.y) || !box.land.Inside(land, 0.3f) || box.WaterDrawnAt(land.x - d.x * 0.15f, land.y - d.y * 0.15f)) continue;
                    shore = land; water = new Vector2(x, z); found = true;
                }
            }
            Assert.IsTrue(found, "a shore point by the pond");
            var kids = pv.Activities.Where(a => pv.Planner.StageOf(a.who) == LifeStage.Kid).Select(a => a.who.Key).Take(3).ToList();
            Assert.AreEqual(3, kids.Count, "three kids");
            bool ball(BallGame.Throw th, float local) => local > th.pickStart + BallGame.Pick * 0.6f + 0.05f && local < th.pickEnd; // held, standing still
            IEnumerator Play(string tag, System.Func<BallGame.Throw, bool> want)
            {
                // a seed whose live game (Direct moves the centre to open ground and starts after a lead) has the wanted miss early on
                BallGame live = null; int seed = -1, k0 = -1;
                for (int sd = 1; sd < 300 && seed < 0; sd++)
                {
                    if (pv.KidPlay.Direct(PlayGame.Ball, kids, shore, 40f, sd) == null) continue;
                    if (!pv.KidPlay.TryCue(kids[0], pv.PoseClock, out _, out var kg, out _) || kg is not BallGame bg) continue;
                    for (int k = 0; k < 6; k++) if (want(bg.ThrowAt(k))) { live = bg; seed = sd; k0 = k; break; }
                }
                Assert.IsNotNull(live, tag + ": a seed with the wanted miss");
                var probe = live.ThrowAt(k0);
                rig.FlyTo(box.transform.TransformPoint(box.land.OnGround((shore.x + probe.restPos.x) * 0.5f, (shore.y + probe.restPos.y) * 0.5f)), Mathf.Max(rig.minZoom * 1.1f, 0.25f), 0.4f);
                for (int i = 0; i < 200 && rig.IsFlying; i++) yield return null;
                int f = 0;
                float nearest = 9f, worstJump = 0f; bool sank = false; Vector3? prevHeld = null;
                var fetcherRef = pv.Activities.First(a => a.who.Key == kids[probe.fetcher]).who;
                while (pv.PoseClock - live.t0 < probe.backAt + 1.5f)
                {
                    yield return null;
                    float local = pv.PoseClock - live.t0;
                    if (local > probe.launch - 0.4f && (f++ % 3) == 0) ScreenCapture.CaptureScreenshot(Shots + $"744/{tag}_{f:000}.png");
                    // what the tester watched for: the fetcher reaches the ball where it lies; a held ball never jumps
                    var ballGo = GameObject.Find("Play Ball");
                    if (ballGo == null || !pv.TryGetDollTransform(fetcherRef, out var doll)) continue;
                    var rootT = ballGo.transform.parent;
                    var dl = rootT.InverseTransformPoint(doll.position);
                    if (local > probe.land + 0.3f && local < probe.pickStart + 0.2f)
                        nearest = Mathf.Min(nearest, new Vector2(dl.x - probe.restPos.x, dl.z - probe.restPos.y).magnitude);
                    if (local > probe.pickStart && local < probe.pickEnd && box.WaterDrawnAt(dl.x, dl.z)) sank = true; // (the people's root is raised: x/z are the land's, y is not)
                    var bl = ballGo.transform.localPosition;
                    var held = ball(probe, local);
                    if (held && prevHeld.HasValue) worstJump = Mathf.Max(worstJump, new Vector2(bl.x - prevHeld.Value.x, bl.z - prevHeld.Value.z).magnitude);
                    prevHeld = held ? bl : (Vector3?)null;
                }
                Debug.Log($"[744] {tag}: seed {seed}, throw {k0}, rest {probe.restPos}, wet {probe.wet}, rolls {probe.rolls}; fetcher nearest {nearest:0.000} u, sank {sank}, worst held jump {worstJump:0.000} u");
                Assert.Less(nearest, 0.1f, tag + ": the fetcher reaches the ball where it lies");
                if (probe.wet) Assert.IsTrue(sank, tag + ": wades into the water for it");
                Assert.Less(worstJump, 0.05f, tag + ": the ball never jumps into the hands");
            }
            yield return Play("pond", th => th.miss && th.wet);
            Debug.Log("[744] " + EcoDebug.PondDries());
            yield return new WaitForSeconds(2f);
            yield return Play("hollow", th => th.miss && th.rolls);
            CloseSlice();
        }
    }
}
