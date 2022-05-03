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
using System.Collections.Generic;
using System.Threading;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_ANDROID

public class OVRExternalComposition : OVRComposition
{
	private GameObject previousMainCameraObject = null;
	public GameObject foregroundCameraGameObject = null;
	public Camera foregroundCamera = null;
	public GameObject backgroundCameraGameObject = null;
	public Camera backgroundCamera = null;
	public GameObject cameraProxyPlane = null;
#if OVR_ANDROID_MRC
	public AudioListener audioListener;
	public OVRMRAudioFilter audioFilter;
	public RenderTexture[] mrcRenderTextureArray = new RenderTexture[2];
	public int frameIndex;
	public int lastMrcEncodeFrameSyncId;
#endif

	public override OVRManager.CompositionMethod CompositionMethod() { return OVRManager.CompositionMethod.External; }

	public OVRExternalComposition(GameObject parentObject, Camera mainCamera)
		: base(parentObject, mainCamera)
	{
#if OVR_ANDROID_MRC
		int frameWidth;
		int frameHeight;
		OVRPlugin.Media.GetMrcFrameSize(out frameWidth, out frameHeight);
		Debug.LogFormat("[OVRExternalComposition] Create render texture {0}, {1}", frameWidth, frameHeight);
		for (int i=0; i<2; ++i)
		{
			mrcRenderTextureArray[i] = new RenderTexture(frameWidth, frameHeight, 24, RenderTextureFormat.ARGB32);
			mrcRenderTextureArray[i].Create();
		}

		frameIndex = 0;
		lastMrcEncodeFrameSyncId = -1;
#endif
		RefreshCameraObjects(parentObject, mainCamera);
	}

