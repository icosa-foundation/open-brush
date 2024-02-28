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
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine.XR;
using Debug = UnityEngine.Debug;

namespace TiltBrush
{
    public class BuildWindow : EditorWindow
    {
        private class HeaderedHorizontalLayout : GUI.Scope
        {
            public HeaderedHorizontalLayout(string header, params GUILayoutOption[] options)
            {
                GUILayout.BeginVertical(new GUIContent(header), EditorStyles.helpBox, options);
                GUILayout.Space(20);
                GUILayout.BeginHorizontal(options);
            }

            protected override void CloseScope()
            {
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        private class HeaderedVerticalLayout : GUI.Scope
        {
            public HeaderedVerticalLayout(string header, params GUILayoutOption[] options)
            {
                GUILayout.BeginVertical(new GUIContent(header), EditorStyles.helpBox, options);
                GUILayout.Space(20);
            }

            protected override void CloseScope()
            {
                GUILayout.EndVertical();
            }
        }

        private class AndroidOperation
        {
            private Future<string[]> m_future;
            private Func<string[], bool> m_successTest;
            private string[] m_arguments;
            private string m_name;

            public string[] Output { get; private set; }
            public bool Succeeded { get; private set; }
            public bool Running
            {
                get => m_future != null;
            }
            public bool HasRun { get; private set; }
            public DateTime FinishTime { get; private set; }
            public string Title { get; set; }
            public bool Enabled { get; set; }
            public bool Verbose { get; set; } = true;

            // Called when a build process is completed, with success/failure as the parameter.
            public event Action<bool> Completed;

            public AndroidOperation(string name, Func<string[], bool> successTest, params string[] arguments)
            {
                m_successTest = successTest;
                m_name = name;
                m_arguments = arguments;
                Enabled = true;
            }

            public void Execute()
            {
                Debug.Assert(m_future == null);
                if (Verbose)
                    Debug.Log("adb " + String.Join(" ", m_arguments));

                m_future = new Future<string[]>(() => RunAdb(m_arguments));
            }

            public void Cancel()
            {
                Succeeded = false;
                Output = new string[] { "Cancelled by the user." };
                FinishTime = DateTime.Now;
                HasRun = true;
                m_future = null;
            }

            public void Update()
            {
                if (m_future != null)
                {
                    try
                    {
                        string[] results;
                        if (m_future.TryGetResult(out results))
                        {
                            Succeeded = m_successTest == null ? false : m_successTest(results);
                            Output = results;
                            FinishTime = DateTime.Now;
                            m_future = null;
                            HasRun = true;
                            if (Completed != null)
                            {
                                Completed(Succeeded);
                                Completed = null;
                            }
                        }
                    }
                    catch (FutureFailed ex)
                    {
                        Output = ex.Message.Split('\n');
                        FinishTime = DateTime.Now;
                        m_future = null;
                        Succeeded = false;
                        HasRun = true;
                    }
                }
            }

            public void OnGUI()
            {
                bool initialGuiEnabled = GUI.enabled;
                HeaderedVerticalLayout layout = null;
                if (Title != null)
                {
                    layout = new HeaderedVerticalLayout(Title);
                }

                using (var bar = new GUILayout.HorizontalScope())
                {
                    string title = string.Format("{0} {1}", m_name, SpinnerString());
                    GUI.enabled = !Running && initialGuiEnabled;
                    if (GUILayout.Button(title))
                    {
                        Execute();
                    }
                    GUI.enabled = Running && initialGuiEnabled;
                    if (GUILayout.Button("Cancel"))
                    {
                        Cancel();
                    }
                    GUI.enabled = initialGuiEnabled;
                }

                string shortName = m_name.Split(' ')[0];
                if (HasRun)
                {
                    GUILayout.Label(
                        string.Format("{0} {1} at {2}",
                            shortName, Succeeded ? "Succeeded" : "Failed", FinishTime));
                }
                else if (Running)
                {
                    GUILayout.Label(string.Format("{0} is running.", shortName));
                }
                else
                {
                    GUILayout.Label(string.Format("{0} has not run yet.", shortName));
                }

                if (Output != null)
                {
                    foreach (string line in Output)
                    {
                        GUILayout.Label(line);
                    }
                }

                if (layout != null)
                {
                    layout.Dispose();
                }
            }

            private string SpinnerString()
            {
                if (!Running)
                {
                    return "";
                }
                var spinner = ".....".ToCharArray();
                spinner[DateTime.Now.Second % 5] = '|';
                return new string(spinner);
            }
        }

        private const double kSecondsBetweenDeviceScan = 3;
        private const string kClearConsoleOnBuild = "BuildWindow.ClearConsoleOnBuild";
        private const string kAutoUploadAfterBuild = "BuildWindow.AutoUploadAfterBuild";
        private const string kAutoRunAfterUpload = "BuildWindow.AutoRunAfterUpload";
        private const string kSuccessfulBuildTime = "BuildWindow.SuccessfulBuildTime";
        private const string kBuildStartTime = "BuildWindow.BuildStartTime";
        private DateTime m_timeOfLastDeviceScan;
        private string[] m_androidDevices = new string[0];
        private string m_selectedAndroid = "";
        private Future<string[]> m_deviceListFuture = null;
        private string m_currentBuildPath = "";
        private DateTime? m_currentBuildTime = null;

        private AndroidOperation m_upload;
        private AndroidOperation m_launch;
        private AndroidOperation m_turnOnAdbDebugging;
        private AndroidOperation m_launchWithProfile;
        private AndroidOperation m_terminate;

        private List<string> m_buildLog = new List<string>();
        private StreamReader m_buildLogReader = null;
        private int? m_buildLogPosition;
        private System.IntPtr m_hwnd;
        private DateTime m_buildCompleteTime;

        private string CurrentBuildStatus => BuildTiltBrush.BuildStatus;

        private static bool AdbExists
        {
            get
            {
                string path = BuildTiltBrush.AdbPath;
                if (!string.IsNullOrEmpty(path))
                {
                    return File.Exists(path);
                }
                return false;
            }
        }

        private bool AndroidConnected
        {
            get => !string.IsNullOrEmpty(m_selectedAndroid) && m_androidDevices.Length > 0;
        }

        private bool ClearConsoleOnBuild
        {
            get => EditorPrefs.GetBool(kClearConsoleOnBuild, false);
            set => EditorPrefs.SetBool(kClearConsoleOnBuild, value);
        }

        private bool UploadAfterBuild
        {
            get => EditorPrefs.GetBool(kAutoUploadAfterBuild, false);
            set => EditorPrefs.SetBool(kAutoUploadAfterBuild, value);
        }

        private bool RunAfterUpload
        {
            get => EditorPrefs.GetBool(kAutoRunAfterUpload, false);
            set => EditorPrefs.SetBool(kAutoRunAfterUpload, value);
        }

        [MenuItem("Open Brush/Build/Build Window", false, 1)]
        public static void CreateWindow()
        {
            BuildWindow window = EditorWindow.GetWindow<BuildWindow>();
            window.Show();
        }

        protected void OnEnable()
        {
            titleContent = new GUIContent("Build Window");
            m_timeOfLastDeviceScan = DateTime.Now;
            OnBuildSettingsChanged();
            m_hwnd = GetActiveWindowHandle();
            BuildTiltBrush.OnBackgroundBuildFinish += OnBuildComplete;
        }

        private void OnDisable()
        {
            BuildTiltBrush.OnBackgroundBuildFinish -= OnBuildComplete;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            MakeBuildConfigGui();

            MakeDeviceGui();

            MakeBuildActionsGui();

            MakeBuildStatusGui();

            EditorGUILayout.EndVertical();
        }

        private void MakeBuildConfigGui()
        {
            GUILayoutOption[] options = new GUILayoutOption[]
            {
                GUILayout.ExpandHeight(true),
            };

            GUILayoutOption[] toggleOpt = new GUILayoutOption[]
            {
                GUILayout.Width(150),
            };

            using (var changeScope = new EditorGUI.ChangeCheckScope())
            {
                using (var setupBar = new GUILayout.HorizontalScope(GUILayout.Height(110)))
                {
                    // Sdk Modes
                    using (var sdkBar = new HeaderedVerticalLayout("XR Plugin", options))
                    {
                        XrSdkMode[] sdks = BuildTiltBrush.SupportedSdkModes();
                        XrSdkMode selectedSdk = BuildTiltBrush.GuiSelectedSdk;
                        foreach (var sdk in sdks)
                        {
                            bool selected = sdk == selectedSdk;
                            bool newSelected = GUILayout.Toggle(selected, sdk.ToString(), toggleOpt);
                            if (selected != newSelected)
                            {
                                BuildTiltBrush.GuiSelectedSdk = sdk;
                            }
                        }
                    }

                    // Platforms
                    using (var platformBar = new HeaderedVerticalLayout("Platform", options))
                    {
                        BuildTarget[] targets = BuildTiltBrush.SupportedBuildTargets();
                        BuildTarget selectedTarget = BuildTiltBrush.GuiSelectedBuildTarget;
                        XrSdkMode selectedSdk = BuildTiltBrush.GuiSelectedSdk;
                        foreach (var target in targets)
                        {
                            GUI.enabled = BuildTiltBrush.BuildTargetSupported(selectedSdk, target);
                            bool selected = target == selectedTarget;
                            bool newSelected = GUILayout.Toggle(selected, target.ToString(), toggleOpt);
                            if (selected != newSelected)
                            {
                                BuildTiltBrush.GuiSelectedBuildTarget = target;
                            }
                        }
                        GUI.enabled = true;
                    }

                    // Runtime
                    using (var runtimeBar = new HeaderedVerticalLayout("Runtime", options))
                    {
                        bool isIl2cpp = BuildTiltBrush.GuiRuntimeIl2cpp;
                        bool newIsMono = false;
                        bool newIsIl2cpp = false;

                        // Mobile requires IL2CPP
                        if (BuildTiltBrush.GuiSelectedBuildTarget == BuildTarget.Android || BuildTiltBrush.GuiSelectedBuildTarget == BuildTarget.iOS)
                        {
                            newIsIl2cpp = true;
                            GUILayout.Toggle(newIsIl2cpp, "IL2CPP", toggleOpt);
                        }
                        else
                        {
                            newIsMono = GUILayout.Toggle(!isIl2cpp, "Mono", toggleOpt);
                            newIsIl2cpp = GUILayout.Toggle(isIl2cpp, "IL2CPP", toggleOpt);
                        }

                        if (isIl2cpp != newIsIl2cpp || isIl2cpp != !newIsMono)
                        {
                            BuildTiltBrush.GuiRuntimeIl2cpp = !isIl2cpp;
                        }
                    }

                    // Options
                    using (var optionsBar = new HeaderedVerticalLayout("Options", options))
                    {
                        BuildTiltBrush.GuiDevelopment =
                            GUILayout.Toggle(BuildTiltBrush.GuiDevelopment, "Development");
                        BuildTiltBrush.GuiAutoProfile =
                            GUILayout.Toggle(BuildTiltBrush.GuiAutoProfile, "Auto Profile");
                    }
                }

                if (changeScope.changed)
                {
                    OnBuildSettingsChanged();
                }
            }
        }

        private void MakeBuildActionsGui()
        {
            using (var buildBar = new HeaderedVerticalLayout("Build Actions"))
            {
                using (var buttonBar = new GUILayout.HorizontalScope())
                {
                    bool build = GUILayout.Button("Build");
                    GUI.enabled = !BuildTiltBrush.DoingBackgroundBuild;
                    bool buildBackground = GUILayout.Button("Background Build");

                    if (GUILayout.Button("Show Build Folder") && !String.IsNullOrEmpty(m_currentBuildPath))
                    {
                        Process.Start(Path.GetDirectoryName(m_currentBuildPath));
                    }

                    GUI.enabled = BuildTiltBrush.DoingBackgroundBuild;
                    if (GUILayout.Button("Cancel"))
                    {
                        BuildTiltBrush.TerminateBackgroundBuild();
                    }
                    GUI.enabled = true;

                    if (build)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            bool oldBackground = BuildTiltBrush.BackgroundBuild;
                            try
                            {
                                BuildTiltBrush.BackgroundBuild = false;
                                ResetBuildLog();
                                BuildStartTime = DateTime.Now;
                                BuildTiltBrush.MenuItem_Build();
                            }
                            finally
                            {
                                BuildTiltBrush.BackgroundBuild = oldBackground;
                            }
                        };
                    }

                    if (buildBackground)
                    {
                        ResetBuildLog();
                        BuildStartTime = DateTime.Now;
                        BuildTiltBrush.DoBackgroundBuild(BuildTiltBrush.GetGuiOptions(), false);
                    }
                }

                using (var optionsBar = new GUILayout.HorizontalScope())
                {
                    ClearConsoleOnBuild = GUILayout.Toggle(ClearConsoleOnBuild, "Clear console on build");
                    UploadAfterBuild = GUILayout.Toggle(UploadAfterBuild, "Upload after Build");
                    RunAfterUpload = GUILayout.Toggle(RunAfterUpload, "Run after Upload");
                }
                GUILayout.Space(12);

                // Android support buttons
                if (BuildTiltBrush.GuiSelectedBuildTarget == BuildTarget.Android && !string.IsNullOrEmpty(BuildTiltBrush.AdbPath))
                {
                    if (AndroidConnected)
                    {
                        using (var buildBar2 = new HeaderedVerticalLayout("Android Actions"))
                        {
                            GUI.enabled = AndroidConnected && !BuildTiltBrush.DoingBackgroundBuild;
                            m_upload.OnGUI();
                            m_launch.OnGUI();
                            m_turnOnAdbDebugging.OnGUI();
                            m_launchWithProfile.OnGUI();
                            m_terminate.OnGUI();
                            GUI.enabled = true;
                        }
                    }
                }

                int start = Mathf.Clamp(m_buildLog.Count - 11, 0, int.MaxValue);
                if (m_buildLogPosition.HasValue)
                {
                    start = m_buildLogPosition.Value;
                }

                if (m_buildLog.Count > 0)
                {
                    int width = (int)(position.width - 35);
                    int end = Mathf.Clamp(start + 10, 0, m_buildLog.Count);
                    using (var horizSection = new GUILayout.HorizontalScope())
                    {
                        using (var vertSection = new GUILayout.VerticalScope(GUILayout.Width(width)))
                        {
                            for (int i = start; i < end; ++i)
                            {
                                GUILayout.Label(m_buildLog[i], GUILayout.Width(width));
                            }
                        }

                        float size = 10f / m_buildLog.Count;
                        float newStart = GUILayout.VerticalScrollbar(start, size, 0, m_buildLog.Count, GUILayout.Height(180));
                        if (newStart >= (m_buildLog.Count - 11f))
                        {
                            m_buildLogPosition = null;
                        }
                        else
                        {
                            m_buildLogPosition = (int)newStart;
                        }
                    }
                }

                if (BuildTiltBrush.DoingBackgroundBuild)
                {
                    Rect r = EditorGUILayout.BeginVertical();
                    float buildSeconds = (float)(DateTime.Now - BuildStartTime).TotalSeconds;
                    float progress = Mathf.Clamp01(buildSeconds / SuccessfulBuildSeconds) * 0.95f;
                    float remaining = SuccessfulBuildSeconds - buildSeconds;
                    string time = "???";
                    if (remaining > 0)
                    {
                        var span = TimeSpan.FromSeconds(remaining);
                        time = string.Format("{0}:{1:00}", (int)span.TotalMinutes, span.Seconds);
                    }
                    EditorGUI.ProgressBar(r, progress, string.Format("Building - {0} remaining.", time));
                    GUILayout.Space(18);
                    EditorGUILayout.EndVertical();
                }
            }
        }

