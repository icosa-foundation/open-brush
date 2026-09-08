// Copyright 2020 The Tilt Brush Authors
Shader "Custom/VisualizerStage" {
  Properties{
    _Color("Main Color", Color) = (1,1,1,1)
    _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
    _Shininess("Shininess", Range(0.01, 1)) = 0.078125
    _MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _BumpMap("Normalmap", 2D) = "bump" {}
    _EmissionGain("Emission Gain", Range(0, 1)) = 0.5
  }
  SubShader{
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      TEXTURE2D(_WaveFormTex); SAMPLER(sampler_WaveFormTex);

      CBUFFER_START(UnityPerMaterial)
      half4 _Color; half4 _SpecColor; half _Shininess; half _EmissionGain; float4 _MainTex_ST;
      CBUFFER_END

      float4 _BeatOutput;
      float4 _PeakBandLevels;

      float4 bloomColor(float4 color, float gain) {
        float cmin = length(color.rgb) * 0.05;
        color.rgb = max(color.rgb, float3(cmin, cmin, cmin));
        color = pow(color, 2.2);
        color.rgb *= 2 * exp(gain * 10);
        return color;
      }

      struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
      struct V { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };
      V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}

      half4 Frag(V i):SV_Target {
        int quant = 20;
        float index = floor(i.uv.x * quant) / quant;
        float4 wav = SAMPLE_TEXTURE2D(_WaveFormTex, sampler_WaveFormTex, float2(index, 0)) - 0.5;
        float4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
        wav = floor(wav * quant) / quant;

        float3 tint = _PeakBandLevels.x * float3(0.7,0,0.3) + _PeakBandLevels.z * float3(0.3,0,0.7) + _PeakBandLevels.w * 2 * float3(0,1,0);
        tint = normalize(tint + 1e-5);

        float4 c = abs(i.uv.y - 0.5) < wav.g ? float4(tint, 1) : 0;
        c.rgb *= mask.rgb;
        c.rgb = c.rgb * 0.5 + c.rgb * _BeatOutput.y;
        c.a = 1;

        float3 emission = bloomColor(c, _EmissionGain).rgb;
        float3 albedo = _Color.rgb * mask.rgb;
        return half4(albedo + emission + (_SpecColor.rgb * _Shininess * mask.a), 1);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
