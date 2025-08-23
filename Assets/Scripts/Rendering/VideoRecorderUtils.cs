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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TiltBrush
{

    static public class VideoRecorderUtils
    {
        static private float m_VideoCaptureResolutionScale = 1.0f;
        static private int m_DebugVideoCaptureQualityLevel = -1;
        static private int m_PreCaptureQualityLevel = -1;

        // [Range(RenderWrapper.SSAA_MIN, RenderWrapper.SSAA_MAX)]
        static private float m_SuperSampling = 2.0f;
        static private float m_PreCaptureSuperSampling = 1.0f;

#if USD_SUPPORTED
        static private UsdPathSerializer m_UsdPathSerializer;
        static private System.Diagnostics.Stopwatch m_RecordingStopwatch;
        static private string m_UsdPath;
#endif

        static private VideoRecorder m_ActiveVideoRecording;
        static private StillFrameSequenceExporter m_ActiveStillFrameExporter;
        static private bool m_UsingStillFrameFallback = false;

        static public VideoRecorder ActiveVideoRecording
        {
            get { return m_ActiveVideoRecording; }
        }

        static public StillFrameSequenceExporter ActiveStillFrameExporter
        {
            get { return m_ActiveStillFrameExporter; }
        }

        static public bool IsUsingStillFrameFallback
        {
            get { return m_UsingStillFrameFallback; }
        }

        static public int NumFramesInUsdSerializer
        {
            get
            {
#if USD_SUPPORTED
                if (m_UsdPathSerializer != null && !m_UsdPathSerializer.IsRecording)
                {
                    return Mathf.CeilToInt((float)m_UsdPathSerializer.Duration *
                        (int)m_ActiveVideoRecording.FPS);
                }
#endif
                return 0;
            }
        }

        static public bool UsdPathSerializerIsBlocking
        {
            get
            {
#if USD_SUPPORTED
                return (m_UsdPathSerializer != null &&
                    !m_UsdPathSerializer.IsRecording &&
                    !m_UsdPathSerializer.IsFinished);
#else
                return false;
#endif
            }
        }

        static public bool UsdPathIsFinished
        {
            get
            {
#if USD_SUPPORTED
                return (m_UsdPathSerializer != null && m_UsdPathSerializer.IsFinished);
#else
                return true;
#endif
            }
        }

        static public Transform AdvanceAndDeserializeUsd()
        {
#if USD_SUPPORTED
            if (m_UsdPathSerializer != null)
            {
                m_UsdPathSerializer.Time += Time.deltaTime;
                m_UsdPathSerializer.Deserialize();
                return m_UsdPathSerializer.transform;
            }
#endif
            return null;
        }

        static public void SerializerNewUsdFrame()
        {
#if USD_SUPPORTED
            if (m_UsdPathSerializer != null && m_UsdPathSerializer.IsRecording)
            {
                m_UsdPathSerializer.Time = (float)m_RecordingStopwatch.Elapsed.TotalSeconds;
                m_UsdPathSerializer.Serialize();
                
                
                // Capture still frame if using fallback mode - only when USD is actively recording
                if (m_UsingStillFrameFallback && m_ActiveStillFrameExporter != null)
                {
                    float currentTime = (float)m_RecordingStopwatch.Elapsed.TotalSeconds;
                    m_ActiveStillFrameExporter.CaptureFrame(currentTime);
                }
            }
#endif
        }

        static public bool StartVideoCapture(string filePath, VideoRecorder recorder,
                                             UsdPathSerializer usdPathSerializer, bool offlineRender = false)
        {
            // Only one video at a time.
            if (m_ActiveVideoRecording != null || m_ActiveStillFrameExporter != null)
            {
                return false;
            }

            // Don't start recording unless there is enough space left.
            if (!FileUtils.InitializeDirectoryWithUserError(
                Path.GetDirectoryName(filePath),
                "Failed to start video capture"))
            {
                return false;
            }

            // Check if ffmpeg is available or if forcing still frame fallback - if not, use still frame fallback
            string ffmpegPath = FfmpegPipe.GetFfmpegExe();
            bool shouldUseFallback = (ffmpegPath == null || App.Config.m_ForceStillFrameFallback) && !offlineRender;

            if (shouldUseFallback)
            {
                return StartStillFrameSequenceCapture(filePath, recorder, usdPathSerializer);
            }

            // Vertical video is disabled.
            recorder.IsPortrait = false;

            // Start the capture first, which may fail, so do this before toggling any state.
            // While the state below is important for the actual frame capture, starting the capture process
            // does not require it.
            int sampleRate = 0;
            if (AudioCaptureManager.m_Instance.IsCapturingAudio)
            {
                sampleRate = AudioCaptureManager.m_Instance.SampleRate;
            }

            if (!recorder.StartCapture(filePath, sampleRate,
                AudioCaptureManager.m_Instance.IsCapturingAudio, offlineRender,
                offlineRender ? App.UserConfig.Video.OfflineFPS : App.UserConfig.Video.FPS))
            {
                OutputWindowScript.ReportFileSaved("Failed to start capture!", null,
                    OutputWindowScript.InfoCardSpawnPos.Brush);
                return false;
            }

            m_ActiveVideoRecording = recorder;

            // Perform any necessary VR camera rendering optimizations to reduce CPU & GPU workload

            // Debug reduce quality for capture.
            // XXX This should just be ADAPTIVE RENDERING
            if (m_DebugVideoCaptureQualityLevel != -1)
            {
                m_PreCaptureQualityLevel = QualityControls.m_Instance.QualityLevel;
                QualityControls.m_Instance.QualityLevel = m_DebugVideoCaptureQualityLevel;
            }

            // Setup SSAA
            RenderWrapper wrapper = recorder.gameObject.GetComponent<RenderWrapper>();
            m_PreCaptureSuperSampling = wrapper.SuperSampling;
            wrapper.SuperSampling = m_SuperSampling;

#if USD_SUPPORTED
            // Read from the Usd serializer if we're recording offline.  Write to it otherwise.
            m_UsdPathSerializer = usdPathSerializer;
            if (!offlineRender)
            {
                m_UsdPath = SaveLoadScript.m_Instance.SceneFile.Valid ?
                    Path.ChangeExtension(filePath, "usda") : null;
                m_RecordingStopwatch = new System.Diagnostics.Stopwatch();
                m_RecordingStopwatch.Start();
                if (!m_UsdPathSerializer.StartRecording(m_UsdPath))
                {
                    UnityEngine.Object.Destroy(m_UsdPathSerializer);
                    m_UsdPathSerializer = null;
                }
            }
            else
            {
                recorder.SetCaptureFramerate(Mathf.RoundToInt(App.UserConfig.Video.OfflineFPS));
                m_UsdPath = null;
                if (m_UsdPathSerializer.Load(App.Config.m_VideoPathToRender))
                {
                    m_UsdPathSerializer.StartPlayback();
                }
                else
                {
                    UnityEngine.Object.Destroy(m_UsdPathSerializer);
                    m_UsdPathSerializer = null;
                }
            }
#endif

            return true;
        }

        static private bool StartStillFrameSequenceCapture(string filePath, VideoRecorder recorder,
                                                          UsdPathSerializer usdPathSerializer)
        {
            // Get or create the still frame exporter component
            StillFrameSequenceExporter exporter = recorder.gameObject.GetComponent<StillFrameSequenceExporter>();
            if (exporter == null)
            {
                exporter = recorder.gameObject.AddComponent<StillFrameSequenceExporter>();
            }

            float fps = App.UserConfig.Video.FPS;
            
            if (!exporter.StartCapture(filePath, fps))
            {
                OutputWindowScript.ReportFileSaved("Failed to start still frame sequence capture!", null,
                    OutputWindowScript.InfoCardSpawnPos.Brush);
                return false;
            }

            m_ActiveStillFrameExporter = exporter;
            m_UsingStillFrameFallback = true;

            // Setup quality settings (same as video recording)
            if (m_DebugVideoCaptureQualityLevel != -1)
            {
                m_PreCaptureQualityLevel = QualityControls.m_Instance.QualityLevel;
                QualityControls.m_Instance.QualityLevel = m_DebugVideoCaptureQualityLevel;
            }

            // Setup SSAA (same as video recording)
            RenderWrapper wrapper = recorder.gameObject.GetComponent<RenderWrapper>();
            if (wrapper != null)
            {
                m_PreCaptureSuperSampling = wrapper.SuperSampling;
                wrapper.SuperSampling = m_SuperSampling;
            }

#if USD_SUPPORTED
            // Handle USD path serialization for camera path recording
            m_UsdPathSerializer = usdPathSerializer;
            m_UsdPath = SaveLoadScript.m_Instance.SceneFile.Valid ?
                Path.ChangeExtension(filePath, "usda") : null;
            m_RecordingStopwatch = new System.Diagnostics.Stopwatch();
            m_RecordingStopwatch.Start();
            if (m_UsdPathSerializer != null && !m_UsdPathSerializer.StartRecording(m_UsdPath))
            {
                Debug.LogWarning("USD Path Serializer failed to start recording");
                UnityEngine.Object.Destroy(m_UsdPathSerializer);
                m_UsdPathSerializer = null;
            }
#endif

            return true;
        }


        static public void StopVideoCapture(bool saveCapture)
        {
            // Debug reset changes to quality settings.
            if (m_DebugVideoCaptureQualityLevel != -1)
            {
                QualityControls.m_Instance.QualityLevel = m_PreCaptureQualityLevel;
            }

            // Handle different capture modes
            if (m_UsingStillFrameFallback && m_ActiveStillFrameExporter != null)
            {
                // Stop still frame sequence capture
                m_ActiveStillFrameExporter.StopCapture(saveCapture);
                
                // Reset render wrapper if it exists
                var wrapper = m_ActiveStillFrameExporter.gameObject.GetComponent<RenderWrapper>();
                if (wrapper != null)
                {
                    wrapper.SuperSampling = m_PreCaptureSuperSampling;
                }
                
                m_ActiveStillFrameExporter = null;
                m_UsingStillFrameFallback = false;
            }
            else if (m_ActiveVideoRecording != null)
            {
                // Stop video capture
                m_ActiveVideoRecording.gameObject.GetComponent<RenderWrapper>().SuperSampling =
                    m_PreCaptureSuperSampling;
                m_ActiveVideoRecording.StopCapture(save: saveCapture);
                m_ActiveVideoRecording = null;
            }

#if USD_SUPPORTED
            if (m_UsdPathSerializer != null)
            {
                bool wasRecording = m_UsdPathSerializer.IsRecording;
                m_UsdPathSerializer.Stop();
                if (wasRecording)
                {
                    m_RecordingStopwatch.Stop();
                    if (!string.IsNullOrEmpty(m_UsdPath))
                    {
                        if (App.UserConfig.Video.SaveCameraPath && saveCapture)
                        {
                            m_UsdPathSerializer.Save();
                            CreateOfflineRenderBatchFile(SaveLoadScript.m_Instance.SceneFile.FullPath, m_UsdPath);
                        }
                    }
                }
            }

            m_UsdPathSerializer = null;
            m_RecordingStopwatch = null;
#endif

            App.Switchboard.TriggerVideoRecordingStopped();
        }

        /// Creates a batch file the user can execute to make a high quality re-render of the video that
        /// has just been recorded.
        static void CreateOfflineRenderBatchFile(string sketchFile, string usdaFile)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string batFile = Path.ChangeExtension(usdaFile, ".HQ_Render.bat");
            var pathSections = Application.dataPath.Split('/').ToArray();
            var exePath = String.Join("/", pathSections.Take(pathSections.Length - 1).ToArray());

            // It would be nice to think of a way to get this to do something sensible in the editor!
            string offlineRenderExePath = Process.GetCurrentProcess().MainModule.FileName;

            string batText = string.Format(
                "@\"{0}/Support/bin/renderVideo.cmd\" ^\n\t\"{1}\" ^\n\t\"{2}\" ^\n\t\"{3}\"",
                exePath, sketchFile, usdaFile, offlineRenderExePath);
            File.WriteAllText(batFile, batText);
#endif
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            string shFile = Path.ChangeExtension(usdaFile, ".HQ_Render.sh");
            var pathSections = Application.dataPath.Split('/').ToArray();
            var exePath = String.Join("/", pathSections.Take(pathSections.Length - 1).ToArray());

            // It would be nice to think of a way to get this to do something sensible in the editor!
            string offlineRenderExePath = Process.GetCurrentProcess().MainModule.FileName;

            string batText = $"\"{exePath}/Support/bin/renderVideo.sh\" \\\n\t\"{sketchFile}\" \\\n\t\"{usdaFile}\" \\\n\t\"{offlineRenderExePath}\"";
            File.WriteAllText(shFile, batText);
#endif

        }
    }

} // namespace TiltBrush
