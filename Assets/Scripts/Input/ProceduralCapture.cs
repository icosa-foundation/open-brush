// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Records the Game/player view, including the normal panel reticle and controller ray.
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
        public bool IsRecording { get; private set; }
        public string OutputDirectory => m_Directory;
        public int FrameCount => m_Metadata?.FrameCount ?? 0;
        public string Error { get; internal set; }

        public string Begin(string filename, int frameRate)
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
            m_Metadata = new Metadata { FrameRate = frameRate };
            m_PreviousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / frameRate;
            m_StopRequested = false;
            IsRecording = true;
            StartCoroutine(Capture());
            Debug.Log($"[ProceduralCapture] Recording {frameRate} fps to {directory}");
            return directory;
        }

        public void Stop() => m_StopRequested = true;

        private IEnumerator Capture()
        {
            try
            {
                while (!m_StopRequested)
                {
                    yield return new WaitForEndOfFrame();
                    if (m_StopRequested) { break; }
                    Texture2D frame = null;
                    try
                    {
                        frame = ScreenCapture.CaptureScreenshotAsTexture();
                        if (frame == null) { throw new InvalidOperationException("The Game/player view did not produce a frame."); }
                        m_Metadata.Width = frame.width;
                        m_Metadata.Height = frame.height;
                        string path = Path.Combine(m_Directory, $"frame_{m_Metadata.FrameCount:D6}.png");
                        File.WriteAllBytes(path, frame.EncodeToPNG());
                        ++m_Metadata.FrameCount;
                    }
                    catch (Exception exception)
                    {
                        Error = exception.Message;
                        Debug.LogError($"[ProceduralCapture] {Error}");
                        break;
                    }
                    finally { if (frame != null) { Destroy(frame); } }
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
