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

Shader "Brush/Special/Faceted" {
Properties {
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1

  _Opacity("Opacity", Range(0,1)) = 1

  _ColorX("Color X", Color) = (1,0,0,1)
  _ColorY("Color Y", Color) = (0,1,0,1)
  _ColorZ("Color Z", Color) = (0,0,1,1)
}

SubShader {
  Cull Back
  Pass{
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma target 3.0
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

    #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/Brush.cginc"
    #include "Assets/ThirdParty/Shaders/Noise.cginc"
    sampler2D _MainTex;
    float4 _MainTex_ST;
    fixed4 _ColorX;
    fixed4 _ColorY;
    fixed4 _ColorZ;
    uniform float _ClipStart;
    uniform float _ClipEnd;
    uniform float _Opacity;

    struct appdata_t {
      float4 vertex : POSITION;
      fixed4 color : COLOR;
      float3 normal : NORMAL;
      float2 texcoord : TEXCOORD0;
      uint id : SV_VertexID;
    };

    struct v2f {
      float4 vertex : SV_POSITION;
      fixed4 color : COLOR;
      float2 texcoord : TEXCOORD0;
      float3 worldPos : TEXCOORD1;
      float2 id : TEXCOORD2;
    };

    v2f vert (appdata_t v)
    {
      PrepForOds(v.vertex);
      v2f o;
      o.vertex = UnityObjectToClipPos(v.vertex);
      o.color = v.color;
      o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
      o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
      o.id = (float2)v.id;
      return o;
    }

    fixed4 frag (v2f i) : SV_Target
    {
      if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.y < _ClipEnd)) discard;
      if (_Opacity < 1 && Dither8x8(i.vertex.xy) > _Opacity) discard;

      float3 n = normalize(cross(ddy(i.worldPos), ddx(i.worldPos)));
      i.color.xyz = float3(
        lerp(float3(0,0,0), _ColorX, n.x) +
        lerp(float3(0,0,0), _ColorY, n.y) +
        lerp(float3(0,0,0), _ColorZ, n.z)
      );
      return i.color;
    }

    ENDCG
    }
  }
Fallback "Diffuse"
}
