/************************************************************************************

Copyright   :   Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Licensed under the Oculus SDK License Version 3.4.1 (the "License");
you may not use the Oculus SDK except in compliance with the License,
which is provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

https://developer.oculus.com/licenses/sdk-3.4.1

Unless required by applicable law or agreed to in writing, the Oculus SDK
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

************************************************************************************/

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;

[InitializeOnLoad]
class OVRPluginUpdater
{
	enum PluginPlatform
	{
		Android,
		AndroidUniversal,
		OSXUniversal,
		Win,
		Win64,
	}
	class PluginPackage
	{
		public string RootPath;
		public System.Version Version;
		public Dictionary<PluginPlatform, string> Plugins = new Dictionary<PluginPlatform, string>();

		public bool IsBundledPluginPackage()
		{
			return (RootPath == GetBundledPluginRootPath());
		}

		public bool IsEnabled()
		{
			// TODO: Check each individual platform rather than using the Win64 DLL status for the overall package status.
			string path = "";
			if (Plugins.TryGetValue(PluginPlatform.Win64, out path))
			{
				return File.Exists(path);
			}

			return false;
		}

		public bool IsAndroidUniversalEnabled()
		{
			string path = "";
			if (Plugins.TryGetValue(PluginPlatform.AndroidUniversal, out path))
			{
				if (File.Exists(path))
				{
					string basePath = GetCurrentProjectPath();
					string relPath = path.Substring(basePath.Length + 1);

					PluginImporter pi = PluginImporter.GetAtPath(relPath) as PluginImporter;
					if (pi != null)
					{
						return pi.GetCompatibleWithPlatform(BuildTarget.Android);
					}
				}
			}

			return false;
		}

		public bool IsAndroidUniversalPresent()
		{
			string path = "";
			if (Plugins.TryGetValue(PluginPlatform.AndroidUniversal, out path))
			{
				string disabledPath = path + GetDisabledPluginSuffix();

				if (File.Exists(path) || File.Exists(disabledPath))
				{
					return true;
				}
			}

			return false;
		}
	}

	private static bool restartPending = false;
	private static bool unityRunningInBatchmode = false;
	private static bool unityVersionSupportsAndroidUniversal = false;
	private static bool enableAndroidUniversalSupport = true;

	private static System.Version invalidVersion = new System.Version("0.0.0");

	static OVRPluginUpdater()
	{
		EditorApplication.delayCall += OnDelayCall;
	}

	static void OnDelayCall()
	{
		if (System.Environment.CommandLine.Contains("-batchmode"))
		{
			unityRunningInBatchmode = true;
		}
 
		if (enableAndroidUniversalSupport)
		{
#if UNITY_2018_3_OR_NEWER
			unityVersionSupportsAndroidUniversal = true;
#endif
		}

		if (ShouldAttemptPluginUpdate())
		{
			AttemptPluginUpdate(true);
		}
	}

	private static PluginPackage GetPluginPackage(string rootPath)
	{
		return new PluginPackage()
		{
			RootPath = rootPath,
			Version = GetPluginVersion(rootPath),
			Plugins = new Dictionary<PluginPlatform, string>()
			{
				{ PluginPlatform.Android,          rootPath + GetPluginBuildTargetSubPath(PluginPlatform.Android)          },
				{ PluginPlatform.AndroidUniversal, rootPath + GetPluginBuildTargetSubPath(PluginPlatform.AndroidUniversal) },
				{ PluginPlatform.OSXUniversal,     rootPath + GetPluginBuildTargetSubPath(PluginPlatform.OSXUniversal)     },
				{ PluginPlatform.Win,              rootPath + GetPluginBuildTargetSubPath(PluginPlatform.Win)              },
				{ PluginPlatform.Win64,            rootPath + GetPluginBuildTargetSubPath(PluginPlatform.Win64)            },
			}
		};
	}

	private static PluginPackage GetBundledPluginPackage()
	{
		return GetPluginPackage(GetBundledPluginRootPath());
	}

	private static List<PluginPackage> GetAllUtilitiesPluginPackages()
	{
		string pluginRootPath = GetUtilitiesPluginRootPath();
		List<PluginPackage> packages = new List<PluginPackage>();

		if (Directory.Exists(pluginRootPath))
		{
			var dirs = Directory.GetDirectories(pluginRootPath);

			foreach(string dir in dirs)
			{
				packages.Add(GetPluginPackage(dir));
			}
		}

		return packages;
	}

