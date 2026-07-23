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
    _MainTex ("Texture", 2D) = "white" {}
    _VectorX ("X Vector", Float) = 1
    _VectorY ("Y Vector", Float) = 0.5
    _VectorZ ("Z Vector", Float) = 0
    _TilingX ("X Tiling", Float) = 1
    _TilingY ("Y Tiling", Float) = 1
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

        #include <UnityStandardInput.cginc>

        #include "UnityCG.cginc"
        #include "Assets/Shaders/Include/Brush.cginc"
        #include "Assets/Shaders/Include/ColorSpace.cginc"

        float _VectorX;
        float _VectorY;
        float _VectorZ;
        float _TilingX;
        float _TilingY;

        struct appdata_t
        {
          float4 vertex : POSITION;
          fixed4 color : COLOR;
          float3 normal : NORMAL;
          float2 uv : TEXCOORD0;
        };

        struct v2f
        {
          float4 vertex : POSITION;
          float3 viewDir : TEXCOORD0;
          float3 normal : NORMAL;
          float2 uv : TEXCOORD1;
        };


        v2f vert(appdata_t v)
        {
          v2f o;
          o.vertex = UnityObjectToClipPos(v.vertex);
          o.viewDir = normalize(ObjSpaceViewDir(v.vertex));
          o.normal = normalize(v.normal);

          float3 direction = float3(_VectorX, _VectorY, _VectorZ); // The direction to project onto
          float2 tiling = float2(_TilingX, _TilingY);
          float3 N = normalize(direction); // Normalize the projection direction to use as the plane's normal
          float3 U = normalize(cross(float3(0, 1, 0), N)); // U is perpendicular to 'up' and 'N'
          float3 V = cross(N, U); // V is perpendicular to both 'N' and 'U', lies on the plane

          // Project position onto the plane defined by N, U, V
          float3 proj = v.vertex - dot(v.vertex, N) * N; // Project 'position' onto the plane
          o.uv = float2(dot(proj, U) * tiling.x, dot(proj, V) * tiling.y);
          return o;
        }

        fixed4 frag(v2f i) : COLOR
        {
          float facingRatio = saturate(dot(i.viewDir, i.normal));
          facingRatio = 1 - facingRatio;
          float4 texColor = tex2D(_MainTex, i.uv);
          float4 outColor = _Color * (texColor + 0.5) * facingRatio;
          outColor.a = 0.5;
          return outColor;
        }
        ENDCG
    }
  }
}
Fallback "Unlit/Diffuse"
}
