// Style-kit spike: glass v2 = the prototypes' GlassGrime (docs/research/07-art-direction.md §4.3) plus a tinted
// thickness edge, two soft reflection streaks and a faint condensation band at the bottom, so the panes read as glass
// in a still frame instead of as a wireframe. Still one material per pane, no screen-copy refraction.
// UVs come from object space (the pane is a scaled unit cube), so both faces of a pane share the same texel for
// the same physical spot, and C# can map a raycast hit to the mask with InverseTransformPoint.
Shader "AquariumCiv/Spike/GlassV2"
{
    Properties
    {
        _MaskTex ("Grime mask (R)", 2D) = "black" {}
        _NoiseTex ("Grime detail", 2D) = "white" {}
        _NoiseDensity ("Detail tiles per unit", Float) = 0.6
        _Axis ("UV axis (0 = XY pane, 1 = ZY pane)", Float) = 0
        _PaneSize ("Pane size (u, v) in world units", Vector) = (1, 1, 0, 0)
        _CleanColor ("Clean glass", Color) = (0.85, 0.95, 1, 0.10)
        _GrimeColor ("Grime", Color) = (0.42, 0.47, 0.30, 1)
        _DirtyAlpha ("Grime opacity at full", Range(0, 1)) = 0.8
        _FresnelStrength ("Rim sheen", Range(0, 1)) = 0.25
        _Glint ("Fresh-clean glint", Range(0, 1)) = 0
        _EdgeColor ("Edge tint", Color) = (0.62, 0.8, 0.78, 0.5)
        _EdgeWidth ("Edge width (world)", Float) = 0.07
        _StreakAlpha ("Reflection streaks", Range(0, 1)) = 0.16
        _StreakOffset ("Streak offset", Float) = 0
        _Condensation ("Condensation", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                float4 _NoiseTex_ST;
                float _NoiseDensity;
                float _Axis;
                float4 _PaneSize;
                half4 _CleanColor;
                half4 _GrimeColor;
                half _DirtyAlpha;
                half _FresnelStrength;
                half _Glint;
                half4 _EdgeColor;
                float _EdgeWidth;
                half _StreakAlpha;
                float _StreakOffset;
                half _Condensation;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                float2 xy = v.positionOS.xy, zy = v.positionOS.zy;
                o.uv = lerp(xy, zy, step(0.5, _Axis)) + 0.5;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv).r;
                half4 n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv * _PaneSize.xy * _NoiseDensity);

                // Detail noise breaks the mask into smudges and drip streaks; heavier grime fills in the gaps.
                half cover = saturate(mask * (0.55 + 0.9 * n.a) - (1 - mask) * 0.15);
                cover = smoothstep(0.02, 0.9, cover);

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 normal = normalize(i.normalWS);
                half ndv = saturate(abs(dot(normal, viewDir)));
                half fresnel = pow(1 - ndv, 4) * _FresnelStrength;

                Light light = GetMainLight();
                half lambert = saturate(dot(normal, light.direction)) * 0.35 + 0.65;
                half3 grime = _GrimeColor.rgb * (0.85 + 0.3 * n.r) * light.color * lambert;

                half3 rgb = lerp(_CleanColor.rgb, grime, cover);
                half alpha = lerp(_CleanColor.a, _DirtyAlpha, cover);
                // Clean glass gets a faint sheen and, right after a wipe, a sparkle that fades.
                half sheen = fresnel * (1 - cover) + _Glint * (1 - cover) * 0.35;
                rgb = lerp(rgb, 1, saturate(sheen * 2));
                alpha = saturate(alpha + sheen);

                // Thickness edge: real glass shows its green-blue body where you look along it.
                float2 w = i.uv * _PaneSize.xy;
                float edgeDist = min(min(w.x, _PaneSize.x - w.x), min(w.y, _PaneSize.y - w.y));
                half edge = 1 - smoothstep(0, _EdgeWidth, edgeDist);
                rgb = lerp(rgb, _EdgeColor.rgb, edge * 0.8);
                alpha = max(alpha, edge * _EdgeColor.a);

                // Two soft diagonal streaks, as if a window across the room were reflected; stronger at grazing angles.
                float d = (w.x + w.y * 0.55) * 0.45 + _StreakOffset;
                half s1 = smoothstep(0.0, 0.12, frac(d)) * (1 - smoothstep(0.12, 0.3, frac(d)));
                half s2 = smoothstep(0.0, 0.03, frac(d + 0.42)) * (1 - smoothstep(0.03, 0.08, frac(d + 0.42)));
                // Only on the upper half of the pane, fading out toward the bottom.
                half streak = (s1 + s2 * 0.6) * _StreakAlpha * (0.35 + fresnel * 3) * (1 - cover) * smoothstep(0.9, 2.2, w.y);
                rgb = lerp(rgb, 1, saturate(streak * 2));
                alpha = saturate(alpha + streak);

                // Condensation: a faint misty band just above the soil line (the ground sits at about 1 unit).
                half mist = smoothstep(0.8, 1.0, w.y) * (1 - smoothstep(1.05, 1.6, w.y)) * _Condensation * (0.6 + 0.4 * n.g) * (1 - cover);
                rgb = lerp(rgb, half3(0.92, 0.96, 0.97), mist * 0.6);
                alpha = saturate(alpha + mist * 0.25);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
