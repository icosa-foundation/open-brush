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

            if (light != null)
            {
                // Disable lights because we need to do more work on brush shaders
                // for additional lights to have any effect
                light.gameObject.SetActive(false);
            }

            if (light == null ||
                node.Extensions == null ||
                !node.Extensions.ContainsKey("KHR_lights_punctual"))
            {
                return;
            }

            // Remove lights automatically added to legacy Poly scenes by GOOGLE_lighting_rig
            // This assumes these extras indicate the presence of GOOGLE_lighting_rig
            // TODO Maybe we should check this on scene load and set a flag somewhere
            // there's a small chance of false positives.
            // TODO Should we destroy the node as well as the light component
            if (((bool?)node.Extras?["isHeadLight"] ?? false)
             || ((bool?)node.Extras?["isKeyLight"] ?? false))
            {
                Object.Destroy(light);
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
