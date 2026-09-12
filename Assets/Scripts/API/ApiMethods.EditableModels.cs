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
using System.Threading.Tasks;
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
            // A glTF owns an isolated directory so dependency names need no rewriting
            // and one folder-picker continuation can publish the whole dependency tree.
            string modelDirectory = ext == "gltf"
                ? Path.Combine(uri.Host, $"import-{Guid.NewGuid():N}") : uri.Host;
            string fullLocalPath = GetSafeRelativePathInDirectory(
                App.ModelLibraryPath(), modelDirectory, "model import directory");
            string filename = _DownloadMediaFileFromUrlToDirectory(
                uri, fullLocalPath, allowRedirects: true, publish: ext != "gltf");
            if (filename == null) { return; }
            if (ext == "gltf")
            {
                var baseUri = new Uri(uri, ".");
                var jsonString = File.ReadAllText(Path.Combine(fullLocalPath, filename));
                JObject jsonObject = JObject.Parse(jsonString);
                IEnumerable<string> externalFiles = GetGltfExternalFiles(jsonObject);
                foreach (var externalFile in externalFiles)
                {
                    var newUri = new Uri(baseUri, externalFile);
                    if (newUri.Scheme != Uri.UriSchemeHttp && newUri.Scheme != Uri.UriSchemeHttps)
                    {
                        throw new ArgumentException($"Unsupported model dependency URI: {externalFile}");
                    }
                    string dependencyPath = GetSafeRelativePathInDirectory(fullLocalPath,
                        Uri.UnescapeDataString(externalFile), "model dependency path");
                    if (string.Equals(dependencyPath, Path.Combine(fullLocalPath, filename),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException("A glTF dependency cannot replace its model file.");
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(dependencyPath));
                    // Preserve the exact URI-relative filename, including .bin buffers.
                    using var client = new System.Net.WebClient();
                    client.Headers.Add("user-agent", ApiManager.WebRequestUserAgent);
                    client.DownloadFile(newUri, dependencyPath);
                }
                _PublishApiMediaLibraryPathToSharedStorage(fullLocalPath);
            }
            ImportModel(Path.Combine(modelDirectory, filename));
        }

        internal static IEnumerable<string> GetGltfExternalFiles(JObject gltf)
        {
            return new[] { "buffers", "images" }
                .SelectMany(property => gltf[property] as JArray ?? new JArray())
                .Select(item => item["uri"]?.Value<string>())
                .Where(uri => !string.IsNullOrEmpty(uri) &&
                    !uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal);
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

        private static async Task SetupWidgetAfterLoadAsync(Model model, ModelWidget widget, string subtree, CreateWidgetCommand cmd)
        {
            bool assignedToWidget = false;
            try
            {
                await model.LoadModelAsync();
                model.EnsureCollectorExists();

                // Now assign the model, which triggers LoadModel() in the widget
                // This must happen after the model is loaded (m_ModelParent is set)
                widget.Model = model;
                assignedToWidget = true;
                model.ReleaseFromCatalog();

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
                widget.AddSceneLightGizmos();
                cmd.SetWidgetCost(widget.GetTiltMeterCost());
            }
            finally
            {
                if (!assignedToWidget)
                {
                    model.ReleaseFromCatalog();
                }
            }
        }

        [ApiEndpoint(
            "model.import",
            "Imports a model given a filename in Media Library\\Models (Models loaded from a url are saved locally first)",
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
            GetSafeRelativePathInDirectory(
                App.ModelLibraryPath(), relativePath, "model path");
            string subtree = null;
            if (parts.Length > 1)
            {
                subtree = location.Substring(relativePath.Length + 1);
            }
            var model = new Model(relativePath);

            var cmd = new CreateWidgetCommand(WidgetManager.m_Instance.ModelWidgetPrefab, _CurrentBrushTransform(), forceTransform: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            ModelWidget widget = cmd.Widget as ModelWidget;
            if (widget != null)
            {
                // Start async load and setup widget when complete (fire-and-forget)
                // Model assignment happens in SetupWidgetAfterLoadAsync after loading completes
                _ = SetupWidgetAfterLoadAsync(model, widget, subtree, cmd);

                widget.Show(true);
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
