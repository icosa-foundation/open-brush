// Copyright 2020 The Tilt Brush Authors
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
using System.IO;
using System.Linq;
using UnityEngine;
using TMPro;

namespace TiltBrush
{
    public class ReferencePanel : ModalPanel
    {
        [Header("Reference Panel")]
        [SerializeField] private TextMeshPro m_PanelText;
        [SerializeField] private GameObject m_NoData;
        [SerializeField] private Texture2D m_UnknownImageTexture;
        [SerializeField] private ReferencePanelTab[] m_Tabs;
        [SerializeField] private MeshRenderer[] m_ExtraBorders;
        [SerializeField] private GameObject m_RefreshingSpinner;
        [SerializeField] private TextOptionButton m_DirectoryChooserPopupButton;
        [SerializeField] private ActionButton m_DirectoryHomeButton;
        [SerializeField] private ActionButton m_DirectoryUpButton;
        [SerializeField] private TextMeshPro m_InfoText;
        private ReferencePanelTab m_CurrentTab;
        private int m_EnabledCount = 0;
        private string[] m_CurrentSubdirectories;
        private bool m_FolderNavButtonsNeedUpdate;

        public string[] CurrentSubdirectories => m_CurrentSubdirectories;

        public Texture2D UnknownImageTexture
        {
            get { return m_UnknownImageTexture; }
        }

        protected override int PageIndex
        {
            get { return m_CurrentTab.PageIndex; }
            set { m_CurrentTab.PageIndex = value; }
        }

        protected override List<BaseButton> Icons
        {
            get
            {
                return m_CurrentTab.Buttons;
            }
        }
        public ReferencePanelTab CurrentTab => m_CurrentTab;

        public override bool IsInButtonMode(ModeButton button)
        {
            ReferenceButton refButton = button as ReferenceButton;
            return refButton && (m_CurrentTab != null) &&
                refButton.m_ButtonType == m_CurrentTab.ReferenceButtonType;
        }

        enum Mode
        {
            Images,
            Models,
        }

        override protected void OnEnablePanel()
        {
            base.OnEnablePanel();
            m_CurrentPageFlipState = PageFlipState.TransitionIn;

            switch (m_EnabledCount)
            {
                case 0:
                    // The panel is created at app start. We do not want to trigger any heavy work at this point
                    // Current tab is set, but not enabled.
                    m_CurrentTab = m_Tabs[0];
                    break;
                case 1:
                    // The panel has been enabled for the first time. Enable the first tab.
                    SwitchTab(m_Tabs[0]);
                    break;
                default:
                    // If we have no filewatcher, we need to check if any files have changed since the user
                    // last had the panel open.
                    if (!App.PlatformConfig.UseFileSystemWatcher)
                    {
                        m_CurrentTab.Catalog.ForceCatalogScan();
                        RefreshPage();
                    }
                    break;
            }

            m_EnabledCount++;
        }

        void Update()
        {
            BaseUpdate();
            PageFlipUpdate();

            m_CurrentTab.UpdateTab();
            if (m_RefreshingSpinner != null)
            {
                m_RefreshingSpinner.SetActive(m_CurrentTab != null && m_CurrentTab.Catalog.IsScanning);
            }

            if (m_FolderNavButtonsNeedUpdate)
            {
                UpdateNavButtonState();
                m_FolderNavButtonsNeedUpdate = false;
            }
        }

        public override void OnWidgetHide()
        {
            // Reset all overlays for a clean slate on the panel respawn
            m_NoData.SetActive(false);
        }

        protected override void Awake()
        {
            base.Awake();
            foreach (var tab in m_Tabs)
            {
                tab.InitTab();
                tab.Catalog.CatalogChanged += OnCatalogChanged;
            }

            m_CurrentPageFlipState = PageFlipState.Standard;
        }

        protected void OnDestroy()
        {
            foreach (var tab in m_Tabs)
            {
                tab.Catalog.CatalogChanged -= OnCatalogChanged;
            }
        }

        protected override void OnStart()
        {
            RefreshPage();
        }

        public override void InitPanel()
        {
            base.InitPanel();
            foreach (var renderer in m_ExtraBorders)
            {
                renderer.material = m_BorderMaterial;
            }
        }

        public void ButtonPressed(ReferenceButton.Type rType)
        {
            if (rType == ReferenceButton.Type.AddAssets)
            {
                App.InitMediaLibraryPath();
                string dirName = App.MediaLibraryPath();
                System.Diagnostics.Process.Start(dirName);
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush, SketchControlsScript.kRemoveHeadsetFyi,
                    fPopScalar: 0.5f);
                return;
            }

            var newTab = m_Tabs.FirstOrDefault(x => x.ReferenceButtonType == rType);
            if (newTab == null || newTab == m_CurrentTab)
            {
                return;
            }

            SwitchTab(newTab);
        }

        public void SwitchTab(ReferencePanelTab newTab)
        {
            m_CurrentTab.OnTabDisable();
            m_CurrentTab = newTab;
            m_CurrentTab.OnTabEnable();

            m_PanelText.text = m_CurrentTab.PanelName;

            m_NumPages = m_CurrentTab.PageCount;

            GotoPage(m_CurrentTab.PageIndex);
            foreach (var button in m_CurrentTab.Buttons)
            {
                button.ResetScale();
            }
            RefreshPage();
        }

