using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View;
using AquariumCiv.View.PlayTests;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Fa = UnityEngine.TextCore.Text.FontAsset;

namespace AquariumCiv.UiLimits
{
    /// <summary>The save sandbox of the View play tests, which stops at its own assembly.</summary>
    [SetUpFixture]
    public class UiLimitsSandbox
    {
        static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "agent", "uilimits-save"));

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

    /// <summary>
    /// #164: what UI Toolkit can really draw over the tank. Not a check, and not part of the game: an [Explicit] throwaway
    /// panel on GamePanel over the live Slice scene that saves evidence to docs/design/ui/limits. Its USS, SVG and shader
    /// files (the UI Shader Graph comes from limits/SoftShadow.shadergraph.txt) are written to Assets/_UiLimitsTemp for the run and deleted after it, so it leaves no asset behind. Fonts
    /// come from Temp/agent/fonts (Google Fonts' files, not in the repo); without them the font page says so. Run with:
    /// <c>test run --mode playmode --filter UiLimits --filter_type category --include_explicit true --async_tests true</c>.
    /// </summary>
    [Explicit("Writes screenshots into docs/: run by hand"), Category("UiLimits")]
    public class UiLimitsSpike : PlayTestBase
    {
        const int Seed = 7531, Long = 900000;
        const string TempAssets = "Assets/_UiLimitsTemp";
        static readonly string Out = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "design", "ui", "limits")) + "/";
        static readonly string Log = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "agent", "ui-limits.tsv"));
        static readonly string Fonts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "agent", "fonts")) + "/";

        static StyleSheet sheet;
        static Shader softShadowGraph, dropShadow;
        const string DropShadowShader = @"Shader ""Hidden/UiLimits/DropShadow"" {
Properties { _MainTex (""src"", 2D) = ""white"" {} }
SubShader { Cull Off ZWrite Off ZTest Always Blend Off
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include ""UnityCG.cginc""
#include ""UnityUIEFilter.cginc""
sampler2D _MainTex; float4 _MainTex_TexelSize;
struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 rect : TEXCOORD1; };
v2f vert(FilterVertexInput v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.rect = GetFilterUVRect(GetFilterRectIndex(v)); return o; }
float A(v2f i, float2 uv) { return tex2D(_MainTex, uv).a; }
fixed4 frag(v2f i) : SV_Target {
  fixed4 s = tex2D(_MainTex, i.uv);
  float2 t = abs(ddx(i.uv)) + abs(ddy(i.uv)); float2 c = i.uv + float2(0, 16) * t;
  float sum = A(i, c); float w = 1;
  for (int r = 1; r <= 3; r++) for (int k = 0; k < 8; k++) {
    float an = (k + r * 0.5) * 0.7854; float2 o = float2(cos(an), sin(an)) * r * 12.0 * t;
    float g = 1.0 / (1.0 + r); sum += A(i, c + o) * g; w += g; }
  float sa = saturate(sum / w * 1.6);
  float3 col = float3(0.12, 0.1, 0.09) * sa;
  float na = s.a + sa * (1 - s.a); return fixed4(s.rgb + col * (1 - s.a), na);
} ENDCG } } }";
        static VectorImage svgFill, svgStroke, svgGrad;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        GameObject panelGo;
        VisualElement root, page;
        Texture2D photo;

        static void Note(string line)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Log));
            File.AppendAllText(Log, line + "\n");
        }

        // ---------------- the run's temporary assets ----------------
        [OneTimeSetUp]
        public void WriteAssets()
        {
#if UNITY_EDITOR
            File.Delete(Log);
            LogAssert.ignoreFailingMessages = true;
            void Capture(string msg, string stack, LogType t) { if (t != LogType.Log) Note($"import\t{t}\t{msg.Replace('\n', ' ')}"); }
            Application.logMessageReceived += Capture;
            if (!AssetDatabase.IsValidFolder(TempAssets)) AssetDatabase.CreateFolder("Assets", "_UiLimitsTemp");
            void Write(string name, string text)
            {
                File.WriteAllText(TempAssets + "/" + name, text);
                AssetDatabase.ImportAsset(TempAssets + "/" + name, ImportAssetOptions.ForceSynchronousImport);
            }
            Write("UiLimits.uss", UiLimitsUss.Main);
            Note("--- probe.uss import follows");
            Write("Probe.uss", UiLimitsUss.Probe);
            Write("ProbeNth.uss", UiLimitsUss.ProbeNth);
            Write("ProbeNot.uss", UiLimitsUss.ProbeNot);
            Note("probe rules survive: main " + AssetDatabase.LoadAssetAtPath<StyleSheet>(TempAssets + "/Probe.uss")?.GetType().Name);
            Note("--- end of probe");
            Write("DropShadow.shader", DropShadowShader);
            string sg = Out + "SoftShadow.shadergraph.txt";
            if (File.Exists(sg)) { File.Copy(sg, TempAssets + "/SoftShadow.shadergraph", true); AssetDatabase.ImportAsset(TempAssets + "/SoftShadow.shadergraph", ImportAssetOptions.ForceSynchronousImport); }
            softShadowGraph = AssetDatabase.LoadAssetAtPath<Shader>(TempAssets + "/SoftShadow.shadergraph");
            dropShadow = AssetDatabase.LoadAssetAtPath<Shader>(TempAssets + "/DropShadow.shader");
            Note($"shaders\tgraph {softShadowGraph != null} supported {softShadowGraph != null && softShadowGraph.isSupported}\tfilter {dropShadow != null} supported {dropShadow != null && dropShadow.isSupported}");
            Write("fill.svg", UiLimitsUss.Svg);
            Write("stroke.svg", UiLimitsUss.SvgStroke);
            Write("grad.svg", UiLimitsUss.SvgGradient);
            Application.logMessageReceived -= Capture;
            sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TempAssets + "/UiLimits.uss");
            svgFill = Svg("fill.svg");
            svgStroke = Svg("stroke.svg");
            svgGrad = Svg("grad.svg");
            Note($"assets\tsheet {(sheet != null)}\tsvg {svgFill != null} {svgStroke != null} {svgGrad != null}");
#endif
        }

