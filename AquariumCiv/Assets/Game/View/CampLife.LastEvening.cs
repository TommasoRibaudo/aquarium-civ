// #789 (docs/design/animation-specs.md B82, spec approved 2026-11-26; the four new clips wait for the user's sign-off before merge): CampLife's side
// of the last evening. At dusk, while the Keeper watches and nothing else is held, it asks the sim who is in their last evening
// (SimCore.ChooseLastEvening: one elder, once per visit, two game years apart), casts them and up to four kin or friends who are drawn and free,
// the elder's own dog (else a grown dog) if one is drawn, and plays View/Beats/LastEveningScenes.Evening at the fire; the one caption at the stars;
// the sim stamps it (the History line; the elder stays in from the door on: LifePlanner.LastEveningIndoors). Never while away, never queued.
// Cheats: SliceDebugPanel's "Last evening (#789)" block (CampCheats below).
using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.View.ShowMe;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        /// <summary>The window at dusk in which it may start (view hours) [est]: after the day's work, before the first bedtimes.</summary>
        public const float LastEveningFrom = 17.2f, LastEveningTo = 18.6f;
        /// <summary>Real seconds between two looks while the window is open.</summary>
        const float LastEveningLookEvery = 1f;

        /// <summary>The last evening is playing now (HeldMomentStage holds every other moment back while it does).</summary>
        public static bool LastEveningNow { get; private set; }
        /// <summary>Tests and the panel: the scene playing (null none), why the last look started nothing, the caption said, how many started.</summary>
        public CampScene LastEveningScene { get; private set; }
        public string LastEveningWhy { get; private set; } = "";
        public string LastEveningCaption { get; private set; }
        public int LastEveningsStarted { get; private set; }
        /// <summary>Tests: the dogs the last cast looked at (who was drawn, who was busy).</summary>
        public string LastEveningDogNote { get; private set; } = "";

        long lastEveningDone = long.MinValue;
        float lastEveningLook = float.MinValue;
        bool lastEveningSaid;
        /// <summary>A cheat's elder to play now (−1 none) and whether the rule is skipped (a cheat's "play now").</summary>
        int lastEveningCheat = -1; bool lastEveningForce;
        /// <summary>When the cheat was pressed (real time; the note of the dogs at the cast says how long after).</summary>
        float lastEveningCheatAt;

        void ResetLastEvening()
        {
            LastEveningScene = null; LastEveningNow = false; lastEveningDone = long.MinValue; lastEveningLook = float.MinValue; lastEveningSaid = false;
            lastEveningCheat = -1; lastEveningForce = false;
        }

        /// <summary>Someone the scene may take now: drawn, awake, not hunting, at a tent or held by another held scene or a staged spot. An
        /// everyday camp use they are in gives way (the last evening outranks it, as a story does).</summary>
        bool LastEveningFree(IReadOnlyList<PersonActivity> acts, int k)
        {
            if (k <= 0 || !At(k).HasValue) return false;
            foreach (var a in acts)
            {
                if (a.who.Key != k) continue;
                if (a.hunt != HuntPhase.None || a.tent != TentPhase.None || a.kind is ActivityKind.Sleep or ActivityKind.Nap || HeldElsewhere(a)) return false;
                return !(byKey.TryGetValue(k, out var s) && (StoryHeld(s) || s == forced));
            }
            return false;
        }

        /// <summary>Why another held thing keeps it from starting (null: nothing else is held): a held moment, a ritual, a choice, a sheet.</summary>
        string LastEveningBusy()
        {
            if (HeldMomentStage.BusyReason() is string r) return r;
            if (sim.PlayingMoments().Count > 0) return "a held moment is playing";
            if (RitualFocus() != null) return "a ritual is playing";
            foreach (var s in scenes) if (StoryHeld(s)) return "a story is playing";
            return null;
        }

        /// <summary>Each frame (every era): the scene's caption at the stars, and at dusk the look for one.</summary>
        void StepLastEvening(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (LastEveningScene != null && !scenes.Contains(LastEveningScene)) { storyHeld.Remove(LastEveningScene); LastEveningScene = null; }
            LastEveningNow = LastEveningScene != null;
            if (LastEveningScene != null)
            {
                if (!lastEveningSaid && time - LastEveningScene.t0 >= LastEveningScene.b)
                {
                    lastEveningSaid = true;
                    if (!string.IsNullOrEmpty(LastEveningCaption)) ShowMeDirector.Instance?.Say(LastEveningCaption, PersonRef.None, StoryMoment.CaptionSeconds, LineKind.Echo);
                }
                return;
            }
            if (sim == null || planner == null) return;
            bool cheat = lastEveningCheat >= 0;
            if (!cheat && (OnlyCheats || lastEveningDone == today)) return;
            if (Hour < LastEveningFrom || Hour >= LastEveningTo) { if (cheat && (Hour >= LastEveningTo || Hour < 5f)) { lastEveningCheat = -1; LastEveningWhy = "the cheat's dusk went by"; } return; }
            if (clock - lastEveningLook < LastEveningLookEvery && clock >= lastEveningLook) return;
            lastEveningLook = clock;
            string busy = LastEveningBusy();
            if (cheat && lastEveningForce) { if (busy != null) { LastEveningWhy = "waits: " + busy; return; } }
            Notable who;
            if (cheat && lastEveningForce) who = sim.FindNotable(lastEveningCheat) is Notable ch && ch.alive ? ch : null;
            else
            {
                who = sim.ChooseLastEvening(k => LastEveningFree(acts, k), !sim.CatchingUp, busy != null, out string why);
                if (who == null)
                {
                    LastEveningWhy = why;
                    // skipped, not queued: another moment at dusk, or one this visit or within two years, gives tonight up
                    if (busy != null || sim.LastEveningElders().Count == 0 || why != "no kin or friend free") { lastEveningDone = today; lastEveningCheat = -1; }
                    return;
                }
            }
            lastEveningCheat = -1;
            string r = TryLastEvening(acts, time, who);
            LastEveningWhy = r ?? "";
            if (r == null) lastEveningDone = today;
        }

        /// <summary>Casts and starts it for <paramref name="who"/> now (null) or says why not.</summary>
        string TryLastEvening(IReadOnlyList<PersonActivity> acts, float time, Notable who)
        {
            if (who == null || !who.alive) return "nobody is in their last evening";
            if (!(At(who.id) is Vector2 elderAt) || !LastEveningFree(acts, who.id)) return $"{who.name} is not in the box to see (asleep, away or busy)";
            Vector2 fire = meals != null && meals.HasFire ? meals.Fire : planner.FirePoint(out float fx, out float fz) ? new Vector2(fx, fz) : Vector2.zero;
            // up to four kin or friends, in the spec's order (the partner, children, grandchildren, brothers and sisters, friends); at most two kids
            var c = new LastEveningCast { elder = who.id, elderAt = elderAt, fire = fire, toCam = ToCam(fire) };
            int kids = 0;
            foreach (int k in sim.KinAndFriendsOf(who))
            {
                if (c.kin.Count >= LastEveningRules.MaxKin) break;
                if (!LastEveningFree(acts, k)) continue;
                var n = sim.FindNotable(k);
                bool kid = n != null && n.age < SimCore.AdultAge;
                if (kid && kids >= LastEveningRules.MaxKids) continue;
                if (kid) kids++;
                if (k == who.partnerId) c.partner = c.kin.Count;
                c.kin.Add(k); c.kinAt.Add(At(k).Value); c.kinKid.Add(kid);
            }
            if (!c.kinKid.Any(k => !k)) // a kid only with a grown kin there: no grown-up, no kids
                for (int i = c.kin.Count - 1; i >= 0; i--) if (c.kinKid[i]) { c.kin.RemoveAt(i); c.kinAt.RemoveAt(i); c.kinKid.RemoveAt(i); }
            if (c.kin.Count == 0) return "no grown kin or friend free to sit with them";
            if (c.partner >= c.kin.Count || c.partner >= 0 && c.kin[c.partner] != who.partnerId) c.partner = c.kin.IndexOf(who.partnerId);
            // the door: their own tent or house
            var home = planner.HomeOf(PersonRef.Notable(who.id));
            c.door = !home.IsNone && planner.Positions.TryGetPosition(home, out float hx, out float hz) ? new Vector2(hx, hz) : fire + c.toCam * 0.8f;
            // #789 tester (2026-11-27): the racks' legs stay out from between the family and the camera (the seat turns round the fire away from
            // them), and the wave is on the side of the tent the camera sees (the door facing away hid the elder behind the canvas)
            if (planner.TryGetFishRack(now, out var rack))
            {
                var m = new Vector2(rack.x, rack.z); var al = new Vector2(rack.dirX, rack.dirZ) * FishRackSpot.HalfLength;
                c.clear.Add(m); c.clear.Add(m + al); c.clear.Add(m - al);
            }
            if (HideRackAt(out var hm, out var ha)) { ha *= CampSpots.RackHalfLength; c.clear.Add(hm); c.clear.Add(hm + ha); c.clear.Add(hm - ha); }
            if (NearestTent(c.door, 0.5f, out var tent)) { c.door = tent.look; c.wave = tent.look + tent.lookOut * WaveOut; }
            LastEveningGeo = $"fire {fire} toCam {c.toCam} door {c.door} wave {c.wave} clear [{string.Join(" ", c.clear)}] seat {LastEveningScenes.SeatOf(fire, c.toCam, c.clear)}";
            // the dog: the elder's own, else a grown one, if one is drawn and free
            Dog dog = null;
            LastEveningDogNote = $"{Time.unscaledTime - lastEveningCheatAt:0.0} s after the cheat; own dogs:";
            foreach (var d in sim.DogsOfKeeper(who.id))
            {
                LastEveningDogNote += $" {d.name} {sim.DogStageOf(d)} drawn {DogAt(d.id).HasValue} busy {DogBusy(d.id)};";
                if (sim.DogStageOf(d) == DogStage.Pup) continue; // their own dog comes even when not drawn yet (a cheat's new dog: from by the fire)
                if (DogBusy(d.id)) // their own dog in an everyday use gives way to the last evening, as the people's do (the first "with a dog" clips: never cast)
                {
                    int cut = scenes.RemoveAll(x => x.dog == d.id && !StoryHeld(x));
                    if (cut > 0) Index();
                    if (DogBusy(d.id)) continue;
                }
                dog = d; break;
            }
            if (dog == null) { var g = FirstDog(DogStage.Grown); if (g != null && !DogBusy(g.id) && DogAt(g.id).HasValue) dog = g; }
            if (dog != null) { c.dog = dog.id; c.dogAt = DogNow(dog); }
            var sc = LastEveningScenes.Evening(c, time);
            bool was = forcing; forcing = true; // a held scene: no bedtime or meal check, as a story's
            try { Add(Thing(sc, ThingFire)); }
            finally { forcing = was; }
            if (!scenes.Contains(sc)) return "not cast";
            storyHeld.Add(sc); // meals and bedtime don't take its people
            LastEveningScene = sc; LastEveningNow = true; lastEveningSaid = false; LastEveningsStarted++;
            lastEveningDoor = c.door;
            LeanOnLastEvening(lastEveningForce); // #789 tester: the #783 lean takes the camera, as at any held scene (a cheat's: no cooldown)
            lastEveningForce = false;
            // the sim's stamp: the elder stays in from the moment they go in at the door; the History line (its own id, marked read)
            var t = LifeClock.Advance(now, sc.c * Mathf.Max(1f, pv.timeScale));
            float inAt = t.day > now.day ? 23.9f : t.hour;
            var e = sim.MarkLastEvening(who, c.kin, now.day, inAt);
            LastEveningCaption = e?.text;
            planner.Invalidate(); // the elder's day after the door is the stay-in
            return null;
        }

        Vector2 lastEveningDoor;
        /// <summary>The wave this far out from the tent's shown side (box units) [est].</summary>
        const float WaveOut = 0.07f;
        /// <summary>Tests: where the last cast put the fire, the door, the wave and the seat, and what it kept clear.</summary>
        public string LastEveningGeo { get; private set; } = "";

        /// <summary>#789 tester (2026-11-27): the #783 lean on the last evening (HeldMomentLean.BeginScene): its focus is the scene's
        /// (<see cref="FocusNow(CampScene)"/>), its cast <see cref="LastEveningCastDrawn"/>, and it lets go when the scene ends.
        /// <paramref name="noCooldown"/>: the cheat's "play now".</summary>
        public void LeanOnLastEvening(bool noCooldown, bool force = false)
        {
            var sc = LastEveningScene;
            if (sc == null) return;
            var st = new MomentStaging { Focus = () => FocusNow(sc), Cast = into => LastEveningCastDrawn(sc, into) };
            HeldMomentLean.BeginScene(st, -1 - LastEveningsStarted, () => scenes.Contains(sc), noCooldown, force);
        }

        /// <summary>The lean's cast of the last evening: at the fire, the seat and everyone in it; from the walk home on, the elder and those
        /// with them (the ones who stayed at the fire are not the scene then); once the elder is in, the kin at the door.</summary>
        void LastEveningCastDrawn(CampScene s, List<Vector2> into)
        {
            if (s == null || !scenes.Contains(s)) return;
            if (clock - s.t0 < s.d) { CastDrawn(s, into); return; }
            var lead = pv.DrawnAt(s.keys[0]);
            var at = lead ?? lastEveningDoor;
            if (lead is Vector2 l) into.Add(l);
            for (int i = 1; i < s.Count; i++)
                if (s.TryPart(s.keys[i], clock, out _, out _) && pv.DrawnAt(s.keys[i]) is Vector2 p && (p - at).sqrMagnitude < 0.5f * 0.5f) into.Add(p);
        }

        /// <summary>A cheat: play it for <paramref name="elderId"/> at the next look (now: it is dusk). <paramref name="force"/>: skip the rule
        /// (the visit and the spacing; the death set for tonight stays the sim's).</summary>
        public void DebugLastEvening(int elderId, bool force)
        {
            lastEveningCheat = elderId; lastEveningForce = force; lastEveningDone = long.MinValue; lastEveningLook = float.MinValue; lastEveningCheatAt = Time.unscaledTime;
        }
    }

    /// <summary>#789 cheats (SliceDebugPanel's "Last evening (#789)" block; B82 "Debug cheats").</summary>
    public static partial class CampCheats
    {
        static SimCore LeSim => AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;

        /// <summary>The common ground: what plays is ended, the camp's uses cut, the spacing forgotten, and it is dusk.</summary>
        static string LeReady(SimCore sim, PeopleView pv)
        {
            string notes = "";
            int ended = HeldMomentStage.DebugEndPlaying();
            if (ended > 0) notes += $" (ended {ended} moment{(ended == 1 ? "" : "s")})";
            var cv = ChoiceSceneView.Instance;
            if (cv != null && cv.Holding) { cv.End(); notes += " (the held choice let go)"; }
            int cut = pv.Camp != null ? pv.Camp.CutForCheat() : 0;
            if (cut > 0) notes += $" (cut {cut} camp use{(cut == 1 ? "" : "s")})";
            sim.DebugLastEveningClear();
            if (pv.Now.hour < CampLife.LastEveningFrom || pv.Now.hour >= CampLife.LastEveningTo) { PeopleView.DebugSetHour(CampLife.LastEveningFrom + 0.1f); notes += " (skipped to dusk)"; }
            return notes;
        }

        static string Who(SimCore sim, Notable n)
        {
            var with = sim.KinAndFriendsOf(n).Select(k => sim.FindNotable(k)?.name).Where(x => x != null).ToList();
            return $"{n.name} ({Mathf.Floor(n.age)}{(with.Count > 0 ? ", with " + string.Join(", ", with.Take(4)) : ", no kin or friend")})";
        }

        /// <summary>"Last evening: play now": the oldest elder with kin (else a person made 62 with a partner), their death set for tonight, what
        /// plays cleared, dusk, and B82.a plays. Names who.</summary>
        public static string LastEveningPlayNow()
        {
            var sim = LeSim; var pv = PeopleView.Find();
            if (sim == null || pv == null || pv.Camp == null) return "no living box";
            var n = sim.DebugLastEveningElder(true, out string why);
            if (n == null) return "not now: " + why;
            string notes = LeReady(sim, pv);
            pv.Camp.DebugLastEvening(n.id, true);
            return $"last evening: {Who(sim, n)} at the fire at dusk; dies in the night{notes}";
        }

        /// <summary>"Last evening: elder with no kin": the no-scene path: a newcomer of 62 with no kin and no friend, their death set for tonight;
        /// at dusk nothing plays (the rule says why) and they die in the night as today.</summary>
        public static string LastEveningNoKin()
        {
            var sim = LeSim; var pv = PeopleView.Find();
            if (sim == null || pv == null || pv.Camp == null) return "no living box";
            var n = sim.DebugLastEveningElder(false, out string why);
            if (n == null) return "not now: " + why;
            string notes = LeReady(sim, pv);
            pv.Camp.DebugLastEvening(n.id, false);
            return $"{Who(sim, n)} dies in the night: no scene (nobody to sit with them){notes}";
        }

        /// <summary>"Last evening: with a dog": as "play now", the elder given a grown dog of their own first (a camp box: the dogs live there).</summary>
        public static string LastEveningWithDog()
        {
            var sim = LeSim; var pv = PeopleView.Find();
            if (sim == null || pv == null || pv.Camp == null) return "no living box";
            if (!sim.EcologyOn) return "no dogs in this box (a living land: 'Jump: a camp box' first)";
            var n = sim.DebugLastEveningElder(true, out string why);
            if (n == null) return "not now: " + why;
            var d = sim.DebugDogNow(1, DogStage.Grown);
            if (d == null) return "no dog could come";
            d.keeperId = n.id;
            string notes = LeReady(sim, pv);
            pv.Camp.DebugLastEvening(n.id, true);
            return $"last evening: {Who(sim, n)}, {d.name} at their feet; dies in the night{notes}";
        }

        /// <summary>"Last evening: two elders at once": two elders with kin in their last evening tonight; the rule picks one (the pinned, else the
        /// one with the most kin free) and only one scene plays.</summary>
        public static string LastEveningTwo()
        {
            var sim = LeSim; var pv = PeopleView.Find();
            if (sim == null || pv == null || pv.Camp == null) return "no living box";
            var (a, b) = sim.DebugTwoLastEvenings(out string why);
            if (a == null || b == null) return "not now: " + why;
            string notes = LeReady(sim, pv);
            pv.Camp.DebugLastEvening(a.id, false); // the rule chooses (not forced)
            return $"two last evenings tonight: {Who(sim, a)} and {Who(sim, b)}; one scene plays{notes}";
        }
    }
}
