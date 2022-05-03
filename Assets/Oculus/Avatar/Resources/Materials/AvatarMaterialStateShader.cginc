#ifndef AVATAR_UTIL_CG_INCLUDED
#define AVATAR_UTIL_CG_INCLUDED

#include "UnityCG.cginc"

#define SAMPLE_MODE_COLOR 0
#define SAMPLE_MODE_TEXTURE 1
#define SAMPLE_MODE_TEXTURE_SINGLE_CHANNEL 2
#define SAMPLE_MODE_PARALLAX 3
#define SAMPLE_MODE_RSRM 4

#define MASK_TYPE_NONE 0
#define MASK_TYPE_POSITIONAL 1
#define MASK_TYPE_REFLECTION 2
#define MASK_TYPE_FRESNEL 3
#define MASK_TYPE_PULSE 4

#define BLEND_MODE_ADD 0
#define BLEND_MODE_MULTIPLY 1

#ifdef LAYERS_1
#define LAYER_COUNT 1
#elif LAYERS_2
#define LAYER_COUNT 2
#elif LAYERS_3
#define LAYER_COUNT 3
#elif LAYERS_4
#define LAYER_COUNT 4
#elif LAYERS_5
#define LAYER_COUNT 5
#elif LAYERS_6
#define LAYER_COUNT 6
#elif LAYERS_7
#define LAYER_COUNT 7
#elif LAYERS_8
#define LAYER_COUNT 8
#endif

#define DECLARE_LAYER_UNIFORMS(index) \
		int _LayerSampleMode##index; \
		int _LayerBlendMode##index; \
		int _LayerMaskType##index; \
		fixed4 _LayerColor##index; \
		sampler2D _LayerSurface##index; \
		float4 _LayerSurface##index##_ST; \
		float4 _LayerSampleParameters##index; \
		float4 _LayerMaskParameters##index; \
		float4 _LayerMaskAxis##index;

DECLARE_LAYER_UNIFORMS(0)
DECLARE_LAYER_UNIFORMS(1)
DECLARE_LAYER_UNIFORMS(2)
DECLARE_LAYER_UNIFORMS(3)
DECLARE_LAYER_UNIFORMS(4)
DECLARE_LAYER_UNIFORMS(5)
DECLARE_LAYER_UNIFORMS(6)
DECLARE_LAYER_UNIFORMS(7)

struct VertexOutput 
{
	float4 pos : SV_POSITION;
	float2 texcoord : TEXCOORD0;
	float3 worldPos : TEXCOORD1;
	float3 worldNormal : TEXCOORD2;
	float3 viewDir : TEXCOORD3;
	float4 vertColor : COLOR;

#if NORMAL_MAP_ON || PARALLAX_ON
	float3 worldTangent : TANGENT;
	float3 worldBitangent : TEXCOORD5;
#endif
};

float _Alpha;
int _BaseMaskType;
float4 _BaseMaskParameters;
float4 _BaseMaskAxis;
fixed4 _DarkMultiplier;
fixed4 _BaseColor;
sampler2D _AlphaMask;
float4 _AlphaMask_ST;
sampler2D _AlphaMask2;
float4 _AlphaMask2_ST;
sampler2D _NormalMap;
float4 _NormalMap_ST;
sampler2D _ParallaxMap;
float4 _ParallaxMap_ST;
sampler2D _RoughnessMap;
float4 _RoughnessMap_ST;
float4x4 _ProjectorWorldToLocal;

VertexOutput vert(appdata_full v)
{
	VertexOutput o;
	UNITY_INITIALIZE_OUTPUT(VertexOutput, o);

	o.texcoord = v.texcoord.xy;
	o.worldPos = mul(unity_ObjectToWorld, v.vertex);
	o.vertColor = v.color;
	o.viewDir = normalize(_WorldSpaceCameraPos.xyz - o.worldPos);
	o.worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0.0)).xyz);

#if NORMAL_MAP_ON || PARALLAX_ON
	o.worldTangent = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0.0)).xyz);
	o.worldBitangent = normalize(cross(o.worldNormal, o.worldTangent) * v.tangent.w);
#endif

	o.pos = UnityObjectToClipPos(v.vertex);
	return o;
}

#ifndef NORMAL_MAP_ON
#define COMPUTE_NORMAL IN.worldNormal
#else
#define COMPUTE_NORMAL normalize(mul(lerp(float3(0, 0, 1), surfaceNormal, normalMapStrength), tangentTransform))
#endif

