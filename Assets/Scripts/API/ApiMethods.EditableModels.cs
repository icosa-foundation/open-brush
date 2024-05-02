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
using System.IO;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("model.webimport", "Imports a model given a url or a filename in Media Library\\Models (Models loaded from a url are saved locally first)")]
        [ApiEndpoint("import.webmodel", "Same as model.webimport (backwards compatibility for poly.pizza)")]
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
            ImportModel(Path.Combine(uri.Host, filename));
        }

        [ApiEndpoint("model.import", "Imports a model given a url or a filename in Media Library\\Models (Models loaded from a url are saved locally first)")]
        public static void ImportModel(string location)
        {
            if (location.StartsWith("poly:"))
            {
                location = location.Substring(5);
                ApiManager.Instance.LoadPolyModel(location);
                return; // TODO
            }

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
            var tr = _CurrentTransform().TransformBy(Coords.CanvasPose);
            var model = new Model(Model.Location.File(relativePath));

            AsyncHelpers.RunSync(() => model.LoadModelAsync());
            model.EnsureCollectorExists();
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.ModelWidgetPrefab, tr, null, forceTransform: true
            );
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            ModelWidget widget = createCommand.Widget as ModelWidget;
            if (widget != null)
            {
                widget.Model = model;
                widget.Subtree = subtree;
                widget.SyncHierarchyToSubtree();
                widget.Show(true);
                widget.AddSceneLightGizmos();
                createCommand.SetWidgetCost(widget.GetTiltMeterCost());
            }
            else
            {
                Debug.LogWarning("Failed to create EditableModelWidget");
                return;
            }

            WidgetManager.m_Instance.WidgetsDormant = false;
            SketchControlsScript.m_Instance.EatGazeObjectInput();
            SelectionManager.m_Instance.RemoveFromSelection(false);
        }
    }
}
