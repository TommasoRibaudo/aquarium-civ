using System.Collections;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#637 tester (1640x2360): on a tablet the 30 % name sheet was shorter than its content, so Another and Keep rode up
    /// over the name field. At phone and tablet sizes, with the keyboard down and up, the buttons sit below the field and on screen.</summary>
    public class NameSheetLayoutPlayTests : PlayTestBase
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator TheButtonsNeverCoverTheName()
        {
            System.IO.Directory.CreateDirectory(Shots + "637");
            foreach (var (w, h) in new[] { (1640, 2360), (750, 1334), (1179, 2556) })
            {
                yield return OpenSlice(w, h, seed: 6370, skipFirstRun: false);
                var fr = FirstRunView.Instance;
                Assert.IsTrue(fr.Active, "the first run");
                yield return Until(() => fr.Step == FirstRunStep.Wipe, 6f);
                fr.Run.DebugSkipTo(FirstRunStep.Name);
                yield return Frames(6);
                yield return TapAt(GameUiPanel.ScreenCenter(fr.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsTrue(fr.NameSheetOpen, $"{w}x{h}: the plate opens the sheet");
                foreach (bool keyboard in new[] { false, true })
                {
                    FirstRunView.DebugKeyboard = keyboard;
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Shot($"637/name_sheet_{w}x{h}{(keyboard ? "_keyboard" : "")}");
                    var field = fr.Root.Q("firstrun-name-field").worldBound;
                    var panel = fr.Root.panel.visualTree.worldBound;
                    foreach (var id in new[] { "firstrun-name-reroll", "firstrun-name-keep" })
                    {
                        var b = fr.Root.Q(id).worldBound;
                        string at = $"{w}x{h}{(keyboard ? " keyboard up" : "")}: {id}";
                        Assert.GreaterOrEqual(b.yMin + 0.5f, field.yMax, at + " sits below the name field");
                        Assert.LessOrEqual(b.yMax, panel.yMax + 0.5f, at + " is on screen");
                    }
                }
                FirstRunView.DebugKeyboard = false;
                AssertNoErrors($"name sheet {w}x{h}");
            }
        }
    }
}
