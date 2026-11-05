using System.Collections;
using System.Collections.Generic;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #333 look check (explicit): a close view of one rabbit, a knock on the glass beside it, then a strip of frames (every few frames)
    /// and a per-frame log of where it is, how high, its act and its speed, so the notice, the run and the settle can be judged.
    /// Frames go to Temp/agent/knock_NN.png and a contact sheet is made outside.
    /// </summary>
    [Explicit, Category("RabbitKnockFrames")]
    public class RabbitKnockFrames : PlayTestBase
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator Knock_FrameStrip()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(seed: 4242);
                var box = Object.FindAnyObjectByType<BoxView>();
                var rv = RabbitView.Instance;
                PeopleView.DebugSetHour(11f);
                foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (-0.6f, 0.2f) }) EcoDebug.MistBig(x, z);
                EcoDebug.SkipSeasons(2);
                EcoDebug.AddRabbits(5);
                yield return Frames(30);
                yield return Until(() => rv.SignsPlayed > 0, 10f);
                yield return Until(() => !rv.SignPlaying, 10f);
                yield return Frames(30);
                // a rabbit with nothing standing near it (the tent hid the first one's freeze)
                int r0 = rv.Field.Alive[0]; float bestClear = -1f;
                foreach (int id in rv.Field.Alive)
                {
                    if (!rv.TryGetPosition(id, out var q)) continue;
                    float clear = float.MaxValue;
                    foreach (var pc in box.layout.pieces) clear = Mathf.Min(clear, new Vector2(pc.x - q.x, pc.z - q.z).magnitude);
                    if (clear > bestClear) { bestClear = clear; r0 = id; }
                }
                Assert.IsTrue(rv.TryGetPosition(r0, out var at0));
                var rig = Camera.main.GetComponent<BoxCamera>();
                rig.target = box.transform.TransformPoint(box.land.OnGround(at0.x, at0.z)); rig.zoom = rig.minZoom; rig.pitch = 40f; // close and still (tooling 2026-11-04)
                rig.Apply();
                rig.enabled = false; // hold the view still for the strip
                yield return Frames(10);
                Assert.IsTrue(rv.TryGetPosition(r0, out at0));
                var log = new List<string>();
                // the knock lands 0.35 off the rabbit (as the glass beside it would be)
                int n = rv.KnockAtGround(at0.x + 0.35f, at0.z + 0.1f);
                Debug.Log($"[knock-frames] knock startled {n}");
                Vector3 prev = at0;
                float t0 = Time.realtimeSinceStartup;
                for (int f = 0; f < 150; f++)
                {
                    rv.TryGetPosition(r0, out var p);
                    rv.TryGetAct(r0, out bool fleeing);
                    float dtr = Time.unscaledDeltaTime;
                    var sp = Camera.main.WorldToScreenPoint(box.transform.TransformPoint(p));
                    log.Add(string.Join("\t", f, (Time.realtimeSinceStartup - t0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), dtr.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                        new Vector2(p.x - prev.x, p.z - prev.z).magnitude.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture), fleeing, ((int)sp.x).ToString(), ((int)sp.y).ToString(), rv.DebugScare(r0), p.x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), p.z.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
                    prev = p;
                    if (f % 3 == 0) yield return Shot($"knock_{f / 3:00}");
                    else yield return null;
                }
                File.WriteAllLines(Path.Combine(Application.dataPath, "../Temp/agent/knock_frames.txt"), log);
                rig.enabled = true;
            }
            finally { GameClock.EcologyPref = prefWas; }
        }
    }
}
