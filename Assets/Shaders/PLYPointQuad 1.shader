Shader "KissTheDoor/PLY Point Quad"
{
    Properties
    {
        _PointTexture("Point Texture", 2D) = "white" {}
        _PointSize("Point Size (World Units)", Range(0.0001, 0.25)) = 0.01
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
        [Toggle] _UseVertexColor("Use PLY Vertex Color", Float) = 1
        [Enum(Alpha,10,Additive,1)] _DestinationBlend("Blend Mode", Float) = 10
        _ColorBoost("Color Boost", Range(0, 16)) = 1
        _Saturation("Saturation", Range(0, 4)) = 1
        _Contrast("Contrast", Range(0, 4)) = 1
        _AlphaClip("Alpha Clip", Range(0, 1)) = 0.01
        [Toggle] _ZWrite("Write Depth", Float) = 1
    }

    SubShader
    { 
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha [_DestinationBlend]
            Cull Off
            ZWrite [_ZWrite]
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma require geometry
            #pragma vertex Vert
            #pragma geometry Geom
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_PointTexture);
            SAMPLER(sampler_PointTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _PointTexture_ST;
                float4 _Tint;
                float _PointSize;
                float _UseVertexColor;
                float _DestinationBlend;
                float _ColorBoost;
                float _Saturation;
                float _Contrast;
                float _AlphaClip;
                float _ZWrite;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct VertexToGeometry
            {
                float3 positionWS : TEXCOORD0;
                float4 color : COLOR;
            };

            struct GeometryToFragment
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            VertexToGeometry Vert(Attributes input)
            {
                VertexToGeometry output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.color = input.color;
                return output;
            }

            void AppendCorner(
                float3 centerVS,
                float2 corner,
                float2 uv,
                float4 color,
                inout TriangleStream<GeometryToFragment> stream)
            {
                GeometryToFragment output;
                float halfSize = max(_PointSize, 0.000001) * 0.5;
                float3 positionVS = centerVS + float3(corner * halfSize, 0.0);
                output.positionCS = TransformWViewToHClip(positionVS);
                output.uv = uv;
                output.color = color;
                stream.Append(output);
            }

            [maxvertexcount(4)]
            void Geom(point VertexToGeometry input[1], inout TriangleStream<GeometryToFragment> stream)
            {
                float3 centerVS = TransformWorldToView(input[0].positionWS);
                float4 pointColor = lerp(float4(1.0, 1.0, 1.0, 1.0), input[0].color, saturate(_UseVertexColor));

                AppendCorner(centerVS, float2(-1, -1), float2(0, 0), pointColor, stream);
                AppendCorner(centerVS, float2(-1,  1), float2(0, 1), pointColor, stream);
                AppendCorner(centerVS, float2( 1, -1), float2(1, 0), pointColor, stream);
                AppendCorner(centerVS, float2( 1,  1), float2(1, 1), pointColor, stream);
                stream.RestartStrip();
            }

            float4 Frag(GeometryToFragment input) : SV_Target
            {
                float2 uv = input.uv * _PointTexture_ST.xy + _PointTexture_ST.zw;
                float4 textureSample = SAMPLE_TEXTURE2D(_PointTexture, sampler_PointTexture, uv);
                float4 sourceColor = textureSample * input.color * _Tint;
                clip(sourceColor.a - _AlphaClip);

                float luminance = dot(sourceColor.rgb, float3(0.2126, 0.7152, 0.0722));
                float3 adjustedColor = lerp(luminance.xxx, sourceColor.rgb, _Saturation);
                adjustedColor = (adjustedColor - 0.5) * _Contrast + 0.5;
                adjustedColor = max(adjustedColor, 0.0) * _ColorBoost;

                return float4(adjustedColor, sourceColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
