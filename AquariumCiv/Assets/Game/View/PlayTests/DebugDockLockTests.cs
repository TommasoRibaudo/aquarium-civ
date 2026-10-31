using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#98: in the first slice build the dbg toggle stays hidden until a 2 s hold on the top-left corner.</summary>
    [Category("DebugDockLock")]
    public class DebugDockLockTests
    {
        static readonly Rect Safe = new(0f, 20f, 400f, 800f);

        [SetUp] public void Lock() { DebugDock.ResetLock(); DebugDock.ForceLocked = true; }
        [TearDown] public void Reset() => DebugDock.ResetLock();

        [Test]
        public void HiddenAtFirstAndUntilTheHoldIsLongEnough()
        {
            var corner = DebugDock.CornerRect(Safe);
            Assert.IsTrue(DebugDock.Locked, "hidden by default");
            Assert.IsFalse(DebugDock.Hold(true, new Vector2(10, 30), corner, 10f));
            Assert.IsFalse(DebugDock.Hold(true, new Vector2(10, 30), corner, 11.9f), "1.9 s is not enough");
            Assert.IsTrue(DebugDock.Hold(true, new Vector2(10, 30), corner, 12.01f), "2 s unlocks");
            Assert.IsFalse(DebugDock.Locked);
            Assert.IsTrue(DebugDock.Hold(false, default, corner, 13f), "stays unlocked for the session");
        }

        [Test]
        public void LettingGoOrLeavingTheCornerStartsOver()
        {
            var corner = DebugDock.CornerRect(Safe);
            DebugDock.Hold(true, new Vector2(10, 30), corner, 0f);
            DebugDock.Hold(false, default, corner, 1.5f);
            Assert.IsFalse(DebugDock.Hold(true, new Vector2(10, 30), corner, 1.6f));
            Assert.IsFalse(DebugDock.Hold(true, new Vector2(10, 30), corner, 3.0f), "the release reset the timer");
            DebugDock.Hold(true, new Vector2(300, 400), corner, 3.2f); // a wipe across the glass is not a press
            Assert.IsFalse(DebugDock.Hold(true, new Vector2(10, 30), corner, 4.0f));
            Assert.IsFalse(DebugDock.Hold(true, new Vector2(10, 30), corner, 5.5f), "dragged out and back in restarts");
            Assert.IsTrue(DebugDock.Hold(true, new Vector2(10, 30), corner, 6.01f));
        }

        [Test]
        public void TheEditorShowsItFromTheStart()
        {
            DebugDock.ForceLocked = null;
            Assert.AreEqual(!Application.isEditor, DebugDock.Locked);
        }

        [Test]
        public void TheDebugPanelsHaveLegacyInputInAPlayer()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            Assert.Fail("Active Input Handling must stay on Both: IMGUI gets no input events in a player without it (docs/tooling.md)");
#endif
        }
    }

    /// <summary>The long-press by a real touch on the real scene, through <see cref="UnityEngine.InputSystem.Pointer"/>: the
    /// tests above call Hold() directly and would pass with dead input.</summary>
    [Category("DebugDockLock")]
    public class DebugDockTouchTests : PlayTestBase
    {
        /// <summary>A point in pixels (y up) inside the top-left corner of the safe area.</summary>
        static Vector2 InTheCorner => new(Screen.safeArea.xMin + 40f, Screen.safeArea.yMax - 40f);

        IEnumerator HoldFor(Vector2 at, float seconds)
        {
            int id = touchId++;
            BeginTouch(id, at, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(seconds);
            EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(3);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ATwoSecondHoldInTheCornerUnlocksAndNothingElseDoes()
        {
            yield return OpenSlice(seed: 4242);
            Assert.IsNotNull(Object.FindAnyObjectByType<SliceDebugPanel>(), "the panel that watches the corner is in the scene");
            DebugDock.ResetLock();
            DebugDock.ForceLocked = true;
            try
            {
                yield return Frames(3);
                Assert.IsTrue(DebugDock.Locked);
                yield return HoldFor(InTheCorner, 1.2f);
                Assert.IsTrue(DebugDock.Locked, "a short hold is not enough");
                yield return HoldFor(InTheCorner, 1.2f);
                Assert.IsTrue(DebugDock.Locked, "two short holds don't add up");
                yield return HoldFor(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 2.3f);
                Assert.IsTrue(DebugDock.Locked, "a hold elsewhere does nothing");
                yield return HoldFor(new Vector2(InTheCorner.x, Screen.height - InTheCorner.y), 2.3f);
                Assert.IsTrue(DebugDock.Locked, "the bottom-left corner is not the top-left one");
                yield return HoldFor(InTheCorner, 2.3f);
                Assert.IsFalse(DebugDock.Locked, "2 s in the corner unlocks");
                AssertNoErrors("the long-press");
            }
            finally { DebugDock.ResetLock(); }
        }
    }
}
