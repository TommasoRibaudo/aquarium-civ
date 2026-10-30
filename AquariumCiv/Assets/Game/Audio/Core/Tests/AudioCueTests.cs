using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;

namespace AquariumCiv.Audio.Tests
{
    /// <summary>#96: the pure sound model (cues, gate, mix and duck, ambience, the moment timeline, haptics). Runs in
    /// Unity and in Tools/SimTests (dotnet test).</summary>
    public class AudioCueTests
    {
        // ---------- the library ----------

        [Test]
        public void EveryCueHasClipsAndSaneNumbers()
        {
            Assert.That(CueLibrary.All.Count, Is.GreaterThan(30));
            foreach (var c in CueLibrary.All.Values)
            {
                Assert.That(c.clips, Is.Not.Empty, c.id);
                Assert.That(c.volume, Is.InRange(0.05f, 1f), c.id);
                Assert.That(c.maxVoices, Is.GreaterThanOrEqualTo(1), c.id);
                Assert.That(c.pitchJitter, Is.InRange(0f, 3f), c.id);
                if (c.bus == SoundBus.Ui) Assert.That(c.volume, Is.LessThanOrEqualTo(0.6f), $"{c.id}: UI stays under the world");
                if (c.bus == SoundBus.Ambience) Assert.That(c.loop, Is.True, c.id);
            }
        }

        [Test]
        public void EveryClipFileIsInTheProject()
        {
            string root = ResourcesDir();
            if (root == null) Assert.Inconclusive("Assets/Audio/Resources not found from here");
            foreach (var c in CueLibrary.All.Values)
                foreach (var clip in c.clips)
                {
                    if (clip.StartsWith("proc:")) continue;
                    bool found = new[] { ".ogg", ".wav", ".mp3" }.Any(ext => File.Exists(Path.Combine(root, clip + ext)));
                    Assert.That(found, Is.True, $"{c.id}: {clip} missing under {root}");
                }
        }

        [Test]
        public void EveryMomentAudioIdIsMapped()
        {
            foreach (var era in new[] { Era.Village, Era.Town })
            {
                var def = MomentLibrary.For(era);
                foreach (var b in def.beats)
                {
                    if (string.IsNullOrEmpty(b.audio)) continue;
                    Assert.That(MomentCues.AudioIds.ContainsKey(b.audio), Is.True, $"{def.id}/{b.id}: '{b.audio}'");
                    foreach (var (cue, _) in MomentCues.AudioIds[b.audio]) Assert.That(CueLibrary.Get(cue), Is.Not.Null, cue);
                }
            }
        }

        [Test]
        public void GesturesMapToCues()
        {
            Assert.That(CueLibrary.ForGesture(KeeperGesture.FullClean), Is.EqualTo(CueLibrary.FullClean));
            Assert.That(CueLibrary.ForGesture(KeeperGesture.FlickTrash), Is.EqualTo(CueLibrary.MessFlick));
            Assert.That(CueLibrary.ForGesture(KeeperGesture.Glance), Is.Null);
            foreach (KeeperGesture g in Enum.GetValues(typeof(KeeperGesture)))
            {
                var id = CueLibrary.ForGesture(g);
                if (id != null) Assert.That(CueLibrary.Get(id), Is.Not.Null, g.ToString());
            }
        }

        // ---------- the gate ----------

        static CueDef Test(string id, int voices = 2, float cooldown = 0f, bool steal = false, int prio = 50, float len = 1f, params string[] clips)
            => new CueDef(id, SoundBus.World, 0.5f, clips.Length > 0 ? clips : new[] { "a" }) { maxVoices = voices, cooldown = cooldown, steal = steal, priority = prio, length = len };

        static CueGate Gate(params CueDef[] cues)
        {
            var d = cues.ToDictionary(c => c.id);
            return new CueGate(7, id => d.TryGetValue(id, out var c) ? c : null);
        }

        [Test]
        public void UnknownCueDoesNotPlay()
        {
            Assert.That(new CueGate().TryPlay("nope", 0f, out _), Is.False);
        }

        [Test]
        public void CooldownDropsRepeats()
        {
            var g = Gate(Test("t", voices: 5, cooldown: 0.2f));
            Assert.That(g.TryPlay("t", 0f, out _), Is.True);
            Assert.That(g.TryPlay("t", 0.1f, out _), Is.False);
            Assert.That(g.TryPlay("t", 0.25f, out _), Is.True);
        }