        override public void GotoPage(int iIndex)
        {
            m_RequestedPageIndex = iIndex;
            m_IndexOffset = m_RequestedPageIndex * m_CurrentTab.IconCount;
        }

        override public void AdvancePage(int iAmount)
        {
            m_RequestedPageIndex = PageIndex + iAmount;
        }

        protected override void RefreshPage()
        {
            foreach (var tab in m_Tabs)
            {
                tab.RefreshTab(tab == m_CurrentTab);
            }
            m_NumPages = m_CurrentTab.PageCount;

            string currentDir = m_CurrentTab.ReferenceButtonType switch
            {
                ReferenceButton.Type.Images => ReferenceImageCatalog.m_Instance.CurrentImagesDirectory,
                ReferenceButton.Type.BackgroundImages => BackgroundImageCatalog.m_Instance.CurrentBackgroundImagesDirectory,
                ReferenceButton.Type.Models => ModelCatalog.m_Instance.CurrentModelsDirectory,
                ReferenceButton.Type.Videos => VideoCatalog.Instance.CurrentVideoDirectory
            };

            var truncatedPath = currentDir.Substring(App.MediaLibraryPath().Length);
            if (m_DirectoryChooserPopupButton != null)
            {
                m_DirectoryChooserPopupButton.ButtonLabel = $"{truncatedPath}";
                m_CurrentSubdirectories = Directory.GetDirectories(currentDir);
            }

            base.RefreshPage();
            m_FolderNavButtonsNeedUpdate = m_DirectoryChooserPopupButton != null;
            UpdateInfoText();
        }

        private void UpdateNavButtonState()
        {
            if (m_CurrentTab.Catalog.IsSubDirectoryOfHome() && !m_CurrentTab.Catalog.IsHomeDirectory())
            {
                m_DirectoryHomeButton.SetButtonAvailable(true);
                m_DirectoryUpButton.SetButtonAvailable(true);

                m_DirectoryHomeButton.SetDescriptionUnavailable(false);
                m_DirectoryUpButton.SetDescriptionUnavailable(false);
            }
            else
            {
                m_DirectoryHomeButton.SetButtonAvailable(false);
                m_DirectoryUpButton.SetButtonAvailable(false);

                m_DirectoryHomeButton.SetDescriptionUnavailable(true);
                m_DirectoryUpButton.SetDescriptionUnavailable(true);
            }

            if (m_CurrentSubdirectories.Length == 0)
            {
                m_DirectoryChooserPopupButton.SetButtonAvailable(false);
                m_DirectoryChooserPopupButton.SetDescriptionUnavailable(true);
            }
            else
            {
                m_DirectoryChooserPopupButton.SetButtonAvailable(true);
                m_DirectoryChooserPopupButton.SetDescriptionUnavailable(false);
            }

            // Only show for truly empty home directory
            m_NoData.gameObject.SetActive(
                m_CurrentTab.Catalog.IsHomeDirectory() &&
                m_CurrentTab.Catalog.ItemCount == 0 &&
                m_CurrentSubdirectories.Length == 0
            );
        }

        void OnCatalogChanged()
        {
            RefreshPage();
        }

        protected override void OnUpdateGazeBehavior(Color rPanelColor)
        {
            base.OnUpdateGazeBehavior(rPanelColor);
            if (m_CurrentTab != null)
            {
                m_CurrentTab.OnUpdateGazeBehavior(rPanelColor, m_GazeActive,
                    m_CurrentState == PanelState.Available);
            }
        }

        protected override void UpdateButtonTransitionScale(float fScale)
        {
            m_CurrentTab.UpdateButtonTransitionScale(fScale);
        }

        public override bool RaycastAgainstMeshCollider(Ray ray, out RaycastHit hitInfo, float dist)
        {
            if (base.RaycastAgainstMeshCollider(ray, out hitInfo, dist))
            {
                return true;
            }
            return m_CurrentTab.RaycastAgainstMeshCollider(ray, out hitInfo, dist);
        }

        public void ChangeDirectoryForCurrentTab(string path)
        {
            m_CurrentTab.PageIndex = 0;
            GotoPage(m_CurrentTab.PageIndex);
            m_CurrentTab.Catalog.ChangeDirectory(path);
        }

        private void UpdateInfoText()
        {
            if (m_InfoText != null)
            {
                // TODO localize
                m_InfoText.text = $"{m_CurrentTab.Catalog.ItemCount} Files {m_CurrentSubdirectories.Length} Subfolders";
            }
        }

        public void ChangeRelativeFolderForCurrentTab(string relativePath)
        {
            var path = Path.Join(m_CurrentTab.Catalog.HomeDirectory, relativePath);
            ChangeDirectoryForCurrentTab(path);
        }

        public void HomeFolderForCurrentTab(string path)
        {
            ChangeDirectoryForCurrentTab(m_CurrentTab.Catalog.HomeDirectory);
        }

        public void NavigateUpForCurrentTab()
        {
            if (m_CurrentTab.Catalog.IsSubDirectoryOfHome() && !m_CurrentTab.Catalog.IsHomeDirectory())
            {
                var currentDir = new DirectoryInfo(m_CurrentTab.Catalog.GetCurrentDirectory());
                ChangeDirectoryForCurrentTab(currentDir.Parent.FullName);
            }
        }
    }
} // namespace TiltBrush
