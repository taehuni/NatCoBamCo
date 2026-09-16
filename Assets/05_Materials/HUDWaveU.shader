Shader "UI/HUD Wave U"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Range(0,1)) = 1
        _Thickness ("Thickness", Range(0.01,0.25)) = 0.12
        _Softness ("Edge Softness", Range(0.001,0.05)) = 0.01

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
            float _Progress;
            float _Thickness;
            float _Softness;

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
                // The curve is a lower semicircle with two vertical arms.
                // It is generated entirely from UV coordinates; no sprite texture is sampled.
                float2 p = input.uv * 2.0 - 1.0;
                // The target RectTransform is wide and shallow. Compensate for that
                // aspect ratio so the curved bottom stays circular in screen pixels.
                p.x *= 2.27;

                const float radius = 1.35;
                const float centerY = 0.45;
                const float topY = 0.68;
                const float verticalLength = topY - centerY;

                float clampedY = clamp(p.y, centerY, topY);
                float verticalDistance = length(float2(abs(p.x) - radius, p.y - clampedY));
                float2 fromCenter = p - float2(0.0, centerY);
                float arcDistance = abs(length(fromCenter) - radius);
                arcDistance = p.y <= centerY ? arcDistance : 10.0;
                float distanceToCurve = min(verticalDistance, arcDistance);

                float alpha = 1.0 - smoothstep(_Thickness - _Softness, _Thickness + _Softness, distanceToCurve);

                const float pi = 3.14159265;
                float totalLength = verticalLength * 2.0 + pi * radius;
                float travelled;
                if (p.y > centerY)
                {
                    travelled = p.x >= 0.0
                        ? topY - clamp(p.y, centerY, topY)
                        : verticalLength + pi * radius + clamp(p.y - centerY, 0.0, verticalLength);
                }
                else
                {
                    float angle = atan2(-fromCenter.y, fromCenter.x);
                    angle = clamp(angle, 0.0, pi);
                    travelled = verticalLength + angle * radius;
                }

                float curveProgress = travelled / totalLength;
                float progressMask = 1.0 - smoothstep(_Progress, _Progress + 0.008, curveProgress);
                alpha *= progressMask;

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
