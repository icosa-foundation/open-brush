// Copyright 2025 The Open Brush Authors
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

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace TiltBrush
{
    /// <summary>
    /// Editor utility for extracting audio from VR JPEG files
    /// </summary>
    public class VrJpegAudioExtractor : EditorWindow
    {
        private string sourceDirectory = "";
        private bool recursive = false;
        private int filesProcessed = 0;
        private int audioFilesExtracted = 0;
        private List<string> extractedFiles = new List<string>();

        [MenuItem("Tools/VR JPEG/Extract Audio from VR JPEGs")]
        public static void ShowWindow()
        {
            var window = GetWindow<VrJpegAudioExtractor>("VR JPEG Audio Extractor");
            window.minSize = new Vector2(400, 300);
        }

        void OnGUI()
        {
            GUILayout.Label("VR JPEG Audio Extraction Tool", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This tool extracts spatial audio (MP4/AAC) embedded in Google Cardboard Camera " +
                "VR JPEG files. Audio files will be saved in the same directory as the source images.",
                MessageType.Info);

            GUILayout.Space(10);

            // Directory selection
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source Directory:", GUILayout.Width(120));
            sourceDirectory = EditorGUILayout.TextField(sourceDirectory);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select VR JPEG Directory", sourceDirectory, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    sourceDirectory = selected;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Options
            recursive = EditorGUILayout.Toggle("Search Subdirectories", recursive);

            GUILayout.Space(10);

            // Extract button
            GUI.enabled = !string.IsNullOrEmpty(sourceDirectory) && Directory.Exists(sourceDirectory);
            if (GUILayout.Button("Extract Audio from All VR JPEGs", GUILayout.Height(30)))
            {
                ExtractAudioFromDirectory();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            // Results
            if (filesProcessed > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Processed {filesProcessed} VR JPEG file(s)\n" +
                    $"Extracted {audioFilesExtracted} audio file(s)",
                    audioFilesExtracted > 0 ? MessageType.Info : MessageType.Warning);

                if (extractedFiles.Count > 0)
                {
                    GUILayout.Label("Extracted Audio Files:", EditorStyles.boldLabel);
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    foreach (var file in extractedFiles)
                    {
                        GUILayout.Label(Path.GetFileName(file), EditorStyles.miniLabel);
                    }
                    GUILayout.EndVertical();
                }
            }
        }

        private void ExtractAudioFromDirectory()
        {
            filesProcessed = 0;
            audioFilesExtracted = 0;
            extractedFiles.Clear();

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // Find all potential VR JPEG files
            var jpegFiles = new List<string>();
            jpegFiles.AddRange(Directory.GetFiles(sourceDirectory, "*.vr.jpg", searchOption));
            jpegFiles.AddRange(Directory.GetFiles(sourceDirectory, "*.vr.jpeg", searchOption));

            // Also check regular JPEGs that might be VR JPEGs without .vr prefix
            foreach (var file in Directory.GetFiles(sourceDirectory, "*.jpg", searchOption))
            {
                if (!file.EndsWith(".vr.jpg", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (VrJpegMetadata.IsVrJpeg(file))
                    {
                        jpegFiles.Add(file);
                    }
                }
            }

            if (jpegFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("No VR JPEGs Found",
                    $"No VR JPEG files found in:\n{sourceDirectory}",
                    "OK");
                return;
            }

            // Process files with progress bar
            for (int i = 0; i < jpegFiles.Count; i++)
            {
                string file = jpegFiles[i];
                float progress = (float)i / jpegFiles.Count;

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Extracting VR JPEG Audio",
                    $"Processing: {Path.GetFileName(file)}",
                    progress))
                {
                    break;
                }

                try
                {
                    string audioPath = VrJpegUtils.ExtractAndSaveAudio(file);
                    filesProcessed++;

                    if (audioPath != null)
                    {
                        audioFilesExtracted++;
                        extractedFiles.Add(audioPath);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error processing {file}: {e.Message}");
                }
            }

            EditorUtility.ClearProgressBar();

            // Show results
            if (audioFilesExtracted > 0)
            {
                EditorUtility.DisplayDialog("Extraction Complete",
                    $"Successfully extracted audio from {audioFilesExtracted} of {filesProcessed} VR JPEG file(s).\n\n" +
                    $"Audio files saved in the same directories as source images.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Audio Found",
                    $"Processed {filesProcessed} VR JPEG file(s), but none contained embedded audio.",
                    "OK");
            }

            Repaint();
        }
    }
}
#endif
