using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The 81-box leak (tester batch 3): with the Slice loaded there is exactly one <c>Generated</c> root; the Slice unloaded,
    /// nothing of it stays behind; and a planted orphan does fail the check. Teardown of every <see cref="PlayTestBase"/>
    /// class runs the same check, and <c>BoxLeakTests</c> covers the domain reload in edit mode.
    /// </summary>
    [Category("LeakPlay")]
    public class LeakPlayTests : PlayTestBase
    {
        [UnityTest]
        public IEnumerator OneBox_ThenNothingLeftBehind()
        {
            yield return OpenSlice(seed: 1234);
            Assert.AreEqual(1, LeakCheck.GeneratedRoots(), "exactly one Generated root");
            Assert.IsEmpty(LeakCheck.Orphans());

            // A second load of the scene: the first box must go with it.
            yield return OpenSlice(seed: 1234);
            Assert.AreEqual(1, LeakCheck.GeneratedRoots(), "a reload keeps one Generated root");
            Assert.IsEmpty(LeakCheck.Orphans());
            Assert.IsEmpty(LeakCheck.BodilessProps());
        }

        /// <summary>A second box root fails the check (a sceneless orphan can only be planted in edit mode: BoxLeakTests).</summary>
        [UnityTest]
        public IEnumerator ASecondRootIsCaught()
        {
            yield return OpenSlice(seed: 1234);
            var leak = new GameObject("Generated") { hideFlags = HideFlags.DontSave };
            try
            {
                Assert.AreEqual(2, LeakCheck.GeneratedRoots());
                Assert.Throws<AssertionException>(LeakCheck.AssertClean, "and the check fails");
            }
            finally { Object.DestroyImmediate(leak); }
        }
    }
}
