// WIP URP port scaffold for TiltBrush Standard Specular.
// Original shader retained at TiltBrushStandardSpecular.shader during migration.

Shader "TiltBrush/Standard (Specular setup)"
{
  Properties
  {
    _Color("Color", Color) = (1,1,1,1)
    _MainTex("Albedo", 2D) = "white" {}

    _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

    _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
    _GlossMapScale("Smoothness Factor", Range(0.0, 1.0)) = 1.0
    [Enum(Specular Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0

    _SpecColor("Specular", Color) = (0.2,0.2,0.2)
    _SpecGlossMap("Specular", 2D) = "white" {}
    [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
    [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

    _BumpScale("Scale", Float) = 1.0
    _BumpMap("Normal Map", 2D) = "bump" {}

    _Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
    _ParallaxMap ("Height Map", 2D) = "black" {}

    _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
    _OcclusionMap("Occlusion", 2D) = "white" {}

    _EmissionColor("Color", Color) = (0,0,0)
    _EmissionMap("Emission", 2D) = "white" {}

    _DetailMask("Detail Mask", 2D) = "white" {}
    _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
    _DetailNormalMapScale("Scale", Float) = 1.0
    _DetailNormalMap("Normal Map", 2D) = "bump" {}

    [Enum(UV0,0,UV1,1)] _UVSec ("UV Set for secondary textures", Float) = 0

    [HideInInspector] _Mode ("__mode", Float) = 0.0
    [HideInInspector] _SrcBlend ("__src", Float) = 1.0
    [HideInInspector] _DstBlend ("__dst", Float) = 0.0
    [HideInInspector] _ZWrite ("__zw", Float) = 1.0
  }

  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "PerformanceChecks"="False" }
    LOD 300

    Pass
    {
      Name "Forward"
      Tags { "LightMode"="UniversalForward" }
      Blend [_SrcBlend] [_DstBlend]
      ZWrite [_ZWrite]

      HLSLPROGRAM
      #pragma target 3.0
      #pragma vertex Vert
      #pragma fragment Frag
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
      #pragma multi_compile_fragment _ _SHADOWS_SOFT
      #pragma multi_compile_fog

      #pragma shader_feature _NORMALMAP
      #pragma shader_feature _EMISSION
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature ___ _DETAIL_MULX2
      #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      float4 ODS_EyeOffset;
      float4 ODS_CameraPos;
      float ODS_PoleCollapseAmount;

      float OdsCollapseIpd(float3 camOffset)
      {
        float3 vcam = float3(camOffset.x, 0.0, camOffset.z);
        float vcamLen = max(length(vcam), 1e-5);
        float camLen = max(length(camOffset), 1e-5);
        float d = dot(camOffset / camLen, vcam / vcamLen);
        float ang = acos(clamp(d, -1.0, 1.0));
        float t = saturate(ang / (1.57079632679 * 0.8));
        return sin(t / 6.28318530718) * ODS_PoleCollapseAmount;
      }

      void PrepForOds(inout float4 vertex)
      {
      #if defined(ODS_RENDER_CM) || defined(ODS_RENDER)
        float4 worldPos4 = mul(unity_ObjectToWorld, vertex);
        float3 worldPos = worldPos4.xyz;
        float3 camOffset = worldPos - _WorldSpaceCameraPos.xyz;
      #endif

      #if defined(ODS_RENDER_CM)
        if (dot(camOffset.xz, camOffset.xz) > 1e-6)
        {
          float3 worldUp = float3(0.0, 1.0, 0.0);
          float3 D = normalize(camOffset);
          float3 T = normalize(cross(D, worldUp));
          float collapse = OdsCollapseIpd(camOffset);
          float ipd = lerp(ODS_EyeOffset.x, 0.0, collapse);
          float d2 = max(dot(camOffset, camOffset), 1e-6);
          float a = ipd * ipd / d2;
          float b = ipd / d2 * sqrt(max(d2 * d2 - ipd * ipd, 0.0));
          float3 offset = -a * D + b * T;
          worldPos += offset;
        }
      #elif defined(ODS_RENDER)
        if (dot(camOffset.xz, camOffset.xz) > 1e-6)
        {
          float collapse = OdsCollapseIpd(camOffset);
          worldPos += lerp(float3(0.0, 0.0, 0.0), ODS_EyeOffset.xyz, collapse);
        }
      #endif

      #if defined(ODS_RENDER_CM) || defined(ODS_RENDER)
        vertex = mul(unity_WorldToObject, float4(worldPos, 1.0));
      #endif
      }

      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      TEXTURE2D(_SpecGlossMap); SAMPLER(sampler_SpecGlossMap);
      TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
      TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
      TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
      TEXTURE2D(_DetailAlbedoMap); SAMPLER(sampler_DetailAlbedoMap);

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      half4 _SpecColor;
      half4 _EmissionColor;
      half _Glossiness;
      half _GlossMapScale;
      half _Cutoff;
      half _BumpScale;
      half _OcclusionStrength;
      float4 _MainTex_ST;
      float4 _DetailAlbedoMap_ST;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv0 : TEXCOORD0;
        float2 uv1 : TEXCOORD1;
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float2 uvDetail : TEXCOORD1;
        float3 positionWS : TEXCOORD2;
        float3 normalWS : TEXCOORD3;
        float4 tangentWS : TEXCOORD4;
        half fogFactor : TEXCOORD5;
      };

      Varyings Vert(Attributes IN)
      {
        Varyings OUT;
        float4 posOS = IN.positionOS;
        PrepForOds(posOS);

        VertexPositionInputs pos = GetVertexPositionInputs(posOS.xyz);
        VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

        OUT.positionCS = pos.positionCS;
        OUT.positionWS = pos.positionWS;
        OUT.uv = TRANSFORM_TEX(IN.uv0, _MainTex);
        OUT.uvDetail = TRANSFORM_TEX(IN.uv0, _DetailAlbedoMap);
        OUT.normalWS = nrm.normalWS;
        OUT.tangentWS = float4(nrm.tangentWS.xyz, IN.tangentOS.w);
        OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target
      {
        half4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
        half alpha = albedoTex.a;

        #if defined(_ALPHATEST_ON)
        clip(alpha - _Cutoff);
        #endif

        half3 normalWS = normalize(IN.normalWS);
        #if defined(_NORMALMAP)
        half4 n = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv);
        half3 nTS = UnpackNormalScale(n, _BumpScale);
        half3 bitangent = cross(normalWS, normalize(IN.tangentWS.xyz)) * IN.tangentWS.w;
        half3x3 tbn = half3x3(normalize(IN.tangentWS.xyz), normalize(bitangent), normalWS);
        normalWS = normalize(mul(nTS, tbn));
        #endif

        Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
        half3 viewDir = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);
        half3 lightDir = normalize(mainLight.direction);

        half ndotl = saturate(dot(normalWS, lightDir));
        half3 diffuse = albedoTex.rgb * _Color.rgb * mainLight.color * mainLight.shadowAttenuation * ndotl;

        half smoothness = _Glossiness;
        half3 specularColor = _SpecColor.rgb;
        #if defined(_SPECGLOSSMAP)
        half4 sg = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, IN.uv);
        specularColor *= sg.rgb;
        smoothness *= sg.a * _GlossMapScale;
        #endif

        half3 h = normalize(lightDir + viewDir);
        half specPow = exp2(10.0h * smoothness + 1.0h);
        half specTerm = pow(saturate(dot(normalWS, h)), specPow);
        half3 spec = specularColor * specTerm * mainLight.color * mainLight.shadowAttenuation;

        half occlusion = 1.0h;
        if (_OcclusionStrength > 0.001h) {
          occlusion = lerp(1.0h, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, IN.uv).g, _OcclusionStrength);
        }

        half3 emission = 0;
        #if defined(_EMISSION)
        emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;
        #endif

        #if defined(_DETAIL_MULX2)
        half3 detail = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, IN.uvDetail).rgb;
        diffuse *= (detail * 2.0h);
        #endif

        half3 color = (diffuse + spec) * occlusion + emission;
        color = MixFog(color, IN.fogFactor);

        return half4(color, alpha);
      }
      ENDHLSL
    }

    Pass
    {
      Name "ShadowCaster"
      Tags { "LightMode"="ShadowCaster" }
      ZWrite On
      ZTest LEqual

      HLSLPROGRAM
      #pragma target 3.0
      #pragma vertex VertShadow
      #pragma fragment FragShadow
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma shader_feature _ _ALPHATEST_ON
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      float4 ODS_EyeOffset;
      float4 ODS_CameraPos;
      float ODS_PoleCollapseAmount;

      float OdsCollapseIpd(float3 camOffset)
      {
        float3 vcam = float3(camOffset.x, 0.0, camOffset.z);
        float vcamLen = max(length(vcam), 1e-5);
        float camLen = max(length(camOffset), 1e-5);
        float d = dot(camOffset / camLen, vcam / vcamLen);
        float ang = acos(clamp(d, -1.0, 1.0));
        float t = saturate(ang / (1.57079632679 * 0.8));
        return sin(t / 6.28318530718) * ODS_PoleCollapseAmount;
      }

      void PrepForOds(inout float4 vertex)
      {
      #if defined(ODS_RENDER_CM) || defined(ODS_RENDER)
        float4 worldPos4 = mul(unity_ObjectToWorld, vertex);
        float3 worldPos = worldPos4.xyz;
        float3 camOffset = worldPos - _WorldSpaceCameraPos.xyz;
      #endif

      #if defined(ODS_RENDER_CM)
        if (dot(camOffset.xz, camOffset.xz) > 1e-6)
        {
          float3 worldUp = float3(0.0, 1.0, 0.0);
          float3 D = normalize(camOffset);
          float3 T = normalize(cross(D, worldUp));
          float collapse = OdsCollapseIpd(camOffset);
          float ipd = lerp(ODS_EyeOffset.x, 0.0, collapse);
          float d2 = max(dot(camOffset, camOffset), 1e-6);
          float a = ipd * ipd / d2;
          float b = ipd / d2 * sqrt(max(d2 * d2 - ipd * ipd, 0.0));
          float3 offset = -a * D + b * T;
          worldPos += offset;
        }
      #elif defined(ODS_RENDER)
        if (dot(camOffset.xz, camOffset.xz) > 1e-6)
        {
          float collapse = OdsCollapseIpd(camOffset);
          worldPos += lerp(float3(0.0, 0.0, 0.0), ODS_EyeOffset.xyz, collapse);
        }
      #endif

      #if defined(ODS_RENDER_CM) || defined(ODS_RENDER)
        vertex = mul(unity_WorldToObject, float4(worldPos, 1.0));
      #endif
      }

      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial)
      half _Cutoff;
      float4 _MainTex_ST;
      CBUFFER_END

      struct Attributes { float4 positionOS:POSITION; float2 uv0:TEXCOORD0; };
      struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };

      Varyings VertShadow(Attributes IN)
      {
        Varyings OUT;
        float4 posOS = IN.positionOS;
        PrepForOds(posOS);
        OUT.positionCS = TransformObjectToHClip(posOS.xyz);
        OUT.uv = TRANSFORM_TEX(IN.uv0, _MainTex);
        return OUT;
      }

      half4 FragShadow(Varyings IN) : SV_Target
      {
        #if defined(_ALPHATEST_ON)
        half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
        clip(a - _Cutoff);
        #endif
        return 0;
      }
      ENDHLSL
    }

    Pass
    {
      Name "Selection"
      Tags { "LightMode"="SRPDefaultUnlit" }
      Blend OneMinusDstColor One
      ZWrite Off

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      float4 ODS_EyeOffset;
      float4 ODS_CameraPos;
      float ODS_PoleCollapseAmount;

      float OdsCollapseIpd(float3 camOffset)
      {
        float3 vcam = float3(camOffset.x, 0.0, camOffset.z);
        float vcamLen = max(length(vcam), 1e-5);
        float camLen = max(length(camOffset), 1e-5);
        float d = dot(camOffset / camLen, vcam / vcamLen);
        float ang = acos(clamp(d, -1.0, 1.0));
        float t = saturate(ang / (1.57079632679 * 0.8));
        return sin(t / 6.28318530718) * ODS_PoleCollapseAmount;
      }

      void PrepForOds(inout float4 vertex)
      {
      #if defined(ODS_RENDER_CM) || defined(ODS_RENDER)
        float4 worldPos4 = mul(unity_ObjectToWorld, vertex);
        float3 worldPos = worldPos4.xyz;
        float3 camOffset = worldPos - _WorldSpaceCameraPos.xyz;
      #endif

      #if defined(ODS_RENDER_CM)
        if (dot(camOffset.xz, camOffset.xz) > 1e-6)
        {
          float3 worldUp = float3(0.0, 1.0, 0.0);
          float3 D = normalize(camOffset);
          float3 T = normalize(cross(D, worldUp));
          float collapse = OdsCollapseIpd(camOffset);
          float ipd = lerp(ODS_EyeOffset.x, 0.0, collapse);
          float d2 = max(dot(camOffset, camOffset), 1e-6);
          float a = ipd * ipd / d2;
          float b = ipd / d2 * sqrt(max(d2 * d2 - ipd * ipd, 0.0));
          float3 offset = -a * D + b * T;
          worldPos += offset;
        }
      #elif defined(ODS_RENDER)
        if (dot(camOffset.xz, camOffset.xz) > 1e-6)
        {
          float collapse = OdsCollapseIpd(camOffset);
          worldPos += lerp(float3(0.0, 0.0, 0.0), ODS_EyeOffset.xyz, collapse);
        }
      #endif

      #if defined(ODS_RENDER_CM) || defined(ODS_RENDER)
        vertex = mul(unity_WorldToObject, float4(worldPos, 1.0));
      #endif
      }
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      struct appdata_t { float4 vertex : POSITION; };
      struct v2f { float4 pos : SV_POSITION; };

      v2f vert(appdata_t v)
      {
        v2f o;
      #if SELECTION_ON
        PrepForOds(v.vertex);
        o.pos = TransformObjectToHClip(v.vertex.xyz);
      #else
        o.pos = 0;
      #endif
        return o;
      }

      half4 frag(v2f i) : SV_Target
      {
        float4 c = float4(0,0,0,1);
        FRAG_MOBILESELECT(c)
        return c;
      }
      ENDHLSL
    }
  }

  FallBack Off
}

