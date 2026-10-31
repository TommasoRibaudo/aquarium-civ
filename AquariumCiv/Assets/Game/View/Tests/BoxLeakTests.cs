using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AquariumCiv.View.Tests
{
    /// <summary>
    /// The 81-box leak after a domain reload (play tests can't reload): with the Slice open in the Editor there is exactly
    /// one <c>Generated</c> root, and no parentless DontSave root outside a scene. A leaked root is swept when a box wakes.
    /// </summary>
    public class BoxLeakTests
    {
        const string SlicePath = "Assets/Game/Scenes/Slice.unity";

        static List<string> Orphans() => Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t != null && t.parent == null && !t.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(t) && t.name != "Code-based tests runner"
                && (t.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave && (t.gameObject.hideFlags & HideFlags.NotEditable) == 0)
            .Select(t => t.name).ToList();

        static int Generated() => Resources.FindObjectsOfTypeAll<Transform>().Count(t => t != null && t.name == "Generated" && !EditorUtility.IsPersistent(t));

        static void OpenSlice()
        {
            if (SceneManager.GetActiveScene().path != SlicePath) EditorSceneManager.OpenScene(SlicePath, OpenSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator AfterADomainReload_OneBoxAndNoOrphans()
        {
            OpenSlice();
            yield return null;
            EditorUtility.RequestScriptReload();
            yield return new WaitForDomainReload();
            OpenSlice();
            yield return null;
            Assert.IsEmpty(Orphans(), "leaked parentless DontSave roots");
            Assert.AreEqual(1, Generated(), "exactly one Generated root");
        }

        [TestCase("Generated")]
        [TestCase("Planned People")]
        public void ALeakedRootIsSweptWhenABoxWakes(string name)
        {
            OpenSlice();
            var leak = new GameObject(name) { hideFlags = HideFlags.DontSave };
            EditorSceneManager.OpenScene(SlicePath, OpenSceneMode.Single); // the unload detaches it, the new box wakes
            bool swept = leak == null;
            if (!swept) Object.DestroyImmediate(leak);
            Assert.IsTrue(swept, "the box swept the orphan");
        }
    }
}
