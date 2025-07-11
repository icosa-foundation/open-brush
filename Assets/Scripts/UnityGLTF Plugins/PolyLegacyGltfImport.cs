using System.Text.RegularExpressions;
using GLTF.Schema;

namespace UnityGLTF.Plugins
{
    public class PolyLegacyGltfImport : GLTFImportPlugin
    {
        public override string DisplayName => "Poly Legacy Gltf Import Plugin";
        public override string Description => "Various fixes for legacy Poly GLTF files.";
        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
            return new PolyLegacyGltfImportContext();
        }
    }

    public class PolyLegacyGltfImportContext : GLTFImportPluginContext
    {
        public override void OnAfterImportRoot(GLTFRoot gltfRoot)
        {
            if (gltfRoot.Images == null)
            {
                return;
            }

            // Legacy urls of the form:
            // https://www.tiltbrush.com/shaders/brushes/[BrushName]-[BrushGuid]/[BrushName]-[BrushGuid]-v10.0-[Filename].png
            // example:
            // https://www.tiltbrush.com/shaders/brushes/LightWire-4391aaaa-df81-4396-9e33-31e4e4930b27/LightWire-4391aaaa-df81-4396-9e33-31e4e4930b27-v10.0-MainTex.png
            // should be nulled out as they 404
            // and anyway - are later replaced with the local brush material.
            Regex brushRegex = new Regex(@"https://www\.tiltbrush\.com/shaders/brushes/([^-]+)-([a-f0-9\-]+)/\1-\2-v10\.0-(.+)");

            foreach (var image in gltfRoot.Images)
            {
                if (image.Uri == null)  continue;

                var match = brushRegex.Match(image.Uri);
                if (match.Success)
                {
                    image.Uri = null;
                }
            }
        }
    }
}
