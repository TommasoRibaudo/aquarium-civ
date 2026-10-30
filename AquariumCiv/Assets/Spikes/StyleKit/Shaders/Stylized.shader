// Style-kit spike master material (docs/research/07-art-direction.md §4.1). One shader for every solid thing in the box.
// Albedo = vertex colour (the palette lives in the meshes), vertex alpha = baked AO, UV0.x = wind sway, UV0.y = emission.
// Lighting: wrapped diffuse into a soft two-band ramp, shadows tinted cool instead of black, ambient from SH, a height
// gradient (cooler low, warmer high) to fake bounce light, and a faint rim. No textures, no specular: cheap on phones.
Shader "AquariumCiv/Spike/Stylized"
{
    Properties
    {
        _ShadowTint ("Shadow tint", Color) = (0.55, 0.6, 0.78, 1)
        _Wrap ("Light wrap", Range(0, 1)) = 0.45
        _BandAt ("Band position", Range(0, 1)) = 0.42
        _BandSoft ("Band softness", Range(0.01, 0.5)) = 0.16
        _Ambient ("Ambient strength", Range(0, 2)) = 0.75
        _AO ("AO strength", Range(0, 1)) = 0.9
        _LowTint ("Low tint", Color) = (0.9, 0.93, 1.02, 1)
        _HighTint ("High tint", Color) = (1.05, 1.0, 0.94, 1)
        _GroundY ("Gradient start (world y)", Float) = 0.6
        _HeightRange ("Gradient height", Float) = 1.6
        _Rim ("Rim", Range(0, 1)) = 0.18
        _Emission ("Emission boost", Float) = 3
        _Wind ("Wind strength", Float) = 0.018
        _WindSpeed ("Wind speed", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ShadowTint;
            half _Wrap, _BandAt, _BandSoft, _Ambient, _AO;
            half4 _LowTint, _HighTint;
            float _GroundY, _HeightRange;
            half _Rim, _Emission;
            float _Wind, _WindSpeed;
        CBUFFER_END

        float3 Sway(float3 positionWS, float weight)
        {
            float t = _Time.y * _WindSpeed;
            float2 w = float2(sin(t + positionWS.x * 1.7 + positionWS.z * 0.9), cos(t * 0.83 + positionWS.z * 1.3 + positionWS.x * 0.4));
            positionWS.xz += w * _Wind * weight;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = Sway(TransformObjectToWorld(v.positionOS.xyz), v.uv.x);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = i.color.rgb;
                half ao = lerp(1, i.color.a, _AO);
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(n, light.direction);
                half wrapped = saturate((ndl + _Wrap) / (1 + _Wrap));
                half band = smoothstep(_BandAt - _BandSoft, _BandAt + _BandSoft, wrapped);
                half lit = band * lerp(1, light.shadowAttenuation, 0.85);
                half3 direct = lerp(_ShadowTint.rgb * 0.55, light.color, lit);
                half3 ambient = SampleSH(n) * _Ambient;
                half3 col = albedo * (direct * (0.55 + 0.45 * ao) + ambient * ao);
                half h = saturate((i.positionWS.y - _GroundY) / _HeightRange);
                col *= lerp(_LowTint.rgb, _HighTint.rgb, h);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1 - saturate(dot(n, viewDir)), 3) * _Rim * (0.3 + 0.7 * lit);
                col += rim * light.color * (albedo + 0.25);
                col = lerp(col, albedo * _Emission, i.uv.y);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = Sway(TransformObjectToWorld(v.positionOS.xyz), v.uv.x);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - p);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(p, n, lightDir));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(Sway(TransformObjectToWorld(v.positionOS.xyz), v.uv.x));
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}
