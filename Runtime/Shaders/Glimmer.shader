Shader "UI/Glimmer/Shimmer"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.85, 0.85, 0.85, 1)
        _ShimmerColor ("Shimmer Color", Color) = (0.95, 0.95, 0.95, 1)
        _ShimmerSpeed ("Shimmer Speed", Float) = 0.833
        _ShimmerWidth ("Shimmer Width", Range(0.1, 0.5)) = 0.3
        _ShimmerSinCos ("Shimmer Sin/Cos", Vector) = (0.342, 0.940, 0.780, 0)
        _CornerRadius ("Corner Radius", Float) = 4
        _RectSize ("Rect Size", Vector) = (100, 100, 0, 0)
        _OuterUV ("Outer UV", Vector) = (0, 0, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1

        // UI Stencil support
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            half4 _BaseColor;
            half4 _ShimmerColor;
            float _ShimmerSpeed;
            half _ShimmerWidth;
            half3 _ShimmerSinCos; // x=sin, y=cos, z=1/(|sin|+|cos|) precomputed on CPU
            half _CornerRadius;
            float4 _RectSize;
            float4 _OuterUV;
            half _Alpha;
            float4 _ClipRect;
            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                half2 uv : TEXCOORD0;
                float4 uv1 : TEXCOORD1;  // Per-text rect data: (width, height, cornerRadius, flag)
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 rectData : TEXCOORD2;  // Per-text rect data passed from vertex
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // SDF for rounded rectangle
            half sdRoundedBox(float2 p, float2 b, half r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.rectData = v.uv1;  // Pass per-text rect data to fragment

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // For text quads, rect data is encoded in UV1 (rectData): (width, height, cornerRadius, flag)
                // rectData.w > 0 indicates per-vertex data (text quads set flag = 1.0)
                // For regular graphics, UV1 is (0,0,0,0) so we use uniform values
                bool isTextQuad = i.rectData.w > 0.5;
                float2 rectSize = isTextQuad ? i.rectData.xy : _RectSize.xy;
                half cornerRadius = isTextQuad ? i.rectData.z : _CornerRadius;

                // Remap UV from sprite atlas coordinates to 0-1 range
                // For text quads, UVs are already 0-1; for graphics, remap using _OuterUV
                float2 uv = i.uv;
                if (!isTextQuad)
                {
                    uv.x = (uv.x - _OuterUV.x) / (_OuterUV.z - _OuterUV.x);
                    uv.y = (uv.y - _OuterUV.y) / (_OuterUV.w - _OuterUV.y);
                }

                // Shimmer animation (speed = 1/duration, precomputed on CPU)
                half shimmerOffset = frac(_Time.y * _ShimmerSpeed);

                // Diagonal position using remapped UV (sin/cos/reciprocal precomputed on CPU)
                half diag = uv.x * _ShimmerSinCos.y + uv.y * _ShimmerSinCos.x;
                diag *= _ShimmerSinCos.z; // Normalize using precomputed 1/(|sin|+|cos|)

                // Shimmer band
                half shimmerPos = lerp(-_ShimmerWidth, 1.0 + _ShimmerWidth, shimmerOffset);
                half shimmerInfluence = 1.0 - saturate(abs(diag - shimmerPos) / _ShimmerWidth);
                shimmerInfluence = smoothstep(0.0, 1.0, shimmerInfluence);

                // Calculate local position from UV (maps 0-1 to -halfSize to +halfSize)
                float2 localPos = (uv - 0.5) * rectSize;

                // Rounded rectangle SDF
                // Clamp corner radius to half the minimum dimension to prevent artifacts
                float2 rectHalfSize = rectSize * 0.5;
                half clampedRadius = min(cornerRadius, min(rectHalfSize.x, rectHalfSize.y));
                half sdf = sdRoundedBox(localPos, rectHalfSize, clampedRadius);

                // Anti-aliased edge
                half edgeWidth = fwidth(sdf) * 0.5;
                half rectAlpha = smoothstep(edgeWidth, -edgeWidth, sdf);

                // Final color
                half4 finalColor = lerp(_BaseColor, _ShimmerColor, shimmerInfluence);
                finalColor.a *= rectAlpha * _Alpha;

                // Multiply by vertex color alpha (for regular graphics)
                finalColor.a *= i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
