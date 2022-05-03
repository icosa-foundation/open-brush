using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Oculus.VR.Editor
{
	public class OVRPlatformTool : EditorWindow
	{
		public enum TargetPlatform
		{
			Rift,
			OculusGoGearVR,
			Quest,
			None,
		};

		const string urlPlatformUtil =
			"https://www.oculus.com/download_app/?id=1076686279105243";

		static private Process ovrPlatUtilProcess;
		Vector2 commandMenuScroll;
		Vector2 debugLogScroll;

		static public string log;

		private static bool activeProcess = false;
		private static bool ranSelfUpdate = false;
		private static int retryCount = 0;

		private const float buttonPadding = 5.0f;

		private bool showOptionalCommands = false;
		private bool show2DCommands = false;
		private bool showExpansionFileCommands = false;
		private bool showRedistCommands = false;

		private const float INDENT_SPACING = 15f;
		private const float SINGLE_LINE_SPACING = 18f;
		private const float ASSET_CONFIG_BACKGROUND_PADDING = 10f;
		private const float DEFAULT_LABEL_WIDTH = 180f;
		private const int MAX_DOWNLOAD_RETRY_COUNT = 2;

		private static GUIStyle boldFoldoutStyle;

		string[] platformOptions = new string[]
		{
			"Oculus Rift",
			"Oculus Go | Gear VR",
			"Oculus Quest"
		};

		string[] gamepadOptions = new string[]
		{
			"Off",
			"Twinstick",
			"Right D Pad",
			"Left D Pad"
		};

		[MenuItem("Oculus/Tools/Oculus Platform Tool")]
		static void Init()
		{
			OVRPlatformTool.log = string.Empty;
			// Get existing open window or if none, make a new one:
			EditorWindow.GetWindow(typeof(OVRPlatformTool));

			// Populate initial target platform value based on OVRDeviceSelector
#if UNITY_ANDROID
			if (OVRDeviceSelector.isTargetDeviceQuest)
			{
				OVRPlatformToolSettings.TargetPlatform = TargetPlatform.Quest;
			}
			else
			{
				OVRPlatformToolSettings.TargetPlatform = TargetPlatform.OculusGoGearVR;
			}
#else
			OVRPlatformToolSettings.TargetPlatform = TargetPlatform.Rift;
#endif
			EditorUtility.SetDirty(OVRPlatformToolSettings.Instance);

			// Load redist packages by calling list-redists in the CLI
			string dataPath = Application.dataPath;
			var thread = new Thread(delegate () {
				retryCount = 0;
				LoadRedistPackages(dataPath);
			});
			thread.Start();

			OVRPlugin.SendEvent("oculus_platform_tool", "show_window");
		}

		void OnGUI()
		{
			if (boldFoldoutStyle == null)
			{
				boldFoldoutStyle = new GUIStyle(EditorStyles.foldout);
				boldFoldoutStyle.fontStyle = FontStyle.Bold;
			}

			EditorGUIUtility.labelWidth = DEFAULT_LABEL_WIDTH;

			GUILayout.Label("OVR Platform Tool", EditorStyles.boldLabel);
			this.titleContent.text = "OVR Platform Tool";

			GUIContent TargetPlatformLabel = new GUIContent("Target Oculus Platform");
			OVRPlatformToolSettings.TargetPlatform = (TargetPlatform)MakePopup(TargetPlatformLabel, (int)OVRPlatformToolSettings.TargetPlatform, platformOptions);
			SetOVRProjectConfig(OVRPlatformToolSettings.TargetPlatform);
			SetDirtyOnGUIChange();

			commandMenuScroll = EditorGUILayout.BeginScrollView(commandMenuScroll, GUILayout.Height(Screen.height / 2));
			{
				// Add the UI Form
				EditorGUI.BeginChangeCheck();
				GUILayout.Space(15.0f);

				// App ID
				GUIContent AppIDLabel = new GUIContent("Oculus Application ID [?]: ",
					"This AppID will be used when uploading the build.");
				OVRPlatformToolSettings.AppID = MakeTextBox(AppIDLabel, OVRPlatformToolSettings.AppID);

				// App Token
				GUIContent AppTokenLabel = new GUIContent("Oculus App Token [?]: ",
					"You can get your app token from your app's Oculus API Dashboard.");
				OVRPlatformToolSettings.AppToken = MakePasswordBox(AppTokenLabel, OVRPlatformToolSettings.AppToken);

				// Release Channel
				GUIContent ReleaseChannelLabel = new GUIContent("Release Channel [?]: ",
					"Specify the releaes channel of the new build, you can reassign to other channels after upload.");
				OVRPlatformToolSettings.ReleaseChannel = MakeTextBox(ReleaseChannelLabel, OVRPlatformToolSettings.ReleaseChannel);

				// Releaes Note
				GUIContent ReleaseNoteLabel = new GUIContent("Release Note: ");
				OVRPlatformToolSettings.ReleaseNote = MakeTextBox(ReleaseNoteLabel, OVRPlatformToolSettings.ReleaseNote);

				// Platform specific fields
				if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.Rift)
				{
					GUIContent BuildDirLabel = new GUIContent("Rift Build Directory [?]: ",
						"The full path to the directory containing your Rift build files.");
					OVRPlatformToolSettings.RiftBuildDirectory = MakeFileDirectoryField(BuildDirLabel, OVRPlatformToolSettings.RiftBuildDirectory,
						"Choose Rifle Build Directory");

					GUIContent BuildVersionLabel = new GUIContent("Build Version [?]: ",
						"The version number shown to users.");
					OVRPlatformToolSettings.RiftBuildVersion = MakeTextBox(BuildVersionLabel, OVRPlatformToolSettings.RiftBuildVersion);

					GUIContent LaunchFileLabel = new GUIContent("Launch File Path [?]: ",
						"The full path to the executable that launches your app.");
					OVRPlatformToolSettings.RiftLaunchFile = MakeFileDirectoryField(LaunchFileLabel, OVRPlatformToolSettings.RiftLaunchFile,
						"Choose Launch File", true, "exe");
				}
				else
				{
					GUIContent ApkPathLabel = new GUIContent("Build APK File Path [?]: ",
						"The full path to the APK file.");
					OVRPlatformToolSettings.ApkBuildPath = MakeFileDirectoryField(ApkPathLabel, OVRPlatformToolSettings.ApkBuildPath,
						"Choose APK File", true, "apk");

					if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.OculusGoGearVR)
					{
						// Go and Gear VR specific fields
					}
					else if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.Quest)
					{
						// Quest specific fields
					}
				}

				showOptionalCommands = EditorGUILayout.Foldout(showOptionalCommands, "Optional Commands", boldFoldoutStyle);
				if (showOptionalCommands)
				{
					IncrementIndent();

					if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.Rift)
					{
						// Launch Parameters
						GUIContent LaunchParamLabel = new GUIContent("Launch Parameters [?]: ",
							"Specifies any arguments passed to the launcher.");
						OVRPlatformToolSettings.RiftLaunchParams = MakeTextBox(LaunchParamLabel, OVRPlatformToolSettings.RiftLaunchParams);

						GUIContent FirewallExceptionLabel = new GUIContent("Firewall Exception [?]: ",
							"Specifies if a Windows Firewall exception is required.");
						OVRPlatformToolSettings.RiftFirewallException = MakeToggleBox(FirewallExceptionLabel, OVRPlatformToolSettings.RiftFirewallException);

						GUIContent GamepadEmulationLabel = new GUIContent("Gamepad Emulation [?]: ",
							"Specifies the type of gamepad emulation used by the Oculus Touch controllers.");
						OVRPlatformToolSettings.RiftGamepadEmulation = (OVRPlatformToolSettings.GamepadType)MakePopup(GamepadEmulationLabel, (int)OVRPlatformToolSettings.RiftGamepadEmulation, gamepadOptions);

						show2DCommands = EditorGUILayout.Foldout(show2DCommands, "2D", boldFoldoutStyle);
						if (show2DCommands)
						{
							IncrementIndent();

							// 2D Launch File
							GUIContent LaunchFile2DLabel = new GUIContent("2D Launch File [?]: ",
								"The full path to the executable that launches your app in 2D mode.");
							OVRPlatformToolSettings.Rift2DLaunchFile = MakeFileDirectoryField(LaunchFile2DLabel, OVRPlatformToolSettings.Rift2DLaunchFile,
								"Choose 2D Launch File", true, "exe");

							// 2D Launch Parameters
							GUIContent LaunchParam2DLabel = new GUIContent("2D Launch Parameters [?]: ",
								"Specifies any arguments passed to the launcher in 2D mode.");
							OVRPlatformToolSettings.Rift2DLaunchParams = MakeTextBox(LaunchParam2DLabel, OVRPlatformToolSettings.Rift2DLaunchParams);

							DecrementIndent();
						}

						showRedistCommands = EditorGUILayout.Foldout(showRedistCommands, "Redistributable Packages", boldFoldoutStyle);
						if (showRedistCommands)
						{
							IncrementIndent();

							for (int i = 0; i < OVRPlatformToolSettings.RiftRedistPackages.Count; i++)
							{
								GUIContent RedistPackageLabel = new GUIContent(OVRPlatformToolSettings.RiftRedistPackages[i].name);
								OVRPlatformToolSettings.RiftRedistPackages[i].include = MakeToggleBox(RedistPackageLabel, OVRPlatformToolSettings.RiftRedistPackages[i].include);
							}

							DecrementIndent();
						}

						showExpansionFileCommands = EditorGUILayout.Foldout(showExpansionFileCommands, "Expansion Files", boldFoldoutStyle);
						if (showExpansionFileCommands)
						{
							IncrementIndent();

							// Language Pack Directory
							GUIContent LanguagePackLabel = new GUIContent("Language Pack Directory [?]: ",
								"The full path to the directory containing the language packs");
							OVRPlatformToolSettings.LanguagePackDirectory = MakeFileDirectoryField(LanguagePackLabel, OVRPlatformToolSettings.LanguagePackDirectory,
								"Choose Language Pack Directory");
						}
					}
					else
					{
						if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.OculusGoGearVR)
						{
							// Go and Gear VR specific optional fields
						}
						else if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.Quest)
						{
							// Quest specific optional fields
						}

						showExpansionFileCommands = EditorGUILayout.Foldout(showExpansionFileCommands, "Expansion Files", boldFoldoutStyle);
						if (showExpansionFileCommands)
						{
							IncrementIndent();

							// OBB File Path
							GUIContent ObbPathLabel = new GUIContent("OBB File Path [?]: ",
								"The full path to the OBB file.");
							OVRPlatformToolSettings.ObbFilePath = MakeFileDirectoryField(ObbPathLabel, OVRPlatformToolSettings.ObbFilePath,
								"Choose OBB File", true, "obb");
						}
					}

					if (showExpansionFileCommands)
					{
						// Assets Directory
						GUIContent AssetsDirLabel = new GUIContent("Assets Directory [?]: ",
							"The full path to the directory with DLCs for this build.");
						string assetsDirectory = MakeFileDirectoryField(AssetsDirLabel, OVRPlatformToolSettings.AssetsDirectory,
							"Choose Assets Directory");

						if (assetsDirectory != OVRPlatformToolSettings.AssetsDirectory)
						{
							OVRPlatformToolSettings.AssetsDirectory = assetsDirectory;
							OVRPlatformToolSettings.AssetConfigs.Clear();
							if (!string.IsNullOrEmpty(OVRPlatformToolSettings.AssetsDirectory))
							{
								DirectoryInfo dirInfo = new DirectoryInfo(OVRPlatformToolSettings.AssetsDirectory);
								FileInfo[] assetFiles = dirInfo.GetFiles();
								foreach (FileInfo f in assetFiles)
								{
									OVRPlatformToolSettings.AssetConfigs.Add(new AssetConfig(f.Name));
								}
							}
							EditorUtility.SetDirty(OVRPlatformToolSettings.Instance);
						}

						// Display bordered asset configuration list
						GUILayout.Space(3f);
						Rect rect = GUILayoutUtility.GetRect(0, GetAssetConfigElementHeight() + (ASSET_CONFIG_BACKGROUND_PADDING * 2),
							GUILayout.ExpandWidth(true));
						rect.x += (EditorGUI.indentLevel * INDENT_SPACING + 5);
						rect.width -= (EditorGUI.indentLevel * INDENT_SPACING + 10);
						DrawAssetConfigList(rect);

						DecrementIndent();
					}

					EditorGUI.indentLevel--;
				}

				if (EditorGUI.EndChangeCheck())
				{
					EditorUtility.SetDirty(OVRPlatformToolSettings.Instance);
				}
			}
			EditorGUILayout.EndScrollView();

			GUILayout.Space(SINGLE_LINE_SPACING);

			GUILayout.FlexibleSpace();

			// Run OVR Lint Option
			EditorGUIUtility.labelWidth = DEFAULT_LABEL_WIDTH;
			GUIContent RunOvrLintLabel = new GUIContent("Run OVR Lint (Recommended) [?]: ",
				"Run OVR Lint tool to ensure project is optimized for performance and meets Oculus packaging requirement for publishing.");
			OVRPlatformToolSettings.RunOvrLint = MakeToggleBox(RunOvrLintLabel, OVRPlatformToolSettings.RunOvrLint);

			// Add an Upload button
			GUI.enabled = !activeProcess;
			GUIContent btnTxt = new GUIContent("Upload");
			var rt = GUILayoutUtility.GetRect(btnTxt, GUI.skin.button, GUILayout.ExpandWidth(false));
			var btnYPos = rt.center.y;
			rt.center = new Vector2(EditorGUIUtility.currentViewWidth / 2 - rt.width / 2 - buttonPadding, btnYPos);
			if (GUI.Button(rt, btnTxt, GUI.skin.button))
			{
				OVRPlugin.SendEvent("oculus_platform_tool", "upload");
				OVRPlatformTool.log = string.Empty;
				OnUpload(OVRPlatformToolSettings.TargetPlatform);
			}

			// Add a cancel button
			GUI.enabled = activeProcess;
			btnTxt = new GUIContent("Cancel");
			rt = GUILayoutUtility.GetRect(btnTxt, GUI.skin.button, GUILayout.ExpandWidth(false));
			rt.center = new Vector2(EditorGUIUtility.currentViewWidth / 2 + rt.width / 2 + buttonPadding, btnYPos);
			if (GUI.Button(rt, btnTxt, GUI.skin.button))
			{
				if (EditorUtility.DisplayDialog("Cancel Upload Process", "Are you sure you want to cancel the upload process?", "Yes", "No"))
				{
					if (ovrPlatUtilProcess != null)
					{
						ovrPlatUtilProcess.Kill();
						OVRPlatformTool.log += "Upload process was canceled\n";
					}
				}
			}

			GUI.enabled = true;
			GUILayout.FlexibleSpace();

			GUILayout.Space(SINGLE_LINE_SPACING);

			debugLogScroll = EditorGUILayout.BeginScrollView(debugLogScroll);
			GUIStyle logBoxStyle = new GUIStyle();
			logBoxStyle.margin.left = 5;
			logBoxStyle.wordWrap = true;
			logBoxStyle.normal.textColor = logBoxStyle.focused.textColor = EditorStyles.label.normal.textColor;
			EditorGUILayout.SelectableLabel(OVRPlatformTool.log, logBoxStyle, GUILayout.Height(position.height - 30));
			EditorGUILayout.EndScrollView();
		}

		private void SetOVRProjectConfig(TargetPlatform targetPlatform)
		{
#if UNITY_ANDROID

			var targetDeviceTypes = new List<OVRProjectConfig.DeviceType>();

			if (targetPlatform == TargetPlatform.Quest && !OVRDeviceSelector.isTargetDeviceQuest)
			{
				targetDeviceTypes.Add(OVRProjectConfig.DeviceType.Quest);
			}
			else if (targetPlatform == TargetPlatform.OculusGoGearVR && !OVRDeviceSelector.isTargetDeviceGearVrOrGo)
			{
				targetDeviceTypes.Add(OVRProjectConfig.DeviceType.GearVrOrGo);
			}

			if (targetDeviceTypes.Count != 0)
			{
				OVRProjectConfig projectConfig = OVRProjectConfig.GetProjectConfig();
				projectConfig.targetDeviceTypes = targetDeviceTypes;
				OVRProjectConfig.CommitProjectConfig(projectConfig);
			}
#endif
		}

		private void IncrementIndent()
		{
			EditorGUI.indentLevel++;
			EditorGUIUtility.labelWidth = DEFAULT_LABEL_WIDTH - (EditorGUI.indentLevel * INDENT_SPACING);
		}

		private void DecrementIndent()
		{
			EditorGUI.indentLevel--;
			EditorGUIUtility.labelWidth = DEFAULT_LABEL_WIDTH - (EditorGUI.indentLevel * INDENT_SPACING);
		}

		private void OnUpload(TargetPlatform targetPlatform)
		{
			OVRPlatformTool.log = string.Empty;
			SetDirtyOnGUIChange();
			var lintCount = 0;
			if (OVRPlatformToolSettings.RunOvrLint)
			{
				lintCount = OVRLint.RunCheck();
			}
			if (lintCount != 0)
			{
				OVRPlatformTool.log += lintCount.ToString() + " lint suggestions are found. \n" +
					"Please run Oculus\\Tools\\OVR Performance Lint Tool to review and fix lint errors. \n" +
					"You can uncheck Run OVR Lint to bypass lint errors. \n";
				OVRPlugin.SendEvent("oculus_platform_tool_lint", lintCount.ToString());
			}
			else
			{
				// Continue uploading process
				ExecuteCommand(targetPlatform);
			}
		}

		static void ExecuteCommand(TargetPlatform targetPlatform)
		{
			string dataPath = Application.dataPath;
			
			// If we already have a copy of the platform util, check if it needs to be updated
			if (!ranSelfUpdate && File.Exists(dataPath + "/Oculus/VR/Editor/Tools/ovr-platform-util.exe"))
			{
				ranSelfUpdate = true;
				activeProcess = true;
				var updateThread = new Thread(delegate () {
					retryCount = 0;
					CheckForUpdate(dataPath);
				});
				updateThread.Start();
			}

			var thread = new Thread(delegate () {
				// Wait for update process to finish before starting upload process
				while (activeProcess)
				{
					Thread.Sleep(100);
				}
				retryCount = 0;
				Command(targetPlatform, dataPath);
			});
			thread.Start();
		}

		private static string CheckForPlatformUtil(string dataPath)
		{
			string toolDataPath = dataPath + "/Oculus/VR/Editor/Tools";
			if (!Directory.Exists(toolDataPath))
			{
				Directory.CreateDirectory(toolDataPath);
			}

			string platformUtil = toolDataPath + "/ovr-platform-util.exe";
			if (!System.IO.File.Exists(platformUtil))
			{
				OVRPlugin.SendEvent("oculus_platform_tool", "provision_util");
				EditorCoroutine downloadCoroutine = EditorCoroutine.Start(ProvisionPlatformUtil(platformUtil));
				while (!downloadCoroutine.GetCompleted()) { }
			}

			return platformUtil;
		}

		private static void InitializePlatformUtilProcess(string path, string args)
		{
			ovrPlatUtilProcess = new Process();
			var processInfo = new ProcessStartInfo(path, args);

			processInfo.CreateNoWindow = true;
			processInfo.UseShellExecute = false;
			processInfo.RedirectStandardError = true;
			processInfo.RedirectStandardOutput = true;

			ovrPlatUtilProcess.StartInfo = processInfo;
			ovrPlatUtilProcess.EnableRaisingEvents = true;
		}

		static void CheckForUpdate(string dataPath)
		{
			string platformUtilPath = CheckForPlatformUtil(dataPath);
			InitializePlatformUtilProcess(platformUtilPath, "self-update");

			OVRPlatformTool.log += "Checking for update...\n";

			ovrPlatUtilProcess.Exited += new EventHandler(
				(s, e) =>
				{
					if (File.Exists(dataPath + ".ovr-platform-util.exe"))
					{
						OVRPlatformTool.log += "Cleaning up...\n";
						while (File.Exists(dataPath + ".ovr-platform-util.exe")) { }
						OVRPlatformTool.log += "Finished updating platform utility.\n";
					}
					activeProcess = false;
				}
			);

			ovrPlatUtilProcess.OutputDataReceived += new DataReceivedEventHandler(
				(s, e) =>
				{
					if (e.Data != null && e.Data.Length != 0 && !e.Data.Contains("\u001b"))
					{
						OVRPlatformTool.log += e.Data + "\n";
					}
				}
			);

			try
			{
				ovrPlatUtilProcess.Start();
				ovrPlatUtilProcess.BeginOutputReadLine();
			}
			catch
			{
				if (ThrowPlatformUtilStartupError(platformUtilPath))
				{
					CheckForUpdate(dataPath);
				}
			}
		}

		static void LoadRedistPackages(string dataPath)
		{
			// Check / Download the platform util and call list-redists on it
			activeProcess = true;
			string platformUtilPath = CheckForPlatformUtil(dataPath);
			InitializePlatformUtilProcess(platformUtilPath, "list-redists");

			OVRPlatformTool.log += "Loading redistributable packages...\n";

			List<RedistPackage> redistPacks = new List<RedistPackage>();

			ovrPlatUtilProcess.Exited += new EventHandler(
				(s, e) =>
				{
					activeProcess = false;
				}
			);

			ovrPlatUtilProcess.OutputDataReceived += new DataReceivedEventHandler(
				(s, e) =>
				{
					if (e.Data != null && e.Data.Length != 0 && !e.Data.Contains("\u001b") && !e.Data.Contains("ID"))
					{
						// Get the name / ID pair from the CLI and create a redist package instance
						string[] terms = e.Data.Split('|');
						if (terms.Length == 2)
						{
							RedistPackage redistPack = new RedistPackage(terms[1], terms[0]);
							redistPacks.Add(redistPack);
						}
					}
				}
			);

			try
			{
				ovrPlatUtilProcess.Start();
				ovrPlatUtilProcess.BeginOutputReadLine();

				ovrPlatUtilProcess.WaitForExit();

				if (redistPacks.Count != OVRPlatformToolSettings.RiftRedistPackages.Count)
				{
					OVRPlatformTool.log += "Successfully updated redistributable packages.\n";
					OVRPlatformToolSettings.RiftRedistPackages = redistPacks;
				}
				else
				{
					OVRPlatformTool.log += "Redistributable packages up to date.\n";
				}
			}
			catch
			{
				if (ThrowPlatformUtilStartupError(platformUtilPath))
				{
					LoadRedistPackages(dataPath);
				}
			}
		}

		static void Command(TargetPlatform targetPlatform, string dataPath)
		{
			string platformUtilPath = CheckForPlatformUtil(dataPath);

			string args;
			if (genUploadCommand(targetPlatform, out args))
			{
				activeProcess = true;
				InitializePlatformUtilProcess(platformUtilPath, args);

				ovrPlatUtilProcess.Exited += new EventHandler(
					(s, e) =>
					{
						activeProcess = false;
					}
				);

				ovrPlatUtilProcess.OutputDataReceived += new DataReceivedEventHandler(
					(s, e) =>
					{
						if (e.Data != null && e.Data.Length != 0 && !e.Data.Contains("\u001b"))
						{
							OVRPlatformTool.log += e.Data + "\n";
						}
					}
				);
				ovrPlatUtilProcess.ErrorDataReceived += new DataReceivedEventHandler(
					(s, e) =>
					{
						OVRPlatformTool.log += e.Data + "\n";
					}
				);

				try
				{
					ovrPlatUtilProcess.Start();
					ovrPlatUtilProcess.BeginOutputReadLine();
					ovrPlatUtilProcess.BeginErrorReadLine();
				}
				catch
				{
					if (ThrowPlatformUtilStartupError(platformUtilPath))
					{
						Command(targetPlatform, dataPath);
					}
				}
			}
		}

		private static bool genUploadCommand(TargetPlatform targetPlatform, out string command)
		{
			bool success = true;
			command = "";

			switch (targetPlatform)
			{
				case TargetPlatform.Rift:
					command = "upload-rift-build";
					break;
				case TargetPlatform.OculusGoGearVR:
					command = "upload-mobile-build";
					break;
				case TargetPlatform.Quest:
					command = "upload-quest-build";
					break;
				default:
					OVRPlatformTool.log += "ERROR: Invalid target platform selected";
					success = false;
					break;
			}

			// Add App ID
			ValidateTextField(AppIDFieldValidator, OVRPlatformToolSettings.AppID, "App ID", ref success);
			command += " --app-id \"" + OVRPlatformToolSettings.AppID + "\"";

			// Add App Token
			ValidateTextField(GenericFieldValidator, OVRPlatformToolSettings.AppToken, "App Token", ref success);
			command += " --app-secret \"" + OVRPlatformToolSettings.AppToken + "\"";

			// Add Platform specific fields
			if (targetPlatform == TargetPlatform.Rift)
			{
				// Add Rift Build Directory
				ValidateTextField(DirectoryValidator, OVRPlatformToolSettings.RiftBuildDirectory, "Rift Build Directory", ref success);
				command += " --build-dir \"" + OVRPlatformToolSettings.RiftBuildDirectory + "\"";

				// Add Rift Launch File
				ValidateTextField(FileValidator, OVRPlatformToolSettings.RiftLaunchFile, "Rift Launch File Path", ref success);
				command += " --launch-file \"" + OVRPlatformToolSettings.RiftLaunchFile + "\"";

				// Add Rift Build Version
				ValidateTextField(GenericFieldValidator, OVRPlatformToolSettings.RiftBuildVersion, "Build Version", ref success);
				command += " --version \"" + OVRPlatformToolSettings.RiftBuildVersion + "\"";

				// Add Rift Launch Parameters
				if (!string.IsNullOrEmpty(OVRPlatformToolSettings.RiftLaunchParams))
				{
					ValidateTextField(LaunchParameterValidator, OVRPlatformToolSettings.RiftLaunchParams, "Launch Parameters", ref success);
					command += " --launch_params \"" + OVRPlatformToolSettings.RiftLaunchParams + "\"";
				}

				// Add 2D Launch File
				if (!string.IsNullOrEmpty(OVRPlatformToolSettings.Rift2DLaunchFile))
				{
					ValidateTextField(FileValidator, OVRPlatformToolSettings.Rift2DLaunchFile, "2D Launch File", ref success);
					command += " --launch_file_2d \"" + OVRPlatformToolSettings.Rift2DLaunchFile + "\"";

					if (!string.IsNullOrEmpty(OVRPlatformToolSettings.Rift2DLaunchParams))
					{
						ValidateTextField(LaunchParameterValidator, OVRPlatformToolSettings.Rift2DLaunchParams, "2D Launch Parameters", ref success);
						command += " --launch_params_2d \"" + OVRPlatformToolSettings.Rift2DLaunchParams + "\"";
					}
				}

				// Add Firewall Exception
				if (OVRPlatformToolSettings.RiftFirewallException)
				{
					command += " --firewall_exceptions true";
				}

				// Add Redistributable Packages
				List<string> redistCommandIds = new List<string>();
				for (int i = 0; i < OVRPlatformToolSettings.RiftRedistPackages.Count; i++)
				{
					if (OVRPlatformToolSettings.RiftRedistPackages[i].include)
					{
						redistCommandIds.Add(OVRPlatformToolSettings.RiftRedistPackages[i].id);
					}
				}
				if (redistCommandIds.Count > 0)
				{
					command += " --redistributables \"" + string.Join(",", redistCommandIds.ToArray()) + "\"";
				}

				// Add Gamepad Emulation
				if (OVRPlatformToolSettings.RiftGamepadEmulation > OVRPlatformToolSettings.GamepadType.OFF && 
					OVRPlatformToolSettings.RiftGamepadEmulation <= OVRPlatformToolSettings.GamepadType.LEFT_D_PAD)
				{
					command += " --gamepad-emulation ";
					switch (OVRPlatformToolSettings.RiftGamepadEmulation)
					{
						case OVRPlatformToolSettings.GamepadType.TWINSTICK:		command += "TWINSTICK";		break;
						case OVRPlatformToolSettings.GamepadType.RIGHT_D_PAD:	command += "RIGHT_D_PAD";	break;
						case OVRPlatformToolSettings.GamepadType.LEFT_D_PAD:	command += "LEFT_D_PAD";	break;
						default:												command += "OFF";			break;
					}
				}

				// Add Rift Language Pack Directory
				if (!string.IsNullOrEmpty(OVRPlatformToolSettings.LanguagePackDirectory))
				{
					ValidateTextField(DirectoryValidator, OVRPlatformToolSettings.LanguagePackDirectory, "Language Pack Directory", ref success);
					command += " --language_packs_dir \"" + OVRPlatformToolSettings.LanguagePackDirectory + "\"";
				}
			}
			else
			{
				// Add APK Build Path
				ValidateTextField(FileValidator, OVRPlatformToolSettings.ApkBuildPath, "APK Build Path", ref success);
				command += " --apk \"" + OVRPlatformToolSettings.ApkBuildPath + "\"";

				// Add OBB File Path
				if (!string.IsNullOrEmpty(OVRPlatformToolSettings.ObbFilePath))
				{
					ValidateTextField(FileValidator, OVRPlatformToolSettings.ObbFilePath, "OBB File Path", ref success);
					command += " --obb \"" + OVRPlatformToolSettings.ObbFilePath + "\"";
				}

				if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.OculusGoGearVR)
				{
					// Go and Gear VR specific commands
				}
				else if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.Quest)
				{
					// Quest specific commands
				}
			}

			// Add Assets Directory
			if (!string.IsNullOrEmpty(OVRPlatformToolSettings.AssetsDirectory))
			{
				ValidateTextField(DirectoryValidator, OVRPlatformToolSettings.AssetsDirectory, "Assets Directory", ref success);
				command += " --assets-dir \"" + OVRPlatformToolSettings.AssetsDirectory + "\"";

				// Add Asset Configurations
				if (OVRPlatformToolSettings.AssetConfigs.Count > 0)
				{
					List<string> assetConfigs = new List<string>();
					for (int i = 0; i < OVRPlatformToolSettings.AssetConfigs.Count; i++)
					{
						List<string> configParameters = new List<string>();
						AssetConfig config = OVRPlatformToolSettings.AssetConfigs[i];

						if (config.required)
						{
							configParameters.Add("\\\"required\\\":true");
						}
						if (config.type > AssetConfig.AssetType.DEFAULT)
						{
							string typeCommand = "\\\"type\\\":";
							switch (config.type)
							{
								case AssetConfig.AssetType.LANGUAGE_PACK:
									configParameters.Add(typeCommand + "\\\"LANGUAGE_PACK\\\"");
									break;
								case AssetConfig.AssetType.STORE:
									configParameters.Add(typeCommand + "\\\"STORE\\\"");
									break;
								default:
									configParameters.Add(typeCommand + "\\\"DEFAULT\\\"");
									break;
							}
						}
						if (!string.IsNullOrEmpty(config.sku))
						{
							configParameters.Add("\\\"sku\\\":\\\"" + config.sku + "\\\"");
						}

						if (configParameters.Count > 0)
						{
							string configString = "\\\"" + config.name + "\\\":{" + string.Join(",", configParameters.ToArray()) + "}";
							assetConfigs.Add(configString);
						}
					}

					if (assetConfigs.Count > 0)
					{
						command += " --asset_files_config {" + string.Join(",", assetConfigs.ToArray()) + "}";
					}
				}
			}

			// Add Release Channel
			ValidateTextField(GenericFieldValidator, OVRPlatformToolSettings.ReleaseChannel, "Release Channel", ref success);
			command += " --channel \"" + OVRPlatformToolSettings.ReleaseChannel + "\"";

			// Add Notes
			if (!string.IsNullOrEmpty(OVRPlatformToolSettings.ReleaseNote))
			{
				string sanatizedReleaseNote = OVRPlatformToolSettings.ReleaseNote;
				sanatizedReleaseNote = sanatizedReleaseNote.Replace("\"", "\"\"");
				command += " --notes \"" + sanatizedReleaseNote + "\"";
			}

			return success;
		}

		// Private delegate for text field validation functions
		private delegate TSuccess FieldValidatorDelegate<in TText, TError, out TSuccess>(TText text, ref TError error);

		// Validate the text using a given field validator function. An error message will be printed if validation fails. Success will ONLY be modified to false if validation fails.
		static void ValidateTextField(FieldValidatorDelegate<string, string, bool> fieldValidator, string fieldText, string fieldName, ref bool success)
		{
			string error = "";
			if (!fieldValidator(fieldText, ref error))
			{
				OVRPlatformTool.log += "ERROR: Please verify that the " + fieldName + " is correct. ";
				OVRPlatformTool.log += string.IsNullOrEmpty(error) ? "\n" : error + "\n";
				success = false;
			}
		}

		// Checks if the text is null or empty
		static bool GenericFieldValidator(string fieldText, ref string error)
		{
			if (string.IsNullOrEmpty(fieldText))
			{
				error = "The field is empty.";
				return false;
			}
			return true;
		}

		// Checks if the App ID contains only numbers
		static bool AppIDFieldValidator(string fieldText, ref string error)
		{
			if (string.IsNullOrEmpty(fieldText))
			{
				error = "The field is empty.";
				return false;
			}
			else if (!Regex.IsMatch(OVRPlatformToolSettings.AppID, "^[0-9]+$"))
			{
				error = "The field contains invalid characters.";
				return false;
			}
			return true;
		}

		// Check that the directory exists
		static bool DirectoryValidator(string path, ref string error)
		{
			if (!Directory.Exists(path))
			{
				error = "The directory does not exist.";
				return false;
			}
			return true;
		}

		// Check that the file exists
		static bool FileValidator(string path, ref string error)
		{
			if (!File.Exists(path))
			{
				error = "The file does not exist.";
				return false;
			}
			return true;
		}

		// Check if the launch parameter string contains illegal characters
		static bool LaunchParameterValidator(string fieldText, ref string error)
		{
			if (fieldText.Contains("\""))
			{
				error = "The field contains illegal characters.";
				return false;
			}
			return true;
		}

		void OnInspectorUpdate()
		{
			Repaint();
		}

		private static bool ThrowPlatformUtilStartupError(string utilPath)
		{
			if (retryCount < MAX_DOWNLOAD_RETRY_COUNT)
			{
				retryCount++;
				OVRPlatformTool.log += "There was a problem starting Oculus Platform Util. Restarting provision process...\n";
				File.Delete(utilPath);
				return true;
			}
			else
			{
				OVRPlatformTool.log += "OVR Platform Tool had a problem with downloading a valid executable after several trys. Please reopen the tool to try again.\n";
				return false;
			}
		}

		private string MakeTextBox(GUIContent label, string variable)
		{
			string result = string.Empty;

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(false));
			result = EditorGUILayout.TextField(variable);
			EditorGUILayout.EndHorizontal();

			return result;
		}

		private string MakePasswordBox(GUIContent label, string variable)
		{
			string result = string.Empty;

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(false));
			result = EditorGUILayout.PasswordField(variable);
			EditorGUILayout.EndHorizontal();

			return result;
		}

		private bool MakeToggleBox(GUIContent label, bool variable)
		{
			bool result = false;

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(false));
			result = EditorGUILayout.Toggle(variable);
			EditorGUILayout.EndHorizontal();

			return result;
		}

		private int MakePopup(GUIContent label, int variable, string[] options)
		{
			int result = 0;

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(false));
			result = EditorGUILayout.Popup(variable, options);
			EditorGUILayout.EndHorizontal();

			return result;
		}

		private string MakeFileDirectoryField(GUIContent label, string variable, string title, bool isFile = false, string extension = "")
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(false));

			Rect rect = GUILayoutUtility.GetRect(0, SINGLE_LINE_SPACING, GUILayout.ExpandWidth(true));
			EditorGUI.SelectableLabel(rect, variable);

			// Create X button if there is a valid path in the field
			string result = variable;
			if (!string.IsNullOrEmpty(variable))
			{
				Color defaultColor = GUI.backgroundColor;
				GUI.backgroundColor = new Color(.9f, 0.5f, 0.5f);
				rect = GUILayoutUtility.GetRect(SINGLE_LINE_SPACING, SINGLE_LINE_SPACING, GUILayout.ExpandWidth(false));
				if (GUI.Button(rect, "X"))
				{
					result = string.Empty;
				}
				GUI.backgroundColor = defaultColor;
			}

			// Create the Choose button to initiate the file explorer
			rect = GUILayoutUtility.GetRect(75f, SINGLE_LINE_SPACING, GUILayout.ExpandWidth(false));
			if (GUI.Button(rect, "Choose ..."))
			{
				string newPath = string.Empty;
				string path = string.IsNullOrEmpty(variable) ? Application.dataPath : variable;
				if (isFile)
				{
					newPath = EditorUtility.OpenFilePanel(title, path, extension);
				}
				else
				{
					newPath = EditorUtility.OpenFolderPanel(title, path, string.Empty);
				}
				if (newPath.Length > 0)
				{
					result = newPath;
				}
			}
			GUILayout.Space(5f);
			EditorGUILayout.EndHorizontal();

			// If the path has changed, deselect the selectable field so that it can update.
			if (result != variable)
			{
				GUIUtility.hotControl = 0;
				GUIUtility.keyboardControl = 0;
			}

			return result;
		}

		private static void SetDirtyOnGUIChange()
		{
			if (GUI.changed)
			{
				EditorUtility.SetDirty(OVRPlatformToolSettings.Instance);
				GUI.changed = false;
			}
		}

		private static IEnumerator ProvisionPlatformUtil(string dataPath)
		{
#if UNITY_2019_1_OR_NEWER
			var webRequest = new UnityWebRequest(urlPlatformUtil, UnityWebRequest.kHttpVerbGET);
			string path = dataPath;
			webRequest.downloadHandler = new DownloadHandlerFile(path);
			// WWW request timeout in seconds
			webRequest.timeout = 60;
			UnityWebRequestAsyncOperation webOp = webRequest.SendWebRequest();
			while (!webOp.isDone) { }
			if (webRequest.isNetworkError || webRequest.isHttpError)
			{
				var networkErrorMsg = "Failed to provision Oculus Platform Util\n";
				UnityEngine.Debug.LogError(networkErrorMsg);
				OVRPlatformTool.log += networkErrorMsg;
			}
			else
			{
				OVRPlatformTool.log += "Completed Provisioning Oculus Platform Util\n";
			}
			SetDirtyOnGUIChange();
			yield return webOp;
#else
			using (WWW www = new WWW(urlPlatformUtil))
			{
				UnityEngine.Debug.Log("Started Provisioning Oculus Platform Util");
				float timer = 0;
				float timeOut = 60;
				yield return www;
				while (!www.isDone && timer < timeOut)
				{
					timer += Time.deltaTime;
					if (www.error != null)
					{
						UnityEngine.Debug.Log("Download error: " + www.error);
						break;
					}
					OVRPlatformTool.log = string.Format("Downloading.. {0:P1}", www.progress);
					SetDirtyOnGUIChange();
					yield return new WaitForSeconds(1f);
				}
				if (www.isDone)
				{
					System.IO.File.WriteAllBytes(dataPath, www.bytes);
					OVRPlatformTool.log = "Completed Provisioning Oculus Platform Util\n";
					SetDirtyOnGUIChange();
				}
			}
#endif
		}

		private static void DrawAssetConfigList(Rect rect)
		{
			DrawAssetConfigHeader(rect);
			DrawAssetConfigBackground(rect);
			DrawAssetConfigElement(rect);
		}

		private static void DrawAssetConfigElement(Rect rect)
		{
			Rect elementRect = new Rect(rect.x, rect.y + SINGLE_LINE_SPACING + ASSET_CONFIG_BACKGROUND_PADDING / 2,
				rect.width, SINGLE_LINE_SPACING);
			if (OVRPlatformToolSettings.AssetConfigs.Count > 0)
			{
				for (int i = 0; i < OVRPlatformToolSettings.AssetConfigs.Count; i++)
				{
					AssetConfig config = OVRPlatformToolSettings.AssetConfigs[i];
					GUIContent fieldLabel;

					config.SetFoldoutState(EditorGUI.Foldout(elementRect, config.GetFoldoutState(), config.name, boldFoldoutStyle));
					if (config.GetFoldoutState())
					{
						Rect attributeRect = new Rect(elementRect.x + INDENT_SPACING, elementRect.y + SINGLE_LINE_SPACING,
							elementRect.width - INDENT_SPACING - 3f, SINGLE_LINE_SPACING);
						// Extra asset config params are disabled for now until CLI supports them
#if !DISABLE_EXTRA_ASSET_CONFIG
						fieldLabel = new GUIContent("Required Asset [?]", "Whether or not this asset file is required for the app to run.");
						config.required = EditorGUI.Toggle(attributeRect, fieldLabel, config.required);

						attributeRect.y += SINGLE_LINE_SPACING;
						fieldLabel = new GUIContent("Asset Type [?]", "The asset file type.");
						config.type = (AssetConfig.AssetType)EditorGUI.EnumPopup(attributeRect, fieldLabel, config.type);

						attributeRect.y += SINGLE_LINE_SPACING;
#endif
						fieldLabel = new GUIContent("Asset SKU [?]", "The Oculus store SKU for this asset file.");
						config.sku = EditorGUI.TextField(attributeRect, fieldLabel, config.sku);

						elementRect.y = attributeRect.y;
					}
					elementRect.y += SINGLE_LINE_SPACING;
				}
			}
			else
			{
				EditorGUI.LabelField(elementRect, "No asset files found. Choose a valid assets directory.");
			}
		}

		private static float GetAssetConfigElementHeight()
		{
			float totalHeight = 0f;
			if (OVRPlatformToolSettings.AssetConfigs.Count > 0)
			{
				for (int i = 0; i < OVRPlatformToolSettings.AssetConfigs.Count; i++)
				{
					AssetConfig config = OVRPlatformToolSettings.AssetConfigs[i];
#if !DISABLE_EXTRA_ASSET_CONFIG
					totalHeight += config.GetFoldoutState() ? SINGLE_LINE_SPACING * 4 : SINGLE_LINE_SPACING;
#else
					totalHeight += config.GetFoldoutState() ? SINGLE_LINE_SPACING * 2 : SINGLE_LINE_SPACING;
#endif
				}
			}
			else
			{
				totalHeight += SINGLE_LINE_SPACING;
			}
			return totalHeight + ASSET_CONFIG_BACKGROUND_PADDING;
		}

		private static void DrawAssetConfigHeader(Rect rect)
		{
			Rect headerRect = new Rect(rect.x, rect.y, rect.width, SINGLE_LINE_SPACING);
			EditorGUI.DrawRect(headerRect, EditorGUIUtility.isProSkin ? new Color(0.37f, 0.37f, 0.37f) : new Color(0.55f, 0.55f, 0.55f));
			EditorGUI.LabelField(rect, "Asset File Configuration");
		}

		private static void DrawAssetConfigBackground(Rect rect)
		{
			Rect backgroundRect = new Rect(rect.x, rect.y + SINGLE_LINE_SPACING, rect.width, GetAssetConfigElementHeight());
			EditorGUI.DrawRect(backgroundRect, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.63f, 0.63f, 0.63f));
		}

		class GUIHelper
		{
			public delegate void Worker();

			static void InOut(Worker begin, Worker body, Worker end)
			{
				try
				{
					begin();
					body();
				}
				finally
				{
					end();
				}
			}

			public static void HInset(int pixels, Worker worker)
			{
				InOut(
					() => {
						GUILayout.BeginHorizontal();
						GUILayout.Space(pixels);
						GUILayout.BeginVertical();
					},
					worker,
					() => {
						GUILayout.EndVertical();
						GUILayout.EndHorizontal();
					}
				);
			}

			public delegate T ControlWorker<T>();
			public static T MakeControlWithLabel<T>(GUIContent label, ControlWorker<T> worker)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(label);

				var result = worker();

				EditorGUILayout.EndHorizontal();
				return result;
			}
		}

		public class EditorCoroutine
		{
			public static EditorCoroutine Start(IEnumerator routine)
			{
				EditorCoroutine coroutine = new EditorCoroutine(routine);
				coroutine.Start();
				return coroutine;
			}

			readonly IEnumerator routine;
			bool completed;
			EditorCoroutine(IEnumerator _routine)
			{
				routine = _routine;
				completed = false;
			}

			void Start()
			{
				EditorApplication.update += Update;
			}
			public void Stop()
			{
				EditorApplication.update -= Update;
				completed = true;
			}

			public bool GetCompleted()
			{
				return completed;
			}

			void Update()
			{
				if (!routine.MoveNext())
				{
					Stop();
				}
			}
		}
	}
}
