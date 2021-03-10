using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LIV.SDK.Unity
{
    static class SDKShaders
    {
        public static readonly int LIV_COLOR_MASK = Shader.PropertyToID("_LivColorMask");
        public static readonly int LIV_TESSELLATION_PROPERTY = Shader.PropertyToID("_LivTessellation");
        public static readonly int LIV_CLIP_PLANE_HEIGHT_MAP_PROPERTY = Shader.PropertyToID("_LivClipPlaneHeightMap");

        public const string LIV_MR_FOREGROUND_KEYWORD = "LIV_MR_FOREGROUND";
        public const string LIV_MR_BACKGROUND_KEYWORD = "LIV_MR_BACKGROUND";
        public const string LIV_MR_KEYWORD = "LIV_MR";

        public const string LIV_CLIP_PLANE_SIMPLE_SHADER = "Hidden/LIV_ClipPlaneSimple";
        public const string LIV_CLIP_PLANE_SIMPLE_DEBUG_SHADER = "Hidden/LIV_ClipPlaneSimpleDebug";
        public const string LIV_CLIP_PLANE_COMPLEX_SHADER = "Hidden/LIV_ClipPlaneComplex";
        public const string LIV_CLIP_PLANE_COMPLEX_DEBUG_SHADER = "Hidden/LIV_ClipPlaneComplexDebug";
        public const string LIV_WRITE_OPAQUE_TO_ALPHA_SHADER = "Hidden/LIV_WriteOpaqueToAlpha";
        public const string LIV_COMBINE_ALPHA_SHADER = "Hidden/LIV_CombineAlpha";
        public const string LIV_WRITE_SHADER = "Hidden/LIV_Write";
        public const string LIV_FORCE_FORWARD_RENDERING_SHADER = "Hidden/LIV_ForceForwardRendering";

        public static void StartRendering()
        {
            Shader.EnableKeyword(LIV_MR_KEYWORD);
        }

        public static void StopRendering()
        {
            Shader.DisableKeyword(LIV_MR_KEYWORD);
        }

        public static void StartForegroundRendering()
        {
            Shader.EnableKeyword(LIV_MR_FOREGROUND_KEYWORD);
        }

        public static void StopForegroundRendering()
        {
            Shader.DisableKeyword(LIV_MR_FOREGROUND_KEYWORD);
        }

        public static void StartBackgroundRendering()
        {
            Shader.EnableKeyword(LIV_MR_BACKGROUND_KEYWORD);
        }

        public static void StopBackgroundRendering()
        {
            Shader.DisableKeyword(LIV_MR_BACKGROUND_KEYWORD);
        }
    }
}
