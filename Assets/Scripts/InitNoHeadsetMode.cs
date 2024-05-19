// Copyright 2023 The Open Brush Authors
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
using UnityEngine;

namespace TiltBrush
{
    public class InitNoHeadsetMode : MonoBehaviour
    {
        public TextMeshPro m_Heading;
        public GameObject m_SketchLoadingUi;
        public string m_NonXRHelpURL;

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
            var curatedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);
            for (int i = 0; i < curatedSketchSet.NumSketches; i++)
            {
                var sketchName = curatedSketchSet.GetSketchName(i);
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

            var sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User);
            if (index < sketchSet.NumSketches)
            {
                SceneFileInfo rInfo = sketchSet.GetSketchSceneFileInfo(index);
                if (rInfo != null)
                {
                    SketchControlsScript.m_Instance.LoadSketch(rInfo, true);
                }
            }
            else
            {
                index -= sketchSet.NumSketches;
                sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);
                var rInfo = sketchSet.GetSketchSceneFileInfo(index);
                if (rInfo != null)
                {
                    SketchControlsScript.m_Instance.LoadSketch(rInfo, true);
                }
            }

            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
            Destroy(gameObject);
        }

        public void ShowSketchSelectorUi(bool active = true)
        {
            m_SketchLoadingUi.SetActive(active);
        }

        public void HandleHelpButton()
        {
            SketchControlsScript.m_Instance.OpenURLAndInformUser(m_NonXRHelpURL);
        }
    }
}
