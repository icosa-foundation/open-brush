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

// Pros and cons for MonoBehaviour (component) and ScriptableObject (asset):
//
// Component:
// - PRO: Runtime changes take place immediately
// - CON: Runtime changes do not persist after the game exits
// - PRO: Can reference objects in the scene
// - CON: Changes go into Main.unity; harder to review
//
// Asset:
// - CON: Runtime changes are visible only after the .asset hot-reloads
// - PRO: Runtime changes persist after the game exits
// - CON: Cannot reference objects in the scene; only prefabs and other assets
// - PRO: Changes go into their own .asset; easier to review

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

#if OCULUS_SUPPORTED
using Unity.XR.Oculus;
#endif

namespace TiltBrush
{
    public enum XrSdkMode
    {
        Monoscopic = -1,
        OpenXR = 0,
        Oculus,
        Wave,
        Pico,
        Zapbox,
    }

    // The sdk mode indicates which SDK that we're using to drive the display.
    //  - These names are used in our analytics, so they must be protected from obfuscation.
    //    Do not change the names of any of them, unless they've never been released.
    [Serializable]
    public enum SdkMode
    {
        Unset = -1,
        UnityXR,
        Monoscopic,
        Ods,    // Video rendering
    }

    /// These are not used in analytics. They indicate the type of tool tip description that will appear
    /// on a UI component.
    public enum DescriptionType
    {
        None = -1,
        Button = 0,
        Slider,
        PreviewCube,
    }

    /// Script Ordering:
    /// - does not need to come after anything
    /// - must come before everything that uses App.Config (ie, all scripts)
    ///
    /// Used to store global configuration data and constants.
    /// For per-platform data, see PlatformConfig.cs
    /// Despite being public, all this data should be considered read-only
    ///
    public class Config : MonoBehaviour
    {
        // When set, ModelWidget creation waits for Poly models to be loaded into memory.
        // When not set, ModelWidgets may be created with "dummy" Models which are automatically
        // replaced with the real Model once it's loaded.
        public readonly bool kModelWidgetsWaitForLoad = true;

        [System.Serializable]
        public class BrushReplacement
        {
            [BrushDescriptor.AsStringGuid] public string FromGuid;
            [BrushDescriptor.AsStringGuid] public string ToGuid;
        }

        private class UserConfigChange
        {
            public FieldInfo section;
            public MemberInfo setting;
            public object value;
        }

        // This intentionally breaks the naming convention of m_Instance because it is only intended to be
        // used by App.
        static public Config m_SingletonState;

        [Header("Startup")]
        public string m_FakeCommandLineArgsInEditor;

        [Header("Overwritten by build process")]
        [SerializeField] private PlatformConfig m_PlatformConfig;

        // The sdk mode indicates which SDK that we're using to drive the display.
        public SdkMode m_SdkMode;

        // Stores the value of IsExperimental at startup time
        [NonSerialized] public bool m_WasExperimentalAtStartup;

        // Whether or not to just do an automatic profile and then exit.
        public bool m_AutoProfile;
        // How long to wait before starting to profile.
        public float m_AutoProfileWaitTime = 10f;

        [Header("App")]
        public SecretsConfig Secrets;
        public string[] m_SketchFiles = new string[0];
        [NonSerialized] public bool m_QuickLoad = true;

        public SecretsConfig.ServiceAuthData GoogleSecrets => Secrets[SecretsConfig.Service.Google];
        public SecretsConfig.ServiceAuthData SketchfabSecrets => Secrets[SecretsConfig.Service.Sketchfab];
        public SecretsConfig.ServiceAuthData OculusSecrets => Secrets[SecretsConfig.Service.Oculus];
        public SecretsConfig.ServiceAuthData OculusMobileSecrets => Secrets[SecretsConfig.Service.OculusMobile];
        public SecretsConfig.ServiceAuthData PimaxSecrets => Secrets[SecretsConfig.Service.Pimax];
        public SecretsConfig.ServiceAuthData PhotonFusionSecrets => Secrets[SecretsConfig.Service.PhotonFusion];

        public bool DisableAccountLogins;

