using GLTF.Schema;
using GLTF.Schema.KHR_lights_punctual;
using UnityEngine;
using LightType = UnityEngine.LightType;
namespace UnityGLTF.Plugins
{
    public class OpenBrushLightsImport : GLTFImportPlugin
    {
        public override string DisplayName => "玲珑笔灯光";
        public override string Description => "灯光的自定义导入行为.";
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
            if (light == null ||
                node.Extensions == null ||
                !node.Extensions.ContainsKey("KHR_lights_punctual"))
            {
                return;
            }

            if (node.Extensions["KHR_lights_punctual"] is KHR_LightsPunctualNodeExtension lightExtension)
            {
                var intensity = (float)lightExtension.LightId.Value.Intensity;
                var range = (float)lightExtension.LightId.Value.Range;
                // Should be infinite but Unity doesn't support that.
                // float.MaxValue is too big and Unity interprets it as 0.
                if (range <= 0) range = 2e+5f;

                switch (light.type)
                {
                    case LightType.Directional:
                        light.intensity = intensity * 0.001f;
                        break;
                    case LightType.Point:
                        light.range = range;
                        light.intensity = intensity * 0.00001f;
                        break;
                    case LightType.Spot:
                        light.range = range;
                        light.intensity = intensity * 0.00001f;
                        break;
                }
            }
        }
    }
}