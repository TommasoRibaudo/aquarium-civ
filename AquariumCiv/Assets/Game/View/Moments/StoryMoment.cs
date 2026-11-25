using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #722: a story event as a live moment (docs/design/new-events.md §1.1-1.3, animation-specs B78.a-c). Registered with
    /// <see cref="HeldMomentStage"/> for <see cref="MomentKind.Story"/>: CampLife casts and starts the event's scene (the first fruit, the two
    /// marks, the second stone, B80.a the Keeper's child: <see cref="CampLife.StartStory"/>), and the line is said on the caption as it starts
    /// (as the rituals' names). 2.2 (the singer's song) has no scene yet: its line is a card, never a Story moment (<see cref="StoryEvent.Staged"/>).
    /// The moment is done when the scene has ended. Not castable now (its people busy or away): null, so it waits and, after a few tries, is a
    /// card; at night it waits for the morning.
    /// </summary>
    public static class StoryMoment
    {
        /// <summary>How long the line stays on the caption (real seconds) [est].</summary>
        public const float CaptionSeconds = 6f;
        /// <summary>Why the last try started nothing (null: it started).</summary>
        public static string LastWhy { get; private set; } = "";
        /// <summary>The line said for the last story staged (tests, the panel).</summary>
        public static string LastCaption { get; private set; }
        public static int Started { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => HeldMomentStage.Register(MomentKind.Story, Stage);

        /// <summary>Stages <paramref name="m"/> now, or null (it waits; a card after several tries).</summary>
        public static MomentStaging Stage(HeldMoment m, SimCore sim)
        {
            var pv = PeopleView.Find();
            var camp = pv != null ? pv.Camp : null;
            if (camp == null || pv.Planner == null || m == null || sim == null) { LastWhy = "no people view"; return null; }
            if (!LifePlanner.HeldMomentFits(pv.Now, LifePlanner.HeldMomentSeconds(MomentKind.Story)))
            {
                sim.NightWaitMoment(m.id); // it waits for the morning in the queue, not a try
                LastWhy = "night: it waits for the morning";
                return null;
            }
            var scene = camp.StartStory(sim, m);
            if (scene == null) { LastWhy = camp.LastStoryWhy; return null; }
            var entry = Entry(sim, m.entryId);
            if (entry != null)
            {
                LastCaption = entry.text;
                ShowMeDirector.Instance?.Say(entry.text, PersonRef.None, CaptionSeconds, LineKind.Echo);
            }
            LastWhy = null;
            Started++;
            return new MomentStaging { Done = () => !camp.StoryPlaying(scene), Stop = () => camp.StopStory(scene) };
        }

        static ChronicleEntry Entry(SimCore sim, int id)
        {
            var ch = sim.State.chronicle;
            for (int i = ch.Count - 1; i >= 0; i--) if (ch[i].id == id) return ch[i];
            return null;
        }

        static SimCore Sim
        {
            get
            {
                var c = GameClock.Instance;
                return c != null && c.Sim != null && ReferenceEquals(c.Sim, c.ActiveSim) ? c.ActiveSim : null;
            }
        }

        /// <summary>
        /// The "Stories (#722)" cheats: event <paramref name="id"/> happens now (<see cref="SimCore.DebugStory"/>, its gates eased) and its held
        /// moment plays at once while the box is watched (the morning skipped to if it is night). A line with no moment (a later couple's marks,
        /// moments off) plays its scene as a camp use. Returns the status line, or why not.
        /// </summary>
        public static string DebugPlayNow(string id)
        {
            if (id == StoryEvent.SingerSong) return DebugSingerSong();
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.MomentsOn && GameClock.Instance != null) GameClock.Instance.config.moments = true;
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            if (sim.PlayingMoments().Count > 0) return "a moment is playing: wait, or End staged moments";
            string skipped = "";
            if (!LifePlanner.HeldMomentFits(pv.Now, LifePlanner.HeldMomentSeconds(MomentKind.Story)))
            {
                PeopleView.DebugSetHour(id == StoryEvent.TwoMarks ? 16.5f : 9.5f); // the marks: toward dusk after the wedding
                skipped = " (skipped to the day)";
            }
            var e = sim.DebugStory(id, out string why);
            if (e == null) return "not now: " + why;
            var m = sim.MomentOfEntry(e.id);
            if (m == null) return $"{e.text} | {pv.Camp.Force(CampLife.UseOfStory(id))}{skipped}";
            return PlayHeldNow(sim, m, e, skipped);
        }

        /// <summary>A cheat's held moment to the top of the queue and staged now; the status line.</summary>
        static string PlayHeldNow(SimCore sim, HeldMoment m, ChronicleEntry e, string skipped)
        {
            m.priority = SimCore.DebugMomentPriority + m.priority;
            m.cheat = true;
            PeopleView.Find()?.Planner?.Invalidate();
            sim.DebugMomentDueNow();
            var stage = HeldMomentStage.Instance;
            if (stage == null) return e.text + " (no moment stage in the scene)";
            var started = stage.Check();
            return started.Exists(x => x.id == m.id) ? $"playing: {e.text}{skipped}" : $"queued: {e.text} ({stage.LastWhy ?? LastWhy}){skipped}";
        }

        /// <summary>"Trait: the singer's song" (2.2): a singer and a kept ritual (eased), and the ritual plays now as its held moment; the song's
        /// line is written as it plays. Its own scene is not built yet: the line is a card (the next return), not a moment.</summary>
        public static string DebugSingerSong()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.DebugSingerReady(out string ritual, out string why)) return "not now: " + why;
            string r = StagedMoments.DebugPlayRitualNow(ritual);
            var song = sim.State.chronicle.LastOrDefault(x => x.kind == StoryEvent.Kind(StoryEvent.SingerSong));
            return song != null ? $"{song.text} (a card) | {r}" : "the song wasn't made | " + r;
        }

        /// <summary>"People: the Keeper's child marries" (5.2): a grown-up the Keeper named weds now; the wedding moment plays with its line on
        /// the caption.</summary>
        public static string DebugKeeperChildWed()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.MomentsOn && GameClock.Instance != null) GameClock.Instance.config.moments = true;
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            if (sim.PlayingMoments().Count > 0) return "a moment is playing: wait, or End staged moments";
            string skipped = "";
            if (!LifePlanner.HeldMomentFits(pv.Now, LifePlanner.HeldMomentSeconds(MomentKind.Wedding))) { PeopleView.DebugSetHour(9.5f); skipped = " (skipped to the day)"; }
            var e = sim.DebugKeeperChildWed(out string why);
            if (e == null) return "not now: " + why;
            var m = sim.MomentOfEntry(e.id);
            return m == null ? e.text + " (no held moment: moments off)" : PlayHeldNow(sim, m, e, skipped);
        }

        /// <summary>The story asks' cheats (4.1-4.4: "Ask: ..."): raised now, its gate eased (<see cref="SimCore.DebugRaiseStoryAsk"/>), staged in
        /// the box by the choice scene and opened on its card. The status line, or why not.</summary>
        public static string DebugAskNow(string kind)
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            var d = sim.DebugRaiseStoryAsk(kind, out string why);
            if (d == null) return "not now: " + why;
            GameClock.Instance?.DilemmaTracker()?.MarkCheat(d.id);
            GameClock.Instance?.DilemmaTracker()?.Observe(sim, GameClock.Tel);
            GameClock.Instance?.Save();
            return $"raised {d.defId} (#{d.id}): {sim.QuestionOf(d)}; {CardStackView.DebugOpenEntry(d.entryId)}";
        }
    }
}
