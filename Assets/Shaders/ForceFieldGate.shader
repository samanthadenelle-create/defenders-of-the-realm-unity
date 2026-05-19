// =============================================================================
// ForceFieldGate.shader — the violet cardinal-gate force-field shimmer (Week-4).
// -----------------------------------------------------------------------------
// Port spec Part 3 / Part 5 Week 4: src/modules/village/walls/Gate.tsx ->
// Gate.cs + ForceFieldGate.shader. A URP-compatible transparent shader for the
// shimmering violet force field that fills a cardinal gate's opening.
//
// Visual model (docs/four-cardinal-gates-spec.md "force field"):
//   - a vertical hex-ish energy sheet, violet (#7C3AED-family) by default,
//   - animated scrolling shimmer bands + a Fresnel rim glow,
//   - a soft vertical scanline so the field reads as "energy", not glass.
//
// GAMEPLAY HOOK — _Collapse (0 = full strength, 1 = collapsed):
//   Gate.cs drives _Collapse from gate HP. As HP falls below 25%, Gate.cs
//   raises _Collapse toward 1; the shader fades the field's alpha, desaturates
//   the violet toward a dim red, and tears holes in the sheet (noise-thresholded
//   dissolve) so the field visibly comes apart — the cue that enemies can pour
//   through. At _Collapse = 1 the field is effectively gone.
//
// URP: a Transparent, ZWrite-Off, additive-leaning unlit pass written against
// com.unity.render-pipelines.universal's Core.hlsl. No Shader Graph dependency
// so the shader is diff-reviewable as plain text.
// =============================================================================
Shader "DeNelle/Village/ForceFieldGate"
{
    Properties
    {
        [HDR] _FieldColor   ("Field Color (violet)", Color) = (0.486, 0.227, 0.929, 1.0)
        [HDR] _RimColor     ("Fresnel Rim Color", Color)    = (0.55, 0.40, 1.0, 1.0)
        _CollapseColor      ("Collapsed Tint (dim red)", Color) = (0.78, 0.16, 0.16, 1.0)

        _ShimmerSpeed       ("Shimmer Scroll Speed", Float)  = 0.85
        _ShimmerTiling      ("Shimmer Band Tiling", Float)   = 6.0
        _ShimmerStrength    ("Shimmer Strength", Range(0,1)) = 0.45
        _ScanlineTiling     ("Scanline Tiling", Float)       = 22.0
        _ScanlineStrength   ("Scanline Strength", Range(0,1))= 0.30

        _FresnelPower       ("Fresnel Power", Range(0.5,8))  = 3.0
        _BaseAlpha          ("Base Alpha", Range(0,1))       = 0.42
        _PulseSpeed         ("Idle Pulse Speed", Float)      = 1.4
        _PulseDepth         ("Idle Pulse Depth", Range(0,1)) = 0.12

        // Driven by Gate.cs: 0 = force field at full strength, 1 = collapsed.
        _Collapse           ("Collapse (0..1)", Range(0,1))  = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "ForceField"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One        // additive-leaning — the field GLOWS
            ZWrite Off
            Cull Off                  // visible from both approach sides

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _FieldColor;
                half4  _RimColor;
                half4  _CollapseColor;
                float  _ShimmerSpeed;
                float  _ShimmerTiling;
                float  _ShimmerStrength;
                float  _ScanlineTiling;
                float  _ScanlineStrength;
                float  _FresnelPower;
                float  _BaseAlpha;
                float  _PulseSpeed;
                float  _PulseDepth;
                float  _Collapse;
            CBUFFER_END

            // Cheap hash noise — used for the collapse dissolve tear pattern.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.uv          = IN.uv;
                OUT.normalWS    = nrmInputs.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(posInputs.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // ── scrolling shimmer bands — the "energy moving" read ──
                float band = sin((IN.uv.y * _ShimmerTiling) + t * _ShimmerSpeed) * 0.5 + 0.5;
                float shimmer = lerp(1.0, band, _ShimmerStrength);

                // ── soft horizontal scanlines — "field", not "glass" ──
                float scan = sin(IN.uv.y * _ScanlineTiling * 3.14159) * 0.5 + 0.5;
                scan = lerp(1.0, scan, _ScanlineStrength);

                // ── Fresnel rim glow — bright where the sheet is edge-on ──
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float  fresnel = pow(saturate(1.0 - abs(dot(N, V))), _FresnelPower);

                // ── idle breathing pulse ──
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseDepth;

                // ── base colour: violet body + rim, pulsed + shimmered ──
                half3 col = _FieldColor.rgb * shimmer * scan * pulse;
                col += _RimColor.rgb * fresnel;

                float alpha = saturate(_BaseAlpha * shimmer * scan + fresnel * 0.6);

                // ─────────────────────────────────────────────────────────────
                //  COLLAPSE — Gate.cs drives _Collapse 0..1 from gate HP.
                //  The field tears apart and dims so enemies can pour through.
                // ─────────────────────────────────────────────────────────────
                if (_Collapse > 0.0001)
                {
                    // 1) desaturate the violet toward the dim collapse red.
                    col = lerp(col, _CollapseColor.rgb * (shimmer * 0.6 + 0.4), _Collapse);

                    // 2) noise-thresholded dissolve — holes open in the sheet.
                    //    A scrolling cell noise keeps the tear edges flickering.
                    float n = hash21(floor(IN.uv * 26.0) + floor(t * 5.0) * 0.137);
                    // as _Collapse -> 1, the cutoff rises and erases the field.
                    float cutoff = _Collapse * 1.08;
                    if (n < cutoff) discard;

                    // 3) fade the surviving fragments — a failing force field.
                    alpha *= (1.0 - _Collapse * 0.85);
                }

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
