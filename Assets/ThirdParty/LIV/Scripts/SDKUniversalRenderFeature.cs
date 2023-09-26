#if LIV_UNIVERSAL_RENDER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

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
        static List<SDKPass> passes = new List<SDKPass>();

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
            
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            while (passes.Count > 0)
            {
                renderer.EnqueuePass(passes[0]);
                passes.RemoveAt(0);
            }
        }
    }
}
#endif