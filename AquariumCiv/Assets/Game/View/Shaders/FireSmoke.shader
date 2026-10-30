// Camp-fire smoke: a few soft puffs per fire in ONE small alpha-blended mesh (one extra draw for every fire in the box).
// Each puff is a quad whose four corners share the puff's start (POSITION, the top of the flame) and carry the corner in
// UV0 (-1..1), UV1 = (phase, period). It rises 0.6 u over its period (4 s), grows 0.05 -> 0.14 u across and fades
// 0.35 -> 0, all from frac(t / period + phase): no CPU cost. Fades out with the night level (_AC_LampLevel), when the
// firelight, not the smoke, is what you see.
Shader "AquariumCiv/View/FireSmoke"
{
    Properties
    {
        _Alpha ("Peak alpha", Range(0, 1)) = 0.35
        _Rise ("Rise (u)", Float) = 0.6
        _SizeFrom ("Size at start (u)", Float) = 0.05
        _SizeTo ("Size at end (u)", Float) = 0.14
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+4" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Alpha;
                float _Rise, _SizeFrom, _SizeTo;
            CBUFFER_END
            float _AC_LampLevel;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 corner : TEXCOORD0; float2 data : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float phase = v.data.x, period = max(0.5, v.data.y);
                float u = frac(_Time.y / period + phase);
                float3 c = TransformObjectToWorld(v.positionOS.xyz);
                // Rises and drifts a little with the wind.
                c.y += _Rise * u;
                c.x += 0.05 * u * sin(phase * 31.0 + u * 2.0);
                c.z += 0.04 * u * cos(phase * 17.0 + u * 1.7);
                float size = lerp(_SizeFrom, _SizeTo, u);
                float3 vs = TransformWorldToView(c);
                vs.xy += v.corner * size;
                o.positionCS = TransformWViewToHClip(vs);
                o.corner = v.corner;
                float a = _Alpha * (1.0 - u) * saturate(u * 6.0) * (1.0 - saturate(_AC_LampLevel));
                o.color = half4(v.color.rgb, a);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half r2 = saturate(dot(i.corner, i.corner));
                half soft = 1 - r2;
                soft *= soft;
                return half4(i.color.rgb, i.color.a * soft);
            }
            ENDHLSL
        }
    }
}