float3 ComputeColor(
	VertexOutput IN,
	float2 uv,
#if PARALLAX_ON || NORMAL_MAP_ON
	float3x3 tangentTransform,
#endif
#ifdef NORMAL_MAP_ON
	float3 surfaceNormal,
#endif
	sampler2D surface,
	float4 surface_ST,
	fixed4 color,
	int sampleMode,
	float4 sampleParameters
) {
	if (sampleMode == SAMPLE_MODE_TEXTURE) {
		float2 panning = _Time.g * sampleParameters.xy;
		return tex2D(surface, (uv + panning) * surface_ST.xy + surface_ST.zw).rgb * color.rgb;
	}
	else if (sampleMode == SAMPLE_MODE_TEXTURE_SINGLE_CHANNEL) {
		float4 channelMask = sampleParameters;
		float4 channels = tex2D(surface, uv * surface_ST.xy + surface_ST.zw);
		return dot(channels, channelMask) * color.rgb;
	}
#ifdef PARALLAX_ON
	else if (sampleMode == SAMPLE_MODE_PARALLAX) {
		float parallaxMinHeight = sampleParameters.x;
		float parallaxMaxHeight = sampleParameters.y;
		float parallaxValue = tex2D(_ParallaxMap, TRANSFORM_TEX(uv, _ParallaxMap)).r;
		float scaledHeight = lerp(parallaxMinHeight, parallaxMaxHeight, parallaxValue);
		float2 parallaxUV = mul(tangentTransform, IN.viewDir).xy * scaledHeight;
		return tex2D(surface, (uv * surface_ST.xy + surface_ST.zw) + parallaxUV).rgb * color.rgb;
	}
#endif
	else if (sampleMode == SAMPLE_MODE_RSRM) {
		float roughnessMin = sampleParameters.x;
		float roughnessMax = sampleParameters.y;
#ifdef ROUGHNESS_ON
		float roughnessValue = tex2D(_RoughnessMap, TRANSFORM_TEX(uv, _RoughnessMap)).r;
		float scaledRoughness = lerp(roughnessMin, roughnessMax, roughnessValue);
#else
		float scaledRoughness = roughnessMin;
#endif

#ifdef NORMAL_MAP_ON
		float normalMapStrength = sampleParameters.z;
#endif
		float3 viewReflect = reflect(-IN.viewDir, COMPUTE_NORMAL);
		float viewAngle = viewReflect.y * 0.5 + 0.5;
		return tex2D(surface, float2(scaledRoughness, viewAngle)).rgb * color.rgb;
	}
	return color.rgb;
}

float ComputeMask(
	VertexOutput IN,
#ifdef NORMAL_MAP_ON
	float3x3 tangentTransform,
	float3 surfaceNormal,
#endif
	int maskType,
	float4 layerParameters,
	float3 maskAxis
) {
	if (maskType == MASK_TYPE_POSITIONAL) {
		float centerDistance = layerParameters.x;
		float fadeAbove = layerParameters.y;
		float fadeBelow = layerParameters.z;
		float3 objPos = mul(unity_WorldToObject, float4(IN.worldPos, 1.0)).xyz;
		float d = dot(objPos, maskAxis);
		if (d > centerDistance) {
			return saturate(1.0 - (d - centerDistance) / fadeAbove);
		}
		else {
			return saturate(1.0 - (centerDistance - d) / fadeBelow);
		}
	}
	else if (maskType == MASK_TYPE_REFLECTION) {
		float fadeStart = layerParameters.x;
		float fadeEnd = layerParameters.y;
#ifdef NORMAL_MAP_ON
		float normalMapStrength = layerParameters.z;
#endif
		float power = layerParameters.w;
		float3 viewReflect = reflect(-IN.viewDir, COMPUTE_NORMAL);
		float d = max(0.0, dot(viewReflect, maskAxis));
		return saturate(1.0 - (d - fadeStart) / (fadeEnd - fadeStart));
	}
	else if (maskType == MASK_TYPE_FRESNEL) {
		float power = layerParameters.x;
		float fadeStart = layerParameters.y;
		float fadeEnd = layerParameters.z;
#ifdef NORMAL_MAP_ON
		float normalMapStrength = layerParameters.w;
#endif
		float d = saturate(1.0 - max(0.0, dot(IN.viewDir, COMPUTE_NORMAL)));
		float p = pow(d, power);
		return saturate(lerp(fadeStart, fadeEnd, p));
	}
	else if (maskType == MASK_TYPE_PULSE) {
		float distance = layerParameters.x;
		float speed = layerParameters.y;
		float power = layerParameters.z;
		float3 objPos = mul(unity_WorldToObject, float4(IN.worldPos, 1.0)).xyz;
		float d = dot(objPos, maskAxis);
		float theta = 6.2831 * frac((d - _Time.g * speed) / distance);
		return saturate(pow((sin(theta) * 0.5 + 0.5), power));
	}
	else {
		return 1.0;
	}
}

