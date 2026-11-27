// #692 people and things, part 1 (docs/design/animation-specs.md "Batch 3"): the camp's things get used. CampLife is PeopleView's
// director for the uses: each frame it decides which use starts (the D rules: life uses only borrow people from their free time, a
// work use plays inside the work's own block; how often by the view day's roll; one use per thing at a time; B11's 30 s gap), hands
// PeopleView each borrowed person's cue (CampScene parts), fires the effects, draws the small things (an ember on a twig, a berry,
// the spit), and runs the stew pot's day (B30: cold and set out, filled and lifted onto the fire, ladled, scraped and lifted off).
// The scenes themselves are pure builders in View/Beats/CampScenes1.cs. Cheats: the "#692" block in SliceDebugPanel.
using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        /// <summary>How often a use plays (D rules): <b>sometimes</b> about one view day in three; <b>often</b> most days.</summary>
        public const float Sometimes = 0.35f, Often = 0.85f, RabbitSpit = 0.5f;
        /// <summary>B11: the same life use never twice within this many seconds.</summary>
        public const float SameUseGap = 30f, SniffGap = 20f;
        /// <summary>How far a free person may be from the thing to be borrowed for it (box units).</summary>
        public const float BorrowReach = 2.5f;

        readonly PeopleView pv;
        readonly List<CampScene> scenes = new();
        readonly Dictionary<int, CampScene> byKey = new();
        readonly Dictionary<int, float> nextWarm = new();
        readonly HashSet<CampUse> doneToday = new();
        readonly Dictionary<CampUse, float> lastAt = new();
        long today = long.MinValue;
        float fxDone = float.NaN, lastWarm = -99f;
        int dipVersionSeen = -1;
        SimCore sim; LifePlanner planner; MealDirector meals; ViewTime now; float clock;

        public CampLife(PeopleView pv) { this.pv = pv; }

        public IReadOnlyList<CampScene> Scenes => scenes;
        /// <summary>The newest scene playing (AnimLab's "camp" follow).</summary>
        // the user's review of part 1: once the cheat's scene is over the camera holds (it jumped to whatever else played)
        public CampScene Latest => forced != null ? (scenes.Contains(forced) ? forced : null) : scenes.Count > 0 ? scenes[^1] : null;
        CampScene forced;
        Vector2? lastFocus;
        /// <summary>AnimLab: no use starts by itself (only the cheats), so a clip shows one use alone.</summary>
        public static bool OnlyCheats;

        /// <summary>Where the newest scene's people are drawn now, on average (AnimLab's "camp" follow); the scene's focus if none is.</summary>
        /// <summary>#692 p5 retakes (AnimLab's lead): seconds until the latest use's hit (<see cref="CampScene.a"/>; negative after it), null when
        /// none plays.</summary>
        public float? UntilA { get { var s = forced != null && scenes.Contains(forced) ? forced : null; return s == null ? (float?)null : s.t0 + s.a - clock; } } // the cheat's scene only (a natural one's past hit met the lead at once)

        public Vector2? FocusNow()
        {
            var s = Latest;
            if (s == null && RitualFocus() is Vector2 rf) return lastFocus = rf; // S-ritual, B76: the ritual's people, the telling at the fire
            if (s == null) return forced != null ? lastFocus : null; // the user's review: after a cheat's scene the camera stays where it was
            if (s.holdFocus) return lastFocus = s.focus;
            var sum = Vector2.zero; int n = 0;
            for (int i = 0; i < s.Count; i++)
                if (s.TryPart(s.keys[i], clock, out _, out _) && pv.DrawnAt(s.keys[i]) is Vector2 p) { sum += p; n++; }
            var lead = s.TryPart(s.keys[0], clock, out _, out _) || s.followReach > 0.6f ? pv.DrawnAt(s.keys[0]) : null; // #692 part 2: a far-following scene keeps on its lead in a pair beat (B32.d's hug)
            var c = lead ?? (n == 0 ? s.focus : sum / n);
            var d = c - s.focus;
            if (d.magnitude > s.followReach) c = s.focus + d.normalized * s.followReach;
            return lastFocus = Vector2.Lerp(s.focus, c, s.followReach > 0.6f ? 0.85f : 0.5f); // the thing, pulled toward the lead actor (a far-following scene: mostly the lead)
        }
        /// <summary>#783: where the newest scene's people are drawn now, and the scene's own thing (the camera frames both: a lead walking in
        /// from far must not pull the lean off the fire).</summary>
        public void CastDrawn(List<Vector2> into)
        {
            var s = Latest;
            if (s == null) return;
            into.Add(s.focus);
            for (int i = 0; i < s.Count; i++)
                if (s.TryPart(s.keys[i], clock, out _, out _) && pv.DrawnAt(s.keys[i]) is Vector2 p) into.Add(p);
        }
        public bool Playing(CampUse use) { foreach (var s in scenes) if (s.use == use) return true; return false; }
        /// <summary>Tests and the readout: how many times each use started this session.</summary>
        public readonly Dictionary<CampUse, int> Started = new();

        public void Reset()
        {
            scenes.Clear(); byKey.Clear(); nextWarm.Clear(); doneToday.Clear(); lastAt.Clear(); ResetTree(); ResetLand(); ResetAnimals(); ResetCamp(); ResetProps(); ResetDogs(); ResetVillage(); ResetStories();
            fxDone = float.NaN; potState = PotState.Cold; potDay = long.MinValue; spitDay = long.MinValue;
        }

        // ================================================================== the cue PeopleView asks for
        public bool TryCue(int key, float time, out CampPart part, out float into)
        {
            part = null; into = 0f;
            return byKey.TryGetValue(key, out var s) && s.TryPart(key, time, out part, out into);
        }

        public bool Busy(int key) => byKey.ContainsKey(key);
        /// <summary>Tests (B3 tester fixes): the use this person is in now ("-" none).</summary>
        public string UseOf(int key) => byKey.TryGetValue(key, out var s) ? s.use.ToString() : "-";
        /// <summary>A camp use draws this person now (its part covers this moment; tests: the work share).</summary>
        public bool Cued(int key) => byKey.TryGetValue(key, out var s) && s.TryPart(key, clock, out _, out _);

        // ================================================================== once a frame, before the dolls
        public void Step(SimCore sim, LifePlanner planner, IReadOnlyList<PersonActivity> acts, ViewTime now, float time, MealDirector meals)
        {
            this.sim = sim; this.planner = planner; this.meals = meals; this.now = now; clock = time;
            RunLater();
            if (now.day != today) { today = now.day; doneToday.Clear(); }
            for (int i = scenes.Count - 1; i >= 0; i--) if (time > scenes[i].End + 0.2f) scenes.RemoveAt(i);
            Index();
            if (planner != null) CutForMealsAndBed(acts); // B3 tester fixes: every use, as #695's village uses
            if (sim != null && planner != null) StepProps(acts, time); // #693: the choices' things (every era)
            if (sim != null && planner != null) StepRituals(acts, time); // S-ritual: the offering basket's look, the table's boards, purple hands, the boat
            StepTeller(acts); // B76: the Keeper's pick tells tonight's story
            if (sim != null && planner != null && sim.State.era != Era.Camp) StepVillage(acts, time); // #695: the village's and the town's things
            if (sim != null && planner != null) StepStories(acts, time); // #722: the story events' things (the fruit, the marks, the second stone), every era
            if (sim == null || planner == null || sim.State.era != Era.Camp) { StepPot(acts, time, false); return; }
            StepTree(acts, time); // #692 part 2: the Keeper's tree visits (no fire needed)
            StepLand(acts, time); // #692 part 3: grass, saplings, rocks, fallen branches
            StepAnimals(acts, time); // #692 part 4: frogs, deer, a rabbit kit, the ducks
            StepCamp(acts, time); // #692 part 5: the rest of the camp
            StepDogs(acts, time); // #694: the dogs and the wolves in the people's day
            bool fire = meals.HasFire && planner.FireNow != CampFireStage.None;
            StepPot(acts, time, fire);
            if (fire && !OnlyCheats) Trigger(acts, time);
            StepLadle(time);
            Index();
        }

        void Index()
        {
            byKey.Clear();
            foreach (var s in scenes) foreach (int k in s.keys) byKey[k] = s;
        }

        /// <summary>A cheat is starting a use now (its scene is the cheat's: no bedtime or meal check).</summary>
        bool forcing;

        // ================================================================== B3 tester fixes: meals and bedtime end a use
        /// <summary>The uses that are the meal's own beats (the pot, the ladle, the long table's meal, the spit's round, warm hands at the
        /// fire): mealtime does not end them.</summary>
        static bool MealUse(CampUse u) => u is CampUse.WarmHands or CampUse.Ladle or CampUse.PotFill or CampUse.PotOff or CampUse.PotPeek or CampUse.PotSniff
            or CampUse.BigPot or CampUse.TableMeal or CampUse.Spit;

        /// <summary>B55.a / B25.b: a wolf at night: the guard and the drive-off are the one use that may keep (or get) a grown-up up past bedtime.
        /// They go back to bed when it ends (their plan is Sleep, so they walk to the tent).</summary>
        static bool WakesForWolves(CampUse u) => u is CampUse.DogGuard or CampUse.WolfDriveOff;

        /// <summary>B3 tester fixes (the tester: camp uses held people through meals and bedtime): whoever's plan has gone to sleep (or a nap)
        /// or to eat leaves the use they are in, as #695's village uses do. Not a cheat's scene; the wolves' guard and drive-off keep their
        /// grown-ups up; the meal's own beats keep their eaters.</summary>
        void CutForMealsAndBed(IReadOnlyList<PersonActivity> acts)
        {
            bool cut = false;
            foreach (var a in acts)
            {
                bool sleep = a.kind is ActivityKind.Sleep or ActivityKind.Nap, eat = a.kind == ActivityKind.Eat && !OnlyCheats;
                if (!sleep && !eat || !byKey.TryGetValue(a.who.Key, out var s) || s == forced || StoryHeld(s)) continue; // #722: a held moment's scene keeps its people
                if (sleep && WakesForWolves(s.use) || eat && MealUse(s.use)) continue;
                int m = s.IndexOf(a.who.Key);
                if (m >= 0 && s.parts[m].Count > 0) { s.parts[m].Clear(); MealBedCut++; cut = true; }
            }
            if (cut) Index();
        }

        /// <summary>Tests: members a meal or bedtime took out of a camp use (B3 tester fixes).</summary>
        public int MealBedCut { get; private set; }
        /// <summary>Tests: natural uses not started because one of their people goes to bed or to eat before the use would end.</summary>
        public int LateStartsRefused { get; private set; }

        /// <summary>B3 tester fixes: no use starts for someone whose plan goes to sleep or to eat before it would end (the cut-off is each
        /// person's own bedtime and meal: a kid's poke at the fire at 19.5 h held them past bed).</summary>
        bool EndsInBedOrMeal(CampScene s)
        {
            if (WakesForWolves(s.use) || planner == null) return false;
            bool meal = MealUse(s.use);
            float scale = Mathf.Max(1f, pv.timeScale);
            foreach (int k in s.keys)
            {
                bool found = false; PersonRef who = default;
                foreach (var a in pv.Activities) if (a.who.Key == k) { who = a.who; found = true; break; }
                if (!found) continue;
                for (int i = 1; i <= 4; i++)
                {
                    var p = planner.Plan(who, LifeClock.Advance(now, s.Length * scale * i / 4f));
                    if (p.kind is ActivityKind.Sleep or ActivityKind.Nap || !meal && p.kind == ActivityKind.Eat) return true;
                }
            }
            return false;
        }

        void Add(CampScene s)
        {
            if (s == null) return;
            if (!forcing && !OnlyCheats && EndsInBedOrMeal(s)) { LateStartsRefused++; Why(s.use, "would run into bedtime or a meal"); return; } // B3 tester fixes
            // a person in two scenes: the new one wins (a cheat); the old one plays on without them
            foreach (int k in s.keys)
                if (byKey.TryGetValue(k, out var old) && old != s) { int m = old.IndexOf(k); if (m >= 0) old.parts[m].Clear(); }
            scenes.Add(s);
            foreach (var e in s.pairs) PairStage.Add(e); // #692 part 2: the hug at the end of a quiet visit
            Started[s.use] = Started.TryGetValue(s.use, out int n) ? n + 1 : 1;
            lastAt[s.use] = clock;
            Index();
        }

        bool ThingBusy(int thing) { if (thing == 0) return false; foreach (var s in scenes) if (s.thing == thing) return true; return false; }
        const int ThingFire = 1, ThingPot = 2, ThingPole = 3;

        float Roll(CampUse use) => BeatHash.U01((int)(today & 0x7fffffff), (int)use, 6920 + (sim != null ? sim.State.seed & 0xffff : 0));
        bool LifeOk(CampUse use) => !lastAt.TryGetValue(use, out float t) || clock - t > SameUseGap;
        float Hour => now.hour;

        // ================================================================== who is free (D rules: free time only)
        static bool FreeKind(ActivityKind k) => k is ActivityKind.SitOut or ActivityKind.Chat or ActivityKind.ListenStory or ActivityKind.Listen or ActivityKind.Sing
            or ActivityKind.SitTogether or ActivityKind.Visit or ActivityKind.Play;

        /// <summary>#791: view hours ahead a run-in keeps someone from being borrowed (a use holds them about this long; BarnChores held a stopper through her beat).</summary>
        const float RunInLookAhead = 1.5f;

        bool Free(in PersonActivity a, float time)
        {
            if (a.moving || a.enRoute || a.hunt != HuntPhase.None || a.tent != TentPhase.None || !FreeKind(a.kind)) return false;
            if (byKey.ContainsKey(a.who.Key) || meals.PokingNow(a.who.Key) || PairStage.TryCue(a.who.Key, time, out _, out _)) return false;
            if (PeopleView.StagedSpot != null && PeopleView.StagedSpot(a, now, out _, out _, out _)) return false;
            if (PeopleView.LifeSpot != null && PeopleView.LifeSpot(a, now, out _, out _, out _)) return false;
            if (PeopleView.StartHook != null && PeopleView.StartHook(a, out _)) return false; // a new-start scene has them
            if (planner != null && planner.RunInBetween(a.who, now.day, now.hour, now.hour + RunInLookAhead)) return false; // #791: about to meet someone on the way
            return true;
        }

        bool IsKid(in PersonActivity a) => planner.StageOf(a.who) == LifeStage.Kid;
        bool IsGrown(in PersonActivity a) => planner.StageOf(a.who) is LifeStage.Adult or LifeStage.Elder;

        Vector2? At(int key) => pv.DrawnAt(key);

        /// <summary>The cook seated at the fire (MealDirector's −1 none read as <see cref="Nobody"/>).</summary>
        int CookKey => meals.CookAtFire;

        /// <summary>The free person nearest <paramref name="to"/> within <paramref name="reach"/> who passes <paramref name="ok"/> (<see cref="Nobody"/> none: a crowd member's key is negative).</summary>
        int Nearest(IReadOnlyList<PersonActivity> acts, Vector2 to, float reach, Func<PersonActivity, bool> ok, ICollection<int> not = null, bool free = true)
        {
            int best = Nobody; float bd = reach * reach;
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (not != null && not.Contains(a.who.Key)) continue;
                if (free ? !Free(a, clock) : (byKey.ContainsKey(a.who.Key) || a.hunt != HuntPhase.None || a.tent != TentPhase.None)) continue;
                if (!ok(a) || !(At(a.who.Key) is Vector2 p)) continue;
                float d = (p - to).sqrMagnitude;
                if (d < bd) { bd = d; best = a.who.Key; }
            }
            return best;
        }

        Vector2? Stash => planner.Positions.TryGetPosition(new Place(PlaceKind.Stash, -1, 1), out float x, out float z) ? new Vector2(x, z) : (Vector2?)null;

        Vector2 CameraAcross()
        {
            var cam = Camera.main;
            if (cam == null) return Vector2.right;
            var r = cam.transform.right; var v = new Vector2(r.x, r.z);
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector2.right;
        }

        /// <summary>#695: AnimLab's own camera yaw (deg): the uses play on its side, not the game camera's (null: the game camera).</summary>
        public static float? LabYaw;
        /// <summary>#695: the yaw a use asks AnimLab's camera for (sails behind the watching kids; the well's rim from its gable end); null: none.</summary>
        public static float? LabCamYaw;
        /// <summary>The AnimLab yaw (deg) that looks along <paramref name="toCamDir"/> reversed: the camera stands on that side.</summary>
        public static float YawFor(Vector2 toCamDir) => Mathf.Atan2(-toCamDir.x, -toCamDir.y) * Mathf.Rad2Deg;

        Vector2 TowardCamera(Vector2 from)
        {
            if (LabYaw is float ly) { float r = ly * Mathf.Deg2Rad; return from + new Vector2(-Mathf.Sin(r), -Mathf.Cos(r)); }
            var cam = Camera.main;
            if (cam == null) return from + Vector2.down;
            var f = cam.transform.forward; var v = new Vector2(-f.x, -f.z);
            return from + (v.sqrMagnitude > 1e-4f ? v.normalized : Vector2.down);
        }

        // ================================================================== the triggers
        void Trigger(IReadOnlyList<PersonActivity> acts, float time)
        {
            var fire = meals.Fire;
            Season season = LifeClock.SeasonOf(now.day);
            bool evening = Hour >= 17f && Hour < 20.5f, meal = meals.Eaters > 0;

            // B29.a: warm hands, winter or snow, each sitter every 6-10 s, never two within 0.5 s
            if (season == Season.Winter || planner.WeatherOn(now.day) == Weather.Snow)
                foreach (int k in meals.Sitters)
                {
                    if (byKey.ContainsKey(k) || meals.PokingNow(k)) continue;
                    if (!nextWarm.TryGetValue(k, out float at)) { nextWarm[k] = time + BeatHash.Range(1f, 6f, k, 0, 6921); continue; }
                    if (time < at || time - lastWarm < 0.5f) continue;
                    Add(CampScenes1.WarmHands(k, time)); lastWarm = time;
                    nextWarm[k] = time + CampScenes1.WarmSeconds + BeatHash.Range(6f, 10f, k, (int)time, 6922);
                }

            if (meal) return; // never during a meal (the bowl round has the ring)

            // B29.d: the spit, the evening after a catch
            if (SpitTonight() && Hour >= 17.6f && Hour < 19.2f && !doneToday.Contains(CampUse.Spit) && !ThingBusy(ThingFire))
                TryStartSpit(acts, time, false);

            // B30.d: a grown-up passing the simmering pot sniffs and nods (often, at most one per 20 s)
            if (potState == PotState.Hot && Hour >= 16.5f && Hour < 20f && (!lastAt.TryGetValue(CampUse.PotSniff, out float ls) || time - ls > SniffGap))
            {
                var pot = meals.Pot;
                for (int i = 0; i < acts.Count; i++)
                {
                    var a = acts[i];
                    if (!a.moving || byKey.ContainsKey(a.who.Key) || !IsGrown(a) || a.hunt != HuntPhase.None || a.who.Key == CookKey) continue;
                    if (!(At(a.who.Key) is Vector2 p) || (p - pot).sqrMagnitude > 0.3f * 0.3f) continue;
                    if (Roll(CampUse.PotSniff) > Often) break;
                    Add(CampScenes1.PotSniff(a.who.Key, CookKey, p, pot, At(CookKey) ?? pot, time, SideOn(pot, p)));
                    break;
                }
            }

            // B30.c: kids peek in the simmering pot at dusk (sometimes)
            if (potState == PotState.Hot && evening && !doneToday.Contains(CampUse.PotPeek) && Roll(CampUse.PotPeek) < Sometimes && LifeOk(CampUse.PotPeek) && !ThingBusy(ThingPot))
            {
                int kid = Nearest(acts, meals.Pot, BorrowReach, a => IsKid(a));
                if (kid != Nobody) { Add(Thing(CampScenes1.PotPeek(kid, CookKey, At(kid).Value, At(CookKey) ?? meals.Pot + Vector2.down * 0.16f, meals.Pot, time), ThingPot)); doneToday.Add(CampUse.PotPeek); }
            }

            // B29.b: kids poke sticks in the fire (evenings, sometimes, a grown-up sitting by)
            if (evening && !doneToday.Contains(CampUse.KidPoke) && Roll(CampUse.KidPoke) < Sometimes && LifeOk(CampUse.KidPoke) && !ThingBusy(ThingFire))
                TryStartKidPoke(acts, time, false);

            // B29.c: a berry on a stick (late summer and autumn evenings, sometimes)
            if (evening && (season == Season.Autumn || season == Season.Summer && Hour > 0f && IsLateSummer()) && !doneToday.Contains(CampUse.BerryStick)
                && Roll(CampUse.BerryStick) < Sometimes && LifeOk(CampUse.BerryStick) && !ThingBusy(ThingFire))
                TryStartBerry(acts, time, false);

            // B31.a: a feather waits for the pole: tie it at dusk
            if (Hour >= 17f && Hour < 18.6f && sim.PendingPoleFeather() is int fi && fi >= 0 && sim.PoleFeathers[fi].month <= now.day && !ThingBusy(ThingPole))
                TryStartFeather(acts, time, fi, false);

            // B31.b: kids dare each other at the pole (by day, sometimes)
            if (Hour >= 9f && Hour < 16.5f && !doneToday.Contains(CampUse.PoleDare) && Roll(CampUse.PoleDare) < Sometimes && LifeOk(CampUse.PoleDare) && !ThingBusy(ThingPole))
                TryStartDare(acts, time, false, 1.5f);
        }

        bool IsLateSummer() => sim != null && sim.CalendarMonth >= 7;

        static CampScene Thing(CampScene s, int thing) { if (s != null) s.thing = thing; return s; }

        // ================================================================== starting each use (natural and forced)
        string TryStartKidPoke(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var fire = meals.Fire;
            int adult = Nobody;
            foreach (int k in meals.Sitters) if (!byKey.ContainsKey(k) && !meals.PokingNow(k)) { adult = k; break; }
            bool seat = false;
            if (adult == Nobody && force) { adult = Nearest(acts, fire, 9f, a => IsGrown(a), null, false); seat = true; }
            if (adult == Nobody) return "no grown-up sitting at the fire";
            int kid = Nearest(acts, fire, force ? 9f : BorrowReach, a => IsKid(a), new[] { adult }, !force);
            if (kid == Nobody) return "no kid near the fire ('Two kids join the camp' first)";
            var adultAt = seat ? fire + Rotate(TowardCamera(fire) - fire, -40f) * 0.33f /* front-left: the kid squats in front of the fire, clear of the pot */ : At(adult) ?? fire + Vector2.down * 0.32f;
            int pokes = BeatHash.Pick(2, kid, (int)today, 6923) == 0 ? 2 : 3;
            var sc = CampScenes1.KidPoke(kid, adult, At(kid).Value, adultAt, fire, pokes, time);
            if (seat)
            {   // the cheat: a grown-up comes and sits by the fire first (the scene waits for them), then is drawn on that seat
                int am = sc.IndexOf(adult);
                float walk = (At(adult).Value - adultAt).magnitude / LeadPace + 0.6f, first = sc.FirstFrom(am);
                float shift = Mathf.Max(0f, walk - first);
                sc.t0 += shift;
                if (shift > 0f) sc.Part(sc.IndexOf(kid), -shift, 0f, At(kid).Value, adultAt, DollStyle.Stand); // the kid waits for them
                foreach (var p in sc.parts[am]) p.keepSpot = false;
                var lead = sc.Part(am, -(sc.t0 - time), sc.FirstFrom(am), adultAt, fire, DollStyle.Sit, null, PropKind.None, LeadPace);
            }
            Add(Thing(sc, ThingFire));
            doneToday.Add(CampUse.KidPoke);
            return $"kid pokes the fire: kid {kid}, grown-up {adult}, {pokes} pokes";
        }

        string TryStartBerry(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var fire = meals.Fire;
            if (!(Stash is Vector2 stash)) return "no stash pile";
            bool grown = !force && BeatHash.Pick(4, (int)today, 0, 6924) == 0; // now and then a grown-up
            int who = Nearest(acts, fire, force ? 6f : BorrowReach, a => grown ? IsGrown(a) : IsKid(a), null, !force);
            if (who == Nobody && force) { who = Nearest(acts, fire, 6f, a => IsGrown(a), null, false); grown = true; }
            if (who == Nobody) return "nobody free near the fire";
            Add(Thing(CampScenes1.BerryStick(who, At(who).Value, stash, fire, time, !grown), ThingFire));
            doneToday.Add(CampUse.BerryStick);
            return "berry on a stick: " + who;
        }

        // ---- B29.d the spit
        long spitDay = long.MinValue; int spitKind; bool spitForced;

        /// <summary>A rabbit caught today (one evening in two: the other goes in the stew) or a deer.</summary>
        public bool SpitTonight()
        {
            if (sim == null || !sim.EcologyOn) return spitForced && spitDay == today;
            if (spitForced && spitDay == today) return true;
            var eco = sim.State.eco;
            if (eco.deerHuntDay == today && eco.deerHuntDone > 0) { spitKind = 1; return true; }
            if (eco.rabbitCatchDay == today && BeatHash.U01((int)(today & 0x7fffffff), 7, 6925) < RabbitSpit) { spitKind = 0; return true; }
            return false;
        }

        string TryStartSpit(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var fire = meals.Fire;
            int hunter = Nobody;
            var hunts = planner.HuntsOn(now.day);
            if (hunts != null) foreach (var h in hunts) if (h.caught && h.hunter.Key != 0) { hunter = h.hunter.Key; break; }
            if (hunter != Nobody && byKey.ContainsKey(hunter)) hunter = Nobody;
            if (hunter == Nobody) hunter = Nearest(acts, fire, force ? 6f : BorrowReach, a => IsGrown(a), null, !force);
            if (hunter == Nobody) return "no hunter near the fire";
            int turner = Nobody;
            foreach (int k in meals.Sitters) if (k != hunter && !byKey.ContainsKey(k)) { turner = k; break; }
            if (turner == Nobody) turner = Nearest(acts, fire, 6f, a => IsGrown(a), new[] { hunter }, !force);
            if (turner == Nobody) turner = hunter;
            int cook = CookKey != Nobody && CookKey != hunter && CookKey != turner ? CookKey : Nearest(acts, fire, 6f, a => IsGrown(a), new[] { hunter, turner }, false);
            if (cook == Nobody) cook = hunter;
            var s = CampScenes1.Spit(hunter, turner, cook, At(hunter).Value, At(turner) ?? fire, At(cook) ?? fire, fire, CameraAcross(), spitKind == 1, time, out float roundAt, out var stone);
            s.thing = ThingFire;
            spitRound = (s, roundAt, stone, hunter, cook);
            Add(s);
            doneToday.Add(CampUse.Spit); spitDay = today;
            return $"spit ({(spitKind == 1 ? "deer" : "rabbit")}): hunter {hunter}, turner {turner}, cook {cook}";
        }

        (CampScene s, float at, Vector2 stone, int hunter, int cook) spitRound;

        /// <summary>B29.d's round: at the round's time the cook goes round the ring with pieces (B9.b with a piece instead of a bowl);
        /// the hunter gets the first and holds it up; the kids crowd in and sniff.</summary>
        void StepSpitRound(IReadOnlyList<PersonActivity> acts, float time)
        {
            var r = spitRound;
            if (r.s == null || !scenes.Contains(r.s) || time < r.s.t0 + r.at) return;
            spitRound = default;
            var fire = meals.Fire;
            var keys = new List<int>(); var pos = new List<Vector2>();
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                int k = a.who.Key;
                if (k == r.cook || a.moving || a.place.kind != PlaceKind.Fire || !(At(k) is Vector2 p) || (p - fire).sqrMagnitude > 0.8f * 0.8f) continue;
                if (byKey.TryGetValue(k, out var sc) && sc != r.s) continue;
                keys.Add(k); pos.Add(p);
            }
            if (!keys.Contains(r.hunter) && At(r.hunter) is Vector2 hp) { keys.Insert(0, r.hunter); pos.Insert(0, hp); }
            // the hunter first
            int hi = keys.IndexOf(r.hunter);
            var served = new Dictionary<int, float>();
            var dips = new List<Vector2>();
            var stand = r.stone + (r.stone - fire).normalized * 0.09f;
            float start = time + 0.2f;
            var events = new List<PairEvent>();
            if (hi >= 0)
            {
                var e = PairStage.HandOver(r.s.kind, start, r.cook, r.hunter, stand, pos[hi], PropKind.MeatPiece, MealStage.PerPerson, 4f);
                if (e != null) { e.takerSeated = true; events.Add(e); served[r.hunter] = e.BeatStart + e.giverBeat.ContactAt; start = e.End; dips.Add(new Vector2(e.End, e.End)); keys.RemoveAt(hi); pos.RemoveAt(hi); }
            }
            events.AddRange(MealStage.Pass(r.s.kind + 7, start, r.cook, stand, fire, r.stone, keys, pos, served, dips, PropKind.MeatPiece, 0.6f));
            foreach (var e in events) PairStage.Add(e);
            int cm = r.s.IndexOf(r.cook);
            if (cm < 0) cm = r.s.Add(r.cook);
            foreach (var w in dips)
                if (w.y - w.x > 0.3f) r.s.Part(cm, w.x - r.s.t0, w.y - r.s.t0, stand, r.stone, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean += 20f; float k = Mathf.Clamp01((u - (l - 0.45f)) / 0.15f); p.armR = new Vector3(Mathf.Lerp(60f, 100f, k), 0f, 10f); });
            if (served.TryGetValue(r.hunter, out float got) && At(r.hunter) is Vector2 hat) CampScenes1.SpitHolder(r.s, r.hunter, got + 0.2f - r.s.t0, hat, fire);
            var kids = new List<int>(); var kat = new List<Vector2>();
            for (int n = 0; n < 2; n++)
            {
                int kid = Nearest(acts, r.stone, 3f, a => IsKid(a), kids, false);
                if (kid == Nobody || byKey.ContainsKey(kid)) break;
                kids.Add(kid); kat.Add(At(kid).Value);
            }
            CampScenes1.SpitKids(r.s, kids, kat, r.stone, time + 0.4f - r.s.t0);
            Index();
        }

        // ---- B31.a the feather; B31.b the dare
        int tieIndex = -1;

        string TryStartFeather(IReadOnlyList<PersonActivity> acts, float time, int index, bool force)
        {
            if (!sim.FeatherPoleSpot(out float px, out float pz)) return "no feather pole";
            var pole = new Vector2(px, pz);
            var f = sim.PoleFeathers[index];
            int chief = Nobody;
            string leaderRole = SimCore.LeaderOf(sim.State.era);
            foreach (var r in sim.State.roles) if (r.role == leaderRole && r.holderId >= 0) { chief = r.holderId; break; }
            bool Shown(int k) => k > 0 && At(k).HasValue && !byKey.ContainsKey(k) && !Tied(acts, k); // the user's review: the chief left mid-tie to pitch a tent
            if (!Shown(chief))
            {
                chief = Nobody; float age = -1f;
                foreach (var n in sim.State.notables) if (n.alive && n.age > age && n.age >= 18f && Shown(n.id)) { age = n.age; chief = n.id; }
            }
            if (chief == Nobody) chief = Nearest(acts, pole, 9f, a => IsGrown(a), null, false);
            if (chief == Nobody) return "nobody to tie it";
            var forKeys = new List<int>(); var forAt = new List<Vector2>();
            foreach (int id in new[] { f.forA, f.forB })
                if (id > 0 && id != chief && Shown(id) && sim.FindNotable(id) is Notable n && n.age >= 12f) { forKeys.Add(id); forAt.Add(At(id).Value); }
            if (f.kind == PoleFeatherKind.Village || forKeys.Count == 0)
                for (int i = 0; i < 2 && forKeys.Count < 2; i++)
                {
                    var not = new List<int>(forKeys) { chief };
                    int k = Nearest(acts, pole, 6f, a => IsGrown(a), not, !force);
                    if (k == Nobody) break;
                    forKeys.Add(k); forAt.Add(At(k).Value);
                }
            int kid = Nearest(acts, pole, force ? 6f : 3f, a => IsKid(a), null, !force);
            var s = CampScenes1.FeatherTie(chief, forKeys, kid, At(chief).Value, forAt, kid != Nobody ? At(kid).Value : pole, pole, TowardCamera(pole), time);
            s.thing = ThingPole; s.kind = index;
            Add(s);
            tieIndex = index;
            return $"feather ({KindWord(f.kind)}): chief {chief}, for {string.Join(",", forKeys)}";
        }

        /// <summary>Held by work the camp scenes can't take them from (a tent being pitched or struck, a hunt).</summary>
        static bool Tied(IReadOnlyList<PersonActivity> acts, int key)
        {
            for (int i = 0; i < acts.Count; i++) if (acts[i].who.Key == key) return acts[i].tent != TentPhase.None || acts[i].hunt != HuntPhase.None;
            return false;
        }

        public static string KindWord(int k) => k == PoleFeatherKind.Newborn ? "newborn, white" : k == PoleFeatherKind.Marriage ? "marriage, red" : "village, ochre";

        string TryStartDare(IReadOnlyList<PersonActivity> acts, float time, bool force, float reach)
        {
            if (!sim.FeatherPoleSpot(out float px, out float pz)) return "no feather pole";
            var pole = new Vector2(px, pz);
            var kids = new List<int>(); var at = new List<Vector2>();
            while (kids.Count < 4)
            {
                int k = Nearest(acts, pole, force ? 8f : reach, a => IsKid(a), kids, !force);
                if (k == Nobody) break;
                kids.Add(k); at.Add(At(k).Value);
            }
            if (kids.Count < 2) return $"only {kids.Count} kid(s) near the pole ('Two kids join the camp' first)";
            int adult = Nearest(acts, pole, 2.5f, a => IsGrown(a), kids, false);
            Add(Thing(CampScenes1.PoleDare(kids, at, adult, adult != Nobody ? At(adult).Value : pole, pole, TowardCamera(pole), time), ThingPole));
            doneToday.Add(CampUse.PoleDare);
            return $"pole dare: {kids.Count} kids";
        }

        // ================================================================== B30 the pot's day
        public enum PotState { Cold, Hot }
        PotState potState = PotState.Cold;
        long potDay = long.MinValue;
        bool potForcedCold;
        float potQuietSince = -1f;
        long supperDay = long.MinValue;
        public PotState Pot => potState;

        void StepPot(IReadOnlyList<PersonActivity> acts, float time, bool fire)
        {
            if (meals == null) return;
            if (now.day != potDay) { potDay = now.day; if (potState == PotState.Hot && meals.Eaters == 0 && !Playing(CampUse.PotOff)) potState = PotState.Cold; meals.BowlStack = false; }
            meals.PotAt = null; meals.PotLift = 0f;
            if (!fire) { meals.PotHot = true; meals.PotContent = 2; return; }
            if (meals.BigPotAt != null) { potState = PotState.Hot; meals.PotHot = true; meals.PotContent = 2; return; } // #693 (B54.h): the big pot cooks tonight
            bool potDayToday = sim == null || (sim.SupperOn(now.day) != SupperPick.Baskets && !SpitTonight());
            CampScene fill = null, off = null;
            foreach (var s in scenes) { if (s.use == CampUse.PotFill) fill = s; else if (s.use == CampUse.PotOff) off = s; }
            if (fill == null && off == null)
            {
                if (meals.Eaters > 0) { potQuietSince = -1f; if (Hour >= 16.5f) supperDay = now.day; } else if (potQuietSince < 0f) potQuietSince = time;
                if (potForcedCold) potState = PotState.Cold;
                else if (meals.Eaters > 0 && potState == PotState.Cold) potState = PotState.Hot; // a meal at the fire: the pot is on (a cheat's midday meal)
                else if (potDayToday && potState == PotState.Cold && Hour >= 18.4f && Hour < 21.5f && !doneToday.Contains(CampUse.PotOff)) potState = PotState.Hot; // nobody filled it on view
                else if (potState == PotState.Hot && meals.Eaters == 0 && (Hour < 16f || Hour >= 21.5f || !potDayToday)) potState = PotState.Cold;
                // B30.a: the cook's block at the fire starts with the fill (a work use: inside the cook's own block)
                if (!OnlyCheats && potDayToday && !potForcedCold && potState == PotState.Cold && CookKey != Nobody && Hour >= 15f && Hour < 18.4f && !doneToday.Contains(CampUse.PotFill) && meals.Eaters == 0)
                    TryStartFill(acts, time, false);
                // B30.e: after supper the pot is scraped and lifted off
                else if (!OnlyCheats && supperDay == now.day && potState == PotState.Hot && meals.Eaters == 0 && potQuietSince >= 0f && time - potQuietSince > 5f && Hour >= 18.8f && !doneToday.Contains(CampUse.PotOff))
                    TryStartOff(acts, time, false);
            }
            // draw it from the state and the scenes
            var hot = meals.PotHotSpot; var cold = meals.PotColdSpot;
            if (fill != null)
            {
                float s = time - fill.t0;
                float cf = fill.a, ct = fill.b; // a: water in, b: food in; the carry's own times ride in the scene's parts
                GetCarry(fill, out float c0, out float c1);
                meals.PotContent = s < cf ? 0 : s < ct ? 1 : 2;
                Carry(cold, hot, s, c0, c1);
                meals.PotHot = s >= c1 + 0.2f;
                if (s >= c1 + 0.2f) potState = PotState.Hot;
                return;
            }
            if (off != null)
            {
                float s = time - off.t0;
                GetCarry(off, out float c0, out float c1);
                meals.PotContent = s < c0 - 0.6f ? 2 : 0;
                Carry(hot, cold, s, c0, c1);
                meals.PotHot = s < c0 - 0.6f;
                meals.BowlStack = s >= off.b;
                potState = s < c0 ? PotState.Hot : PotState.Cold;
                return;
            }
            meals.PotHot = potState == PotState.Hot;
            meals.PotContent = potState == PotState.Hot ? 2 : 0;
            if (potState == PotState.Cold) meals.PotAt = cold;
        }

        void Carry(Vector2 from, Vector2 to, float s, float c0, float c1)
        {
            float u = Mathf.Clamp01((s - c0) / Mathf.Max(0.01f, c1 - c0));
            meals.PotAt = Vector2.Lerp(from, to, CampPose.Smooth(u));
            meals.PotLift = s > c0 - 0.5f && s < c1 + 0.3f ? 0.06f * Mathf.Clamp01(Mathf.Min((s - (c0 - 0.5f)) / 0.5f, (c1 + 0.3f - s) / 0.3f)) : 0f;
        }

        /// <summary>The carry's window is the one set in the builder; kept on the scene as its effect times (the jolt is 0.2 s after).</summary>
        static void GetCarry(CampScene s, out float c0, out float c1)
        {
            float jolt = 0f;
            foreach (var f in s.fx) if (f.kind == CampFxKind.Jolt) jolt = Mathf.Max(jolt, f.time);
            c1 = jolt - 0.2f; c0 = c1 - 1.2f;
        }

        string TryStartFill(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            int cook = CookKey;
            if (cook == Nobody || byKey.ContainsKey(cook)) cook = Nearest(acts, meals.Fire, 6f, a => IsGrown(a), null, false);
            if (cook == Nobody) return "no cook";
            int helper = Nearest(acts, meals.Fire, force ? 6f : 4f, a => IsGrown(a), new[] { cook }, !force);
            if (helper == Nobody) helper = Nearest(acts, meals.Fire, 9f, a => IsGrown(a), new[] { cook }, false);
            if (helper == Nobody && force) helper = Nearest(acts, meals.Fire, 9f, a => IsKid(a), new[] { cook }, false); // the cheat: a big kid carries the water
            if (helper == Nobody) return "no helper";
            if (!(Stash is Vector2 stash)) return "no stash";
            bool fish = sim != null && sim.StewOn(now.day) == StewKind.Fish;
            potState = PotState.Cold; potForcedCold = false; meals.BowlStack = false;
            var s = CampScenes1.PotFill(cook, helper, At(cook).Value, At(helper).Value, stash, meals.PotColdSpot, meals.PotHotSpot, fish, time, out _, out _);
            Add(Thing(s, ThingPot));
            doneToday.Add(CampUse.PotFill);
            return $"pot filled: cook {cook}, helper {helper}";
        }

        string TryStartOff(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            int cook = CookKey != Nobody && !byKey.ContainsKey(CookKey) ? CookKey : Nearest(acts, meals.Fire, force ? 6f : BorrowReach, a => IsGrown(a), null, !force);
            if (cook == Nobody) cook = Nearest(acts, meals.Fire, 6f, a => IsGrown(a), null, false);
            if (cook == Nobody) { potState = PotState.Cold; doneToday.Add(CampUse.PotOff); return "nobody to lift it: cold"; }
            int helper = Nearest(acts, meals.Fire, 6f, a => IsGrown(a), new[] { cook }, !force);
            int kid = Nearest(acts, meals.Fire, 6f, a => IsKid(a), new[] { cook, helper }, !force);
            var s = CampScenes1.PotOff(cook, helper, kid, At(cook).Value, helper != Nobody ? At(helper).Value : meals.Fire, kid != Nobody ? At(kid).Value : meals.Fire, meals.PotHotSpot, meals.PotColdSpot, time, out _, out _);
            Add(Thing(s, ThingPot));
            doneToday.Add(CampUse.PotOff);
            return $"pot off: cook {cook}, helper {helper}, kid {kid}";
        }

        // ---- B30.b the ladle: a dip at the pot before each hand-over of the bowl round
        void StepLadle(float time)
        {
            if (meals.DipVersion == dipVersionSeen) return;
            dipVersionSeen = meals.DipVersion;
            if (meals.Dips.Count == 0 || meals.DipGiver == 0) return;
            var s = new CampScene(CampUse.Ladle, 0f, meals.PotHotSpot, 0);
            int m = s.Add(meals.DipGiver);
            var stand = meals.DipStand; var pot = meals.PotHotSpot;
            foreach (var w in meals.Dips) DipParts(s, m, w.x, w.y, stand, pot);
            if (s.parts[m].Count > 0) Add(s);
        }

        /// <summary>B30.b (the user's review: "the bowls are not getting filled?"): one dip window [from, to] (scene s): walk back to the pot with
        /// the empty bowl, then the dip (CampScenes1.DipPose): the ladle in, the scoop, the lift, the pour into the bowl, which is full (a bowl
        /// of stew) from the pour on; a swell of steam at the scoop.</summary>
        static void DipParts(CampScene s, int m, float from, float to, Vector2 stand, Vector2 pot)
        {
            if (to - from <= 0.05f) return;
            float dip = CampScenes1.DipSeconds, d0 = to - dip;
            if (d0 > from + 0.05f) s.Part(m, from, d0, stand, pot, DollStyle.Stand, null, PropKind.BowlEmpty, 0.5f);
            float a0 = Mathf.Max(from, d0), pour = d0 + CampScenes1.PourAt;
            if (pour > a0)
            {
                float off = a0 - d0;
                s.Part(m, a0, pour, stand, pot, DollStyle.Stand, (ref DollPose p, float u, float l) => CampScenes1.DipPose(ref p, u + off, dip), PropKind.BowlEmpty, 0.5f).dipFrom = off;
            }
            float b0 = Mathf.Max(a0, pour), boff = b0 - d0;
            s.Part(m, b0, to, stand, pot, DollStyle.Stand, (ref DollPose p, float u, float l) => CampScenes1.DipPose(ref p, u + boff, dip), PropKind.Bowl, 0.5f).dipFrom = boff;
            s.Fx(d0 + 0.5f, CampFxKind.PotSteam, new Vector3(pot.x, MealDirector.PotTop, pot.y), 0.6f);
        }

        /// <summary>The user's review of B30.d: across the camera's view from <paramref name="pot"/> (side on, so the body never hides the pot), the side
        /// nearer <paramref name="from"/>.</summary>
        Vector2 SideOn(Vector2 pot, Vector2 from)
        {
            var toCam = (TowardCamera(pot) - pot).normalized;
            var across = new Vector2(-toCam.y, toCam.x);
            if (Vector2.Dot(across, from - pot) < 0f) across = -across;
            return (across + toCam * 0.25f).normalized;
        }

        /// <summary>B30.b: where the bowl-giver stands to dip: <see cref="DipStandOff"/> from the pot, beside it as the camera sees it (side on, so
        /// the body never hides the dip), on the side away from the fire, a little toward the camera.</summary>
        Vector2 DipStandAt(Vector2 pot, Vector2 fire)
        {
            var toCam = (TowardCamera(pot) - pot).normalized;
            var across = new Vector2(-toCam.y, toCam.x);
            if (Vector2.Dot(across, pot - fire) < 0f) across = -across;
            return pot + (across + toCam * 0.25f).normalized * DipStandOff;
        }
        /// <summary>The bowl-giver stands this far from the pot's middle to dip (the rim ~0.045, a grown-up ~0.035, an arm 0.075 and a lean).</summary>
        public const float DipStandOff = 0.11f;
        /// <summary>Tests and the probe: frames the ladle has been in a hand.</summary>
        public int LadleFrames { get; private set; }

        /// <summary>B30.b: the pot's ladle in the bowl-giver's right hand during a dip: hanging from the fist into the pot, tipped over the
        /// bowl at the pour.</summary>
        void DrawLadle(Transform root, float time)
        {
            if (meals == null) return;
            foreach (var s in scenes)
            {
                if (s.use != CampUse.Ladle || s.Count == 0) continue;
                int key = s.keys[0];
                if (!s.TryPart(key, time, out var part, out float into) || part.dipFrom < 0f) continue;
                float dt = into + part.dipFrom;
                if (dt < CampScenes1.LadleFrom || dt > CampScenes1.LadleTo) continue;
                var v = pv.ViewOf(key);
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                var hand = v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
                var q = v.transform.rotation * Quaternion.Euler(0f, 0f, -CampScenes1.LadleTilt(dt));
                meals.HoldLadle(hand - q * Vector3.up * (LadleGrip * root.lossyScale.y), q);
                LadleFrames++;
                return;
            }
        }

        /// <summary>The fist holds the ladle this far up its handle from the scoop (box units; the ladle is 0.09 long).</summary>
        public const float LadleGrip = 0.075f;

        // ================================================================== after the dolls: effects and the drawn things
        readonly List<Transform> tipPool = new();
        readonly Dictionary<int, Mesh> tipMeshes = new();
        GameObject spitGo; Transform spitStick1, spitStick2, spitBar, spitMeat; Mesh[] meatMeshes; int meatKindBuilt = -1;

        public void AfterDolls(Transform root, Material material, float time, Func<float, float, float> groundAt)
        {
            if (root == null) return;
            if (meals != null && planner != null) StepSpitRound(pv.Activities, time);
            FireFx(root, time, groundAt);
            DrawTips(root, material, time);
            DrawSpit(root, material, time, groundAt);
            DrawLadle(root, time);
            DrawTree(root, time); // #692 part 2: the newborn lifted, the glow, the charm held back until it is tied, the hush
            DrawLand(root, material, time, groundAt); // #692 part 3: the boulders and what the land's uses leave
            DrawAnimals(root, material, time, groundAt); // #692 part 4: the frog in the hands, the kit, the crumbs
            DrawCamp(root, material, time, groundAt); // #692 part 5: lanterns, toys, strips, gifts, boats, the swing, crowns
            DrawProps(root, material, time, groundAt); // #693: the basket tower, the split log, the ladder, the flour
            DrawDogs(root, material, time, groundAt); // #694: the dogs, the visiting wolf, the fetch stick
            DrawVillage(root, material, time, groundAt); // #695: the scarecrow, a home's things, the ladder, the buckets, the grid, the posts
            DrawRituals(root, material, time, groundAt); // S-ritual: the log heap, the ladder, the board, the small basket, the empty basket carried, the rod on the boat
            DrawStories(root, material, time, groundAt); // #722: the fruit on the tree, the marks on its trunk, the second stone rolled in
        }

        void FireFx(Transform root, float time, Func<float, float, float> groundAt)
        {
            float from = float.IsNaN(fxDone) || time - fxDone > 1f || time < fxDone ? time - 1f / 30f : fxDone;
            fxDone = time;
            foreach (var s in scenes)
                foreach (var f in s.fx)
                {
                    float at = s.t0 + f.time;
                    if (at <= from || at > time) continue;
                    Fire(root, s, f, groundAt);
                }
        }

        void Fire(Transform root, CampScene s, in CampFx f, Func<float, float, float> groundAt)
        {
            Vector3 world;
            if (f.anchor == CampAnchor.Ground) world = root.TransformPoint(new Vector3(f.at.x, groundAt(f.at.x, f.at.z) + f.at.y, f.at.z));
            else
            {
                if (f.member < 0 || f.member >= s.Count) return;
                var v = pv.ViewOf(s.keys[f.member]);
                if (v == null || !v.gameObject.activeInHierarchy) return;
                world = Anchor(v, f.anchor) + Vector3.up * (f.at.y * root.lossyScale.y);
            }
            var o = PuffOptions.Default; if (f.count > 0) o.count = f.count;
            switch (f.kind)
            {
                case CampFxKind.Sparks: o.fire = true; HitFx.Puff(PuffKind.Sparks, world, Vector3.up, f.scale, o); break;
                case CampFxKind.Flare: o.fire = true; HitFx.Puff(PuffKind.Sparks, world + Vector3.up * 0.02f * root.lossyScale.y, Vector3.up, f.scale * 1.3f, o); break; // the flare stand-in, as the thrown log's (#370)
                case CampFxKind.Steam: HitFx.Puff(PuffKind.Steam, world, Vector3.up, f.scale, o); break;
                case CampFxKind.Dust: o.groundY = world.y; HitFx.Puff(PuffKind.Dust, world, Vector3.up, f.scale, o); break;
                case CampFxKind.Droplets: HitFx.Puff(PuffKind.Droplets, world, Vector3.up, f.scale, o); break;
                case CampFxKind.Ring: HitFx.Puff(PuffKind.Ring, world, Vector3.up, f.scale, o); break;
                case CampFxKind.Grass: HitFx.Puff(PuffKind.Grass, world, Vector3.up, f.scale, o); break;
                case CampFxKind.Jolt:
                    if (s.use is CampUse.FeatherTie or CampUse.PoleDare) ConsequencePropsView.FlutterPole(s.use == CampUse.FeatherTie ? 1.6f : 0.8f);
                    else if (s.use is CampUse.PotFill or CampUse.PotOff) HitFx.Puff(PuffKind.Dust, world, Vector3.up, 0.4f, o);
                    break;
                case CampFxKind.PotSteam: meals?.SteamBurst(f.scale > 0f ? f.scale : 1f); break;
                case CampFxKind.Leaves: o.count = Mathf.Max(1, f.count); HitFx.Puff(PuffKind.Leaves, world, Vector3.down, f.scale, o); break;
                case CampFxKind.TreeShake: HitFx.Shake(ShakeTarget.Tree, world, CameraAcross3(root), 0f); break;
                case CampFxKind.Glow: TreeGlow(world, time: clock); break;
                case CampFxKind.Chips: HitFx.Puff(PuffKind.Chips, world, Vector3.up, f.scale, o); break; // #692 part 3
                case CampFxKind.Whistle: o.count = Mathf.Clamp(f.count, 1, 2); HitFx.Puff(PuffKind.Grass, world, Vector3.up, 0.25f, o); break; // #692 p3 (the user's review: a grey disc hid the kid): a tiny grass flick at the hands
                case CampFxKind.Mark: LandMark(s, f); break; // #692 part 3
                case CampFxKind.Perk: HitFx.Shake(ShakeTarget.Tree, world, CameraAcross3(root), 0f, true); break; // #692 part 3 (B34.a)
                case CampFxKind.Clash: // #692 part 3 (B36.a): the white flash at the cross and a light wood knock
                    HitFx.Puff(PuffKind.Sparks, world, Vector3.up, f.scale, o);
                    AudioDirector.Instance?.Play(AquariumCiv.Audio.CueLibrary.KnockBack, 0f, 0.6f, 0f);
                    break;
                case CampFxKind.Chime: break; // [est] a soft chime: no CC0 chime in the project yet (none played)
                case CampFxKind.WaterRing: case CampFxKind.WaterDrops: case CampFxKind.Leave: case CampFxKind.Creak: case CampFxKind.BushSway: FireCamp(root, s, f, world); break; // #692 part 5
                case CampFxKind.Prop: FireProp(s, f); break; // #693
                case CampFxKind.Village: FireVillage(s, f); break; // #695
            }
            if (s.use == CampUse.FeatherTie && f.kind == CampFxKind.Jolt && sim != null && s.kind >= 0) { sim.MarkPoleFeatherTied(s.kind); s.kind = -1; }
        }

        static Vector3 Anchor(PegDollView v, CampAnchor a)
        {
            switch (a)
            {
                case CampAnchor.Head: return v.transform.TransformPoint(new Vector3(0f, PegDoll.HeadY * v.scale, 0f));
                case CampAnchor.TwigTip:
                    if (v.prop != null && v.prop.gameObject.activeInHierarchy) return v.prop.TransformPoint(new Vector3(0f, 0.13f, 0f));
                    goto default;
                default: return v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
            }
        }

        void DrawTips(Transform root, Material material, float time)
        {
            int used = 0;
            foreach (var s in scenes)
                foreach (var tip in s.tips)
                {
                    float u = time - s.t0;
                    if (u < tip.from || u >= tip.to || tip.member >= s.Count) continue;
                    if (tip.kind >= CampTipKind.FlatStone) continue; // #692 part 5: drawn by CampLife.Camp
                    var v = pv.ViewOf(s.keys[tip.member]);
                    if (v == null || !v.gameObject.activeInHierarchy) continue;
                    float k = Mathf.Clamp01((u - tip.from) / Mathf.Max(0.01f, tip.to - tip.from));
                    float grow = Mathf.Lerp(1f, tip.grow, k), dark = tip.darken * k;
                    var t = Tip(root, material, used++);
                    int tint = Mathf.RoundToInt(dark * 4f);
                    t.GetComponent<MeshFilter>().sharedMesh = TipMesh(tip.kind, tint);
                    float sc0 = pv.ScaleOf(s.keys[tip.member], out float sc) ? sc : 1f;
                    if (tip.kind is CampTipKind.Leaf or CampTipKind.Flower or CampTipKind.Charm) // #692 part 2
                    {
                        PlaceTreeTip(t, v, tip, u, root, sc0);
                        t.gameObject.SetActive(true);
                        continue;
                    }
                    if (tip.kind >= CampTipKind.Stone) // #692 part 3
                    {
                        PlaceLandTip(t, v, tip, u, root, sc0);
                        t.gameObject.SetActive(true);
                        continue;
                    }
                    t.localRotation = Quaternion.identity;
                    var at = Anchor(v, CampAnchor.TwigTip);
                    t.position = at;
                    float size = (tip.kind == CampTipKind.Ember ? 0.018f : 0.022f) * grow * sc0 * (tip.scale > 0f ? tip.scale : 1f); // #722: the first fruit, bigger
                    if (tip.kind == CampTipKind.Ember) size *= 0.8f + 0.25f * Mathf.Sin(time * 11f);
                    t.localScale = Vector3.one * size;
                    t.gameObject.SetActive(true);
                }
            for (int i = used; i < tipPool.Count; i++) if (tipPool[i] != null) tipPool[i].gameObject.SetActive(false);
        }

        Transform Tip(Transform root, Material material, int i)
        {
            while (i < tipPool.Count && tipPool[i] == null) tipPool.RemoveAt(i); // the box was rebuilt: its tips went with it
            while (tipPool.Count <= i)
            {
                var go = new GameObject("Camp tip") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tipPool.Add(go.transform);
            }
            return tipPool[i];
        }

        /// <summary>A unit ball (r 1) in the tip's colour: the ember a hot orange; the berry red, darkening to a burnt purple-brown in 4 steps.</summary>
        Mesh TipMesh(CampTipKind kind, int tint)
        {
            int key = (int)kind * 10 + tint;
            if (tipMeshes.TryGetValue(key, out var m) && m != null) return m;
            Color c = kind == CampTipKind.Ember ? new Color(1f, 0.55f, 0.12f)
                : Color.Lerp(new Color(0.78f, 0.12f, 0.22f), new Color(0.28f, 0.08f, 0.10f), tint / 4f);
            var mb = new MeshBuilder(6926 + key);
            if (TreeTipMesh(kind, mb) || LandTipMesh(kind, mb)) { m = mb.ToMesh("Camp_Tip_" + key); m.hideFlags = HideFlags.DontSave; return tipMeshes[key] = m; }
            mb.Ico(Vector3.zero, 1f, Vector3.one, c, 0.05f);
            m = mb.ToMesh("Camp_Tip_" + key); m.hideFlags = HideFlags.DontSave;
            return tipMeshes[key] = m;
        }

        // ---- the spit (B29.d): two forked sticks, the bar, the meat that browns and turns
        void DrawSpit(Transform root, Material material, float time, Func<float, float, float> groundAt)
        {
            CampScene s = null;
            foreach (var x in scenes) if (x.use == CampUse.Spit) s = x;
            if (s == null || meals == null || !meals.HasFire) { if (spitGo != null) spitGo.SetActive(false); return; }
            if (spitGo == null)
            {
                spitGo = new GameObject("Camp spit") { hideFlags = HideFlags.DontSave };
                spitGo.transform.SetParent(root, false);
                var stick = Piece(SpitMeshes.Stick()); var bar = Piece(SpitMeshes.Bar());
                spitStick1 = Mk("Spit stick", stick, material); spitStick2 = Mk("Spit stick", stick, material);
                spitBar = Mk("Spit bar", bar, material);
                spitMeat = Mk("Spit meat", null, material); spitMeat.SetParent(spitBar, false);
            }
            if (meatKindBuilt != s.kind) { meatMeshes = SpitMeshes.Meat(s.kind == 1); meatKindBuilt = s.kind; }
            spitGo.SetActive(true);
            float u = time - s.t0;
            var dusts = new List<float>();
            foreach (var f in s.fx) if (f.kind == CampFxKind.Dust) dusts.Add(f.time);
            var fire = meals.Fire; var across = CameraAcross();
            Vector2 p1 = fire + across * CampScenes1.SpitHalf, p2 = fire - across * CampScenes1.SpitHalf;
            float yaw = Mathf.Atan2(across.x, across.y) * Mathf.Rad2Deg;
            Place(spitStick1, p1, 0f, yaw, groundAt); Place(spitStick2, p2, 0f, yaw, groundAt);
            spitStick1.gameObject.SetActive(dusts.Count > 0 && u >= dusts[0]);
            spitStick2.gameObject.SetActive(dusts.Count > 1 && u >= dusts[1]);
            float layAt = s.a, liftAt = s.b;
            bool barOn = u >= layAt;
            spitBar.gameObject.SetActive(barOn);
            if (!barOn) return;
            Vector2 at = fire; float h = 0.1f;
            if (liftAt > 0f && u >= liftAt)
            {
                var stone = spitRound.s == s ? spitRound.stone : fire + new Vector2(-across.y, across.x) * 0.3f + across * 0.08f;
                float k = CampPose.Smooth((u - liftAt) / 1.2f);
                at = Vector2.Lerp(fire, stone, k); h = Mathf.Lerp(0.1f, 0.025f, k) + 0.04f * Mathf.Sin(k * Mathf.PI);
            }
            Place(spitBar, at, h, yaw, groundAt);
            float turnFrom = layAt + 0.3f;
            float brown = Mathf.Clamp01((u - turnFrom) / CampScenes1.TurnSeconds);
            spitMeat.GetComponent<MeshFilter>().sharedMesh = meatMeshes[Mathf.Clamp(Mathf.RoundToInt(brown * (meatMeshes.Length - 1)), 0, meatMeshes.Length - 1)];
            bool turning = u >= turnFrom && u < turnFrom + CampScenes1.TurnSeconds;
            float spin = turning ? (u - turnFrom) / CampScenes1.MeatTurn * 360f : 0f;
            spitMeat.localRotation = Quaternion.Euler(0f, 0f, spin);
            spitMeat.localScale = Vector3.one * MeatScale; // at the props' scale: the 0.05 u rabbit read as a speck over the flames
            spitMeat.gameObject.SetActive(true);
        }

        static Mesh Piece(Mesh m) => m;
        /// <summary>The meat on the spit is drawn this much bigger than life, as every prop (1.5, life-in-the-box §5).</summary>
        public const float MeatScale = 1.5f;

        Transform Mk(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(spitGo.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        void Place(Transform t, Vector2 at, float h, float yaw, Func<float, float, float> groundAt)
        {
            t.localPosition = new Vector3(at.x, groundAt(at.x, at.y) + h, at.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = Vector3.one;
        }

        public void Destroy()
        {
            foreach (var t in tipPool) if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            tipPool.Clear();
            if (spitGo != null) UnityEngine.Object.Destroy(spitGo);
            spitGo = null;
            DestroyLand();
            DestroyAnimals();
            DestroyCamp();
            DestroyProps();
            DestroyDogs();
            DestroyVillage();
            DestroyStories();
        }

        // ================================================================== cheats (SliceDebugPanel "#692")
        /// <summary>Starts a use now with the people nearest the thing (D rules: "a cheat starts the use now"). Returns a status line.</summary>
        public string Force(CampUse use)
        {
            int before = scenes.Count;
            forcing = true;
            string r;
            try { r = IsStoryUse(use) ? ForceStory(use) : IsRitualUse(use) ? ForceRitual(use) : IsVillageUse(use) ? ForceVillage(use) : IsDogUse(use) ? ForceDog(use) : IsPropUse(use) ? ForceProp(use) : IsTreeUse(use) ? ForceTree(use) : IsLandUse(use) ? ForceLand(use) : IsAnimalUse(use) ? ForceAnimal(use) : IsCampUse(use) ? ForceCamp(use) : ForceUse(use); }
            finally { forcing = false; }
            if (scenes.Count > before) forced = scenes[^1];
            return r;
        }

        string ForceUse(CampUse use)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            var acts = pv.Activities; float time = clock;
            if (!meals.HasFire && use != CampUse.PoleDare && use != CampUse.FeatherTie) return "no fire in the camp";
            switch (use)
            {
                case CampUse.WarmHands: return ForceWarm(acts, time);
                case CampUse.KidPoke: return TryStartKidPoke(acts, time, true);
                case CampUse.BerryStick: return TryStartBerry(acts, time, true);
                case CampUse.Spit: return TryStartSpit(acts, time, true);
                case CampUse.PotFill: if (Playing(CampUse.PotFill)) return "filling already"; doneToday.Remove(CampUse.PotFill); return TryStartFill(acts, time, true);
                case CampUse.PotOff: potState = PotState.Hot; doneToday.Remove(CampUse.PotOff); return TryStartOff(acts, time, true);
                case CampUse.PotPeek:
                {
                    potState = PotState.Hot; potForcedCold = false;
                    var hotPot = meals.PotHotSpot; meals.PotAt = null; // the user's review: the cheat aimed at the cold spot, 0.15 u out
                    int kid = Nearest(acts, hotPot, 8f, a => IsKid(a), null, false);
                    if (kid == Nobody) return "no kid on view ('Two kids join the camp' first)";
                    int cook = CookKey;
                    if (cook == Nobody) cook = Nearest(acts, hotPot, 6f, a => IsGrown(a), new[] { kid }, false);
                    var cookAt = cook != Nobody ? At(cook) ?? meals.CookSpot(hotPot + Vector2.down) : meals.CookSpot(hotPot + Vector2.down);
                    if (cook != Nobody && CookKey != cook) { var c = Stand(cook, cookAt, meals.CookSpot(hotPot + Vector2.down), hotPot, time, 9f, DollStyle.Stir, PropKind.Stick); cookAt = c; }
                    Add(Thing(CampScenes1.PotPeek(kid, cook, At(kid).Value, cookAt, hotPot, time + (cook != Nobody && CookKey != cook ? 1.5f : 0f)), ThingPot));
                    return "kids peek in the pot";
                }
                case CampUse.PotSniff:
                {
                    potState = PotState.Hot; potForcedCold = false;
                    var hotPot = meals.PotHotSpot; meals.PotAt = null; // the user's review: at the pot's cooking spot, right at its rim
                    int g = Nearest(acts, hotPot, 8f, a => IsGrown(a) && a.who.Key != CookKey, null, false);
                    if (g == Nobody) return "no grown-up on view";
                    Add(Thing(CampScenes1.PotSniff(g, CookKey, At(g).Value, hotPot, At(CookKey) ?? hotPot, time, SideOn(hotPot, At(g).Value)), ThingPot));
                    return "someone sniffs the pot";
                }
                case CampUse.FeatherTie:
                {
                    int fi = sim.PendingPoleFeather();
                    if (fi < 0) return "no feather waiting: use 'Feather pole: tie a feather'";
                    return TryStartFeather(acts, time, fi, true);
                }
                case CampUse.PoleDare: return TryStartDare(acts, time, true, 8f);
                case CampUse.Ladle: return ForceLadle(acts, time);
            }
            return "not a camp use: " + use;
        }

        /// <summary>A cheat's helper: walk someone to a spot and hold a move there for <paramref name="hold"/> s (their own little scene).</summary>
        Vector2 Stand(int key, Vector2 from, Vector2 to, Vector2 face, float time, float hold, DollStyle style, PropKind prop)
        {
            var s = new CampScene(CampUse.None, time, to);
            int m = s.Add(key);
            float w = Mathf.Clamp((to - from).magnitude / CampScenes1.Walk, 0.3f, 6f);
            s.Part(m, 0f, w, to, face, style, null, prop, CampScenes1.Walk);
            s.Part(m, w, w + hold, to, face, style, null, prop);
            Add(s);
            return to;
        }

        /// <summary>B29.a's cheat: up to three grown-ups sit down round the fire and warm their hands (the season is set by the panel).</summary>
        string ForceWarm(IReadOnlyList<PersonActivity> acts, float time)
        {
            var fire = meals.Fire;
            var who = new List<int>();
            while (who.Count < 3)
            {
                int k = Nearest(acts, fire, 9f, a => IsGrown(a), who, false);
                if (k == Nobody) break;
                who.Add(k);
            }
            if (who.Count == 0) return "no grown-up on view";
            var camFacing = TowardCamera(fire) - fire;
            for (int i = 0; i < who.Count; i++)
            {
                var spot = fire + Rotate(camFacing, -50f + 50f * i) * 0.3f;
                var s = new CampScene(CampUse.WarmHands, time, fire, 0);
                int m = s.Add(who[i]);
                float w = Mathf.Clamp(((At(who[i]) ?? spot) - spot).magnitude / CampScenes1.Walk, 0.4f, 7f);
                s.Part(m, 0f, w, spot, fire, DollStyle.Sit, null, PropKind.None, CampScenes1.Walk);
                float start = w + 0.6f + 1.1f * i;
                s.Part(m, w, start, spot, fire, DollStyle.Sit);
                s.Part(m, start, start + CampScenes1.WarmSeconds, spot, fire, DollStyle.Sit, CampScenes1.WarmPose);
                s.Fx(start + CampScenes1.WarmSeconds - 0.2f, CampFxKind.Steam, new Vector3(0f, 0.01f, 0f), 0.3f, 1, CampAnchor.Head, m);
                s.Part(m, start + CampScenes1.WarmSeconds, start + CampScenes1.WarmSeconds + 4f, spot, fire, DollStyle.Sit);
                s.Part(m, start + CampScenes1.WarmSeconds + 4f, start + CampScenes1.WarmSeconds * 2f + 4f, spot, fire, DollStyle.Sit, CampScenes1.WarmPose);
                s.Fx(start + CampScenes1.WarmSeconds * 2f + 3.8f, CampFxKind.Steam, new Vector3(0f, 0.01f, 0f), 0.3f, 1, CampAnchor.Head, m);
                Add(s);
            }
            return $"warm hands: {who.Count} at the fire";
        }

        /// <summary>A cheat's lead-in: a member walks from <paramref name="from"/> to <paramref name="to"/> and holds <paramref name="style"/> there until
        /// the scene first needs them.</summary>
        const float LeadPace = 0.35f;

        static Vector2 Rotate(Vector2 v, float deg) { v = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.down; float r = deg * Mathf.Deg2Rad; return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r)); }

        /// <summary>B30.b's cheat: up to four sit down round the fire, the pot is on, and one goes round with the bowls, dipping the ladle
        /// before each (MealStage.Pass with dips, the same as supper's round).</summary>
        string ForceLadle(IReadOnlyList<PersonActivity> acts, float time)
        {
            var fire = meals.Fire;
            potState = PotState.Hot; potForcedCold = false; meals.PotAt = null; // the user's review: the pot was still at its cold spot on the cheat's frame
            var who = new List<int>();
            int giver = Nearest(acts, meals.PotHotSpot, 9f, a => IsGrown(a), null, false); // the bowl-giver is a grown-up (a kid was picked)
            if (giver == Nobody) return "no grown-up on view";
            who.Add(giver);
            while (who.Count < 5) { int k = Nearest(acts, fire, 9f, a => IsGrown(a) || IsKid(a), who, false); if (k == Nobody) break; who.Add(k); }
            if (who.Count < 3) return $"only {who.Count} on view";
            var seats = new List<Vector2>(); var takers = new List<int>();
            var cam = TowardCamera(fire) - fire;
            float lead = 0f;
            for (int i = 1; i < who.Count; i++)
            {
                var seat = fire + Rotate(cam, -70f + 140f * (i - 1) / Mathf.Max(1, who.Count - 2)) * 0.33f;
                seats.Add(seat); takers.Add(who[i]);
                lead = Mathf.Max(lead, ((At(who[i]) ?? seat) - seat).magnitude / LeadPace + 0.5f);
            }
            var stand = DipStandAt(meals.PotHotSpot, fire); // the user's review: right at the pot, on the camera's side
            lead = Mathf.Max(lead, ((At(giver) ?? stand) - stand).magnitude / LeadPace + 0.3f);
            var served = new Dictionary<int, float>(); var dips = new List<Vector2>();
            var events = MealStage.Pass(77, time + lead, giver, stand, fire, meals.PotHotSpot, takers, seats, served, dips, PropKind.Bowl, CampScenes1.DipSeconds);
            var s = new CampScene(CampUse.Ladle, time, meals.PotHotSpot, ThingPot) { holdFocus = true };
            int g = s.Add(giver);
            s.Part(g, 0f, lead, stand, meals.PotHotSpot, DollStyle.Stand, null, PropKind.None, LeadPace);
            float end = lead;
            foreach (var w in dips)
            {
                DipParts(s, g, w.x - time, w.y - time, stand, meals.PotHotSpot);
            }
            foreach (var e in events) end = Mathf.Max(end, e.End - time);
            for (int i = 0; i < takers.Count; i++)
            {
                int m = s.Add(takers[i]);
                float got = served.TryGetValue(takers[i], out float x) ? x - time : lead;
                s.Part(m, 0f, got - 1.2f, seats[i], fire, DollStyle.Sit, null, PropKind.None, LeadPace);
                s.Part(m, got + 0.4f, end + 3f, seats[i], fire, DollStyle.EatMeal, null, PropKind.Bowl);
            }
            foreach (var e in events) PairStage.Add(e);
            Add(s);
            return $"bowl round: giver {giver}, {takers.Count} seated, {dips.Count} dips";
        }

        /// <summary>"Pot: cold / cooking" (toggle).</summary>
        public string TogglePot()
        {
            if (meals == null) return "no camp yet";
            potForcedCold = potState == PotState.Hot;
            potState = potForcedCold ? PotState.Cold : PotState.Hot;
            return "pot: " + (potForcedCold ? "cold, set out" : "cooking");
        }

        /// <summary>"Roast on the spit (rabbit / deer)": tonight is a spit night.</summary>
        public string ForceSpit(bool deer)
        {
            spitForced = true; spitDay = today; spitKind = deer ? 1 : 0;
            doneToday.Remove(CampUse.Spit);
            return Force(CampUse.Spit);
        }

        /// <summary>Debug: each scene's members, their part now and where they are drawn.</summary>
        public string DebugCues()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in scenes)
            {
                sb.Append(s.use).Append(" t=").Append((clock - s.t0).ToString("0.0")).Append('/').Append(s.Length.ToString("0.0")).Append(':');
                for (int i = 0; i < s.Count; i++)
                {
                    bool has = s.TryPart(s.keys[i], clock, out var p, out float u);
                    var at = pv.DrawnAt(s.keys[i]);
                    string act = "";
                    foreach (var a in pv.Activities) if (a.who.Key == s.keys[i]) { act = a.kind + "@" + a.place.kind + (a.moving ? " moving" : ""); break; }
                    sb.Append($" [{s.keys[i]} {act} part {(has ? p.style + (p.keepSpot ? " keep" : $" to {p.pos.x:0.00},{p.pos.y:0.00}") : "none")} drawn {(at.HasValue ? $"{at.Value.x:0.00},{at.Value.y:0.00}" : "-")}]");
                }
                sb.Append(" | ");
            }
            return sb.ToString();
        }

        readonly List<(float due, Func<string> f)> later = new();
        public string LastLater { get; private set; } = "";
        public void Later(float delay, Func<string> f) => later.Add((clock + delay, f));

        void RunLater()
        {
            for (int i = later.Count - 1; i >= 0; i--)
                if (clock >= later[i].due) { var f = later[i].f; later.RemoveAt(i); LastLater = f(); Debug.Log("[CampLife] later: " + LastLater); }
        }

        public string Status()
        {
            var sb = new System.Text.StringBuilder($"camp uses: pot {potState}");
            foreach (var s in scenes) sb.Append(' ').Append(s.use).Append('×').Append(s.Count);
            return sb.ToString();
        }
    }
}

