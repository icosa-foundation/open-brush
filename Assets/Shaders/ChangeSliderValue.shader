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

Shader "Custom/ChangeSliderValue" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _BaseDiffuseColor ("Base Diffuse Color", Color) = (1,1,1,0)
    _MainTex ("Texture", 2D) = "white" {}
    _Shininess("Smoothness", Range(0.01, 1)) = 0.013
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Ratio ("Size Ratio", Float) = 1
  }
  SubShader {
    Tags { "RenderType"="Opaque" }
    LOD 100

    CGPROGRAM
    #pragma surface surf Standard nofog

    sampler2D _MainTex;
    half _Shininess;
    fixed4 _Color;
    fixed4 _BaseDiffuseColor;
    fixed4 _EmissionColor;
    float _Ratio;
    struct Input {
      float2 uv_MainTex;
      float3 worldPos;
    };

    void surf (Input IN, inout SurfaceOutputStandard o) {
      fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
      o.Smoothness = _Shininess;

      // Add slider bar
      if (lerp(.075, .925, IN.uv_MainTex.x) <= _Ratio)
      {
        c.rgb += c.a;
      }

      c *= _Color;
      o.Albedo = c + _BaseDiffuseColor;
      o.Emission = c * normalize(_EmissionColor);
      o.Alpha = 1;
    }
    ENDCG
  }
  FallBack "Diffuse"
}

