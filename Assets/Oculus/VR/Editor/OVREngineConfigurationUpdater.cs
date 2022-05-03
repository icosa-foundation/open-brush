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
using System.IO;

[InitializeOnLoad]
class OVREngineConfigurationUpdater
{
	private const string prefName = "OVREngineConfigurationUpdater_Enabled";
	private const string menuItemName = "Oculus/Tools/Use Required Project Settings";
	private const string androidAssetsPath = "Assets/Plugins/Android/assets";
	private const string androidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
	static bool setPrefsForUtilities;

	[MenuItem(menuItemName)]
	static void ToggleUtilities()
	{
		setPrefsForUtilities = !setPrefsForUtilities;
		Menu.SetChecked(menuItemName, setPrefsForUtilities);

		int newValue = (setPrefsForUtilities) ? 1 : 0;
		PlayerPrefs.SetInt(prefName, newValue);
		PlayerPrefs.Save();

		Debug.Log("Using required project settings: " + setPrefsForUtilities);
	}
	
#if UNITY_2017_3_OR_NEWER
	private static readonly string dashSupportEnableConfirmedKey = "Oculus_Utilities_OVREngineConfiguration_DashSupportEnableConfirmed_" + Application.unityVersion + OVRManager.utilitiesVersion;
	private static bool dashSupportEnableConfirmed
	{
		get
		{
			return PlayerPrefs.GetInt(dashSupportEnableConfirmedKey, 0) == 1;
		}

		set
		{
			PlayerPrefs.SetInt(dashSupportEnableConfirmedKey, value ? 1 : 0);
		}
	}
	
	private static void DashSupportWarningPrompt()
	{
		/// <summary>
		/// Since Unity 2017.3.0f1 and 2017.3.0f2 have "Dash Support" enabled by default
		/// We need prompt developers in case they never test their app with dash
		/// </summary>
		/// 
		if (Application.unityVersion == "2017.3.0f1" || Application.unityVersion == "2017.3.0f2")
		{
			if (!dashSupportEnableConfirmed)
			{
				bool dialogResult = EditorUtility.DisplayDialog("Oculus Dash support", "Your current Unity engine " + Application.unityVersion +
					" has Oculus Dash Supporting enabled by default. please make sure to test your app with Dash enabled runtime 1.21 or newer," +
					" Otherwise, you can also turn it off under XR Settings -> Oculus", "Understand", "Learn more ");

				if (!dialogResult)
				{
					Application.OpenURL("https://developer.oculus.com/documentation/unity/latest/concepts/unity-lifecycle/");
				}

				dashSupportEnableConfirmed = true;
			}
		}
	}
#endif

    static OVREngineConfigurationUpdater()
	{
		EditorApplication.delayCall += OnDelayCall;
		EditorApplication.update += OnUpdate;

#if UNITY_2017_3_OR_NEWER
		DashSupportWarningPrompt();
#endif
	}

	static void OnDelayCall()
	{
		setPrefsForUtilities = PlayerPrefs.GetInt(prefName, 1) != 0;
		Menu.SetChecked(menuItemName, setPrefsForUtilities);

		if (!setPrefsForUtilities)
			return;

		OVRPlugin.AddCustomMetadata("build_target", EditorUserBuildSettings.activeBuildTarget.ToString());
		EnforceAndroidSettings();
		EnforceInputManagerBindings();
#if UNITY_ANDROID
		EnforceOSIG();
#endif
	}

	static void OnUpdate()
	{
		if (!setPrefsForUtilities)
			return;
		
		EnforceBundleId();
#if !USING_XR_SDK
		EnforceVRSupport();
#endif
		EnforceInstallLocation();
	}

