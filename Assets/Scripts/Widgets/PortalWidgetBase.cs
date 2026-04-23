// Copyright 2026 The Open Brush Authors
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
using System.Linq;
using UnityEngine.Networking;

namespace TiltBrush
{
    public abstract class PortalWidgetBase : ShapeWidget
    {
        protected const string kLogPrefix = "[PortalDbg_20260313]";
        protected const string kLoadCompareLogPrefix = "[PortalLoadCmp_20260313]";

        private string m_Destination;
        private bool m_TriggerWithBrushInside = true;
        private bool m_AllowRepeatWhileInside;
        private Color m_LoadingColor = new Color(0.1f, 0.85f, 1.0f, 1.0f);
        private float m_LoadingFillDuration = 0.2f;
        private float m_LoadingMinFill = 0.6f;
        private float m_LoadingPulseAmplitude = 0.15f;
        private float m_LoadingPulseFrequency = 2.0f;

        private bool m_CommandTriggeredWhileInside;
        private Texture m_DefaultThumbnail;
        private IcosaSketchSet[] m_ObservedSketchSets;
        private Coroutine m_ThumbnailFetchCoroutine;
        private string m_ThumbnailFetchAssetId;
        private Texture m_LastLoggedThumbnail;
        private bool m_HasLoggedThumbnailState;
        private Texture2D m_OwnedThumbnail;
        private Color m_DefaultColor = Color.white;
        private Color m_DefaultEmissionColor = Color.black;
        private bool m_IsShowingLoadingVisual;
        private float m_LoadingVisualStartTime;
        private float? m_LoadingProgress;
        private Coroutine m_WaitForPreloadThenLoadCoroutine;
        private string m_WaitForPreloadDestination;

        public string Destination
        {
            get => m_Destination;
            set
            {
                m_Destination = value;
                RefreshDestinationThumbnail();
            }
        }

        public TrTransform GetSaveTransform()
        {
            var xf = TrTransform.FromLocalTransform(transform);
            xf.scale = GetSignedWidgetSize();
            return xf;
        }

        // Shared initialization logic for static FromTiltPortal factory methods in subclasses.
        protected static void InitFromTiltPortal(PortalWidgetBase widget, TiltPortal tiltPortal)
        {
            widget.m_SkipIntroAnim = true;
            widget.transform.parent = App.Instance.m_CanvasTransform;
            widget.transform.localScale = Vector3.one;
            widget.SetSignedWidgetSize(tiltPortal.Transform.scale);
            widget.Show(bShow: true, bPlayAudio: false);
            widget.transform.localPosition = tiltPortal.Transform.translation;
            widget.transform.localRotation = tiltPortal.Transform.rotation;
            widget.Destination = tiltPortal.Destination;
            if (tiltPortal.Pinned)
            {
                widget.PinFromSave();
            }
            widget.Group = App.GroupManager.GetGroupFromId(tiltPortal.GroupId);
            widget.SetCanvas(App.Scene.GetOrCreateLayer(tiltPortal.LayerId));
            widget.UpdateScale();
        }

        protected override void Awake()
        {
            base.Awake();
            RestoreStencilWidgetLayers();
            Renderer thumbnailRenderer = GetThumbnailRenderer();
            if (thumbnailRenderer != null)
            {
                Material material = thumbnailRenderer.material;
                m_DefaultThumbnail = material.mainTexture;
                if (material.HasProperty("_Color"))
                {
                    m_DefaultColor = material.color;
                }
                if (material.HasProperty("_EmissionColor"))
                {
                    m_DefaultEmissionColor = material.GetColor("_EmissionColor");
                }
                thumbnailRenderer.enabled = true;
            }
        }

        protected virtual void OnEnable()
        {
            RefreshDestinationThumbnail();
        }

        protected override void OnDestroy()
        {
            UnsubscribeFromSketchSets();
            StopThumbnailFetch();
            StopWaitingForPreload();
            StopLoadingVisuals();
            ReleaseOwnedThumbnail();
            base.OnDestroy();
        }

        public override void RestoreGameObjectLayer(int layer)
        {
            HierarchyUtils.RecursivelySetLayer(transform, layer);
            RestoreStencilWidgetLayers();
        }

