using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTF.Schema;
using GLTFast;
using TiltBrushToolkit;
using UnityEngine;
using UnityGLTF;

namespace TiltBrush
{
    internal class NewGltfImporter
    {
        public static ImportState BeginImport(string localPath)
        {
            var go = new GameObject();
            var gltf = go.AddComponent<GLTFast.GltfAsset>();
            gltf.Url = localPath;
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

            public void Dispose()
            {
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

        public static Task StartSyncImport(string localPath, string assetLocation, Model model, List<string> warnings)
        {
            return App.UserConfig.Import.UseUnityGltf ?
                _ImportUsingUnityGltf(localPath, assetLocation, model, warnings) :
                _ImportUsingGltfast(localPath, assetLocation, model, warnings);
        }

        private static GameObject _ImportUsingLegacyGltf(string localPath, string assetLocation)
        {
            var loader = new TiltBrushUriLoader(localPath, assetLocation, loadImages: false);
            var materialCollector = new ImportMaterialCollector(assetLocation, uniqueSeed: localPath);
            var importOptions = new GltfImportOptions
            {
                rescalingMode = GltfImportOptions.RescalingMode.CONVERT,
                scaleFactor = App.METERS_TO_UNITS,
                recenter = false
            };
            ImportGltf.Import(localPath, loader, materialCollector, importOptions);
            return _ImportUsingLegacyGltf(localPath, assetLocation);
        }

        private static async Task _ImportUsingGltfast(
            string localPath,
            string assetLocation,
            Model model,
            List<string> warnings)
        {
            var gltf = new GltfImport();
            bool success = await gltf.Load(localPath);
            var go = new GameObject();
            if (success)
            {
                var settings = new InstantiationSettings
                {
                    LightIntensityFactor = 0.1f,
                    // Mask = ~(ComponentType.Light | ComponentType.Camera)
                };
                success = await gltf.InstantiateMainSceneAsync(
                    new GameObjectInstantiator(gltf, go.transform, null, settings)
                );
            }

            if (!success)
            {
                var importMaterialCollector = new ImportMaterialCollector(assetLocation, uniqueSeed: localPath);
                model.AssignMaterialsToCollector(importMaterialCollector);
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            }
            else
            {
                // Fall back to the older import code
                go = _ImportUsingLegacyGltf(localPath, assetLocation);
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            }
        }

        private static async Task _ImportUsingUnityGltf(
            string localPath,
            string assetLocation,
            Model model,
            List<string> warnings)
        {
            try
            {
                ImportOptions options = new ImportOptions();
                GLTFSceneImporter gltf = new GLTFSceneImporter(localPath, options);

                gltf.IsMultithreaded = false;
                AsyncHelpers.RunSync(() => gltf.LoadSceneAsync());
                GameObject go = gltf.CreatedObject;
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            }
            catch (Exception e)
            {
                // Fall back to the older import code
                GameObject go = _ImportUsingLegacyGltf(localPath, assetLocation);
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            }
        }


    }
}
