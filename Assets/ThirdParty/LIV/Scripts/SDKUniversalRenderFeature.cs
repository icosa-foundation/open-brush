#if LIV_UNIVERSAL_RENDER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace LIV.SDK.Unity
{
    public class SDKPass : ScriptableRenderPass
    {
        public CommandBuffer commandBuffer;

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            context.ExecuteCommandBuffer(commandBuffer);
        }
    }

    public class SDKUniversalRenderFeature : ScriptableRendererFeature
    {
        private const string FILE_NAME = "LIVUniversalRenderFeature";
        static List<SDKPass> passes = new List<SDKPass>();
        private bool _logAddRenderPasses = true;

        public static void AddPass(SDKPass pass)
        {
            passes.Add(pass);
        }

        public static void ClearPasses()
        {
            passes.Clear();
        }

        public override void Create()
        {
            _logAddRenderPasses = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            while (passes.Count > 0)
            {
                renderer.EnqueuePass(passes[0]);
                passes.RemoveAt(0);
            }

            if (_logAddRenderPasses)
            {
                Debug.Log("LIV URP Render Feature: Universal Render Pipeline Added Render Passes.");
                _logAddRenderPasses = false;
            }
        }


        private static SDKUniversalRenderFeature _instance;

        public static SDKUniversalRenderFeature instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<SDKUniversalRenderFeature>(FILE_NAME);

                if (_instance == null)
                {
                    _instance = ScriptableObject.CreateInstance<SDKUniversalRenderFeature>();
                    _instance.name = nameof(SDKUniversalRenderFeature);
                }

                return _instance;
            }
        }
    }
}
#endif