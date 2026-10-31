using NUnit.Framework;
using UnityEngine;

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
    }
}
