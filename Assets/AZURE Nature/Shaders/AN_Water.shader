// Made with Amplify Shader Editor v1.9.1.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Raygeas/AZURE Water"
{
	Properties
	{
		[NoScaleOffset][Normal][Header(Maps)][Space(7)]_WavesNormal("Waves Normal", 2D) = "white" {}
		[Header(Settings)][Space(5)]_Color1("Color 1", Color) = (0,0,0,0)
		_Color2("Color 2", Color) = (0,0,0,0)
		_Opacity("Opacity", Range( 0 , 1)) = 0
		_Smoothness("Smoothness", Range( 0 , 1)) = 0
		_WavesTile("Waves Tile", Float) = 1
		_WavesSpeed("Waves Speed", Range( 0 , 1)) = 0
		_WavesNormalIntensity("Waves Normal Intensity", Range( 0 , 2)) = 1
		_FoamContrast("Foam Contrast", Range( 0 , 1)) = 0
		_FoamDistance("Foam Distance", Range( 0 , 5)) = 0
		_FoamDensity("Foam Density", Range( 0.1 , 1)) = 0.5
		_DepthDistance("Depth Distance", Float) = 0
		_RefractionScale("Refraction Scale", Range( 0 , 1)) = 0.2
		_CoastOpacity("Coast Opacity", Range( 0 , 1)) = 0
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
		Cull Back
		GrabPass{ }
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityStandardUtils.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.0
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#pragma surface surf Standard alpha:fade keepalpha nodirlightmap vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
			float4 screenPos;
			float eyeDepth;
		};

		uniform sampler2D _WavesNormal;
		uniform float _WavesSpeed;
		uniform float _WavesTile;
		uniform float _WavesNormalIntensity;
		uniform float4 _Color2;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _DepthDistance;
		uniform float4 _Color1;
		ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
		uniform float _RefractionScale;
		uniform float _FoamDensity;
		uniform float _FoamContrast;
		uniform float _FoamDistance;
		uniform float _Smoothness;
		uniform float _Opacity;
		uniform float _CoastOpacity;


		inline float4 ASE_ComputeGrabScreenPos( float4 pos )
		{
			#if UNITY_UV_STARTS_AT_TOP
			float scale = -1.0;
			#else
			float scale = 1.0;
			#endif
			float4 o = pos;
			o.y = pos.w * 0.5f;
			o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
			return o;
		}


		float2 voronoihash61( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi61( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
		{
			float2 n = floor( v );
			float2 f = frac( v );
			float F1 = 8.0;
			float F2 = 8.0; float2 mg = 0;
			for ( int j = -1; j <= 1; j++ )
			{
				for ( int i = -1; i <= 1; i++ )
			 	{
			 		float2 g = float2( i, j );
			 		float2 o = voronoihash61( n + g );
					o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
					float d = 0.5 * dot( r, r );
			 //		if( d<F1 ) {
			 //			F2 = F1;
			 			float h = smoothstep(0.0, 1.0, 0.5 + 0.5 * (F1 - d) / smoothness); F1 = lerp(F1, d, h) - smoothness * h * (1.0 - h);mg = g; mr = r; id = o;
			 //		} else if( d<F2 ) {
			 //			F2 = d;
			
			 //		}
			 	}
			}
			return F1;
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			o.eyeDepth = -UnityObjectToViewPos( v.vertex.xyz ).z;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float temp_output_132_0 = ( _WavesSpeed * 0.1 );
			float mulTime43 = _Time.y * temp_output_132_0;
			float2 _Vector0 = float2(1,1);
			float3 ase_worldPos = i.worldPos;
			float2 appendResult106 = (float2(ase_worldPos.x , ase_worldPos.z));
			float2 WorldSpaceTile68 = ( appendResult106 * _WavesTile );
			float2 panner112 = ( mulTime43 * _Vector0 + WorldSpaceTile68);
			float2 panner114 = ( ( 1.0 - mulTime43 ) * _Vector0 + WorldSpaceTile68);
			float3 Normal89 = ( UnpackScaleNormal( tex2D( _WavesNormal, panner112 ), _WavesNormalIntensity ) + UnpackScaleNormal( tex2D( _WavesNormal, panner114 ), _WavesNormalIntensity ) );
			o.Normal = Normal89;
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float clampResult140 = clamp( _DepthDistance , 0.1 , 100.0 );
			float screenDepth83 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth83 = abs( ( screenDepth83 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( clampResult140 ) );
			float temp_output_364_0 = saturate( distanceDepth83 );
			float4 Depth158 = saturate( ( saturate( ( _Color2 * temp_output_364_0 ) ) + saturate( ( ( 1.0 - temp_output_364_0 ) * _Color1 ) ) ) );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			float4 screenColor341 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,ase_grabScreenPosNorm.xy);
			float4 temp_output_277_0 = ( ase_grabScreenPosNorm + float4( ( Normal89 * ( _RefractionScale * 0.1 ) ) , 0.0 ) );
			float4 screenColor274 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,temp_output_277_0.xy);
			float eyeDepth337 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, temp_output_277_0.xy ));
			float ifLocalVar336 = 0;
			if( eyeDepth337 > i.eyeDepth )
				ifLocalVar336 = 1.0;
			else if( eyeDepth337 < i.eyeDepth )
				ifLocalVar336 = 0.0;
			float4 lerpResult342 = lerp( screenColor341 , screenColor274 , ifLocalVar336);
			float4 Refractions282 = saturate( lerpResult342 );
			float4 lerpResult323 = lerp( Depth158 , Refractions282 , Depth158);
			float mulTime3 = _Time.y * ( temp_output_132_0 * 100 );
			float time61 = mulTime3;
			float2 voronoiSmoothId61 = 0;
			float voronoiSmooth61 = ( 1.0 - _FoamDensity );
			float2 coords61 = WorldSpaceTile68 * 50.0;
			float2 id61 = 0;
			float2 uv61 = 0;
			float voroi61 = voronoi61( coords61, time61, id61, uv61, voronoiSmooth61, voronoiSmoothId61 );
			float clampResult138 = clamp( _FoamContrast , 0.0 , 0.95 );
			float screenDepth17 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth17 = abs( ( screenDepth17 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _FoamDistance ) );
			float Foam183 = saturate( ( pow( saturate( voroi61 ) , ( 1.0 - clampResult138 ) ) + ( 1.0 - distanceDepth17 ) ) );
			o.Albedo = ( lerpResult323 + Foam183 ).rgb;
			o.Smoothness = _Smoothness;
			float screenDepth345 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth345 = abs( ( screenDepth345 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _CoastOpacity ) );
			o.Alpha = ( _Opacity * saturate( distanceDepth345 ) );
		}

		ENDCG
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=19108
Node;AmplifyShaderEditor.CommentaryNode;95;-2684.993,788.4299;Inherit;False;970.8306;441.1572;;5;68;107;106;100;66;World Space Tile;1,1,1,1;0;0
Node;AmplifyShaderEditor.WorldPosInputsNode;66;-2622.027,885.5659;Inherit;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;100;-2370.237,1084.813;Inherit;False;Property;_WavesTile;Waves Tile;5;0;Create;True;0;0;0;False;0;False;1;0.15;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;58;-5550.533,-993.7839;Inherit;False;Property;_WavesSpeed;Waves Speed;6;0;Create;True;0;0;0;False;0;False;0;0.1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;106;-2351.414,909.6379;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleNode;132;-5237.797,-987.9325;Inherit;False;0.1;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;107;-2154.367,987.3421;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;94;-4849.317,-741.4441;Inherit;False;2219.204;635.6195;;16;89;51;46;38;112;114;88;129;128;53;127;116;101;117;43;113;Normal;1,1,1,1;0;0
Node;AmplifyShaderEditor.Vector2Node;113;-4569.425,-456.7563;Inherit;False;Constant;_Vector0;Vector 0;15;0;Create;True;0;0;0;False;0;False;1,1;1,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode;68;-1976.508,982.7732;Inherit;False;WorldSpaceTile;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;43;-4790.86,-565.7832;Inherit;False;1;0;FLOAT;0.01;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;117;-4388.332,-353.4807;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.WireNode;116;-4387.03,-496.4804;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;101;-4360.976,-458.9648;Inherit;False;68;WorldSpaceTile;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode;53;-4566.438,-250.0213;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;127;-4095.972,-422.4999;Inherit;False;Property;_WavesNormalIntensity;Waves Normal Intensity;7;0;Create;True;0;0;0;False;0;False;1;0.15;0;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;112;-4087.049,-613.376;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;114;-4085.262,-297.6844;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.WireNode;128;-3822.972,-252.4999;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;129;-3822.972,-527.4999;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;46;-3435.147,-328.2323;Inherit;True;Property;_TextureSample1;Texture Sample 1;9;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;58a059b07b093a745b47c2191525ddce;True;0;True;bump;Auto;True;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;157;-4844.658,-37.79259;Inherit;False;2232.14;734.4948;;14;158;229;227;85;83;140;84;211;230;355;356;360;361;364;Depth;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;38;-3437.942,-644.7739;Inherit;True;Property;_TextureSample0;Texture Sample 0;8;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;58a059b07b093a745b47c2191525ddce;True;0;True;bump;Auto;True;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;84;-4815.066,324.6495;Inherit;False;Property;_DepthDistance;Depth Distance;11;0;Create;True;0;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;343;-4838.384,784.4971;Inherit;False;2098.784;833.2174;;16;344;276;282;302;342;336;341;274;338;340;339;337;277;278;273;281;Refraction;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;51;-3071.296,-480.7129;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;140;-4290.595,327.9685;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.1;False;2;FLOAT;100;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;276;-4805.279,1258.042;Inherit;False;Property;_RefractionScale;Refraction Scale;12;0;Create;True;0;0;0;False;0;False;0.2;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;89;-2874.999,-485.6454;Inherit;False;Normal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DepthFade;83;-4078.166,302.9496;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleNode;344;-4506.194,1262.309;Inherit;False;0.1;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;281;-4538.498,1133.13;Inherit;False;89;Normal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;179;-4846.875,-1463.306;Inherit;False;2068.099;656.4753;;18;183;188;186;19;6;64;17;136;18;61;138;135;3;7;5;130;131;63;Foam;1,1,1,1;0;0
Node;AmplifyShaderEditor.SaturateNode;364;-3817.542,312.7014;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleNode;131;-4702.088,-1301.849;Inherit;False;100;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;278;-4289.611,1180.191;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GrabScreenPosition;273;-4383.235,878.0894;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;63;-4793.106,-1121.849;Inherit;False;Property;_FoamDensity;Foam Density;10;0;Create;True;0;0;0;False;0;False;0.5;0.392;0.1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;7;-4281.874,-1121.359;Inherit;False;Property;_FoamContrast;Foam Contrast;8;0;Create;True;0;0;0;False;0;False;0;0.7;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;277;-4099.323,1062.857;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;130;-4546.644,-1387.98;Inherit;False;68;WorldSpaceTile;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode;229;-3759.916,397.9956;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;5;-4512.252,-1214.334;Inherit;False;Constant;_VoronoiScale;Voronoi Scale;7;0;Create;True;0;0;0;False;0;False;50;50;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;135;-4494.521,-1117.147;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;227;-3812.819,491.8506;Inherit;False;Property;_Color1;Color 1;1;0;Create;True;0;0;0;False;2;Header(Settings);Space(5);False;0,0,0,0;0,0.310371,0.6886792,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleTimeNode;3;-4513.123,-1301.942;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;85;-3816.418,58.36393;Inherit;False;Property;_Color2;Color 2;2;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0.7075471,0.7008876,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.VoronoiNode;61;-4247.94,-1289.851;Inherit;False;0;0;1;0;1;False;1;False;True;False;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;3;FLOAT;0;FLOAT;1;FLOAT2;2
Node;AmplifyShaderEditor.ScreenDepthNode;337;-3917.284,1216.882;Inherit;False;0;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;340;-3859.729,1506.948;Inherit;False;Constant;_Float2;Float 2;13;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SurfaceDepthNode;338;-3971.449,1312.356;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;355;-3555.943,169.053;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;339;-3860.057,1412.131;Inherit;False;Constant;_Float1;Float 1;13;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;138;-3956.907,-1116.104;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0.95;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;18;-4159.086,-947.4217;Inherit;False;Property;_FoamDistance;Foam Distance;9;0;Create;True;0;0;0;False;0;False;0;0.7;0;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;356;-3556.392,433.7633;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ScreenColorNode;341;-3674.766,875.5948;Inherit;False;Global;_GrabScreen1;Grab Screen 1;12;0;Create;True;0;0;0;False;0;False;Instance;274;False;False;False;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;360;-3372.392,213.7633;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;361;-3373.392,366.7633;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ConditionalIfNode;336;-3646.734,1324.909;Inherit;False;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;17;-3845.474,-967.9393;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;136;-3761.058,-1115.657;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenColorNode;274;-3653.063,1055.286;Inherit;False;Global;_GrabScreen0;Grab Screen 0;12;0;Create;True;0;0;0;False;0;False;Object;-1;False;False;False;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;64;-3746.012,-1290.282;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;19;-3574.874,-968.2001;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;342;-3368.581,1085.435;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.PowerNode;6;-3572.226,-1219.038;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;230;-3172.141,257.9694;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;186;-3371.75,-1105.804;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;211;-3009.974,257.9755;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;302;-3167.676,1085.865;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;158;-2825.167,253.1071;Inherit;False;Depth;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;188;-3187.473,-1107.528;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;282;-2980,1080.204;Inherit;False;Refractions;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;346;-2479.36,405.4789;Inherit;False;Property;_CoastOpacity;Coast Opacity;13;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;292;-2451.912,-44.12227;Inherit;False;282;Refractions;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;209;-2455.258,-201.4909;Inherit;False;158;Depth;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.DepthFade;345;-2159.36,388.4789;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;183;-3005.991,-1112.722;Inherit;False;Foam;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;194;-2001.092,274.3044;Inherit;False;Property;_Opacity;Opacity;3;0;Create;True;0;0;0;False;0;False;0;0.8;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;351;-1874.36,388.4789;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;191;-1929.629,42.13228;Inherit;False;183;Foam;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;323;-2093.611,-125.2519;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;25;-1655.177,-44.08315;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;16;-1799.64,175.3327;Inherit;False;Property;_Smoothness;Smoothness;4;0;Create;True;0;0;0;False;0;False;0;0.9;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;350;-1671.36,327.4789;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;103;-1708.893,78.12845;Inherit;False;89;Normal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-1434.705,11.53514;Float;False;True;-1;2;;0;0;Standard;Raygeas/AZURE Water;False;False;False;False;False;False;False;False;True;False;False;False;False;False;True;True;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Transparent;0.5;True;False;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.TexturePropertyNode;88;-3770.253,-469.6925;Inherit;True;Property;_WavesNormal;Waves Normal;0;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;2;Header(Maps);Space(7);False;None;58a059b07b093a745b47c2191525ddce;True;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
WireConnection;106;0;66;1
WireConnection;106;1;66;3
WireConnection;132;0;58;0
WireConnection;107;0;106;0
WireConnection;107;1;100;0
WireConnection;68;0;107;0
WireConnection;43;0;132;0
WireConnection;117;0;113;0
WireConnection;116;0;113;0
WireConnection;53;0;43;0
WireConnection;112;0;101;0
WireConnection;112;2;116;0
WireConnection;112;1;43;0
WireConnection;114;0;101;0
WireConnection;114;2;117;0
WireConnection;114;1;53;0
WireConnection;128;0;127;0
WireConnection;129;0;127;0
WireConnection;46;0;88;0
WireConnection;46;1;114;0
WireConnection;46;5;128;0
WireConnection;38;0;88;0
WireConnection;38;1;112;0
WireConnection;38;5;129;0
WireConnection;51;0;38;0
WireConnection;51;1;46;0
WireConnection;140;0;84;0
WireConnection;89;0;51;0
WireConnection;83;0;140;0
WireConnection;344;0;276;0
WireConnection;364;0;83;0
WireConnection;131;0;132;0
WireConnection;278;0;281;0
WireConnection;278;1;344;0
WireConnection;277;0;273;0
WireConnection;277;1;278;0
WireConnection;229;0;364;0
WireConnection;135;0;63;0
WireConnection;3;0;131;0
WireConnection;61;0;130;0
WireConnection;61;1;3;0
WireConnection;61;2;5;0
WireConnection;61;3;135;0
WireConnection;337;0;277;0
WireConnection;355;0;85;0
WireConnection;355;1;364;0
WireConnection;138;0;7;0
WireConnection;356;0;229;0
WireConnection;356;1;227;0
WireConnection;341;0;273;0
WireConnection;360;0;355;0
WireConnection;361;0;356;0
WireConnection;336;0;337;0
WireConnection;336;1;338;0
WireConnection;336;2;339;0
WireConnection;336;4;340;0
WireConnection;17;0;18;0
WireConnection;136;0;138;0
WireConnection;274;0;277;0
WireConnection;64;0;61;0
WireConnection;19;0;17;0
WireConnection;342;0;341;0
WireConnection;342;1;274;0
WireConnection;342;2;336;0
WireConnection;6;0;64;0
WireConnection;6;1;136;0
WireConnection;230;0;360;0
WireConnection;230;1;361;0
WireConnection;186;0;6;0
WireConnection;186;1;19;0
WireConnection;211;0;230;0
WireConnection;302;0;342;0
WireConnection;158;0;211;0
WireConnection;188;0;186;0
WireConnection;282;0;302;0
WireConnection;345;0;346;0
WireConnection;183;0;188;0
WireConnection;351;0;345;0
WireConnection;323;0;209;0
WireConnection;323;1;292;0
WireConnection;323;2;209;0
WireConnection;25;0;323;0
WireConnection;25;1;191;0
WireConnection;350;0;194;0
WireConnection;350;1;351;0
WireConnection;0;0;25;0
WireConnection;0;1;103;0
WireConnection;0;4;16;0
WireConnection;0;9;350;0
ASEEND*/
//CHKSM=7574DE453D00AE1BEE9022E15CBFC66093225461