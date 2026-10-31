using System;
using System.Collections;
using System.IO;
using AquariumCiv.GameUI;
using AquariumCiv.View.TimeLapseScreen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// The Snapshot button's picture (#79): the box as it stands at the playhead, rendered from the box camera into its own
    /// texture (no UI in it), with one small label in the corner (name, year, era), saved as a PNG under
    /// <c>persistentDataPath/game/snapshots</c>. Same render path as <see cref="EraStills"/>; unlike the era still it is
    /// taken on demand and overwrites. The label is drawn by a throwaway UI Toolkit panel that targets the same texture.
    /// </summary>
    public static class TimeLapseSnapshot
    {
        public const int Width = 1080;

        public static string Folder => Path.Combine(Application.persistentDataPath, "game", "snapshots");

        /// <summary>Takes the picture and calls <paramref name="done"/> with the file path, or null if it failed.</summary>
        public static IEnumerator Take(SnapshotData data, Action<string> done)
        {
            var bc = UnityEngine.Object.FindAnyObjectByType<BoxCamera>();
            var cam = bc != null ? bc.GetComponent<Camera>() : null;
            if (cam == null) cam = Camera.main;
            if (cam == null) { done(null); yield break; }

            int w = Width, h = Mathf.RoundToInt(Width * Screen.height / (float)Mathf.Max(1, Screen.width));
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevTarget = cam.targetTexture;
            GameObject labelGo = null;
            PanelSettings labelPanel = null;
            Texture2D tex = null;
            string path = null;
            Exception failed = null;
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = prevTarget; }
                labelPanel = MakeLabelPanel(rt, data.label, out labelGo);
            }
            catch (Exception e) { failed = e; }
            // the label panel draws onto the texture on the next UI update
            yield return null;
            yield return new WaitForEndOfFrame();
            try
            {
                if (failed == null)
                {
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    Directory.CreateDirectory(Folder);
                    path = Path.Combine(Folder, data.fileName);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                }
            }
            catch (Exception e) { failed = e; path = null; }
            finally
            {
                cam.targetTexture = prevTarget;
                if (labelGo != null) UnityEngine.Object.Destroy(labelGo);
                if (labelPanel != null) UnityEngine.Object.Destroy(labelPanel);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
            if (failed != null) Debug.LogWarning("[TimeLapseSnapshot] " + failed.Message);
            done(path);
        }

        static PanelSettings MakeLabelPanel(RenderTexture rt, string label, out GameObject go)
        {
            var ps = UnityEngine.Object.Instantiate(GameUiPanel.Settings);
            ps.targetTexture = rt;
            ps.clearColor = false;                 // keep the picture under the label
            go = new GameObject("TimeLapseSnapshotLabel") { hideFlags = HideFlags.HideAndDontSave };
            var d = go.AddComponent<UIDocument>();
            d.panelSettings = ps;
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute; box.style.left = 0; box.style.right = 0; box.style.bottom = 0; box.style.top = 0;
            box.style.justifyContent = Justify.FlexEnd; box.style.alignItems = Align.FlexStart;
            var l = new Label(label) { pickingMode = PickingMode.Ignore };
            l.style.marginLeft = 36; l.style.marginBottom = 36;
            l.style.paddingLeft = 24; l.style.paddingRight = 24; l.style.paddingTop = 10; l.style.paddingBottom = 10;
            l.style.fontSize = 38; l.style.color = new Color(0.94f, 0.89f, 0.8f);
            l.style.backgroundColor = new Color(0.18f, 0.16f, 0.15f, 0.7f);
            l.style.borderTopLeftRadius = l.style.borderTopRightRadius = l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = 20;
            box.Add(l);
            d.rootVisualElement.Add(box);
            return ps;
        }
    }
}