	private static string GetCurrentProjectPath()
	{
		return Directory.GetParent(Application.dataPath).FullName;
	}

	private static string GetUtilitiesPluginRootPath()
	{
		return GetUtilitiesRootPath() + @"/Plugins";
	}

	private static string GetUtilitiesRootPath()
	{
		var so = ScriptableObject.CreateInstance(typeof(OVRPluginUpdaterStub));
		var script = MonoScript.FromScriptableObject(so);
		string assetPath = AssetDatabase.GetAssetPath(script);
		string editorDir = Directory.GetParent(assetPath).FullName;
		string ovrDir = Directory.GetParent(editorDir).FullName;

		return ovrDir;
	}

	private static string GetBundledPluginRootPath()
	{
		string basePath = EditorApplication.applicationContentsPath;
		string pluginPath = @"/UnityExtensions/Unity/VR";

		return basePath + pluginPath;
	}

	private static string GetPluginBuildTargetSubPath(PluginPlatform target)
	{
		string path = string.Empty;

		switch (target)
		{
			case PluginPlatform.Android:
				path = @"/Android/OVRPlugin.aar";
				break;
			case PluginPlatform.AndroidUniversal:
				path = @"/AndroidUniversal/OVRPlugin.aar";
				break;
			case PluginPlatform.OSXUniversal:
				path = @"/OSXUniversal/OVRPlugin.bundle";
				break;
			case PluginPlatform.Win:
				path = @"/Win/OVRPlugin.dll";
				break;
			case PluginPlatform.Win64:
				path = @"/Win64/OVRPlugin.dll";
				break;
			default:
				throw new ArgumentException("Attempted GetPluginBuildTargetSubPath() for unsupported BuildTarget: " + target);
		}

		return path;
	}

	private static string GetDisabledPluginSuffix()
	{
		return @".disabled";
	}

	private static System.Version GetPluginVersion(string path)
	{
		System.Version pluginVersion = invalidVersion;

		try
		{
			pluginVersion = new System.Version(Path.GetFileName(path));
		}
		catch
		{
			pluginVersion = invalidVersion;
		}

		if (pluginVersion == invalidVersion)
		{
			//Unable to determine version from path, fallback to Win64 DLL meta data
			path += GetPluginBuildTargetSubPath(PluginPlatform.Win64);
			if (!File.Exists(path))
			{
				path += GetDisabledPluginSuffix();
				if (!File.Exists(path))
				{
					return invalidVersion;
				}
			}

			FileVersionInfo pluginVersionInfo = FileVersionInfo.GetVersionInfo(path);
			if (pluginVersionInfo == null || pluginVersionInfo.ProductVersion == null || pluginVersionInfo.ProductVersion == "")
			{
				return invalidVersion;
			}

			pluginVersion = new System.Version(pluginVersionInfo.ProductVersion);
		}

		return pluginVersion;
	}

	public static string GetVersionDescription(System.Version version)
	{
		bool isVersionValid = (version != invalidVersion);
		return isVersionValid ? version.ToString() : "(Unknown)";
	}

	private static bool ShouldAttemptPluginUpdate()
	{
		if (unityRunningInBatchmode)
		{
			return false;
		}
		else
		{
			return !UnitySupportsEnabledAndroidPlugin() || (autoUpdateEnabled && !restartPending && !Application.isPlaying);
		}
	}

	private static void DisableAllUtilitiesPluginPackages()
	{
		List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

		foreach(PluginPackage pluginPkg in allUtilsPluginPkgs)
		{
			foreach(string path in pluginPkg.Plugins.Values)
			{
				if ((Directory.Exists(path)) || (File.Exists(path)))
				{
					string basePath = GetCurrentProjectPath();
					string relPath = path.Substring(basePath.Length + 1);
					string relDisabledPath = relPath + GetDisabledPluginSuffix();

					AssetDatabase.MoveAsset(relPath, relDisabledPath);
					AssetDatabase.ImportAsset(relDisabledPath, ImportAssetOptions.ForceUpdate);
				}
			}
		}

		AssetDatabase.Refresh();
		AssetDatabase.SaveAssets();
	}

