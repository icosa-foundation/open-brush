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
using UnityEngine.UI;

namespace TiltBrush
{
    public class InitNoHeadsetMode : MonoBehaviour
    {
        public TextMeshPro m_Heading;
        public GameObject m_SketchLoadingUi;
        public string m_NonXRHelpURL;
        private List<SceneFileInfo> m_Sketches;
        private TMP_Dropdown m_Dropdown;

        private const int BatchSize = 2;
        private const int MaxSketches = 20;

        public static InitNoHeadsetMode m_Instance;

        void Start()
        {
            m_Instance = this;
            App.Instance.m_NoVrUi.SetActive(true);
            m_Dropdown = GetComponentInChildren<TMP_Dropdown>();
            if (m_Dropdown != null)
            {
                m_Dropdown.gameObject.SetActive(false);
            }

            m_Dropdown.ClearOptions();
            m_Sketches = new List<SceneFileInfo>();
            StartCoroutine(DownloadAllCuratedSketchesInBatches(BatchSize, MaxSketches));
        }

        private IEnumerator DownloadAllCuratedSketchesInBatches(int numSketches, int maxSketches)
        {
            var curatedSketchSet = (IcosaSketchSet)SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);

            while (true)
            {
                yield return StartCoroutine(DownloadCuratedSketches(numSketches, maxSketches));
                RefreshDropdownItemsForSet(curatedSketchSet);

                // Count how many are now available
                int available = 0;
                for (int i = 0; i < curatedSketchSet.NumSketches; i++)
                {
                    var info = curatedSketchSet.GetSketchSceneFileInfo(i);
                    if (info != null && info.Available)
                        available++;
                }
                if (available >= maxSketches)
                    break;
            }
        }

        public IEnumerator DownloadCuratedSketches(int numSketches, int maxSketches)
        {
            var curatedSketchSet = (IcosaSketchSet)SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);

            // Count already downloaded sketches
            int alreadyDownloaded = 0;
            for (int i = 0; i < curatedSketchSet.NumSketches; i++)
            {
                var info = curatedSketchSet.GetSketchSceneFileInfo(i);
                if (info != null && info.Available)
                    alreadyDownloaded++;
            }

            int toDownload = Mathf.Min(numSketches, maxSketches - alreadyDownloaded);
            if (toDownload <= 0)
                yield break;

            yield return new WaitUntil(() => curatedSketchSet.NumSketches >= alreadyDownloaded + toDownload);

            List<int> indicesToDownload = new List<int>();
            for (int i = alreadyDownloaded; i < alreadyDownloaded + toDownload; i++)
            {
                indicesToDownload.Add(i);
            }

            yield return StartCoroutine(curatedSketchSet.DownloadFilesCoroutine(indicesToDownload, () =>
            {
                RefreshDropdownItemsForSet(curatedSketchSet);
            }));
        }

        public void OnClickOutsideDropdown()
        {
            // Hide the dropdown when clicking outside
            if (m_Dropdown != null && m_Dropdown.gameObject.activeSelf)
            {
                m_Dropdown.Hide();
            }
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
            // Select the first item if available
            if (m_Dropdown.options.Count > 0)
            {
                m_Dropdown.value = 0;
                m_Dropdown.RefreshShownValue();
            }

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

        public void InitEditMode()
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y = 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            ShutdownSelf();
        }

        public void InitViewOnlyMode()
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y = 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
            var dropdown = GetComponentInChildren<TMP_Dropdown>();
            var index = dropdown.value;
            SceneFileInfo rInfo = m_Sketches[index];
            SketchControlsScript.m_Instance.LoadSketch(rInfo, quickload: true);
            ShutdownSelf();
        }

        private void ShutdownSelf()
        {
            m_Instance = null;
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