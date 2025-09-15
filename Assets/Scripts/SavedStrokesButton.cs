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

                    // Button forward is into the panel, not out of the panel; so flip it around
                    TrTransform xfSpawn = Coords.AsGlobal[transform]
                        * TrTransform.R(Quaternion.AngleAxis(180, Vector3.up));

                    App.Scene.MoveStrokesCentroidTo(strokes, xfSpawn.translation, true, xfSpawn.rotation * Vector3.forward);
                    var group = App.GroupManager.NewUnusedGroup();
                    for (int i = 0; i < strokes.Count; i++)
                    {
                        strokes[i].Group = group;
                    }
                    AudioManager.m_Instance.PlayDuplicateSound(Vector3.zero);
                    AudioManager.m_Instance.PlayGroupedSound(Vector3.zero);
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
