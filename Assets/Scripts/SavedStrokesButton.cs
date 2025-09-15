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
    public class SavedStrokesButton : BaseButton
    {
        private SavedStrokeFile m_SavedStrokeFile;
        public int m_Index;

        override protected void OnButtonPressed()
        {
            base.OnButtonPressed();

            if (m_SavedStrokeFile != null)
            {
                int currentLayerIndex = App.Scene.GetIndexOfCanvas(App.Scene.ActiveCanvas);
                if (SaveLoadScript.m_Instance.Load(m_SavedStrokeFile.FileInfo, bAdditive: true, currentLayerIndex, out List<Stroke> strokes))
                {
                    SelectionManager.m_Instance.ClearActiveSelection();
                    SketchMemoryScript.m_Instance.SetPlaybackMode(SketchMemoryScript.PlaybackMode.Distance, 54);
                    SketchMemoryScript.m_Instance.BeginDrawingFromMemory(bDrawFromStart: true, false, false);
                    SketchMemoryScript.m_Instance.QuickLoadDrawingMemory();
                    SketchMemoryScript.m_Instance.ContinueDrawingFromMemory();

                    Vector3 buttonPosition = Coords.AsGlobal[transform].translation;
                    Vector3 cameraPosition = Camera.main.transform.position;
                    Vector3 midpoint = (buttonPosition + cameraPosition) * 0.5f;

                    App.Scene.MoveStrokesCentroidTo(strokes, midpoint);
                    var group = App.GroupManager.NewUnusedGroup();
                    for (int i = 0; i < strokes.Count; i++)
                    {
                        strokes[i].Group = group;
                    }
                    SelectionManager.m_Instance.SelectStrokes(strokes);
                    SelectionManager.m_Instance.UpdateSelectionWidget();
                    AudioManager.m_Instance.PlayDuplicateSound(midpoint);
                    AudioManager.m_Instance.PlayGroupedSound(midpoint);
                }
            }
        }

        public void RefreshDescription()
        {
            if (m_SavedStrokeFile != null)
            {
                SetDescriptionText(m_SavedStrokeFile.FileInfo.HumanName);
            }
        }

        public SavedStrokeFile SavedStrokeFile
        {
            get { return m_SavedStrokeFile; }
            set
            {
                m_SavedStrokeFile = value;
                SetButtonTexture(m_SavedStrokeFile.Thumbnail);
            }
        }
    }
}