        protected override void InitPin()
        {
            base.InitPin();
            RestoreStencilWidgetLayers();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            UpdateLoadingVisuals();
            LogThumbnailStateIfChanged();

            if (!m_TriggerWithBrushInside)
            {
                m_CommandTriggeredWhileInside = false;
                return;
            }

            bool brushInside = GetActivationScore(
                InputManager.m_Instance.GetBrushControllerAttachPoint().position,
                InputManager.ControllerName.Brush) >= 0;

            if (!brushInside)
            {
                m_CommandTriggeredWhileInside = false;
                return;
            }

            bool triggerDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate);
            bool canTriggerAgain = m_AllowRepeatWhileInside || !m_CommandTriggeredWhileInside;
            if (!triggerDown || !canTriggerAgain)
            {
                return;
            }

            m_CommandTriggeredWhileInside = ExecutePortalCommand();
        }

        private void RefreshDestinationThumbnail()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (TryApplyIcosaThumbnail())
            {
                TryPreloadDestinationSketch();
                return;
            }

            Debug.LogWarning($"{kLogPrefix} Thumbnail not available yet for '{m_Destination}' on {name}");
            ApplyThumbnailTexture(m_DefaultThumbnail);
            SubscribeToSketchSets();
            EnsureThumbnailFetchStarted();
            TryPreloadDestinationSketch();
        }

        private bool TryApplyIcosaThumbnail()
        {
            if (string.IsNullOrWhiteSpace(m_Destination) || SketchCatalog.m_Instance == null)
            {
                return false;
            }

            foreach (var setType in new[]
                     {
                         SketchSetType.Curated,
                         SketchSetType.Liked,
                         SketchSetType.User,
                     })
            {
                var icosaSet = SketchCatalog.m_Instance.GetSet(setType) as IcosaSketchSet;
                if (icosaSet == null)
                {
                    continue;
                }

                if (!icosaSet.IsReadyForAccess)
                {
                    continue;
                }

                if (!icosaSet.TryGetIconForAssetId(m_Destination, out var icon))
                {
                    continue;
                }

                ApplyThumbnailTexture(icon);
                UnsubscribeFromSketchSets();
                return true;
            }

            return false;
        }

        private void SubscribeToSketchSets()
        {
            if (SketchCatalog.m_Instance == null || m_ObservedSketchSets != null)
            {
                return;
            }

            m_ObservedSketchSets = new[]
            {
                SketchCatalog.m_Instance.GetSet(SketchSetType.Curated) as IcosaSketchSet,
                SketchCatalog.m_Instance.GetSet(SketchSetType.Liked) as IcosaSketchSet,
                SketchCatalog.m_Instance.GetSet(SketchSetType.User) as IcosaSketchSet,
            }.Where(x => x != null).ToArray();

            foreach (var set in m_ObservedSketchSets)
            {
                set.OnChanged += OnSketchSetChanged;
                set.RequestRefresh();
            }
        }

        private void UnsubscribeFromSketchSets()
        {
            if (m_ObservedSketchSets == null)
            {
                return;
            }

            foreach (var set in m_ObservedSketchSets)
            {
                set.OnChanged -= OnSketchSetChanged;
            }

            m_ObservedSketchSets = null;
        }

        private void OnSketchSetChanged()
        {
            TryApplyIcosaThumbnail();
            TryPreloadDestinationSketch();
        }

        private void EnsureThumbnailFetchStarted()
        {
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(m_Destination))
            {
                return;
            }

            if (m_ThumbnailFetchCoroutine != null && m_ThumbnailFetchAssetId == m_Destination)
            {
                return;
            }

            StopThumbnailFetch();
            m_ThumbnailFetchAssetId = m_Destination;
            m_ThumbnailFetchCoroutine = StartCoroutine(FetchThumbnailForAssetIdCoroutine(m_Destination));
        }

        private void StopThumbnailFetch()
        {
            if (m_ThumbnailFetchCoroutine != null)
            {
                StopCoroutine(m_ThumbnailFetchCoroutine);
                m_ThumbnailFetchCoroutine = null;
            }
            m_ThumbnailFetchAssetId = null;
        }

        private void StopWaitingForPreload()
        {
            if (m_WaitForPreloadThenLoadCoroutine != null)
            {
                StopCoroutine(m_WaitForPreloadThenLoadCoroutine);
                m_WaitForPreloadThenLoadCoroutine = null;
            }

            m_WaitForPreloadDestination = null;
        }

        private System.Collections.IEnumerator FetchThumbnailForAssetIdCoroutine(string assetId)
        {
            Debug.Log($"{kLogPrefix} Starting background thumbnail fetch for '{assetId}' on {name}");

            IcosaSceneFileInfo sceneFileInfo = null;
            yield return VrAssetService.m_Instance.GetSketchInfo(
                assetId,
                info => sceneFileInfo = info,
                () => sceneFileInfo = null);

            if (sceneFileInfo == null || string.IsNullOrWhiteSpace(sceneFileInfo.IconUrl))
            {
                Debug.LogWarning($"{kLogPrefix} Failed to fetch thumbnail metadata for '{assetId}' on {name}");
                m_ThumbnailFetchCoroutine = null;
                m_ThumbnailFetchAssetId = null;
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(sceneFileInfo.IconUrl))
            {
                yield return request.SendWebRequest();
                if (request.isNetworkError || request.responseCode >= 400 || !string.IsNullOrEmpty(request.error))
                {
                    Debug.LogWarning($"{kLogPrefix} Failed to download thumbnail for '{assetId}' on {name}: {request.error}");
                    m_ThumbnailFetchCoroutine = null;
                    m_ThumbnailFetchAssetId = null;
                    yield break;
                }

                if (m_Destination == assetId)
                {
                    ApplyThumbnailTexture(DownloadHandlerTexture.GetContent(request));
                    Debug.Log($"{kLogPrefix} Applied background-fetched thumbnail for '{assetId}' on {name}");
                }
            }

            m_ThumbnailFetchCoroutine = null;
            m_ThumbnailFetchAssetId = null;
        }

        private void ApplyThumbnailTexture(Texture texture)
        {
            Renderer thumbnailRenderer = GetThumbnailRenderer();
            if (thumbnailRenderer == null)
            {
                Debug.LogWarning($"{kLogPrefix} Cannot apply thumbnail for '{m_Destination}' on {name}: thumbnail renderer is null");
                return;
            }

            Texture textureToApply = PrepareTextureForPortal(texture);
            Material[] rendererMaterials = thumbnailRenderer.materials;
            Material primaryMaterial = rendererMaterials.FirstOrDefault();
            if (primaryMaterial == null)
            {
                Debug.LogWarning($"{kLogPrefix} Cannot apply thumbnail for '{m_Destination}' on {name}: material is null on renderer {thumbnailRenderer.name}");
                return;
            }

            Vector2 previousScale = primaryMaterial.HasProperty("_MainTex")
                ? primaryMaterial.GetTextureScale("_MainTex")
                : Vector2.zero;
            Vector2 previousOffset = primaryMaterial.HasProperty("_MainTex")
                ? primaryMaterial.GetTextureOffset("_MainTex")
                : Vector2.zero;

            ApplyThumbnailToMaterials(rendererMaterials, textureToApply);
            thumbnailRenderer.materials = rendererMaterials;
            SyncTrackedMaterialCopies(thumbnailRenderer, textureToApply);

            Material currentPrimaryMaterial = thumbnailRenderer.materials.FirstOrDefault();
            if (currentPrimaryMaterial == null)
            {
                return;
            }

            Vector2 currentScale = currentPrimaryMaterial.HasProperty("_MainTex")
                ? currentPrimaryMaterial.GetTextureScale("_MainTex")
                : Vector2.zero;
            Vector2 currentOffset = currentPrimaryMaterial.HasProperty("_MainTex")
                ? currentPrimaryMaterial.GetTextureOffset("_MainTex")
                : Vector2.zero;

            Debug.Log(
                $"{kLogPrefix} Applied thumbnail for '{m_Destination}' on {name}. " +
                $"Current mainTexture='{DescribeTexture(currentPrimaryMaterial.mainTexture)}' " +
                $"scale={currentScale} offset={currentOffset}");
            m_LastLoggedThumbnail = currentPrimaryMaterial.mainTexture;
            m_HasLoggedThumbnailState = true;
        }

        private void ApplyThumbnailToMaterials(Material[] materials, Texture texture)
        {
            if (materials == null)
            {
                return;
            }

            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                material.mainTexture = texture;
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTextureScale("_MainTex", Vector2.one);
                    material.SetTextureOffset("_MainTex", Vector2.zero);
                }
            }
        }

        private void ApplyLoadingVisualToMaterials(Material[] materials, float fill)
        {
            if (materials == null)
            {
                return;
            }

            Color color = Color.Lerp(m_DefaultColor, m_LoadingColor, fill);
            Color emission = Color.Lerp(m_DefaultEmissionColor, m_LoadingColor * 0.35f, fill);
            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_Color"))
                {
                    material.color = color;
                }
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                }
            }
        }

        private void SyncTrackedMaterialCopies(Renderer thumbnailRenderer, Texture texture)
        {
            if (m_InitialMaterials == null || m_NewMaterials == null || thumbnailRenderer == null)
            {
                return;
            }

            if (m_InitialMaterials.TryGetValue(thumbnailRenderer, out var initialMaterials))
            {
                ApplyThumbnailToMaterials(initialMaterials, texture);
                m_InitialMaterials[thumbnailRenderer] = initialMaterials;
            }

            if (m_NewMaterials.TryGetValue(thumbnailRenderer, out var newMaterials))
            {
                ApplyThumbnailToMaterials(newMaterials, texture);
                m_NewMaterials[thumbnailRenderer] = newMaterials;
            }
        }

        private void SyncTrackedLoadingVisualCopies(Renderer thumbnailRenderer, float fill)
        {
            if (m_InitialMaterials == null || m_NewMaterials == null || thumbnailRenderer == null)
            {
                return;
            }

            if (m_InitialMaterials.TryGetValue(thumbnailRenderer, out var initialMaterials))
            {
                ApplyLoadingVisualToMaterials(initialMaterials, fill);
                m_InitialMaterials[thumbnailRenderer] = initialMaterials;
            }

            if (m_NewMaterials.TryGetValue(thumbnailRenderer, out var newMaterials))
            {
                ApplyLoadingVisualToMaterials(newMaterials, fill);
                m_NewMaterials[thumbnailRenderer] = newMaterials;
            }
        }

        private Texture PrepareTextureForPortal(Texture texture)
        {
            if (texture == null || texture == m_DefaultThumbnail)
            {
                ReleaseOwnedThumbnail();
                return texture;
            }

            if (texture is not Texture2D texture2D)
            {
                ReleaseOwnedThumbnail();
                return texture;
            }

            if (ReferenceEquals(texture2D, m_OwnedThumbnail))
            {
                return m_OwnedThumbnail;
            }

            ReleaseOwnedThumbnail();
            m_OwnedThumbnail = Object.Instantiate(texture2D);
            m_OwnedThumbnail.name = string.IsNullOrWhiteSpace(texture2D.name)
                ? $"PortalThumb_{m_Destination}"
                : $"{texture2D.name}_PortalCopy";
            return m_OwnedThumbnail;
        }

        private void ReleaseOwnedThumbnail()
        {
            if (m_OwnedThumbnail == null)
            {
                return;
            }

            Object.Destroy(m_OwnedThumbnail);
            m_OwnedThumbnail = null;
        }

        private void StartLoadingVisuals()
        {
            m_IsShowingLoadingVisual = true;
            m_LoadingVisualStartTime = Time.realtimeSinceStartup;
            m_LoadingProgress = 0.0f;
            ApplyLoadingVisual(m_LoadingMinFill);
        }

        private void StopLoadingVisuals()
        {
            if (!m_IsShowingLoadingVisual)
            {
                return;
            }

            m_IsShowingLoadingVisual = false;
            m_LoadingProgress = null;
            ApplyLoadingVisual(0.0f, restoreDefaults: true);
        }

        private void UpdateLoadingVisuals()
        {
            if (!m_IsShowingLoadingVisual)
            {
                return;
            }

            float elapsed = Time.realtimeSinceStartup - m_LoadingVisualStartTime;
            float fill;
            if (m_LoadingProgress is float progress)
            {
                fill = Mathf.Lerp(m_LoadingMinFill, 1.0f, Mathf.Clamp01(progress));
            }
            else if (elapsed < m_LoadingFillDuration)
            {
                fill = Mathf.Lerp(
                    m_LoadingMinFill,
                    m_LoadingMinFill,
                    elapsed / Mathf.Max(m_LoadingFillDuration, 0.0001f));
            }
            else
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(
                    (elapsed - m_LoadingFillDuration) * m_LoadingPulseFrequency * Mathf.PI * 2.0f);
                fill = m_LoadingMinFill + pulse * m_LoadingPulseAmplitude;
            }

            ApplyLoadingVisual(fill);
        }

        private void UpdateLoadingProgress(float progress)
        {
            if (!m_IsShowingLoadingVisual)
            {
                StartLoadingVisuals();
            }

            m_LoadingProgress = Mathf.Clamp01(progress);
            ApplyLoadingVisual(Mathf.Lerp(m_LoadingMinFill, 1.0f, m_LoadingProgress.Value));
        }

        private void ApplyLoadingVisual(float fill, bool restoreDefaults = false)
        {
            Renderer thumbnailRenderer = GetThumbnailRenderer();
            if (thumbnailRenderer == null)
            {
                return;
            }

            Material[] materials = thumbnailRenderer.materials;
            ApplyLoadingVisualToMaterials(materials, restoreDefaults ? 0.0f : fill);
            thumbnailRenderer.materials = materials;
            SyncTrackedLoadingVisualCopies(thumbnailRenderer, restoreDefaults ? 0.0f : fill);
        }

        private void LogThumbnailStateIfChanged()
        {
            Renderer thumbnailRenderer = GetThumbnailRenderer();
            if (thumbnailRenderer == null)
            {
                return;
            }

            Material material = thumbnailRenderer.material;
            if (material == null)
            {
                return;
            }

            Texture currentTexture = material.mainTexture;
            if (m_HasLoggedThumbnailState && currentTexture == m_LastLoggedThumbnail)
            {
                return;
            }

            Vector2 scale = material.HasProperty("_MainTex")
                ? material.GetTextureScale("_MainTex")
                : Vector2.zero;
            Vector2 offset = material.HasProperty("_MainTex")
                ? material.GetTextureOffset("_MainTex")
                : Vector2.zero;

            Debug.Log(
                $"{kLogPrefix} Observed thumbnail state change on renderer '{thumbnailRenderer.name}' material '{material.name}' " +
                $"for '{m_Destination}' on {name}. mainTexture='{DescribeTexture(currentTexture)}' scale={scale} offset={offset}");

            m_LastLoggedThumbnail = currentTexture;
            m_HasLoggedThumbnailState = true;
        }

        private Renderer GetThumbnailRenderer()
        {
            return m_Mesh != null ? m_Mesh.GetComponent<Renderer>() : null;
        }

        private static string DescribeTexture(Texture texture)
        {
            return texture == null ? "null" : $"{texture.name} ({texture.width}x{texture.height})";
        }

        private void TryPreloadDestinationSketch()
        {
            if (string.IsNullOrWhiteSpace(m_Destination) || SketchCatalog.m_Instance == null)
            {
                return;
            }

            foreach (var setType in new[]
                     {
                         SketchSetType.Curated,
                         SketchSetType.Liked,
                         SketchSetType.User,
                     })
            {
                var icosaSet = SketchCatalog.m_Instance.GetSet(setType) as IcosaSketchSet;
                if (icosaSet == null)
                {
                    continue;
                }

                if (!icosaSet.IsReadyForAccess)
                {
                    icosaSet.RequestRefresh();
                    continue;
                }

                if (!icosaSet.TryPreloadSketchForAssetId(m_Destination))
                {
                    continue;
                }

                return;
            }
        }

        private bool TryGetDestinationSketchInfo(
            out SketchSetType setType,
            out IcosaSketchSet icosaSet,
            out int sketchIndex,
            out SceneFileInfo sceneFileInfo)
        {
            setType = default;
            icosaSet = null;
            sketchIndex = -1;
            sceneFileInfo = null;

            if (string.IsNullOrWhiteSpace(m_Destination) || SketchCatalog.m_Instance == null)
            {
                return false;
            }

            foreach (var candidateSetType in new[]
                     {
                         SketchSetType.Curated,
                         SketchSetType.Liked,
                         SketchSetType.User,
                     })
            {
                var candidateSet = SketchCatalog.m_Instance.GetSet(candidateSetType) as IcosaSketchSet;
                if (candidateSet == null || !candidateSet.IsReadyForAccess)
                {
                    continue;
                }

                if (!candidateSet.TryGetSketchIndexForAssetId(m_Destination, out var candidateIndex))
                {
                    continue;
                }

                var candidateSceneFileInfo = candidateSet.GetSketchSceneFileInfo(candidateIndex);
                if (candidateSceneFileInfo == null)
                {
                    Debug.LogWarning(
                        $"{kLogPrefix} Resolved destination '{m_Destination}' via {candidateSetType} set index {candidateIndex}, but SceneFileInfo was null on {name}");
                    return false;
                }

                setType = candidateSetType;
                icosaSet = candidateSet;
                sketchIndex = candidateIndex;
                sceneFileInfo = candidateSceneFileInfo;
                return true;
            }

            return false;
        }

        private void WaitForPreloadThenLoad(IcosaSketchSet icosaSet)
        {
            if (icosaSet == null || string.IsNullOrWhiteSpace(m_Destination))
            {
                return;
            }

            if (m_WaitForPreloadThenLoadCoroutine != null && m_WaitForPreloadDestination == m_Destination)
            {
                return;
            }

            StopWaitingForPreload();
            m_WaitForPreloadDestination = m_Destination;
            m_WaitForPreloadThenLoadCoroutine =
                StartCoroutine(WaitForPreloadThenLoadCoroutine(m_Destination, icosaSet));
        }

        private System.Collections.IEnumerator WaitForPreloadThenLoadCoroutine(
            string assetId,
            IcosaSketchSet icosaSet)
        {
            while (m_Destination == assetId && icosaSet != null && icosaSet.IsPreloadingSketchForAssetId(assetId))
            {
                yield return null;
            }

            m_WaitForPreloadThenLoadCoroutine = null;
            m_WaitForPreloadDestination = null;

            if (m_Destination != assetId)
            {
                yield break;
            }

            TryLoadDestinationSketch();
        }

        private bool ExecutePortalCommand()
        {
            if (AudioManager.m_Instance != null)
            {
                AudioManager.m_Instance.PlayPopUpSound(transform.position);
            }
            StartLoadingVisuals();
            return TryLoadDestinationSketch();
        }

        private bool TryLoadDestinationSketch()
        {
            if (string.IsNullOrWhiteSpace(m_Destination) || SketchCatalog.m_Instance == null)
            {
                Debug.LogWarning($"{kLogPrefix} Cannot load destination. Destination empty or SketchCatalog unavailable on {name}");
                return false;
            }

            if (TryGetDestinationSketchInfo(out var setType, out var icosaSet, out var sketchIndex, out var sceneFileInfo))
            {
                if (!sceneFileInfo.Available)
                {
                    if (icosaSet.IsPreloadingSketchForAssetId(m_Destination))
                    {
                        StartLoadingVisuals();
                        WaitForPreloadThenLoad(icosaSet);
                        return true;
                    }

                    StartLoadingVisuals();
                    Debug.Log(
                        $"{kLoadCompareLogPrefix} Portal taking direct Icosa fetch branch. " +
                        $"destination='{m_Destination}' set={setType} index={sketchIndex} portal={name}");
                    StartCoroutine(VrAssetService.m_Instance.LoadTiltFile(m_Destination, UpdateLoadingProgress));
                    return true;
                }

                StartLoadingVisuals();
                Debug.Log(
                    $"{kLoadCompareLogPrefix} Portal issuing LoadConfirmUnsaved. " +
                    $"destination='{m_Destination}' set={setType} index={sketchIndex} portal={name}");
                SketchControlsScript.m_Instance.IssueGlobalCommand(
                    SketchControlsScript.GlobalCommands.LoadConfirmUnsaved,
                    sketchIndex,
                    (int)setType);
                return true;
            }

            Debug.LogWarning(
                $"{kLoadCompareLogPrefix} Portal destination not found in loaded sketch sets. " +
                $"destination='{m_Destination}' portal={name}");
            Debug.LogWarning($"{kLogPrefix} Destination '{m_Destination}' not found in Curated/Liked/User sketch sets. Using direct Icosa fetch on {name}");
            StartLoadingVisuals();
            StartCoroutine(VrAssetService.m_Instance.LoadTiltFile(m_Destination, UpdateLoadingProgress));
            return true;
        }
    }
} // namespace TiltBrush
