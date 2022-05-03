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

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Assets.OVR.Scripts;

/// <summary>
///Scans the project and warns about the following conditions:
///Audio sources > 16
///Using MSAA levels other than recommended level
///Excessive pixel lights (>1 on Gear VR; >3 on Rift)
///Directional Lightmapping Modes (on Gear; use Non-Directional)
///Preload audio setting on individual audio clips
///Decompressing audio clips on load
///Disabling occlusion mesh
///Android target API level set to 21 or higher
///Unity skybox use (on by default, but if you can't see the skybox switching to Color is much faster on Gear)
///Lights marked as "baked" but that were not included in the last bake (and are therefore realtime).
///Lack of static batching and dynamic batching settings activated.
///Full screen image effects (Gear)
///Warn about large textures that are marked as uncompressed.
///32-bit depth buffer (use 16)
///Use of projectors (Gear; can be used carefully but slow enough to warrant a warning)
///Maybe in the future once quantified: Graphics jobs and IL2CPP on Gear.
///Real-time global illumination
///No texture compression, or non-ASTC texture compression as a global setting (Gear).
///Using deferred rendering
///Excessive texture resolution after LOD bias (>2k on Gear VR; >4k on Rift)
///Not using trilinear or aniso filtering and not generating mipmaps
///Excessive render scale (>1.2)
///Slow physics settings: Sleep Threshold < 0.005, Default Contact Offset < 0.01, Solver Iteration Count > 6
///Shadows on when approaching the geometry or draw call limits
///Non-static objects with colliders that are missing rigidbodies on themselves or in the parent chain.
///No initialization of GPU/CPU throttling settings, or init to dangerous values (-1 or > 3)  (Gear)
///Using inefficient effects: SSAO, motion blur, global fog, parallax mapping, etc.
///Too many Overlay layers
///Use of Standard shader or Standard Specular shader on Gear.  More generally, excessive use of multipass shaders (legacy specular, etc).
///Multiple cameras with clears (on Gear, potential for excessive fill cost)
///Excessive shader passes (>2)
///Material pointers that have been instanced in the editor (esp. if we could determine that the instance has no deltas from the original)
///Excessive draw calls (>150 on Gear VR; >2000 on Rift)
///Excessive tris or verts (>100k on Gear VR; >1M on Rift)
///Large textures, lots of prefabs in startup scene (for bootstrap optimization)
///GPU skinning: testing Android-only, as most Rift devs are GPU-bound.
/// </summary>
[InitializeOnLoadAttribute]
public class OVRLint : EditorWindow
{
	//TODO: The following require reflection or static analysis.
	///Use of ONSP reflections (Gear)
	///Use of LoadLevelAsync / LoadLevelAdditiveAsync (on Gear, this kills frame rate so dramatically it's probably better to just go to black and load synchronously)
	///Use of Linq in non-editor assemblies (common cause of GCs).  Minor: use of foreach.
	///Use of Unity WWW (exceptionally high overhead for large file downloads, but acceptable for tiny gets).
	///Declared but empty Awake/Start/Update/OnCollisionEnter/OnCollisionExit/OnCollisionStay.  Also OnCollision* star methods that declare the Collision  argument but do not reference it (omitting it short-circuits the collision contact calculation).

	private static List<FixRecord> mRecords = new List<FixRecord>();
	private static List<FixRecord> mRuntimeEditModeRequiredRecords = new List<FixRecord>();
#if !UNITY_2017_2_OR_NEWER
	private static bool mWasPlaying = false;
#endif
	private Vector2 mScrollPosition;

	[MenuItem("Oculus/Tools/OVR Performance Lint Tool")]
	static void Init()
	{
		// Get existing open window or if none, make a new one:
		EditorWindow.GetWindow(typeof(OVRLint));
		OVRPlugin.SendEvent("perf_lint", "activated");
		OVRLint.RunCheck();
#if !UNITY_2017_2_OR_NEWER
		mWasPlaying = EditorApplication.isPlaying;
#endif
	}