	static void EnforceAndroidSettings()
	{
		if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
			return;

		if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
		{
			Debug.Log("OVREngineConfigurationUpdater: Setting orientation to Landscape Left");
			// Default screen orientation must be set to landscape left.
			PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
		}

		if (!PlayerSettings.virtualRealitySupported)
		{
			// NOTE: This value should not affect the main window surface
			// when Built-in VR support is enabled.

			// NOTE: On Adreno Lollipop, it is an error to have antiAliasing set on the
			// main window surface with front buffer rendering enabled. The view will
			// render black.
			// On Adreno KitKat, some tiling control modes will cause the view to render
			// black.
			if (QualitySettings.antiAliasing != 0 && QualitySettings.antiAliasing != 1)
			{
				Debug.Log("OVREngineConfigurationUpdater: Disabling antiAliasing");
				QualitySettings.antiAliasing = 1;
			}
		}

		if (QualitySettings.vSyncCount != 0)
		{
			Debug.Log("OVREngineConfigurationUpdater: Setting vsyncCount to 0");
			// We sync in the TimeWarp, so we don't want unity syncing elsewhere.
			QualitySettings.vSyncCount = 0;
		}
	}

	static void EnforceVRSupport()
	{
		if (PlayerSettings.virtualRealitySupported)
			return;
		
		var mgrs = GameObject.FindObjectsOfType<OVRManager>();
		for (int i = 0; i < mgrs.Length; ++i)
		{
			if (mgrs [i].isActiveAndEnabled)
			{
				Debug.Log ("Enabling Unity VR support");
				PlayerSettings.virtualRealitySupported = true;

				bool oculusFound = false;
#if UNITY_2017_2_OR_NEWER
				foreach (var device in UnityEngine.XR.XRSettings.supportedDevices)
#else
				foreach (var device in UnityEngine.VR.VRSettings.supportedDevices)
#endif
					oculusFound |= (device == "Oculus");

				if (!oculusFound)
					Debug.LogError("Please add Oculus to the list of supported devices to use the Utilities.");

				return;
			}
		}
	}

	private static void EnforceBundleId()
	{
		if (!PlayerSettings.virtualRealitySupported)
			return;

		if (PlayerSettings.applicationIdentifier == "" || PlayerSettings.applicationIdentifier == "com.Company.ProductName")
		{
			string defaultBundleId = "com.oculus.UnitySample";
			Debug.LogWarning("\"" + PlayerSettings.applicationIdentifier + "\" is not a valid bundle identifier. Defaulting to \"" + defaultBundleId + "\".");
			PlayerSettings.applicationIdentifier = defaultBundleId;
		}
	}

	private static void EnforceInstallLocation()
	{
		if (PlayerSettings.Android.preferredInstallLocation != AndroidPreferredInstallLocation.Auto)
			PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
	}