        [Test]
        public void VoiceLimitDropsOrSteals()
        {
            var g = Gate(Test("drop", voices: 2), Test("steal", voices: 1, steal: true));
            Assert.That(g.TryPlay("drop", 0f, out _), Is.True);
            Assert.That(g.TryPlay("drop", 0.1f, out _), Is.True);
            Assert.That(g.TryPlay("drop", 0.2f, out _), Is.False, "third voice dropped");
            Assert.That(g.TryPlay("drop", 1.05f, out _), Is.True, "the first has ended");

            Assert.That(g.TryPlay("steal", 0f, out var first), Is.True);
            Assert.That(g.TryPlay("steal", 0.1f, out var second), Is.True);
            Assert.That(second.stolenVoice, Is.EqualTo(first.voice));
            Assert.That(g.Active(0.2f, "steal"), Is.EqualTo(1));
        }

        [Test]
        public void FullMixGivesWayByPriority()
        {
            var cues = Enumerable.Range(0, CueGate.MaxTotalVoices).Select(i => Test("low" + i, prio: 10)).ToList();
            cues.Add(Test("high", prio: 90));
            cues.Add(Test("lower", prio: 5));
            var g = Gate(cues.ToArray());
            for (int i = 0; i < CueGate.MaxTotalVoices; i++) Assert.That(g.TryPlay("low" + i, 0f, out _), Is.True);
            Assert.That(g.TryPlay("lower", 0.1f, out _), Is.False, "lower priority than everything sounding");
            Assert.That(g.TryPlay("high", 0.1f, out var p), Is.True);
            Assert.That(p.stolenVoice, Is.GreaterThan(0));
            Assert.That(g.Active(0.1f), Is.EqualTo(CueGate.MaxTotalVoices));
        }

        [Test]
        public void NeverTheSameClipTwiceInARow()
        {
            var g = Gate(Test("c", voices: 99, len: 0.01f, clips: new[] { "x", "y", "z" }));
            string last = null;
            for (int i = 0; i < 200; i++)
            {
                Assert.That(g.TryPlay("c", i, out var p), Is.True);
                Assert.That(p.clip, Is.Not.EqualTo(last));
                last = p.clip;
            }
        }

