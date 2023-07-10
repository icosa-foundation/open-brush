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
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("model.import", "Imports a model given a url or a filename in Media Library\\Models (Models loaded from a url are saved locally first)")]
        public static ModelWidget ImportModel(string location)
        {
            if (location.StartsWith("poly:"))
            {
                location = location.Substring(5);
                ApiManager.Instance.LoadPolyModel(location);
                return null; // TODO
            }

            if (location.StartsWith("http://") || location.StartsWith("https://"))
            {
                // You can't rely on urls ending with a file extension
                // But try and fall back to assuming web models will be gltf/glb
                // TODO Try deriving from MIME types
                if (location.EndsWith(".off") || location.EndsWith(".obj"))
                {
                    location = _DownloadMediaFileFromUrl(location, App.ModelLibraryPath());
                }
                else
                {
                    Uri uri = new Uri(location);
                    ApiManager.Instance.LoadPolyModel(uri);
                }
            }

            // At this point we've got a relative path to a file in Models
            string relativePath = location;
            var tr = _CurrentTransform().TransformBy(Coords.CanvasPose);
            var model = new Model(Model.Location.File(relativePath));

            model.LoadModel();
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.ModelWidgetPrefab, tr, null, forceTransform: true
            );
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            ModelWidget widget = createCommand.Widget as ModelWidget;
            if (widget != null)
            {
                widget.Model = model;
                widget.Show(true);
                createCommand.SetWidgetCost(widget.GetTiltMeterCost());
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
    }
}
