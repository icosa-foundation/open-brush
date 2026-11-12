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
        public LoadSavedStrokesCommand(
            SavedStrokeFile savedStrokeFile,
            int targetLayerIndex,
            Vector3 commandMidpoint,
            BaseCommand parent = null) : base(parent)
        {
            var selection = SelectionManager.m_Instance;
            if (selection.HasSelection)
            {
                new SelectCommand(selection.SelectedStrokes.ToList(), selection.SelectedWidgets.ToList(),
                    selection.SelectionTransform, deselect: true, parent: this);
            }
            // Child order is deselect -> load/place -> select; undo reverses it exactly once.
            new LoadCommand(savedStrokeFile, targetLayerIndex, commandMidpoint, this);
        }

        private sealed class LoadCommand : BaseCommand
        {
            private readonly SavedStrokeFile m_SavedStrokeFile;
            private readonly int m_TargetLayerIndex;
            private readonly Vector3 m_CommandMidpoint;

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

            internal LoadCommand(SavedStrokeFile savedStrokeFile, int targetLayerIndex,
                Vector3 commandMidpoint, BaseCommand parent) : base(parent)
            {
                m_SavedStrokeFile = savedStrokeFile;
                m_TargetLayerIndex = targetLayerIndex;
                m_CommandMidpoint = commandMidpoint;
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

                    SketchMemoryScript.m_Instance.RenderStrokesDirectly(m_LoadedStrokes);

                    App.Scene.MoveStrokesCentroidTo(m_LoadedStrokes, m_CommandMidpoint);

                    m_Group = App.GroupManager.NewUnusedGroup();
                    for (int i = 0; i < m_LoadedStrokes.Count; i++)
                    {
                        m_LoadedStrokes[i].Group = m_Group;
                    }

                    m_StrokesVisible = true;

                    // Create a sub-command to select the newly loaded strokes
                    // Its Redo() will be called automatically by the base class
                    new SelectCommand(
                        m_LoadedStrokes,
                        null,
                        SelectionManager.m_Instance.SelectionTransform,
                        deselect: false,
                        parent: this);

                    AudioManager.m_Instance.PlayDuplicateSound(m_CommandMidpoint);
                    AudioManager.m_Instance.PlayGroupedSound(m_CommandMidpoint);
                }
                else if (m_LoadSucceeded && !m_StrokesVisible)
                {
                    // Redo after undo
                    foreach (var stroke in m_LoadedStrokes)
                    {
                        stroke.Hide(false);
                    }

                    m_StrokesVisible = true;

                    // The selection child is redone automatically after the strokes are shown.
                }
            }

            protected override void OnUndo()
            {
                if (!m_LoadSucceeded)
                {
                    return;
                }

                // The selection child has already been undone by the base class.

                foreach (var stroke in m_LoadedStrokes)
                {
                    stroke.Hide(true);
                }

                m_StrokesVisible = false;

                AudioManager.m_Instance.PlayUndoSound(m_CommandMidpoint);
            }
        }
    }
} // namespace TiltBrush