        private void ResetBuildLog()
        {
            if (ClearConsoleOnBuild)
            {
                Debug.ClearDeveloperConsole();
            }

            m_buildLogPosition = null;
            if (m_buildLogReader != null)
            {
                m_buildLogReader.Close();
            }

            m_buildLogReader = null;
            m_buildLog.Clear();
        }

        private void MakeDeviceGui()
        {
            // Show the devices supported by Unity XR.
            using (var unused = new HeaderedVerticalLayout("Supported XR Devices"))
            {
                EditorGUILayout.LabelField("XR Plugins Selected", BuildTiltBrush.GetXrPlugins());

                EditorGUILayout.LabelField(
                    "XR Plugin Devices", string.Join(", ", XRSettings.supportedDevices),
                    EditorStyles.wordWrappedLabel);
            }

            if (BuildTiltBrush.GuiSelectedBuildTarget == BuildTarget.Android)
            {
                using (var droids = new HeaderedVerticalLayout("Android devices"))
                {
                    foreach (string device in m_androidDevices)
                    {
                        bool selected = device == m_selectedAndroid;
                        bool newSelected = GUILayout.Toggle(selected, device);
                        if (selected != newSelected)
                        {
                            m_selectedAndroid = device;
                            MakeBuildActionsGui();
                            OnBuildSettingsChanged();
                        }
                    }
                }
            }

        }