	private void RefreshCameraObjects(GameObject parentObject, Camera mainCamera)
	{
		if (mainCamera.gameObject != previousMainCameraObject)
		{
			Debug.LogFormat("[OVRExternalComposition] Camera refreshed. Rebind camera to {0}", mainCamera.gameObject.name);

			OVRCompositionUtil.SafeDestroy(ref backgroundCameraGameObject);
			backgroundCamera = null;
			OVRCompositionUtil.SafeDestroy(ref foregroundCameraGameObject);
			foregroundCamera = null;
			OVRCompositionUtil.SafeDestroy(ref cameraProxyPlane);

			RefreshCameraRig(parentObject, mainCamera);

			Debug.Assert(backgroundCameraGameObject == null);
			backgroundCameraGameObject = Object.Instantiate(mainCamera.gameObject);
			backgroundCameraGameObject.name = "OculusMRC_BackgroundCamera";
			backgroundCameraGameObject.transform.parent = cameraInTrackingSpace ? cameraRig.trackingSpace : parentObject.transform;
			if (backgroundCameraGameObject.GetComponent<AudioListener>())
			{
				Object.Destroy(backgroundCameraGameObject.GetComponent<AudioListener>());
			}
			if (backgroundCameraGameObject.GetComponent<OVRManager>())
			{
				Object.Destroy(backgroundCameraGameObject.GetComponent<OVRManager>());
			}
			backgroundCamera = backgroundCameraGameObject.GetComponent<Camera>();
			backgroundCamera.tag = "Untagged";
			backgroundCamera.stereoTargetEye = StereoTargetEyeMask.None;
			backgroundCamera.depth = 99990.0f;
			backgroundCamera.rect = new Rect(0.0f, 0.0f, 0.5f, 1.0f);
			backgroundCamera.cullingMask = mainCamera.cullingMask & (~OVRManager.instance.extraHiddenLayers);
#if OVR_ANDROID_MRC
			backgroundCamera.targetTexture = mrcRenderTextureArray[0];
#endif

			Debug.Assert(foregroundCameraGameObject == null);
			foregroundCameraGameObject = Object.Instantiate(mainCamera.gameObject);
			foregroundCameraGameObject.name = "OculusMRC_ForgroundCamera";
			foregroundCameraGameObject.transform.parent = cameraInTrackingSpace ? cameraRig.trackingSpace : parentObject.transform;
			if (foregroundCameraGameObject.GetComponent<AudioListener>())
			{
				Object.Destroy(foregroundCameraGameObject.GetComponent<AudioListener>());
			}
			if (foregroundCameraGameObject.GetComponent<OVRManager>())
			{
				Object.Destroy(foregroundCameraGameObject.GetComponent<OVRManager>());
			}
			foregroundCamera = foregroundCameraGameObject.GetComponent<Camera>();
			foregroundCamera.tag = "Untagged";
			foregroundCamera.stereoTargetEye = StereoTargetEyeMask.None;
			foregroundCamera.depth = backgroundCamera.depth + 1.0f;     // enforce the forground be rendered after the background
			foregroundCamera.rect = new Rect(0.5f, 0.0f, 0.5f, 1.0f);
			foregroundCamera.clearFlags = CameraClearFlags.Color;
#if OVR_ANDROID_MRC
			foregroundCamera.backgroundColor = OVRManager.instance.externalCompositionBackdropColorQuest;
#else
			foregroundCamera.backgroundColor = OVRManager.instance.externalCompositionBackdropColorRift;
#endif
			foregroundCamera.cullingMask = mainCamera.cullingMask & (~OVRManager.instance.extraHiddenLayers);
#if OVR_ANDROID_MRC
			foregroundCamera.targetTexture = mrcRenderTextureArray[0];
#endif

			// Create cameraProxyPlane for clipping
			Debug.Assert(cameraProxyPlane == null);
			cameraProxyPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
			cameraProxyPlane.name = "OculusMRC_ProxyClipPlane";
			cameraProxyPlane.transform.parent = cameraInTrackingSpace ? cameraRig.trackingSpace : parentObject.transform;
			cameraProxyPlane.GetComponent<Collider>().enabled = false;
			cameraProxyPlane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			Material clipMaterial = new Material(Shader.Find("Oculus/OVRMRClipPlane"));
			cameraProxyPlane.GetComponent<MeshRenderer>().material = clipMaterial;
#if OVR_ANDROID_MRC
			clipMaterial.SetColor("_Color", OVRManager.instance.externalCompositionBackdropColorQuest);
#else
			clipMaterial.SetColor("_Color", OVRManager.instance.externalCompositionBackdropColorRift);
#endif
			clipMaterial.SetFloat("_Visible", 0.0f);
			cameraProxyPlane.transform.localScale = new Vector3(1000, 1000, 1000);
			cameraProxyPlane.SetActive(true);
			OVRMRForegroundCameraManager foregroundCameraManager = foregroundCameraGameObject.AddComponent<OVRMRForegroundCameraManager>();
			foregroundCameraManager.composition = this;
			foregroundCameraManager.clipPlaneGameObj = cameraProxyPlane;

			previousMainCameraObject = mainCamera.gameObject;
		}
	}

#if OVR_ANDROID_MRC
	private void RefreshAudioFilter()
	{
		if (cameraRig != null && (audioListener == null || !audioListener.enabled || !audioListener.gameObject.activeInHierarchy))
		{
			CleanupAudioFilter();

			AudioListener tmpAudioListener = cameraRig.centerEyeAnchor.gameObject.activeInHierarchy ? cameraRig.centerEyeAnchor.GetComponent<AudioListener>() : null;
			if (tmpAudioListener != null && !tmpAudioListener.enabled) tmpAudioListener = null;
			if (tmpAudioListener == null)
			{
				if (Camera.main != null && Camera.main.gameObject.activeInHierarchy)
				{
					tmpAudioListener = Camera.main.GetComponent<AudioListener>();
					if (tmpAudioListener != null && !tmpAudioListener.enabled) tmpAudioListener = null;
				}
			}
			if (tmpAudioListener == null)
			{
				Object[] allListeners = Object.FindObjectsOfType<AudioListener>();
				foreach (var l in allListeners)
				{
					AudioListener al = l as AudioListener;
					if (al != null && al.enabled && al.gameObject.activeInHierarchy)
					{
						tmpAudioListener = al;
						break;
					}
				}
			}
			if (tmpAudioListener == null)
			{
				Debug.LogWarning("[OVRExternalComposition] No AudioListener in scene");
			}
			else
			{
				Debug.LogFormat("[OVRExternalComposition] AudioListener found, obj {0}", tmpAudioListener.gameObject.name);
			}
			audioListener = tmpAudioListener;

			audioFilter = audioListener.gameObject.AddComponent<OVRMRAudioFilter>();
			audioFilter.composition = this;
			Debug.LogFormat("OVRMRAudioFilter added");
		}
	}