        /// Return a value kinda sorta half-way between "building for Android" and "running on Android"
        /// In order of increasing strictness, here are the in-Editor semantics of various methods
        /// of querying the platform. All of these methods return true when running on-device.
        /// Note that each level is a strict subset of the level(s) above:
        ///
        /// 1. true if build target is Android
        ///      #if UNITY_ANDROID / #endif
        ///      EditorUserBuildSetings.activeBuildTarget == BuildTarget.Android
        /// 2. true if build target is Android AND if SpoofMobileHardware.MobileHardware is set
        ///      Config.IsMobileHardware
        /// 3. never true in Editor; only true on-device
        ///      Application.platform == RuntimePlatform.Android
        ///      App.Config.IsMobileHardware && !SpoofMobileHardware.MobileHardware:
        ///
        /// TODO: Can we get away with just #1 and #3, and remove #2? That would let us remove
        /// SpoofMobileHardware.MobileHardware too.
        public bool IsMobileHardware
        {
            // Only sadness will ensue if the user tries to set Override.MobileHardware=true
            // but their editor platform is still set to Windows.
#if UNITY_EDITOR && UNITY_ANDROID
            get => Application.platform == RuntimePlatform.Android || SpoofMobileHardware.MobileHardware;
#elif UNITY_EDITOR && UNITY_IOS
            get => Application.platform == RuntimePlatform.IPhonePlayer || SpoofMobileHardware.MobileHardware;
#else
            get => Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
#endif
        }

        [Header("Ods")]
        public int m_OdsNumFrames = 0;
        public float m_OdsFps = 30;
        public string m_OdsOutputPath = "";
        public string m_OdsOutputPrefix = "";
        [NonSerialized] public bool m_OdsPreview = false;
        [NonSerialized] public bool m_OdsCollapseIpd = true;
        [NonSerialized] public float m_OdsTurnTableDegrees = 0.0f;

        [Header("Versioning")]
        public string m_VersionNumber; // eg "17.0b", "18.3"
        public string m_BuildStamp;    // eg "f73783b61", "f73783b61-exp", "menuitem"

        [Header("Misc")]
        public bool m_UseBatchedBrushes;
        // Delete Batch's GeometryPool after about a second.
        public bool m_EnableBatchMemoryOptimization;
        public string m_MediaLibraryReadme;
        public DropperTool m_Dropper;
        public bool m_AxisManipulationIsResize;
        public GameObject m_LabsButtonOverlayPrefab;
        public bool m_GpuIntersectionEnabled = true;
        public bool m_AutosaveRestoreEnabled = false;
        public bool m_AllowWidgetPinning;
        public bool m_DebugWebRequest;
        public bool m_ToggleProfileOnAppButton = false;

        [Header("Global Shaders")]
        public Shader m_BlitToComputeShader;

        [Header("Upload and Export")]
        // Some brushes put a birth time in the vertex attributes; because we export
        // this data (we really shouldn't) it's helpful to disable it when one needs
        // deterministic export.
        public bool m_ForceDeterministicBirthTimeForExport;
        [NonSerialized] public List<string> m_FilePatternsToExport;
        [NonSerialized] public string m_ExportPath;
        [NonSerialized] public string m_VideoPathToRender;
        // TODO: m22 ripcord; remove for m23
        public bool m_EnableReferenceModelExport;
        // TODO: m22 ripcord; remove for m23
        public bool m_EnableGlbVersion2;
        [Tooltip("Causes the temporary Upload directory to be kept around (Editor only)")]
        public bool m_DebugUpload;
        public TiltBrushToolkit.TbtSettings m_TbtSettings;

        [Header("Loading")]
        public bool m_ReplaceBrushesOnLoad;
        [SerializeField] List<BrushReplacement> m_BrushReplacementMap;
        public string m_IntroSketchUsdFilename;
        [Range(0.001f, 4)]
        public float m_IntroSketchSpeed = 1.0f;
        public bool m_IntroLooped = false;

        [Header("Description Prefabs")]
        [SerializeField] GameObject m_ButtonDescriptionOneLinePrefab;
        [SerializeField] GameObject m_ButtonDescriptionTwoLinesPrefab;
        [SerializeField] GameObject m_ButtonDescriptionThreeLinesPrefab;
        [SerializeField] GameObject m_SliderDescriptionOneLinePrefab;
        [SerializeField] GameObject m_SliderDescriptionTwoLinesPrefab;
        [SerializeField] GameObject m_PreviewCubeDescriptionOneLinePrefab;
        [SerializeField] GameObject m_PreviewCubeDescriptionTwoLinesPrefab;

