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
using System.Text;
using JetBrains.Annotations;
using TiltBrush;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Environment = System.Environment;

//----------------------------------------------------------------------------------------
// Notes on build flags which can be added to Player Settings.
//
//  - OCULUS_SUPPORTED
//      - Oculus is an optional target. Define this flag to add Oculus targets.
//
//----------------------------------------------------------------------------------------
// All output from this class is prefixed with "_btb_" to facilitate extracting
// it from Unity's very noisy and spammy Editor.log file.

static class BuildTiltBrush
{
    // Types, consts, enums

    // The vendor name - used for the company name in builds and fbx output. Can have spaces.
    public const string kDisplayVendorName = "Icosa Foundation";
    // The vendor name as the reverse DNS - used for naming mobile builds - shouldn't have spaces.
    public const string kVendorReverseDNS = "foundation.icosa";

    // Executable Base
    public const string kGuiBuildExecutableName = "OpenBrush";
    // Windows Executable
    public const string kGuiBuildWindowsExecutableName = kGuiBuildExecutableName + ".exe";
    // Linux Executable
    public const string kGuiBuildLinuxExecutableName = kGuiBuildExecutableName;
    // OSX Executable
    public const string kGuiBuildOsxExecutableName = kGuiBuildExecutableName + ".app";
    // Android Application Identifier
    public static string GuiBuildAndroidApplicationIdentifier => $"{kVendorReverseDNS}.{kGuiBuildExecutableName}".ToLower();
    // Android Executable
    public static string GuiBuildAndroidExecutableName => GuiBuildAndroidApplicationIdentifier + ".apk";
    public static string GuiBuildiOSApplicationIdentifier => $"{kVendorReverseDNS}.{kGuiBuildExecutableName}".ToLower();

    public class TiltBuildOptions
    {
        public bool AutoProfile;
        public bool Il2Cpp;
        public BuildTarget Target;
        public XrSdkMode XrSdk;
        public string Location;
        public string Stamp;
        public BuildOptions UnityOptions;
        public string Description;
        public bool disableAccountLogins;
    }

    [Serializable()]
    public class BuildFailedException : System.Exception
    {
        // The << >> markers help the build script parse the message
        public BuildFailedException(string message)
            : base(string.Format("<<{0}>>", message))
        {
        }
    }

    const string kMenuBackgroundBuild = "Open Brush/Build/Background Build";
    const string kMenuPluginPref = "Open Brush/Build/Plugin";
    const string kMenuPluginMono = "Open Brush/Build/Plugin: Mono";
    const string kMenuPluginOpenXr = "Open Brush/Build/Plugin: OpenXR";
    const string kMenuPluginOculus = "Open Brush/Build/Plugin: Oculus";
    const string kMenuPluginWave = "Open Brush/Build/Plugin: Wave";
    const string kMenuPluginPico = "Open Brush/Build/Plugin: Pico";
    const string kMenuPlatformPref = "Open Brush/Build/Platform";
    const string kMenuPlatformWindows = "Open Brush/Build/Platform: Windows";
    const string kMenuPlatformLinux = "Open Brush/Build/Platform: Linux";
    const string kMenuPlatformOsx = "Open Brush/Build/Platform: OSX";
    const string kMenuPlatformAndroid = "Open Brush/Build/Platform: Android";
    const string kMenuDevelopment = "Open Brush/Build/Development";
    const string kMenuMono = "Open Brush/Build/Runtime: Mono";
    const string kMenuIl2cpp = "Open Brush/Build/Runtime: IL2CPP";
    const string kMenuAutoProfile = "Open Brush/Build/Auto Profile";

    const string kBuildCopyDir = "BuildCopy";
    private static string[] kBuildDirs = { "Assets", "Packages", "ProjectSettings", "Support" };

    private static readonly List<KeyValuePair<XrSdkMode, BuildTarget>> kValidSdkTargets
        = new List<KeyValuePair<XrSdkMode, BuildTarget>>()
        {
            // OpenXR
            new KeyValuePair<XrSdkMode, BuildTarget>(XrSdkMode.OpenXR, BuildTarget.StandaloneWindows64),
            new KeyValuePair<XrSdkMode, BuildTarget>(XrSdkMode.OpenXR, BuildTarget.Android),

            // Zapbox
            new KeyValuePair<XrSdkMode, BuildTarget>(XrSdkMode.Zapbox, BuildTarget.iOS),

#if OCULUS_SUPPORTED
            // Oculus
            new KeyValuePair<XrSdkMode, BuildTarget>(XrSdkMode.Oculus, BuildTarget.StandaloneWindows64),
            new KeyValuePair<XrSdkMode, BuildTarget>(XrSdkMode.Oculus, BuildTarget.Android),
#endif // OCULUS_SUPPORTED
            // Wave
            new KeyValuePair<XrSdkMode, BuildTarget>(XrSdkMode.Wave, BuildTarget.Android),
#if PICO_SUPPORTED
            // Pico
            new KeyValuePair<XrSdkMode, BuildTarget>(XrSdkMode.Pico, BuildTarget.Android),
#endif // PICO_SUPPORTED
        };

    static readonly List<CopyRequest> kToCopy = new List<CopyRequest>
    {
        // Non-Android because this file is now hosted at
        // https://docs.google.com/document/d/1L70mH-vmrLMxZX4525qpffAyqVcg770I74Pw_RGN8Ic
        // TODO: Find a way to view the generated file from disk on Android so we
        // don't have to keep the hosted file in sync.
        new CopyRequest("Support/ThirdParty/GeneratedThirdPartyNotices.txt", "NOTICE")
        {
            omitForAndroid = true
        },
        new CopyRequest(FfmpegPipe.kFfmpegDir) { omitForAndroid = true },
        new CopyRequest("Support/tiltasaurus.json"),
        new CopyRequest("Support/README.txt") { omitForAndroid = true },
        new CopyRequest("Support/exportManifest.json"),
        new CopyRequest("Support/bin/renderVideo.cmd") { omitForAndroid = true },
        new CopyRequest("Support/bin/renderVideo.sh") { omitForAndroid = true },
        new CopyRequest("Support/whiteTextureMap.png"),
        // No longer needed, now that these are hosted
        // new CopyRequest("Support/GlTFShaders"),
    };

    // Used to transfer information from DoBuild() to the post-build callback
    class PostBuildInfo
    {
        public List<CopyRequest> copyRequests;
        public XrSdkMode xrSdk;
    }
    static PostBuildInfo m_forPostBuild;

    /// Called on the main thread once the background build is finished
    public static event Action<int> OnBackgroundBuildFinish;

    // Properties
    // EditorPrefs is used as authoritative state for the Gui* properties.
    // Assigning to Gui* properties updates the gui as a side effect.

    public static bool BackgroundBuild
    {
        get => EditorPrefs.GetBool(kMenuBackgroundBuild, false);
        set
        {
            EditorPrefs.SetBool(kMenuBackgroundBuild, value);
            Menu.SetChecked(kMenuBackgroundBuild, value);
        }
    }

#if UNITY_ANDROID
#if UNITY_EDITOR_WIN
    private static readonly string m_adbPath = Path.Combine(UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath, "platform-tools", "adb.exe");
#else
    private static readonly string m_adbPath = Path.Combine(UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath, "platform-tools", "adb");
#endif
#else
    private static readonly string m_adbPath = "ADB_NOT_AVAILABLE_WITHOUT_UNITY_ANDROID";
#endif
    public static string AdbPath => m_adbPath;

    private static string m_buildStatus = "-";
    public static string BuildStatus => m_buildStatus; // info about current status

    // Gui setting for "Sdk" radio buttons
    public static XrSdkMode GuiSelectedSdk
    {
        get
        {
            return AsEnum(EditorPrefs.GetString(kMenuPluginPref, "OpenXR"), XrSdkMode.OpenXR);
        }
        set
        {
            EditorPrefs.SetString(kMenuPluginPref, value.ToString());
            Menu.SetChecked(kMenuPluginOpenXr, value == XrSdkMode.OpenXR);
#if OCULUS_SUPPORTED
            Menu.SetChecked(kMenuPluginOculus, value == XrSdkMode.Oculus);
#endif // OCULUS_SUPPORTED
            Menu.SetChecked(kMenuPluginWave, value == XrSdkMode.Wave);
            Menu.SetChecked(kMenuPluginPico, value == XrSdkMode.Pico);

            if (!BuildTargetSupported(value, GuiSelectedBuildTarget))
            {
                GuiSelectedBuildTarget = kValidSdkTargets.First(x => x.Key == value).Value;
            }
        }
    }

    public static BuildTarget GuiSelectedBuildTarget
    {
        get
        {
            return AsEnum(EditorPrefs.GetString(kMenuPlatformPref, "StandaloneWindows64"),
                BuildTarget.StandaloneWindows64);
        }
        set
        {
            EditorPrefs.SetString(kMenuPlatformPref, value.ToString());
            Menu.SetChecked(kMenuPlatformWindows, value == BuildTarget.StandaloneWindows64);
            Menu.SetChecked(kMenuPlatformLinux, value == BuildTarget.StandaloneLinux64);
            Menu.SetChecked(kMenuPlatformOsx, value == BuildTarget.StandaloneOSX);
            Menu.SetChecked(kMenuPlatformAndroid, value == BuildTarget.Android);
        }
    }

