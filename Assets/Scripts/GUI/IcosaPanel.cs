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

using UnityEngine;
using UnityEngine.Localization;
using TMPro;
using System.Collections.Generic;

namespace TiltBrush
{

    public enum IcosaSetType
    {
        User,
        Liked,
        Featured,
        AllModels
    }

    public enum IcosaBrowseMode
    {
        Standard,   // Browse individual assets
        Collections // Browse collections
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
        [SerializeField] private LocalizedString m_PanelTextAllModels;
        public string PanelTextAllModels { get { return m_PanelTextAllModels.GetLocalizedStringAsync().Result; } }
        [SerializeField] private LocalizedString m_PanelTextLiked; // Liked Models
        public string PanelTextLiked { get { return m_PanelTextLiked.GetLocalizedStringAsync().Result; } }
        [SerializeField] private LocalizedString m_PanelTextUserCollections; // User Collections
        public string PanelTextUserCollections { get { return m_PanelTextUserCollections.GetLocalizedStringAsync().Result; } }
        [SerializeField] private LocalizedString m_PanelTextFeaturedCollections; // Public Collections
        public string PanelTextFeaturedCollections { get { return m_PanelTextFeaturedCollections.GetLocalizedStringAsync().Result; } }
        [SerializeField] private Renderer m_PolyGalleryRenderer;
        [SerializeField] private GameObject m_NoObjectsMessage;
        [SerializeField] private GameObject m_InternetError;
        [SerializeField] private GameObject m_NoAuthoredModelsMessage;
        [SerializeField] private GameObject m_NoLikesMessage;
        [SerializeField] private GameObject m_NoCollectionsMessage;
        [SerializeField] private GameObject m_NotLoggedInMessage;
        [SerializeField] private GameObject m_OutOfDateMessage;
        [SerializeField] private GameObject m_NotSupportedMessage;
        [SerializeField] private GameObject m_NoPolyConnectionMessage;
        [SerializeField] private HoverIcon m_FilterInfoIcon;

        private IcosaSetType m_CurrentSet;
        public IcosaSetType CurrentSet => m_CurrentSet;
        private IcosaBrowseMode m_BrowseMode;
        public IcosaBrowseMode BrowseMode => m_BrowseMode;
        private string m_CurrentCollectionId; // When viewing assets from a specific collection
        public string CurrentCollectionId => m_CurrentCollectionId;
        private bool m_LoggedIn;

        // State for automatically loading models.
        int m_LastPageIndexForLoad = -1;
        IcosaSetType m_LastSetTypeForLoad = IcosaSetType.User;
        IcosaBrowseMode m_LastBrowseModeForLoad = IcosaBrowseMode.Standard;

        // Flag to defer RefreshPage to once per frame
        private bool m_RefreshRequested = false;
        private Dictionary<IcosaSetType, float> m_CooldownByType = new Dictionary<IcosaSetType, float>();

        public bool ShowingFeatured { get { return m_CurrentSet == IcosaSetType.Featured; } }
        public bool ShowingAllModels { get { return m_CurrentSet == IcosaSetType.AllModels; } }
        public bool ShowingLikes { get { return m_CurrentSet == IcosaSetType.Liked; } }
        public bool ShowingUser { get { return m_CurrentSet == IcosaSetType.User; } }
        public bool InCollectionsMode { get { return m_BrowseMode == IcosaBrowseMode.Collections; } }
        public bool InStandardMode { get { return m_BrowseMode == IcosaBrowseMode.Standard; } }

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
            m_NoCollectionsMessage.SetActive(false);
            m_NotLoggedInMessage.SetActive(false);
            m_OutOfDateMessage.SetActive(false);
            if (m_NotSupportedMessage)
                m_NotSupportedMessage.SetActive(false);
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
            // Rate-limit RefreshPage calls to prevent cascade from rapid CatalogChanged events
            if (!m_CooldownByType.ContainsKey(m_CurrentSet))
            {
                m_CooldownByType[m_CurrentSet] = 0f;
            }

