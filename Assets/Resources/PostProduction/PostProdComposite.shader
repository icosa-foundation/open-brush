// Copyright 2026 The Open Brush Authors
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

// Post-production compositing preview (POSTPROD_PREVIEW_PLAN.md, stage 5). Implements
// exactly the formula already proven in make_composite_video.py:100-105 - written once
// here so preview and final render (stage 9) can't drift apart from each other or from
// the external Python reference pipeline:
//
//   visible = cgDepth > 0 && cgDepth < plateDepth   (strict, no epsilon, no median filter)
//   alpha   = max(cg.rgb) * visible                  (brightness-derived, gated by visible)
//   out     = cg * alpha + plate * (1 - alpha)
//
// _NaiveMode short-circuits the depth comparison (plate behind live CG, gated only on
// "does the CG have any geometry here") for quick alignment checks without needing
// depth loaded - same shader, same alpha derivation, just no plateDepth comparison.
// (recompile check 2026-10-20)
Shader "Hidden/PostProdComposite"
{
    Properties
    {
        _CGColorTex ("CG Color", 2D) = "black" {}
        _CGDepthTex ("CG Depth (metres, single channel, 0 = no geometry)", 2D) = "black" {}
        _PlateColorTex ("Plate Color", 2D) = "black" {}
        _PlateDepthTex ("Plate Depth (metres, single channel)", 2D) = "black" {}
        _NaiveMode ("Naive Mode (no depth test)", Float) = 0
        // 1 = plate depth from the nearest texel (never blended); 0 = bilinear.
        _DepthNearest ("Plate Depth Nearest", Float) = 1
        // 1 = _CGDepthTex is Unity DepthNormals-encoded (live CG path,
        // PostProductionLiveCg): ba = EncodeFloatRG(linear 0-1 depth), b == 1 && a == 0
        // means nothing rendered. Decoded here exactly as ScreenshotManager decodes it on
        // the CPU for the snapshot EXR. 0 = metres in .r (snapshot path).
        _CGDepthEncoded ("CG depth is DepthNormals-encoded", Float) = 0
        _CGFarClipMetres ("CG far clip (metres), for decoding encoded depth", Float) = 100
        // 1 = the output is the undistorted, overscanned working frame (PostProductionLens):
        // each output pixel samples the raw plate where the real lens put it.
        _UndistortPlate ("Undistort plate", Float) = 0
        // 1 = the output is the original (distorted) camera frame, for export matching the
        // real lens: the plate is used as is and the CG, rendered in the undistorted working
        // frame, is sampled where each output pixel lies once undistorted. Never set together
        // with _UndistortPlate.
        _DistortCG ("Distort CG into the original lens", Float) = 0
        // TEMP DEBUG (stage 5 bring-up): 0 = normal composite, 1 = depth/colour
        // sampling-alignment visualization - red = CG depth texture says there's
        // geometry here, green = CG colour texture actually shows a bright stroke
        // here, sampled at the SAME UV through the real GPU pipeline. Yellow means
        // they agree; red-only or green-only means the two textures' UV mapping is
        // misaligned (e.g. one flipped relative to the other).
        _DebugMode ("Debug Mode", Float) = 0
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _CGColorTex;
            sampler2D _CGDepthTex;
            sampler2D _PlateColorTex;
            sampler2D _PlateDepthTex;
            float _NaiveMode;
            float _DepthNearest;
            float _DebugMode;
            float _CGDepthEncoded;
            float _CGFarClipMetres;
            float _UndistortPlate;
            float _DistortCG;
            float4 _WorkK;     // fx, fy, cx, cy - undistorted working frame, pixels
            float4 _WorkSize;  // width, height
            float4 _SrcK;      // fx, fy, cx, cy - real camera calibration, pixels
            float4 _SrcSize;   // width, height
            float4 _Dist0;     // k1, k2, p1, p2
            float _Dist1;      // k3
            float4 _PlateDepthTex_TexelSize;

            // Working-frame UV -> raw plate UV through the OpenCV distortion model (the
            // forward model, so no iteration). Image rows run top-down; UVs bottom-up.
            float2 PlateUV(float2 uv, out float inside)
            {
                float u = uv.x * _WorkSize.x - 0.5;
                float v = (1.0 - uv.y) * _WorkSize.y - 0.5;
                float x = (u - _WorkK.z) / _WorkK.x;
                float y = (v - _WorkK.w) / _WorkK.y;
                float r2 = x * x + y * y;
                float radial = 1.0 + r2 * (_Dist0.x + r2 * (_Dist0.y + r2 * _Dist1));
                float xd = x * radial + 2.0 * _Dist0.z * x * y + _Dist0.w * (r2 + 2.0 * x * x);
                float yd = y * radial + _Dist0.z * (r2 + 2.0 * y * y) + 2.0 * _Dist0.w * x * y;
                float us = _SrcK.x * xd + _SrcK.z;
                float vs = _SrcK.y * yd + _SrcK.w;
                float2 suv = float2((us + 0.5) / _SrcSize.x, 1.0 - (vs + 0.5) / _SrcSize.y);
                inside = (suv.x >= 0.0 && suv.x <= 1.0 && suv.y >= 0.0 && suv.y <= 1.0) ? 1.0 : 0.0;
                return suv;
            }

            // Raw (distorted) frame UV -> working-frame UV: the inverse of PlateUV, by the
            // same fixed-point iteration as PostProductionLens.UndistortNormalized (and
            // cv2.undistortPoints).
            float2 WorkUV(float2 uv, out float inside)
            {
                float u = uv.x * _SrcSize.x - 0.5;
                float v = (1.0 - uv.y) * _SrcSize.y - 0.5;
                float xd = (u - _SrcK.z) / _SrcK.x;
                float yd = (v - _SrcK.w) / _SrcK.y;
                float x = xd;
                float y = yd;
                for (int it = 0; it < 20; it++)
                {
                    float r2 = x * x + y * y;
                    float icdist = 1.0 / (1.0 + ((_Dist1 * r2 + _Dist0.y) * r2 + _Dist0.x) * r2);
                    float deltaX = 2.0 * _Dist0.z * x * y + _Dist0.w * (r2 + 2.0 * x * x);
                    float deltaY = _Dist0.z * (r2 + 2.0 * y * y) + 2.0 * _Dist0.w * x * y;
                    x = (xd - deltaX) * icdist;
                    y = (yd - deltaY) * icdist;
                }
                float uw = _WorkK.x * x + _WorkK.z;
                float vw = _WorkK.y * y + _WorkK.w;
                float2 wuv = float2((uw + 0.5) / _WorkSize.x, 1.0 - (vw + 0.5) / _WorkSize.y);
                inside = (wuv.x >= 0.0 && wuv.x <= 1.0 && wuv.y >= 0.0 && wuv.y <= 1.0) ? 1.0 : 0.0;
                return wuv;
            }

            // Plate depth in metres. Nearest by default: interpolating across a depth edge
            // invents in-between depths that change what is occluded. Bilinear (the texture's
            // own filtering) suits depth that was upscaled smoothly.
            float SamplePlateDepth(float2 uv)
            {
                if (_DepthNearest > 0.5)
                {
                    uv = (floor(uv * _PlateDepthTex_TexelSize.zw) + 0.5) * _PlateDepthTex_TexelSize.xy;
                }
                return tex2D(_PlateDepthTex, uv).r;
            }

            float SampleCgDepthMetres(float2 uv)
            {
                float4 enc = tex2D(_CGDepthTex, uv);
                if (_CGDepthEncoded < 0.5)
                {
                    return enc.r;
                }
                // Byte-exact "nothing rendered" test (b == 255, a == 0), with half-step
                // tolerance for 8-bit storage.
                if (enc.b > 254.5 / 255.0 && enc.a < 0.5 / 255.0)
                {
                    return 0.0;
                }
                return DecodeFloatRG(enc.ba) * _CGFarClipMetres;
            }

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 cgUv = i.uv;
                float cgInside = 1.0;
                if (_DistortCG > 0.5)
                {
                    cgUv = WorkUV(i.uv, cgInside);
                }
                // CG colour is sampled bilinearly (the export passes a resolved, bilinear
                // copy); encoded depth is point-filtered, so it is never blended.
                fixed4 cg = tex2D(_CGColorTex, cgUv) * cgInside;
                float cgDepth = cgInside > 0.5 ? SampleCgDepthMetres(cgUv) : 0.0;

                if (_DebugMode > 0.5)
                {
                    float hasDepth = cgDepth > 0.0 ? 1.0 : 0.0;
                    float hasColor = max(max(cg.r, cg.g), cg.b) > 0.1 ? 1.0 : 0.0;
                    return fixed4(hasDepth, hasColor, 0.0, 1.0);
                }

                fixed4 plate;
                float plateDepth;
                if (_UndistortPlate > 0.5)
                {
                    float inside;
                    float2 suv = PlateUV(i.uv, inside);
                    plate = tex2D(_PlateColorTex, suv) * inside;
                    // Outside the real frame there is no plate, so nothing occludes the CG.
                    plateDepth = inside > 0.5 ? SamplePlateDepth(suv) : 1.0e6;
                }
                else
                {
                    plate = tex2D(_PlateColorTex, i.uv);
                    plateDepth = SamplePlateDepth(i.uv);
                }

                float visible;
                if (_NaiveMode > 0.5)
                {
                    visible = cgDepth > 0.0 ? 1.0 : 0.0;
                }
                else
                {
                    visible = (cgDepth > 0.0 && cgDepth < plateDepth) ? 1.0 : 0.0;
                }

                float alpha = max(max(cg.r, cg.g), cg.b) * visible;
                fixed4 outColor = cg * alpha + plate * (1.0 - alpha);
                outColor.a = 1.0;
                return outColor;
            }
            ENDCG
        }
    }
}
