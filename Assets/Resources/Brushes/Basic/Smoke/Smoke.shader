﻿// Copyright 2020 The Tilt Brush Authors
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

Shader "Brush/Particle/Smoke" {
Properties {
  _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
  _ScrollRate("Scroll Rate", Float) = 1.0
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
  Blend SrcAlpha One
  AlphaTest Greater .01
  ColorMask RGB
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_particles
      #pragma target 3.0
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Particles.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;
      fixed4 _TintColor;

      struct v2f {
        float4 pos : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      float4 _MainTex_ST;
      float _ScrollRate;

      //
      // Functions for line/plane distances. Experimental.
      //
      float dist_from_line(float3 line_dir, float3 point_on_line, float3 pos) {
        float3 point_to_line = pos - point_on_line;
        float3 dist_along_line = dot(point_to_line, line_dir);
        float3 closest_point_on_line = dist_along_line * line_dir + point_on_line;
        return length(closest_point_on_line - pos);
      }

      float dist_from_line_repeating(float3 line_dir, float3 point_on_line, float3 pos) {
        float3 point_to_line = pos - point_on_line;
        float3 dist_along_line = dot(point_to_line, line_dir);
        float3 closest_point_on_line = dist_along_line * line_dir + point_on_line;
        return length( sin(closest_point_on_line - pos));
      }

      float4 dist_from_plane(float3 plane_normal, float3 point_on_plane, float3 pos) {
        float dist = dot(plane_normal, pos - point_on_plane);
        float3 closest_point_on_plane = pos - dist * plane_normal;
        return float4(closest_point_on_plane.xyz, abs(dist));
      }

      v2f vert (ParticleVertex_t v)
      {
        v.color = TbVertToSrgb(v.color);
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        float birthTime = v.texcoord.w;
        float rotation = v.texcoord.z;
        float halfSize = GetParticleHalfSize(v.corner.xyz, v.center, birthTime);
        float4 center = float4(v.center.xyz, 1);
        float4 center_WS = mul(unity_ObjectToWorld, center);

        float t = _Time.y*_ScrollRate + v.color.a * 10;
        float time = _Time.x * 5;
        float d = 30;
        float freq = .1;
        float3 disp = float3(1,0,0) * curlX(center_WS.xyz * freq + time, d);
        disp += float3(0,1,0) * curlY(center_WS.xyz * freq +time, d);
        disp += float3(0,0,1) * curlZ(center_WS.xyz * freq + time, d);
        disp = disp * 5 * kDecimetersToWorldUnits;

        center_WS.xyz += mul(xf_CS, float4(disp, 0));

        PrepForOdsWorldSpace(center_WS);
        float4 corner = OrientParticle_WS(center_WS.xyz, halfSize, v.vid, rotation);
        o.pos = mul(UNITY_MATRIX_VP, corner);

        o.color = v.color;
        v.color.a = 1;
        o.texcoord = TRANSFORM_TEX(v.texcoord.xy,_MainTex);

        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        float4 c =  tex2D(_MainTex, i.texcoord);
        c *= i.color * _TintColor;
        c = SrgbToNative(c);
#if SELECTION_ON
        float strength = length(c.rgb * c.a) * 3;
        FRAG_MOBILESELECT(c)
        c.rgb *= strength;
#endif
        return c;
      }
      ENDCG
    }
  }
}
}
