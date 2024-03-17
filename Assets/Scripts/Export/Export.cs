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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityGLTF;

namespace TiltBrush
{

    public static class Export
    {
        const string kExportDocumentationUrl = "https://docs.google.com/document/d/11ZsHozYn9FnWG7y3s3WAyKIACfbfwb4PbaS8cZ_xjvo#heading=h.im5f33smiavy";
#if UNITY_ANDROID || UNITY_IOS
  const string kExportReadmeName = "README.txt";
  const string kExportReadmeBody = "Please see " + kExportDocumentationUrl;
#else
        const string kExportReadmeName = "README.url";
        const string kExportReadmeBody = @"[InternetShortcut]
URL=" + kExportDocumentationUrl;
#endif
        const string kExportSuccess = "EXPORT_SUCCESS";

        // Returns a writable name for the export file, creating any directories as necessary;
        // or null on failure.
        private static string MakeExportPath(string parent, string basename, string ext)
        {
            string child = FileUtils.GenerateNonexistentFilename(parent, basename: ext, extension: "");
            if (!FileUtils.InitializeDirectoryWithUserError(
                    child, "Failed to create export directory for " + ext))
            {
                return null;
            }

            return Path.Combine(child, string.Format("{0}.{1}", basename, ext));
        }

        // A helper for managing our progress bar
        class Progress
        {
            private Dictionary<string, float> m_workAmount = new Dictionary<string, float>();
            private float m_totalWork = 0.001f; // avoids divide-by-zero
            private float m_completedWork = 0;

            public Progress()
            {
                OverlayManager.m_Instance.UpdateProgress(0);
            }

            // If you call SetWork with a non-zero value, you shoud also eventually call CompleteWork
            public void SetWork(string name, float effort = 1)
            {
                if (!m_workAmount.ContainsKey(name))
                {
                    m_workAmount[name] = effort;
                    m_totalWork += effort;
                }
                else
                {
                    Debug.LogErrorFormat("Added {0} twice", name);
                }
            }

            // It's ok to call CompleteWork with a name you haven't yet set; it's a no-op.
            public void CompleteWork(string name)
            {
                float work;
                m_workAmount.TryGetValue(name, out work);
                m_completedWork += work;
                OverlayManager.m_Instance.UpdateProgress(m_completedWork / m_totalWork);
            }
        }

        public class AutoTimer : IDisposable
        {
            private System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            private string m_tag;
            public AutoTimer(string tag)
            {
                m_tag = tag;
                timer.Start();
            }
            public void Dispose()
            {
                timer.Stop();
                Debug.LogFormat("{0} time: {1}s", m_tag, timer.ElapsedMilliseconds / 1000.0f);
            }
        }

        static bool IsExportEnabled(string format)
        {
            // I couldn't figure out how to get the default value from the config file for a dictionary with a getter/setter
            // so I'm handling defaults here.
            var formats = App.UserConfig.Export.Formats;
            if (formats == null)
            {
                formats = new Dictionary<string, bool>
                {
                    { "fbx", true },
                    { "glb", true },
                    { "newglb", false },
                    { "json", false },
                    { "latk", false },
                    { "obj", true },
                    { "stl", false },
                    { "usd", false },
                    { "wrl", false },
                };
            }
            return formats.GetValueOrDefault(format);
        }

