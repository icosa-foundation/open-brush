// Copyright 2024 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
            ImportGltf.GltfImportResult result = ImportGltf.Import(localPath, loader, materialCollector, importOptions);
            return result.root;
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
                    LightIntensityFactor = 0.0001f,
                    Mask = ~ComponentType.Camera
                };
                success = await gltf.InstantiateMainSceneAsync(
                    new GameObjectInstantiator(gltf, go.transform, null, settings)
                );
            }

            if (success)
            {
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            }
            else
            {
                Debug.LogError("Failed to import using GLTFast. Falling back to legacy import");
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
                Debug.LogError("Failed to import using UnityGltf. Falling back to legacy import");
                // Fall back to the older import code
                GameObject go = _ImportUsingLegacyGltf(localPath, assetLocation);
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            }
        }
    }
}