	OVRLint()
	{
#if UNITY_2017_2_OR_NEWER
		EditorApplication.playModeStateChanged += HandlePlayModeState;
#else
		EditorApplication.playmodeStateChanged += () =>
		{
			// When Unity starts playing, it would also trigger play mode changed event with isPlaying == false
			// Fixes should only be applied when it was transitioned from playing mode
			if (!EditorApplication.isPlaying && mWasPlaying)
			{
				ApplyEditModeRequiredFix();
				mWasPlaying = false;
			}
			else
			{
				mWasPlaying = true;
			}
		};
#endif
	}

#if UNITY_2017_2_OR_NEWER
	private static void HandlePlayModeState(PlayModeStateChange state)
	{
		if (state == PlayModeStateChange.EnteredEditMode)
		{
			ApplyEditModeRequiredFix();
		}
	}
#endif

	private static void ApplyEditModeRequiredFix()
	{
		// Apply runtime fixes that require edit mode when applying fix
		foreach (FixRecord record in mRuntimeEditModeRequiredRecords)
		{
			record.fixMethod(null, false, 0);
			OVRPlugin.SendEvent("perf_lint_apply_fix", record.category);
			record.complete = true;
		}
		mRuntimeEditModeRequiredRecords.Clear();
	}

	void OnGUI()
	{
		GUILayout.Label("OVR Performance Lint Tool", EditorStyles.boldLabel);
		if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
		{
			RunCheck();
		}

		string lastCategory = "";

		mScrollPosition = EditorGUILayout.BeginScrollView(mScrollPosition);

		for (int x = 0; x < mRecords.Count; x++)
		{
			FixRecord record = mRecords[x];

			if (!record.category.Equals(lastCategory))  // new category
			{
				lastCategory = record.category;
				EditorGUILayout.Separator();
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(lastCategory, EditorStyles.label, GUILayout.Width(200));
				bool moreThanOne = (x + 1 < mRecords.Count && mRecords[x + 1].category.Equals(lastCategory));
				if (record.buttonNames != null && record.buttonNames.Length > 0)
				{
					if (moreThanOne)
					{
						GUILayout.Label("Apply to all:", EditorStyles.label, GUILayout.Width(75));
						for (int y = 0; y < record.buttonNames.Length; y++)
						{
							if (GUILayout.Button(record.buttonNames[y], EditorStyles.toolbarButton, GUILayout.Width(200)))
							{
								List<FixRecord> recordsToProcess = new List<FixRecord>();

								for (int z = x; z < mRecords.Count; z++)
								{
									FixRecord thisRecord = mRecords[z];
									bool isLast = false;
									if (z + 1 >= mRecords.Count || !mRecords[z + 1].category.Equals(lastCategory))
									{
										isLast = true;
									}

									if (!thisRecord.complete)
									{
										recordsToProcess.Add(thisRecord);
									}

									if (isLast)
									{
										break;
									}
								}

								UnityEngine.Object[] undoObjects = new UnityEngine.Object[recordsToProcess.Count];
								for (int z = 0; z < recordsToProcess.Count; z++)
								{
									undoObjects[z] = recordsToProcess[z].targetObject;
								}
								Undo.RecordObjects(undoObjects, record.category + " (Multiple)");
								for (int z = 0; z < recordsToProcess.Count; z++)
								{
									FixRecord thisRecord = recordsToProcess[z];
									thisRecord.fixMethod(thisRecord.targetObject, (z + 1 == recordsToProcess.Count), y);
									OVRPlugin.SendEvent("perf_lint_apply_fix", thisRecord.category);
									thisRecord.complete = true;
								}
							}
						}
					}
				}
				EditorGUILayout.EndHorizontal();
				if (moreThanOne || record.targetObject)
				{
					GUILayout.Label(record.message);
				}
			}

			EditorGUILayout.BeginHorizontal();
			GUI.enabled = !record.complete;
			if (record.targetObject)
			{
				EditorGUILayout.ObjectField(record.targetObject, record.targetObject.GetType(), true);
			}
			else
			{
				GUILayout.Label(record.message);
			}
			if (record.buttonNames != null)
			{
				for (int y = 0; y < record.buttonNames.Length; y++)
				{
					if (GUILayout.Button(record.buttonNames[y], EditorStyles.toolbarButton, GUILayout.Width(200)))
					{
						if (record.targetObject != null)
						{
							Undo.RecordObject(record.targetObject, record.category);
						}

						if (record.editModeRequired)
						{
							// Add to the fix record list that requires edit mode
							mRuntimeEditModeRequiredRecords.Add(record);
						}
						else
						{
							// Apply the fix directly
							record.fixMethod(record.targetObject, true, y);
							OVRPlugin.SendEvent("perf_lint_apply_fix", record.category);
							record.complete = true;
						}

						if (mRuntimeEditModeRequiredRecords.Count != 0)
						{
							// Stop the scene to apply edit mode required records
							EditorApplication.ExecuteMenuItem("Edit/Play");
						}
					}
				}

			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.EndScrollView();
	}


	public static int RunCheck()
	{
		mRecords.Clear();
		mRuntimeEditModeRequiredRecords.Clear();

		CheckStaticCommonIssues();
#if UNITY_ANDROID
		CheckStaticAndroidIssues();
#endif

		if (EditorApplication.isPlaying)
		{
			CheckRuntimeCommonIssues();
#if UNITY_ANDROID
			CheckRuntimeAndroidIssues();
#endif
		}

		mRecords.Sort(delegate (FixRecord record1, FixRecord record2)
		{
			return record1.category.CompareTo(record2.category);
		});
		return mRecords.Count;
	}

	static void AddFix(string category, string message, FixMethodDelegate method, UnityEngine.Object target, bool editModeRequired, params string[] buttons)
	{
		OVRPlugin.SendEvent("perf_lint_add_fix", category);
		mRecords.Add(new FixRecord(category, message, method, target, editModeRequired, buttons));
	}

	static void CheckStaticCommonIssues()
	{
		if (OVRManager.IsUnityAlphaOrBetaVersion())
		{
			AddFix("General", OVRManager.UnityAlphaOrBetaVersionWarningMessage, null, null, false);
		}

		if (QualitySettings.anisotropicFiltering != AnisotropicFiltering.Enable && QualitySettings.anisotropicFiltering != AnisotropicFiltering.ForceEnable)
		{
			AddFix("Optimize Aniso", "Anisotropic filtering is recommended for optimal image sharpness and GPU performance.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				// Ideally this would be multi-option: offer Enable or ForceEnable.
				QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
			}, null, false, "Fix");
		}

#if UNITY_ANDROID
		int recommendedPixelLightCount = 1;
#else
		int recommendedPixelLightCount = 3;
#endif

		if (QualitySettings.pixelLightCount > recommendedPixelLightCount)
		{
			AddFix("Optimize Pixel Light Count", "For GPU performance set no more than " + recommendedPixelLightCount + " pixel lights in Quality Settings (currently " + QualitySettings.pixelLightCount + ").", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				QualitySettings.pixelLightCount = recommendedPixelLightCount;
			}, null, false, "Fix");
		}

#if false
		// Should we recommend this?  Seems to be mutually exclusive w/ dynamic batching.
		if (!PlayerSettings.graphicsJobs)
		{
			AddFix ("Optimize Graphics Jobs", "For CPU performance, please use graphics jobs.", delegate(UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.graphicsJobs = true;
			}, null, false, "Fix");
		}
#endif

#if UNITY_2017_2_OR_NEWER
		if ((!PlayerSettings.MTRendering || !PlayerSettings.GetMobileMTRendering(BuildTargetGroup.Android)))
#else
		if ((!PlayerSettings.MTRendering || !PlayerSettings.mobileMTRendering))
#endif
		{
			AddFix("Optimize MT Rendering", "For CPU performance, please enable multithreaded rendering.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
#if UNITY_2017_2_OR_NEWER
				PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Standalone, true);
				PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
#else
				PlayerSettings.MTRendering = PlayerSettings.mobileMTRendering = true;
#endif
			}, null, false, "Fix");
		}

