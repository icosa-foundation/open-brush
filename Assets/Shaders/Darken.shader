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

Shader "Brush/Darken" {
Properties {
  // Unused by this shader, but required by BrushDescriptor.HasExportTexture().
  _MainTex ("Texture", 2D) = "white" {}
  _Opacity ("Opacity", Range(0, 1)) = 1
  // Multiplies vertex alpha, so it can push the stroke past the alpha the geometry produces.
  // Above 1 the darkening saturates towards full strength.
  _AlphaMultiply ("Alpha Multiply", Range(0, 4)) = 1
  _SoftMultiply ("Softness", Range(0,1)) = 0.1
  // Read by App.cs and the Lua API; see Stroke.SetShaderClipping.
  _Dissolve ("Dissolve", Range(0, 1)) = 1
  _ClipStart ("Clip Start", Float) = 0
  _ClipEnd ("Clip End", Float) = -1
  // Width-direction fade, measured across the ribbon. The along-stroke fade is baked into vertex
  // alpha by the brush script (LineWithFadeBrush), so this handles the other axis.
  _EdgeFadeoff ("Edge Fadeoff", Range(0, 1)) = 0.1
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        // TODO: investigate different blend mode that allows us to remove blend in the frag
  Blend DstColor Zero
  AlphaTest Greater .01
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma multi_compile __ SHADER_SCRIPTING_ON
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #include "UnityCG.cginc"
      #include "Packages/com.icosa.open-brush-unity-tools/Runtime/Shaders/Include/Brush.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
        float2 texcoord : TEXCOORD0;
        uint id : SV_VertexID;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float2 id : TEXCOORD2;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      uniform half _ClipStart;
      uniform half _ClipEnd;
      uniform half _Dissolve;
      uniform half _Opacity;
      uniform half _AlphaMultiply;
      uniform half _SoftMultiply;
      uniform half _EdgeFadeoff;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);

        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = v.texcoord;
        o.color = v.color;
        o.id = (float2)v.id;
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        #ifdef SHADER_SCRIPTING_ON
        if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(i.pos.xy) >= _Dissolve) discard;
        #endif

        // Vertex alpha carries the along-stroke fades baked in by the brush.
        half4 c = i.color;

        // Fade off across the ribbon's width, using v as the across-width coordinate.
        half edgeFade = 1.0;
        if (_EdgeFadeoff > 0) {
            half distFromEdgeV = min(i.texcoord.y, 1.0 - i.texcoord.y);
            edgeFade = saturate(distFromEdgeV / _EdgeFadeoff);
        }

        half k = saturate(c.a * _Opacity * _AlphaMultiply * _SoftMultiply * edgeFade);
        c = lerp(1, c, k);
        c.a = saturate(c.a * _Opacity * _AlphaMultiply);
        return c;
      }
      ENDCG
    }
  }
}
}