	private float[] cachedAudioDataArray = null;

	private int CastMrcFrame(int castTextureIndex)
	{
		int audioFrames;
		int audioChannels;
		GetAndResetAudioData(ref cachedAudioDataArray, out audioFrames, out audioChannels);

		int syncId = -1;
		//Debug.Log("EncodeFrameThreadObject EncodeMrcFrame");
		bool ret = false;
		if (OVRPlugin.Media.GetMrcInputVideoBufferType() == OVRPlugin.Media.InputVideoBufferType.TextureHandle)
		{
			ret = OVRPlugin.Media.EncodeMrcFrame(mrcRenderTextureArray[castTextureIndex].GetNativeTexturePtr(), cachedAudioDataArray, audioFrames, audioChannels, AudioSettings.dspTime, ref syncId);
		}
		else
		{
			ret = OVRPlugin.Media.EncodeMrcFrame(mrcRenderTextureArray[castTextureIndex], cachedAudioDataArray, audioFrames, audioChannels, AudioSettings.dspTime, ref syncId);
		}

		if (!ret)
		{
			Debug.LogWarning("EncodeMrcFrame failed. Likely caused by OBS plugin disconnection");
			return -1;
		}

		return syncId;
	}

	private void SetCameraTargetTexture(int drawTextureIndex)
	{
		RenderTexture texture = mrcRenderTextureArray[drawTextureIndex];
		if (backgroundCamera.targetTexture != texture)
		{
			backgroundCamera.targetTexture = texture;
		}
		if (foregroundCamera.targetTexture != texture)
		{
			foregroundCamera.targetTexture = texture;
		}
	}
#endif