        [Test]
        public void JitterStaysInRangeAndSemitonesShiftPitch()
        {
            var c = Test("j", voices: 99, len: 0.01f);
            c.pitchJitter = 1f; c.volumeJitter = 0.1f;
            var g = Gate(c);
            float lo = Pitch.FromSemitones(-1f) - 1e-4f, hi = Pitch.FromSemitones(1f) + 1e-4f;
            for (int i = 0; i < 100; i++)
            {
                g.TryPlay("j", i, out var p);
                Assert.That(p.pitch, Is.InRange(lo, hi));
                Assert.That(p.volume, Is.InRange(0.45f - 1e-4f, 0.55f + 1e-4f));
            }
            c.pitchJitter = 0f;
            g.TryPlay("j", 500f, out var up);
            Assert.That(g.TryPlay("j", 501f, out up, semitones: 12f), Is.True);
            Assert.That(up.pitch, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void LoopHoldsItsVoiceUntilStopped()
        {
            var g = new CueGate();
            Assert.That(g.TryPlay(CueLibrary.WipeRub, 0f, out var rub), Is.True);
            Assert.That(g.TryPlay(CueLibrary.WipeRub, 100f, out _), Is.False, "one rub at a time, however long");
            g.Stop(rub.voice);
            Assert.That(g.TryPlay(CueLibrary.WipeRub, 100f, out _), Is.True);
        }

        [Test]
        public void TheChordDucksAndTheTapDoesNot()
        {
            var g = new CueGate();
            g.TryPlay(CueLibrary.UiTap, 0f, out _);
            Assert.That(g.AnyDucking(0.01f), Is.False);
            g.TryPlay(CueLibrary.Chord, 0f, out var chord);
            Assert.That(g.AnyDucking(0.5f), Is.True);
            Assert.That(chord.haptic, Is.EqualTo(HapticKind.Success));
            Assert.That(g.AnyDucking(10f), Is.False);
        }

        // ---------- mix and duck ----------

        [Test]
        public void SettingsDriveTheBuses()
        {
            var s = AudioSettings.Default;
            Assert.That(MixLevels.Bus(SoundBus.Ui, s), Is.EqualTo(1f));
            Assert.That(MixLevels.Bus(SoundBus.Music, s), Is.EqualTo(0.6f).Within(1e-5f));
            s.deviceSilent = true;
            Assert.That(MixLevels.Bus(SoundBus.Music, s), Is.EqualTo(0f));
            Assert.That(MixLevels.Bus(SoundBus.Ambience, s), Is.EqualTo(0f));
            Assert.That(MixLevels.Bus(SoundBus.Care, s), Is.EqualTo(1f));
            s.sound = false;
            foreach (SoundBus b in Enum.GetValues(typeof(SoundBus))) Assert.That(MixLevels.Bus(b, s), Is.EqualTo(0f), b.ToString());
            Assert.That(MixLevels.Decibels(1f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(MixLevels.Decibels(0.5f), Is.EqualTo(-6.02f).Within(0.01f));
            Assert.That(MixLevels.Decibels(0f), Is.EqualTo(-80f));
        }

        [Test]
        public void MusicDucksFastUnderACueAndComesBackSlowly()
        {
            var d = new Ducker();
            for (int i = 0; i < 10; i++) d.Tick(0.05f, true, false); // 0.5 s > Attack
            Assert.That(d.Gain, Is.EqualTo(Ducker.DuckedGain).Within(1e-4f));
            d.Tick(0.3f, false, false);
            Assert.That(d.Gain, Is.EqualTo(Ducker.DuckedGain).Within(1e-4f), "holds a little after the cue");
            for (int i = 0; i < 10; i++) d.Tick(0.1f, false, false);
            Assert.That(d.Gain, Is.GreaterThan(Ducker.DuckedGain).And.LessThan(1f), "rising, not back yet");
            for (int i = 0; i < 40; i++) d.Tick(0.1f, false, false);
            Assert.That(d.Gain, Is.EqualTo(1f));
        }

        [Test]
        public void AMomentTakesTheMusicNearlyAway()
        {
            var d = new Ducker();
            for (int i = 0; i < 20; i++) d.Tick(0.1f, false, true);
            Assert.That(d.Gain, Is.EqualTo(Ducker.MomentGain).Within(1e-4f));
            var s = AudioSettings.Default;
            Assert.That(MixLevels.Bus(SoundBus.Ambience, s, d.Gain), Is.GreaterThan(MixLevels.Bus(SoundBus.Music, s, d.Gain)));
        }

        // ---------- ambience ----------

        static AmbienceInput At(float hour, Weather w = Weather.Clear, Season s = Season.Summer, float mess = 0f, Era era = Era.Camp)
            => new() { hour = hour, weather = w, season = s, mess01 = mess, era = era };

        [Test]
        public void BirdsByDayCricketsByNight()
        {
            var noon = Ambience.Targets(At(10f));
            Assert.That(noon.birds, Is.GreaterThan(0.4f));
            Assert.That(noon.crickets, Is.EqualTo(0f));
            var night = Ambience.Targets(At(23f));
            Assert.That(night.birds, Is.EqualTo(0f));
            Assert.That(night.crickets, Is.GreaterThan(0.9f));
            Assert.That(night.calm, Is.GreaterThan(noon.calm));
            var dawn = Ambience.Targets(At(6f));
            Assert.That(dawn.birds, Is.GreaterThanOrEqualTo(noon.birds), "the dawn chorus");
            var dusk = Ambience.Targets(At(19.5f));
            Assert.That(dusk.birds, Is.GreaterThan(0f));
            Assert.That(dusk.crickets, Is.GreaterThan(0f), "they overlap at dusk");
            Assert.That(Ambience.Targets(At(25f)).crickets, Is.EqualTo(Ambience.Targets(At(1f)).crickets), "wraps at 24");
        }

        [Test]
        public void WeatherSeasonMessAndEraShapeTheLayers()
        {
            var storm = Ambience.Targets(At(10f, Weather.Storm));
            Assert.That(storm.rain, Is.EqualTo(1f));
            Assert.That(storm.wind, Is.GreaterThan(0.5f));
            Assert.That(storm.birds, Is.EqualTo(0f));
            Assert.That(Ambience.Targets(At(10f, Weather.KeeperRain)).rain, Is.GreaterThan(0f));
            Assert.That(Ambience.Targets(At(10f)).rain, Is.EqualTo(0f));
            Assert.That(Ambience.Targets(At(23f, s: Season.Winter)).crickets, Is.EqualTo(0f));
            Assert.That(Ambience.Targets(At(10f, mess: 1f)).birds, Is.LessThan(Ambience.Targets(At(10f)).birds));
            Assert.That(Ambience.Targets(At(10f, era: Era.Town)).birds, Is.LessThan(Ambience.Targets(At(10f)).birds));
            var m = At(10f); m.inMoment = true;
            Assert.That(Ambience.Targets(m).birds, Is.LessThan(Ambience.Targets(At(10f)).birds));
        }

        [Test]
        public void FaderFadesNotCuts()
        {
            var f = new AmbienceFader();
            var target = new AmbienceLevels { rain = 1f };
            f.Tick(1f, target);
            Assert.That(f.Level(3), Is.EqualTo(1f / AmbienceFader.FadeSeconds).Within(1e-4f));
            for (int i = 0; i < 10; i++) f.Tick(1f, target);
            Assert.That(f.Level(3), Is.EqualTo(1f));
            Assert.That(f.Audible(1), Is.False);
            f.Snap(new AmbienceLevels { birds = 0.5f });
            Assert.That(f.Level(1), Is.EqualTo(0.5f));
            Assert.That(f.Level(3), Is.EqualTo(0f));
        }

        // ---------- the moment timeline ----------

        static RebuildWave Wave(int lots, float start, float duration) =>
            new(Enumerable.Range(1, lots).Select(i => (i, new MVec3(i * 0.3f, 0f, 0f))), MVec3.Zero, start, duration);

        [Test]
        public void CampToVillageTimeline()
        {
            var def = MomentLibrary.CampToVillage();
            var ctx = new MomentContext { leaderPresent = true, firstMoment = false };
            var waveBeat = def.beats.First(b => b.Has(BeatAction.RebuildWave));
            int wi = def.beats.IndexOf(waveBeat);
            var wave = Wave(6, waveBeat.start, def.beats[wi + 1].start - waveBeat.start);
            var cues = new MomentCues(def, ctx, wave);

            Assert.That(cues.Unknown, Is.Empty);
            var e = cues.Entries;
            Assert.That(e[0].cue, Is.EqualTo(CueLibrary.Flute));
            Assert.That(e[0].time, Is.EqualTo(0f));
            Assert.That(e.Count(x => x.beat == "elder"), Is.EqualTo(0), "the leader is there: the elder's beat doesn't play");
            Assert.That(e.Count(x => x.beat == "chief" && x.cue == CueLibrary.Drum), Is.EqualTo(2));

            var knocks = e.Where(x => x.cue == CueLibrary.Knock).ToList();
            Assert.That(knocks.Count, Is.EqualTo(6));
            Assert.That(knocks.All(k => k.tick), Is.True, "a light tick per lot");
            for (int i = 1; i < knocks.Count; i++)
            {
                Assert.That(knocks[i].time, Is.GreaterThan(knocks[i - 1].time));
                Assert.That(knocks[i].semitones, Is.GreaterThan(knocks[i - 1].semitones), "a rising scale");
            }
            var cheer = def.beats.First(b => b.Has(BeatAction.Cheer));
            Assert.That(e.Any(x => x.cue == CueLibrary.Chord && x.time == cheer.start), Is.True);
            for (int i = 1; i < e.Count; i++) Assert.That(e[i].time, Is.GreaterThanOrEqualTo(e[i - 1].time), "sorted");
        }

        [Test]
        public void VillageToTownUsesHammersAndTheBell()
        {
            var def = MomentLibrary.VillageToTown();
            var ctx = new MomentContext { leaderPresent = false, somethingCleared = false };
            var waveBeat = def.beats.First(b => b.Has(BeatAction.RebuildWave));
            var cues = new MomentCues(def, ctx);
            cues.AddWave(Wave(4, waveBeat.start, 10f), waveBeat);
            Assert.That(cues.Unknown, Is.Empty);
            Assert.That(cues.Entries.Count(x => x.cue == CueLibrary.Hammer), Is.EqualTo(4));
            Assert.That(cues.Entries.Any(x => x.cue == CueLibrary.Bell), Is.True);
            Assert.That(cues.Entries.Any(x => x.cue == CueLibrary.Fiddle), Is.True, "fiddle in the village and town");
            Assert.That(cues.Entries.Any(x => x.cue == CueLibrary.PauseNote), Is.False, "nothing cleared: no pause note");
        }

        [Test]
        public void DueHandsOutEachCueOnceAndSkipsStaleOnes()
        {
            var def = MomentLibrary.CampToVillage();
            var cues = new MomentCues(def, new MomentContext());
            var seen = new List<MomentCues.Entry>();
            float t = -0.01f;
            while (t < 80f) { float n = t + 1f / 30f; seen.AddRange(cues.Due(t, n)); t = n; }
            Assert.That(seen.Count, Is.EqualTo(cues.Entries.Count), "frame by frame: every cue once");

            // A 10 s hitch: only what's fresh plays, but the chord is never lost.
            var cheer = def.beats.First(b => b.Has(BeatAction.Cheer));
            var jump = cues.Due(cheer.start - 5f, cheer.start + 5f).ToList();
            Assert.That(jump.Any(x => x.cue == CueLibrary.Chord), Is.True);
            Assert.That(jump.All(x => x.cue == CueLibrary.Chord || cheer.start + 5f - x.time <= MomentCues.StaleAfter), Is.True);
        }

        [Test]
        public void UnknownAudioIdsAreReported()
        {
            var def = new MomentDef { id = "t" };
            def.beats.Add(new MomentBeat { id = "a", start = 0f, audio = "kazoo" });
            def.beats.Add(new MomentBeat { id = "b", start = 1f, audio = "chord" });
            var cues = new MomentCues(def, new MomentContext());
            Assert.That(cues.Unknown, Is.EqualTo(new[] { "kazoo" }));
            Assert.That(cues.Entries.Any(x => x.cue == CueLibrary.Chord), Is.True);
        }

        // ---------- haptics ----------

        [Test]
        public void HapticsRespectTheSettingAndTheGap()
        {
            var h = new HapticGate();
            Assert.That(h.TryBuzz(HapticKind.Light, 0f, false, out _), Is.False, "off in settings");
            Assert.That(h.TryBuzz(HapticKind.Light, 0f, true, out var p), Is.True);
            Assert.That(p.ios, Is.EqualTo(HapticPattern.IosKind.ImpactLight));
            Assert.That(h.TryBuzz(HapticKind.Light, 0.02f, true, out _), Is.False, "too soon");
            Assert.That(h.TryBuzz(HapticKind.Success, 0.03f, true, out p), Is.True, "a stronger one cuts in");
            Assert.That(p.ios, Is.EqualTo(HapticPattern.IosKind.NotifySuccess));
            Assert.That(h.TryBuzz(HapticKind.None, 1f, true, out _), Is.False);
        }

        [Test]
        public void LightTicksAreCappedPerSecond()
        {
            var h = new HapticGate();
            int n = 0;
            for (int i = 0; i < 30; i++) if (h.TryBuzz(HapticKind.Selection, i * 0.07f, true, out _) && i * 0.07f < 1f) n++;
            Assert.That(n, Is.LessThanOrEqualTo(HapticGate.MaxTicksPerSecond));
            Assert.That(n, Is.GreaterThan(3));
        }

        [Test]
        public void PatternsAreSoftAndShort()
        {
            foreach (HapticKind k in Enum.GetValues(typeof(HapticKind)))
            {
                var p = HapticMap.For(k);
                if (k == HapticKind.None) { Assert.That(p.IsNone, Is.True); continue; }
                Assert.That(p.androidTimings.Length, Is.EqualTo(p.androidAmplitudes.Length), k.ToString());
                Assert.That(p.AndroidTotalMs, Is.InRange(1L, 150L), k.ToString());
                Assert.That(p.androidAmplitudes.All(a => a >= 0 && a <= 255), Is.True, k.ToString());
                Assert.That(p.ios, Is.Not.EqualTo(HapticPattern.IosKind.None), k.ToString());
            }
        }

        // ---------- helpers ----------

        /// <summary>Assets/Audio/Resources: from the Unity project folder (Unity's working directory) or by walking up
        /// from the test binary (dotnet test in Tools/SimTests).</summary>
        static string ResourcesDir()
        {
            const string rel = "Assets/Audio/Resources";
            if (Directory.Exists(rel)) return Path.GetFullPath(rel);
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var p = Path.Combine(dir.FullName, "AquariumCiv", rel);
                if (Directory.Exists(p)) return p;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
