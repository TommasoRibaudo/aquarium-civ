// Soft additive glows, camera-facing (spike polish note: "the fire is too small to glow"). One mesh holds every glow in
// the box: each is a quad whose four corners share the glow's centre (POSITION) and carry the corner in UV0 (-1..1),
// UV1 = (radius, kind), UV2 = (phase, period). Colour = vertex colour. Kinds:
//   0   a fire's halo: a ring (zero inside the flame), pushed BEHIND the flame, breathing with the flicker's phase (UV2.x)
//   1+  a lamp (UV1.y = 1 + phase): follows the global _AC_LampLevel with the same per-lamp threshold as the Stylized shader
//   3   an ember: rises 0.35 u over UV2.y seconds, driven by frac(t / period + phase), drifts sideways, shrinks to nothing
//   4   a fire's warm pool: a flat disc on the ground, flickering in sync with the flame (UV2.x = phase)
//   5   a billboard flame (#65): a teardrop drawn in the fragment stage on a quad standing on its foot (POSITION), half
//       width UV1.x, UV2 = (phase, height), turned to the camera round the vertical; three flat bands and soft tips
//   6   a spark (#65): rises fast and slows, stretched along its motion, cools from gold to red; UV2 = (phase, period)
//   7   a fire's warm wash (#65): a disc pulled TOWARDS the camera, so it lands on the people and logs near the fire; UV2.y
//       = its height above the ground (fades there), stronger at night
// Kind 4's UV2.y is an extra strength (0 = plain): full at night, 40 % by day.
// Premultiplied blend: the glows return alpha 0, so they stay purely additive; flames and sparks return some alpha, so
// they hold up against a bright day.
// The flicker n() is the Stylized shader's FlameNoise: the same two sines at 7.3 and 11.9 Hz. SlowFlicker is the calmer
// one kinds 5 to 7 use (and a boosted pool): big bright things must not strobe (practices section 12).
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
        _FlameCore ("Flame core", Color) = (1, 0.68, 0.2, 1)
        _FlameMid ("Flame body", Color) = (1, 0.48, 0.12, 1)
        _FlameEdge ("Flame edge", Color) = (0.9, 0.28, 0.12, 1)
        _FlameBoost ("Flame brightness", Float) = 1.1
        _FlameOpacity ("Flame opacity", Range(0, 1)) = 0.92
        _FlameSoft ("Flame edge softness", Range(0.01, 0.6)) = 0.18
        _SparkIntensity ("Spark intensity", Float) = 1.6
        _SparkOpacity ("Spark opacity", Range(0, 1)) = 0.8
        _SparkRise ("Spark rise (u)", Float) = 0.5
        _WashIntensity ("Warm wash intensity", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity, _Breath, _HaloIntensity, _HaloHole, _HaloCore, _PoolIntensity, _EmberIntensity;
                half4 _FlameCore, _FlameMid, _FlameEdge;
                half _FlameBoost, _FlameOpacity, _FlameSoft, _SparkIntensity, _SparkOpacity, _WashIntensity;
                float _SparkRise;
            CBUFFER_END
            float _AC_LampLevel;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 corner : TEXCOORD0; float2 data : TEXCOORD1; float2 extra : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half4 color : COLOR; half kind : TEXCOORD1; half above : TEXCOORD2; half3 flame : TEXCOORD3; };

            float Hash11(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            float FlameNoise(float phase)
            {
                float t = _Time.y;
                return 0.5 * (sin(6.2832 * 7.3 * t + phase * 6.2832) + sin(6.2832 * 11.9 * t + phase * 17.0));
            }

            // Under 4 Hz, for what is big or bright on screen. -1..1.
            float SlowFlicker(float phase)
            {
                float t = _Time.y;
                return 0.5 * (sin(6.2832 * 1.7 * t + phase * 6.2832) + sin(6.2832 * 3.1 * t + phase * 17.0));
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centreWS = TransformObjectToWorld(v.positionOS.xyz);
                float radius = v.data.x;
                float kind = v.data.y;
                float strength = 1.0;
                float opaque = 0.0;
                half3 tint = v.color.rgb;
                float3 posWS;
                o.above = 1;
                o.flame = half3(0, 0, 0);
                o.kind = kind < 0.5 ? 0 : kind < 2.0 ? 1 : kind < 3.5 ? 3 : kind < 4.5 ? 4 : kind < 5.5 ? 5 : kind < 6.5 ? 6 : 7;
                if (o.kind == 5)
                {
                    // Billboard flame: stands on its foot, leans a little to the camera's up so a steep view doesn't flatten it.
                    float phase = v.extra.x, t = _Time.y;
                    float3 up = normalize(float3(0, 1, 0) + UNITY_MATRIX_V[1].xyz);
                    float tall = v.extra.y * (1.0 + 0.14 * SlowFlicker(phase));
                    posWS = centreWS + UNITY_MATRIX_V[0].xyz * (v.corner.x * radius) + up * ((v.corner.y * 0.5 + 0.5) * tall);
                    o.positionCS = TransformWorldToHClip(posWS);
                    // x: the whole flame leaning; y: how far the licks bend it; z: the licks' phase, running up it.
                    o.flame = half3(0.16 * sin(t * 2.3 + phase * 6.2832), 0.14, frac(t * 1.6 + phase) * 6.2832);
                    opaque = _FlameOpacity;
                }
                else if (o.kind == 6)
                {
                    // Spark: quick off the fire, slowing; thin and long while it is fast.
                    float phase = v.extra.x, period = max(0.1, v.extra.y);
                    float u = frac(_Time.y / period + phase);
                    float a = u * 6.2832 * 1.25 + phase * 40.0;
                    float3 off = float3(0.07 * u * sin(a), _SparkRise * u * (2.0 - u), 0.07 * u * cos(a * 0.8 + 1.0));
                    off.xz += (float2(Hash11(phase * 91.0), Hash11(phase * 53.0)) - 0.5) * 0.08;
                    float size = radius * saturate(u * 12.0) * (1.0 - u * u);
                    float3 c = TransformWorldToView(centreWS + off);
                    c.xy += v.corner * size * float2(1.0, 1.0 + 1.5 * (1.0 - u));
                    o.positionCS = TransformWViewToHClip(c);
                    strength = _SparkIntensity;
                    tint *= lerp(half3(1.2, 0.8, 0.4), half3(1.0, 0.3, 0.15), (half)u);
                    opaque = _SparkOpacity;
                }
                else if (o.kind == 4)
                {
                    // Warm pool: lies flat on the ground and flickers a little with the flame.
                    if (v.extra.y > 0.0)
                        strength = _PoolIntensity * (1.0 + 0.2 * SlowFlicker(v.extra.x)) * (1.0 + v.extra.y * lerp(0.4, 1.0, saturate(_AC_LampLevel)));
                    else
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
                    else if (o.kind == 7)
                    {
                        // Warm wash: in front of what stands near the fire, fading where it meets the ground like the halo.
                        strength = _WashIntensity * lerp(0.3, 1.0, saturate(_AC_LampLevel)) * (1.0 + 0.2 * SlowFlicker(v.extra.x));
                        centreVS.z += radius * 0.8;
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
                o.color = half4(tint * strength, opaque);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (i.kind > 4.5 && i.kind < 5.5)
                {
                    // A teardrop: h runs foot to tip; wide is 0 at the foot, widest a quarter up, tapering to a point.
                    half h = saturate(i.corner.y * 0.5 + 0.5);
                    half bend = i.flame.x * h * h + i.flame.y * h * sin(h * 7.0 - i.flame.z);
                    half t = sqrt(h);
                    half wide = 3.2 * t * (1.0 - t);
                    half heat = saturate(1.0 - abs(i.corner.x - bend) / max(wide, 0.02));
                    half band = heat * (1.0 - 0.55 * h);
                    half3 ramp = lerp(_FlameEdge.rgb, _FlameMid.rgb, smoothstep(0.1, 0.16, band));
                    ramp = lerp(ramp, _FlameCore.rgb, smoothstep(0.42, 0.48, band));
                    half a = smoothstep(0.0, _FlameSoft, heat) * (1.0 - smoothstep(0.72, 1.0, h));
                    return half4(ramp * i.color.rgb * _FlameBoost * a, a * i.color.a);
                }
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
                else if ((i.kind > 2.5 && i.kind < 3.5) || (i.kind > 5.5 && i.kind < 6.5))
                {
                    half r = sqrt(r2);
                    fall = saturate((1 - r) * 2.5); // a small hard-edged spark
                }
                else if (i.kind > 6.5)
                    fall *= smoothstep(0.0, 0.12, i.above);
                return half4(i.color.rgb * fall, i.color.a * fall);
            }
            ENDHLSL
        }
    }
}