	public override void Update(GameObject gameObject, Camera mainCamera)
	{
		RefreshCameraObjects(gameObject, mainCamera);

		OVRPlugin.SetHandNodePoseStateLatency(0.0);     // the HandNodePoseStateLatency doesn't apply to the external composition. Always enforce it to 0.0

#if OVR_ANDROID_MRC
		RefreshAudioFilter();

		int drawTextureIndex = (frameIndex / 2) % 2;
		int castTextureIndex = 1 - drawTextureIndex;

		backgroundCamera.enabled = (frameIndex % 2) == 0;
		foregroundCamera.enabled = (frameIndex % 2) == 1;

		if (frameIndex % 2 == 0)
		{
			if (lastMrcEncodeFrameSyncId != -1)
			{
				OVRPlugin.Media.SyncMrcFrame(lastMrcEncodeFrameSyncId);
				lastMrcEncodeFrameSyncId = -1;
			}
			lastMrcEncodeFrameSyncId = CastMrcFrame(castTextureIndex);
			SetCameraTargetTexture(drawTextureIndex);
		}

		++ frameIndex;
#endif

		backgroundCamera.clearFlags = mainCamera.clearFlags;
		backgroundCamera.backgroundColor = mainCamera.backgroundColor;
		backgroundCamera.cullingMask = mainCamera.cullingMask & (~OVRManager.instance.extraHiddenLayers);
		backgroundCamera.nearClipPlane = mainCamera.nearClipPlane;
		backgroundCamera.farClipPlane = mainCamera.farClipPlane;

		foregroundCamera.cullingMask = mainCamera.cullingMask & (~OVRManager.instance.extraHiddenLayers);
		foregroundCamera.nearClipPlane = mainCamera.nearClipPlane;
		foregroundCamera.farClipPlane = mainCamera.farClipPlane;

		if (OVRMixedReality.useFakeExternalCamera || OVRPlugin.GetExternalCameraCount() == 0)
		{
			OVRPose worldSpacePose = new OVRPose();
			OVRPose trackingSpacePose = new OVRPose();
			trackingSpacePose.position = OVRManager.instance.trackingOriginType == OVRManager.TrackingOrigin.EyeLevel ?
				OVRMixedReality.fakeCameraEyeLevelPosition :
				OVRMixedReality.fakeCameraFloorLevelPosition;
			trackingSpacePose.orientation = OVRMixedReality.fakeCameraRotation;
			worldSpacePose = OVRExtensions.ToWorldSpacePose(trackingSpacePose);

			backgroundCamera.fieldOfView = OVRMixedReality.fakeCameraFov;
			backgroundCamera.aspect = OVRMixedReality.fakeCameraAspect;
			foregroundCamera.fieldOfView = OVRMixedReality.fakeCameraFov;
			foregroundCamera.aspect = OVRMixedReality.fakeCameraAspect;

			if (cameraInTrackingSpace)
			{
				backgroundCamera.transform.FromOVRPose(trackingSpacePose, true);
				foregroundCamera.transform.FromOVRPose(trackingSpacePose, true);
			}
			else
			{
				backgroundCamera.transform.FromOVRPose(worldSpacePose);
				foregroundCamera.transform.FromOVRPose(worldSpacePose);
			}
		}
		else
		{
			OVRPlugin.CameraExtrinsics extrinsics;
			OVRPlugin.CameraIntrinsics intrinsics;
			OVRPlugin.Posef calibrationRawPose;

			// So far, only support 1 camera for MR and always use camera index 0
			if (OVRPlugin.GetMixedRealityCameraInfo(0, out extrinsics, out intrinsics, out calibrationRawPose))
			{
				float fovY = Mathf.Atan(intrinsics.FOVPort.UpTan) * Mathf.Rad2Deg * 2;
				float aspect = intrinsics.FOVPort.LeftTan / intrinsics.FOVPort.UpTan;
				backgroundCamera.fieldOfView = fovY;
				backgroundCamera.aspect = aspect;
				foregroundCamera.fieldOfView = fovY;
				foregroundCamera.aspect = intrinsics.FOVPort.LeftTan / intrinsics.FOVPort.UpTan;

				if (cameraInTrackingSpace)
				{
					OVRPose trackingSpacePose = ComputeCameraTrackingSpacePose(extrinsics, calibrationRawPose);
					backgroundCamera.transform.FromOVRPose(trackingSpacePose, true);
					foregroundCamera.transform.FromOVRPose(trackingSpacePose, true);
				}
				else
				{
					OVRPose worldSpacePose = ComputeCameraWorldSpacePose(extrinsics, calibrationRawPose);
					backgroundCamera.transform.FromOVRPose(worldSpacePose);
					foregroundCamera.transform.FromOVRPose(worldSpacePose);
				}
			}
			else
			{
				Debug.LogError("Failed to get external camera information");
				return;
			}
		}

		// Assume player always standing straightly
		Vector3 externalCameraToHeadXZ = mainCamera.transform.position - foregroundCamera.transform.position;
		externalCameraToHeadXZ.y = 0;
		cameraProxyPlane.transform.position = mainCamera.transform.position;
		cameraProxyPlane.transform.LookAt(cameraProxyPlane.transform.position + externalCameraToHeadXZ);
	}

#if OVR_ANDROID_MRC
	private void CleanupAudioFilter()
	{
		if (audioFilter)
		{
			audioFilter.composition = null;
			Object.Destroy(audioFilter);
			Debug.LogFormat("OVRMRAudioFilter destroyed");
			audioFilter = null;
		}

	}
#endif