namespace AquariumCiv.View
{
    /// <summary>#692 cheats (SliceDebugPanel's "#692" block): each starts a use now with the people nearest its thing.</summary>
    public static partial class CampCheats
    {
        static CampLife Camp => PeopleView.Find()?.Camp;

        /// <summary>AnimLab: run a cheat <paramref name="delay"/> s of game time from now (the setup steps' real seconds pass before the
        /// recording starts, so a use started by the last step would be half over at frame 0; tooling.md 2026-11-15).</summary>
        public static string Later(float delay, System.Func<string> cheat)
        {
            var c = Camp;
            if (c == null) return "no PeopleView";
            c.Later(delay, cheat);
            return $"in {delay:0.0} s";
        }
        static string Do(CampUse use) => Camp == null ? "no PeopleView" : Camp.Force(use);

        /// <summary>"Winter fire: warm hands": snow today, three sit down at the fire and warm their hands.</summary>
        public static string WarmHands()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no PeopleView";
            pv.Planner.ForcedWeather = AquariumCiv.Life.Weather.Snow;
            return "snow; " + Do(CampUse.WarmHands);
        }

        /// <summary>AnimLab and the tests: a lived-in camp (a pond, the wood, the stock set down, a log fire) at <paramref name="hour"/>, with
        /// <paramref name="kids"/> kids added (a fresh living land has none). The recipe of Playtest1BPlaythrough.LivedInCamp.</summary>
        public static string StageCamp(float hour, int kids = 2, bool onlyCheats = true)
        {
            CampLife.OnlyCheats = onlyCheats; // a clip shows its one use alone
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            if (sim == null || box == null) return "no sim";
            PeopleView.DebugSetHour(hour);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && sim.State.era != AquariumCiv.Sim.Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            // (no EcoDebug.Table here: a new pond sends everyone to look at it, a placed-life scene that holds them for a while)
            sim.DebugGrowFarWood();
            sim.MarkFirstWildWoodSeen();
            if (!sim.StockSetDown) sim.SetDownStock();
            sim.DebugFireStage(2);
            box.Rebuild();
            if (kids > 0) sim.DebugAddKids(kids);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(hour);
            return $"camp at {hour:0.0} h, era {sim.State.era}, kids +{kids}";
        }

