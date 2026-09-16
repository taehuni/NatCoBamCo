Shader "UI/HUD Wave Dock Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Softness ("Edge Softness", Range(0.001,0.05)) = 0.012
        _JoinUV ("Header Join UV", Range(0,1)) = 0.615

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
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
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            float _Softness;
            float _JoinUV;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // Match the procedural U gauge exactly, but fill its cup silhouette.
                // This cuts away the rectangular corners and everything below the arc.
                float2 p = input.uv * 2.0 - 1.0;
                p.x *= 2.27;

                const float radius = 1.35;
                const float centerY = 0.45;
                const float topY = 0.68;
                const float edgeInset = 0.06;
                float outerRadius = radius + edgeInset;

                float distanceToShape;
                if (p.y <= centerY)
                {
                    distanceToShape = length(p - float2(0.0, centerY)) - outerRadius;
                }
                else
                {
                    float sideDistance = abs(p.x) - outerRadius;
                    float topDistance = p.y - topY;
                    distanceToShape = max(sideDistance, topDistance);
                }

                float alpha = 1.0 - smoothstep(-_Softness, _Softness, distanceToShape);

                // The header already draws everything above this line. Keeping the
                // backdrop only below it avoids double alpha and removes the seam.
                float headerJoin = 1.0 - smoothstep(_JoinUV, _JoinUV + 0.006, input.uv.y);
                alpha *= headerJoin;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                fixed4 color = input.color;
                color.a *= alpha;

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
