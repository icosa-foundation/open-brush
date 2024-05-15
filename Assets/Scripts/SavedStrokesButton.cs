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

            SketchSet sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.SavedStrokes);
            SceneFileInfo rInfo = sketchSet.GetSketchSceneFileInfo(m_Index);
            if (rInfo != null)
            {
                var prevLayer = App.Scene.ActiveCanvas;
                var tempLayer = App.Scene.AddLayerNow();
                PointerManager.m_Instance.EnablePointerStrokeGeneration(true);
                if (SaveLoadScript.m_Instance.Load(rInfo, true))
                {
                    SketchMemoryScript.m_Instance.SetPlaybackMode(SketchMemoryScript.PlaybackMode.Timestamps, 1);
                    SketchMemoryScript.m_Instance.BeginDrawingFromMemory(bDrawFromStart: true, false, false);
                    App.Instance.SetDesiredState(App.AppState.QuickLoad);
                }
                var strokes = SketchMemoryScript.m_Instance.GetAllUnselectedActiveStrokes();
                var widgets = WidgetManager.m_Instance.GetAllUnselectedActiveWidgets();
                var group = App.GroupManager.NewUnusedGroup();
                for (int i = 0; i < strokes.Count; i++) {strokes[i].Group = group;}
                for (int i = 0; i < widgets.Count; i++) {widgets[i].Group = group;}
                SquashLayerCommand cmd = new SquashLayerCommand(tempLayer, prevLayer);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            }
        }

        public void RefreshDescription()
        {
            if (m_SavedStrokeFile != null)
            {
                SetDescriptionText(m_SavedStrokeFile.HumanName);
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
    public class SavedStrokeFile
    {
        public SavedStrokeFile(SceneFileInfo sceneFileInfo, Texture2D thumbnail)
        {
            HumanName = sceneFileInfo.HumanName;
            Thumbnail = thumbnail;
        }

        public string HumanName { get; set; }
        public Texture2D Thumbnail { get; set; }
    }
}
