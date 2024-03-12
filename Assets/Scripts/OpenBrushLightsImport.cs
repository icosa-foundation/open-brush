using GLTF.Schema;
using GLTF.Schema.KHR_lights_punctual;
using UnityEngine;
using LightType = UnityEngine.LightType;
namespace UnityGLTF.Plugins
{
    public class OpenBrushLightsImport : GLTFImportPlugin
    {
        public override string DisplayName => "Open Brush Lights";
        public override string Description => "Customized import behaviour for lights.";
        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
            return new OpenBrushLightsImportContext();
        }
    }

    public class OpenBrushLightsImportContext : GLTFImportPluginContext
    {
        public override void OnAfterImportNode(Node node, int nodeIndex, GameObject nodeObject)
        {
            base.OnAfterImportNode(node, nodeIndex, nodeObject);
            var light = nodeObject.GetComponent<Light>();
            var lightExtension = node.Extensions["KHR_lights_punctual"] as KHR_LightsPunctualNodeExtension;
            if (light != null && lightExtension != null)
            {
                var intensity = (float)lightExtension.LightId.Value.Intensity;
                var range = (float)lightExtension.LightId.Value.Range;
                if (range <= 0) range = float.MaxValue;

                switch (light.type)
                {
                    case LightType.Directional:
                        light.intensity = intensity * 0.01f;
                        break;
                    case LightType.Point:
                    case LightType.Spot:
                        light.range = range;
                        light.intensity = intensity * 0.0001f;
                        break;
                }
            }
        }
    }
}