        private void MakeBuildStatusGui()
        {
            using (var builds = new HeaderedVerticalLayout("Build Status"))
            {
                EditorGUILayout.LabelField("Status", CurrentBuildStatus);

                EditorGUILayout.LabelField("Build Output", m_currentBuildPath);

                // Android specific information
                if (BuildTiltBrush.GuiSelectedBuildTarget == BuildTarget.Android)
                {
                    EditorGUILayout.LabelField("Adb Path", BuildTiltBrush.AdbPath ?? "Unset");
                    if (!AdbExists)
                    {
                        EditorGUILayout.LabelField("Adb status", "ADB not found in expected path.");
                    }
                }

                if (m_currentBuildTime.HasValue)
                {
                    TimeSpan age = DateTime.Now - m_currentBuildTime.Value;
                    EditorGUILayout.LabelField("Creation Time", m_currentBuildTime.Value.ToString());
                    StringBuilder textAge = new StringBuilder();
                    if (age.Days > 0) { textAge.AppendFormat("{0}d ", age.Days); }
                    if (age.Hours > 0) { textAge.AppendFormat("{0}h ", age.Hours); }
                    if (age.Minutes > 0) { textAge.AppendFormat("{0}m ", age.Minutes); }
                    textAge.AppendFormat("{0}s", age.Seconds);
                    EditorGUILayout.LabelField("Age", textAge.ToString());
                }
                else
                {
                    GUILayout.Label("Not built yet.");
                }
            }
        }

