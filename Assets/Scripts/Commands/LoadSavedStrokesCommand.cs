// Copyright 2024 The Tilt Brush Authors
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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Loads a saved stroke file additively and applies the same selection and placement
    /// adjustments as the SavedStrokesButton, while making the change undoable.
    /// </summary>
    public class LoadSavedStrokesCommand : BaseCommand
    {
        private readonly SavedStrokeFile m_SavedStrokeFile;
        private readonly int m_TargetLayerIndex;
        private readonly Vector3 m_CommandMidpoint;

        private SelectCommand m_DeselectPreviousCommand;
        private SelectCommand m_SelectLoadedStrokesCommand;

        private List<Stroke> m_LoadedStrokes;
        private SketchGroupTag m_Group;
        private bool m_LoadAttempted;
        private bool m_LoadSucceeded;
        private bool m_StrokesVisible;

        public override bool NeedsSave => m_LoadSucceeded;

        protected override void OnDispose()
        {
            if (m_LoadedStrokes != null)
            {
                foreach (var stroke in m_LoadedStrokes)
                {
                    SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                    stroke.DestroyStroke();
                }
            }
        }

        public LoadSavedStrokesCommand(
            SavedStrokeFile savedStrokeFile,
            int targetLayerIndex,
            Vector3 commandMidpoint,
            BaseCommand parent = null)
            : base(parent)
        {
            m_SavedStrokeFile = savedStrokeFile;
            m_TargetLayerIndex = targetLayerIndex;
            m_CommandMidpoint = commandMidpoint;

            // Create a sub-command to deselect the current selection (if any)
            var selectionManager = SelectionManager.m_Instance;
            if (selectionManager.HasSelection)
            {
                m_DeselectPreviousCommand = new SelectCommand(
                    selectionManager.SelectedStrokes.ToList(),
                    selectionManager.SelectedWidgets.ToList(),
                    selectionManager.SelectionTransform,
                    deselect: true,
                    parent: this);
            }
        }

        protected override void OnRedo()
        {
            if (!m_LoadAttempted)
            {
                m_LoadAttempted = true;

                // Deselect the previous selection first (before loading)
                // We manually call Redo here because we need it to happen before loading
                if (m_DeselectPreviousCommand != null)
                {
                    m_DeselectPreviousCommand.Redo();
                }

                m_LoadSucceeded = SaveLoadScript.m_Instance.Load(
                    m_SavedStrokeFile.FileInfo,
                    bAdditive: true,
                    m_TargetLayerIndex,
                    out m_LoadedStrokes);

                if (!m_LoadSucceeded)
                {
                    m_LoadedStrokes = new List<Stroke>();
                    return;
                }

                SketchMemoryScript.m_Instance.SetPlaybackMode(
                    SketchMemoryScript.PlaybackMode.Distance, 54);

                // Because we want QuickLoad to complete in a single frame,
                // we need to set App.PlatformConfig.QuickLoadMaxDistancePerFrame to a very large value
                // Without this strokes were not loaded correctly on Android devices where this value is low by default
                float originalMaxDistance = App.PlatformConfig.QuickLoadMaxDistancePerFrame;
                App.PlatformConfig.QuickLoadMaxDistancePerFrame = 10000.0f;
                SketchMemoryScript.m_Instance.BeginDrawingFromMemory(bDrawFromStart: true, false, false);
                SketchMemoryScript.m_Instance.QuickLoadDrawingMemory();
                SketchMemoryScript.m_Instance.ContinueDrawingFromMemory();
                App.PlatformConfig.QuickLoadMaxDistancePerFrame = originalMaxDistance;

                App.Scene.MoveStrokesCentroidTo(m_LoadedStrokes, m_CommandMidpoint);

                m_Group = App.GroupManager.NewUnusedGroup();
                for (int i = 0; i < m_LoadedStrokes.Count; i++)
                {
                    m_LoadedStrokes[i].Group = m_Group;
                }

                m_StrokesVisible = true;

                // Create a sub-command to select the newly loaded strokes
                // Its Redo() will be called automatically by the base class
                m_SelectLoadedStrokesCommand = new SelectCommand(
                    m_LoadedStrokes,
                    null,
                    SelectionManager.m_Instance.SelectionTransform,
                    deselect: false,
                    parent: this);

                AudioManager.m_Instance.PlayDuplicateSound(m_CommandMidpoint);
                AudioManager.m_Instance.PlayGroupedSound(m_CommandMidpoint);
            }
            else if (!m_StrokesVisible)
            {
                // Redo after undo
                // Manually deselect previous before showing strokes
                if (m_DeselectPreviousCommand != null)
                {
                    m_DeselectPreviousCommand.Redo();
                }

                foreach (var stroke in m_LoadedStrokes)
                {
                    stroke.Hide(false);
                }

                m_StrokesVisible = true;

                // m_SelectLoadedStrokesCommand.Redo() will be called automatically by base class
            }
        }

        protected override void OnUndo()
        {
            if (!m_LoadSucceeded)
            {
                return;
            }

            // Note: m_SelectLoadedStrokesCommand.Undo() will be called automatically by base class first

            foreach (var stroke in m_LoadedStrokes)
            {
                stroke.Hide(true);
            }

            m_StrokesVisible = false;

            // Restore the previous selection by undoing the deselect
            // We manually call this because we need it to happen after hiding strokes
            if (m_DeselectPreviousCommand != null)
            {
                m_DeselectPreviousCommand.Undo();
            }

            AudioManager.m_Instance.PlayUndoSound(m_CommandMidpoint);
        }
    }
} // namespace TiltBrush