        public GameObject CreateDescriptionFor(DescriptionType type, int numberOfLines)
        {
            switch (type)
            {
                case DescriptionType.None:
                    return null;
                case DescriptionType.Button:
                    switch (numberOfLines)
                    {
                        case 1:
                            return Instantiate(m_ButtonDescriptionOneLinePrefab);
                        case 2:
                            return Instantiate(m_ButtonDescriptionTwoLinesPrefab);
                        case 3:
                            return Instantiate(m_ButtonDescriptionThreeLinesPrefab);
                        default:
                            throw new Exception($"{type} description does not have a ${numberOfLines} line variant");
                    }
                case DescriptionType.Slider:
                    switch (numberOfLines)
                    {
                        case 1:
                            return Instantiate(m_SliderDescriptionOneLinePrefab);
                        case 2:
                            return Instantiate(m_SliderDescriptionTwoLinesPrefab);
                        default:
                            throw new Exception($"{type} description does not have a ${numberOfLines} line variant");
                    }
                case DescriptionType.PreviewCube:
                    switch (numberOfLines)
                    {
                        case 1:
                            return Instantiate(m_PreviewCubeDescriptionOneLinePrefab);
                        case 2:
                            return Instantiate(m_PreviewCubeDescriptionTwoLinesPrefab);
                        default:
                            throw new Exception($"{type} description does not have a ${numberOfLines} line variant");
                    }
                default:
                    throw new Exception($"Unknown description type: {type}");
            }
        }

        public bool OfflineRender
        {
            get => !string.IsNullOrEmpty(m_VideoPathToRender) && m_SdkMode != SdkMode.Ods;
        }

        public PlatformConfig PlatformConfig
        {
            get
            {
#if UNITY_EDITOR
                // Ignore m_PlatformConfig: we want whatever a build would give us.
                // Should we cache this value?
                var ret = EditTimeAssetReferences.Instance.GetConfigForBuildTarget(
                    UnityEditor.EditorUserBuildSettings.activeBuildTarget);
                // This is just to keep the compiler from spewing a warning about this field
                if (ret == null) { ret = m_PlatformConfig; }
                return ret;
#else
                return m_PlatformConfig;
#endif
            }
        }

        // ------------------------------------------------------------
        // Private data
        // ------------------------------------------------------------
        private Dictionary<Guid, Guid> m_BrushReplacement = null;
        private List<UserConfigChange> m_UserConfigChanges = new List<UserConfigChange>();

        // ------------------------------------------------------------
        // Yucky externals
        // ------------------------------------------------------------

        public Guid GetReplacementBrush(Guid original)
        {
            Guid replacement;
            if (m_BrushReplacement.TryGetValue(original, out replacement))
            {
                return replacement;
            }
            return original;
        }