        private void ScanAndroidDevices()
        {
            if (m_deviceListFuture == null)
            {
                if ((DateTime.Now - m_timeOfLastDeviceScan).TotalSeconds > kSecondsBetweenDeviceScan &&
                    !EditorApplication.isPlaying)
                {
                    m_deviceListFuture = new Future<string[]>(() => RunAdb("devices"));
                }
            }
            else
            {
                string[] results;
                if (m_deviceListFuture.TryGetResult(out results))
                {
                    m_androidDevices = results.Skip(1).Where(x => !string.IsNullOrEmpty(x.Trim()))
                        .Select(x => x.Split(' ', '\t')[0]).ToArray();
                    if (m_androidDevices.Length != 0 && !m_androidDevices.Contains(m_selectedAndroid))
                    {
                        m_selectedAndroid = m_androidDevices[0];
                    }
                    m_deviceListFuture = null;
                    Repaint();
                    m_timeOfLastDeviceScan = DateTime.Now;
                }
            }
        }

        private void OnBuildSettingsChanged()
        {
            // TODO: check correct plugins for XR mode

            m_currentBuildPath = BuildTiltBrush.GetAppPathForGuiBuild();
            if (File.Exists(m_currentBuildPath))
            {
                m_currentBuildTime = File.GetLastWriteTime(m_currentBuildPath);
            }
            else
            {
                m_currentBuildTime = null;
            }

            string exeName = Path.GetFileName(m_currentBuildPath);
            string exeTitle = Path.GetFileNameWithoutExtension(exeName);

            // Note, we add "unityeditor" to the package name - Unity appends this.
            string packageName = exeTitle + "unityeditor";

            if (m_upload != null)
            {
                m_upload.Cancel();
            }

            m_upload = new AndroidOperation(
                string.Format("Upload {0} to {1}", exeName, m_selectedAndroid),
                (results) => results.Any(x => x.StartsWith("Success")),
                // adb args:
                "-s", m_selectedAndroid, "install", "-r", "-g", m_currentBuildPath
            );

            if (m_launch != null) { m_launch.Cancel(); }
            m_launch = new AndroidOperation(
                string.Format("Launch {0}", exeName),
                (results) => results.Any(x => x.Contains("Starting: Intent")),
                // adb args:
                "-s", m_selectedAndroid,
                //  -S will force stop any previous instance.
                "shell", "am", "start", "-S", packageName + "/com.unity3d.player.UnityPlayerActivity"
            );

            if (m_terminate != null) { m_terminate.Cancel(); }
            m_terminate = new AndroidOperation(
                string.Format("Terminate {0}", exeName),
                (results) => true,
                // adb args:
                "-s", m_selectedAndroid,
                "shell", "am", "force-stop", packageName
            );

            if (m_turnOnAdbDebugging != null) { m_turnOnAdbDebugging.Cancel(); }
            m_turnOnAdbDebugging = new AndroidOperation(
                "Turn on adb debugging/profiling",
                (results) => true,
                // adb args:
                "-s", m_selectedAndroid,
                "forward", "tcp:34999", "localabstract:Unity-" + packageName
            );

            if (m_launchWithProfile != null) { m_launchWithProfile.Cancel(); }
            m_launchWithProfile = new AndroidOperation(
                string.Format("Launch with deep profile {0}", exeName),
                (results) => results.Any(x => x.Contains("Starting: Intent")),
                // adb args:
                "-s", m_selectedAndroid,
                "shell", "am", "start", packageName + "/com.unity3d.player.UnityPlayerActivity",
                "-e", "unity", "-deepprofiling"
            );
        }