float3 ComputeBlend(float3 source, float3 blend, float mask, int blendMode) {
	if (blendMode == BLEND_MODE_MULTIPLY) {
		return source * (blend * mask);
	}
	else {
		return source + (blend * mask);
	}
}

float4 ComputeSurface(VertexOutput IN) 
{
#if PROJECTOR_ON
	float3 projectorPos = mul(_ProjectorWorldToLocal, float4(IN.worldPos, 1.0)).xyz;
	if (abs(projectorPos.x) > 1.0 || abs(projectorPos.y) > 1.0 || abs(projectorPos.z) > 1.0)
	{
		discard;
	}
	float2 uv = projectorPos.xy * 0.5 + 0.5;
#else
	float2 uv = IN.texcoord.xy;
#endif

	fixed4 c = _BaseColor;
	IN.worldNormal = normalize(IN.worldNormal);

#if PARALLAX_ON || NORMAL_MAP_ON
	float3x3 tangentTransform = float3x3(IN.worldTangent, IN.worldBitangent, IN.worldNormal);
#endif

#ifdef NORMAL_MAP_ON
	float3 surfaceNormal = UnpackNormal(tex2D(_NormalMap, TRANSFORM_TEX(uv, _NormalMap)));
#endif

#if PARALLAX_ON || NORMAL_MAP_ON
#ifndef NORMAL_MAP_ON
#define COLOR_INPUTS IN, uv, tangentTransform
#define MASK_INPUTS IN
#else
#define COLOR_INPUTS IN, uv, tangentTransform, surfaceNormal
#define MASK_INPUTS IN, tangentTransform, surfaceNormal
#endif
#else
#define COLOR_INPUTS IN, uv
#define MASK_INPUTS IN
#endif

#define LAYER_COLOR(index) ComputeColor(COLOR_INPUTS, _LayerSurface##index, _LayerSurface##index##_ST, _LayerColor##index, _LayerSampleMode##index, _LayerSampleParameters##index)
#define LAYER_MASK(index) ComputeMask(MASK_INPUTS, _LayerMaskType##index, _LayerMaskParameters##index, _LayerMaskAxis##index##.xyz)
#define LAYER_BLEND(index, c) ComputeBlend(c, LAYER_COLOR(index), LAYER_MASK(index), _LayerBlendMode##index)

	c.rgb = LAYER_BLEND(0, c.rgb);
#if LAYER_COUNT > 1
	c.rgb = LAYER_BLEND(1, c.rgb);
#endif
#if LAYER_COUNT > 2
	c.rgb = LAYER_BLEND(2, c.rgb);
#endif
#if LAYER_COUNT > 3
	c.rgb = LAYER_BLEND(3, c.rgb);
#endif
#if LAYER_COUNT > 4
	c.rgb = LAYER_BLEND(4, c.rgb);
#endif
#if LAYER_COUNT > 5
	c.rgb = LAYER_BLEND(5, c.rgb);
#endif
#if LAYER_COUNT > 6
	c.rgb = LAYER_BLEND(6, c.rgb);
#endif
#if LAYER_COUNT > 7
	c.rgb = LAYER_BLEND(7, c.rgb);
#endif

#ifdef VERTALPHA_ON
	float scaledValue = IN.vertColor.a * 2.0;
	float alpha0weight = max(0.0, 1.0 - scaledValue);
	float alpha2weight = max(0.0, scaledValue - 1.0);
	float alpha1weight = 1.0 - alpha0weight - alpha2weight;
	c.a = _Alpha * c.a * (tex2D(_AlphaMask, TRANSFORM_TEX(uv, _AlphaMask)).r * alpha1weight + tex2D(_AlphaMask2, TRANSFORM_TEX(uv, _AlphaMask2)).r * alpha2weight + alpha0weight) * ComputeMask(MASK_INPUTS, _BaseMaskType, _BaseMaskParameters, _BaseMaskAxis);
#else
	c.a = _Alpha * c.a * tex2D(_AlphaMask, TRANSFORM_TEX(uv, _AlphaMask)).r * IN.vertColor.a * ComputeMask(MASK_INPUTS, _BaseMaskType, _BaseMaskParameters, _BaseMaskAxis);
#endif
	c.rgb = lerp(c.rgb, c.rgb * _DarkMultiplier, IN.vertColor.r);

	return c;
}

#endif
