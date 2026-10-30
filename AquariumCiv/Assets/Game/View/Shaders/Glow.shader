// Soft additive glows, camera-facing (spike polish note: "the fire is too small to glow"). One mesh holds every glow in
// the box: each is a quad whose four corners share the glow's centre (POSITION) and carry the corner in UV0 (-1..1),
// UV1 = (radius, kind), UV2 = (phase, period). Colour = vertex colour. Kinds:
//   0   a fire's halo: a ring (zero inside the flame), pushed BEHIND the flame, breathing with the flicker's phase (UV2.x)
//   1+  a lamp (UV1.y = 1 + phase): follows the global _AC_LampLevel with the same per-lamp threshold as the Stylized shader
//   3   an ember: rises 0.35 u over UV2.y seconds, driven by frac(t / period + phase), drifts sideways, shrinks to nothing
//   4   a fire's warm pool: a flat disc on the ground, flickering in sync with the flame (UV2.x = phase)
// The flicker n() is the Stylized shader's FlameNoise: the same two sines at 7.3 and 11.9 Hz.
Shader "AquariumCiv/View/Glow"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 0.55
        _Breath ("Fire breathing (unused)", Range(0, 1)) = 0.25
        _HaloIntensity ("Fire halo intensity", Float) = 0.4
        _HaloHole ("Halo core size (fraction of its radius)", Range(0, 0.6)) = 0.2
        _HaloCore ("Halo strength inside the core (0 = a hole)", Range(0, 1)) = 0.55
        _PoolIntensity ("Warm pool intensity", Float) = 0.12
        _EmberIntensity ("Ember intensity", Float) = 0.8
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
                half _Intensity, _Breath, _HaloIntensity, _HaloHole, _HaloCore, _PoolIntensity, _EmberIntensity;
            CBUFFER_END
            float _AC_LampLevel;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 corner : TEXCOORD0; float2 data : TEXCOORD1; float2 extra : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half4 color : COLOR; half kind : TEXCOORD1; half above : TEXCOORD2; };

            float Hash11(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            float FlameNoise(float phase)
            {
                float t = _Time.y;
                return 0.5 * (sin(6.2832 * 7.3 * t + phase * 6.2832) + sin(6.2832 * 11.9 * t + phase * 17.0));
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centreWS = TransformObjectToWorld(v.positionOS.xyz);
                float radius = v.data.x;
                float kind = v.data.y;
                float strength = 1.0;
                float3 posWS;
                o.above = 1;
                o.kind = kind < 0.5 ? 0 : kind < 2.0 ? 1 : kind < 3.5 ? 3 : 4;
                if (o.kind == 4)
                {
                    // Warm pool: lies flat on the ground and flickers a little with the flame.
                    strength = _PoolIntensity * (1.0 + 0.2 * FlameNoise(v.extra.x));
                    posWS = centreWS + float3(v.corner.x, 0, v.corner.y) * radius;
                    o.positionCS = TransformWorldToHClip(posWS);
                }
                else if (o.kind == 3)
                {
                    // Ember: a spark rising from the flame, each on its own period and phase.
                    float phase = v.extra.x, period = max(0.1, v.extra.y);
                    float u = frac(_Time.y / period + phase);
                    float a = u * 6.2832 * 1.5 + phase * 40.0;
                    float3 off = float3(0.03 * sin(a), 0.35 * u, 0.03 * cos(a * 0.8 + 1.0));
                    off.xz += (float2(Hash11(phase * 91.0), Hash11(phase * 53.0)) - 0.5) * 0.06;
                    float size = radius * (1.0 - u) * saturate(u * 10.0);
                    float3 c = TransformWorldToView(centreWS + off);
                    c.xy += v.corner * size;
                    o.positionCS = TransformWViewToHClip(c);
                    strength = _EmberIntensity;
                }
                else
                {
                    float3 centreVS = TransformWorldToView(centreWS);
                    if (o.kind == 0)
                    {
                        // A fire's halo: behind the flame, breathing with its flicker.
                        strength = _HaloIntensity * (1.0 + 0.2 * FlameNoise(v.extra.x));
                        centreVS.z -= radius * 0.3;
                        // How far above the ground (the fire's foot, v.extra.y below the centre) this corner of the quad is, so the
                        // part of the quad that dips into the terrain fades out instead of ending in a hard line.
                        float3 corner3 = centreVS; corner3.xy += v.corner * radius;
                        o.above = TransformViewToWorld(corner3).y - (centreWS.y - v.extra.y);
                    }
                    else
                    {
                        float phase = kind - 1.0;
                        float t0 = 0.12 + phase * 0.6;
                        float x = saturate((_AC_LampLevel - t0) / 0.14);
                        float n = Hash11(floor(_Time.y * 13.0) + phase * 917.0);
                        strength = _Intensity * ((x > 0.0 && x < 1.0) ? step(1.0 - x * x, n) * x : x);
                        // Pull the quad a little towards the camera so it isn't cut by the ground it sits on.
                        centreVS.z += radius * 0.6;
                    }
                    centreVS.xy += v.corner * radius;
                    o.positionCS = TransformWViewToHClip(centreVS);
                }
                o.corner = v.corner;
                o.color = half4(v.color.rgb * strength, 1);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half r2 = saturate(dot(i.corner, i.corner));
                half fall = 1 - r2;
                fall *= fall;
                if (i.kind < 0.5)
                {
                    // Softer inside the flame's own radius (it sits behind the flame, which hides it there anyway) and
                    // fading out where the quad meets the ground.
                    half r = sqrt(r2);
                    fall *= lerp(_HaloCore, 1, smoothstep(_HaloHole * 0.6, _HaloHole * 1.7, r));
                    fall *= smoothstep(0.0, 0.12, i.above);
                }
                else if (i.kind > 2.5 && i.kind < 3.5)
                {
                    half r = sqrt(r2);
                    fall = saturate((1 - r) * 2.5); // a small hard-edged spark
                }
                return half4(i.color.rgb * fall, 0);
            }
            ENDHLSL
        }
    }
}