    public static BuildTarget[] SupportedBuildTargets()
    {
        return kValidSdkTargets.Select(x => x.Value).Distinct().ToArray();
    }

    public static XrSdkMode[] SupportedSdkModes()
    {
        return kValidSdkTargets.Select(x => x.Key).Distinct().ToArray();
    }

    public static bool BuildTargetSupported(XrSdkMode mode, BuildTarget target)
    {
        return kValidSdkTargets.Any(x => x.Key == mode && x.Value == target);
    }

    // Gui setting for "Experimental" checkbox

    // Gui setting for "Development" checkbox
    public static bool GuiDevelopment
    {
        get => EditorPrefs.GetBool(kMenuDevelopment, false);
        set
        {
            EditorPrefs.SetBool(kMenuDevelopment, value);
            Menu.SetChecked(kMenuDevelopment, value);
        }
    }

    // Gui setting for "Auto Profile" checkbox
    public static bool GuiAutoProfile
    {
        get => EditorPrefs.GetBool(kMenuAutoProfile, false);
        set
        {
            EditorPrefs.SetBool(kMenuAutoProfile, value);
            Menu.SetChecked(kMenuAutoProfile, value);
        }
    }

    public static bool GuiRuntimeIl2cpp
    {
        get => EditorPrefs.GetBool(kMenuIl2cpp, false);
        set
        {
            EditorPrefs.SetBool(kMenuIl2cpp, value);
            Menu.SetChecked(kMenuIl2cpp, value);
            Menu.SetChecked(kMenuMono, !value);
        }
    }

    public static bool GuiRuntimeMono
    {
        get { return !GuiRuntimeIl2cpp; }
        set { GuiRuntimeIl2cpp = !value; }
    }

    public static TiltBuildOptions GetGuiOptions()
    {
        return new TiltBuildOptions
        {
            AutoProfile = GuiAutoProfile,
            Il2Cpp = GuiRuntimeIl2cpp,
            Target = GuiSelectedBuildTarget,
            XrSdk = GuiSelectedSdk,
            Location = GetAppPathForGuiBuild(),
            Stamp = "menuitem",
            UnityOptions = GuiDevelopment
                ? (BuildOptions.AllowDebugging | BuildOptions.Development | BuildOptions.CleanBuildCache)
                : BuildOptions.None,
            Description = "unity editor",
        };
    }

    // Menu items
    [MenuItem("Open Brush/Build/Do Build... #&b", false, 2)]
    public static void MenuItem_Build()
    {
        TiltBuildOptions tiltOptions = GetGuiOptions();

        if (BackgroundBuild)
        {
            DoBackgroundBuild(tiltOptions, true);
        }
        else
        {
            using (var unused = new RestoreCurrentScene())
            {
                DoBuild(tiltOptions);
            }
        }
    }

