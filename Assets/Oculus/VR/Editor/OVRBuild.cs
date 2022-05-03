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
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Allows Oculus to build apps from the command line.
/// </summary>
partial class OculusBuildApp : EditorWindow
{
	static void SetPCTarget()
	{
		if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows)
		{
			EditorUserBuildSettings.SwitchActiveBuildTarget (BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows);
		}
		UnityEditorInternal.VR.VREditor.SetVREnabledOnTargetGroup(BuildTargetGroup.Standalone, true);
		PlayerSettings.virtualRealitySupported = true;
		AssetDatabase.SaveAssets();
	}

	static void SetAndroidTarget()
	{
		EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
		EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

		if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
		{
			EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
		}

		UnityEditorInternal.VR.VREditor.SetVREnabledOnTargetGroup(BuildTargetGroup.Standalone, true);
		PlayerSettings.virtualRealitySupported = true;
		AssetDatabase.SaveAssets();
	}

#if UNITY_EDITOR_WIN && UNITY_2018_3_OR_NEWER && UNITY_ANDROID
	// Build setting constants
	const string REMOTE_APK_PATH = "/sdcard/Oculus/Temp";
	const float USB_TRANSFER_SPEED_THRES = 25.0f;
	const float USB_3_TRANSFER_SPEED = 32.0f;
	const int NUM_BUILD_AND_RUN_STEPS = 9;
	const int BYTES_TO_MEGABYTES = 1048576;

	// Progress bar variables
	static int totalBuildSteps;
	static int currentStep;
	static string progressMessage;

	// Build setting varaibles
	static string gradlePath;
	static string jdkPath;
	static string androidSdkPath;
	static string applicationIdentifier;
	static string productName;
	static string dataPath;

	static string gradleTempExport;
	static string gradleExport;
	static bool showCancel;
	static bool buildFailed;

	static double totalBuildTime;

	static DirectorySyncer.CancellationTokenSource syncCancelToken;
	static Process gradleBuildProcess;
	static Thread buildThread;

	static bool? apkOutputSuccessful;

	private void OnGUI()
	{
		// Fix progress bar window size
		minSize = new Vector2(500, 170);
		maxSize = new Vector2(500, 170);

		// Show progress bar
		Rect progressRect = EditorGUILayout.BeginVertical();
		progressRect.height = 25.0f;
		float progress = currentStep / (float)totalBuildSteps;
		EditorGUI.ProgressBar(progressRect, progress, progressMessage);

		// Show cancel button only after Unity export has finished.
		if (showCancel)
		{
			GUIContent btnTxt = new GUIContent("Cancel");
			var rt = GUILayoutUtility.GetRect(btnTxt, GUI.skin.button, GUILayout.ExpandWidth(false));
			rt.center = new Vector2(EditorGUIUtility.currentViewWidth / 2, progressRect.height * 2);
			if (GUI.Button(rt, btnTxt, GUI.skin.button))
			{
				CancelBuild();
			}
		}
		EditorGUILayout.EndVertical();

		// Close window if progress has completed or Unity export failed
		if (progress >= 1.0f || buildFailed)
		{
			Close();
		}
	}

	void Update()
	{
		// Force window update if not in focus to ensure progress bar still updates
		var window = EditorWindow.focusedWindow;
		if (window != null && window.ToString().Contains("OculusBuildApp"))
		{
			Repaint();
		}
	}

	void CancelBuild()
	{
		SetProgressBarMessage("Canceling . . .");

		if (syncCancelToken != null)
		{
			syncCancelToken.Cancel();
		}

		if (apkOutputSuccessful.HasValue && apkOutputSuccessful.Value)
		{
			buildThread.Abort();
			buildFailed = true;
		}

		if (gradleBuildProcess != null && !gradleBuildProcess.HasExited)
		{
			var cancelThread = new Thread(delegate ()
			{
				CancelGradleBuild();
			});
			cancelThread.Start();
		}
	}

	void CancelGradleBuild()
	{
		Process cancelGradleProcess = new Process();
		string arguments = "-Xmx1024m -classpath \"" + gradlePath +
			"\" org.gradle.launcher.GradleMain --stop";
		var processInfo = new System.Diagnostics.ProcessStartInfo
		{
			WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal,
			FileName = jdkPath,
			Arguments = arguments,
			RedirectStandardInput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardError = true,
			RedirectStandardOutput = true,
		};

		cancelGradleProcess.StartInfo = processInfo;
		cancelGradleProcess.EnableRaisingEvents = true;

		cancelGradleProcess.OutputDataReceived += new DataReceivedEventHandler(
			(s, e) =>
			{
				if (e != null && e.Data != null && e.Data.Length != 0)
				{
					UnityEngine.Debug.LogFormat("Gradle: {0}", e.Data);
				}
			}
		);

		apkOutputSuccessful = false;

		cancelGradleProcess.Start();
		cancelGradleProcess.BeginOutputReadLine();
		cancelGradleProcess.WaitForExit();

		buildFailed = true;
	}

	[MenuItem("Oculus/OVR Build And Run", false, 10)]
	static void StartBuildAndRun()
	{
		EditorWindow.GetWindow(typeof(OculusBuildApp));

		showCancel = false;
		buildFailed = false;
		totalBuildTime = 0;

		InitializeProgressBar(NUM_BUILD_AND_RUN_STEPS);
		IncrementProgressBar("Exporting Unity Project . . .");

		OVRPlugin.SetDeveloperMode(OVRPlugin.Bool.True);
		OVRPlugin.AddCustomMetadata("build_type", "ovr_build_and_run");

		if (!CheckADBDevices())
		{
			return;
		}

		// Record OVR Build and Run start event
		OVRPlugin.SendEvent("ovr_build_and_run_start", "", "ovrbuild");

		apkOutputSuccessful = null;
		syncCancelToken = null;
		gradleBuildProcess = null;

		UnityEngine.Debug.Log("OVRBuild: Starting Unity build ...");

		SetupDirectories();

		// 1. Get scenes to build in Unity, and export gradle project
		List<string> sceneList = GetScenesToBuild();
		DateTime unityExportStart = DateTime.Now;
		var buildResult = BuildPipeline.BuildPlayer(sceneList.ToArray(), gradleTempExport, BuildTarget.Android,
			BuildOptions.AcceptExternalModificationsToPlayer |
			BuildOptions.Development |
			BuildOptions.AllowDebugging);

		if (buildResult.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
		{
			double unityExportTime = (DateTime.Now - unityExportStart).TotalSeconds;
			OVRPlugin.SendEvent("build_step_unity_export", unityExportTime.ToString(), "ovrbuild");
			totalBuildTime += unityExportTime;

			// Set static variables so build thread has updated data
			showCancel = true;
			gradlePath = OVRConfig.Instance.GetGradlePath();
			jdkPath = OVRConfig.Instance.GetJDKPath();
			androidSdkPath = OVRConfig.Instance.GetAndroidSDKPath();
			applicationIdentifier = PlayerSettings.applicationIdentifier;
			productName = Application.productName;
			dataPath = Application.dataPath;

			buildThread = new Thread(delegate ()
			{
				OVRBuildRun();
			});
			buildThread.Start();
			return;
		}
		else if (buildResult.summary.result == UnityEditor.Build.Reporting.BuildResult.Cancelled)
		{
			UnityEngine.Debug.Log("Build cancelled.");
		}
		else
		{
			UnityEngine.Debug.Log("Build failed.");
		}
		buildFailed = true;
	}

	static void OVRBuildRun()
	{
		// 2. Process gradle project
		IncrementProgressBar("Processing gradle project . . .");
		if (ProcessGradleProject())
		{
			// 3. Build gradle project
			IncrementProgressBar("Starting gradle build . . .");
			if (BuildGradleProject())
			{
				OVRPlugin.SendEvent("build_complete", totalBuildTime.ToString(), "ovrbuild");
				// 4. Deploy and run
				if (DeployAPK())
				{
					return;
				}
			}
		}
		buildFailed = true;
	}

	private static bool BuildGradleProject()
	{
		gradleBuildProcess = new Process();
		string arguments = "-Xmx4096m -classpath \"" + gradlePath +
			"\" org.gradle.launcher.GradleMain assembleDebug -x validateSigningDebug";
		var gradleProjectPath = Path.Combine(gradleExport, productName);
		var processInfo = new System.Diagnostics.ProcessStartInfo
		{
			WorkingDirectory = gradleProjectPath,
			WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal,
			FileName = jdkPath,
			Arguments = arguments,
			RedirectStandardInput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardError = true,
			RedirectStandardOutput = true,
		};

		gradleBuildProcess.StartInfo = processInfo;
		gradleBuildProcess.EnableRaisingEvents = true;

		DateTime gradleStartTime = System.DateTime.Now;
		DateTime gradleEndTime = System.DateTime.MinValue;

		gradleBuildProcess.Exited += new System.EventHandler(
			(s, e) =>
			{
				UnityEngine.Debug.Log("Gradle: Exited");
			}
		);

		gradleBuildProcess.OutputDataReceived += new DataReceivedEventHandler(
			(s, e) =>
			{
				if (e != null && e.Data != null &&
					e.Data.Length != 0 && e.Data.Contains("BUILD"))
				{
					UnityEngine.Debug.LogFormat("Gradle: {0}", e.Data);
					if (e.Data.Contains("SUCCESSFUL"))
					{
						UnityEngine.Debug.LogFormat("APK Build Completed: {0}",
							Path.Combine(Path.Combine(gradleProjectPath, "build\\outputs\\apk\\debug"), productName + "-debug.apk").Replace("/", "\\"));
						if (!apkOutputSuccessful.HasValue)
						{
							apkOutputSuccessful = true;
						}
						gradleEndTime = System.DateTime.Now;
					}
					else if (e.Data.Contains("FAILED"))
					{
						apkOutputSuccessful = false;
					}
				}
			}
		);

		gradleBuildProcess.ErrorDataReceived += new DataReceivedEventHandler(
			(s, e) =>
			{
				if (e != null && e.Data != null &&
					e.Data.Length != 0)
				{
					UnityEngine.Debug.LogErrorFormat("Gradle: {0}", e.Data);
				}
				apkOutputSuccessful = false;
			}
		);

		gradleBuildProcess.Start();
		gradleBuildProcess.BeginOutputReadLine();
		IncrementProgressBar("Building gradle project . . .");

		gradleBuildProcess.WaitForExit();

		// Add a timeout for if gradle unexpectedlly exits or errors out
		Stopwatch timeout = new Stopwatch();
		timeout.Start();
		while (apkOutputSuccessful == null)
		{
			if (timeout.ElapsedMilliseconds > 5000)
			{
				UnityEngine.Debug.LogError("Gradle has exited unexpectedly.");
				apkOutputSuccessful = false;
			}
			System.Threading.Thread.Sleep(100);
		}

		// Record time it takes to build gradle project only if we had a successful build
		double gradleTime = (gradleEndTime - gradleStartTime).TotalSeconds;
		if (gradleTime > 0)
		{
			OVRPlugin.SendEvent("build_step_building_gradle_project", gradleTime.ToString(), "ovrbuild");
			totalBuildTime += gradleTime;
		}

		return apkOutputSuccessful.HasValue && apkOutputSuccessful.Value;
	}

	private static bool ProcessGradleProject()
	{
		DateTime syncStartTime = System.DateTime.Now;
		DateTime syncEndTime = System.DateTime.MinValue;

		try
		{
			var ps = System.Text.RegularExpressions.Regex.Escape("" + Path.DirectorySeparatorChar);
			// ignore files .gradle/** build/** foo/.gradle/** and bar/build/**   
			var ignorePattern = string.Format("^([^{0}]+{0})?(\\.gradle|build){0}", ps);

			var syncer = new DirectorySyncer(gradleTempExport,
				gradleExport, ignorePattern);

			syncCancelToken = new DirectorySyncer.CancellationTokenSource();
			var syncResult = syncer.Synchronize(syncCancelToken.Token);
			syncEndTime = System.DateTime.Now;
		}
		catch (Exception e)
		{
			UnityEngine.Debug.Log("OVRBuild: Processing gradle project failed with exception: " + 
				e.Message);
			return false;
		}

		if (syncCancelToken.IsCancellationRequested)
		{
			return false;
		}

		// Record time it takes to sync gradle projects only if the sync was successful
		double syncTime = (syncEndTime - syncStartTime).TotalSeconds;
		if (syncTime > 0)
		{
			OVRPlugin.SendEvent("build_step_sync_gradle_project", syncTime.ToString(), "ovrbuild");
			totalBuildTime += syncTime;
		}

		return true;
	}

	private static List<string> GetScenesToBuild()
	{
		var sceneList = new List<string>();
		foreach (var scene in EditorBuildSettings.scenes)
		{
			// Enumerate scenes in project and check if scene is enabled to build
			if (scene.enabled)
			{
				sceneList.Add(scene.path);
			}
		}
		return sceneList;
	}

	public static bool DeployAPK()
	{
		// Create new instance of ADB Tool
		var adbTool = new OVRADBTool(androidSdkPath);

		if (adbTool.isReady)
		{
			string apkPathLocal;
			string gradleExportFolder = Path.Combine(Path.Combine(gradleExport, productName), "build\\outputs\\apk\\debug");

			// Check to see if gradle output directory exists
			gradleExportFolder = gradleExportFolder.Replace("/", "\\");
			if (!Directory.Exists(gradleExportFolder))
			{
				UnityEngine.Debug.LogError("Could not find the gradle project at the expected path: " + gradleExportFolder);
				return false;
			}

			// Search for output APK in gradle output directory
			apkPathLocal = Path.Combine(gradleExportFolder, productName + "-debug.apk");
			if (!System.IO.File.Exists(apkPathLocal))
			{
				UnityEngine.Debug.LogError(string.Format("Could not find {0} in the gradle project.", productName + "-debug.apk"));
				return false;
			}

			string output, error;
			DateTime timerStart;

			// Ensure that the Oculus temp directory is on the device by making it
			IncrementProgressBar("Making Temp directory on device");
			string[] mkdirCommand = { "-d shell", "mkdir -p", REMOTE_APK_PATH };
			if (adbTool.RunCommand(mkdirCommand, null, out output, out error) != 0) return false;

			// Push APK to device, also time how long it takes
			timerStart = System.DateTime.Now;
			IncrementProgressBar("Pushing APK to device . . .");
			string[] pushCommand = { "-d push", "\"" + apkPathLocal + "\"", REMOTE_APK_PATH };
			if (adbTool.RunCommand(pushCommand, null, out output, out error) != 0) return false;

			// Calculate the transfer speed and determine if user is using USB 2.0 or 3.0
			TimeSpan pushTime = System.DateTime.Now - timerStart;
			FileInfo apkInfo = new System.IO.FileInfo(apkPathLocal);
			double transferSpeed = (apkInfo.Length / pushTime.TotalSeconds) / BYTES_TO_MEGABYTES;
			bool informLog = transferSpeed < USB_TRANSFER_SPEED_THRES;
			UnityEngine.Debug.Log("OVRADBTool: Push Success");

			// Install the APK package on the device
			IncrementProgressBar("Installing APK . . .");
			string apkPath = REMOTE_APK_PATH + "/" + productName + "-debug.apk";
			apkPath = apkPath.Replace(" ", "\\ ");
			string[] installCommand = { "-d shell", "pm install -r", apkPath };

			timerStart = System.DateTime.Now;
			if (adbTool.RunCommand(installCommand, null, out output, out error) != 0) return false;
			TimeSpan installTime = System.DateTime.Now - timerStart;
			UnityEngine.Debug.Log("OVRADBTool: Install Success");

			// Start the application on the device
			IncrementProgressBar("Launching application on device . . .");
			string playerActivityName = "\"" + applicationIdentifier + "/" + applicationIdentifier + ".UnityPlayerActivity\"";
			string[] appStartCommand = { "-d shell", "am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -S -f 0x10200000 -n", playerActivityName };
			if (adbTool.RunCommand(appStartCommand, null, out output, out error) != 0) return false;
			UnityEngine.Debug.Log("OVRADBTool: Application Start Success");

			// Send back metrics on push and install steps
			OVRPlugin.AddCustomMetadata("transfer_speed", transferSpeed.ToString());
			OVRPlugin.SendEvent("build_step_push_apk", pushTime.TotalSeconds.ToString(), "ovrbuild");
			OVRPlugin.SendEvent("build_step_install_apk", installTime.TotalSeconds.ToString(), "ovrbuild");

			IncrementProgressBar("Success!");

			// If the user is using a USB 2.0 cable, inform them about improved transfer speeds and estimate time saved
			if (informLog)
			{
				var usb3Time = apkInfo.Length / (USB_3_TRANSFER_SPEED * BYTES_TO_MEGABYTES);
				UnityEngine.Debug.Log(string.Format("OVRBuild has detected slow transfer speeds. A USB 3.0 cable is recommended to reduce the time it takes to deploy your project by approximatly {0:0.0} seconds", pushTime.TotalSeconds - usb3Time));
				return true;
			}
		}
		else
		{
			UnityEngine.Debug.LogError("Could not find the ADB executable in the specified Android SDK directory.");
		}
		return false;
	}

	private static bool CheckADBDevices()
	{
		// Check if there are any ADB devices connected before starting the build process
		var adbTool = new OVRADBTool(OVRConfig.Instance.GetAndroidSDKPath());
		if (adbTool.isReady)
		{
			List<string> devices = adbTool.GetDevices();
			if (devices.Count == 0)
			{
				OVRPlugin.SendEvent("no_adb_devices", "", "ovrbuild");
				UnityEngine.Debug.LogError("No ADB devices connected. Cannot perform OVR Build and Run.");
				return false;
			}
			else if(devices.Count > 1)
			{
				OVRPlugin.SendEvent("multiple_adb_devices", "", "ovrbuild");
				UnityEngine.Debug.LogError("Multiple ADB devices connected. Cannot perform OVR Build and Run.");
				return false;
			}
		}
		else
		{
			OVRPlugin.SendEvent("ovr_adbtool_initialize_failure", "", "ovrbuild");
			UnityEngine.Debug.LogError("OVR ADB Tool failed to initialize. Check the Android SDK path in [Edit -> Preferences -> External Tools]");
			return false;
		}
		return true;
	}

	private static void SetupDirectories()
	{
		gradleTempExport = Path.Combine(Path.Combine(Application.dataPath, "../Temp"), "OVRGradleTempExport");
		gradleExport = Path.Combine(Path.Combine(Application.dataPath, "../Temp"), "OVRGradleExport");
		if (!Directory.Exists(gradleExport))
		{
			Directory.CreateDirectory(gradleExport);
		}
	}

	private static void InitializeProgressBar(int stepCount)
	{
		currentStep = 0;
		totalBuildSteps = stepCount;
	}

	private static void IncrementProgressBar(string message)
	{
		currentStep++;
		progressMessage = message;
		UnityEngine.Debug.Log("OVRBuild: " + message);
	}

	private static void SetProgressBarMessage(string message)
	{
		progressMessage = message;
		UnityEngine.Debug.Log("OVRBuild: " + message);
	}
#endif //UNITY_EDITOR_WIN && UNITY_2018_1_OR_NEWER && UNITY_ANDROID
}
