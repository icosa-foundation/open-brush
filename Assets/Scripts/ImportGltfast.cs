using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            if (App.UserConfig.Import.UseUnityGltf)
            {
                return _StartSyncImportUnityGltf(localPath, assetLocation, model, warnings);
            }
            else
            {
                return _StartSyncImportGltfast(localPath, assetLocation, model, warnings);
            }
        }

        public static GameObject LegacyImporter(string localPath, string assetLocation)
        {
            var loader = new TiltBrushUriLoader(localPath, assetLocation, loadImages: false);

            var materialCollector = new ImportMaterialCollector(assetLocation, uniqueSeed: localPath);
            var importOptions = new GltfImportOptions
            {
                rescalingMode = GltfImportOptions.RescalingMode.CONVERT,
                scaleFactor = App.METERS_TO_UNITS,
                recenter = false
            };
            ImportGltf.GltfImportResult result = ImportGltf.Import(localPath, loader, materialCollector, importOptions);
            return LegacyImporter(localPath, assetLocation);
        }

        internal static async Task _StartSyncImportGltfast(
            string localPath,
            string assetLocation,
            Model model,
            List<string> warnings)
        {
            var gltf = new GltfImport();
            var importMaterialCollector = new ImportMaterialCollector(assetLocation, uniqueSeed: localPath);
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

            if (success)
            {
                var result = new GltfImportResult
                {
                    root = go,
                    materialCollector = importMaterialCollector
                };
            }
            else
            {
                // Fall back to the older import code
                go = LegacyImporter(localPath, assetLocation);
            }
            model.CalcBoundsGltf(go);
            model.EndCreatePrefab(go, warnings);
            if (success) model.AssignMaterialsToCollector(importMaterialCollector);
        }

        internal static async Task _StartSyncImportUnityGltf(
            string localPath,
            string assetLocation,
            Model model,
            List<string> warnings)
        {
            // try
            {
                var importMaterialCollector = new ImportMaterialCollector(assetLocation, uniqueSeed: localPath);

                ImportOptions options = new ImportOptions();
                GLTFSceneImporter gltf = new GLTFSceneImporter(localPath, options);
                gltf.IsMultithreaded = false;
                gltf.Timeout = 1000;
                AsyncHelpers.RunSync(() => gltf.LoadSceneAsync());
                GameObject go = gltf.CreatedObject;
                var result = new GltfImportResult
                {
                    root = go,
                    materialCollector = importMaterialCollector
                };
                // model.m_Valid = true;
                // model.AssignMaterialsToCollector(importMaterialCollector);
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            }
            // catch (Exception e)
            {
                // Debug.Log($"{e.Message}\n{e.StackTrace}");
                // Fall back to the older import code
                // GameObject go = LegacyImporter(localPath, assetLocation);
                // model.CalcBoundsGltf(go);
                // model.EndCreatePrefab(go, warnings);
            }
        }


    }
}
