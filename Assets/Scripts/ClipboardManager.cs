// Copyright 2020 The Tilt Brush Authors
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

    /// Stores a transient collection of strokes for implementing features such as Copy, Paste and
    /// Duplicate. The ClipboardManager implicitly uses the SelectionManager as the source for brush
    /// strokes during copy operations.
    ///
    /// Future work: support other types of objects such as reference images, models, guides, mirror
    /// position, etc.
    public class ClipboardManager : MonoBehaviour
    {
        static public ClipboardManager Instance { get; private set; }

        [SerializeField] private Vector3 m_DuplicateOffset;

        /// Returns true if the current selection can be copied.
        public bool CanCopy
        {
            get
            {
                return SelectionManager.m_Instance.HasSelection;
            }
        }

        void Awake()
        {
            Instance = this;
        }

        /// Copies and pastes the current selection to the current canvas.
        public void DuplicateSelection(bool stampMode = false)
        {
            TrTransform dupXf = SelectionManager.m_Instance.SelectionTransform;
            if (!stampMode)
            {
                // Offset the duplicated selection
                float gridSize = SelectionManager.m_Instance.SnappingGridSize;

                Vector3 offset;
                if (gridSize > 0)
                {
                    // Use one grid square as offset in each enabled snap axis
                    // Grid size is in canvas local space (room space)
                    offset = new Vector3(
                        SelectionManager.m_Instance.m_EnableSnapTranslationX ? gridSize : m_DuplicateOffset.x * 0.5f,
                        SelectionManager.m_Instance.m_EnableSnapTranslationY ? gridSize : m_DuplicateOffset.y * 0.5f,
                        SelectionManager.m_Instance.m_EnableSnapTranslationZ ? gridSize : m_DuplicateOffset.z * 0.5f
                    );
                }
                else
                {
                    // No grid snapping, use default offset
                    offset = m_DuplicateOffset * 0.5f;
                }

                // Convert from room space to scene-relative space
                offset /= App.Scene.Pose.scale;
                dupXf.translation += offset;
            }

            // Lil' jiggle.
            var controller = InputManager.ControllerName.Brush;
            if (SketchControlsScript.m_Instance.OneHandGrabController != InputManager.ControllerName.None)
            {
                controller = SketchControlsScript.m_Instance.OneHandGrabController;
            }
            InputManager.m_Instance.TriggerHapticsPulse(controller, 3, 0.15f, 0.07f);
            AudioManager.m_Instance.PlayDuplicateSound(InputManager.m_Instance.GetControllerPosition(controller));

            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new DuplicateSelectionCommand(dupXf)
            );
        }
    }

} // namespace TiltBrush
