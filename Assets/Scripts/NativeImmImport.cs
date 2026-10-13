// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Collections;
using System.IO;
using ImmPlayer;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    /// <summary>Owns a non-editable IMM document for the current sketch session.</summary>
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

        public static string CurrentPath => s_Active != null ? s_Active.m_Path : null;
        public static bool IsLoading => s_Active != null && !s_Active.m_Ready;
        public static int? CurrentChapterIndex => s_Active != null ? s_Active.m_ChapterIndex : null;
        public static TrTransform? CurrentSceneTransform => s_Active != null
            ? TrTransform.FromLocalTransform(s_Active.transform) : null;

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

        private void Update()
        {
            if (m_Document == null || !m_Document.IsLoaded) return;
            if (m_Ready && transform.hasChanged)
            {
                m_Document.SetTransform(transform);
                transform.hasChanged = false;
            }
            if (!m_UsesRenderGraph && m_Camera != null)
                m_Manager.SetCameraMatrices(0, m_Camera, ImmPlayerManager.StereoMode.Mono);
            if (m_Ready) return;
            var state = m_Document.GetStateInfo().Loading;
            if (state == ImmDocument.LoadingState.Failed)
            {
                Debug.LogError($"{LogPrefix} Native document failed to finish loading: {m_Path}");
                Unload();
            }
            else if (state == ImmDocument.LoadingState.Loaded)
            {
                if (m_ChapterIndex >= 0) m_Document.SetChapter(m_ChapterIndex);
                if (m_AlignInitialView) AlignSceneToInitialView();
                m_Document.SetTransform(transform);
                transform.hasChanged = false;
                m_Document.Show();
                m_Document.Resume();
                m_Ready = true;
                Debug.Log($"{LogPrefix} Native document ready: {m_Path}");
            }
        }

        private void AlignSceneToInitialView()
        {
            int spawnAreaId = m_Document.GetInitialSpawnAreaId();
            var xrOrigin = m_Camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            Transform viewTarget = xrOrigin != null ? xrOrigin.transform : m_Camera.transform;
            if (spawnAreaId < 0 ||
                !m_Document.TryGetSpawnAreaViewTargetPose(spawnAreaId, transform,
                    viewTarget, m_Camera.transform, true, out Pose targetView))
            {
                transform.position = m_Camera.transform.position + m_Camera.transform.forward * 20f;
                Debug.LogWarning($"{LogPrefix} No initial IMM viewpoint; placed document in front: {m_Path}");
                return;
            }

            TrTransform currentView = TrTransform.TR(viewTarget.position, viewTarget.rotation);
            TrTransform authoredView = TrTransform.TR(targetView.position, targetView.rotation);
            App.Scene.Pose = currentView * authoredView.inverse * App.Scene.Pose;
            Debug.Log($"{LogPrefix} Aligned scene to initial IMM viewpoint: {m_Path}");
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
