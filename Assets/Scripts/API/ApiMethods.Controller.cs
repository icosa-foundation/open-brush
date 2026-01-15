// Copyright 2021 The Open Brush Authors
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

using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint(
            "controller.brush.geometry.offset",
            "Sets the brush controller geometry offset (x,y,z). Use this to calibrate brush position for different hardware.",
            "0,-0.182,0.379"
        )]
        public static void SetBrushGeometryOffset(Vector3 offset)
        {
            var behavior = InputManager.Brush.Behavior;
            behavior.SetRuntimeGeometryOffset(offset);
            Debug.Log($"[GEOM_CALIB] Brush geometry offset set to: {offset}");
        }

        [ApiEndpoint(
            "controller.brush.geometry.rotation",
            "Sets the brush controller geometry rotation in euler angles (x,y,z degrees). Use this to calibrate brush rotation for different hardware.",
            "35,0,0"
        )]
        public static void SetBrushGeometryRotation(Vector3 eulerAngles)
        {
            var behavior = InputManager.Brush.Behavior;
            behavior.SetRuntimeGeometryRotation(eulerAngles);
            Debug.Log($"[GEOM_CALIB] Brush geometry rotation set to: {eulerAngles}");
        }

        [ApiEndpoint(
            "controller.brush.geometry.reset",
            "Resets the brush controller geometry to the prefab defaults"
        )]
        public static void ResetBrushGeometry()
        {
            var behavior = InputManager.Brush.Behavior;
            behavior.ClearRuntimeGeometryOverrides();
            Debug.Log("[GEOM_CALIB] Brush geometry reset to defaults");
        }

        [ApiEndpoint(
            "controller.brush.geometry.get",
            "Gets the current brush controller geometry offset and rotation values"
        )]
        public static string GetBrushGeometry()
        {
            var behavior = InputManager.Brush.Behavior;
            var offset = behavior.EffectiveGeometryOffset;
            var rotation = behavior.EffectiveGeometryRotation.eulerAngles;
            var result = $"offset:{offset.x},{offset.y},{offset.z}|rotation:{rotation.x},{rotation.y},{rotation.z}";
            Debug.Log($"[GEOM_CALIB] Current brush geometry: {result}");
            return result;
        }

        [ApiEndpoint(
            "controller.wand.geometry.offset",
            "Sets the wand controller geometry offset (x,y,z). Use this to calibrate wand position for different hardware.",
            "0,-0.182,0.379"
        )]
        public static void SetWandGeometryOffset(Vector3 offset)
        {
            var behavior = InputManager.Wand.Behavior;
            behavior.SetRuntimeGeometryOffset(offset);
            Debug.Log($"[GEOM_CALIB] Wand geometry offset set to: {offset}");
        }

        [ApiEndpoint(
            "controller.wand.geometry.rotation",
            "Sets the wand controller geometry rotation in euler angles (x,y,z degrees). Use this to calibrate wand rotation for different hardware.",
            "35,0,0"
        )]
        public static void SetWandGeometryRotation(Vector3 eulerAngles)
        {
            var behavior = InputManager.Wand.Behavior;
            behavior.SetRuntimeGeometryRotation(eulerAngles);
            Debug.Log($"[GEOM_CALIB] Wand geometry rotation set to: {eulerAngles}");
        }

        [ApiEndpoint(
            "controller.wand.geometry.reset",
            "Resets the wand controller geometry to the prefab defaults"
        )]
        public static void ResetWandGeometry()
        {
            var behavior = InputManager.Wand.Behavior;
            behavior.ClearRuntimeGeometryOverrides();
            Debug.Log("[GEOM_CALIB] Wand geometry reset to defaults");
        }

        [ApiEndpoint(
            "controller.wand.geometry.get",
            "Gets the current wand controller geometry offset and rotation values"
        )]
        public static string GetWandGeometry()
        {
            var behavior = InputManager.Wand.Behavior;
            var offset = behavior.EffectiveGeometryOffset;
            var rotation = behavior.EffectiveGeometryRotation.eulerAngles;
            var result = $"offset:{offset.x},{offset.y},{offset.z}|rotation:{rotation.x},{rotation.y},{rotation.z}";
            Debug.Log($"[GEOM_CALIB] Current wand geometry: {result}");
            return result;
        }
    }
}