        public static string[] RunAdb(params string[] arguments)
        {
            if (!AdbExists)
                return new string[] { "" };

            var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo(BuildTiltBrush.AdbPath, String.Join(" ", arguments));
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            process.WaitForExit();
            return process.StandardOutput.ReadToEnd().Split('\n').Concat(process.StandardError.ReadToEnd().Split('\n')).ToArray();
        }

        private void Update()
        {
            if (BuildTiltBrush.GuiSelectedBuildTarget == BuildTarget.Android)
            {
                ScanAndroidDevices();
                m_upload.Update();
                m_launch.Update();
                m_turnOnAdbDebugging.Update();
                m_launchWithProfile.Update();
                m_terminate.Update();
            }

            UpdateBuildLog();
        }

        private void UpdateBuildLog()
        {
            if (!BuildTiltBrush.DoingBackgroundBuild)
            {
                return;
            }

            if (m_buildLogReader == null)
            {
                if (!File.Exists(BuildTiltBrush.BackgroundBuildLogPath))
                {
                    return;
                }

                var fileStream = new FileStream(BuildTiltBrush.BackgroundBuildLogPath, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite);
                if (fileStream == null)
                {
                    return;
                }
                m_buildLogReader = new StreamReader(fileStream);
            }

            string line = null;
            while ((line = m_buildLogReader.ReadLine()) != null)
            {
                m_buildLog.Add(line);
            }
        }

