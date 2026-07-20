using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LIV.SDK.Unity
{
    public static class SDKShaders
    {

        public static readonly Color GREEN_COLOR = new Color(0f, 1f, 0f, 0.5f);
        public static readonly Color BLUE_COLOR = new Color(0f, 0f, 1f, 0.5f);
        public static readonly Color RED_COLOR = new Color(1f, 0f, 0f, 0.5f);

        public static readonly int LIV_COLOR = Shader.PropertyToID("_LivColor");
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

        private class ShaderCache
        {
            public ShaderCache(AssetBundle bundle)
            {

                try
                {
                    ClipPlaneSimple = bundle.LoadAsset<Shader>("LIV_ClipPlaneSimple");
                    ClipPlaneSimpleDebug = bundle.LoadAsset<Shader>("LIV_ClipPlaneSimpleDebug");
                    ClipPlaneComplex = bundle.LoadAsset<Shader>("LIV_ClipPlaneComplex");
                    ClipPlaneComplexDebug = bundle.LoadAsset<Shader>("LIV_ClipPlaneComplexDebug");
                    WriteOpaqueToAlpha = bundle.LoadAsset<Shader>("LIV_WriteOpaqueToAlpha");
                    CombineAlpha = bundle.LoadAsset<Shader>("LIV_CombineAlpha");
                    Write = bundle.LoadAsset<Shader>("LIV_Write");
                    ForceForwardRendering = bundle.LoadAsset<Shader>("LIV_ForceForwardRendering");

                    state = ClipPlaneSimple != null &&
                        ClipPlaneSimpleDebug != null &&
                        ClipPlaneComplex != null &&
                        ClipPlaneComplexDebug != null &&
                        WriteOpaqueToAlpha != null &&
                        CombineAlpha != null &&
                        Write != null &&
                        ForceForwardRendering != null;

                    if (!state)
                        Debug.LogError(string.Format("Failed to retreive at least one of the LIV shaders from {0}",
                            bundle.name));
                }
                catch (Exception e)
                {
                    Debug.LogError(string.Format("Failed to initialize the ShaderCache from asset bundle {0} : {1}",
                        bundle.name, e));
                    state = false;
                }
            }

            //Note, if we come up with *one more shader* to put in there, make a damn Dictonary
            public readonly Shader ClipPlaneSimple = null;
            public readonly Shader ClipPlaneSimpleDebug = null;
            public readonly Shader ClipPlaneComplex = null;
            public readonly Shader ClipPlaneComplexDebug = null;
            public readonly Shader WriteOpaqueToAlpha = null;
            public readonly Shader CombineAlpha = null;
            public readonly Shader Write = null;
            public readonly Shader ForceForwardRendering = null;
            public readonly bool state = false;
        }

        private static ShaderCache _shaderCache = null;


        public static bool LoadFromAssetBundle(AssetBundle bundle)
        {
            _shaderCache = new ShaderCache(bundle);
            return _shaderCache.state;
        }

        public static Shader GetShader(string name)
        {
            if (_shaderCache != null && _shaderCache.state)
                return GetShaderFromCache(name);
            return Shader.Find(name); //Attempt to find it in the player build instead
        }

        private static Shader GetShaderFromCache(string name)
        {
            switch (name)
            {
                default: return null;
                case LIV_CLIP_PLANE_SIMPLE_SHADER:
                    return _shaderCache.ClipPlaneSimple;
                case LIV_CLIP_PLANE_SIMPLE_DEBUG_SHADER:
                    return _shaderCache.ClipPlaneSimpleDebug;
                case LIV_CLIP_PLANE_COMPLEX_SHADER:
                    return _shaderCache.ClipPlaneComplex;
                case LIV_CLIP_PLANE_COMPLEX_DEBUG_SHADER:
                    return _shaderCache.ClipPlaneComplexDebug;
                case LIV_WRITE_OPAQUE_TO_ALPHA_SHADER:
                    return _shaderCache.WriteOpaqueToAlpha;
                case LIV_COMBINE_ALPHA_SHADER:
                    return _shaderCache.CombineAlpha;
                case LIV_WRITE_SHADER:
                    return _shaderCache.Write;
                case LIV_FORCE_FORWARD_RENDERING_SHADER:
                    return _shaderCache.ForceForwardRendering;
            }
        }


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
