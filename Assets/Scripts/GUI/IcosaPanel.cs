﻿// Copyright 2020 The Tilt Brush Authors
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

using UnityEngine;
using UnityEngine.Localization;
using TMPro;

namespace TiltBrush
{

    public enum IcosaSetType
    {
        User,
        Liked,
        Featured
    }

    public class IcosaPanel : ModalPanel
    {
        [SerializeField] private TextMeshPro m_PanelText;
        [SerializeField] private TextMeshPro m_PanelTextSubtitle;
        [SerializeField] private TextMeshPro m_PanelTextUserSubtitle;
        [SerializeField] private LocalizedString m_PanelTextStandard;
        public string PanelTextStandard { get { return m_PanelTextStandard.GetLocalizedStringAsync().Result; } }
        [SerializeField] private LocalizedString m_PanelTextFeatured;
        public string PanelTextFeatured { get { return m_PanelTextFeatured.GetLocalizedStringAsync().Result; } }
        [SerializeField] private LocalizedString m_PanelTextLiked; // Liked Models
        public string PanelTextLiked { get { return m_PanelTextLiked.GetLocalizedStringAsync().Result; } }
        [SerializeField] private Renderer m_PolyGalleryRenderer;
        [SerializeField] private GameObject m_NoObjectsMessage;
        [SerializeField] private GameObject m_InternetError;
        [SerializeField] private GameObject m_NoAuthoredModelsMessage;
        [SerializeField] private GameObject m_NoLikesMessage;
        [SerializeField] private GameObject m_NotLoggedInMessage;
        [SerializeField] private GameObject m_OutOfDateMessage;
        [SerializeField] private GameObject m_NoPolyConnectionMessage;

        private IcosaSetType m_CurrentSet;
        public IcosaSetType CurrentSet => m_CurrentSet;
        private bool m_LoggedIn;

        // State for automatically loading models.
        int m_LastPageIndexForLoad = -1;
        IcosaSetType m_LastSetTypeForLoad = IcosaSetType.User;

        public bool ShowingFeatured { get { return m_CurrentSet == IcosaSetType.Featured; } }
        public bool ShowingLikes { get { return m_CurrentSet == IcosaSetType.Liked; } }
        public bool ShowingUser { get { return m_CurrentSet == IcosaSetType.User; } }



        override public void OnWidgetShowAnimComplete()
        {
            SetVisiblePolySet(m_CurrentSet);
        }

        public override void InitPanel()
        {
            base.InitPanel();

            m_LoggedIn = App.IcosaIsLoggedIn;

            m_NoObjectsMessage.SetActive(false);
            m_InternetError.SetActive(false);
            m_NoAuthoredModelsMessage.SetActive(false);
            m_NoLikesMessage.SetActive(false);
            m_NotLoggedInMessage.SetActive(false);
            m_OutOfDateMessage.SetActive(false);
            m_NoPolyConnectionMessage.SetActive(false);
        }

        public override bool IsInButtonMode(ModeButton button)
        {
            var polySetButton = button as IcosaSetButton;
            return polySetButton && polySetButton.m_ButtonType == m_CurrentSet;
        }

        protected override void OnStart()
        {
            // Initialize icons.
            IcosaModelButton[] rPanelButtons = m_Mesh.GetComponentsInChildren<IcosaModelButton>();
            foreach (IcosaModelButton icon in rPanelButtons)
            {
                GameObject go = icon.gameObject;
                icon.SetButtonGrayscale(icon);
                go.SetActive(false);
                Icons.Add(icon);
            }

            m_CurrentSet = IcosaSetType.User;

            // Make sure Poly gallery button starts at greyscale when panel is initialized
            m_PolyGalleryRenderer.material.SetFloat("_Grayscale", 1);

            App.IcosaAssetCatalog.RequestAutoRefresh(m_CurrentSet);
            App.IcosaAssetCatalog.CatalogChanged += OnIcosaAssetCatalogChanged;
        }

        void SetVisiblePolySet(IcosaSetType type)
        {
            m_CurrentSet = type;
            RefreshCurrentSet(false);
        }

        public void RefreshCurrentSet(bool forced)
        {
            if (forced)
            {
                App.IcosaAssetCatalog.RequestForcedRefresh(m_CurrentSet);
            }
            else
            {
                App.IcosaAssetCatalog.RequestAutoRefresh(m_CurrentSet);
            }
            ResetPageIndex();
            RefreshPage();
        }

        void OnIcosaAssetCatalogChanged()
        {
            RefreshPage();
        }

