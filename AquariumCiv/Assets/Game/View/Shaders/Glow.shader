// Soft additive glows, camera-facing (spike polish note: "the fire is too small to glow"). One mesh holds every glow in
// the box: each is a quad whose four corners share the glow's centre (POSITION) and carry the corner in UV0 (-1..1),
// UV1 = (radius, kind). Kind 0 = a fire (always on, breathing), kind 1 = a lamp (follows the global _AC_LampLevel with
// the same per-lamp threshold as the Stylized shader; UV1 is (radius, 1 + phase)). Colour = vertex colour.
Shader "AquariumCiv/View/Glow"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 0.55
        _Breath ("Fire breathing", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity, _Breath;
            CBUFFER_END
            float _AC_LampLevel;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 corner : TEXCOORD0; float2 data : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half4 color : COLOR; };

            float Hash11(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centreVS = TransformWorldToView(TransformObjectToWorld(v.positionOS.xyz));
                float radius = v.data.x;
                float strength;
                if (v.data.y < 0.5)
                {
                    // Fire: a slow breath plus a quick flutter, different per fire (seeded by its position).
                    float seed = v.positionOS.x * 3.1 + v.positionOS.z * 1.7;
                    float t = _Time.y;
                    strength = 1.0 - _Breath * (0.5 + 0.5 * sin(t * 2.3 + seed)) * (0.6 + 0.4 * sin(t * 7.9 + seed * 2.0));
                }
                else
                {
                    float phase = v.data.y - 1.0;
                    float t0 = 0.12 + phase * 0.6;
                    float x = saturate((_AC_LampLevel - t0) / 0.14);
                    float n = Hash11(floor(_Time.y * 13.0) + phase * 917.0);
                    strength = (x > 0.0 && x < 1.0) ? step(1.0 - x * x, n) * x : x;
                }
                // Pull the quad a little towards the camera so it isn't cut by the ground it sits on.
                centreVS.z += radius * 0.6;
                centreVS.xy += v.corner * radius;
                o.positionCS = TransformWViewToHClip(centreVS);
                o.corner = v.corner;
                o.color = half4(v.color.rgb * strength * _Intensity, 1);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half r2 = saturate(dot(i.corner, i.corner));
                half fall = (1 - r2);
                fall *= fall;
                return half4(i.color.rgb * fall, 0);
            }
            ENDHLSL
        }
    }
}
