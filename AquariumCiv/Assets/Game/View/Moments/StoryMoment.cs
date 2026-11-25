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
    /// (as the rituals' names). 2.2 (the singer's song) rides the ritual's own moment (B80.d), never a Story moment (<see cref="StoryEvent.Staged"/>).
    /// Batch 3 (B80.b-h): the antlers, the tinkerer, the gardener, the joker, the dry spell, the first snow; the antlers wait for the new leader's moment.
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
            // #722 (1.4, B80.b): the antlers pass right after the new leader's own moment (both queue as the seat changes); a cheat plays alone
            if (!m.cheat && sim.StoryOfEntry(m.entryId) == StoryEvent.AntlersPassed && NewLeaderPending(sim))
            {
                sim.NightWaitMoment(m.id); // it waits, not a try
                LastWhy = "after the new leader's moment";
                return null;
            }
            // #722 (1.3): a second stone made this frame has no camp spot yet; it gets one next frame (the scene raises it there): a wait, not a try
            if (sim.StoryOfEntry(m.entryId) == StoryEvent.SecondStone && !camp.SecondStoneHasSpot)
            {
                sim.NightWaitMoment(m.id);
                LastWhy = "the second stone's camp spot comes next frame";
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

        static bool NewLeaderPending(SimCore sim)
        {
            foreach (var x in sim.WaitingMoments()) if (x.kind == MomentKind.NewLeader) return true;
            foreach (var x in sim.PlayingMoments()) if (x.kind == MomentKind.NewLeader) return true;
            return false;
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

        /// <summary>The season each event's cheat plays in (null: any): 1.1's fruit in summer, 3.3's first snow in winter (snow on the ground).</summary>
        public static Season? SeasonOf(string id) => id switch
        {
            StoryEvent.FirstFruit => Season.Summer,
            StoryEvent.FirstSnow => Season.Winter,
            _ => null,
        };

        /// <summary>The events whose scene is at the pond's edge (B80.c, B80.g): their cheat makes the water first.</summary>
        public static bool NeedsWater(string id) => id is StoryEvent.TinkererBucket or StoryEvent.DrySpell;

        /// <summary>The events at the Keeper's tree (B78.a, B78.b, B80.g): their cheat grows it first if it isn't.</summary>
        static bool NeedsTree(string id) => id is StoryEvent.FirstFruit or StoryEvent.TwoMarks or StoryEvent.DrySpell;

        /// <summary>
        /// The cheats' common ground (they work from any ordinary box, by hand on a phone): every held moment playing now is ended and every camp
        /// use cut, so the scene's people are free; the season the event needs is skipped to (the sim's, so the ground shows it, and the view's
        /// day follows it); the pond made if its scene needs water; a day hour (<paramref name="hour"/>) if it is night. The notes for the status line.
        /// </summary>
        public static string ReadyForCheat(SimCore sim, PeopleView pv, Season? season, bool water, float hour = 9.5f, MomentKind kind = MomentKind.Story)
        {
            string notes = "";
            int ended = HeldMomentStage.DebugEndPlaying();
            if (ended > 0) notes += $" (ended {ended} moment{(ended == 1 ? "" : "s")})";
            var cv = ChoiceSceneView.Instance;
            if (cv != null && cv.Holding) { cv.End(); notes += " (the held choice let go: its card stays)"; } // it holds the box: no moment starts over it
            int cut = pv.Camp != null ? pv.Camp.CutForCheat() : 0;
            if (cut > 0) notes += $" (cut {cut} camp use{(cut == 1 ? "" : "s")})";
            bool moved = false;
            if (water && sim.EcologyOn && sim.EcoPondCells == 0) { notes += " | " + EcoDebug.WaterBreaksNow(); moved = true; }
            if (season is Season want && LifeClock.SeasonOf(sim.State.month) != want)
            {
                for (int i = 0; i < 4 && LifeClock.SeasonOf(sim.State.month) != want; i++) EcoDebug.SkipToNextSeason();
                notes += $" (skipped to {want})";
                moved = true;
            }
            bool fits = LifePlanner.HeldMomentFits(pv.Now, LifePlanner.HeldMomentSeconds(kind));
            if (!fits) notes += " (skipped to the day)";
            if (moved || !fits) PeopleView.DebugSetHour(fits ? pv.Now.hour : hour); // the view's day is the sim's month again (its season with it)
            return notes;
        }

        /// <summary>Who a cheat's line may name: someone the camp can cast now and the planner has free for a moment (awake, not hunting, not at
        /// a tent, not in the ask).</summary>
        public static System.Func<Notable, bool> CastFilter(PeopleView pv) =>
            n => n != null && n.alive && pv != null && pv.Camp != null && pv.Planner != null && pv.Camp.CanCastNow(n.id) && pv.Planner.FreeForHeldMoment(n.id, pv.Now);

        /// <summary>
        /// The "Stories (#722)" cheats: event <paramref name="id"/> happens now (<see cref="SimCore.DebugStory"/>, its gates eased) and its held
        /// moment plays at once while the box is watched. From any ordinary box (<see cref="ReadyForCheat"/>): what plays is ended, the camp's
        /// people freed, the season set (summer for the fruit, winter and snow for the first snow), the pond made for the scenes at its edge, the
        /// dry spell had for 3.2, the morning skipped to if it is night; the line names someone who can be cast now. A line with no moment (a
        /// later couple's marks, moments off) plays its scene as a camp use. Returns the status line, or why not.
        /// </summary>
        public static string DebugPlayNow(string id)
        {
            if (id == StoryEvent.SingerSong) return DebugSingerSong();
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.MomentsOn && GameClock.Instance != null) GameClock.Instance.config.moments = true;
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            string skipped = ReadyForCheat(sim, pv, SeasonOf(id), NeedsWater(id), id == StoryEvent.TwoMarks ? 16.5f : 9.5f); // the marks: toward dusk after the wedding
            if (NeedsTree(id) && !sim.KeeperTreeGrown) skipped += " | " + CampCheats.GrowTree();
            if (id == StoryEvent.FirstSnow) SnowNow(pv);
            string dry = id == StoryEvent.DrySpell ? " | " + EcoDebug.DrySpell() : ""; // B80.g: the "Dry spell now" cheat first (the grass browns at once)
            ChronicleEntry e; string why;
            sim.DebugCastable = CastFilter(pv);
            try { e = sim.DebugStory(id, out why); }
            finally { sim.DebugCastable = null; }
            if (e == null) return "not now: " + why + skipped;
            if (id == StoryEvent.AntlersPassed) UnityEngine.Object.FindAnyObjectByType<BoxView>()?.Refresh(); // a second home may have gone up for the new door
            if (id == StoryEvent.SecondStone && sim.StoryMarks.secondStoneId >= 0 && ConsequencePropsView.Instance != null)
            {   // the stone was made just now: it gets its camp spot before the scene starts (hidden: it is rolled in)
                ConsequencePropsView.HeldBack.Add(sim.StoryMarks.secondStoneId);
                ConsequencePropsView.Instance.Settle();
            }
            var m = sim.MomentOfEntry(e.id);
            if (m == null) return $"{e.text} | {pv.Camp.Force(CampLife.UseOfStory(id))}{skipped}{dry}";
            skipped += dry;
            return PlayHeldNow(sim, m, e, skipped);
        }

        /// <summary>The first snow's cheat: it snows in the box for the scene (the planner's weather; the season's again after 90 s).</summary>
        static void SnowNow(PeopleView pv)
        {
            pv.Planner.ForcedWeather = Weather.Snow;
            pv.Camp?.Later(90f, () =>
            {
                var p = PeopleView.Find();
                if (p != null && p.Planner != null && p.Planner.ForcedWeather == Weather.Snow) p.Planner.ForcedWeather = null;
                return "the first snow's cheat: the weather is the season's again";
            });
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
            if (started.Exists(x => x.id == m.id)) return $"playing: {e.text}{skipped}";
            string why = m.kind == MomentKind.Story ? LastWhy : StagedMoments.LastWhy;
            return $"queued: {e.text} ({stage.LastWhy ?? why}){skipped}";
        }

        /// <summary>"Trait: the singer's song" (2.2, B80.d): a singer and a kept ritual (eased), and the ritual plays now as its held moment; the
        /// song's line is written as it plays and rides that moment: the singer's notes first, everyone's by the end, its line on the caption.</summary>
        public static string DebugSingerSong()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.DebugSingerReady(out string ritual, out string why)) return "not now: " + why;
            string r = StagedMoments.DebugPlayRitualNow(ritual);
            var song = sim.State.chronicle.LastOrDefault(x => x.kind == StoryEvent.Kind(StoryEvent.SingerSong));
            return song != null ? $"{song.text} | {r}" : "the song wasn't made | " + r;
        }

        /// <summary>"People: the Keeper's child marries" (5.2): a grown-up the Keeper named weds now (two unwed grown-ups the planner can stage;
        /// one joins if the box has fewer); what plays is ended and the wedding moment plays with its line on the caption.</summary>
        public static string DebugKeeperChildWed()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.MomentsOn && GameClock.Instance != null) GameClock.Instance.config.moments = true;
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            string skipped = ReadyForCheat(sim, pv, null, false, 9.5f, MomentKind.Wedding);
            ChronicleEntry e; string why;
            sim.DebugCastable = CastFilter(pv); // a couple the planner can stage now (awake, not hunting, not at a tent)
            try { e = sim.DebugKeeperChildWed(out why); }
            finally { sim.DebugCastable = null; }
            if (e == null) return "not now: " + why + skipped;
            var m = sim.MomentOfEntry(e.id);
            return m == null ? e.text + " (no held moment: moments off)" : PlayHeldNow(sim, m, e, skipped);
        }

        /// <summary>The story asks' cheats (4.1-4.4: "Ask: ..."): raised now, its gate eased (<see cref="SimCore.DebugRaiseStoryAsk"/>), staged in
        /// the box by the choice scene and opened on its card. The status line, or why not.</summary>
        public static string DebugAskNow(string kind)
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            HeldMomentStage.DebugEndPlaying(); // the choice scene waits for a playing moment: the cheat's ask is staged now
            var d = sim.DebugRaiseStoryAsk(kind, out string why);
            if (d == null) return "not now: " + why;
            GameClock.Instance?.DilemmaTracker()?.MarkCheat(d.id);
            GameClock.Instance?.DilemmaTracker()?.Observe(sim, GameClock.Tel);
            GameClock.Instance?.Save();
            return $"raised {d.defId} (#{d.id}): {sim.QuestionOf(d)}; {CardStackView.DebugOpenEntry(d.entryId)}";
        }
    }
}
