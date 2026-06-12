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
        [SerializeField] private RectTransform m_SketchGridContent;
        [SerializeField] private NoHeadsetSketchGridItem m_SketchGridItemPrefab;
        [SerializeField] private GameObject m_EmptySketchListMessage;
        [SerializeField] private Texture2D m_UnknownImageTexture;
        [SerializeField] private Texture2D m_LoadingImageTexture;
        [SerializeField] public Texture2D m_ClickCursor;

        private readonly List<SketchGridEntry> m_Sketches = new List<SketchGridEntry>();
        private readonly List<NoHeadsetSketchGridItem> m_GridItems =
            new List<NoHeadsetSketchGridItem>();
        private readonly Dictionary<SketchSetType, SketchSet> m_VisibleSets =
            new Dictionary<SketchSetType, SketchSet>();
        private readonly Dictionary<SketchSetType, Button> m_CategoryTabs =
            new Dictionary<SketchSetType, Button>();
        private readonly HashSet<string> m_DownloadingSketchKeys = new HashSet<string>();
        private readonly List<Sprite> m_GeneratedSprites = new List<Sprite>();
        private readonly List<Sprite> m_ThumbnailSprites = new List<Sprite>();
        private readonly List<Texture2D> m_OwnedThumbnailTextures = new List<Texture2D>();
        private static bool sm_HasSavedGridState;
        private static readonly Dictionary<SketchSetType, float> sm_SavedScrollPositions =
            new Dictionary<SketchSetType, float>();
        private TMP_Dropdown m_Dropdown;
        private GameObject m_ViewSketchButton;
        private GameObject m_RuntimeGridRoot;
        private RectTransform m_RuntimeStackRect;
        private RectTransform m_RuntimeTabBarRect;
        private RectTransform m_RuntimeGridRect;
        private ScrollRect m_SketchGridScrollRect;
        private NoHeadsetSketchGridLayout m_SketchGridLayout;
        private Vector2Int m_LastScreenSize;
        private Sprite m_LoadingSprite;
        private Sprite m_UnknownSprite;
        private SketchSetType m_SelectedSetType = SketchSetType.Curated;
        private TMP_FontAsset m_RuntimeFontAsset;
        private Material m_RuntimeFontMaterial;
        private bool m_HasSavedCursorState;
        private bool m_PreviousCursorVisible;
        private CursorLockMode m_PreviousCursorLockState;
        private string m_VisibleSketchSignature = "";
        private string m_ThumbnailRequestSignature = "";
        private bool m_CuratedDownloadsActive;
        private bool m_RestoreSavedScrollOnNextRefresh;

        private const int BatchSize = 2;
        private const int MaxSketches = 20;
        private const int LocalThumbnailLoadsPerFrame = 8;
        private const float LocalGridCellAspect = 0.95f;
        private const float RemoteGridCellAspect = 1.12f;
        private const string LogPrefix = "NOXR_GRID";

        public static InitNoHeadsetMode m_Instance;

        private class SketchGridEntry
        {
            public SketchSet SketchSet;
            public SketchSetType SetType;
            public int SketchIndex;
            public SceneFileInfo SceneFileInfo;
            public string DisplayName;
            public string AuthorLabel;
            public bool AuthorMetadataAssigned;
            public bool ThumbnailAssigned;
        }

        void Start()
        {
            m_Instance = this;
            m_SelectedSetType = SketchSetType.Curated;
            if (sm_HasSavedGridState)
            {
                m_RestoreSavedScrollOnNextRefresh = true;
            }
            Debug.Log($"{LogPrefix} startup selected tab {m_SelectedSetType}");
            App.Instance.m_NoVrUi.SetActive(true);
            CacheAndShowCursor();
            InitializeGridUi();
            RefreshSketchGrid();
            StartCoroutine(DownloadAllCuratedSketchesInBatches(BatchSize, MaxSketches));
        }

        private void Update()
        {
            NoHeadsetPointerCursor.ForcePointerVisible();
            RefreshRuntimeGridFrame();
            RefreshVisibleThumbnails();
        }

        private IEnumerator DownloadAllCuratedSketchesInBatches(int numSketches, int maxSketches)
        {
            var curatedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Curated) as IcosaSketchSet;
            if (curatedSketchSet == null)
            {
                Debug.LogWarning($"{LogPrefix} curated sketch set is not downloadable");
                RefreshSketchGrid();
                yield break;
            }

            m_CuratedDownloadsActive = true;
            while (true)
            {
                yield return StartCoroutine(DownloadCuratedSketches(numSketches, maxSketches));
                RefreshSketchGridAfterCuratedBatch();

                int available = 0;
                for (int i = 0; i < curatedSketchSet.NumSketches; i++)
                {
                    var info = curatedSketchSet.GetSketchSceneFileInfo(i);
                    if (info != null && info.Available)
                    {
                        available++;
                    }
                }

                if (available >= maxSketches)
                {
                    break;
                }

                if (available >= curatedSketchSet.NumSketches)
                {
                    Debug.LogWarning($"{LogPrefix} curated downloads stopped at {available} available sketches");
                    break;
                }
            }
            m_CuratedDownloadsActive = false;
            RefreshSketchGrid();
        }

        public IEnumerator DownloadCuratedSketches(int numSketches, int maxSketches)
        {
            var curatedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Curated) as IcosaSketchSet;
            if (curatedSketchSet == null)
            {
                yield break;
            }

            int alreadyDownloaded = 0;
            for (int i = 0; i < curatedSketchSet.NumSketches; i++)
            {
                var info = curatedSketchSet.GetSketchSceneFileInfo(i);
                if (info != null && info.Available)
                {
                    alreadyDownloaded++;
                }
            }

            int toDownload = Mathf.Min(numSketches, maxSketches - alreadyDownloaded);
            if (toDownload <= 0)
            {
                yield break;
            }

            yield return new WaitUntil(() => curatedSketchSet.NumSketches >= alreadyDownloaded + toDownload);

            List<int> indicesToDownload = new List<int>();
            for (int i = alreadyDownloaded; i < alreadyDownloaded + toDownload; i++)
            {
                indicesToDownload.Add(i);
            }

            yield return StartCoroutine(curatedSketchSet.DownloadFilesCoroutine(indicesToDownload, () =>
            {
                RefreshSketchGridAfterCuratedBatch();
            }));
        }

        private void RefreshSketchGridAfterCuratedBatch()
        {
            if (m_SelectedSetType != SketchSetType.Curated || !m_CuratedDownloadsActive)
            {
                RefreshSketchGrid();
            }
        }

        public void OnClickOutsideDropdown()
        {
            if (m_Dropdown != null && m_Dropdown.gameObject.activeSelf)
            {
                m_Dropdown.Hide();
            }
        }

        public void RefreshDropdownItemsForSet(SketchSet sketchset)
        {
            RefreshSketchGrid();
        }

        public void RefreshSketchGrid()
        {
            if (m_SketchGridContent == null || m_SketchGridItemPrefab == null)
            {
                Debug.LogError($"{LogPrefix} missing sketch grid UI references");
                return;
            }

            var userSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User);
            var curatedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Curated);
            var likedSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Liked);

            int userCount = CountSelectableSketches(userSketchSet);
            int curatedCount = CountSelectableSketches(curatedSketchSet);
            int likedCount = App.IcosaIsLoggedIn ? CountSelectableSketches(likedSketchSet) : 0;

            if (m_SelectedSetType != SketchSetType.Curated
                && !HasSelectableSketchesForSet(m_SelectedSetType, userCount, curatedCount, likedCount))
            {
                m_SelectedSetType = GetFirstSelectableSetType(userCount, curatedCount, likedCount);
            }
            bool selectedSetLoading = IsSetLoading(m_SelectedSetType, userSketchSet,
                curatedSketchSet, likedSketchSet);
            if (m_SelectedSetType == SketchSetType.Curated && curatedCount == 0)
            {
                selectedSetLoading = true;
            }
            UpdateCategoryTabs(userCount, curatedCount, likedCount);
            UpdateGridCellAspect();

            List<SketchGridEntry> visibleSketches = new List<SketchGridEntry>();
            switch (m_SelectedSetType)
            {
                case SketchSetType.User:
                    AddSketchGridEntries(userSketchSet, SketchSetType.User, visibleSketches);
                    break;
                case SketchSetType.Curated:
                    AddSketchGridEntries(curatedSketchSet, SketchSetType.Curated, visibleSketches);
                    break;
                case SketchSetType.Liked:
                    AddSketchGridEntries(likedSketchSet, SketchSetType.Liked, visibleSketches);
                    break;
                default:
                    AddSketchGridEntries(userSketchSet, SketchSetType.User, visibleSketches);
                    break;
            }

            string signature = GetSketchListSignature(m_SelectedSetType, visibleSketches);
            if (signature != m_VisibleSketchSignature)
            {
                ApplyVisibleSketches(visibleSketches, signature);
            }
            else
            {
                RefreshVisibleSketchEntries(visibleSketches);
                RefreshVisibleTileText();
            }

            if (m_EmptySketchListMessage != null)
            {
                bool showStatusMessage = m_Sketches.Count == 0
                    && (selectedSetLoading
                        || HasSelectableSketchesForSet(m_SelectedSetType, userCount, curatedCount, likedCount));
                SetSketchListStatusMessage(showStatusMessage, selectedSetLoading);
            }
            SetGridActive(m_Sketches.Count > 0
                || selectedSetLoading
                || HasSelectableSketchesForSet(m_SelectedSetType, userCount, curatedCount, likedCount));

            if (m_RestoreSavedScrollOnNextRefresh)
            {
                m_RestoreSavedScrollOnNextRefresh = false;
                StartCoroutine(RestoreGridScrollPositionAfterLayout());
            }
        }

        private int CountSelectableSketches(SketchSet sketchset)
        {
            if (sketchset == null || !sketchset.IsReadyForAccess)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < sketchset.NumSketches; i++)
            {
                var info = sketchset.GetSketchSceneFileInfo(i);
                if (info != null && sketchset.IsSketchIndexValid(i))
                {
                    count++;
                }
            }
            return count;
        }

        private bool HasSelectableSketchesForSet(SketchSetType setType, int userCount, int curatedCount,
            int likedCount)
        {
            switch (setType)
            {
                case SketchSetType.User:
                    return userCount > 0;
                case SketchSetType.Curated:
                    return curatedCount > 0;
                case SketchSetType.Liked:
                    return App.IcosaIsLoggedIn && likedCount > 0;
                default:
                    return false;
            }
        }

        private bool IsSetLoading(SketchSetType setType, SketchSet userSketchSet,
            SketchSet curatedSketchSet, SketchSet likedSketchSet)
        {
            SketchSet sketchSet = null;
            switch (setType)
            {
                case SketchSetType.User:
                    sketchSet = userSketchSet;
                    break;
                case SketchSetType.Curated:
                    sketchSet = curatedSketchSet;
                    break;
                case SketchSetType.Liked:
                    sketchSet = likedSketchSet;
                    break;
            }

            if (sketchSet == null)
            {
                return false;
            }
            return !sketchSet.IsReadyForAccess || sketchSet.IsActivelyRefreshingSketches
                || (setType == SketchSetType.Curated && m_CuratedDownloadsActive);
        }

        private void SetSketchListStatusMessage(bool active, bool loading)
        {
            m_EmptySketchListMessage.SetActive(active);
            if (!active)
            {
                return;
            }

            TextMeshProUGUI text = m_EmptySketchListMessage.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                return;
            }
            text.text = loading ? GetLoadingMessage(m_SelectedSetType) : "No sketches are ready to load.";
        }

        private string GetLoadingMessage(SketchSetType setType)
        {
            switch (setType)
            {
                case SketchSetType.Curated:
                    return "Loading featured sketches...";
                case SketchSetType.Liked:
                    return "Loading liked sketches...";
                default:
                    return "Loading sketches...";
            }
        }

        private SketchSetType GetFirstSelectableSetType(int userCount, int curatedCount, int likedCount)
        {
            if (curatedCount > 0)
            {
                return SketchSetType.Curated;
            }
            if (userCount > 0)
            {
                return SketchSetType.User;
            }
            if (App.IcosaIsLoggedIn && likedCount > 0)
            {
                return SketchSetType.Liked;
            }
            return SketchSetType.Curated;
        }

        private void AddSketchGridEntries(SketchSet sketchset, SketchSetType setType,
            List<SketchGridEntry> entries)
        {
            if (sketchset == null || !sketchset.IsReadyForAccess)
            {
                return;
            }

            for (int i = 0; i < sketchset.NumSketches; i++)
            {
                var info = sketchset.GetSketchSceneFileInfo(i);
                if (info == null || !sketchset.IsSketchIndexValid(i))
                {
                    continue;
                }

                string sketchName = sketchset.GetSketchName(i);
                string authorLabel = GetInitialAuthorLabel(setType, info);
                entries.Add(new SketchGridEntry
                {
                    SketchSet = sketchset,
                    SetType = setType,
                    SketchIndex = i,
                    SceneFileInfo = info,
                    DisplayName = string.IsNullOrEmpty(sketchName)
                        ? info.HumanName
                        : sketchName,
                    AuthorLabel = authorLabel,
                    AuthorMetadataAssigned = !ShouldShowAuthor(setType)
                        || !string.IsNullOrEmpty(authorLabel)
                });
            }
        }

        private string GetInitialAuthorLabel(SketchSetType setType, SceneFileInfo info)
        {
            if (!ShouldShowAuthor(setType) || !(info is IcosaSceneFileInfo icosaInfo))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(icosaInfo.Author) ? null : icosaInfo.Author;
        }

        private string GetSketchListSignature(SketchSetType setType, List<SketchGridEntry> entries)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(setType);
            for (int i = 0; i < entries.Count; i++)
            {
                builder.Append('|');
                builder.Append(entries[i].SetType);
                builder.Append(':');
                builder.Append(entries[i].SketchIndex);
                builder.Append(':');
                builder.Append(entries[i].SceneFileInfo != null
                    ? entries[i].SceneFileInfo.FullPath
                    : entries[i].DisplayName);
            }
            return builder.ToString();
        }

        private void ApplyVisibleSketches(List<SketchGridEntry> visibleSketches, string signature)
        {
            DestroyThumbnailSprites();
            m_Sketches.Clear();
            m_Sketches.AddRange(visibleSketches);
            m_VisibleSketchSignature = signature;

            EnsureGridItemCount(m_Sketches.Count);
            for (int i = 0; i < m_GridItems.Count; i++)
            {
                NoHeadsetSketchGridItem item = m_GridItems[i];
                if (item == null)
                {
                    continue;
                }

                bool active = i < m_Sketches.Count;
                item.gameObject.SetActive(active);
                if (active)
                {
                    SketchGridEntry entry = m_Sketches[i];
                    item.SetThumbnailFrame(UsesLocalThumbnailFrame(entry));
                    item.Init(i, entry.DisplayName, null, m_LoadingSprite, true, LoadSketchEntry);
                    item.SetAuthor(GetVisibleAuthorLabel(entry));
                    item.SetAvailableVisual(entry.SceneFileInfo != null && entry.SceneFileInfo.Available);
                    item.SetInteractionEnabled(!IsDownloadInFlight(entry));
                }
                else
                {
                    item.SetThumbnailFrame(false);
                    item.ClearListeners();
                }
            }

            RequestVisibleThumbnailMetadata();
        }

        private bool UsesLocalThumbnailFrame(SketchGridEntry entry)
        {
            return entry != null && entry.SetType == SketchSetType.User;
        }

        private bool ShouldShowAuthor(SketchGridEntry entry)
        {
            return entry != null && ShouldShowAuthor(entry.SetType);
        }

        private bool ShouldShowAuthor(SketchSetType setType)
        {
            return setType == SketchSetType.Curated || setType == SketchSetType.Liked;
        }

        private string GetVisibleAuthorLabel(SketchGridEntry entry)
        {
            return ShouldShowAuthor(entry) ? entry.AuthorLabel : null;
        }

        private void RefreshVisibleTileText()
        {
            int count = Mathf.Min(m_Sketches.Count, m_GridItems.Count);
            for (int i = 0; i < count; i++)
            {
                if (m_GridItems[i] != null && m_GridItems[i].gameObject.activeSelf)
                {
                    m_GridItems[i].SetTitle(m_Sketches[i].DisplayName);
                    m_GridItems[i].SetAuthor(GetVisibleAuthorLabel(m_Sketches[i]));
                    m_GridItems[i].SetAvailableVisual(m_Sketches[i].SceneFileInfo != null
                        && m_Sketches[i].SceneFileInfo.Available);
                    m_GridItems[i].SetInteractionEnabled(!IsDownloadInFlight(m_Sketches[i]));
                    if (IsDownloadInFlight(m_Sketches[i]))
                    {
                        m_GridItems[i].SetThumbnail(m_LoadingSprite, true);
                    }
                }
            }
        }

        private void RefreshVisibleSketchEntries(List<SketchGridEntry> visibleSketches)
        {
            int count = Mathf.Min(m_Sketches.Count, visibleSketches.Count);
            for (int i = 0; i < count; i++)
            {
                SketchGridEntry current = m_Sketches[i];
                SketchGridEntry refreshed = visibleSketches[i];
                current.DisplayName = refreshed.DisplayName;
                current.SceneFileInfo = refreshed.SceneFileInfo;

                if (!string.IsNullOrEmpty(refreshed.AuthorLabel))
                {
                    current.AuthorLabel = refreshed.AuthorLabel;
                    current.AuthorMetadataAssigned = true;
                }
                else if (!ShouldShowAuthor(current))
                {
                    current.AuthorLabel = null;
                    current.AuthorMetadataAssigned = true;
                }
            }
        }

        private void EnsureGridItemCount(int count)
        {
            while (m_GridItems.Count < count)
            {
                NoHeadsetSketchGridItem item = Instantiate(m_SketchGridItemPrefab, m_SketchGridContent);
                item.gameObject.SetActive(false);
                AttachPointerCursorHandler(item.gameObject);
                m_GridItems.Add(item);
            }
        }

        private void RequestVisibleThumbnailMetadata(bool force = false)
        {
            if (m_Sketches.Count == 0)
            {
                m_ThumbnailRequestSignature = "";
                return;
            }

            string requestSignature = GetSketchListSignature(m_SelectedSetType, m_Sketches);
            if (!force && requestSignature == m_ThumbnailRequestSignature)
            {
                return;
            }

            m_VisibleSets.Clear();
            Dictionary<SketchSet, List<int>> requestsBySet = new Dictionary<SketchSet, List<int>>();
            for (int i = 0; i < m_Sketches.Count; i++)
            {
                SketchGridEntry entry = m_Sketches[i];
                if (!requestsBySet.TryGetValue(entry.SketchSet, out List<int> requests))
                {
                    requests = new List<int>();
                    requestsBySet[entry.SketchSet] = requests;
                    m_VisibleSets[entry.SetType] = entry.SketchSet;
                }
                requests.Add(entry.SketchIndex);
            }

            foreach (var pair in requestsBySet)
            {
                pair.Key.RequestOnlyLoadedMetadata(pair.Value);
            }
            m_ThumbnailRequestSignature = requestSignature;
        }

        public void InitEditMode()
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y = 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
            ShutdownSelf();
        }

        public void InitViewOnlyMode()
        {
            LoadSketchEntry(0);
        }

        private void LoadSketchEntry(int index)
        {
            if (index < 0 || index >= m_Sketches.Count)
            {
                Debug.LogWarning($"{LogPrefix} invalid sketch index {index}");
                return;
            }

            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y = 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            SketchGridEntry entry = m_Sketches[index];
            if (entry.SceneFileInfo != null && !entry.SceneFileInfo.Available
                && (entry.SetType == SketchSetType.Curated || entry.SetType == SketchSetType.Liked)
                && entry.SketchSet is IcosaSketchSet icosaSketchSet)
            {
                string downloadKey = GetDownloadKey(entry);
                if (!m_DownloadingSketchKeys.Add(downloadKey))
                {
                    Debug.Log($"{LogPrefix} download already in flight for {downloadKey}");
                    return;
                }

                SetTileDownloading(index);
                StartCoroutine(DownloadAndLoadSketchEntry(icosaSketchSet, entry.SketchIndex, downloadKey));
                return;
            }

            IssueSketchbookLoadCommand(entry);
        }

        private bool IsDownloadInFlight(SketchGridEntry entry)
        {
            return entry != null && m_DownloadingSketchKeys.Contains(GetDownloadKey(entry));
        }

        private string GetDownloadKey(SketchGridEntry entry)
        {
            return $"{entry.SetType}:{entry.SketchIndex}";
        }

        private void SetTileDownloading(int index)
        {
            if (index >= 0 && index < m_GridItems.Count && m_GridItems[index] != null)
            {
                m_GridItems[index].SetThumbnail(m_LoadingSprite, true);
                m_GridItems[index].SetInteractionEnabled(false);
            }
        }

        private IEnumerator DownloadAndLoadSketchEntry(IcosaSketchSet sketchSet, int sketchIndex,
            string downloadKey)
        {
            bool downloadRefreshReceived = false;
            yield return StartCoroutine(sketchSet.DownloadFilesCoroutine(new List<int> { sketchIndex }, () =>
            {
                m_DownloadingSketchKeys.Remove(downloadKey);
                downloadRefreshReceived = true;
                RefreshSketchGrid();
            }));

            if (!downloadRefreshReceived)
            {
                m_DownloadingSketchKeys.Remove(downloadKey);
                RefreshSketchGrid();
            }

            if (!sketchSet.IsSketchIndexValid(sketchIndex))
            {
                Debug.LogWarning($"{LogPrefix} downloaded sketch index {sketchIndex} is no longer valid");
                yield break;
            }

            SketchGridEntry entry = null;
            for (int i = 0; i < m_Sketches.Count; i++)
            {
                if (m_Sketches[i].SketchSet == sketchSet && m_Sketches[i].SketchIndex == sketchIndex)
                {
                    entry = m_Sketches[i];
                    break;
                }
            }

            if (entry == null)
            {
                entry = new SketchGridEntry
                {
                    SketchSet = sketchSet,
                    SetType = sketchSet.Type,
                    SketchIndex = sketchIndex,
                    SceneFileInfo = sketchSet.GetSketchSceneFileInfo(sketchIndex),
                    DisplayName = sketchSet.GetSketchName(sketchIndex)
                };
            }

            if (entry.SceneFileInfo == null || !entry.SceneFileInfo.Available)
            {
                Debug.LogWarning($"{LogPrefix} downloaded sketch index {sketchIndex} is not available");
                yield break;
            }

            IssueSketchbookLoadCommand(entry);
        }

        private void IssueSketchbookLoadCommand(SketchGridEntry entry)
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
            SketchControlsScript.m_Instance.IssueGlobalCommand(
                SketchControlsScript.GlobalCommands.LoadConfirmUnsaved,
                entry.SketchIndex,
                (int)entry.SetType);
            ShutdownSelf();
        }

        private void ShutdownSelf()
        {
            SaveGridState();
            DestroyGeneratedSprites();
            RestoreCursorState();
            m_Instance = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SaveGridState();
            DestroyGeneratedSprites();
            RestoreCursorState();
            m_Instance = null;
        }

        public void ShowSketchSelectorUi(bool active = true)
        {
            if (m_SketchLoadingUi != null)
            {
                m_SketchLoadingUi.SetActive(active);
            }
        }

        public void HandleHelpButton()
        {
            SketchControlsScript.m_Instance.OpenURLAndInformUser(m_NonXRHelpURL);
        }

        private void CacheAndShowCursor()
        {
            if (!m_HasSavedCursorState)
            {
                m_PreviousCursorVisible = Cursor.visible;
                m_PreviousCursorLockState = Cursor.lockState;
                m_HasSavedCursorState = true;
            }
            NoHeadsetPointerCursor.ForcePointerVisible();
        }

        private void RestoreCursorState()
        {
            if (!m_HasSavedCursorState)
            {
                return;
            }

            NoHeadsetPointerCursor.ResetCursor();
            Cursor.visible = m_PreviousCursorVisible;
            Cursor.lockState = m_PreviousCursorLockState;
            m_HasSavedCursorState = false;
        }

        private void AttachPointerCursorHandler(GameObject target)
        {
            if (target != null && target.GetComponent<NoHeadsetPointerCursor>() == null)
            {
                target.AddComponent<NoHeadsetPointerCursor>();
            }
        }

        private void InitializeGridUi()
        {
            m_Dropdown = GetComponentInChildren<TMP_Dropdown>(true);
            if (m_Dropdown != null)
            {
                m_Dropdown.gameObject.SetActive(false);
            }

            Transform uiParent = m_SketchLoadingUi != null
                ? m_SketchLoadingUi.transform
                : transform;
            TextMeshProUGUI templateText = uiParent.GetComponentInChildren<TextMeshProUGUI>(true);
            if (templateText != null)
            {
                m_RuntimeFontAsset = templateText.font;
                m_RuntimeFontMaterial = templateText.fontSharedMaterial;
            }
            Transform viewButtonTransform = uiParent.Find("View Sketch Button");
            if (viewButtonTransform != null)
            {
                m_ViewSketchButton = viewButtonTransform.gameObject;
                m_ViewSketchButton.SetActive(false);
            }
            RectTransform parentRect = uiParent as RectTransform;
            if (m_SketchGridContent == null)
            {
                BindAuthoredGridUi(uiParent);
            }
            if (m_SketchGridContent == null)
            {
                Debug.LogError($"{LogPrefix} missing authored Sketch Grid Scroll View");
                return;
            }
            BindExistingGridUi(parentRect);
            if (m_SketchGridItemPrefab == null)
            {
                Debug.LogError($"{LogPrefix} missing Sketch Grid Item prefab reference");
                return;
            }
            UpdateCategoryTabs(0, 0, 0);
            SetGridActive(false);

            m_LoadingSprite = CreateSprite(m_LoadingImageTexture);
            m_UnknownSprite = CreateSprite(m_UnknownImageTexture);
        }

        private void BindAuthoredGridUi(Transform uiParent)
        {
            Transform scrollViewTransform = FindDeepChild(uiParent, "Sketch Grid Scroll View");
            if (scrollViewTransform == null)
            {
                return;
            }

            ScrollRect scrollRect = scrollViewTransform.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                Debug.LogWarning($"{LogPrefix} authored Sketch Grid Scroll View has no ScrollRect");
                return;
            }
            if (scrollRect.content == null)
            {
                Debug.LogWarning($"{LogPrefix} authored Sketch Grid Scroll View has no Content");
                return;
            }

            m_RuntimeGridRoot = scrollViewTransform.gameObject;
            m_RuntimeGridRect = scrollViewTransform as RectTransform;
            m_SketchGridScrollRect = scrollRect;
            m_SketchGridContent = scrollRect.content;

            NoHeadsetSketchGridLayout layout =
                m_SketchGridContent.GetComponent<NoHeadsetSketchGridLayout>();
            m_SketchGridLayout = layout;
            if (layout != null && scrollRect.viewport != null)
            {
                layout.SetViewport(scrollRect.viewport);
            }

            if (scrollRect.viewport != null)
            {
                AttachPointerCursorHandler(scrollRect.viewport.gameObject);
            }
            if (scrollRect.verticalScrollbar != null)
            {
                AttachPointerCursorHandler(scrollRect.verticalScrollbar.gameObject);
            }

            Debug.Log($"{LogPrefix} bound authored sketch grid scroll view");
        }

        private void BindExistingGridUi(RectTransform uiParent)
        {
            if (m_RuntimeGridRect == null && m_SketchGridContent != null)
            {
                ScrollRect scrollRect = m_SketchGridContent.GetComponentInParent<ScrollRect>(true);
                if (scrollRect != null)
                {
                    m_RuntimeGridRoot = scrollRect.gameObject;
                    m_RuntimeGridRect = scrollRect.transform as RectTransform;
                    m_SketchGridScrollRect = scrollRect;
                    if (scrollRect.viewport != null)
                    {
                        NoHeadsetSketchGridLayout layout =
                            m_SketchGridContent.GetComponent<NoHeadsetSketchGridLayout>();
                        m_SketchGridLayout = layout;
                        if (layout != null)
                        {
                            layout.SetViewport(scrollRect.viewport);
                        }
                    }
                }
            }

            if (m_RuntimeTabBarRect == null && uiParent != null)
            {
                BindAuthoredTabBar(uiParent);
            }
            if (m_RuntimeTabBarRect == null && uiParent != null)
            {
                CreateRuntimeTabBar(uiParent);
            }
            if (m_RuntimeStackRect == null && uiParent != null)
            {
                Transform stack = FindDeepChild(uiParent, "Sketch Viewer Stack");
                m_RuntimeStackRect = stack as RectTransform;
                if (m_RuntimeStackRect != null)
                {
                    RefreshRuntimeGridFrame(force: true);
                }
            }

            if (m_SketchGridItemPrefab != null)
            {
                m_SketchGridItemPrefab.gameObject.SetActive(false);
            }

            if (m_EmptySketchListMessage == null && uiParent != null)
            {
                Transform emptyMessageTransform = FindDeepChild(uiParent, "Empty Sketch List Message");
                m_EmptySketchListMessage = emptyMessageTransform != null
                    ? emptyMessageTransform.gameObject
                    : CreateEmptyMessage(uiParent);
            }
        }

        private Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform found = FindDeepChild(child, childName);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void BindAuthoredTabBar(RectTransform parent)
        {
            Transform tabBar = FindDeepChild(parent, "Sketch Category Tabs");
            if (tabBar == null)
            {
                return;
            }

            m_RuntimeTabBarRect = tabBar as RectTransform;
            BindCategoryTab(tabBar, SketchSetType.User);
            BindCategoryTab(tabBar, SketchSetType.Curated);
            BindCategoryTab(tabBar, SketchSetType.Liked);
            RefreshRuntimeGridFrame(force: true);
        }

        private void BindCategoryTab(Transform tabBar, SketchSetType setType)
        {
            Transform tabTransform = tabBar.Find(GetTabLabel(setType));
            if (tabTransform == null)
            {
                Debug.LogWarning($"{LogPrefix} authored tab missing: {GetTabLabel(setType)}");
                return;
            }

            Button button = tabTransform.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning($"{LogPrefix} authored tab has no Button: {GetTabLabel(setType)}");
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectCategory(setType));
            m_CategoryTabs[setType] = button;
        }

        private void CreateRuntimeTabBar(RectTransform parent)
        {
            GameObject tabBarObject = new GameObject("Sketch Category Tabs",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabBarObject.transform.SetParent(parent, false);
            m_RuntimeTabBarRect = tabBarObject.GetComponent<RectTransform>();
            m_RuntimeTabBarRect.anchorMin = new Vector2(0.5f, 0.5f);
            m_RuntimeTabBarRect.anchorMax = new Vector2(0.5f, 0.5f);
            RefreshRuntimeGridFrame(force: true);

            HorizontalLayoutGroup layout = tabBarObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateCategoryTab(tabBarObject.transform, SketchSetType.User);
            CreateCategoryTab(tabBarObject.transform, SketchSetType.Curated);
            CreateCategoryTab(tabBarObject.transform, SketchSetType.Liked);
            RefreshRuntimeGridFrame(force: true);
        }

        private void CreateCategoryTab(Transform parent, SketchSetType setType)
        {
            GameObject tabObject = new GameObject(GetTabLabel(setType),
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(NoHeadsetPointerCursor));
            tabObject.transform.SetParent(parent, false);
            RectTransform rectTransform = tabObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(150f, 24f);

            Image image = tabObject.GetComponent<Image>();
            image.color = GetTabColor(setType);

            Button button = tabObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SelectCategory(setType));
            m_CategoryTabs[setType] = button;

            TextMeshProUGUI label = CreateItemText("Label", tabObject.transform, Vector2.zero,
                Vector2.one, 9f, Color.white);
            label.text = GetTabLabel(setType);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 9f;
            label.fontStyle = FontStyles.Normal;
        }

        private void SelectCategory(SketchSetType setType)
        {
            if (m_SelectedSetType == setType)
            {
                return;
            }

            SaveCurrentScrollPosition();
            m_SelectedSetType = setType;
            sm_HasSavedGridState = true;
            m_RestoreSavedScrollOnNextRefresh = true;
            RefreshSketchGrid();
        }

        private void SaveGridState()
        {
            sm_HasSavedGridState = true;
            SaveCurrentScrollPosition();
        }

        private void SaveCurrentScrollPosition()
        {
            if (m_SketchGridScrollRect == null)
            {
                return;
            }
            sm_SavedScrollPositions[m_SelectedSetType] =
                m_SketchGridScrollRect.verticalNormalizedPosition;
        }

        private IEnumerator RestoreGridScrollPositionAfterLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (m_SketchGridScrollRect == null)
            {
                yield break;
            }

            float position = sm_SavedScrollPositions.TryGetValue(m_SelectedSetType,
                out float savedPosition)
                ? savedPosition
                : 1f;
            m_SketchGridScrollRect.verticalNormalizedPosition = position;
        }

        private void UpdateCategoryTabs(int userCount, int curatedCount, int likedCount)
        {
            UpdateCategoryTab(SketchSetType.User, userCount);
            UpdateCategoryTab(SketchSetType.Curated, curatedCount);
            UpdateCategoryTab(SketchSetType.Liked, likedCount, App.IcosaIsLoggedIn);
            RefreshRuntimeGridFrame(force: true);
        }

        private void UpdateGridCellAspect()
        {
            if (m_SketchGridLayout == null)
            {
                return;
            }

            m_SketchGridLayout.SetCellAspect(m_SelectedSetType == SketchSetType.User
                ? LocalGridCellAspect
                : RemoteGridCellAspect);
        }

        private void UpdateCategoryTab(SketchSetType setType, int count, bool visible = true)
        {
            if (!m_CategoryTabs.TryGetValue(setType, out Button tab) || tab == null)
            {
                return;
            }

            tab.gameObject.SetActive(visible);
            tab.interactable = count > 0;
            Image image = tab.GetComponent<Image>();
            if (image != null)
            {
                image.color = GetTabColor(setType);
            }
        }

        private void SetGridActive(bool active)
        {
            if (m_RuntimeGridRoot != null)
            {
                m_RuntimeGridRoot.SetActive(active);
            }
            else if (m_SketchGridContent != null)
            {
                m_SketchGridContent.gameObject.SetActive(active);
            }

            if (m_RuntimeTabBarRect != null)
            {
                m_RuntimeTabBarRect.gameObject.SetActive(active || AnyVisibleSetReady());
            }
        }

        private void RefreshRuntimeGridFrame(bool force = false)
        {
            if (m_RuntimeGridRect == null)
            {
                return;
            }

            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && screenSize == m_LastScreenSize)
            {
                return;
            }
            m_LastScreenSize = screenSize;

            bool portrait = screenSize.y > screenSize.x;
            bool smallLandscape = !portrait && screenSize.y < 520;
            Vector2 visibleCanvasSize = GetVisibleCanvasSize();
            float horizontalMargin = portrait ? 18f : 32f;
            float topMargin = portrait ? 66f : smallLandscape ? 50f : 66f;
            const float bottomMargin = 4f;
            float maxGridWidth = portrait ? 380f : smallLandscape ? 520f : 620f;
            float availableWidth = Mathf.Max(120f, visibleCanvasSize.x - horizontalMargin);
            float gridWidth = Mathf.Min(availableWidth, maxGridWidth);
            float stackTop = visibleCanvasSize.y * 0.5f - topMargin;
            float stackBottom = -visibleCanvasSize.y * 0.5f + bottomMargin;
            float stackHeight = Mathf.Max(130f, stackTop - stackBottom);
            if (m_RuntimeStackRect != null)
            {
                m_RuntimeStackRect.anchoredPosition =
                    new Vector2(0f, stackBottom + stackHeight * 0.5f);
                m_RuntimeStackRect.sizeDelta = new Vector2(gridWidth, stackHeight);
            }

            if (m_RuntimeTabBarRect != null)
            {
                RefreshRuntimeTabWidths(gridWidth);
            }
        }

        private Vector2 GetVisibleCanvasSize()
        {
            Canvas canvas = m_RuntimeGridRect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null ? canvas.worldCamera : null;
            RectTransform rootRect = m_RuntimeGridRect.parent as RectTransform;
            if (camera != null && camera.orthographic && rootRect != null)
            {
                Vector3 scale = rootRect.lossyScale;
                float scaleX = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
                float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
                float worldHeight = camera.orthographicSize * 2f;
                float worldWidth = worldHeight * camera.aspect;
                return new Vector2(worldWidth / scaleX, worldHeight / scaleY);
            }

            if (rootRect != null && rootRect.rect.width > 1f && rootRect.rect.height > 1f)
            {
                return rootRect.rect.size;
            }

            return new Vector2(360f, 360f);
        }

        private void RefreshRuntimeTabWidths(float tabBarWidth)
        {
            int visibleTabCount = 0;
            foreach (Button tab in m_CategoryTabs.Values)
            {
                if (tab != null && tab.gameObject.activeSelf)
                {
                    visibleTabCount++;
                }
            }

            if (visibleTabCount == 0)
            {
                return;
            }

            float spacing = 6f;
            float tabWidth = (tabBarWidth - spacing * (visibleTabCount - 1)) / visibleTabCount;
            foreach (Button tab in m_CategoryTabs.Values)
            {
                if (tab == null || !tab.gameObject.activeSelf)
                {
                    continue;
                }

                RectTransform rectTransform = tab.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(tabWidth, 24f);
                }
            }
        }

        private TextMeshProUGUI CreateItemText(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, float fontSize, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = new Vector2(8f, 0f);
            rectTransform.offsetMax = new Vector2(-8f, 0f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (m_RuntimeFontAsset != null)
            {
                text.font = m_RuntimeFontAsset;
            }
            if (m_RuntimeFontMaterial != null)
            {
                text.fontSharedMaterial = m_RuntimeFontMaterial;
            }
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private GameObject CreateEmptyMessage(RectTransform parent)
        {
            GameObject emptyObject = new GameObject("Empty Sketch List Message",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyObject.transform.SetParent(parent, false);
            RectTransform rectTransform = emptyObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 44f);
            rectTransform.sizeDelta = new Vector2(520f, 40f);

            TextMeshProUGUI text = emptyObject.GetComponent<TextMeshProUGUI>();
            text.text = "No sketches are ready to load.";
            text.fontSize = 12f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            emptyObject.SetActive(false);
            return emptyObject;
        }

        private void RefreshVisibleThumbnails()
        {
            int count = Mathf.Min(m_Sketches.Count, m_GridItems.Count);
            bool assignedTextureInvalidated = false;
            int localThumbnailLoads = 0;
            for (int i = 0; i < count; i++)
            {
                SketchGridEntry entry = m_Sketches[i];
                if (IsDownloadInFlight(entry))
                {
                    if (m_GridItems[i] != null)
                    {
                        m_GridItems[i].SetThumbnail(m_LoadingSprite, true);
                        m_GridItems[i].SetInteractionEnabled(false);
                    }
                    continue;
                }

                if (entry.ThumbnailAssigned)
                {
                    NoHeadsetSketchGridItem item = m_GridItems[i];
                    RefreshAuthorMetadata(entry, item);
                    if (item != null && item.HasLoadedThumbnailTexture())
                    {
                        continue;
                    }
                    assignedTextureInvalidated |= item != null && item.HasAssignedThumbnailSprite();
                    entry.ThumbnailAssigned = false;
                    if (item != null)
                    {
                        item.SetThumbnail(m_LoadingSprite, true);
                    }
                }

                if (!entry.SketchSet.GetSketchIcon(entry.SketchIndex, out Texture2D icon,
                        out string[] authors, out string __))
                {
                    if (localThumbnailLoads < LocalThumbnailLoadsPerFrame
                        && entry.SketchSet is FileSketchSet
                        && TryCreateLocalThumbnailSprite(entry.SceneFileInfo, out Sprite localSprite))
                    {
                        localThumbnailLoads++;
                        entry.ThumbnailAssigned = true;
                        m_GridItems[i].SetThumbnail(localSprite, false);
                    }
                    continue;
                }

                SetAuthorMetadata(entry, m_GridItems[i], authors);

                if (icon != null)
                {
                    Sprite sprite = CreateThumbnailSprite(icon);
                    entry.ThumbnailAssigned = true;
                    m_GridItems[i].SetThumbnail(sprite, false);
                    continue;
                }

                m_GridItems[i].SetThumbnail(m_UnknownSprite, false);
                entry.ThumbnailAssigned = true;
            }

            if (assignedTextureInvalidated)
            {
                ResetVisibleThumbnailAssignments();
                RequestVisibleThumbnailMetadata(force: true);
            }
        }

        private void RefreshAuthorMetadata(SketchGridEntry entry, NoHeadsetSketchGridItem item)
        {
            if (entry == null || entry.AuthorMetadataAssigned)
            {
                return;
            }

            if (!ShouldShowAuthor(entry))
            {
                SetAuthorMetadata(entry, item, null);
                return;
            }

            if (entry.SketchSet.GetSketchIcon(entry.SketchIndex, out Texture2D _,
                    out string[] authors, out string __))
            {
                SetAuthorMetadata(entry, item, authors);
            }
        }

        private void SetAuthorMetadata(SketchGridEntry entry, NoHeadsetSketchGridItem item,
            string[] authors)
        {
            entry.AuthorLabel = ShouldShowAuthor(entry) ? GetAuthorLabel(authors) : null;
            entry.AuthorMetadataAssigned = true;
            if (item != null)
            {
                item.SetAuthor(GetVisibleAuthorLabel(entry));
            }
        }

        private string GetAuthorLabel(string[] authors)
        {
            if (authors == null || authors.Length == 0)
            {
                return null;
            }

            return string.Join(", ", authors);
        }

        private bool TryCreateLocalThumbnailSprite(SceneFileInfo sceneFileInfo, out Sprite sprite)
        {
            sprite = null;
            if (sceneFileInfo == null || !sceneFileInfo.Exists)
            {
                sprite = m_UnknownSprite;
                return true;
            }

            byte[] thumbnailBytes = FileSketchSet.ReadThumbnail(sceneFileInfo);
            if (thumbnailBytes == null || thumbnailBytes.Length == 0)
            {
                sprite = m_UnknownSprite;
                return true;
            }

            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGB24, true);
            if (!texture.LoadImage(thumbnailBytes))
            {
                Destroy(texture);
                sprite = m_UnknownSprite;
                return true;
            }
            texture.Apply();
            m_OwnedThumbnailTextures.Add(texture);
            sprite = CreateThumbnailSprite(texture);
            return true;
        }

        private void ResetVisibleThumbnailAssignments()
        {
            DestroyThumbnailSprites();
            int count = Mathf.Min(m_Sketches.Count, m_GridItems.Count);
            for (int i = 0; i < count; i++)
            {
                m_Sketches[i].ThumbnailAssigned = false;
                if (m_GridItems[i] != null)
                {
                    m_GridItems[i].SetThumbnail(m_LoadingSprite, true);
                }
            }
        }

        private Sprite CreateSprite(Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            m_GeneratedSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateThumbnailSprite(Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            m_ThumbnailSprites.Add(sprite);
            return sprite;
        }

        private void ClearGridItems()
        {
            for (int i = 0; i < m_GridItems.Count; i++)
            {
                if (m_GridItems[i] != null)
                {
                    m_GridItems[i].ClearListeners();
                    Destroy(m_GridItems[i].gameObject);
                }
            }
            m_GridItems.Clear();
            DestroyThumbnailSprites();
            m_LoadingSprite = CreateSprite(m_LoadingImageTexture);
            m_UnknownSprite = CreateSprite(m_UnknownImageTexture);
        }

        private void DestroyGeneratedSprites()
        {
            DestroyThumbnailSprites();
            for (int i = 0; i < m_GeneratedSprites.Count; i++)
            {
                if (m_GeneratedSprites[i] != null)
                {
                    Destroy(m_GeneratedSprites[i]);
                }
            }
            m_GeneratedSprites.Clear();
        }

        private void DestroyThumbnailSprites()
        {
            for (int i = 0; i < m_ThumbnailSprites.Count; i++)
            {
                if (m_ThumbnailSprites[i] != null)
                {
                    Destroy(m_ThumbnailSprites[i]);
                }
            }
            m_ThumbnailSprites.Clear();

            for (int i = 0; i < m_OwnedThumbnailTextures.Count; i++)
            {
                if (m_OwnedThumbnailTextures[i] != null)
                {
                    Destroy(m_OwnedThumbnailTextures[i]);
                }
            }
            m_OwnedThumbnailTextures.Clear();
        }

        private bool AnyVisibleSetReady()
        {
            return IsSetReady(SketchSetType.User)
                || IsSetReady(SketchSetType.Curated)
                || IsSetReady(SketchSetType.Liked);
        }

        private bool IsSetReady(SketchSetType setType)
        {
            var sketchset = SketchCatalog.m_Instance.GetSet(setType);
            return sketchset != null && sketchset.IsReadyForAccess;
        }

        private string GetTabLabel(SketchSetType setType)
        {
            switch (setType)
            {
                case SketchSetType.User:
                    return "Your Sketches";
                case SketchSetType.Curated:
                    return "Featured Sketches";
                case SketchSetType.Liked:
                    return "Liked Sketches";
                default:
                    return setType.ToString();
            }
        }

        private Color GetTabColor(SketchSetType setType)
        {
            if (m_SelectedSetType == setType)
            {
                return new Color(1f, 1f, 1f, 0.18f);
            }
            return new Color(0f, 0f, 0f, 0.22f);
        }
    }
}
