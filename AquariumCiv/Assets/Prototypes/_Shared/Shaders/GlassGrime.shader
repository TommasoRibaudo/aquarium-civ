// Terrarium glass with a paintable grime mask (P1). One material per pane.
// UVs come from object space (the pane is a scaled unit cube), so both faces of a pane share the same texel for
// the same physical spot, and C# can map a raycast hit to the mask with InverseTransformPoint.
Shader "AquariumCiv/GlassGrime"
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
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
