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

        private readonly SelectCommand m_PreviousSelectionCommand;

        private List<Stroke> m_LoadedStrokes;
        private SketchGroupTag m_Group;
        private bool m_LoadAttempted;
        private bool m_LoadSucceeded;
        private bool m_StrokesVisible;

        public override bool NeedsSave => m_LoadSucceeded;

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

            var selectionManager = SelectionManager.m_Instance;
            if (selectionManager.HasSelection)
            {
                m_PreviousSelectionCommand = new SelectCommand(
                    selectionManager.SelectedStrokes.ToList(),
                    selectionManager.SelectedWidgets.ToList(),
                    selectionManager.SelectionTransform,
                    deselect: true,
                    checkForClearedSelection: true);
            }
        }

        protected override void OnRedo()
        {
            if (!m_LoadAttempted)
            {
                m_LoadAttempted = true;
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
                SketchMemoryScript.m_Instance.BeginDrawingFromMemory(bDrawFromStart: true, false, false);
                SketchMemoryScript.m_Instance.QuickLoadDrawingMemory();
                SketchMemoryScript.m_Instance.ContinueDrawingFromMemory();

                App.Scene.MoveStrokesCentroidTo(m_LoadedStrokes, m_CommandMidpoint);

                m_Group = App.GroupManager.NewUnusedGroup();
                for (int i = 0; i < m_LoadedStrokes.Count; i++)
                {
                    m_LoadedStrokes[i].Group = m_Group;
                }

                m_StrokesVisible = true;
            }
            else if (!m_StrokesVisible)
            {
                foreach (var stroke in m_LoadedStrokes)
                {
                    stroke.Hide(false);
                }

                m_StrokesVisible = true;
            }

            if (!m_LoadSucceeded)
            {
                return;
            }

            if (m_PreviousSelectionCommand != null)
            {
                m_PreviousSelectionCommand.Redo();
            }

            SelectionManager.m_Instance.SelectStrokes(m_LoadedStrokes);
            SelectionManager.m_Instance.UpdateSelectionWidget();

            AudioManager.m_Instance.PlayDuplicateSound(m_CommandMidpoint);
            AudioManager.m_Instance.PlayGroupedSound(m_CommandMidpoint);
        }

        protected override void OnUndo()
        {
            if (!m_LoadSucceeded)
            {
                return;
            }

            SelectionManager.m_Instance.DeselectStrokes(m_LoadedStrokes);
            SelectionManager.m_Instance.UpdateSelectionWidget();

            foreach (var stroke in m_LoadedStrokes)
            {
                stroke.Hide(true);
            }

            m_StrokesVisible = false;

            if (m_PreviousSelectionCommand != null)
            {
                m_PreviousSelectionCommand.Undo();
            }
            else
            {
                SelectionManager.m_Instance.UpdateSelectionWidget();
            }

            AudioManager.m_Instance.PlayUndoSound(m_CommandMidpoint);
        }
    }
} // namespace TiltBrush
