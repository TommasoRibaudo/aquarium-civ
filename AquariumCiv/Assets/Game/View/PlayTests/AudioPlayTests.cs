using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The sound (#96) in the real Slice scene: an era moment plays its cues in order with the music ducked; cleans and
    /// knocks reach the director and the cooldowns hold; Sound off silences every source; the ambience follows the hour;
    /// every clip loads, and nothing logs an error or a missing clip. Nobody can listen here: the director's log says
    /// what played. Backs up and restores the slice's save and the sound settings.
    /// </summary>
    [Category("AudioPlay")]
    public class AudioPlayTests
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        string savePath, backup;
        bool soundWas, hapticsWas;
        float sfxWas;
        readonly List<string> problems = new();

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) problems.Add(type + ": " + msg);
            else if (msg.StartsWith("[Audio] missing") || msg.StartsWith("[Audio] unknown") || msg.Contains("no cue for audio id")) problems.Add(msg);
        }

        IEnumerator Open()
        {
            LogAssert.ignoreFailingMessages = true;
            problems.Clear();
            savePath = GameClock.DefaultSavePath;
            backup = savePath + ".audio-test-backup";
            if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
            if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
            soundWas = GameSettings.Sound; hapticsWas = GameSettings.Haptics; sfxWas = GameSettings.SfxVolume;
            GameSettings.Sound = true; GameSettings.Haptics = true; GameSettings.SfxVolume = 1f;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            for (int i = 0; i < 60; i++) yield return null;
            // The start-up bootstraps run after the first scene only, so a scene loaded in play mode gets them by hand.
            if (GameClock.Instance != null)
            {
                if (GameHud.Instance == null) GameHud.Create();
                if (MomentPlayer.Instance == null) MomentPlayer.Create();
                var b = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                if (CareView.Instance == null && b != null) b.gameObject.AddComponent<CareView>();
                if (GlassVoice.Instance == null && b != null) b.gameObject.AddComponent<GlassVoice>();
            }
            Application.logMessageReceived += OnLog; // after the load: only what the sound does is judged
            AudioDirector.Ensure();
            yield return null; yield return null;
        }

        void Close()
        {
            Application.logMessageReceived -= OnLog;
            MomentPlayer.DebugHoldHurry = false;
            if (MomentPlayer.Instance != null && MomentPlayer.Instance.Playing) MomentPlayer.Instance.Leave();
            GameSettings.Sound = soundWas; GameSettings.Haptics = hapticsWas; GameSettings.SfxVolume = sfxWas;
            try
            {
                if (File.Exists(backup + ".bak")) { File.Copy(backup + ".bak", savePath + ".bak", true); File.Delete(backup + ".bak"); }
                if (File.Exists(backup)) { File.Copy(backup, savePath, true); File.Delete(backup); }
                else if (File.Exists(savePath)) File.Delete(savePath);
            }
            catch (Exception e) { Debug.LogWarning("restore save: " + e.Message); }
        }

        static IEnumerator Until(Func<bool> done, float seconds)
        {
            float t = 0f;
            while (!done() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        int Count(string cue) { int n = 0; foreach (var r in AudioDirector.Instance.Recent) if (r.Split(' ')[1] == cue) n++; return n; }

        [UnityTest]
        public IEnumerator EveryClipLoadsAndTheAmbienceFollowsTheHour()
        {
            yield return Open();
            try
            {
                var a = AudioDirector.Instance;
                Assert.IsNotNull(a);
                Assert.IsEmpty(a.CheckClips(), "every CueLibrary clip resolves");
                PeopleView.DebugSetHour(10f);
                yield return new WaitForSecondsRealtime(AmbienceFader.FadeSeconds + 1f);
                Debug.Log("[audio-test] 10h: " + AudioDirector.Status());
                Assert.IsTrue(a.LayerPlaying(0), "the calm loop");
                if (a.AmbienceTarget.birds > 0.05f) Assert.IsTrue(a.LayerPlaying(1), "birds by day");
                Assert.IsFalse(a.LayerPlaying(2), "no crickets by day");
                PeopleView.DebugSetHour(23f);
                yield return new WaitForSecondsRealtime(AmbienceFader.FadeSeconds + 1f);
                Debug.Log("[audio-test] 23h: " + AudioDirector.Status());
                Assert.IsFalse(a.LayerPlaying(1), "no birds at night");
                if (a.AmbienceTarget.crickets > 0.05f) Assert.IsTrue(a.LayerPlaying(2), "crickets at night");
                AudioDirector.DebugCycleLayer(3); // rain forced on
                yield return new WaitForSecondsRealtime(1f);
                Assert.IsTrue(a.LayerPlaying(3), "the debug cheat forces the rain");
                AudioDirector.DebugCycleLayer(3); AudioDirector.DebugCycleLayer(3); // back to auto

                // the flies come with the mess and go when it's clean
                var sim = GameClock.Instance.Sim;
                PeopleView.DebugSetHour(10f);
                sim.State.grime = 1f;
                yield return new WaitForSecondsRealtime(AmbienceFader.FadeSeconds + 1f);
                Debug.Log($"[audio-test] mess {sim.MessLevel:0.00}: " + AudioDirector.Status());
                Assert.Greater(a.AmbienceTarget.flies, 0f, "a messy box has flies");
                Assert.IsTrue(a.LayerPlaying(5), "the fly buzz sounds");
                sim.CleanAll();
                yield return new WaitForSecondsRealtime(AmbienceFader.FadeSeconds + 1f);
                Assert.AreEqual(0f, a.AmbienceTarget.flies, "clean: no flies");
                Assert.IsFalse(a.LayerPlaying(5), "the buzz is gone");
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator CleansAndKnocksPlayTheirCuesWithCooldowns()
        {
            yield return Open();
            try
            {
                var a = AudioDirector.Instance;
                var sim = GameClock.Instance.Sim;
                var care = CareView.Instance;
                var glass = GlassVoice.Instance;
                var pv = PeopleView.Find();
                Assert.IsNotNull(care); Assert.IsNotNull(glass); Assert.IsNotNull(pv);
                int buzzes = a.HapticsFired, dropped = a.DroppedCount;

                care.Cheer(sim, true);
                care.Cheer(sim, true); // at once: the full-clean chime's cooldown drops the second
                yield return new WaitForSecondsRealtime(0.6f); // the people's cheer comes a beat later
                Assert.AreEqual(1, Count(CueLibrary.FullClean), "one chime for two cheers in a row");
                Assert.GreaterOrEqual(Count(CueLibrary.Cheer), 1, "the people cheer");
                Assert.Greater(a.DroppedCount, dropped);
                Assert.Greater(a.HapticsFired, buzzes, "a success buzz");
                Assert.AreEqual(HapticKind.Success, a.LastHaptic);

                for (int i = 0; i < 4; i++) glass.Tap(pv.Planner, pv.Now); // four taps in one frame
                yield return null;
                Assert.AreEqual(1, Count(CueLibrary.TapGlass), "one knock sound per 0.12 s");
                yield return new WaitForSecondsRealtime(0.2f);
                glass.Tap(pv.Planner, pv.Now);
                yield return null;
                Assert.AreEqual(2, Count(CueLibrary.TapGlass), "a knock after the cooldown sounds");
                Debug.Log("[audio-test] recent: " + string.Join(" | ", a.Recent));
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator SoundOffSilencesEverything()
        {
            yield return Open();
            try
            {
                var a = AudioDirector.Instance;
                PeopleView.DebugSetHour(10f);
                a.PlayNow(CueLibrary.Bell);
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.Greater(a.LoudestSource(), 0f, "something sounds with Sound on");
                GameSettings.Sound = false;
                yield return null; yield return null;
                Assert.AreEqual(0f, a.LoudestSource(), "every source silent (the bell mid-ring, the ambience)");
                Assert.AreEqual(0f, AudioListener.volume, "the listener too");
                Assert.AreEqual(0f, Sfx.Gain(SoundBus.Care), "the wipe rub's bus");
                int muted = a.MutedCount;
                Sfx.Play(CueLibrary.UiTap);
                Assert.Greater(a.MutedCount, muted, "a cue asked for is counted as muted, not played");
                GameSettings.Sound = true;
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.Greater(a.Gain(SoundBus.Care), 0f);
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator AMomentPlaysItsCuesInOrderWithTheMusicDucked()
        {
            yield return Open();
            try
            {
                var a = AudioDirector.Instance;
                var sim = GameClock.Instance.Sim;
                var mp = MomentPlayer.Instance;
                Assert.IsNotNull(mp);
                if (sim.State.era != Era.Camp) Assert.Inconclusive("the fresh box should start in the camp");
                sim.State.grime = 0f;
                Assert.IsTrue(sim.ForceEraReady());
                yield return null;
                MomentPlayer.DebugHurry(4f);
                MomentPlayer.DebugHoldHurry = true;
                Assert.IsTrue(mp.Watch(true), "the moment starts");
                yield return new WaitForSecondsRealtime(1f);
                Assert.LessOrEqual(a.MusicDuck, Ducker.MomentGain + 0.05f, "the music sinks for the moment");
                Assert.AreEqual(a.MusicDuck, MusicDirector.ExternalDuck, 1e-4f, "and the music director follows");
                yield return Until(() => a.MomentLog.Contains(CueLibrary.Fiddle) || !mp.Playing, 60f);
                var log = a.MomentLog;
                Debug.Log("[audio-test] moment cues: " + string.Join(", ", log));
                Assert.AreEqual(CueLibrary.Flute, log[0], "the dawn's flute first");
                int flute = log.IndexOf(CueLibrary.Flute), tune = log.IndexOf(CueLibrary.TuneUp), knock = log.IndexOf(CueLibrary.Knock),
                    chord = log.IndexOf(CueLibrary.Chord), fiddle = log.IndexOf(CueLibrary.Fiddle);
                Assert.Greater(tune, flute);
                Assert.Greater(chord, tune);
                if (knock >= 0) { Assert.Greater(knock, tune); Assert.Greater(chord, knock); }
                else Debug.LogWarning("[audio-test] no lots to rebuild: no knocks");
                Assert.Greater(fiddle, chord, "the festival's fiddle after the cheer's chord");
                Assert.AreEqual(sim.State.era, Era.Village, "the cheer moved the era");
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally { Close(); }
        }
    }
}
