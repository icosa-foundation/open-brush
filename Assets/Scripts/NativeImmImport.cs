// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Collections;
using System.IO;
using System.Linq;
using ImmPlayer;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    /// <summary>Owns a non-editable IMM document for the current sketch session.</summary>
    // Read evaluated viewpoint state after ImmPlayerManager's LateUpdate.
    [DefaultExecutionOrder(100)]
    public sealed class NativeImmImport : MonoBehaviour
    {
        private const string LogPrefix = "[OB_NATIVE_IMM_20261013]";
        private static NativeImmImport s_Active;
        private static int s_UnloadingDocumentId = -1;

        private ImmDocument m_Document;
        private ImmPlayerManager m_Manager;
        private ImmCamera m_CameraRegistration;
        private bool m_OwnsCameraRegistration;
        private Camera m_Camera;
        private bool m_UsesRenderGraph;
        private bool m_Ready;
        private bool m_AlignInitialView;
        private int m_ChapterIndex;
        private string m_Path;
        private bool m_DeletePathOnUnload;
        private int[] m_ViewpointIds = new int[0];
        private int m_ViewpointIndex = -1;
        private int m_ViewpointCount;
        private int m_SelectedViewpointId = -1;
        private bool m_PlaybackStarted;
        private ulong m_ChapterRequest;
        private ulong m_ChapterNavigationRequest;
        private SpawnAreaSnapshot m_ViewpointState;
        private SpawnAreaPose m_PreviousViewpointPose;

        public static int ViewpointCount => s_Active != null && s_Active.m_Ready && !IsChangingChapter
            ? s_Active.m_ViewpointCount : 0;
        public static int CurrentViewpointIndex => s_Active != null ? s_Active.m_ViewpointIndex : -1;

        public static bool IsChangingChapter => s_Active != null && s_Active.m_ChapterNavigationRequest != 0;
        public static int ChapterCount => s_Active != null && s_Active.m_Ready
            ? s_Active.m_Document.GetChapterCount() : 0;
        public static bool CanPause => s_Active != null && s_Active.m_Ready && !IsChangingChapter &&
            s_Active.m_Document.GetStateInfo().Playback == ImmDocument.PlaybackState.Playing;

        public static void PausePlayback()
        {
            if (!CanPause) return;
            s_Active.m_Document.Pause();
            Debug.Log("[OB_IMM_PLAYBACK_20261014] Pause requested");
        }

        public static bool CanContinue
        {
            get
            {
                if (s_Active == null || !s_Active.m_Ready || IsChangingChapter) return false;
                var playback = s_Active.m_Document.GetStateInfo().Playback;
                return playback == ImmDocument.PlaybackState.Waiting || playback == ImmDocument.PlaybackState.Paused;
            }
        }

        public static void ContinuePlayback()
        {
            if (!CanContinue) return;
            if (s_Active.m_Document.GetStateInfo().Playback == ImmDocument.PlaybackState.Waiting)
                s_Active.m_Document.Continue();
            else
                s_Active.m_Document.Resume();
            Debug.Log("[OB_IMM_PLAYBACK_20261014] Play/Continue requested");
        }

        public static void CycleChapter(int direction)
        {
            if (ChapterCount < 2 || IsChangingChapter || !s_Active.RefreshViewpoints()) return;
            int target = (s_Active.m_ViewpointState.chapter + direction + ChapterCount) % ChapterCount;
            s_Active.m_ChapterNavigationRequest = s_Active.m_Document.RequestSetChapter(target);
            if (IsChangingChapter)
                Debug.Log($"[OB_IMM_PLAYBACK_20261014] Chapter {target} requested, token={s_Active.m_ChapterNavigationRequest}");
        }

        public static void CycleViewpoint(int direction)
        {
            if (s_Active == null || !s_Active.m_Ready || IsChangingChapter) return;
            var active = s_Active;
            if (!active.RefreshViewpoints() || active.m_ViewpointCount < 2 ||
                active.m_ViewpointState.commandStatus == PlaybackRequestStatus.Pending) return;
            int count = active.m_ViewpointCount;
            int index = active.m_ViewpointIndex < 0
                ? (direction < 0 ? count - 1 : 0)
                : (active.m_ViewpointIndex + direction + count) % count;
            if (!active.SelectViewpoint(active.m_ViewpointIds[index])) return;
            Debug.Log($"[OB_IMM_VIEWPOINT_20261014] Viewpoint {index + 1}/{count}");
        }

        public static string CurrentPath => s_Active != null ? s_Active.m_Path : null;
        public static bool IsActive => s_Active != null;
        public static bool IsLoading => s_Active != null && !s_Active.m_Ready;
        public static int? CurrentChapterIndex => s_Active != null ? s_Active.m_ChapterIndex : null;
        public static TrTransform? CurrentSceneTransform => s_Active != null
            ? TrTransform.FromLocalTransform(s_Active.transform) : null;

        public static bool AllowsEnvironment(Environment environment) => !IsActive ||
            environment != null && (environment.name == "Black" || environment.name == "White");

        public static bool CanLoad(string path)
        {
            if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".imm", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"{LogPrefix} IMM file not found: {path}");
                return false;
            }

            if (App.VrSdk == null || App.VrSdk.GetVrCamera() == null)
            {
                Debug.LogError($"{LogPrefix} No active viewing camera for native IMM rendering.");
                return false;
            }

            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {
                Debug.LogError($"{LogPrefix} Native IMM import requires the URP renderer.");
                return false;
            }

            return true;
        }

        public static void Load(string path, int chapterIndex, TrTransform? sceneTransform = null)
        {
            Unload();
            var black = EnvironmentCatalog.m_Instance.AllEnvironments.First(
                environment => environment.name == "Black");
            SceneSettings.m_Instance.SetDesiredPreset(black, keepSceneTransform: true,
                skipFade: true);
            var owner = new GameObject("Native IMM Import").AddComponent<NativeImmImport>();
            s_Active = owner;
            owner.m_Path = path;
            owner.m_ChapterIndex = chapterIndex;
            owner.m_AlignInitialView = !sceneTransform.HasValue;
            owner.transform.SetParent(App.Scene.transform, false);
            if (sceneTransform.HasValue)
            {
                var pose = sceneTransform.Value;
                owner.transform.localPosition = pose.translation;
                owner.transform.localRotation = pose.rotation;
                owner.transform.localScale = Vector3.one * pose.scale;
            }
            else
            {
                owner.transform.localRotation = Quaternion.identity;
                owner.transform.localScale = Vector3.one * 10f;
            }
            owner.StartCoroutine(owner.BeginLoadAfterUnload());
        }

        public static void LoadFromSketch(SceneFileInfo fileInfo, int chapterIndex, TrTransform? sceneTransform)
        {
            using (var source = fileInfo.GetReadStream(TiltFile.FN_NATIVE_IMM))
            {
                if (source == null)
                {
                    Debug.LogError($"{LogPrefix} Saved sketch has no embedded native IMM: {fileInfo.FullPath}");
                    return;
                }

                string path = Path.Combine(Application.temporaryCachePath, $"open-brush-imm-{System.Guid.NewGuid():N}.imm");
                try
                {
                    using (var destination = File.Create(path)) source.CopyTo(destination);
                    if (!CanLoad(path))
                    {
                        File.Delete(path);
                        return;
                    }
                    Load(path, chapterIndex, sceneTransform);
                    if (s_Active != null) s_Active.m_DeletePathOnUnload = true;
                    else File.Delete(path);
                }
                catch (System.Exception error)
                {
                    Debug.LogError($"{LogPrefix} Failed to extract saved native IMM: {error}");
                    if (File.Exists(path)) File.Delete(path);
                }
            }
        }

        public static void Unload()
        {
            if (s_Active == null) return;
            var active = s_Active;
            s_Active = null;
            if (active.m_Document != null)
                s_UnloadingDocumentId = active.m_Document.DocumentId;
            Destroy(active.gameObject);
        }

        private IEnumerator BeginLoadAfterUnload()
        {
            if (s_UnloadingDocumentId >= 0)
            {
                int documentId = s_UnloadingDocumentId;
                yield return null; // Let the previous owner's OnDestroy request its unload.
                while (ImmNativePlugin.IsDocumentActive(documentId))
                    yield return null;
                s_UnloadingDocumentId = -1;
                Debug.Log($"{LogPrefix} Previous document {documentId} finished unloading");
            }
            BeginLoad();
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void BeginLoad()
        {
            try
            {
                m_Manager = ImmPlayerManager.Instance;
                if (!m_Manager.IsInitialized && !m_Manager.Initialize())
                    throw new System.InvalidOperationException("IMM player initialization failed");

                m_Camera = App.VrSdk.GetVrCamera();
                var device = SystemInfo.graphicsDeviceType;
                m_UsesRenderGraph = device == GraphicsDeviceType.Direct3D12 ||
                    device == GraphicsDeviceType.Metal || device == GraphicsDeviceType.Vulkan;
                if (m_UsesRenderGraph)
                {
                    m_CameraRegistration = m_Camera.GetComponent<ImmCamera>();
                    if (m_CameraRegistration == null)
                    {
                        m_CameraRegistration = m_Camera.gameObject.AddComponent<ImmCamera>();
                        m_OwnsCameraRegistration = true;
                    }
                }

                m_Document = m_Manager.LoadDocument(m_Path);
                if (m_Document == null)
                    throw new System.InvalidOperationException("IMM player rejected the document");

                Debug.Log($"{LogPrefix} Loading native document: {m_Path}");
            }
            catch (System.Exception error)
            {
                Debug.LogError($"{LogPrefix} Failed to load {m_Path}: {error}");
                Unload();
            }
        }

        private void LateUpdate()
        {
            if (m_Document == null || !m_Document.IsLoaded) return;
            if (m_Ready && transform.hasChanged)
            {
                m_Document.SetTransform(transform);
                transform.hasChanged = false;
            }
            if (!m_UsesRenderGraph && m_Camera != null)
                m_Manager.SetCameraMatrices(0, m_Camera, ImmPlayerManager.StereoMode.Mono);
            if (m_Ready)
            {
                if (!RefreshViewpoints()) return;
                if (m_ChapterNavigationRequest != 0)
                {
                    FinishChapterNavigation();
                    return;
                }
                if (m_ViewpointState.commandStatus == PlaybackRequestStatus.Pending) return;
                m_ChapterIndex = m_ViewpointState.chapter;
                if (m_ViewpointState.needsUpdate != 0)
                    ApplyAuthoredViewpoint();
                else if (m_SelectedViewpointId == m_ViewpointState.activeId &&
                         m_ViewpointState.activeAvailable != 0)
                    FollowViewpoint(m_ViewpointState.activePose);
                if (m_SelectedViewpointId >= 0 && m_ViewpointIndex < 0)
                    m_SelectedViewpointId = -1; // Hidden selection stays parked until explicitly selected again.
                return;
            }
            var state = m_Document.GetStateInfo().Loading;
            if (state == ImmDocument.LoadingState.Failed)
            {
                Debug.LogError($"{LogPrefix} Native document failed to finish loading: {m_Path}");
                Unload();
            }
            else if (state == ImmDocument.LoadingState.Loaded)
            {
                if (!m_Document.IsSequenceReady() || SceneSettings.m_Instance.IsTransitioning)
                    return;
                if (!m_PlaybackStarted)
                {
                    m_Document.Show();
                    m_Document.Resume();
                    m_PlaybackStarted = true;
                }
                if (m_ChapterIndex >= 0)
                {
                    if (m_ChapterRequest == 0)
                    {
                        if (m_ChapterIndex >= m_Document.GetChapterCount())
                        {
                            Debug.LogError($"[OB_IMM_VIEWPOINT_20261014] Invalid chapter {m_ChapterIndex}, count={m_Document.GetChapterCount()}");
                            Unload();
                            return;
                        }
                        m_ChapterRequest = m_Document.RequestSetChapter(m_ChapterIndex);
                        return; // Zero means busy; retry on a later player update.
                    }
                    if (!RefreshViewpoints()) return;
                    if (!m_ViewpointState.IsComplete(m_ChapterRequest))
                    {
                        if (m_ViewpointState.requestedCommand != m_ChapterRequest ||
                            m_ViewpointState.commandStatus == PlaybackRequestStatus.Rejected ||
                            m_ViewpointState.commandStatus == PlaybackRequestStatus.Superseded)
                        {
                            Debug.LogError($"[OB_IMM_VIEWPOINT_20261014] Chapter {m_ChapterIndex} request {m_ChapterRequest} failed: {m_ViewpointState.commandStatus}");
                            Unload();
                        }
                        return;
                    }
                }
                else if (!RefreshViewpoints()) return;
                if (m_AlignInitialView)
                {
                    // A newly opened file starts at viewpoint 1 in the available UI list.
                    int initialId = m_ViewpointCount > 0 ? m_ViewpointIds[0] : -1;
                    if (initialId >= 0)
                    {
                        if (!SelectViewpoint(initialId)) return;
                    }
                    else
                    {
                        transform.position = m_Camera.transform.position + m_Camera.transform.forward * 20f;
                        Debug.LogWarning($"[OB_IMM_VIEWPOINT_20261014] No available initial viewpoint: {m_Path}");
                    }
                }
                // A saved sketch owns its restored placement. Acknowledge the startup
                // preference without selecting/following it; later authored changes still apply.
                if (m_ViewpointState.needsUpdate != 0)
                    m_Document.SetSpawnAreaNeedsUpdate(false);
                m_ChapterIndex = m_ViewpointState.chapter;
                m_Document.SetTransform(transform);
                transform.hasChanged = false;
                Color background = m_Manager.GetPlayerInfo().BackgroundColor.gamma;
                SceneSettings.m_Instance.SkyColorA = background;
                SceneSettings.m_Instance.SkyColorB = background;
                m_Ready = true;
                Debug.Log($"[OB_IMM_VIEWPOINT_20261014] Evaluated chapter={m_ChapterIndex}, request={m_ChapterRequest}, available={m_ViewpointCount}, restored={!m_AlignInitialView}");
                Debug.Log($"{LogPrefix} Native document ready: {m_Path}");
            }
        }

        private bool RefreshViewpoints()
        {
            if (!m_Document.TryGetSpawnAreaSnapshot(m_ViewpointIds, out m_ViewpointState))
            {
                if (m_ViewpointState.availableCount <= m_ViewpointIds.Length)
                {
                    m_ViewpointCount = 0;
                    m_ViewpointIndex = -1;
                    return false;
                }
                m_ViewpointIds = new int[m_ViewpointState.availableCount];
                if (!m_Document.TryGetSpawnAreaSnapshot(m_ViewpointIds, out m_ViewpointState))
                {
                    m_ViewpointCount = 0;
                    m_ViewpointIndex = -1;
                    return false;
                }
            }
            m_ViewpointCount = m_ViewpointState.availableCount;
            m_ViewpointIndex = System.Array.IndexOf(m_ViewpointIds, m_SelectedViewpointId, 0, m_ViewpointCount);
            return true;
        }

        private void FinishChapterNavigation()
        {
            if (!m_ViewpointState.IsComplete(m_ChapterNavigationRequest))
            {
                if (m_ViewpointState.requestedCommand != m_ChapterNavigationRequest ||
                    m_ViewpointState.commandStatus == PlaybackRequestStatus.Rejected ||
                    m_ViewpointState.commandStatus == PlaybackRequestStatus.Superseded)
                {
                    Debug.LogWarning($"[OB_IMM_PLAYBACK_20261014] Chapter request {m_ChapterNavigationRequest} failed: {m_ViewpointState.commandStatus}");
                    m_ChapterNavigationRequest = 0;
                }
                return;
            }
            int viewpoint = m_ViewpointState.preferredAvailable != 0 ? m_ViewpointState.preferredId
                : (m_ViewpointCount > 0 ? m_ViewpointIds[0] : -1);
            if (viewpoint >= 0 && !SelectViewpoint(viewpoint)) return;
            if (viewpoint < 0) m_SelectedViewpointId = -1;
            if (m_ViewpointState.needsUpdate != 0) m_Document.SetSpawnAreaNeedsUpdate(false);
            m_ChapterIndex = m_ViewpointState.chapter;
            m_ChapterNavigationRequest = 0;
            Debug.Log($"[OB_IMM_PLAYBACK_20261014] Chapter {m_ChapterIndex} evaluated without reloading document {m_Document.DocumentId}");
        }

        private bool SelectViewpoint(int spawnAreaId)
        {
            if (!m_Document.TrySetActiveSpawnAreaId(spawnAreaId, out SpawnAreaPose pose,
                    m_ViewpointState.evaluationRevision)) return false;
            AlignSceneToViewpoint(pose);
            m_SelectedViewpointId = spawnAreaId;
            m_PreviousViewpointPose = pose;
            m_ViewpointIndex = System.Array.IndexOf(m_ViewpointIds, spawnAreaId, 0, m_ViewpointCount);
            return true;
        }

        private void ApplyAuthoredViewpoint()
        {
            if (m_ViewpointState.preferredAvailable != 0)
            {
                if (!SelectViewpoint(m_ViewpointState.preferredId)) return;
            }
            else
            {
                // Retain placement rather than jump to an unrelated eligible viewpoint.
                Debug.Log($"[OB_IMM_VIEWPOINT_20261014] Ignoring unavailable authored viewpoint {m_ViewpointState.preferredId}");
            }
            m_Document.SetSpawnAreaNeedsUpdate(false);
        }

        private TrTransform GetViewpointWorldTransform(SpawnAreaPose pose)
        {
            // Same IMM handedness conversion as ImmDocument's world/view pose helpers.
            // Use the pose returned by checked selection, avoiding a second native query.
            var flip = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));
            var converted = flip * Matrix4x4.TRS(pose.GetPosition(), pose.GetRotation(),
                Vector3.one * pose.sca) * flip;
            Vector3 up = converted.GetColumn(1);
            Vector3 forward = converted.GetColumn(2);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            return TrTransform.TRS(transform.TransformPoint(converted.GetColumn(3)),
                transform.rotation * Quaternion.LookRotation(forward.normalized, up.normalized),
                Mathf.Abs(transform.lossyScale.x * pose.sca));
        }

        private static Quaternion GetViewpointYaw(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.Cross(rotation * Vector3.right, Vector3.up);
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void AlignSceneToViewpoint(SpawnAreaPose pose)
        {
            var xrOrigin = m_Camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            Transform viewTarget = xrOrigin != null ? xrOrigin.transform : m_Camera.transform;
            TrTransform authoredView = GetViewpointWorldTransform(pose);
            // Anchor in world units: the XR rig scales tracked metres to Open Brush units.
            // Eye-level areas place the authored eye at the actual HMD position. Floor-level
            // areas retain the user's tracked height above the rig's floor.
            Vector3 targetPosition = m_Camera.transform.position;
            if (pose.isFloorLevel != 0) targetPosition.y = viewTarget.position.y;
            Quaternion rotation = GetViewpointYaw(m_Camera.transform.rotation) *
                Quaternion.Inverse(GetViewpointYaw(authoredView.rotation));
            float scale = App.METERS_TO_UNITS / authoredView.scale;
            // Start upright rather than let scene tilt protection rotate the anchor afterward.
            App.Scene.Pose = TrTransform.TRS(targetPosition, rotation, scale) *
                TrTransform.T(-authoredView.translation) * App.Scene.Pose;
            UpdateDocumentTransform();
            Debug.Log($"[OB_IMM_VIEW_HEIGHT_20261015] floor={pose.isFloorLevel}, authoredScale={pose.sca}, targetY={targetPosition.y}, sceneScale={App.Scene.Pose.scale}");
        }

        private void FollowViewpoint(SpawnAreaPose pose)
        {
            // Apply only authored motion so headset motion and user scene placement remain free.
            TrTransform previous = GetViewpointWorldTransform(m_PreviousViewpointPose);
            TrTransform current = GetViewpointWorldTransform(pose);
            if (previous.translation != current.translation || previous.rotation != current.rotation ||
                previous.scale != current.scale)
            {
                var previousUpright = TrTransform.TRS(previous.translation,
                    GetViewpointYaw(previous.rotation), previous.scale);
                var currentUpright = TrTransform.TRS(current.translation,
                    GetViewpointYaw(current.rotation), current.scale);
                App.Scene.Pose = previousUpright * currentUpright.inverse * App.Scene.Pose;
                UpdateDocumentTransform();
            }
            m_PreviousViewpointPose = pose;
        }

        private void UpdateDocumentTransform()
        {
            m_Document.SetTransform(transform);
            transform.hasChanged = false;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (m_UsesRenderGraph || camera != m_Camera || m_Document == null || !m_Document.IsLoaded)
                return;
            m_Manager.SetCameraMatrices(0, camera, ImmPlayerManager.StereoMode.Mono);
            m_Manager.IssueRenderEvent(0);
        }

        private void OnDestroy()
        {
            if (s_Active == this) s_Active = null;
            bool canDeleteExtractedFile = m_Document == null ||
                m_Document.GetStateInfo().Loading != ImmDocument.LoadingState.Loading;
            if (m_Document != null && m_Manager != null)
                m_Manager.UnloadDocument(m_Document);
            if (m_OwnsCameraRegistration && m_CameraRegistration != null)
                Destroy(m_CameraRegistration);
            if (m_DeletePathOnUnload && canDeleteExtractedFile && File.Exists(m_Path))
            {
                try { File.Delete(m_Path); }
                catch (IOException error) { Debug.LogWarning($"{LogPrefix} Could not remove temporary IMM: {error.Message}"); }
            }
        }
    }
}
