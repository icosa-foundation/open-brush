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
        private List<SceneFileInfo> m_Sketches;
        private TMP_Dropdown m_Dropdown;

        void Start()
        {
            App.Instance.m_NoVrUi.SetActive(true);
            m_Dropdown = GetComponentInChildren<TMP_Dropdown>();
            if (m_Dropdown != null)
            {
                m_Dropdown.gameObject.SetActive(false);
            }

            m_Dropdown.ClearOptions();
            m_Sketches = new List<SceneFileInfo>();

            StartCoroutine(DownloadCuratedSketches(10));
        }

        public IEnumerator DownloadCuratedSketches(int numSketches)
        {
            var curatedSketchSet = (IcosaSketchSet)SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);
            yield return new WaitUntil(() => curatedSketchSet.NumSketches >= numSketches);
            yield return StartCoroutine(curatedSketchSet.DownloadFilesCoroutine(() =>
            {
                RefreshDropdownItemsForSet(curatedSketchSet);
            }));
        }

        // Public so it can be called from the download callback
        public void RefreshDropdownItemsForSet(SketchSet sketchset)
        {
            // Check if any set has items before clearing and repopulating
            var userSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User);
            var curatedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);
            var likedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Liked);

            bool anyHasItems =
                (userSketchSet.IsReadyForAccess && userSketchSet.NumSketches > 0) ||
                (curatedSketchSet.IsReadyForAccess && curatedSketchSet.NumSketches > 0) ||
                (likedSketchSet.IsReadyForAccess && likedSketchSet.NumSketches > 0);

            if (!anyHasItems)
            {
                // Don't clear or show the dropdown if nothing is ready
                m_Dropdown.gameObject.SetActive(false);
                return;
            }

            m_Dropdown.ClearOptions();
            m_Sketches.Clear();

            // Repopulate all sets
            AddDropdownItems(userSketchSet);
            AddDropdownItems(curatedSketchSet);
            AddDropdownItems(likedSketchSet);

            // Show dropdown if there is at least one item, otherwise hide it
            m_Dropdown.gameObject.SetActive(m_Dropdown.options.Count > 0);
        }

        private void AddDropdownItems(SketchSet sketchset)
        {
            if (!sketchset.IsReadyForAccess) return;

            for (int i = 0; i < sketchset.NumSketches; i++)
            {
                var info = sketchset.GetSketchSceneFileInfo(i);
                if (info == null || !sketchset.IsSketchIndexValid(i) || !info.Available)
                {
                    continue; // skip invalid sketches
                }
                var sketchName = sketchset.GetSketchName(i);
                m_Sketches.Add(info);

                sketchset.GetSketchIcon(i, out Texture2D icon,
                    out string[] _, out string __);
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

        private IEnumerator UiInitCoroutine()
        {
            // Initial population
            var userSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User);
            var curatedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);
            var likedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Liked);

            yield return new WaitUntil(() => userSketchSet.IsReadyForAccess);
            yield return new WaitUntil(() => curatedSketchSet.IsReadyForAccess);
            yield return new WaitUntil(() => likedSketchSet.IsReadyForAccess);

            RefreshDropdownItemsForSet(null);
        }

        public void InitEditMode()
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y = 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            Destroy(gameObject);
        }

        public void InitViewOnlyMode()
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y = 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            var dropdown = GetComponentInChildren<TMP_Dropdown>();
            var index = dropdown.value;
            SceneFileInfo rInfo = m_Sketches[index];
            SketchControlsScript.m_Instance.LoadSketch(rInfo, true);
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
            gameObject.SetActive(false);
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