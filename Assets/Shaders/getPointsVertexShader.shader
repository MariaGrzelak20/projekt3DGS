Shader "Custom/getPointsVertexShader"
{
  Properties
    {
        
        _QuadSize ("Quad Size", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalRenderPipeline" }

        Pass
        {
            Name "VertexBillboardsPass"
            Blend One Zero
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma geometry Geo
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            uniform float3 _CameraPos;

            struct appdata
            {
                float4 positionOS : POSITION;
                float4 color:COLOR;
            };

            struct v2g
            {
                float4 positionWS : POSITION;
                 float4 color:COLOR;
            };

            struct g2f
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            float _QuadSize;

            // Vertex Shader
            v2g Vert(appdata v)
            {
                v2g o;
                o.positionWS = float4(TransformObjectToWorld(v.positionOS.xyz),1.0);
                o.color = v.color;
                return o;
            }

            // Geometry Shader
            [maxvertexcount(6)]
            void Geo(point v2g input[1], inout TriangleStream<g2f> triStream)
            {
                float4 centerWS = input[0].positionWS;
                //float3 right = float3(_QuadSize, 0, 0);
                //float3 up = float3(0, _QuadSize, 0);
                float4 color    = input[0].color;
                float3 toCamera = normalize(_CameraPos - centerWS.xyz); // Direction from quad center to the camera
                float3 cameraUp = float3(0, 1, 0); // Assume global up vector; change if camera roll is needed
                float3 right = normalize(cross(cameraUp, toCamera)) * _QuadSize; // Right vector of the quad
                float3 up = normalize(cross(toCamera, right)) * _QuadSize;    

                float4 corners[4] = {
                    centerWS + float4(-right - up, 0),
                    centerWS + float4(right - up, 0),
                    centerWS + float4(right + up, 0),
                    centerWS + float4(-right + up, 0)
                };

                g2f quad[6] = {
                    {TransformWorldToHClip(corners[0]), color},
                    {TransformWorldToHClip(corners[1]), color},
                    {TransformWorldToHClip(corners[2]), color},
                    {TransformWorldToHClip(corners[0]), color},
                    {TransformWorldToHClip(corners[2]), color},
                    {TransformWorldToHClip(corners[3]), color}
                };

                for (int i = 0; i < 6; i++)
                {
                    triStream.Append(quad[i]);
                }
            }

            // Fragment Shader
            half4 Frag(g2f input) : SV_Target
            {
                return half4(input.color);
            }
            ENDHLSL
        }
    }
}
