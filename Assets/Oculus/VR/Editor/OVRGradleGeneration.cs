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

//#define BUILDSESSION
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using UnityEditor;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Build;
#if UNITY_2018_1_OR_NEWER
using UnityEditor.Build.Reporting;
#endif
using System;

[InitializeOnLoad]
public class OVRGradleGeneration
#if UNITY_2018_1_OR_NEWER
	: IPreprocessBuildWithReport, IPostprocessBuildWithReport
#if UNITY_ANDROID
	, IPostGenerateGradleAndroidProject
#endif
{
	public OVRADBTool adbTool;
	public Process adbProcess;

	public int callbackOrder { get { return 3; } }
	static private System.DateTime buildStartTime;
	static private System.Guid buildGuid;

	private const string prefName = "OVRAutoIncrementVersionCode_Enabled";
	private const string menuItemAutoIncVersion = "Oculus/Tools/Auto Increment Version Code";
	static bool autoIncrementVersion = false;

	static OVRGradleGeneration()
	{
		EditorApplication.delayCall += OnDelayCall;
	}

	static void OnDelayCall()
	{
#if UNITY_ANDROID
		autoIncrementVersion = PlayerPrefs.GetInt(prefName, 0) != 0;
		Menu.SetChecked(menuItemAutoIncVersion, autoIncrementVersion);
#endif
	}

#if UNITY_ANDROID
	[MenuItem(menuItemAutoIncVersion)]
	static void ToggleUtilities()
	{
		autoIncrementVersion = !autoIncrementVersion;
		Menu.SetChecked(menuItemAutoIncVersion, autoIncrementVersion);

		int newValue = (autoIncrementVersion) ? 1 : 0;
		PlayerPrefs.SetInt(prefName, newValue);
		PlayerPrefs.Save();

		UnityEngine.Debug.Log("Auto Increment Version Code: " + autoIncrementVersion);
	}
#endif

	public void OnPreprocessBuild(BuildReport report)
	{
#if UNITY_ANDROID
		// Generate error when Vulkan is selected as the perferred graphics API, which is not currently supported in Unity XR
		if (!PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
		{
			GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
			if (apis.Length >= 1 && apis[0] == GraphicsDeviceType.Vulkan)
			{
				throw new BuildFailedException("XR is currently not supported when using the Vulkan Graphics API. Please go to PlayerSettings and remove 'Vulkan' from the list of Graphics APIs.");
			}
		}
#endif

		buildStartTime = System.DateTime.Now;
		buildGuid = System.Guid.NewGuid();

		if (!report.summary.outputPath.Contains("OVRGradleTempExport"))
		{
			OVRPlugin.SetDeveloperMode(OVRPlugin.Bool.True);
			OVRPlugin.AddCustomMetadata("build_type", "standard");
		}

		OVRPlugin.AddCustomMetadata("build_guid", buildGuid.ToString());
		OVRPlugin.AddCustomMetadata("target_platform", report.summary.platform.ToString());
		OVRPlugin.AddCustomMetadata("scripting_runtime_version", UnityEditor.PlayerSettings.scriptingRuntimeVersion.ToString());
		if (report.summary.platform == UnityEditor.BuildTarget.StandaloneWindows
			|| report.summary.platform == UnityEditor.BuildTarget.StandaloneWindows64)
		{
			OVRPlugin.AddCustomMetadata("target_oculus_platform", "rift");
		}
#if BUILDSESSION
		StreamWriter writer = new StreamWriter("build_session", false);
		UnityEngine.Debug.LogFormat("Build Session: {0}", buildGuid.ToString());
		writer.WriteLine(buildGuid.ToString());
		writer.Close();
#endif
	}

	public void OnPostGenerateGradleAndroidProject(string path)
	{
		UnityEngine.Debug.Log("OVRGradleGeneration triggered.");
#if UNITY_ANDROID
		var targetOculusPlatform = new List<string>();
		if (OVRDeviceSelector.isTargetDeviceGearVrOrGo)
		{
			targetOculusPlatform.Add("geargo");
		}
		if (OVRDeviceSelector.isTargetDeviceQuest)
		{
			targetOculusPlatform.Add("quest");
		}
		OVRPlugin.AddCustomMetadata("target_oculus_platform", String.Join("_", targetOculusPlatform.ToArray()));
		UnityEngine.Debug.LogFormat("  GearVR or Go = {0}  Quest = {1}", OVRDeviceSelector.isTargetDeviceGearVrOrGo, OVRDeviceSelector.isTargetDeviceQuest);

		bool isQuestOnly = OVRDeviceSelector.isTargetDeviceQuest && !OVRDeviceSelector.isTargetDeviceGearVrOrGo;

		if (isQuestOnly)
		{
			if (File.Exists(Path.Combine(path, "build.gradle")))
			{
				try
				{
					string gradle = File.ReadAllText(Path.Combine(path, "build.gradle"));

					int v2Signingindex = gradle.IndexOf("v2SigningEnabled false");
					if (v2Signingindex != -1)
					{
						gradle = gradle.Replace("v2SigningEnabled false", "v2SigningEnabled true");
						System.IO.File.WriteAllText(Path.Combine(path, "build.gradle"), gradle);
					}
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogWarningFormat("Unable to overwrite build.gradle, error {0}", e.Message);
				}
			}
			else
			{
				UnityEngine.Debug.LogWarning("Unable to locate build.gradle");
			}
		}

		PatchAndroidManifest(path);
#endif
	}

#if UNITY_ANDROID
	public void PatchAndroidManifest(string path)
	{
		string manifestFolder = Path.Combine(path, "src/main");
		try
		{
			// Load android manfiest file
			XmlDocument doc = new XmlDocument();
			doc.Load(manifestFolder + "/AndroidManifest.xml");

			string androidNamepsaceURI;
			XmlElement element = (XmlElement)doc.SelectSingleNode("/manifest");
			if (element == null)
			{
				UnityEngine.Debug.LogError("Could not find manifest tag in android manifest.");
				return;
			}

			// Get android namespace URI from the manifest
			androidNamepsaceURI = element.GetAttribute("xmlns:android");
			if (!string.IsNullOrEmpty(androidNamepsaceURI))
			{
				// Look for intent filter category and change LAUNCHER to INFO
				XmlNodeList nodeList = doc.SelectNodes("/manifest/application/activity/intent-filter/category");
				foreach (XmlElement e in nodeList)
				{
					string attr = e.GetAttribute("name", androidNamepsaceURI);
					if (attr == "android.intent.category.LAUNCHER")
					{
						e.SetAttribute("name", androidNamepsaceURI, "android.intent.category.INFO");
					}
				}

				//If Quest is the target device, add the headtracking manifest tag
				if (OVRDeviceSelector.isTargetDeviceQuest)
				{
					XmlNodeList manifestUsesFeatureNodes = doc.SelectNodes("/manifest/uses-feature");
					bool foundHeadtrackingTag = false;
					foreach (XmlElement e in manifestUsesFeatureNodes)
					{
						string attr = e.GetAttribute("name", androidNamepsaceURI);
						if (attr == "android.hardware.vr.headtracking")
							foundHeadtrackingTag = true;
					}
					//If the tag already exists, don't patch with a new one. If it doesn't, we add it.
					if (!foundHeadtrackingTag)
					{
						XmlNode manifestElement = doc.SelectSingleNode("/manifest");
						XmlElement headtrackingTag = doc.CreateElement("uses-feature");
						headtrackingTag.SetAttribute("name", androidNamepsaceURI, "android.hardware.vr.headtracking");
						headtrackingTag.SetAttribute("version", androidNamepsaceURI, "1");
						string tagRequired = OVRDeviceSelector.isTargetDeviceGearVrOrGo ? "false" : "true";
						headtrackingTag.SetAttribute("required", androidNamepsaceURI, tagRequired);
						manifestElement.AppendChild(headtrackingTag);
					}
				}

				// Disable allowBackup in manifest and add Android NSC XML file
				XmlElement applicationNode = (XmlElement)doc.SelectSingleNode("/manifest/application");
				if(applicationNode != null)
				{
					OVRProjectConfig projectConfig = OVRProjectConfig.GetProjectConfig();
					if (projectConfig != null)
					{
						if (projectConfig.disableBackups)
						{
							applicationNode.SetAttribute("allowBackup", androidNamepsaceURI, "false");
						}

						if (projectConfig.enableNSCConfig)
						{
							applicationNode.SetAttribute("networkSecurityConfig", androidNamepsaceURI, "@xml/network_sec_config");

							string securityConfigFile = Path.Combine(Application.dataPath, "Oculus/VR/Editor/network_sec_config.xml");
							string xmlDirectory = Path.Combine(path, "src/main/res/xml");
							try
							{
								if (!Directory.Exists(xmlDirectory))
								{
									Directory.CreateDirectory(xmlDirectory);
								}
								File.Copy(securityConfigFile, Path.Combine(xmlDirectory, "network_sec_config.xml"), true);
							}
							catch (Exception e)
							{
								UnityEngine.Debug.LogError(e.Message);
							}
						}
					}
				}
				doc.Save(manifestFolder + "/AndroidManifest.xml");
			}
		}
		catch (Exception e)
		{
			UnityEngine.Debug.LogError(e.Message);
		}
	}
#endif

	public void OnPostprocessBuild(BuildReport report)
	{
#if UNITY_ANDROID
		if(autoIncrementVersion)
		{
			if((report.summary.options & BuildOptions.Development) == 0)
			{
				PlayerSettings.Android.bundleVersionCode++;
				UnityEngine.Debug.Log("Incrementing version code to " + PlayerSettings.Android.bundleVersionCode);
			}
		}

		bool isExporting = true;
		foreach (var step in report.steps)
		{
			if (step.name.Contains("Compile scripts")
				|| step.name.Contains("Building scenes")
				|| step.name.Contains("Writing asset files")
				|| step.name.Contains("Preparing APK resources")
				|| step.name.Contains("Creating Android manifest")
				|| step.name.Contains("Processing plugins")
				|| step.name.Contains("Exporting project")
				|| step.name.Contains("Building Gradle project"))
			{
				OVRPlugin.SendEvent("build_step_" + step.name.ToLower().Replace(' ', '_'),
					step.duration.TotalSeconds.ToString(), "ovrbuild");
#if BUILDSESSION
				UnityEngine.Debug.LogFormat("build_step_" + step.name.ToLower().Replace(' ', '_') + ": {0}", step.duration.TotalSeconds.ToString());
#endif
				if(step.name.Contains("Building Gradle project"))
				{
					isExporting = false;
				}
			}
		}
		OVRPlugin.AddCustomMetadata("build_step_count", report.steps.Length.ToString());
		if (report.summary.outputPath.Contains("apk")) // Exclude Gradle Project Output
		{
			var fileInfo = new System.IO.FileInfo(report.summary.outputPath);
			OVRPlugin.AddCustomMetadata("build_output_size", fileInfo.Length.ToString());
		}
#endif
		if (!report.summary.outputPath.Contains("OVRGradleTempExport"))
		{
			OVRPlugin.SendEvent("build_complete", (System.DateTime.Now - buildStartTime).TotalSeconds.ToString(), "ovrbuild");
#if BUILDSESSION
			UnityEngine.Debug.LogFormat("build_complete: {0}", (System.DateTime.Now - buildStartTime).TotalSeconds.ToString());
#endif
		}

#if UNITY_ANDROID
		if (!isExporting)
		{
			// Get the hosts path to Android SDK
			if (adbTool == null)
			{
				adbTool = new OVRADBTool(OVRConfig.Instance.GetAndroidSDKPath(false));
			}

			if (adbTool.isReady)
			{
				// Check to see if there are any ADB devices connected before continuing.
				List<string> devices = adbTool.GetDevices();
				if(devices.Count == 0)
				{
					return;
				}

				// Clear current logs on device
				Process adbClearProcess;
				adbClearProcess = adbTool.RunCommandAsync(new string[] { "logcat --clear" }, null);

				// Add a timeout if we cannot get a response from adb logcat --clear in time.
				Stopwatch timeout = new Stopwatch();
				timeout.Start();
				while (!adbClearProcess.WaitForExit(100))
				{
					if (timeout.ElapsedMilliseconds > 2000)
					{
						adbClearProcess.Kill();
						return;
					}
				}

				// Check if existing ADB process is still running, kill if needed
				if (adbProcess != null && !adbProcess.HasExited)
				{
					adbProcess.Kill();
				}

				// Begin thread to time upload and install
				var thread = new Thread(delegate ()
				{
					TimeDeploy();
				});
				thread.Start();
			}
		}
#endif
	}

#if UNITY_ANDROID
	public bool WaitForProcess;
	public bool TransferStarted;
	public DateTime UploadStart;
	public DateTime UploadEnd;
	public DateTime InstallEnd;

	public void TimeDeploy()
	{
		if (adbTool != null)
		{
			TransferStarted = false;
			DataReceivedEventHandler outputRecieved = new DataReceivedEventHandler(
				(s, e) =>
				{
					if (e.Data.Length != 0 && !e.Data.Contains("\u001b"))
					{
						if (e.Data.Contains("free_cache"))
						{
							// Device recieved install command and is starting upload
							UploadStart = System.DateTime.Now;
							TransferStarted = true;
						}
						else if (e.Data.Contains("Running dexopt"))
						{
							// Upload has finished and Package Manager is starting install
							UploadEnd = System.DateTime.Now;
						}
						else if (e.Data.Contains("dex2oat took"))
						{
							// Package Manager finished install
							InstallEnd = System.DateTime.Now;
							WaitForProcess = false;
						}
						else if (e.Data.Contains("W PackageManager"))
						{
							// Warning from Package Manager is a failure in the install process
							WaitForProcess = false;
						}
					}
				}
			);

			WaitForProcess = true;
			adbProcess = adbTool.RunCommandAsync(new string[] { "logcat" }, outputRecieved);

			Stopwatch transferTimeout = new Stopwatch();
			transferTimeout.Start();
			while (adbProcess != null && !adbProcess.WaitForExit(100))
			{
				if (!WaitForProcess)
				{
					adbProcess.Kill();
					float UploadTime = (float)(UploadEnd - UploadStart).TotalMilliseconds / 1000f;
					float InstallTime = (float)(InstallEnd - UploadEnd).TotalMilliseconds / 1000f;

					if (UploadTime > 0f)
					{
						OVRPlugin.SendEvent("deploy_task", UploadTime.ToString(), "ovrbuild");
					}
					if (InstallTime > 0f)
					{
						OVRPlugin.SendEvent("install_task", InstallTime.ToString(), "ovrbuild");
					}
				}

				if (!TransferStarted && transferTimeout.ElapsedMilliseconds > 5000)
				{
					adbProcess.Kill();
				}
			}
		}
	}
#endif
#else
		{
#endif
		}
