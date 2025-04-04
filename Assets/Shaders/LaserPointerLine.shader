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

Shader "Custom/LaserPointerLine" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _ScrollSpeed("Scroll Speed", Float) = 1
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Tags { "Queue"="Transparent" "RenderType"="Transparent" }
    Blend One OneMinusSrcAlpha

    CGPROGRAM
    #pragma surface surf Lambert keepalpha

    struct Input {
      float2 uv_MainTex;
    };

    uniform float4 _Color;
    uniform half _ScrollSpeed;
    uniform half4 _EmissionColor;
    sampler2D _MainTex;

    void surf (Input IN, inout SurfaceOutput o) {
      
      o.Albedo = 0;

      // Compute animated alpha
      float alpha = (sin(IN.uv_MainTex.x + _Time.x * _ScrollSpeed) + 1.0f) * 0.5f;
      
      // Multiply the emission by alpha to output premultiplied color.
      o.Emission = _EmissionColor.xyz * 0.1f * alpha;
      o.Alpha = alpha;
    }
    ENDCG
  }
  FallBack "Diffuse"
}
