/************************************************************************************
Copyright : Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Licensed under the Oculus Utilities SDK License Version 1.31 (the "License"); you may not use
the Utilities SDK except in compliance with the License, which is provided at the time of installation
or download, or which otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at
https://developer.oculus.com/licenses/utilities-1.31

Unless required by applicable law or agreed to in writing, the Utilities SDK distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
ANY KIND, either express or implied. See the License for the specific language governing
permissions and limitations under the License.
************************************************************************************/

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

[CustomEditor(typeof(OVRManager))]
public class OVRManagerEditor : Editor
{
	override public void OnInspectorGUI()
	{
#if UNITY_ANDROID
		EditorGUILayout.LabelField("Target Devices");
		EditorGUI.indentLevel++;
		OVRProjectConfig projectConfig = OVRProjectConfig.GetProjectConfig();
		List<OVRProjectConfig.DeviceType> oldTargetDeviceTypes = projectConfig.targetDeviceTypes;
		List<OVRProjectConfig.DeviceType> targetDeviceTypes = new List<OVRProjectConfig.DeviceType>(oldTargetDeviceTypes);
		bool hasModified = false;
		int newCount = Mathf.Max(0, EditorGUILayout.IntField("Size", targetDeviceTypes.Count));
		while (newCount < targetDeviceTypes.Count)
		{
			targetDeviceTypes.RemoveAt(targetDeviceTypes.Count - 1);
			hasModified = true;
		}
		while (newCount > targetDeviceTypes.Count)
		{
			targetDeviceTypes.Add(OVRProjectConfig.DeviceType.Quest);
			hasModified = true;
		}
		for (int i = 0; i < targetDeviceTypes.Count; i++)
		{
			var deviceType = (OVRProjectConfig.DeviceType)EditorGUILayout.EnumPopup(string.Format("Element {0}", i), targetDeviceTypes[i]);
			if (deviceType != targetDeviceTypes[i])
			{
				targetDeviceTypes[i] = deviceType;
				hasModified = true;
			}
		}
		if (hasModified)
		{
			projectConfig.targetDeviceTypes = targetDeviceTypes;
			OVRProjectConfig.CommitProjectConfig(projectConfig);
		}
		EditorGUI.indentLevel--;
		EditorGUILayout.Space();
#endif

		DrawDefaultInspector();

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_ANDROID
		OVRManager manager = (OVRManager)target;
#endif

#if UNITY_ANDROID
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Security", EditorStyles.boldLabel);
		EditorGUI.BeginChangeCheck();

		bool disableBackups = projectConfig.disableBackups;
		bool enableNSCConfig = projectConfig.enableNSCConfig;
		SetupBoolField("Disable Backups", ref disableBackups);
		SetupBoolField("Enable NSC Configuration", ref enableNSCConfig);

		if(EditorGUI.EndChangeCheck())
		{
			projectConfig.disableBackups = disableBackups;
			projectConfig.enableNSCConfig = enableNSCConfig;
			OVRProjectConfig.CommitProjectConfig(projectConfig);
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Mixed Reality Capture for Quest (experimental)", EditorStyles.boldLabel);
		EditorGUI.indentLevel++;
		SetupMrcActivationModeField("ActivationMode", ref manager.mrcActivationMode);
		EditorGUI.indentLevel--;
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Mixed Reality Capture", EditorStyles.boldLabel);
		SetupBoolField("Show Properties", ref manager.expandMixedRealityCapturePropertySheet);
		if (manager.expandMixedRealityCapturePropertySheet)
		{
			string[] layerMaskOptions = new string[32];
			for (int i=0; i<32; ++i)
			{
				layerMaskOptions[i] = LayerMask.LayerToName(i);
				if (layerMaskOptions[i].Length == 0)
				{
					layerMaskOptions[i] = "<Layer " + i.ToString() + ">";
				}
			}

			EditorGUI.indentLevel++;

			EditorGUILayout.Space();
			SetupBoolField("enableMixedReality", ref manager.enableMixedReality);
			SetupCompositoinMethodField("compositionMethod", ref manager.compositionMethod);
			SetupLayerMaskField("extraHiddenLayers", ref manager.extraHiddenLayers, layerMaskOptions);

			if (manager.compositionMethod == OVRManager.CompositionMethod.External)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("External Composition", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;

				SetupColorField("backdropColor (Rift)", ref manager.externalCompositionBackdropColorRift);
				SetupColorField("backdropColor (Quest)", ref manager.externalCompositionBackdropColorQuest);
			}

			if (manager.compositionMethod == OVRManager.CompositionMethod.Direct)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Direct Composition", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
				SetupCameraDeviceField("capturingCameraDevice", ref manager.capturingCameraDevice);
				SetupBoolField("flipCameraFrameHorizontally", ref manager.flipCameraFrameHorizontally);
				SetupBoolField("flipCameraFrameVertically", ref manager.flipCameraFrameVertically);

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Chroma Key", EditorStyles.boldLabel);
				SetupColorField("chromaKeyColor", ref manager.chromaKeyColor);
				SetupFloatField("chromaKeySimilarity", ref manager.chromaKeySimilarity);
				SetupFloatField("chromaKeySmoothRange", ref manager.chromaKeySmoothRange);
				SetupFloatField("chromaKeySpillRange", ref manager.chromaKeySpillRange);

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Dynamic Lighting", EditorStyles.boldLabel);
				SetupBoolField("useDynamicLighting", ref manager.useDynamicLighting);
				SetupDepthQualityField("depthQuality", ref manager.depthQuality);
				SetupFloatField("dynamicLightingSmoothFactor", ref manager.dynamicLightingSmoothFactor);
				SetupFloatField("dynamicLightingDepthVariationClampingValue", ref manager.dynamicLightingDepthVariationClampingValue);

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Virtual Green Screen", EditorStyles.boldLabel);
				SetupVirtualGreenTypeField("virtualGreenScreenType", ref manager.virtualGreenScreenType);
				SetupFloatField("virtualGreenScreenTopY", ref manager.virtualGreenScreenTopY);
				SetupFloatField("virtualGreenScreenBottomY", ref manager.virtualGreenScreenBottomY);
				SetupBoolField("virtualGreenScreenApplyDepthCulling", ref manager.virtualGreenScreenApplyDepthCulling);
				SetupFloatField("virtualGreenScreenDepthTolerance", ref manager.virtualGreenScreenDepthTolerance);

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Latency Control", EditorStyles.boldLabel);
				SetupFloatField("handPoseStateLatency", ref manager.handPoseStateLatency);
				EditorGUI.indentLevel--;
			}

			EditorGUI.indentLevel--;
		}
#endif
	}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_ANDROID
	void SetupBoolField(string name, ref bool member)
	{
		EditorGUI.BeginChangeCheck();
		bool value = EditorGUILayout.Toggle(name, member);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			member = value;
		}
	}

	void SetupIntField(string name, ref int member)
	{
		EditorGUI.BeginChangeCheck();
		int value = EditorGUILayout.IntField(name, member);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			member = value;
		}
	}

	void SetupFloatField(string name, ref float member)
	{
		EditorGUI.BeginChangeCheck();
		float value = EditorGUILayout.FloatField(name, member);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			member = value;
		}
	}

	void SetupDoubleField(string name, ref double member)
	{
		EditorGUI.BeginChangeCheck();
		double value = EditorGUILayout.DoubleField(name, member);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			member = value;
		}
	}
	void SetupColorField(string name, ref Color member)
	{
		EditorGUI.BeginChangeCheck();
		Color value = EditorGUILayout.ColorField(name, member);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			member = value;
		}
	}

	void SetupLayerMaskField(string name, ref LayerMask layerMask, string[] layerMaskOptions)
	{
		EditorGUI.BeginChangeCheck();
		int value = EditorGUILayout.MaskField(name, layerMask, layerMaskOptions);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			layerMask = value;
		}
	}

	void SetupCompositoinMethodField(string name, ref OVRManager.CompositionMethod method)
	{
		EditorGUI.BeginChangeCheck();
		OVRManager.CompositionMethod value = (OVRManager.CompositionMethod)EditorGUILayout.EnumPopup(name, method);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			method = value;
		}
	}

	void SetupCameraDeviceField(string name, ref OVRManager.CameraDevice device)
	{
		EditorGUI.BeginChangeCheck();
		OVRManager.CameraDevice value = (OVRManager.CameraDevice)EditorGUILayout.EnumPopup(name, device);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			device = value;
		}
	}

	void SetupDepthQualityField(string name, ref OVRManager.DepthQuality depthQuality)
	{
		EditorGUI.BeginChangeCheck();
		OVRManager.DepthQuality value = (OVRManager.DepthQuality)EditorGUILayout.EnumPopup(name, depthQuality);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			depthQuality = value;
		}
	}

	void SetupVirtualGreenTypeField(string name, ref OVRManager.VirtualGreenScreenType virtualGreenScreenType)
	{
		EditorGUI.BeginChangeCheck();
		OVRManager.VirtualGreenScreenType value = (OVRManager.VirtualGreenScreenType)EditorGUILayout.EnumPopup(name, virtualGreenScreenType);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			virtualGreenScreenType = value;
		}
	}

	void SetupMrcActivationModeField(string name, ref OVRManager.MrcActivationMode mrcActivationMode)
	{
		EditorGUI.BeginChangeCheck();
		OVRManager.MrcActivationMode value = (OVRManager.MrcActivationMode)EditorGUILayout.EnumPopup(name, mrcActivationMode);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(target, "Changed " + name);
			mrcActivationMode = value;
		}
	}

#endif
}