        private void OnBuildComplete(int exitCode)
        {
            m_buildCompleteTime = DateTime.Now;
            OnBuildSettingsChanged();
            Repaint();
            if (UploadAfterBuild && exitCode == 0)
            {
                m_upload.Completed += OnUploadComplete;
                EditorApplication.delayCall += m_upload.Execute;
            }
            else
            {
                EditorApplication.delayCall += () => { NotifyBuildFinished("Build", exitCode); };
            }
        }

        private void NotifyBuildFinished(string operation, int exitCode)
        {
            NotifyFlash(10, FlashFlags.Tray);
            if (exitCode == 0)
            {
                EditorUtility.DisplayDialog(operation, operation + " Complete", "OK");
                SuccessfulBuildSeconds = (float)(m_buildCompleteTime - BuildStartTime).TotalSeconds;
            }
            else
            {
                EditorUtility.DisplayDialog(operation,
                    string.Format("{0} failed with exit code {1}", operation, exitCode), "OK");
            }
            NotifyFlash(1, FlashFlags.Stop);
        }

        private void OnUploadComplete(bool success)
        {
            if (success && RunAfterUpload)
            {
                m_launch.Completed += OnRunComplete;
                m_launch.Execute();
            }
            else
            {
                EditorApplication.delayCall += () => { NotifyBuildFinished("Build and Upload", 0); };
            }
        }

