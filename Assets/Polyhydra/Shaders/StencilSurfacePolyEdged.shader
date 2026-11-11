// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.


Shader "Custom/StencilSurfacePolyEdged"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _BackColor ("Backside Color", Color) = (1,1,1,1)
        _LocalScale ("Local Scale", Vector) = (1,1,1)
        _GridSize ("Grid Size", Float) = -.05
        _GridLineWidth ("Grid Line Width", Float) = .005
        _FrameWidth ("Frame Width", Float) = -1
        _EdgeDistance("_EdgeDistance", Range(0, 1)) = .02
        _EdgeThickness("_EdgeThickness", Range(0, .5)) = .001
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Assets/Polyhydra/Shaders/PolyhydraEdges.hlsl"
    float4x4 xf_CS;
    #include "Packages/com.icosa.open-brush-unity-tools/Runtime/Shaders/Include/MobileSelection.cginc"

    #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON

    uniform float4 _Color;
    uniform float4 _BackColor;
    uniform float3 _LocalScale;
    uniform float _GridSize;
    uniform float _GlobalGridSizeMultiplier;
    uniform float _GlobalGridLineWidthMultiplier;
    uniform float _GlobalFrameWidthMultiplier;
    uniform float _GridLineWidth;
    uniform float _FrameWidth;
    uniform float _ModeSwitch;
    uniform int _UserIsInteractingWithStencilWidget;
    uniform int _WidgetsDormant;
    float _EdgeDistance;
    float _EdgeThickness;


    struct appdata_t
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 texcoord : TEXCOORD0;
        float2 edgeUv : TEXCOORD1;
        float3 barycentric : TEXCOORD2;

        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float3 pos : TEXCOORD1;
        float3 normal : TEXCOORD2;
        float2 texcoord : TEXCOORD3;
        float4 screenPos : TEXCOORD4;
        float2 edgeUv : TEXCOORD5;
        float3 barycentric : TEXCOORD6;

        UNITY_VERTEX_OUTPUT_STEREO
    };

    v2f vert(appdata_t v)
    {
        v2f o = (v2f)0;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.pos = v.vertex;
        o.vertex = TransformObjectToHClip(v.vertex.xyz);

        // Push the stencil back in depth to prevent z fighting when the user is drawing on top of it.
        if (!_UserIsInteractingWithStencilWidget)
        {
            #if UNITY_REVERSED_Z
                o.vertex.z -= .0025 * o.vertex.w;
            #else
                o.vertex.z += .0025 * o.vertex.w;
            #endif
        }

        o.normal = v.normal;
        o.texcoord = v.texcoord;
        o.screenPos = ComputeScreenPos(o.vertex);
        o.edgeUv = v.edgeUv;
        o.barycentric = v.barycentric;
        return o;
    }

    struct Facings
    {
        float facingX;
        float facingY;
        float facingZ;
    };


    float calcPolyEdge(v2f i)
    {
        return PolyhydraEdgeMask(i.edgeUv.x, i.barycentric, _EdgeDistance, _EdgeThickness);
    }

    float getInteriorGrid(v2f i, in float gridSizeMultiplier, in float gridWidthMultiplier,
                          in float UVGridWidthMultiplier, out Facings facings)
    {
        float3 localPos = i.pos * _LocalScale;

        // Change grid size based on scene scale (except for the plane where we want more control)
        float gridMultiplier = 1;

        const float sceneScale = length(mul(xf_CS, float4(1, 0, 0, 0)));
        if (sceneScale > 5) gridMultiplier = 1;
        else if (sceneScale > 1) gridMultiplier = 2;
        else if (sceneScale > .5) gridMultiplier = 4;
        else if (sceneScale > .25) gridMultiplier = 8;
        else gridMultiplier = 16;

        float gridSize = _GridSize * gridMultiplier * gridSizeMultiplier * _GlobalGridSizeMultiplier;
        float gridLineWidth = _GridLineWidth * gridMultiplier * gridWidthMultiplier * _GlobalGridLineWidthMultiplier;

        facings.facingY = pow(dot(i.normal, float3(0, 1, 0)), 4);
        facings.facingX = pow(dot(i.normal, float3(1, 0, 0)), 4);
        facings.facingZ = pow(dot(i.normal, float3(0, 0, 1)), 4);

        // Edges along the interior of the cube
        float interiorGrid = 0;

        const float halfLineWidth = gridLineWidth / 2.0;
        interiorGrid += fmod((abs(localPos.y) + halfLineWidth), gridSize) < gridLineWidth ? 1 - facings.facingY : 0;
        interiorGrid = max(fmod((abs(localPos.x) + halfLineWidth), gridSize) < gridLineWidth ? 1 - facings.facingX : 0, interiorGrid);
        interiorGrid = max(fmod((abs(localPos.z) + halfLineWidth), gridSize) < gridLineWidth ? 1 - facings.facingZ : 0, interiorGrid);

        return interiorGrid;
    }

    float4 createStencilGrid(v2f i, float gridSizeMultiplier, float gridWidthMultiplier, float UVGridWidthMultiplier)
    {
        float4 c = float4(0, 0, 0, 0);

        Facings facings;
        float interiorGrid = getInteriorGrid(i, gridSizeMultiplier, gridWidthMultiplier, UVGridWidthMultiplier, facings);

        // Edges along the border of the cube, capsule or sphere
        float outerEdges = 0;

        float sceneScale = length(mul(xf_CS, float4(1, 0, 0, 0)));
        float gridMultiplier = sceneScale > 5 ? 1 : sceneScale > 1 ? 2 : sceneScale > .5 ? 4 : sceneScale > .25 ? 8 : 16;
        float frameWidth = _FrameWidth * gridMultiplier * UVGridWidthMultiplier * _GlobalFrameWidthMultiplier;
        const float gridWidthX = frameWidth / _LocalScale.x;
        const float gridWidthY = frameWidth / _LocalScale.y;
        const float gridWidthZ = frameWidth / _LocalScale.z;

        // top / bottom
        outerEdges += facings.facingY * (abs(.5 - i.texcoord.x) > (.5 - gridWidthX));
        outerEdges += facings.facingY * (abs(.5 - i.texcoord.y) > (.5 - gridWidthZ));

        // left / right
        outerEdges += facings.facingX * (abs(.5 - i.texcoord.x) > (.5 - gridWidthZ));
        outerEdges += facings.facingX * (abs(.5 - i.texcoord.y) > (.5 - gridWidthY));

        // front / back
        outerEdges += facings.facingZ * (abs(.5 - i.texcoord.x) > (.5 - gridWidthX));
        outerEdges += facings.facingZ * (abs(.5 - i.texcoord.y) > (.5 - gridWidthY));

        // Compute a float that fades out when the camera gets too close
        // Magic numbers tuned to taste here.
        const float fStartFade = .95;
        const float fEndFade = .985;

        // Get NDC (0 to 1) depth value
        // Unity uses a reverse z-buffer: 1 -> near plane, 0 -> far plane
        float depthFactor = i.screenPos.z / i.screenPos.w;
        depthFactor = 1 - smoothstep(fStartFade, fEndFade, depthFactor);

        // Get a [0, 1] value based on an S curve to depthFactor easing in at fStartFade and out fEndFade
        interiorGrid = depthFactor * saturate(interiorGrid);

        // Add in the outer frame (A.K.A edges) and combine it with the interior edges
        c.rgb += saturate(outerEdges);
        c.rgb += saturate(interiorGrid) * (1 - saturate(outerEdges));

        return c;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "StencilGrid"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask RGB
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            half4 frag(v2f i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                bool front = IS_FRONT_VFACE(facing, true, false);
                // URP draws one forward pass: combine the original front/back appearances.
                float4 c = createStencilGrid(i, front ? 1 : 2, front ? 1 : .5, front ? .5 : .25);
                c.rgb = max(c.rgb, calcPolyEdge(i));
                #if SELECTION_ON
                    return float4(GetSelectionColor().rgb, c.r) * (front ? 1 : .65);
                #elif HIGHLIGHT_ON
                    return float4(_BrushColor.rgb, c.r) * (front ? 1 : .65);
                #endif
                c.a = c.r * .65;
                if (front)
                {
                    c.rgb *= 1.5;
                    return c * c.a * _Color;
                }
                c.rgb += .2;
                c.a = _WidgetsDormant ? max(.5, c.a) : c.a;
                return c * c.a * _Color * _BackColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
