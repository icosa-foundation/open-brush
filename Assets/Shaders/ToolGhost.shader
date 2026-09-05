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

Shader "Custom/ToolGhost"
{
  Properties
  {
    _Color ("Color", Color) = (1, 1, 1, 1)
    _GridDensity ("Grid Density", Float) = 8
    _GridLineWidth ("Grid Line Width", Range(0.001, 0.25)) = 0.04
    _FresnelStrength ("Fresnel Strength", Range(-8, 8)) = 1
  }

  Category
  {
    SubShader
    {

      Tags
      {
        "Queue" = "Overlay" "IgnoreProjector" = "True" "RenderType" = "Transparent"
      }

      Pass
      {
        Blend One One
        Lighting Off Cull Off ZTest Always ZWrite Off Fog
        {
          Mode Off
        }

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0
        #pragma multi_compile_instancing

        #include <UnityStandardInput.cginc>

        #include "UnityCG.cginc"
        #include "Assets/Shaders/Include/Brush.cginc"
        #include "Assets/Shaders/Include/ColorSpace.cginc"

        float _GridDensity;
        float _GridLineWidth;
        float _FresnelStrength;

        struct appdata_t
        {
          float4 vertex : POSITION;
          fixed4 color : COLOR;
          float3 normal : NORMAL;
          float2 uv : TEXCOORD0;

          UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f
        {
          float4 vertex : SV_POSITION;
          float3 objectPosition : TEXCOORD0;

          UNITY_VERTEX_INPUT_INSTANCE_ID
          UNITY_VERTEX_OUTPUT_STEREO
        };


        v2f vert(appdata_t v)
        {
          UNITY_SETUP_INSTANCE_ID(v);
          v2f o;
          UNITY_INITIALIZE_OUTPUT(v2f, o);
          UNITY_TRANSFER_INSTANCE_ID(v, o);
          UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
          o.vertex = UnityObjectToClipPos(v.vertex);
          o.objectPosition = v.vertex.xyz;
          return o;
        }

        float ProceduralGrid(float3 objectPosition)
        {
          float3 gridPosition = objectPosition * max(abs(_GridDensity), 0.0001);
          float3 distanceToLine = abs(frac(gridPosition + 0.5) - 0.5);
          float3 filterWidth = max(fwidth(gridPosition), 0.0001);
          float3 gridLines = 1.0 - smoothstep(
              _GridLineWidth - filterWidth,
              _GridLineWidth + filterWidth,
              distanceToLine);
          return max(gridLines.x, max(gridLines.y, gridLines.z));
        }

        fixed4 frag(v2f i) : SV_Target
        {
          UNITY_SETUP_INSTANCE_ID(i);
          UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
          float3 sphereDirection = normalize(i.objectPosition);
          float3 spherePosition = sphereDirection * 0.5;
          float3 worldPosition = mul(
              unity_ObjectToWorld, float4(spherePosition, 1.0)).xyz;
          float3 viewDir = normalize(UnityWorldSpaceViewDir(worldPosition));
          float3 normal = normalize(UnityObjectToWorldNormal(sphereDirection));
          float facingRatio = saturate(dot(viewDir, normal));
          facingRatio = 1 - facingRatio;
          facingRatio = _FresnelStrength >= 0
              ? pow(max(facingRatio, 0.0001), _FresnelStrength)
              : pow(max(1 - facingRatio, 0.0001), -_FresnelStrength);
          float grid = ProceduralGrid(spherePosition);
          float4 outColor = _Color * (grid + _Color.a) * facingRatio + 0.05;
          outColor.a = _Color.a;
          return outColor;
        }
        ENDCG
    }
  }
}
Fallback "Unlit/Diffuse"
}