        protected override void RefreshPage()
        {
            m_NoLikesMessage.SetActive(false);
            m_NoAuthoredModelsMessage.SetActive(false);
            m_NotLoggedInMessage.SetActive(false);
            m_InternetError.SetActive(false);
            if (VrAssetService.m_Instance.NoConnection)
            {
                m_NoPolyConnectionMessage.SetActive(true);
                RefreshPanelText();
                base.RefreshPage();
                return;
            }
            if (!VrAssetService.m_Instance.Available)
            {
                m_OutOfDateMessage.SetActive(true);
                RefreshPanelText();
                base.RefreshPage();
                return;
            }

            m_NumPages = ((App.IcosaAssetCatalog.NumCloudModels(m_CurrentSet) - 1) / Icons.Count) + 1;
            int numCloudModels = App.IcosaAssetCatalog.NumCloudModels(m_CurrentSet);

            if (m_LastPageIndexForLoad != PageIndex || m_LastSetTypeForLoad != m_CurrentSet)
            {
                // Unload the previous page's models.

                // This function may be called multiple times as icons load, only unload the old models once,
                // otherwise the current page's models will be thrashed.
                m_LastPageIndexForLoad = PageIndex;
                m_LastSetTypeForLoad = m_CurrentSet;

                // Destroy previews so only the thumbnail is visible.
                for (int i = 0; i < Icons.Count; i++)
                {
                    ((ModelButton)Icons[i]).DestroyModelPreview();
                }

                App.IcosaAssetCatalog.UnloadUnusedModels();
            }

            for (int i = 0; i < Icons.Count; i++)
            {
                IcosaModelButton icon = (IcosaModelButton)Icons[i];
                // Set sketch index relative to page based index
                int iMapIndex = m_IndexOffset + i;

                // Init icon according to availability of sketch
                GameObject go = icon.gameObject;
                if (iMapIndex < numCloudModels)
                {
                    IcosaAssetCatalog.AssetDetails asset =
                        App.IcosaAssetCatalog.GetPolyAsset(m_CurrentSet, iMapIndex);
                    go.SetActive(true);

                    if (icon.Asset != null && asset.AssetId != icon.Asset.AssetId)
                    {
                        icon.DestroyModelPreview();
                    }
                    icon.SetPreset(asset, iMapIndex);

                    // Note that App.UserConfig.Flags.PolyModelPreload falls through to
                    // App.PlatformConfig.EnablePolyPreload if it isn't set in Tilt Brush.cfg.
                    if (App.UserConfig.Flags.IcosaModelPreload)
                    {
                        icon.RequestModelPreload(PageIndex);
                    }
                }
                else
                {
                    go.SetActive(false);
                }
            }

            // Use featured model count as a proxy for "icosa is working"
            bool internetError = App.IcosaAssetCatalog.NumCloudModels(IcosaSetType.Featured) == 0;
            m_InternetError.SetActive(internetError);

            RefreshPanelText();
            switch (m_CurrentSet)
            {
                case IcosaSetType.User:
                    if (!internetError)
                    {
                        if (App.IcosaIsLoggedIn)
                        {
                            if (numCloudModels == 0)
                            {
                                m_NoAuthoredModelsMessage.SetActive(true);
                            }
                        }
                        else
                        {
                            m_NotLoggedInMessage.SetActive(true);
                        }
                    }
                    break;
                case IcosaSetType.Liked:
                    if (!internetError)
                    {
                        if (App.IcosaIsLoggedIn)
                        {
                            if (numCloudModels == 0)
                            {
                                m_NoLikesMessage.SetActive(true);
                            }
                        }
                        else
                        {
                            m_NotLoggedInMessage.SetActive(true);
                        }
                    }
                    break;
            }

            base.RefreshPage();
        }

        void RefreshPanelText()
        {
            switch (m_CurrentSet)
            {
                case IcosaSetType.User:
                    m_PanelText.text = PanelTextStandard;
                    m_PanelTextSubtitle.gameObject.SetActive(false);
                    m_PanelTextUserSubtitle.gameObject.SetActive(true);
                    break;
                case IcosaSetType.Featured:
                    m_PanelText.text = PanelTextFeatured;
                    m_PanelTextSubtitle.gameObject.SetActive(false);
                    m_PanelTextUserSubtitle.gameObject.SetActive(false);
                    break;
                case IcosaSetType.Liked:
                    m_PanelText.text = PanelTextLiked;
                    m_PanelTextSubtitle.gameObject.SetActive(true);
                    m_PanelTextUserSubtitle.gameObject.SetActive(false);
                    break;
            }
        }

        void Update()
        {
            BaseUpdate();

            // Update share button's text.
            bool loggedIn = App.IcosaIsLoggedIn;
            if (loggedIn != m_LoggedIn)
            {
                m_LoggedIn = loggedIn;
                RefreshPage();
            }

            PageFlipUpdate();
        }

        override protected void OnUpdateActive()
        {
            // If we're not active, hide all our preview panels
            if (!m_GazeActive)
            {
                m_PolyGalleryRenderer.material.SetFloat("_Grayscale", 1);
                foreach (var baseButton in Icons)
                {
                    IcosaModelButton icon = (IcosaModelButton)baseButton;
                    icon.ResetState();
                    icon.SetButtonGrayscale(icon);
                }

            }
            else if (m_CurrentState == PanelState.Available)
            {
                m_PolyGalleryRenderer.material.SetFloat("_Grayscale", 0);
                foreach (var baseButton in Icons)
                {
                    IcosaModelButton icon = (IcosaModelButton)baseButton;
                    icon.SetButtonGrayscale(false);
                }
            }
        }

        // Works specifically with PolySetButtons.
        public void ButtonPressed(IcosaSetType rType)
        {
            SetVisiblePolySet(rType);
        }

        public void SetInitialSearchText(KeyboardPopupButton btn)
        {
            btn.m_CommandParam = (int)m_CurrentSet;
            KeyboardPopUpWindow.m_InitialText = CurrentQuery.SearchText;
        }

        public IcosaAssetCatalog.QueryParameters CurrentQuery =>
            App.IcosaAssetCatalog.QueryOptionParametersForSet(m_CurrentSet);
    }
} // namespace TiltBrush
