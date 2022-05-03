Shader "OvrAvatar/AvatarSurfaceShader" {
	Properties{
		// Global parameters
		_Alpha("Alpha", Range(0.0, 1.0)) = 1.0
		_DarkMultiplier("Dark Multiplier", Color) = (0.6, 0.6, 0.6, 1.0)
		_BaseColor("Base Color", Color) = (0.0, 0.0, 0.0, 0.0)
		_BaseMaskType("Base Mask Type", Int) = 0
		_BaseMaskParameters("Base Mask Parameters", Vector) = (0, 0, 0, 0)
		_BaseMaskAxis("Base Mask Axis", Vector) = (0, 1, 0, 0)
		_AlphaMask("Alpha Mask", 2D) = "white" {}
		_NormalMap("Normal Map", 2D) = "" {}
		_ParallaxMap("Parallax Map", 2D) = "" {}
		_RoughnessMap("Roughness Map", 2D) = "" {}

		// Layer 0 parameters
		_LayerSampleMode0("Layer Sample Mode 0", Int) = 0
		_LayerBlendMode0("Layer Blend Mode 0", Int) = 0
		_LayerMaskType0("Layer Mask Type 0", Int) = 0
		_LayerColor0("Layer Color 0", Color) = (1.0, 1.0, 1.0, 1.0)
		_LayerSurface0("Layer Surface 0", 2D) = "" {}
		_LayerSampleParameters0("Layer Sample Parameters 0", Vector) = (0, 0, 0, 0)
		_LayerMaskParameters0("Layer Mask Parameters 0", Vector) = (0, 0, 0, 0)
		_LayerMaskAxis0("Layer Mask Axis 0", Vector) = (0, 1, 0, 0)

		// Layer 1 parameters
		_LayerSampleMode1("Layer Sample Mode 1", Int) = 0
		_LayerBlendMode1("Layer Blend Mode 1", Int) = 0
		_LayerMaskType1("Layer Mask Type 1", Int) = 0
		_LayerColor1("Layer Color 1", Color) = (1.0, 1.0, 1.0, 1.0)
		_LayerSurface1("Layer Surface 1", 2D) = "" {}
		_LayerSampleParameters1("Layer Sample Parameters 1", Vector) = (0, 0, 0, 0)
		_LayerMaskParameters1("Layer Mask Parameters 1", Vector) = (0, 0, 0, 0)
		_LayerMaskAxis1("Layer Mask Axis 1", Vector) = (0, 1, 0, 0)

		// Layer 2 parameters
		_LayerSampleMode2("Layer Sample Mode 2", Int) = 0
		_LayerBlendMode2("Layer Blend Mode 2", Int) = 0
		_LayerMaskType2("Layer Mask Type 2", Int) = 0
		_LayerColor2("Layer Color 2", Color) = (1.0, 1.0, 1.0, 1.0)
		_LayerSurface2("Layer Surface 2", 2D) = "" {}
		_LayerSampleParameters2("Layer Sample Parameters 2", Vector) = (0, 0, 0, 0)
		_LayerMaskParameters2("Layer Mask Parameters 2", Vector) = (0, 0, 0, 0)
		_LayerMaskAxis2("Layer Mask Axis 2", Vector) = (0, 1, 0, 0)

		// Layer 3 parameters
		_LayerSampleMode3("Layer Sample Mode 3", Int) = 0
		_LayerBlendMode3("Layer Blend Mode 3", Int) = 0
		_LayerMaskType3("Layer Mask Type 3", Int) = 0
		_LayerColor3("Layer Color 3", Color) = (1.0, 1.0, 1.0, 1.0)
		_LayerSurface3("Layer Surface 3", 2D) = "" {}
		_LayerSampleParameters3("Layer Sample Parameters 3", Vector) = (0, 0, 0, 0)
		_LayerMaskParameters3("Layer Mask Parameters 3", Vector) = (0, 0, 0, 0)
		_LayerMaskAxis3("Layer Mask Axis 3", Vector) = (0, 1, 0, 0)

		// Layer 4 parameters
		_LayerSampleMode4("Layer Sample Mode 4", Int) = 0
		_LayerBlendMode4("Layer Blend Mode 4", Int) = 0
		_LayerMaskType4("Layer Mask Type 4", Int) = 0
		_LayerColor4("Layer Color 4", Color) = (1.0, 1.0, 1.0, 1.0)
		_LayerSurface4("Layer Surface 4", 2D) = "" {}
		_LayerSampleParameters4("Layer Sample Parameters 4", Vector) = (0, 0, 0, 0)
		_LayerMaskParameters4("Layer Mask Parameters 4", Vector) = (0, 0, 0, 0)
		_LayerMaskAxis4("Layer Mask Axis 4", Vector) = (0, 1, 0, 0)

		// Layer 5 parameters
		_LayerSampleMode5("Layer Sample Mode 5", Int) = 0
		_LayerBlendMode5("Layer Blend Mode 5", Int) = 0
		_LayerMaskType5("Layer Mask Type 5", Int) = 0
		_LayerColor5("Layer Color 5", Color) = (1.0, 1.0, 1.0, 1.0)
		_LayerSurface5("Layer Surface 5", 2D) = "" {}
		_LayerSampleParameters5("Layer Sample Parameters 5", Vector) = (0, 0, 0, 0)
		_LayerMaskParameters5("Layer Mask Parameters 5", Vector) = (0, 0, 0, 0)
		_LayerMaskAxis5("Layer Mask Axis 5", Vector) = (0, 1, 0, 0)

		// Layer 6 parameters
		_LayerSampleMode6("Layer Sample Mode 6", Int) = 0
		_LayerBlendMode6("Layer Blend Mode 6", Int) = 0
		_LayerMaskType6("Layer Mask Type 6", Int) = 0
		_LayerColor6("Layer Color 6", Color) = (1.0, 1.0, 1.0, 1.0)
		_LayerSurface6("Layer Surface 6", 2D) = "" {}
		_LayerSampleParameters6("Layer Sample Parameters 6", Vector) = (0, 0, 0, 0)
		_LayerMaskParameters6("Layer Mask Parameters 6", Vector) = (0, 0, 0, 0)
		_LayerMaskAxis6("Layer Mask Axis 6", Vector) = (0, 1, 0, 0)

		// Layer 7 parameters
		_LayerSampleMode7("Layer Sample Mode 7", Int) = 0
		_LayerBlendMode7("Layer Blend Mode 7", Int) = 0
		_LayerMaskType7("Layer Mask Type 7", Int) = 0
		_LayerColor7("Layer Color 7", Color) = (1.0, 1.0, 1.0, 1.0)
		_LayerSurface7("Layer Surface 7", 2D) = "" {}
		_LayerSampleParameters7("Layer Sample Parameters 7", Vector) = (0, 0, 0, 0)
		_LayerMaskParameters7("Layer Mask Parameters 7", Vector) = (0, 0, 0, 0)
		_LayerMaskAxis7("Layer Mask Axis 7", Vector) = (0, 1, 0, 0)
	}

	SubShader
	{
		Tags 
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
		}

		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		LOD 200

		Pass
		{
			Name "FORWARD"
			Tags
			{
				"LightMode" = "ForwardBase"
			}

			CGPROGRAM
			#pragma only_renderers d3d11 gles3 gles
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile PROJECTOR_OFF PROJECTOR_ON
			#pragma multi_compile NORMAL_MAP_OFF NORMAL_MAP_ON
			#pragma multi_compile PARALLAX_OFF PARALLAX_ON
			#pragma multi_compile ROUGHNESS_OFF ROUGHNESS_ON
			#pragma multi_compile VERTALPHA_OFF VERTALPHA_ON
			#pragma multi_compile LAYERS_1 LAYERS_2 LAYERS_3 LAYERS_4 LAYERS_5 LAYERS_6 LAYERS_7 LAYERS_8

			#include "Assets/Oculus/Avatar/Resources/Materials/AvatarMaterialStateShader.cginc"

			float4 frag(VertexOutput IN) : COLOR
			{
				return ComputeSurface(IN);
			}

			ENDCG
		}
	}

	FallBack "Diffuse"
	CustomEditor "AvatarMaterialEditor"
}
