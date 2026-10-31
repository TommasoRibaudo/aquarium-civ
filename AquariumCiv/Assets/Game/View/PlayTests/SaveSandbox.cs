using System.Collections;
using System.IO;
using AquariumCiv.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Every play test in this assembly saves to a folder of its own for the whole run. Backing the player's save up and
    /// putting it back did not hold: the GameClock outlives the test that restored it, and its autosave, focus and quit saves
    /// wrote the test box over the real one (tester run 4), and a run that died left no restore at all. Now there is nothing
    /// to restore. Each clock reads the path once, so the saves made as play mode ends land here too.
    /// </summary>
    [SetUpFixture]
    public class SaveSandbox
    {
        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "agent", "playtest-save"));

        [OneTimeSetUp]
        public void Enter()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            GameClock.SaveFolderOverride = Folder;
        }

        [OneTimeTearDown]
        public void Leave() => GameClock.SaveFolderOverride = null;
    }

    [Category("SaveSandbox")]
    public class SaveSandboxTests : PlayTestBase
    {
        static string Real(string ext) => Path.Combine(Application.persistentDataPath, "game", "box.json" + ext);
        static long Stamp(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;

        [UnityTest, Timeout(60000)]
        public IEnumerator ATestNeverWritesThePlayersSave()
        {
            long main = Stamp(Real("")), bak = Stamp(Real(".bak"));
            yield return OpenSlice(seed: 4243);
            StringAssert.StartsWith(SaveSandbox.Folder, Path.GetFullPath(GameClock.Instance.SavePath), "the clock saves in the sandbox");
            GameClock.Instance.Save();
            Assert.IsTrue(File.Exists(GameClock.Instance.SavePath), "a save went through");
            Assert.AreEqual(main, Stamp(Real("")), "box.json is as it was");
            Assert.AreEqual(bak, Stamp(Real(".bak")), "box.json.bak is as it was");
        }
    }
}
