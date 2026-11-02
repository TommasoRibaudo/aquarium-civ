using System;
using System.Collections.Generic;
using System.IO;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>What the book knows about an era's still (era-moment.md §7): the picture, and how it was taken.</summary>
    [Serializable]
    public class EraStillInfo
    {
        public int seed;           // the box (SimState.seed)
        public int era;            // the era the box became (Era as int)
        public string eraName;
        public long month;         // the sim month of the cheer
        public int year;           // the year as the HUD shows it
        public string place;
        public float targetX, targetY, targetZ, yaw, pitch, zoom; // the camera pose, to re-render it later
        public string png;         // the file name next to this one
        public string takenUtc;
    }

    /// <summary>
    /// The still at the cheer for the book (#91; the book itself is #80, not built yet): a picture of the box taken from
    /// the moment's camera, without any UI, saved once per box and era as a PNG plus a JSON sidecar under the save folder
    /// (<c>persistentDataPath/game/stills/box{seed}_era{n}.png|.json</c>). The book reads them with <see cref="Find"/> /
    /// <see cref="All"/>. The camera pose is kept so a later version can re-render the still from the year snapshot.
    /// </summary>
    public static class EraStills
    {
        public const int Width = 720;

        public static string Folder => Path.Combine(Application.persistentDataPath, "game", "stills");
        static string Stem(int seed, Era era) => $"box{seed}_era{(int)era}";

        /// <summary>The still of a box's era, or null (no file yet).</summary>
        public static EraStillInfo Find(int seed, Era era)
        {
            string json = Path.Combine(Folder, Stem(seed, era) + ".json");
            if (!File.Exists(json)) return null;
            try { return JsonUtility.FromJson<EraStillInfo>(File.ReadAllText(json)); }
            catch (Exception e) { Debug.LogWarning("[EraStills] " + e.Message); return null; }
        }

        public static string PngPath(EraStillInfo info) => info == null ? null : Path.Combine(Folder, info.png);

        /// <summary>Every still of a box, in era order.</summary>
        public static List<EraStillInfo> All(int seed)
        {
            var r = new List<EraStillInfo>();
            for (int e = 0; e <= (int)Era.Town; e++) { var i = Find(seed, (Era)e); if (i != null) r.Add(i); }
            return r;
        }

        /// <summary>Takes the still now from <paramref name="cam"/> (no UI: the camera renders into its own texture), once
        /// per box and era. Returns the info, or the existing one when the era already has its still.</summary>
        public static EraStillInfo Take(Camera cam, BoxCamera rig, SimCore sim)
        {
            if (cam == null || sim == null) return null;
            var s = sim.State;
            var existing = Find(s.seed, s.era);
            if (existing != null && !IsBlank(existing)) return existing; // once per era, but a grey one from before the depth fix doesn't get to stay
            int w = Width, h = Mathf.RoundToInt(Width * Screen.height / (float)Mathf.Max(1, Screen.width));
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); // sRGB, or the PNG comes out washed
            var prevTarget = cam.targetTexture;
            Texture2D tex = null;
            var day = DayLightDriver.Instance;
            try
            {
                day?.BeginStill(DayLightDriver.StillHour); // every era's picture has the same light
                FitDepth(cam);
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = prevTarget; }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                if (LooksBlank(tex)) { Debug.LogWarning("[EraStills] the still came out blank: not kept"); return null; }
                Directory.CreateDirectory(Folder);
                string stem = Stem(s.seed, s.era);
                File.WriteAllBytes(Path.Combine(Folder, stem + ".png"), tex.EncodeToPNG());
                var info = new EraStillInfo
                {
                    seed = s.seed, era = (int)s.era, eraName = s.era.ToString(), month = s.month,
                    year = (int)(s.month / SimConfig.MonthsPerYear), place = sim.PlaceName, png = stem + ".png",
                    takenUtc = DateTime.UtcNow.ToString("o"),
                };
                if (rig != null) { info.targetX = rig.target.x; info.targetY = rig.target.y; info.targetZ = rig.target.z; info.yaw = rig.yaw; info.pitch = rig.pitch; info.zoom = rig.zoom; }
                File.WriteAllText(Path.Combine(Folder, stem + ".json"), JsonUtility.ToJson(info, true));
                Debug.Log($"[EraStills] saved {stem}.png ({w}x{h})");
                return info;
            }
            catch (Exception e) { Debug.LogWarning("[EraStills] could not take the still: " + e.Message); return null; }
            finally
            {
                day?.EndStill();
                cam.targetTexture = prevTarget;
                RenderTexture.ReleaseTemporary(rt);
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
        }

        /// <summary>The box as the camera sees it now, with no UI, as a texture the caller owns (the book's cover before the first still).</summary>
        public static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevTarget = cam.targetTexture;
            try
            {
                FitDepth(cam);
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = prevTarget; }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                return tex;
            }
            catch (Exception e) { Debug.LogWarning("[EraStills] could not render: " + e.Message); return null; }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>A render request raises no camera event, so the box camera's depth is fitted by hand first, whoever holds the camera.</summary>
        public static void FitDepth(Camera cam)
        {
            if (cam != null && cam.TryGetComponent(out BoxCamera rig)) rig.FitDepthNow();
        }

        /// <summary>The still on disk is a blank.</summary>
        static bool IsBlank(EraStillInfo info)
        {
            string path = PngPath(info);
            if (!File.Exists(path)) return true;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try { return !tex.LoadImage(File.ReadAllBytes(path)) || LooksBlank(tex); }
            finally { UnityEngine.Object.Destroy(tex); }
        }

        /// <summary>Forgets one still: a blank found on disk, so the book falls back to what it does with none.</summary>
        public static void Forget(EraStillInfo info)
        {
            if (info == null) return;
            string png = PngPath(info), json = Path.ChangeExtension(png, ".json");
            if (File.Exists(png)) File.Delete(png);
            if (File.Exists(json)) File.Delete(json);
        }

        /// <summary>A picture with nothing in it (tester run 10: a camera past its near plane drew only the soft grey of the post stack, and a still is
        /// taken once per era): over a coarse grid, next to no neighbouring cells differ. A vignette changes slowly; any real box has edges.</summary>
        public static bool LooksBlank(Texture2D tex)
        {
            if (tex == null) return true;
            const int N = 48;
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height, edges = 0;
            float Luma(int cx, int cy)
            {
                var c = px[Mathf.Min(h - 1, cy * h / N) * w + Mathf.Min(w - 1, cx * w / N)];
                return 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            }
            for (int y = 0; y < N - 1; y++)
                for (int x = 0; x < N - 1; x++)
                {
                    float l = Luma(x, y);
                    if (Mathf.Abs(l - Luma(x + 1, y)) > BlankStep || Mathf.Abs(l - Luma(x, y + 1)) > BlankStep) edges++;
                }
            return edges < (N - 1) * (N - 1) / 50;
        }

        /// <summary>A step between grid cells that a soft gradient doesn't make (out of 255).</summary>
        const float BlankStep = 8f;

        /// <summary>Debug / tests: forget a box's stills.</summary>
        public static void DebugClear(int seed)
        {
            for (int e = 0; e <= (int)Era.Town; e++)
                foreach (var ext in new[] { ".png", ".json" })
                {
                    string p = Path.Combine(Folder, Stem(seed, (Era)e) + ext);
                    if (File.Exists(p)) File.Delete(p);
                }
        }
    }
}
