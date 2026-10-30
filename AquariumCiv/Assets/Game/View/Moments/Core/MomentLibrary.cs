using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.View.Moments
{
    /// <summary>
    /// The era moments as data (era-moment.md §4 camp → village, #89; §5 village → town, #90). Times are the doc's
    /// tables; camera keys are first guesses [est], kept inside BoxCamera's ranges (the doc's ~120° town orbit doesn't
    /// fit the -30..55 yaw range, so it sweeps the whole 85°). Phase 2 may load these into a ScriptableObject so they tune
    /// in the Inspector; these defaults stay the source of truth for tests.
    /// </summary>
    public static class MomentLibrary
    {
        public const string CampToVillageId = "camp_to_village", VillageToTownId = "village_to_town";

        /// <summary>The moment into <paramref name="to"/> (null for the camp, which has none).</summary>
        public static MomentDef For(Era to) => to switch
        {
            Era.Village => CampToVillage(),
            Era.Town => VillageToTown(),
            _ => null,
        };

        static MomentBeat Beat(string id, float start, params BeatAction[] actions) => new() { id = id, start = start, actions = new List<BeatAction>(actions) };

        static CameraKey Key(float at, MomentAnchor a, float yaw, float pitch, float zoom, MomentEase e = MomentEase.Smooth) => new(at, a, yaw, pitch, zoom, e);

        /// <summary>Camp → village, ~60 s (+ the first-time question).</summary>
        public static MomentDef CampToVillage()
        {
            var m = new MomentDef { id = CampToVillageId, toEra = Era.Village };
            var b = m.beats;

            var dawn = Beat("dawn", 0f, BeatAction.Gather);
            dawn.light = MomentLight.Dawn; dawn.audio = "flute";
            dawn.camera.Add(Key(2.5f, MomentAnchor.Hearth, 15f, 22f, 0.55f)); // low over the fire, three-quarter view
            b.Add(dawn);

            var chief = Beat("chief", 8f, BeatAction.LeaderSpeaks, BeatAction.PlantStake);
            chief.when = BeatCondition.LeaderPresent; chief.line = "We stay. This is home."; chief.audio = "drum";
            chief.camera.Add(Key(1.2f, MomentAnchor.Leader, 20f, 18f, 0.38f));
            b.Add(chief);

            var elder = Beat("elder", 8f, BeatAction.LeaderSpeaks, BeatAction.PlantStake); // no chief: the eldest speaks
            elder.when = BeatCondition.LeaderMissing; elder.line = "We stay. This is home."; elder.audio = "drum";
            elder.camera.Add(Key(1.2f, MomentAnchor.Elder, 20f, 18f, 0.38f));
            b.Add(elder);

            var green = Beat("green", 15f, BeatAction.GreenSpreads);
            green.audio = "tune_up";
            green.camera.Add(Key(2f, MomentAnchor.Hearth, 22f, 30f, 0.7f));
            b.Add(green);

            var wave = Beat("wave", 20f, BeatAction.RebuildWave);
            wave.param = "village"; wave.audio = "knock_scale"; wave.haptic = true;
            wave.camera.Add(Key(1.5f, MomentAnchor.Hearth, 50f, 32f, 0.85f));
            wave.camera.Add(Key(18f, MomentAnchor.Box, -10f, 38f, 1f)); // ~60° orbit, pulling back to the whole box
            b.Add(wave);

            var headman = Beat("headman", 40f, BeatAction.PassSash);
            headman.when = BeatCondition.LeaderPresent; headman.line = "Headman {leader}, then.";
            headman.camera.Add(Key(1.2f, MomentAnchor.Leader, 10f, 20f, 0.4f));
            b.Add(headman);

            var vote = Beat("vote", 40f, BeatAction.Vote);
            vote.when = BeatCondition.LeaderMissing; vote.line = "The village chose its headman.";
            vote.camera.Add(Key(1.2f, MomentAnchor.Hearth, 10f, 24f, 0.5f));
            b.Add(vote);

            var cheer = Beat("cheer", 46f, BeatAction.Cheer);
            cheer.light = MomentLight.Midday; cheer.audio = "chord";
            cheer.caption = "{place} · a farming village · year {year}";
            cheer.camera.Add(Key(1.5f, MomentAnchor.Box, 28f, 30f, 0.95f)); // everyone faces the glass
            b.Add(cheer);

            var tool = Beat("tool", 53f, BeatAction.ToolUnlock);
            tool.param = "channel"; tool.line = "Drag from the water"; tool.audio = "chime";
            b.Add(tool);

            var festival = Beat("festival", 58f, BeatAction.FestivalStart);
            festival.freeCamera = true; festival.hold = 7f;
            b.Add(festival);

            var ask = Beat("notify", 65f, BeatAction.AskNotification);
            ask.when = BeatCondition.FirstMomentOnly; ask.line = "Want a note when the next one is ready?"; ask.hold = 6f;
            b.Add(ask);
            return m;
        }

        /// <summary>Village → town, ~90 s.</summary>
        public static MomentDef VillageToTown()
        {
            var m = new MomentDef { id = VillageToTownId, toEra = Era.Town };
            var b = m.beats;

            var bell = Beat("bell", 0f, BeatAction.BellRing, BeatAction.Gather);
            bell.light = MomentLight.Morning; bell.audio = "bell_fiddle";
            bell.camera.Add(Key(2.5f, MomentAnchor.Hearth, 30f, 55f, 0.9f)); // high over the green
            b.Add(bell);

            var speech = Beat("headman", 10f, BeatAction.LeaderSpeaks);
            speech.when = BeatCondition.LeaderPresent; speech.line = "We've outgrown ourselves."; speech.audio = "cheer_small";
            speech.camera.Add(Key(1.2f, MomentAnchor.Leader, 20f, 20f, 0.4f));
            b.Add(speech);

            var elder = Beat("elder", 10f, BeatAction.LeaderSpeaks);
            elder.when = BeatCondition.LeaderMissing; elder.line = "We've outgrown ourselves."; elder.audio = "cheer_small";
            elder.camera.Add(Key(1.2f, MomentAnchor.Elder, 20f, 20f, 0.4f));
            b.Add(elder);

            var square = Beat("square", 18f, BeatAction.SquareSpreads);
            square.audio = "cobble_taps";
            square.camera.Add(Key(2f, MomentAnchor.Square, 25f, 40f, 0.75f));
            b.Add(square);

            var cleared = Beat("cleared", 28f, BeatAction.ClearHearth);
            cleared.when = BeatCondition.SomethingCleared; cleared.audio = "pause_note";
            cleared.camera.Add(Key(1.5f, MomentAnchor.Square, 15f, 30f, 0.55f));
            b.Add(cleared);

            var wave = Beat("wave", 35f, BeatAction.RebuildWave);
            wave.param = "town"; wave.audio = "hammer_scale"; wave.haptic = true;
            wave.camera.Add(Key(1.5f, MomentAnchor.Square, 55f, 34f, 0.85f));
            wave.camera.Add(Key(28f, MomentAnchor.Box, -30f, 40f, 1.05f)); // the long orbit: the whole 85° BoxCamera allows
            b.Add(wave);

            var lord = Beat("lord", 65f, BeatAction.LordChair);
            lord.when = BeatCondition.LeaderPresent;
            lord.camera.Add(Key(1.2f, MomentAnchor.Steps, 20f, 22f, 0.42f));
            b.Add(lord);

            var vote = Beat("vote", 65f, BeatAction.Vote, BeatAction.LordChair);
            vote.when = BeatCondition.LeaderMissing; vote.line = "The town chose its first lord.";
            vote.camera.Add(Key(1.2f, MomentAnchor.Steps, 20f, 24f, 0.5f));
            b.Add(vote);

            var cheer = Beat("cheer", 75f, BeatAction.Cheer);
            cheer.light = MomentLight.Midday; cheer.audio = "chord";
            cheer.caption = "{place} · a town of crafts and guilds · year {year}";
            cheer.camera.Add(Key(1.5f, MomentAnchor.Box, 28f, 30f, 0.95f));
            b.Add(cheer);

            var tool = Beat("tool", 82f, BeatAction.ToolUnlock);
            tool.param = "outcrop"; tool.line = "Press and hold on a hill"; tool.audio = "chime";
            b.Add(tool);

            var festival = Beat("festival", 88f, BeatAction.FestivalStart);
            festival.freeCamera = true; festival.hold = 7f;
            b.Add(festival);

            // Only if the first moment ended before its question (the player left after the cheer).
            var ask = Beat("notify", 95f, BeatAction.AskNotification);
            ask.when = BeatCondition.FirstMomentOnly; ask.line = "Want a note when the next one is ready?"; ask.hold = 6f;
            b.Add(ask);
            return m;
        }
    }
}
