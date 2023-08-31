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

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace TiltBrush
{
    public class SavedStrokesPanel : ModalPanel
    {
        [SerializeField] private GameObject[] m_IconsOnNormalPage;
        [SerializeField] private GameObject m_CloseButton;
        [SerializeField] private Renderer m_OnlineGalleryButtonRenderer;
        [SerializeField] private Renderer m_ProfileButtonRenderer;

        private SceneFileInfo m_FirstSketch;
        private SketchSet m_SketchSet;
        private SketchSetType m_CurrentSketchSet;
        private OptionButton m_PaintButtonScript;
        private List<BaseButton> m_IconScriptsOnNormalPage;

        [SerializeField] private TextMeshPro m_PanelTextPro;
        [SerializeField] private LocalizedString m_PanelTextStandard;
        public string PanelTextStandard { get { return m_PanelTextStandard.GetLocalizedString(); } }
        [SerializeField] private LocalizedString m_PanelTextDrive;
        public string PanelTextDrive { get { return m_PanelTextDrive.GetLocalizedString(); } }

        override public void InitPanel()
        {
            base.InitPanel();
            m_PaintButtonScript = m_CloseButton.GetComponent<OptionButton>();
            m_IconScriptsOnNormalPage = new List<BaseButton>();
            for (int i = 0; i < m_IconsOnNormalPage.Length; ++i)
            {
                m_IconScriptsOnNormalPage.Add(m_IconsOnNormalPage[i].GetComponent<BaseButton>());
            }
        }
        
        public override bool IsInButtonMode(ModeButton button)
        {
            GalleryButton galleryButton = button as GalleryButton;
            return galleryButton &&
                ((galleryButton.m_ButtonType == GalleryButton.Type.Local && m_CurrentSketchSet == SketchSetType.User) ||
                (galleryButton.m_ButtonType == GalleryButton.Type.Drive && m_CurrentSketchSet == SketchSetType.Drive));
        }
        
        override protected void OnUpdateActive()
        {
            // If we're not active, hide all our preview panels
            if (!m_GazeActive)
            {
                for (int i = 0; i < m_IconScriptsOnNormalPage.Count; ++i)
                {
                    m_IconScriptsOnNormalPage[i].ResetState();
                }
                if (m_PaintButtonScript)
                {
                    m_PaintButtonScript.ResetState();
                }
            }
            else if (m_CurrentState == PanelState.Available)
            {
                m_OnlineGalleryButtonRenderer.material.SetFloat("_Grayscale", 0);
                m_ProfileButtonRenderer.material.SetFloat("_Grayscale", 0);
                m_SketchSet.RequestRefresh();
            }
        }

        void OnDestroy()
        {
            if (m_SketchSet != null)
            {
                m_SketchSet.OnChanged -= OnSketchSetDirty;
            }
        }

        override protected void OnEnablePanel()
        {
            base.OnEnablePanel();
            if (m_SketchSet != null)
            {
                m_SketchSet.RequestRefresh();
            }
        }
        
        private void OnSketchSetDirty()
        {
            ComputeNumPages();

            SceneFileInfo first = (m_SketchSet.NumSketches > 0) ?
                m_SketchSet.GetSketchSceneFileInfo(0) : null;
            // If first sketch changed, return to first page.
            if (m_FirstSketch != null && !m_FirstSketch.Equals(first))
            {
                PageIndex = 0;
            }
            else
            {
                PageIndex = Mathf.Min(PageIndex, m_NumPages - 1);
            }
            m_FirstSketch = first;
            GotoPage(PageIndex);
            UpdateIndexOffset();
            RefreshPage();
        }

        private void ComputeNumPages()
        {
            if (m_SketchSet.NumSketches <= m_IconsOnNormalPage.Length)
            {
                m_NumPages = 1;
                return;
            }
            int remainingSketches = m_SketchSet.NumSketches - m_IconsOnNormalPage.Length;
            int normalPages = ((remainingSketches - 1) / m_IconsOnNormalPage.Length) + 1;
            m_NumPages = 1 + normalPages;
        }
        
        void SetVisibleSketchSet(SketchSetType type)
        {
            if (m_CurrentSketchSet != type)
            {
                // Clean up our old sketch set.
                if (m_SketchSet != null)
                {
                    m_SketchSet.OnChanged -= OnSketchSetDirty;
                }

                // Cache new set.
                m_SketchSet = SketchCatalog.m_Instance.GetSet(type);
                m_SketchSet.OnChanged += OnSketchSetDirty;
                m_SketchSet.RequestRefresh();

                // Tell all the icons which set to reference when loading sketches.
                IEnumerable<LoadSketchButton> allIcons = m_IconsOnNormalPage
                    .Select(icon => icon.GetComponent<LoadSketchButton>())
                    .Where(icon => icon != null);
                foreach (LoadSketchButton icon in allIcons)
                {
                    icon.SketchSet = m_SketchSet;
                }

                ComputeNumPages();
                ResetPageIndex();
                m_CurrentSketchSet = type;
                RefreshPage();

                switch (m_CurrentSketchSet)
                {
                    case SketchSetType.User:
                        m_PanelTextPro.text = PanelTextStandard;
                        break;
                    // case SketchSetType.Curated:
                    //     m_PanelTextPro.text = PanelTextShowcase;
                    //     break;
                    // case SketchSetType.Liked:
                    //     m_PanelTextPro.text = PanelTextLiked;
                    //     break;
                    case SketchSetType.Drive:
                        m_PanelTextPro.text = PanelTextDrive;
                        break;
                }
            }
        }
        
        protected override void OnStart()
        {
            // Initialize icons.
            LoadSketchButton[] rPanelButtons = m_Mesh.GetComponentsInChildren<LoadSketchButton>();
            foreach (LoadSketchButton icon in rPanelButtons)
            {
                GameObject go = icon.gameObject;
                go.SetActive(false);
            }

            // // GameObject is active in prefab so the button registers.
            // m_NoLikesMessage.SetActive(false);
            // m_NotLoggedInMessage.SetActive(false);
            // m_NotLoggedInDriveMessage.SetActive(false);
            //
            // // Dynamically position the gallery buttons.
            // OnDriveSetHasSketchesChanged();

            // Set the sketch set var to Liked, then function set to force state.
            m_CurrentSketchSet = SketchSetType.Liked;
            SetVisibleSketchSet(SketchSetType.User);

            Action refresh = () =>
            {
                // if (m_ContactingServerMessage.activeSelf ||
                //     m_NoShowcaseMessage.activeSelf ||
                //     m_LoadingGallery.activeSelf)
                // {
                //     // Update the overlays more frequently when these overlays are shown to reflect whether
                //     // we are actively trying to get sketches from Poly.
                //     RefreshPage();
                // }
            };
            SketchCatalog.m_Instance.GetSet(SketchSetType.Liked).OnSketchRefreshingChanged += refresh;
            SketchCatalog.m_Instance.GetSet(SketchSetType.Curated).OnSketchRefreshingChanged += refresh;
            SketchCatalog.m_Instance.GetSet(SketchSetType.Drive).OnSketchRefreshingChanged += refresh;
            App.GoogleIdentity.OnLogout += refresh;
        }
    }
}
