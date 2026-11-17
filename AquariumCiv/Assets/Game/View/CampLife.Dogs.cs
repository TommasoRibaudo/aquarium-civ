// #694 (docs/design/animation-specs.md B24.b, B25, B55, B56; #444/#445's view sides): the dogs drawn, and the dogs and the wolves in the
// people's day. Every dog the sim has (SimCore.Dogs, cap 1-3) is a QuadDoll here (P0's approved quadruped: a pup is P0's Pup with its
// line's coat, a grown dog QuadLook.Dog by generation with the hide collar, an old one grey round the muzzle). It is drawn beside the people,
// never by a herd view, so it is in no deer, rabbit or wolf count and no animal budget (the sim's cap is the dogs' own).
// Its day when no scene has it: a pup follows its keeper (the child who found it) 0.1 u behind and sleeps by the embers at night; a grown
// dog lies in the sun by its keeper's tent by day, goes out with the rabbit hunters (it runs ahead and flushes the rabbits at the strike,
// wags at the catch), and sleeps at the fire ring's edge every night; an old dog lies by the fire more.
// The scenes (View/Beats/DogScenes.cs) cast it with the people: the pup's chew, play, sleep, theft, shoo and race; the grown dog's hunt,
// guard, fetch, ride, fire and sun. A wolf near the camp at night (WolfView's watcher, the cheats) is lent here (WolfView hides it) and
// drawn in the guard (a grown dog barks it off) or the drive-off (the grown-ups' shouts and torch); the kids are held close meanwhile.
// The hard rules are counted every frame: VisitorRuleBreaks (a person within 0.3 u, or inside the tent ring) must stay 0.
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        static bool IsDogUse(CampUse u) => u >= CampUse.PupChew && u <= CampUse.KidsClose;
        static bool NeedsWolf(CampUse u) => u is CampUse.DogGuard or CampUse.WolfDriveOff or CampUse.KidsClose;

        sealed class DogActor
        {
            public QuadDoll doll; public string key;
            public Vector2 pos; public float yaw; public bool placed;
            public QuadAct act; public float actSince; public bool moving; public float speed;
            public Vector2 keeperWas; public Vector2 keeperHeading = Vector2.down; public float keeperStill;
            public Transform held; public QuadHeld heldKind;
            public int huntWith = int.MinValue; public bool flushed;
        }

        readonly Dictionary<int, DogActor> dogActors = new();
        readonly HashSet<CampScene> dogFlushed = new();
        Transform dogHolder, stickT;
        QuadDoll wolfDoll; Vector2 wPos; float wYaw, wGrow, wLast = -1f, dLast = -1f; QuadAct wAct; float wActSince; bool wPlaced;
        long wolfNight = long.MinValue; int howlsSeen = -1; float howlHoldUntil = -1f;
        readonly List<Vector3> wolfBuf = new();
        readonly Dictionary<int, HuntPhase> huntWas = new();

        /// <summary>Tests and the readout: dogs drawn now; frames the visiting wolf broke a hard rule (a person within 0.3 u, inside the tent
        /// ring); its nearest approach to the people driving it off; frames it was drawn; the flushes at the hunt.</summary>
        public int DogsDrawn { get { int n = 0; foreach (var a in dogActors.Values) if (a.doll?.go != null && a.doll.go.activeSelf) n++; return n; } }
        public int VisitorRuleBreaks { get; private set; }
        public float VisitorMinToDrivers { get; private set; } = float.MaxValue;
        public float VisitorMinToPeople { get; private set; } = float.MaxValue;
        public int VisitorFrames { get; private set; }
        public int DogFlushes { get; private set; }
        public int DogHuntFollows { get; private set; }
        public Transform DogTransform(int id) => dogActors.TryGetValue(id, out var a) && a.doll?.go != null ? a.doll.go.transform : null;
        public Vector2? DogAt(int id) => dogActors.TryGetValue(id, out var a) && a.placed ? a.pos : (Vector2?)null;
        public string DogActOf(int id) => dogActors.TryGetValue(id, out var a) ? (a.moving ? "moving" : a.act.ToString()) : "none";

        void ResetDogs()
        {
            dogFlushed.Clear(); wolfNight = long.MinValue; howlsSeen = -1; howlHoldUntil = -1f; huntWas.Clear();
            VisitorRuleBreaks = 0; VisitorMinToDrivers = VisitorMinToPeople = float.MaxValue; VisitorFrames = 0;
            WolfView.LentId = -1;
        }

        void DestroyDogs()
        {
            foreach (var a in dogActors.Values) { a.doll?.Destroy(); if (a.held != null) Object.Destroy(a.held.gameObject); }
            dogActors.Clear();
            wolfDoll?.Destroy(); wolfDoll = null;
            if (stickT != null) Object.Destroy(stickT.gameObject);
            stickT = null;
            if (dogHolder != null) Object.Destroy(dogHolder.gameObject);
            dogHolder = null;
            WolfView.LentId = -1;
        }

        // ================================================================== who and where
        List<Dog> AliveDogs() => sim != null ? new List<Dog>(sim.Dogs) : new List<Dog>();
        Dog FirstDog(DogStage? stage, bool notOld = false, int minGen = 0)
        {
            if (sim == null) return null;
            foreach (var d in sim.Dogs)
            {
                var st = sim.DogStageOf(d);
                if (stage.HasValue && (stage.Value == DogStage.Pup ? st != DogStage.Pup : st == DogStage.Pup)) continue;
                if (notOld && st == DogStage.Old) continue;
                if (d.gen < minGen) continue;
                return d;
            }
            return null;
        }
        bool DogBusy(int id) { foreach (var s in scenes) if (s.dog == id) return true; return false; }
        bool WolfScenePlaying() { foreach (var s in scenes) if (NeedsWolf(s.use) && s.quads.Exists(q => q.actor == 1)) return true; return false; }
        Vector2 DogNow(Dog d) => DogAt(d.id) ?? (meals != null && meals.HasFire ? meals.Fire + Vector2.down * 0.3f : Vector2.zero);

        bool KeeperTent(Dog d, out Tent tent)
        {
            tent = default;
            var st = sim?.Settlement; var k = sim?.FindNotable(d.keeperId);
            if (st == null || k == null || !k.alive) return false;
            var lot = st.HomeOf(k);
            if (lot == null) return false;
            foreach (var t in Tents()) if (t.id == lot.id) { tent = t; return true; }
            return false;
        }

        /// <summary>B25: the grown dog's spot in the sun by its keeper's tent (its shown side), else near the fire.</summary>
        Vector2 SunSpot(Dog d, int idx)
        {
            if (KeeperTent(d, out var t)) return t.look + t.lookOut * (DogGeo.SunOut + t.r * 0.1f) + new Vector2(-t.lookOut.y, t.lookOut.x) * (0.06f * idx);
            var fire = meals != null && meals.HasFire ? meals.Fire : Vector2.zero;
            return fire + Rotate(ToCam(fire), 120f + 35f * idx) * 0.5f;
        }

        Vector2 FireSpot(int idx, float r)
        {
            var fire = meals.Fire; var pot = meals.PotHotSpot;
            var p = fire + Rotate(ToCam(fire), -50f + 70f * idx) * r;
            return (p - pot).magnitude < 0.18f ? fire + Rotate(ToCam(fire), 130f + 70f * idx) * r : p; // clear of the pot and its tripod
        }

        /// <summary>A spot <paramref name="r"/> from the fire, <paramref name="deg"/> either way round from the camera's side: the one clear of the pot
        /// and its tripod (#694 clip: the pup chewed hidden behind the tripod).</summary>
        Vector2 FireSide(float r, float deg)
        {
            var fire = meals.Fire; var pot = meals.PotHotSpot;
            var a = fire + Rotate(ToCam(fire), deg) * r; var b = fire + Rotate(ToCam(fire), -deg) * r;
            return (a - pot).magnitude >= (b - pot).magnitude ? a : b;
        }

        float AgeOf(int key) => key > 0 && sim?.FindNotable(key) is Notable n ? n.age : 10f;

        static bool RabbitHuntOut(HuntPhase h) => h is HuntPhase.WalkOut or HuntPhase.Stalk or HuntPhase.Strike or HuntPhase.Bolt or HuntPhase.FetchSpear or HuntPhase.PullSpear
            or HuntPhase.ToCatch or HuntPhase.PickUp or HuntPhase.HoldUp or HuntPhase.WalkBack or HuntPhase.CarryBack;

        // ================================================================== each frame: the natural uses (D rules)
        void StepDogs(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (sim == null || planner == null || meals == null) return;
            StepDogMoments(time);
            if (OnlyCheats || sim.DogCount == 0 && !sim.WolvesPlaced) return;
            float h = Hour;
            bool Go(CampUse u, float chance)
            {
                if (doneToday.Contains(u) || Roll(u) >= chance || !LifeOk(u)) return false;
                if (triedAt.TryGetValue(u, out float tr) && clock - tr < 3f && clock >= tr) return false;
                triedAt[u] = clock; return true;
            }
            // the wolves near the camp at night: the guard or the drive-off, at most once a night; the kids held close
            if (sim.WolvesPlaced && meals.HasFire && (h >= 19f || h < 5f) && !WolfScenePlaying())
            {
                long night = h < 5f ? now.day - 1 : now.day;
                if (wolfNight != night && WolfNear(out var wolfAt)) { wolfNight = night; TryWolfNear(acts, time, false, wolfAt); return; }
            }
            // the howl (B21.a): kids held close, often
            var wv = WolfView.Instance;
            if (wv != null)
            {
                if (howlsSeen < 0) howlsSeen = wv.DuskHowls;
                bool howl = wv.Howling || wv.DuskHowls != howlsSeen;
                howlsSeen = wv.DuskHowls;
                if (howl && time > howlHoldUntil && !Playing(CampUse.KidsClose) && Roll(CampUse.KidsClose) < Often && LifeOk(CampUse.KidsClose))
                { howlHoldUntil = time + 60f; TryKidsClose(acts, time, false, meals.Fire + Vector2.up * 1.5f, false, 5f); }
            }
            if (sim.DogCount == 0 || meals.Eaters > 0) return;
            var wet = planner.WeatherOn(now.day) is Weather.KeeperRain or Weather.Storm;
            var pup = FirstDog(DogStage.Pup); var grown = FirstDog(DogStage.Grown, true);
            if (h >= 9f && h < 16.5f && !wet)
            {
                if (pup != null && !DogBusy(pup.id))
                {
                    if (Go(CampUse.PupPlay, Sometimes) && TryPupPlay(acts, time, false, pup) == null) return;
                    if (Go(CampUse.PupRunAlong, Sometimes) && TryPupRun(acts, time, false, pup) == null) return;
                    if (Go(CampUse.PupChew, Often) && TryPupChew(acts, time, false, pup) == null) return;
                    if (Go(CampUse.PupShoo, Sometimes) && TryPupShoo(acts, time, false, pup) == null) return;
                    if (Go(CampUse.PupSteal, VeryRare) && TryPupSteal(acts, time, false, pup) == null) return;
                }
                if (grown != null && !DogBusy(grown.id) && (!dogActors.TryGetValue(grown.id, out var ga) || ga.huntWith == int.MinValue))
                {
                    if (Go(CampUse.DogFetch, Sometimes) && TryFetch(acts, time, false, grown) == null) return;
                    if (grown.gen >= 2 && Go(CampUse.DogRide, VeryRare) && TryRide(acts, time, false, grown) == null) return;
                }
            }
            if (h >= 18.6f && h < 20.6f)
            {
                if (pup != null && !DogBusy(pup.id) && Go(CampUse.PupSleep, Often) && TryPupSleep(acts, time, false, pup) == null) return;
                var any = FirstDog(DogStage.Grown);
                if (any != null && !DogBusy(any.id) && Go(CampUse.DogFire, Rare) && TryDogFire(acts, time, false, any) == null) return;
            }
        }

        /// <summary>A wolf drawn by WolfView within <see cref="WolfDriveOff.NearEdge"/> of the camp's edge (the eyes at the firelight's edge, a
        /// bold one on the scraps path).</summary>
        bool WolfNear(out Vector2 at)
        {
            at = default;
            var wv = WolfView.Instance;
            if (wv == null || sim == null) return false;
            wv.LocalPositions(wolfBuf);
            var g = sim.WolfGroundNow();
            float edge = WolfDriveOff.CampEdge(g);
            var fire = new Vector2(g.fireX, g.fireZ);
            float best = float.MaxValue;
            foreach (var w in wolfBuf)
            {
                var p = new Vector2(w.x, w.z); float d = (p - fire).magnitude;
                if (d < edge + WolfDriveOff.NearEdge && d < best) { best = d; at = p; }
            }
            return best < float.MaxValue;
        }

        /// <summary>The wolf lent for a scene (WolfView hides this one meanwhile): an adult of the pack.</summary>
        int LendId()
        {
            var wv = WolfView.Instance;
            int n = wv != null ? wv.Field.Count : 0;
            for (int i = 1; i < n; i++) if (!wv.Field.IsPup(i)) return i;
            return 0;
        }

        // ================================================================== starting each use (natural and forced); null = started, else why not
        string StartDog(CampScene s, CampUse use) { Add(s); doneToday.Add(use); return null; }

        string TryPupChew(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog pup)
        {
            var at = DogNow(pup);
            int keeper = pup.keeperId > 0 && (force ? Shown(acts, pup.keeperId) : FreeKey(acts, pup.keeperId)) ? pup.keeperId : -1;
            // on the bare earth by the fire, where the hide shows (#694 clip: in the grass the pup was lost) [est]
            var spot = FireSide(0.42f, 65f);
            return StartDog(DogScenes.PupChew(pup.id, at, keeper, keeper >= 0 ? At(keeper).Value : default, spot, ToCam(spot), time), CampUse.PupChew);
        }

        string TryPupPlay(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog pup)
        {
            var at = DogNow(pup);
            var kids = Pick(acts, at, a => IsKid(a), 2, force);
            if (kids.Count < 1 || !force && kids.Count < 2) return "kids needed ('Two kids join the camp' first)";
            var spot = force ? meals.Fire + Rotate(ToCam(meals.Fire), 60f) * 0.55f : at;
            return StartDog(DogScenes.PupPlay(pup.id, at, kids, AtAll(kids), spot, ToCam(spot), time), CampUse.PupPlay);
        }

        string TryPupSleep(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog pup)
        {
            if (!meals.HasFire) return "no fire in the camp";
            return StartDog(DogScenes.PupSleep(pup.id, DogNow(pup), meals.Fire, ToCam(meals.Fire), force ? 10f : 14f, time, FireSide(DogGeo.EmberSleep, 55f)), CampUse.PupSleep);
        }

        /// <summary>Where the meat and the fish are: the fish rack when drawn, else the stash, else beside the fire.</summary>
        Vector2 MeatAt()
        {
            if (FishRackAt(out var mid, out _, out _)) return mid;
            if (Stash is Vector2 st) return st;
            return meals.Fire + Rotate(ToCam(meals.Fire), -80f) * 0.4f;
        }

        string TryPupSteal(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog pup)
        {
            var stash = Stash ?? MeatAt();
            int g = Grown1(acts, stash, force);
            var away = Rotate(ToCam(stash), 90f);
            var fire = meals.Fire;
            if (Vector2.Dot(away, stash - fire) < 0f) away = -away; // off away from the fire
            return StartDog(DogScenes.PupSteal(pup.id, DogNow(pup), stash, g, g >= 0 ? At(g).Value : default, away, ToCam(stash), time), CampUse.PupSteal);
        }

        string TryPupShoo(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog pup)
        {
            var meat = MeatAt();
            if (!force && (!FishRackAt(out _, out _, out int fish) || fish + meatStrips == 0)) return "no meat on the rack";
            int g = Grown1(acts, meat, force);
            if (g < 0) return "no grown-up free";
            return StartDog(DogScenes.PupShoo(pup.id, DogNow(pup), meat, g, At(g).Value, ToCam(meat), time), CampUse.PupShoo);
        }

        string TryPupRun(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog pup)
        {
            var at = DogNow(pup);
            var kids = Pick(acts, at, a => IsKid(a), 2, force);
            if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
            var fire = meals.Fire;
            var start = fire + ToCam(fire) * 0.65f - CameraAcross() * 0.45f; // in front of the fire, clear of the pot's tripod
            return StartDog(DogScenes.PupRunAlong(pup.id, at, kids, AtAll(kids), start, CameraAcross(), ToCam(start), time), CampUse.PupRunAlong);
        }

        string TryDogHunt(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog dog)
        {
            var fire = meals.Fire;
            int hunter = Grown1(acts, fire, force);
            if (hunter < 0) return "no grown-up free";
            var hAt = At(hunter).Value;
            Vector2 rabbits = fire + Rotate(ToCam(fire), 80f) * 1.2f;
            var rv = RabbitView.Instance;
            if (rv != null)
            {
                int id = rv.NearestShown(fire.x, fire.y);
                if (id >= 0 && rv.TryGetPosition(id, out var rp)) rabbits = new Vector2(rp.x, rp.z);
            }
            if ((rabbits - hAt).magnitude > 0.9f) rabbits = hAt + (rabbits - hAt).normalized * 0.9f; // [est] the walk-out keeps to one frame
            return StartDog(DogScenes.DogHunt(dog.id, DogNow(dog), hunter, hAt, rabbits, ToCam(hAt), time), CampUse.DogHunt);
        }

        string TryFetch(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog dog)
        {
            var at = DogNow(dog);
            int kid = Kid1(acts, at, force);
            if (kid < 0) return "no kid free ('Two kids join the camp' first)";
            var fire = meals.Fire;
            var spot = fire + Rotate(ToCam(fire), -40f) * 0.6f;
            return StartDog(DogScenes.Fetch(kid, At(kid).Value, dog.id, at, spot, ToCam(spot), 3, time), CampUse.DogFetch);
        }

        string TryRide(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog dog)
        {
            var at = DogNow(dog);
            var kids = Pick(acts, at, a => IsKid(a), 3, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            kids.Sort((x, y) => AgeOf(x).CompareTo(AgeOf(y)));
            int small = kids[0], older = kids.Count > 1 ? kids[^1] : -1;
            var fire = meals.Fire;
            var spot = fire + Rotate(ToCam(fire), 30f) * 0.5f - CameraAcross() * 0.2f;
            return StartDog(DogScenes.Ride(small, At(small).Value, older, older >= 0 ? At(older).Value : default, dog.id, at, spot, CameraAcross(), ToCam(spot), time), CampUse.DogRide);
        }

        string TryDogFire(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog dog)
        {
            if (!meals.HasFire) return "no fire in the camp";
            int kid = Kid1(acts, meals.Fire, force);
            return StartDog(DogScenes.DogFire(dog.id, DogNow(dog), meals.Fire, kid, kid >= 0 ? At(kid).Value : default, ToCam(meals.Fire), time, FireSide(DogGeo.FireSleep, 50f)), CampUse.DogFire);
        }

        string TryDogSun(IReadOnlyList<PersonActivity> acts, float time, bool force, Dog dog)
        {
            var spot = SunSpot(dog, 0);
            return StartDog(DogScenes.DogSun(dog.id, DogNow(dog), spot, ToCam(spot), 12f, time), CampUse.DogSun);
        }

        /// <summary>The grown-ups and everyone else drawn near the fire (box x, z pairs for the plan's people check).</summary>
        List<float> PeoplePairs(IReadOnlyList<PersonActivity> acts, ICollection<int> not)
        {
            var r = new List<float>();
            foreach (var a in acts) { if (not != null && not.Contains(a.who.Key)) continue; if (At(a.who.Key) is Vector2 p) { r.Add(p.x); r.Add(p.y); } }
            return r;
        }

        /// <summary>A wolf near at night (natural, or the cheats): a grown dog guards; else 2-3 grown-ups drive it off; the other grown-ups hold
        /// the kids close. <paramref name="answer"/> forces one (the cheats).</summary>
        string TryWolfNear(IReadOnlyList<PersonActivity> acts, float time, bool force, Vector2 wolfAt, WolfDriveOff.Answer? answer = null)
        {
            var fire = meals.Fire;
            var guard = FirstDog(DogStage.Grown);
            var g = sim.WolfGroundNow();
            var grownUps = Pick(acts, fire, a => IsGrown(a), 3, force);
            while (force && grownUps.Count < 2) { int gk = StageGrown(acts, fire, grownUps); if (gk < 0 || grownUps.Contains(gk)) break; grownUps.Add(gk); } // the cheat: any grown-up drawn
            var ans = answer ?? WolfDriveOff.AnswerFor(guard != null && !DogBusy(guard.id), grownUps.Count);
            string r;
            var used = new List<int>();
            if (ans == WolfDriveOff.Answer.Guard)
            {
                if (guard == null) return "no grown dog ('Grown dog now' first)";
                int sitter = grownUps.Count > 0 ? grownUps[0] : -1;
                var plan = WolfDriveOff.Make(g, wolfAt, null, PeoplePairs(acts, null));
                if (!plan.ok) return "no clear way for the wolf (tents all round)";
                if (sitter >= 0) used.Add(sitter);
                var seat = sitter >= 0 ? At(sitter).Value : default;
                if (sitter >= 0 && (seat - fire).magnitude > 0.45f) seat = fire + Rotate(ToCam(fire), -35f) * 0.3f; // the cheat: they sit by the fire first
                r = StartDog(DogScenes.DogGuard(guard.id, DogNow(guard), plan, fire, sitter, seat, ToCam(fire), time), CampUse.DogGuard);
                if (r == null) r = $"the dog barks at the wolf{(sitter >= 0 ? "; a grown-up scratches its head" : "")}";
            }
            else if (ans == WolfDriveOff.Answer.DriveOff)
            {
                if (grownUps.Count < 2) return "two grown-ups needed by the fire";
                var now0 = AtAll(grownUps);
                var at = new List<Vector2>(now0);
                if (force)
                {   // the cheat: they first sit down by the fire on the wolf's side, as they would at dusk [est]
                    var way = Dir2(fire, wolfAt);
                    for (int i = 0; i < at.Count; i++) if ((at[i] - fire).magnitude > 0.45f) at[i] = fire + Rotate(way, (i - 1) * 40f + 20f) * 0.3f;
                }
                var plan = WolfDriveOff.Make(g, wolfAt, at, PeoplePairs(acts, grownUps));
                if (!plan.ok) return "no clear way for the wolf (tents all round)";
                used.AddRange(grownUps);
                Add(DogScenes.DriveOff(grownUps, at, plan, fire, ToCam(fire), time, now0)); doneToday.Add(CampUse.WolfDriveOff);
                r = $"{grownUps.Count} drive the wolf off";
            }
            else r = "nobody goes out";
            // the kids held close by the grown-ups not out at the wolf (B55.b)
            var main = ans != WolfDriveOff.Answer.HoldKids && scenes.Count > 0 ? scenes[^1] : null;
            string k = TryKidsClose(acts, time, force, wolfAt, false, 9f, used);
            if (main != null && scenes.Count > 0 && scenes[^1] != main) { scenes.Remove(main); scenes.Add(main); } // the wolf's scene is the newest (the cheat's camera follows it)
            return r + (k == null ? "; kids held close" : "");
        }

        string TryKidsClose(IReadOnlyList<PersonActivity> acts, float time, bool force, Vector2 wolfAt, bool bringWolf, float holdFor, ICollection<int> not = null)
        {
            var kids = new List<int>(); var grown = new List<int>();
            var skip = new List<int>(); if (not != null) skip.AddRange(not);
            var kidAll = Pick(acts, meals.Fire, a => IsKid(a), 3, force, skip);
            foreach (int k in kidAll)
            {
                var kp = At(k).Value;
                skip.Add(k);
                int g = force ? Grown1(acts, kp, true, skip) : Nearest(acts, kp, 1f, a => IsGrown(a), skip);
                if (g < 0) continue;
                skip.Add(g); kids.Add(k); grown.Add(g);
            }
            if (kids.Count == 0) return "no kid with a grown-up near";
            var plan = bringWolf ? WolfDriveOff.Make(sim.WolfGroundNow(), wolfAt, null, PeoplePairs(acts, null)) : default;
            var kidsAt = AtAll(kids);
            if (force)
            {   // the cheat: the kids come in by the fire first (on the wolf's side), as they would at dusk [est]
                var way = Dir2(meals.Fire, bringWolf && plan.ok ? plan.wolfStop : wolfAt);
                for (int i = 0; i < kidsAt.Count; i++) if ((kidsAt[i] - meals.Fire).magnitude > 0.7f) kidsAt[i] = meals.Fire + Rotate(way, (i - 0.5f * (kidsAt.Count - 1)) * 45f) * 0.45f;
            }
            Add(DogScenes.KidsClose(grown, AtAll(grown), kids, kidsAt, bringWolf && plan.ok ? plan.wolfStop : wolfAt, plan, holdFor, ToCam(meals.Fire), time));
            doneToday.Add(CampUse.KidsClose);
            return null;
        }

        // ================================================================== moments (the flush)
        void StepDogMoments(float time)
        {
            foreach (var s in scenes)
                if (s.use == CampUse.DogHunt && !dogFlushed.Contains(s) && time - s.t0 >= s.a && s.dog >= 0)
                {
                    dogFlushed.Add(s);
                    var at = DogAt(s.dog) ?? s.focus;
                    var rv = RabbitView.Instance;
                    if (rv != null) { rv.KnockAtGround(at.x, at.y); DogFlushes++; }
                }
            dogFlushed.RemoveWhere(s => !scenes.Contains(s));
        }

        // ================================================================== cheats
        string ForceDog(CampUse use)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            if (!meals.HasFire) return "no fire in the camp";
            var acts = pv.Activities; float time = clock;
            Dog pup = FirstDog(DogStage.Pup), grown = FirstDog(DogStage.Grown, true), big = FirstDog(DogStage.Grown, true, 2), anyGrown = FirstDog(DogStage.Grown);
            string NoPup() => "no pup ('Pup now' first)";
            string NoDog() => "no grown dog ('Grown dog now' first)";
            switch (use)
            {
                case CampUse.PupChew: return pup == null ? NoPup() : TryPupChew(acts, time, true, pup) ?? "the pup chews a hide";
                case CampUse.PupPlay: return pup == null ? NoPup() : TryPupPlay(acts, time, true, pup) ?? "the pup plays with the kids";
                case CampUse.PupSleep: return pup == null ? NoPup() : TryPupSleep(acts, time, true, pup) ?? "the pup sleeps by the embers";
                case CampUse.PupSteal: return pup == null ? NoPup() : TryPupSteal(acts, time, true, pup) ?? "the pup steals a fish";
                case CampUse.PupShoo: return pup == null ? NoPup() : TryPupShoo(acts, time, true, pup) ?? "the pup is shooed off the meat";
                case CampUse.PupRunAlong: return pup == null ? NoPup() : TryPupRun(acts, time, true, pup) ?? "the pup runs along with the kids";
                case CampUse.DogHunt: return grown == null ? NoDog() : TryDogHunt(acts, time, true, grown) ?? "the dog goes hunting";
                case CampUse.DogFetch: return grown == null ? NoDog() : TryFetch(acts, time, true, grown) ?? "the dog fetches";
                case CampUse.DogRide: return big == null ? "no big dog ('Big dog now' first)" : TryRide(acts, time, true, big) ?? "a kid rides the dog";
                case CampUse.DogFire: return anyGrown == null ? NoDog() : TryDogFire(acts, time, true, anyGrown) ?? "the dog sleeps by the fire";
                case CampUse.DogSun: return anyGrown == null ? NoDog() : TryDogSun(acts, time, true, anyGrown) ?? "the dog lies in the sun";
                case CampUse.DogGuard:
                case CampUse.WolfDriveOff:
                case CampUse.KidsClose:
                {
                    string ready = EnsureWolves();
                    if (ready != null) return ready;
                    var toward = DenAt();
                    if (use == CampUse.KidsClose) return TryKidsClose(acts, time, true, toward, true, 7f) ?? "wolves in view: the kids held close";
                    if (use == CampUse.DogGuard && anyGrown == null) return NoDog();
                    return TryWolfNear(acts, time, true, toward, use == CampUse.DogGuard ? WolfDriveOff.Answer.Guard : WolfDriveOff.Answer.DriveOff);
                }
            }
            return "not a dog use: " + use;
        }

        Vector2 DenAt()
        {
            int den = sim.WolfDen;
            var fire = meals.Fire;
            if (den < 0) return fire + Rotate(ToCam(fire), 180f) * 2f;
            var d = new Vector2(SiteMap.CXc(den), SiteMap.CZc(den));
            // [est] the wolf comes in on the camera's side of the den's ray, so a phone sees it
            var way = (d - fire).normalized; var cam = ToCam(fire);
            var mid = (way + cam * 0.9f); if (mid.sqrMagnitude < 1e-4f) mid = Rotate(cam, 60f);
            return fire + mid.normalized * 2f;
        }

        string EnsureWolves()
        {
            if (!sim.EcologyOn) return "no living land";
            if (sim.WolvesPlaced) return null;
            sim.DebugReadyWolves();
            return sim.DebugPlaceWolves() >= 0 ? null : "no den spot for the wolves";
        }

        // ================================================================== drawing
        Transform DogHolder(Transform root)
        {
            if (dogHolder == null || dogHolder.parent != root)
            {
                if (dogHolder != null) Object.Destroy(dogHolder.gameObject);
                dogHolder = new GameObject("Camp dogs") { hideFlags = HideFlags.DontSave }.transform;
                dogHolder.SetParent(root, false);
                foreach (var a in dogActors.Values) a.doll?.Destroy();
                dogActors.Clear(); wolfDoll = null; stickT = null;
            }
            return dogHolder;
        }

        QuadLook LookOf(Dog d)
        {
            var st = sim.DogStageOf(d);
            QuadLook l;
            if (st == DogStage.Pup)
            {
                l = QuadLook.Of(QuadKind.Pup, d.id);
                if (d.gen > 0)
                {
                    var g = QuadLook.Dog(d.gen, d.id);
                    l.coat = g.coat; l.saddle = g.saddle; l.belly = g.belly; l.patch = g.patch * 0.8f; l.patchCol = g.patchCol;
                    l.earDroop = g.earDroop; l.tailCurl = g.tailCurl; l.tailHang = g.tailHang; l.paleFace = g.paleFace;
                }
            }
            else
            {
                l = QuadLook.Dog(d.gen, d.id);
                l.collar = sim.DogCollar(d); // the hide collar from grown on (B25.a)
                l.greyMuzzle = sim.DogGrey(d); // B25.c the old dog
            }
            return l;
        }

        void DrawDogs(Transform root, Material material, float time, System.Func<float, float, float> groundAt)
        {
            if (sim == null || meals == null || planner == null || sim.State.era != Era.Camp || !sim.EcologyOn)
            {
                foreach (var a in dogActors.Values) a.doll?.SetActive(false);
                wolfDoll?.SetActive(false); if (stickT != null) stickT.gameObject.SetActive(false);
                WolfView.LentId = -1; return;
            }
            var holder = DogHolder(root);
            float dt = dLast < 0f ? 0f : Mathf.Clamp(time - dLast, 0f, 0.1f); dLast = time;
            var alive = AliveDogs();
            var keep = new HashSet<int>();
            for (int i = 0; i < alive.Count; i++)
            {
                var d = alive[i]; keep.Add(d.id);
                var look = LookOf(d); string key = look.Key() + (look.greyMuzzle ? ":grey" : "");
                if (!dogActors.TryGetValue(d.id, out var a)) dogActors[d.id] = a = new DogActor();
                if (a.doll == null || a.doll.go == null || a.key != key)
                {
                    a.doll?.Destroy();
                    a.doll = new QuadDoll(look, holder, material); a.key = key;
                    a.doll.go.name = (sim.DogStageOf(d) == DogStage.Pup ? "Pup " : "Dog ") + d.name;
                }
                StepDog(d, a, i, time, dt, groundAt, holder, material);
            }
            var gone = new List<int>();
            foreach (var kv in dogActors) if (!keep.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { var a = dogActors[id]; a.doll?.Destroy(); if (a.held != null) Object.Destroy(a.held.gameObject); dogActors.Remove(id); }
            DrawVisitor(holder, material, time, groundAt);
            DrawFetchStick(holder, material, time, groundAt);
        }

        CampScene SceneOfDog(int id) { CampScene r = null; foreach (var s in scenes) if (s.dog == id) r = s; return r; }

        void StepDog(Dog d, DogActor a, int idx, float time, float dt, System.Func<float, float, float> groundAt, Transform holder, Material material)
        {
            var st = sim.DogStageOf(d);
            float h = Hour;
            bool night = h >= 20.5f || h < 6f;
            Vector2 target; float speed = DogGeo.DogTrot; QuadAct act = QuadAct.Stand; Vector2 face = Vector2.zero; QuadHeld held = QuadHeld.None;
            var s = SceneOfDog(d.id);
            var part = s?.QuadAt(0, time - s.t0);
            if (s != null && part == null) part = s.quads.FindLast(q => q.actor == 0 && q.to <= time - s.t0);
            a.huntWith = int.MinValue;
            if (part != null) { target = part.pos; speed = part.speed; act = part.act; face = part.face; held = part.held; }
            else
            {
                var fire = meals.HasFire ? meals.Fire : Vector2.zero;
                Vector2? keeper = d.keeperId > 0 ? At(d.keeperId) : null;
                if (keeper is Vector2 kp)
                {
                    var mv = kp - a.keeperWas;
                    if (mv.magnitude > 0.002f) { a.keeperHeading = mv.normalized; a.keeperStill = 0f; } else a.keeperStill += dt;
                    a.keeperWas = kp;
                }
                // a grown dog out with the rabbit hunters (B25.a): ahead while they stalk, the flush at the strike, at heel otherwise
                int hunter = 0; bool hunting = false; HuntPhase hp = HuntPhase.None;
                if (st == DogStage.Grown)
                    foreach (var pa in pv.Activities)
                        if (RabbitHuntOut(pa.hunt) && IsGrown(pa) && At(pa.who.Key).HasValue) { hunter = pa.who.Key; hp = pa.hunt; hunting = true; break; }
                if (hunting)
                {
                    a.huntWith = hunter;
                    var hAt = At(hunter).Value;
                    huntWas.TryGetValue(hunter, out var was);
                    if (was != hp) { huntWas[hunter] = hp; if (was == HuntPhase.None || !RabbitHuntOut(was)) DogHuntFollows++; }
                    var heading = Dir2(fire, hAt);
                    var side = new Vector2(-heading.y, heading.x);
                    if (hp is HuntPhase.Stalk) { target = hAt + heading * 0.3f; speed = DogGeo.DogWalk; act = QuadAct.HeadDown; a.flushed = false; }
                    else if (hp is HuntPhase.Strike or HuntPhase.Bolt)
                    {
                        target = hAt + heading * 0.4f; speed = DogGeo.DogRun; act = QuadAct.Bark;
                        if (!a.flushed && RabbitView.Instance != null) { a.flushed = true; RabbitView.Instance.KnockAtGround(a.pos.x, a.pos.y); DogFlushes++; }
                    }
                    else if (hp is HuntPhase.HoldUp or HuntPhase.PickUp) { target = hAt + side * 0.09f; act = QuadAct.Wag; face = hAt; }
                    else { target = hAt + side * 0.09f; act = QuadAct.Wag; speed = DogGeo.DogTrot; }
                }
                else if (st == DogStage.Pup)
                {
                    if ((night || keeper == null) && meals.HasFire) { target = FireSpot(idx, DogGeo.EmberSleep); act = QuadAct.Sleep; face = target + Rotate(fire - target, 90f); speed = DogGeo.DogWalk; }
                    else if (keeper is Vector2 k2)
                    {
                        target = k2 - a.keeperHeading * DogGeo.PupBehind + new Vector2(-a.keeperHeading.y, a.keeperHeading.x) * (0.03f * (idx % 2 == 0 ? 1 : -1));
                        speed = (target - a.pos).magnitude > 0.3f ? DogGeo.DogRun : DogGeo.DogTrot;
                        act = a.keeperStill > 6f ? QuadAct.Lie : a.keeperStill > 2f ? QuadAct.Sit : QuadAct.Wag; face = k2;
                    }
                    else { target = SunSpot(d, idx); act = QuadAct.Lie; speed = DogGeo.DogWalk; }
                }
                else
                {
                    bool fireTime = night || h >= 19.5f || st == DogStage.Old && h >= 15f || planner.WeatherOn(now.day) is Weather.KeeperRain or Weather.Storm;
                    if (fireTime && meals.HasFire)
                    {
                        target = FireSpot(idx, st == DogStage.Old ? DogGeo.FireSleep + 0.03f : DogGeo.FireSleep);
                        act = night ? QuadAct.Sleep : QuadAct.Lie; face = target + Rotate(fire - target, 90f); speed = DogGeo.DogWalk;
                    }
                    else { target = SunSpot(d, idx); act = QuadAct.Lie; speed = DogGeo.DogWalk; face = target + CameraAcross(); }
                }
            }
            if (!a.placed) { a.pos = target; a.placed = true; a.yaw = 0f; }
            var dv = target - a.pos; float dist = dv.magnitude;
            bool moving = dist > 0.012f;
            if (moving) { a.pos += dv / dist * Mathf.Min(dist, speed * dt); }
            if (moving != a.moving || act != a.act) { a.actSince = 0f; a.act = act; a.moving = moving; } else a.actSince += dt;
            a.speed = speed;
            var lookTo = moving ? dv : face != Vector2.zero ? face - a.pos : Vector2.zero;
            if (lookTo.sqrMagnitude > 1e-6f) a.yaw = Mathf.MoveTowardsAngle(a.yaw, Mathf.Atan2(lookTo.x, lookTo.y) * Mathf.Rad2Deg, 260f * Mathf.Max(dt, 0.016f));
            var pos = a.pos;
            if (!moving && act == QuadAct.Bark && face != Vector2.zero) pos += Dir2(a.pos, face) * DogGeo.BarkHop * Mathf.Min(3f, a.actSince / DogGeo.BarkEvery + 0.5f) * 0.5f;
            var p = QuadPose.Stand(new Vector3(pos.x, groundAt(pos.x, pos.y), pos.y), a.yaw);
            PoseQuad(ref p, moving, speed, act, a.actSince, time + d.id * 0.37f, true);
            a.doll.Apply(p); a.doll.SetActive(true);
            DrawHeld(a, held, holder, material);
        }

        /// <summary>The pose of a dog or the visiting wolf: the gait from the speed while it moves, else the act.</summary>
        static void PoseQuad(ref QuadPose p, bool moving, float speed, QuadAct act, float since, float t, bool dog)
        {
            if (moving)
            {
                if (speed < 0.16f) QuadMotion.Walk(ref p, t); else if (speed < 0.45f) QuadMotion.Trot(ref p, t); else QuadMotion.Bolt(ref p, t, false);
                if (dog) { p.tail += act is QuadAct.Wag or QuadAct.Stand ? 25f : 0f; p.wag = act == QuadAct.Wag ? Mathf.Sin(t * Mathf.PI * 8f) * 25f : 0f; }
                if (act == QuadAct.HeadDown) { p.neck += 30f; p.head += 15f; p.tail -= 10f; }
                return;
            }
            switch (act)
            {
                case QuadAct.Sit: p.sit = Mathf.Clamp01(since / 0.4f); p.tail = -10f; if (dog) p.wag = Mathf.Sin(t * 5f) * 12f; break;
                case QuadAct.Lie:
                    QuadMotion.LieDown(ref p, since);
                    float up = Mathf.Repeat(t, 9f) > 7.4f ? Mathf.Sin((Mathf.Repeat(t, 9f) - 7.4f) / 1.6f * Mathf.PI) : 0f; // now and then the head comes up
                    p.neck -= 20f * up; p.head -= 10f * up; break;
                case QuadAct.Sleep: QuadMotion.SleepCurl(ref p, t); break;
                case QuadAct.PlayBow: QuadMotion.PlayBow(ref p, since); break;
                case QuadAct.Sniff: QuadMotion.Sniff(ref p, Mathf.Repeat(since, 2.2f)); break;
                case QuadAct.Alert: QuadMotion.Alert(ref p, since, 0f, false); if (!dog) { p.neck = -4f; p.tail = -8f; } else p.tail = 30f; break;
                case QuadAct.Bark:
                {
                    // B25.b: three head jabs, a hop forward with each (the position hop is the caller's)
                    float c = Mathf.Repeat(since, DogGeo.BarkEvery) / DogGeo.BarkEvery, jab = c < 0.4f ? Mathf.Sin(c / 0.4f * Mathf.PI) : 0f;
                    p.phase = float.NaN; p.neck = -10f - 18f * jab; p.head = -8f * jab; p.lift = 0.012f * jab; p.bodyPitch = -4f * jab; p.tail = 35f; p.earSwivel = 20f;
                    break;
                }
                case QuadAct.Chew:
                {
                    QuadMotion.LieDown(ref p, since + 0.6f, 0.6f);
                    float c = Mathf.Repeat(since, 2.5f), shake = c < 1f ? 1f : 0f; // B24.b: head shakes ±25 deg at 4 Hz, a second in each 2.5 s
                    p.headYaw = 25f * Mathf.Sin(since * 4f * Mathf.PI * 2f) * shake; p.neck += 6f; p.head += 8f;
                    break;
                }
                case QuadAct.Growl: p.neck = 22f; p.head = 14f; p.bodyPitch = 5f; p.earSwivel = -30f; p.tail = -20f; p.lift = -0.004f; break;
                case QuadAct.Wag: if (dog) { p.tail = 40f; p.wag = Mathf.Sin(t * Mathf.PI * 8f) * 30f; } p.neck = -6f; break; // 4 Hz
                case QuadAct.HeadDown: p.neck = 50f; p.head = 30f; p.bodyPitch = 6f; break;
                default: if (dog) { p.tail = 15f; p.wag = Mathf.Sin(t * 3f) * 8f; } break;
            }
        }

        // ---- the things in a dog's mouth: a stick, a fish, a hide
        static readonly Dictionary<QuadHeld, Mesh> heldMeshes = new();
        static Mesh HeldMesh(QuadHeld k)
        {
            if (heldMeshes.TryGetValue(k, out var m) && m != null) return m;
            var mb = new MeshBuilder(6940 + (int)k);
            switch (k)
            {
                case QuadHeld.Stick: mb.Box(Vector3.zero, new Vector3(0.075f, 0.007f, 0.007f), new Color(0.45f, 0.32f, 0.2f)); break;
                case QuadHeld.Fish: mb.Ico(Vector3.zero, 0.02f, new Vector3(1.9f, 0.6f, 0.35f), new Color(0.62f, 0.7f, 0.76f), 0.05f); mb.Box(new Vector3(0.042f, 0f, 0f), new Vector3(0.012f, 0.016f, 0.004f), new Color(0.5f, 0.58f, 0.64f)); break;
                default: mb.Box(Vector3.zero, new Vector3(0.05f, 0.006f, 0.035f), new Color(0.62f, 0.46f, 0.3f), Quaternion.Euler(0f, 0f, 12f)); break;
            }
            m = mb.ToMesh("Dog_Held_" + k); m.hideFlags = HideFlags.DontSave;
            return heldMeshes[k] = m;
        }

        Transform MkHeld(Transform holder, Material material, string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(holder, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void DrawHeld(DogActor a, QuadHeld held, Transform holder, Material material)
        {
            if (held == QuadHeld.None) { if (a.held != null) a.held.gameObject.SetActive(false); return; }
            if (a.held == null) a.held = MkHeld(holder, material, "Dog's mouthful");
            if (a.heldKind != held) { a.heldKind = held; a.held.GetComponent<MeshFilter>().sharedMesh = HeldMesh(held); }
            a.held.position = a.doll.MuzzleWorld + Vector3.down * 0.006f;
            a.held.rotation = Quaternion.Euler(0f, a.yaw + (held == QuadHeld.Stick ? 0f : 90f), 0f);
            a.held.localScale = Vector3.one * (held == QuadHeld.Stick ? 1.3f : 1.7f); // [est] read at phone scale (the #693 retakes drew held things 1.4-2x)
            a.held.gameObject.SetActive(true);
        }

        // ---- B56.c the stick: in the kid's hand, flying, on the ground (the dog's mouth is DrawHeld)
        void DrawFetchStick(Transform holder, Material material, float time, System.Func<float, float, float> groundAt)
        {
            CampScene s = null; foreach (var x in scenes) if (x.use == CampUse.DogFetch) s = x;
            bool shown = false;
            if (s != null && s.Count > 0)
            {
                float u = time - s.t0;
                var v = pv.ViewOf(s.keys[0]);
                for (int i = 0; i < s.arcs.Count && !shown; i++)
                {
                    var arc = s.arcs[i];
                    float hold0 = arc.from - CampScenes4.ThrowRelease - 0.4f;
                    var carry = s.quads.Find(q => q.actor == 0 && q.held == QuadHeld.Stick && q.from >= arc.from);
                    float pick = carry != null ? carry.from : arc.from + 3f, drop = carry != null ? carry.to : pick;
                    float next = i + 1 < s.arcs.Count ? s.arcs[i + 1].from - CampScenes4.ThrowRelease - 0.4f : s.Length;
                    Vector3 at; float yaw = 0f;
                    if (u >= hold0 && u < arc.from && v != null && v.gameObject.activeInHierarchy) at = holder.InverseTransformPoint(v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)));
                    else if (u >= arc.from && u < arc.from + arc.seconds && v != null)
                    {
                        float k = (u - arc.from) / arc.seconds;
                        var from = holder.InverseTransformPoint(v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)));
                        var to = new Vector3(arc.to.x, groundAt(arc.to.x, arc.to.z) + 0.004f, arc.to.z);
                        at = Vector3.Lerp(from, to, k) + Vector3.up * (4f * arc.h * k * (1f - k)); yaw = k * 540f;
                    }
                    else if (u >= arc.from + arc.seconds && u < pick) at = new Vector3(arc.to.x, groundAt(arc.to.x, arc.to.z) + 0.004f, arc.to.z);
                    else if (u >= drop && u < next && carry != null) at = new Vector3(carry.pos.x, groundAt(carry.pos.x, carry.pos.y) + 0.004f, carry.pos.y);
                    else continue;
                    if (stickT == null) { stickT = MkHeld(holder, material, "Fetch stick"); stickT.GetComponent<MeshFilter>().sharedMesh = HeldMesh(QuadHeld.Stick); }
                    stickT.localPosition = at; stickT.localRotation = Quaternion.Euler(0f, 30f + yaw, 0f); stickT.localScale = Vector3.one * 1.3f;
                    shown = true;
                }
            }
            if (stickT != null) stickT.gameObject.SetActive(shown);
        }

        // ---- the visiting wolf (lent by WolfView)
        void DrawVisitor(Transform holder, Material material, float time, System.Func<float, float, float> groundAt)
        {
            CampScene s = null; foreach (var x in scenes) if (NeedsWolf(x.use) && x.quads.Exists(q => q.actor == 1)) s = x;
            if (s == null) { wolfDoll?.SetActive(false); WolfView.LentId = -1; wPlaced = false; return; }
            int lend = LendId();
            WolfView.LentId = lend;
            if (wolfDoll == null || wolfDoll.go == null) { wolfDoll = new QuadDoll(QuadLook.Of(QuadKind.Wolf, lend), holder, material); wolfDoll.go.name = "Visiting wolf"; }
            float u = time - s.t0;
            var part = s.QuadAt(1, u);
            float first = float.MaxValue, last = 0f; QuadPart lastPart = null;
            foreach (var q in s.quads) if (q.actor == 1) { first = Mathf.Min(first, q.from); if (q.to > last) { last = q.to; lastPart = q; } }
            if (part == null) { wolfDoll.SetActive(false); wPlaced = false; return; }
            float dt = wLast < 0f ? 0f : Mathf.Clamp(time - wLast, 0f, 0.1f); wLast = time;
            if (!wPlaced)
            {
                var start = s.use == CampUse.WolfDriveOff || s.use == CampUse.DogGuard || s.use == CampUse.KidsClose ? part.pos + (part.pos - (meals.HasFire ? meals.Fire : part.pos)).normalized * DogGeo.WolfFrom : part.pos;
                if (!WolfRules.OnLand(start.x, start.y)) start = part.pos;
                wPos = start; wPlaced = true; wGrow = 0f; wYaw = Mathf.Atan2(part.pos.x - start.x, part.pos.y - start.y) * Mathf.Rad2Deg;
            }
            var dv = part.pos - wPos; float dist = dv.magnitude; bool moving = dist > 0.012f;
            bool backing = part.act == QuadAct.Alert && moving; // B55.a: it backs off facing the people
            if (moving) wPos += dv / dist * Mathf.Min(dist, part.speed * dt);
            if (part.act != wAct) wActSince = 0f; else wActSince += dt;
            wAct = part.act;
            var lookTo = moving && !backing ? dv : part.face - wPos;
            if (lookTo.sqrMagnitude > 1e-6f) wYaw = Mathf.MoveTowardsAngle(wYaw, Mathf.Atan2(lookTo.x, lookTo.y) * Mathf.Rad2Deg, 280f * Mathf.Max(dt, 0.016f));
            bool leaving = part == lastPart;
            float k = leaving && part.Len > 0.6f ? Mathf.Clamp01((part.to - u) / 0.6f) : 1f; // it fades into the dark at the end of its trot away
            wGrow = Mathf.MoveTowards(wGrow, k, dt / 0.6f);
            var p = QuadPose.Stand(new Vector3(wPos.x, groundAt(wPos.x, wPos.y), wPos.y), wYaw);
            p.scale = Mathf.SmoothStep(0f, 1f, wGrow);
            PoseQuad(ref p, moving && !backing, part.speed, part.act, wActSince, time, false);
            if (backing) { QuadMotion.Walk(ref p, -time); QuadMotion.Alert(ref p, 1f, 0f, false); p.neck = -4f; p.tail = -20f; }
            wolfDoll.Apply(p); wolfDoll.SetActive(p.scale > 0.02f);
            // the hard rules, every frame it is drawn
            VisitorFrames++;
            var ring = sim.WolfGroundNow().ring;
            bool bad = WolfRules.InTentRing(wPos.x, wPos.y, ring);
            var drivers = s.use == CampUse.WolfDriveOff ? s.keys : null;
            foreach (var a in pv.Activities)
            {
                if (!(At(a.who.Key) is Vector2 q)) continue;
                float dd = (q - wPos).magnitude;
                if (dd < WolfRules.PersonGap) bad = true;
                VisitorMinToPeople = Mathf.Min(VisitorMinToPeople, dd);
                if (drivers != null && drivers.Contains(a.who.Key)) VisitorMinToDrivers = Mathf.Min(VisitorMinToDrivers, dd);
            }
            if (bad) VisitorRuleBreaks++;
        }

        /// <summary>The readout: the dogs and what each does.</summary>
        public string DogStatus()
        {
            if (sim == null) return "no sim";
            var sb = new System.Text.StringBuilder($"dogs {sim.DogCount} drawn {DogsDrawn}:");
            foreach (var d in sim.Dogs) sb.Append($" {d.name} ({sim.DogStageOf(d)}, gen {d.gen}, keeper {d.keeperId}) {DogActOf(d.id)};");
            if (wolfDoll?.go != null) sb.Append($" visitor at ({wPos.x:0.00},{wPos.y:0.00}) shown {wolfDoll.go.activeSelf} scale {wGrow:0.00} lent {WolfView.LentId};");
            if (meals != null) sb.Append($" fire ({meals.Fire.x:0.00},{meals.Fire.y:0.00});");
            foreach (var sc in scenes) { sb.Append($" scene {sc.use} focus ({sc.focus.x:0.00},{sc.focus.y:0.00}) u {clock - sc.t0:0.0}/{sc.Length:0.0} cast"); foreach (int k in sc.keys) sb.Append(At(k) is Vector2 q ? $" {k}@({q.x:0.00},{q.y:0.00})" : $" {k}@-"); sb.Append(';'); }
            sb.Append($" visitor frames {VisitorFrames}, rule breaks {VisitorRuleBreaks}, min to drivers {(VisitorMinToDrivers < 99f ? VisitorMinToDrivers.ToString("0.00") : "-")}, flushes {DogFlushes}");
            return sb.ToString();
        }
    }

    public static partial class CampCheats
    {
        static AquariumCiv.Sim.SimCore SimNow => AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
        static string DogNow(int gen, AquariumCiv.Sim.DogStage stage, string what)
        {
            var sim = SimNow;
            if (sim == null) return "no sim";
            var d = sim.DebugDogNow(gen, stage);
            return d == null ? "no dog could come (no living land, or no child)" : $"{what}: {d.name} (gen {d.gen}, {sim.DogStageOf(d)}, keeper {d.keeperId})";
        }
        /// <summary>"Pup now": a pup comes home now (the lost pup's "bring it home"), bonded to a child.</summary>
        public static string PupNow() => DogNow(0, AquariumCiv.Sim.DogStage.Pup, "a pup");
        /// <summary>"Grown dog now": a grown dog of generation 1 (the collar).</summary>
        public static string GrownDogNow() => DogNow(1, AquariumCiv.Sim.DogStage.Grown, "a grown dog");
        /// <summary>"Big dog now": a grown dog of generation 3 (big enough to ride, B56.d).</summary>
        public static string BigDogNow() => DogNow(3, AquariumCiv.Sim.DogStage.Grown, "a big dog");
        /// <summary>"Old dog now": an old dog (grey muzzle).</summary>
        public static string OldDogNow() => DogNow(4, AquariumCiv.Sim.DogStage.Old, "an old dog");
        public static string PupChew() => Do(CampUse.PupChew);
        public static string PupPlay() => Do(CampUse.PupPlay);
        public static string PupSleep() => Do(CampUse.PupSleep);
        public static string PupSteal() => Do(CampUse.PupSteal);
        public static string PupShoo() => Do(CampUse.PupShoo);
        public static string PupRunAlong() => Do(CampUse.PupRunAlong);
        public static string DogHunt() => Do(CampUse.DogHunt);
        public static string DogGuard() => Do(CampUse.DogGuard);
        public static string DogFetch() => Do(CampUse.DogFetch);
        public static string DogRide() => Do(CampUse.DogRide);
        public static string DogFire() => Do(CampUse.DogFire);
        public static string DogSun() => Do(CampUse.DogSun);
        public static string DriveOff() => Do(CampUse.WolfDriveOff);
        public static string KidsClose() => Do(CampUse.KidsClose);
        public static string DogStatus() { var c = PeopleView.Find()?.Camp; return c == null ? "no PeopleView" : c.DogStatus(); }
    }
}
