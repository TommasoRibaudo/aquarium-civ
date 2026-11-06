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
        [HideInInspector] _SrcBlend ("Src blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst blend", Float) = 0
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

        // Globals (not per material): the night level every lamp in the box follows; the day's extra ambient strength (added to
        // _Ambient, 0 by day and in edit mode); the lamps that light their neighbours, nearest the view first (xyz, w = how lit,
        // DayLightDriver), with _AC_LampParams = (1 / reach^2, strength, flicker held, pool size); how far each part of the box's
        // frame has faded (#162; the frame's vertices say which part in UV1.x = -1 - part).
        float _AC_LampLevel;
        float _AC_AmbientBoost;
        float4 _AC_LampPos[8];
        float4 _AC_LampParams;
        float _AC_FrameFade[8];

        // #357 hit answers: the shake centres of the bushes and trees baked into the land (HitFx fills them; _AC_ShakeCount is 0 when none,
        // and then the vertex shader skips all of it). A: x, z, age in s (< 0 waits), tan of the sway angle. B: Hz, tau s, reach u, base y.
        // C: direction x, z (away from whoever struck). Only vertices with a sway weight (leaves, grass) move.
        float _AC_ShakeCount;
        float4 _AC_ShakeA[16];
        float4 _AC_ShakeB[16];
        float4 _AC_ShakeC[16];

        float Hash11(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

        // A light's slow breathing, 0.89..1: two sines a few seconds long (0.31 and 0.53 Hz), each lamp on its own phase. A fire
        // burns unevenly, but the walls, people and things round it bounce the light and smooth that out to a slow rise and fall.
        float Breath(float seed)
        {
            float t = _Time.y;
            return 1.0 - 0.07 * (0.5 + 0.5 * sin(6.2832 * 0.31 * t + seed * 6.2832)) - 0.04 * (0.5 + 0.5 * sin(6.2832 * 0.53 * t + seed * 17.0));
        }

        // How lit a lamp is (0..1). Each lamp has a threshold in the dusk set by its phase; it fades on smoothly as the dusk
        // crosses it, then breathes. No flicker (#214).
        half LampOn(float2 lamp)
        {
            if (lamp.x < 0.5 || lamp.x > 1.5) return 0; // 2 and up is a flame, not a lamp
            // Soft glow (the Keeper's tree): phase 2 + strength. No dusk threshold per lamp; it rises smoothly with the night level
            // and breathes slowly, and its strength is the share of the warm lit colour mixed in (small = barely there).
            if (lamp.y > 1.5) return (half)((lamp.y - 2.0) * smoothstep(0.15, 0.85, _AC_LampLevel) * (_AC_LampParams.z > 0.5 ? 1.0 : Breath(0.37)));
            float t0 = 0.12 + lamp.y * 0.6;
            float x = saturate((_AC_LampLevel - t0) / 0.14);
            if (_AC_LampParams.z > 0.5) return (half)x; // the clock runs fast (time-lapse): no breathing either
            return (half)(smoothstep(0.0, 1.0, x) * Breath(lamp.y));
        }

        // The lamps' light on what stands near them, per vertex: at most 8 loops of a few instructions, no URP additional light, no
        // new variant. The soft end of (1 - d^2 / reach^2)^2 needs no square root.
        half LampLight(float3 p, float3 n)
        {
            if (_AC_LampParams.y <= 0.0) return 0;
            half sum = 0;
            for (int i = 0; i < 8; i++)
            {
                float3 d = _AC_LampPos[i].xyz - p;
                float k = saturate(1.0 - dot(d, d) * _AC_LampParams.x);
                sum += (half)(k * k * _AC_LampPos[i].w * (0.65 + 0.35 * saturate(dot(n, d) * 2.0)));
            }
            return sum * (half)_AC_LampParams.y;
        }

        float3 Sway(float3 positionWS, float weight)
        {
            float t = _Time.y * _WindSpeed;
            float2 w = float2(sin(t + positionWS.x * 1.7 + positionWS.z * 0.9), cos(t * 0.83 + positionWS.z * 1.3 + positionWS.x * 0.4));
            positionWS.xz += w * _Wind * weight;
            return positionWS;
        }

        // A damped sway about the base, away from the stroke: a vertex height h above the base moves h * tan(angle) * wobble, fading with the
        // distance from the centre (exp(-(d/reach)^2)). Same maths as ShakeMath.SwayOffset.
        float3 Shake(float3 p, float weight)
        {
            if (weight < 0.01 || _AC_ShakeCount < 0.5) return p;
            int n = (int)_AC_ShakeCount;
            [loop] for (int i = 0; i < n; i++)
            {
                float4 a = _AC_ShakeA[i];
                if (a.z < 0.0) continue;
                float4 b = _AC_ShakeB[i];
                float2 d = p.xz - a.xy;
                float k = exp(-dot(d, d) / (b.z * b.z));
                float s = a.w * exp(-a.z / b.y) * sin(6.2832 * b.x * a.z);
                p.xz += _AC_ShakeC[i].xy * (s * max(p.y - b.w, 0.0) * k);
            }
            return p;
        }

        // Flame flicker (the fire pit): UV1.x = 2 + height weight (0 at the flame's foot, 1 at its tip), UV1.y = the fire's
        // phase. Stretch +-6 % and wobble sideways 0.004 u, both scaled by the weight; two slow sines at 0.37 and 0.61 Hz (periods of about 2 s and 3 s: a fire breathes, it doesn't jitter: #214).
        // The glow shader's halo and ground pool use the same n(), so they breathe with the flames.
        float FlameNoise(float phase)
        {
            float t = _Time.y;
            return 0.5 * (sin(6.2832 * 0.37 * t + phase * 6.2832) + sin(6.2832 * 0.61 * t + phase * 17.0));
        }

        float3 Displace(float3 positionWS, float swayWeight, float2 lamp)
        {
            if (lamp.x > 1.5)
            {
                float w = saturate(lamp.x - 2.0);
                float n = FlameNoise(lamp.y);
                float ph = lamp.y * 6.2832 + positionWS.x * 23.0 + positionWS.z * 17.0;
                float t = _Time.y;
                positionWS.y += w * 0.3 * 0.06 * n; // height weight x flame height x stretch
                positionWS.xz += 0.004 * w * float2(sin(t * 0.8 + ph), cos(t * 0.6 + ph * 1.3));
                return positionWS;
            }
            return Shake(Sway(positionWS, swayWeight), swayWeight);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            Blend [_SrcBlend] [_DstBlend]

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
                float4 uv : TEXCOORD2; // x sway, y emission, z lamp on, w frame opacity
                half2 fog : TEXCOORD3; // x fog, y lamp light
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = Displace(TransformObjectToWorld(v.positionOS.xyz), v.uv.x, v.lamp);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                float opacity = v.lamp.x < -0.5 ? 1.0 - _AC_FrameFade[(int)(-v.lamp.x - 0.5)] : 1.0;
                o.uv = float4(v.uv, LampOn(v.lamp), opacity);
                o.fog = half2(ComputeFogFactor(o.positionCS.z), LampLight(p, o.normalWS));
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
                half3 ambient = SampleSH(n) * (_Ambient + _AC_AmbientBoost);
                half3 col = albedo * (direct * (0.55 + 0.45 * ao) + ambient * ao + half3(1.0, 0.78, 0.45) * i.fog.y);
                half h = saturate((i.positionWS.y - _GroundY) / _HeightRange);
                col *= lerp(_LowTint.rgb, _HighTint.rgb, h);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1 - saturate(dot(n, viewDir)), 3) * _Rim * (0.3 + 0.7 * lit);
                col += rim * light.color * (albedo + 0.25);
                col = lerp(col, albedo * _Emission, i.uv.y);
                col = lerp(col, albedo * half3(1.0, 0.78, 0.45) * _LampBoost, i.uv.z); // warm, so lit windows stay amber after tonemapping
                col = MixFog(col, i.fog.x);
                return half4(col, i.uv.w);
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
