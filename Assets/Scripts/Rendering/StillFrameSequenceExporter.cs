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

using System.IO;
using UnityEngine;

namespace TiltBrush
{
    public class StillFrameSequenceExporter : MonoBehaviour
    {
        private string m_FilePath;
        private string m_BaseFileName;
        private string m_DirectoryPath;
        private int m_FrameCount;
        private float m_FPS;
        private bool m_IsCapturing;
        private bool m_IsSaving;
        private ScreenshotManager m_ScreenshotManager;
        private float m_LastCaptureTime;
        private float m_FrameInterval;

        public string FilePath => m_FilePath;


        private void Awake()
        {
            m_ScreenshotManager = GetComponent<ScreenshotManager>();
            if (m_ScreenshotManager == null)
            {
                Debug.LogError("StillFrameSequenceExporter requires a ScreenshotManager component");
            }
        }

        public bool StartCapture(string filePath, float fps)
        {
            if (m_IsCapturing)
            {
                return true;
            }

            m_FilePath = filePath;
            m_BaseFileName = Path.GetFileNameWithoutExtension(filePath);
            // Create subfolder for still frames using the same naming as the video file
            string baseDir = Path.GetDirectoryName(filePath);
            m_DirectoryPath = Path.Combine(baseDir, m_BaseFileName + "_frames");
            m_FPS = fps;
            m_FrameCount = 0;
            m_FrameInterval = 1.0f / fps;
            m_LastCaptureTime = 0f;

            // Ensure directory exists
            if (!FileUtils.InitializeDirectoryWithUserError(
                m_DirectoryPath,
                "Failed to start still frame sequence capture"))
            {
                return false;
            }

            m_IsCapturing = true;
            m_IsSaving = false;

            // Create metadata file with frame rate information
            CreateMetadataFile();

            return true;
        }

        public bool ShouldCapture(float currentTime)
        {
            if (!m_IsCapturing)
            {
                return false;
            }

            return currentTime >= m_LastCaptureTime + m_FrameInterval;
        }

        public void CaptureFrame(float currentTime)
        {
            if (!m_IsCapturing || !ShouldCapture(currentTime))
            {
                return;
            }

            m_LastCaptureTime = currentTime;

            string frameFileName = string.Format($"{m_BaseFileName}_frame_{m_FrameCount + 1:D6}.{FilenameExtension}");
            string frameFilePath = Path.Combine(m_DirectoryPath, frameFileName);

            // Create a render texture for the screenshot
            RenderTexture renderTexture = m_ScreenshotManager.CreateTemporaryTargetForSave(
                App.UserConfig.Video.Resolution,
                (App.UserConfig.Video.Resolution * 9) / 16); // 16:9 aspect ratio

            try
            {
                // Render the current frame
                m_ScreenshotManager.RenderToTexture(renderTexture);

                byte[] frameData = ScreenshotManager.SaveToMemory(renderTexture, UsePng); // false = JPG
                File.WriteAllBytes(frameFilePath, frameData);

                m_FrameCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to capture frame {m_FrameCount}: {e.Message}");
            }
            finally
            {
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
        public string FilenameExtension => UsePng ? "png" : "jpg";
        public bool UsePng => App.UserConfig.Video.UsePngForFrameSequence;

        public void StopCapture(bool save)
        {
            if (!m_IsCapturing)
            {
                return;
            }

            m_IsCapturing = false;

            if (save)
            {
                m_IsSaving = true;
                // Update metadata file with final frame count
                UpdateMetadataFile();
                m_IsSaving = false;
            }
            else
            {
                // Delete captured frames if not saving
                DeleteFrameSequence();
            }
        }

        private void CreateMetadataFile()
        {
            string baseDir = Path.GetDirectoryName(m_FilePath);
            string metadataPath = Path.Combine(baseDir, m_BaseFileName + "_sequence.txt");

            try
            {
                using (StreamWriter writer = new StreamWriter(metadataPath))
                {
                    writer.WriteLine("Open Brush Camera Path Frame Sequence");
                    writer.WriteLine($"Base Name: {m_BaseFileName}");
                    writer.WriteLine($"Frame Rate: {m_FPS} fps");
                    writer.WriteLine($"Format: {FilenameExtension}");
                    writer.WriteLine($"Resolution: {App.UserConfig.Video.Resolution}x{(App.UserConfig.Video.Resolution * 9) / 16}");
                    writer.WriteLine($"Start Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine("Status: Recording");
                    writer.WriteLine("");
                    writer.WriteLine("To convert to video, use a tool like ffmpeg:");
                    writer.WriteLine($"ffmpeg -r {m_FPS} -i \"{m_BaseFileName}_frame_%06d.{FilenameExtension}\" -c:v libx264 -pix_fmt yuv420p \"../{m_BaseFileName}.mp4\"");
                    writer.WriteLine("");
                    writer.WriteLine("(Run this command from inside the frames folder, or adjust paths accordingly)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to create metadata file: {e.Message}");
            }
        }


        private void UpdateMetadataFile()
        {
            string baseDir = Path.GetDirectoryName(m_FilePath);
            string metadataPath = Path.Combine(baseDir, m_BaseFileName + "_sequence.txt");
            try
            {
                string content = File.ReadAllText(metadataPath);
                content = content.Replace("Status: Recording", $"Status: Complete ({m_FrameCount} frames)");
                content = content.Replace("Start Time:", $"End Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\nStart Time:");
                File.WriteAllText(metadataPath, content);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to update metadata file: {e.Message}");
            }
        }

        private void DeleteFrameSequence()
        {
            try
            {
                // Delete all frame files
                for (int i = 1; i <= m_FrameCount; i++)
                {
                    string frameFileName = string.Format($"{m_BaseFileName}_frame_{i:D6}.{FilenameExtension}");
                    string frameFilePath = Path.Combine(m_DirectoryPath, frameFileName);
                    if (File.Exists(frameFilePath))
                    {
                        File.Delete(frameFilePath);
                    }
                }

                // Delete metadata file
                string baseDir = Path.GetDirectoryName(m_FilePath);
                string metadataPath = Path.Combine(baseDir, m_BaseFileName + "_sequence.txt");
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to delete frame sequence: {e.Message}");
            }
        }
    }
}