    public static string GetAppPathForGuiBuild()
    {
        BuildTarget buildTarget = GuiSelectedBuildTarget;

        string sdk = GuiSelectedSdk.ToString();
        if (GuiSelectedBuildTarget == BuildTarget.Android)
            sdk += "Mobile";

        var directoryName = string.Format(
            "{0}_{1}_{4}{2}{3}",
            sdk,
            GuiDevelopment ? "Debug" : "Release",
            GuiRuntimeIl2cpp ? "_Il2cpp" : "",
            GuiAutoProfile ? "_AutoProfile" : "",
            kGuiBuildExecutableName);
        var location = Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));

        location = Path.Combine(Path.Combine(location, "Builds"), directoryName);
        switch (buildTarget)
        {
            case BuildTarget.Android:
                location += "/" + GuiBuildAndroidExecutableName;
                break;
            case BuildTarget.iOS:
                location += "/" + kGuiBuildExecutableName;
                break;
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                location += "/" + kGuiBuildWindowsExecutableName;
                break;
            case BuildTarget.StandaloneLinux64:
                location += "/" + kGuiBuildLinuxExecutableName;
                break;
            case BuildTarget.StandaloneOSX:
                location += "/" + kGuiBuildOsxExecutableName;
                break;
            default:
                throw new BuildFailedException("Unsupported BuildTarget: " + buildTarget.ToString());
        }

        return location;
    }

    // There are a pair of functions for each menu item in the Tilt->Build menu.
    // The first of each is the method that gets called when that menuitem gets selected.
    // The second is a 'validation' method that gets called before the menuitem gets show.
    // This is meant to allow us to enable or disable a menu item, but in this case we are using
    // it to make sure the checkbox is correctly set for each item, as it always gets called before
    // a menuitem is shown, and Unity has a habit of losing whether or not a checkbox should be shown.
    [MenuItem(kMenuBackgroundBuild, isValidateFunction: false, priority: 2)]
    static void MenuItem_BackgroundBuild()
    {
        BackgroundBuild = !BackgroundBuild;
    }

    [MenuItem(kMenuBackgroundBuild, isValidateFunction: true)]
    static bool MenuItem_BackgroundBuild_Validate()
    {
        Menu.SetChecked(kMenuBackgroundBuild, BackgroundBuild);
        return true;
    }

    //=======  SDKs =======

    [MenuItem(kMenuPluginOpenXr, isValidateFunction: false, priority: 110)]
    static void MenuItem_Plugin_OpenXr()
    {
        GuiSelectedSdk = XrSdkMode.OpenXR;
    }

    [MenuItem(kMenuPluginOpenXr, isValidateFunction: true)]
    static bool MenuItem_Plugin_OpenXr_Validate()
    {
        Menu.SetChecked(kMenuPluginOpenXr, GuiSelectedSdk == XrSdkMode.OpenXR);
        return true;
    }

    [MenuItem(kMenuPluginOculus, isValidateFunction: false, priority: 105)]
    static void MenuItem_Plugin_Oculus()
    {
        GuiSelectedSdk = XrSdkMode.Oculus;
    }

    [MenuItem(kMenuPluginOculus, isValidateFunction: true)]
    static bool MenuItem_Plugin_Oculus_Validate()
    {
#if OCULUS_SUPPORTED
        Menu.SetChecked(kMenuPluginOculus, GuiSelectedSdk == XrSdkMode.Oculus);
        return true;
#else
        return false;
#endif
    }

    [MenuItem(kMenuPluginWave, isValidateFunction: false, priority: 115)]
    static void MenuItem_Plugin_Wave()
    {
        GuiSelectedSdk = XrSdkMode.Wave;
    }

    [MenuItem(kMenuPluginWave, isValidateFunction: true)]
    static bool MenuItem_Plugin_Wave_Validate()
    {
        Menu.SetChecked(kMenuPluginWave, GuiSelectedSdk == XrSdkMode.Wave);
        return true;
    }

    [MenuItem(kMenuPluginPico, isValidateFunction: false, priority: 125)]
    static void MenuItem_Plugin_Pico()
    {
        GuiSelectedSdk = XrSdkMode.Pico;
    }

    [MenuItem(kMenuPluginPico, isValidateFunction: true)]
    static bool MenuItem_Plugin_Pico_Validate()
    {
#if PICO_SUPPORTED
        Menu.SetChecked(kMenuPluginPico, GuiSelectedSdk == XrSdkMode.Pico);
        return true;
#else
        return false;
#endif
    }

    //=======  Platforms =======

    [MenuItem(kMenuPlatformWindows, isValidateFunction: false, priority: 200)]
    static void MenuItem_Platform_Windows()
    {
        GuiSelectedBuildTarget = BuildTarget.StandaloneWindows64;
    }

    [MenuItem(kMenuPlatformWindows, isValidateFunction: true)]
    static bool MenuItem_Platform_Windows_Validate()
    {
        Menu.SetChecked(kMenuPlatformWindows,
            GuiSelectedBuildTarget == BuildTarget.StandaloneWindows64);
        return BuildTargetSupported(GuiSelectedSdk, BuildTarget.StandaloneWindows64);
    }

    // [MenuItem(kMenuPlatformLinux, isValidateFunction: false, priority: 202)]
    // static void MenuItem_Platform_Linux()
    // {
    //     GuiSelectedBuildTarget = BuildTarget.StandaloneLinux64;
    // }
    //
    // [MenuItem(kMenuPlatformLinux, isValidateFunction: true)]
    // static bool MenuItem_Platform_Linux_Validate()
    // {
    //     Menu.SetChecked(kMenuPlatformLinux, GuiSelectedBuildTarget == BuildTarget.StandaloneLinux64);
    //     return BuildTargetSupported(GuiSelectedSdk, BuildTarget.StandaloneLinux64);
    // }

    [MenuItem(kMenuPlatformOsx, isValidateFunction: false, priority: 205)]
    static void MenuItem_Platform_Osx()
    {
        GuiSelectedBuildTarget = BuildTarget.StandaloneOSX;
    }

    [MenuItem(kMenuPlatformOsx, isValidateFunction: true)]
    static bool MenuItem_Platform_Osx_Validate()
    {
        Menu.SetChecked(kMenuPlatformOsx, GuiSelectedBuildTarget == BuildTarget.StandaloneOSX);
        return BuildTargetSupported(GuiSelectedSdk, BuildTarget.StandaloneOSX);
    }

    [MenuItem(kMenuPlatformAndroid, isValidateFunction: false, priority: 210)]
    static void MenuItem_Platform_Android()
    {
        GuiSelectedBuildTarget = BuildTarget.Android;
    }

    [MenuItem(kMenuPlatformAndroid, isValidateFunction: true)]
    static bool MenuItem_Platform_Android_Validate()
    {
        Menu.SetChecked(kMenuPlatformAndroid, GuiSelectedBuildTarget == BuildTarget.Android);
        return BuildTargetSupported(GuiSelectedSdk, BuildTarget.Android);
    }

    //=======  Runtimes =======

    [MenuItem(kMenuMono, isValidateFunction: false, priority: 300)]
    static void MenuItem_Runtime_Mono()
    {
        GuiRuntimeMono = !GuiRuntimeMono;
    }

    [MenuItem(kMenuMono, isValidateFunction: true)]
    static bool MenuItem_Runtime_Mono_Validate()
    {
        Menu.SetChecked(kMenuMono, GuiRuntimeMono);
        return true;
    }

    [MenuItem(kMenuIl2cpp, isValidateFunction: false, priority: 305)]
    static void MenuItem_Runtime_Il2cpp()
    {
        GuiRuntimeIl2cpp = !GuiRuntimeIl2cpp;
    }

    [MenuItem(kMenuIl2cpp, isValidateFunction: true)]
    static bool MenuItem_Runtime_Il2cpp_Validate()
    {
        Menu.SetChecked(kMenuIl2cpp, GuiRuntimeIl2cpp);
        return true;
    }

    //=======  Options =======



    [MenuItem(kMenuDevelopment, isValidateFunction: false, priority: 405)]
    static void MenuItem_Development()
    {
        GuiDevelopment = !GuiDevelopment;
        if (!GuiDevelopment)
        {
            GuiAutoProfile = false;
        }
    }

    [MenuItem(kMenuDevelopment, isValidateFunction: true)]
    static bool MenuItem_Development_Validate()
    {
        Menu.SetChecked(kMenuDevelopment, GuiDevelopment);
        return true;
    }

    [MenuItem(kMenuAutoProfile, isValidateFunction: false, priority: 410)]
    static void MenuItem_AutoProfile()
    {
        GuiAutoProfile = !GuiAutoProfile;
        if (GuiAutoProfile)
        {
            GuiDevelopment = true;
        }
    }

    [MenuItem(kMenuAutoProfile, isValidateFunction: true)]
    static bool MenuItem_AutoProfile_Validate()
    {
        Menu.SetChecked(kMenuAutoProfile, GuiAutoProfile);
        return true;
    }

    static T AsEnum<T>(string s, T defaultValue)
    {
        try
        {
            return (T)Enum.Parse(typeof(T), s, true);
        }
        catch (ArgumentException)
        {
            Debug.LogErrorFormat("_btb_ Unknown value for {0}: {1}", typeof(T).FullName, s);
            return defaultValue;
        }
    }

    static T? AsEnum<T>(string s) where T : struct
    {
        try
        {
            return (T)Enum.Parse(typeof(T), s, true);
        }
        catch (ArgumentException)
        {
            Debug.LogErrorFormat("_btb_ Unknown value for {0}: {1}", typeof(T).FullName, s);
            return null;
        }
    }

    static public BuildTargetGroup TargetToGroup(BuildTarget buildTarget)
    {
        switch (buildTarget)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
            case BuildTarget.StandaloneLinux64:
            case BuildTarget.StandaloneOSX:
                return BuildTargetGroup.Standalone;
            case BuildTarget.Android:
                return BuildTargetGroup.Android;
            case BuildTarget.iOS:
                return BuildTargetGroup.iOS;
            default:
                throw new ArgumentException("buildTarget");
        }
    }

    // Removes the the given suffix from text.
    // Returns true on success
    static bool RemoveSuffix(ref string text, string suffix)
    {
        if (!text.EndsWith(suffix))
        {
            return false;
        }
        text = text.Substring(0, text.Length - suffix.Length);
        return true;
    }

    // Make changes to PlayerSettings that either can't be or shouldn't be serialized
    class TempSetCommandLineOnlyPlayerSettings : IDisposable
    {
        string m_oldKeystoreName, m_oldKeystorePass;
        string m_oldKeyaliasName, m_oldKeyaliasPass;
        public TempSetCommandLineOnlyPlayerSettings(
            string keystoreName, string keystorePass,
            string keyaliasName, string keyaliasPass)
        {
            m_oldKeystoreName = CheckUnset(PlayerSettings.Android.keystoreName, "keystoreName");
            m_oldKeystorePass = CheckUnset(PlayerSettings.Android.keystorePass, "keystorePass");
            m_oldKeyaliasName = CheckUnset(PlayerSettings.Android.keyaliasName, "keyaliasName");
            m_oldKeyaliasPass = CheckUnset(PlayerSettings.Android.keyaliasPass, "keyaliasPass");

            if (keystoreName != null) { PlayerSettings.Android.keystoreName = keystoreName; }
            if (keystorePass != null) { PlayerSettings.Android.keystorePass = keystorePass; }
            if (keyaliasName != null) { PlayerSettings.Android.keyaliasName = keyaliasName; }
            if (keyaliasPass != null) { PlayerSettings.Android.keyaliasPass = keyaliasPass; }
        }

        public void Dispose()
        {
            PlayerSettings.Android.keystoreName = m_oldKeystoreName;
            PlayerSettings.Android.keystorePass = m_oldKeystorePass;
            PlayerSettings.Android.keyaliasName = m_oldKeyaliasName;
            PlayerSettings.Android.keyaliasPass = m_oldKeyaliasPass;
            AssetDatabase.SaveAssets();
        }

        private static string CheckUnset(string value, string name)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Debug.LogWarningFormat("Expected: {0} is unset", name);
            }
            return value;
        }
    }

    // Command-line arguments must come directly after "-executeMethod BuildTiltBrush.CommandLine"
    //
    //   -btb-stamp STR      Pushed into App.m_BuildStamp.
    //   -btb-display DISP   See SdkMode for values. Default: UnityXR.
    //   -btb-target TARGET  See UnityEditor.BuildTarget for values. Default: StandaloneWindows.
    //   -btb-bopt   OPT     See UnityEditor.BuildOptions for values. Can pass multiple times.
    //   -btb-out    DIR     Set output location: the name of the desired executable. Required.
    //   -btb-experimental   Set the build to be an experimental build.
    //   -btb-autoprofile    Set the build to be an auto-profile build.
    //   -btb-key{store,alias}{name,pass} Info needed for Android signing
    //   -btb-increment-bundle-version  Increment PlayerSettings "bundleVersionCode"; use this
    //                       when uploading a build. [This is no longer used]
    //
    [PublicAPI]
    static void CommandLine()
    {
        BuildTarget? target = null;
        TiltBuildOptions tiltOptions = new TiltBuildOptions()
        {
            Stamp = "",
            XrSdk = XrSdkMode.OpenXR,
            UnityOptions = BuildOptions.None,
        };
        string keystoreName = null;
        string keyaliasName = null;
        string keystorePass = Environment.GetEnvironmentVariable("BTB_KEYSTORE_PASS");
        string keyaliasPass = Environment.GetEnvironmentVariable("BTB_KEYALIAS_PASS");

#if OCULUS_SUPPORTED
        // Call these once to create the files. Normally (i.e., in a GUI build), they're created with
        // [UnityEditor.InitializeOnLoad], but in case they're missing, like in CI, make sure they're
        // there!
        OVRProjectConfig defaultOculusProjectConfig = OVRProjectConfig.GetProjectConfig();
        string useless_app_id = Assets.Oculus.VR.Editor.OVRPlatformToolSettings.AppID;
#endif

        {
            string[] args = Environment.GetCommandLineArgs();
            int i = 0;
            for (; i < args.Length; ++i)
            {
                if (args[i] == "BuildTiltBrush.CommandLine")
                {
                    break;
                }
            }
            if (i == args.Length)
            {
                Die(2, "Could not find command line arguments");
            }

            for (i = i + 1; i < args.Length; ++i)
            {
                if (args[i] == "-btb-display")
                {
                    string mode = args[++i];
                    // TODO: Legacy; remove when our build shortcuts are updated
                    tiltOptions.XrSdk = AsEnum(mode, tiltOptions.XrSdk);
                }
                else if (args[i] == "-btb-description")
                {
                    tiltOptions.Description = args[++i];
                }
                else if (args[i] == "-btb-il2cpp")
                {
                    tiltOptions.Il2Cpp = true;
                }
                else if (args[i] == "-btb-bopt")
                {
                    tiltOptions.UnityOptions |= AsEnum(args[++i], BuildOptions.None);
                }
                else if (args[i] == "-btb-target")
                {
                    target = AsEnum<BuildTarget>(args[++i]);
                }
                else if (args[i] == "-customBuildPath")
                {
                    tiltOptions.Location = args[++i];
                }
                else if (args[i] == "-btb-out")
                {
                    tiltOptions.Location = args[++i];
                }
                else if (args[i] == "-btb-stamp")
                {
                    tiltOptions.Stamp = args[++i];
                }
                else if (args[i] == "-executeMethod" || args[i] == "-quit")
                {
                    break;
                }
                else if (args[i] == "--btb-autoprofile")
                {
                    tiltOptions.AutoProfile = true;
                }
                else if (args[i] == "-btb-keystore-name")
                {
                    // Unity forces this to be an absolute path, so it can't be checked in
                    keystoreName = args[++i];
                }
                else if (args[i] == "-btb-keyalias-name")
                {
                    // Should not be checked in; Unity requires a password if alias name is filled in
                    keyaliasName = args[++i];
                }
                else if (args[i] == "-btb-increment-bundle-version")
                {
                    // Don't restore this, because user might want to check in this change?
                    PlayerSettings.Android.bundleVersionCode += 1;
                }
                else if (args[i] == "-buildVersion")
                {
                    // TODO: do we want to do anything with this? Can we use it instead of the version string
                    // set externally?
                    i++;
                }
                else if (args[i] == "-androidTargetSdkVersion")
                {
                    // Not supported in Open Brush (added to game-ci in https://github.com/game-ci/unity-builder/pull/298)
                    // By default, this field has no value, but if set, we need to skip it
                    if (!args[i + 1].StartsWith("-"))
                    {
                        i++;
                    }
                }
                else if (args[i] == "-androidVersionCode")
                {
                    PlayerSettings.Android.bundleVersionCode = Int32.Parse(args[++i]);
                }
                else if (args[i] == "-androidKeystoreName")
                {
                    keystoreName = args[++i];
                }
                else if (args[i] == "-androidKeystorePass")
                {
                    keystorePass = args[++i];
                }
                else if (args[i] == "-androidKeyaliasName")
                {
                    keyaliasName = args[++i];
                }
                else if (args[i] == "-androidKeyaliasPass")
                {
                    keyaliasPass = args[++i];
                }
                else if (args[i] == "-setDefaultPlatformTextureFormat")
                {
                    i++;
                }
                else if (args[i] == "-btb-disableAccountLogins")
                {
                    tiltOptions.disableAccountLogins = true;
                }
                else if (args[i] == "-androidExportType")
                {
                    // Not supported in Open Brush (added to game-ci in v3)
                    i++;
                }
                else if (args[i] == "-androidSymbolType")
                {
                    // Not supported in Open Brush (added to game-ci in v3)
                    i++;
                }
                else
                {
                    Die(3, "Unknown argument {0}", args[i]);
                    EditorApplication.Exit(3);
                }
            }
            if (tiltOptions.Location == null)
            {
                Die(4, "You must pass -btb-out");
            }
        }

        if (target == null)
        {
            target = BuildTarget.StandaloneWindows64;
        }

        if (target == BuildTarget.Android)
        {
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Debugging;
        }

        tiltOptions.Target = target.Value;
        using (var unused = new TempSetCommandLineOnlyPlayerSettings(
            keystoreName, keystorePass,
            keyaliasName, keyaliasPass))
        {
            try
            {
                DoBuild(tiltOptions);
            }
            catch (Exception Ex)
            {
                string oneLineMessage = Ex.Message.Replace("\n", " ");
                Debug.LogErrorFormat("::error ::Build failed with Exception <<{0}>>", oneLineMessage);
                Debug.LogError($"{Ex.StackTrace}\n\n");
                // For some reason, Unity exits if we rethrow this (or never caught it), but with an exit code of 0. So instead, we'll explicitly exit here
                // throw;
                Die(6, oneLineMessage);
            }
        }
    }

    class TempDefineSymbols : System.IDisposable
    {
        string m_prevSymbols;
        BuildTargetGroup m_group;

        // For convenience, the extra symbols can be "" or null
        public TempDefineSymbols(BuildTarget target, params string[] symbols)
        {
            m_group = BuildTiltBrush.TargetToGroup(target);
            m_prevSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(m_group);
            var newSymbols = m_prevSymbols.Split(';') // might be [""]
                .Concat(symbols.Where(elt => elt != null))
                .Select(elt => elt.Trim())
                .Where(elt => elt != "")
                .ToArray();
            var newDefs = string.Join(";", newSymbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(m_group, newDefs);
            Debug.Log($"Build defines for {m_group.ToString()}: {newDefs}");
        }

        public void Dispose()
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(m_group, m_prevSymbols);
        }
    }

    class TempSetPlayerSettings : IDisposable
    {
        private BuildTarget m_Target;
        private UIOrientation m_OrientationSettings;
        private iOSTargetDevice m_iOSTargetDevice;
        private Texture2D[] m_Icons;

        public TempSetPlayerSettings(TiltBuildOptions tiltOptions)
        {
            m_Target = tiltOptions.Target;
            m_OrientationSettings = PlayerSettings.defaultInterfaceOrientation;
            m_iOSTargetDevice = PlayerSettings.iOS.targetDevice;
            m_Icons = PlayerSettings.GetIcons(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(TargetToGroup(m_Target)), IconKind.Any);

            switch (tiltOptions.XrSdk)
            {
                case XrSdkMode.Zapbox:
                    PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                    PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
                    var zapboxIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Trademarked/TiltBrushLogoZapbox.png");

                    Texture2D[] newIcons = { zapboxIcon };

                    var buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(TargetToGroup(m_Target));

                    PlayerSettings.SetIcons(buildTarget, newIcons, IconKind.Any);
#if UNITY_IOS
                    PlayerSettings.SetIcons(buildTarget, newIcons, IconKind.Notification);
                    PlayerSettings.SetIcons(buildTarget, newIcons, IconKind.Settings);
                    PlayerSettings.SetIcons(buildTarget, newIcons, IconKind.Spotlight);
                    PlayerSettings.SetIcons(buildTarget, newIcons, IconKind.Notification);
                    PlayerSettings.SetIcons(buildTarget, newIcons, IconKind.Store);
#endif
                    break;
                default:
                    break;
            }
        }

        public void Dispose()
        {
            PlayerSettings.defaultInterfaceOrientation = m_OrientationSettings;
            PlayerSettings.iOS.targetDevice = m_iOSTargetDevice;
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(TargetToGroup(m_Target)), m_Icons, IconKind.Any);
            AssetDatabase.SaveAssets();
        }
    }

    class TempSetScriptingBackend : IDisposable
    {
        private ScriptingImplementation m_prevbackend;
        private BuildTargetGroup m_group;

        public TempSetScriptingBackend(BuildTarget target, bool useIl2cpp)
        {
            m_group = BuildTiltBrush.TargetToGroup(target);
            m_prevbackend = PlayerSettings.GetScriptingBackend(m_group);

            // Build script assumes there are only 2 possibilities. It's been true so far,
            // but detect if that assumption ever becomes dangerous and we need to generalize
            var desired = useIl2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;
            if (m_prevbackend != ScriptingImplementation.IL2CPP &&
                m_prevbackend != ScriptingImplementation.Mono2x)
            {
                throw new BuildFailedException(string.Format(
                    "Internal error: trying to switch away from {0}", m_prevbackend));
            }
            PlayerSettings.SetScriptingBackend(m_group, desired);
        }

        public void Dispose()
        {
            PlayerSettings.SetScriptingBackend(m_group, m_prevbackend);
        }
    }

    // Must come after TempHookUpSingletons
    class TempSetBundleVersion : IDisposable
    {
        string m_prevBundleVersion;
        public TempSetBundleVersion(BuildTarget target, string configVersionNumber, string stamp)
        {
            m_prevBundleVersion = PlayerSettings.bundleVersion;
            // https://stackoverflow.com/a/9741724/194921 for more on the meaning/format of this string
            PlayerSettings.bundleVersion = configVersionNumber;
            if (!string.IsNullOrEmpty(stamp) && target != BuildTarget.iOS)
            {
                PlayerSettings.bundleVersion += string.Format("-{0}", stamp);
            }


        }
        public void Dispose()
        {
            PlayerSettings.bundleVersion = m_prevBundleVersion;
        }
    }

    class TempSetAppNames : IDisposable
    {
        private string m_identifier;
        private string m_name;
        private string m_company;
        private bool m_IsAndroidOrIos;
        private BuildTarget m_Target;
        public TempSetAppNames(BuildTarget target, string Description)
        {
            m_Target = target;
            m_IsAndroidOrIos = m_Target == BuildTarget.Android || m_Target == BuildTarget.iOS;
            m_identifier = PlayerSettings.GetApplicationIdentifier(TargetToGroup(target));
            m_name = PlayerSettings.productName;
            m_company = PlayerSettings.companyName;
            string new_name = App.kAppDisplayName;

            string new_identifier = m_identifier;
            switch (m_Target)
            {
                case BuildTarget.Android:
                    new_identifier = GuiBuildAndroidApplicationIdentifier;
                    break;
                case BuildTarget.iOS:
                    new_identifier = GuiBuildiOSApplicationIdentifier;
                    break;
                default:
                    break;
            }

#if OCULUS_SUPPORTED || USE_QUEST_PACKAGE_NAME
            //Can't change Quest identifier
            new_identifier = "com.Icosa.OpenBrush";
#elif ZAPBOX_SUPPORTED
            // Zapbox has a separate listing
            new_identifier = "foundation.icosa.openbrushzapbox";
#endif
            if (!String.IsNullOrEmpty(Description))
            {
                new_name += "-(" + Description + ")";
                new_identifier += "-" + Description.Replace("_", "").Replace("#", "").Replace("-", "");
            }
            if (m_IsAndroidOrIos)
            {
                PlayerSettings.SetApplicationIdentifier(TargetToGroup(target), new_identifier);
            }
            PlayerSettings.productName = new_name;
            PlayerSettings.companyName = kDisplayVendorName;
        }

        public void Dispose()
        {
            if (m_IsAndroidOrIos)
            {
                PlayerSettings.SetApplicationIdentifier(TargetToGroup(m_Target), m_identifier);
            }
            PlayerSettings.productName = m_name;
            PlayerSettings.companyName = m_company;
        }
    }

    class TempSetOpenXrFeatureGroup : IDisposable
    {
        readonly List<UnityEngine.XR.OpenXR.Features.OpenXRFeature> enabledFeatures;
        readonly List<UnityEngine.XR.OpenXR.Features.OpenXRFeature> requiredFeatures;
        readonly BuildTargetGroup m_targetGroup;

        public TempSetOpenXrFeatureGroup(TiltBuildOptions tiltOptions)
        {
            enabledFeatures = new();
            requiredFeatures = new();
            List<string> requiredFeatureStrings = new();

            m_targetGroup = TargetToGroup(tiltOptions.Target);

            switch (tiltOptions.XrSdk)
            {
                case XrSdkMode.Oculus:
                    // requiredFeatureStrings.Add("com.oculus.openxr.feature.oculusxr");
                    // if (m_targetGroup == BuildTargetGroup.Android)
                    // {
                    //     requiredFeatureStrings.Add("com.unity.openxr.feature.oculusquest");
                    // }
                    break;
            }

            if (requiredFeatureStrings.Count == 0)
            {
                return;
            }

            // Refresh list of features present in project, then iterate and disable all of them.
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(m_targetGroup);
            var featureList = new List<UnityEngine.XR.OpenXR.Features.OpenXRFeature>();
            int featuresCount = UnityEngine.XR.OpenXR.OpenXRSettings.Instance.GetFeatures(featureList);
            if (featuresCount > 0)
            {
                foreach (var feature in featureList)
                {
                    if (feature.enabled)
                    {
                        enabledFeatures.Add(feature);
                    }
                    feature.enabled = false;
                }
            }

            foreach (var feature in featureList)
            {
                if (feature.enabled)
                {
                    throw new BuildFailedException($"Shouldn't be here! {feature.name}");
                }
            }

            if (requiredFeatures.Count == 0)
            {
                return;
            }

            // Locate and enable features, fail if not found.
            foreach (string requiredFeatureString in requiredFeatureStrings)
            {
                var requiredFeature = UnityEditor.XR.OpenXR.Features.FeatureHelpers.GetFeatureWithIdForBuildTarget(m_targetGroup, requiredFeatureString);
                if (requiredFeature == null)
                {
                    throw new BuildFailedException($"Could not find required OpenXR Feature {requiredFeatureString}. Is it installed?");
                }
                requiredFeatures.Add(requiredFeature);
                requiredFeature.enabled = true;
            }
        }

        public void Dispose()
        {
            foreach (var requiredFeature in requiredFeatures)
            {
                if (!enabledFeatures.Contains(requiredFeature))
                {
                    requiredFeature.enabled = false;
                }
            }

            foreach (var enabledFeature in enabledFeatures)
            {
                enabledFeature.enabled = true;
            }
        }
    }

    class TempSetXrPlugin : IDisposable
    {
        List<XRLoader> m_plugins;
        bool m_xrEnabled;
        BuildTargetGroup m_targetGroup;

        public TempSetXrPlugin(TiltBuildOptions tiltOptions)
        {
            m_plugins = new();
            string[] targetXrPluginsRequired = new string[] { };

            m_targetGroup = TargetToGroup(tiltOptions.Target);
            var targetSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(m_targetGroup);
            m_xrEnabled = targetSettings.InitManagerOnStart;

            switch (tiltOptions.XrSdk)
            {
                case XrSdkMode.Oculus:
                    targetXrPluginsRequired = new string[] { "Unity.XR.Oculus.OculusLoader" };
                    break;
                case XrSdkMode.OpenXR:
                    targetXrPluginsRequired = new string[] { "UnityEngine.XR.OpenXR.OpenXRLoader" };
                    break;
                case XrSdkMode.Pico:
                    targetXrPluginsRequired = new string[] { "Unity.XR.PXR.PXR_Loader" };
                    break;
                case XrSdkMode.Zapbox:
                    targetXrPluginsRequired = new string[] { "Zappar.XR.ZapboxLoader" };
                    break;
                default:
                    break;
            }


            m_plugins = targetSettings.Manager.activeLoaders.ToList(); // Note, copy of loaders here to avoid iterating changing container
            // Remove unwanted loaders
            foreach (var loader in m_plugins)
            {
                // If loader not in required list, remove it.
                if (!targetXrPluginsRequired.Any(s => s == loader.GetType().FullName))
                {
                    UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.RemoveLoader(targetSettings.Manager, loader.GetType().FullName, m_targetGroup);
                }
            }
            // Add any missing loaders.
            foreach (var loaderName in targetXrPluginsRequired.ToList())
            {
                if (!targetSettings.Manager.activeLoaders.Any(s => s.GetType().FullName == loaderName))
                {
                    if (!UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(targetSettings.Manager, loaderName, m_targetGroup))
                        throw new BuildFailedException($"Could not load XR plugin {loaderName}. Is it installed?");
                }
            }

            Debug.Log("Building with XR plugins: " + String.Join(", ", targetSettings.Manager.activeLoaders));
            EditorUtility.SetDirty(targetSettings);
        }

        public void Dispose()
        {
            var targetSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(m_targetGroup);
            targetSettings.InitManagerOnStart = m_xrEnabled;

            // Remove build loaders.
            foreach (var loader in targetSettings.Manager.activeLoaders.ToList())
            {
                // Remove plugins not in the original list.
                if (!m_plugins.Any(s => s.GetType() == loader.GetType()))
                {
                    UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.RemoveLoader(targetSettings.Manager, loader.GetType().FullName, m_targetGroup);
                }
            }
            // Restore any missing plugins
            foreach (var loader in m_plugins)
            {
                if (!targetSettings.Manager.activeLoaders.Any(s => s.GetType() == loader.GetType()))
                {
                    UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(targetSettings.Manager, loader.GetType().FullName, m_targetGroup);
                }
            }

            EditorUtility.SetDirty(targetSettings);
        }
    }

    class TempSetGraphicsApis : IDisposable
    {
        UnityEngine.Rendering.GraphicsDeviceType[] m_graphicsApis;

        BuildTarget m_Target;

        public TempSetGraphicsApis(TiltBuildOptions tiltOptions)
        {
            m_Target = tiltOptions.Target;
            m_graphicsApis = PlayerSettings.GetGraphicsAPIs(m_Target);
            UnityEngine.Rendering.GraphicsDeviceType[] targetGraphicsApisRequired;

            switch (tiltOptions.XrSdk)
            {
                case XrSdkMode.Pico:
                case XrSdkMode.Wave:
                    targetGraphicsApisRequired = new UnityEngine.Rendering.GraphicsDeviceType[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 };
                    break;
                default:
                    targetGraphicsApisRequired = m_graphicsApis;
                    break;
            }

            PlayerSettings.SetGraphicsAPIs(m_Target, targetGraphicsApisRequired);
        }

        public void Dispose()
        {
            PlayerSettings.SetGraphicsAPIs(m_Target, m_graphicsApis);
        }
    }

    class RestoreFileContents : IDisposable
    {
        string[] m_files;
        public RestoreFileContents(params string[] files)
        {
            m_files = files;
            foreach (var originalPath in m_files)
            {
                string backupPath = MakeBackupPath(originalPath);
                FileUtil.DeleteFileOrDirectory(backupPath);
                FileUtil.CopyFileOrDirectory(originalPath, backupPath);
            }
        }
        public void Dispose()
        {
            foreach (var originalPath in m_files)
            {
                string backupPath = MakeBackupPath(originalPath);
                FileUtil.DeleteFileOrDirectory(originalPath);
                FileUtil.MoveFileOrDirectory(backupPath, originalPath);
            }
        }

        private static string MakeBackupPath(string originalPath)
        {
            string dirName = Path.GetDirectoryName(originalPath);
            string fileName = Path.GetFileName(originalPath);
            return Path.Combine(dirName, "Temp_" + fileName);
        }
    }

    public class RestoreCurrentScene : System.IDisposable
    {
        SceneSetup[] m_scene;
        public RestoreCurrentScene()
        {
            m_scene = EditorSceneManager.GetSceneManagerSetup();
        }
        public void Dispose()
        {
            if (m_scene != null)
            {
                // force reload
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.RestoreSceneManagerSetup(m_scene);
            }
        }
    }

    // Load a scene so it can be temporarily modified in-place.
    // Upon Dispose(), scene is restored to its old state.
    // Note: does not try and restore current scene.
    class TempModifyScene : System.IDisposable
    {
        string m_scene, m_backup;
        public TempModifyScene(string sceneName)
        {
            m_scene = sceneName;
            if (!string.IsNullOrEmpty(m_scene))
            {
                m_backup = Path.Combine(Path.GetDirectoryName(sceneName),
                    "Temp_" + Path.GetFileName(sceneName));
                FileUtil.DeleteFileOrDirectory(m_backup);
                FileUtil.CopyFileOrDirectory(m_scene, m_backup);
                // force reload
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.OpenScene(m_scene);
            }
            else
            {
                m_backup = null;
            }
        }
        public void Dispose()
        {
            if (m_backup != null)
            {
                FileUtil.DeleteFileOrDirectory(m_scene);
                FileUtil.MoveFileOrDirectory(m_backup, m_scene);
            }
        }
    }

    // Set up any singletons that are needed at build time.
    // Probably needs to happen after TempModifyScene.
    public class TempHookUpSingletons : System.IDisposable
    {
        public TempHookUpSingletons()
        {
            if (Application.isPlaying) { return; }
            App.Instance = GameObject.Find("/App").GetComponent<App>();
            BrushCatalog.m_Instance = GameObject.Find("/App").GetComponent<BrushCatalog>();
            Config.m_SingletonState = GameObject.Find("/App/Config").GetComponent<Config>();
        }

        public void Dispose()
        {
            if (Application.isPlaying) { return; }
            App.Instance = null;
            BrushCatalog.m_Instance = null;
        }
    }

    /// Puts CopyRequests in streaming assets, for those platforms that can't
    /// handle loose files. App.CopySupportFiles() copies them from the .apk to the filesystem
    /// upon startup.
    class TempCopyToStreamingAssets : System.IDisposable
    {
        private string m_tempCopy;

        public static TempCopyToStreamingAssets Create(BuildTarget target, IEnumerable<CopyRequest> requests)
        {
            if (target == BuildTarget.Android)
            {
                return new TempCopyToStreamingAssets(requests.Where(r => !r.omitForAndroid).ToList());
            }
            else
            {
                // Other platforms don't need the copy into streaming assets
                return null;
            }
        }

        public TempCopyToStreamingAssets(List<CopyRequest> requests)
        {
            if (Directory.Exists(Application.streamingAssetsPath))
            {
                m_tempCopy = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName());
                SyncDirectoryTo(Application.streamingAssetsPath, m_tempCopy);
            }
            else
            {
                Directory.CreateDirectory(Application.streamingAssetsPath);
            }
            ExecuteCopyRequests(requests, App.PlatformPath(), Application.streamingAssetsPath);
        }

        public void Dispose()
        {
            if (m_tempCopy != null)
            {
                SyncDirectoryTo(m_tempCopy, Application.streamingAssetsPath);
                Directory.Delete(m_tempCopy, recursive: true);
            }
            else
            {
                Directory.Delete(Application.streamingAssetsPath, recursive: true);
            }
        }
    }

    class TempSetStereoRenderPath : IDisposable
    {
        private StereoRenderingPath m_Path;

        public TempSetStereoRenderPath(StereoRenderingPath path)
        {
            m_Path = PlayerSettings.stereoRenderingPath;
            PlayerSettings.stereoRenderingPath = path;
        }

        public void Dispose()
        {
            PlayerSettings.stereoRenderingPath = m_Path;
            AssetDatabase.SaveAssets();
        }
    }

    // [MenuItem("Open Brush/Show Brush Export Textures")]
    [UsedImplicitly]
    static void ShowBrushExportTextures()
    {
        using (var unused = new TempHookUpSingletons())
        {
            // Set consultUserConfig = false to keep user config from affecting the build output.
            TiltBrushManifest manifest = App.Instance.GetMergedManifest(forceExperimental: true);

            StringBuilder s = new StringBuilder();
            foreach (BrushDescriptor desc in manifest.UniqueBrushes())
            {
                s.Append(string.Format("Brush {0}:\n", desc.name));
                foreach (var request in desc.CopyRequests)
                {
                    s.Append(string.Format("  copy {0} -> {1}", request.source, request.dest));
                }
            }

            Debug.LogFormat("Brush export textures\n{0}", s);
        }
    }

    static string GetCommandLineLogFileSetting()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int logFileindex = Array.IndexOf(args, "-logFile");
        if (logFileindex == -1 || (logFileindex + 1) >= args.Length)
        {
            return null;
        }

        return args[logFileindex];
    }

    static string GetDefaultEditorLogFilename()
    {
#if UNITY_EDITOR_OSX
        return "~/Library/Logs/Unity/Editor.log";
#else
        string localAppData = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, @"Unity\Editor\Editor.log");
