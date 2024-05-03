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


Shader "Custom/StencilSurface" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _BackColor ("Backside Color", Color) = (1,1,1,1)
  _LocalScale ("Local Scale", Vector) = (1,1,1)
  _GridSize ("Grid Size", Float) = 1
  _GridLineWidth ("Grid Line Width", Float) = .01
  _FrameWidth ("Frame Width", Float) = .1
  [KeywordEnum(Plane, Cube, Sphere, Capsule)] _Shape ("Shape Type", Float) = 0
}

CGINCLUDE
  #include "UnityCG.cginc"
  #include "Assets/Shaders/Include/Brush.cginc"
  #include "Assets/Shaders/Include/MobileSelection.cginc"

  #pragma multi_compile _SHAPE_PLANE _SHAPE_CUBE _SHAPE_SPHERE _SHAPE_CAPSULE
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

  struct appdata_t {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;

    UNITY_VERTEX_INPUT_INSTANCE_ID
  };

  struct v2f {
    float4 vertex : SV_POSITION;
    float3 pos : TEXCOORD1;
    float3 normal : TEXCOORD2;
    float2 texcoord : TEXCOORD3;
    float4 screenPos : TEXCOORD4;

    UNITY_VERTEX_OUTPUT_STEREO
  };

  v2f vert (appdata_t v)
  {
    v2f o;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.pos = v.vertex;
    o.vertex = UnityObjectToClipPos(v.vertex);

    // Push the stencil back in depth to prevent z fighting when the user is drawing on top of it.
    if (!_UserIsInteractingWithStencilWidget) {
      o.vertex.z += .0025 * o.vertex.w;
    }

    o.normal = v.normal;
    o.texcoord = v.texcoord;
    o.screenPos = ComputeScreenPos(o.vertex);
    return o;
  }

  struct Facings {
    float facingX;
    float facingY;
    float facingZ;
  };
  Facings facings;

  float getInteriorGrid(v2f i, in float gridSizeMultiplier, in float gridWidthMultiplier, in float UVGridWidthMultiplier) {
    float3 localPos = i.pos * _LocalScale;

    // Change grid size based on scene scale (except for the plane where we want more control)
    float gridMultiplier = 1;

#ifndef _SHAPE_PLANE
    const float sceneScale = length(mul(xf_CS, float4(1,0,0,0)));
    if (sceneScale > 5) gridMultiplier = 1;
    else if (sceneScale > 1) gridMultiplier = 2;
    else if (sceneScale > .5) gridMultiplier = 4;
    else if (sceneScale > .25) gridMultiplier = 8;
    else gridMultiplier = 16;
#endif

    _GridSize *= gridMultiplier * gridSizeMultiplier * _GlobalGridSizeMultiplier;
    _GridLineWidth *= gridMultiplier * gridWidthMultiplier * _GlobalGridLineWidthMultiplier;
    _FrameWidth *= gridMultiplier * UVGridWidthMultiplier * _GlobalFrameWidthMultiplier;

#ifndef _SHAPE_PLANE
    facings.facingY = pow(dot(i.normal, float3(0,1,0)),4);
    facings.facingX = pow(dot(i.normal, float3(1,0,0)),4);
    facings.facingZ = pow(dot(i.normal, float3(0,0,1)),4);
#endif

    // Edges along the interior of the cube
    float interiorGrid = 0;

    // Grid line creation follows a formula that goes something like this:
    // mod(abs(myValue) + offset half of a line width to center on a line, distance to next line)
    //https://graphtoy.com/?f1(x,t)=0.05&v1=false&f2(x,t)=0.5&v2=true&f3(x,t)=mod(abs(x)+f1(x,t)/2,f2(x,t))%3Cf1(x,t)&v3=true&f4(x,t)=mod(abs(x)%20+%20f2(x,t)/2%20+%20f1(x,t)/2,f2(x,t))%3Cf1(x,t)&v4=false&f5(x,t)=(t+floor(x-t))/2-5&v5=false&f6(x,t)=sin(f5(x,t))-5&v6=false&grid=true&coords=-0.1521486828664754,0.5308298491119252,1.2183071759372512

    const float halfLineWidth = _GridLineWidth / 2.0;
#if _SHAPE_PLANE
    interiorGrid += fmod((abs(localPos.y) + halfLineWidth), _GridSize) < _GridLineWidth ? 1 : 0;
    interiorGrid = max(fmod((abs(localPos.x) + halfLineWidth), _GridSize) < _GridLineWidth ? 1 : 0, interiorGrid);
#else
    interiorGrid += fmod((abs(localPos.y) + halfLineWidth), _GridSize) < _GridLineWidth ? 1 - facings.facingY : 0;
    interiorGrid = max(fmod((abs(localPos.x) + halfLineWidth), _GridSize) < _GridLineWidth ? 1 - facings.facingX : 0, interiorGrid);
    interiorGrid = max(fmod((abs(localPos.z) + halfLineWidth), _GridSize) < _GridLineWidth ? 1 - facings.facingZ : 0, interiorGrid);
#endif

    // This was previously calculated using the code below.  The fmodBias is problematic when it
    // comes to dynamically adjusting the grid size (it jumps around dramatically) and it doesn't
    // seem necessary if you just take the absolute value instead (as seen in the code above).
    //float fmodBias = 10000; //keep fmod from wrapping into negative values
    // interiorGrid += fmod( (localPos.y + fmodBias + (_GridLineWidth / 2.0f) ), _GridSize) < _GridLineWidth ? 1 - facings.facingY : 0;
    // interiorGrid = max(fmod( (localPos.x + fmodBias + (_GridLineWidth / 2.0f) ), _GridSize) < _GridLineWidth ? 1 - facings.facingX : 0, interiorGrid);
    // interiorGrid = max(fmod( (localPos.z + fmodBias + (_GridLineWidth / 2.0f) ), _GridSize) < _GridLineWidth ? 1 - facings.facingZ : 0, interiorGrid);

    return interiorGrid;
  }

  float4 createStencilGrid (v2f i, float gridSizeMultiplier, float gridWidthMultiplier, float UVGridWidthMultiplier) {
    float4 c = float4(0,0,0,0);

    float interiorGrid = getInteriorGrid(i, gridSizeMultiplier, gridWidthMultiplier, UVGridWidthMultiplier);

    // Edges along the border of the cube, capsule or sphere
    float outerEdges = 0;

#if _SHAPE_PLANE
    const float gridWidthX = _FrameWidth / _LocalScale.x;
    const float gridWidthY = _FrameWidth / _LocalScale.y;
    outerEdges += (abs(.5 - i.texcoord.x) > (.5 - gridWidthX));
    outerEdges += (abs(.5 - i.texcoord.y) > (.5 - gridWidthY));
#elif _SHAPE_CUBE
    const float gridWidthX = _FrameWidth / _LocalScale.x;
    const float gridWidthY = _FrameWidth / _LocalScale.y;
    const float gridWidthZ = _FrameWidth / _LocalScale.z;

    // top / bottom
    outerEdges += facings.facingY * (abs(.5 - i.texcoord.x) > (.5 - gridWidthX));
    outerEdges += facings.facingY * (abs(.5 - i.texcoord.y) > (.5 - gridWidthZ));

    // left / right
    outerEdges += facings.facingX * (abs(.5 - i.texcoord.x) > (.5 - gridWidthZ));
    outerEdges += facings.facingX * (abs(.5 - i.texcoord.y) > (.5 - gridWidthY));

    // front / back
    outerEdges += facings.facingZ * (abs(.5 - i.texcoord.x) > (.5 - gridWidthX));
    outerEdges += facings.facingZ * (abs(.5 - i.texcoord.y) > (.5 - gridWidthY));
#elif _SHAPE_CAPSULE
    const int numLines = 4;
    const float gridWidthX = .5 * _FrameWidth / _LocalScale.x;
    outerEdges += fmod(((i.texcoord.x - (gridWidthX / 2.0f) / (numLines)) * numLines + 1000), 1) > (1-gridWidthX);

    const float gridWidthY =  .25 * _FrameWidth / (_LocalScale.y);
    outerEdges += abs(.5 - i.texcoord.y) >  (.5 - gridWidthY);
#elif _SHAPE_SPHERE
    const float gridWidthX = _FrameWidth / _LocalScale.x;
    outerEdges += abs(fmod(i.pos.x * 2 + 0 , 1)) < gridWidthX * 2;
    outerEdges += abs(fmod(i.pos.y * 2 + 0 , 1)) < gridWidthX * 2;
    outerEdges += abs(fmod(i.pos.z * 2 + 0 , 1)) < gridWidthX * 2;
#else
    return float4(1,0,1,1);
#endif

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
ENDCG

SubShader {
Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}

LOD 100
ColorMask RGB
Lighting Off Fog { Color (0,0,0,0) }
ZWrite Off

// back faces
Cull Front
Blend SrcAlpha OneMinusSrcAlpha // overlay

Pass {
  CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    fixed4 frag (v2f i) : SV_Target
    {
      float4 c = createStencilGrid(i,2,.5,.25);

      #if SELECTION_ON
         return float4(GetSelectionColor().rgb, c.r) * 0.65;
      #elif HIGHLIGHT_ON
         return float4(_BrushColor.rgb, c.r) * 0.65;
      #endif

      c.a = c.r * .65;
      c.rgb += float3(.2,.2,.2);
      c.a = _WidgetsDormant ? max (.5, c.a) : c.a;
      return c * c.a * _Color * _BackColor;
    }
  ENDCG
  }

// front faces
Cull Back
Blend SrcAlpha OneMinusSrcAlpha // overlay
Pass {
  CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag

    fixed4 frag (v2f i) : SV_Target
    {
      float4 c = createStencilGrid(i,1,1,.5);

      #if SELECTION_ON
         return float4(GetSelectionColor().rgb, c.r);
      #elif HIGHLIGHT_ON
         return float4(_BrushColor.rgb, c.r);
      #endif

      c.a = c.r * .65;
      c.rgb *= 1.5;
      return c * c.a * _Color;
    }
  ENDCG
  }

} // end subshader
Fallback "Unlit/Diffuse"
}
