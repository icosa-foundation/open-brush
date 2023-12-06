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

Shader "Brush/StandardSingleSided" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
    _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _BumpMap ("Normalmap", 2D) = "bump" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5

    _Opacity ("Opacity", Range(0,1)) = 1
    _ClipStart("Clip Start", Float) = 0
    _ClipEnd("Clip End", Float) = -1
  }

  // -------------------------------------------------------------------------------------------- //
  // DESKTOP VERSION.
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 400
    Cull Back

    CGPROGRAM
      #pragma target 4.0
      #pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

      #include "Assets/Shaders/Include/Brush.cginc"

      struct Input {
        float2 uv_MainTex;
        float2 uv_BumpMap;
        float4 color : Color;
        uint id : SV_VertexID;
        float4 screenPos;
      };

      struct appdata_full_plus_id {
        float4 vertex : POSITION;
        float4 tangent : TANGENT;
        float3 normal : NORMAL;
        float4 texcoord : TEXCOORD0;
        float4 texcoord1 : TEXCOORD1;
        float4 texcoord2 : TEXCOORD2;
        float4 texcoord3 : TEXCOORD3;
        fixed4 color : COLOR;
        uint id : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      sampler2D _MainTex;
      sampler2D _BumpMap;
      fixed4 _Color;
      half _Shininess;

  	  uniform float _ClipStart;
	    uniform float _ClipEnd;
      uniform half _Opacity;

      void vert (inout appdata_full_plus_id i, out Input o) {
        UNITY_INITIALIZE_OUTPUT(Input, o);
        // o.tangent = v.tangent;
        PrepForOds(i.vertex);
        i.color = TbVertToNative(i.color);
        o.id = i.id;
      }

      void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

        if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
        if (_Opacity < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Opacity) discard;

        fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
        o.Albedo = tex.rgb * _Color.rgb * IN.color.rgb;
        o.Smoothness = _Shininess;
        o.Specular = _SpecColor;
        o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
        o.Alpha = tex.a * IN.color.a;
      }
    ENDCG
  }

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION - Vert/Frag, MSAA + Alpha-To-Coverage, w/Bump.
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags{ "Queue" = "Geometry" "IgnoreProjector" = "True" }
    Cull Back
    LOD 201

    Pass {
      Tags { "LightMode"="ForwardBase" }
      AlphaToMask On

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0

        #include "Assets/Shaders/Include/Brush.cginc"
        #include "UnityCG.cginc"
        #include "Lighting.cginc"

        // Disable all the things.
        #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight noshadow

        struct appdata {
          float4 vertex : POSITION;
          float2 uv : TEXCOORD0;
          half3 normal : NORMAL;
          fixed4 color : COLOR;
          float4 tangent : TANGENT;
          uint id : SV_VertexID;
        };

        struct v2f {
          float4 pos : SV_POSITION;
          float2 uv : TEXCOORD0;
          half3 worldNormal : NORMAL;
          fixed4 color : COLOR;
          half3 tspace0 : TEXCOORD1;
          half3 tspace1 : TEXCOORD2;
          half3 tspace2 : TEXCOORD3;
          uint id : TEXCOORD4;
        };

        sampler2D _MainTex;
        float4 _MainTex_ST;
        float4 _MainTex_TexelSize;
        sampler2D _BumpMap;

        fixed _Cutoff;
        half _MipScale;

        uniform float _ClipStart;
        uniform float _ClipEnd;
        uniform half _Opacity;

        float ComputeMipLevel(float2 uv) {
          float2 dx = ddx(uv);
          float2 dy = ddy(uv);
          float delta_max_sqr = max(dot(dx, dx), dot(dy, dy));
          return max(0.0, 0.5 * log2(delta_max_sqr));
        }

        v2f vert (appdata v) {
          v2f o;
          o.pos = UnityObjectToClipPos(v.vertex);
          o.uv = TRANSFORM_TEX(v.uv, _MainTex);
          o.worldNormal = UnityObjectToWorldNormal(v.normal);
          o.color = v.color;

          half3 wNormal = UnityObjectToWorldNormal(v.normal);
          half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
          half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
          half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
          o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
          o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
          o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);
          return o;
        }

        fixed4 frag (v2f i, fixed vface : VFACE) : SV_Target {

          if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
          if (_Opacity < 1 && Dither8x8(i.pos.xy) >= _Opacity) discard;

          fixed4 col = i.color;
          col.a = tex2D(_MainTex, i.uv).a * col.a;
          col.a *= 1 + max(0, ComputeMipLevel(i.uv * _MainTex_TexelSize.zw)) * _MipScale;
          col.a = (col.a - _Cutoff) / max(2 * fwidth(col.a), 0.0001) + 0.5;

          half3 tnormal = UnpackNormal(tex2D(_BumpMap, i.uv));
          tnormal.z *= vface;

          // Transform normal from tangent to world space.
          half3 worldNormal;
          worldNormal.x = dot(i.tspace0, tnormal);
          worldNormal.y = dot(i.tspace1, tnormal);
          worldNormal.z = dot(i.tspace2, tnormal);

          fixed ndotl = saturate(dot(worldNormal, normalize(_WorldSpaceLightPos0.xyz)));
          fixed3 lighting = ndotl * _LightColor0;
          lighting += ShadeSH9(half4(worldNormal, 1.0));

          col.rgb *= lighting;

          return col;
        }

      ENDCG
    } // pass
  } // subshader

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION - Vert/Frag, Alpha Tested, w/Bump.
  // -------------------------------------------------------------------------------------------- //
  SubShader{
    Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
    Cull Back
    LOD 200

    Pass {
      Tags { "LightMode"="ForwardBase" }

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0
        #pragma multi_compile __ SELECTION_ON
        #pragma multi_compile_fog

        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "Assets/Shaders/Include/MobileSelection.cginc"

        // Disable all the things.
        #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight noshadow

        struct appdata {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
            half3 normal : NORMAL;
            fixed4 color : COLOR;
            float4 tangent : TANGENT;
            uint id : SV_VertexID;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            fixed4 color : COLOR;
            half3 tspace0 : TEXCOORD1;
            half3 tspace1 : TANGENT;
            half3 tspace2 : NORMAL;
            float4 worldPos : TEXCOORD4;
            float2 id : TEXCOORD5;
            UNITY_FOG_COORDS(5)
        };

        sampler2D _MainTex;
        float4 _MainTex_ST;
        sampler2D _BumpMap;
        half _Shininess;

        fixed _Cutoff;
        uniform float _ClipStart;
        uniform float _ClipEnd;
        uniform half _Opacity;

        v2f vert (appdata v) {
          v2f o;
          o.pos = UnityObjectToClipPos(v.vertex);
          o.uv = TRANSFORM_TEX(v.uv, _MainTex);
          o.color = v.color;

          half3 wNormal = UnityObjectToWorldNormal(v.normal);
          half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
          half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
          half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
          o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
          o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
          o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);
          o.worldPos = mul (unity_ObjectToWorld, v.vertex);
          UNITY_TRANSFER_FOG(o, o.pos);
          o.id = (float2)v.id;
          return o;
        }

        fixed4 frag (v2f i, fixed vface : VFACE) : SV_Target {

          if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;


          fixed4 col = i.color;
          col.a = tex2D(_MainTex, i.uv).a * col.a;
          if (col.a < _Cutoff) { discard; }

          // The standard shader we have desaturates the color of objects depending on the
          // brightness of their specular color - this seems to be a reasonable emulation.
          float desaturated = dot(col, float3(0.3, 0.59, 0.11));
          col.rgb = lerp(col, desaturated, _SpecColor * 1.2);

          col.a = 1;
          half3 tnormal = UnpackNormal(tex2D(_BumpMap, i.uv));
          tnormal.z *= vface;

          // transform normal from tangent to world space
          half3 worldNormal;
          worldNormal.x = dot(i.tspace0, tnormal);
          worldNormal.y = dot(i.tspace1, tnormal);
          worldNormal.z = dot(i.tspace2, tnormal);

          fixed ndotl = saturate(dot(worldNormal, normalize(_WorldSpaceLightPos0.xyz)));
          fixed3 lighting = ndotl * _LightColor0;
          lighting += ShadeSH9(half4(worldNormal, 1.0));
          col.rgb *= lighting;
          UNITY_APPLY_FOG(i.fogCoord, col);
          FRAG_MOBILESELECT(col)
          return col;
        }
      ENDCG
    } // pass
  } // subshader

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION -- vert/frag, MSAA + Alpha-To-Coverage, No Bump.
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags{ "Queue" = "Geometry" "IgnoreProjector" = "True" }
    Cull Back
    LOD 150

    Pass {
      Tags { "LightMode"="ForwardBase" }
      AlphaToMask On

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0

        #include "Assets/Shaders/Include/Brush.cginc"
        #include "UnityCG.cginc"
        #include "Lighting.cginc"

        // Disable all the things.
        #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight noshadow

        struct appdata {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
            half3 normal : NORMAL;
            fixed4 color : COLOR;
            uint id : SV_VertexID;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            half3 worldNormal : NORMAL;
            fixed4 color : COLOR;
            uint id : TEXCOORD2;
        };

        sampler2D _MainTex;
        float4 _MainTex_ST;
        float4 _MainTex_TexelSize;

        fixed _Cutoff;
        half _MipScale;

        uniform float _ClipStart;
        uniform float _ClipEnd;
        uniform half _Opacity;

        float ComputeMipLevel(float2 uv) {
          float2 dx = ddx(uv);
          float2 dy = ddy(uv);
          float delta_max_sqr = max(dot(dx, dx), dot(dy, dy));
          return max(0.0, 0.5 * log2(delta_max_sqr));
        }

        v2f vert (appdata v) {
          v2f o;
          o.pos = UnityObjectToClipPos(v.vertex);
          o.uv = TRANSFORM_TEX(v.uv, _MainTex);
          o.worldNormal = UnityObjectToWorldNormal(v.normal);
          o.color = v.color;
          return o;
        }

        fixed4 frag (v2f i, fixed vface : VFACE) : SV_Target {

          if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
          if (_Opacity < 1 && Dither8x8(i.pos.xy) >= _Opacity) discard;

          fixed4 col = i.color;
          col.a *= tex2D(_MainTex, i.uv).a;
          col.a *= 1 + max(0, ComputeMipLevel(i.uv * _MainTex_TexelSize.zw)) * _MipScale;
          col.a = (col.a - _Cutoff) / max(2 * fwidth(col.a), 0.0001) + 0.5;

          half3 worldNormal = normalize(i.worldNormal * vface);

          fixed ndotl = saturate(dot(worldNormal, normalize(_WorldSpaceLightPos0.xyz)));
          fixed3 lighting = ndotl * _LightColor0;
          lighting += ShadeSH9(half4(worldNormal, 1.0));

          col.rgb *= lighting;

          return col;
        }
      ENDCG
    } // pass
  } // subshader

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION - Lambert SurfaceShader, Alpha Test, No Bump.
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 50
    Cull Back

    CGPROGRAM
      #pragma surface surf Lambert vertex:vert alphatest:_Cutoff
      #pragma target 3.0

      #include "Assets/Shaders/Include/Brush.cginc"

      sampler2D _MainTex;
      fixed4 _Color;

      uniform float _ClipStart;
      uniform float _ClipEnd;
      uniform half _Opacity;

      struct Input {
        float2 uv_MainTex;
        float4 color : COLOR;
        uint id : SV_VertexID;
        float4 screenPos;
      };

      void vert (inout appdata_full v) {
      }

      void surf (Input IN, inout SurfaceOutput o) {

        if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
        if (_Opacity < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Opacity) discard;

        fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
        o.Albedo = c.rgb * IN.color.rgb;
        o.Alpha = c.a * IN.color.a;
      }
    ENDCG
  }

  FallBack "Transparent/Cutout/VertexLit"
}
