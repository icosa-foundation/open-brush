// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    /// Records the performer's camera at explicit dimensions, including normal panel UI and controller rays.
    /// Fixed simulation steps preserve motion timing when PNG encoding is slower than playback.
    public sealed class ProceduralCapture : MonoBehaviour
    {
        [Serializable]
        private class Metadata
        {
            public int FrameRate;
            public int FrameCount;
            public int Width;
            public int Height;
            public string Format = "png";
        }

        private Metadata m_Metadata;
        private string m_Directory;
        private bool m_StopRequested;
        private float m_PreviousCaptureDelta;
        private readonly Dictionary<Renderer, bool> m_MessageVisibility = new Dictionary<Renderer, bool>();
        private OutputWindowScript m_ErrorOutput;
        private bool m_PreviousErrorSuppression;
        private Camera m_RenderCamera;
        private RenderTexture m_RenderTarget;
        private RenderTexture m_SaveTarget;
        private UniversalAdditionalCameraData m_SourceCameraData;
        private UniversalAdditionalCameraData m_RenderCameraData;
        public bool IsRecording { get; private set; }
        public string OutputDirectory => m_Directory;
        public int FrameCount => m_Metadata?.FrameCount ?? 0;
        public string Error { get; internal set; }

        public string Begin(string filename, int frameRate,
            int width = AutomationScript.OutputOptions.DefaultWidth, int height = AutomationScript.OutputOptions.DefaultHeight)
        {
            if (IsRecording || Time.captureDeltaTime > 0 || VideoRecorderUtils.ActiveVideoRecording != null ||
                VideoRecorderUtils.ActiveStillFrameExporter != null)
            {
                throw new InvalidOperationException("Another capture already owns the recording clock.");
            }
            if (App.Config.m_SdkMode != SdkMode.VirtualInput)
            {
                throw new InvalidOperationException("Rehearsal recording requires Virtual Input mode.");
            }
            if (frameRate < 1 || frameRate > 120) { throw new ArgumentOutOfRangeException(nameof(frameRate)); }
            new AutomationScript.OutputOptions {Width = width, Height = height}.Validate();
            if (width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
                throw new ArgumentException("Output dimensions exceed this device's texture size limit.");
            string name = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrWhiteSpace(name)) { name = $"tutorial-{DateTime.Now:yyyyMMdd-HHmmss}"; }
            string directory = Path.Combine(App.VideosPath(), $"{name}_frames");
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            {
                throw new IOException($"Recording folder already contains files: {directory}");
            }
            Directory.CreateDirectory(directory);
            m_Directory = directory;
            Error = null;
            m_Metadata = new Metadata { FrameRate = frameRate, Width = width, Height = height };
            try { CreateRenderTargets(width, height); }
            catch { ReleaseRenderTargets(); throw; }
            m_PreviousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / frameRate;
            m_StopRequested = false;
            IsRecording = true;
            m_ErrorOutput = OutputWindowScript.m_Instance;
            if (m_ErrorOutput != null)
            {
                m_PreviousErrorSuppression = m_ErrorOutput.SuppressErrorCards;
                m_ErrorOutput.SuppressErrorCards = true;
            }
            HideMessages(OutputWindowScript.m_Instance);
            HideMessages(ControllerConsoleScript.m_Instance);
            foreach (var overlay in FindObjectsByType<ExceptionRenderScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                HideMessages(overlay);
            }
            foreach (var card in FindObjectsByType<InfoCardAnimation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (card.IsError) { HideMessages(card); }
            }
            StartCoroutine(Capture());
            Debug.Log($"[ProceduralCapture] Recording {width}x{height} at {frameRate} fps to {directory}");
            return directory;
        }

        public void Stop() => m_StopRequested = true;

        private void CreateRenderTargets(int width, int height)
        {
            var source = App.VrSdk.GetVrCamera();
            var owner = new GameObject("Automation Capture Camera") {hideFlags = HideFlags.HideAndDontSave};
            m_RenderCamera = owner.AddComponent<Camera>();
            m_RenderCamera.enabled = false;
            m_SourceCameraData = source.GetComponent<UniversalAdditionalCameraData>();
            m_RenderCameraData = owner.AddComponent<UniversalAdditionalCameraData>();
            if (m_SourceCameraData != null)
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(m_SourceCameraData), m_RenderCameraData);
            m_RenderCameraData.allowXRRendering = false;
            m_RenderCameraData.renderType = CameraRenderType.Base;
            m_RenderTarget = new RenderTexture(CaptureColorUtils.CreateDescriptor(width, height, CaptureColorUtils.GetFormat(source)))
                {name = "Automation capture render", hideFlags = HideFlags.HideAndDontSave};
            m_SaveTarget = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {name = "Automation capture pixels", hideFlags = HideFlags.HideAndDontSave};
            if (!m_RenderTarget.Create() || !m_SaveTarget.Create())
                throw new InvalidOperationException("Could not allocate the automation output textures.");
        }

        private byte[] RenderFrame()
        {
            var source = App.VrSdk.GetVrCamera();
            m_RenderCamera.CopyFrom(source);
            m_RenderCamera.enabled = false;
            m_RenderCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            m_RenderCamera.targetTexture = m_RenderTarget;
            m_RenderCamera.rect = new Rect(0, 0, 1, 1);
            m_RenderCamera.aspect = (float)m_Metadata.Width / m_Metadata.Height;
            if (m_SourceCameraData != null)
            {
                m_RenderCameraData.renderPostProcessing = m_SourceCameraData.renderPostProcessing;
                m_RenderCameraData.antialiasing = m_SourceCameraData.antialiasing;
                m_RenderCameraData.antialiasingQuality = m_SourceCameraData.antialiasingQuality;
                m_RenderCameraData.volumeLayerMask = m_SourceCameraData.volumeLayerMask;
                m_RenderCameraData.volumeTrigger = m_SourceCameraData.volumeTrigger;
                m_RenderCameraData.renderShadows = m_SourceCameraData.renderShadows;
                m_RenderCameraData.stopNaN = m_SourceCameraData.stopNaN;
                m_RenderCameraData.dithering = m_SourceCameraData.dithering;
            }
            m_RenderCamera.Render();
            CaptureColorUtils.Resolve(m_RenderTarget, m_SaveTarget, false);
            return ScreenshotManager.SaveToMemory(m_SaveTarget, true);
        }

        private void ReleaseRenderTargets()
        {
            if (m_RenderCamera != null)
            {
                m_RenderCamera.targetTexture = null;
                Destroy(m_RenderCamera.gameObject);
                m_RenderCamera = null;
            }
            foreach (var target in new[] {m_RenderTarget, m_SaveTarget})
                if (target != null) { target.Release(); Destroy(target); }
            m_RenderTarget = null;
            m_SaveTarget = null;
        }

        private void HideMessages(Component messages)
        {
            if (messages == null) { return; }
            // Lines are pooled at startup. forceRenderingOff leaves their normal lifecycle,
            // log output and renderer.enabled updates intact throughout the recording.
            foreach (var renderer in messages.GetComponentsInChildren<Renderer>(true))
            {
                if (m_MessageVisibility.ContainsKey(renderer)) { continue; }
                m_MessageVisibility.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }

        private IEnumerator Capture()
        {
            try
            {
                while (!m_StopRequested)
                {
                    yield return new WaitForEndOfFrame();
                    if (m_StopRequested || InputManager.m_Instance.ProceduralAutomationStatus == "Stopped") { break; }
                    try
                    {
                        string path = Path.Combine(m_Directory, $"frame_{m_Metadata.FrameCount:D6}.png");
                        File.WriteAllBytes(path, RenderFrame());
                        ++m_Metadata.FrameCount;
                    }
                    catch (Exception exception)
                    {
                        Error = exception.Message;
                        Debug.LogError($"[ProceduralCapture] {Error}");
                        InputManager.m_Instance.StopProceduralAutomation();
                        break;
                    }
                    if (InputManager.m_Instance.ProceduralAutomationStatus == "Stopped") { break; }
                }
            }
            finally { Finish(); }
        }

        private void OnDisable() => Finish();

        private void Finish()
        {
            if (!IsRecording) { return; }
            IsRecording = false;
            m_StopRequested = true;
            Time.captureDeltaTime = m_PreviousCaptureDelta;
            foreach (var message in m_MessageVisibility)
            {
                if (message.Key != null) { message.Key.forceRenderingOff = message.Value; }
            }
            m_MessageVisibility.Clear();
            if (m_ErrorOutput != null) { m_ErrorOutput.SuppressErrorCards = m_PreviousErrorSuppression; }
            m_ErrorOutput = null;
            ReleaseRenderTargets();
            try { File.WriteAllText(Path.Combine(m_Directory, "recording.json"), JsonUtility.ToJson(m_Metadata, true)); }
            catch (Exception exception)
            {
                Error = exception.Message;
                Debug.LogError($"[ProceduralCapture] Could not save recording metadata: {Error}");
            }
            Debug.Log($"[ProceduralCapture] Saved {m_Metadata.FrameCount} frames to {m_Directory}");
        }
    }
}