	private static void EnablePluginPackage(PluginPackage pluginPkg)
	{
		foreach(var kvp in pluginPkg.Plugins)
		{
			PluginPlatform platform = kvp.Key;
			string path = kvp.Value;

			if ((Directory.Exists(path + GetDisabledPluginSuffix())) || (File.Exists(path + GetDisabledPluginSuffix())))
			{
				string basePath = GetCurrentProjectPath();
				string relPath = path.Substring(basePath.Length + 1);
				string relDisabledPath = relPath + GetDisabledPluginSuffix();

				AssetDatabase.MoveAsset(relDisabledPath, relPath);
				AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);

				PluginImporter pi = PluginImporter.GetAtPath(relPath) as PluginImporter;
				if (pi == null)
				{
					continue;
				}

				// Disable support for all platforms, then conditionally enable desired support below
				pi.SetCompatibleWithEditor(false);
				pi.SetCompatibleWithAnyPlatform(false);
				pi.SetCompatibleWithPlatform(BuildTarget.Android, false);
				pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
				pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
#if UNITY_2017_3_OR_NEWER
				pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
#else
				pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXUniversal, false);
				pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXIntel, false);
				pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXIntel64, false);
#endif

				switch (platform)
				{
					case PluginPlatform.Android:
						pi.SetCompatibleWithPlatform(BuildTarget.Android, !unityVersionSupportsAndroidUniversal);
						if (!unityVersionSupportsAndroidUniversal)
						{
							pi.SetPlatformData(BuildTarget.Android, "CPU", "ARMv7");
						}
						break;
					case PluginPlatform.AndroidUniversal:
						pi.SetCompatibleWithPlatform(BuildTarget.Android, unityVersionSupportsAndroidUniversal);
						break;
					case PluginPlatform.OSXUniversal:
#if UNITY_2017_3_OR_NEWER
						pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
#else
						pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXUniversal, true);
						pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXIntel, true);
						pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXIntel64, true);
#endif
						pi.SetCompatibleWithEditor(true);
						pi.SetEditorData("CPU", "AnyCPU");
						pi.SetEditorData("OS", "OSX");
						pi.SetPlatformData("Editor", "CPU", "AnyCPU");
						pi.SetPlatformData("Editor", "OS", "OSX");
						break;
					case PluginPlatform.Win:
						pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, true);
						pi.SetCompatibleWithEditor(true);
						pi.SetEditorData("CPU", "X86");
						pi.SetEditorData("OS", "Windows");
						pi.SetPlatformData("Editor", "CPU", "X86");
						pi.SetPlatformData("Editor", "OS", "Windows");
						break;
					case PluginPlatform.Win64:
						pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
						pi.SetCompatibleWithEditor(true);
						pi.SetEditorData("CPU", "X86_64");
						pi.SetEditorData("OS", "Windows");
						pi.SetPlatformData("Editor", "CPU", "X86_64");
						pi.SetPlatformData("Editor", "OS", "Windows");
						break;
					default:
						throw new ArgumentException("Attempted EnablePluginPackage() for unsupported BuildTarget: " + platform);
				}

				AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);
			}
		}

		AssetDatabase.Refresh();
		AssetDatabase.SaveAssets();
	}

	private static readonly string autoUpdateEnabledKey = "Oculus_Utilities_OVRPluginUpdater_AutoUpdate_" + OVRManager.utilitiesVersion;
	private static bool autoUpdateEnabled
	{
		get {
			return PlayerPrefs.GetInt(autoUpdateEnabledKey, 1) == 1;
		}

		set {
			PlayerPrefs.SetInt(autoUpdateEnabledKey, value ? 1 : 0);
		}
	}

	[MenuItem("Oculus/Tools/Disable OVR Utilities Plugin")]
	private static void AttemptPluginDisable()
	{
		PluginPackage bundledPluginPkg = GetBundledPluginPackage();
		List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

		PluginPackage enabledUtilsPluginPkg = null;

		foreach(PluginPackage pluginPkg in allUtilsPluginPkgs)
		{
			if (pluginPkg.IsEnabled())
			{
				if ((enabledUtilsPluginPkg == null) || (pluginPkg.Version > enabledUtilsPluginPkg.Version))
				{
					enabledUtilsPluginPkg = pluginPkg;
				}
			}
		}

		if (enabledUtilsPluginPkg == null)
		{
			if (unityRunningInBatchmode
#if UNITY_2018_3_OR_NEWER
				|| EditorUtility.DisplayDialog("Disable Oculus Utilities Plugin",
					"The OVRPlugin included with Oculus Utilities is already disabled."
						+ " The OVRPlugin installed through the Package Manager will continue to be used.\n",
					"Ok",
					""))
#else
				|| EditorUtility.DisplayDialog("Disable Oculus Utilities Plugin",
					"The OVRPlugin included with Oculus Utilities is already disabled."
						+ " The OVRPlugin bundled with the Unity Editor will continue to be used.\n\n"
						+ "Bundled version: "
						+ GetVersionDescription(bundledPluginPkg.Version),
					"Ok",
					""))
#endif
			{
				return;
			}
		}
		else
		{
			if (unityRunningInBatchmode
#if UNITY_2018_3_OR_NEWER
				|| EditorUtility.DisplayDialog("Disable Oculus Utilities Plugin",
					"Do you want to disable the OVRPlugin included with Oculus Utilities and revert to the OVRPlugin installed through the Package Manager?\n\n"
						+ "Current version: " + GetVersionDescription(enabledUtilsPluginPkg.Version),
					"Yes",
					"No"))
#else
				|| EditorUtility.DisplayDialog("Disable Oculus Utilities Plugin",
					"Do you want to disable the OVRPlugin included with Oculus Utilities and revert to the OVRPlugin bundled with the Unity Editor?\n\n"
						+ "Current version: " + GetVersionDescription(enabledUtilsPluginPkg.Version)
						+ "\nBundled version: " + GetVersionDescription(bundledPluginPkg.Version),
					"Yes",
					"No"))
