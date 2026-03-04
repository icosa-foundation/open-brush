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

Shader "Custom/OutlineMesh" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
  }
  SubShader {
    Tags {
      "RenderPipeline"="UniversalPipeline"
      "Queue"="Geometry"
      "IgnoreProjector"="True"
      "RenderType"="Opaque"
    }
    LOD 100

    Pass {
      Name "ForwardUnlit"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      half4 _Color;

      struct Attributes {
        float4 positionOS : POSITION;
        half4 color : COLOR;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
        half3 color : COLOR;
      };

      Varyings Vert(Attributes IN) {
        Varyings OUT;
        OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
        OUT.color = IN.color.rgb;
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target {
        return half4(_Color.rgb * IN.color, 1.0h);
      }
      ENDHLSL
    }
  }

  SubShader {
    Tags {"Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Opaque"}
    LOD 100

    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"

      fixed4 _Color;

      struct appdata {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
      };

      struct v2f {
        float4 pos : SV_POSITION;
        fixed3 color : COLOR;
      };

      v2f vert(appdata v) {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.color = v.color.rgb;
        return o;
      }

      fixed4 frag(v2f i) : SV_Target {
        return fixed4(_Color.rgb * i.color, 1.0);
      }
      ENDCG
    }
  }

  FallBack Off
}
