// Copyright 2022 The Open Brush Authors
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
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint(
            "model.webimport",
            "Imports a model given a url or a filename in Media Library\\Models (Models loaded from a url are saved locally first)",
            "Andy\\Andy.obj"
        )]
        [ApiEndpoint(
            "import.webmodel",
            "Same as model.webimport (backwards compatibility for poly.pizza)",
            "Andy\\Andy.obj"
        )]
        public static void ImportWebModel(string url)
        {
            Uri uri;
            try { uri = new Uri(url); }
            catch (UriFormatException)
            { return; }
            var ext = uri.Segments.Last().Split('.').Last();

            // Is it a valid 3d model extension?
            if (ext != "off" && ext != "obj" && ext != "gltf" && ext != "glb" && ext != "fbx" && ext != "svg")
            {
                return;
            }
            var destinationPath = Path.Combine("Models", uri.Host);
            string filename = _DownloadMediaFileFromUrl(uri, destinationPath);
            // Very basic workaround for dependent files like .bin and textures
            // TODO
            // 1. Handle GLB
            // 2. Handle other formats
            // 3. Handle zip files
            // 4. Create subdirectories for each model
            if (ext == "gltf")
            {
                // Split the url into a base uri and the filename
                var baseUri = new Uri(uri, ".");

                var fullLocalPath = Path.Combine(App.ModelLibraryPath(), uri.Host);

                var jsonString = File.ReadAllText(Path.Combine(fullLocalPath, filename));
                JObject jsonObject = JObject.Parse(jsonString);
                List<string> externalFiles = jsonObject["buffers"].Select(j => j["uri"].Value<string>()).ToList();
                externalFiles.AddRange(jsonObject["images"].Select(j => j["uri"].Value<string>()).ToList());
                foreach (var externalFile in externalFiles)
                {
                    var newUri = new Uri(baseUri, externalFile);
                    var subdir = Path.GetDirectoryName(externalFile);
                    _DownloadMediaFileFromUrl(newUri, Path.Combine(fullLocalPath, subdir));
                }
            }
            ImportModel(Path.Combine(uri.Host, filename));
        }

        [ApiEndpoint(
            "model.icosaimport",
            "Imports a model from the Icosa Gallery given a model id",
            "9L2Lt-sxzdp"
        )]
        public static void ImportIcosaModel(string modelId)
        {
            ApiManager.Instance.LoadPolyModel(modelId);
        }

        [ApiEndpoint(
            "model.import",
            "Imports a model given an absolute path or a filename in Media Library\\Models (Models loaded from a url are saved locally first)",
            "Andy.glb"
        )]
        public static ModelWidget ImportModel(string location)
        {
            // Normalize path slashes
            location = location.Replace(@"\\", "/");
            location = location.Replace(@"//", "/");
            location = location.Replace(@"\", "/");

            var parts = location.Split("#");

            // At this point we've got a relative path to a file in Models
            string relativePath = parts[0];
            string subtree = null;
            if (parts.Length > 1)
            {
                subtree = location.Substring(relativePath.Length + 1);
            }
            var model = new Model(relativePath);

            AsyncHelpers.RunSync(() => model.LoadModelAsync());
            model.EnsureCollectorExists();
            var cmd = new CreateWidgetCommand(WidgetManager.m_Instance.ModelWidgetPrefab, _CurrentBrushTransform(), forceTransform: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            ModelWidget widget = cmd.Widget as ModelWidget;
            if (widget != null)
            {
                widget.Model = model;

                // Calculate proper size based on model bounds (same as normal model loading)
                float maxExtent = 2 * Mathf.Max(model.m_MeshBounds.extents.x,
                    Mathf.Max(model.m_MeshBounds.extents.y, model.m_MeshBounds.extents.z));
                float consistentSize;
                if (maxExtent == 0.0f)
                {
                    consistentSize = 1.0f;
                }
                else
                {
                    consistentSize = 0.25f * App.METERS_TO_UNITS / maxExtent;
                }

                widget.SetSignedWidgetSize(consistentSize);

                // Now enable preservation to prevent async overrides
                widget.SetPreserveCustomSize(true);
                widget.Subtree = subtree;
                widget.SyncHierarchyToSubtree();
                widget.Show(true);
                widget.AddSceneLightGizmos();
                cmd.SetWidgetCost(widget.GetTiltMeterCost());
            }
            else
            {
                Debug.LogWarning("Failed to create EditableModelWidget");
                return null;
            }

            WidgetManager.m_Instance.WidgetsDormant = false;
            SketchControlsScript.m_Instance.EatGazeObjectInput();
            SelectionManager.m_Instance.RemoveFromSelection(false);

            return widget;
        }

        [ApiEndpoint(
            "model.breakapart",
            "Breaks apart a model",
            "0"
        )]
        public static void BreakApartModel(int index)
        {
            var model = _GetActiveModel(index);
            var cmd = new BreakModelApartCommand(model);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }
    }
}