#if UNITY_ANDROID
		if (!PlayerSettings.use32BitDisplayBuffer)
		{
			AddFix("Optimize Display Buffer Format", "We recommend to enable use32BitDisplayBuffer.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.use32BitDisplayBuffer = true;
			}, null, false, "Fix");
		}
#endif

#if UNITY_2017_3_OR_NEWER && !UNITY_ANDROID
		if (!PlayerSettings.VROculus.dashSupport)
		{
			AddFix("Enable Dash Integration", "We recommend to enable Dash Integration for better user experience.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.VROculus.dashSupport = true;
			}, null, false, "Fix");
		}

		if (!PlayerSettings.VROculus.sharedDepthBuffer)
		{
			AddFix("Enable Depth Buffer Sharing", "We recommend to enable Depth Buffer Sharing for better user experience on Oculus Dash.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.VROculus.sharedDepthBuffer = true;
			}, null, false, "Fix");
		}
#endif

		BuildTargetGroup target = EditorUserBuildSettings.selectedBuildTargetGroup;
		var tier = UnityEngine.Rendering.GraphicsTier.Tier1;
		var tierSettings = UnityEditor.Rendering.EditorGraphicsSettings.GetTierSettings(target, tier);

		if ((tierSettings.renderingPath == RenderingPath.DeferredShading ||
			tierSettings.renderingPath == RenderingPath.DeferredLighting))
		{
			AddFix("Optimize Rendering Path", "For CPU performance, please do not use deferred shading.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				tierSettings.renderingPath = RenderingPath.Forward;
				UnityEditor.Rendering.EditorGraphicsSettings.SetTierSettings(target, tier, tierSettings);
			}, null, false, "Use Forward");
		}

		if (PlayerSettings.stereoRenderingPath == StereoRenderingPath.MultiPass)
		{
			AddFix("Optimize Stereo Rendering", "For CPU performance, please enable single-pass or instanced stereo rendering.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
			}, null, false, "Fix");
		}

		if (LightmapSettings.lightmaps.Length > 0 && LightmapSettings.lightmapsMode != LightmapsMode.NonDirectional)
		{
			AddFix("Optimize Lightmap Directionality", "Switching from directional lightmaps to non-directional lightmaps can save a small amount of GPU time.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
			}, null, false, "Switch to non-directional lightmaps");
		}

		if (Lightmapping.realtimeGI)
		{
			AddFix("Disable Realtime GI", "Disabling real-time global illumination can improve GPU performance.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				Lightmapping.realtimeGI = false;
			}, null, false, "Set Lightmapping.realtimeGI = false.");
		}

		var lights = GameObject.FindObjectsOfType<Light>();
		for (int i = 0; i < lights.Length; ++i)
		{
#if UNITY_2017_3_OR_NEWER
			if (lights [i].type != LightType.Directional && !lights [i].bakingOutput.isBaked && IsLightBaked(lights[i]))
#else
			if (lights[i].type != LightType.Directional && !lights[i].isBaked && IsLightBaked(lights[i]))
#endif
			{
				AddFix("Unbaked Lights", "The following lights in the scene are marked as Baked, but they don't have up to date lightmap data. Generate the lightmap data, or set it to auto-generate, in Window->Lighting->Settings.", null, lights[i], false, null);
			}

			if (lights[i].shadows != LightShadows.None && !IsLightBaked(lights[i]))
			{
				AddFix("Optimize Shadows", "For CPU performance, consider disabling shadows on realtime lights.", delegate (UnityEngine.Object obj, bool last, int selected)
				{
					Light thisLight = (Light)obj;
					thisLight.shadows = LightShadows.None;
				}, lights[i], false, "Set \"Shadow Type\" to \"No Shadows\"");
			}
		}

		var sources = GameObject.FindObjectsOfType<AudioSource>();
		if (sources.Length > 16)
		{
			List<AudioSource> playingAudioSources = new List<AudioSource>();
			foreach (var audioSource in sources)
			{
				if (audioSource.isPlaying)
				{
					playingAudioSources.Add(audioSource);
				}
			}

			if (playingAudioSources.Count > 16)
			{
				// Sort playing audio sources by priority
				playingAudioSources.Sort(delegate (AudioSource x, AudioSource y)
				{
					return x.priority.CompareTo(y.priority);
				});
				for (int i = 16; i < playingAudioSources.Count; ++i)
				{
					AddFix("Optimize Audio Source Count", "For CPU performance, please disable all but the top 16 AudioSources.", delegate (UnityEngine.Object obj, bool last, int selected)
					{
						AudioSource audioSource = (AudioSource)obj;
						audioSource.enabled = false;
					}, playingAudioSources[i], false, "Disable");
				}
			}
		}

		var clips = GameObject.FindObjectsOfType<AudioClip>();
		for (int i = 0; i < clips.Length; ++i)
		{
			if (clips[i].loadType == AudioClipLoadType.DecompressOnLoad)
			{
				AddFix("Audio Loading", "For fast loading, please don't use decompress on load for audio clips", delegate (UnityEngine.Object obj, bool last, int selected)
				{
					AudioClip thisClip = (AudioClip)obj;
					if (selected == 0)
					{
						SetAudioLoadType(thisClip, AudioClipLoadType.CompressedInMemory, last);
					}
					else
					{
						SetAudioLoadType(thisClip, AudioClipLoadType.Streaming, last);
					}

				}, clips[i], false, "Change to Compressed in Memory", "Change to Streaming");
			}

			if (clips[i].preloadAudioData)
			{
				AddFix("Audio Preload", "For fast loading, please don't preload data for audio clips.", delegate (UnityEngine.Object obj, bool last, int selected)
				{
					SetAudioPreload(clips[i], false, last);
				}, clips[i], false, "Fix");
			}
		}

		if (Physics.defaultContactOffset < 0.01f)
		{
			AddFix("Optimize Contact Offset", "For CPU performance, please don't use default contact offset below 0.01.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				Physics.defaultContactOffset = 0.01f;
			}, null, false, "Fix");
		}

		if (Physics.sleepThreshold < 0.005f)
		{
			AddFix("Optimize Sleep Threshold", "For CPU performance, please don't use sleep threshold below 0.005.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				Physics.sleepThreshold = 0.005f;
			}, null, false, "Fix");
		}

		if (Physics.defaultSolverIterations > 8)
		{
			AddFix("Optimize Solver Iterations", "For CPU performance, please don't use excessive solver iteration counts.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				Physics.defaultSolverIterations = 8;
			}, null, false, "Fix");
		}

		var materials = Resources.FindObjectsOfTypeAll<Material>();
		for (int i = 0; i < materials.Length; ++i)
		{
			if (materials[i].shader.name.Contains("Parallax") || materials[i].IsKeywordEnabled("_PARALLAXMAP"))
			{
				AddFix("Optimize Shading", "For GPU performance, please don't use parallax-mapped materials.", delegate (UnityEngine.Object obj, bool last, int selected)
				{
					Material thisMaterial = (Material)obj;
					if (thisMaterial.IsKeywordEnabled("_PARALLAXMAP"))
					{
						thisMaterial.DisableKeyword("_PARALLAXMAP");
					}

					if (thisMaterial.shader.name.Contains("Parallax"))
					{
						var newName = thisMaterial.shader.name.Replace("-ParallaxSpec", "-BumpSpec");
						newName = newName.Replace("-Parallax", "-Bump");
						var newShader = Shader.Find(newName);
						if (newShader)
						{
							thisMaterial.shader = newShader;
						}
						else
						{
							Debug.LogWarning("Unable to find a replacement for shader " + materials[i].shader.name);
						}
					}
				}, materials[i], false, "Fix");
			}
		}

		var renderers = GameObject.FindObjectsOfType<Renderer>();
		for (int i = 0; i < renderers.Length; ++i)
		{
			if (renderers[i].sharedMaterial == null)
			{
				AddFix("Instanced Materials", "Please avoid instanced materials on renderers.", null, renderers[i], false);
			}
		}

		var overlays = GameObject.FindObjectsOfType<OVROverlay>();
		if (overlays.Length > 4)
		{
			AddFix("Optimize VR Layer Count", "For GPU performance, please use 4 or fewer VR layers.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				for (int i = 4; i < OVROverlay.instances.Length; ++i)
				{
					OVROverlay.instances[i].enabled = false;
				}
			}, null, false, "Fix");
		}

		var splashScreen = PlayerSettings.virtualRealitySplashScreen;
		if (splashScreen != null)
		{
			if (splashScreen.filterMode != FilterMode.Trilinear)
			{
				AddFix("Optimize VR Splash Filtering", "For visual quality, please use trilinear filtering on your VR splash screen.", delegate (UnityEngine.Object obj, bool last, int EditorSelectedRenderState)
				{
					var assetPath = AssetDatabase.GetAssetPath(splashScreen);
					var importer = (TextureImporter)TextureImporter.GetAtPath(assetPath);
					importer.filterMode = FilterMode.Trilinear;
					AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
				}, null, false, "Fix");
			}

			if (splashScreen.mipmapCount <= 1)
			{
				AddFix("Generate VR Splash Mipmaps", "For visual quality, please use mipmaps with your VR splash screen.", delegate (UnityEngine.Object obj, bool last, int EditorSelectedRenderState)
				{
					var assetPath = AssetDatabase.GetAssetPath(splashScreen);
					var importer = (TextureImporter)TextureImporter.GetAtPath(assetPath);
					importer.mipmapEnabled = true;
					AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
				}, null, false, "Fix");
			}
		}
	}

	static void CheckRuntimeCommonIssues()
	{
		if (!OVRPlugin.occlusionMesh)
		{
			AddFix("Occlusion Mesh", "Enabling the occlusion mesh saves substantial GPU resources, generally with no visual impact. Enable unless you have an exceptional use case.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				OVRPlugin.occlusionMesh = true;
			}, null, false, "Set OVRPlugin.occlusionMesh = true");
		}

		if (OVRManager.instance != null && !OVRManager.instance.useRecommendedMSAALevel)
		{
			AddFix("Optimize MSAA", "OVRManager can select the optimal antialiasing for the installed hardware at runtime. Recommend enabling this.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				var ovrManagers = GameObject.FindObjectsOfType<OVRManager>();
				foreach (var ovrManager in ovrManagers)
				{
					ovrManager.useRecommendedMSAALevel = true;
				}
			}, null, true, "Stop Play and Fix");
		}

