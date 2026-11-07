// The living land's wet look (P8 #266): one small shader, four uses picked by _Mode (one material each).
//   0 Wet    multiplies the ground it lies on by a darker, warmer soil: the land keeps its own light and shadow, only wetter
//   1 Sheen  adds a faint sky glint on the same mesh (a slight sheen on wet soil, strongest where it grazes the eye)
//   2 Puddle wet-blue/brown water: a sky reflection that grows at grazing angles and lies brightest on the far side, a sun
//            glint and a soft window highlight, a dark wet-mud rim with a thin bright meniscus, the odd ripple
//   3 Tint   a plain soft-edged colour (the green that shows at once under the mister, until the chunk has caught up)
//   4 Drop   a round, soft particle (the mister's droplets; stretched billboards make them streaks)
//   5 Fresh  the new grass since the last look (#281, #300): tints the ground it lies on (2x multiply, Blend DstColor SrcColor),
//            so it keeps the land's own light at every hour. Vertex colour = the hue (scaled so no channel brightens), vertex
//            alpha = how much; by day it also lifts a touch (fresh, lighter green), at night it never shows brighter than the land.
// Vertex colour = colour, vertex alpha = how much of it (Wet and Sheen boost it by _WetBoost: one short mist already reads
// as damp soil, #295). Puddle vertex colour rg = where in the puddle (0..1 across, 0.5 the middle). Puddle UV0 = (distance from the middle in cells, 0), UV1 = (ripple
// phase 0..1, ripple strength 0..1). No textures; the day's light comes from the main light and the ambient probe.
Shader "AquariumCiv/View/EcoGround"
{
    Properties
    {
        [Enum(Wet,0,Sheen,1,Puddle,2,Tint,3,Drop,4,Fresh,5)] _Mode ("Mode", Float) = 0
        _WetColor ("Wet soil multiplier", Color) = (0.46, 0.37, 0.31, 1)
        _WetBoost ("Wet look per unit of wet", Range(1, 3)) = 1.9
        _SheenAmount ("Sheen", Range(0, 1)) = 0.22
        _SkyHigh ("Sky overhead", Color) = (0.62, 0.74, 0.88, 1)
        _SkyLow ("Sky at the horizon", Color) = (0.82, 0.86, 0.88, 1)
        _PuddleDeep ("Puddle bed", Color) = (0.07, 0.055, 0.045, 1)
        _Gloss ("Reflection at grazing angles", Range(0, 1)) = 0.85
        _Ripple ("Ripple", Range(0, 1)) = 0.6
        _FreshLift ("Fresh green lift by day", Range(0, 1)) = 0.25
        _FreshDay ("Daylight 0..1 (set by EcoSurface from the day's light)", Range(0, 1)) = 1
        [HideInInspector] _SrcBlend ("Src blend", Float) = 5
        [HideInInspector] _DstBlend ("Dst blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-60" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                half4 _WetColor, _SkyHigh, _SkyLow, _PuddleDeep;
                half _SheenAmount, _Gloss, _Ripple, _WetBoost, _FreshLift, _FreshDay;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float3 posWS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.posWS);
                o.color = v.color; o.uv = v.uv; o.uv1 = v.uv1;
                return o;
            }

            // How much light the day gives the sky: the ambient probe and the sun, roughly 0.15 (night) .. 1.
            half DayLight()
            {
                Light l = GetMainLight();
                half3 amb = SampleSH(half3(0, 1, 0));
                half d = dot(amb, half3(0.33, 0.33, 0.33)) * 0.9 + dot(l.color, half3(0.33, 0.33, 0.33)) * 0.45;
                return saturate(d);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = saturate(i.color.a);
                float3 V = normalize(GetCameraPositionWS() - i.posWS);
                half nv = saturate(V.y); // the ground is flat enough: its normal is up
                half day = DayLight();
                if (_Mode < 1.5) a = saturate(a * _WetBoost);
                if (_Mode < 0.5) return half4(lerp(half3(1, 1, 1), _WetColor.rgb, a), 1);
                if (_Mode < 1.5)
                {
                    half g = pow(1.0 - nv, 3.0) * 0.8 + 0.12;
                    return half4(_SkyLow.rgb * (_SheenAmount * a * g * day), 1);
                }
                if (_Mode < 2.5)
                {
                    float2 p = i.color.rg * 2.0 - 1.0;                 // where in the puddle, -1..1 (box-local x, z)
                    float3 R = reflect(-V, float3(0, 1, 0));
                    half fres = lerp(0.28, _Gloss, pow(1.0 - nv, 2.5));
                    half3 sky = lerp(_SkyLow.rgb, _SkyHigh.rgb, saturate(R.y * 1.3)) * (0.3 + day * 0.75);
                    half3 bed = _PuddleDeep.rgb * (0.45 + day * 0.55);
                    half3 col = lerp(bed, sky, fres * 0.35);
                    // the water mirrors the sky low over the land on its far side and shows its dark bed on the near side: a soft
                    // gradient across the puddle, which is what makes it read as a wet, glossy surface and not a flat stone
                    float3 Vo = TransformWorldToObjectDir(V);           // p is in the box's own x, z: so is the view
                    float2 away = -Vo.xz / max(1e-4, length(Vo.xz));
                    half far = smoothstep(-0.35, 0.95, dot(p, away));
                    col = lerp(col, sky * 1.25, pow(far, 1.5) * 0.9);
                    // gloss: the sun's glint, and a soft window of light toward the far side (always there, so it reads as wet)
                    Light l = GetMainLight();
                    half spec = pow(saturate(dot(R, l.direction)), 160.0) * 3.0;
                    float2 gp = p - away * 0.36 - float2(-away.y, away.x) * 0.22;
                    half glint = smoothstep(0.16, 0.0, length(gp)) * 1.2;
                    col += spec * l.color + glint * sky * (0.4 + 0.6 * day);
                    // the odd ripple: a ring that runs out from the middle now and then
                    float cyc = frac(_Time.y * 0.075 + i.uv1.x);   // a 13 s round, the ring runs in its first third
                    float ring = saturate(cyc / 0.34) * 0.9;
                    float w = abs(i.uv.x - ring);
                    half rip = smoothstep(0.12, 0.0, w) * (1.0 - cyc / 0.34) * step(cyc, 0.34) * i.uv1.y * _Ripple;
                    col += rip * sky * 0.6;
                    // the rim: dark wet mud round the water (fading into the ground), a thin bright meniscus at the edge
                    half r = i.uv.x;
                    half mud = smoothstep(0.66, 0.84, r);
                    col = lerp(col, _WetColor.rgb * 0.45 * (0.4 + 0.6 * day), mud * 0.4);
                    // shallow: the near side lets the wet ground show through, the mirrored far side and the glint are solid
                    half see = lerp(0.5, 0.95, saturate(far + glint + spec));
                    return half4(col, a * see * lerp(1.0, 0.8, mud));
                }
                if (_Mode < 3.5) return half4(i.color.rgb * (0.35 + day * 0.65), a); // the cheap lit tint
                if (_Mode > 4.5)
                {
                    half3 hue = i.color.rgb / max(1e-3, max(i.color.r, max(i.color.g, i.color.b))); // no channel above 1: never brighter by itself
                    half lift = 1.0 + _FreshLift * smoothstep(0.55, 0.9, _FreshDay); // by day only: the night stays as dark as the land
                    half3 f = lerp(half3(1, 1, 1), hue * lift, a);
                    return half4(f * 0.5, 1); // 2x multiply: 0.5 leaves the ground as it is
                }
                // a droplet: round and soft from the quad's UV, a touch brighter in the middle
                float2 q = i.uv * 2.0 - 1.0;
                half m = saturate(1.0 - dot(q, q));
                half3 dcol = i.color.rgb * (0.12 + day * 1.0); // #379: lit like the land (a 0.55 floor made white puffs on a dusk land)
                return half4(dcol, a * m * m);
            }
            ENDHLSL
        }
    }
}
