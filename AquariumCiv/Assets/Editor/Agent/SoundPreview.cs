using System;
using System.IO;
using System.Reflection;
using AquariumCiv.Prototypes;
using UnityEngine;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Agent helper: render the synthesized cleaning sounds (Juice one-shots and a scripted RubSynth wipe) to WAV files,
    /// so they can be heard without playing. Call from `unity command script eval`:
    /// AquariumCiv.EditorTools.SoundPreview.RenderAll("F:/GameDev/AquariumCiv/Temp/agent/sounds")
    /// </summary>
    public static class SoundPreview
    {
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static string RenderAll(string dir)
        {
            Directory.CreateDirectory(dir);
            var juice = typeof(Juice);
            int n = 0;
            foreach (var name in new[] { "MakePop", "MakeChime", "MakeWhoosh" })
            {
                Write(dir, name.Substring(4), (AudioClip)juice.GetMethod(name, Any).Invoke(null, null));
                n++;
            }
            for (int v = 0; v < 2; v++)
            {
                var rng = new System.Random(100 + v * 7919);
                Write(dir, $"Grab_{v}", (AudioClip)juice.GetMethod("MakeGrab", Any).Invoke(null, new object[] { rng }));
                Write(dir, $"Flick_{v}", (AudioClip)juice.GetMethod("MakeFlick", Any).Invoke(null, new object[] { new System.Random(100 + v * 7919) }));
                Write(dir, $"CrunchTrash_{v}", (AudioClip)juice.GetMethod("MakeCrunch", Any).Invoke(null, new object[] { new System.Random(100 + v * 7919), false }));
                Write(dir, $"CrunchVine_{v}", (AudioClip)juice.GetMethod("MakeCrunch", Any).Invoke(null, new object[] { new System.Random(100 + v * 7919), true }));
                n += 4;
            }
            WriteSequence(dir);
            WriteWipe(dir);
            return $"wrote {n + 2} wavs to {dir}";
        }

        /// <summary>Grab, flick, then the crunch 0.4 s later: the whole toss as the player hears it.</summary>
        static void WriteSequence(string dir)
        {
            var juice = typeof(Juice);
            var grab = Data((AudioClip)juice.GetMethod("MakeGrab", Any).Invoke(null, new object[] { new System.Random(1) }));
            var flick = Data((AudioClip)juice.GetMethod("MakeFlick", Any).Invoke(null, new object[] { new System.Random(2) }));
            var crunch = Data((AudioClip)juice.GetMethod("MakeCrunch", Any).Invoke(null, new object[] { new System.Random(3), false }));
            const int rate = 44100;
            var d = new float[rate * 2];
            Mix(d, grab, (int)(0.1f * rate), 0.8f);
            Mix(d, flick, (int)(0.6f * rate), 0.9f);
            Mix(d, crunch, (int)(1.0f * rate), 1f);
            WriteWav(Path.Combine(dir, "Toss_sequence.wav"), d, rate);
        }

        /// <summary>A scripted wipe through RubSynth: dirty strokes, mid grime, then clean glass (squeaks).</summary>
        static void WriteWipe(string dir)
        {
            var go = new GameObject("RubPreview") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var synth = go.AddComponent<RubSynth>(); // Awake doesn't run in edit mode: drive the filter by hand
                typeof(RubSynth).GetField("sr", Any).SetValue(synth, 48000f);
                var filter = typeof(RubSynth).GetMethod("OnAudioFilterRead", Any);
                const int rate = 48000, block = 512;
                float seconds = 6f;
                var outData = new float[(int)(rate * seconds)];
                var buf = new float[block];
                for (int at = 0; at + block <= outData.Length; at += block)
                {
                    float t = at / (float)rate;
                    float phase = t % 1f; // one stroke per second: speed up, hold, release
                    float speed = phase < 0.75f ? Mathf.Sin(phase / 0.75f * Mathf.PI) : 0f;
                    float grime = t < 2f ? 0.8f : t < 4f ? 0.25f : 0.02f;
                    synth.Set(speed, grime, 0.8f);
                    Array.Clear(buf, 0, block);
                    filter.Invoke(synth, new object[] { buf, 1 });
                    Array.Copy(buf, 0, outData, at, block);
                }
                WriteWav(Path.Combine(dir, "Wipe_dirty-mid-clean.wav"), outData, rate);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static float[] Data(AudioClip c)
        {
            var d = new float[c.samples * c.channels];
            c.GetData(d, 0);
            return d;
        }

        static void Mix(float[] into, float[] src, int at, float gain)
        {
            for (int i = 0; i < src.Length && at + i < into.Length; i++) into[at + i] += src[i] * gain;
        }

        static void Write(string dir, string name, AudioClip clip) => WriteWav(Path.Combine(dir, name + ".wav"), Data(clip), clip.frequency);

        static void WriteWav(string path, float[] d, int rate)
        {
            using var w = new BinaryWriter(File.Create(path));
            w.Write("RIFF".ToCharArray());
            w.Write(36 + d.Length * 2);
            w.Write("WAVEfmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write("data".ToCharArray());
            w.Write(d.Length * 2);
            foreach (var x in d) w.Write((short)(Mathf.Clamp(x, -1f, 1f) * 32767));
        }
    }
}
