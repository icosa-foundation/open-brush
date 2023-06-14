// Copyright 2023 The Tilt Brush Authors
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

using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace TiltBrush
{
    public class InitNoHeadsetMode : MonoBehaviour
    {

        void Start()
        {
            var dropdown = GetComponentInChildren<TMP_Dropdown>();
            dropdown.ClearOptions();
            var userSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User);
            for (int i = 0; i < userSketchSet.NumSketches; i++)
            {
                var sketchName = userSketchSet.GetSketchName(i);
                dropdown.options.Add(new TMP_Dropdown.OptionData(sketchName));
            }
        }

        public void Init()
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y += 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            var dropdown = GetComponentInChildren<TMP_Dropdown>();
            var index = dropdown.value;
            // SketchControlsScript.m_Instance.IssueGlobalCommand(
            //     SketchControlsScript.GlobalCommands.Load, sketchIndex, 0
            // );

            var sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User);
            SceneFileInfo rInfo = sketchSet.GetSketchSceneFileInfo(index);
            if (rInfo != null)
            {
                SketchControlsScript.m_Instance.LoadSketch(rInfo, true);
            }
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
            Destroy(gameObject);
        }
    }
}