#endif
			{
				DisableAllUtilitiesPluginPackages();

				if (unityRunningInBatchmode
#if UNITY_2018_3_OR_NEWER
					|| EditorUtility.DisplayDialog("Restart Unity",
						"Now you will be using the OVRPlugin installed through Package Manager."
							+ "\n\nPlease restart the Unity Editor to complete the update process.",
						"Restart",
						"Not Now"))
#else
					|| EditorUtility.DisplayDialog("Restart Unity",
						"OVRPlugin has been updated to "
							+ GetVersionDescription(bundledPluginPkg.Version)
							+ ".\n\nPlease restart the Unity Editor to complete the update process."
#if !UNITY_2017_1_OR_NEWER
							+ " You may need to manually relaunch Unity if you are using Unity 5.6 and higher."
#endif
							,
						"Restart",
						"Not Now"))
#endif
				{
					RestartUnityEditor();
				}
			}
		}
	}

	[MenuItem("Oculus/Tools/Update OVR Utilities Plugin")]
	private static void RunPluginUpdate()
	{
		autoUpdateEnabled = true;
		AttemptPluginUpdate(false);
	}

	// Separate entry point needed since "-executeMethod" does not support parameters or default parameter values
	private static void BatchmodePluginUpdate()
	{
		OnDelayCall(); // manually invoke when running editor in batchmode
		AttemptPluginUpdate(false);
	}

	private static void AttemptPluginUpdate(bool triggeredByAutoUpdate)
	{
		OVRPlugin.SendEvent("attempt_plugin_update_auto", triggeredByAutoUpdate.ToString());

		PluginPackage bundledPluginPkg = GetBundledPluginPackage();
		List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

		PluginPackage enabledUtilsPluginPkg = null;
		PluginPackage newestUtilsPluginPkg = null;

		foreach(PluginPackage pluginPkg in allUtilsPluginPkgs)
		{
			if ((newestUtilsPluginPkg == null) || (pluginPkg.Version > newestUtilsPluginPkg.Version))
			{
				newestUtilsPluginPkg = pluginPkg;
			}

			if (pluginPkg.IsEnabled())
			{
				if ((enabledUtilsPluginPkg == null) || (pluginPkg.Version > enabledUtilsPluginPkg.Version))
				{
					enabledUtilsPluginPkg = pluginPkg;
				}
			}
		}

		bool reenableCurrentPluginPkg = false;
		PluginPackage targetPluginPkg = null;

		if ((newestUtilsPluginPkg != null) && (newestUtilsPluginPkg.Version > bundledPluginPkg.Version))
		{
			if ((enabledUtilsPluginPkg == null) || (enabledUtilsPluginPkg.Version != newestUtilsPluginPkg.Version))
			{
				targetPluginPkg = newestUtilsPluginPkg;
			}
		}
		else if ((enabledUtilsPluginPkg != null) && (enabledUtilsPluginPkg.Version < bundledPluginPkg.Version))
		{
			targetPluginPkg = bundledPluginPkg;
		}

		PluginPackage currentPluginPkg = (enabledUtilsPluginPkg != null) ? enabledUtilsPluginPkg : bundledPluginPkg;

		if ((targetPluginPkg == null) && !UnitySupportsEnabledAndroidPlugin())
		{
			// Force reenabling the current package to configure the correct android plugin for this unity version.
			reenableCurrentPluginPkg = true;
			targetPluginPkg = currentPluginPkg;
		}

		if (targetPluginPkg == null)
		{
			if (!triggeredByAutoUpdate && !unityRunningInBatchmode)
			{
#if UNITY_2018_3_OR_NEWER
				EditorUtility.DisplayDialog("Update Oculus Utilities Plugin",
					"OVRPlugin is already up to date.\n\nCurrent version: "
						+ GetVersionDescription(currentPluginPkg.Version),
					"Ok",
					"");
#else
				EditorUtility.DisplayDialog("Update Oculus Utilities Plugin",
					"OVRPlugin is already up to date.\n\nCurrent version: "
						+ GetVersionDescription(currentPluginPkg.Version) + "\nBundled version: "
						+ GetVersionDescription(bundledPluginPkg.Version),
					"Ok",
					"");
#endif
			}

			return; // No update necessary.
		}

		System.Version targetVersion = targetPluginPkg.Version;

		bool userAcceptsUpdate = false;

		if (unityRunningInBatchmode)
		{
			userAcceptsUpdate = true;
		}
		else
		{
			string dialogBody = "Oculus Utilities has detected that a newer OVRPlugin is available."
				+ " Using the newest version is recommended. Do you want to enable it?\n\n"
				+ "Current version: "
				+ GetVersionDescription(currentPluginPkg.Version)
				+ "\nAvailable version: "
				+ targetVersion;

			if (reenableCurrentPluginPkg)
			{
				dialogBody = "Oculus Utilities has detected a configuration change that requires re-enabling the current OVRPlugin."
					+ " Do you want to proceed?\n\nCurrent version: "
					+ GetVersionDescription(currentPluginPkg.Version);
			}

			int dialogResult = EditorUtility.DisplayDialogComplex("Update Oculus Utilities Plugin", dialogBody, "Yes", "No, Don't Ask Again", "No");

			switch (dialogResult)
			{
				case 0: // "Yes"
					userAcceptsUpdate = true;
					break;
				case 1: // "No, Don't Ask Again"
					autoUpdateEnabled = false;

					EditorUtility.DisplayDialog("Oculus Utilities OVRPlugin",
						"To manually update in the future, use the following menu option:\n\n"
							+ "[Oculus -> Tools -> Update OVR Utilities Plugin]",
						"Ok",
						"");
					return;
				case 2: // "No"
					return;
			}
		}

		if (userAcceptsUpdate)
		{
			DisableAllUtilitiesPluginPackages();

			if (!targetPluginPkg.IsBundledPluginPackage())
			{
				EnablePluginPackage(targetPluginPkg);
			}

			if (unityRunningInBatchmode
				|| EditorUtility.DisplayDialog("Restart Unity",
					"OVRPlugin has been updated to "
						+ GetVersionDescription(targetPluginPkg.Version)
						+ ".\n\nPlease restart the Unity Editor to complete the update process."
#if !UNITY_2017_1_OR_NEWER
						+ " You may need to manually relaunch Unity if you are using Unity 5.6 and higher."
#endif
						,
					"Restart",
					"Not Now"))
			{
				RestartUnityEditor();
			}
		}
	}

	private static bool UnitySupportsEnabledAndroidPlugin()
	{
		List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

		foreach(PluginPackage pluginPkg in allUtilsPluginPkgs)
		{
			if (pluginPkg.IsEnabled())
			{
				if (pluginPkg.IsAndroidUniversalEnabled() && !unityVersionSupportsAndroidUniversal)
				{
					// Android Universal should only be enabled on supported Unity versions since it can prevent app launch.
					return false;
				}
				else if (!pluginPkg.IsAndroidUniversalEnabled() && pluginPkg.IsAndroidUniversalPresent() && unityVersionSupportsAndroidUniversal)
				{
					// Android Universal is present and should be enabled on supported Unity versions since ARM64 config will fail otherwise.
					return false;
				}
			}
		}

		return true;
	}

	private static void RestartUnityEditor()
	{
		if (unityRunningInBatchmode)
		{
			EditorApplication.Exit(0);
		}
		else
		{
			restartPending = true;
			EditorApplication.OpenProject(GetCurrentProjectPath());
		}
	}
}