        /// <summary>AnimLab: skip on to <paramref name="season"/> (the sim's) and set the view's day to it (the view's season follows the view
        /// day: tooling.md 2026-11-15). Call, let the game run a step, then set the hour.</summary>
        public static string ToSeason(AquariumCiv.Life.Season season)
        {
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            for (int i = 0; i < 8 && AquariumCiv.Life.LifeClock.SeasonOf(sim.State.month) != season; i++) EcoDebug.SkipSeasons(1);
            return PeopleView.DebugSetHour(23.97f);
        }

        public static string KidPoke() => Do(CampUse.KidPoke);
        public static string BerryStick() => Do(CampUse.BerryStick);
        public static string Spit(bool deer) => Camp == null ? "no PeopleView" : Camp.ForceSpit(deer);
        public static string PotFill() => Do(CampUse.PotFill);
        public static string PotOff() => Do(CampUse.PotOff);
        public static string PotToggle() => Camp == null ? "no PeopleView" : Camp.TogglePot();
        public static string PotPeek() => Do(CampUse.PotPeek);
        public static string Ladle() => Do(CampUse.Ladle);
        public static string PotSniff() => Do(CampUse.PotSniff);
        public static string PoleDare()
        {
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            if (sim != null && !sim.DebugEnsureFeatherPole()) return "no pole could stand";
            return Do(CampUse.PoleDare);
        }

        /// <summary>"Feather pole: tie a feather (newborn / marriage / village)": the pole stands, a feather of that kind waits, and it is tied now.</summary>
        public static string TieFeather(int kind)
        {
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            if (sim.DebugPoleFeather(kind) < 0) return "no pole could stand";
            return CampLife.KindWord(kind) + ": " + Do(CampUse.FeatherTie);
        }
    }
}
