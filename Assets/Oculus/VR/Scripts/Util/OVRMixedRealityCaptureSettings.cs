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

#if UNITY_ANDROID && !UNITY_EDITOR
#define OVR_ANDROID_MRC
#endif

using UnityEngine;
using System;
using System.IO;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || OVR_ANDROID_MRC
public class OVRMixedRealityCaptureSettings : ScriptableObject
{
	public bool enableMixedReality = false;
	public LayerMask extraHiddenLayers;
	public OVRManager.CompositionMethod compositionMethod = OVRManager.CompositionMethod.External;
	public Color externalCompositionBackdropColorRift = Color.green;
	public Color externalCompositionBackdropColorQuest = Color.clear;
	public OVRManager.CameraDevice capturingCameraDevice = OVRManager.CameraDevice.WebCamera0;
	public bool flipCameraFrameHorizontally = false;
	public bool flipCameraFrameVertically = false;
	public float handPoseStateLatency = 0.0f;
	public float sandwichCompositionRenderLatency = 0.0f;
	public int sandwichCompositionBufferedFrames = 8;
	public Color chromaKeyColor = Color.green;
	public float chromaKeySimilarity = 0.6f;
	public float chromaKeySmoothRange = 0.03f;
	public float chromaKeySpillRange = 0.04f;
	public bool useDynamicLighting = false;
	public OVRManager.DepthQuality depthQuality = OVRManager.DepthQuality.Medium;
	public float dynamicLightingSmoothFactor = 8.0f;
	public float dynamicLightingDepthVariationClampingValue = 0.001f;
	public OVRManager.VirtualGreenScreenType virtualGreenScreenType = OVRManager.VirtualGreenScreenType.Off;
	public float virtualGreenScreenTopY;
	public float virtualGreenScreenBottomY;
	public bool virtualGreenScreenApplyDepthCulling = false;
	public float virtualGreenScreenDepthTolerance = 0.2f;

	public void ReadFrom(OVRManager manager)
	{
		enableMixedReality = manager.enableMixedReality;
		compositionMethod = manager.compositionMethod;
		extraHiddenLayers = manager.extraHiddenLayers;
		externalCompositionBackdropColorRift = manager.externalCompositionBackdropColorRift;
		externalCompositionBackdropColorQuest = manager.externalCompositionBackdropColorQuest;
		capturingCameraDevice = manager.capturingCameraDevice;
		flipCameraFrameHorizontally = manager.flipCameraFrameHorizontally;
		flipCameraFrameVertically = manager.flipCameraFrameVertically;
		handPoseStateLatency = manager.handPoseStateLatency;
		sandwichCompositionRenderLatency = manager.sandwichCompositionRenderLatency;
		sandwichCompositionBufferedFrames = manager.sandwichCompositionBufferedFrames;
		chromaKeyColor = manager.chromaKeyColor;
		chromaKeySimilarity = manager.chromaKeySimilarity;
		chromaKeySmoothRange = manager.chromaKeySmoothRange;
		chromaKeySpillRange = manager.chromaKeySpillRange;
		useDynamicLighting = manager.useDynamicLighting;
		depthQuality = manager.depthQuality;
		dynamicLightingSmoothFactor = manager.dynamicLightingSmoothFactor;
		dynamicLightingDepthVariationClampingValue = manager.dynamicLightingDepthVariationClampingValue;
		virtualGreenScreenType = manager.virtualGreenScreenType;
		virtualGreenScreenTopY = manager.virtualGreenScreenTopY;
		virtualGreenScreenBottomY = manager.virtualGreenScreenBottomY;
		virtualGreenScreenApplyDepthCulling = manager.virtualGreenScreenApplyDepthCulling;
		virtualGreenScreenDepthTolerance = manager.virtualGreenScreenDepthTolerance;
	}
	public void ApplyTo(OVRManager manager)
	{
		manager.enableMixedReality = enableMixedReality;
		manager.compositionMethod = compositionMethod;
		manager.extraHiddenLayers = extraHiddenLayers;
		manager.externalCompositionBackdropColorRift = externalCompositionBackdropColorRift;
		manager.externalCompositionBackdropColorQuest = externalCompositionBackdropColorQuest;
		manager.capturingCameraDevice = capturingCameraDevice;
		manager.flipCameraFrameHorizontally = flipCameraFrameHorizontally;
		manager.flipCameraFrameVertically = flipCameraFrameVertically;
		manager.handPoseStateLatency = handPoseStateLatency;
		manager.sandwichCompositionRenderLatency = sandwichCompositionRenderLatency;
		manager.sandwichCompositionBufferedFrames = sandwichCompositionBufferedFrames;
		manager.chromaKeyColor = chromaKeyColor;
		manager.chromaKeySimilarity = chromaKeySimilarity;
		manager.chromaKeySmoothRange = chromaKeySmoothRange;
		manager.chromaKeySpillRange = chromaKeySpillRange;
		manager.useDynamicLighting = useDynamicLighting;
		manager.depthQuality = depthQuality;
		manager.dynamicLightingSmoothFactor = dynamicLightingSmoothFactor;
		manager.dynamicLightingDepthVariationClampingValue = dynamicLightingDepthVariationClampingValue;
		manager.virtualGreenScreenType = virtualGreenScreenType;
		manager.virtualGreenScreenTopY = virtualGreenScreenTopY;
		manager.virtualGreenScreenBottomY = virtualGreenScreenBottomY;
		manager.virtualGreenScreenApplyDepthCulling = virtualGreenScreenApplyDepthCulling;
		manager.virtualGreenScreenDepthTolerance = virtualGreenScreenDepthTolerance;
	}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN        // Rift MRC only
    const string configFileName = "mrc.config";
	public void WriteToConfigurationFile()
	{
		string text = JsonUtility.ToJson(this, true);
		try
		{
			string configPath = Path.Combine(Application.dataPath, configFileName);
			Debug.Log("Write OVRMixedRealityCaptureSettings to " + configPath);
			File.WriteAllText(configPath, text);
		}
		catch(Exception e)
		{
			Debug.LogWarning("Exception caught " + e.Message);
		}
	}

	public void CombineWithConfigurationFile()
	{
		try
		{
			string configPath = Path.Combine(Application.dataPath, configFileName);
			if (File.Exists(configPath))
			{
				Debug.Log("MixedRealityCapture configuration file found at " + configPath);
				string text = File.ReadAllText(configPath);
				Debug.Log("Apply MixedRealityCapture configuration");
				JsonUtility.FromJsonOverwrite(text, this);
			}
			else
			{
				Debug.Log("MixedRealityCapture configuration file doesn't exist at " + configPath);
			}
		}
		catch(Exception e)
		{
			Debug.LogWarning("Exception caught " + e.Message);
		}
	}
#endif
}
#endif
