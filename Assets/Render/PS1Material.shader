Shader "PS1/PS1Material"
{
    Properties
    {
        _MainTex        ("Texture",         2D)    = "white" {}
        _Color          ("Color",           Color) = (1,1,1,1)
        _SnapResolution ("Snap Resolution", Float) = 128.0
        _AffineStrength ("Affine Strength", Range(0,1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="HDRenderPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "ForwardOnly" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _Color;
            float     _SnapResolution;
            float     _AffineStrength;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // Пакуем UV и W в float3: xy=UV*W, z=W  (для affine trick)
                float3 uvAndW     : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                // Стандартный clip-space
                float4 clip = TransformObjectToHClip(IN.positionOS);

                // ── Vertex Snapping ───────────────────────────────────────────
                // Снэпаем XY в NDC к сетке размером _SnapResolution
                // Это даёт дёргающуюся геометрию как на PS1
                float2 snap = _SnapResolution;
                clip.xy = floor(clip.xy / clip.w * snap + 0.5) / snap * clip.w;

                OUT.positionCS = clip;

                // ── Affine Texture Mapping ────────────────────────────────────
                // На PS1 UV интерполировались без деления на W → искажение.
                // Трюк: умножаем UV на W перед растеризацией, в фрагменте делим.
                // lerp(перспектива, аффин) через _AffineStrength.
                float2 uv = TRANSFORM_TEX(IN.uv, _MainTex);
                float  w  = clip.w;
                // При AffineStrength=1 делаем полный PS1-стиль (без W-коррекции)
                // При AffineStrength=0 обычная перспективная коррекция
                float2 affineUV = uv * lerp(w, 1.0, _AffineStrength);
                OUT.uvAndW = float3(affineUV, lerp(w, 1.0, _AffineStrength));

                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // Восстанавливаем UV из аффинного пространства
                float2 uv = IN.uvAndW.xy / IN.uvAndW.z;

                float4 col = tex2D(_MainTex, uv) * _Color;
                return col;
            }
            ENDHLSL
        }

        // Shadow caster pass (нужен чтобы объект отбрасывал тени)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            float _SnapResolution;

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float4 clip = TransformObjectToHClip(IN.positionOS);
                float2 snap = _SnapResolution;
                clip.xy = floor(clip.xy / clip.w * snap + 0.5) / snap * clip.w;
                OUT.positionCS = clip;
                return OUT;
            }

            float4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
