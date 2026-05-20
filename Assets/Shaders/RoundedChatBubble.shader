// =============================================================================
// RoundedChatBubble.shader — URP-compatible unlit rounded-rect for the
// world-space speech bubbles (TownsfolkBubble + the BuildingInteractable
// prompts). Uses a signed-distance-field rounded-rect on the quad's UV so
// the corners read soft + clean without a 9-slice sprite asset.
// -----------------------------------------------------------------------------
// Parameters (all per-material):
//   _BaseColor    — fill colour (RGBA, alpha modulated by the SDF mask)
//   _Radius       — corner radius (0..0.5, UV units)
//   _Aspect       — width/height of the quad (so corner radius matches in
//                   both axes when the quad is non-square)
// =============================================================================
Shader "DeNelle/UI/RoundedChatBubble"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (1, 0.99, 0.96, 0.94)
        _Radius          ("Corner Radius (0..0.5)", Range(0, 0.5)) = 0.22
        _Aspect          ("Width / Height ratio",   Float) = 2.6
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Radius;
                float  _Aspect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // SDF of a rounded rectangle centred at the origin in a 1×1 box.
            // Returns negative inside, positive outside.
            float RoundedBoxSDF(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Centre the UV at (0,0) and scale x by aspect so corners are
                // geometrically square regardless of the quad's stretch.
                float2 p = (IN.uv - 0.5) * float2(_Aspect, 1.0);
                float2 halfSize = float2(_Aspect * 0.5, 0.5);
                float dist = RoundedBoxSDF(p, halfSize, _Radius);

                // 1-pixel-ish soft edge — fwidth gives screen-space derivative
                // so the mask stays crisp regardless of quad size on screen.
                float edge = fwidth(dist);
                float mask = saturate(0.5 - dist / max(edge, 1e-5));

                half4 col = _BaseColor;
                col.a *= mask;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