        void ParseArgs(string[] args)
        {
            List<string> files = new List<string>();

            bool isInBatchMode = false;

            // If someone entered a sketch via the editor, we need to preserve that here.
            foreach (var s in m_SketchFiles)
            {
                files.Add(s);
            }

            // Process all args.
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 0)
                {
                    // Skip "TiltBrush.exe"
                    continue;

                }
                else if (args[i] == "--captureOds")
                {
                    m_SdkMode = SdkMode.Ods;
                    UnityEngine.XR.XRSettings.enabled = false;
                    Debug.Log("CaptureODS: Enable ");

                }
                else if (args[i] == "--noQuickLoad")
                {
                    m_QuickLoad = false;
                    Debug.Log("QuickLoad: Disable ");

                }
                else if (args[i].EndsWith(SaveLoadScript.TILT_SUFFIX))
                {
                    files.Add(args[i]);
                    Debug.LogFormat("Sketch: {0}", args[i]);

                }
                else if (args[i] == "--outputPath")
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("Invalid outputPath argument, path expected");
                    }
                    m_OdsOutputPath = args[++i];
                    Debug.LogFormat("ODS Output Path: {0}", args[i]);

                }
                else if (args[i] == "--outputPrefix")
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("Invalid prefix argument, name expected");
                    }
                    m_OdsOutputPrefix = args[i];
                    Debug.LogFormat("ODS Output Prefix: {0}", args[i]);

                }
                else if (args[i] == "--preview")
                {
                    // 1k is ugly enough to stop people from shipping videos with this quality, but clear enough
                    // to see a preview. To some people, 2k may look good enough to ship, but is horribly
                    // aliased in the HMD, so let's not give an option for that.
                    m_OdsPreview = true;
                    Debug.LogFormat("Enable: ODS Preview");

                }
                else if (args[i] == "--noCorrection")
                {
                    m_OdsCollapseIpd = false;
                    Debug.LogFormat("Enable: NO Correction");

                }
                else if (args[i] == "--turnTable")
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("Invalid turnTable rotation argument, N degrees expected");
                    }
                    if (!float.TryParse(args[++i], out m_OdsTurnTableDegrees))
                    {
                        throw new ApplicationException("Invalid turnTable rotation argument, " +
                            "angle in degrees expected");
                    }
                    Debug.LogFormat("Enable: Turntable {0}", m_OdsTurnTableDegrees);

                }
                else if (args[i] == "--numFrames")
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("Invalid numFrames argument, N frames expected");
                    }
                    if (!int.TryParse(args[++i], out m_OdsNumFrames))
                    {
                        throw new ApplicationException("Invalid numFrames argument, " +
                            "integer frame count expected");
                    }
                    Debug.LogFormat("ODS Num Frames: {0}", m_OdsNumFrames);

                }
                else if (args[i] == "--fps")
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("Invalid fps argument, value expected");
                    }
                    if (!float.TryParse(args[++i], out m_OdsFps))
                    {
                        throw new ApplicationException("Invalid fps argument, " +
                            "floating point frame rate expected");
                    }
                    Debug.LogFormat("ODS FPS: {0}", m_OdsFps);

                }
                else if (args[i] == "--export")
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("Invalid export argument, filename expected.");
                    }
                    if (m_FilePatternsToExport == null)
                    {
                        m_FilePatternsToExport = new List<string>();
                    }
                    while (((i + 1) < args.Length) && !args[i + 1].StartsWith("-"))
                    {
                        m_FilePatternsToExport.Add(args[++i]);
                    }
                }
                else if (args[i] == "--exportPath")
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("Invalid exportPath argument, path expected.");
                    }
                    m_ExportPath = args[i + 1];
                }
                else if (args[i] == "-batchmode" || args[i] == "-nographics")
                {
                    // If we're in batch mode, do monoscopic.
                    isInBatchMode = true;
                }
                else if (args[i] == "--renderCameraPath")
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("Invalid renderCameraPath argument, tbpath expected.");
                    }
                    m_VideoPathToRender = args[++i];
                    m_SdkMode = SdkMode.Monoscopic;
                    UnityEngine.XR.XRSettings.enabled = false;
                }
                else if (args[i].Contains("."))
                {
                    if (i == args.Length - 1)
                    {
                        throw new ApplicationException("User Config Settings require an argument.");
                    }
                    ParseUserSetting(args[i], args[++i]);
                }
                else
                {
                    Debug.LogFormat("Unknown argument: {0}", args[i]);
                }
            }

            if (isInBatchMode)
            {
                if (OfflineRender || m_SdkMode == SdkMode.Ods)
                {
                    throw new ApplicationException("Video rendering not supported in batch mode.");
                }
                m_SdkMode = SdkMode.Monoscopic;
            }

            m_SketchFiles = files.ToArray();
        }

        // ------------------------------------------------------------
        // Yucky internals
        // ------------------------------------------------------------

        public static bool IsExperimental
        {
            get => PlayerPrefs.HasKey("ExperimentalMode") && PlayerPrefs.GetInt("ExperimentalMode") == 1;
        }

        public bool GeometryShaderSuppported
        {
            get
            {
#if OCULUS_SUPPORTED
                SystemHeadset headset = Unity.XR.Oculus.Utils.GetSystemHeadsetType();
                return headset != SystemHeadset.Oculus_Quest;
#endif // OCULUS_SUPPORTED
#if ZAPBOX_SUPPORTED
                return false;
#endif
                return SystemInfo.supportsGeometryShaders;
            }
        }

        // Non-Static version of above
        public bool GetIsExperimental()
        {
            return PlayerPrefs.HasKey("ExperimentalMode") && PlayerPrefs.GetInt("ExperimentalMode") == 1;
        }

        public void SetIsExperimental(bool active)
        {
            PlayerPrefs.SetInt("ExperimentalMode", active ? 1 : 0);
            BrushCatalog.m_Instance.Init();
            BrushCatalog.m_Instance.BeginReload();
        }

        void Awake()
        {
            m_SingletonState = this;
            m_WasExperimentalAtStartup = GetIsExperimental();

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(m_FakeCommandLineArgsInEditor))
            {
                try
                {
                    // This splits the arguments by spaces, excepting arguments enclosed by quotes.
                    var args = ("TiltBrush.exe " + m_FakeCommandLineArgsInEditor).Split('"')
                        .Select((element, index) => index % 2 == 0
                            ? element.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            : new string[] { element })
                        .SelectMany(element => element).ToArray();
                    ParseArgs(args);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                    Application.Quit();
                }
            }
