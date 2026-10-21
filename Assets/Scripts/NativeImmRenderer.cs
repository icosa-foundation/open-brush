// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using ImmPlayer;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush
{
    /// <summary>Shares one native render registration across whole sketches and IMM references.</summary>
    [DefaultExecutionOrder(90)]
    public sealed class NativeImmRenderer : MonoBehaviour
    {
        private static int sm_ActiveUsers;
        private static ImmPlayerManager sm_ActiveManager;
        private int m_Users;
        private Camera m_Camera;
        private ImmPlayerManager m_Manager;
        private ImmCamera m_Registration;
        private bool m_OwnsRegistration;
        private bool m_UsesRenderGraph;

        internal static NativeImmRenderer RetainForCapture(Camera camera)
        {
            return sm_ActiveUsers > 0 && sm_ActiveManager != null
                ? Retain(camera, sm_ActiveManager) : null;
        }

        public static NativeImmRenderer Retain(Camera camera, ImmPlayerManager manager)
        {
            var renderer = camera.GetComponent<NativeImmRenderer>();
            if (renderer == null) renderer = camera.gameObject.AddComponent<NativeImmRenderer>();
            renderer.m_Camera = camera;
            renderer.m_Manager = manager;
            var device = SystemInfo.graphicsDeviceType;
            renderer.m_UsesRenderGraph = device == GraphicsDeviceType.Direct3D12 ||
                device == GraphicsDeviceType.Metal || device == GraphicsDeviceType.Vulkan;
            if (renderer.m_UsesRenderGraph && renderer.m_Registration == null)
            {
                renderer.m_Registration = camera.GetComponent<ImmCamera>();
                if (renderer.m_Registration == null)
                {
                    renderer.m_Registration = camera.gameObject.AddComponent<ImmCamera>();
                    renderer.m_OwnsRegistration = true;
                }
            }
            renderer.m_Users++;
            ++sm_ActiveUsers;
            sm_ActiveManager = manager;
            renderer.enabled = true;
            if (renderer.m_OwnsRegistration) renderer.m_Registration.enabled = true;
            return renderer;
        }

        public void Release()
        {
            if (m_Users <= 0) return;
            --sm_ActiveUsers;
            if (sm_ActiveUsers == 0) sm_ActiveManager = null;
            if (--m_Users != 0) return;
            enabled = false;
            if (m_OwnsRegistration && m_Registration != null) m_Registration.enabled = false;
            // Keep this lightweight registration on the camera for rapid undo/reinsertion.
        }

        private void OnEnable() => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        private void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        private void LateUpdate()
        {
            if (!m_UsesRenderGraph && m_Manager != null && m_Camera != null)
                m_Manager.SetCameraMatrices(0, m_Camera, ImmPlayerManager.StereoMode.Mono);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (m_UsesRenderGraph || camera != m_Camera || m_Manager == null || m_Users == 0) return;
            m_Manager.SetCameraMatrices(0, camera, ImmPlayerManager.StereoMode.Mono);
            m_Manager.IssueRenderEvent(0);
        }

        private void OnDestroy()
        {
            sm_ActiveUsers -= m_Users;
            m_Users = 0;
            if (sm_ActiveUsers == 0) sm_ActiveManager = null;
            if (m_OwnsRegistration && m_Registration != null) Destroy(m_Registration);
        }
    }
}
