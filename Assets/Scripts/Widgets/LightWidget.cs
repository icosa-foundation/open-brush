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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public abstract class LightWidget : MediaWidget
    {
        public static List<LightWidget> FromModelWidget(ModelWidget modelWidget)
        {
            var go = modelWidget.gameObject;
            var lightWidgets = new List<LightWidget>();
            foreach (var gizmo in go.GetComponentsInChildren<SceneLightGizmo>())
            {
                gizmo.transform.SetParent(go.transform.parent, true);
                var lightWidget = gizmo.gameObject.AddComponent<LightWidget>();
                lightWidgets.Add(lightWidget);
            }
            Destroy(modelWidget);
            return lightWidgets;
        }
    }

} // namespace TiltBrush
