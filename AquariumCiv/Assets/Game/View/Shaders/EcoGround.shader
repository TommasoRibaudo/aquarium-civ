// The living land's wet look (P8 #266): one small shader, four uses picked by _Mode (one material each).
//   0 Wet    multiplies the ground it lies on by a darker, warmer soil: the land keeps its own light and shadow, only wetter
//   1 Sheen  adds a faint sky glint on the same mesh (a slight sheen on wet soil, strongest where it grazes the eye)
//   2 Puddle dark glossy water: a sky reflection that grows at grazing angles, the odd ripple, a soft rim (vertex alpha)
//   3 Tint   a plain soft-edged colour (the green that shows at once under the mister, until the chunk has caught up)
//   4 Drop   a round, soft particle (the mister's droplets; stretched billboards make them streaks)
// Vertex colour = colour, vertex alpha = how much of it. Puddle UV0 = (distance from the middle in cells, 0), UV1 = (ripple
// phase 0..1, ripple strength 0..1). No textures; the day's light comes from the main light and the ambient probe.
Shader "AquariumCiv/View/EcoGround"
{
    Properties
    {
        [Enum(Wet,0,Sheen,1,Puddle,2,Tint,3,Drop,4)] _Mode ("Mode", Float) = 0
        _WetColor ("Wet soil multiplier", Color) = (0.46, 0.37, 0.31, 1)
        _SheenAmount ("Sheen", Range(0, 1)) = 0.22
        _SkyHigh ("Sky overhead", Color) = (0.62, 0.74, 0.88, 1)
        _SkyLow ("Sky at the horizon", Color) = (0.82, 0.86, 0.88, 1)
        _PuddleDeep ("Puddle bed", Color) = (0.07, 0.055, 0.045, 1)
        _Gloss ("Reflection at grazing angles", Range(0, 1)) = 0.85
        _Ripple ("Ripple", Range(0, 1)) = 0.6
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
                half _SheenAmount, _Gloss, _Ripple;
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
                if (_Mode < 0.5) return half4(lerp(half3(1, 1, 1), _WetColor.rgb, a), 1);
                if (_Mode < 1.5)
                {
                    half g = pow(1.0 - nv, 3.0) * 0.8 + 0.12;
                    return half4(_SkyLow.rgb * (_SheenAmount * a * g * day), 1);
                }
                if (_Mode < 2.5)
                {
                    half fres = lerp(0.17, _Gloss, pow(1.0 - nv, 3.0));
                    float3 R = reflect(-V, float3(0, 1, 0));
                    half3 sky = lerp(_SkyLow.rgb, _SkyHigh.rgb, saturate(R.y * 1.3)) * day * 0.8;
                    half3 col = lerp(_PuddleDeep.rgb * (0.4 + day * 0.6), sky, fres);
                    col += sky * 0.1 * smoothstep(0.55, 0.0, i.uv.x) * (0.4 + 0.6 * nv); // the sky lies brightest over the middle
                    // the odd ripple: a ring that runs out from the middle now and then
                    float cyc = frac(_Time.y * 0.075 + i.uv1.x);   // a 13 s round, the ring runs in its first third
                    float ring = saturate(cyc / 0.34) * 0.9;
                    float w = abs(i.uv.x - ring);
                    half rip = smoothstep(0.12, 0.0, w) * (1.0 - cyc / 0.34) * step(cyc, 0.34) * i.uv1.y * _Ripple;
                    col += rip * sky * 0.7;
                    return half4(col, a * (0.82 + 0.1 * fres));
                }
                if (_Mode < 3.5) return half4(i.color.rgb * (0.35 + day * 0.65), a); // the cheap lit tint
                // a droplet: round and soft from the quad's UV, a touch brighter in the middle
                float2 q = i.uv * 2.0 - 1.0;
                half m = saturate(1.0 - dot(q, q));
                half3 dcol = i.color.rgb * (0.55 + day * 0.6);
                return half4(dcol, a * m * m);
            }
            ENDHLSL
        }
    }
}
