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

using UnityEngine;
namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("snap.angle", "Sets the current snapping angle. Angle must be a supported value (15, 30, 45, 60, 75 or 90)")]
        public static void SetSnapAngle(string angle)
        {
            SelectionManager.m_Instance.SetSnappingAngle(angle);
        }

        [ApiEndpoint("snap.grid", "Sets the current snapping grid. Size must be a supported value (0.1, 0.25, 0.5 ,1, 2, 3, 5")]
        public static void SetSnapGrid(string size)
        {
            SelectionManager.m_Instance.SetSnappingGridSize(size);
        }

        [ApiEndpoint("snap.selected.angles", "Applies the current snap angle to all selected objects")]
        public static void SnapSelectedAngles()
        {
            TransformItems.SnapSelectedRotationAngles();
        }

        [ApiEndpoint("snap.selected.positions", "Applies the current snap grid to all selected objects")]
        public static void SnapSelectedPositions()
        {
            TransformItems.SnapSelectionToGrid();
        }
    }
}
