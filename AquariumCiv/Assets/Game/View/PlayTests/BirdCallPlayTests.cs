using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#195: the five calls exist and play on the pooled source, and the loop runs through the song.</summary>
    [Category("Birds")]
    public class BirdCallPlayTests : PlayTestBase
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator TheCallsPlayOnThePooledSource()
        {
            yield return OpenSlice(seed: 7531);
            var a = AudioDirector.Instance;
            Assert.IsNotNull(a);
            int played = a.BirdCallsPlayed;
            for (int i = 0; i < CareSound.BirdCalls; i++)
            {
                StringAssert.StartsWith("bird call " + i, AudioDirector.DebugBirdCall(i));
                yield return Frames(2);
            }
            Assert.AreEqual(played + CareSound.BirdCalls, a.BirdCallsPlayed);
            Assert.That(a.BirdSwell, Is.InRange(BirdSong.GapLevel, 1f));
            for (int i = 0; i < CareSound.BirdCalls; i++)
            {
                var clip = CareSound.MakeProc("bird" + i);
                Assert.IsNotNull(clip, "call " + i);
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                float peak = 0f;
                foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
                Assert.That(peak, Is.InRange(0.1f, 1f), "call " + i + " sounds and doesn't clip");
                Object.Destroy(clip);
            }
            AssertNoErrors("bird calls");
        }
    }
}
