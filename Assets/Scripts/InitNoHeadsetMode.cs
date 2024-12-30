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

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class InitNoHeadsetMode : MonoBehaviour
    {
        public TextMeshPro m_Heading;
        public GameObject m_SketchLoadingUi;
        public string m_NonXRHelpURL;
        private List<SketchSet> m_SketchSets;
        private TMP_Dropdown m_Dropdown;

        void Start()
        {
            m_Dropdown = GetComponentInChildren<TMP_Dropdown>();
            m_Dropdown.ClearOptions();
            m_SketchSets = new List<SketchSet>();

            StartCoroutine(UiInitCoroutine());
        }

        private IEnumerator UiInitCoroutine()
        {
            IEnumerator AddDropdownItems(SketchSet sketchset)
            {
                yield return new WaitUntil(() => sketchset.IsReadyForAccess);

                for (int i = 0; i < sketchset.NumSketches; i++)
                {
                    var sketchName = sketchset.GetSketchName(i);
                    m_SketchSets.Add(sketchset);
                    sketchset.GetSketchIcon(i, out Texture2D icon,
                        out string[] _, out string __);
                    // TODO Icon will usually be null as
                    // we haven't called RequestLoadIconAndMetadata
                    if (icon != null)
                    {
                        var sprite = Sprite.Create(icon, new Rect(0, 0,
                            icon.width, icon.height), new Vector2(0.5f, 0.5f));
                        m_Dropdown.options.Add(new TMP_Dropdown.OptionData(sketchName, sprite));
                    }
                    else
                    {
                        m_Dropdown.options.Add(new TMP_Dropdown.OptionData(sketchName));
                    }
                }
            }

            var userSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User);
            var curatedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);
            var likedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Liked);

            yield return StartCoroutine(AddDropdownItems(userSketchSet));
            yield return StartCoroutine(AddDropdownItems(curatedSketchSet));
            yield return StartCoroutine(AddDropdownItems(likedSketchSet));
        }

        public void InitEditMode()
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y += 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            Destroy(gameObject);
        }

        public void InitViewOnlyMode()
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y += 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            var dropdown = GetComponentInChildren<TMP_Dropdown>();
            var index = dropdown.value;
            var sketchSet = m_SketchSets[index];
            SceneFileInfo rInfo = sketchSet.GetSketchSceneFileInfo(index);
            SketchControlsScript.m_Instance.LoadSketch(rInfo, true);
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
