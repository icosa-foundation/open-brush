// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

Shader "Hidden/OpenBrush/VoxelBrushPreview"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct PreviewBox
            {
                float4 minimum;
                float4 maximum;
                float4 color;
            };
            StructuredBuffer<PreviewBox> _Boxes;
            float4x4 _ModelToWorld;
            float _LineWidth;

            struct appdata
            {
                uint id : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float4 color : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                v2f output;
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                PreviewBox box = _Boxes[input.id / 144];
                uint vertex = input.id % 144;
                uint edge = vertex / 12;
                uint axis = edge / 4;
                uint corner = edge % 4;
                uint plane = (vertex % 12) / 6;
                uint triangleVertex = vertex % 6;
                float along = (triangleVertex == 2 || triangleVertex == 3 || triangleVertex == 4) ? 1 : 0;
                float side = (triangleVertex == 1 || triangleVertex == 3 || triangleVertex == 5) ? 1 : -1;
                float3 point = box.minimum.xyz;
                point[axis] = lerp(box.minimum[axis], box.maximum[axis], along);
                point[(axis + 1) % 3] = (corner & 1) != 0 ? box.maximum[(axis + 1) % 3] : box.minimum[(axis + 1) % 3];
                point[(axis + 2) % 3] = (corner & 2) != 0 ? box.maximum[(axis + 2) % 3] : box.minimum[(axis + 2) % 3];
                point[(axis + 1 + plane) % 3] += side * _LineWidth * 0.5;
                output.position = UnityWorldToClipPos(mul(_ModelToWorld, float4(point, 1)).xyz);
                output.color = box.color;
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(input.color.rgb * input.color.a, input.color.a);
            }
            ENDHLSL
        }
    }
}
