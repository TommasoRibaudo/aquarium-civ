using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// "Aquarium Civ/Open questions sign-off": starts the explicit play test OpenQuestionsSignOff (AquariumCiv.View.PlayTests) through the
    /// Test Runner API, so nobody has to find it in the Test Runner window. The test walks every change of docs/open-questions.md
    /// and waits for a verdict on a panel in the Game view; the verdicts land in docs/overnight/open-questions-signoff.md.
    /// </summary>
    public static class OpenQuestionsSignOffMenu
    {
        public const string TestName = "AquariumCiv.View.PlayTests.OpenQuestionsSignOff.OpenQuestions_SignOff";

        [MenuItem("Aquarium Civ/Open questions sign-off")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Open questions sign-off", "Stop play mode (and wait for any compile) first, then run it again.", "OK");
                return;
            }
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter { testMode = TestMode.PlayMode, testNames = new[] { TestName } };
            api.Execute(new ExecutionSettings(filter));
            Debug.Log("[oqso] starting " + TestName + ": the Game view will show the sign-off panel when the box is ready (about 30 s).");
        }
    }
}
