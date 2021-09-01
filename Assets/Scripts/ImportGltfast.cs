using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using TiltBrushToolkit;
using UnityEngine;
namespace TiltBrush
{
    internal class ImportGltfast
    {
        public static ImportState BeginImport(string localPath)
        {
            var go = new GameObject();
            var gltf = go.AddComponent<GLTFast.GltfAsset>();
            gltf.url = localPath;
            var state = new ImportState(AxisConvention.kGltf2);
            return state;
        }
        public sealed class ImportState : IDisposable
        {
            // Change-of-basis matrix
            internal readonly Matrix4x4 gltfFromUnity;
            // Change-of-basis matrix
            internal readonly Matrix4x4 unityFromGltf;
            // The parsed gltf; filled in by BeginImport
            internal GameObject root;

            internal ImportState(AxisConvention axes)
            {
                gltfFromUnity = AxisConvention.GetFromUnity(axes);
                unityFromGltf = AxisConvention.GetToUnity(axes);
            }

            public void Dispose() {
                // if (root != null) { root.Dispose(); }
            }
        }

        internal class GltfImportResult
        {
            public GameObject root;
            public ImportMaterialCollector materialCollector;
        }
        
        public static GltfImportResult EndAsyncImport(ImportState state)
        {
            var result = new GltfImportResult();
            result.root = state.root;
            return result;
        }

        public static async void StartSyncImport(
            string localPath,
            ImportMaterialCollector importMaterialCollector,
            Model model,
            List<string> warnings)
        {
            
            var gltf = new GltfImport();
            var success = await gltf.Load(localPath);
            var go = new GameObject();
            if (success) {
                gltf.InstantiateMainScene(go.transform);
                var result = new GltfImportResult();
                result.root = go;
                result.materialCollector = importMaterialCollector;
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            } else {
                Debug.LogError("Loading glTF failed!");
            }
        }
    }
}