#if UNITY_EDITOR
        static VectorImage Svg(string file)
        {
            string path = TempAssets + "/" + file;
            if (AssetImporter.GetAtPath(path) is Unity.VectorGraphics.Editor.SVGImporter imp)
            {
                imp.SvgType = Unity.VectorGraphics.Editor.SVGType.VectorImage;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<VectorImage>(path);
        }
#endif

        [OneTimeTearDown]
        public void DeleteAssets()
        {
#if UNITY_EDITOR
            AssetDatabase.DeleteAsset(TempAssets);
#endif
        }

        public override void TearDown()
        {
            StopBackdrop();
            if (panelGo != null) UnityEngine.Object.Destroy(panelGo);
            DebugDock.ResetLock();
            base.TearDown();
        }

        // ---------------- harness ----------------
        IEnumerator Open(bool hideHud = true, float years = 8f)
        {
            yield return OpenSlice(1179, 2556, seed: Seed);
            DebugDock.ForceLocked = true;
            for (float y = 0f; y < years; y += 10f) { SimLayoutSource.DebugSkipYears(Mathf.Min(10f, years - y)); yield return null; }
            Sim.CleanAll();
            Sim.CardsSeenToNow();
            PeopleView.DebugSetHour(11f);
            yield return Frames(40);
            yield return new WaitForEndOfFrame();
            photo = ScreenCapture.CaptureScreenshotAsTexture();
            if (hideHud) GameHud.Instance.Root.style.display = DisplayStyle.None;
            panelGo = new GameObject("UiLimitsPanel");
            var doc = panelGo.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 500;
            root = doc.rootVisualElement;
            root.AddToClassList("fill");
            root.pickingMode = PickingMode.Ignore;
            if (sheet != null) root.styleSheets.Add(sheet);
            NewPage();
            yield return Frames(2);
        }

        void NewPage()
        {
            root.Clear();
            page = new VisualElement();
            page.AddToClassList("page");
            root.Add(page);
        }

        IEnumerator Cap(string name, int settle = 6)
        {
            yield return Frames(settle);
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Out + name + ".jpg", tex.EncodeToJPG(82));
            Note($"shot\t{name}\t{tex.width}x{tex.height}");
            UnityEngine.Object.Destroy(tex);
        }

        static VisualElement El(VisualElement parent, params string[] cls)
        {
            var e = new VisualElement();
            foreach (var c in cls) e.AddToClassList(c);
            parent?.Add(e);
            return e;
        }

        static Label Txt(VisualElement parent, string text, params string[] cls)
        {
            var l = new Label(text);
            foreach (var c in cls) l.AddToClassList(c);
            parent?.Add(l);
            return l;
        }

        Label Tag(string text) => Txt(page, text, "tag");
        static VisualElement Row(VisualElement parent) => El(parent, "lrow");

        // ---------------- procedural art (stand-ins for real textures) ----------------
        static Texture2D Tex(int w, int h, Func<int, int, Color> f, TextureWrapMode wrap = TextureWrapMode.Clamp, bool mips = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips) { wrapMode = wrap, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.Apply(mips);
            return t;
        }

        static float RoundBox(float x, float y, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x) - hw + r, qy = Mathf.Abs(y) - hh + r;
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - r;
        }

        /// <summary>A soft rounded-rectangle shadow: the 9-slice. Centre 96 pixels of slice each side.</summary>
        static Texture2D ShadowTex() => Tex(256, 256, (x, y) =>
        {
            float d = RoundBox(x - 127.5f, y - 127.5f, 76f, 76f, 34f);
            float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-40f, 44f, d));
            return new Color(0.12f, 0.1f, 0.09f, a * 0.8f);
        }, mips: false);

        static Texture2D Gradient(int w, int h, Color a, Color b, bool vertical = true) => Tex(w, h, (x, y) =>
            Color.Lerp(a, b, vertical ? y / (h - 1f) : x / (w - 1f)), mips: false);

        static float Fbm(float x, float y) =>
            Mathf.PerlinNoise(x, y) * 0.5f + Mathf.PerlinNoise(x * 2.1f + 13f, y * 2.1f + 7f) * 0.3f + Mathf.PerlinNoise(x * 4.3f + 3f, y * 4.3f + 23f) * 0.2f;

        /// <summary>Warm paper with a deckled edge: a stand-in for a real scan.</summary>
        static Texture2D PaperTex(bool tileable) => Tex(256, 256, (x, y) =>
        {
            var c = new Color(0.93f, 0.87f, 0.75f);
            float grain = Fbm(x * 0.09f, y * 0.09f) - 0.5f;
            float fibre = Mathf.PerlinNoise(x * 0.6f, y * 0.04f) - 0.5f;
            c = c * (1f + grain * 0.12f + fibre * 0.05f);
            c.a = 1f;
            if (tileable) return c;
            float d = RoundBox(x - 127.5f, y - 127.5f, 118f, 118f, 16f) + (Fbm(x * 0.13f, y * 0.13f) - 0.5f) * 14f;
            c.a = 1f - Mathf.Clamp01((d + 1f) / 2f);
            float edge = Mathf.Clamp01((d + 14f) / 14f);
            c = Color.Lerp(c, new Color(0.72f, 0.6f, 0.42f, c.a), edge * 0.5f);
            return c;
        }, tileable ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);

        /// <summary>Brushed brass plate with a bevel and four rivets. Slice 48.</summary>
        static Texture2D BrassTex() => Tex(128, 128, (x, y) =>
        {
            float d = RoundBox(x - 63.5f, y - 63.5f, 62f, 62f, 18f);
            float v = y / 127f;
            var c = Color.Lerp(new Color(0.55f, 0.4f, 0.17f), new Color(0.89f, 0.74f, 0.42f), v);
            c *= 1f + (Mathf.PerlinNoise(x * 0.03f, y * 0.9f) - 0.5f) * 0.12f;
            float rim = Mathf.InverseLerp(-7f, -2f, d);
            float light = y > 63 ? 1.25f : 0.62f;
            c = Color.Lerp(c, c * light, Mathf.Clamp01(rim));
            foreach (var (rx, ry) in new[] { (20f, 20f), (107f, 20f), (20f, 107f), (107f, 107f) })
            {
                float r = new Vector2(x - rx, y - ry).magnitude;
                if (r < 6.5f)
                {
                    c = Color.Lerp(new Color(0.95f, 0.85f, 0.6f), new Color(0.4f, 0.28f, 0.12f), Mathf.Clamp01((rx - x + ry - y) / 12f + 0.5f));
                }
            }
            c.a = 1f - Mathf.Clamp01(d + 0.5f);
            return c;
        });

        /// <summary>A star rasterised from a polygon, for sprite vs vector.</summary>
        static Texture2D StarTex(int size)
        {
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0 ? 0.44f : 0.2f), a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                poly[i] = new Vector2(0.5f + r * Mathf.Cos(a), 0.5f + r * Mathf.Sin(a));
            }
            bool In(float px, float py)
            {
                bool c = false;
                for (int i = 0, j = 9; i < 10; j = i++)
                    if ((poly[i].y > py) != (poly[j].y > py) && px < (poly[j].x - poly[i].x) * (py - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
                return c;
            }
            return Tex(size, size, (x, y) =>
            {
                int n = 0;
                for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) if (In((x + (a + 0.5f) / 4f) / size, (y + (b + 0.5f) / 4f) / size)) n++;
                return new Color(1f, 1f, 1f, n / 16f);
            });
        }

        /// <summary>A gradient drawn with vertex colours, no texture at all.</summary>
        class VertexGradient : VisualElement
        {
            public VertexGradient(Color top, Color bottom)
            {
                generateVisualContent += ctx =>
                {
                    var r = contentRect;
                    var m = ctx.Allocate(4, 6);
                    m.SetNextVertex(new Vertex { position = new Vector3(0, 0, Vertex.nearZ), tint = top });
                    m.SetNextVertex(new Vertex { position = new Vector3(r.width, 0, Vertex.nearZ), tint = top });
                    m.SetNextVertex(new Vertex { position = new Vector3(r.width, r.height, Vertex.nearZ), tint = bottom });
                    m.SetNextVertex(new Vertex { position = new Vector3(0, r.height, Vertex.nearZ), tint = bottom });
                    foreach (ushort i in new ushort[] { 0, 1, 2, 2, 3, 0 }) m.SetNextIndex(i);
                };
            }
        }

        // ---------------- 1. fonts ----------------
        static Fa LoadFont(string file, int pt = 72, GlyphRenderMode mode = GlyphRenderMode.SDFAA, int padding = 9)
        {
            string p = Fonts + file;
            if (!File.Exists(p)) { Note($"font\tMISSING {p}"); return null; }
            var f = Fa.CreateFontAsset(p, 0, pt, padding, mode, 1024, 1024);
            Note($"font\t{file}\t{mode}\t{(f == null ? "NULL" : f.name + " " + f.atlasPopulationMode)}");
            return f;
        }

        static Label Face(VisualElement parent, Fa f, string text, float size, string color = "ink")
        {
            var l = Txt(parent, text, color);
            if (f != null) l.style.unityFontDefinition = FontDefinition.FromSDFFont(f);
            l.style.fontSize = size;
            return l;
        }

        const string Sample = "Mossbarrow, year 212: Wren built the granary.";

        [UnityTest, Timeout(Long)]
        public IEnumerator P1_Font()
        {
            yield return Open();
            var deflt = Txt(page, "default font", "tag");
            yield return Frames(2);
            Note($"font\tdefault in GamePanel: {deflt.resolvedStyle.unityFontDefinition.fontAsset?.name ?? "?"} / {deflt.resolvedStyle.unityFont?.name ?? "?"}; textSettings {GameUiPanel.Settings.textSettings?.name ?? "null"}");
            var lora = LoadFont("Lora-wght.ttf");
            var pt = LoadFont("PT_Serif-Web-Regular.ttf");
            var ptBold = LoadFont("PT_Serif-Web-Bold.ttf");
            var ptItalic = LoadFont("PT_Serif-Web-Italic.ttf");
            var bitmap = LoadFont("PT_Serif-Web-Regular.ttf", 42, GlyphRenderMode.SMOOTH_HINTED, 4);
            var baked = LoadFont("PT_Serif-Web-Regular.ttf");
            uint[] none = null;
            if (baked != null)
            {
                baked.TryAddCharacters(Sample + " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;!?'-", out string missing);
                baked.atlasPopulationMode = UnityEngine.TextCore.Text.AtlasPopulationMode.Static;
                Note($"font\tbaked atlas missing: [{missing}] {none}");
            }

            var card = El(page, "card");
            Face(card, null, "Default font 33: " + Sample, 33);
            Face(card, null, "Default font 38: " + Sample, 38);
            var c2 = El(page, "card"); c2.style.marginTop = 10;
            Face(c2, lora, "Lora SDF 33: " + Sample, 33);
            Face(c2, lora, "Lora SDF 38: " + Sample, 38);
            Face(c2, lora, "Lora SDF 47: " + Sample, 47);
            Face(c2, pt, "PT Serif SDF 33: " + Sample, 33);
            Face(c2, pt, "PT Serif SDF 38: " + Sample, 38);
            Face(c2, baked, "PT Serif static atlas 38: " + Sample, 38);
            Face(c2, bitmap, "PT Serif bitmap 38: " + Sample, 38);
            Tag("Bold and italic: real files vs faked");
            var c3 = El(page, "card");
            var b = Face(c3, ptBold, "Real bold file: " + Sample, 38);
            var i = Face(c3, ptItalic, "Real italic file: " + Sample, 38);
            Face(c3, pt, "Faked -unity-font-style bold: " + Sample, 38).AddToClassList("bold");
            Face(c3, pt, "Faked -unity-font-style italic: " + Sample, 38).AddToClassList("italic");
            Face(c3, lora, "Lora (variable) faked bold: " + Sample, 38).AddToClassList("bold");
            Face(c3, pt, "Rich text <b>bold</b> <i>italic</i> on regular", 38);

            Tag("Fallback: Lora alone, then Lora > PT Serif > default");
            const string intl = "Ñandú cœur — Ελληνικά Русский 日本語 ✓ ★ → ⟳";
            var plain = LoadFont("Lora-wght.ttf");
            var withFb = LoadFont("Lora-wght.ttf");
            var fbDefault = deflt.resolvedStyle.unityFontDefinition.fontAsset;
            if (withFb != null)
            {
                withFb.fallbackFontAssetTable = new List<Fa> { pt };
                if (fbDefault != null) withFb.fallbackFontAssetTable.Add(fbDefault);
            }
            var c4 = El(page, "card");
            Face(c4, plain, intl, 38);
            Face(c4, withFb, intl, 38);
            Face(c4, null, intl, 38);
            yield return Cap("01-font", 12);
            AssertNoErrors("font");
        }

        // ---------------- 2. soft shadows ----------------
        static FilterFunctionDefinition DropDef()
        {
            var def = ScriptableObject.CreateInstance<FilterFunctionDefinition>();
            def.filterName = "drop-shadow";
            def.parameters = new FilterParameterDeclaration[0];
            var m = new PostProcessingMargins { left = 48, right = 48, top = 48, bottom = 64 };
            def.passes = new[] { new PostProcessingPass { material = new Material(dropShadow), passIndex = 0, writeMargins = m, computeRequiredWriteMarginsCallback = (FilterFunction f) => m, computeRequiredReadMarginsCallback = (FilterFunction f) => m } };
            return def;
        }

        VisualElement Shadowed(string label, Action<VisualElement> shadow)
        {
            Tag(label);
            var host = El(page, "shadow-host");
            shadow?.Invoke(host);
            Txt(El(host, "card"), "Panel on the tank", "ink");
            return host;
        }

        [UnityTest, Timeout(Long)]
        public IEnumerator P2_Shadows()
        {
            yield return Open();
            var tex = ShadowTex();
            Shadowed("Hard offset element: plain USS", h => El(h, "shadow-hard"));
            Shadowed("9-slice shadow texture", h => El(h, "shadow-9").style.backgroundImage = Background.FromTexture2D(tex));
            Shadowed("Sibling + filter blur(22), same size as the panel: barely shows", h => El(h, "shadow-blur"));
            Shadowed("Sibling + filter blur(18), 16 px wider each side and 34 px lower", h => El(h, "sb-a"));
            Shadowed("UI Shader Graph on a shadow element (-unity-material)", h =>
            {
                if (softShadowGraph == null) return;
                var e = El(h, "sg-shadow");
                e.style.unityMaterial = new StyleMaterialDefinition(new MaterialDefinition(new Material(softShadowGraph)));
            });
            Shadowed("Custom FilterFunctionDefinition on the panel itself (hand HLSL)", h =>
            {
                var def = DropDef();
                h.schedule.Execute(() => h.Q(className: "card").style.filter = new List<FilterFunction> { new FilterFunction(def) });
            });
#if UNITY_EDITOR
            yield return Frames(4);
            yield return Until(() => !ShaderUtil.anythingCompiling, 25f);
            yield return Frames(10);
#endif
            yield return Cap("02-shadows", 12);
            AssertNoErrors("shadows");
        }

        [UnityTest, Timeout(Long)]
        public IEnumerator P2b_FilterBlurSizes()
        {
            yield return Open();
            FilterFunction B(float v) { var f = new FilterFunction(FilterFunctionType.Blur); f.AddParameter(new FilterParameter(v)); return f; }
            foreach (var col in new[] { new Color(0.12f, 0.1f, 0.09f), new Color(0.9f, 0.1f, 0.1f), new Color(0.12f, 0.1f, 0.09f, 0.75f) })
            {
                Tag("alone: blur 0 / 6 / 14 / 22 / 40, colour " + col);
                var r = Row(page);
                foreach (float v in new[] { 0f, 6f, 14f, 22f, 40f })
                {
                    var e = El(r);
                    e.style.width = 150; e.style.height = 110; e.style.marginRight = 50; e.style.marginBottom = 30; e.style.marginTop = 30;
                    e.style.backgroundColor = col;
                    if (v > 0f) e.style.filter = new List<FilterFunction> { B(v) };
                }
            }
            Tag("with a card sibling on top, not absolute");
            var h2 = El(page);
            h2.style.height = 200; h2.style.marginTop = 30;
            var sh = El(h2);
            sh.style.position = Position.Absolute; sh.style.left = 40; sh.style.right = 40; sh.style.top = 20; sh.style.bottom = 0;
            sh.style.backgroundColor = new Color(0.12f, 0.1f, 0.09f);
            sh.style.filter = new List<FilterFunction> { B(22f) };
            var cd = El(h2, "card");
            cd.style.position = Position.Absolute; cd.style.left = 40; cd.style.right = 40; cd.style.top = 0; cd.style.bottom = 20;
            Tag("same, the sheet's radius on the shadow element");
            var h3 = El(page);
            h3.style.height = 200; h3.style.marginTop = 30;
            var sh3 = El(h3, "shadow-blur");
            var cd3 = El(h3, "card");
            cd3.style.position = Position.Absolute; cd3.style.left = 0; cd3.style.right = 0; cd3.style.top = 0; cd3.style.bottom = 0;
            yield return Cap("02b-filter-blur-sizes", 12);
        }

        // ---------------- 3. blur behind ----------------
        sealed class BackdropCopy : ScriptableRenderPass
        {
            readonly RTHandle[] chain;
            public BackdropCopy(params RenderTexture[] rts)
            {
                chain = rts.Select(r => RTHandles.Alloc(r)).ToArray();
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph rg, ContextContainer frame)
            {
                var res = frame.Get<UniversalResourceData>();
                if (res.isActiveTargetBackBuffer) return;
                var src = res.activeColorTexture;
                foreach (var h in chain)
                {
                    var dst = rg.ImportTexture(h);
                    rg.AddBlitPass(src, dst, Vector2.one, Vector2.zero, passName: "UiLimits backdrop");
                    src = dst;
                }
            }

            public void Release() { foreach (var h in chain) h.Release(); }
        }

        BackdropCopy copy;
        Camera copyCam;
        readonly List<RenderTexture> rts = new();
        Action<ScriptableRenderContext, Camera> hook;

        RenderTexture Rt(int w, int h, int depth = 0)
        {
            var rt = new RenderTexture(w, h, depth, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            rt.Create();
            rts.Add(rt);
            return rt;
        }

        void StartCopy(params RenderTexture[] chain)
        {
            StopBackdrop();
            copy = new BackdropCopy(chain);
            hook = (ctx, cam) =>
            {
                if (cam == Camera.main) cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(copy);
            };
            RenderPipelineManager.beginCameraRendering += hook;
        }

        void StartSecondCamera(RenderTexture rt)
        {
            StopBackdrop();
            var main = Camera.main;
            var go = new GameObject("UiLimitsCopyCam");
            copyCam = go.AddComponent<Camera>();
            copyCam.CopyFrom(main);
            copyCam.targetTexture = rt;
            copyCam.depth = main.depth - 1;
            go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        }

        void StopBackdrop()
        {
            if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
            hook = null;
            copy?.Release();
            copy = null;
            if (copyCam != null) UnityEngine.Object.Destroy(copyCam.gameObject);
            copyCam = null;
        }

        /// <summary>A panel showing a screen copy behind itself. Aligned by <see cref="AlignFrost"/> after layout.</summary>
        VisualElement Frost(VisualElement parent, RenderTexture rt, float blur, string text)
        {
            var clip = El(parent, "frost");
            var inner = El(clip, "frost-inner");
            var img = El(inner, "frost-img");
            img.name = "frost-img";
            img.style.backgroundImage = Background.FromRenderTexture(rt);
            if (blur > 0f)
            {
                var f = new FilterFunction(FilterFunctionType.Blur);
                f.AddParameter(new FilterParameter(blur));
                inner.style.filter = new List<FilterFunction> { f };
            }
            El(clip, "frost-tint");
            Txt(clip, text, "frost-text");
            return clip;
        }

        void AlignFrost()
        {
            float w = root.worldBound.width, h = root.worldBound.height;
            root.Query("frost-img").ForEach(img =>
            {
                var inner = img.parent;
                img.style.left = -inner.worldBound.x;
                img.style.top = -inner.worldBound.y;
                img.style.width = w;
                img.style.height = h;
            });
        }

        [UnityTest, Timeout(Long)]
        public IEnumerator P3_Blur()
        {
            yield return Open();
            int W = Screen.width, H = Screen.height;
            RenderTexture r4 = Rt(W / 4, H / 4), r8 = Rt(W / 8, H / 8), r16 = Rt(W / 16, H / 16);
            StartCopy(r4, r8, r16);

            Tag("USS filter: blur(6px) on a translucent panel: only its own subtree blurs, the tank does not");
            var self = El(page, "glass-flat", "blur-self");
            Txt(self, "This text is the panel's child", "ink");
            Tag("Flat tint, no blur: rgba panel");
            Txt(El(page, "glass-flat"), "Tinted scrim. The tank stays sharp behind it.", "ink");
            El(page).style.height = 250;
            Tag("Frosted: URP copy at 1/4 + USS filter: blur(14px) in the panel");
            Frost(page, r4, 14f, "Real blur of the moving world");
            Tag("Frosted: URP copy at 1/8, bilinear, no filter");
            Frost(page, r8, 0f, "Copy only");
            Tag("Frosted: URP copy at 1/16, bilinear, no filter");
            Frost(page, r16, 0f, "Copy only, smaller");
            yield return Frames(3);
            AlignFrost();
            yield return Cap("03-blur", 10);
            SimLayoutSource.DebugSkipYears(0f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Cap("03-blur-later", 2);

            NewPage();
            RenderTexture cam4 = Rt(W / 4, H / 4, 24);
            StartSecondCamera(cam4);
            El(page).style.height = 800;
            Tag("Frosted: second low-res camera (1/4) + USS blur(14px)");
            Frost(page, cam4, 14f, "Second camera route");
            yield return Frames(3);
            AlignFrost();
            yield return Cap("03-blur-second-camera", 12);
            StopBackdrop();
            NewPage();
            RenderTexture e4 = Rt(W / 4, H / 4), e8 = Rt(W / 8, H / 8);
            StartCopy(e4, e8);
            El(page).style.height = 800;
            Tag("Frosted: URP copy at 1/8 + USS blur(6px): a lighter filter on a smaller copy");
            Frost(page, e8, 6f, "Smaller copy, smaller blur");
            yield return Frames(3);
            AlignFrost();
            yield return Cap("03-blur-eighth-filter", 12);
            StopBackdrop();
            AssertNoErrors("blur");
        }

        // ---------------- 4. gradients, masks, borders ----------------
        static void StretchFill(VisualElement e)
        {
            e.style.backgroundPositionX = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(ScaleMode.StretchToFill);
            e.style.backgroundPositionY = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(ScaleMode.StretchToFill);
            e.style.backgroundRepeat = BackgroundPropertyHelper.ConvertScaleModeToBackgroundRepeat(ScaleMode.StretchToFill);
            e.style.backgroundSize = BackgroundPropertyHelper.ConvertScaleModeToBackgroundSize(ScaleMode.StretchToFill);
        }

        [UnityTest, Timeout(Long)]
        public IEnumerator P4_GradientsMasks()
        {
            yield return Open();
            Color warmA = new Color32(0xF2, 0xD4, 0x9B, 255), warmB = new Color32(0x8A, 0x5A, 0x2B, 255);
            Tag("Gradients: 1x256 texture | SVG gradient | vertex colours");
            var r1 = Row(page);
            El(r1, "sw").style.backgroundImage = Background.FromTexture2D(Gradient(1, 256, warmB, warmA));
            var sv = El(r1, "sw");
            sv.style.backgroundImage = Background.FromVectorImage(svgGrad);
            StretchFill(sv);
            var vg = new VertexGradient(warmA, warmB); vg.AddToClassList("sw"); r1.Add(vg);
            Tag("Banding test: a subtle gradient, 8-bit texture over 1000 px");
            var band = El(page);
            band.style.height = 180;
            band.style.backgroundImage = Background.FromTexture2D(Gradient(256, 1, new Color32(0xEF, 0xE3, 0xCC, 255), new Color32(0xDE, 0xCF, 0xB2, 255), false));
            StretchFill(band);

            var dith = El(page);
            dith.style.height = 100; dith.style.marginTop = 6;
            Color dA = new Color32(0xEF, 0xE3, 0xCC, 255), dB = new Color32(0xDE, 0xCF, 0xB2, 255);
            var rng = new System.Random(5);
            dith.style.backgroundImage = Background.FromTexture2D(Tex(1024, 64, (x, y) =>
            {
                var c = Color.Lerp(dA, dB, x / 1023f);
                float n = ((float)rng.NextDouble() - 0.5f) * 1.5f / 255f;
                return new Color(c.r + n, c.g + n, c.b + n, 1f);
            }, mips: false));
            StretchFill(dith);
            Tag("Rounded and circle masks: overflow hidden over an image | nested mask");
            var r2 = Row(page);
            El(El(r2, "mask-r"), "mask-img").style.backgroundImage = Background.FromTexture2D(photo);
            El(El(r2, "mask-c"), "mask-img").style.backgroundImage = Background.FromTexture2D(photo);
            var outer = El(r2, "mask-r");
            outer.style.backgroundColor = new Color(0.78f, 0.44f, 0.31f);
            El(El(outer, "mask-nested"), "mask-img").style.backgroundImage = Background.FromTexture2D(photo);
            Tag("Same image with border-radius on itself, no overflow (no stencil)");
            var r3 = Row(page);
            var selfR = El(r3, "b-pill-bg");
            selfR.style.backgroundImage = Background.FromTexture2D(photo);

            Tag("Rotated child inside a rounded mask");
            var r5 = Row(page);
            var rot = El(El(r5, "mask-r"), "mask-img");
            rot.style.backgroundImage = Background.FromTexture2D(photo);
            rot.style.rotate = new Rotate(20f);

            Tag("Borders: uneven widths | two colours (bevel) | uneven radii");
            var r4 = Row(page);
            Txt(El(r4, "b-uneven"), "2/6/14/6", "ink");
            Txt(El(r4, "b-colors"), "bevel", "ink");
            Txt(El(r4, "b-radii"), "60/8", "ink");
            yield return Cap("04-gradients-masks", 12);
            AssertNoErrors("gradients");
        }

        // ---------------- 5. text ----------------
        [UnityTest, Timeout(Long)]
        public IEnumerator P5_Text()
        {
            yield return Open();
            var pt = LoadFont("PT_Serif-Web-Regular.ttf");
            var bg = El(page);
            Tag("Outline 0 / 1 / 2 / 4 px, default font, then PT Serif");
            var bk = El(page, "backing");
            var o = Row(bk);
            foreach (var c in new[] { "", "o1", "o2", "o4" }) { var l = Txt(o, "Year 212  ", "t"); if (c != "") l.AddToClassList(c); }
            var o2 = Row(bk);
            foreach (var c in new[] { "", "o1", "o2", "o4" }) { var l = Txt(o2, "Year 212  ", "t"); if (c != "") l.AddToClassList(c); if (pt != null) l.style.unityFontDefinition = FontDefinition.FromSDFFont(pt); }
            Tag("text-shadow: hard / blur 4 / blur 12, default then PT Serif");
            var bk2 = El(page, "backing");
            var s = Row(bk2);
            foreach (var c in new[] { "s0", "s4", "s12" }) Txt(s, "Year 212  ", "t", c);
            var s2 = Row(bk2);
            foreach (var c in new[] { "s0", "s4", "s12" }) { var l = Txt(s2, "Year 212  ", "t", c); if (pt != null) l.style.unityFontDefinition = FontDefinition.FromSDFFont(pt); }
            Tag("Letter spacing -2 / 0 / +6, word spacing 24");
            Txt(page, "Tight letters on the tank", "t", "ls-m");
            Txt(page, "Normal letters on the tank", "t");
            Txt(page, "Wide letters on the tank", "t", "ls-p");
            Txt(page, "Wide words on the tank", "t", "ws");
            Tag("Line height. Plain | rich tag 150 % | rich tag 70px | -unity-paragraph-spacing 30 | spacing 0");
            const string para = "The river rose in spring and the people moved their fires uphill, as their mothers did.";
            var card = El(page, "card");
            var plain = Txt(card, para, "ink");
            plain.style.fontSize = 34;
            var tagged = Txt(card, "<line-height=150%>" + para + "</line-height>", "ink");
            tagged.style.fontSize = 34; tagged.style.marginTop = 10;
            var px = Txt(card, "<line-height=70px>" + para + "</line-height>", "ink");
            px.style.fontSize = 34; px.style.marginTop = 10;
            var para2 = Txt(card, "First paragraph, one line.\nSecond paragraph, spaced by USS.", "ink", "para");
            para2.style.fontSize = 34; para2.style.marginTop = 10;
            var para0 = Txt(card, "First paragraph, one line.\nSecond paragraph, spaced by USS.", "ink");
            para0.style.fontSize = 34; para0.style.marginTop = 10;
            Tag("Wrapping: ellipsis no-wrap | pre | pre-wrap | auto-size");
            Txt(page, "A very long name that cannot fit on one line at all", "t-sm", "nowrap");
            Txt(page, "pre  keeps   spaces\nand breaks", "t-sm", "pre");
            Txt(page, "pre-wrap  keeps   spaces and still wraps at the box edge", "t-sm", "prewrap");
            Txt(page, "Auto-sized to fit this box, whatever it says", "t-sm", "autosize");
            Tag("Bold and italic on the default font, then rich text tags");
            Txt(page, "bold", "t-sm", "bold");
            Txt(page, "italic", "t-sm", "italic");
            Txt(page, "bold and italic", "t-sm", "bolditalic");
            Txt(page, "<u>under</u> <s>strike</s> <color=#C66F4E>colour</color> <size=120%>size</size> x<sup>2</sup>", "t-sm");
            yield return Frames(4);
            Note($"text\tplain h {plain.resolvedStyle.height}\ttagged 150% h {tagged.resolvedStyle.height}\ttagged 70px h {px.resolvedStyle.height}\tparagraph 30 h {para2.resolvedStyle.height}\tparagraph 0 h {para0.resolvedStyle.height}");
            yield return Cap("05-text", 10);
            AssertNoErrors("text");
        }

        // ---------------- 6. icons ----------------
        [UnityTest, Timeout(Long)]
        public IEnumerator P6_Icons()
        {
            yield return Open();
            var backing = El(page);
            backing.style.backgroundColor = new Color(0.94f, 0.89f, 0.8f);
            backing.style.paddingLeft = 24;
            backing.style.paddingBottom = 24;
            backing.style.borderBottomLeftRadius = 24; backing.style.borderBottomRightRadius = 24;
            VisualElement p0 = page;
            page = backing;
            void Ic(VisualElement row, Background b, string cls, string tint)
            {
                var e = El(row, cls);
                if (tint != null) e.AddToClassList(tint);
                e.style.backgroundImage = b;
            }
            var vec = Background.FromVectorImage(svgFill);
            var line = Background.FromVectorImage(svgStroke);
            Tag("SVG VectorImage at 44 pt (121 px), tinted by USS: white / terracotta / moss / ink. Second row a line icon");
            var r1 = Row(page);
            foreach (var t in new[] { null, "tint-a", "tint-b", "tint-c" }) Ic(r1, vec, "icon", t);
            var r2 = Row(page);
            foreach (var t in new[] { null, "tint-a", "tint-b", "tint-c" }) Ic(r2, line, "icon", t);
            Tag("24 pt (66 px): vector / sprite 64 / sprite 128");
            var r3 = Row(page);
            Ic(r3, vec, "icon-sm", "tint-c");
            Ic(r3, line, "icon-sm", "tint-c");
            Ic(r3, Background.FromTexture2D(StarTex(64)), "icon-sm", "tint-c");
            Ic(r3, Background.FromTexture2D(StarTex(128)), "icon-sm", "tint-c");
            Tag("Sprite at 44 pt from 64 / 96 / 128 / 256 px sources, tinted");
            var r4 = Row(page);
            foreach (int size in new[] { 64, 96, 128, 256 }) Ic(r4, Background.FromTexture2D(StarTex(size)), "icon", "tint-c");
            Tag("Scaled 2x by USS scale: vector / sprite 128");
            var r5 = Row(page);
            Ic(r5, vec, "icon-xl", "tint-a");
            Ic(r5, Background.FromTexture2D(StarTex(128)), "icon-xl", "tint-a");
            page = p0;
            yield return Cap("06-icons", 12);
            AssertNoErrors("icons");
        }

        // ---------------- 7. motion ----------------
        static string Read(VisualElement e, string prop)
        {
            var p = typeof(IResolvedStyle).GetProperty(prop);
            if (p == null) return "(no resolved value)";
            object v = p.GetValue(e.resolvedStyle);
            if (v is IEnumerable<FilterFunction> ff) return string.Join(";", ff.Select(f => f.type + "(" + (f.parameterCount > 0 ? f.GetParameter(0).floatValue.ToString("0.0") : "") + ")"));
            return v?.ToString() ?? "null";
        }

        [UnityTest, Timeout(Long)]
        public IEnumerator P7_Motion()
        {
            yield return Open();
            FilterFunction Blur(float v) { var f = new FilterFunction(FilterFunctionType.Blur); f.AddParameter(new FilterParameter(v)); return f; }
            var cases = new (string prop, Action<IStyle> from, Action<IStyle> to)[]
            {
                ("opacity", s => s.opacity = 1f, s => s.opacity = 0f),
                ("translate", s => s.translate = new Translate(0, 0), s => s.translate = new Translate(100, 0)),
                ("scale", s => s.scale = new Scale(Vector2.one), s => s.scale = new Scale(new Vector2(2, 2))),
                ("rotate", s => s.rotate = new Rotate(0), s => s.rotate = new Rotate(90)),
                ("width", s => s.width = 100, s => s.width = 300),
                ("height", s => s.height = 100, s => s.height = 300),
                ("height", s => s.height = StyleKeyword.Auto, s => s.height = 300),
                ("left", s => s.left = 0, s => s.left = 200),
                ("color", s => s.color = Color.white, s => s.color = Color.red),
                ("backgroundColor", s => s.backgroundColor = Color.white, s => s.backgroundColor = Color.red),
                ("borderTopLeftRadius", s => s.borderTopLeftRadius = 0, s => s.borderTopLeftRadius = 40),
                ("borderTopWidth", s => s.borderTopWidth = 0, s => s.borderTopWidth = 20),
                ("borderTopColor", s => s.borderTopColor = Color.white, s => s.borderTopColor = Color.red),
                ("marginLeft", s => s.marginLeft = 0, s => s.marginLeft = 40),
                ("paddingLeft", s => s.paddingLeft = 0, s => s.paddingLeft = 40),
                ("fontSize", s => s.fontSize = 30, s => s.fontSize = 60),
                ("letterSpacing", s => s.letterSpacing = 0, s => s.letterSpacing = 20),
                ("wordSpacing", s => s.wordSpacing = 0, s => s.wordSpacing = 20),
                ("unityTextOutlineWidth", s => s.unityTextOutlineWidth = 0, s => s.unityTextOutlineWidth = 8),
                ("textShadow", s => s.textShadow = new TextShadow(), s => s.textShadow = new TextShadow { offset = new Vector2(10, 10), blurRadius = 8, color = Color.black }),
                ("unityBackgroundImageTintColor", s => s.unityBackgroundImageTintColor = Color.white, s => s.unityBackgroundImageTintColor = Color.red),
                ("filter", s => s.filter = new List<FilterFunction> { Blur(0) }, s => s.filter = new List<FilterFunction> { Blur(20) }),
                ("flexGrow", s => s.flexGrow = 0, s => s.flexGrow = 1),
                ("justifyContent", s => s.justifyContent = Justify.FlexStart, s => s.justifyContent = Justify.Center),
                ("flexDirection", s => s.flexDirection = FlexDirection.Column, s => s.flexDirection = FlexDirection.Row),
                ("display", s => s.display = DisplayStyle.Flex, s => s.display = DisplayStyle.None),
                ("visibility", s => s.visibility = Visibility.Visible, s => s.visibility = Visibility.Hidden),
                ("position", s => s.position = Position.Absolute, s => s.position = Position.Relative),
                ("unityFontStyleAndWeight", s => s.unityFontStyleAndWeight = FontStyle.Normal, s => s.unityFontStyleAndWeight = FontStyle.Bold),
            };
            foreach (var (prop, from, to) in cases)
            {
                var e = Txt(page, "x", "tr");
                from(e.style);
                yield return Frames(3);
                string a = Read(e, prop);
                var ran = new List<string>();
                e.RegisterCallback<TransitionRunEvent>(ev => ran.AddRange(ev.stylePropertyNames.Select(n => n.ToString())));
                to(e.style);
                yield return new WaitForSecondsRealtime(0.2f);
                string mid = Read(e, prop);
                yield return new WaitForSecondsRealtime(0.35f);
                string end = Read(e, prop);
                string verdict = mid != a && mid != end ? "INTERPOLATES" : a == end ? "UNCHANGED?" : mid == end ? "JUMPS at once" : "HOLDS then jumps";
                Note($"motion\t{prop}\t{a}\t{mid}\t{end}\t{verdict}	ran [{string.Join(",", ran)}]");
                e.RemoveFromHierarchy();
                yield return null;
            }

            // enter and exit, at 1.2 s so a shot can catch them
            NewPage();
            var scrim = El(page, "scrim", "fade", "fade-out");
            var sheet1 = El(page, "sheet", "slide", "slide-out");
            Txt(sheet1, "A sheet entering", "ink");
            Txt(sheet1, "translate + opacity + scrim fade", "ink");
            yield return Frames(2);
            yield return Cap("07-motion-a-start", 0);
            scrim.RemoveFromClassList("fade-out");
            sheet1.RemoveFromClassList("slide-out");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Cap("07-motion-b-mid", 0);
            yield return new WaitForSecondsRealtime(1f);
            yield return Cap("07-motion-c-end", 0);

            // a class removed in the same frame the element is added does not animate
            var same = El(page, "sheet", "slide", "slide-out");
            same.RemoveFromClassList("slide-out");
            yield return Frames(3);
            Note($"motion-enter\tclass removed in the same frame as Add: translate after 3 frames = {same.resolvedStyle.translate.y}");
            same.RemoveFromHierarchy();
            var hidden = El(page, "sheet", "slide");
            hidden.style.display = DisplayStyle.None;
            hidden.AddToClassList("slide-out");
            yield return Frames(3);
            hidden.style.display = DisplayStyle.Flex;
            hidden.RemoveFromClassList("slide-out");
            yield return new WaitForSecondsRealtime(0.2f);
            Note($"motion-enter\tdisplay none to flex + class off in one frame: translate y at 0.2 s = {hidden.resolvedStyle.translate.y} (height {hidden.resolvedStyle.height})");
            hidden.RemoveFromHierarchy();
            var exit = El(page, "sheet", "slide");
            yield return Frames(3);
            int ended = 0;
            exit.RegisterCallback<TransitionEndEvent>(_ => ended++);
            exit.AddToClassList("slide-out");
            yield return new WaitForSecondsRealtime(1.5f);
            Note($"motion-exit\tTransitionEndEvent fired {ended} times (use it to set display none after an exit)");
            AssertNoErrors("motion");
        }

        // ---------------- 8. layout ----------------
        [UnityTest, Timeout(Long)]
        public IEnumerator P8_Layout()
        {
            yield return Open();
            var inset = GameUiPanel.SafeInsets(new Rect(0, 102, 1179, 2277), 1179, 2556);
            Note($"layout\tinsets for an iPhone-like safe area: {inset}");
            page.style.paddingTop = inset.y + 10;
            var box = El(root, "safe-box");
            box.style.left = inset.x; box.style.top = inset.y; box.style.right = inset.z; box.style.bottom = inset.w;
            box.pickingMode = PickingMode.Ignore;

            Tag("gap: 40px (ignored) vs one-sided margins");
            var g = El(page, "gap-row");
            var g1 = Txt(g, "One", "chip"); var g2 = Txt(g, "Two", "chip"); Txt(g, "Three", "chip");
            var m = El(page, "m-row"); m.style.marginTop = 10;
            Txt(m, "One", "chip"); Txt(m, "Two", "chip"); Txt(m, "Three", "chip");
            Tag(":first-child / :last-child: green if the rule applied");
            var pr = El(page, "lrow");
            var pf = Txt(pr, "first", "chip", "first-test"); var pl = Txt(pr, "last", "chip", "last-test");
            Tag("aspect-ratio: 1.5 on width 300");
            var ar = El(page, "ratio");
            Tag("Safe area: green box is a fake iPhone inset. The sheet runs under the home bar, its content does not");
            var sheet1 = El(page, "sheet");
            Txt(sheet1, "Bottom sheet, height: 30 %", "ink");
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.AddToClassList("scroll");
            sheet1.Add(sv);
            for (int i = 1; i <= 8; i++) Txt(sv, $"Scrolling row {i}", "ink");
            sheet1.style.paddingBottom = inset.w + 10;
            yield return Frames(4);
            Note($"layout\tgap: chip1 right {g1.worldBound.xMax} chip2 left {g2.worldBound.xMin} delta {g2.worldBound.xMin - g1.worldBound.xMax}");
            Note($"layout\tpseudo first {pf.resolvedStyle.backgroundColor} last {pl.resolvedStyle.backgroundColor}");
            Note($"layout\taspect-ratio h {ar.resolvedStyle.height}");
            Note($"layout\tsheet h {sheet1.resolvedStyle.height} of {root.resolvedStyle.height} = {sheet1.resolvedStyle.height / root.resolvedStyle.height:P1}");
            yield return Cap("08-layout", 10);
            AssertNoErrors("layout");
        }

        // ---------------- 9. tactile ----------------
        [UnityTest, Timeout(Long)]
        public IEnumerator P9_Tactile()
        {
            yield return Open();
            var pt = LoadFont("PT_Serif-Web-Regular.ttf");
            var ptBold = LoadFont("PT_Serif-Web-Bold.ttf");
            var paper = PaperTex(false);
            var paperTile = PaperTex(true);
            var brass = BrassTex();
            void Font(VisualElement e, Fa f) { if (f != null) e.style.unityFontDefinition = FontDefinition.FromSDFFont(f); }

            Tag("Paper 9-slice (procedural stand-in), deckled edge, stretched centre");
            var p1 = El(page, "paper");
            p1.style.backgroundImage = Background.FromTexture2D(paper);
            var h1 = Txt(p1, "The Granary Stands", "ink"); h1.style.fontSize = 56; Font(h1, ptBold);
            var b1 = Txt(p1, "Wren and her cousins raised it before the first frost. People came from across the stream to see it.", "ink"); Font(b1, pt);
            b1.style.whiteSpace = WhiteSpace.Normal;
            Tag("Same paper at button size, and tiled slices");
            var r0 = Row(page);
            var p2 = El(r0, "paper-sm");
            p2.style.backgroundImage = Background.FromTexture2D(paper);
            Txt(p2, "Remember", "ink");
            var p3 = El(r0, "paper-sm", "paper-tiled");
            p3.style.backgroundImage = Background.FromTexture2D(paper);
            Txt(p3, "Tiled", "ink");
            Tag("Repeating paper tile by background-repeat, rounded");
            var tile = El(page);
            tile.style.height = 150; tile.style.borderTopLeftRadius = 32; tile.style.borderTopRightRadius = 32; tile.style.borderBottomLeftRadius = 32; tile.style.borderBottomRightRadius = 32;
            tile.style.backgroundImage = Background.FromTexture2D(paperTile);
            tile.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            tile.style.backgroundSize = new BackgroundSize(new Length(256), new Length(256));
            Tag("Brass 9-slice: 420 and 640 wide");
            var r1 = Row(page);
            Txt(El(r1, "brass"), "Shape", "brass-t").parent.style.backgroundImage = Background.FromTexture2D(brass);
            Txt(El(r1, "brass", "brass-wide"), "Keep Watch", "brass-t").parent.style.backgroundImage = Background.FromTexture2D(brass);
            Tag("Portrait in a circle mask with a brass ring (real tank pixels as the image)");
            var r2 = Row(page);
            var ring = El(r2, "portrait-ring");
            El(ring, "portrait-img").style.backgroundImage = Background.FromTexture2D(photo);
            var ring2 = El(r2, "portrait-ring");
            ring2.style.width = 121; ring2.style.height = 121; ring2.style.borderTopLeftRadius = 60; ring2.style.borderTopRightRadius = 60; ring2.style.borderBottomLeftRadius = 60; ring2.style.borderBottomRightRadius = 60; ring2.style.borderTopWidth = 6; ring2.style.borderBottomWidth = 6; ring2.style.borderLeftWidth = 6; ring2.style.borderRightWidth = 6;
            El(ring2, "portrait-img").style.backgroundImage = Background.FromTexture2D(photo);
            yield return Cap("09-tactile", 14);
            AssertNoErrors("tactile");
        }

        // ---------------- frame cost, Editor only ----------------
        struct Stat { public string name; public List<float> frames; public List<float> cpu; public List<float> gpu; public long setPass, draw; }

        static float Median(List<float> v) { if (v.Count == 0) return 0f; var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
        static float P(List<float> v, float q) { if (v.Count == 0) return 0f; var s = v.OrderBy(x => x).ToList(); return s[Mathf.Min(s.Count - 1, (int)(s.Count * q))]; }

        IEnumerator Measure(Stat st, int frames)
        {
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var timings = new FrameTiming[1];
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                st.frames.Add(Time.unscaledDeltaTime * 1000f);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    st.cpu.Add((float)timings[0].cpuFrameTime);
                    st.gpu.Add((float)timings[0].gpuFrameTime);
                }
            }
            Note($"cost-raw\t{st.name}\tsetpass {setPass.LastValue}\tdraw {draws.LastValue}");
        }

        VisualElement Filled(VisualElement parent, params string[] cls)
        {
            var e = El(parent, cls);
            for (int i = 0; i < 6; i++) Txt(e, "A line of panel text, about this long", "ink");
            return e;
        }

        [UnityTest, Timeout(Long)]
        public IEnumerator Z_Cost()
        {
            yield return Open(hideHud: false);
            int W = Screen.width, H = Screen.height, vs = QualitySettings.vSyncCount, fps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Note($"cost\tfeature {FrameTimingManager.IsFeatureEnabled()}\tvsync {vs}\ttarget {fps}\tscreen {W}x{H}\tmsaa {QualitySettings.antiAliasing}");
            RenderTexture r4 = Rt(W / 4, H / 4), r8 = Rt(W / 8, H / 8), r16 = Rt(W / 16, H / 16), cam4 = Rt(W / 4, H / 4, 24);
            var shadow = ShadowTex();
            IVisualElementScheduledItem loop = null;
            var scenarios = new (string name, Action setup)[]
            {
                ("00 tank + HUD (baseline)", () => { }),
                ("01 solid sheet, 30 % height, 6 lines", () => Filled(page, "c-sheet", "c-solid")),
                ("02 fullscreen flat scrim + sheet", () => { El(page, "scrim"); Filled(page, "c-sheet", "c-solid"); }),
                ("03 sheet with rounded overflow mask", () => Filled(page, "c-sheet", "c-solid", "c-mask")),
                ("04 sheet, text outline + shadow", () => { var e = Filled(page, "c-sheet", "c-solid"); foreach (var l in e.Children()) l.AddToClassList("c-fx"); }),
                ("05 sheet + 9-slice shadow", () => { El(page, "c-shadow-9").style.backgroundImage = Background.FromTexture2D(shadow); Filled(page, "c-sheet", "c-solid"); }),
                ("06 sheet + blurred sibling shadow", () => { El(page, "c-shadow-blur"); Filled(page, "c-sheet", "c-solid"); }),
                ("07 sheet with filter blur on itself", () => Filled(page, "c-sheet", "c-solid", "c-blur")),
                ("07b ten small panels, each with filter blur 6", () => { for (int k = 0; k < 10; k++) { var e = El(page, "c-blur"); e.style.height = 60; e.style.marginTop = 10; e.style.backgroundColor = new Color(0.9f, 0.85f, 0.75f); } }),
                ("07c sheet with UI Shader Graph material", () => { var e = Filled(page, "c-sheet", "c-solid"); if (softShadowGraph != null) e.style.unityMaterial = new StyleMaterialDefinition(new MaterialDefinition(new Material(softShadowGraph))); }),
                ("07d sheet with custom drop-shadow filter", () => { var e = Filled(page, "c-sheet", "c-solid"); e.style.filter = new List<FilterFunction> { new FilterFunction(DropDef()) }; }),
                ("08 sheet moving (translate + opacity loop)", () =>
                {
                    var e = Filled(page, "c-sheet", "c-solid", "c-loop");
                    loop = e.schedule.Execute(() => e.ToggleInClassList("c-loop-out")).Every(310);
                }),
                ("08b sheet moving with blurred sibling shadow", () =>
                {
                    var box = El(page, "fill", "c-loop");
                    El(box, "c-shadow-blur");
                    Filled(box, "c-sheet", "c-solid");
                    loop = box.schedule.Execute(() => box.ToggleInClassList("c-loop-out")).Every(310);
                }),
                ("09 URP copy only (1/4,1/8,1/16), not shown", () => StartCopy(r4, r8, r16)),
                ("10 frosted sheet, copy 1/8, no filter", () => { StartCopy(r4, r8); FrostSheet(r8, 0f); }),
                ("10b frosted sheet, copy 1/8 + USS blur 6", () => { StartCopy(r4, r8); FrostSheet(r8, 6f); }),
                ("11 frosted sheet, copy 1/4 + USS blur 14", () => { StartCopy(r4); FrostSheet(r4, 14f); }),
                ("12 frosted sheet, second camera 1/4 + USS blur 14", () => { StartSecondCamera(cam4); FrostSheet(cam4, 14f); }),
            };
            var results = scenarios.Select(s => new List<(float med, float p95, float cpu, float gpu)>()).ToArray();
            for (int round = 0; round < 3; round++)
            {
                for (int i = 0; i < scenarios.Length; i++)
                {
                    StopBackdrop();
                    loop?.Pause();
                    NewPage();
                    scenarios[i].setup();
                    yield return Frames(4);
                    AlignFrost();
                    yield return Frames(30);
                    var st = new Stat { name = scenarios[i].name, frames = new List<float>(), cpu = new List<float>(), gpu = new List<float>() };
                    yield return Measure(st, 240);
                    results[i].Add((Median(st.frames), P(st.frames, 0.95f), Median(st.cpu), Median(st.gpu)));
                }
            }
            StopBackdrop();
            NewPage();
            QualitySettings.vSyncCount = vs;
            Application.targetFrameRate = fps;
            var sb = new StringBuilder();
            sb.AppendLine("scenario\tframe ms median (3 rounds)\tp95\tcpu\tgpu");
            for (int i = 0; i < scenarios.Length; i++)
            {
                var r = results[i];
                sb.AppendLine($"{scenarios[i].name}\t{string.Join(" ", r.Select(x => x.med.ToString("0.00")))}\t{r.Average(x => x.p95):0.00}\t{r.Average(x => x.cpu):0.00}\t{r.Average(x => x.gpu):0.00}");
            }
            File.WriteAllText(Path.GetDirectoryName(Log) + "/ui-limits-cost.tsv", sb.ToString());
            Note("cost\tdone");
            AssertNoErrors("cost");
        }

        void FrostSheet(RenderTexture rt, float blur)
        {
            var e = Frost(page, rt, blur, "A frosted sheet at 30 % height");
            e.AddToClassList("c-sheet");
            e.style.height = Length.Percent(30f);
            e.style.position = Position.Absolute;
        }
    }
}