#if UNITY_2017_2_OR_NEWER
		if (UnityEngine.XR.XRSettings.eyeTextureResolutionScale > 1.5)
#else
		if (UnityEngine.VR.VRSettings.renderScale > 1.5)
#endif
		{
			AddFix("Optimize Render Scale", "Render scale above 1.5 is extremely expensive on the GPU, with little if any positive visual benefit.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
#if UNITY_2017_2_OR_NEWER
				UnityEngine.XR.XRSettings.eyeTextureResolutionScale = 1.5f;
#else
				UnityEngine.VR.VRSettings.renderScale = 1.5f;
#endif
			}, null, false, "Fix");
		}
	}

	static void CheckStaticAndroidIssues()
	{
		// Check that the minSDKVersion meets requirement, 21 for Gear and Go, 23 for Quest
		AndroidSdkVersions recommendedAndroidMinSdkVersion = AndroidSdkVersions.AndroidApiLevel21;
		if (OVRDeviceSelector.isTargetDeviceQuest)
		{
			recommendedAndroidMinSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
		}
		if ((int)PlayerSettings.Android.minSdkVersion < (int)recommendedAndroidMinSdkVersion)
		{
			AddFix("Set Min Android API Level", "Please require at least API level " + (int)recommendedAndroidMinSdkVersion, delegate (UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.Android.minSdkVersion = recommendedAndroidMinSdkVersion;
			}, null, false, "Fix");
		}

		// Check that compileSDKVersion meets minimal version 26 as required for Quest's headtracking feature
		// Unity Sets compileSDKVersion in Gradle as the value used in targetSdkVersion
		AndroidSdkVersions requiredAndroidTargetSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
		if (OVRDeviceSelector.isTargetDeviceQuest &&
			(int)PlayerSettings.Android.targetSdkVersion < (int)requiredAndroidTargetSdkVersion)
		{
			AddFix("Set Android Target SDK Level", "Oculus Quest apps require at least target API level " +
				(int)requiredAndroidTargetSdkVersion, delegate (UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.Android.targetSdkVersion = requiredAndroidTargetSdkVersion;
			}, null, false, "Fix");
		}

		if (!PlayerSettings.gpuSkinning)
		{
			AddFix("Optimize GPU Skinning", "If you are CPU-bound, consider using GPU skinning.", 
				delegate (UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.gpuSkinning = true;
			}, null, false, "Fix");
		}


		if (RenderSettings.skybox)
		{
			AddFix("Optimize Clearing", "For GPU performance, please don't use Unity's built-in Skybox.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				RenderSettings.skybox = null;
			}, null, false, "Clear Skybox");
		}

		var materials = Resources.FindObjectsOfTypeAll<Material>();
		for (int i = 0; i < materials.Length; ++i)
		{
			if (materials[i].IsKeywordEnabled("_SPECGLOSSMAP") || materials[i].IsKeywordEnabled("_METALLICGLOSSMAP"))
			{
				AddFix("Optimize Specular Material", "For GPU performance, please don't use specular shader on materials.", delegate (UnityEngine.Object obj, bool last, int selected)
				{
					Material thisMaterial = (Material)obj;
					thisMaterial.DisableKeyword("_SPECGLOSSMAP");
					thisMaterial.DisableKeyword("_METALLICGLOSSMAP");
				}, materials[i], false, "Fix");
			}

			if (materials[i].passCount > 1)
			{
				AddFix("Material Passes", "Please use 2 or fewer passes in materials.", null, materials[i], false);
			}
		}

		ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(UnityEditor.BuildTargetGroup.Android);
		if (backend != UnityEditor.ScriptingImplementation.IL2CPP)
		{
			AddFix("Optimize Scripting Backend", "For CPU performance, please use IL2CPP.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				PlayerSettings.SetScriptingBackend(UnityEditor.BuildTargetGroup.Android, UnityEditor.ScriptingImplementation.IL2CPP);
			}, null, false, "Fix");
		}

		var monoBehaviours = GameObject.FindObjectsOfType<MonoBehaviour>();
		System.Type effectBaseType = System.Type.GetType("UnityStandardAssets.ImageEffects.PostEffectsBase");
		if (effectBaseType != null)
		{
			for (int i = 0; i < monoBehaviours.Length; ++i)
			{
				if (monoBehaviours[i].GetType().IsSubclassOf(effectBaseType))
				{
					AddFix("Image Effects", "Please don't use image effects.", null, monoBehaviours[i], false);
				}
			}
		}

		var textures = Resources.FindObjectsOfTypeAll<Texture2D>();

		int maxTextureSize = 1024 * (1 << QualitySettings.masterTextureLimit);
		maxTextureSize = maxTextureSize * maxTextureSize;

		for (int i = 0; i < textures.Length; ++i)
		{
			if (textures[i].filterMode == FilterMode.Trilinear && textures[i].mipmapCount == 1)
			{
				AddFix("Optimize Texture Filtering", "For GPU performance, please generate mipmaps or disable trilinear filtering for textures.", delegate (UnityEngine.Object obj, bool last, int selected)
				{
					Texture2D thisTexture = (Texture2D)obj;
					if (selected == 0)
					{
						thisTexture.filterMode = FilterMode.Bilinear;
					}
					else
					{
						SetTextureUseMips(thisTexture, true, last);
					}
				}, textures[i], false, "Switch to Bilinear", "Generate Mipmaps");
			}
		}

		var projectors = GameObject.FindObjectsOfType<Projector>();
		if (projectors.Length > 0)
		{
			AddFix("Optimize Projectors", "For GPU performance, please don't use projectors.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				Projector[] thisProjectors = GameObject.FindObjectsOfType<Projector>();
				for (int i = 0; i < thisProjectors.Length; ++i)
				{
					thisProjectors[i].enabled = false;
				}
			}, null, false, "Disable Projectors");
		}

		if (EditorUserBuildSettings.androidBuildSubtarget != MobileTextureSubtarget.ASTC)
		{
			AddFix("Optimize Texture Compression", "For GPU performance, please use ASTC.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
			}, null, false, "Fix");
		}

		var cameras = GameObject.FindObjectsOfType<Camera>();
		int clearCount = 0;
		for (int i = 0; i < cameras.Length; ++i)
		{
			if (cameras[i].clearFlags != CameraClearFlags.Nothing && cameras[i].clearFlags != CameraClearFlags.Depth)
				++clearCount;
		}

		if (clearCount > 2)
		{
			AddFix("Camera Clears", "Please use 2 or fewer clears.", null, null, false);
		}

		for (int i = 0; i < cameras.Length; ++i)
		{
			if (cameras[i].forceIntoRenderTexture)
			{
				AddFix("Optimize Mobile Rendering", "For GPU performance, please don't enable forceIntoRenderTexture on your camera, this might be a flag pollution created by post process stack you used before, \nif your post process had already been turned off, we strongly encourage you to disable forceIntoRenderTexture. If you still want to use post process for some reasons, \nyou can leave this one on, but be warned, enabling this flag will introduce huge GPU performance cost. To view your flag status, please turn on you inspector's debug mode",
				delegate (UnityEngine.Object obj, bool last, int selected)
				{
					Camera thisCamera = (Camera)obj;
					thisCamera.forceIntoRenderTexture = false;
				}, cameras[i], false, "Disable forceIntoRenderTexture");
			}
		}
	}

	static void CheckRuntimeAndroidIssues()
	{
		if (UnityStats.usedTextureMemorySize + UnityStats.vboTotalBytes > 1000000)
		{
			AddFix("Graphics Memory", "Please use less than 1GB of vertex and texture memory.", null, null, false);
		}

		if (OVRManager.cpuLevel < 0 || OVRManager.cpuLevel > 3)
		{
			AddFix("Optimize CPU level", "For battery life, please use a safe CPU level.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				OVRManager.cpuLevel = 2;
			}, null, false, "Set to CPU2");
		}

		if (OVRManager.gpuLevel < 0 || OVRManager.gpuLevel > 3)
		{
			AddFix("Optimize GPU level", "For battery life, please use a safe GPU level.", delegate (UnityEngine.Object obj, bool last, int selected)
			{
				OVRManager.gpuLevel = 2;
			}, null, false, "Set to GPU2");
		}

		if (UnityStats.triangles > 100000 || UnityStats.vertices > 100000)
		{
			AddFix("Triangles and Verts", "Please use less than 100000 triangles or vertices.", null, null, false);
		}

		// Warn for 50 if in non-VR mode?
		if (UnityStats.drawCalls > 100)
		{
			AddFix("Draw Calls", "Please use less than 100 draw calls.", null, null, false);
		}
	}


	enum LightmapType { Realtime = 4, Baked = 2, Mixed = 1 };

	static bool IsLightBaked(Light light)
	{
		return light.lightmapBakeType == LightmapBakeType.Baked;
	}

	static void SetAudioPreload(AudioClip clip, bool preload, bool refreshImmediately)
	{
		if (clip != null)
		{
			string assetPath = AssetDatabase.GetAssetPath(clip);
			AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
			if (importer != null)
			{
				if (preload != importer.preloadAudioData)
				{
					importer.preloadAudioData = preload;

					AssetDatabase.ImportAsset(assetPath);
					if (refreshImmediately)
					{
						AssetDatabase.Refresh();
					}
				}
			}
		}
	}

	static void SetAudioLoadType(AudioClip clip, AudioClipLoadType loadType, bool refreshImmediately)
	{
		if (clip != null)
		{
			string assetPath = AssetDatabase.GetAssetPath(clip);
			AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
			if (importer != null)
			{
				if (loadType != importer.defaultSampleSettings.loadType)
				{
					AudioImporterSampleSettings settings = importer.defaultSampleSettings;
					settings.loadType = loadType;
					importer.defaultSampleSettings = settings;

					AssetDatabase.ImportAsset(assetPath);
					if (refreshImmediately)
					{
						AssetDatabase.Refresh();
					}
				}
			}
		}
	}

	public static void SetTextureUseMips(Texture texture, bool useMips, bool refreshImmediately)
	{
		if (texture != null)
		{
			string assetPath = AssetDatabase.GetAssetPath(texture);
			TextureImporter tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
			if (tImporter != null && tImporter.mipmapEnabled != useMips)
			{
				tImporter.mipmapEnabled = useMips;

				AssetDatabase.ImportAsset(assetPath);
				if (refreshImmediately)
				{
					AssetDatabase.Refresh();
				}
			}
		}
	}

	static T FindComponentInParents<T>(GameObject obj) where T : Component
	{
		T component = null;
		if (obj != null)
		{
			Transform parent = obj.transform.parent;
			if (parent != null)
			{
				do
				{
					component = parent.GetComponent(typeof(T)) as T;
					parent = parent.parent;
				} while (parent != null && component == null);
			}
		}
		return component;
	}
}

#endif