#elif !(UNITY_ANDROID || UNITY_IOS)
            try
            {
                ParseArgs(System.Environment.GetCommandLineArgs());
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                Application.Quit();
            }
#endif

            m_BrushReplacement = new Dictionary<Guid, Guid>();
            if (IsExperimental)
            {
                foreach (var brush in m_BrushReplacementMap)
                {
                    m_BrushReplacement.Add(new Guid(brush.FromGuid), new Guid(brush.ToGuid));
                }
            }
        }

        /// Parses a setting taken from the command line of the form --Section.Setting value
        /// Where Section and Setting should be valid members of UserConfig.
        /// Stores the result in a list for applying to UserConfigs later.
        private void ParseUserSetting(string setting, string value)
        {
            var parts = setting.Split('.');
            string sectionName = parts[0].Substring(2);
            string settingName = parts[1];

            UserConfigChange change = new UserConfigChange();
            change.section = typeof(UserConfig).GetField(sectionName);
            if (change.section == null)
            {
                throw new ApplicationException(
                    string.Format("User Config section '{0}' not recognised.", sectionName));
            }
            change.setting = change.section.FieldType.GetMember(settingName).FirstOrDefault();
            if (change.setting == null)
            {
                throw new ApplicationException(string.Format(
                    "User Config section '{0}' does not have a {1} value.", sectionName, settingName));
            }
            Type memberType = null;
            if (change.setting is FieldInfo)
            {
                memberType = ((FieldInfo)change.setting).FieldType;
            }
            else if (change.setting is PropertyInfo)
            {
                memberType = ((PropertyInfo)change.setting).PropertyType;
            }
            else
            {
                throw new ApplicationException(string.Format(
                    "User Config section '{0}' does not have a {1} value.", sectionName, settingName));
            }
            try
            {
                change.value = Convert.ChangeType(value, memberType);
            }
            catch (Exception)
            {
                throw new ApplicationException(string.Format(
                    "User Config {0}.{1} cannot be set to '{2}'.", sectionName, settingName, value));
            }
            m_UserConfigChanges.Add(change);
        }

        /// Apply any changes specified on the command line to a user config object
        public void ApplyUserConfigOverrides(UserConfig userConfig)
        {
            foreach (var change in m_UserConfigChanges)
            {
                var section = change.section.GetValue(userConfig);
                if (section == null)
                {
                    Debug.LogWarningFormat("Weird - could not access UserConfig.{0}.", change.section.Name);
                    continue;
                }
                if (change.setting is FieldInfo)
                {
                    ((FieldInfo)change.setting).SetValue(section, change.value);
                }
                else
                {
                    ((PropertyInfo)change.setting).SetValue(section, change.value, null);
                }
                change.section.SetValue(userConfig, section);
            }
            foreach (var replacement in userConfig.Testing.BrushReplacementMap)
            {
                m_BrushReplacement.Add(replacement.Key, replacement.Value);
            }

            // Report deprecated members to users.
            if (userConfig.Flags.HighResolutionSnapshots)
            {
                OutputWindowScript.Error("HighResolutionSnapshots is deprecated.");
                OutputWindowScript.Error("Use SnapshotHeight and SnapshotWidth.");
            }
        }

#if UNITY_EDITOR
        /// Called at build time, just before this Config instance is saved to Main.unity
        public void DoBuildTimeConfiguration(UnityEditor.BuildTarget target, bool disableAccountLogins = false)
        {
            m_PlatformConfig = EditTimeAssetReferences.Instance.GetConfigForBuildTarget(target);
            DisableAccountLogins = disableAccountLogins;
        }
#endif
    }

} // namespace TiltBrush