        public static void ExportScene()
        {
            var current = SaveLoadScript.m_Instance.SceneFile;
            string safeHumanName = FileUtils.SanitizeFilename(current.HumanName);
            string basename = FileUtils.SanitizeFilename(
                (current.Valid && (safeHumanName != "")) ? safeHumanName : "Untitled");

            string parent = FileUtils.GenerateNonexistentFilename(App.UserExportPath(), basename, "");
            if (!FileUtils.InitializeDirectoryWithUserError(
                    parent, "Failed to create export directory")) return;

            // Set up progress bar.
            var progress = new Progress();
            if (App.PlatformConfig.EnableExportJson && IsExportEnabled("json"))
            {
                progress.SetWork("json");
            }
#if FBX_SUPPORTED
            if (App.PlatformConfig.EnableExportFbx && IsExportEnabled("fbx"))
            {
                progress.SetWork("fbx");
            }
            if (IsExportEnabled("obj"))
            {
                progress.SetWork("obj");
            }
#endif

#if USD_SUPPORTED
            if (App.PlatformConfig.EnableExportUsd && IsExportEnabled("usd"))
            {
                progress.SetWork("usd");
            }
#endif

            if (IsExportEnabled("latk"))
            {
                progress.SetWork("latk");
            }

            if (IsExportEnabled("wrl"))
            {
                progress.SetWork("wrl");
            }

            if (IsExportEnabled("stl"))
            {
                progress.SetWork("stl");
            }

            if (App.PlatformConfig.EnableExportGlb)
            {
                if (IsExportEnabled("glb")) { progress.SetWork("glb"); }
                if (IsExportEnabled("newglb")) { progress.SetWork("newglb"); }
            }

            string filename;

            if (App.PlatformConfig.EnableExportJson && IsExportEnabled("json") &&
                (filename = MakeExportPath(parent, basename, "json")) != null)
            {
                using (var unused = new AutoTimer("raw export"))
                {
                    OverlayManager.m_Instance.UpdateProgress(0.1f);
                    ExportRaw.Export(filename);

                    // Also write the metadata that would normally go in the .tilt file
                    SketchSnapshot.ExportMetadata(filename.Replace(".json", ".metadata.json"));
                }
                progress.CompleteWork("json");
            }

#if FBX_SUPPORTED
            if (App.PlatformConfig.EnableExportFbx && IsExportEnabled("fbx") &&
                (filename = MakeExportPath(parent, basename, "fbx")) != null)
            {
                using (var unused = new AutoTimer("fbx export"))
                {
                    OverlayManager.m_Instance.UpdateProgress(0.3f);
                    ExportFbx.Export(filename,
                        App.UserConfig.Export.ExportBinaryFbx ? ExportFbx.kFbxBinary : ExportFbx.kFbxAscii,
                        App.UserConfig.Export.ExportFbxVersion);
                    OverlayManager.m_Instance.UpdateProgress(0.5f);
                }
                progress.CompleteWork("fbx");
            }

            if (IsExportEnabled("obj") && App.PlatformConfig.EnableExportFbx &&
                (filename = MakeExportPath(parent, basename, "obj")) != null)
            {
                // This has never been tested with the new fbx export style and may not work
                ExportFbx.Export(filename, ExportFbx.kObj);
                progress.CompleteWork("obj");
            }
#endif

#if USD_SUPPORTED
            if (App.PlatformConfig.EnableExportUsd && IsExportEnabled("usd") &&
                (filename = MakeExportPath(parent, basename, "usd")) != null)
            {
                using (var unused = new AutoTimer("usd export"))
                {
                    ExportUsd.ExportPayload(filename);
                }
                progress.CompleteWork("usd");
            }
#endif

            if (IsExportEnabled("latk") &&
                (filename = MakeExportPath(parent, basename, "latk")) != null)
            {
                using (var unused = new AutoTimer("latk export"))
                {
                    ExportLatk.Export(filename);
                }
                progress.CompleteWork("latk");
            }

            if (IsExportEnabled("wrl") &&
                (filename = MakeExportPath(parent, basename, "wrl")) != null)
            {
                ExportVrml.Export(filename);
                progress.CompleteWork("wrl");
            }

            if (IsExportEnabled("stl") &&
                (filename = MakeExportPath(parent, basename, "stl")) != null)
            {
                try
                {
                    ExportStl.Export(filename);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    OutputWindowScript.Error("STL export failed", e.Message);
                }
                progress.CompleteWork("stl");
            }

            if (App.PlatformConfig.EnableExportGlb && IsExportEnabled("glb"))
            {
                string extension = App.Config.m_EnableGlbVersion2 ? "glb" : "glb1";
                int gltfVersion = App.Config.m_EnableGlbVersion2 ? 2 : 1;
                filename = MakeExportPath(parent, basename, extension);
                if (filename != null)
                {
                    using (var unused = new AutoTimer("glb export"))
                    {
                        OverlayManager.m_Instance.UpdateProgress(0.6f);

                        // TBT doesn't need (or want) brush textures in the output because it replaces all
                        // the materials, so it's fine to keep those http:. However, Sketchfab doesn't support
                        // http textures so if uploaded, this glb will have missing textures.
                        var exporter = new ExportGlTF();
                        exporter.ExportBrushStrokes(
                            filename, AxisConvention.kGltf2, binary: true, doExtras: false,
                            includeLocalMediaContent: true,
                            gltfVersion: gltfVersion,
                            selfContained: true
                        );
                    }
                }
                progress.CompleteWork("glb");
            }

            if (App.PlatformConfig.EnableExportGlb && IsExportEnabled("newglb"))
            {
                string extension = "glb";
                using (var unused = new AutoTimer("glb export"))
                {
                    OverlayManager.m_Instance.UpdateProgress(0.7f);
                    var settings = GLTFSettings.GetOrCreateSettings();
                    settings.UseMainCameraVisibility = false;
                    var context = new ExportContext
                    {
                        ExportLayers = LayerMask.GetMask("MainCanvas")
                    };
                    var layers = App.Scene.LayerCanvases.Select(x => x.transform).ToArray();
                    var unityGltfexporter = new GLTFSceneExporter(layers, context);
                    unityGltfexporter.SaveGLB(Path.Combine(parent, $"newglb"), $"{basename}.{extension}");
                }
                progress.CompleteWork("newglb");
            }

            OutputWindowScript.m_Instance.CreateInfoCardAtController(
                InputManager.ControllerName.Brush, basename +
                $" {LocalizationSettings.StringDatabase.GetLocalizedString(kExportSuccess)}");
            ControllerConsoleScript.m_Instance.AddNewLine("Located in " + App.UserExportPath());

            string readmeFilename = Path.Combine(App.UserExportPath(), kExportReadmeName);
            if (!File.Exists(readmeFilename) && !Directory.Exists(readmeFilename))
            {
                File.WriteAllText(readmeFilename, kExportReadmeBody);
            }
        }
    }
} // namespace TiltBrush
