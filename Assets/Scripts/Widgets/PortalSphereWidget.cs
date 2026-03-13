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
    public class PortalSphereWidget : SphereStencil
    {
        private const string kLogPrefix = "[PortalDbg_20260313]";

        [SerializeField] private string m_Destination;
        [SerializeField] private bool m_TriggerWithBrushInside = true;
        [SerializeField] private bool m_AllowRepeatWhileInside;
        [SerializeField] private SketchControlsScript.GlobalCommands m_Command =
            SketchControlsScript.GlobalCommands.Null;
        [SerializeField] private int m_CommandParam1 = -1;
        [SerializeField] private int m_CommandParam2 = -1;
        [SerializeField] private string m_CommandStringParam;

        private bool m_CommandTriggeredWhileInside;
        private Texture m_DefaultThumbnail;
        private IcosaSketchSet[] m_ObservedSketchSets;
        private Coroutine m_ThumbnailFetchCoroutine;
        private string m_ThumbnailFetchAssetId;
        private Texture m_LastLoggedThumbnail;
        private bool m_HasLoggedThumbnailState;
        private Texture2D m_OwnedThumbnail;

        public string Destination
        {
            get => m_Destination;
            set
            {
                m_Destination = value;
                RefreshDestinationThumbnail();
            }
        }

        public override bool ParticipatesInMagnetization => false;

        protected override void Awake()
        {
            base.Awake();
            Renderer thumbnailRenderer = GetThumbnailRenderer();
            if (thumbnailRenderer != null)
            {
                m_DefaultThumbnail = thumbnailRenderer.material.mainTexture;
            }
        }

        private void OnEnable()
        {
            RefreshDestinationThumbnail();
        }

        protected override void OnDestroy()
        {
            UnsubscribeFromSketchSets();
            StopThumbnailFetch();
            ReleaseOwnedThumbnail();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
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
                return;
            }

            Debug.LogWarning($"{kLogPrefix} Thumbnail not available yet for '{m_Destination}' on {name}");
            ApplyThumbnailTexture(m_DefaultThumbnail);
            SubscribeToSketchSets();
            EnsureThumbnailFetchStarted();
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

        private bool ExecutePortalCommand()
        {
            if (TryLoadDestinationSketch())
            {
                return true;
            }

            if (m_Command == SketchControlsScript.GlobalCommands.Null)
            {
                Debug.LogWarning($"{kLogPrefix} No destination load path or fallback command for '{m_Destination}' on {name}");
                return false;
            }

            Debug.Log($"{kLogPrefix} Executing fallback command {m_Command} for '{m_Destination}' on {name}");
            SketchControlsScript.m_Instance.IssueGlobalCommand(
                m_Command,
                m_CommandParam1,
                m_CommandParam2,
                string.IsNullOrWhiteSpace(m_CommandStringParam) ? null : m_CommandStringParam);
            return true;
        }

        private bool TryLoadDestinationSketch()
        {
            if (string.IsNullOrWhiteSpace(m_Destination) || SketchCatalog.m_Instance == null)
            {
                Debug.LogWarning($"{kLogPrefix} Cannot load destination. Destination empty or SketchCatalog unavailable on {name}");
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
                if (icosaSet == null || !icosaSet.IsReadyForAccess)
                {
                    continue;
                }

                if (!icosaSet.TryGetSketchIndexForAssetId(m_Destination, out var sketchIndex))
                {
                    continue;
                }

                var sceneFileInfo = icosaSet.GetSketchSceneFileInfo(sketchIndex);
                if (sceneFileInfo == null)
                {
                    Debug.LogWarning($"{kLogPrefix} Resolved destination '{m_Destination}' via {setType} set index {sketchIndex}, but SceneFileInfo was null on {name}");
                    return false;
                }

                if (!sceneFileInfo.Available)
                {
                    StartCoroutine(VrAssetService.m_Instance.LoadTiltFile(m_Destination));
                    return true;
                }

                SketchControlsScript.m_Instance.IssueGlobalCommand(
                    SketchControlsScript.GlobalCommands.LoadConfirmUnsaved,
                    sketchIndex,
                    (int)setType);
                return true;
            }

            Debug.LogWarning($"{kLogPrefix} Destination '{m_Destination}' not found in Curated/Liked/User sketch sets. Using direct Icosa fetch on {name}");
            StartCoroutine(VrAssetService.m_Instance.LoadTiltFile(m_Destination));
            return true;
        }
    }
}