        private void OnRunComplete(bool success)
        {
            EditorApplication.delayCall += () => { NotifyBuildFinished("Build, Upload, and Launch", 0); };
        }

        private float SuccessfulBuildSeconds
        {
            get => EditorPrefs.GetFloat(kSuccessfulBuildTime, 300);
            set => EditorPrefs.SetFloat(kSuccessfulBuildTime, Mathf.Lerp(value, SuccessfulBuildSeconds, 0.8f));
        }

        private DateTime BuildStartTime
        {
            get
            {
                string datetimeString = EditorPrefs.GetString(kBuildStartTime, null);
                if (string.IsNullOrEmpty(datetimeString))
                {
                    return DateTime.Now - TimeSpan.FromMinutes(1);
                }
                return DateTime.Parse(datetimeString);
            }
            set => EditorPrefs.SetString(kBuildStartTime, value.ToString());
        }

#if UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern Int32 FlashWindowEx(ref FLASHWINFO pwfi);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern System.IntPtr GetActiveWindow();

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public Int32 dwFlags;
            public UInt32 uCount;
            public Int32 dwTimeout;
        }
#endif

        public enum FlashFlags
        {
            Stop = 0,         // Stop flashing
            Titlebar = 1,     // Flash the window title
            Tray = 2,         // Flash the taskbar button
            Continuously = 4, // Flash continuously
            NoForeground = 8, // Stop flashing if window comes to the foreground
        }

        private IntPtr GetActiveWindowHandle()
        {
#if UNITY_EDITOR_WIN
            return GetActiveWindow();
#else
            return IntPtr.Zero;
#endif
        }


        private void NotifyFlash(uint numFlashes, params FlashFlags[] flags)
        {
#if UNITY_EDITOR_WIN
            int flagInt = flags.Cast<int>().Sum();
            FLASHWINFO fw = new FLASHWINFO();
            fw.cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(FLASHWINFO)));
            fw.hwnd = m_hwnd;
            fw.dwFlags = flagInt;
            fw.uCount = numFlashes;
            FlashWindowEx(ref fw);
#endif
        }
    }
}
