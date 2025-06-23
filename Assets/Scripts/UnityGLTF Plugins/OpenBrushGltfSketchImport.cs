using System.Collections.Generic;
using GLTF.Schema;
using TiltBrush;
using UnityEngine;

namespace UnityGLTF.Plugins
{
    public class OpenBrushGltfSketchImport : GLTFImportPlugin
    {
        public override string DisplayName => "Open Brush Sketch Import";
        public override string Description => "Customized import behaviour for Open Brush sketches.";

        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
            return new OpenBrushGltfSketchImportContext(context);
        }
    }

    public class OpenBrushGltfSketchImportContext : GLTFImportPluginContext
    {
        private ImportMaterialCollector m_MaterialCollector;
        private Dictionary<int, GLTFTexture> m_GLTFTextures;
        private readonly GLTFImportContext m_Context;

        public OpenBrushGltfSketchImportContext(GLTFImportContext context)
        {
            m_Context = context;
        }

        public override void OnAfterImportRoot(GLTFRoot gltfRoot)
        {
            var uniqueSeed = m_Context.GetHashCode().ToString();
            m_MaterialCollector = new ImportMaterialCollector(uniqueSeed, uniqueSeed);
            m_GLTFTextures = new Dictionary<int, GLTFTexture>();
            base.OnAfterImportRoot(gltfRoot);
        }

        public override void OnBeforeImportScene(GLTFScene scene)
        {
            m_MaterialCollector = new ImportMaterialCollector(scene.Name, uniqueSeed: scene.GetHashCode().ToString());
            m_GLTFTextures = new Dictionary<int, GLTFTexture>();
            base.OnBeforeImportScene(scene);
        }

        public override void OnAfterImportTexture(GLTFTexture texture, int textureIndex, Texture textureObject)
        {
            m_GLTFTextures[textureIndex] = texture;
            base.OnAfterImportTexture(texture, textureIndex, textureObject);
        }

        public override void OnAfterImportMaterial(GLTFMaterial material, int materialIndex, Material materialObject)
        {
            m_MaterialCollector.Add(materialObject, material, m_GLTFTextures);
            base.OnAfterImportMaterial(material, materialIndex, materialObject);
        }
    }
}