	public override void Cleanup()
	{
		OVRCompositionUtil.SafeDestroy(ref backgroundCameraGameObject);
		backgroundCamera = null;
		OVRCompositionUtil.SafeDestroy(ref foregroundCameraGameObject);
		foregroundCamera = null;
		OVRCompositionUtil.SafeDestroy(ref cameraProxyPlane);
		Debug.Log("ExternalComposition deactivated");

#if OVR_ANDROID_MRC
		if (lastMrcEncodeFrameSyncId != -1)
		{
			OVRPlugin.Media.SyncMrcFrame(lastMrcEncodeFrameSyncId);
			lastMrcEncodeFrameSyncId = -1;
		}

		CleanupAudioFilter();

		for (int i=0; i<2; ++i)
		{
			mrcRenderTextureArray[i].Release();
			mrcRenderTextureArray[i] = null;
		}

		frameIndex = 0;
#endif
	}

	private readonly object audioDataLock = new object();
	private List<float> cachedAudioData = new List<float>(16384);
	private int cachedChannels = 0;

	public void CacheAudioData(float[] data, int channels)
	{
		lock(audioDataLock)
		{
			if (channels != cachedChannels)
			{
				cachedAudioData.Clear();
			}
			cachedChannels = channels;
			cachedAudioData.AddRange(data);
			//Debug.LogFormat("[CacheAudioData] dspTime {0} indata {1} channels {2} accu_len {3}", AudioSettings.dspTime, data.Length, channels, cachedAudioData.Count);
		}
	}

	public void GetAndResetAudioData(ref float[] audioData, out int audioFrames, out int channels)
	{
		lock(audioDataLock)
		{
			//Debug.LogFormat("[GetAndResetAudioData] dspTime {0} accu_len {1}", AudioSettings.dspTime, cachedAudioData.Count);
			if (audioData == null || audioData.Length < cachedAudioData.Count)
			{
				audioData = new float[cachedAudioData.Capacity];
			}
			cachedAudioData.CopyTo(audioData);
			audioFrames = cachedAudioData.Count;
			channels = cachedChannels;
			cachedAudioData.Clear();
		}
	}

}

/// <summary>
/// Helper internal class for foregroundCamera, don't call it outside
/// </summary>
internal class OVRMRForegroundCameraManager : MonoBehaviour
{
	public OVRExternalComposition composition;
	public GameObject clipPlaneGameObj;
	private Material clipPlaneMaterial;
	void OnPreRender()
	{
		// the clipPlaneGameObj should be only visible to foreground camera
		if (clipPlaneGameObj)
		{
			if (clipPlaneMaterial == null)
				clipPlaneMaterial = clipPlaneGameObj.GetComponent<MeshRenderer>().material;
			clipPlaneGameObj.GetComponent<MeshRenderer>().material.SetFloat("_Visible", 1.0f);
		}
	}
	void OnPostRender()
	{
		if (clipPlaneGameObj)
		{
			Debug.Assert(clipPlaneMaterial);
			clipPlaneGameObj.GetComponent<MeshRenderer>().material.SetFloat("_Visible", 0.0f);
		}
	}
}

#if OVR_ANDROID_MRC

public class OVRMRAudioFilter : MonoBehaviour
{
	private bool running = false;

	public OVRExternalComposition composition;

	void Start()
	{
		running = true;
	}

	void OnAudioFilterRead(float[] data, int channels)
	{
		if (!running)
			return;

		if (composition != null)
		{
			composition.CacheAudioData(data, channels);
		}
	}
}
#endif

#endif
