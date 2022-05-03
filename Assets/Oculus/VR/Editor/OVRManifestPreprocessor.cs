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
using System.IO;

public class OVRManifestPreprocessor
{
	[MenuItem("Oculus/Tools/Create store-compatible AndroidManifest.xml", false, 100000)]
	public static void GenerateManifestForSubmission()
	{
		var so = ScriptableObject.CreateInstance(typeof(OVRPluginUpdaterStub));
		var script = MonoScript.FromScriptableObject(so);
		string assetPath = AssetDatabase.GetAssetPath(script);
		string editorDir = Directory.GetParent(assetPath).FullName;
		string srcFile = editorDir + "/AndroidManifest.OVRSubmission.xml";

		if (!File.Exists(srcFile))
		{
			Debug.LogError("Cannot find Android manifest template for submission." +
				" Please delete the OVR folder and reimport the Oculus Utilities.");
			return;
		}

		string manifestFolder = Application.dataPath + "/Plugins/Android";

		if (!Directory.Exists(manifestFolder))
			Directory.CreateDirectory(manifestFolder);

		string dstFile = manifestFolder + "/AndroidManifest.xml";

		if (File.Exists(dstFile))
		{
			Debug.LogWarning("Cannot create Oculus store-compatible manifest due to conflicting file: \""
				+ dstFile + "\". Please remove it and try again.");
			return;
		}

		string manifestText = File.ReadAllText(srcFile);
		int dofTextIndex = manifestText.IndexOf("<!-- Request the headset DoF mode -->");
		if (dofTextIndex != -1)
		{
			if (OVRDeviceSelector.isTargetDeviceQuest)
			{
				string headTrackingFeatureText = string.Format("<uses-feature android:name=\"android.hardware.vr.headtracking\" android:version=\"1\" android:required=\"{0}\" />", 
					OVRDeviceSelector.isTargetDeviceGearVrOrGo ? "false" : "true");
				manifestText = manifestText.Insert(dofTextIndex, headTrackingFeatureText);
			}
		}
		else
		{
			Debug.LogWarning("Manifest error: unable to locate headset DoF mode");
		}

		System.IO.File.WriteAllText(dstFile, manifestText);
		AssetDatabase.Refresh();
	}

	[MenuItem("Oculus/Tools/Remove AndroidManifest.xml")]
	public static void RemoveAndroidManifest()
	{
		AssetDatabase.DeleteAsset("Assets/Plugins/Android/AndroidManifest.xml");
		AssetDatabase.Refresh();
	}
}
