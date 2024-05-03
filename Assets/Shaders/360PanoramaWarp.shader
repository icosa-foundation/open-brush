Shader "Custom/360PanoramaWarp"
{
    Properties
    {
        [MaterialToggle] _Stereoscopic("Stereoscopic",float) = 1.0
        _MainTex ("Texture", 2D) = "white" {}
        _WarpStart ("Warp Start Distance", Float) = 1.5
        _WarpEnd ("Warp End Distance", Float) = 0.5
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _Shininess ("Shininess", Float) = 20.0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float3 worldNormal : TEXCOORD0;
                float3 normal : NORMAL;
                float3 worldViewDir : TEXCOORD1;
                float warpAlpha : TEXCOORD2;
                float4 vertex : SV_POSITION;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _SpecularColor;
            float _Shininess;
            float _Stereoscopic;

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _WarpParams;
            float _WarpStart;
            float _WarpEnd;

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 zeroPos = mul(unity_ObjectToWorld, float4(0.0, 0.0, 0.0, 1.0));
                float distToZero = length(_WorldSpaceCameraPos - zeroPos.xyz);
                float dd = (distToZero - _WarpStart) / (_WarpEnd - _WarpStart);
                o.warpAlpha = clamp(dd, 0.0, 1.0);
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldViewDir = worldPos.xyz - _WorldSpaceCameraPos;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = mul((float3x3)unity_ObjectToWorld, v.normal);
                o.normal = v.normal;
                return o;
            }

            inline float2 ToRadialCoords(float3 coords)
            {
                float3 normalizedCoords = normalize(coords);
                float latitude = acos(normalizedCoords.y);
                float longitude = atan2(normalizedCoords.z, normalizedCoords.x);

                float2 sphereCoords;
                if (_Stereoscopic == 1)
                {
                    sphereCoords = float2(longitude, latitude) * float2(1.0 / UNITY_PI, 0.5 / UNITY_PI);
                    sphereCoords.y = fmod(sphereCoords.y * 2.0 + 1.0, 1.0) - 0.5;
                    return float2(sphereCoords.x * 0.5 + 0.5, 0.5 - sphereCoords.y);
                }
                else
                {
                    sphereCoords = float2(longitude, latitude) * 1.0 / UNITY_PI;
                    return float2(sphereCoords.x * 0.5 + 0.5, 1 - sphereCoords.y);
                }
            }

            float3 panoMap(float3 vdir, float4 _MainTex_ST)
            {
                float2 uv = ToRadialCoords(vdir);
                if (_Stereoscopic == 1)
                {
                    uv = uv * fixed2(1.0, 0.5) + fixed2(0, unity_StereoEyeIndex * 0.5);
                }

                uv.x *= _MainTex_ST.x + _MainTex_ST.z;
                uv.y *= _MainTex_ST.y + _MainTex_ST.w;
                return tex2D(_MainTex, uv, ddx(0), ddy(0));
            }


            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 nn = normalize(i.worldNormal);
                float3 ndir = normalize(i.worldViewDir);
                float3 sampleDir = i.warpAlpha * ndir + (1.0 - i.warpAlpha) * i.normal;
                float3 color = panoMap(sampleDir, _MainTex_ST);

                float3 lightDir = normalize(float3(-1.5, -1, -1));
                float3 reflectDir = reflect(-lightDir, nn);
                float spec = pow(max(dot(reflectDir, ndir), 0.0), _Shininess);
                float3 specular = _SpecularColor.rgb * spec;

                return float4(color + specular, 1.0);
            }
            ENDCG
        }
    }
}