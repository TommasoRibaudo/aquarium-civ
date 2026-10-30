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
        public const string CampToVillageId = "camp_to_village", VillageToTownId = "village_to_town", LastMomentId = "last_moment";

        /// <summary>The moment into <paramref name="to"/> (null for the camp, which has none).</summary>
        public static MomentDef For(Era to) => to switch
        {
            Era.Village => CampToVillage(),
            Era.Town => VillageToTown(),
            _ => null,
        };

        /// <summary>The last moment (era-moment.md §8, #93), ~2.7 min. The crossing beat carries the Cheer action: the director's
        /// callback completes the box there (nothing moves before it, so a moment left earlier replays).</summary>
        public static MomentDef LastMoment()
        {
            var m = new MomentDef { id = LastMomentId, toEra = Era.Town };
            var b = m.beats;
            MomentBeat Add(string id, float start, MomentLight light, string audio, string line, params BeatAction[] a)
            {
                var x = Beat(id, start, a); x.light = light; x.audio = audio; x.line = line; b.Add(x); return x;
            }
            var evening = Add("evening", 0f, MomentLight.Dusk, "fiddle", "", BeatAction.Gather);
            evening.camera.Add(Key(3f, MomentAnchor.Steps, 20f, 24f, 0.6f));
            var look = Add("lookback", 15f, MomentLight.Keep, "", "{lookback}", BeatAction.LeaderSpeaks);
            look.camera.Add(Key(2f, MomentAnchor.Leader, 22f, 20f, 0.42f)); look.camera.Add(Key(26f, MomentAnchor.Hearth, 10f, 30f, 0.8f));
            var lant = Add("lanterns", 45f, MomentLight.Keep, "chime", "", BeatAction.LegendLanterns);
            lant.camera.Add(Key(3f, MomentAnchor.Box, 28f, 14f, 0.9f)); lant.camera.Add(Key(22f, MomentAnchor.Box, 28f, 10f, 1.05f));
            var founders = Add("founders", 70f, MomentLight.Keep, "", "", BeatAction.GatherFounders);
            founders.camera.Add(Key(2f, MomentAnchor.Box, 40f, 24f, 0.75f));
            var shelf = Add("shelf", 85f, MomentLight.Keep, "thump", "", BeatAction.SetDownBox);
            shelf.camera.Add(Key(5f, MomentAnchor.Shelf, 12f, 36f, 2.2f, MomentEase.EaseOut));
            var cross = Add("crossing", 95f, MomentLight.Keep, "fiddle", "", BeatAction.Crossing, BeatAction.Cheer);
            cross.camera.Add(Key(4f, MomentAnchor.Shelf, 8f, 30f, 2.2f)); cross.camera.Add(Key(18f, MomentAnchor.NewBox, 8f, 26f, 1.6f));
            var wipe = Add("wipe", 115f, MomentLight.Keep, "chime", "One last wipe", BeatAction.LastWipe);
            wipe.camera.Add(Key(3f, MomentAnchor.Shelf, 18f, 24f, 2.1f));
            var plate = Add("plate", 125f, MomentLight.Keep, "", "", BeatAction.EngravePlate);
            plate.camera.Add(Key(3f, MomentAnchor.Box, 6f, 20f, 0.55f));
            var roll = Add("rollcall", 135f, MomentLight.Keep, "", "", BeatAction.RollCall);
            roll.camera.Add(Key(3f, MomentAnchor.Shelf, 14f, 30f, 2.1f));
            var arrive = Add("newbox", 155f, MomentLight.Morning, "flute", "", BeatAction.ArriveNewBox);
            arrive.hold = 6f; arrive.freeCamera = false;
            arrive.camera.Add(Key(4f, MomentAnchor.NewBox, 20f, 28f, 0.9f));
            return m;
        }

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
            wave.camera.Add(Key(1.5f, MomentAnchor.Hearth, 50f, 30f, 0.62f)); // close enough that each rebuild reads at phone size (P6)
            wave.camera.Add(Key(18f, MomentAnchor.Hearth, -10f, 36f, 0.85f)); // ~60° orbit, pulling back towards the whole village
            b.Add(wave);

            var headman = Beat("headman", 40f, BeatAction.PassSash);
            headman.when = BeatCondition.LeaderPresent; headman.line = "Headman {leader}, then.";
            headman.camera.Add(Key(1.2f, MomentAnchor.Leader, 10f, 20f, 0.4f));
            b.Add(headman);

            var vote = Beat("vote", 40f, BeatAction.Vote);
            vote.when = BeatCondition.LeaderMissing; vote.line = "The village chose {winner} as its headman.";
            vote.camera.Add(Key(1.2f, MomentAnchor.Hearth, 10f, 24f, 0.5f));
            b.Add(vote);

            var cheer = Beat("cheer", 46f, BeatAction.Cheer);
            cheer.light = MomentLight.Midday; cheer.audio = "chord";
            cheer.caption = "{place} · a farming village · year {year}";
            cheer.camera.Add(Key(1.5f, MomentAnchor.Hearth, 28f, 26f, 0.6f)); // everyone faces the glass: close enough to see them cheer
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
            square.camera.Add(Key(2f, MomentAnchor.Square, 25f, 36f, 0.55f)); // close on the cobbles and stalls
            b.Add(square);

            var cleared = Beat("cleared", 28f, BeatAction.ClearHearth);
            cleared.when = BeatCondition.SomethingCleared; cleared.audio = "pause_note";
            cleared.camera.Add(Key(1.5f, MomentAnchor.Square, 15f, 30f, 0.55f));
            b.Add(cleared);

            var wave = Beat("wave", 35f, BeatAction.RebuildWave);
            wave.param = "town"; wave.audio = "hammer_scale"; wave.haptic = true;
            wave.camera.Add(Key(1.5f, MomentAnchor.Square, 55f, 32f, 0.65f)); // close enough that each rebuild reads at phone size (P6)
            wave.camera.Add(Key(28f, MomentAnchor.Square, -30f, 38f, 0.9f)); // the long orbit: the whole 85° BoxCamera allows, pulling back
            b.Add(wave);

            var lord = Beat("lord", 65f, BeatAction.LordChair);
            lord.when = BeatCondition.LeaderPresent;
            lord.camera.Add(Key(1.2f, MomentAnchor.Steps, 20f, 22f, 0.42f));
            b.Add(lord);

            var vote = Beat("vote", 65f, BeatAction.Vote, BeatAction.LordChair);
            vote.when = BeatCondition.LeaderMissing; vote.line = "The town chose {winner} as its first lord.";
            vote.camera.Add(Key(1.2f, MomentAnchor.Steps, 20f, 24f, 0.5f));
            b.Add(vote);

            var cheer = Beat("cheer", 75f, BeatAction.Cheer);
            cheer.light = MomentLight.Midday; cheer.audio = "chord";
            cheer.caption = "{place} · a town of crafts and guilds · year {year}";
            cheer.camera.Add(Key(1.5f, MomentAnchor.Square, 28f, 26f, 0.6f)); // the crowd cheering on the square
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
