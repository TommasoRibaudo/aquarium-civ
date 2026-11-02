using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// A small copy of the camera's frame for a frosted surface to show (#178, docs/design/ui/limits.md). It is a render-graph
    /// pass handed to whichever renderer the main camera uses, not a feature on the renderer assets: while nothing asked for
    /// a copy there is no callback, no pass, no texture, and the assets are untouched. Phones get Mobile_Renderer this way
    /// and the Editor gets PC_Renderer, with nothing to keep in step.
    /// </summary>
    public static class FrostBackdrop
    {
        static CopyPass pass;
        static RTHandle handle;

        public static bool Active => pass != null;
        /// <summary>The copy, or null while off. Holds the frame without any UI, so a sheet never sees itself.</summary>
        public static RenderTexture Target { get; private set; }
        /// <summary>Every copy recorded since launch. Stands still while off, which is how a test sees that there is no pass.</summary>
        public static long CopiesRecorded { get; private set; }

        /// <summary>Starts copying at 1/<paramref name="divisor"/> of the camera's size and returns the texture to show.</summary>
        public static RenderTexture Start(int divisor)
        {
            Stop();
            var cam = Camera.main;
            int w = cam != null ? cam.pixelWidth : Screen.width, h = cam != null ? cam.pixelHeight : Screen.height;
            Target = new RenderTexture(Mathf.Max(1, w / divisor), Mathf.Max(1, h / divisor), 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "FrostBackdrop",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Target.Create();
            handle = RTHandles.Alloc(Target);
            pass = new CopyPass(handle, divisor > 4);
            RenderPipelineManager.beginCameraRendering += OnCamera;
            return Target;
        }

        public static void Stop()
        {
            if (pass == null) return;
            RenderPipelineManager.beginCameraRendering -= OnCamera;
            pass = null;
            handle.Release();
            handle = null;
            Target.Release();
            Object.Destroy(Target);
            Target = null;
        }

        static void OnCamera(ScriptableRenderContext context, Camera cam)
        {
            if (pass != null && cam == Camera.main) cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }

        sealed class CopyPass : ScriptableRenderPass
        {
            const string Name = "Frost backdrop";
            readonly RTHandle target;
            readonly bool viaQuarter;

            /// <param name="viaQuarter">Halve once on the way down: a direct 1/8 bilinear skips pixels and the glass edges step (limits.md).</param>
            public CopyPass(RTHandle target, bool viaQuarter)
            {
                this.target = target;
                this.viaQuarter = viaQuarter;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var destination = graph.ImportTexture(target);
                if (viaQuarter)
                {
                    var desc = graph.GetTextureDesc(destination);
                    desc.width *= 2;
                    desc.height *= 2;
                    desc.name = "FrostBackdropQuarter";
                    desc.clearBuffer = false;
                    var quarter = graph.CreateTexture(desc);
                    graph.AddBlitPass(source, quarter, Vector2.one, Vector2.zero, passName: Name);
                    source = quarter;
                }
                graph.AddBlitPass(source, destination, Vector2.one, Vector2.zero, passName: Name);
                CopiesRecorded++;
            }
        }
    }
}
