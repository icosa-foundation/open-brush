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

// Clamps input to LDR (so alpha blending works better)
// alpha-composites premultiplied-alpha overlay
// then composites a premultiplied-alpha texture onto
// the source.
Shader "Custom/BlitLdrPmaOverlay" {
  Properties {
    _MainTex ("", 2D) = "white" {}
    _BlitTexture ("", 2D) = "white" {}
    _OverlayTex ("Overlay Texture", 2D) = "black" {}
    _OverlayUvRange  ("Overlay UV Range", Vector) = (0, 0, 1, 1)
  }

  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" }
    ZTest Off Cull Off ZWrite Off Fog { Mode Off }
    Blend Off

    Pass{
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

      TEXTURE2D(_OverlayTex);
      SAMPLER(sampler_OverlayTex);
      float4 _OverlayUvRange;

      float4 frag(Varyings i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        // Get the original color.
        float4 mainTex = SAMPLE_TEXTURE2D_X_LOD(
          _BlitTexture,
          sampler_LinearClamp,
          i.texcoord,
          _BlitMipLevel);

        // Calculate the overlay's texture coordinates.
        float2 uvMin = _OverlayUvRange.xy;
        float2 uvMax = _OverlayUvRange.zw;
        float2 uvSize = uvMax - uvMin;
        float2 overlayUV = saturate((i.texcoord - uvMin) / uvSize);

        // Get the overlay color.
        float4 overlayTex = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, overlayUV);

        // Composite the result.
        return (1.0f - overlayTex.a) * saturate(mainTex) + overlayTex;
      }
      ENDHLSL
    }
  }
}
