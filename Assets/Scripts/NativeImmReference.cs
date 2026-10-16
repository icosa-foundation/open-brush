// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections;
using ImmPlayer;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>Owns one native document, without changing the sketch, environment or viewpoint.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class NativeImmReference : MonoBehaviour
    {
        private const string LogPrefix = "[OB_ICOSA_IMM_20261016]";
        private const float LoadTimeoutSeconds = 120;
        [SerializeField] private string m_Path;
        private bool m_IsInstance;
        private bool m_Ready;
        private bool m_Visible;
        private ImmPlayerManager m_Manager;
        private ImmDocument m_Document;
        private NativeImmRenderer m_Renderer;
        private Matrix4x4 m_PreviousTransform;

        public static IEnumerator CreateModelCoroutine(string assetId, string path, Action<Model> onReady)
        {
            if (!TryLoad(path, out var manager, out var document, out var renderer)) yield break;
            try
            {
                float started = Time.realtimeSinceStartup;
                while (!IsReady(document))
                {
                    if (LoadFailed(document, started, path)) yield break;
                    yield return null;
                }
                // Bounds include the native document transform. Use Unity's identity convention
                // (including the adapter's handedness conversion) before reading local bounds.
                document.SetTransform(Matrix4x4.identity);
                Model model = null;
                try
                {
                    model = Model.CreateNativeImm(assetId, path, document.GetBoundingBox());
                    model.m_ModelParent.gameObject.AddComponent<NativeImmReference>().m_Path = path;
                }
                catch (Exception error) { ReportFailure(path, error.Message); }
                if (model != null) onReady(model);
            }
            finally
            {
                manager.UnloadDocument(document);
                renderer.Release();
            }
        }

        // Only actual ModelWidget instances activate native rendering. Templates, previews and
        // snap ghosts may be cloned freely without creating native documents.
        public void Activate()
        {
            m_IsInstance = true;
            if (isActiveAndEnabled) StartCoroutine(LoadCoroutine());
        }

        private void OnEnable()
        {
            if (!m_IsInstance) return;
            if (m_Ready)
            {
                RetainRenderer();
                UpdateVisibilityAndTransform();
            }
            else StartCoroutine(LoadCoroutine());
        }

        private IEnumerator LoadCoroutine()
        {
            if (m_Document == null && !TryLoad(m_Path, out m_Manager, out m_Document, out m_Renderer))
                yield break;
            RetainRenderer();
            float started = Time.realtimeSinceStartup;
            while (!IsReady(m_Document))
            {
                if (LoadFailed(m_Document, started, m_Path))
                {
                    ReleaseDocument();
                    yield break;
                }
                yield return null;
            }
            m_Ready = true;
            UpdateVisibilityAndTransform();
            Debug.Log($"{LogPrefix} Reference ready: {m_Path}, document={m_Document.DocumentId}");
        }

        private static bool IsReady(ImmDocument document) =>
            document.GetStateInfo().Loading == ImmDocument.LoadingState.Loaded && document.IsSequenceReady();

        private static bool LoadFailed(ImmDocument document, float started, string path)
        {
            if (document.GetStateInfo().Loading != ImmDocument.LoadingState.Failed &&
                Time.realtimeSinceStartup - started < LoadTimeoutSeconds) return false;
            ReportFailure(path, "Native document failed to finish loading");
            return true;
        }

        private static bool TryLoad(string path, out ImmPlayerManager manager,
            out ImmDocument document, out NativeImmRenderer renderer)
        {
            manager = null;
            document = null;
            renderer = null;
            try
            {
                if (!NativeImmImport.CanLoad(path)) throw new InvalidOperationException("Native IMM rendering is unavailable");
                manager = ImmPlayerManager.Instance;
                if (!manager.IsInitialized && !manager.Initialize())
                    throw new InvalidOperationException("IMM player initialization failed");
                renderer = NativeImmRenderer.Retain(App.VrSdk.GetVrCamera(), manager);
                document = manager.LoadDocument(path, new ImmDocumentLoadOptions
                {
                    InitiallyHidden = true,
                    Suppress360Backdrops = true
                });
                if (document == null) throw new InvalidOperationException("IMM player rejected the document");
                return true;
            }
            catch (Exception error)
            {
                if (document != null) manager.UnloadDocument(document);
                if (renderer != null) renderer.Release();
                renderer = null;
                document = null;
                ReportFailure(path, error.Message);
                return false;
            }
        }

        private void RetainRenderer()
        {
            if (m_Renderer == null)
                m_Renderer = NativeImmRenderer.Retain(App.VrSdk.GetVrCamera(), m_Manager);
        }

        private void LateUpdate()
        {
            if (m_Ready) UpdateVisibilityAndTransform();
        }

        private void UpdateVisibilityAndTransform()
        {
            var matrix = transform.localToWorldMatrix;
            // Intro/hide animations can collapse a widget to zero. Avoid passing a singular
            // matrix to the native transform decomposition, and hide until it has a size.
            bool hasSize = Mathf.Abs(matrix.determinant) > 1e-12f;
            if (!hasSize)
            {
                if (m_Visible) m_Document.Hide();
                m_Visible = false;
                return;
            }
            if (!m_Visible || matrix != m_PreviousTransform)
            {
                m_Document.SetTransform(matrix);
                m_PreviousTransform = matrix;
            }
            if (!m_Visible)
            {
                m_Document.Show();
                m_Visible = true;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (m_Document != null && m_Ready) m_Document.Hide();
            m_Visible = false;
            if (m_Renderer != null) m_Renderer.Release();
            m_Renderer = null;
        }

        private void OnDestroy() => ReleaseDocument();

        private void ReleaseDocument()
        {
            if (m_Document != null && m_Manager != null) m_Manager.UnloadDocument(m_Document);
            m_Document = null;
            m_Ready = false;
            if (m_Renderer != null) m_Renderer.Release();
            m_Renderer = null;
        }

        private static void ReportFailure(string path, string reason)
        {
            Debug.LogWarning($"{LogPrefix} {reason}: {path}");
            OutputWindowScript.Error("Could not load IMM reference", reason);
        }
    }
}
