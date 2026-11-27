// #807 (tester 2026-11-27): the debug panel's new box ("Barren box" / "Old box") pressed while the time-lapse is open started the new box's
// first run under the old box's year bar. The new box closes the time-lapse first.
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    public class DebugNewBoxPlayTests : PlayTestBase
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator BarrenBox_ClosesTheTimeLapseFirst()
        {
            yield return OpenSlice(750, 1334);
            var tls = TimeLapseScreenView.Instance;
            Assert.IsNotNull(tls, "the time-lapse screen is in the scene");
            Assert.IsTrue(tls.Open(), "the time-lapse opens");
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(tls.IsOpen);
            Debug.Log("[807] " + EcoDebug.NewBox(true));
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsFalse(tls.IsOpen, "the old box's time-lapse is closed under the new box");
            var tl = TimeLapseView.Find();
            Assert.IsTrue(tl == null || !tl.Active, "no time-lapse runs over the new box");
            AssertNoErrors("807 new box");
            CloseSlice();
        }
    }
}