            if (m_CooldownByType[m_CurrentSet] <= 0f)
            {
                RefreshPage();
                m_CooldownByType[m_CurrentSet] = MIN_REFRESH_INTERVAL;
            }
        }
        private const float MIN_REFRESH_INTERVAL = 0.5f; // 500ms between refreshes per set

        protected override void RefreshPage()
        {
            m_NoLikesMessage.SetActive(false);
            m_NoAuthoredModelsMessage.SetActive(false);
            m_NoCollectionsMessage.SetActive(false);
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

            // Determine item count based on browse mode
            int numItems;
            if (InCollectionsMode)
            {
                numItems = App.IcosaAssetCatalog.NumCollections(m_CurrentSet);
            }
            else
            {
                numItems = App.IcosaAssetCatalog.NumCloudModels(m_CurrentSet);
            }
            m_NumPages = ((numItems - 1) / Icons.Count) + 1;

            // [ICOSALOAD] time the whole main-thread RefreshPage body and its sub-phases.
            var __rpSw = System.Diagnostics.Stopwatch.StartNew();
            long __unloadMs = 0;

            if (m_LastPageIndexForLoad != PageIndex || m_LastSetTypeForLoad != m_CurrentSet ||
                m_LastBrowseModeForLoad != m_BrowseMode)
            {
                // Unload the previous page's models.

                // This function may be called multiple times as icons load, only unload the old models once,
                // otherwise the current page's models will be thrashed.
                m_LastPageIndexForLoad = PageIndex;
                m_LastSetTypeForLoad = m_CurrentSet;
                m_LastBrowseModeForLoad = m_BrowseMode;

                var __unloadSw = System.Diagnostics.Stopwatch.StartNew();
                // Destroy the on-button preview instances so only the thumbnail is visible. This does
                // NOT unload the underlying Model geometry - those stay cached in IcosaAssetCatalog
                // (subject to its memory budget) so returning to this page/tab doesn't re-import them.
                for (int i = 0; i < Icons.Count; i++)
                {
                    ((ModelButton)Icons[i]).DestroyModelPreview();
                }
                __unloadMs = __unloadSw.ElapsedMilliseconds;
            }

            var __iconLoopSw = System.Diagnostics.Stopwatch.StartNew();
            var visibleAssetIds = new List<string>();
            for (int i = 0; i < Icons.Count; i++)
            {
                IcosaModelButton icon = (IcosaModelButton)Icons[i];
                // Set sketch index relative to page based index
                int iMapIndex = m_IndexOffset + i;

                // Init icon according to availability of sketch or collection
                GameObject go = icon.gameObject;
                if (iMapIndex < numItems)
                {
                    go.SetActive(true);

                    if (InCollectionsMode)
                    {
                        // Display collection
                        IcosaAssetCatalog.CollectionDetails collection =
                            App.IcosaAssetCatalog.GetIcosaCollection(m_CurrentSet, iMapIndex);
                        icon.SetPresetCollection(collection, iMapIndex);
                    }
                    else
                    {
                        // Display asset
                        IcosaAssetCatalog.AssetDetails asset =
                            App.IcosaAssetCatalog.GetIcosaAsset(m_CurrentSet, iMapIndex);
                        visibleAssetIds.Add(asset.AssetId);

                        if (icon.Asset != null && asset.AssetId != icon.Asset.AssetId)
                        {
                            icon.DestroyModelPreview();
                        }
                        icon.SetPreset(asset, iMapIndex);

                        // Note that App.UserConfig.Flags.IcosaModelPreload falls through to
                        // App.PlatformConfig.EnableIcosaPreload if it isn't set in Tilt Brush.cfg.
                        // The flag allows previews for cached models, plus remote models whose selected
                        // download format is not backed by the Internet Archive.
                        // Gating preload also gates the preview, since the preview is built from the
                        // loaded model.
                        int maxPreviewTris = App.UserConfig.Flags.IcosaMaxPreviewTriangleCount;
                        bool tooManyTris = maxPreviewTris > 0 && asset.TriangleCount > maxPreviewTris;
                        bool measuredOversized = App.IcosaAssetCatalog.IsModelOversized(asset.AssetId);
                        bool tooComplexToPreview = tooManyTris || measuredOversized;
                        if (tooComplexToPreview)
                        {
                            Debug.Log($"[ICOSALOAD] skip preview/preload {asset.AssetId} " +
                                $"tris={asset.TriangleCount} (limit={maxPreviewTris}) oversized={measuredOversized}");
                        }
                        bool canPreload = App.IcosaAssetCatalog.HasCachedModel(asset.AssetId)
                            || App.IcosaAssetCatalog.CanAutoDownloadForPreview(asset.AssetId);
                        if (App.UserConfig.Flags.IcosaModelPreload && !tooComplexToPreview && canPreload)
                        {
                            icon.RequestModelPreload(PageIndex);
                        }
                    }
                }
                else
                {
                    go.SetActive(false);
                }
            }
            // Pin the page we're showing so the memory budget can't evict (and thus thrash-reload) it.
            App.IcosaAssetCatalog.SetVisibleModels(visibleAssetIds);

            long __iconLoopMs = __iconLoopSw.ElapsedMilliseconds;

            // Use featured model count as a proxy for "icosa is working"
            bool internetError = App.IcosaAssetCatalog.NumCloudModels(IcosaSetType.Featured) == 0;
            m_InternetError.SetActive(internetError);

            var __panelTextSw = System.Diagnostics.Stopwatch.StartNew();
            RefreshPanelText();
            long __panelTextMs = __panelTextSw.ElapsedMilliseconds;
            switch (m_CurrentSet)
            {
                case IcosaSetType.User:
                    if (!internetError)
                    {
                        if (App.IcosaIsLoggedIn)
                        {
                            if (numItems == 0)
                            {
                                if (InCollectionsMode)
                                {
                                    m_NoCollectionsMessage.SetActive(true);
                                }
                                else
                                {
                                    m_NoAuthoredModelsMessage.SetActive(true);
                                }
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
                            if (numItems == 0)
                            {
                                if (InCollectionsMode)
                                {
                                    m_NoCollectionsMessage.SetActive(true);
                                }
                                else
                                {
                                    m_NoLikesMessage.SetActive(true);
                                }
                            }
                        }
                        else
                        {
                            m_NotLoggedInMessage.SetActive(true);
                        }
                    }
                    break;
                case IcosaSetType.Featured:
                    if (!internetError && InCollectionsMode && numItems == 0)
                    {
                        m_NoCollectionsMessage.SetActive(true);
                    }
                    break;
            }

            __rpSw.Stop();
            if (__rpSw.ElapsedMilliseconds >= 5)
            {
                Debug.Log($"[ICOSALOAD] RefreshPage set={m_CurrentSet} total={__rpSw.ElapsedMilliseconds}ms " +
                    $"(unload={__unloadMs}ms iconLoop={__iconLoopMs}ms panelText={__panelTextMs}ms)");
            }


            m_FilterInfoIcon.SetDescriptionText($"Filtering {CurrentSetFriendlyName} by:");
            m_FilterInfoIcon.SetExtraDescriptionText(CurrentQuery.FriendlyString);

            base.RefreshPage();
        }

        public string CurrentSetFriendlyName => m_CurrentSet switch
        {
            IcosaSetType.User => "Your Models",
            IcosaSetType.Liked => "Liked Models",
            IcosaSetType.Featured => "Featured Models",
            IcosaSetType.AllModels => "All Models",
            _ => "Models"
        };


        void RefreshPanelText()
        {
            if (InCollectionsMode)
            {
                // In collections mode, show different text based on which tab
                switch (m_CurrentSet)
                {
                    case IcosaSetType.User:
                        m_PanelText.text = PanelTextUserCollections;
                        m_PanelTextSubtitle.gameObject.SetActive(false);
                        m_PanelTextUserSubtitle.gameObject.SetActive(true);
                        break;
                    case IcosaSetType.Featured:
                        m_PanelText.text = PanelTextFeaturedCollections;
                        m_PanelTextSubtitle.gameObject.SetActive(false);
                        m_PanelTextUserSubtitle.gameObject.SetActive(false);
                        break;
                    case IcosaSetType.Liked:
                        // Liked collections not yet supported by API
                        m_PanelText.text = PanelTextLiked;
                        m_PanelTextSubtitle.gameObject.SetActive(true);
                        m_PanelTextUserSubtitle.gameObject.SetActive(false);
                        break;
                    case IcosaSetType.AllModels:
                        m_PanelText.text = PanelTextFeaturedCollections;
                        m_PanelTextSubtitle.gameObject.SetActive(false);
                        m_PanelTextUserSubtitle.gameObject.SetActive(false);
                        break;
                }
            }
            else
            {
                // Standard mode
                switch (m_CurrentSet)
                {
                    case IcosaSetType.User:
                        m_PanelText.text = PanelTextStandard;
                        m_PanelTextSubtitle.gameObject.SetActive(false);
                        m_PanelTextUserSubtitle.gameObject.SetActive(true);
                        break;
                    case IcosaSetType.Featured:
                        m_PanelText.text = PanelTextFeatured;
                        m_PanelTextSubtitle.gameObject.SetActive(true);
                        m_PanelTextUserSubtitle.gameObject.SetActive(false);
                        break;
                    case IcosaSetType.AllModels:
                        m_PanelText.text = PanelTextAllModels;
                        m_PanelTextSubtitle.gameObject.SetActive(true);
                        m_PanelTextUserSubtitle.gameObject.SetActive(false);
                        break;
                    case IcosaSetType.Liked:
                        m_PanelText.text = PanelTextLiked;
                        m_PanelTextSubtitle.gameObject.SetActive(true);
                        m_PanelTextUserSubtitle.gameObject.SetActive(false);
                        break;
                }
            }
        }

        void Update()
        {
            BaseUpdate();

            // Decrement all cooldowns
            var keys = new List<IcosaSetType>(m_CooldownByType.Keys);
            foreach (var key in keys)
            {
                if (m_CooldownByType[key] > 0f)
                {
                    m_CooldownByType[key] -= UnityEngine.Time.deltaTime;
                }
            }

            // Update share button's text.
            bool loggedIn = App.IcosaIsLoggedIn;
            if (loggedIn != m_LoggedIn)
            {
                App.IcosaAssetCatalog.RequestForcedRefresh(IcosaSetType.Liked);
                App.IcosaAssetCatalog.RequestForcedRefresh(IcosaSetType.User);
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

        public IcosaAssetCatalog.IcosaQueryParameters CurrentQuery =>
            App.IcosaAssetCatalog.QueryOptionParametersForSet(m_CurrentSet);

        // Toggle between Standard and Collections mode
        public void ToggleBrowseMode()
        {
            m_BrowseMode = m_BrowseMode == IcosaBrowseMode.Standard
                ? IcosaBrowseMode.Collections
                : IcosaBrowseMode.Standard;
            m_CurrentCollectionId = null; // Clear collection filter when toggling
            ResetPageIndex();
            RefreshCurrentSet(false);
        }

        // Enter collections browse mode
        public void EnterCollectionsMode()
        {
            if (m_BrowseMode != IcosaBrowseMode.Collections)
            {
                m_BrowseMode = IcosaBrowseMode.Collections;
                m_CurrentCollectionId = null;
                ResetPageIndex();
                RefreshCurrentSet(false);
            }
        }

        // Enter standard browse mode
        public void EnterStandardMode()
        {
            if (m_BrowseMode != IcosaBrowseMode.Standard)
            {
                m_BrowseMode = IcosaBrowseMode.Standard;
                m_CurrentCollectionId = null;
                ResetPageIndex();
                RefreshCurrentSet(false);
            }
        }

        // View assets from a specific collection
        public void ViewCollection(string collectionId)
        {
            m_CurrentCollectionId = collectionId;
            m_BrowseMode = IcosaBrowseMode.Standard;
            ResetPageIndex();
            RefreshCurrentSet(false);
        }

        // Clear collection filter and return to normal browse mode
        public void ClearCollectionFilter()
        {
            if (m_CurrentCollectionId != null)
            {
                m_CurrentCollectionId = null;
                ResetPageIndex();
                RefreshCurrentSet(false);
            }
        }
    }
} // namespace TiltBrush
