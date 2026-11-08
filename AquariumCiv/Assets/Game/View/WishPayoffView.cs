using System.Collections.Generic;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #191: a wish coming true feels like something. Watches the sim's wishes and plays what <see cref="WishPayoff.Plan"/> says when one
    /// turns Given (the gift, by a wish's Give or a plain placing) or Met (a smaller beat): the sound, puffs where the gift lands, the wisher
    /// cheering with their own words, the camera toward them when they're off screen. At night with the wisher asleep, someone awake
    /// says it and the wisher does when they wake. View only: it reads the sim and writes nothing to it; a wish closed while the box
    /// was catching up isn't played.
    /// </summary>
    public class WishPayoffView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static WishPayoffView Instance { get; private set; }

        const float CheckSeconds = 0.2f, NightFrom = 20f, NightUntil = 6f, FlyGiveUpSeconds = 3f;
        /// <summary>A wish closed further back than this was the sim catching up, not something the player just saw.</summary>
        const int FreshMonths = 2;

        readonly HashSet<int> given = new(), met = new();
        readonly List<(int id, PeopleQuote line, float wakeBy)> waking = new();
        readonly List<int> awake = new();
        readonly List<PeopleQuote> held = new();
        float nextHeld;
        SimCore bound;
        float nextCheck;
        int flyTo = -1;
        float flyUntil;

        /// <summary>The last plan played, and how many (tests, the debug panel).</summary>
        public WishPayoffPlan LastPlan { get; private set; }
        public int Played { get; private set; }

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<WishPayoffView>() == null) b.gameObject.AddComponent<WishPayoffView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
        }

        void OnDisable() { if (Instance == this) Instance = null; }

        void Update()
        {
            StepFly();
            StepHeld();
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckSeconds;
            var sim = Sim;
            if (sim == null) { bound = null; return; }
            if (!ReferenceEquals(sim, bound)) { Bind(sim); return; }
            foreach (var w in sim.State.wishes)
            {
                if (w.state == WishState.Given && given.Add(w.id)) Pay(sim, w, WishBeat.Given);
                else if (w.state == WishState.Met && met.Add(w.id)) Pay(sim, w, WishBeat.Met);
            }
            StepWaking(sim);
        }

        /// <summary>A new box: what's already closed isn't news.</summary>
        void Bind(SimCore sim)
        {
            bound = sim;
            given.Clear(); met.Clear(); waking.Clear(); held.Clear();
            flyTo = -1;
            foreach (var w in sim.State.wishes)
            {
                if (w.state == WishState.Given) given.Add(w.id);
                if (w.state == WishState.Met) met.Add(w.id);
            }
        }

        void Pay(SimCore sim, Wish wish, WishBeat beat)
        {
            if (sim.State.month - wish.endMonth > FreshMonths) return;
            var pv = PeopleView.Find();
            var now = pv != null ? pv.Now : new ViewTime(sim.State.month, 10f);
            bool night = now.hour >= NightFrom || now.hour < NightUntil;
            ReadAwake(pv);
            var plan = WishPayoff.Plan(sim, wish, beat, night, awake.Contains, awake, OnScreen(pv, wish.notableId));
            if (plan == null) return;
            LastPlan = plan;
            Played++;
            Debug.Log($"[Wish] {beat} {wish.kind} for {wish.notableId}, night {night}, says {plan.wisherSaysNow}, fly {plan.flyToWisher}");
            Sfx.Play(plan.cue, 0f, plan.quiet ? QuietVolume : 1f);
            var mp = MomentPlayer.Instance;
            if (mp != null && plan.effectNow) mp.GiftPuffs(wish.x, wish.z, beat == WishBeat.Given ? 1.4f : 1f, beat == WishBeat.Given ? 8 : 0);
            if (mp != null && night && beat == WishBeat.Given && !plan.wisherSaysNow && plan.witnessLine == null) mp.GiftGlow(wish.x, wish.z); // nobody awake to say it
            if (plan.wisherSaysNow) Tell(pv, now, plan.wisherId, plan.wisherLine);
            else if (plan.wisherLine != null) waking.Add((plan.wisherId, plan.wisherLine, Time.unscaledTime + WakeGiveUpSeconds));
            if (plan.witnessLine != null) Say(plan.witnessLine);
            if (plan.flyToWisher && plan.wisherSaysNow && beat == WishBeat.Given) { flyTo = plan.wisherId; flyUntil = Time.unscaledTime + FlyGiveUpSeconds; }
        }

        const float QuietVolume = 0.55f, WakeGiveUpSeconds = 600f;

        void Tell(PeopleView pv, ViewTime now, int id, PeopleQuote line)
        {
            pv?.Planner?.WisherReacts(id, now);
            Say(line);
        }

        void Say(PeopleQuote line)
        {
            if (line == null) return;
            var show = ShowMeDirector.Instance;
            var mp = MomentPlayer.Instance;
            if (show == null) return;
            var first = FirstRunView.Instance;
            if (first != null && first.HasTheWord) { held.Add(line); return; } // the first run's words: this waits its turn (tester run 6)
            if (mp != null && mp.Playing) { held.Add(line); return; } // #327: a moment's words too, rather than dropping it
            show.SayOrWait(line.text, PersonRef.Notable(line.speakerId), PeopleVoice.SecondsFor(line.text), LineKind.Wish); // #327: busy, it waits its turn
        }

        /// <summary>Lines kept back while the first run had the word. Said one at a time once it's passed, oldest first.</summary>
        void StepHeld()
        {
            if (held.Count == 0) return;
            var first = FirstRunView.Instance;
            if (first != null && first.HasTheWord) return;
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return;
            if (Time.unscaledTime < nextHeld || !GameUiPanel.Bubbles.Free(BubbleHost.Rank.World)) return; // someone is speaking: after them
            var line = held[0];
            held.RemoveAt(0);
            nextHeld = Time.unscaledTime + PeopleVoice.SecondsFor(line.text);
            Say(line);
        }

        /// <summary>Lines waiting for the first run's word to pass (tests).</summary>
        public int HeldLines => held.Count;

        /// <summary>Who is awake now: the plan's own view of it, not a second idea of bedtime.</summary>
        void ReadAwake(PeopleView pv) // not "Awake": Unity takes that name for its own message and logs an error
        {
            awake.Clear();
            if (pv == null) return;
            foreach (var a in pv.Activities)
                if (a.who.IsNotable && a.kind != ActivityKind.Sleep && a.kind != ActivityKind.None) awake.Add(a.who.notableId);
        }

        static bool OnScreen(PeopleView pv, int id)
        {
            var cam = Camera.main;
            if (pv == null || cam == null || !pv.TryGetDollTop(PersonRef.Notable(id), out var w)) return false;
            var v = cam.WorldToViewportPoint(w);
            var box = cam.GetComponent<BoxCamera>();
            if (box != null && box.zoom > BoxCamera.NudgeZoom + 0.02f) return false; // in frame at the whole-box view, but a dozen pixels tall: not easy to spot
            return v.z > 0f && v.x > 0.08f && v.x < 0.92f && v.y > 0.3f && v.y < 0.92f; // over the tools' lane isn't seen
        }

        void StepWaking(SimCore sim)
        {
            if (waking.Count == 0) return;
            var pv = PeopleView.Find();
            ReadAwake(pv);
            for (int i = waking.Count - 1; i >= 0; i--)
            {
                var (id, line, by) = waking[i];
                if (Time.unscaledTime > by || sim.FindNotable(id) is not { alive: true }) { waking.RemoveAt(i); continue; }
                if (!awake.Contains(id) || !pv.TryGetDollTop(PersonRef.Notable(id), out _)) continue;
                waking.RemoveAt(i);
                Tell(pv, pv.Now, id, line);
            }
        }

        void StepFly()
        {
            if (flyTo < 0) return;
            if (Time.unscaledTime > flyUntil) { flyTo = -1; return; }
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            var pv = PeopleView.Find();
            if (SomethingOpen()) { flyTo = -1; return; } // the player is reading: dropped, not queued (tester run 6)
            if (cam == null || pv == null || cam.Touching || cam.IsFlying) return; // the finger that gave it may still be down; the show's own flight goes first
            if (pv.TryGetDollPosition(PersonRef.Notable(flyTo), out var p))
                cam.NudgeToward(pv.transform.TransformPoint(new Vector3(p.x, 0f, p.y)));
            flyTo = -1;
        }

        /// <summary>A person's card or any sheet is up: the camera stays with what the player is reading.</summary>
        static bool SomethingOpen()
        {
            var card = PersonCardView.Instance;
            if (card != null && (card.AnyOpen || card.BuildingOpen)) return true;
            var hud = GameHud.Instance;
            if (hud != null && hud.SheetOpen) return true;
            var book = BookView.Instance;
            if (book != null && book.IsOpen) return true;
            var lapse = TimeLapseScreenView.Instance;
            if (lapse != null && lapse.IsOpen) return true;
            var note = FeedbackUI.Instance;
            return note != null && note.IsOpen;
        }

        /// <summary>Debug: the glow a gift leaves when everyone sleeps, at the box's middle, day or night.</summary>
        public static string DebugGlow()
        {
            var mp = MomentPlayer.Instance;
            if (mp == null) return "no MomentPlayer";
            mp.GiftGlow(0f, 0f);
            return "gift glow at the middle";
        }

        /// <summary>Debug: a wish now if none is open, and give it. Any tool's charge is spent like a real gift.</summary>
        public static string DebugGrant()
        {
            var sim = Sim;
            if (sim == null) return "no sim";
            var w = sim.OpenWish ?? sim.DebugForceWish();
            if (w == null) return "nobody can wish";
            var r = sim.GiveWish(w.id);
            return r.Ok ? $"{sim.FindNotable(w.notableId)?.name} got their {w.kind}" : $"{w.kind} can't be given: {r.fail}";
        }
    }
}
