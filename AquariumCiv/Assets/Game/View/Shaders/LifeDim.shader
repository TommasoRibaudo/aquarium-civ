// P9 #424 / #423 (user 2026-11-08): while a species is in hand the land dims a little, all but the valid ground. Three uses, one
// shader: the dim itself (a quad over the whole view, black at a low alpha, drawn after the box), the "restore" on the valid ground
// (Blend DstColor One with rgb = k x the glow's soft vertex alpha: dst x (1 + k a) undoes the dim there, edge fading as the glow
// does), and the ghost's pale silhouettes. Unlit vertex colour times _Color; _Premul 1 multiplies rgb by the vertex alpha.
Shader "AquariumCiv/View/LifeDim"
{
    Properties
    {
        _Color ("Tint", Color) = (0, 0, 0, 0.2)
        _Premul ("rgb x vertex alpha (the restore)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Premul;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = i.color * _Color;
                c.rgb *= lerp(1.0h, i.color.a, _Premul);
                return c;
            }
            ENDHLSL
        }
    }
}