	private static void EnforceInputManagerBindings()
	{
		try
		{
			BindAxis(new Axis() { name = "Oculus_GearVR_LThumbstickX",  axis =  0,               });
			BindAxis(new Axis() { name = "Oculus_GearVR_LThumbstickY",  axis =  1, invert = true });
			BindAxis(new Axis() { name = "Oculus_GearVR_RThumbstickX",  axis =  2,               });
			BindAxis(new Axis() { name = "Oculus_GearVR_RThumbstickY",  axis =  3, invert = true });
			BindAxis(new Axis() { name = "Oculus_GearVR_DpadX",         axis =  4,               });
			BindAxis(new Axis() { name = "Oculus_GearVR_DpadY",         axis =  5, invert = true });
			BindAxis(new Axis() { name = "Oculus_GearVR_LIndexTrigger", axis = 12,               });
			BindAxis(new Axis() { name = "Oculus_GearVR_RIndexTrigger", axis = 11,               });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_Button2", positiveButton = "joystick button 0", gravity = 1000f, sensitivity = 1000f, type = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_Button4", positiveButton = "joystick button 2", gravity = 1000f, sensitivity = 1000f, type = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_PrimaryThumbstick", positiveButton = "joystick button 8", gravity = 0f, dead = 0f, sensitivity = 0.1f, type = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_SecondaryThumbstick", positiveButton = "joystick button 9", gravity = 0f, dead = 0f, sensitivity = 0.1f, type = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_PrimaryIndexTrigger", dead = 0.19f, type = 2, axis = 8, joyNum = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_SecondaryIndexTrigger", dead = 0.19f, type = 2, axis = 9, joyNum = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_PrimaryHandTrigger", dead = 0.19f, type = 2, axis = 10, joyNum = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_SecondaryHandTrigger", dead = 0.19f, type = 2, axis = 11, joyNum = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_PrimaryThumbstickHorizontal", dead = 0.19f, type = 2, axis = 0, joyNum = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_PrimaryThumbstickVertical", dead = 0.19f, type = 2, axis = 1, joyNum = 0, invert = true });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_SecondaryThumbstickHorizontal", dead = 0.19f, type = 2, axis = 3, joyNum = 0 });
			BindAxis(new Axis() { name = "Oculus_CrossPlatform_SecondaryThumbstickVertical", dead = 0.19f, type = 2, axis = 4, joyNum = 0, invert = true });
		}
		catch
		{
			Debug.LogError("Failed to apply Oculus GearVR input manager bindings.");
		}
	}

	private static void EnforceOSIG()
	{
		// Don't bug the user in play mode.
		if (Application.isPlaying)
			return;
		
		// Don't warn if the project may be set up for submission or global signing.
		if (File.Exists(androidManifestPath))
			return;

		bool foundPossibleOsig = false;
		if (Directory.Exists(androidAssetsPath))
		{
			var files = Directory.GetFiles(androidAssetsPath);
			for (int i = 0; i < files.Length; ++i)
			{
				if (!files[i].Contains(".txt"))
				{
					foundPossibleOsig = true;
					break;
				}
			}
		}

		if (!foundPossibleOsig)
			Debug.LogWarning("Missing Gear VR OSIG at " + androidAssetsPath + ". Please see https://dashboard.oculus.com/tools/osig-generator");
	}

	private class Axis
	{
		public string name = String.Empty;
		public string descriptiveName = String.Empty;
		public string descriptiveNegativeName = String.Empty;
		public string negativeButton = String.Empty;
		public string positiveButton = String.Empty;
		public string altNegativeButton = String.Empty;
		public string altPositiveButton = String.Empty;
		public float gravity = 0.0f;
		public float dead = 0.001f;
		public float sensitivity = 1.0f;
		public bool snap = false;
		public bool invert = false;
		public int type = 2;
		public int axis = 0;
		public int joyNum = 0;
	}

	private static void BindAxis(Axis axis)
	{
		SerializedObject serializedObject = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
		SerializedProperty axesProperty = serializedObject.FindProperty("m_Axes");

		SerializedProperty axisIter = axesProperty.Copy();
		axisIter.Next(true);
		axisIter.Next(true);
		while (axisIter.Next(false))
		{
			if (axisIter.FindPropertyRelative("m_Name").stringValue == axis.name)
			{
				// Axis already exists. Don't create binding.
				return;
			}
		}

		axesProperty.arraySize++;
		serializedObject.ApplyModifiedProperties();

		SerializedProperty axisProperty = axesProperty.GetArrayElementAtIndex(axesProperty.arraySize - 1);
		axisProperty.FindPropertyRelative("m_Name").stringValue = axis.name;
		axisProperty.FindPropertyRelative("descriptiveName").stringValue = axis.descriptiveName;
		axisProperty.FindPropertyRelative("descriptiveNegativeName").stringValue = axis.descriptiveNegativeName;
		axisProperty.FindPropertyRelative("negativeButton").stringValue = axis.negativeButton;
		axisProperty.FindPropertyRelative("positiveButton").stringValue = axis.positiveButton;
		axisProperty.FindPropertyRelative("altNegativeButton").stringValue = axis.altNegativeButton;
		axisProperty.FindPropertyRelative("altPositiveButton").stringValue = axis.altPositiveButton;
		axisProperty.FindPropertyRelative("gravity").floatValue = axis.gravity;
		axisProperty.FindPropertyRelative("dead").floatValue = axis.dead;
		axisProperty.FindPropertyRelative("sensitivity").floatValue = axis.sensitivity;
		axisProperty.FindPropertyRelative("snap").boolValue = axis.snap;
		axisProperty.FindPropertyRelative("invert").boolValue = axis.invert;
		axisProperty.FindPropertyRelative("type").intValue = axis.type;
		axisProperty.FindPropertyRelative("axis").intValue = axis.axis;
		axisProperty.FindPropertyRelative("joyNum").intValue = axis.joyNum;
		serializedObject.ApplyModifiedProperties();
	}
}

