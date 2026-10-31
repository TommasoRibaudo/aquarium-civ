// The box's master material (ported from the style-kit spike, docs/design/spikes/style-kit.md; research 07 §4.1).
// One shader for every solid thing in the box.
// Albedo = vertex colour (the palette lives in the meshes), vertex alpha = baked AO, UV0.x = wind sway, UV0.y = emission,
// UV1 = (lamp flag, lamp phase): lamps (windows, lanterns) light with the global _AC_LampLevel (0 = day, 1 = night,
// set by NightLamps), each at its own moment in the dusk, flickering as it comes on and, rarely, once it is on.
// Lighting: wrapped diffuse into a soft two-band ramp, shadows tinted cool instead of black, ambient from SH, a height
// gradient (cooler low, warmer high) to fake bounce light, and a faint rim. No textures, no specular: cheap on phones.
Shader "AquariumCiv/View/Stylized"
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
        _LampBoost ("Lamp brightness at night", Float) = 2.2
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
            half _LampBoost;
        CBUFFER_END

        // Global (not per material): the night level every lamp in the box follows.
        float _AC_LampLevel;

        float Hash11(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

        // How lit a lamp is (0..1). Each lamp has a threshold in the dusk set by its phase; while the level crosses it
        // the lamp sputters (random on/off, more often on as it warms); once on, it dips now and then for a moment.
        half LampOn(float2 lamp)
        {
            if (lamp.x < 0.5 || lamp.x > 1.5) return 0; // 2 and up is a flame, not a lamp
            float t0 = 0.12 + lamp.y * 0.6;
            float x = saturate((_AC_LampLevel - t0) / 0.14);
            float n = Hash11(floor(_Time.y * 13.0) + lamp.y * 917.0);
            float sputter = (x > 0.0 && x < 1.0) ? step(1.0 - x * x, n) * (0.55 + 0.45 * x) : x;
            float dip = step(0.988, Hash11(floor(_Time.y * 7.0) + lamp.y * 571.0 + 3.0)) * step(0.999, x);
            return (half)(sputter * (1.0 - 0.7 * dip));
        }

        float3 Sway(float3 positionWS, float weight)
        {
            float t = _Time.y * _WindSpeed;
            float2 w = float2(sin(t + positionWS.x * 1.7 + positionWS.z * 0.9), cos(t * 0.83 + positionWS.z * 1.3 + positionWS.x * 0.4));
            positionWS.xz += w * _Wind * weight;
            return positionWS;
        }

        // Flame flicker (the fire pit): UV1.x = 2 + height weight (0 at the flame's foot, 1 at its tip), UV1.y = the fire's
        // phase. Stretch +-18 % and wobble sideways 0.012 u, both scaled by the weight; two sines at 7.3 and 11.9 Hz.
        // The glow shader's halo and ground pool use the same n(), so they breathe with the flames.
        float FlameNoise(float phase)
        {
            float t = _Time.y;
            return 0.5 * (sin(6.2832 * 7.3 * t + phase * 6.2832) + sin(6.2832 * 11.9 * t + phase * 17.0));
        }

        float3 Displace(float3 positionWS, float swayWeight, float2 lamp)
        {
            if (lamp.x > 1.5)
            {
                float w = saturate(lamp.x - 2.0);
                float n = FlameNoise(lamp.y);
                float ph = lamp.y * 6.2832 + positionWS.x * 23.0 + positionWS.z * 17.0;
                float t = _Time.y;
                positionWS.y += w * 0.3 * 0.18 * n; // height weight x flame height x stretch
                positionWS.xz += 0.012 * w * float2(sin(t * 9.1 + ph), cos(t * 7.3 + ph * 1.3));
                return positionWS;
            }
            return Sway(positionWS, swayWeight);
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; float2 lamp : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float3 uv : TEXCOORD2; // x sway, y emission, z lamp on
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = Displace(TransformObjectToWorld(v.positionOS.xyz), v.uv.x, v.lamp);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.uv = float3(v.uv, LampOn(v.lamp));
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
                col = lerp(col, albedo * half3(1.0, 0.78, 0.45) * _LampBoost, i.uv.z); // warm, so lit windows stay amber after tonemapping
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 lamp : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = Displace(TransformObjectToWorld(v.positionOS.xyz), v.uv.x, v.lamp);
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
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 lamp : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(Displace(TransformObjectToWorld(v.positionOS.xyz), v.uv.x, v.lamp));
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}