#endif
    }

    // Note: calling this will change the current scene, discarding any unsaved changes.
    // Save the scene before calling, and consider also using RestoreCurrentScene().
    //
    // Things that DoBuild does that Unity GUI build does not:
    // - Sanity-checking of brush GUID and LUIDs
    // - Tweaks inspector values to enable/disable obfuscation and experimental
    // - Copies additional files into the build (brush textures for export)
    //
    public static void DoBuild(TiltBuildOptions tiltOptions)
    {
        BuildTarget target = tiltOptions.Target;
        string location = tiltOptions.Location;
        string stamp = tiltOptions.Stamp;
        XrSdkMode xrSdk = tiltOptions.XrSdk;
        BuildOptions options = tiltOptions.UnityOptions;

        m_buildStatus = "Started build";

        // Add your new scenes in this List for your app.
        // During the build process the Scene List in the Build Settings is ignored.
        // Only the following scenes are included in the build.
        string[] scenes = { "Assets/Scenes/Loading.unity", "Assets/Scenes/Main.unity" };
        Note("BuildTiltBrush: Start target:{0} mode:{1} profile:{2} options:{3}",
            target, xrSdk, tiltOptions.AutoProfile,
            // For some reason, "None" comes through as "CompressTextures"
            options == BuildOptions.None ? "None" : options.ToString());

        var copyRequests = new List<CopyRequest>(kToCopy);

        // It's important here for Main.unity (currently scenes[1]) to be the last scene
        // "temp modified".  TempModifyScene opens the scene and if Main.unity is not the open
        // scene when TempHookUpSingletons runs, the build will fail.
        using (var unused = new TempModifyScene(scenes[0]))
        using (var unused12 = new TempModifyScene(scenes[1]))
        using (var unused11 = new TempSetStereoRenderPath(target == BuildTarget.Android
            ? StereoRenderingPath.SinglePass : StereoRenderingPath.MultiPass))
        using (var unused3 = new TempDefineSymbols(
            target,
            tiltOptions.Il2Cpp ? "DISABLE_AUDIO_CAPTURE" : null,
            tiltOptions.AutoProfile ? "AUTOPROFILE_ENABLED" : null))
        using (var unused4 = new TempHookUpSingletons())
        using (var unused5 = new TempSetScriptingBackend(target, tiltOptions.Il2Cpp))
        using (var unused14 = new TempSetGraphicsApis(tiltOptions))
        using (var unused6 = new TempSetBundleVersion(target, App.Config.m_VersionNumber, stamp))
        using (var unused10 = new TempSetAppNames(target, tiltOptions.Description))
        using (var unused7 = new TempSetXrPlugin(tiltOptions))
        using (var unused15 = new TempSetPlayerSettings(tiltOptions))
        using (var unused13 = new TempSetOpenXrFeatureGroup(tiltOptions))
        using (var unused9 = new RestoreFileContents(
            Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "ProjectSettings/GraphicsSettings.asset")))
        {
            var config = App.Config;
            config.m_SdkMode = SdkMode.UnityXR;
            config.m_AutoProfile = tiltOptions.AutoProfile;
            config.m_BuildStamp = stamp;
            //config.OnValidate(xrSdk, TargetToGroup(target));
            config.DoBuildTimeConfiguration(target, tiltOptions.disableAccountLogins);
            EditorUtility.SetDirty(config);

            if (GuiSelectedBuildTarget == BuildTarget.Android)
            {
                if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                {
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    Debug.Log("Set Android architecture to ARM64.");
                }
            }

            // Some mildly-hacky shenanigans here; GetMergedManifest() doesn't expect
            // to be run at build-time (ie when nobody has called Start(), Awake()).
            // TempHookupSingletons() has done just enough initialization to make it happy.
            // Also set consultUserConfig = false to keep user config from affecting the build output.
            TiltBrushManifest manifest = App.Instance.GetMergedManifest(forceExperimental: true);

            // Some sanity checks
            {
                m_buildStatus = "Checking integrity";

                var errors = new List<string>();
                var locallyUniqueIds = new Dictionary<string, BrushDescriptor>();
                var globallyUniqueIds = new Dictionary<System.Guid, BrushDescriptor>();

                foreach (var desc in manifest.UniqueBrushes())
                {
                    // TODO: change this to an explicit inspector field
                    // Need to also update the fbx, json export code; and the code
                    // (not yet landed) that exports info to the Tilt Brush Toolkit
                    string luid = desc.name;

                    try
                    {
                        locallyUniqueIds.Add(luid, desc);
                    }
                    catch (ArgumentException)
                    {
                        errors.Add(string.Format(
                            "Brush {0} and {1} have same LUID {2}",
                            desc.name, locallyUniqueIds[luid].name, luid));
                    }
                    try
                    {
                        globallyUniqueIds.Add(desc.m_Guid, desc);
                    }
                    catch (ArgumentException)
                    {
                        errors.Add(string.Format(
                            "Brush {0} and {1} have same GUID {2}",
                            desc.name, globallyUniqueIds[desc.m_Guid].name, desc.m_Guid));
                    }
                }

                // Don't care so much about experimental brush version numbers;
                // only check the brushes that go into the prod build.
                TiltBrushManifest productionManifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>(
                    "Assets/Manifest.asset");

                foreach (var desc in productionManifest.UniqueBrushes())
                {
                    if (string.IsNullOrEmpty(desc.m_CreationVersion))
                    {
                        errors.Add(string.Format("Brush {0} has empty m_CreationVersion", desc.name));
                    }
                    if (string.IsNullOrEmpty(desc.m_DurableName))
                    {
                        errors.Add(string.Format("Brush {0} has empty m_DurableName", desc.name));
                    }
                }

                if (errors.Any())
                {
                    throw new BuildFailedException(
                        string.Format("Build sanity checks failed:\n{0}",
                            string.Join("\n", errors.ToArray())));
                }
                // b/139746720
                {
                    foreach (var asset in new[]
                    {
                        "Assets/ThirdParty/Oculus/LipSync/Scripts/OVRLipSyncMicInput.cs",
                        "Assets/ThirdParty/Oculus/Platform/Scripts/MicrophoneInput.cs",
                    })
                    {
                        // For some reason AssetPathToGUID() still returns a guid even after the
                        // files are deleted :-P. So use the filesystem I guess?
                        if (File.Exists(asset))
                        {
                            throw new BuildFailedException(
                                string.Format("{0} not allowed in build", asset));
                        }
                    }
                }
            }

            var supportBrushTexturesRequests = new GlTFEditorExporter.ExportRequests();
            foreach (BrushDescriptor desc in manifest.UniqueBrushes())
            {
                copyRequests.AddRange(desc.CopyRequests);
                GlTFEditorExporter.ExportBrush(supportBrushTexturesRequests, desc,
                    ExportUtils.kProjectRelativeSupportBrushTexturesRoot);
            }
            foreach (var exportRequest in supportBrushTexturesRequests.exports)
            {
                copyRequests.Add(new CopyRequest(exportRequest.source, exportRequest.destination));
            }

            // Save our changes and notify the editor that there have been changes.
            m_buildStatus = "Saving scene";
            EditorSceneManager.SaveOpenScenes();

            // If we're building android, we need to copy Support files into streaming assets
            m_buildStatus = "Copying platform support files";
            using (var unused8 = TempCopyToStreamingAssets.Create(target, copyRequests))
            {
                // When the editor log has not been redirected with -logFile, we copy the appropriate part
                // of the editor log to Build.log. Because the editor log may have data from many sessions,
                // we store the current length of the log before the build kicks off, and then only copy
                // across that which was added to the editor log after that.
                string editorLog = GetDefaultEditorLogFilename();
                long startEditorLength = 0;
                bool copyBuildLog = GetCommandLineLogFileSetting() == null;
                if (copyBuildLog)
                {
                    if (File.Exists(editorLog))
                    {
                        startEditorLength = new FileInfo(editorLog).Length;
                    }
                    else
                    {
                        copyBuildLog = false;
                    }
                }
                string buildDirectory = Path.GetDirectoryName(location);
                Directory.CreateDirectory(buildDirectory);
                m_forPostBuild = new PostBuildInfo
                {
                    xrSdk = xrSdk,
                    copyRequests = copyRequests
                };

                // Some information on what we are building
                var buildDesc = $"Building player: {target}";
                if (target == BuildTarget.Android)
                {
                    buildDesc += $", {PlayerSettings.Android.targetArchitectures}";
                }
                m_buildStatus = buildDesc;

                // Start building
                var thing = BuildPipeline.BuildPlayer(scenes, location, target, options);
                string error = FormatBuildReport(thing);
                if (!string.IsNullOrEmpty(error))
                {
                    string message = $"BuildPipeline.BuildPlayer() returned: \"{error}\"";
                    Note(message);
                    m_buildStatus = $"Build player failed: {error}";
                    throw new BuildFailedException(message);
                }
                else
                {
                    Note("BuildTiltBrush: End");
                    EditorPrefs.SetString("LastTiltBrushBuildLocation", location);
                }

                // Now copy across the tail end of the editor log to Build.log, if required.
                if (copyBuildLog)
                {
                    m_buildStatus = "Copying log";
                    string logPath = Path.Combine(buildDirectory, "Build.log");
                    File.Delete(logPath);
                    using (StreamWriter logWriter = new StreamWriter(logPath))
                    {
                        using (FileStream logReader =
                            File.Open(editorLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            logReader.Seek(startEditorLength, SeekOrigin.Begin);
                            using (StreamReader readStream = new StreamReader(logReader))
                            {
                                logWriter.Write(readStream.ReadToEnd());
                                readStream.Close();
                            }

                            logReader.Close();
                        }

                        logWriter.Close();
                    }
                }
            }
        }

        // At the end of a GUI build, the in-memory value of VR::enabledDevices
        // is correct, but the on-disk value is not. "File -> Save Project"
        // flushes the change to disk, but I can't find a way to do that
        // programmatically. Either AssetDatabase.SaveAssets() doesn't also
        // save ProjectSettings.asset; or doing it here isn't late enough.
        // AssetDatabase.SaveAssets();

        m_buildStatus = "Finished";
    }

    // Get XR Plugins for selected build target.
    public static string GetXrPlugins()
    {
        var grp = BuildTiltBrush.TargetToGroup(GuiSelectedBuildTarget);

        XRGeneralSettings settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(grp);
        if (settings == null)
            return "Not using XR";

        var count = settings.Manager.activeLoaders.Count;
        if (count == 0)
            return "No XR plugins selected";

        string res = "";
        for (int i = 0; i < settings.Manager.activeLoaders.Count; ++i)
        {
            if (i > 0)
                res += ", ";
            res += settings.Manager.activeLoaders[i].name + $" ({settings.Manager.activeLoaders[i].GetType().FullName})";
        }
        return res;
    }

    // Returns null if no errors; otherwise a string with what went wrong.
    private static string FormatBuildStep(BuildStep step)
    {
        var errors = step.messages
            .Where(m => (m.type == LogType.Error || m.type == LogType.Exception))
            .Select(m => m.content)
            .ToArray();
        if (errors.Length > 0)
        {
            return step.name + "\n" + string.Join("\n", errors.Select(s => "  " + s).ToArray());
        }
        else
        {
            return null;
        }
    }

    // Returns null if no errors; otherwise a string with what went wrong.
    private static string FormatBuildReport(BuildReport report)
    {
        if (report.summary.result == BuildResult.Succeeded)
        {
            return null;
        }
        var steps = report.steps.Select(FormatBuildStep).Where(s => s != null);
        return "Errors:\n" + string.Join("\n", steps.ToArray());
    }

    // Disables the Oculus resolution-setting override for non-Oculus builds.
    // Copies loose-file app data.
    [UnityEditor.Callbacks.PostProcessBuildAttribute(2)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        PostBuildInfo info = m_forPostBuild;
        m_forPostBuild = null;

        string dataDir = path.Replace(".exe", "_Data"); // eg TiltBrush_Data
        string looseFilesDest;
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                {
                    // Not used right now
                    looseFilesDest = Path.GetDirectoryName(path);

                    break;
                }
            case BuildTarget.iOS:
                {
                    // TODO: is it possible to embed loose files on iOS?
                    looseFilesDest = null;
#if UNITY_IOS
                    string pbxPath = path + "/Unity-iPhone.xcodeproj/project.pbxproj";

                    PBXProject project = new PBXProject();
                    project.ReadFromString(File.ReadAllText(pbxPath));
                    string pbxTarget = project.GetUnityMainTargetGuid();

                    // additional framework libs
                    project.AddFrameworkToProject(pbxTarget, "Security.framework", false);
                    project.AddFrameworkToProject(pbxTarget, "CoreData.framework", false);
                    // disable bitcode due to issue with Cardboard plugin (b/27129333)
                    // TODO:Mikesky - I've disabled this disable, does bitcode work now?
                    //project.SetBuildProperty(pbxTarget, "ENABLE_BITCODE", "false");

                    File.WriteAllText(pbxPath, project.WriteToString());

                    string plistPath = path + "/Info.plist";
                    PlistDocument plist = new PlistDocument();
                    plist.ReadFromFile(plistPath);
                    PlistElementDict root = plist.root;

                    PlistElementBoolean enable = new (true);
                    root["UIFileSharingEnabled"] = enable;
                    root["LSSupportsOpeningDocumentsInPlace"] = enable;

                    //save plist values
                    plist.WriteToFile(plistPath);
#endif
                    break;
                }
            case BuildTarget.StandaloneOSX:
                looseFilesDest = Path.GetDirectoryName(path);
                break;

            default:
                looseFilesDest = null;
                break;
        }

        // Copy loose files next to the app.

        if (looseFilesDest == null)
        {
            // This is OK on Android; we put things in Streaming Assets
            if (target != BuildTarget.Android)
            {
                Note("WARNING: Don't know how to copy loose data on this platform.");
            }
        }
        else
        {
            // In the editor, Application.dataPath is the Assets/ folder
            string sourceBase = Path.GetDirectoryName(Application.dataPath);
            ExecuteCopyRequests(info.copyRequests, sourceBase, looseFilesDest);
        }
    }

    private static void ExecuteCopyRequests(List<CopyRequest> requests, string sourceBase,
                                            string destBase)
    {
        foreach (var copyRequest in requests)
        {
            string copySource = Path.Combine(sourceBase, copyRequest.source);
            string copyDest = Path.Combine(destBase, copyRequest.dest);
            if (MaybeSameFile(copySource, copyDest))
            {
                // Maybe die instead? The build process has the right to destroy
                // any file in the output directory, so it's not really safe to
                // build into an already-populated source directory.
                continue;
            }

            // Overwrite result if it exists
            if (File.Exists(copyDest))
            {
                new FileInfo(copyDest).Delete();
            }
            else if (Directory.Exists(copyDest))
            {
                new DirectoryInfo(copyDest).Delete(true);
            }

            CopyRecursive(copySource, copyDest);
        }
    }

    // This is a difficult problem in general. This version is prone
    // to false-negatives. On case-sensitive filesystems, it might
    // give false-positives as well.
    private static bool MaybeSameFile(string file1, string file2)
    {
        string full1 = Path.GetFullPath(file1);
        string full2 = Path.GetFullPath(file2);
        return string.Equals(full1, full2, System.StringComparison.OrdinalIgnoreCase);
    }

    // Copy file or directory from source to dest.
    // dest must not already exist.
    private static void CopyRecursive(string source, string dest)
    {
        if (File.Exists(source))
        {
            if (source.EndsWith(".meta"))
            {
                return;
            }
            if (!Directory.Exists(Path.GetDirectoryName(dest)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
            }
            new FileInfo(source).CopyTo(dest);
        }
        else if (Directory.Exists(source))
        {
            Directory.CreateDirectory(dest);
            DirectoryInfo dir = new DirectoryInfo(source);
            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Extension == ".meta")
                {
                    continue;
                }
                file.CopyTo(Path.Combine(dest, file.Name), false);
            }
            foreach (DirectoryInfo subdir in dir.GetDirectories())
            {
                CopyRecursive(subdir.FullName, Path.Combine(dest, subdir.Name));
            }
        }
        else
        {
            throw new BuildFailedException(string.Format("Cannot copy nonexistent '{0}'", source));
        }
    }

    static void Note(string msg, params System.Object[] args)
    {
        // TODO: Is there a way to get this to stdout somehow?
        if (args != null && args.Length > 0)
        {
            msg = string.Format(msg, args);
        }
        Debug.LogFormat("_btb_ {0}", msg);
    }

    static void Die(int exitCode, string msg = null, params System.Object[] args)
    {
        // TODO: Is there a way to get this to stdout somehow?
        if (msg != null)
        {
            Debug.LogErrorFormat("_btb_ Abort <<{0}>>", string.Format(msg, args));
        }
        EditorApplication.Exit(exitCode);
    }

    /// Copies all the project files to a build directory to allow it to build
    /// using another instance of Unity.
    private static void SyncProjectToBuildCopy()
    {
        var projectDir = new DirectoryInfo(Application.dataPath).Parent;
        var rootCopyDir = projectDir.CreateSubdirectory(kBuildCopyDir);
        try
        {
            for (int i = 0; i < kBuildDirs.Length; ++i)
            {
                float progress = i / (float)kBuildDirs.Length;
                if (EditorUtility.DisplayCancelableProgressBar("Syncing", "Syncing Project to build dir.",
                    progress))
                {
                    return;
                }
                string source = Path.Combine(projectDir.FullName, kBuildDirs[i]);
                string destination = Path.Combine(rootCopyDir.FullName, kBuildDirs[i]);
                SyncDirectoryTo(source, destination);
            }
            SyncDirectoryTo(projectDir.FullName, rootCopyDir.FullName, subdirs: false);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void SyncDirectoryTo(string source, string destination, bool subdirs = true)
    {
#if UNITY_EDITOR_WIN
        string args = string.Format("\"{0}\" \"{1}\" {2} /PURGE",
            source, destination, subdirs ? "/E" : "");
        string copyexe = "robocopy.exe";
#else
        string args = string.Format("\"{0}/\" \"{1}/\" {2} --delete",
                                    source, destination, subdirs ? "-a" : "-d");
        string copyexe = "rsync";
#endif

        var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo(copyexe, args);
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        process.WaitForExit();
    }

    /// Syncs, then kicks off another build in the background.
    /// If continuation is passed, it is called with the exit code when the process exits.
    public static int s_NumBackgroundBuilds = 0;

    private static int s_BackgroundBuildProcessId = 0;

    public static bool DoingBackgroundBuild => s_BackgroundBuildProcessId != 0;

    private const string kBackgroundProcessId = "Tilt Brush Background Build Process Id";

    public static string BackgroundBuildLogPath => Path.Combine(kBuildCopyDir, "BackgroundBuild.log");

    public static void DoBackgroundBuild(TiltBuildOptions tiltOptions, bool interactive)
    {
        SyncProjectToBuildCopy();

        // assume the continuation is enough notification for the user

        BuildTarget target = tiltOptions.Target;
        string location = tiltOptions.Location;
        string stamp = tiltOptions.Stamp;
        XrSdkMode xrSdk = tiltOptions.XrSdk;
        BuildOptions options = tiltOptions.UnityOptions;
        var projectDir = new DirectoryInfo(Application.dataPath).Parent;
        var rootCopyDir = projectDir.CreateSubdirectory(kBuildCopyDir);
        StringBuilder args = new StringBuilder();
        string logFile = Path.Combine(rootCopyDir.FullName, "BackgroundBuild.log");
        FileUtil.DeleteFileOrDirectory(logFile);
#if UNITY_EDITOR_OSX
        args.AppendFormat("--args ");
#endif
        args.AppendFormat("-logFile {0} ", logFile);
        if (!interactive) { args.Append("-batchmode "); }
        args.AppendFormat("-projectpath {0} ", rootCopyDir.FullName);
        args.Append("-executemethod BuildTiltBrush.CommandLine ");
        args.AppendFormat("-btb-display {0} ", xrSdk);
        args.AppendFormat("-btb-target {0} ", target);
        foreach (var value in Enum.GetValues(typeof(BuildOptions)))
        {
            if (((int)options & (int)value) != 0)
            {
                args.AppendFormat("-btb-bopt {0} ", value);
            }
        }
        if (tiltOptions.Il2Cpp) { args.Append("-btb-il2cpp "); }
        if (!string.IsNullOrEmpty(stamp)) { args.AppendFormat("-btb-stamp {0} ", stamp); }
        if (tiltOptions.AutoProfile) { args.Append("-btb-autoprofile "); }
        args.AppendFormat("-btb-out {0} ", location);
        if (!interactive) { args.Append("-quit "); }

        var process = new System.Diagnostics.Process();
        StringBuilder unityPath = new StringBuilder();
        unityPath.AppendFormat(EditorApplication.applicationPath);
#if UNITY_EDITOR_OSX
        // We want to run the inner Unity executable, not the GUI wrapper
        unityPath.AppendFormat("/Contents/MacOS/Unity");
#endif
        process.StartInfo = new System.Diagnostics.ProcessStartInfo(unityPath.ToString(), args.ToString());
        DetectBackgroundProcessExit(process);
        process.Start();
        s_BackgroundBuildProcessId = process.Id;
        EditorPrefs.SetInt(kBackgroundProcessId, s_BackgroundBuildProcessId);
        s_NumBackgroundBuilds += 1;
    }

    [DidReloadScripts]
    private static void OnScriptReload()
    {
        s_BackgroundBuildProcessId = EditorPrefs.GetInt(kBackgroundProcessId);
        if (s_BackgroundBuildProcessId != 0)
        {
            System.Diagnostics.Process process;
            try
            {
                process = System.Diagnostics.Process.GetProcessById(s_BackgroundBuildProcessId);
                if (process.ProcessName.Contains("Unity"))
                {
                    DetectBackgroundProcessExit(process);
                }
                else
                {
                    s_BackgroundBuildProcessId = 0;
                }
            }
            catch (ArgumentException)
            {
                s_BackgroundBuildProcessId = 0;
            }
            catch (InvalidOperationException)
            {
                // If process has exited already, process.ProcessName is unreadable.
                // Maybe we could be smarter here and close some timing holes, but this at least
                // avoids a console error on each subsequent script reload.
                EditorPrefs.DeleteKey(kBackgroundProcessId);
                s_BackgroundBuildProcessId = 0;
            }
        }
    }

    private static void DetectBackgroundProcessExit(System.Diagnostics.Process process)
    {
        process.EnableRaisingEvents = true;
        process.Exited += (sender, eventArgs) =>
        {
            s_NumBackgroundBuilds -= 1;
            s_BackgroundBuildProcessId = 0;
            int code = ((System.Diagnostics.Process)sender).ExitCode;
            // Schedule it on the correct thread
            EditorApplication.delayCall += () =>
            {
                EditorPrefs.DeleteKey(kBackgroundProcessId);
                if (OnBackgroundBuildFinish != null)
                {
                    OnBackgroundBuildFinish.Invoke(code);
                }
            };
        };
    }

    public static void TerminateBackgroundBuild()
    {
        if (DoingBackgroundBuild)
        {
            var process = System.Diagnostics.Process.GetProcessById(s_BackgroundBuildProcessId);
            if (process != null)
            {
                process.Kill();
                s_NumBackgroundBuilds -= 1;
                s_BackgroundBuildProcessId = 0;
            }
        }
    }

}
